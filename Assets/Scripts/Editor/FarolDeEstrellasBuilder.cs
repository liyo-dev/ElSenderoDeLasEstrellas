#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Cambia el aspecto de los puntos de guardado por el farol de estrellas (INC-1041), que sustituye al
/// santuario de pedestal y cristal (INC-1009) y a los círculos mágicos que quedaran.
///
/// 1) Configura el modelo Assets/Art/World/Santuario/FarolDeEstrellas.obj (lo genera
///    Herramientas/generar_farol.py: farol de piedra con tejado azul, estrella dentro y losas alrededor).
/// 2) Crea los materiales propios del farol (copias del material del pack con color liso; el pack no se toca).
/// 3) Crea (o rehace) Assets/Prefabs/Interactables/FarolDeEstrellas.prefab con la luz, el collider y el
///    componente FarolDeEstrellas.
/// 4) Recorre Assets/Scenes y en cada SavePoint quita el santuario o el círculo, pone el farol apoyado en
///    el suelo y sube el icono de «pulsa para guardar» por encima del tejado. NO toca el SavePoint, el
///    Interactable, el trigger ni el teletransporte.
///
/// Idempotente: un punto que ya tiene el farol solo se recoloca el icono; volver a ejecutarlo rehace
/// el prefab y todos los faroles puestos se actualizan solos.
public static class FarolDeEstrellasBuilder
{
    const string Carpeta = "Assets/Art/World/Santuario";
    const string RutaModelo = Carpeta + "/FarolDeEstrellas.obj";
    const string RutaPrefab = "Assets/Prefabs/Interactables/FarolDeEstrellas.prefab";
    const string MaterialDelPack = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/DefaultPBR.mat";
    const string PrefabChispas = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode yellow.prefab";
    const string CarpetaDeEscenas = "Assets/Scenes";

    const string NombreDelFarol = "FarolDeEstrellas";
    const string NombreDelSantuarioViejo = "SantuarioDeGuardado";

    // Medidas del modelo (metros, ver generar_farol.py). Para hacerlo todo más grande o más pequeño,
    // cambiar solo EscalaDelFarol.
    const float EscalaDelFarol = 1f;
    const float AlturaDeLaEstrella = 1.12f;
    const float AlturaDelFarol = 1.95f;
    const float HuecoSobreElTejado = 0.35f;
    const float RadioDelCollider = 0.33f;

    static readonly Color ColorPiedra = new Color(0.78f, 0.74f, 0.66f);
    static readonly Color ColorTejado = new Color(0.33f, 0.40f, 0.58f);
    static readonly Color ColorLosas = new Color(0.70f, 0.67f, 0.60f);
    static readonly Color ColorEstrella = new Color(1f, 0.84f, 0.30f);
    static readonly Color ColorEstrellaApagada = new Color(0.56f, 0.56f, 0.60f);
    static readonly Color ColorDeLaLuz = new Color(1f, 0.83f, 0.52f);

    [MenuItem("El Sendero/Escenario/Farol de estrellas: crear y poner en todos los puntos de guardado (INC-1041)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Farol de estrellas", "Sal del modo Play antes de ejecutarlo.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var prefab = CrearPrefab();
        if (prefab == null) return;

        var informe = new StringBuilder();
        int puestos = 0;
        var rutas = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { CarpetaDeEscenas }))
            rutas.Add(AssetDatabase.GUIDToAssetPath(guid));

