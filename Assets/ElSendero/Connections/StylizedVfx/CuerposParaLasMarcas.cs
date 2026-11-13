using UnityEngine;

/// Enchufe de Kits.StylizedVfx: en El Sendero los personajes viven en la capa Default, como el
/// suelo, y no todos llevan Rigidbody; se reconocen por su NPCSimpleAnimator (lo llevan el jugador
/// y todos los NPCs). Así una marca de suelo (GroundMark) no se apoya en la cabeza de nadie.
public static class CuerposParaLasMarcas
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Enchufar()
    {
        Kits.StylizedVfx.GroundMark.IsBody = c => c.transform.root.GetComponent<NPCSimpleAnimator>() != null;
    }
}
