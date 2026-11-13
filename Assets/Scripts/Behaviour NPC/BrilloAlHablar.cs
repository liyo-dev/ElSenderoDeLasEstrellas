using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sube el brillo (emisión) de unas piezas del personaje mientras habla y lo devuelve al callar, con un temblor
/// suave mientras dura la frase, como si la voz saliera de la luz (p. ej. unos ojos de luz bajo una capucha).
/// Qué piezas brillan se dice por el final del nombre del material («Blanco» casa con «Mat_Caras_X_Blanco»);
/// el brillo de reposo es el del propio material. No toca los materiales compartidos: va por MaterialPropertyBlock.
/// Escucha <see cref="SenalesDeHabla"/>: vale para la caja de diálogo, los bocadillos y las cinemáticas.
/// </summary>
[DisallowMultipleComponent]
public class BrilloAlHablar : MonoBehaviour
{
    static readonly int EmisionId = Shader.PropertyToID("_EmissionColor");

    [Tooltip("Final del nombre de los materiales que brillan más al hablar.")]
    [SerializeField] string[] materiales = { "Blanco", "Iris", "IrisOscuro", "Negro" };
    [Tooltip("Cuántas veces el brillo de reposo mientras habla.")]
    [SerializeField, Min(1f)] float multiplicadorAlHablar = 1.8f;
    [Tooltip("Cuánto tiembla el brillo mientras habla (fracción del brillo al hablar).")]
    [SerializeField, Range(0f, 0.5f)] float temblor = 0.15f;
    [Tooltip("Veces por segundo que tiembla, como las sílabas.")]
    [SerializeField, Min(0.1f)] float temblorPorSegundo = 7f;
    [Tooltip("Segundos que tarda en encenderse al empezar a hablar.")]
    [SerializeField, Min(0.01f)] float subida = 0.12f;
    [Tooltip("Segundos que tarda en volver al reposo al callar.")]
    [SerializeField, Min(0.01f)] float bajada = 0.5f;

    struct Pieza { public Renderer renderer; public int indice; public Color reposo; }

    readonly List<Pieza> _piezas = new();
    MaterialPropertyBlock _bloque;
    bool _hablando;
    float _hablandoHasta;
    float _factor = 1f;
    bool _enReposo = true;

    void Awake()
    {
        _bloque = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null || !m.HasProperty(EmisionId) || !Casa(m.name)) continue;
                _piezas.Add(new Pieza { renderer = r, indice = i, reposo = m.GetColor(EmisionId) });
            }
        }
    }

    bool Casa(string nombre)
    {
        foreach (var fin in materiales)
            if (!string.IsNullOrEmpty(fin) && (nombre == fin || nombre.EndsWith("_" + fin, System.StringComparison.Ordinal))) return true;
        return false;
    }

    void OnEnable()
    {
        SenalesDeHabla.OnEmpiezaAHablar += AlEmpezar;
        SenalesDeHabla.OnDejaDeHablar += AlParar;
    }

    void OnDisable()
    {
        SenalesDeHabla.OnEmpiezaAHablar -= AlEmpezar;
        SenalesDeHabla.OnDejaDeHablar -= AlParar;
        _hablando = false;
        _factor = 1f;
        Aplicar(1f);
        _enReposo = true;
    }

    bool EsEste(Transform quien) => quien != null && (quien == transform || transform.IsChildOf(quien) || quien.IsChildOf(transform));

    void AlEmpezar(Transform quien, float segundos)
    {
        if (!EsEste(quien)) return;
        _hablando = true;
        // Si nadie avisa del final, se apaga solo al acabar la frase (un poco después).
        _hablandoHasta = segundos > 0f ? Time.time + segundos + 0.3f : float.PositiveInfinity;
        _enReposo = false;
    }

    void AlParar(Transform quien)
    {
        if (!EsEste(quien)) return;
        _hablando = false;
    }

    void Update()
    {
        if (_enReposo || _piezas.Count == 0) return;
        if (_hablando && Time.time > _hablandoHasta) _hablando = false;

        float objetivo = 1f;
        if (_hablando)
        {
            float ruido = Mathf.PerlinNoise(Time.time * temblorPorSegundo, 0.37f) * 2f - 1f;
            objetivo = multiplicadorAlHablar * (1f + temblor * ruido);
        }
        float tiempo = objetivo > _factor ? subida : bajada;
        _factor = Mathf.Lerp(_factor, objetivo, 1f - Mathf.Exp(-Time.deltaTime * 4f / tiempo));

        if (!_hablando && Mathf.Abs(_factor - 1f) < 0.005f)
        {
            _factor = 1f;
            Aplicar(1f);
            _enReposo = true;
            return;
        }
        Aplicar(_factor);
    }

    void Aplicar(float factor)
    {
        if (_bloque == null) return;
        for (int i = 0; i < _piezas.Count; i++)
        {
            var p = _piezas[i];
            if (p.renderer == null) continue;
            p.renderer.GetPropertyBlock(_bloque, p.indice);
            if (factor == 1f) _bloque.Clear();
            else _bloque.SetColor(EmisionId, p.reposo * factor);
            p.renderer.SetPropertyBlock(_bloque, p.indice);
        }
    }
}
