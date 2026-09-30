using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Una de las portadas del menú principal (una por etapa de la historia). Todo lo que se ve en
/// ella —decorado, personajes, su propia luz direccional— cuelga de este GameObject, que
/// <see cref="PortadaDelMenu"/> enciende o apaga entero. Aquí van además el encuadre de la
/// cámara, el cielo, el ambiente y la música de la portada.
/// </summary>
[DisallowMultipleComponent]
public class PortadaEtapa : MonoBehaviour
{
    public enum Ambiente { DelCielo, Plano }

    [Header("Cuándo se ve")]
    [Tooltip("Flag narrativo (el de un nodo 'Poner flag') que tiene que estar en la partida guardada para ver esta portada. Vacío en la primera etapa: es la de partida nueva.")]
    [SerializeField] private string flagQueLaDesbloquea = "";

    [Header("Cámara")]
    [Tooltip("Posición y orientación de la cámara del menú en esta portada.")]
    [SerializeField] private Transform encuadre;
    [Tooltip("Campo de visión vertical de la cámara. 0 = no lo cambia.")]
    [Min(0f)] [SerializeField] private float campoDeVision = 0f;

    [Header("Música")]
    [Tooltip("Música del menú con esta portada.")]
    [SerializeField] private AudioClip musica;

    [Header("Cielo y luz")]
    [Tooltip("Skybox de la portada. Vacío = no lo cambia.")]
    [SerializeField] private Material cielo;
    [Tooltip("Luz direccional de la portada (hija de este GameObject). Se usa como sol del RenderSettings.")]
    [SerializeField] private Light sol;
    [Tooltip("De dónde sale la luz ambiental: del propio skybox o de un color plano.")]
    [SerializeField] private Ambiente ambiente = Ambiente.DelCielo;
    [Tooltip("Color de la luz ambiental cuando el ambiente es plano.")]
    [SerializeField] private Color colorAmbiente = new Color(0.4f, 0.45f, 0.55f);

    [Header("Niebla")]
    [SerializeField] private bool niebla;
    [SerializeField] private Color colorNiebla = new Color(0.7f, 0.8f, 0.9f);
    [Min(0f)] [SerializeField] private float densidadNiebla = 0.01f;

    [Header("Al pulsar Nueva Partida / Continuar")]
    [Tooltip("Personajes que reproducen su animación de salida (p. ej. Will se levanta del banco).")]
    [SerializeField] private ActorDePortada[] actoresQueSalen;
    [Tooltip("Segundos que se espera, tras la animación de salida, antes de cargar la partida.")]
    [Min(0f)] [SerializeField] private float esperaAlSalir = 0f;

    public string FlagQueLaDesbloquea => flagQueLaDesbloquea;
    public Transform Encuadre => encuadre;
    public float CampoDeVision => campoDeVision;
    public AudioClip Musica => musica;

    /// <summary>Aplica cielo, luz ambiental, sol y niebla de esta portada.</summary>
    public void AplicarAmbiente()
    {
        if (cielo != null) RenderSettings.skybox = cielo;
        if (sol != null) RenderSettings.sun = sol;

        if (ambiente == Ambiente.Plano)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = colorAmbiente;
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
        }

        RenderSettings.fog = niebla;
        if (niebla)
        {
            RenderSettings.fogColor = colorNiebla;
            RenderSettings.fogDensity = densidadNiebla;
        }

        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Lanza la animación de salida de sus personajes y devuelve cuántos segundos hay que esperar
    /// antes de cargar (0 si no tiene salida).
    /// </summary>
    public float ReproducirSalida()
    {
        bool alguno = false;
        if (actoresQueSalen != null)
            foreach (var actor in actoresQueSalen)
                if (actor != null && actor.Salir()) alguno = true;
        return alguno ? esperaAlSalir : 0f;
    }
}
