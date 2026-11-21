using UnityEditor;
using UnityEngine;

/// Menú de un solo uso: Estela y Liam empiezan con el escudo (B), como Will tras la lección de Erika.
/// Sus fichas lo traían apagado y nada en la historia se lo daba, así que llevándolos el jugador la B
/// no hacía nada. Al ejecutarse, este archivo sale a «Versiones antiguas/». Ver INC-1361.
public static class EscudoDeEstelaYLiam
{
    static readonly string[] Fichas = { "Assets/_PERSONAJES/Ficha_Estela.asset", "Assets/_PERSONAJES/Ficha_Liam.asset" };

    [MenuItem("El Sendero/Combate/Escudo de Estela y Liam desde el principio (INC-1361)")]
    public static void Ejecutar()
    {
        int cambiadas = 0;
        foreach (var ruta in Fichas)
        {
            var ficha = AssetDatabase.LoadAssetAtPath<FichaDePersonaje>(ruta);
            if (ficha == null) { Debug.LogWarning($"[INC-1361] No está {ruta}"); continue; }
            var so = new SerializedObject(ficha);
            var escudo = so.FindProperty("habilidadesIniciales.shield");
            if (escudo == null) { Debug.LogWarning($"[INC-1361] {ruta} no tiene habilidadesIniciales.shield"); continue; }
            if (escudo.boolValue) continue;
            escudo.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ficha);
            cambiadas++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[INC-1361] Escudo puesto en {cambiadas} ficha(s).");
    }
}
