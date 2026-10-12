using UnityEngine;
using UnityEngine.UI;

/// Recorre en bucle una serie de sprites sobre una Image, como un GIF. Sirve para los iconos
/// animados sobre la cabeza (p. ej. la charla de Eldran y Victoria). Ver INC-700.
[RequireComponent(typeof(Image))]
public class IconoAnimado : MonoBehaviour
{
    [Tooltip("Fotogramas en orden; al acabar vuelve al primero.")]
    [SerializeField] private Sprite[] fotogramas;

    [Tooltip("Fotogramas por segundo.")]
    [SerializeField, Min(0.1f)] private float fps = 6f;

    [Tooltip("Usa el tiempo real: el icono sigue animándose aunque el juego vaya a cámara lenta o en pausa.")]
    [SerializeField] private bool tiempoReal = true;

    private Image _imagen;
    private float _inicio;
    private int _actual = -1;

    private float Ahora => tiempoReal ? Time.unscaledTime : Time.time;

    private void Awake()
    {
        _imagen = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _inicio = Ahora;
        _actual = -1;
        Pintar();
    }

    private void Update()
    {
        Pintar();
    }

    private void Pintar()
    {
        if (_imagen == null || fotogramas == null || fotogramas.Length == 0) return;

        int i = Mathf.FloorToInt((Ahora - _inicio) * fps) % fotogramas.Length;
        if (i < 0) i += fotogramas.Length;
        if (i == _actual) return;

        _actual = i;
        _imagen.sprite = fotogramas[i];
    }
}
