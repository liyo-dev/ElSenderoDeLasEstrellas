using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Bomba oscura conjurada por el Mago Oscuro (estilo Sephiroth en KH2).
/// Flota en el aire, emite un zumbido creciente y pulsa de púrpura a rojo inestable.
/// Si expira su temporizador o alguien del grupo se acerca demasiado, EXPLOTA en un radio amplio
/// y daña a todo el grupo que alcance (DanoEnZona; cada personaje con su vida, GDD § 6.1).
/// Si el jugador le dispara a tiempo con magia, se destruye de forma segura.
/// </summary>
public sealed class BombaOscura : MonoBehaviour
{
    [Header("Configuración")]
    public float tiempoDetonacion = 2.6f;
    public float radioExplosion = 5.0f;
    public float dano = 32f;
    public float empuje = 12f;
    public float radioProximidadInmediata = 1.8f;

    [Header("Efectos")]
    public GameObject vfxExplosion;
    public string sfxExplosion = "SFX_Prologo_Explosion";
    public string sfxZumbido = "SFX_Prologo_AgujeroNegro_Carga";

    private float _creadaEn;
    private bool _detonada;
    private Damageable _damageable;

    // Elevación del empuje de la explosión (0 = horizontal).
    private const float ElevacionDelEmpuje = 0.35f;

    public static BombaOscura Crear(Vector3 posicion, GameObject vfxBomba = null, GameObject vfxExplosion = null)
    {
        var go = new GameObject("BombaOscura");
        go.transform.position = posicion;

        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.8f;
        col.isTrigger = true;

        var bomba = go.AddComponent<BombaOscura>();
        bomba.vfxExplosion = vfxExplosion;

        // Añadir Damageable para que el jugador pueda destruirla con hechizos
        var dmg = go.AddComponent<Damageable>();
        dmg.SetMaxAndCurrent(20f, 20f);
        dmg.SetDestroyOnDeath(false);
        dmg.OnDied += () => bomba.DesactivarPorJugador();
        bomba._damageable = dmg;

        // Visual de la esfera oscura
        if (vfxBomba != null)
        {
            var vfx = Instantiate(vfxBomba, go.transform);
            vfx.transform.localPosition = Vector3.zero;
            vfx.transform.localScale = Vector3.one * 0.8f;
        }
        else
        {
            // Fallback visual: esfera oscura procedural
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Esfera";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * 1.1f;
            Destroy(visual.GetComponent<Collider>());
            var ren = visual.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.12f, 0.04f, 0.22f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(0.6f, 0.1f, 0.9f) * 1.5f);
                }
                ren.sharedMaterial = mat;
            }
        }

        return bomba;
    }

    private void Start()
    {
        _creadaEn = Time.time;

        if (!string.IsNullOrEmpty(sfxZumbido) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxZumbido, 0.8f, transform.position);
    }

    private void Update()
    {
        if (_detonada) return;

        float tiempoTranscurrido = Time.time - _creadaEn;
        float factor = Mathf.Clamp01(tiempoTranscurrido / tiempoDetonacion);

        // Pulso oscilante que se acelera según se acerca la explosión
        float pulso = 1f + 0.2f * Mathf.Sin(tiempoTranscurrido * Mathf.Lerp(4f, 22f, factor));
        transform.localScale = Vector3.one * pulso;

        // Proximidad inmediata: si alguien del grupo se acerca demasiado, explota en el acto
        if (DanoEnZona.AlguienEnEsfera(transform.position, radioProximidadInmediata))
        {
            Detonar();
            return;
        }

        // Fin de cuenta atrás
        if (tiempoTranscurrido >= tiempoDetonacion)
        {
            Detonar();
        }
    }

    public void Detonar()
    {
        if (_detonada) return;
        _detonada = true;

        Vector3 pos = transform.position;

        // Feedback
        FeedbackService.CameraShake(0.55f, 0.35f);
        FeedbackService.Vibrar(0.7f, 0.8f, 0.35f);

        if (vfxExplosion != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxExplosion, pos, Quaternion.identity, 2.5f);

        if (!string.IsNullOrEmpty(sfxExplosion) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(sfxExplosion, 1f, pos);

        DanoEnZona.GolpearEnEsfera(pos, radioExplosion, dano, empuje, ElevacionDelEmpuje);

        Destroy(gameObject);
    }

    private void DesactivarPorJugador()
    {
        if (_detonada) return;
        _detonada = true;

        // Pequeño chisporroteo sin dañar al jugador (recompensa por dispararle)
        if (vfxExplosion != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxExplosion, transform.position, Quaternion.identity, 1.0f);

        FeedbackService.CameraShake(0.15f, 0.15f);
        Destroy(gameObject);
    }
}
