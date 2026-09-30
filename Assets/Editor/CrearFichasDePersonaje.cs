using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Crea las fichas de Will, Estela y Liam (`Assets/_PERSONAJES/`) y pone el componente
/// `Personaje` con su ficha en el cuerpo de cada uno: `_WILL` (el cuerpo con el controller),
/// `_WILL_NPC`, `_ESTELA` y `_LIAM`. Los hechizos de Estela y Liam son los que tenían en sus
/// NPCPartyConfig (por GUID: esos campos ya no existen). Se puede repetir sin duplicar nada.
/// Ver INC-483.
public static class CrearFichasDePersonaje
{
    private const string Carpeta = "Assets/_PERSONAJES";

    [MenuItem("El Sendero/Archivo/Personajes: crear las fichas de Will, Estela y Liam")]
    public static void Ejecutar()
    {
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets", "_PERSONAJES");

        var will = CrearFicha("Will", Slot.Will, 100f, 0f);
        var estela = CrearFicha("Estela", Slot.Estela, 100f, 100f,
            "60af2fbe1683a7648bad32993e434d75", "f4fc5e380ac486947b74e825bf2f0835", null);
        var liam = CrearFicha("Liam", Slot.Liam, 100f, 100f,
            "5e2557db11732694182c948ffac0eab8", "0686630f8f80a5c4291f775845a499d7", "fb52c3e17e7394045aee1b7785a90735");
        AssetDatabase.SaveAssets();

        Poner("Assets/Prefabs/_WILL.prefab", "vBasicController_MaleCharacterPBR", will);
        Poner("Assets/Prefabs/_WILL_NPC.prefab", null, will);
        Poner("Assets/Prefabs/_ESTELA.prefab", null, estela);
        Poner("Assets/Prefabs/_LIAM.prefab", null, liam);

        Debug.Log("[CrearFichasDePersonaje] ✓ Fichas creadas en " + Carpeta + " y puestas en _WILL, _WILL_NPC, _ESTELA y _LIAM.");
    }

    private static FichaDePersonaje CrearFicha(string nombre, Slot personaje, float vida, float magia,
        params string[] guidsHechizos)
    {
        string ruta = $"{Carpeta}/Ficha_{nombre}.asset";
        var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(ruta);
        if (ficha != null) return ficha;

        ficha = ScriptableObject.CreateInstance<FichaDePersonaje>();
        AssetDatabase.CreateAsset(ficha, ruta);

        var so = new SerializedObject(ficha);
        so.FindProperty("personaje").intValue = (int)personaje;
        var est = so.FindProperty("estadisticasIniciales");
        est.FindPropertyRelative("vida").floatValue = vida;
        est.FindPropertyRelative("magia").floatValue = magia;
        est.FindPropertyRelative("ataque").floatValue = EstadisticasDelPersonaje.AtaqueInicial;
        est.FindPropertyRelative("defensa").floatValue = EstadisticasDelPersonaje.DefensaInicial;

        string[] campos = { "hechizoIzquierdo", "hechizoDerecho", "hechizoEspecial" };
        for (int i = 0; i < campos.Length && guidsHechizos != null && i < guidsHechizos.Length; i++)
        {
            if (string.IsNullOrEmpty(guidsHechizos[i])) continue;
            var hechizo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(AssetDatabase.GUIDToAssetPath(guidsHechizos[i]));
            if (hechizo == null) Debug.LogWarning($"[CrearFichasDePersonaje] {nombre}: no se encontró el hechizo {guidsHechizos[i]}.");
            so.FindProperty(campos[i]).objectReferenceValue = hechizo;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ficha);
        return ficha;
    }

    private static void Poner(string rutaPrefab, string hijo, FichaDePersonaje ficha)
    {
        var contenido = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var cuerpo = string.IsNullOrEmpty(hijo) ? contenido.transform : contenido.transform.Find(hijo);
            if (cuerpo == null)
            {
                Debug.LogError($"[CrearFichasDePersonaje] {rutaPrefab}: no se encontró '{hijo}'.");
                return;
            }
            var personaje = cuerpo.GetComponent<Personaje>();
            if (personaje == null) personaje = cuerpo.gameObject.AddComponent<Personaje>();
            var so = new SerializedObject(personaje);
            so.FindProperty("ficha").objectReferenceValue = ficha;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contenido, rutaPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
