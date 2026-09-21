#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Animations;

/// Preguntas sobre un `AnimatorController` que hay que contestar recorriendo su grafo: en qué
/// capas vive un estado, y qué nombres son en realidad parámetros y no estados.
///
/// Existe por el mismo motivo que `AnimatorLayerUtil` en runtime: esta recursión de diez líneas
/// estaba a punto de quedarse copiada en `NpcAnimatorSaltoYVueloWiring` y en
/// `ValidarGestosDeSecuencias`, y una de las dos copias acabaría desactualizada.
///
/// La diferencia con `AnimatorLayerUtil` es de contexto, no de intención: aquel trabaja en runtime
/// sobre un `Animator` ya instanciado (`animator.HasState`), y este en el Editor sobre el ASSET,
/// donde no hay ningún `Animator` al que preguntar y hay que abrir las sub-máquinas a mano.
///
/// ── Por qué importa lo de los parámetros ──────────────────────────────────────────────────────
/// INC-264: una secuencia pedía el gesto `Fidget` diez veces por fase y no lo hacía nadie. `Fidget`
/// es un PARÁMETRO de `NPC_NoWeapon`, no un estado. El comprobador de entonces lo dio por bueno
/// porque buscaba `m_Name:` con una expresión regular sobre el YAML, y un `.controller` tiene
/// `m_Name` en parámetros, capas y blend trees además de en los estados. Con la API de Editor esa
/// confusión no puede ocurrir, pero conviene poder DECIRLO en el aviso: «eso es un parámetro» se
/// arregla solo, y «ese estado no existe» manda a buscar por todo el proyecto.
public static class AnimatorControllerEstados
{
    /// Índices de las capas en las que existe un estado con ese nombre. Vacío = no existe en
    /// ninguna. Más de uno = ambiguo, y en runtime gana la capa preferida de
    /// `AnimatorLayerUtil.ResolveLayer` (UpperBody), que casi nunca es lo que se quiere para un
    /// gesto de cuerpo entero.
    public static List<int> CapasConElEstado(AnimatorController controller, string nombre)
    {
        var capas = new List<int>();
        if (controller == null || string.IsNullOrEmpty(nombre) || controller.layers == null)
            return capas;

        for (int i = 0; i < controller.layers.Length; i++)
        {
            var maquina = controller.layers[i]?.stateMachine;
            if (maquina != null && Contiene(maquina, nombre))
                capas.Add(i);
        }
        return capas;
    }

    public static bool Existe(AnimatorController controller, string nombre)
        => CapasConElEstado(controller, nombre).Count > 0;

    /// El estado con ese nombre, mirando todas las capas y sub-máquinas, o null. Hace falta para
    /// RENOMBRAR uno ya existente: `Existe` contesta sí o no, y para cambiarle el nombre hay que
    /// tener el objeto.
    public static AnimatorState Buscar(AnimatorController controller, string nombre)
    {
        if (controller == null || string.IsNullOrEmpty(nombre) || controller.layers == null)
            return null;

        foreach (var capa in controller.layers)
        {
            if (capa?.stateMachine == null) continue;
            var encontrado = Buscar(capa.stateMachine, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    private static AnimatorState Buscar(AnimatorStateMachine maquina, string nombre)
    {
        foreach (var hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == nombre) return hijo.state;

        foreach (var sub in maquina.stateMachines)
        {
            if (sub.stateMachine == null) continue;
            var encontrado = Buscar(sub.stateMachine, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    /// ¿Ese nombre es un PARÁMETRO del controller? Para poder distinguir en el aviso entre «está
    /// mal escrito» y «eso no es un estado, es un parámetro» (ver INC-264).
    public static bool EsParametro(AnimatorController controller, string nombre)
    {
        if (controller == null || string.IsNullOrEmpty(nombre) || controller.parameters == null)
            return false;

        foreach (var p in controller.parameters)
            if (p != null && p.name == nombre) return true;

        return false;
    }

    /// Todos los nombres de estado del controller, de todas las capas y sub-máquinas. Para poder
    /// sugerir el más parecido cuando uno no existe.
    public static HashSet<string> TodosLosEstados(AnimatorController controller)
    {
        var nombres = new HashSet<string>();
        if (controller == null || controller.layers == null) return nombres;

        foreach (var capa in controller.layers)
            if (capa?.stateMachine != null)
                Recoger(capa.stateMachine, nombres);

        return nombres;
    }

    private static bool Contiene(AnimatorStateMachine maquina, string nombre)
    {
        foreach (var hijo in maquina.states)
            if (hijo.state != null && hijo.state.name == nombre) return true;

        foreach (var sub in maquina.stateMachines)
            if (sub.stateMachine != null && Contiene(sub.stateMachine, nombre)) return true;

        return false;
    }

    private static void Recoger(AnimatorStateMachine maquina, HashSet<string> destino)
    {
        foreach (var hijo in maquina.states)
            if (hijo.state != null) destino.Add(hijo.state.name);

        foreach (var sub in maquina.stateMachines)
            if (sub.stateMachine != null) Recoger(sub.stateMachine, destino);
    }
}
#endif
