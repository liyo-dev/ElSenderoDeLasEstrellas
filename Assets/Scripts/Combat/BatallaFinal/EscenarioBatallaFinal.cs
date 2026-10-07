using System;
using UnityEngine;

/// Las piezas de la arena de la batalla final que usa el Mago Oscuro: el altar, dónde puede
/// aparecer y lo que conjura en cada fase. Al empezar solo está el altar; lo demás aparece y se
/// deshace con su magia (ObjetoConjurado). Solo guarda referencias; la escena de prueba la rellena
/// BatallaFinalLabBuilder y la escena del Sendero tendrá la suya. Ver INC-663.
public sealed class EscenarioBatallaFinal : MonoBehaviour
{
    [Header("Arena")]
    public Transform centro;
    public float radio = 20f;

    [Header("Altar")]
    public Transform altar;
    [Tooltip("Dónde se coloca el Mago junto al altar (regeneración y Marea).")]
    public Transform puntoDelAltar;

    [Header("Fase 1")]
    [Tooltip("Sitios a los que se teletransporta.")]
    public Transform[] puntosDeSalto;
    [Tooltip("Las espinas que conjura: sus cristales le protegen y suman rayos a sus salvas.")]
    public JuegoDeConjuros espinas;

    [Header("Fase 2")]
    [Tooltip("Pilares con anclas, plataformas y lanzadores. Cada vez que cae, conjura el siguiente juego.")]
    public JuegoDeConjuros[] fase2;
    [Tooltip("Centros de las zonas del suelo que se corrompen.")]
    public Transform[] cuadrantes;

    [Header("Tiempo")]
    [Tooltip("Graba dónde estaba cada uno para rebobinar.")]
    public RegistroTemporal registro;
}

/// Lo que se conjura de una vez: los objetos que aparecen, los cristales que le protegen mientras
/// están y, si vuela, por dónde.
[Serializable]
public sealed class JuegoDeConjuros
{
    public ObjetoConjurado[] objetos;
    public CristalProtector[] cristales;
    public Transform[] puntosDeVuelo;
}
