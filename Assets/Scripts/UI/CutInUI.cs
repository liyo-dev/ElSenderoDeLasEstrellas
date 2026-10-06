using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public enum EncuadreCutIn { Ojos = 0, Cara = 1, Busto = 2 }
public enum LadoCutIn { Izquierda, Derecha }

/// Primeros planos en vivo en franjas que entran y salen con tiempo real. Hay dos ranuras
/// independientes para enfrentar a dos personajes a la vez (pantalla partida de anime).
public sealed class CutInUI : MonoBehaviour
{
    public const int Ranuras = 2;
    public static CutInUI Instance { get; private set; }

    /// Alguna franja está visible o entrando/saliendo.
    public bool Ocupado
    {
        get
        {
            if (_franjas == null) return false;
            foreach (var f in _franjas) if (f != null && f.Ocupada) return true;
            return false;
        }
    }

    /// Alguna franja está entrando o saliendo.
    public bool Animando
    {
        get
        {
            if (_franjas == null) return false;
            foreach (var f in _franjas) if (f != null && f.animando) return true;
            return false;
        }
    }

    public bool OcupadaRanura(int ranura) => Valida(ranura) && _franjas[ranura].Ocupada;
    public bool AnimandoRanura(int ranura) => Valida(ranura) && _franjas[ranura].animando;

    sealed class Franja
    {
        public RawImage imagen;
        public Camera camara;
        public RenderTexture textura;
        public SequenceActor actor;
        public bool visible, animando;
        public float ojos, distancia, altura, origen, destino, avance, tiempo, restante, lado, fov, angulo;
        public bool Ocupada => visible || animando;
    }

