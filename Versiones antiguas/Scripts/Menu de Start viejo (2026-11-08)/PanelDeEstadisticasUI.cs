using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra estadísticas del personaje (total y «(+N)» del equipo), una por fila, y destaca los
/// cambios con un destello y el color de subida o bajada, como el informe de victoria. Cada fila
/// declara qué estadística enseña y explica su efecto en palabras, así que sirve para cualquier panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class PanelDeEstadisticasUI : MonoBehaviour
{
    [Serializable]
    public sealed class Fila
    {
        [Tooltip("Texto de la fila: UnityEngine.UI.Text o TextMeshPro.")]
        public Graphic texto;
        public TipoDeEstadistica tipo;
    }

    [SerializeField] private Fila[] filas = Array.Empty<Fila>();
    [Tooltip("Separación entre el nombre y el valor.")]
    [SerializeField] private string separador = "   ";

    private float[] anteriores;
    private Tween[] animaciones;
    private Vector3[] escalas;
    private bool inicializado;

    private void OnEnable()
    {
        int n = filas.Length;
        if (anteriores == null || anteriores.Length != n)
        {
            anteriores = new float[n];
            animaciones = new Tween[n];
            escalas = new Vector3[n];
        }
        for (int i = 0; i < n; i++)
            if (filas[i]?.texto != null) escalas[i] = filas[i].texto.rectTransform.localScale;
        inicializado = false;
        EstadisticasDelPersonaje.Cambiadas += Refrescar;
        Refrescar();
    }

    private void OnDisable()
    {
        EstadisticasDelPersonaje.Cambiadas -= Refrescar;
        for (int i = 0; i < filas.Length; i++)
        {
            animaciones[i]?.Kill();
            animaciones[i] = null;
            if (filas[i]?.texto != null) filas[i].texto.rectTransform.localScale = escalas[i];
        }
        inicializado = false;
    }

    private void Refrescar()
    {
        var total = EstadisticasDelPersonaje.Total;
        var bonos = EstadisticasDelPersonaje.Bonos;
        for (int i = 0; i < filas.Length; i++)
        {
            var fila = filas[i];
            if (fila?.texto == null) continue;
            float valor = TextoDeEstadisticas.Valor(total, fila.tipo);
            float bono = TextoDeEstadisticas.Valor(bonos, fila.tipo);
            float cambio = valor - anteriores[i];
            bool animar = inicializado && cambio != 0f;
            anteriores[i] = valor;
            // Conserva el destello de otra fila si el aviso cambia una estadística distinta.
            if (!animar && inicializado && animaciones[i] != null && animaciones[i].IsActive()) continue;

            animaciones[i]?.Kill();
            var rt = fila.texto.rectTransform;
            rt.localScale = escalas[i];
            string nombre = TextoDeEstadisticas.Nombre(fila.tipo);
            string sufijo = bono == 0f ? "" : " " + TextoDeEstadisticas.ConColor($"({bono:+0;-0;0})", bono);
            string normal = $"{nombre}{separador}{valor:0}{sufijo}";
            string efecto = TextoDeEstadisticas.Efecto(fila.tipo, valor, corto: true);
            if (!animar) { PonerTexto(fila.texto, normal, efecto); continue; }

            PonerTexto(fila.texto, $"{nombre}{separador}{TextoDeEstadisticas.ConColor(valor.ToString("0"), cambio)}{sufijo}", efecto);
            var grafico = fila.texto;
            int indice = i;
            animaciones[i] = DOTween.Sequence().SetUpdate(true)
                .Append(rt.DOPunchScale(Vector3.one * 0.12f, 0.35f, 4, 0.5f))
                .AppendInterval(0.6f)
                .OnComplete(() => { PonerTexto(grafico, normal, efecto); animaciones[indice] = null; });
        }
        inicializado = true;
    }

    private static string ConEfecto(string texto, string efecto, string tamano) =>
        string.IsNullOrEmpty(efecto) ? texto : $"{texto}\n<size={tamano}>{efecto}</size>";

    private static void PonerTexto(Graphic grafico, string texto, string efecto)
    {
        switch (grafico)
        {
            case Text t:
                t.supportRichText = true;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.text = ConEfecto(texto, efecto, Mathf.RoundToInt(t.fontSize * 0.7f).ToString());
                break;
            case TMP_Text m:
                m.richText = true;
                m.text = ConEfecto(texto, efecto, "70%");
                break;
        }
    }
}
