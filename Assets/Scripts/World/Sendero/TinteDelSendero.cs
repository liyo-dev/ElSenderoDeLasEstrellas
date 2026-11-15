using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiñe el resplandor del horizonte del cielo del Sendero según el tramo en el que está el jugador:
/// dorado camino de la puerta de Will, rosa camino de la de Estela, plata fría camino de la de Liam y
/// blanco cerca del altar. Las estrellas del cielo no cambian; solo el resplandor, despacio. Las
/// estrellas que suben (EstrellasQueSuben) siguen el mismo color, algo más claro. Trabaja sobre una
/// copia del material del cielo para no tocar el asset.
/// </summary>
public class TinteDelSendero : MonoBehaviour
{
    [Serializable]
    public struct Tramo
    {
        [Tooltip("Desde qué Z del mundo manda este color.")]
        public float desdeZ;
        [ColorUsage(false, true)] public Color color;
        [Min(0f)] public float fuerza;
    }

    private static readonly int TinteId = Shader.PropertyToID("_Tinte");
    private static readonly int FuerzaId = Shader.PropertyToID("_FuerzaTinte");

    [Tooltip("Tramos ordenados por Z.")]
    [SerializeField] private List<Tramo> tramos = new();
    [Tooltip("Segundos que tarda en pasar de un color a otro.")]
    [SerializeField, Min(0.1f)] private float transicion = 6f;
    [Tooltip("Estrellas que suben y toman el color del tramo.")]
    [SerializeField] private List<EstrellasQueSuben> estrellas = new();
    [Tooltip("Cuánto se aclara hacia el blanco el color del tramo en las estrellas que suben.")]
    [SerializeField, Range(0f, 1f)] private float aclaradoDeEstrellas = 0.4f;

    private Material _original;
    private Material _copia;
    private Transform _jugador;
    private Color _color;
    private float _fuerza;
    private Color _colorDeEstrellas;

    public List<Tramo> Tramos => tramos;
    public List<EstrellasQueSuben> Estrellas => estrellas;

    private void OnEnable()
    {
        _original = RenderSettings.skybox;
        if (_original != null && _original.HasProperty(TinteId))
        {
            _copia = new Material(_original) { name = _original.name + " (tinte)" };
            RenderSettings.skybox = _copia;
            _color = _copia.GetColor(TinteId);
            _fuerza = _copia.GetFloat(FuerzaId);
        }
        else if (tramos.Count > 0)
        {
            _color = tramos[0].color;
        }
        _colorDeEstrellas = new Color(-1f, -1f, -1f);
        PlayerService.OnPlayerRegistered += AlRegistrarJugador;
        PlayerService.OnPlayerUnregistered += AlQuitarJugador;
        AlRegistrarJugador(PlayerService.Player);
    }

    private void OnDisable()
    {
        PlayerService.OnPlayerRegistered -= AlRegistrarJugador;
        PlayerService.OnPlayerUnregistered -= AlQuitarJugador;
        if (_copia != null)
        {
            if (RenderSettings.skybox == _copia) RenderSettings.skybox = _original;
            Destroy(_copia);
            _copia = null;
        }
    }

    private void AlRegistrarJugador(GameObject jugador) => _jugador = jugador != null ? jugador.transform : null;
    private void AlQuitarJugador() => _jugador = null;

    private void Update()
    {
        if (_jugador == null || tramos.Count == 0) return;
        float z = _jugador.position.z;
        var objetivo = tramos[0];
        for (int i = 1; i < tramos.Count; i++)
            if (z >= tramos[i].desdeZ) objetivo = tramos[i];
        float k = 1f - Mathf.Exp(-Time.deltaTime * 3f / transicion);
        _color = Color.Lerp(_color, objetivo.color, k);
        _fuerza = Mathf.Lerp(_fuerza, objetivo.fuerza, k);
        if (_copia != null)
        {
            _copia.SetColor(TinteId, _color);
            _copia.SetFloat(FuerzaId, _fuerza);
        }
        TenirEstrellas();
    }

    private void TenirEstrellas()
    {
        if (estrellas.Count == 0) return;
        Color c = Color.Lerp(_color, Color.white, aclaradoDeEstrellas);
        c.a = 1f;
        if (Mathf.Abs(c.r - _colorDeEstrellas.r) + Mathf.Abs(c.g - _colorDeEstrellas.g) + Mathf.Abs(c.b - _colorDeEstrellas.b) < 0.01f) return;
        _colorDeEstrellas = c;
        for (int i = 0; i < estrellas.Count; i++)
            if (estrellas[i] != null) estrellas[i].CambiarColor(c);
    }
}
