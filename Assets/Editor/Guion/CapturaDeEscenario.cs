#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Captura un escenario para poder escribir guiones encima: una foto cenital con la rejilla en
/// metros, dónde está cada marca y cada NPC al aparecer, por dónde se puede andar (NavMesh), qué
/// estorba (colliders) y qué animaciones tiene cada personaje.
///
/// Deja en «Guiones/<escena>/escenario/» (fuera de Assets, no se importa):
///   planta_general.png / planta_detalle.png  la foto desde arriba
///   planta_general.svg / planta_detalle.svg  la foto con rejilla, marcas y rótulos
///   escenario.json                           todos los datos, para escribir el guion
///   animaciones.md                           el catálogo de estados de cada personaje
public static class CapturaDeEscenario
{
    public const int Lado = 2048;

    public static string CarpetaDeGuiones
        => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Guiones");

    [MenuItem("El Sendero/Guion/1. Capturar el escenario del prólogo", priority = 1)]
    public static void MenuPrologo() => Capturar("Prologo_Valle");

    /// Carpeta de un escenario: la escena, o «escena_zona» si es un trozo de una escena grande.
    public static string CarpetaDeEscena(string escena, string zona)
        => string.IsNullOrEmpty(zona) ? escena : escena + "_" + zona;

    public static string Capturar(string nombreEscena) => Capturar(nombreEscena, null, null);

    /// Captura solo un trozo de la escena (para MainWorld, que es enorme): lo que cae dentro de
    /// «zona». Se guarda en Guiones/escena_nombreZona/escenario.
    public static string Capturar(string nombreEscena, Rect? zona, string nombreZona)
    {
        var escena = AbrirEscena(nombreEscena);
        if (!escena.IsValid() || !escena.isLoaded)
        {
            EditorUtility.DisplayDialog("Captura", $"No he podido abrir la escena '{nombreEscena}'.", "Vale");
            return null;
        }

        string carpeta = Path.Combine(CarpetaDeGuiones, CarpetaDeEscena(nombreEscena, zona.HasValue ? nombreZona : null), "escenario");
        Directory.CreateDirectory(carpeta);

        var marcas = RecogerMarcas(escena);
        var spawns = RecogerSpawns(escena);
        var props = RecogerProps(escena);
        if (zona.HasValue)
        {
            var z = zona.Value;
            marcas = marcas.Where(m => z.Contains(new Vector2(m.pos.x, m.pos.z))).ToList();
            spawns = spawns.Where(s => z.Contains(new Vector2(s.pos.x, s.pos.z))).ToList();
            props = props.Where(p => z.Contains(new Vector2(p.pos.x, p.pos.z))).ToList();
        }
        if (!zona.HasValue && marcas.Count == 0 && spawns.Count == 0)
        {
            EditorUtility.DisplayDialog("Captura", "La escena no tiene marcas ni puntos de aparición.", "Vale");
            return null;
        }

        Rect general = zona ?? Envolvente(marcas.Select(m => m.pos).Concat(spawns.Select(s => s.pos)), 8f);
        Rect detalle = zona ?? Envolvente(spawns.Select(s => s.pos), 10f);
        if (detalle.width < 1f) detalle = general;

        try
        {
            EditorUtility.DisplayProgressBar("Captura del escenario", "Foto general…", 0.1f);
            Fotografiar(escena, general, Path.Combine(carpeta, "planta_general.png"), out int gw, out int gh);
            EditorUtility.DisplayProgressBar("Captura del escenario", "Foto de detalle…", 0.3f);
            Fotografiar(escena, detalle, Path.Combine(carpeta, "planta_detalle.png"), out int dw, out int dh);

            EditorUtility.DisplayProgressBar("Captura del escenario", "NavMesh y obstáculos…", 0.5f);
            var nav = NavMesh.CalculateTriangulation();
            var colliders = RecogerColliders(escena, general);

            EditorUtility.DisplayProgressBar("Captura del escenario", "Animaciones…", 0.7f);
            var catalogo = new Dictionary<string, List<EstadoDeAnimacion>>();
            var alturas = new Dictionary<string, float>();
            foreach (var s in spawns)
            {
                if (s.prefab == null || string.IsNullOrEmpty(s.persistenceId) || catalogo.ContainsKey(s.persistenceId)) continue;
                catalogo[s.persistenceId] = Catalogo(s.prefab);
                alturas[s.persistenceId] = Altura(s.prefab);
            }

            EditorUtility.DisplayProgressBar("Captura del escenario", "Escribiendo…", 0.9f);
            EscribirSvg(Path.Combine(carpeta, "planta_general.svg"), "planta_general.png", general, gw, gh, marcas, spawns, props, nav, colliders, 10f);
            EscribirSvg(Path.Combine(carpeta, "planta_detalle.svg"), "planta_detalle.png", detalle, dw, dh, marcas, spawns, props, nav, colliders, 2f);
            EscribirJson(Path.Combine(carpeta, "escenario.json"), nombreEscena, general, detalle, marcas, spawns, props, nav, colliders, catalogo, alturas);
            EscribirCatalogo(Path.Combine(carpeta, "animaciones.md"), catalogo, alturas);
        }
        finally { EditorUtility.ClearProgressBar(); }

        Debug.Log($"[Captura] Escenario '{nombreEscena}' capturado en {carpeta}: {marcas.Count} marcas, " +
                  $"{spawns.Count} personajes, {props.Count} props.");
        return carpeta;
    }

