#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Herramientas de un solo uso de la fase 6 del rediseño (INC-867). Ya se ejecutaron: quitar los
/// planos colocados a mano de WillHouse y crear «Guiones/EntradaPanico.asset» (la acción del botón
/// del Despertar para InputPromptBeat). Quedan el montaje del Despertar con guion y el volcado de
/// posiciones para escribir los guiones del Cap. 1.
public static class Fase6Preparativos
{
    private const string Modulos = "Assets/_SEQUENCES/Modulos/SEQ_StarAwakening_Modulos.prefab";

    /// Monta el Despertar con guion en sus dos fases de jugabilidad: la del botón («3. El botón») y
    /// la de la derrota («4. Sin reacción»), que pasan a ser un GuionBeat cada una. Quita del
    /// prefab de módulos el PanicInputDetector, que ya no usa nadie (la prueba la pide el guion).
    [MenuItem("El Sendero/Archivo/Fase 6: montar despertar")]
    public static void MontarDespertar()
    {
        const string ruta = "Assets/_SEQUENCES/SEQ_StarAwakening.asset";
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(ruta);
        if (def == null) { Debug.LogError("[Fase 6] No encuentro " + ruta); return; }
        int cambiadas = 0;
        foreach (var fase in def.phases)
        {
            if (fase == null) continue;
            string guion = fase.name.StartsWith("5 ") ? "Despertar_3_ElBoton"
                : fase.name.StartsWith("8 ") ? "Despertar_4_SinReaccion" : null;
            if (guion == null) continue;
            fase.name = fase.name.StartsWith("5 ") ? "5 - El boton (guion)" : "8 - Si Will no reacciona a tiempo (guion)";
            fase.beats = new System.Collections.Generic.List<SequenceBeat> { new GuionBeat { note = "Guion: " + guion } };
            cambiadas++;
        }
        def.summary = "Eldran avisa. Will se gira y ve venir el proyectil. Eldran sale corriendo a esconderse " +
            "gritandole que se aparte, y Will se queda solo y se planta en pose de combate. El plano se abre " +
            "a general y ahi el jugador tiene que reaccionar. Si acierta, devuelve el golpe; si falla, la bola " +
            "le alcanza y es Game Over. Las cuatro partes son guiones horneados.";
        EditorUtility.SetDirty(def);

        var prefab = PrefabUtility.LoadPrefabContents(Modulos);
        var detector = prefab.GetComponent<PanicInputDetector>();
        if (detector != null) Object.DestroyImmediate(detector);
        PrefabUtility.SaveAsPrefabAsset(prefab, Modulos);
        PrefabUtility.UnloadPrefabContents(prefab);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Fase 6] Despertar: {cambiadas} fases pasadas a guion.");
    }

    /// Quita MainWorld de las escenas abiertas (si no tiene cambios) para hornear un interior: su
    /// terreno pasa por encima de las casas y el horneado lo toma por decorado que tapa el plano.
    [MenuItem("El Sendero/Archivo/Fase 6: cerrar MainWorld")]
    public static void CerrarMainWorld()
    {
        var esc = SceneManager.GetSceneByName("MainWorld");
        if (!esc.IsValid() || !esc.isLoaded) return;
        if (esc.isDirty) { Debug.LogError("[Fase 6] MainWorld tiene cambios sin guardar; no la cierro."); return; }
        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(esc, true);
    }

    [MenuItem("El Sendero/Archivo/Fase 6: abrir MainWorld")]
    public static void AbrirMainWorld()
    {
        var esc = SceneManager.GetSceneByName("MainWorld");
        if (esc.IsValid() && esc.isLoaded) return;
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Worlds/MainWorld.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Additive);
    }

    /// Apunta en Logs/claude/posiciones_fase6.txt dónde están los disparadores, marcas y puntos de
    /// las escenas de las cuatro cinemáticas del Cap. 1, para escribir sus guiones.
    [MenuItem("El Sendero/Archivo/Fase 6: posiciones")]
    public static void Posiciones()
    {
        var sb = new System.Text.StringBuilder();
        string[] nombres = { "WILL_EXITS_HOUSE", "WILL_REACHED_ELDRAN", "House_FrontDoor", "SPAWN_", "Ventana",
            "Discusion", "Oficio_", "Mercado", "Tomasa", "Eldran", "Victoria", "Oliver", "Peras", "Caja", "Cama", "Bed",
            "Despertar", "Will" };
        for (int k = 0; k < SceneManager.sceneCount; k++)
        {
            var esc = SceneManager.GetSceneAt(k);
            if (!esc.isLoaded) continue;
            sb.AppendLine("## " + esc.name);
            foreach (var raiz in esc.GetRootGameObjects())
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name;
                    bool vale = false;
                    foreach (var x in nombres) if (n.IndexOf(x, System.StringComparison.OrdinalIgnoreCase) >= 0) { vale = true; break; }
                    if (!vale) continue;
                    if (t.GetComponent<Renderer>() != null && t.GetComponent<Collider>() == null) continue;
                    var p = t.position;
                    string extra = "";
                    var c = t.GetComponent<Collider>();
                    if (c != null) extra = $" caja=({c.bounds.center.x:0.00},{c.bounds.center.y:0.00},{c.bounds.center.z:0.00}) tam=({c.bounds.size.x:0.0},{c.bounds.size.y:0.0},{c.bounds.size.z:0.0})";
                    var comps = string.Join(",", System.Array.ConvertAll(t.GetComponents<Component>(), q => q == null ? "?" : q.GetType().Name));
                    sb.AppendLine($"{RutaDe(t)} pos=({p.x:0.00},{p.y:0.00},{p.z:0.00}) rumbo={t.eulerAngles.y:0}{extra} [{comps}]");
                }
        }
        foreach (var cama in Object.FindObjectsByType<SleepTrigger>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(cama);
            foreach (var campo in new[] { "wakeUpPosition", "bedPosition", "wakeUpCameraAnchor" })
            {
                var t = so.FindProperty(campo)?.objectReferenceValue as Transform;
                if (t != null) sb.AppendLine($"SleepTrigger {cama.name}.{campo} pos=({t.position.x:0.00},{t.position.y:0.00},{t.position.z:0.00}) rumbo={t.eulerAngles.y:0}");
            }
        }
        foreach (var jugador in Object.FindObjectsByType<SequencePlayer>(FindObjectsInactive.Include))
        {
            var so = new SerializedObject(jugador);
            string Nombre(string campo) { var o = so.FindProperty(campo)?.objectReferenceValue; return o != null ? o.name : "-"; }
            sb.AppendLine($"Player {jugador.name} def={Nombre("_definition")} entrada={Nombre("_entryTransition")} salida={Nombre("_exitTransition")} interior={Nombre("_interiorAnchor")}");
        }
        foreach (var stage in Object.FindObjectsByType<SequenceStage>(FindObjectsInactive.Include))
            foreach (var m in stage.Marks)
                if (m.target != null)
                    sb.AppendLine($"Stage {stage.name} marca {m.name}/{m.target.name} pos=({m.target.position.x:0.00},{m.target.position.y:0.00},{m.target.position.z:0.00}) rumbo={m.target.eulerAngles.y:0}");
        string ruta = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "claude", "posiciones_fase6.txt");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ruta));
        System.IO.File.WriteAllText(ruta, sb.ToString());
    }

    private static string RutaDe(Transform t) => t.parent == null ? t.name : RutaDe(t.parent) + "/" + t.name;
}
#endif
