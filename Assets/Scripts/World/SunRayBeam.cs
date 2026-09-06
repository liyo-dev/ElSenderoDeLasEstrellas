using UnityEngine;

/// <summary>
/// Orienta un plano de "rayo de sol" (shader Quibli/Light Beam) para que siga en todo momento la
/// direccion actual de la luz direccional de la escena - necesario porque DayNightCycle cambia el
/// angulo del sol entre Amanecer/Dia/Atardecer (ver DayNightCycle.timeSettings: sunRotationX/Y
/// distintos por periodo), asi que un rayo con rotacion fija solo se veria alineado con el sol
/// durante uno de esos tres periodos y torcido en los demas.
///
/// Se apoya en la unica luz Directional de la escena ("Directional light" en MainWorld.unity), leida
/// via el registro estatico DayNightCycle.Sun (poblado en su Awake()) en vez de buscarla por tipo en
/// cada frame - ver AGENTS.md §2, "nunca FindObjectOfType/FindObjectsByType en Update/LateUpdate,
/// usar registros". Ese registro solo existe en Play (DayNightCycle no tiene [ExecuteAlways]), asi
/// que en el Editor sin Play se usa una busqueda de respaldo UNA sola vez en OnEnable(), nunca
/// repetida en LateUpdate.
///
/// El eje local +X del plano (su "longitud", tal como lo escala QuibliSunRayDresser - ver nota sobre
/// _UvFadeX/_UvFadeY ahi) se alinea contra -sol.forward - la direccion DESDE la que llega la luz, o
/// sea "hacia el cielo" - de modo que el rayo cuelga del cielo hacia el suelo, sea cual sea el
/// angulo del sol en cada momento. giroPropio anade un giro fijo alrededor de ese eje, usado por QuibliSunRayDresser para cruzar dos
/// planos (mismo truco de "billboard cruzado" que ya usan los BillboardBush-*.asset de arbustos) y
/// que el rayo se vea razonablemente bien mirandolo desde angulos distintos.
///
/// [ExecuteAlways]: se reorienta tambien en el Editor sin pulsar Play, para poder verlo bien
/// colocado nada mas ejecutar el menu de El Sendero, y para seguir el sol si Raul lo gira a mano
/// desde el Editor (p. ej. probando periodos del dia).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class SunRayBeam : MonoBehaviour
{
    [Tooltip("Giro adicional (grados) alrededor del eje del rayo, fijado al crear el objeto.")]
    public float giroPropio;

    private Transform _sol;

    void OnEnable()
    {
        BuscarSolInicial();
        Alinear();
    }

    void LateUpdate()
    {
        // FIX (5 sept 2026, AGENTS.md §2): nunca FindObjectsByType aqui. DayNightCycle.Sun es una
        // simple lectura de campo estatico (gratis), no una busqueda de escena - se usa para
        // "adoptar" el sol en cuanto DayNightCycle.Awake() corre en Play, cubriendo el caso de que
        // este componente se activara antes que el (orden de ejecucion no garantizado entre ambos).
        if (_sol == null && DayNightCycle.Sun != null) _sol = DayNightCycle.Sun;
        Alinear();
    }

    private void BuscarSolInicial()
    {
        if (DayNightCycle.Sun != null)
        {
            _sol = DayNightCycle.Sun;
            return;
        }

        // Respaldo solo para el Editor sin Play (DayNightCycle no tiene [ExecuteAlways], asi que su
        // registro estatico no existe fuera de Play) - busqueda unica al activarse, nunca repetida.
        Light[] luces = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        foreach (Light luz in luces)
        {
            if (luz.type == LightType.Directional)
            {
                _sol = luz.transform;
                return;
            }
        }
    }

    private void Alinear()
    {
        if (_sol == null) return;
        Quaternion alineacion = Quaternion.FromToRotation(Vector3.right, -_sol.forward);
        transform.rotation = alineacion * Quaternion.Euler(giroPropio, 0f, 0f);
    }
}
