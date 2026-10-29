using UnityEngine;

/// <summary>
/// Resolución compartida de "en qué layer del Animator vive realmente este estado", para
/// personajes con una Base Layer (cuerpo completo, capa 0) y una UpperBody layer (torso/brazos,
/// sin piernas, vía AvatarMask — normalmente capa 1). Antes de esta clase, esta misma lógica de
/// 6 líneas (probar la capa preferida, si no existe caer a la capa 0) estaba copiada y pegada de
/// forma casi idéntica en NPCSimpleAnimator.PlaySocialGesture() y
/// PlayerDialogueAnimator.CapaDelEstado() — ver claude/dedup-resolucion-layer-2026-08-30.md.
///
/// NO sustituye a AnimatorStateCache (que recorre TODAS las capas y cachea resultados para
/// nombres con alias/variantes de ruta) ni a los sistemas de resolución de capa de vuelo
/// (FollowPlayerState.DetectFlightLayer / PlayerFlyingController.DetectFlightLayer, que buscan
/// una capa que contenga uno de varios estados candidatos) — esos resuelven un problema distinto
/// y se quedan como están.
/// </summary>
public static class AnimatorLayerUtil
{
    /// <summary>
    /// Resuelve en qué layer vive realmente el estado con hash <paramref name="stateHash"/>:
    /// prueba primero <paramref name="preferredLayer"/> (por defecto 1, la UpperBody layer) y si
    /// no existe ahí cae al Base Layer (capa 0). Devuelve -1 si no existe en ninguna de las dos.
    /// </summary>
    public static int ResolveLayer(Animator animator, int stateHash, int preferredLayer = 1)
    {
        if (animator == null)
            return -1;

        if (preferredLayer > 0 && animator.layerCount > preferredLayer
            && animator.HasState(preferredLayer, stateHash))
        {
            return preferredLayer;
        }

        if (animator.HasState(0, stateHash))
            return 0;

        return -1;
    }

    /// <summary>
    /// Igual que <see cref="ResolveLayer(Animator, int, int)"/> pero a partir del nombre de
    /// estado en vez de su hash ya calculado. Comprueba también sub-máquinas de estado comunes
    /// (<c>Base Layer.Locomotion.&lt;stateName&gt;</c> y <c>Base Layer.&lt;stateName&gt;</c>).
    /// </summary>
    public static int ResolveLayer(Animator animator, string stateName, int preferredLayer = 1)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return -1;

        int capa = ResolveLayer(animator, Animator.StringToHash(stateName), preferredLayer);
        if (capa >= 0)
            return capa;

        if (!stateName.Contains('.'))
        {
            if (animator.HasState(0, Animator.StringToHash($"Base Layer.Locomotion.{stateName}")))
                return 0;
            if (animator.HasState(0, Animator.StringToHash($"Base Layer.{stateName}")))
                return 0;
            if (preferredLayer > 0 && animator.layerCount > preferredLayer
                && animator.HasState(preferredLayer, Animator.StringToHash($"UpperBody.{stateName}")))
                return preferredLayer;
        }

        return -1;
    }

    /// <summary>
    /// Devuelve el nombre o ruta válida del estado en <paramref name="layer"/> para pasarlo a
    /// <see cref="Animator.Play(int, int, float)"/> o <see cref="Animator.CrossFadeInFixedTime(int, float, int, float)"/>.
    /// </summary>
    public static string ResolveStatePath(Animator animator, string stateName, int layer = 0)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return stateName;

        if (animator.HasState(layer, Animator.StringToHash(stateName)))
            return stateName;

        if (!stateName.Contains('.'))
        {
            string locPath = $"Base Layer.Locomotion.{stateName}";
            if (layer == 0 && animator.HasState(0, Animator.StringToHash(locPath)))
                return locPath;

            string basePath = $"Base Layer.{stateName}";
            if (layer == 0 && animator.HasState(0, Animator.StringToHash(basePath)))
                return basePath;

            string upperPath = $"UpperBody.{stateName}";
            if (layer > 0 && layer < animator.layerCount && animator.HasState(layer, Animator.StringToHash(upperPath)))
                return upperPath;
        }

        return stateName;
    }
}