    Franja[] _franjas;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    public static CutInUI Obtener(Transform propietario)
    {
        if (Instance != null) return Instance;
        var go = new GameObject("CutIn", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(propietario, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 9993;
        return go.AddComponent<CutInUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _franjas = new Franja[Ranuras];
        for (int i = 0; i < Ranuras; i++) _franjas[i] = CrearFranja(i);
        OcultarInmediato();
    }

    Franja CrearFranja(int indice)
    {
        var f = new Franja();
        var go = new GameObject("Franja" + indice, typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);
        f.imagen = go.GetComponent<RawImage>();
        f.imagen.raycastTarget = false;
        // Las dos franjas se inclinan en sentidos opuestos: se leen como un enfrentamiento.
        f.imagen.rectTransform.localRotation = Quaternion.Euler(0f, 0f, indice == 0 ? 6f : -6f);
        CrearBorde(f.imagen.transform, true);
        CrearBorde(f.imagen.transform, false);
        f.textura = new RenderTexture(1600, 450, 24) { name = "CutIn" + indice, antiAliasing = 1 };
        f.textura.Create();
        f.imagen.texture = f.textura;
        var auxiliar = new GameObject("CamaraCutIn" + indice, typeof(Camera), typeof(UniversalAdditionalCameraData));
        auxiliar.transform.SetParent(transform, false);
        f.camara = auxiliar.GetComponent<Camera>();
        f.camara.enabled = false;
        return f;
    }

    static void CrearBorde(Transform padre, bool superior)
    {
        var go = new GameObject(superior ? "BordeSuperior" : "BordeInferior", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        var imagen = go.GetComponent<Image>();
        imagen.color = Color.white;
        imagen.raycastTarget = false;
        var rect = imagen.rectTransform;
        rect.anchorMin = new Vector2(0f, superior ? 1f : 0f);
        rect.anchorMax = new Vector2(1f, superior ? 1f : 0f);
        rect.sizeDelta = new Vector2(0f, 2f);
        rect.anchoredPosition = Vector2.zero;
    }

    bool Valida(int ranura) => _franjas != null && ranura >= 0 && ranura < _franjas.Length;

    public void Mostrar(SequenceActor actor, EncuadreCutIn encuadre, LadoCutIn lado, float altura, float duracion, int ranura = 0)
    {
        if (actor?.Transform == null || !Valida(ranura)) return;
        var f = _franjas[ranura];
        f.actor = actor;
        MedirEncuadre(f, encuadre);
        f.angulo = ElegirAngulo(f);
        var principal = Camera.main;
        if (principal != null)
        {
            f.camara.CopyFrom(principal);
            var fuente = principal.GetComponent<UniversalAdditionalCameraData>();
            var datos = f.camara.GetComponent<UniversalAdditionalCameraData>();
            datos.renderType = CameraRenderType.Base;
            if (fuente != null)
            {
                datos.renderPostProcessing = fuente.renderPostProcessing;
                datos.volumeLayerMask = fuente.volumeLayerMask;
                datos.volumeTrigger = fuente.volumeTrigger;
                datos.antialiasing = fuente.antialiasing;
                datos.requiresColorOption = fuente.requiresColorOption;
                datos.requiresDepthOption = fuente.requiresDepthOption;
            }
        }
        f.camara.targetTexture = f.textura;
        f.camara.rect = new Rect(0f, 0f, 1f, 1f);
        f.camara.orthographic = false;
        f.camara.aspect = 1600f / 450f;
        f.camara.fieldOfView = f.fov;
        f.camara.nearClipPlane = .02f;
        f.camara.enabled = true;
        f.altura = Mathf.Clamp01(altura);
        f.lado = lado == LadoCutIn.Izquierda ? -1f : 1f;
        f.restante = duracion > 0f ? duracion : -1f;
        if (!f.imagen.gameObject.activeSelf) f.imagen.gameObject.SetActive(true);
        SeguirActor(f);
        Transicion(f, true);
    }

    /// Retira una ranura, o todas con ranura negativa.
    public void Retirar(int ranura = -1)
    {
        if (_franjas == null) return;
        for (int i = 0; i < _franjas.Length; i++)
            if ((ranura < 0 || ranura == i) && _franjas[i].Ocupada) Transicion(_franjas[i], false);
    }

    static void Transicion(Franja f, bool mostrar)
    {
        f.visible = mostrar;
        f.origen = f.avance;
        f.destino = mostrar ? 1f : 0f;
        f.tiempo = 0f;
        f.animando = true;
    }

    void Update()
    {
        if (_franjas == null) return;
        float dt = Time.unscaledDeltaTime;
        foreach (var f in _franjas)
        {
            if (!f.Ocupada) continue;
            if (f.actor?.Transform == null) { Ocultar(f); continue; }
            if (f.animando)
            {
                f.tiempo += dt;
                float t = Mathf.Clamp01(f.tiempo / .15f);
                f.avance = Mathf.Lerp(f.origen, f.destino, t * t * (3f - 2f * t));
                if (t >= 1f)
                {
                    f.animando = false;
                    if (!f.visible) { Ocultar(f); continue; }
                }
            }
            var rect = f.imagen.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, f.altura);
            rect.sizeDelta = new Vector2(Screen.width * 1.12f, Screen.height * .28f);
            rect.anchoredPosition = new Vector2(f.lado * (1f - f.avance) * Screen.width * 1.3f, 0f);
            if (f.visible && f.restante >= 0f)
            {
                f.restante -= dt;
                if (f.restante <= 0f) Transicion(f, false);
            }
        }
    }

    void LateUpdate()
    {
        if (_franjas == null) return;
        foreach (var f in _franjas)
            if (f.Ocupada && f.actor?.Transform != null) SeguirActor(f);
    }

    // Se mide al abrir la franja, sin búsquedas ni asignaciones por fotograma.
    //
    // La franja es muy apaisada (≈3,55:1) y ocupa un 28 % del alto de la pantalla: para que la cara
    // se lea, la cabeza tiene que llenar su alto. Por eso el encuadre se calcula con el tamaño real
    // de la cabeza (de la base del cráneo a la coronilla), no con el del cuerpo entero, y los
    // accesorios altos —púas de casco, sombreros— no lo agrandan. Ver INC-582.
    static void MedirEncuadre(Franja f, EncuadreCutIn encuadre)
    {
        var actor = f.actor;
        float pies = actor.Transform.position.y;
        // Los ojos: las mallas de ojos activas (Eye04…) dicen dónde están de verdad. En los chibis
        // quedan muy por debajo de la coronilla y el pivote de la cabeza está casi a su altura,
        // así que las medidas por hueso o por «alto de la cabeza» fallaban (franjas con el
        // personaje entero y pequeño, INC-591).
        float ojos = actor.AlturaDeOjos;
        float suma = 0f; int n = 0;
        foreach (var r in actor.Transform.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            string nombre = r.name.ToLowerInvariant();
            if (!nombre.Contains("eye") || nombre.Contains("brow") || nombre.Contains("lash") || nombre.Contains("lid")) continue;
            if (r.bounds.size.y > .6f) continue;
            suma += r.bounds.center.y - pies; n++;
        }
        if (n > 0) ojos = suma / n;
        // Alto de la cabeza: los ojos quedan a un 30 % desde la barbilla. Cuernos y púas no cuentan
        // (de ahí el tope).
        float cabeza = Mathf.Clamp((actor.HeadTopHeight - ojos) / .7f, .3f, .8f);

        // Alto visible de la franja y dónde quedan los ojos dentro de ella (0 abajo, 1 arriba).
        float alto, ojosEnCuadro;
        switch (encuadre)
        {
            case EncuadreCutIn.Ojos: alto = cabeza * .55f; ojosEnCuadro = .5f; f.fov = 20f; break;
            case EncuadreCutIn.Busto: alto = cabeza * 2.1f; ojosEnCuadro = .6f; f.fov = 32f; break;
            default: alto = cabeza * 1.1f; ojosEnCuadro = .45f; f.fov = 26f; break;
        }
        f.ojos = ojos + (.5f - ojosEnCuadro) * alto;
        f.distancia = alto * .5f / Mathf.Tan(f.fov * .5f * Mathf.Deg2Rad) + cabeza * .35f;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        Debug.Log($"[CutInUI] {actor.Id} ({encuadre}): ojos {ojos:F2} m ({n} mallas), cabeza {cabeza:F2} m, " +
            $"alto visible {alto:F2} m, distancia {f.distancia:F2} m, fov {f.fov:F0}.");
#endif
    }

    /// Ángulo (respecto a la cara) desde el que nada tapa: ni decorado ni otro personaje.
    static float ElegirAngulo(Franja f)
    {
        Vector3 objetivo = f.actor.Transform.position + Vector3.up * f.ojos;
        Vector3 frente = ShotComposer.FrenteFiable(f.actor);
        foreach (float a in new[] { 0f, 25f, -25f, 50f, -50f, 75f, -75f })
        {
            Vector3 cam = objetivo + Quaternion.AngleAxis(a, Vector3.up) * frente * f.distancia;
            if (!Tapado(f.actor, objetivo, cam)) return a;
        }
        return 0f;
    }

    static bool Tapado(SequenceActor actor, Vector3 cara, Vector3 cam)
    {
        Vector3 d = cam - cara;
        float largo = d.magnitude;
        if (largo < .3f) return false;
        d /= largo;
        foreach (var h in Physics.SphereCastAll(cara + d * .25f, .12f, d, largo - .25f, ~0, QueryTriggerInteraction.Ignore))
            if (!h.transform.IsChildOf(actor.Transform)) return true;
        // Personajes sin collider: se mira su posición.
        if (Game.NPC.NPCRegistry.HasInstance)
            foreach (var id in Game.NPC.NPCRegistry.Instance.GetAllRegisteredIDs())
            {
                var otro = Game.NPC.NPCRegistry.Instance.GetNPCByID(id);
                if (otro == null || otro.transform == actor.Transform || !otro.gameObject.activeInHierarchy) continue;
                Vector3 p = otro.transform.position;
                if (p.y > cara.y + .3f || p.y + 1.8f < cara.y - .3f) continue;
                Vector3 a = new Vector3(cara.x, 0f, cara.z), b = new Vector3(cam.x, 0f, cam.z), q = new Vector3(p.x, 0f, p.z);
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                if (t > .1f && (a + ab * t - q).magnitude < .5f) return true;
            }
        return false;
    }

    static void SeguirActor(Franja f)
    {
        Vector3 objetivo = f.actor.Transform.position + Vector3.up * f.ojos;
        Vector3 frente = Quaternion.AngleAxis(f.angulo, Vector3.up) * ShotComposer.FrenteFiable(f.actor);
        f.camara.transform.position = objetivo + frente * f.distancia;
        f.camara.transform.rotation = Quaternion.LookRotation(-frente, Vector3.up);
    }

    static void Ocultar(Franja f)
    {
        f.visible = f.animando = false;
        f.avance = 0f;
        f.actor = null;
        if (f.camara != null) f.camara.enabled = false;
        if (f.imagen != null && f.imagen.gameObject.activeSelf) f.imagen.gameObject.SetActive(false);
    }

    public void OcultarInmediato()
    {
        if (_franjas == null) return;
        foreach (var f in _franjas) if (f != null) Ocultar(f);
    }

    void OnDisable() { OcultarInmediato(); }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_franjas == null) return;
        foreach (var f in _franjas)
        {
            if (f == null) continue;
            if (f.camara != null) f.camara.targetTexture = null;
            if (f.textura != null) { f.textura.Release(); Destroy(f.textura); }
        }
    }
}
