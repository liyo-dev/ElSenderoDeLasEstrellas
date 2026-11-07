using Core;
using UnityEngine;
using UnityEngine.UI;
using static PiezasDeUI;

/// <summary>
/// Ronda de equilibrio: un indicador que las rachas (y un peso hacia un lado, <see cref="sesgo"/>)
/// empujan fuera del centro, y el jugador lo devuelve con el stick o las flechas. Dura
/// <see cref="duracion"/> segundos; sale mejor cuanto más tiempo pasa el indicador en la zona buena.
/// </summary>
public sealed class EquilibrioEnRonda : PruebaDeRonda
{
    [Header("Equilibrio")]
    [SerializeField, Min(1f)] private float duracion = 6f;
    [Tooltip("Fuerza máxima de las rachas que empujan el indicador (anchos de media barra por segundo).")]
    [SerializeField, Min(0f)] private float empuje = 0.9f;
    [Tooltip("Empuje constante hacia un lado (+ derecha, − izquierda): el peso que arrastra.")]
    [SerializeField] private float sesgo = 0.25f;
    [SerializeField, Min(0.2f)] private float segundosPorRacha = 1.1f;
    [Tooltip("Lo que corrige el stick a fondo (anchos de media barra por segundo).")]
    [SerializeField, Min(0.1f)] private float correccion = 1.6f;
    [Tooltip("Mitad del ancho de la zona buena, en fracción de media barra.")]
    [SerializeField, Range(0.05f, 0.9f)] private float zonaBuena = 0.3f;

    private float _x, _racha, _rachaObjetivo, _siguienteRacha, _t, _enZona;
    private bool _dentro;
    private RectTransform _aguja;
    private Image _tiempo;

    public override float Rendimiento => Mathf.Clamp01(_enZona / duracion);

    protected override void Preparar()
    {
        _x = 0f;
        _t = 0f;
        _enZona = 0f;
        _racha = 0f;
        _dentro = true;
        NuevaRacha();

        var fondo = Barra("Barra", 10f, AnchoDeBarra, 46f, new Color(1f, 1f, 1f, 0.85f));
        var zona = Imagen("Zona buena", fondo.transform, Estilo.tarjeta, new Color(Estilo.colorResalte.r, Estilo.colorResalte.g, Estilo.colorResalte.b, 0.55f));
        Colocar(zona.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(AnchoDeBarra * zonaBuena, 46f));
        var aguja = Imagen("Aguja", fondo.transform, Estilo.chip, Estilo.colorTexto);
        _aguja = aguja.rectTransform;
        Colocar(_aguja, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 78f));
        _tiempo = Relleno("Tiempo", Barra("Reloj", -48f, AnchoDeBarra * 0.6f, 10f, new Color(1f, 1f, 1f, 0.25f)), Estilo.colorTextoSuave);
    }

    public override bool Actualizar(float dt)
    {
        _t += dt;
        if (_t >= _siguienteRacha) NuevaRacha();
        _racha = Mathf.MoveTowards(_racha, _rachaObjetivo, empuje * 2f * dt);
        float stick = Mathf.Clamp(GamepadInputReader.Navigation.x, -1f, 1f);
        _x = Mathf.Clamp(_x + (_racha + sesgo + stick * correccion) * dt, -1f, 1f);

        bool dentro = Mathf.Abs(_x) <= zonaBuena;
        if (dentro) _enZona += dt;
        if (dentro != _dentro) Sonar(dentro ? sfxAlAcertar : sfxAlFallar);
        _dentro = dentro;

        _aguja.anchoredPosition = new Vector2(_x * AnchoDeBarra * 0.5f, 0f);
        Llenar(_tiempo, 1f - _t / duracion);
        return _t >= duracion;
    }

    private void NuevaRacha()
    {
        _rachaObjetivo = Random.Range(-empuje, empuje);
        _siguienteRacha = _t + segundosPorRacha * Random.Range(0.7f, 1.3f);
    }
}
