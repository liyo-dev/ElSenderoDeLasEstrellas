#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Game.NPC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Informe de solo lectura: qué hace cada NPC de una escena en cada tramo de la historia.
/// Junta los roster (`NpcRosterSO`), la configuración ambiental de cada prefab (oficio, paseo,
/// actividad), las agendas (`Resources/Agendas`) y todo componente de la escena o del prefab que
/// tenga un campo `TramoDeHistoria` (rutas de trabajo, discusiones, objetos por tramo…).
/// No modifica nada: si la escena no está abierta, la abre aditiva y la vuelve a cerrar.
/// Escribe «Claude outputs/npcs-por-tramo-&lt;escena&gt;.md».
public static class InformeDeNpcsPorTramo
{
    private const string Escena = "MainWorld";

    [MenuItem("El Sendero/NPCs/Informe: qué hace cada NPC por tramo (MainWorld)")]
    public static void GenerarMainWorld() => Generar(Escena);

    public static void Generar(string nombreEscena)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[InformeDeNpcsPorTramo] Sal de Play."); return; }

        var sb = new StringBuilder($"# Qué hace cada NPC por tramo · {nombreEscena}\n\n");
        Scene escena = SceneManager.GetSceneByName(nombreEscena);
        bool abiertaAqui = false;
        if (!escena.IsValid() || !escena.isLoaded)
        {
            string ruta = null;
            foreach (var g in AssetDatabase.FindAssets($"{nombreEscena} t:Scene"))
            {
                var r = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(r) == nombreEscena) { ruta = r; break; }
            }
            if (ruta == null) { Debug.LogWarning($"[InformeDeNpcsPorTramo] No encuentro {nombreEscena}."); return; }
            escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
            abiertaAqui = true;
        }

        try
        {
            Roster(sb, escena);
            ComponentesConTramo(sb, escena);
            Agendas(sb);
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }

        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Claude outputs");
        Directory.CreateDirectory(carpeta);
        string salida = Path.Combine(carpeta, $"npcs-por-tramo-{nombreEscena}.md");
        File.WriteAllText(salida, sb.ToString());
        OrdenesDelEditor.Avisar("Informe de NPCs", "Escrito en " + salida);
    }

    private static void Roster(StringBuilder sb, Scene escena)
    {
        sb.Append("## Roster y configuración ambiental\n\n");
        foreach (var g in AssetDatabase.FindAssets("t:NpcRosterSO"))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(g));
            var so = new SerializedObject(asset);
            var entradas = so.FindProperty("entries");
            if (entradas == null) continue;
            sb.Append($"### {asset.name}\n\n");
            for (int i = 0; i < entradas.arraySize; i++)
            {
                var e = entradas.GetArrayElementAtIndex(i);
                string id = e.FindPropertyRelative("persistenceId")?.stringValue;
                string spawn = e.FindPropertyRelative("spawnId")?.stringValue;
                string flag = e.FindPropertyRelative("requiredFlag")?.stringValue;
                bool inv = e.FindPropertyRelative("invertCondition")?.boolValue ?? false;
                var prefab = e.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;
                var marca = Buscar(escena, spawn);
                sb.Append($"- **{(string.IsNullOrEmpty(id) ? "(sin id)" : id)}** · prefab `{(prefab ? prefab.name : "-")}` · spawn `{spawn}`");
                if (marca) sb.Append($" en {Pos(marca.position)}");
                if (!string.IsNullOrEmpty(flag)) sb.Append($" · aparece {(inv ? "hasta" : "desde")} `{flag}`");
                sb.Append('\n');
                if (prefab == null) continue;
                var mgr = prefab.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
                var amb = mgr != null && mgr.Configuration != null ? mgr.Configuration.ambientConfig : null;
                if (amb != null)
                    sb.Append($"  - ambiente `{amb.name}`: pasea={amb.enableWander} (r={amb.wanderRadius}) · actividad={amb.ambientActivity} · oficio={amb.permitirTrabajo} `{amb.puestoId}` · refugio={amb.canSeekShelter}\n");
                foreach (var mb in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                    foreach (var linea in Tramos(mb))
                        sb.Append($"  - en el prefab, {linea}\n");
            }
            sb.Append('\n');
        }
    }

    private static void ComponentesConTramo(StringBuilder sb, Scene escena)
    {
        sb.Append("## Componentes de la escena con tramo de historia\n\n");
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var mb in raiz.GetComponentsInChildren<MonoBehaviour>(true))
                foreach (var linea in Tramos(mb))
                    sb.Append($"- `{Ruta(mb.transform)}` {(mb.gameObject.activeInHierarchy ? "" : "(apagado) ")}{linea}\n");
        sb.Append('\n');
    }

    /// Una línea por campo `TramoDeHistoria` del componente, con los datos que lo explican.
    private static IEnumerable<string> Tramos(MonoBehaviour mb)
    {
        if (mb == null) yield break;
        var so = new SerializedObject(mb);
        var p = so.GetIterator();
        bool entrar = true;
        while (p.NextVisible(entrar))
        {
            entrar = p.propertyType == SerializedPropertyType.Generic && p.type != "TramoDeHistoria" && !p.isArray;
            if (p.type != "TramoDeHistoria") continue;
            string extra = "";
            if (mb is NPCWorkRoute r)
                extra = $" puesto=`{r.puestoId}` recogida={Pos(r.puntoRecogida)} entrega={Pos(r.puntoEntrega)} terminaAlVaciar={r.terminarAlVaciarOrigen} alTerminar=`{(r.puntoAlTerminar ? r.puntoAlTerminar.name : "-")}`";
            else if (mb is DiscusionEnBucle)
                extra = $" `{so.FindProperty("miId")?.stringValue}` con `{so.FindProperty("conQuienId")?.stringValue}`";
            yield return $"**{mb.GetType().Name}**.{p.name}:{extra} · {Tramo(p)}";
        }
    }

    private static void Agendas(StringBuilder sb)
    {
        sb.Append("## Agendas (Resources/Agendas)\n\n");
        foreach (var ag in Resources.LoadAll<ScriptableObject>("Agendas"))
        {
            var so = new SerializedObject(ag);
            sb.Append($"- **{so.FindProperty("actorId")?.stringValue}** (`{ag.name}`)\n");
            var en = so.FindProperty("entradas");
            for (int i = 0; en != null && i < en.arraySize; i++)
            {
                var e = en.GetArrayElementAtIndex(i);
                var pres = e.FindPropertyRelative("presencia");
                sb.Append($"  - «{e.FindPropertyRelative("nombre").stringValue}» · presencia={pres.enumNames[pres.enumValueIndex]} `{e.FindPropertyRelative("marca").stringValue}` · frases={e.FindPropertyRelative("frases").arraySize} · {Tramo(e.FindPropertyRelative("tramo"))}\n");
            }
        }
    }

    private static string Tramo(SerializedProperty t)
    {
        string L(string n)
        {
            var a = t.FindPropertyRelative(n);
            if (a == null || a.arraySize == 0) return null;
            var l = new List<string>();
            for (int i = 0; i < a.arraySize; i++) l.Add(a.GetArrayElementAtIndex(i).stringValue);
            return string.Join(", ", l);
        }
        var partes = new List<string>();
        void A(string etiqueta, string v) { if (v != null) partes.Add($"{etiqueta} [{v}]"); }
        A("empieza señal", L("empiezaConSenal"));
        A("empieza misión activa", L("empiezaConMisionActiva"));
        A("acaba señal", L("acabaConSenal"));
        A("acaba misión iniciada", L("acabaConMisionIniciada"));
        A("acaba misión completada", L("acabaConMisionCompletada"));
        return partes.Count == 0 ? "siempre" : string.Join(" · ", partes);
    }

    private static Transform Buscar(Scene escena, string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
        return null;
    }

    private static string Pos(Transform t) => t ? Pos(t.position) : "-";
    private static string Pos(Vector3 v) => $"({v.x:F0}, {v.z:F0})";

    private static string Ruta(Transform t)
    {
        string r = t.name;
        for (var q = t.parent; q != null; q = q.parent) r = q.name + "/" + r;
        return r;
    }
}
#endif
