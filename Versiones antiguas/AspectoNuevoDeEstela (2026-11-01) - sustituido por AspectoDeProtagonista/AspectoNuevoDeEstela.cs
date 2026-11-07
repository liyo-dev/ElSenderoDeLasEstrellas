using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// El aspecto nuevo de Estela (1 nov 2026, hecho en Blender: Tools/Modelos/Estela/): pelo naranja con
/// coleta alta y lazo azul (Hair14_Coleta), traje de hechicera (Body21_Hechicera: su cuerpo de siempre
/// con túnica, mangas, hombreras, cinturón y botas) y capa (Cloak04_Hechicera, como las del pack). Lo añade como piezas a los modelos que
/// pueden enseñarla (el del jugador, su NPC y su retrato), lo pone como su aspecto de partida y deja
/// el de antes (Body09, Hair08, AC09_Ribbon) en el modelo, sin usar. Se puede repetir.
/// Ojo: una partida ya empezada guarda la ropa que llevaba; el aspecto nuevo sale en partidas nuevas
/// (o cambiándoselo en el menú de equipo).
/// </summary>
public static class AspectoNuevoDeEstela
{
    const string Carpeta = "Assets/Art/Characters/Protagonistas/Estela";
    const string Pelo = "Hair14_Coleta", Cuerpo = "Body21_Hechicera", Capa = "Cloak04_Hechicera";
    const string PeloViejo = "Hair08", CuerpoViejo = "Body09", LazoViejo = "AC09_Ribbon", CapaVecina = "Cloak01|Body09";   // los modelos sin capas la llevan junto al cuerpo
    const string RutaEstela = "Assets/Prefabs/_ESTELA.prefab";
    static readonly string[] Modelos = { "Assets/Prefabs/_WILL.prefab", RutaEstela, "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" };
    /// <summary>Los que la enseñan siempre a ella: se les cambia el aspecto de partida.</summary>
    static readonly string[] SoloEstela = { RutaEstela, "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" };
    const string EscenaDelRegistro = "Assets/Scenes/Systems/Start.unity";

    static readonly (string material, Color color)[] Colores =
    {
        ("Pelo", new Color(0.95f, 0.3f, 0.06f)), ("PeloClaro", new Color(1f, 0.45f, 0.12f)), ("LazoAzul", new Color(0.2f, 0.45f, 0.95f)),
        ("Tunica", new Color(0.18f, 0.32f, 0.72f)), ("Capa", new Color(0.93f, 0.86f, 0.7f)), ("Hombrera", new Color(0.32f, 0.34f, 0.42f)),
        ("Gema", new Color(0.9f, 0.08f, 0.15f)), ("Cuero", new Color(0.48f, 0.28f, 0.14f)), ("Dorado", new Color(1f, 0.8f, 0.3f)),
    };

    [MenuItem("El Sendero/Personajes/Estela: aspecto de hechicera")]
    public static void Aplicar()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Estela", "Hazlo fuera de Play.", "Vale"); return; }
        var informe = new StringBuilder();

        // El material del cuerpo de siempre: estilo de los colores lisos y el de la parte de su cuerpo que va debajo.
        var estela = AssetDatabase.LoadAssetAtPath<GameObject>(RutaEstela);
        var materialDelCuerpo = estela?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == CuerpoViejo)?.sharedMaterial;
        if (materialDelCuerpo == null) { EditorUtility.DisplayDialog("Estela", $"No encuentro {CuerpoViejo} en {RutaEstela}.", "Vale"); return; }
        string estilo = AssetDatabase.GetAssetPath(materialDelCuerpo);
        foreach (var pieza in new[] { Pelo, Cuerpo, Capa })
        {
            string fbx = $"{Carpeta}/{pieza}.fbx";
            PersonajeHechoEnBlender.PrepararPrenda(fbx, Carpeta + "/Materiales", estilo, Colores, informe);
            if (AssetImporter.GetAtPath(fbx) is ModelImporter imp)
            {
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "lambert1"), materialDelCuerpo);
                imp.SaveAndReimport();
            }
        }
        var modeloPelo = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/{Pelo}.fbx");
        var modeloCuerpo = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/{Cuerpo}.fbx");
        var modeloCapa = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/{Capa}.fbx");

        foreach (var ruta in Modelos)
        {
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                bool ok = PiezaNuevaDelModelo.Anadir(raiz, modeloPelo, Pelo, PeloViejo, informe)
                        & PiezaNuevaDelModelo.Anadir(raiz, modeloCuerpo, Cuerpo, CuerpoViejo, informe)
                        & PiezaNuevaDelModelo.Anadir(raiz, modeloCapa, Capa, CapaVecina, informe);
                if (ok && SoloEstela.Contains(ruta))
                    PiezaNuevaDelModelo.Encender(raiz, new[] { Pelo, Cuerpo, Capa }, new[] { PeloViejo, CuerpoViejo, LazoViejo });
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        CambiarAspectoDePartida(informe);
        AssetDatabase.SaveAssets();
        Debug.Log("[AspectoNuevoDeEstela] " + informe.ToString().Replace('\n', ' '));
        EditorUtility.DisplayDialog("Estela: aspecto de hechicera", informe.ToString(), "Vale");
    }

    /// <summary>El aspecto con el que empieza Estela (CharacterAppearanceRegistry de Start): pelo y cuerpo nuevos, sin el lazo de antes.</summary>
    static void CambiarAspectoDePartida(StringBuilder informe)
    {
        var abierta = EditorSceneManager.GetSceneByPath(EscenaDelRegistro);
        bool cerrar = !abierta.isLoaded;
        if (cerrar && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { informe.AppendLine("Aspecto de partida: cancelado (escenas sin guardar)."); return; }
        var escena = cerrar ? EditorSceneManager.OpenScene(EscenaDelRegistro, OpenSceneMode.Additive) : abierta;
        try
        {
            var registro = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterAppearanceRegistry>(true)).FirstOrDefault();
            if (registro == null) { informe.AppendLine($"Aspecto de partida: no encuentro CharacterAppearanceRegistry en {EscenaDelRegistro}."); return; }
            var so = new SerializedObject(registro);
            var lista = so.FindProperty("estelaDefault");
            var entradas = new List<(int cat, string pieza)>();
            for (int i = 0; i < lista.arraySize; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                entradas.Add((e.FindPropertyRelative("category").intValue, e.FindPropertyRelative("partName").stringValue));
            }
            entradas.RemoveAll(e => e.cat == (int)PartCategory.Body || e.cat == (int)PartCategory.Hair || e.cat == (int)PartCategory.Cloak || e.pieza == LazoViejo);
            entradas.Add(((int)PartCategory.Body, Cuerpo));
            entradas.Add(((int)PartCategory.Cloak, Capa));
            entradas.Add(((int)PartCategory.Hair, Pelo));
            lista.arraySize = entradas.Count;
            for (int i = 0; i < entradas.Count; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("category").intValue = entradas[i].cat;
                e.FindPropertyRelative("partName").stringValue = entradas[i].pieza;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            informe.AppendLine($"Aspecto de partida de Estela: {string.Join(", ", entradas.Select(e => e.pieza))}.");
        }
        finally { if (cerrar) EditorSceneManager.CloseScene(escena, true); }
    }
}
