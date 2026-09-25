using UnityEngine;

/// Apaga la luz direccional de una escena ADITIVA cuando el mundo ya tiene su propio sol.
///
/// ── El bug que evita ──────────────────────────────────────────────────────────────────────────
/// Una escena aditiva que trae su propia luz direccional NO sustituye al sol del mundo: se SUMA a
/// él. El resultado son dos soles a la vez, con sombras cruzadas y todo lavado, y cuesta verlo
/// porque cada escena por separado se ve bien. Le pasó a Prologo_Valle: traía un
/// 'Luz_Sol_Manana' que se sumaba al sol que maneja DayNightCycle. Es el mismo error que la cámara
/// duplicada del 17 sep (una MainCamera propia en una escena aditiva), con otra ropa.
///
/// Pero quitar la luz a secas deja la escena imposible de editar: abierta sola en el Editor, sin
/// MainWorld, no se ve nada. Así que la luz se queda, y se apaga sola al arrancar si hay otro sol.
[RequireComponent(typeof(Light))]
[AddComponentMenu("El Sendero/Sol de escena aditiva")]
public class SolDeEscenaAditiva : MonoBehaviour
{
    private void Awake()
    {
        var mia = GetComponent<Light>();
        if (mia == null || mia.type != LightType.Directional) return;

        // El sol del ciclo día/noche manda siempre que exista.
        if (DayNightCycle.Sun != null && DayNightCycle.Sun != transform)
        {
            Apagar(mia, "el sol del ciclo día/noche");
            return;
        }

        // Si no hay ciclo, vale cualquier otra direccional encendida que no sea esta.
        foreach (var otra in FindObjectsByType<Light>())
        {
            if (otra == mia || otra.type != LightType.Directional || !otra.isActiveAndEnabled) continue;
            Apagar(mia, "otra luz direccional de la escena (" + otra.name + ")");
            return;
        }
    }

    private void Apagar(Light mia, string motivo)
    {
        mia.enabled = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[SolDeEscenaAditiva] '{name}' se apaga: ya manda {motivo}. Esta luz existe solo " +
                  "para poder editar la escena abierta suelta.");
#endif
    }
}
