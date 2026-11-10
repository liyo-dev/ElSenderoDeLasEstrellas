using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sendero.UI
{
    /// <summary>
    /// Convierte una barra de relleno (Image «Filled») en una barra apilada por capas
    /// (<see cref="EstiloDeBarrasApiladas"/>): debajo pone una copia con el color de la capa
    /// anterior y al lado el «×N». Quien la usa sigue moviendo el fillAmount del relleno, pero con
    /// la parte llena de la capa de arriba (<see cref="ParteDeArriba"/>) en vez de actual / máximo.
    /// </summary>
    public sealed class BarraApilada
    {
        private readonly Image _relleno;
        private readonly Image _debajo;
        private readonly TextMeshProUGUI _numero;
        private readonly Color[] _colores;
        private readonly EstiloDeBarrasApiladas _estilo;

        /// Capas que se están enseñando ahora (-1 antes de la primera vez).
        public int Capas { get; private set; } = -1;

        public BarraApilada(Image relleno, Color[] colores, TMP_FontAsset fuente, float tamanoDelNumero)
        {
            _estilo = EstiloDeBarrasApiladas.Actual;
            _relleno = relleno;
            _colores = colores;

            var blanco = SpriteEnBlanco.De(relleno.sprite);
            if (blanco != null) relleno.sprite = blanco;

            var original = relleno.rectTransform;
            var go = new GameObject("Capa de debajo", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(original.parent, false);
            rt.SetSiblingIndex(original.GetSiblingIndex());
            rt.anchorMin = original.anchorMin;
            rt.anchorMax = original.anchorMax;
            rt.pivot = original.pivot;
            rt.anchoredPosition = original.anchoredPosition;
            rt.sizeDelta = original.sizeDelta;
            rt.localRotation = original.localRotation;
            _debajo = go.GetComponent<Image>();
            _debajo.sprite = relleno.sprite;
            _debajo.type = relleno.type;
            _debajo.fillMethod = relleno.fillMethod;
            _debajo.fillOrigin = relleno.fillOrigin;
            _debajo.fillAmount = 1f;
            _debajo.preserveAspect = relleno.preserveAspect;
            _debajo.raycastTarget = false;
            go.SetActive(false);

            var numero = new GameObject("Capas (×N)", typeof(RectTransform));
            var rtNumero = (RectTransform)numero.transform;
            rtNumero.SetParent(original, false);
            rtNumero.anchorMin = rtNumero.anchorMax = new Vector2(1f, 0.5f);
            rtNumero.pivot = new Vector2(0f, 0.5f);
            rtNumero.anchoredPosition = new Vector2(tamanoDelNumero * 0.35f, 0f);
            rtNumero.sizeDelta = new Vector2(tamanoDelNumero * 2.6f, tamanoDelNumero * 1.4f);
            _numero = numero.AddComponent<TextMeshProUGUI>();
            _numero.font = fuente != null ? fuente : TMP_Settings.defaultFontAsset;
            _numero.fontSize = tamanoDelNumero;
            _numero.fontStyle = FontStyles.Bold;
            _numero.color = _estilo.colorDelNumero;
            _numero.alignment = TextAlignmentOptions.MidlineLeft;
            _numero.textWrappingMode = TextWrappingModes.NoWrap;
            _numero.raycastTarget = false;
            numero.SetActive(false);
        }

        public int CapasDe(float valor) => _estilo.Capas(valor);
        public float ParteDeArriba(float valor) => _estilo.ParteDeArriba(valor);

        /// Pone los colores, la capa de debajo y el «×N» para ese número de capas.
        public void PonerCapas(int capas)
        {
            if (capas == Capas) return;
            Capas = capas;
            _relleno.color = EstiloDeBarrasApiladas.ColorDeCapa(_colores, capas - 1);
            bool varias = capas > 1;
            if (_debajo.gameObject.activeSelf != varias) _debajo.gameObject.SetActive(varias);
            if (varias) _debajo.color = EstiloDeBarrasApiladas.ColorDeCapa(_colores, capas - 2);
            if (_numero.gameObject.activeSelf != varias) _numero.gameObject.SetActive(varias);
            if (varias) _numero.text = "×" + capas;
        }

        /// Prepara el relleno para ir a <paramref name="valor"/>: si cambia de capa, lo deja lleno
        /// (al bajar a la de debajo) o vacío (al subir a una nueva) para que la animación salga de
        /// ahí. Devuelve la parte llena de la capa de arriba, que es el fillAmount al que ir.
        public float PrepararPara(float valor)
        {
            int capas = CapasDe(valor);
            int antes = Capas;
            if (antes >= 0 && capas != antes)
                _relleno.fillAmount = capas < antes ? 1f : 0f;
            PonerCapas(capas);
            return ParteDeArriba(valor);
        }
    }
}
