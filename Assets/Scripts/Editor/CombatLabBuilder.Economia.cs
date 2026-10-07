using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Zona de economía del CombatLab, en la entrada del campo sur: Renard (tienda de Esencia y
/// contratos de caza), Tomasa (tienda de Moneda), placas que dan Esencia y Monedas, y un corral
/// de arañas que se reponen solas al fondo del campo. Arranca el grafo de misiones secundarias
/// para que salgan la presentación de Renard y sus contratos. Ver INC-635, INC-636 e INC-637.
public static partial class CombatLabBuilder
{
    private const string RaizEconomia = "LAB_ECONOMIA (tiendas, Esencia y contratos)";
    private const string GrafoSecundario = "Misiones Secundarias";

    [MenuItem("El Sendero/Combate/CombatLab: montar tiendas, Esencia y contratos")]
    public static void MontarEconomia()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("CombatLab", "Sal de Play para montar la zona.", "Aceptar");
            return;
        }
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("CombatLab", "No existe el LAB. Créalo antes con «Crear o regenerar el LAB».", "Aceptar");
            return;
        }

        var escena = SceneManager.GetSceneByPath(ScenePath);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        var avisos = CrearZonaEconomia(escena);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);

        string texto = avisos.Count == 0
            ? "Zona de economía montada en el campo sur del CombatLab."
            : "Zona montada con avisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("CombatLab", texto, "Aceptar");
    }

    /// Crea (o rehace) la zona dentro de la escena indicada. Lo que falte se avisa y se salta.
    private static List<string> CrearZonaEconomia(Scene escena)
    {
        var avisos = new List<string>();
        foreach (var raizExistente in escena.GetRootGameObjects())
            if (raizExistente.name == RaizEconomia) Object.DestroyImmediate(raizExistente);

        var raiz = new GameObject(RaizEconomia);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        var r = raiz.transform;

        CrearTexto("Tiendas, Esencia y contratos", r, new Vector3(0f, 4.2f, -22f), 180f);

        var renard = Instanciar("Assets/_NPCs/Pueblo/Generados/Renard.prefab", "Renard", r,
            new Vector3(7f, 0f, -27f), 0f, avisos, "Ejecuta antes «Montar la tienda de Renard».");
        if (renard != null) CrearTexto("Renard (Esencia)", r, new Vector3(7f, 3.2f, -27f), 180f);

        var tomasa = Instanciar("Assets/_NPCs/Pueblo/Generados/Tomasa.prefab", "Tomasa", r,
            new Vector3(-7f, 0f, -27f), 0f, avisos, null);
        if (tomasa != null) CrearTexto("Tomasa (Moneda)", r, new Vector3(-7f, 3.2f, -27f), 180f);

        var placaEsencia = GetMaterial("Lab_PlacaEsencia", new Color(0.62f, 0.38f, 0.95f));
        var placaMoneda = GetMaterial("Lab_PlacaMoneda", new Color(0.95f, 0.75f, 0.20f));
        Placa("Placa: +100 Esencia", "Assets/_ITEMS/IT_Esencia.asset", 100, r, new Vector3(3f, 0f, -23f), placaEsencia, avisos);
        Placa("Placa: +100 Monedas", "Assets/_ITEMS/IT_Coin.asset", 100, r, new Vector3(-3f, 0f, -23f), placaMoneda, avisos);

        var arana = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/Spider1.prefab");
        if (arana != null)
        {
            var corral = new GameObject("Corral de arañas (se reponen solas)");
            corral.transform.SetParent(r);
            corral.transform.position = new Vector3(25f, 0f, -82f);
            corral.AddComponent<CorralDeEnemigosDelLab>().Configurar(arana, 6, 6f);
            CrearTexto("Arañas: Esencia y contratos", r, new Vector3(25f, 4f, -74f), 180f);
        }
        else avisos.Add("No encuentro Spider1.prefab: no hay corral de arañas.");

        var grafo = new GameObject("Grafo de misiones secundarias (Renard y sus contratos)");
        grafo.transform.SetParent(r);
        var arranque = grafo.AddComponent<NarrativeGraphStarter>();
        var so = new SerializedObject(arranque);
        var etiquetas = so.FindProperty("graphLabels");
        etiquetas.arraySize = 1;
        etiquetas.GetArrayElementAtIndex(0).stringValue = GrafoSecundario;
        so.ApplyModifiedPropertiesWithoutUndo();
        return avisos;
    }

    private static GameObject Instanciar(string ruta, string nombre, Transform padre, Vector3 posicion, float giro,
                                         List<string> avisos, string pista)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab == null)
        {
            avisos.Add($"Falta {ruta}." + (string.IsNullOrEmpty(pista) ? "" : " " + pista));
            return null;
        }
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        instancia.name = nombre;
        instancia.transform.SetPositionAndRotation(posicion, Quaternion.Euler(0f, giro, 0f));
        return instancia;
    }

    private static void Placa(string nombre, string rutaObjeto, int cantidad, Transform padre, Vector3 posicion,
                              Material material, List<string> avisos)
    {
        var objeto = AssetDatabase.LoadAssetAtPath<ItemData>(rutaObjeto);
        if (objeto == null)
        {
            avisos.Add($"Falta {rutaObjeto}: no se crea «{nombre}».");
            return;
        }
        var placa = CrearCubo(nombre, padre, posicion + Vector3.up * 0.05f, new Vector3(2f, 0.1f, 2f), material, estatico: false);
        var caja = placa.GetComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = new Vector3(1f, 20f, 1f);
        caja.center = new Vector3(0f, 10f, 0f);
        placa.AddComponent<PlacaDeRecursosDelLab>().Configurar(objeto, cantidad);
        CrearTexto(nombre.Replace("Placa: ", ""), padre, posicion + Vector3.up * 1.6f, 180f);
    }
}
