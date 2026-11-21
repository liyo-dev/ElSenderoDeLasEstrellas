using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea (o pone al día) el combo de Mente de Liam que le vuelve invisible: «Velo Mental»
/// (B · Y · Y), con su material translúcido, en la ficha de Liam y en la biblioteca de hechizos.
/// Parte de una copia de Paso Sombrío (icono, gesto y sonido) y lleva el destello del Dardo
/// Mental. Ver INC-1358.
/// </summary>
public static class InvisibilidadDeLiam
{
    private const string Hechizo = "Assets/_SPELLS/Invisibilidad.asset";
    private const string Base = "Assets/_SPELLS/PasoSombrio.asset";
    private const string Destello = "Assets/_SPELLS/DardoMental.asset";
    private const string Material = "Assets/ElSendero/Data/Combate/Mat_Invisible.mat";
    private const string FichaDeLiam = "Assets/_PERSONAJES/Ficha_Liam.asset";
    private const string Biblioteca = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";

    [MenuItem("El Sendero/Magia/Velo Mental: invisibilidad de Liam (INC-1358)")]
    public static void Montar()
    {
        var material = CrearMaterial();

        var hechizo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(Hechizo);
        if (hechizo == null)
        {
            if (!AssetDatabase.CopyAsset(Base, Hechizo)) { OrdenesDelEditor.Avisar("Velo Mental", "No he podido copiar " + Base); return; }
            hechizo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(Hechizo);
        }

        hechizo.spellId = SpellId.Invisibilidad;
        hechizo.displayNameId = "";
        hechizo.displayName = "Velo Mental";
        hechizo.lore = "La mente se cierra y el mundo deja de verte. Dura hasta que golpeas.";
        hechizo.loreId = "";
        hechizo.kind = MagicKind.Invisibility;
        hechizo.element = MagicElement.Mind;
        hechizo.caster = PartyControlManager.CharacterSlot.Liam;
        hechizo.comboSequence = new[] { ComboButton.B, ComboButton.Y, ComboButton.Y };
        hechizo.invisibilidadSegundos = 6f;
        hechizo.materialDeInvisible = material;
        hechizo.manaCost = 30f;
        hechizo.damage = 0f;
        hechizo.enGrimorio = true;
        var dardo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(Destello);
        if (dardo != null && dardo.spawnVFX != null) hechizo.spawnVFX = dardo.spawnVFX;
        EditorUtility.SetDirty(hechizo);

        string informe = "✓ Velo Mental (B · Y · Y, Liam, Mente, 6 s).";
        informe += AnadirALaLista(FichaDeLiam, "combos", hechizo) ? "\n✓ En la ficha de Liam." : "\n✗ No encuentro la ficha de Liam.";
        informe += AnadirALaLista(Biblioteca, "spells", hechizo) ? "\n✓ En la biblioteca de hechizos." : "\n✗ No encuentro la biblioteca de hechizos.";
        AssetDatabase.SaveAssets();
        Debug.Log("[Velo Mental]\n" + informe + "\nComprueba las secuencias: El Sendero ▸ Combate ▸ Comprobar secuencias de combo.");
    }

    private static Material CrearMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Material);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) { Debug.LogWarning("[Velo Mental] No encuentro el shader Unlit de URP: sin material (solo pierde la sombra)."); return null; }
        mat = new Material(shader) { name = "Mat_Invisible" };
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(0.72f, 0.64f, 1f, 0.16f));
        AssetDatabase.CreateAsset(mat, Material);
        return mat;
    }

    private static bool AnadirALaLista(string ruta, string campo, MagicSpellSO hechizo)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(ruta);
        if (asset == null) return false;
        var so = new SerializedObject(asset);
        var lista = so.FindProperty(campo);
        if (lista == null || !lista.isArray) return false;
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).objectReferenceValue == hechizo) return true;
        lista.arraySize++;
        lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = hechizo;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return true;
    }
}
