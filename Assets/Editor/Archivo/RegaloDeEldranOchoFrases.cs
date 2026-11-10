#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Deja la charla del regalo de Eldran (DG_ELDRAN_REGALO) en sus ocho frases, alternando Eldran y
/// Will. Las frases de la 1 a la 6 se conservan; añade la 7 (Eldran) y la 8 (Will) si faltan.
/// Ver INC-946.
public static class RegaloDeEldranOchoFrases
{
    private const string Ruta = "Assets/_DIALOGUES/DIALOGUE NPCS/Eldran/DG_ELDRAN_REGALO.asset";

    [MenuItem("El Sendero/Archivo/Diálogos: regalo de Eldran en ocho frases")]
    public static void Aplicar()
    {
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(Ruta);
        if (dialogo == null)
        {
            Debug.LogError("[RegaloDeEldran] No se encuentra " + Ruta);
            return;
        }

        int antes = dialogo.lines != null ? dialogo.lines.Length : 0;
        if (antes >= 8)
        {
            Debug.Log("[RegaloDeEldran] Ya tiene " + antes + " frases; no se cambia nada.");
            return;
        }

        Undo.RecordObject(dialogo, "Regalo de Eldran en ocho frases");
        var lineas = new DialogueLine[8];
        for (int i = 0; i < 8; i++)
        {
            bool habla = i < antes;
            bool esWill = i % 2 == 1;
            lineas[i] = habla ? dialogo.lines[i] : new DialogueLine
            {
                speakerNameId = esWill ? "CHAR_WILL" : "CHAR_ELDRAN",
                textId = "DLG_ELDRAN_REGALO_0" + (i + 1),
                isPlayerSpeaking = esWill,
            };
        }
        dialogo.lines = lineas;
        EditorUtility.SetDirty(dialogo);
        AssetDatabase.SaveAssets();
        Debug.Log("[RegaloDeEldran] De " + antes + " a 8 frases.");
    }
}
#endif
