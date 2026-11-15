#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.NPC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Saca un PNG por plano de un guion horneado, en edición y sin entrar en Play. Coloca a los actores
/// en su posición horneada, pone la cámara con `PoseDeCamara` y rinde con el pipeline (URP) a una
/// RenderTexture. No es un fotograma exacto: los actores van en su pose de reposo y no se lanzan VFX
/// ni partículas. Para juzgar encuadre, cabezas cortadas y oclusiones, sí. Ver INC-1230 y TDD § 33.4.
///
/// Al terminar deja la escena como estaba: posiciones, rotaciones y objetos activos.
internal static class PrevizDeGuion
{
    internal sealed class Fotograma
    {
        public int indice;
        public PlanoHorneado plano;
        public string ruta;
    }

    private struct Estado
    {
        public Vector3 posicion;
        public Quaternion rotacion;
        public bool activo;
    }

    public static List<Fotograma> Generar(GuionHorneado guion, out string error,
        int ancho = 640, int alto = 360)
    {
        error = null;
        var fotos = new List<Fotograma>();
        if (guion == null) { error = "No hay guion."; return fotos; }
        if (guion.planos == null || guion.planos.Count == 0) { error = "El guion no tiene planos."; return fotos; }

        string escenaNombre = EscenaDe(guion);
        if (string.IsNullOrEmpty(escenaNombre)) { error = "El guion no dice de qué escena es."; return fotos; }

        bool yaEstabaAbierta = SceneManager.GetSceneByName(escenaNombre).isLoaded;
        Scene escena = CapturaDeEscenario.AbrirEscena(escenaNombre);
        if (!escena.IsValid() || !escena.isLoaded)
        {
            error = $"No puedo abrir la escena '{escenaNombre}'.";
            return fotos;
        }

        var originales = new Dictionary<Transform, Estado>();
        var instanciados = new List<GameObject>();
        var uiOculta = new List<GameObject>();
        var actores = ResolverActores(guion, instanciados);
        OcultarUiDeEscena(escena, uiOculta);
        bool modoAnimacion = false;
        if (instanciados.Count > 0 && !AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            modoAnimacion = true;
        }
        var camaraGo = new GameObject("~PrevizDeGuion") { hideFlags = HideFlags.HideAndDontSave };
        var camara = camaraGo.AddComponent<Camera>();
        camaraGo.AddComponent<UniversalAdditionalCameraData>();
        camara.enabled = false;
        camara.clearFlags = CameraClearFlags.Skybox;
        camara.nearClipPlane = 0.05f;
        camara.farClipPlane = 900f;

        var rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var png = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "previz", guion.nombre ?? "guion");
        Directory.CreateDirectory(carpeta);

        try
        {
            for (int i = 0; i < guion.planos.Count; i++)
            {
                PlanoHorneado plano = guion.planos[i];
                float t = Mathf.Lerp(plano.t0, plano.t1, 0.5f);

                EditorUtility.DisplayProgressBar("Previz del guion",
                    $"Plano {i + 1}/{guion.planos.Count}", (float)i / guion.planos.Count);

                ColocarActores(guion, actores, t, originales);
                Physics.SyncTransforms();

                if (!guion.PoseDeCamara(t, out Vector3 posicion, out Quaternion rotacion, out float fov))
                {
                    Debug.LogWarning($"[Previz] El plano {i + 1} no tiene pose de cámara; se salta.");
                    continue;
                }

                camara.transform.SetPositionAndRotation(posicion, rotacion);
                camara.fieldOfView = fov;
                camara.targetTexture = rt;

                var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(camara, peticion))
                    RenderPipeline.SubmitRenderRequest(camara, peticion);
                else
                    camara.Render();

                RenderTexture anterior = RenderTexture.active;
                RenderTexture.active = rt;
                png.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
                png.Apply();
                RenderTexture.active = anterior;
                camara.targetTexture = null;

                string ruta = Path.Combine(carpeta, $"{i + 1:000}.png");
                File.WriteAllBytes(ruta, png.EncodeToPNG());
                fotos.Add(new Fotograma { indice = i, plano = plano, ruta = ruta });
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (modoAnimacion) AnimationMode.StopAnimationMode();

            foreach (var kv in originales)
            {
                if (kv.Key == null) continue;
                kv.Key.gameObject.SetActive(kv.Value.activo);
                kv.Key.SetPositionAndRotation(kv.Value.posicion, kv.Value.rotacion);
            }
            Physics.SyncTransforms();

            foreach (GameObject go in uiOculta)
                if (go != null) go.SetActive(true);
            foreach (GameObject go in instanciados)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);

