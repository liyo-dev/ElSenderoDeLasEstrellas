#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Silas joven (prefab MagoPrimero, en el recuerdo de Cap2_02_MananaDespues) no tenía
/// NPCSimpleAnimator: en las secuencias se deslizaba sin andar. Se lo pone en la raíz, donde está
/// su Animator de NPC. Menú de un solo uso (INC-1108).
public static class AnimadorParaMagoPrimero
{
    const string Ruta = "Assets/_NPCs/MagoPrimero.prefab";

    [MenuItem("El Sendero/Archivo/Personajes: dar NPCSimpleAnimator a MagoPrimero (INC-1108)")]
    public static void Poner()
    {
        var raiz = PrefabUtility.LoadPrefabContents(Ruta);
        try
        {
            if (raiz.GetComponent<NPCSimpleAnimator>() != null)
            {
                OrdenesDelEditor.Avisar("MagoPrimero", "Ya tenía NPCSimpleAnimator.");
                return;
            }
            if (raiz.GetComponent<Animator>() == null)
            {
                OrdenesDelEditor.Avisar("MagoPrimero", "La raíz no tiene Animator: no lo pongo.");
                return;
            }
            raiz.AddComponent<NPCSimpleAnimator>();
            PrefabUtility.SaveAsPrefabAsset(raiz, Ruta);
            OrdenesDelEditor.Avisar("MagoPrimero", "NPCSimpleAnimator puesto en la raíz.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
#endif
