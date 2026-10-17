using UnityEngine;

/// <summary>
/// Piezas de la arena de la batalla final: altar, centro, puntos de salto en tierra y puntos de vuelo aéreos.
/// La arena es diáfana y abierta (sin columnas ni plataformas flotantes), pensada para un duelo
/// implacable en tierra y en el aire (estilo KH2 Sephiroth).
/// </summary>
public sealed class EscenarioBatallaFinal : MonoBehaviour
{
    [Header("Arena")]
    public Transform centro;
    public float radio = 22f;

    [Header("Altar")]
    public Transform altar;
    [Tooltip("Dónde se coloca el Mago junto al altar (regeneración y Marea).")]
    public Transform puntoDelAltar;

    [Header("Puntos de combate")]
    [Tooltip("Puntos de teletransporte y deslizamiento en tierra.")]
    public Transform[] puntosDeSalto;
    [Tooltip("Puntos de vuelo en el aire durante la fase del Abismo (combate aéreo obligatorio).")]
    public Transform[] puntosDeVuelo;

    [Header("Tiempo")]
    [Tooltip("Graba dónde estaba cada uno para rebobinar.")]
    public RegistroTemporal registro;
}