        try
        {
            for (int i = 0; i < rutas.Count; i++)
            {
                string ruta = rutas[i];
                EditorUtility.DisplayProgressBar("Farol de estrellas",
                    $"Buscando puntos de guardado en {Path.GetFileNameWithoutExtension(ruta)}…", (float)i / rutas.Count);

                var escena = EditorSceneManager.GetSceneByPath(ruta);
                bool estabaAbierta = escena.isLoaded;
                if (!estabaAbierta) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
                try
                {
                    int cambios = PonerEnLaEscena(escena, prefab, informe, out int nuevos);
                    if (cambios > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(escena);
                        EditorSceneManager.SaveScene(escena);
                        puestos += nuevos;
                    }
                }
                finally
                {
                    if (!estabaAbierta) EditorSceneManager.CloseScene(escena, true);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string resumen = puestos > 0
            ? $"Faroles puestos: {puestos}."
            : "No he puesto ningún farol nuevo (ya lo tenían o no hay puntos de guardado en Assets/Scenes).";
        Debug.Log("[Farol de estrellas] " + resumen + "\n" + informe);
        OrdenesDelEditor.Avisar("Farol de estrellas", resumen + "\n\n" + informe);
    }

    // ───────────────────────────── Modelo y materiales ─────────────────────────────

    static void ConfigurarModelo()
    {
        var imp = AssetImporter.GetAtPath(RutaModelo) as ModelImporter;
        if (imp == null) return;
        bool cambia = imp.globalScale != 1f
                      || imp.importNormals != ModelImporterNormals.Import
                      || imp.materialImportMode != ModelImporterMaterialImportMode.None
                      || imp.addCollider
                      || imp.importAnimation;
        if (!cambia) return;
        imp.globalScale = 1f;
        imp.importNormals = ModelImporterNormals.Import;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.addCollider = false;
        imp.importAnimation = false;
        imp.SaveAndReimport();
    }

    /// Material liso a partir del del pack (mismo shader toon y contorno), sin la textura de paleta.
    static Material MaterialLiso(Material origen, string nombre, Color color, bool brilla, float fuerzaDelBrillo = 1f)
    {
        string ruta = $"{Carpeta}/{nombre}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null)
        {
            m = new Material(origen) { name = nombre };
            AssetDatabase.CreateAsset(m, ruta);
        }
        foreach (var t in new[] { "_BaseMap", "_MainTex", "_EmissionMap" })
            if (m.HasProperty(t)) m.SetTexture(t, null);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);

        if (brilla)
        {
            if (m.HasProperty("_Emission")) m.SetFloat("_Emission", 1f);
            if (m.HasProperty("_EmissionColorUI")) m.SetColor("_EmissionColorUI", color);
            if (m.HasProperty("_EmissionScaleUI")) m.SetFloat("_EmissionScaleUI", fuerzaDelBrillo);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * fuerzaDelBrillo);
            m.EnableKeyword("_EMISSION");
        }
        else
        {
            if (m.HasProperty("_Emission")) m.SetFloat("_Emission", 0f);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    // ───────────────────────────── Prefab ─────────────────────────────

    static GameObject CrearPrefab()
    {
        ConfigurarModelo();
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaModelo);
        var origen = AssetDatabase.LoadAssetAtPath<Material>(MaterialDelPack);
        if (modelo == null || origen == null)
        {
            string falta = modelo == null ? RutaModelo : MaterialDelPack;
            Debug.LogError("[Farol de estrellas] No encuentro " + falta);
            OrdenesDelEditor.Avisar("Farol de estrellas", "No encuentro " + falta + ". No he cambiado nada.");
            return null;
        }
        var chispas = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabChispas); // opcional
        if (chispas == null) Debug.LogWarning("[Farol de estrellas] No encuentro «Sparks explode yellow»: el farol se encenderá sin chispas.");

        var matPiedra = MaterialLiso(origen, "FarolPiedra", ColorPiedra, false);
        var matTejado = MaterialLiso(origen, "FarolTejado", ColorTejado, false);
        var matLosas = MaterialLiso(origen, "FarolLosas", ColorLosas, false);
        var matEncendida = MaterialLiso(origen, "FarolEstrellaEncendida", ColorEstrella, true, 1.6f);
        var matApagada = MaterialLiso(origen, "FarolEstrellaApagada", ColorEstrellaApagada, false);

        GameObject resultado;
        var taller = EditorSceneManager.NewPreviewScene();
        try
        {
            var raiz = new GameObject(NombreDelFarol);
            SceneManager.MoveGameObjectToScene(raiz, taller);

            var cuerpo = (GameObject)PrefabUtility.InstantiatePrefab(modelo, taller);
            PrefabUtility.UnpackPrefabInstance(cuerpo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            cuerpo.name = "Modelo";
            cuerpo.transform.SetParent(raiz.transform, false);
            cuerpo.transform.localPosition = Vector3.zero;
            cuerpo.transform.localRotation = Quaternion.identity;
            cuerpo.transform.localScale = Vector3.one * EscalaDelFarol;

            Renderer estrellaRend = null;
            var faltan = new List<string> { "Piedra", "Tejado", "Losas", "Estrella" };
            foreach (var r in cuerpo.GetComponentsInChildren<Renderer>(true))
            {
                Material m = null;
                string n = r.gameObject.name;
                if (n.Contains("Piedra")) m = matPiedra;
                else if (n.Contains("Tejado")) m = matTejado;
                else if (n.Contains("Losas")) m = matLosas;
                else if (n.Contains("Estrella")) { m = matApagada; estrellaRend = r; }
                if (m == null) continue;
                faltan.RemoveAll(f => n.Contains(f));
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                r.shadowCastingMode = n.Contains("Losas") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            if (faltan.Count > 0)
                Debug.LogWarning("[Farol de estrellas] En el modelo no encuentro: " + string.Join(", ", faltan));

            // La estrella: un pivote a su altura (la malla viene centrada en su origen).
            Transform pivote = null;
            if (estrellaRend != null)
            {
                pivote = new GameObject("Estrella").transform;
                pivote.SetParent(raiz.transform, false);
                pivote.localPosition = new Vector3(0f, AlturaDeLaEstrella * EscalaDelFarol, 0f);
                var t = estrellaRend.transform;
                t.name = "Malla";
                t.SetParent(pivote, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one * EscalaDelFarol;
            }

            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(raiz.transform, false);
            luzGo.transform.localPosition = new Vector3(0f, AlturaDeLaEstrella * EscalaDelFarol, 0f);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = ColorDeLaLuz;
            luz.range = 5f * EscalaDelFarol;
            luz.intensity = 1.4f;
            luz.shadows = LightShadows.None;
            luz.enabled = false; // apagado hasta que el punto se descubra

            // Que no se atraviese, pero sin ocupar tanto que Will no llegue al trigger del punto.
            var col = raiz.AddComponent<CapsuleCollider>();
            col.radius = RadioDelCollider * EscalaDelFarol;
            col.height = AlturaDelFarol * EscalaDelFarol;
            col.center = new Vector3(0f, AlturaDelFarol * EscalaDelFarol * 0.5f, 0f);

            var farol = raiz.AddComponent<FarolDeEstrellas>();
            var so = new SerializedObject(farol);
            so.FindProperty("estrella").objectReferenceValue = pivote;
            so.FindProperty("estrellaRenderer").objectReferenceValue = estrellaRend;
            so.FindProperty("luz").objectReferenceValue = luz;
            so.FindProperty("materialEncendida").objectReferenceValue = matEncendida;
            so.FindProperty("materialApagada").objectReferenceValue = matApagada;
            so.FindProperty("vfxAlEncenderse").objectReferenceValue = chispas;
            so.ApplyModifiedPropertiesWithoutUndo();

            resultado = PrefabUtility.SaveAsPrefabAsset(raiz, RutaPrefab);
            Object.DestroyImmediate(raiz);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(taller);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return resultado;
    }

    // ───────────────────────────── Escenas ─────────────────────────────

    static int PonerEnLaEscena(Scene escena, GameObject prefab, StringBuilder informe, out int nuevos)
    {
        int cambios = 0;
        nuevos = 0;
        string nombreEscena = Path.GetFileNameWithoutExtension(escena.path);
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var punto in raiz.GetComponentsInChildren<SavePoint>(true))
            {
                if (punto == null) continue;
                var r = Sustituir(punto, prefab, escena);
                if (r.cambio) cambios++;
                if (r.nuevo) nuevos++;
                if (!string.IsNullOrEmpty(r.texto)) informe.AppendLine($"· {nombreEscena} / {r.texto}");
            }
        }
        return cambios;
    }

    struct Resultado { public bool cambio, nuevo; public string texto; }

    static Resultado Sustituir(SavePoint punto, GameObject prefab, Scene escena)
    {
        var interactable = punto.GetComponentInParent<Interactable>(true);
        var raiz = interactable != null ? interactable.transform : punto.transform;
        string nombre = raiz.name;
        var hint = LeerHint(interactable);

        if (PrefabUtility.IsPartOfPrefabInstance(raiz.gameObject))
            return new Resultado { texto = $"{nombre}: NO tocado, es parte de un prefab (aviso a Claude)." };

        var yaPuesto = raiz.Find(NombreDelFarol);
        if (yaPuesto != null)
        {
            bool movido = SubirIcono(hint, raiz, yaPuesto.position.y);
            return new Resultado { cambio = movido, texto = movido ? $"{nombre}: ya tenía el farol; icono recolocado." : null };
        }

        // Fuera lo viejo: el santuario de INC-1009 y cualquier hijo con partículas (el círculo),
        // menos el SavePoint y el icono de «pulsa para guardar».
        var viejos = new List<GameObject>();
        foreach (Transform hijo in raiz)
        {
            if (hijo == punto.transform || punto.transform.IsChildOf(hijo)) continue;
            if (hint != null && (hint.transform == hijo || hint.transform.IsChildOf(hijo))) continue;
            if (hijo.name.StartsWith(NombreDelSantuarioViejo) || hijo.GetComponentInChildren<ParticleSystem>(true) != null)
                viejos.Add(hijo.gameObject);
        }
        foreach (var v in viejos) Object.DestroyImmediate(v);
        var sistema = raiz.GetComponent<ParticleSystem>();
        var dibujo = raiz.GetComponent<ParticleSystemRenderer>();
        if (sistema != null) Object.DestroyImmediate(sistema);
        if (dibujo != null) Object.DestroyImmediate(dibujo);

        var farol = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        farol.name = NombreDelFarol;
        farol.transform.SetParent(raiz, false);
        var e = raiz.lossyScale;
        farol.transform.localScale = new Vector3(Inverso(e.x), Inverso(e.y), Inverso(e.z));
        farol.transform.localRotation = Quaternion.identity;
        float suelo = AlturaDelSuelo(raiz);
        farol.transform.position = new Vector3(raiz.position.x, suelo, raiz.position.z);

        SubirIcono(hint, raiz, suelo);
        return new Resultado
        {
            cambio = true, nuevo = true,
            texto = $"{nombre}: farol puesto (quitado: {viejos.Count} pieza(s) vieja(s))."
        };
    }

    /// Deja el icono de «pulsa para guardar» por encima del tejado, para que no tape el farol.
    static bool SubirIcono(GameObject hint, Transform raiz, float suelo)
    {
        if (hint == null || !hint.transform.IsChildOf(raiz)) return false;
        float y = suelo + (AlturaDelFarol + HuecoSobreElTejado) * EscalaDelFarol;
        var t = hint.transform;
        if (t.position.y >= y - 0.01f) return false;
        t.position = new Vector3(t.position.x, y, t.position.z);
        return true;
    }

    static GameObject LeerHint(Interactable interactable)
    {
        if (interactable == null) return null;
        var so = new SerializedObject(interactable);
        var p = so.FindProperty("hint");
        return p != null ? p.objectReferenceValue as GameObject : null;
    }

    static float Inverso(float escala) => Mathf.Abs(escala) > 0.0001f ? 1f / escala : 1f;

    /// Altura del suelo bajo el punto (máximo 1 m por encima o por debajo); si no hay, la del propio punto.
    static float AlturaDelSuelo(Transform raiz)
    {
        Physics.SyncTransforms();
        var origen = raiz.position + Vector3.up * 1f;
        var golpes = Physics.RaycastAll(origen, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        float y = raiz.position.y;
        foreach (var g in golpes)
        {
            if (g.collider.transform.IsChildOf(raiz)) continue;
            if (g.distance < mejor && Mathf.Abs(g.point.y - raiz.position.y) <= 1f)
            {
                mejor = g.distance;
                y = g.point.y;
            }
        }
        return y;
    }
}
#endif
