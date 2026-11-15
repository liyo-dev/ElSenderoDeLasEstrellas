#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Cambia el círculo mágico de los puntos de guardado por un pequeño santuario: un pedestal de
/// piedra con un cristal que flota encima (INC-1009).
///
/// 1) Crea (o rehace) el prefab Assets/Prefabs/Interactables/SantuarioDeGuardado.prefab con piezas
///    ya presentes en el proyecto: el pedestal Pillar02 y el cristal Crystal01 de «RPG Tiny Fantasy
///    World 01 PBR», el brillo ambiental «Crystal effect blue» de Hovl y una luz puntual. El cristal
///    lleva un material propio con emisión; el pack no se toca.
/// 2) Recorre las escenas de Assets/Scenes y, en cada SavePoint, quita el círculo (las partículas
///    del efecto «AoE Magic») y pone el santuario. NO toca el SavePoint, el Interactable, el trigger
///    ni el icono de «pulsa para guardar»: solo cambia lo que se ve.
///
/// Idempotente: un punto que ya tiene el santuario se salta; volver a ejecutarlo rehace el prefab y
/// todos los santuarios puestos se actualizan solos.
public static class SantuarioDeGuardadoBuilder
{
    const string RutaPrefab = "Assets/Prefabs/Interactables/SantuarioDeGuardado.prefab";
    const string CarpetaDelMaterial = "Assets/Art/World/Santuario";
    const string RutaMaterial = CarpetaDelMaterial + "/CristalDelSantuario.mat";

    const string Pack = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR";
    const string PrefabPedestal = Pack + "/Prefab/BuildingUtilityDeco/Pillar02.prefab";
    const string PrefabCristal = Pack + "/Prefab/BuildingUtilityDeco/Crystal01.prefab";
    const string MaterialDelPack = Pack + "/Material/DefaultPBR.mat";
    const string PrefabBrillo = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Environment/Crystal effect blue.prefab";

    const string NombreDelSantuario = "SantuarioDeGuardado";
    const string CarpetaDeEscenas = "Assets/Scenes";

    // Medidas en metros del mundo (el santuario compensa la escala del punto de guardado).
    const float AlturaDelPedestal = 1.0f;
    const float AlturaDelCristal = 0.5f;
    const float HuecoBajoElCristal = 0.2f;
    const float EscalaDelBrillo = 0.5f;

