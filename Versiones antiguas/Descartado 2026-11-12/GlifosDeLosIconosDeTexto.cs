#if UNITY_EDITOR
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using Core.InputGlyphs;

/// DialogueIcons.asset traía sus propios dibujos de los botones (interactable_lb, _rb…). TMP busca
/// un nombre primero en el asset y después en sus fallbacks, así que esos dibujos viejos tapaban los
/// iconos del mando o teclado activo que InputGlyphService pone como fallback: en el menú de Start
/// salían interrogaciones. Se renombran (prefijo «viejo_») para que gane el icono de la familia
/// activa. Se puede ejecutar varias veces. Ver INC-1098
public static class GlifosDeLosIconosDeTexto
{
    private const string Ruta = "Assets/Art/UI/DialogueIcons/DialogueIcons.asset";

    [MenuItem("El Sendero/Archivo/Iconos de texto: que manden los glifos del mando (INC-1098)")]
    private static void Aplicar()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(Ruta);
        if (asset == null) { OrdenesDelEditor.Avisar("Iconos de texto", "No encuentro " + Ruta); return; }
        var sb = new StringBuilder();
        foreach (var personaje in asset.spriteCharacterTable)
        {
            if (personaje == null || !InputGlyphNames.All.Contains(personaje.name)) continue;
            sb.Append(personaje.name).Append(' ');
            personaje.name = "viejo_" + personaje.name;
        }
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Iconos de texto", sb.Length > 0 ? "Renombrados: " + sb : "Ya estaban renombrados.");
    }
}
#endif
