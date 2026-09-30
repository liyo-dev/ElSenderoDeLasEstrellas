using UnityEngine;
using Sendero.Core.Feedback;

/// Lo que se ve y se siente cuando un jefe cambia de fase, igual para todos: se vuelve
/// invulnerable y el suelo tiembla mientras carga; en el estallido, cámara lenta, destello, una
/// onda que aparta al jugador (sin daño) y el cuerpo cambia de color. La IA de cada jefe decide
/// cuándo ocurre y con qué animación; este componente pone el efecto. También tiñe el cuerpo en
/// otros momentos (el Demonio caído se apaga). Ver INC-489.
[DisallowMultipleComponent]
public sealed class TransicionDeFaseDeJefe : MonoBehaviour
{
    [Tooltip("Efecto de onda en el suelo en el estallido.")]
    [SerializeField] private GameObject vfxOnda;
    [SerializeField] private float radioOnda = 7f;
    [Tooltip("Fuerza con la que la onda aparta al jugador. No hace daño.")]
    [SerializeField] private float empuje = 9f;
    [Tooltip("Segundos invulnerable desde que empieza a cargar.")]
    [SerializeField] private float invulnerabilidad = 2.2f;
    [Tooltip("Color que multiplica el cuerpo en cada fase (1, 2, 3...), para que el cambio se vea.")]
    [SerializeField] private Color[] tintePorFase = { Color.white, new Color(1f, 0.8f, 0.68f), new Color(1f, 0.55f, 0.5f) };

    private Damageable _vida;

    void Awake() => _vida = GetComponent<Damageable>();

    /// Principio del cambio: invulnerable y el suelo empieza a temblar.
    public void Cargar()
    {
        if (_vida) _vida.GrantInvulnerability(invulnerabilidad);
        FeedbackService.CameraShake(0.25f, 0.7f);
    }

    /// El golpe del cambio. 'fase' empieza en 0; 'ultima' = la fase final (destello rojo y más fuerte).
    public void Estallar(int fase, bool ultima)
    {
        FeedbackService.HitStop(0.25f, 0.45f);
        FeedbackService.CameraShake(ultima ? 1f : 0.7f, 0.6f);
        FeedbackService.ScreenFlash(ultima ? new Color(0.7f, 0f, 0f, 0.4f) : new Color(1f, 0.55f, 0.1f, 0.3f), 0.3f);

        Vector3 centro = transform.position;
        if (vfxOnda && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxOnda, centro, Quaternion.identity, 3f);

        EmpujarAlJugador(centro);
        Tintar(TinteDeFase(fase), apagarEmision: false);
    }

    public Color TinteDeFase(int fase)
        => tintePorFase != null && tintePorFase.Length > 0
            ? tintePorFase[Mathf.Clamp(fase, 0, tintePorFase.Length - 1)]
            : Color.white;

    /// Multiplica el color de todos los materiales del cuerpo (y opcionalmente apaga su emisión)
    /// con un MaterialPropertyBlock, siempre a partir del color original del material: no se
    /// acumula y nunca toca los materiales compartidos del prefab. Cubre _Color (Standard) y
    /// _BaseColor (URP); HasProperty() lo hace inocuo si el shader no los usa.
    public void Tintar(Color multiplicador, bool apagarEmision)
    {
        multiplicador.a = 1f;
        var renderers = GetComponentsInChildren<Renderer>(true);
        var mpb = new MaterialPropertyBlock();

        foreach (var r in renderers)
        {
            if (!r || !r.sharedMaterial || r is ParticleSystemRenderer) continue;

            r.GetPropertyBlock(mpb);
            if (r.sharedMaterial.HasProperty("_Color"))
                mpb.SetColor("_Color", r.sharedMaterial.GetColor("_Color") * multiplicador);
            if (r.sharedMaterial.HasProperty("_BaseColor"))
                mpb.SetColor("_BaseColor", r.sharedMaterial.GetColor("_BaseColor") * multiplicador);
            if (apagarEmision && r.sharedMaterial.HasProperty("_EmissionColor"))
                mpb.SetColor("_EmissionColor", Color.black);
            r.SetPropertyBlock(mpb);
        }
    }

    /// La onda aparta al jugador si está cerca. Siempre al jugador, no al objetivo del jefe (que
    /// puede ser un señuelo).
    private void EmpujarAlJugador(Vector3 centro)
    {
        if (empuje <= 0f || !PlayerService.TryGetPlayer(out var jugador) || jugador == null) return;

        Vector3 fuera = jugador.transform.position - centro;
        fuera.y = 0f;
        if (fuera.sqrMagnitude > radioOnda * radioOnda) return;
        if (fuera.sqrMagnitude < 0.01f) fuera = -jugador.transform.forward;

        var salud = jugador.GetComponent<PlayerHealthSystem>();
        if (salud != null) salud.Empujar(fuera.normalized, empuje);
    }
}
