using UnityEngine;

/// <summary>
/// Si la build es la demo: dónde se corta la historia lo dice el grafo (<see cref="FinDeLaDemoNode"/>),
/// y este asset dice si se corta. Vive en Resources/Demo; sin asset, no es la demo.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Configuración de la demo", fileName = "ConfiguracionDeLaDemo")]
public sealed class ConfiguracionDeLaDemo : ScriptableObject
{
    private const string Ruta = "Demo/ConfiguracionDeLaDemo";

    [Tooltip("Marcado: la build es la demo y la historia termina en el fin de la demo.")]
    public bool esDemo = true;

    private static ConfiguracionDeLaDemo s_cargada;
    private static bool s_buscada;

    public static bool EsDemo
    {
        get
        {
            if (!s_buscada) { s_cargada = Resources.Load<ConfiguracionDeLaDemo>(Ruta); s_buscada = true; }
            return s_cargada != null && s_cargada.esDemo;
        }
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_cargada = null; s_buscada = false; }
#endif
}
