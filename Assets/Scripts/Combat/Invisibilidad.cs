using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Vuelve invisible a un personaje unos segundos: los enemigos no le ven (quien elige objetivo
/// prefiere a otro visible, el cerebro por puntuación le pierde de vista y le busca donde le vio
/// por última vez, los vigías no le ven) y se le pinta con un material translúcido, sin sombra.
/// Se rompe al atacar o al acabar el tiempo. Quien pregunta usa <see cref="EsInvisible"/>.
/// Lo pone un hechizo de <see cref="MagicKind.Invisibility"/>. Ver INC-1358.
/// </summary>
[DisallowMultipleComponent]
public sealed class Invisibilidad : MonoBehaviour
{
    private static readonly HashSet<Transform> s_invisibles = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_invisibles.Clear();
#endif

    /// <summary>Ese personaje (o el cuerpo al que pertenece) es invisible ahora mismo.</summary>
    public static bool EsInvisible(Transform t)
    {
        if (t == null || s_invisibles.Count == 0) return false;
        foreach (var invisible in s_invisibles)
            if (invisible != null && (t == invisible || t.IsChildOf(invisible))) return true;
        return false;
    }

    /// <summary>Vuelve invisible a 'cuerpo' durante 'segundos' (si ya lo era, renueva el tiempo).</summary>
    public static void Poner(GameObject cuerpo, float segundos, Material material)
    {
        if (cuerpo == null || segundos <= 0f) return;
        if (!cuerpo.TryGetComponent(out Invisibilidad inv)) inv = cuerpo.AddComponent<Invisibilidad>();
        inv.Empezar(segundos, material);
    }

    /// <summary>Le quita la invisibilidad a 'cuerpo', si la tiene.</summary>
    public static void Quitar(GameObject cuerpo)
    {
        if (cuerpo != null && cuerpo.TryGetComponent(out Invisibilidad inv)) inv.Terminar();
    }

    /// <summary>Es invisible ahora.</summary>
    public bool Activa { get; private set; }

    private float _hasta;
    private float _desde;
    private Material _material;
    private readonly List<Renderer> _renderers = new();
    private readonly List<Material[]> _originales = new();
    private readonly List<ShadowCastingMode> _sombras = new();

    // Un ataque que sale en el mismo instante de lanzar la invisibilidad no la rompe.
    private const float GraciaAlEmpezar = 0.3f;

    // Se rompe cuando ataca este cuerpo (rápidos, combos, remates), lo lleve el jugador o la IA.
    private MagicCaster _lanzador;

    private void OnEnable()
    {
        _lanzador = GetComponentInChildren<MagicCaster>(true);
        if (_lanzador == null) _lanzador = GetComponentInParent<MagicCaster>();
        if (_lanzador != null) _lanzador.OnSpellCast += AlLanzar;
    }

    private void OnDisable()
    {
        if (_lanzador != null) _lanzador.OnSpellCast -= AlLanzar;
        _lanzador = null;
        Terminar();
    }

    // Antes de acabarse parpadea (se ve y deja de verse), cada vez más deprisa: avisa de que se
    // va a acabar. Mientras parpadea sigue siendo invisible para los enemigos.
    private const float SegundosDeParpadeo = 1.2f;
    private bool _viendoseAlParpadear;

    private void Update()
    {
        if (!Activa) return;
        float queda = _hasta - Time.time;
        if (queda <= 0f) { Terminar(); return; }
        if (queda > SegundosDeParpadeo || _material == null) return;
        float avance = 1f - queda / SegundosDeParpadeo;          // 0 → 1
        float hz = Mathf.Lerp(4f, 12f, avance);
        bool verse = Mathf.Repeat((SegundosDeParpadeo - queda) * hz, 1f) < 0.4f;
        if (verse != _viendoseAlParpadear) PonerMateriales(verse);
    }

    private void PonerMateriales(bool originales)
    {
        _viendoseAlParpadear = originales;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            if (originales) r.sharedMaterials = _originales[i];
            else
            {
                var nuevos = new Material[_originales[i].Length];
                for (int k = 0; k < nuevos.Length; k++) nuevos[k] = _material;
                r.sharedMaterials = nuevos;
            }
        }
    }

    private void Empezar(float segundos, Material material)
    {
        _hasta = Time.time + segundos;
        if (Activa) { if (_viendoseAlParpadear) PonerMateriales(false); return; }
        _desde = Time.time;
        _material = material;
        Activa = true;
        s_invisibles.Add(transform);
        Pintar();
    }

    private void Terminar()
    {
        if (!Activa) return;
        Activa = false;
        s_invisibles.Remove(transform);
        Despintar();
    }

    private void AlLanzar(MagicSpellSO hechizo, CastHand mano)
    {
        if (!Activa || Time.time - _desde < GraciaAlEmpezar) return;
        if (hechizo != null && hechizo.kind == MagicKind.Invisibility) return;
        Terminar();
    }

    // Translúcido y sin sombra: todos los materiales de las mallas pasan al de invisible.
    private void Pintar()
    {
        _renderers.Clear();
        _originales.Clear();
        _sombras.Clear();
        var encontrados = GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < encontrados.Length; i++)
        {
            var r = encontrados[i];
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            _renderers.Add(r);
            _originales.Add(r.sharedMaterials);
            _sombras.Add(r.shadowCastingMode);
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (_material != null)
            {
                var nuevos = new Material[r.sharedMaterials.Length];
                for (int k = 0; k < nuevos.Length; k++) nuevos[k] = _material;
                r.sharedMaterials = nuevos;
            }
        }
    }

    private void Despintar()
    {
        _viendoseAlParpadear = false;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            r.sharedMaterials = _originales[i];
            r.shadowCastingMode = _sombras[i];
        }
        _renderers.Clear();
        _originales.Clear();
        _sombras.Clear();
    }
}
