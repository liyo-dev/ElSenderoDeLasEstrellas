using System.Collections.Generic;
using Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static PiezasDeUI;

/// <summary>
/// Ronda de ritmo: los botones llegan por una pista al compás (<see cref="pulsosPorMinuto"/>) y hay
/// que pulsar el indicado cuando pasa por el círculo, con <see cref="ventana"/> segundos de margen.
/// Pulsar otro botón o dejarlo pasar es un fallo. Sale mejor cuantos más aciertos.
/// </summary>
public sealed class RitmoEnRonda : PruebaDeRonda
{
    private enum Estado { Pendiente, Acertada, Fallada }

    private sealed class Nota
    {
        public float tiempo;
        public GamepadInputReader.BotonDeCara boton;
        public Image glifo;
        public Estado estado;
    }

    private static readonly GamepadInputReader.BotonDeCara[] TodosLosBotones =
    {
        GamepadInputReader.BotonDeCara.Sur, GamepadInputReader.BotonDeCara.Este,
        GamepadInputReader.BotonDeCara.Oeste, GamepadInputReader.BotonDeCara.Norte,
    };

    [Header("Ritmo")]
    [SerializeField, Min(30f)] private float pulsosPorMinuto = 96f;
    [SerializeField, Min(1)] private int notas = 8;
    [Tooltip("Pulsos por nota (1 = una en cada pulso, 2 = una de cada dos).")]
    [SerializeField, Min(1)] private int pulsosPorNota = 1;
    [Tooltip("Botones que pueden salir.")]
    [SerializeField] private GamepadInputReader.BotonDeCara[] botones = (GamepadInputReader.BotonDeCara[])TodosLosBotones.Clone();
    [Tooltip("Segundos de margen antes y después del pulso.")]
    [SerializeField, Min(0.05f)] private float ventana = 0.2f;
    [Tooltip("Pulsos de entrada antes de la primera nota.")]
    [SerializeField, Min(0)] private int pulsosDeEntrada = 2;
    [SerializeField, Min(50f)] private float pixelesPorSegundo = 320f;
    [Tooltip("Sonido de cada pulso (el compás). Vacío = sin compás.")]
    [SerializeField] private string sfxDelPulso = "";

    private readonly List<Nota> _notas = new List<Nota>();
    private float _t, _siguientePulso, _fin;
    private int _aciertos;
    private TextMeshProUGUI _cuenta;
    private RectTransform _pista;

    private float Pulso => 60f / pulsosPorMinuto;
    private float XDelCirculo => -AnchoDeBarra * 0.5f + 70f;

    public override float Rendimiento => notas > 0 ? Mathf.Clamp01(_aciertos / (float)notas) : 0f;

    protected override void Preparar()
    {
        _t = 0f;
        _aciertos = 0;
        _siguientePulso = 0f;
        _notas.Clear();

        var pista = Barra("Pista", 10f, AnchoDeBarra, 86f, new Color(1f, 1f, 1f, 0.35f));
        _pista = pista.rectTransform;
        var mascara = pista.gameObject.AddComponent<RectMask2D>();
        mascara.padding = Vector4.zero;
        var circulo = Imagen("Círculo", pista.transform, Estilo.chip, new Color(Estilo.colorResalte.r, Estilo.colorResalte.g, Estilo.colorResalte.b, 0.8f));
        Colocar(circulo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(XDelCirculo, 0f), new Vector2(84f, 84f));

        var lista = botones != null && botones.Length > 0 ? botones : TodosLosBotones;
        for (int i = 0; i < notas; i++)
        {
            var nota = new Nota
            {
                tiempo = (pulsosDeEntrada + i * pulsosPorNota) * Pulso,
                boton = lista[Random.Range(0, lista.Length)],
            };
            nota.glifo = Glifo($"Nota {i + 1}", pista.transform, nota.boton, new Vector2(AnchoDeBarra, 0f), 64f);
            _notas.Add(nota);
        }
        _fin = (_notas.Count > 0 ? _notas[_notas.Count - 1].tiempo : 0f) + ventana + 0.5f;
        _cuenta = Rotulo("Aciertos", "", new Vector2(0f, -58f), 30f);
        Pintar();
    }

    public override bool Actualizar(float dt)
    {
        _t += dt;
        if (!string.IsNullOrEmpty(sfxDelPulso) && _t >= _siguientePulso && _t < _fin)
        {
            Sonar(sfxDelPulso);
            _siguientePulso += Pulso;
        }

        foreach (var boton in TodosLosBotones)
            if (GamepadInputReader.BotonDeCaraPulsado(boton)) Pulsar(boton);

        foreach (var nota in _notas)
        {
            if (nota.estado != Estado.Pendiente || _t - nota.tiempo <= ventana) continue;
            nota.estado = Estado.Fallada;
            Sonar(sfxAlFallar);
        }
        Pintar();
        return _t >= _fin;
    }

    private void Pulsar(GamepadInputReader.BotonDeCara boton)
    {
        foreach (var nota in _notas)
        {
            if (nota.estado != Estado.Pendiente || Mathf.Abs(nota.tiempo - _t) > ventana) continue;
            bool bien = nota.boton == boton;
            nota.estado = bien ? Estado.Acertada : Estado.Fallada;
            if (bien) _aciertos++;
            Sonar(bien ? sfxAlAcertar : sfxAlFallar);
            return;
        }
    }

    private void Pintar()
    {
        foreach (var nota in _notas)
        {
            if (nota.glifo == null) continue;
            var rt = nota.glifo.rectTransform;
            rt.anchoredPosition = new Vector2(XDelCirculo + (nota.tiempo - _t) * pixelesPorSegundo, 0f);
            switch (nota.estado)
            {
                case Estado.Acertada:
                    rt.localScale = Vector3.one * 1.25f;
                    nota.glifo.color = new Color(1f, 1f, 1f, Mathf.Clamp01(nota.glifo.color.a - Time.deltaTime * 3f));
                    break;
                case Estado.Fallada:
                    nota.glifo.color = new Color(0.55f, 0.5f, 0.6f, 0.6f);
                    break;
            }
        }
        if (_cuenta != null) _cuenta.text = $"{_aciertos} / {notas}";
    }
}
