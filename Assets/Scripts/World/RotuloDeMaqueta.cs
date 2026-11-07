using UnityEngine;

/// <summary>
/// Rótulo de maqueta: el texto flotante que nombra marcas y zonas de un nivel montado por menú
/// (<c>KitDeLaberintoDeEspejos.Rotulo</c>). Solo se ve en el Editor fuera de Play: se apaga al
/// entrar en Play, el horneado de guiones lo esconde mientras calcula planos y storyboard (ni sale
/// en las fotos ni cuenta como decorado que tapa) y la etiqueta EditorOnly lo quita de las builds.
/// </summary>
[DisallowMultipleComponent]
public sealed class RotuloDeMaqueta : MonoBehaviour
{
    private void Awake() => gameObject.SetActive(false);
}
