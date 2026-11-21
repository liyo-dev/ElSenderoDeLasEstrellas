using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sendero.UI
{
    /// <summary>
    /// Hace clicable una imagen sin que el EventSystem la seleccione: el menú de Start lleva su
    /// propia navegación y una selección del EventSystem despertaría los efectos de foco de otros
    /// menús (empujar el texto del botón elegido, cursores). Avisa también al pasar el ratón por
    /// encima, para que el menú mueva ahí su foco.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClicSinFoco : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public event Action AlPulsar;
        public event Action AlPasar;

        public void OnPointerEnter(PointerEventData eventData) => AlPasar?.Invoke();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button == PointerEventData.InputButton.Left) AlPulsar?.Invoke();
        }
    }
}
