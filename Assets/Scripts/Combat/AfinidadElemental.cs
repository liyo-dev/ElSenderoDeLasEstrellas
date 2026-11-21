using System;
using UnityEngine;

/// Cómo le sienta a una criatura un elemento: igual, le duele más (débil) o le hace menos
/// (resiste). Nunca hay inmunidad: lo peor es la resistencia.
public enum AfinidadAlElemento
{
    Normal = 0,
    Debil = 1,
    Resiste = 2
}

/// <summary>
/// Debilidades y resistencias elementales de una criatura (enemigos, bestias, jefes). Va junto al
/// <see cref="Damageable"/>; quien no lo lleva (las personas, los NPCs del combate) recibe la magia
/// igual de cualquier elemento. Los multiplicadores son los de <see cref="FormulasDeCombate"/>.
///
/// El golpe con elemento entra por <see cref="Golpear"/> (proyectiles, bolas devueltas y zonas):
/// multiplica el daño antes de las reglas de daño del propio objeto (<see cref="IFiltroDeDano"/>),
/// así que la debilidad suma a las mecánicas de un jefe y no las sustituye: si el golpe no entra,
/// no hay señal ni descubrimiento. Si entra con debilidad o resistencia, lo enseña (destello del
/// color del elemento, sonido y efecto, de <see cref="SenalesDeAfinidad"/>) y lo apunta en
/// <see cref="CriaturasConocidas"/> para el Grimorio. El destello lo pone <see cref="ReaccionAlGolpe"/>
/// si el objeto lo tiene (lee <see cref="GolpeEnCurso"/>); si no, este componente.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class AfinidadElemental : MonoBehaviour
{
    [Tooltip("Clave de la criatura en los textos del juego (su nombre). Es también la clave con la que el Grimorio apunta lo descubierto; varias variantes de una misma criatura comparten clave.")]
    [SerializeField] private string claveDeCriatura = "";
    [Tooltip("Elementos que le duelen más (daño × FormulasDeCombate.MultiplicadorDebil).")]
    [SerializeField] private MagicElement[] debilidades = Array.Empty<MagicElement>();
    [Tooltip("Elementos que le hacen menos (daño × FormulasDeCombate.MultiplicadorResiste). Solo en casos concretos.")]
    [SerializeField] private MagicElement[] resistencias = Array.Empty<MagicElement>();

    /// Una criatura ha recibido un golpe que entra con debilidad o resistencia: quién, el elemento y cómo le sienta.
    public static event Action<AfinidadElemental, MagicElement, AfinidadAlElemento> AlGolpeConAfinidad;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AlGolpeConAfinidad = null;
#endif

    /// Golpe elemental que se está aplicando ahora mismo a esta criatura (dentro de <see cref="Golpear"/>).
    public readonly struct Golpe
    {
        public readonly bool activo;
        public readonly MagicElement elemento;
        public readonly AfinidadAlElemento afinidad;
        public Golpe(MagicElement elemento, AfinidadAlElemento afinidad) { activo = true; this.elemento = elemento; this.afinidad = afinidad; }
    }

    public string ClaveDeCriatura => string.IsNullOrEmpty(claveDeCriatura) ? name : claveDeCriatura;

    /// El golpe que está entrando ahora (solo mientras dura el TakeDamage de <see cref="Golpear"/>).
    public Golpe GolpeEnCurso { get; private set; }

    private Damageable _vida;
    private ReaccionAlGolpe _reaccion;
    private DestelloDelCuerpo _destello;
    private float _destelloHasta = -1f;
    private float _ultimaSenal = -999f;

    void Awake()
    {
        _vida = GetComponent<Damageable>();
        _reaccion = GetComponent<ReaccionAlGolpe>();
        if (_reaccion == null) _destello = new DestelloDelCuerpo(gameObject);
        _vida.OnDamagedBy += AlRecibirDano;
        // Solo se enciende (Update) mientras dura un destello propio.
        enabled = false;
    }

    void OnDestroy()
    {
        if (_vida != null) _vida.OnDamagedBy -= AlRecibirDano;
    }

    void OnDisable()
    {
        _destello?.Quitar();
        _destelloHasta = -1f;
    }

    void Update()
    {
        if (_destelloHasta > 0f && Time.time < _destelloHasta) return;
        _destello?.Quitar();
        _destelloHasta = -1f;
        enabled = false;
    }

    /// Cómo le sienta este elemento.
    public AfinidadAlElemento Contra(MagicElement elemento)
    {
        if (Contiene(debilidades, elemento)) return AfinidadAlElemento.Debil;
        if (Contiene(resistencias, elemento)) return AfinidadAlElemento.Resiste;
        return AfinidadAlElemento.Normal;
    }

    /// Elementos que le duelen más y que le hacen menos (para el Grimorio y las herramientas).
    public MagicElement[] Debilidades => debilidades;
    public MagicElement[] Resistencias => resistencias;

    /// La afinidad del que recibe el golpe (en su objeto o en un padre). Null = no es una criatura.
    public static AfinidadElemental De(Damageable receptor)
    {
        if (receptor == null) return null;
        var a = receptor.GetComponent<AfinidadElemental>();
        return a != null ? a : receptor.GetComponentInParent<AfinidadElemental>();
    }

    /// <summary>
    /// Aplica un golpe con elemento: con la afinidad de la criatura si la tiene, tal cual si no.
    /// <paramref name="dano"/> llega ya con el ataque de quien lo lanzó.
    /// </summary>
    public static void Golpear(Damageable receptor, float dano, GameObject instigador, MagicElement elemento)
    {
        if (receptor == null) return;
        var afinidad = De(receptor);
        if (afinidad == null) { receptor.TakeDamage(dano, instigador); return; }

        var tipo = afinidad.Contra(elemento);
        dano *= FormulasDeCombate.MultiplicadorElemental(tipo);
        afinidad.GolpeEnCurso = new Golpe(elemento, tipo);
        try { receptor.TakeDamage(dano, instigador); }
        finally { afinidad.GolpeEnCurso = default; }
    }

    private void AlRecibirDano(float cantidad, GameObject quien)
    {
        var golpe = GolpeEnCurso;
        if (!golpe.activo || golpe.afinidad == AfinidadAlElemento.Normal) return;

        CriaturasConocidas.Apuntar(ClaveDeCriatura, golpe.elemento, golpe.afinidad);
        AlGolpeConAfinidad?.Invoke(this, golpe.elemento, golpe.afinidad);

        var senales = SenalesDeAfinidad.Tabla;
        if (Time.time < _ultimaSenal + senales.separacionEntreSenales || !gameObject.activeInHierarchy) return;
        _ultimaSenal = Time.time;

        var senal = senales.De(golpe.elemento, golpe.afinidad);
        Vector3 centro = transform.position + Vector3.up * senales.alturaDelEfecto;
        if (senal.vfx != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(senal.vfx, centro, Quaternion.identity, Mathf.Max(0.1f, senales.vidaDelEfecto));
        if (!string.IsNullOrEmpty(senal.sonido))
            AudioService.Instance?.PlaySFX(senal.sonido, senal.volumen, centro);
        if (golpe.afinidad == AfinidadAlElemento.Debil && senales.pausaEnLaDebilidad > 0f)
            Sendero.Core.Feedback.FeedbackService.HitStop(senales.escalaDeTiempoEnLaPausa, senales.pausaEnLaDebilidad);

        // Con ReaccionAlGolpe, el destello lo pone él con este color (lee GolpeEnCurso).
        if (_destello != null)
        {
            _destello.Poner(senal.destello);
            _destelloHasta = Time.time + senal.duracionDelDestello;
            enabled = true;
        }
    }

    private static bool Contiene(MagicElement[] lista, MagicElement e)
    {
        if (lista == null) return false;
        for (int i = 0; i < lista.Length; i++) if (lista[i] == e) return true;
        return false;
    }
}
