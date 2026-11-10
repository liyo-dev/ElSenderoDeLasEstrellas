#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Game.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Desde el Cap. 2, Victoria y Oliver charlan en el puesto de Victoria en vez de esperar quietos
/// cada uno por su lado. Idempotente:
/// - dos marcas en MainWorld junto a `SP_Victoria` (el sitio de Victoria al acabar su oficio);
/// - una entrada nueva, la primera, en las agendas de los dos (en su marca, con sus frases de siempre);
/// - una `DiscusionEnBucle` en modo charla de ambiente en el prefab de Victoria (se les puede hablar).
/// Ver INC-958.
public static class CharlaDeVictoriaYOliver
{
    private const string Desde = "CH_Cap1_BACK_1";
    private const string MarcaVictoria = "Cap2_VictoriaCharla";
    private const string MarcaOliver = "Cap2_OliverCharla";
    private const string PrefabVictoria = "7c5203c6842c1184db2ce618207f3165";
    private const float Separacion = 1.6f;

    [MenuItem("El Sendero/Archivo/NPCs: Victoria y Oliver charlan desde el Cap. 2 (INC-958)")]
    public static void Montar()
    {
        if (EditorApplication.isPlaying) { OrdenesDelEditor.Avisar("Charla", "Sal de Play."); return; }
        var informe = new StringBuilder();
        if (!Marcas(informe)) { OrdenesDelEditor.Avisar("Charla", informe.ToString()); return; }
        Agenda("Agenda_Victoria", MarcaVictoria, "Desde el Cap. 2 · charla con Oliver en su puesto", informe);
        Agenda("Agenda_Oliver", MarcaOliver, "Desde el Cap. 2 · charla con Victoria en su puesto", informe);
        Prefab(informe);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Charla de Victoria y Oliver", informe.ToString());
    }

    private static bool Marcas(StringBuilder informe)
    {
        Scene escena = SceneManager.GetSceneByName("MainWorld");
        bool abiertaAqui = false;
        if (!escena.isLoaded)
        {
            escena = EditorSceneManager.OpenScene("Assets/Scenes/Worlds/MainWorld.unity", OpenSceneMode.Additive);
            abiertaAqui = true;
        }
        else if (escena.isDirty)
        {
            informe.Append("MainWorld tiene cambios sin guardar: guárdala y vuelve a ejecutarlo.");
            return false;
        }

        try
        {
            Transform sitio = null, padreOficios = null;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "SP_Victoria") sitio = t;
                    if (t.name == "Oficios del mercado") padreOficios = t;
                }
            if (sitio == null) { informe.Append("No encuentro SP_Victoria en MainWorld."); return false; }

            Vector3 pv = Suelo(sitio.position);
            Vector3 adelante = sitio.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
            adelante.Normalize();
            // Oliver se pone a su lado y un poco delante, para que se vean de tres cuartos desde la calle.
            Vector3 lado = Vector3.Cross(Vector3.up, adelante);
            Vector3 po = Suelo(pv + (lado * 0.8f + adelante).normalized * Separacion);

            var padre = padreOficios != null ? padreOficios : sitio.parent;
            Anclar(padre, MarcaVictoria, pv, po - pv);
            Anclar(padre, MarcaOliver, po, pv - po);
            informe.Append($"Marcas: {MarcaVictoria} en ({pv.x:F1}, {pv.z:F1}), {MarcaOliver} en ({po.x:F1}, {po.z:F1}). ");

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            return true;
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static Vector3 Suelo(Vector3 p) =>
        NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas) ? hit.position : p;

    private static void Anclar(Transform padre, string id, Vector3 pos, Vector3 mirar)
    {
        SpawnAnchor ancla = null;
        foreach (var a in padre.GetComponentsInChildren<SpawnAnchor>(true))
            if (a.anchorId == id) ancla = a;
        if (ancla == null)
        {
            var go = new GameObject(id);
            go.transform.SetParent(padre, false);
            ancla = go.AddComponent<SpawnAnchor>();
            ancla.anchorId = id;
        }
        mirar.y = 0f;
        ancla.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(mirar.normalized, Vector3.up));
        EditorUtility.SetDirty(ancla.gameObject);
    }

    private static void Agenda(string nombre, string marca, string titulo, StringBuilder informe)
    {
        var agenda = AssetDatabase.LoadAssetAtPath<AgendaDelActor>($"Assets/Resources/Agendas/{nombre}.asset");
        if (agenda == null) { informe.Append($"No encuentro {nombre}. "); return; }

        agenda.entradas.RemoveAll(e => e != null && e.nombre == titulo);
        var frases = new List<NarrativeDialogueAsset>();
        foreach (var e in agenda.entradas)
            if (e != null && e.frases != null) frases.AddRange(e.frases);

        agenda.entradas.Insert(0, new EntradaDeAgenda
        {
            nombre = titulo,
            tramo = new TramoDeHistoria { empiezaConSenal = new List<string> { Desde } },
            presencia = PresenciaEnAgenda.EnUnaMarca,
            marca = marca,
            frases = frases,
        });
        EditorUtility.SetDirty(agenda);
        informe.Append($"{nombre}: entrada «{titulo}» con {frases.Count} frases. ");
    }

    private static void Prefab(StringBuilder informe)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(PrefabVictoria);
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            DiscusionEnBucle charla = null;
            foreach (var d in raiz.GetComponents<DiscusionEnBucle>())
                if (new SerializedObject(d).FindProperty("conQuienId").stringValue == "NPC_Oliver") charla = d;
            if (charla == null) charla = raiz.AddComponent<DiscusionEnBucle>();

            var so = new SerializedObject(charla);
            so.FindProperty("miId").stringValue = "NPC_Victoria";
            so.FindProperty("conQuienId").stringValue = "NPC_Oliver";
            Lista(so.FindProperty("tramo.empiezaConSenal"), Desde);
            Lista(so.FindProperty("tramo.empiezaConMisionActiva"));
            Lista(so.FindProperty("tramo.acabaConSenal"));
            Lista(so.FindProperty("tramo.acabaConMisionIniciada"));
            Lista(so.FindProperty("tramo.acabaConMisionCompletada"));
            Lista(so.FindProperty("replica"), "Laugh01", "Question01", "Cheer01");
            Lista(so.FindProperty("hablar"), "Talk01", "Talk02", "Talk03");
            so.FindProperty("animo").intValue = (int)NPCEmotion.Happy;
            so.FindProperty("turno").floatValue = 2.4f;
            // Sin icono de charla: Victoria y Oliver llevan encima el de misión en el Cap. 2.
            so.FindProperty("iconoDePelea").objectReferenceValue = null;
            so.FindProperty("seLesPuedeHablar").boolValue = true;
            so.FindProperty("distanciaParaCharlar").floatValue = 3.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            informe.Append($"Prefab {raiz.name}: charla con Oliver puesta.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static void Lista(SerializedProperty p, params string[] valores)
    {
        p.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) p.GetArrayElementAtIndex(i).stringValue = valores[i];
    }
}
#endif
