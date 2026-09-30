using UnityEngine;

/// Las piezas de la arena de la batalla final (el Corazón del Sendero) que usa el Mago Oscuro:
/// dónde puede aparecer, por dónde vuela, las anclas, los conductos, el altar... Solo guarda
/// referencias; la escena de prueba la rellena BatallaFinalLabBuilder y la escena del Sendero
/// tendrá la suya. Ver INC-509.
public sealed class EscenarioBatallaFinal : MonoBehaviour
{
    [Header("Arena")]
    public Transform centro;
    public float radio = 20f;

    [Header("Altar")]
    [Tooltip("El altar: de aquí sale el conducto de sombra que alimenta al Mago.")]
    public Transform altar;
    [Tooltip("Dónde se coloca el Mago cuando se funde con el altar (marea y fase 3).")]
    public Transform puntoDelAltar;
    [Tooltip("La unión del conducto con el altar: donde tiene que dar la aguja de luz del final.")]
    public Transform unionDelConducto;
    [Tooltip("Rayo del altar al Mago (su vínculo). Se ve durante todo el combate.")]
    public LineRenderer conductoPrincipal;

    [Header("Fase 1: patrones")]
    [Tooltip("Sitios a los que se teletransporta.")]
    public Transform[] puntosDeSalto;

    [Header("Fase 2: el Sendero se deforma")]
    [Tooltip("Sitios por los que vuela (encima de los pilares).")]
    public Transform[] puntosDeVuelo;
    [Tooltip("Centros de las zonas del suelo que se corrompen.")]
    public Transform[] cuadrantes;
    public AnclaDelSendero[] anclas;

    [Header("Fase 3: los conductos")]
    public RedDeConductos red;
    public Transform[] puntosDeInvocacion;

    [Header("Tiempo")]
    [Tooltip("Graba dónde estaba cada uno para el Hechizo del Tiempo.")]
    public RegistroTemporal registro;
}
