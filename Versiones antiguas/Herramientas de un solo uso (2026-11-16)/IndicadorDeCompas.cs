using Kits.Puzzles;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El compás en pantalla, para el blockout: cuatro puntos que se encienden por turno (1-2-3-4) y el
/// golpe fuerte (PUM) más grande y de otro color. Sirve para comprobar de un vistazo si el nivel va
/// con el reloj. Es una ayuda de montaje: se retira cuando el nivel se vista.
/// </summary>
public sealed class IndicadorDeCompas : MonoBehaviour
{
    [Tooltip("Reloj del tramo. Si se deja vacío, se usa el de la escena.")]
    [SerializeField] private TempoClock pulse;
    [SerializeField] private int tiempos = 4;
    [Tooltip("Cada cuántos tiempos cae el golpe fuerte (2 = PUM en 1 y 3).")]
    [SerializeField, Min(1)] private int fuerteCada = 2;
    [SerializeField, Min(6f)] private float lado = 16f;
    [SerializeField, Min(0f)] private float separacion = 10f;
    [SerializeField] private Color colorDelTiempo = new Color(1f, 0.95f, 0.85f, 0.9f);
    [SerializeField] private Color colorDelFuerte = new Color(1f, 0.45f, 0.1f, 1f);
    [SerializeField] private Color colorApagado = new Color(1f, 1f, 1f, 0.18f);

    private Image[] _puntos;
    private TempoClock _pulse;
    private float _siguienteEnganche;

    private void Start()
    {
        Construir();
        Enlazar();
    }

    private void OnDisable()
    {
        if (_pulse != null) _pulse.Beat -= AlPasarUnTiempo;
        _pulse = null;
    }

    private void Update()
    {
        if (_pulse == null && Time.time >= _siguienteEnganche)
        {
            _siguienteEnganche = Time.time + 0.5f;
            Enlazar();
        }
        // Se apagan solos a mitad de tiempo: si no, el punto se queda encendido hasta el siguiente.
        if (_pulse == null || !_pulse.IsRunning || _puntos == null) return;
        float apagadoDesde = 1f - 0.55f;
        if (_pulse.BeatPhase01 < apagadoDesde) return;
        for (int i = 0; i < _puntos.Length; i++)
            if (_puntos[i] != null) _puntos[i].color = colorApagado;
    }

    private void Enlazar()
    {
        _pulse = pulse != null ? pulse : TempoClock.Current;
        if (_pulse == null) return;
        _pulse.Beat -= AlPasarUnTiempo;
        _pulse.Beat += AlPasarUnTiempo;
    }

    private void Construir()
    {
        var lienzo = new GameObject("Compás en pantalla (lienzo)").AddComponent<Canvas>();
        lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
        lienzo.sortingOrder = 4000;
        lienzo.transform.SetParent(transform, false);

        var fila = new GameObject("Puntos").AddComponent<RectTransform>();
        fila.SetParent(lienzo.transform, false);
        fila.anchorMin = new Vector2(0.5f, 1f);
        fila.anchorMax = new Vector2(0.5f, 1f);
        fila.pivot = new Vector2(0.5f, 1f);
        fila.anchoredPosition = new Vector2(0f, -46f);
        fila.sizeDelta = new Vector2(400f, 40f);

        var montaje = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        montaje.childAlignment = TextAnchor.MiddleCenter;
        montaje.spacing = separacion;
        montaje.childForceExpandWidth = false;
        montaje.childForceExpandHeight = false;
        montaje.childControlWidth = false;
        montaje.childControlHeight = false;

        _puntos = new Image[Mathf.Max(1, tiempos)];
        for (int i = 0; i < _puntos.Length; i++)
        {
            var go = new GameObject($"Tiempo {i + 1}");
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(fila, false);
            bool fuerte = i % Mathf.Max(1, fuerteCada) == 0;
            float tam = fuerte ? lado * 1.5f : lado;
            rect.sizeDelta = new Vector2(tam, tam);

            var imagen = go.AddComponent<Image>();
            imagen.color = colorApagado;
            imagen.raycastTarget = false;
            _puntos[i] = imagen;
        }
    }

    private void AlPasarUnTiempo(int compas, int tiempo)
    {
        if (_puntos == null) return;
        for (int i = 0; i < _puntos.Length; i++)
        {
            if (_puntos[i] == null) continue;
            bool encendido = i == tiempo;
            bool fuerte = i % Mathf.Max(1, fuerteCada) == 0;
            _puntos[i].color = encendido ? (fuerte ? colorDelFuerte : colorDelTiempo) : colorApagado;
        }
    }
}
