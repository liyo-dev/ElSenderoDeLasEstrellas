using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Aspecto de elegido de una ficha de papel (<see cref="PiezasDeUI.Carta"/>) que va con un
/// control: cuando el control tiene el foco o está marcada (la pestaña abierta, la opción puesta),
/// la carta se tiñe de su color de elegida, se gira, se desplaza, su sombra cae más y aparecen
/// los adornos (estrella, insignia del botón). Todo con un paso suave; sin foco ni marca vuelve
/// a su aspecto normal.
/// </summary>
public sealed class FichaElegible : MonoBehaviour
{
    [SerializeField] private Selectable control;
    [SerializeField] private RectTransform carta;
    [SerializeField] private Image relleno;
    [SerializeField] private RectTransform sombra;
    [SerializeField] private TMP_Text texto;
    [SerializeField] private GameObject[] adornos;

    [SerializeField] private Color colorNormal = new Color(0.965f, 0.914f, 0.82f, 1f);
    [SerializeField] private Color colorElegida = new Color(1f, 0.831f, 0.369f, 1f);
    [SerializeField] private Color textoNormal = new Color(0.14f, 0.12f, 0.10f, 1f);
    [SerializeField] private Color textoElegida = new Color(0.14f, 0.12f, 0.10f, 1f);
    [SerializeField] private float giroNormal;
    [SerializeField] private float giroElegida = -1.4f;
    [SerializeField] private Vector2 desplazamientoElegida;
    [SerializeField] private float caidaNormal = 5f;
    [SerializeField] private float caidaElegida = 7f;

    private float _foco = -1f;

    /// Marcada se ve elegida aunque no tenga el foco (la pestaña abierta, la opción puesta).
    public bool Marcada { get; set; }

    /// La que manda en la elección: foco, si sigue el foco; si no, solo la marca.
    public bool SigueElFoco { get; set; } = true;

    public void Configurar(Selectable control, PiezasDeUI.Carta carta, TMP_Text texto, GameObject[] adornos)
    {
        this.control = control;
        this.carta = carta.raiz;
        relleno = carta.relleno;
        sombra = carta.sombra;
        this.texto = texto;
        this.adornos = adornos;
        _foco = -1f;
    }

    public void Colores(Color normal, Color elegida, Color textoNormal, Color textoElegida)
    {
        colorNormal = normal;
        colorElegida = elegida;
        this.textoNormal = textoNormal;
        this.textoElegida = textoElegida;
        _foco = -1f;
    }

    public void Movimiento(float giroNormal, float giroElegida, Vector2 desplazamiento, float caidaNormal, float caidaElegida)
    {
        this.giroNormal = giroNormal;
        this.giroElegida = giroElegida;
        desplazamientoElegida = desplazamiento;
        this.caidaNormal = caidaNormal;
        this.caidaElegida = caidaElegida;
        _foco = -1f;
    }

    private bool Elegida()
    {
        if (Marcada) return true;
        if (!SigueElFoco || control == null || !control.interactable) return false;
        var es = EventSystem.current;
        return es != null && es.currentSelectedGameObject == control.gameObject;
    }

    private void OnEnable() => Aplicar(Elegida() ? 1f : 0f);

    private void Update()
    {
        float objetivo = Elegida() ? 1f : 0f;
        if (Mathf.Approximately(_foco, objetivo)) return;
        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        float f = Mathf.Abs(objetivo - _foco) < 0.01f ? objetivo : Mathf.Lerp(Mathf.Max(0f, _foco), objetivo, k);
        Aplicar(f);
    }

    private void Aplicar(float f)
    {
        _foco = f;
        if (relleno != null)
        {
            var c = Color.Lerp(colorNormal, colorElegida, f);
            if (control != null && !control.interactable) c.a *= 0.55f;
            relleno.color = c;
        }
        if (texto != null) texto.color = Color.Lerp(textoNormal, textoElegida, f);
        if (carta != null)
        {
            carta.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(giroNormal, giroElegida, f));
            carta.anchoredPosition = desplazamientoElegida * f;
        }
        if (sombra != null)
        {
            float caida = Mathf.Lerp(caidaNormal, caidaElegida, f);
            sombra.offsetMin = new Vector2(0f, -caida);
            sombra.offsetMax = new Vector2(0f, -caida);
        }
        if (adornos != null)
            for (int i = 0; i < adornos.Length; i++)
                if (adornos[i] != null && adornos[i].activeSelf != f > 0.5f) adornos[i].SetActive(f > 0.5f);
    }
}
