using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista de la barra de vida de un jefe: el prefab Resources/UI/BarraDeJefe, con la misma pastilla
/// del HUD del jugador (hp_bar_bg + hp_bar_fill) teñida de rojo coral para distinguirla. Solo
/// pinta; la lógica (vida, fases, menús) está en BossHealthBar. El prefab lo construye
/// BarraDeJefeBuilder. Ver INC-645.
/// </summary>
public sealed class BarraDeJefeUI : MonoBehaviour
{
    public const string RutaEnResources = "UI/BarraDeJefe";

    [Header("Piezas")]
    [SerializeField] private CanvasGroup grupo;
    [SerializeField] private RectTransform contenedor;
    [SerializeField] private Image relleno;
    [SerializeField] private TMP_Text nombre;
    [SerializeField] private TMP_Text vida;
    [Tooltip("Capa con el mismo rectángulo que el relleno donde se colocan las marcas de fase.")]
    [SerializeField] private RectTransform capaDeMarcas;
    [Tooltip("Marca de fase de muestra (inactiva); se clona una por cada fase siguiente del jefe.")]
    [SerializeField] private Image plantillaDeMarca;

    [Header("Aspecto")]
    [Tooltip("Tinte del relleno dorado mientras el jefe tiene vida de sobra.")]
    [SerializeField] private Color tinteNormal = new Color(1f, 0.55f, 0.5f, 1f);
    [Tooltip("Tinte del relleno cuando al jefe le queda poca vida.")]
    [SerializeField] private Color tinteCritico = new Color(1f, 0.32f, 0.32f, 1f);
    [Tooltip("Color al que destella el relleno al recibir un golpe.")]
    [SerializeField] private Color destelloDano = Color.white;
    [Tooltip("Color al que destella el relleno cuando el jefe se cura.")]
    [SerializeField] private Color destelloCuracion = new Color(0.55f, 1f, 0.6f, 1f);
    [Tooltip("Color de la marca de una fase que aún no ha empezado.")]
    [SerializeField] private Color marcaPendiente = new Color(0.106f, 0.086f, 0.180f, 1f);
    [Tooltip("Color de la marca de una fase ya superada.")]
    [SerializeField] private Color marcaSuperada = new Color(0.106f, 0.086f, 0.180f, 0.3f);

    private Color _tinte;
    private Tween _fundido;
    private readonly List<Image> _marcas = new List<Image>();

    /// Una barra nueva sacada del prefab. Null si el prefab no existe (falta ejecutar el menú
    /// que lo crea).
    public static BarraDeJefeUI Crear()
    {
        var prefab = Resources.Load<BarraDeJefeUI>(RutaEnResources);
        if (prefab == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[BarraDeJefeUI] Falta Resources/" + RutaEnResources +
                             ": ejecuta «El Sendero/UI/Crear o regenerar la barra de jefe (INC-645)».");
#endif
            return null;
        }
        var barra = Instantiate(prefab);
        barra.name = prefab.name;
        barra._tinte = barra.tinteNormal;
        barra.relleno.color = barra._tinte;
        barra.grupo.alpha = 0f;
        return barra;
    }

    void OnDestroy()
    {
        _fundido?.Kill();
        if (relleno) relleno.DOKill();
        if (contenedor) contenedor.DOKill();
    }

    public void PonerNombre(string texto) => nombre.text = texto;

    public void PonerRelleno(float fraccion) => relleno.fillAmount = fraccion;

    /// Texto «actual / máx» y tinte según si la vida está en zona crítica.
    public void PonerVida(float actual, float maximo, bool critico)
    {
        vida.text = $"{Mathf.Ceil(actual)} / {maximo}";
        Color tinte = critico ? tinteCritico : tinteNormal;
        if (tinte == _tinte) return;
        _tinte = tinte;
        relleno.DOKill();
        relleno.color = _tinte;
    }

    public void DestelloDano() => Destello(destelloDano, 0.15f);

    public void DestelloCuracion() => Destello(destelloCuracion, 0.2f);

    private void Destello(Color color, float vuelta)
    {
        relleno.DOKill();
        relleno.color = color;
        relleno.DOColor(_tinte, vuelta).SetDelay(0.06f).SetUpdate(true);
    }

    /// Sacudida de la barra al empezar una fase nueva.
    public void GolpeDeFase()
    {
        contenedor.DOKill(true);
        contenedor.DOPunchScale(Vector3.one * 0.15f, 0.6f, 6).SetUpdate(true);
    }

    /// Una raya por cada fase siguiente, justo en el porcentaje de vida en que empieza: el
    /// jugador ve venir el cambio.
    public void CrearMarcas(IReadOnlyList<float> umbrales, int faseActual)
    {
        if (umbrales == null) return;
        for (int i = 0; i < umbrales.Count; i++)
        {
            var marca = Instantiate(plantillaDeMarca, capaDeMarcas);
            marca.name = $"MarcaFase_{i + 2}";
            var rt = marca.rectTransform;
            float x = Mathf.Clamp01(umbrales[i]);
            rt.anchorMin = new Vector2(x, 0f);
            rt.anchorMax = new Vector2(x, 1f);
            rt.anchoredPosition = Vector2.zero;
            marca.color = faseActual > i ? marcaSuperada : marcaPendiente;
            marca.gameObject.SetActive(true);
            _marcas.Add(marca);
        }
    }

    public void MarcarSuperada(int indice)
    {
        if (indice >= 0 && indice < _marcas.Count && _marcas[indice])
            _marcas[indice].color = marcaSuperada;
    }

    public void Fundido(float alfa, float duracion, float retraso = 0f)
    {
        _fundido?.Kill();
        _fundido = grupo.DOFade(alfa, duracion)
            .SetDelay(retraso)
            .SetEase(alfa > 0f ? Ease.OutCubic : Ease.InCubic)
            .SetUpdate(true);
    }
}
