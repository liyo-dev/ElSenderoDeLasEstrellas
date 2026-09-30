using UnityEngine;

/// <summary>
/// Utilidades para usar un personaje jugable (el mismo prefab de la partida) como decorado del
/// menú principal: le apaga la IA y la física en vivo, y fija los parámetros del Animator que
/// harían saltar al estado de caída. Lo usan <see cref="MainMenuFlyingCompanion"/> y
/// <see cref="ActorDePortada"/>.
/// </summary>
public static class DecoradoDeMenu
{
    static readonly int HashIsGrounded     = Animator.StringToHash("IsGrounded");
    static readonly int HashGroundDistance = Animator.StringToHash("GroundDistance");
    static readonly int HashIsFlying       = Animator.StringToHash("isFlying");

    /// <summary>
    /// Desactiva (sin destruir) el cerebro de IA, el animador de NPC, el NavMeshAgent y la física:
    /// como decorado no deben patrullar, hablar solos ni pelear con quien coloca al personaje.
    /// </summary>
    public static void ApagarSistemasDeJuego(GameObject personaje)
    {
        if (personaje == null) return;

        var brain = personaje.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (brain != null) brain.enabled = false;

        var simpleAnimator = personaje.GetComponent<NPCSimpleAnimator>();
        if (simpleAnimator != null) simpleAnimator.enabled = false;

        var agent = personaje.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var rb = personaje.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    /// <summary>Qué parámetros de suelo/vuelo tiene un Animator (se lee una vez y se guarda).</summary>
    public struct ParametrosDeSuelo { public bool suelo, distancia, vuelo; }

    public static ParametrosDeSuelo LeerParametrosDeSuelo(Animator anim)
    {
        var r = new ParametrosDeSuelo();
        if (anim == null) return r;
        foreach (var p in anim.parameters)
        {
            if (p.nameHash == HashIsGrounded && p.type == AnimatorControllerParameterType.Bool) r.suelo = true;
            else if (p.nameHash == HashGroundDistance && p.type == AnimatorControllerParameterType.Float) r.distancia = true;
            else if (p.nameHash == HashIsFlying && p.type == AnimatorControllerParameterType.Bool) r.vuelo = true;
        }
        return r;
    }

    /// <summary>
    /// Deja al personaje «en el suelo» para el Animator: el controlador compartido de los
    /// jugables tiene un Any State → caída que interrumpe cualquier pose si IsGrounded es false.
    /// </summary>
    public static void ForzarEnSuelo(Animator anim, ParametrosDeSuelo parametros)
    {
        if (anim == null) return;
        if (parametros.suelo) anim.SetBool(HashIsGrounded, true);
        if (parametros.distancia) anim.SetFloat(HashGroundDistance, 0f);
        if (parametros.vuelo) anim.SetBool(HashIsFlying, false);
    }

    /// <summary>Busca en qué capa del Animator está un estado; -1 si no existe.</summary>
    public static int CapaDelEstado(Animator anim, string estado)
    {
        if (anim == null || string.IsNullOrEmpty(estado)) return -1;
        int hash = Animator.StringToHash(estado);
        for (int layer = 0; layer < anim.layerCount; layer++)
            if (anim.HasState(layer, hash)) return layer;
        return -1;
    }
}