    [MenuItem("El Sendero/Escenario/Santuario de guardado: crear y poner en todos los puntos (INC-1009)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("Santuario de guardado", "Sal del modo Play antes de ejecutarlo.");
            return;
        }
        // Se abren y se guardan escenas: que nada de lo abierto se quede sin guardar.
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
                EditorUtility.DisplayProgressBar("Santuario de guardado",
                    $"Buscando puntos de guardado en {Path.GetFileNameWithoutExtension(ruta)}…", (float)i / rutas.Count);

                var escena = EditorSceneManager.GetSceneByPath(ruta);
                bool estabaAbierta = escena.isLoaded;
                if (!estabaAbierta) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
                try
                {
                    int enEsta = PonerEnLaEscena(escena, prefab, informe);
                    if (enEsta > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(escena);
                        EditorSceneManager.SaveScene(escena);
                        puestos += enEsta;
                        informe.AppendLine($"· {Path.GetFileNameWithoutExtension(ruta)}: {enEsta} santuario(s) puesto(s).");
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
            ? $"Santuarios puestos: {puestos}."
            : "No he cambiado ningún punto de guardado (ya tenían el santuario o no hay ninguno en Assets/Scenes).";
        Debug.Log("[Santuario de guardado] " + resumen + "\n" + informe);
        OrdenesDelEditor.Avisar("Santuario de guardado", resumen + "\n\n" + informe);
    }

    // ───────────────────────────── Prefab ─────────────────────────────

    static GameObject CrearPrefab()
    {
        var pedestalPack = Cargar<GameObject>(PrefabPedestal);
        var cristalPack = Cargar<GameObject>(PrefabCristal);
        var materialDelPack = Cargar<Material>(MaterialDelPack);
        if (pedestalPack == null || cristalPack == null || materialDelPack == null) return null;
        var brilloPack = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBrillo); // opcional
        if (brilloPack == null) Debug.LogWarning("[Santuario de guardado] No encuentro «Crystal effect blue»: el santuario saldrá sin brillo de partículas.");

        var material = CrearMaterialDelCristal(materialDelPack);

        GameObject resultado;
        var taller = EditorSceneManager.NewPreviewScene();
        try
        {
            var raiz = new GameObject(NombreDelSantuario);
            SceneManager.MoveGameObjectToScene(raiz, taller);

            // Pedestal: la piedra del pack, sin LOD (se queda la malla buena), a 1 m de alto y apoyada en el suelo.
            var pedestal = Pieza(pedestalPack, "Pedestal", raiz.transform, taller);
            Escalar(pedestal.transform, AlturaDelPedestal);
            Apoyar(pedestal.transform, Vector3.zero);

            // Cristal: un pivote en el centro de su base, para que gire sobre sí mismo.
            var pivote = new GameObject("Cristal").transform;
            pivote.SetParent(raiz.transform, false);
            pivote.localPosition = new Vector3(0f, AlturaDelPedestal + HuecoBajoElCristal, 0f);
            var modelo = Pieza(cristalPack, "Modelo", pivote, taller);
            Escalar(modelo.transform, AlturaDelCristal);
            Apoyar(modelo.transform, pivote.position);
            foreach (var c in modelo.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c); // flota: no estorba
            Renderer renderizador = null;
            foreach (var r in modelo.GetComponentsInChildren<Renderer>(true))
            {
                var materiales = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
                r.sharedMaterials = materiales;
                if (renderizador == null) renderizador = r;
            }

            float centroDelCristal = pivote.localPosition.y + AlturaDelCristal * 0.5f;

            if (brilloPack != null)
            {
                var brillo = (GameObject)PrefabUtility.InstantiatePrefab(brilloPack, taller);
                brillo.name = "Brillo";
                brillo.transform.SetParent(raiz.transform, false);
                brillo.transform.localPosition = new Vector3(0f, centroDelCristal, 0f);
                brillo.transform.localScale = Vector3.one * EscalaDelBrillo;
            }

            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(raiz.transform, false);
            luzGo.transform.localPosition = new Vector3(0f, centroDelCristal, 0f);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = new Color(0.62f, 0.8f, 1f);
            luz.range = 5f;
            luz.intensity = 1.1f;
            luz.shadows = LightShadows.None;

            var santuario = raiz.AddComponent<SantuarioDeGuardado>();
            var so = new SerializedObject(santuario);
            so.FindProperty("cristal").objectReferenceValue = pivote;
            so.FindProperty("cristalRenderer").objectReferenceValue = renderizador;
            so.FindProperty("luz").objectReferenceValue = luz;
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

    /// Material propio del cristal: el del pack (mismo shader Quibli) teñido de azul claro y con emisión.
    /// El material del pack no se toca.
    static Material CrearMaterialDelCristal(Material origen)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaDelMaterial))
            AssetDatabase.CreateFolder("Assets/Art/World", "Santuario");

        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (m == null)
        {
            m = new Material(origen) { name = "CristalDelSantuario" };
            AssetDatabase.CreateAsset(m, RutaMaterial);
        }
        var tinte = new Color(0.66f, 0.86f, 1f, 1f);
        var brillo = new Color(0.30f, 0.62f, 1f, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tinte);
        if (m.HasProperty("_Emission")) m.SetFloat("_Emission", 1f);
        if (m.HasProperty("_EmissionColorUI")) m.SetColor("_EmissionColorUI", brillo);
        if (m.HasProperty("_EmissionScaleUI")) m.SetFloat("_EmissionScaleUI", 1.4f);
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", brillo * 1.4f);
        m.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Instancia una pieza del pack en el taller y la deja suelta (sin prefab ni LOD) para poder retocarla:
    /// una sola malla (la buena) y los colliders que traiga.
    static GameObject Pieza(GameObject origen, string nombre, Transform padre, Scene taller)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(origen, taller);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var lod = go.GetComponent<LODGroup>();
        if (lod != null) Object.DestroyImmediate(lod);
        var aBorrar = new List<GameObject>();
        foreach (Transform t in go.transform)
            if (t.name == "LOD1") aBorrar.Add(t.gameObject);
        foreach (var b in aBorrar) Object.DestroyImmediate(b);
        return go;
    }

