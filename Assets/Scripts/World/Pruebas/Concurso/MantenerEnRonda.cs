using Core;
using UnityEngine;
using UnityEngine.UI;
using static PiezasDeUI;

/// <summary>
/// Ronda de mantener un botón el tiempo justo: mientras se mantiene, la barra se llena; hay que
/// soltar con la barra dentro de la zona marcada. Si se pasa del todo, se desborda. Sale mejor
/// cuanto más cerca de la zona se suelta.
/// </summary>
public sealed class MantenerEnRonda : PruebaDeRonda
{
    private enum Fase { Esperando, Cargando, Hecho }

    [Header("Mantener")]
    [SerializeField] private GamepadInputReader.BotonDeCara boton = GamepadInputReader.BotonDeCara.Sur;
    [Tooltip("Fracción de la barra que se llena por segundo mientras se mantiene.")]
    [SerializeField, Min(0.05f)] private float cargaPorSegundo = 0.45f;
    [SerializeField, Range(0f, 1f)] private float objetivoDesde = 0.7f;
    [SerializeField, Range(0f, 1f)] private float objetivoHasta = 0.85f;
    [Tooltip("Segundos que espera a que se pulse; luego se da por acabada sin carga.")]
    [SerializeField, Min(1f)] private float esperaMaxima = 8f;
    [Tooltip("Segundos que se ve el resultado antes de acabar.")]
    [SerializeField, Min(0f)] private float pausaAlAcabar = 0.6f;

    private Fase _fase;
    private float _carga, _t, _finEn;
    private Image _relleno;
    private Image _glifo;

    public override string Instruccion
    {
        get
        {
            string texto = base.Instruccion;
            return texto.Contains("{0}") ? string.Format(texto, EtiquetaDe(boton)) : texto;
        }
    }

    public override float Rendimiento
    {
        get
        {
            if (_carga <= 0f || _carga >= 1f) return 0f;
            if (_carga >= objetivoDesde && _carga <= objetivoHasta) return 1f;
            float distancia = _carga < objetivoDesde ? objetivoDesde - _carga : _carga - objetivoHasta;
            return Mathf.Clamp01(1f - distancia * 4f);
        }
    }

    protected override void Preparar()
    {
        _fase = Fase.Esperando;
        _carga = 0f;
        _t = 0f;

        var barra = Barra("Barra", 10f, AnchoDeBarra, 46f, new Color(1f, 1f, 1f, 0.85f));
        var zona = Imagen("Zona buena", barra.transform, Estilo.tarjeta, new Color(Estilo.colorResalte.r, Estilo.colorResalte.g, Estilo.colorResalte.b, 0.55f));
        float desde = Mathf.Min(objetivoDesde, objetivoHasta), hasta = Mathf.Max(objetivoDesde, objetivoHasta);
        Colocar(zona.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(((desde + hasta) * 0.5f - 0.5f) * AnchoDeBarra, 0f),
                new Vector2((hasta - desde) * AnchoDeBarra, 46f));
        _relleno = Relleno("Carga", barra, Estilo.colorTexto);
        _glifo = Glifo("Botón", Zona, boton, new Vector2(-AnchoDeBarra * 0.5f - 60f, 10f), 72f);
    }

    public override bool Actualizar(float dt)
    {
        _t += dt;
        switch (_fase)
        {
            case Fase.Esperando:
                if (GamepadInputReader.BotonDeCaraMantenido(boton)) _fase = Fase.Cargando;
                else if (_t >= esperaMaxima) Acabar();
                break;
            case Fase.Cargando:
                _carga = Mathf.Min(1f, _carga + cargaPorSegundo * dt);
                if (_carga >= 1f || !GamepadInputReader.BotonDeCaraMantenido(boton)) Acabar();
                break;
            case Fase.Hecho:
                return _t >= _finEn;
        }
        Llenar(_relleno, _carga);
        if (_glifo != null) _glifo.rectTransform.localScale = Vector3.one * (_fase == Fase.Cargando ? 0.9f : 1f + 0.08f * Mathf.Sin(_t * 6f));
        return false;
    }

    private void Acabar()
    {
        _fase = Fase.Hecho;
        _finEn = _t + pausaAlAcabar;
        Llenar(_relleno, _carga);
        Sonar(Rendimiento >= 1f ? sfxAlAcertar : sfxAlFallar);
    }
}
