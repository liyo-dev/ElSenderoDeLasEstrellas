using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Slot = PartyControlManager.CharacterSlot;

namespace Sendero.UI
{
    /// <summary>
    /// Retratos vivos del grupo: un estudio fuera del mundo (muy por debajo del suelo, más allá
    /// del plano lejano de las cámaras de juego) con una copia de cada personaje —solo mallas,
    /// Animator, cara y ropa, sin nada de juego— y dos cámaras por personaje que la filman a sendas
    /// RenderTexture, una sobre negro y otra sobre blanco. URP no conserva el alfa de la
    /// RenderTexture, así que el retrato del HUD saca la transparencia de la diferencia entre las
    /// dos (ver <see cref="ImagenDeRetrato.TexturaSobreFondo"/>). Además le avisa de cuándo se ve y
    /// de qué reacciones hacer (<see cref="IFuenteDeRetratoVivo"/>).
    ///
    /// Cada cara es un estado del Animator del estudio (Normal, Cansado, Caido, Contento) más la
    /// emoción de su <see cref="NPCEmotionController"/> (parpadea solo); el golpe, el saludo y la
    /// victoria son estados de una vez que vuelven a la cara que toque. La ropa se copia del
    /// cuerpo real del personaje (<see cref="ModularAutoBuilder"/>) cada pocos segundos mientras
    /// se ve. La luz del estudio solo se enciende mientras dibujan sus cámaras, y el sol del
    /// mundo se apaga en ese momento, así que el retrato no cambia con la hora ni el clima.
    ///
    /// Si el componente está desactivado o una copia no se puede crear, no da imagen y el retrato
    /// usa su respaldo (los retratos horneados).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EstudioDeRetratos : MonoBehaviour, IFuenteDeRetratoVivo
    {
        private static readonly int EstadoNormal = Animator.StringToHash("Normal");
        private static readonly int EstadoCansado = Animator.StringToHash("Cansado");
        private static readonly int EstadoCaido = Animator.StringToHash("Caido");
        private static readonly int EstadoContento = Animator.StringToHash("Contento");
        private static readonly int EstadoGolpe = Animator.StringToHash("Golpe");
        private static readonly int EstadoSaludo = Animator.StringToHash("Saludo");
        private static readonly int EstadoVictoria = Animator.StringToHash("Victoria");

        [Header("Modelos (0 = Liam, 1 = Will, 2 = Estela)")]
        [Tooltip("Copias del personaje sin componentes de juego: mallas, Animator y ropa.")]
        [SerializeField] private GameObject[] modelos = new GameObject[3];
        [SerializeField] private RuntimeAnimatorController controlador;

        [Header("Encuadre")]
        [SerializeField] private int resolucion = 384;
        [Tooltip("Parte del alto del personaje que entra en el plano, desde arriba (cabeza y pecho).")]
        [SerializeField, Range(0.3f, 1f)] private float alturaDesdeArriba = 0.62f;
        [SerializeField, Range(0f, 0.3f)] private float margen = 0.04f;
        [SerializeField] private float giro = 12f;
        [SerializeField] private float inclinacion = 4f;
        [Tooltip("Parte del plano que queda por debajo del borde del panel del HUD.")]
        [SerializeField, Range(0f, 0.5f)] private float fraccionBajoElBorde = 0.1f;
        [Tooltip("Distancia entre las copias, para que ninguna se cuele en el plano de otra.")]
        [SerializeField] private float separacion = 8f;
        [SerializeField] private string capa = "UI_Portrait";

        [Header("Luz del estudio")]
        [SerializeField] private Color colorDeLuz = new Color(1f, 0.97f, 0.92f, 1f);
        [SerializeField] private float luzPrincipal = 1.15f;
        [SerializeField] private float luzDeRelleno = 0.5f;

        [Header("Ritmo")]
        [Tooltip("Cada cuántos segundos se copia la ropa del personaje real mientras se ve.")]
        [SerializeField] private float sincronizarRopaCada = 2f;
        [SerializeField] private float fundido = 0.15f;
        [Tooltip("Dibuja un solo retrato por fotograma, por turnos: con tres a la vista cada uno se " +
                 "refresca a un tercio de la frecuencia del juego y se ahorran cuatro cámaras por fotograma.")]
        [SerializeField] private bool repartirEntreFotogramas = true;

        /// Renderer sin efectos (ni SSAO ni post) con el que dibujan las cámaras del estudio; si el
        /// pipeline no lo tiene, usan el de por defecto. Ver INC-865.
        public const string RendererDeRetratos = "Retratos_Renderer";

        private sealed class Copia
        {
            public GameObject raiz;
            public Animator animator;
            public NPCEmotionController cara;
            public ModularAutoBuilder ropa;
            public Camera camara;
            public RenderTexture textura;
            public Camera camaraDelFondo;
            public RenderTexture texturaDelFondo;
            public bool visible;
            public ExpresionDeRetrato expresion;
            public int estadoBase = EstadoNormal;
            public float finDeReaccion;
            public Dictionary<PartCategory, string> ropaAplicada;
        }

        private readonly Copia[] _copias = new Copia[3];
        private readonly bool[] _fallida = new bool[3];
        private Light[] _luces;
        private Light _solApagado;
        private int _camarasVisibles;
        private float _proximaRopa;
        private int _turno;

        // ─── IFuenteDeRetrato ──────────────────────────────────────────────────

        public bool TryObtener(Slot personaje, ExpresionDeRetrato expresion, out ImagenDeRetrato imagen)
        {
            imagen = default;
            if (!isActiveAndEnabled) return false;
            var c = Asegurar((int)personaje);
            if (c == null) return false;
            PonerCara(c, expresion);
            imagen = new ImagenDeRetrato(c.textura, new Rect(0f, 0f, 1f, 1f), 1f, fraccionBajoElBorde, c.texturaDelFondo);
            return true;
        }

        public void Mostrar(Slot personaje, bool visible)
        {
            var c = _copias[(int)personaje];
            if (c == null) { if (visible && isActiveAndEnabled) c = Asegurar((int)personaje); if (c == null) return; }
            if (c.visible == visible) return;
            // Al salir del Play, el retrato puede pedir que lo escondan con las copias ya destruidas.
            if (c.raiz == null || c.camara == null || c.camaraDelFondo == null) return;
            c.visible = visible;
            _camarasVisibles += visible ? 1 : -1;
            if (c.raiz.activeSelf != visible) c.raiz.SetActive(visible);
            c.camara.enabled = visible;
            c.camaraDelFondo.enabled = visible;
            // El que acaba de aparecer es el siguiente en dibujarse.
            if (visible) _turno = ((int)personaje + _copias.Length - 1) % _copias.Length;
            if (visible) { PonerEstado(c, c.estadoBase, 0f); SincronizarRopa((int)personaje, c); }
        }

        public void Reaccionar(Slot personaje, ReaccionDeRetrato reaccion)
        {
            var c = _copias[(int)personaje];
            if (c == null || !c.visible || c.expresion == ExpresionDeRetrato.Caido) return;
            int estado = reaccion == ReaccionDeRetrato.Golpe ? EstadoGolpe : EstadoSaludo;
            PlayOnce(c, estado);
            if (c.cara != null)
                c.cara.Reaccionar(reaccion == ReaccionDeRetrato.Golpe ? NPCEmotion.Scared : NPCEmotion.Happy, 0.7f);
        }

        // ─── Ciclo ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            // El estudio de Start acompaña al HUD al descargar las escenas de entrada.
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += AntesDeDibujar;
            RenderPipelineManager.endCameraRendering += DespuesDeDibujar;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= AntesDeDibujar;
            RenderPipelineManager.endCameraRendering -= DespuesDeDibujar;
            RestaurarSol();
            for (int i = 0; i < _copias.Length; i++)
            {
                var c = _copias[i];
                if (c == null || !c.visible) continue;
                c.visible = false;
                if (c.camara != null) c.camara.enabled = false;
                if (c.camaraDelFondo != null) c.camaraDelFondo.enabled = false;
                if (c.raiz != null) c.raiz.SetActive(false);
            }
            _camarasVisibles = 0;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _copias.Length; i++)
            {
                var c = _copias[i];
                if (c == null) continue;
                if (c.camara != null) c.camara.targetTexture = null;
                if (c.camaraDelFondo != null) c.camaraDelFondo.targetTexture = null;
                if (c.textura != null) { c.textura.Release(); Destroy(c.textura); }
                if (c.texturaDelFondo != null) { c.texturaDelFondo.Release(); Destroy(c.texturaDelFondo); }
                if (c.raiz != null) Destroy(c.raiz);
            }
        }

        private void Update()
        {
            if (_camarasVisibles <= 0) return;
            if (repartirEntreFotogramas) RepartirTurno();
            float ahora = Time.unscaledTime;
            for (int i = 0; i < _copias.Length; i++)
            {
                var c = _copias[i];
                if (c == null || !c.visible || c.finDeReaccion <= 0f || ahora < c.finDeReaccion) continue;
                c.finDeReaccion = 0f;
                PonerEstado(c, c.estadoBase, fundido);
            }
            if (ahora < _proximaRopa) return;
            _proximaRopa = ahora + Mathf.Max(0.25f, sincronizarRopaCada);
            for (int i = 0; i < _copias.Length; i++)
                if (_copias[i] != null && _copias[i].visible) SincronizarRopa(i, _copias[i]);
        }

        /// Deja encendidas solo las dos cámaras del retrato al que le toca este fotograma.
        private void RepartirTurno()
        {
            int n = _copias.Length;
            for (int paso = 1; paso <= n; paso++)
            {
                int i = (_turno + paso) % n;
                if (_copias[i] != null && _copias[i].visible) { _turno = i; break; }
            }
            for (int i = 0; i < n; i++)
            {
                var c = _copias[i];
                if (c == null || c.camara == null || c.camaraDelFondo == null) continue;
                bool dibuja = c.visible && i == _turno;
                if (c.camara.enabled != dibuja) c.camara.enabled = dibuja;
                if (c.camaraDelFondo.enabled != dibuja) c.camaraDelFondo.enabled = dibuja;
            }
        }

        private static int IndiceDelRendererDeRetratos()
        {
            var pipeline = UniversalRenderPipeline.asset;
            if (pipeline == null) return -1;
            var lista = pipeline.rendererDataList;
            for (int i = 0; i < lista.Length; i++)
                if (lista[i] != null && lista[i].name == RendererDeRetratos) return i;
            return -1;
        }

        // ─── Copias ────────────────────────────────────────────────────────────

        private Copia Asegurar(int slot)
        {
            if (_copias[slot] != null) return _copias[slot];
            if (_fallida[slot] || slot < 0 || slot >= modelos.Length || modelos[slot] == null) { _fallida[slot] = true; return null; }
            if (_luces == null) CrearLuces();

            var c = new Copia();
            var sitio = transform.position + Vector3.right * (separacion * slot);
            c.raiz = Instantiate(modelos[slot], sitio, Quaternion.identity, transform);
            c.raiz.name = modelos[slot].name;
            int layer = LayerMask.NameToLayer(capa);
            foreach (var t in c.raiz.GetComponentsInChildren<Transform>(true))
                if (layer >= 0) t.gameObject.layer = layer;
            foreach (var r in c.raiz.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (r is SkinnedMeshRenderer piel) piel.updateWhenOffscreen = true;
            }
            c.cara = c.raiz.GetComponentInChildren<NPCEmotionController>(true);
            c.ropa = c.raiz.GetComponentInChildren<ModularAutoBuilder>(true);
            c.animator = BuscarAnimator(c.raiz);
            if (c.animator == null || controlador == null) { Destroy(c.raiz); _fallida[slot] = true; return null; }
            c.animator.runtimeAnimatorController = controlador;
            c.animator.applyRootMotion = false;
            c.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            c.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            c.animator.Update(0f);
            OrientarDeFrente(c.raiz.transform, c.animator);

            // Encuadre: la parte de arriba del personaje en reposo, en ortográfica (como los iconos).
            var limites = Limites(c.raiz);
            float alto = limites.size.y * alturaDesdeArriba;
            var centro = new Vector3(limites.center.x, limites.max.y - alto * 0.5f, limites.center.z);
            var vista = Quaternion.Euler(inclinacion, giro + 180f, 0f);
            float distancia = Mathf.Max(limites.size.magnitude, 0.5f) * 2f;

            var posicion = centro - vista * Vector3.forward * distancia;
            float tamanoDelPlano = alto * 0.5f * (1f + margen);
            int mascara = layer >= 0 ? 1 << layer : ~0;
            // Dos cámaras idénticas, una sobre negro y otra sobre blanco: el shader del retrato
            // compara las dos imágenes y saca de la diferencia lo que cubre el personaje, porque
            // URP no conserva el alfa de la RenderTexture.
            c.camara = CrearCamara(c.raiz.name, posicion, vista, tamanoDelPlano, distancia, mascara, Color.black, out c.textura);
            c.camaraDelFondo = CrearCamara(c.raiz.name + " (fondo)", posicion, vista, tamanoDelPlano, distancia, mascara, Color.white, out c.texturaDelFondo);
            c.raiz.SetActive(false);
            c.expresion = (ExpresionDeRetrato)(-1);
            _copias[slot] = c;
            return c;
        }

        /// Cámara del estudio que dibuja al personaje sobre un color plano. Las dos cámaras de una
        /// copia comparten posición, encuadre y capa y solo cambian el fondo; el retrato compara sus
        /// dos imágenes para quedarse con la transparencia.
        private Camera CrearCamara(string nombre, Vector3 posicion, Quaternion rotacion, float tamano,
            float distancia, int mascara, Color fondo, out RenderTexture textura)
        {
            var camGo = new GameObject("Cámara de retrato de " + nombre, typeof(Camera), typeof(UniversalAdditionalCameraData));
            camGo.transform.SetParent(transform, false);
            camGo.transform.SetPositionAndRotation(posicion, rotacion);
            var camara = camGo.GetComponent<Camera>();
            camara.orthographic = true;
            camara.orthographicSize = tamano;
            camara.nearClipPlane = 0.05f;
            camara.farClipPlane = distancia * 2f;
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = fondo;
            camara.cullingMask = mascara;
            camara.allowHDR = false;
            camara.allowMSAA = true;
            camara.useOcclusionCulling = false;
            camara.depth = -100f;
            var datos = camGo.GetComponent<UniversalAdditionalCameraData>();
            datos.renderType = CameraRenderType.Base;
            datos.renderPostProcessing = false;
            datos.renderShadows = false;
            datos.antialiasing = AntialiasingMode.None;
            datos.requiresColorOption = CameraOverrideOption.Off;
            datos.requiresDepthOption = CameraOverrideOption.Off;
            datos.volumeLayerMask = 0;
            int renderer = IndiceDelRendererDeRetratos();
            if (renderer >= 0) datos.SetRenderer(renderer);
            textura = new RenderTexture(resolucion, resolucion, 24, RenderTextureFormat.ARGB32)
                { name = "Retrato vivo de " + nombre, antiAliasing = 4 };
            textura.Create();
            camara.targetTexture = textura;
            camara.enabled = false;
            return camara;
        }

        /// La derecha anatómica y la vertical del cuerpo definen el frente, aunque el avatar
        /// o la pose de reposo estén girados respecto a la raíz del prefab.
        public static void OrientarDeFrente(Transform raiz, Animator animator)
        {
            if (!animator.isHuman) return;
            var izquierda = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var derecha = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var caderas = animator.GetBoneTransform(HumanBodyBones.Hips);
            var cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
            if (izquierda == null || derecha == null || caderas == null || cabeza == null) return;

            var frente = Vector3.Cross(derecha.position - izquierda.position, cabeza.position - caderas.position);
            frente = Vector3.ProjectOnPlane(frente, Vector3.up);
            if (frente.sqrMagnitude < 0.000001f) return;
            raiz.rotation = Quaternion.FromToRotation(frente.normalized, Vector3.forward) * raiz.rotation;
        }

        private static Animator BuscarAnimator(GameObject raiz)
        {
            foreach (var a in raiz.GetComponentsInChildren<Animator>(true))
                if (a.avatar != null && a.avatar.isHuman) return a;
            return raiz.GetComponentInChildren<Animator>(true);
        }

        private static Bounds Limites(GameObject raiz)
        {
            bool hay = false;
            var b = new Bounds(raiz.transform.position, Vector3.one);
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (!hay) { b = r.bounds; hay = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        // ─── Caras y reacciones ────────────────────────────────────────────────

        private void PonerCara(Copia c, ExpresionDeRetrato expresion)
        {
            if (c.expresion == expresion) return;
            bool aContento = expresion == ExpresionDeRetrato.Contento && c.expresion != ExpresionDeRetrato.Contento;
            c.expresion = expresion;
            c.estadoBase = expresion switch
            {
                ExpresionDeRetrato.Dolorido => EstadoCansado,
                ExpresionDeRetrato.Caido => EstadoCaido,
                ExpresionDeRetrato.Contento => EstadoContento,
                _ => EstadoNormal
            };
            if (c.cara != null)
            {
                switch (expresion)
                {
                    case ExpresionDeRetrato.Dolorido: c.cara.SetEmotion(NPCEmotion.Tired); break;
                    case ExpresionDeRetrato.Caido: c.cara.SetEmotion(NPCEmotion.Sad); c.cara.AplicarOjos("Eye09"); break;
                    case ExpresionDeRetrato.Contento: c.cara.SetEmotion(NPCEmotion.Happy); break;
                    default: c.cara.VolverAReposo(); break;
                }
            }
            if (!c.visible) return;
            if (expresion == ExpresionDeRetrato.Caido) { c.finDeReaccion = 0f; PonerEstado(c, c.estadoBase, fundido * 2f); }
            else if (aContento) PlayOnce(c, EstadoVictoria);
            else if (c.finDeReaccion <= 0f) PonerEstado(c, c.estadoBase, fundido);
        }

        private void PlayOnce(Copia c, int estado)
        {
            PonerEstado(c, estado, 0.05f);
            c.animator.Update(0f);
            var info = c.animator.GetNextAnimatorStateInfo(0);
            if (info.shortNameHash != estado) info = c.animator.GetCurrentAnimatorStateInfo(0);
            float largo = info.length > 0.05f ? info.length / Mathf.Max(0.01f, info.speed) : 1f;
            c.finDeReaccion = Time.unscaledTime + Mathf.Min(largo, 3f);
        }

        private void PonerEstado(Copia c, int estado, float transicion)
        {
            if (c.animator == null || !c.animator.isActiveAndEnabled) return;
            if (transicion <= 0f) c.animator.Play(estado, 0, 0f);
            else c.animator.CrossFadeInFixedTime(estado, transicion, 0);
        }

        // ─── Ropa ──────────────────────────────────────────────────────────────

        private void SincronizarRopa(int slot, Copia c)
        {
            if (c.ropa == null) return;
            var real = RopaReal(slot);
            if (real == null) return;
            var seleccion = real.GetSelection();
            if (Iguales(seleccion, c.ropaAplicada)) return;
            c.ropa.ApplySelection(seleccion);
            c.ropa.RefreshRendererBoundsAfterAppearanceChange();
            c.ropaAplicada = seleccion;
        }

        /// La ropa del cuerpo real de ese personaje: el del jugador si va al mando; si no, el de su NPC.
        private static ModularAutoBuilder RopaReal(int slot)
        {
            var cuerpo = ControlDelJugador.CuerpoDe((Slot)slot);
            return cuerpo != null ? cuerpo.GetComponentInChildren<ModularAutoBuilder>(true) : null;
        }

        private static bool Iguales(Dictionary<PartCategory, string> a, Dictionary<PartCategory, string> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            foreach (var kv in a)
                if (!b.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
            return true;
        }

        // ─── Luz ───────────────────────────────────────────────────────────────

        private void CrearLuces()
        {
            _luces = new[]
            {
                CrearLuz("Luz del estudio", Quaternion.Euler(inclinacion + 35f, giro + 180f - 35f, 0f), luzPrincipal),
                CrearLuz("Relleno del estudio", Quaternion.Euler(inclinacion - 15f, giro + 180f + 45f, 0f), luzDeRelleno),
            };
        }

        private Light CrearLuz(string nombre, Quaternion rotacion, float intensidad)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            go.transform.rotation = rotacion;
            var luz = go.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.color = colorDeLuz;
            luz.intensity = intensidad;
            luz.shadows = LightShadows.None;
            luz.enabled = false;
            return luz;
        }

        private bool EsCamaraDelEstudio(Camera camara)
        {
            for (int i = 0; i < _copias.Length; i++)
                if (_copias[i] != null && (_copias[i].camara == camara || _copias[i].camaraDelFondo == camara)) return true;
            return false;
        }

        private void AntesDeDibujar(ScriptableRenderContext _, Camera camara)
        {
            if (_camarasVisibles <= 0 || !EsCamaraDelEstudio(camara)) return;
            for (int i = 0; i < _luces.Length; i++) _luces[i].enabled = true;
            var sol = RenderSettings.sun;
            if (sol != null && sol.enabled) { sol.enabled = false; _solApagado = sol; }
        }

        private void DespuesDeDibujar(ScriptableRenderContext _, Camera camara)
        {
            if (_luces == null || !EsCamaraDelEstudio(camara)) return;
            for (int i = 0; i < _luces.Length; i++) _luces[i].enabled = false;
            RestaurarSol();
        }

        private void RestaurarSol()
        {
            if (_solApagado != null) _solApagado.enabled = true;
            _solApagado = null;
            if (_luces != null) for (int i = 0; i < _luces.Length; i++) if (_luces[i] != null) _luces[i].enabled = false;
        }
    }
}
