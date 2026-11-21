using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El símbolo de «guardando»: la estrella de cuatro puntas dorada del título sobre un medallón de
/// papel con borde de tinta, girando y latiendo con un halo detrás. Es el mismo en la esquina
/// (<see cref="IndicadorDeGuardado"/>) y en el aviso del arranque
/// (<see cref="AvisoDeGuardadoAlArrancar"/>), para que el jugador reconozca lo que se le explicó.
/// Se anima solo mientras está encendido, con tiempo sin escalar (sigue en pausa).
/// </summary>
public sealed class SimboloDeGuardado : MonoBehaviour
{
    const float DosPi = Mathf.PI * 2f;

    private RectTransform _estrella;
    private RectTransform _halo;
    private Image _imagenDelHalo;
    private Image _chispa;
    private EstiloDelGuardado _ajustes;
    private float _tiempo;

    /// <summary>Monta el símbolo de <paramref name="lado"/> unidades centrado en su padre.</summary>
    public static SimboloDeGuardado Crear(Transform padre, float lado, EstiloDelGuardado ajustes)
    {
        var raiz = new GameObject("SimboloDeGuardado", typeof(RectTransform)).transform as RectTransform;
        raiz.SetParent(padre, false);
        Centrar(raiz, lado, Vector2.zero);

        var halo = Pieza("Halo", raiz, PiezasDeUI.Resplandor, PaletaUI.Oro, lado * 1.9f, Vector2.zero);
        Pieza("Sombra", raiz, PiezasDeUI.Circulo, PiezasDeUI.SombraDeCarta, lado, new Vector2(0f, -lado * 0.06f));
        Pieza("Borde", raiz, PiezasDeUI.Circulo, PaletaUI.Tinta, lado, Vector2.zero);
        Pieza("Filo", raiz, PiezasDeUI.Circulo, PaletaUI.PapelClaro, lado * 0.93f, Vector2.zero);
        Pieza("Papel", raiz, PiezasDeUI.Circulo, PaletaUI.Papel, lado * 0.86f, Vector2.zero);
        var estrella = Pieza("Estrella", raiz, PiezasDeUI.Estrella, PaletaUI.Oro, lado * 0.74f, Vector2.zero);
        var chispa = Pieza("Chispa", raiz, PiezasDeUI.EstrellaLisa, PaletaUI.PapelClaro, lado * 0.24f, new Vector2(lado * 0.36f, lado * 0.36f));

        var simbolo = raiz.gameObject.AddComponent<SimboloDeGuardado>();
        simbolo._ajustes = ajustes;
        simbolo._estrella = estrella.rectTransform;
        simbolo._halo = halo.rectTransform;
        simbolo._imagenDelHalo = halo;
        simbolo._chispa = chispa;
        simbolo.Pintar();
        return simbolo;
    }

    private static Image Pieza(string nombre, Transform padre, Sprite sprite, Color color, float lado, Vector2 desplazamiento)
    {
        var img = PiezasDeUI.Imagen(nombre, padre, sprite, color);
        img.raycastTarget = false;
        Centrar(img.rectTransform, lado, desplazamiento);
        return img;
    }

    private static void Centrar(RectTransform rt, float lado, Vector2 desplazamiento) =>
        PiezasDeUI.Colocar(rt, new Vector2(0.5f, 0.5f), desplazamiento, new Vector2(lado, lado));

    void OnEnable() => _tiempo = 0f;

    void Update()
    {
        _tiempo += Time.unscaledDeltaTime;
        Pintar();
    }

    private void Pintar()
    {
        if (_estrella == null || _ajustes == null) return;
        // Latido marcado: sube rápido y baja despacio, como un pulso.
        float fase = _ajustes.latidosPorSegundo * _tiempo;
        float pulso = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(fase * DosPi), 3f);
        float escala = 1f + _ajustes.latido * pulso;

        _estrella.localRotation = Quaternion.Euler(0f, 0f, -_ajustes.gradosPorSegundo * _tiempo);
        _estrella.localScale = new Vector3(escala, escala, 1f);

        float escalaDelHalo = 0.85f + 0.25f * pulso;
        _halo.localScale = new Vector3(escalaDelHalo, escalaDelHalo, 1f);
        var oro = PaletaUI.Oro;
        oro.a = 0.25f + 0.4f * pulso;
        _imagenDelHalo.color = oro;

        // La chispa titila a destiempo del latido.
        var chispa = PaletaUI.PapelClaro;
        chispa.a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin((fase + 0.5f) * DosPi * 0.5f));
        _chispa.color = chispa;
    }
}
