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
/// Al terminar deja la escena como estaba: posiciones, rotaciones, poses y objetos activos. La misma
/// <see cref="Sesion"/> sirve a la vista en vivo del Director.
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

        using Sesion sesion = Sesion.Abrir(guion, out error);
        if (sesion == null) return fotos;

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

                if (!sesion.Rendir(t, rt))
                {
                    Debug.LogWarning($"[Previz] Plano {i + 1}: {sesion.Error} Se salta.");
                    continue;
                }

                RenderTexture anterior = RenderTexture.active;
                RenderTexture.active = rt;
                png.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
                png.Apply();
                RenderTexture.active = anterior;

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
            UnityEngine.Object.DestroyImmediate(png);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        return fotos;
    }

    /// La escena de un guion abierta y lista para colocarla en cualquier segundo y rendirla. La
    /// usan las fotos del previz (un segundo por plano) y la vista en vivo del Director (cada
    /// fotograma mientras se reproduce o se arrastra la barra).
    ///
    /// Mientras está colocada, la escena está tocada: actores movidos y posados, la interfaz de la
    /// escena apagada, cargas encendidas. <see cref="Soltar"/> lo devuelve todo a como estaba sin
    /// cerrar la sesión, y se llama solo antes de guardar una escena, para que nunca se guarde
    /// tocada. <see cref="Dispose"/> suelta, destruye lo creado y cierra la escena si la abrió ella.
    internal sealed class Sesion : IDisposable
    {
        private readonly GuionHorneado _guion;
        private readonly Scene _escena;
        private readonly bool _abrioLaEscena;
        private readonly Dictionary<Transform, Estado> _originales = new();
        private readonly List<GameObject> _instanciados = new();
        private readonly List<GameObject> _activados = new();
        private readonly List<GameObject> _uiOculta = new();
        private readonly Dictionary<string, SequenceActor> _actores;
        private readonly List<CargaEnEscena> _cargas;
        private GameObject _camaraGo;
        private Camera _camara;
        private bool _colocada;
        private bool _modoAnimacionPropio;
        private bool _cerrada;

        public string Error { get; private set; } = string.Empty;

        private Sesion(GuionHorneado guion, Scene escena, bool abrioLaEscena)
        {
            _guion = guion;
            _escena = escena;
            _abrioLaEscena = abrioLaEscena;
            _actores = ResolverActores(guion, _instanciados);
            _cargas = ResolverCargas(guion, _actores, escena);
            foreach (GameObject copia in _instanciados)
                if (copia != null) copia.SetActive(false);

            _camaraGo = new GameObject("~PrevizDeGuion") { hideFlags = HideFlags.HideAndDontSave };
            _camara = _camaraGo.AddComponent<Camera>();
            _camaraGo.AddComponent<UniversalAdditionalCameraData>();
            _camara.enabled = false;
            _camara.clearFlags = CameraClearFlags.Skybox;
            _camara.nearClipPlane = 0.05f;
            _camara.farClipPlane = 900f;

            EditorSceneManager.sceneSaving += AlGuardarUnaEscena;
        }

        /// Abre la escena del guion (si no lo estaba) y prepara el reparto. Null si no se puede.
        public static Sesion Abrir(GuionHorneado guion, out string error)
        {
            error = null;
            if (guion == null) { error = "No hay guion."; return null; }

            string escenaNombre = EscenaDe(guion);
            if (string.IsNullOrEmpty(escenaNombre)) { error = "El guion no dice de qué escena es."; return null; }

            bool yaEstabaAbierta = SceneManager.GetSceneByName(escenaNombre).isLoaded;
            Scene escena = CapturaDeEscenario.AbrirEscena(escenaNombre);
            if (!escena.IsValid() || !escena.isLoaded)
            {
                error = $"No puedo abrir la escena '{escenaNombre}'.";
                return null;
            }
            return new Sesion(guion, escena, !yaEstabaAbierta);
        }

        /// Coloca la escena en `t` y la rinde en `destino`.
        public bool Rendir(float t, RenderTexture destino)
        {
            Error = string.Empty;
            if (_cerrada) { Error = "La vista está cerrada."; return false; }
            if (destino == null) { Error = "No hay textura donde rendir."; return false; }
            if (!_escena.IsValid() || !_escena.isLoaded) { Error = "La escena del guion ya no está abierta."; return false; }

            Colocar(t);

            if (!_guion.PoseDeCamara(t, out Vector3 posicion, out Quaternion rotacion, out float fov))
            {
                Error = "Este momento no tiene plano de cámara.";
                return false;
            }

            _camara.transform.SetPositionAndRotation(posicion, rotacion);
            _camara.fieldOfView = fov;
            _camara.aspect = destino.width / (float)Mathf.Max(1, destino.height);
            _camara.targetTexture = destino;
            try
            {
                var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = destino };
                if (RenderPipeline.SupportsRenderRequest(_camara, peticion))
                    RenderPipeline.SubmitRenderRequest(_camara, peticion);
                else
                    _camara.Render();
            }
            finally
            {
                _camara.targetTexture = null;
            }
            return true;
        }

        /// Pone actores, poses y cargas en `t`. Prepara la escena la primera vez.
        public void Colocar(float t)
        {
            if (!_colocada)
            {
                OcultarUiDeEscena(_escena, _uiOculta);
                foreach (GameObject copia in _instanciados)
                    if (copia != null) copia.SetActive(true);
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    _modoAnimacionPropio = true;
                }
                _colocada = true;
            }

            ColocarActores(_guion, _actores, t, _originales);
            ColocarCargas(_cargas, t, _originales, _activados);
            Physics.SyncTransforms();
        }

        /// Deja la escena como estaba, sin cerrar la sesión. El siguiente Rendir la vuelve a colocar.
        public void Soltar()
        {
            if (!_colocada) return;
            _colocada = false;

            // Primero las poses: al parar el modo de animación se deshace lo muestreado.
            if (_modoAnimacionPropio && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            _modoAnimacionPropio = false;

            foreach (var kv in _originales)
            {
                if (kv.Key == null) continue;
                kv.Key.gameObject.SetActive(kv.Value.activo);
                kv.Key.SetPositionAndRotation(kv.Value.posicion, kv.Value.rotacion);
            }
            _originales.Clear();
            Physics.SyncTransforms();

            for (int i = _activados.Count - 1; i >= 0; i--)
                if (_activados[i] != null) _activados[i].SetActive(false);
            _activados.Clear();

            foreach (GameObject go in _uiOculta)
                if (go != null) go.SetActive(true);
            _uiOculta.Clear();

            foreach (GameObject copia in _instanciados)
                if (copia != null) copia.SetActive(false);
        }

        public void Dispose()
        {
            if (_cerrada) return;
            _cerrada = true;
            EditorSceneManager.sceneSaving -= AlGuardarUnaEscena;

            Soltar();

            foreach (GameObject go in _instanciados)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _instanciados.Clear();

            if (_camaraGo != null) UnityEngine.Object.DestroyImmediate(_camaraGo);
            _camaraGo = null;
            _camara = null;

            // Se cierra solo si la abrió la sesión y nadie la ha cambiado mientras: cerrar descarta
            // los cambios sin preguntar. Si está cambiada, se queda abierta para que se guarde.
            if (_abrioLaEscena && _escena.IsValid() && _escena.isLoaded && !_escena.isDirty)
                EditorSceneManager.CloseScene(_escena, true);
        }

        private void AlGuardarUnaEscena(Scene escena, string ruta) => Soltar();
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

    private sealed class CargaEnEscena
    {
        public CargaArrastrada carga;
        public ActorHorneado datos;
        public SequenceActor actor;
    }

    /// Las cargas que arrastra alguien del reparto (el carro de los contrabandistas): se colocan
    /// detrás de su actor en cada foto, con la distancia y el giro que llevan configurados. Fuera de
    /// Play sus componentes no se mueven solos, así que sin esto el carro no sale en el previz.
    private static List<CargaEnEscena> ResolverCargas(GuionHorneado guion, Dictionary<string, SequenceActor> actores, Scene escena)
    {
        var cargas = new List<CargaEnEscena>();
        if (guion.actores == null) return cargas;

        foreach (CargaArrastrada carga in UnityEngine.Object.FindObjectsByType<CargaArrastrada>(FindObjectsInactive.Include))
        {
            if (carga == null || string.IsNullOrEmpty(carga.ActorId)) continue;
            if (carga.gameObject.scene != escena) continue;

            ActorHorneado datos = null;
            foreach (ActorHorneado actor in guion.actores)
                if (actor != null && actor.id == carga.ActorId) { datos = actor; break; }
            if (datos == null) continue;
            if (!actores.TryGetValue(datos.id, out SequenceActor deGuion) || deGuion?.Transform == null) continue;

            cargas.Add(new CargaEnEscena { carga = carga, datos = datos, actor = deGuion });
        }
        return cargas;
    }

    private static void ColocarCargas(List<CargaEnEscena> cargas, float t,
        Dictionary<Transform, Estado> originales, List<GameObject> activados)
    {
        foreach (CargaEnEscena enEscena in cargas)
        {
            Transform tr = enEscena.carga.transform;
            if (!originales.ContainsKey(tr))
                originales[tr] = new Estado
                {
                    posicion = tr.position,
                    rotacion = tr.rotation,
                    activo = tr.gameObject.activeSelf
                };

            ActivarCadena(tr, activados);
            enEscena.carga.PoseDetrasDe(enEscena.actor.Transform, DireccionDe(enEscena.datos, t),
                out Vector3 posicion, out Quaternion rotacion);
            tr.SetPositionAndRotation(posicion, rotacion);
        }
    }

    /// La carga vive dentro de un grupo que solo se enciende en su tramo (p. ej. «… (tramo)»). Para
    /// la foto hay que encender la cadena entera y devolverla como estaba al acabar.
    private static void ActivarCadena(Transform tr, List<GameObject> activados)
    {
        var cadena = new List<Transform>();
        for (Transform t = tr; t != null; t = t.parent) cadena.Add(t);

        for (int i = cadena.Count - 1; i >= 0; i--)
        {
            GameObject go = cadena[i].gameObject;
            if (go.activeSelf) continue;
            go.SetActive(true);
            if (!activados.Contains(go)) activados.Add(go);
        }
    }

    /// La dirección en la que va el personaje en ese segundo: la de su camino, no la de su mirada
    /// (mira a otro y el carro seguiría detrás de él, donde iba).
    private static Vector3 DireccionDe(ActorHorneado datos, float t)
    {
        if (datos?.posiciones == null || datos.posiciones.Count == 0) return Vector3.forward;

        Vector3 ahora = Interpolar.Posicion(datos.posiciones, t);
        Vector3 antes = Interpolar.Posicion(datos.posiciones, Mathf.Max(0f, t - 0.25f));
        Vector3 direccion = ahora - antes;
        if (direccion.sqrMagnitude > 0.0001f) return direccion.normalized;

        direccion = ahora - Interpolar.Posicion(datos.posiciones, 0f);
        return direccion.sqrMagnitude > 0.0001f ? direccion.normalized : Vector3.forward;
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

    /// Pone la pose base del actor (el bucle en el que está, o su idle), en la copia del prefab o en
    /// el NPC de la escena. Solo con el modo de animación del Editor en marcha: lo muestreado se
    /// deshace al pararlo (<see cref="Sesion.Soltar"/>), así que la escena no queda tocada. No
    /// reproduce gestos ni poses de medio cuerpo: para juzgar el encuadre, la pose base es lo que importa.
    private static void PoseEn(ActorHorneado datos, SequenceActor actor, float t)
    {
        Transform tr = actor.Transform;
        if (tr == null || !AnimationMode.InAnimationMode()) return;

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
