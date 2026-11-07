using UnityEngine;

/// <summary>
/// El hueso de un personaje para ponerle algo (en la mano, en la cabeza), tenga esqueleto humanoide o
/// genérico. Humanoide: el de Mecanim. Genérico (modelos de pack sin Avatar humano): por nombre, primero
/// el punto de agarre que traen muchos packs («RightHandProp»), luego el hueso tal cual («RightHand») y
/// luego con prefijo («mixamorig:RightHand», «Bip_RightHand»).
/// </summary>
public static class HuesoDelActor
{
    public static Transform De(Animator anim, HumanBodyBones hueso)
    {
        if (anim == null) return null;
        if (anim.isHuman) return anim.GetBoneTransform(hueso);
        string nombre = hueso.ToString();
        Transform exacto = null, conPrefijo = null;
        foreach (var t in anim.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre + "Prop") return t;
            if (exacto == null && t.name == nombre) exacto = t;
            else if (conPrefijo == null && (t.name.EndsWith(":" + nombre) || t.name.EndsWith("_" + nombre))) conPrefijo = t;
        }
        return exacto != null ? exacto : conPrefijo;
    }
}
