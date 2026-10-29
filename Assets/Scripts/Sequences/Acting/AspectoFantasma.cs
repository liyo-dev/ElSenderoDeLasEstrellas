using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Vuelve translúcido a un personaje (espíritu, alguien que se desvanece o que se materializa) y
/// anima su opacidad. Cambia los materiales de sus mallas por copias Unlit transparentes que
/// conservan la textura y el color del original, teñidas y con brillo (con bloom, un tinte por
/// encima de 1 resplandece). Al restaurar devuelve los materiales originales tal cual.
///
/// No toca la malla ni la animación; los efectos de partículas y líneas del personaje no se tocan.
/// </summary>
[DisallowMultipleComponent]
public class AspectoFantasma : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private struct Original { public Renderer renderer; public Material[] materiales; public bool sinDibujar; }

    private readonly List<Original> _originales = new();
    private readonly List<Material> _fantasmas = new();
    private readonly List<Color> _coloresBase = new();
    private Color _tinte = Color.white;
    private float _opacidad, _opacidadObjetivo, _velocidad;
    private bool _aplicado;

    public bool Aplicado => _aplicado;
    public float Opacidad => _opacidad;

    /// <summary>
    /// Pone el aspecto translúcido en <paramref name="raiz"/> con la opacidad inicial dada y lo
    /// devuelve para animarlo. <paramref name="aditivo"/>: suma luz en vez de tapar (más etéreo).
    /// </summary>
    public static AspectoFantasma Aplicar(Transform raiz, Color tinte, float opacidadInicial, bool aditivo = false)
    {
        if (raiz == null) return null;
        var aspecto = raiz.GetComponent<AspectoFantasma>();
        if (aspecto == null) aspecto = raiz.gameObject.AddComponent<AspectoFantasma>();
        aspecto.Poner(tinte, opacidadInicial, aditivo);
        return aspecto;
    }

    /// <summary>Quita el aspecto translúcido de <paramref name="raiz"/> si lo tenía.</summary>
    public static void Quitar(Transform raiz)
    {
        if (raiz == null) return;
        var aspecto = raiz.GetComponent<AspectoFantasma>();
        if (aspecto != null) aspecto.Restaurar();
    }

    /// <summary>Lleva la opacidad a <paramref name="objetivo"/> en <paramref name="segundos"/>.</summary>
    public void FundirA(float objetivo, float segundos)
    {
        _opacidadObjetivo = Mathf.Clamp01(objetivo);
        _velocidad = segundos > 0f ? Mathf.Abs(_opacidadObjetivo - _opacidad) / segundos : 0f;
        if (_velocidad <= 0f) { _opacidad = _opacidadObjetivo; AplicarOpacidad(); }
    }

    /// <summary>Devuelve los materiales originales y el «no dibujar» que tenía cada malla.</summary>
    public void Restaurar()
    {
        if (!_aplicado) return;
        for (int i = 0; i < _originales.Count; i++)
        {
            var r = _originales[i].renderer;
            if (r == null) continue;
            r.sharedMaterials = _originales[i].materiales;
            r.forceRenderingOff = _originales[i].sinDibujar;
        }
        for (int i = 0; i < _fantasmas.Count; i++)
            if (_fantasmas[i] != null) Destroy(_fantasmas[i]);
        _originales.Clear();
        _fantasmas.Clear();
        _coloresBase.Clear();
        _aplicado = false;
    }

    private void Poner(Color tinte, float opacidad, bool aditivo)
    {
        Restaurar();
        _tinte = tinte;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            var originales = r.sharedMaterials;
            var nuevos = new Material[originales.Length];
            for (int i = 0; i < originales.Length; i++)
            {
                var m = new Material(shader) { name = "Fantasma_" + (originales[i] != null ? originales[i].name : "sin") };
                Texture tex = null;
                Color color = Color.white;
                if (originales[i] != null)
                {
                    if (originales[i].HasProperty(BaseMapId)) tex = originales[i].GetTexture(BaseMapId);
                    if (tex == null && originales[i].HasProperty(MainTexId)) tex = originales[i].GetTexture(MainTexId);
                    if (originales[i].HasProperty(BaseColorId)) color = originales[i].GetColor(BaseColorId);
                    else if (originales[i].HasProperty(ColorId)) color = originales[i].GetColor(ColorId);
                }
                m.SetTexture(BaseMapId, tex);
                ConfigurarTransparente(m, aditivo);
                nuevos[i] = m;
                _fantasmas.Add(m);
                _coloresBase.Add(new Color(color.r, color.g, color.b, 1f));
            }
            _originales.Add(new Original { renderer = r, materiales = originales, sinDibujar = r.forceRenderingOff });
            r.sharedMaterials = nuevos;
        }

        _aplicado = true;
        _opacidad = _opacidadObjetivo = Mathf.Clamp01(opacidad);
        _velocidad = 0f;
        AplicarOpacidad();
    }

    private static void ConfigurarTransparente(Material m, bool aditivo)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", aditivo ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", aditivo ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        // Escribe profundidad: de cada personaje solo se ve la capa de delante. Sin esto el orden
        // entre mallas transparentes es el de sus centros y la cabeza (grande) se pintaba encima
        // del pelo: se quedaban calvos.
        m.SetFloat("_ZWrite", 1f);
        m.SetFloat("_Cull", (float)CullMode.Back);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    private void Update()
    {
        if (!_aplicado || _velocidad <= 0f) return;
        _opacidad = Mathf.MoveTowards(_opacidad, _opacidadObjetivo, _velocidad * Time.deltaTime);
        AplicarOpacidad();
        if (Mathf.Approximately(_opacidad, _opacidadObjetivo)) _velocidad = 0f;
    }

    private void AplicarOpacidad()
    {
        for (int i = 0; i < _fantasmas.Count; i++)
        {
            if (_fantasmas[i] == null) continue;
            Color c = _coloresBase[i] * _tinte;
            c.a = _opacidad;
            _fantasmas[i].SetColor(BaseColorId, c);
        }
        // Del todo invisible: que no quede ni la silueta por el ZTest.
        for (int i = 0; i < _originales.Count; i++)
            if (_originales[i].renderer != null) _originales[i].renderer.forceRenderingOff = _opacidad <= 0.001f;
    }

    private void OnDestroy() => Restaurar();
}
