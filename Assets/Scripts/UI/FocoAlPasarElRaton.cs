using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Con ratón, pasar por encima de un control le da el foco del EventSystem (el mismo aspecto de
/// elegido que con teclado o mando); el clic lo acepta como siempre. Así ratón, teclado y mando
/// mueven la misma selección.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public sealed class FocoAlPasarElRaton : MonoBehaviour, IPointerEnterHandler
{
    private Selectable _control;

    private void Awake() => _control = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        var es = EventSystem.current;
        if (es == null || _control == null || !_control.IsInteractable() || es.currentSelectedGameObject == gameObject) return;
        es.SetSelectedGameObject(gameObject);
    }
}
