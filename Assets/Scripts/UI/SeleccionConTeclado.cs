using Sendero.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Que las flechas, WASD, la cruceta o el stick funcionen siempre en un menú: si no hay nada
/// elegido dentro de él (el ratón lo soltó, se abrió sin foco), la primera pulsación de dirección
/// elige el último control que tuvo el foco aquí o, si no, el primero. Va en la raíz del menú.
/// </summary>
[DisallowMultipleComponent]
public sealed class SeleccionConTeclado : MonoBehaviour
{
    [Tooltip("Control que se elige si aún no se eligió ninguno (vacío: el primero activo).")]
    [SerializeField] private Selectable primero;

    private GameObject _ultimo;

    public void Configurar(Selectable primerControl) => primero = primerControl;

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;
        var elegido = es.currentSelectedGameObject;
        if (elegido != null && elegido.activeInHierarchy && elegido.transform.IsChildOf(transform)) { _ultimo = elegido; return; }
        if (elegido != null && elegido.activeInHierarchy) return; // el foco está en otro menú abierto
        if (EntradaDelMenu.Horizontal() == 0 && EntradaDelMenu.Vertical() == 0) return;

        GameObject destino = null;
        if (_ultimo != null && _ultimo.activeInHierarchy && _ultimo.transform.IsChildOf(transform)) destino = _ultimo;
        else if (primero != null && primero.IsActive() && primero.IsInteractable()) destino = primero.gameObject;
        else
            foreach (var s in GetComponentsInChildren<Selectable>())
                if (s.IsActive() && s.IsInteractable() && s.navigation.mode != Navigation.Mode.None) { destino = s.gameObject; break; }
        if (destino != null) es.SetSelectedGameObject(destino);
    }
}
