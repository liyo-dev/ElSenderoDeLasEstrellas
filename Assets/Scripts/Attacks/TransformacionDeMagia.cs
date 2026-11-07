using System;
using UnityEngine;

/// <summary>
/// Cómo sale la magia del jugador en un sitio donde no es la de siempre: otro aspecto, otro
/// sonido y, si se pide, sin hacer daño (en Chuchelandia, purpurina y corazoncitos). Solo datos;
/// la aplica <see cref="MagicCaster"/> a los hechizos que lanza mientras alguien la tenga puesta
/// (<see cref="ZonaQueTransformaLaMagia"/>), con una copia en memoria de cada hechizo: el asset
/// del hechizo no cambia y su código no sabe nada de esto.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Combate/Transformación de magia", fileName = "TransformacionDeMagia_")]
public sealed class TransformacionDeMagia : ScriptableObject
{
    [Header("A qué hechizos afecta")]
    [Tooltip("Tipos de hechizo que se transforman.")]
    [SerializeField] private MagicKind[] tipos = { MagicKind.Projectile, MagicKind.Special, MagicKind.Zone };
    [Tooltip("Solo los hechizos de estos elementos. Vacía = de cualquier elemento.")]
    [SerializeField] private MagicElement[] elementos = Array.Empty<MagicElement>();
    [Tooltip("Solo estos hechizos. Vacía = todos los que cumplan lo anterior.")]
    [SerializeField] private MagicSpellSO[] hechizos = Array.Empty<MagicSpellSO>();
    [Tooltip("Solo la magia de ataque: los hechizos sin daño (curas, escudos) salen como siempre.")]
    [SerializeField] private bool soloMagiaDeAtaque = true;

    [Header("Qué cambia")]
    [Tooltip("Sale sin daño, sin empuje, sin estados, sin zona de impacto y sin abrir protecciones. El elemento se conserva: los receptores de magia (dianas, cristales) siguen reaccionando.")]
    [SerializeField] private bool sinDano = true;
    [Tooltip("Efecto en bucle que sustituye a lo que se ve del hechizo en vuelo; va pegado al proyectil o a la zona. Vacío = se ve el de siempre.")]
    [SerializeField] private GameObject visualEnVuelo;
    [Tooltip("Efecto de un solo uso al salir. Vacío = el del hechizo.")]
    [SerializeField] private GameObject vfxAlSalir;
    [Tooltip("Efecto de un solo uso al impactar. Vacío = el del hechizo.")]
    [SerializeField] private GameObject vfxDeImpacto;
    [Tooltip("Efecto de un solo uso al desaparecer sin impactar. Vacío = el del hechizo.")]
    [SerializeField] private GameObject vfxAlDesaparecer;
    [Tooltip("Clave de SFX al lanzar (sustituye al sonido del hechizo). Vacío = el del hechizo.")]
    [SerializeField] private string sfxAlLanzar = "";
    [Tooltip("Clave de SFX al impactar (sustituye a la del hechizo). Vacío = la del hechizo.")]
    [SerializeField] private string sfxDeImpacto = "";

    /// <summary>El hechizo se transforma con esta transformación.</summary>
    public bool Afecta(MagicSpellSO hechizo)
    {
        if (hechizo == null) return false;
        if (Array.IndexOf(tipos, hechizo.kind) < 0) return false;
        if (elementos.Length > 0 && Array.IndexOf(elementos, hechizo.element) < 0) return false;
        if (hechizos.Length > 0 && Array.IndexOf(hechizos, hechizo) < 0) return false;
        if (soloMagiaDeAtaque && hechizo.damage <= 0f) return false;
        return true;
    }

    /// <summary>
    /// Copia en memoria del hechizo con los cambios. Quien la pide la guarda y la destruye
    /// (<see cref="MagicCaster"/> hace una por hechizo y la reutiliza).
    /// </summary>
    public MagicSpellSO CrearVariante(MagicSpellSO hechizo)
    {
        var variante = Instantiate(hechizo);
        variante.name = $"{hechizo.name} ({name})";
        if (sinDano)
        {
            variante.damage = 0f;
            variante.knockbackForce = 0f;
            variante.statusEffect = EstadoDeCombate.Ninguno;
            variante.impactZone = null;
            variante.remateAereo = null;
            variante.abreProteccionSegundos = 0f;
        }
        if (visualEnVuelo != null) variante.visualSustitutoEnVuelo = visualEnVuelo;
        if (vfxAlSalir != null) variante.spawnVFX = vfxAlSalir;
        if (vfxDeImpacto != null) variante.impactVFX = vfxDeImpacto;
        if (vfxAlDesaparecer != null) variante.despawnVFX = vfxAlDesaparecer;
        if (!string.IsNullOrEmpty(sfxAlLanzar))
        {
            variante.castSFXKey = sfxAlLanzar;
            variante.castEvent = null;
            variante.chargeSFXKey = null;
            variante.releaseSFXKey = null;
        }
        if (!string.IsNullOrEmpty(sfxDeImpacto))
        {
            variante.impactSFXKey = sfxDeImpacto;
            variante.impactEvent = null;
        }
        return variante;
    }

#if UNITY_EDITOR
    // Valores de partida al crear el asset: destellos en vuelo, corazones al impactar y confeti al
    // desaparecer, con efectos que ya tiene el proyecto. Se cambian en el Inspector.
    private const string DestellosEnVuelo = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Sparkle/Sparkle_ellow_flying.prefab";
    private const string CorazonesAlImpactar = "Assets/_VFX/Hechizos/VFX_Hechizo_CorazonEstelar_Impacto.prefab";
    private const string ConfetiAlDesaparecer = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Confetti_blast_multicolor.prefab";

    private void Reset()
    {
        visualEnVuelo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DestellosEnVuelo);
        vfxDeImpacto = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CorazonesAlImpactar);
        vfxAlDesaparecer = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ConfetiAlDesaparecer);
    }
#endif
}
