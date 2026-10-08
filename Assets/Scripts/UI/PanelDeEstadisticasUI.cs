using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Muestra el total y los bonos y destaca los cambios mientras el equipo está abierto.</summary>
[DisallowMultipleComponent]
public sealed class PanelDeEstadisticasUI : MonoBehaviour
{
    [SerializeField] private Text titulo;
    [SerializeField] private Text[] filas = new Text[4];
    private readonly float[] anteriores = new float[4];
    private readonly Tween[] animaciones = new Tween[4];
    private readonly Vector3[] escalas = new Vector3[4];
    private bool inicializado;

    private void OnEnable()
    {
        EstadisticasDelPersonaje.Cambiadas += Refrescar;
        inicializado = false;
        for (int i = 0; i < 4 && i < filas.Length; i++)
            if (filas[i] != null) escalas[i] = filas[i].rectTransform.localScale;
        Refrescar();
    }

    private void OnDisable()
    {
        EstadisticasDelPersonaje.Cambiadas -= Refrescar;
        for (int i = 0; i < 4 && i < filas.Length; i++)
        {
            animaciones[i]?.Kill();
            animaciones[i] = null;
            if (filas[i] != null) filas[i].rectTransform.localScale = escalas[i];
        }
        inicializado = false;
    }

    private void Refrescar()
    {
        if (titulo != null)
            titulo.text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get("BATTLE_REPORT_STATS", "Estadísticas") : "Estadísticas";
        var total = EstadisticasDelPersonaje.Total;
        var bonos = EstadisticasDelPersonaje.Bonos;
        for (int i = 0; i < 4 && i < filas.Length; i++)
        {
            var texto = filas[i];
            if (texto == null) continue;
            var tipo = (TipoDeEstadistica)i;
            float valor = TextoDeEstadisticas.Valor(total, tipo);
            float bono = TextoDeEstadisticas.Valor(bonos, tipo);
            float cambio = valor - anteriores[i];
            bool animar = inicializado && cambio != 0f;
            // Conserva el destello de otra fila si el evento cambia una estadística distinta.
            if (!animar && inicializado && animaciones[i] != null && animaciones[i].IsActive()) continue;
            animaciones[i]?.Kill();
            texto.rectTransform.localScale = escalas[i];
            texto.supportRichText = true;
            string normal = $"{TextoDeEstadisticas.Nombre(tipo)}: {valor:0}" +
                (bono == 0f ? "" : " " + TextoDeEstadisticas.ConColor($"({bono:+0;-0;0})", bono));
            texto.text = animar
                ? $"{TextoDeEstadisticas.Nombre(tipo)}: {TextoDeEstadisticas.ConColor(valor.ToString("0"), cambio)}" +
                  (bono == 0f ? "" : " " + TextoDeEstadisticas.ConColor($"({bono:+0;-0;0})", bono))
                : normal;
            anteriores[i] = valor;
            if (animar)
            {
                int indice = i;
                animaciones[i] = DOTween.Sequence().SetUpdate(true)
                    .Append(texto.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.3f, 3, 0.5f))
                    .AppendInterval(0.35f)
                    .OnComplete(() => { texto.text = normal; animaciones[indice] = null; });
            }
        }
        inicializado = true;
    }
}