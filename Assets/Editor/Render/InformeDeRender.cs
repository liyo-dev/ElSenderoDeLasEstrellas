using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Informe para revisar parpadeos y rendimiento de la escena abierta: materiales más usados con su shader,
/// su textura principal y cómo se importa (mipmaps, filtro, anisotropía, tamaño), más el recuento de
/// renderers, sombras y LODs, y los detalles del terreno (flores y hierba) con su shader. Escribe Guiones/_ordenes/render.md.
public static class InformeDeRender
{
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: materiales, texturas y render de la escena")]
    static void Informe()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
        var sb = new StringBuilder($"# Render de la escena ({renderers.Length} renderers activos)\n\n");
        sb.Append($"- Proyectan sombra: {renderers.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)}\n");
        sb.Append($"- Con LODGroup: {Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude).Length}\n");
        sb.Append($"- Luces: {Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).Length}\n\n");
        sb.Append("| Material | Shader | Renderers | Ejemplo | Textura | Mip | Filtro | Aniso | Tamaño |\n|---|---|---|---|---|---|---|---|---|\n");
        var porMaterial = renderers.SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => (m, r)))
            .GroupBy(x => x.m).OrderByDescending(g => g.Count()).Take(60);
        foreach (var g in porMaterial)
        {
            var mat = g.Key;
            Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture;
            string ruta = tex != null ? AssetDatabase.GetAssetPath(tex) : "";
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            sb.Append($"| {mat.name} | {mat.shader.name} | {g.Count()} | {g.First().r.name} | {Path.GetFileName(ruta)} | {(imp != null ? imp.mipmapEnabled.ToString() : "-")} | {(imp != null ? imp.filterMode.ToString() : "-")} | {(imp != null ? imp.anisoLevel.ToString() : "-")} | {(tex != null ? $"{tex.width}x{tex.height}" : "-")} |\n");
        }
        InformeDelDetalleDelTerreno(sb);
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "render.md"), sb.ToString());
        Debug.Log("[Render] informe escrito");
    }

    /// Flores y hierba que pinta el terreno (detalles): cómo se dibuja cada una y con qué shader.
    static void InformeDelDetalleDelTerreno(StringBuilder sb)
    {
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            var datos = t.terrainData;
            if (datos == null) continue;
            sb.Append($"\n## Detalle del terreno «{t.name}» (distancia {t.detailObjectDistance} m, densidad {t.detailObjectDensity})\n\n");
            sb.Append($"- Material de los detalles de hierba del terreno: {(t.materialTemplate != null ? t.materialTemplate.shader.name : "-")}\n\n");
            sb.Append("| # | Prototipo | Modo | Instanciado | Materiales (shader) | Sombra del prototipo |\n|---|---|---|---|---|---|\n");
            var protos = datos.detailPrototypes;
            for (int i = 0; i < protos.Length; i++)
            {
                var p = protos[i];
                string nombre = p.usePrototypeMesh ? (p.prototype != null ? AssetDatabase.GetAssetPath(p.prototype) : "(sin malla)") : (p.prototypeTexture != null ? p.prototypeTexture.name : "(sin textura)");
                string mats = "-", sombra = "-";
                if (p.usePrototypeMesh && p.prototype != null)
                {
                    var rs = p.prototype.GetComponentsInChildren<Renderer>(true);
                    mats = string.Join(", ", rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct()
                        .Select(m => $"{m.name} ({m.shader.name}, recorte {m.IsKeywordEnabled("_ALPHATEST_ON")}, recibe {!m.IsKeywordEnabled("_RECEIVE_SHADOWS_OFF")})"));
                    sombra = string.Join(", ", rs.Select(r => r.shadowCastingMode.ToString()).Distinct());
                }
                sb.Append($"| {i} | {nombre} | {p.renderMode} | {p.useInstancing} | {mats} | {sombra} |\n");
            }
        }
    }

    /// Cámaras (también las apagadas), con lo que decide cómo se ven: renderer, postproceso, antialias,
    /// capas que ven y volúmenes que escuchan; los volúmenes globales con su peso y el contorno que
    /// resulta, y dónde cuelga la lluvia. Para comparar la cámara de juego con las de diálogo y
    /// cinemática en Play. Escribe Guiones/_ordenes/camaras.md.
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: cámaras, volúmenes y lluvia")]
    static void InformeDeCamaras()
    {
        var sb = new StringBuilder("# Cámaras\n\n| Cámara | Activa | Prof. | Renderer | Post | AA | MSAA | Capas | Volúmenes | Disparador | Cerca/Lejos | Posición |\n|---|---|---|---|---|---|---|---|---|---|---|---|\n");
        var campoRenderer = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            var d = cam.GetUniversalAdditionalCameraData();
            sb.Append($"| {Ruta(cam.transform)} | {cam.isActiveAndEnabled} | {cam.depth} | {(campoRenderer != null ? campoRenderer.GetValue(d) : "?")} | {d.renderPostProcessing} | {d.antialiasing} | {cam.allowMSAA} | {Capas(cam.cullingMask)} | {Capas(d.volumeLayerMask)} | {(d.volumeTrigger != null ? d.volumeTrigger.name : "-")} | {cam.nearClipPlane}/{cam.farClipPlane} | {cam.transform.position} |\n");
        }
        sb.Append("\n# Volúmenes\n\n| Volumen | Escena | Capa | Global | Activo | Peso | Prioridad | Perfil | Efectos |\n|---|---|---|---|---|---|---|---|---|\n");
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            var perfil = v.sharedProfile;
            string efectos = perfil != null ? string.Join(", ", perfil.components.Where(c => c.active).Select(c => c.GetType().Name)) : "-";
            sb.Append($"| {Ruta(v.transform)} | {v.gameObject.scene.name} | {LayerMask.LayerToName(v.gameObject.layer)} | {v.isGlobal} | {v.isActiveAndEnabled} | {v.weight:F2} | {v.priority} | {(perfil != null ? perfil.name : "-")} | {efectos} |\n");
        }
        sb.Append($"\nPostprocesoDeEscena que manda: {(PostprocesoDeEscena.Activo != null ? Ruta(PostprocesoDeEscena.Activo.transform) : "ninguno")}\n\n# Clima\n\n");
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name == "[Lluvia]" || t.name == "[Nieve]" || t.name == "[Viento]"))
            sb.Append($"- {Ruta(t)} activo={t.gameObject.activeInHierarchy} capa={LayerMask.LayerToName(t.gameObject.layer)} pos={t.position}\n");
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "camaras.md"), sb.ToString());
        Debug.Log("[Render] informe de cámaras escrito");
    }

    /// Materiales de los renderers de las escenas abiertas cuya textura principal tiene partes
    /// transparentes y no recortan (se ve el fondo de la textura como una placa), con la textura, cómo
    /// se importa su alfa y si el material es un archivo propio o va dentro del modelo. Incluye los
    /// prototipos de detalle del terreno. Escribe Guiones/_ordenes/recortes.md.
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: texturas con transparencia sin recorte")]
    static void InformeDeRecortes()
    {
        var mats = new Dictionary<Material, (int n, string ejemplo)>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null) mats[m] = mats.TryGetValue(m, out var x) ? (x.n + 1, x.ejemplo) : (1, Ruta(r.transform));
        }
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            if (t.terrainData != null)
                foreach (var p in t.terrainData.detailPrototypes)
                    if (p.usePrototypeMesh && p.prototype != null)
                        foreach (var r in p.prototype.GetComponentsInChildren<Renderer>(true))
                            foreach (var m in r.sharedMaterials)
                                if (m != null && !mats.ContainsKey(m)) mats[m] = (0, "detalle del terreno " + p.prototype.name);
        var sb = new StringBuilder($"# Texturas con transparencia sin recorte ({mats.Count} materiales en las escenas abiertas)\n\n| Material | Archivo | Shader | Usos | Ejemplo | Textura | Alfa importado | Transparente | Recorte |\n|---|---|---|---|---|---|---|---|---|\n");
        int faltan = 0;
        foreach (var par in mats.OrderByDescending(p => p.Value.n))
        {
            var m = par.Key;
            if (m.shader == null) continue;
            Texture2D tex = (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture) as Texture2D;
            if (tex == null) continue;
            bool recorta = m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) || m.renderQueue >= (int)RenderQueue.Transparent;
            if (recorta) continue;
            string rutaTex = AssetDatabase.GetAssetPath(tex);
            var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
            bool conAlfa = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(tex.graphicsFormat);
            bool fuenteConAlfa = imp != null && imp.DoesSourceTextureHaveAlpha();
            if (!conAlfa && !fuenteConAlfa) continue;
            float hueco = conAlfa ? PasoAlAspectoDeDibujo.FraccionTransparente(tex) : -1f;
            if (conAlfa && hueco < 0.04f) continue;
            faltan++;
            sb.Append($"| {m.name} | {AssetDatabase.GetAssetPath(m)} | {m.shader.name} | {par.Value.n} | {par.Value.ejemplo} | {rutaTex} | {(imp != null ? imp.alphaSource + (conAlfa ? "" : " (la importación tira el alfa de la fuente)") : "-")} | {(hueco >= 0f ? hueco.ToString("P0") : "?")} | no |\n");
        }
        sb.Insert(0, $"<!-- {faltan} sin recorte -->\n");
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "recortes.md"), sb.ToString());
        OrdenesDelEditor.Avisar("Recortes", $"{faltan} materiales con textura con transparencia sin recorte. Detalle en Guiones/_ordenes/recortes.md");
    }


    /// Todos los materiales del proyecto (fuera de VFX, plugins y TextMesh Pro) que pueden verse como una
    /// placa: textura con transparencia en la fuente (aunque la importación la tire) sin recortar, o
    /// transparentes de verdad, o con un shader que no es el del dibujo (packs que se quedaron sin pasar,
    /// shaders perdidos). Escribe Guiones/_ordenes/recortes_proyecto.md.
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: transparencias de todos los materiales del proyecto")]
    static void InformeDelProyecto()
    {
        string[] fuera = { "Assets/Plugins/", "Assets/VFX/", "Assets/_VFX/", "Assets/Art/VFX/", "Assets/TextMesh Pro/" };
        var sb = new StringBuilder("# Transparencias en los materiales del proyecto\n\n| Material | Shader | Textura | Alfa en la fuente | Alfa importado | Hueco | Recorte | Superficie | Cola |\n|---|---|---|---|---|---|---|---|---|\n");
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!ruta.EndsWith(".mat") || fuera.Any(f => ruta.StartsWith(f))) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (m == null) continue;
            string shader = m.shader != null ? m.shader.name : "(sin shader)";
            bool raro = m.shader == null || shader.StartsWith("Hidden/InternalErrorShader") || shader.Contains("Quibli");
            Texture2D tex = (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null) as Texture2D;
            string rutaTex = tex != null ? AssetDatabase.GetAssetPath(tex) : "";
            var imp = AssetImporter.GetAtPath(rutaTex) as TextureImporter;
            bool fuente = imp != null && imp.DoesSourceTextureHaveAlpha();
            bool importado = tex != null && UnityEngine.Experimental.Rendering.GraphicsFormatUtility.HasAlphaChannel(tex.graphicsFormat);
            bool recorta = m.IsKeywordEnabled("_ALPHATEST_ON");
            bool transparente = (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) || m.renderQueue >= 3000;
            float hueco = importado ? PasoAlAspectoDeDibujo.FraccionTransparente(tex) : -1f;
            bool placa = !recorta && !transparente && (fuente || importado) && (hueco < 0f || hueco >= 0.04f);
            if (!placa && !transparente && !raro) continue;
            n++;
            sb.Append($"| {ruta} | {shader} | {Path.GetFileName(rutaTex)} | {fuente} | {(imp != null ? imp.alphaSource.ToString() : "-")}{(importado ? "" : " (sin alfa)")} | {(hueco >= 0f ? hueco.ToString("P0") : "?")} | {recorta} | {(transparente ? "transparente" : "opaca")} | {m.renderQueue} |\n");
        }
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "recortes_proyecto.md"), sb.ToString());
        OrdenesDelEditor.Avisar("Recortes", $"{n} materiales que revisar. Detalle en Guiones/_ordenes/recortes_proyecto.md");
    }


    /// Materiales de las escenas abiertas que el contorno de tinta no «ve»: no tienen pase DepthNormals,
    /// van en la cola transparente o no escriben profundidad. Sobre ellos el contorno dibuja las líneas
    /// de lo que hay detrás. También los que recortan en el color pero no en la profundidad (sin
    /// _ALPHATEST_ON en el material pero con clip propio). Escribe Guiones/_ordenes/sin_profundidad.md.
    [MenuItem("El Sendero/Aspecto de dibujo/Informe: lo que no escribe profundidad (el contorno ve a través)")]
    static void InformeSinProfundidad()
    {
        var porMaterial = new Dictionary<Material, (int n, string ejemplo, string tipo)>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    foreach (var m in r.sharedMaterials)
                        if (m != null) porMaterial[m] = porMaterial.TryGetValue(m, out var x) ? (x.n + 1, x.ejemplo, x.tipo) : (1, Ruta(r.transform), r.GetType().Name);
                }
        }
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
            if (t.terrainData != null)
                foreach (var arbol in t.terrainData.treePrototypes)
                    if (arbol.prefab != null)
                        foreach (var r in arbol.prefab.GetComponentsInChildren<Renderer>(true))
                            foreach (var m in r.sharedMaterials)
                                if (m != null && !porMaterial.ContainsKey(m)) porMaterial[m] = (0, $"árbol del terreno {t.name}: {arbol.prefab.name}", "Terrain tree");
        var sb = new StringBuilder("# Lo que el contorno no ve (escenas abiertas)\n\n| Material | Shader | Usos | Ejemplo | Problema |\n|---|---|---|---|---|\n");
        int n = 0;
        foreach (var par in porMaterial.OrderByDescending(p => p.Value.n))
        {
            var m = par.Key;
            if (m.shader == null) continue;
            var problemas = new List<string>();
            if (m.FindPass("DepthNormals") < 0 && m.FindPass("DepthNormalsOnly") < 0) problemas.Add("sin pase DepthNormals");
            if (m.renderQueue > 2500) problemas.Add($"cola {m.renderQueue}");
            if (m.HasProperty("_ZWrite") && m.GetFloat("_ZWrite") < 0.5f) problemas.Add("no escribe profundidad");
            if (!m.shader.isSupported) problemas.Add("shader no soportado");
            if (problemas.Count == 0) continue;
            n++;
            sb.Append($"| {m.name} | {m.shader.name} | {par.Value.n} | {par.Value.ejemplo} | {string.Join(", ", problemas)} |\n");
        }
        File.WriteAllText(Path.Combine("Guiones", "_ordenes", "sin_profundidad.md"), sb.ToString());
        OrdenesDelEditor.Avisar("Profundidad", $"{n} materiales que el contorno no ve. Detalle en Guiones/_ordenes/sin_profundidad.md");
    }

    static string Ruta(Transform t) => t.parent == null ? $"{t.gameObject.scene.name}/{t.name}" : Ruta(t.parent) + "/" + t.name;

    static string Capas(int mascara)
    {
        if (mascara == -1) return "todas";
        var nombres = new List<string>();
        for (int i = 0; i < 32; i++) if ((mascara & (1 << i)) != 0) nombres.Add(LayerMask.LayerToName(i) is { Length: > 0 } n ? n : i.ToString());
        return string.Join(" ", nombres);
    }
}