    // ── Datos ───────────────────────────────────────────────────────────────────────────────

    public struct Marca { public string nombre; public Vector3 pos; public float rumbo; }
    public struct Aparicion { public string spawnId, persistenceId, nombre; public Vector3 pos; public float rumbo; public GameObject prefab; }
    public struct EstadoDeAnimacion { public int capa; public string nombreCapa, estado, clip, etiqueta; public float largo; public bool bucle, mezcla; }
    public struct Obstaculo { public string nombre; public Bounds caja; }

    public static Scene AbrirEscena(string nombre)
    {
        var escena = SceneManager.GetSceneByName(nombre);
        if (escena.IsValid() && escena.isLoaded) return escena;
        foreach (var guid in AssetDatabase.FindAssets($"{nombre} t:Scene"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(ruta) != nombre) continue;
            Debug.Log($"[Guion] Abro '{ruta}' en aditivo.");
            return EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        }
        return escena;
    }

    public static SequenceStage BuscarStage(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var s = raiz.GetComponentInChildren<SequenceStage>(true);
            if (s != null) return s;
        }
        return null;
    }

    public static List<Marca> RecogerMarcas(Scene escena)
    {
        var lista = new List<Marca>();
        var vistos = new HashSet<string>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var stage in raiz.GetComponentsInChildren<SequenceStage>(true))
                foreach (var m in stage.Marks)
                {
                    if (m.target == null || string.IsNullOrWhiteSpace(m.name) || !vistos.Add(m.name.Trim())) continue;
                    lista.Add(new Marca { nombre = m.name.Trim(), pos = m.target.position, rumbo = m.target.eulerAngles.y });
                }
        // Cualquier objeto llamado M_* también vale como marca.
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("M_") && vistos.Add(t.name))
                    lista.Add(new Marca { nombre = t.name, pos = t.position, rumbo = t.eulerAngles.y });
        return lista.OrderBy(m => m.nombre).ToList();
    }

    public static List<Aparicion> RecogerSpawns(Scene escena)
    {
        var roster = new Dictionary<string, NpcRosterSO.Entry>();
        foreach (var r in Resources.LoadAll<NpcRosterSO>(NpcSpawner.RostersResourcePath))
            foreach (var e in r.entries)
                if (e != null && e.enabled && !string.IsNullOrEmpty(e.spawnId)) roster[e.spawnId] = e;

        var lista = new List<Aparicion>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var p in raiz.GetComponentsInChildren<NpcSpawnPoint>(true))
            {
                roster.TryGetValue(p.spawnId ?? "", out var e);
                string id = e != null ? e.persistenceId : null;
                if (string.IsNullOrEmpty(id) && e?.prefab != null)
                {
                    var m = e.prefab.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
                    if (m != null) id = m.PersistenceId;
                }
                lista.Add(new Aparicion
                {
                    spawnId = p.spawnId, persistenceId = id, nombre = e?.gameObjectName,
                    pos = p.transform.position, rumbo = p.transform.eulerAngles.y, prefab = e?.prefab
                });
            }
        return lista.OrderBy(a => a.persistenceId ?? a.spawnId).ToList();
    }

    public static List<Marca> RecogerProps(Scene escena)
    {
        var lista = new List<Marca>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var stage in raiz.GetComponentsInChildren<SequenceStage>(true))
                foreach (var p in stage.Props)
                    if (p.target != null && !string.IsNullOrEmpty(p.id))
                        lista.Add(new Marca { nombre = p.id, pos = p.target.position, rumbo = p.target.eulerAngles.y });
        return lista;
    }

    private static List<Obstaculo> RecogerColliders(Scene escena, Rect area)
    {
        var lista = new List<Obstaculo>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var c in raiz.GetComponentsInChildren<Collider>(false))
            {
                if (!c.enabled || c.isTrigger || c is TerrainCollider) continue;
                var b = c.bounds;
                if (b.size.x > 60f || b.size.z > 60f) continue; // suelos enteros
                if (b.max.x < area.xMin || b.min.x > area.xMax || b.max.z < area.yMin || b.min.z > area.yMax) continue;
                lista.Add(new Obstaculo { nombre = c.transform.root.name + "/" + c.name, caja = b });
                if (lista.Count >= 4000) return lista;
            }
        return lista;
    }

    public static Rect Envolvente(IEnumerable<Vector3> puntos, float margen)
    {
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        foreach (var p in puntos)
        {
            x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
            z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
        }
        if (x0 > x1) return new Rect(0, 0, 1, 1);
        return Rect.MinMaxRect(Mathf.Floor(x0 - margen), Mathf.Floor(z0 - margen), Mathf.Ceil(x1 + margen), Mathf.Ceil(z1 + margen));
    }

    // ── Animaciones ─────────────────────────────────────────────────────────────────────────

    public static List<EstadoDeAnimacion> Catalogo(GameObject prefab)
    {
        var lista = new List<EstadoDeAnimacion>();
        var animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
        var rac = animator != null ? animator.runtimeAnimatorController : null;
        if (rac == null) return lista;

        var sustituciones = new Dictionary<AnimationClip, AnimationClip>();
        var controller = rac as AnimatorController;
        if (rac is AnimatorOverrideController aoc)
        {
            controller = aoc.runtimeAnimatorController as AnimatorController;
            var pares = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            aoc.GetOverrides(pares);
            foreach (var p in pares) if (p.Key != null && p.Value != null) sustituciones[p.Key] = p.Value;
        }
        if (controller == null) return lista;

        for (int capa = 0; capa < controller.layers.Length; capa++)
        {
            var l = controller.layers[capa];
            RecorrerMaquina(l.stateMachine, capa, l.name, sustituciones, lista);
        }
        return lista;
    }

    private static void RecorrerMaquina(AnimatorStateMachine sm, int capa, string nombreCapa,
        Dictionary<AnimationClip, AnimationClip> sust, List<EstadoDeAnimacion> lista)
    {
        if (sm == null) return;
        foreach (var cs in sm.states)
        {
            var st = cs.state;
            var e = new EstadoDeAnimacion { capa = capa, nombreCapa = nombreCapa, estado = st.name, etiqueta = st.tag };
            if (st.motion is AnimationClip clip)
            {
                if (sust.TryGetValue(clip, out var otro)) clip = otro;
                e.clip = clip.name;
                e.largo = clip.length / Mathf.Max(0.01f, Mathf.Abs(st.speed));
                e.bucle = clip.isLooping;
            }
            else if (st.motion is BlendTree bt) { e.clip = "(mezcla) " + bt.name; e.mezcla = true; e.bucle = true; }
            lista.Add(e);
        }
        foreach (var sub in sm.stateMachines) RecorrerMaquina(sub.stateMachine, capa, nombreCapa, sust, lista);
    }

    public static float Altura(GameObject prefab) => Medidas(prefab).altura;

    /// Alto del personaje y altura de sus ojos. Los ojos salen del hueso de la cabeza (los
    /// personajes son chibis: la cabeza es enorme y los ojos quedan muy por debajo de la coronilla).
    /// Solo cuentan los renderers activos: los prefabs traen sombreros y pelos de recambio ocultos.
    public static (float altura, float ojos) Medidas(GameObject prefab)
    {
        if (prefab == null) return (1.75f, 1.5f);
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float top = 0f; bool hay = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer) continue;
                if (r.bounds.size.y > 4f || r.bounds.size.y < 0.02f) continue;
                top = hay ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y; hay = true;
            }

            Transform cabeza = null;
            var anim = go.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.avatar != null && anim.avatar.isHuman)
            {
                try { cabeza = anim.GetBoneTransform(HumanBodyBones.Head); } catch { cabeza = null; }
            }
            if (cabeza == null)
                cabeza = go.GetComponentsInChildren<Transform>(true)
                    .Where(t =>
                    {
                        string n = t.name.ToLowerInvariant();
                        return n.Contains("head") && !n.Contains("end") && !n.Contains("top") && !n.Contains("nub") && !n.Contains("hair");
                    })
                    .OrderBy(t => t.name.Length).FirstOrDefault();

            // Los ojos: primero las mallas de ojos activas (Eye04…), si no, el hueso de la cabeza
            // (en estos esqueletos el pivote de la cabeza queda casi a la altura de los ojos).
            float ojosMalla = 0f; int nOjos = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                string n = r.name.ToLowerInvariant();
                if (!n.Contains("eye") || n.Contains("brow") || n.Contains("lash") || n.Contains("lid")) continue;
                if (r.bounds.size.y > 0.6f || r.bounds.center.y < 0.2f) continue;
                ojosMalla += r.bounds.center.y; nOjos++;
            }
            if (cabeza != null && cabeza.position.y > 0.2f)
            {
                float yCabeza = cabeza.position.y;
                float coronilla = hay ? Mathf.Clamp(top, yCabeza + 0.15f, yCabeza + 0.9f) : yCabeza + 0.6f;
                float ojos = nOjos > 0 ? ojosMalla / nOjos : yCabeza + 0.05f * (coronilla - yCabeza);
                if (ojos >= coronilla - 0.05f) ojos = yCabeza + 0.05f * (coronilla - yCabeza);
                return (coronilla, ojos);
            }
            if (nOjos > 0 && hay && ojosMalla / nOjos < top) return (top, ojosMalla / nOjos);
            float alto = hay ? top : 1.75f;
            return (alto, alto * 0.85f);
        }
        finally { EditorSceneManager.ClosePreviewScene(escena); }
    }

    // ── Foto cenital ────────────────────────────────────────────────────────────────────────

    public static void Fotografiar(Scene escena, Rect area, string ruta, out int ancho, out int alto)
    {
        float ppm = Mathf.Min(Lado / area.width, Lado / area.height);
        ancho = Mathf.Max(64, Mathf.RoundToInt(area.width * ppm));
        alto = Mathf.Max(64, Mathf.RoundToInt(area.height * ppm));

        float techo = 150f;
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
                if (r.bounds.size.y < 200f) techo = Mathf.Max(techo, r.bounds.max.y);

        var go = new GameObject("Captura cenital") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(ancho, alto, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var fogPrevio = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = area.height * 0.5f;
            cam.aspect = (float)ancho / alto;
            cam.transform.SetPositionAndRotation(new Vector3(area.center.x, techo + 20f, area.center.y), Quaternion.Euler(90f, 0f, 0f));
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = techo + 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            cam.targetTexture = rt;
            cam.scene = escena;
            var datos = go.AddComponent<UniversalAdditionalCameraData>();
            datos.renderPostProcessing = false;
            datos.renderShadows = false;

            var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, peticion)) RenderPipeline.SubmitRenderRequest(cam, peticion);
            else cam.Render();

            var png = new Texture2D(ancho, alto, TextureFormat.RGB24, false);
            var previo = RenderTexture.active;
            RenderTexture.active = rt;
            png.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            png.Apply();
            RenderTexture.active = previo;
            File.WriteAllBytes(ruta, png.EncodeToPNG());
            Object.DestroyImmediate(png);
        }
        finally
        {
            RenderSettings.fog = fogPrevio;
            Object.DestroyImmediate(go);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }

    // ── Escritura ───────────────────────────────────────────────────────────────────────────

    private static readonly string[] Colores =
        { "#ffd27a", "#7ad7ff", "#ff8fb1", "#9dff7a", "#c79bff", "#ffb36b", "#6bffd8", "#ff6b6b", "#e6e6e6", "#b0ff3a", "#ffa0ff", "#80a0ff", "#ffe14d", "#55e0a0" };

    public static string ColorDe(int i) => Colores[((i % Colores.Length) + Colores.Length) % Colores.Length];

    private static void EscribirSvg(string ruta, string fondo, Rect area, int w, int h, List<Marca> marcas,
        List<Aparicion> spawns, List<Marca> props, NavMeshTriangulation nav, List<Obstaculo> obstaculos, float paso)
    {
        var svg = new LienzoSvg(area, w, h, fondo);
        DibujarNavMesh(svg, nav, area);
        foreach (var o in obstaculos) svg.Caja(o.caja, "#ff9a3c", 0.5f);
        svg.Rejilla(paso);
        float escala = w / area.width;
        foreach (var m in marcas)
        {
            if (!area.Contains(new Vector2(m.pos.x, m.pos.z))) continue;
            svg.Punto(m.pos, 3f, "#ffffff");
            svg.Flecha(m.pos, m.rumbo, 8f, "#ffffff");
            svg.TextoEn(m.pos, m.nombre, Mathf.Clamp(escala * 0.28f, 8f, 13f), "#ffffff", 5, -4);
        }
        foreach (var p in props)
        {
            if (!area.Contains(new Vector2(p.pos.x, p.pos.z))) continue;
            svg.Punto(p.pos, 5f, "#ff9a3c");
            svg.TextoEn(p.pos, p.nombre, Mathf.Clamp(escala * 0.3f, 9f, 13f), "#ff9a3c", 6, 12);
        }
        for (int i = 0; i < spawns.Count; i++)
        {
            var s = spawns[i];
            svg.Punto(s.pos, Mathf.Clamp(escala * 0.3f, 4f, 9f), ColorDe(i));
            svg.Flecha(s.pos, s.rumbo, Mathf.Clamp(escala * 0.8f, 10f, 22f), ColorDe(i));
            svg.TextoEn(s.pos, s.persistenceId ?? s.spawnId, Mathf.Clamp(escala * 0.35f, 10f, 15f), ColorDe(i), 8, 14);
        }
        svg.Texto(new Vector2(10, h - 12), $"Rejilla cada {paso} m · X hacia la derecha, Z hacia arriba", 14, "#ffffff");
        File.WriteAllText(ruta, svg.Cerrar());
    }

    public static void DibujarNavMesh(LienzoSvg svg, NavMeshTriangulation nav, Rect area)
    {
        if (nav.indices == null) return;
        for (int i = 0; i + 2 < nav.indices.Length; i += 3)
        {
            var a = nav.vertices[nav.indices[i]]; var b = nav.vertices[nav.indices[i + 1]]; var c = nav.vertices[nav.indices[i + 2]];
            var centro = (a + b + c) / 3f;
            if (!area.Contains(new Vector2(centro.x, centro.z))) continue;
            svg.Triangulo(a, b, c, "#3cff6e", 0.16f);
        }
    }

    private static void EscribirJson(string ruta, string escena, Rect general, Rect detalle, List<Marca> marcas,
        List<Aparicion> spawns, List<Marca> props, NavMeshTriangulation nav, List<Obstaculo> obstaculos,
        Dictionary<string, List<EstadoDeAnimacion>> catalogo, Dictionary<string, float> alturas)
    {
        var j = new GuionJson();
        j.Obj();
        j.Val("escena", escena);
        j.Val("nota", "Coordenadas del mundo en metros. En las plantas, X crece a la derecha y Z hacia arriba.");
        EscribirArea(j, "area_general", general);
        EscribirArea(j, "area_detalle", detalle);
        j.Arr("marcas");
        foreach (var m in marcas) j.Obj().Val("nombre", m.nombre).Vec("pos", m.pos).Val("rumbo", m.rumbo).FinObj();
        j.FinArr();
        j.Arr("props");
        foreach (var m in props) j.Obj().Val("id", m.nombre).Vec("pos", m.pos).Val("rumbo", m.rumbo).FinObj();
        j.FinArr();
        j.Arr("personajes");
        foreach (var s in spawns)
        {
            j.Obj().Val("id", s.persistenceId).Val("spawn", s.spawnId).Val("objeto", s.nombre)
                .Val("prefab", s.prefab != null ? s.prefab.name : null).Vec("pos", s.pos).Val("rumbo", s.rumbo);
            if (s.persistenceId != null && alturas.TryGetValue(s.persistenceId, out float alto)) j.Val("altura", alto);
            if (s.prefab != null) j.Val("ojos", Medidas(s.prefab).ojos);
            j.FinObj();
        }
        j.FinArr();
        j.Arr("obstaculos");
        foreach (var o in obstaculos)
            j.Obj().Val("nombre", o.nombre).Vec("min", o.caja.min).Vec("max", o.caja.max).FinObj();
        j.FinArr();

        // NavMesh solo dentro del área general.
        var indices = new List<int>();
        var mapa = new Dictionary<int, int>();
        var vertices = new List<Vector3>();
        if (nav.indices != null)
            for (int i = 0; i + 2 < nav.indices.Length; i += 3)
            {
                var c = (nav.vertices[nav.indices[i]] + nav.vertices[nav.indices[i + 1]] + nav.vertices[nav.indices[i + 2]]) / 3f;
                if (!general.Contains(new Vector2(c.x, c.z))) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = nav.indices[i + k];
                    if (!mapa.TryGetValue(v, out int nuevo)) { nuevo = vertices.Count; vertices.Add(nav.vertices[v]); mapa[v] = nuevo; }
                    indices.Add(nuevo);
                }
            }
        j.Obj("navmesh");
        j.Arr("vertices"); foreach (var v in vertices) j.Num(v.x).Num(v.y).Num(v.z); j.FinArr();
        j.Arr("triangulos"); foreach (int i in indices) j.Ent(i); j.FinArr();
        j.FinObj();

        j.Obj("animaciones");
        foreach (var kv in catalogo)
        {
            j.Arr(kv.Key);
            foreach (var e in kv.Value)
                j.Obj().Val("estado", e.estado).Val("capa", e.nombreCapa).Val("clip", e.clip).Val("largo", e.largo)
                    .Val("bucle", e.bucle).Val("etiqueta", e.etiqueta).FinObj();
            j.FinArr();
        }
        j.FinObj();
        j.FinObj();
        File.WriteAllText(ruta, j.ToString());
    }

    private static void EscribirArea(GuionJson j, string clave, Rect r)
        => j.Obj(clave).Val("xmin", r.xMin).Val("zmin", r.yMin).Val("xmax", r.xMax).Val("zmax", r.yMax).FinObj();

    private static void EscribirCatalogo(string ruta, Dictionary<string, List<EstadoDeAnimacion>> catalogo, Dictionary<string, float> alturas)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Catálogo de animaciones por personaje\n");
        sb.AppendLine("Estados del Animator de cada personaje del escenario. «bucle» = se repite solo; los demás son gestos de una vez.\n");
        foreach (var kv in catalogo)
        {
            alturas.TryGetValue(kv.Key, out float alto);
            sb.AppendLine($"## {kv.Key} (altura {alto:0.00} m)\n");
            sb.AppendLine("| Capa | Estado | Clip | Dura | Bucle |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var e in kv.Value.OrderBy(e => e.capa).ThenBy(e => e.estado))
                sb.AppendLine($"| {e.nombreCapa} | {e.estado} | {e.clip} | {e.largo:0.00} | {(e.bucle ? "sí" : "")} |");
            sb.AppendLine();
        }
        File.WriteAllText(ruta, sb.ToString());
    }
}
#endif
