using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Montaje del grupo en combate (INC-717): pone SaludDelGrupo y ReanimarManteniendoA en
/// GrupoDelJugador.prefab (si faltan, GrupoDelJugador los añade en juego, pero así se ajustan en
/// el Inspector) y crea la Pluma estelar, el objeto que levanta a un compañero caído.
/// Idempotente.
/// </summary>
public static class GrupoEnCombateBuilder
{
    private const string RutaGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    private const string SpriteAnillo = "Assets/Art/UI/HUD/ability_socket_ring.png";
    private const string RutaPluma = "Assets/_ITEMS/IT_PlumaEstelar.asset";
    private const string IconoProvisional = "Assets/_ITEMS/IT_PocionVida.asset";

    [MenuItem("El Sendero/Grupo/Montar caídas y revivir (INC-717)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Grupo en combate", "Sal de Play para montarlo.", "Aceptar");
            return;
        }

        var hecho = new List<string>();
        var avisos = new List<string>();
        MontarGrupo(hecho, avisos);
        CrearPluma(hecho, avisos);
        AssetDatabase.SaveAssets();
        ItemRegistrySincronizador.Sincronizar();

        string texto = string.Join("\n", hecho);
        if (avisos.Count > 0) texto += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Grupo en combate", texto, "Aceptar");
    }

    private static void MontarGrupo(List<string> hecho, List<string> avisos)
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaGrupo);
        if (raiz == null) { avisos.Add($"No encuentro {RutaGrupo}."); return; }
        try
        {
            bool cambiado = false;
            if (raiz.GetComponent<SaludDelGrupo>() == null)
            {
                raiz.AddComponent<SaludDelGrupo>();
                hecho.Add("Añadido SaludDelGrupo a GrupoDelJugador.");
                cambiado = true;
            }
            else hecho.Add("GrupoDelJugador ya tenía SaludDelGrupo.");

            var reanimar = raiz.GetComponent<ReanimarManteniendoA>();
            if (reanimar == null)
            {
                reanimar = raiz.AddComponent<ReanimarManteniendoA>();
                hecho.Add("Añadido ReanimarManteniendoA a GrupoDelJugador.");
                cambiado = true;
            }
            else hecho.Add("GrupoDelJugador ya tenía ReanimarManteniendoA.");

            var so = new SerializedObject(reanimar);
            var anillo = so.FindProperty("anillo");
            if (anillo != null && anillo.objectReferenceValue == null)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteAnillo);
                if (sprite != null)
                {
                    anillo.objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    cambiado = true;
                }
                else avisos.Add($"No encuentro {SpriteAnillo}: el aviso usará el anillo del aviso de combate.");
            }

            if (cambiado) PrefabUtility.SaveAsPrefabAsset(raiz, RutaGrupo);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static void CrearPluma(List<string> hecho, List<string> avisos)
    {
        var pluma = AssetDatabase.LoadAssetAtPath<ItemData>(RutaPluma);
        bool nueva = pluma == null;
        if (nueva) pluma = ScriptableObject.CreateInstance<ItemData>();

        pluma.itemId = "plumaEstelar";
        pluma.displayNameId = "ITEM_PLUMAESTELAR_NAME";
        pluma.displayName = "Pluma estelar";
        pluma.pluralDisplayNameId = "ITEM_PLUMAESTELAR_NAME_PLURAL";
        pluma.pluralDisplayName = "Plumas estelares";
        pluma.useDescriptionId = "ITEM_PLUMAESTELAR_DESC";
        pluma.useDescription = "Levanta al compañero caído más cercano con la mitad de su vida.";
        pluma.usableFromInventory = true;
        pluma.usageKind = ItemData.ItemUsageKind.Consumable;
        pluma.buyPrice = 120;
        pluma.sellValue = 30;
        pluma.useEffects = new List<PickupEffect>
        {
            new PickupEffect { effectType = PickupEffectType.Revivir, quantity = 1, amount = 0.5f, consumeEvenIfNoChange = false }
        };
        if (pluma.icon == null)
        {
            var referencia = AssetDatabase.LoadAssetAtPath<ItemData>(IconoProvisional);
            if (referencia != null) pluma.icon = referencia.icon;
            avisos.Add("La Pluma estelar lleva de momento el icono de la Poción de vida.");
        }

        if (nueva)
        {
            AssetDatabase.CreateAsset(pluma, RutaPluma);
            hecho.Add($"Creada la Pluma estelar: {RutaPluma}.");
        }
        else
        {
            EditorUtility.SetDirty(pluma);
            hecho.Add("Pluma estelar actualizada.");
        }
    }
}
