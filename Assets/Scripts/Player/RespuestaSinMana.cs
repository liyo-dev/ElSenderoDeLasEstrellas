using System.Collections;
using UnityEngine;
using Invector.vCharacterController;
using Sendero.UI;

/// <summary>
/// Lo que se ve y se oye al pedir un hechizo sin maná suficiente (<see cref="MagicCaster.OnFaltaDeMana"/>):
/// amago de la mano sin bloquear el movimiento, unas chispas apagadas que caen y se deshacen en
/// humo junto a la mano, un sonido seco, la barra de maná del HUD que tiembla y un momento de
/// cara. No gasta nada ni frena al jugador: solo explica por qué no ha salido el hechizo.
/// Va junto al <see cref="MagicCaster"/>. Ver INC-1190.
/// </summary>
[DisallowMultipleComponent]
public class RespuestaSinMana : MonoBehaviour
{
    [Header("Gesto")]
    [Tooltip("Estado de la capa superior (ruta completa) del amago. Vacío: sin gesto.")]
    [SerializeField] private string gesto = "UpperBody.Magic.MagicRight";
    [Tooltip("Segundos que dura el amago antes de bajar la capa superior.")]
    [SerializeField, Min(0.05f)] private float duracionDelGesto = 0.35f;
    [Tooltip("Hueso de la mano de la que salen las chispas.")]
    [SerializeField] private HumanBodyBones mano = HumanBodyBones.RightHand;
    [Tooltip("Segundos desde que empieza el amago hasta que salen las chispas (la mano estirada).")]
    [SerializeField, Min(0f)] private float retrasoDeLasChispas = 0.14f;

    [Header("Chispas y humo")]
    [Tooltip("Material de las chispas (el mismo de las chispas del combo).")]
    [SerializeField] private Material materialChispas;
    [Tooltip("Material del humo (el mismo del polvo del combo).")]
    [SerializeField] private Material materialHumo;
    [SerializeField] private Color colorChispas = new Color(0.78f, 0.84f, 1f, 0.9f);
    [SerializeField] private Color colorHumo = new Color(0.62f, 0.64f, 0.7f, 0.45f);
    [SerializeField, Min(0)] private int chispas = 7;
    [SerializeField, Min(0)] private int bocanadas = 3;

    [Header("Sonido")]
    [Tooltip("Clave del sonido (AudioGraphProfile). Vacío: sin sonido.")]
    [SerializeField] private string sfx = "SpellFail";
    [SerializeField, Range(0.5f, 1.5f)] private float tono = 0.85f;
    [SerializeField, Range(0f, 1f)] private float volumen = 0.8f;

    [Header("Cara y HUD")]
    [Tooltip("Cara un momento al fallar. None: sin cambio de cara.")]
    [SerializeField] private NPCEmotion cara = NPCEmotion.Tired;
    [SerializeField, Min(0f)] private float segundosDeCara = 0.8f;
    [Tooltip("Hace temblar la barra de maná del HUD.")]
    [SerializeField] private bool sacudirBarraDeMana = true;

    private MagicCaster _caster;
    private vThirdPersonController _controller;
    private Animator _animator;
    private NPCEmotionController _emociones;
    private ParticleSystem _chispas, _humo;
    private Coroutine _co;

    void Awake()
    {
        _caster = GetComponent<MagicCaster>();
        _controller = GetComponentInParent<vThirdPersonController>();
        Transform raiz = _controller != null ? _controller.transform : transform;
        _animator = raiz.GetComponent<Animator>();
        if (_animator == null) _animator = raiz.GetComponentInChildren<Animator>(true);
        _emociones = raiz.GetComponentInChildren<NPCEmotionController>(true);
        _chispas = CrearChispas();
        _humo = CrearHumo();
    }

    void OnEnable()
    {
        if (_caster != null) _caster.OnFaltaDeMana += AlFaltarMana;
    }

    void OnDisable()
    {
        if (_caster != null) _caster.OnFaltaDeMana -= AlFaltarMana;
        if (_co != null) { StopCoroutine(_co); _co = null; }
    }

    private void AlFaltarMana(MagicSpellSO _)
    {
        if (_controller != null && !string.IsNullOrEmpty(gesto))
            _controller.PlayUpperBodyActionFor(gesto, duracionDelGesto);

        if (!string.IsNullOrEmpty(sfx) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfx, volumen, null, tono);

        if (_emociones != null && cara != NPCEmotion.None && segundosDeCara > 0f)
            _emociones.Reaccionar(cara, segundosDeCara);

        if (sacudirBarraDeMana && PlayerHUDV2.Instance != null)
            PlayerHUDV2.Instance.AvisarFaltaDeMana();

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(Co_Chispas());
    }

    private IEnumerator Co_Chispas()
    {
        if (retrasoDeLasChispas > 0f) yield return new WaitForSeconds(retrasoDeLasChispas);
        _co = null;

        Vector3 punto = PuntoDeLaMano();
        if (_chispas != null && chispas > 0)
        {
            _chispas.transform.position = punto;
            _chispas.Emit(chispas);
        }
        if (_humo != null && bocanadas > 0)
        {
            _humo.transform.position = punto;
            _humo.Emit(bocanadas);
        }
    }

    private Vector3 PuntoDeLaMano()
    {
        Transform hueso = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(mano) : null;
        if (hueso != null) return hueso.position;
        Transform raiz = _controller != null ? _controller.transform : transform;
        return raiz.position + Vector3.up * 1f + raiz.forward * 0.35f;
    }

    // ── Partículas (montadas por código, sin prefab) ─────────────────────────────

    private ParticleSystem CrearChispas()
    {
        var ps = CrearSistema("ChispasSinMana", materialChispas, 32);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        main.startColor = colorChispas;
        main.gravityModifier = 0.6f; // chispas sin fuerza: suben un poco y caen

        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 35f;
        forma.radius = 0.03f;
        forma.rotation = new Vector3(-90f, 0f, 0f); // hacia arriba

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        ConDesvanecido(ps, 1f);
        return ps;
    }

    private ParticleSystem CrearHumo()
    {
        var ps = CrearSistema("HumoSinMana", materialHumo, 8);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = colorHumo;
        main.gravityModifier = -0.05f;

        var forma = ps.shape;
        forma.enabled = true;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = 0.05f;

        var tam = ps.sizeOverLifetime;
        tam.enabled = true;
        tam.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.8f));

        ConDesvanecido(ps, 0.6f);
        return ps;
    }

    private ParticleSystem CrearSistema(string nombre, Material material, int maximo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maximo;

        var emision = ps.emission;
        emision.enabled = false;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enabled = material != null;
        return ps;
    }

    private static void ConDesvanecido(ParticleSystem ps, float alfaInicial)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(alfaInicial, 0f), new GradientAlphaKey(alfaInicial, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }
}
