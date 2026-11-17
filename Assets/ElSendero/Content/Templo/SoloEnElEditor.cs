using UnityEngine;

/// <summary>
/// Marca lo que solo sirve para mirar el blockout desde el editor (los carteles tumbados en el
/// suelo, que se leen en la vista de planta): al entrar en Play se apaga. Los carteles de juego
/// son los que flotan y miran a la cámara.
/// </summary>
[DisallowMultipleComponent]
public sealed class SoloEnElEditor : MonoBehaviour
{
    private void Awake()
    {
        if (Application.isPlaying) gameObject.SetActive(false);
    }
}