    /// Escala la pieza (por igual en los tres ejes) para que mida «alto» metros de alto.
    static void Escalar(Transform pieza, float alto)
    {
        if (!Medir(pieza, out var b) || b.size.y < 0.0001f) return;
        pieza.localScale *= alto / b.size.y;
    }

    /// Deja la base de la pieza (centro en X/Z, punto más bajo en Y) en el punto dado, en coordenadas del mundo.
    static void Apoyar(Transform pieza, Vector3 punto)
    {
        if (!Medir(pieza, out var b)) return;
        pieza.position += new Vector3(punto.x - b.center.x, punto.y - b.min.y, punto.z - b.center.z);
    }

    /// Caja que envuelve todas las mallas de la pieza, en el mundo y con las transformaciones de ahora.
    static bool Medir(Transform raiz, out Bounds caja)
    {
        caja = default;
        bool primero = true;
        foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            var malla = mf.sharedMesh;
            if (malla == null) continue;
            var mb = malla.bounds;
            for (int i = 0; i < 8; i++)
            {
                var signo = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                var punto = mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, signo));
                if (primero) { caja = new Bounds(punto, Vector3.zero); primero = false; }
                else caja.Encapsulate(punto);
            }
        }
        return !primero;
    }

    // ───────────────────────────── Escenas ─────────────────────────────

    static int PonerEnLaEscena(Scene escena, GameObject prefab, StringBuilder informe)
    {
        int n = 0;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var punto in raiz.GetComponentsInChildren<SavePoint>(true))
            {
                if (punto != null && Sustituir(punto, prefab, escena, informe)) n++;
            }
        }
        return n;
    }

    static bool Sustituir(SavePoint punto, GameObject prefab, Scene escena, StringBuilder informe)
    {
        // La raíz visible del punto es la que lleva el Interactable (el SavePoint suele ser un hijo).
        var interactable = punto.GetComponentInParent<Interactable>(true);
        var raiz = interactable != null ? interactable.transform : punto.transform;
        string nombre = raiz.name;

        if (raiz.Find(NombreDelSantuario) != null)
        {
            informe.AppendLine($"· {nombre}: ya tenía el santuario.");
            return false;
        }
        if (PrefabUtility.IsPartOfPrefabInstance(raiz.gameObject))
        {
            informe.AppendLine($"· {nombre}: NO tocado, es parte de un prefab (aviso a Claude).");
            return false;
        }

        // Fuera lo que da el círculo: los hijos con partículas (menos el SavePoint y el icono de «pulsa para guardar»)
        // y las partículas de la propia raíz.
        var hint = LeerHint(interactable);
        var viejos = new List<GameObject>();
        foreach (Transform hijo in raiz)
        {
            if (hijo == punto.transform || punto.transform.IsChildOf(hijo)) continue;
            if (hint != null && (hint.transform == hijo || hint.transform.IsChildOf(hijo))) continue;
            if (hijo.GetComponentInChildren<ParticleSystem>(true) != null) viejos.Add(hijo.gameObject);
        }
        foreach (var v in viejos) Object.DestroyImmediate(v);

        var sistema = raiz.GetComponent<ParticleSystem>();
        var dibujo = raiz.GetComponent<ParticleSystemRenderer>();
        if (sistema != null) Object.DestroyImmediate(sistema);
        if (dibujo != null) Object.DestroyImmediate(dibujo); // si Unity ya se lo llevó con el sistema, es null y no pasa nada

        // El santuario, apoyado en el suelo si lo hay cerca y con el tamaño de verdad aunque la raíz esté escalada.
        var santuario = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        santuario.transform.SetParent(raiz, false);
        var e = raiz.lossyScale;
        santuario.transform.localScale = new Vector3(Inverso(e.x), Inverso(e.y), Inverso(e.z));
        santuario.transform.localRotation = Quaternion.identity;
        santuario.transform.position = new Vector3(raiz.position.x, AlturaDelSuelo(raiz), raiz.position.z);

        informe.AppendLine($"· {nombre}: círculo quitado ({viejos.Count} hijo(s) con partículas) y santuario puesto.");
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

    static T Cargar<T>(string ruta) where T : Object
    {
        var o = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (o == null)
        {
            Debug.LogError("[Santuario de guardado] No encuentro " + ruta);
            OrdenesDelEditor.Avisar("Santuario de guardado", "No encuentro " + ruta + ". No he cambiado nada.");
        }
        return o;
    }
}
#endif