            UnityEngine.Object.DestroyImmediate(camaraGo);
            UnityEngine.Object.DestroyImmediate(png);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            if (!yaEstabaAbierta && escena.IsValid() && escena.isLoaded)
                EditorSceneManager.CloseScene(escena, true);
        }

        return fotos;
    }

    private static string EscenaDe(GuionHorneado guion)
    {
        if (!string.IsNullOrEmpty(guion.escena)) return guion.escena;
        if (string.IsNullOrEmpty(guion.rutaDelTexto) || !File.Exists(guion.rutaDelTexto)) return null;

        try { return GuionTexto.Leer(File.ReadAllText(guion.rutaDelTexto)).escena; }
        catch { return null; }
    }

    private static Dictionary<string, SequenceActor> ResolverActores(GuionHorneado guion, List<GameObject> instanciados)
    {
        var actores = new Dictionary<string, SequenceActor>();
        if (guion.actores == null) return actores;

        foreach (ActorHorneado datos in guion.actores)
        {
            if (string.IsNullOrEmpty(datos?.id)) continue;

            SequenceActor actor = ResolverEnEscena(datos.id);
            if (actor?.Transform == null && !string.IsNullOrEmpty(datos.prefab))
                actor = InstanciarPrefab(datos, instanciados);

            if (actor?.Transform != null) actores[datos.id] = actor;
        }
        return actores;
    }

    private static SequenceActor ResolverEnEscena(string id)
    {
        if (id == SequenceActor.PlayerId || id == "Actor_Will" || id == "will")
            return SequenceActor.TryResolve(id, out SequenceActor jugador) ? jugador : null;

        foreach (NPCBehaviourManagerV2 npc in UnityEngine.Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsInactive.Include))
            if (npc != null && npc.PersistenceId == id)
                return SequenceActor.ForTransform(id, npc.transform);

        return null;
    }

    private static SequenceActor InstanciarPrefab(ActorHorneado datos, List<GameObject> instanciados)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(datos.prefab);
        if (prefab == null) return null;

        GameObject copia = UnityEngine.Object.Instantiate(prefab);
        copia.hideFlags = HideFlags.HideAndDontSave;
        foreach (MonoBehaviour comportamiento in copia.GetComponentsInChildren<MonoBehaviour>(true))
            comportamiento.enabled = false;
        foreach (Collider colisionador in copia.GetComponentsInChildren<Collider>(true))
            colisionador.enabled = false;

        instanciados.Add(copia);
        return SequenceActor.ForTransform(datos.id, copia.transform);
    }

    /// Tapa el HUD y los iconos de interacción de la escena mientras dura el render: no forman parte
    /// de la película y en el previz salen pegados al encuadre.
    private static void OcultarUiDeEscena(Scene escena, List<GameObject> apagados)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Canvas canvas in raiz.GetComponentsInChildren<Canvas>(true))
                Apagar(canvas.gameObject, apagados);
            foreach (BillboardUI billboard in raiz.GetComponentsInChildren<BillboardUI>(true))
                Apagar(billboard.gameObject, apagados);
        }
    }

    private static void Apagar(GameObject go, List<GameObject> apagados)
    {
        if (go == null || !go.activeSelf) return;
        go.SetActive(false);
        apagados.Add(go);
    }

    private static void ColocarActores(GuionHorneado guion, Dictionary<string, SequenceActor> actores,
        float t, Dictionary<Transform, Estado> originales)
    {
        if (guion.actores == null) return;

        foreach (ActorHorneado datos in guion.actores)
        {
            if (datos?.posiciones == null || datos.posiciones.Count == 0) continue;
            if (!actores.TryGetValue(datos.id, out SequenceActor actor) || actor.Transform == null) continue;

            Transform tr = actor.Transform;
            if (!originales.ContainsKey(tr))
                originales[tr] = new Estado
                {
                    posicion = tr.position,
                    rotacion = tr.rotation,
                    activo = tr.gameObject.activeSelf
                };

            if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            tr.position = Interpolar.Posicion(datos.posiciones, t);

            if (RotacionEn(datos, t, guion, actores, out Quaternion rotacion))
                tr.rotation = rotacion;

            bool visible = datos.visible == null || datos.visible.Count == 0
                           || datos.visible.Any(v => t >= v.desde && t < v.hasta);
            if (tr.gameObject.activeSelf != visible) tr.gameObject.SetActive(visible);

            PoseEn(datos, actor, t);
        }
    }

    /// Pone la pose base del actor (el bucle en el que está, o su idle) sobre la copia del prefab.
    /// Solo toca copias: una pose muestreada sobre un NPC de la escena habría que deshacerla, y el
    /// previz no puede dejar la escena tocada. No reproduce gestos ni poses de medio cuerpo: para
    /// juzgar el encuadre, la pose base es lo que importa.
    private static void PoseEn(ActorHorneado datos, SequenceActor actor, float t)
    {
        Transform tr = actor.Transform;
        if (tr == null || (tr.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0) return;

        var animador = tr.GetComponentInChildren<Animator>(true);
        if (animador == null) return;

        string estado = EstadoEn(datos, t, out float desde);
        AnimatorController controller = ResolveController(animador);
        AnimatorState animatorState = BuscarEstado(controller, estado);
        AnimationClip clip = animatorState != null ? ResolveClip(animatorState.motion) : null;
        if (clip == null) return;

        float tiempo = clip.isLooping
            ? Mathf.Repeat(t - desde, Mathf.Max(0.01f, clip.length))
            : Mathf.Clamp(t - desde, 0f, Mathf.Max(0f, clip.length));

        AnimationMode.BeginSampling();
        try { AnimationMode.SampleAnimationClip(animador.gameObject, clip, tiempo); }
        finally { AnimationMode.EndSampling(); }
    }

    private static string EstadoEn(ActorHorneado datos, float t, out float desde)
    {
        desde = 0f;
        string estado = null;
        if (datos.animacion == null) return null;

        foreach (OrdenDeAnimacion orden in datos.animacion)
        {
            if (orden.t > t + 1e-4f) break;
            if (orden.tipo == TipoDeAnimacion.Bucle) { estado = orden.estado; desde = orden.t; }
            else if (orden.tipo == TipoDeAnimacion.Reposo) { estado = null; desde = orden.t; }
        }
        return estado;
    }

    private static AnimatorController ResolveController(Animator animador)
    {
        RuntimeAnimatorController runtime = animador.runtimeAnimatorController;
        if (runtime is AnimatorOverrideController overrides) runtime = overrides.runtimeAnimatorController;
        return runtime as AnimatorController;
    }

    private static AnimatorState BuscarEstado(AnimatorController controller, string nombre)
    {
        if (controller == null) return null;

        AnimatorState encontrado = null;
        AnimatorState idle = null;
        foreach (AnimatorControllerLayer capa in controller.layers)
            if (capa.stateMachine != null)
                BuscarEstadoEn(capa.stateMachine, nombre, ref encontrado, ref idle);

        return encontrado ?? idle;
    }

    private static void BuscarEstadoEn(AnimatorStateMachine maquina, string nombre,
        ref AnimatorState encontrado, ref AnimatorState idle)
    {
        foreach (ChildAnimatorState hijo in maquina.states)
        {
            AnimatorState estado = hijo.state;
            if (estado == null) continue;
            if (!string.IsNullOrEmpty(nombre)
                && string.Equals(estado.name, nombre, StringComparison.OrdinalIgnoreCase))
            {
                encontrado = estado;
                return;
            }
            if (idle == null && estado.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                idle = estado;
        }

        foreach (ChildAnimatorStateMachine sub in maquina.stateMachines)
            if (sub.stateMachine != null)
                BuscarEstadoEn(sub.stateMachine, nombre, ref encontrado, ref idle);
    }

    private static AnimationClip ResolveClip(Motion motion)
    {
        if (motion is AnimationClip clip) return clip;
        if (motion is BlendTree arbol)
        {
            foreach (ChildMotion hijo in arbol.children)
            {
                AnimationClip resuelto = ResolveClip(hijo.motion);
                if (resuelto != null) return resuelto;
            }
        }
        return null;
    }

    private static bool RotacionEn(ActorHorneado datos, float t, GuionHorneado guion,
        Dictionary<string, SequenceActor> actores, out Quaternion rotacion)
    {
        rotacion = Quaternion.identity;
        if (datos.miradas == null || datos.miradas.Count == 0) return false;

        ClaveDeMirada clave = default;
        bool hay = false;
        foreach (ClaveDeMirada m in datos.miradas)
        {
            if (m.t > t + 1e-4f) break;
            clave = m;
            hay = true;
        }
        if (!hay) return false;

        if (clave.tipo == TipoDeMirada.Rumbo)
        {
            rotacion = Quaternion.Euler(0f, clave.rumbo, 0f);
            return true;
        }

        Vector3 punto = clave.tipo == TipoDeMirada.Actor
            ? PosicionDe(clave.actor, t, guion, actores)
            : clave.punto;

        Vector3 direccion = punto - Interpolar.Posicion(datos.posiciones, t);
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 1e-4f) return false;

        rotacion = Quaternion.LookRotation(direccion.normalized, Vector3.up);
        return true;
    }

    private static Vector3 PosicionDe(string actorId, float t, GuionHorneado guion,
        Dictionary<string, SequenceActor> actores)
    {
        if (guion.actores != null)
            foreach (ActorHorneado otro in guion.actores)
                if (otro?.id == actorId && otro.posiciones != null && otro.posiciones.Count > 0)
                    return Interpolar.Posicion(otro.posiciones, t);

        return actores.TryGetValue(actorId, out SequenceActor actor) && actor.Transform != null
            ? actor.Transform.position
            : Vector3.zero;
    }
}
#endif
