#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Mide, sin tocar nada, todas las animaciones humanoides del juego y de los packs que ya
/// tenemos: acerca la mano y el antebrazo a la cabeza y al torso, y mira si el cuello se
/// fuerza. Escribe un informe en docs/ y un CSV para poder ordenar y filtrar.
///
/// La medida se hace sobre el personaje real, no sobre el hueso aislado: la cabeza de estos
/// personajes ocupa casi la mitad del cuerpo, asÃ­ que una pose que en el pack original es
/// correcta aquÃ­ puede meter la mano dentro de la cara. Ese es el problema que buscamos.
/// </summary>
public static class AuditorDeAnimaciones
{
    private const string Menu = "El Sendero/Personajes/Animaciones/";
    private const string InformeMd = "docs/auditoria-animaciones.md";
    private const string InformeCsv = "docs/auditoria-animaciones.csv";
    private const float FpsUsadas = 30f;
    private const float FpsSinUsar = 15f;

    private static readonly string[] Controladores =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animator/ArrowsCTRL.controller",
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animator/BowsCTRL.controller",
    };

    private static readonly string[] CarpetasCatalogo =
    {
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation",
        "Assets/Plugins/Kevin Iglesias/Human Animations/Animations",
        "Assets/Art/Animations/Mixamo",
    };

    private static readonly string[] PrefabsUsados =
    {
        "Assets/_NPCs/Eldran.prefab",
        "Assets/Prefabs/_WILL.prefab",
        "Assets/Prefabs/_LIAM.prefab",
        "Assets/Prefabs/_ESTELA.prefab",
    };

    private const string PrefabSinUsar = "Assets/_NPCs/Eldran.prefab";

    [MenuItem(Menu + "Auditar todas (humanoide)", priority = 100)]
    public static void AuditarTodas()
    {
        Ejecutar(false);
    }

    [MenuItem(Menu + "Auditar solo las usadas", priority = 101)]
    public static void AuditarSoloUsadas()
    {
        Ejecutar(true);
    }

    private static void Ejecutar(bool soloUsadas)
    {
        DateTime inicio = DateTime.Now;
        string raiz = Path.GetDirectoryName(Application.dataPath);

        try
        {
            Debug.Log($"[Auditor] Inicio. Proyecto: {raiz}");

            Dictionary<string, AnimationClip> clipsUsados;
            Dictionary<string, List<UsoDeClip>> usos = RecolectarUsos(out clipsUsados);
            Dictionary<string, ResultadoClip> resultados = RecolectarClips(usos, clipsUsados, soloUsadas);

            Debug.Log($"[Auditor] Clips usados por los controllers: {clipsUsados.Count}. " +
                      $"Clips a analizar: {resultados.Count}.");

            foreach (string prefab in PrefabsUsados)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null)
                {
                    Debug.LogWarning($"[Auditor] No existe el prefab {prefab}; lo salto.");
                    continue;
                }

                try
                {
                    using (RigDePrueba rig = new RigDePrueba(prefab))
                    {
                        MedirConRig(rig, resultados, r => r.Usado);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Auditor] No se pudo medir con {prefab}: {ex.Message}");
                }
            }

            if (!soloUsadas)
            {
                try
                {
                    using (RigDePrueba rig = new RigDePrueba(PrefabSinUsar))
                    {
                        MedirConRig(rig, resultados, r => !r.Usado);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Auditor] No se pudo medir el catÃ¡logo con {PrefabSinUsar}: {ex.Message}");
                }
            }

            EscribirInformes(resultados, inicio, soloUsadas);

            List<ResultadoClip> todos = resultados.Values.Where(r => r.Humanoide).ToList();
            int conProblema = todos.Count(r => r.Severidad != "Limpio" && r.Severidad != "Sin datos");
            Debug.Log($"[Auditor] Fin. Clips humanoides: {todos.Count}; con algÃºn problema: {conProblema}. " +
                      $"Informe: {InformeMd}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Auditor] Error: {ex}");
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void MedirConRig(
        RigDePrueba rig,
        Dictionary<string, ResultadoClip> resultados,
        Func<ResultadoClip, bool> filtro)
    {
        List<ResultadoClip> lista = resultados.Values
            .Where(filtro)
            .Where(r => r.Humanoide)
            .ToList();
        Debug.Log($"[Auditor] Midiendo {lista.Count} clips con {rig.Nombre}.");

        int hechos = 0;
        AnimationMode.StartAnimationMode();
        try
        {
            foreach (ResultadoClip resultado in lista)
            {
                EditorUtility.DisplayProgressBar(
                    "AuditorÃ­a de animaciones",
                    $"{rig.Nombre}: {resultado.Clip.name}",
                    lista.Count > 0 ? (float)hechos / lista.Count : 0f);

                MedicionPorRig medicion = MedirClip(rig, resultado.Clip,
                    resultado.Usado ? FpsUsadas : FpsSinUsar);
                resultado.Mediciones.Add(medicion);

                hechos++;
                if (hechos % 25 == 0)
                    Debug.Log($"[Auditor] {rig.Nombre}: {hechos}/{lista.Count} ({resultado.Clip.name})");
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }
    }

    private static MedicionPorRig MedirClip(RigDePrueba rig, AnimationClip clip, float fps)
    {
        MedicionPorRig medicion = new MedicionPorRig
        {
            Rig = rig.Nombre,
            Duracion = clip.length,
        };

        int pasos = Mathf.Clamp(Mathf.CeilToInt(clip.length * fps), 8, 240);

        try
        {
            for (int i = 0; i <= pasos; i++)
            {
                float t = clip.length * i / pasos;

                AnimationMode.BeginSampling();
                try
                {
                    AnimationMode.SampleAnimationClip(rig.Go, clip, t);
                }
                finally
                {
                    AnimationMode.EndSampling();
                }

                MedicionFrame frame = rig.Cuerpo.Medir();

                if (frame.Cabeza > medicion.MaxCabeza)
                {
                    medicion.MaxCabeza = frame.Cabeza;
                    medicion.TiempoCabeza = t;
                    medicion.ParteCabeza = frame.ParteCabeza;
                }

                if (frame.Cabeza > 0.005f)
                    medicion.FramesCabeza++;

                if (frame.Torso > medicion.MaxTorso)
                {
                    medicion.MaxTorso = frame.Torso;
                    medicion.TiempoTorso = t;
                    medicion.ParteTorso = frame.ParteTorso;
                }

                if (frame.Torso > 0.02f)
                    medicion.FramesTorso++;

                if (Mathf.Abs(frame.Cuello) > Mathf.Abs(medicion.MaxCuello))
                {
                    medicion.MaxCuello = frame.Cuello;
                    medicion.TiempoCuello = t;
                }

                if (Mathf.Abs(frame.Cuello) >= 55f)
                    medicion.FramesCuello++;
            }
        }
        catch (Exception ex)
        {
            medicion.Error = true;
            medicion.MensajeError = ex.Message;
            Debug.LogWarning($"[Auditor] No se pudo muestrear '{clip.name}' en {rig.Nombre}: {ex.Message}");
        }

        return medicion;
    }

    // ---------------------------------------------------------------------------------------
    // RecolecciÃ³n de clips y estados
    // ---------------------------------------------------------------------------------------

    private static Dictionary<string, List<UsoDeClip>> RecolectarUsos(
        out Dictionary<string, AnimationClip> clipsUsados)
    {
        Dictionary<string, List<UsoDeClip>> usos = new Dictionary<string, List<UsoDeClip>>();
        clipsUsados = new Dictionary<string, AnimationClip>();

        foreach (string rutaControlador in Controladores)
        {
            AnimatorController controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(rutaControlador);
            if (controlador == null)
            {
                Debug.LogWarning($"[Auditor] No encuentro el controller {rutaControlador}.");
                continue;
            }

            foreach (AnimatorControllerLayer capa in controlador.layers)
            {
                HashSet<AnimatorStateMachine> visitadas = new HashSet<AnimatorStateMachine>();
                RecorrerEstado(capa.stateMachine, controlador.name, capa.name, usos, clipsUsados, visitadas);
            }
        }

        return usos;
    }

    private static void RecorrerEstado(
        AnimatorStateMachine maquina,
        string controlador,
        string capa,
        Dictionary<string, List<UsoDeClip>> usos,
        Dictionary<string, AnimationClip> clipsUsados,
        HashSet<AnimatorStateMachine> visitadas)
    {
        if (maquina == null || !visitadas.Add(maquina))
            return;

        foreach (ChildAnimatorState hijo in maquina.states)
        {
            AnimatorState estado = hijo.state;
            if (estado == null)
                continue;

            AnadirMotion(estado.motion, controlador, capa, estado.name, usos, clipsUsados);
        }

        foreach (ChildAnimatorStateMachine sub in maquina.stateMachines)
            RecorrerEstado(sub.stateMachine, controlador, capa, usos, clipsUsados, visitadas);
    }

    private static void AnadirMotion(
        Motion motion,
        string controlador,
        string capa,
        string estado,
        Dictionary<string, List<UsoDeClip>> usos,
        Dictionary<string, AnimationClip> clipsUsados)
    {
        if (motion == null)
            return;

        if (motion is AnimationClip clip)
        {
            if (clip.empty || clip.length <= 0f || clip.name.StartsWith("__preview__"))
                return;

            string clave = Clave(clip);
            if (!usos.TryGetValue(clave, out List<UsoDeClip> lista))
            {
                lista = new List<UsoDeClip>();
                usos[clave] = lista;
                clipsUsados[clave] = clip;
            }

            bool repetido = lista.Any(u =>
                u.Controlador == controlador && u.Capa == capa && u.Estado == estado);
            if (!repetido)
            {
                lista.Add(new UsoDeClip
                {
                    Controlador = controlador,
                    Capa = capa,
                    Estado = estado,
                });
            }
            return;
        }

        if (motion is BlendTree arbol)
        {
            foreach (ChildMotion hijo in arbol.children)
                AnadirMotion(hijo.motion, controlador, capa, estado, usos, clipsUsados);
        }
    }

    private static Dictionary<string, ResultadoClip> RecolectarClips(
        Dictionary<string, List<UsoDeClip>> usos,
        Dictionary<string, AnimationClip> clipsUsados,
        bool soloUsadas)
    {
        Dictionary<string, ResultadoClip> resultados = new Dictionary<string, ResultadoClip>();

        foreach (KeyValuePair<string, AnimationClip> par in clipsUsados)
        {
            resultados[par.Key] = new ResultadoClip
            {
                Clip = par.Value,
                Ruta = AssetDatabase.GetAssetPath(par.Value),
                Usado = true,
                Humanoide = par.Value.humanMotion,
                Usos = usos[par.Key],
            };
        }

        if (!soloUsadas)
        {
            foreach (string carpeta in CarpetasCatalogo)
            {
                if (!AssetDatabase.IsValidFolder(carpeta))
                {
                    Debug.LogWarning($"[Auditor] No existe la carpeta {carpeta}.");
                    continue;
                }

                string[] modelos = AssetDatabase.FindAssets("t:Model", new[] { carpeta });
                Debug.Log($"[Auditor] {carpeta}: {modelos.Length} modelos.");

                foreach (string guid in modelos)
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ruta);
                    foreach (UnityEngine.Object asset in assets)
                    {
                        if (asset is AnimationClip clip)
                            AnadirClipCatalogo(resultados, clip, ruta);
                    }
                }

                string[] animaciones = AssetDatabase.FindAssets("t:AnimationClip", new[] { carpeta });
                foreach (string guid in animaciones)
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
                    if (clip != null)
                        AnadirClipCatalogo(resultados, clip, ruta);
                }
            }
        }

        return resultados;
    }

    private static void AnadirClipCatalogo(
        Dictionary<string, ResultadoClip> resultados, AnimationClip clip, string ruta)
    {
        if (clip.empty || clip.length <= 0f || clip.name.StartsWith("__preview__"))
            return;

        string clave = Clave(clip);
        if (resultados.ContainsKey(clave))
            return;

        resultados[clave] = new ResultadoClip
        {
            Clip = clip,
            Ruta = ruta,
            Usado = false,
            Humanoide = clip.humanMotion,
        };
    }

    private static string Clave(AnimationClip clip)
    {
        return AssetDatabase.GetAssetPath(clip) + "|" + clip.name;
    }

    // ---------------------------------------------------------------------------------------
    // Rig y medida
    // ---------------------------------------------------------------------------------------

    public sealed class RigDePrueba : IDisposable
    {
        public readonly string Nombre;
        public readonly GameObject Go;
        public readonly Animator Animator;
        public readonly CuerpoDePrueba Cuerpo;

        public RigDePrueba(string rutaPrefab)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            if (prefab == null)
                throw new InvalidOperationException($"No encuentro el prefab {rutaPrefab}.");

            Nombre = prefab.name;
            Go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Go.SetActive(false);

            foreach (MonoBehaviour mb in Go.GetComponentsInChildren<MonoBehaviour>(true))
                mb.enabled = false;
            foreach (Camera camara in Go.GetComponentsInChildren<Camera>(true))
                camara.enabled = false;
            foreach (Light luz in Go.GetComponentsInChildren<Light>(true))
                luz.enabled = false;
            foreach (AudioSource audio in Go.GetComponentsInChildren<AudioSource>(true))
                audio.enabled = false;

            Go.transform.position = Vector3.zero;
            Go.transform.rotation = Quaternion.identity;
            Go.SetActive(true);

            Animator = Go.GetComponentInChildren<Animator>(true);
            if (Animator == null || !Animator.isHuman || Animator.avatar == null)
                throw new InvalidOperationException($"El prefab {rutaPrefab} no tiene un Animator humanoide vÃ¡lido.");

            Cuerpo = new CuerpoDePrueba(Go, Animator);
            Debug.Log($"[Auditor] Rig {Nombre}: altura={Cuerpo.Altura:0.###} m, " +
                      $"extents cabeza=({Cuerpo.ExtentsCabeza.x:0.##}, {Cuerpo.ExtentsCabeza.y:0.##}, {Cuerpo.ExtentsCabeza.z:0.##}) m, " +
                      $"radio torso={Cuerpo.RadioTorso:0.###} m.");
        }

        public void Dispose()
        {
            if (Go != null)
                UnityEngine.Object.DestroyImmediate(Go);
        }
    }

    public sealed class CuerpoDePrueba
    {
        private readonly Transform cabeza;
        private readonly Transform caderas;
        private readonly Transform pecho;
        private readonly Transform[] manos = new Transform[2];
        private readonly Transform[] antebrazos = new Transform[2];
        private readonly float anguloNeutro;
        private readonly Vector3 centroCabezaLocal;

        public readonly float Altura;
        public readonly Vector3 ExtentsCabeza;
        public readonly float RadioTorso;
        public readonly float RadioMano;
        public readonly float RadioAntebrazo;

        public float RadioCabezaEfectivo => Mathf.Max(ExtentsCabeza.x, Mathf.Max(ExtentsCabeza.y, ExtentsCabeza.z));
        public Transform Cabeza => cabeza;
        public Transform Caderas => caderas;
        public Transform Pecho => pecho;
        public float AnguloNeutro => anguloNeutro;
        public Transform Mano(int lado) => manos[lado];
        public Transform Antebrazo(int lado) => antebrazos[lado];
        public Vector3 CentroCabeza() => cabeza.TransformPoint(centroCabezaLocal);
        public float AnguloCuelloActual() => AnguloCuello();

        public CuerpoDePrueba(GameObject go, Animator animator)
        {
            cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
            caderas = animator.GetBoneTransform(HumanBodyBones.Hips);
            pecho = animator.GetBoneTransform(HumanBodyBones.UpperChest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Chest);

            if (cabeza == null || caderas == null || pecho == null)
                throw new InvalidOperationException("Faltan huesos base (Head/Hips/Chest) en el rig.");

            manos[0] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            manos[1] = animator.GetBoneTransform(HumanBodyBones.RightHand);
            antebrazos[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            antebrazos[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);

            if (manos[0] == null || manos[1] == null || antebrazos[0] == null || antebrazos[1] == null)
                throw new InvalidOperationException("Faltan huesos de brazo en el rig.");

            Altura = CalcularAltura(go, animator);
            ExtentsCabeza = CalcularExtentsCabeza(go, cabeza, Altura, out centroCabezaLocal);
            RadioTorso = Mathf.Clamp(Altura * 0.09f, 0.10f, 0.20f);
            RadioMano = Altura * 0.055f;
            RadioAntebrazo = Altura * 0.032f;
            anguloNeutro = AnguloCuello();
        }

        /// <summary>
        /// Penetración de una esfera (mano, codo) en el elipsoide de la cabeza. Devuelve 0 si
        /// está fuera y la profundidad en metros si está dentro. El elipsoide va en el espacio
        /// local del hueso de la cabeza, para que acompañe al giro de la cabeza.
        /// </summary>
        public float PenetracionCabeza(Vector3 punto, float radio)
        {
            Vector3 local = cabeza.InverseTransformPoint(punto) - centroCabezaLocal;
            Vector3 e = new Vector3(
                Mathf.Max(0.01f, ExtentsCabeza.x - radio),
                Mathf.Max(0.01f, ExtentsCabeza.y - radio),
                Mathf.Max(0.01f, ExtentsCabeza.z - radio));
            float norma = Mathf.Sqrt(
                local.x / e.x * (local.x / e.x) +
                local.y / e.y * (local.y / e.y) +
                local.z / e.z * (local.z / e.z));
            if (norma >= 1f)
                return 0f;
            return (1f - norma) * Mathf.Min(e.x, Mathf.Min(e.y, e.z));
        }

        /// <summary>
        /// Igual que PenetracionCabeza, pero para el antebrazo: prueba varios puntos del
        /// segmento y se queda con el peor.
        /// </summary>
        public float PenetracionCabezaSegmento(Vector3 a, Vector3 b, float radio)
        {
            const int pasos = 6;
            float peor = 0f;
            for (int i = 0; i <= pasos; i++)
            {
                Vector3 punto = Vector3.Lerp(a, b, (float)i / pasos);
                float d = PenetracionCabeza(punto, radio);
                if (d > peor)
                    peor = d;
            }
            return peor;
        }

        public MedicionFrame Medir()
        {
            Vector3 cadera = caderas.position;
            Vector3 pechoMundo = pecho.position;

            float maxCabeza = 0f;
            string parteCabeza = "";
            float maxTorso = 0f;
            string parteTorso = "";

            for (int i = 0; i < 2; i++)
            {
                Transform mano = manos[i];
                Transform antebrazo = antebrazos[i];
                if (mano == null || antebrazo == null)
                    continue;

                Vector3 puntoMano = mano.position;
                Vector3 puntoCodo = antebrazo.position;

                float dManoCabeza = PenetracionCabeza(puntoMano, RadioMano);
                if (dManoCabeza > maxCabeza)
                {
                    maxCabeza = dManoCabeza;
                    parteCabeza = i == 0 ? "mano izquierda" : "mano derecha";
                }

                float dAntebrazoCabeza = PenetracionCabezaSegmento(puntoCodo, puntoMano, RadioAntebrazo);
                if (dAntebrazoCabeza > maxCabeza)
                {
                    maxCabeza = dAntebrazoCabeza;
                    parteCabeza = i == 0 ? "antebrazo izquierdo" : "antebrazo derecho";
                }

                float dCodoCabeza = PenetracionCabeza(puntoCodo, RadioAntebrazo);
                if (dCodoCabeza > maxCabeza)
                {
                    maxCabeza = dCodoCabeza;
                    parteCabeza = i == 0 ? "codo izquierdo" : "codo derecho";
                }

                // Solo miramos el torso por encima de la cintura: las manos apoyadas en la
                // cadera son una pose normal, no un error.
                if (puntoMano.y > cadera.y + Altura * 0.10f)
                {
                    float dManoTorso = RadioMano + RadioTorso
                        - DistanciaPuntoSegmento(puntoMano, cadera, pechoMundo);
                    if (dManoTorso > maxTorso)
                    {
                        maxTorso = dManoTorso;
                        parteTorso = i == 0 ? "mano izquierda" : "mano derecha";
                    }
                }

                if (puntoCodo.y > cadera.y + Altura * 0.10f || puntoMano.y > cadera.y + Altura * 0.10f)
                {
                    float dAntebrazoTorso = RadioAntebrazo + RadioTorso
                        - DistanciaSegmentoSegmento(puntoCodo, puntoMano, cadera, pechoMundo);
                    if (dAntebrazoTorso > maxTorso)
                    {
                        maxTorso = dAntebrazoTorso;
                        parteTorso = i == 0 ? "antebrazo izquierdo" : "antebrazo derecho";
                    }
                }
            }

            float cuello = Mathf.DeltaAngle(anguloNeutro, AnguloCuello());

            return new MedicionFrame
            {
                Cabeza = Mathf.Max(0f, maxCabeza),
                Torso = Mathf.Max(0f, maxTorso),
                Cuello = cuello,
                ParteCabeza = parteCabeza,
                ParteTorso = parteTorso,
            };
        }

        public float AnguloCuello()
        {
            Vector3 derecha = caderas.right;
            if (derecha.sqrMagnitude < 0.0001f)
                derecha = Vector3.right;
            return Vector3.SignedAngle(caderas.forward, cabeza.forward, derecha);
        }

        public static float CalcularAltura(GameObject go, Animator animator)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;
                Bounds b = renderer.bounds;
                if (b.size.y <= 0f)
                    continue;
                minY = Mathf.Min(minY, b.min.y);
                maxY = Mathf.Max(maxY, b.max.y);
            }

            if (maxY - minY > 0.1f)
                return maxY - minY;

            Transform cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform pieIzq = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform pieDer = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float arriba = cabeza != null ? cabeza.position.y + 0.2f : 2f;
            float abajo = Mathf.Min(
                pieIzq != null ? pieIzq.position.y : 0f,
                pieDer != null ? pieDer.position.y : 0f);
            return Mathf.Max(0.2f, arriba - abajo);
        }

        private static Vector3 CalcularExtentsCabeza(
            GameObject go, Transform cabeza, float altura, out Vector3 centroLocal)
        {
            centroLocal = Vector3.zero;
            if (cabeza == null)
                return new Vector3(0.15f, 0.15f, 0.15f);

            List<Bounds> candidatos = new List<Bounds>();
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                bool bajoCabeza = renderer.transform == cabeza || renderer.transform.IsChildOf(cabeza);
                bool porNombre = Contiene(renderer.name, "head") || Contiene(renderer.name, "cabeza")
                    || Contiene(renderer.name, "face") || Contiene(renderer.name, "cara")
                    || Contiene(renderer.name, "hair") || Contiene(renderer.name, "pelo");
                bool porRoot = renderer is SkinnedMeshRenderer smr && smr.rootBone != null
                    && (smr.rootBone == cabeza || smr.rootBone.IsChildOf(cabeza));

                if (!bajoCabeza && !porNombre && !porRoot)
                    continue;

                // Los accesorios que salen de la cabeza (cintas, sombreros) no son la cabeza:
                // si entran, inflarían el volumen y darían falsos positivos en la cara.
                if (Contiene(renderer.name, "ribbon") || Contiene(renderer.name, "cinta")
                    || Contiene(renderer.name, "hat") || Contiene(renderer.name, "sombrero")
                    || Contiene(renderer.name, "gorro"))
                    continue;

                Bounds b = renderer.bounds;
                if (b.size.y <= 0f)
                    continue;

                // Si el renderer abarca casi todo el cuerpo, no es la cabeza: puede ser una
                // malla Ãºnica con rootBone en la cadera. Mejor no usarla como referencia.
                if (b.size.y > altura * 0.65f || b.size.x > altura * 0.65f || b.size.z > altura * 0.65f)
                    continue;

                candidatos.Add(b);
            }

            if (candidatos.Count == 0)
            {
                Vector3 centroMundo = cabeza.position + cabeza.up * (altura * 0.14f);
                centroLocal = cabeza.InverseTransformPoint(centroMundo);
                return new Vector3(altura * 0.16f, altura * 0.18f, altura * 0.16f);
            }

            Bounds union = candidatos[0];
            for (int i = 1; i < candidatos.Count; i++)
                union.Encapsulate(candidatos[i]);

            centroLocal = cabeza.InverseTransformPoint(union.center);

            // Los extents del union están en ejes del mundo; los pasamos al espacio local de
            // la cabeza y tomamos el valor absoluto de cada eje. Queda un poco conservador,
            // que es justo lo que queremos.
            Vector3 ext = cabeza.InverseTransformVector(union.extents);
            ext = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z)) * 0.92f;
            ext = new Vector3(
                Mathf.Clamp(ext.x, altura * 0.08f, altura * 0.35f),
                Mathf.Clamp(ext.y, altura * 0.08f, altura * 0.35f),
                Mathf.Clamp(ext.z, altura * 0.08f, altura * 0.35f));
            return ext;
        }

        public static bool Contiene(string texto, string buscado)
        {
            return texto.IndexOf(buscado, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static float DistanciaPuntoSegmento(Vector3 punto, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float largo = ab.sqrMagnitude;
            if (largo < 1e-8f)
                return Vector3.Distance(punto, a);

            float t = Vector3.Dot(punto - a, ab) / largo;
            t = Mathf.Clamp01(t);
            return Vector3.Distance(punto, a + ab * t);
        }

        public static float DistanciaSegmentoSegmento(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;

            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float s;
            float t;

            if (a < 1e-8f && e < 1e-8f)
                return Vector3.Distance(p1, p2);

            if (a < 1e-8f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-8f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denominador = a * e - b * b;
                    s = Mathf.Abs(denominador) > 1e-8f
                        ? Mathf.Clamp01((b * f - c * e) / denominador)
                        : 0f;
                    t = (b * s + f) / e;

                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Informe
    // ---------------------------------------------------------------------------------------

    private static void EscribirInformes(
        Dictionary<string, ResultadoClip> resultados,
        DateTime inicio,
        bool soloUsadas)
    {
        string raiz = Path.GetDirectoryName(Application.dataPath);
        string rutaMd = Path.Combine(raiz, InformeMd);
        string rutaCsv = Path.Combine(raiz, InformeCsv);
        Directory.CreateDirectory(Path.GetDirectoryName(rutaMd));
        Directory.CreateDirectory(Path.GetDirectoryName(rutaCsv));

        List<ResultadoClip> todos = resultados.Values
            .Where(r => r.Humanoide)
            .OrderByDescending(Puntuacion)
            .ToList();

        List<ResultadoClip> usados = todos.Where(r => r.Usado).ToList();
        List<ResultadoClip> sinUsar = todos.Where(r => !r.Usado).ToList();

        StringBuilder md = new StringBuilder(32768);
        md.AppendLine("# AuditorÃ­a de animaciones humanoides");
        md.AppendLine();
        md.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm}.");
        md.AppendLine();
        md.AppendLine("Medida automÃ¡tica sobre el personaje real. No modifica ninguna animaciÃ³n.");
        md.AppendLine();
        md.AppendLine("## Resumen");
        md.AppendLine();
        md.AppendLine($"- Clips humanoides analizados: **{todos.Count}**.");
        md.AppendLine($"- Usados por los controllers: **{usados.Count}**; sin usar: **{sinUsar.Count}**.");
        md.AppendLine($"- Con penetraciÃ³n de cabeza: **{todos.Count(r => r.PeorCabeza != null && r.PeorCabeza.MaxCabeza > 0.005f)}**.");
        md.AppendLine($"- Con penetraciÃ³n de torso: **{todos.Count(r => r.PeorTorso != null && r.PeorTorso.MaxTorso > 0.02f)}**.");
        md.AppendLine($"- Con cuello forzado (â‰¥55Â°): **{todos.Count(r => r.PeorCuello != null && Mathf.Abs(r.PeorCuello.MaxCuello) >= 55f)}**.");
        md.AppendLine($"- Limpios: **{todos.Count(r => r.Severidad == "Limpio")}**.");
        md.AppendLine($"- Con error de muestreo: **{todos.Count(r => r.Mediciones.Any(m => m.Error))}**.");
        md.AppendLine();
        md.AppendLine("## Los 40 peores");
        md.AppendLine();
        md.AppendLine("| # | Clip | Rig | Cabeza | Torso | Cuello | Severidad | Uso |");
        md.AppendLine("|---:|---|---|---:|---:|---:|---|---|");

        int puesto = 0;
        foreach (ResultadoClip r in todos.Take(40))
        {
            puesto++;
            md.AppendLine($"| {puesto} | {NombreClip(r)} | {RigDe(r)} | {Cm(r.PeorCabeza?.MaxCabeza)} | " +
                          $"{Cm(r.PeorTorso?.MaxTorso)} | {Grados(r.PeorCuello?.MaxCuello)} | " +
                          $"{r.Severidad} | {Uso(r)} |");
        }

        md.AppendLine();
        md.AppendLine("## PenetraciÃ³n de cabeza");
        md.AppendLine();
        md.AppendLine("La mano o el antebrazo entran en el volumen de la cabeza. Es el fallo que mÃ¡s se nota en plano cercano.");
        md.AppendLine();
        md.AppendLine("| Clip | Rig | Profundidad | Momento | Parte | Uso |");
        md.AppendLine("|---|---|---:|---:|---|---|");
        foreach (ResultadoClip r in todos
                     .Where(r => r.PeorCabeza != null && r.PeorCabeza.MaxCabeza > 0.005f)
                     .OrderByDescending(r => r.PeorCabeza.MaxCabeza)
                     .Take(80))
        {
            MedicionPorRig m = r.PeorCabeza;
            md.AppendLine($"| {NombreClip(r)} | {m.Rig} | {Cm(m.MaxCabeza)} | {m.TiempoCabeza:0.00} s | " +
                          $"{m.ParteCabeza} | {Uso(r)} |");
        }

        md.AppendLine();
        md.AppendLine("## PenetraciÃ³n de torso");
        md.AppendLine();
        md.AppendLine("Solo se mide por encima de la cintura; las manos apoyadas en la cadera no cuentan.");
        md.AppendLine();
        md.AppendLine("| Clip | Rig | Profundidad | Momento | Parte | Uso |");
        md.AppendLine("|---|---|---:|---:|---|---|");
        foreach (ResultadoClip r in todos
                     .Where(r => r.PeorTorso != null && r.PeorTorso.MaxTorso > 0.02f)
                     .OrderByDescending(r => r.PeorTorso.MaxTorso)
                     .Take(80))
        {
            MedicionPorRig m = r.PeorTorso;
            md.AppendLine($"| {NombreClip(r)} | {m.Rig} | {Cm(m.MaxTorso)} | {m.TiempoTorso:0.00} s | " +
                          $"{m.ParteTorso} | {Uso(r)} |");
        }

        md.AppendLine();
        md.AppendLine("## Cuello forzado");
        md.AppendLine();
        md.AppendLine("DesviaciÃ³n de la cabeza respecto a la orientaciÃ³n natural del personaje. El lÃ­mite actual del avatar ya recorta mucho; si aparece aquÃ­, la pose original pedÃ­a todavÃ­a mÃ¡s.");
        md.AppendLine();
        md.AppendLine("| Clip | Rig | DesviaciÃ³n | Momento | Uso |");
        md.AppendLine("|---|---|---:|---:|---|");
        foreach (ResultadoClip r in todos
                     .Where(r => r.PeorCuello != null && Mathf.Abs(r.PeorCuello.MaxCuello) >= 55f)
                     .OrderByDescending(r => Mathf.Abs(r.PeorCuello.MaxCuello))
                     .Take(60))
        {
            MedicionPorRig m = r.PeorCuello;
            md.AppendLine($"| {NombreClip(r)} | {m.Rig} | {Grados(m.MaxCuello)} | {m.TiempoCuello:0.00} s | {Uso(r)} |");
        }

        if (!soloUsadas)
        {
            md.AppendLine();
            md.AppendLine("## Clips sin usar con problemas");
            md.AppendLine();
            md.AppendLine("Material del catÃ¡logo que todavÃ­a no estÃ¡ en ningÃºn controller. Sirve para saber quÃ© se puede aÃ±adir sin retocar y quÃ© habrÃ­a que corregir antes.");
            md.AppendLine();
            md.AppendLine("| Clip | Cabeza | Torso | Cuello | Severidad |");
            md.AppendLine("|---|---:|---:|---:|---|");
            foreach (ResultadoClip r in sinUsar
                         .Where(r => r.Severidad != "Limpio" && r.Severidad != "Sin datos")
                         .OrderByDescending(Puntuacion)
                         .Take(100))
            {
                md.AppendLine($"| {NombreClip(r)} | {Cm(r.PeorCabeza?.MaxCabeza)} | {Cm(r.PeorTorso?.MaxTorso)} | " +
                              $"{Grados(r.PeorCuello?.MaxCuello)} | {r.Severidad} |");
            }
        }

        md.AppendLine();
        md.AppendLine("## MetodologÃ­a");
        md.AppendLine();
        md.AppendLine("- Se recorren los estados y blend trees de `NPC_NoWeapon` e `Invector@BasicLocomotion`.");
        md.AppendLine("- Los clips usados se prueban en Eldran, Will, Liam y Estela; los del catÃ¡logo, en Eldran.");
        md.AppendLine("- Se muestrea el clip a 30 fps (15 fps para los que no se usan), con un tope de 240 muestras por clip, y se mide en cada frame.");
        md.AppendLine("- Cabeza: esfera ajustada a las mallas de cabeza/pelo/cara, con un 8 % de margen hacia dentro.");
        md.AppendLine("- Torso: cÃ¡psula desde la cadera hasta el pecho, con radio proporcional al ancho de hombros.");
        md.AppendLine("- Mano: esfera de 5,5 % de la altura; antebrazo: cÃ¡psula de 3,2 %.");
        md.AppendLine("- Se marca penetraciÃ³n de cabeza a partir de 5 mm y de torso a partir de 20 mm.");
        md.AppendLine("- Cuello: Ã¡ngulo de la cabeza respecto a la cadera, descontando la orientaciÃ³n natural del prefab.");
        md.AppendLine("- Frames: 30 fps para los usados, 15 fps para el catÃ¡logo; un clip de 3 s son 90 muestras.");
        md.AppendLine();
        md.AppendLine("## Siguiente paso");
        md.AppendLine();
        md.AppendLine("El corrector leerÃ¡ este informe, corregirÃ¡ solo los frames que lo necesiten y hornearÃ¡ copias en `Assets/Art/Animations/Corregidas/`. Los originales no se tocan.");
        md.AppendLine();

        File.WriteAllText(rutaMd, md.ToString(), new UTF8Encoding(false));

        StringBuilder csv = new StringBuilder(16384);
        csv.AppendLine("Clip;Ruta;Usado;Controladores;Duracion;Rig;Cabeza_cm;Cabeza_frame;Parte_cabeza;Torso_cm;Torso_frame;Parte_torso;Cuello_grados;Cuello_frame;Severidad");
        foreach (ResultadoClip r in todos)
        {
            MedicionPorRig c = r.PeorCabeza;
            MedicionPorRig t = r.PeorTorso;
            MedicionPorRig n = r.PeorCuello;
            csv.AppendLine(string.Join(";", new[]
            {
                Escapar(r.Clip.name),
                Escapar(r.Ruta),
                r.Usado ? "si" : "no",
                Escapar(string.Join(" | ", r.Usos.Select(u => u.ToString()))),
                r.Clip.length.ToString("0.###", CultureInfo.InvariantCulture),
                Escapar(c?.Rig ?? ""),
                CmPlano(c?.MaxCabeza),
                c != null ? c.TiempoCabeza.ToString("0.00", CultureInfo.InvariantCulture) : "",
                Escapar(c?.ParteCabeza ?? ""),
                CmPlano(t?.MaxTorso),
                t != null ? t.TiempoTorso.ToString("0.00", CultureInfo.InvariantCulture) : "",
                Escapar(t?.ParteTorso ?? ""),
                n != null ? n.MaxCuello.ToString("0.0", CultureInfo.InvariantCulture) : "",
                n != null ? n.TiempoCuello.ToString("0.00", CultureInfo.InvariantCulture) : "",
                r.Severidad,
            }));
        }
        File.WriteAllText(rutaCsv, csv.ToString(), new UTF8Encoding(false));

        Debug.Log($"[Auditor] Informe escrito en {rutaMd} y {rutaCsv}. DuraciÃ³n: {(DateTime.Now - inicio).TotalMinutes:0.0} min.");
    }

    private static double Puntuacion(ResultadoClip r)
    {
        double cabeza = r.PeorCabeza?.MaxCabeza ?? 0f;
        double torso = r.PeorTorso?.MaxTorso ?? 0f;
        double cuello = Mathf.Abs(r.PeorCuello?.MaxCuello ?? 0f) / 1000.0;
        return cabeza * 10.0 + torso * 4.0 + cuello;
    }

    private static string NombreClip(ResultadoClip r)
    {
        return Escapar(r.Clip.name);
    }

    private static string RigDe(ResultadoClip r)
    {
        return r.PeorCabeza?.Rig ?? r.PeorTorso?.Rig ?? r.PeorCuello?.Rig ?? "";
    }

    private static string Uso(ResultadoClip r)
    {
        if (!r.Usado || r.Usos.Count == 0)
            return "â€”";
        string primero = r.Usos[0].ToString();
        return r.Usos.Count > 1 ? $"{Escapar(primero)} (+{r.Usos.Count - 1})" : Escapar(primero);
    }

    private static string Cm(float? metros)
    {
        return metros.HasValue ? $"{metros.Value * 100f:0.0} cm" : "â€”";
    }

    private static string CmPlano(float? metros)
    {
        return metros.HasValue ? (metros.Value * 100f).ToString("0.0", CultureInfo.InvariantCulture) : "";
    }

    private static string Grados(float? grados)
    {
        return grados.HasValue ? $"{grados.Value:0.0}Â°" : "â€”";
    }

    private static string Escapar(string texto)
    {
        return (texto ?? "").Replace("|", "\\|").Replace(";", ",");
    }

    // ---------------------------------------------------------------------------------------
    // Tipos auxiliares
    // ---------------------------------------------------------------------------------------

    public class UsoDeClip
    {
        public string Controlador;
        public string Capa;
        public string Estado;

        public override string ToString()
        {
            return $"{Controlador} / {Capa} / {Estado}";
        }
    }

    public class MedicionPorRig
    {
        public string Rig;
        public float Duracion;
        public float MaxCabeza;
        public float TiempoCabeza;
        public string ParteCabeza = "";
        public int FramesCabeza;
        public float MaxTorso;
        public float TiempoTorso;
        public string ParteTorso = "";
        public int FramesTorso;
        public float MaxCuello;
        public float TiempoCuello;
        public int FramesCuello;
        public bool Error;
        public string MensajeError = "";
    }

    public struct MedicionFrame
    {
        public float Cabeza;
        public float Torso;
        public float Cuello;
        public string ParteCabeza;
        public string ParteTorso;
    }

    public class ResultadoClip
    {
        public AnimationClip Clip;
        public string Ruta;
        public bool Usado;
        public bool Humanoide;
        public List<UsoDeClip> Usos = new List<UsoDeClip>();
        public List<MedicionPorRig> Mediciones = new List<MedicionPorRig>();

        public MedicionPorRig PeorCabeza
        {
            get
            {
                MedicionPorRig peor = null;
                foreach (MedicionPorRig m in Mediciones)
                {
                    if (m.Error)
                        continue;
                    if (peor == null || m.MaxCabeza > peor.MaxCabeza)
                        peor = m;
                }
                return peor;
            }
        }

        public MedicionPorRig PeorTorso
        {
            get
            {
                MedicionPorRig peor = null;
                foreach (MedicionPorRig m in Mediciones)
                {
                    if (m.Error)
                        continue;
                    if (peor == null || m.MaxTorso > peor.MaxTorso)
                        peor = m;
                }
                return peor;
            }
        }

        public MedicionPorRig PeorCuello
        {
            get
            {
                MedicionPorRig peor = null;
                foreach (MedicionPorRig m in Mediciones)
                {
                    if (m.Error)
                        continue;
                    if (peor == null || Mathf.Abs(m.MaxCuello) > Mathf.Abs(peor.MaxCuello))
                        peor = m;
                }
                return peor;
            }
        }

        public string Severidad
        {
            get
            {
                MedicionPorRig c = PeorCabeza;
                MedicionPorRig t = PeorTorso;
                MedicionPorRig n = PeorCuello;
                if (c == null)
                    return "Sin datos";

                float cabeza = c.MaxCabeza;
                float torso = t != null ? t.MaxTorso : 0f;
                float cuello = n != null ? Mathf.Abs(n.MaxCuello) : 0f;

                if (cabeza >= 0.05f || torso >= 0.06f || cuello >= 80f)
                    return "Grave";
                if (cabeza >= 0.02f || torso >= 0.03f || cuello >= 65f)
                    return "Media";
                if (cabeza > 0.005f || torso > 0.02f || cuello >= 55f)
                    return "Leve";
                return "Limpio";
            }
        }
    }
}
#endif


