using UnityEngine;

namespace Game.NPC.Common
{
    /// Dónde está el jugador. PlayerService ya registra siempre el cuerpo que se mueve (ver
    /// PlayerService.ResolverCuerpo, INC-482), así que aquí basta con leerlo.
    public static class PlayerLocator
    {
        public static Transform ResolvePlayer(bool allowSceneLookup = true)
        {
            if (PlayerService.TryGetComponent(out Transform player, includeInactive: true))
                return player;

            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup) && playerGo)
                return playerGo.transform;

            if (!allowSceneLookup)
                return null;

            var fallback = GameObject.FindGameObjectWithTag("Player");
            if (fallback != null)
            {
                PlayerService.RegisterPlayer(fallback, false);
                return PlayerService.PlayerTransform;
            }

            return null;
        }

        public static Transform ResolvePlayerCamera()
        {
            if (Camera.main)
                return Camera.main.transform;

            return null;
        }
    }
}
