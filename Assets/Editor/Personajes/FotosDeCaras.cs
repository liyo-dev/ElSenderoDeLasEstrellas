#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// Fotos de prueba de las caras (7 nov 2026, prueba del estilo B2 en Will): pone un protagonista, sin sus
/// scripts, en la escena abierta, le pone cada emoción de su perfil y saca fotos con una cámara de URP
/// (Quibli, luces y postproceso de la escena): de cerca, plano medio y a distancia de juego.
/// En MainWorld va delante de la fuente de la plaza (el sitio del escaparate); en otra escena, al origen.
/// Las fotos salen en «Guiones/_fotos_caras/<personaje>/». No deja nada en la escena (todo DontSave y se
/// borra al acabar). Desde el buzón: «menu El Sendero/Personajes/Fotos de caras de Will (prueba)».
public static class FotosDeCaras
{
    private static readonly NPCEmotion[] Emociones =
    {
        NPCEmotion.Neutral, NPCEmotion.Happy, NPCEmotion.Angry, NPCEmotion.Sad, NPCEmotion.Surprised,
        NPCEmotion.Scared, NPCEmotion.Thinking, NPCEmotion.Crying,
    };
    private const int W = 1280, H = 720;

    [MenuItem("El Sendero/Personajes/Fotos de caras de Will (prueba)")]
    public static void Will() => Hacer(WILL, "Will", null);

    /// Lo mismo, con los otros dos a los lados (con su cara de reposo), para comparar a distancia de juego.
    [MenuItem("El Sendero/Personajes/Fotos de caras de Will con Liam y Estela (prueba)")]
    public static void WillConCompis() => Hacer(WILL, "Will", new[] { LIAM, ESTELA });

    [MenuItem("El Sendero/Personajes/Fotos de caras de Estela con Will y Liam (prueba)")]
    public static void EstelaConCompis() => Hacer(ESTELA, "Estela", new[] { WILL, LIAM });

    [MenuItem("El Sendero/Personajes/Fotos de caras de Liam con Will y Estela (prueba)")]
    public static void LiamConCompis() => Hacer(LIAM, "Liam", new[] { WILL, ESTELA });

    /// El grupo abrazándose de frío (estado UpperBody.ClimaFrio) con la cara de helado, de frente y de tres
    /// cuartos, en «Guiones/_fotos_caras/Frio/». Sin la tiritona ni los hombros encogidos, que se ponen en juego.
    [MenuItem("El Sendero/Personajes/Fotos del grupo con frío (prueba)")]
    public static void GrupoConFrio()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AbrazarseDeFrio.RutaDelClip);
        if (clip == null) { OrdenesDelEditor.Avisar("Fotos de caras", "Falta " + AbrazarseDeFrio.RutaDelClip + ": pasa antes el menú de abrazarse de frío."); return; }
        Hacer(WILL, "Frio", new[] { LIAM, ESTELA }, cuerpo: (ClimaSobreElJugador.EstadoDeFrio, clip), soloEmocion: NPCEmotion.Frozen);
    }

    /// Hoja de caras: la cara neutra de cerca de los NPCs con caras nuevas y de los niños, uno a uno, en
    /// «Guiones/_fotos_caras/NPCs/». Si un NPC no se ve en el Editor (o no tiene perfil), lo dice en el informe.
    [MenuItem("El Sendero/Personajes/Hoja de caras de los NPCs y los niños (prueba)")]
    public static void HojaNPCs()
    {
        foreach (var n in HojaDeNPCs)
        {
            string ruta = n.StartsWith("Assets/") ? n : $"Assets/_NPCs/{n}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
            {
                string corto = Path.GetFileName(n);
                ruta = AssetDatabase.FindAssets($"t:Prefab {corto}", new[] { "Assets/_NPCs" }).Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == corto) ?? ruta;
            }
            Hacer(ruta, Path.GetFileNameWithoutExtension(ruta).Replace("#", "").Replace(" ", ""), null, true);
        }
    }

    private static readonly string[] HojaDeNPCs =
    {
        "_GrafoNarrativo/Eldran", "King", "Victoria", "_GrafoNarrativo/Oliver", "Combat/Vicky", "Combat/Erika", "Combat/Lety",
        "_GrafoNarrativo/Boticaria", "_GrafoNarrativo/Selene", "Tendera", "Nora", "WoodsGuard", "Roberto", "Pueblo/Jorge",
        "NonInteractable/TownNpc#4", "NiñoPez", "AmigaDelNiñoPez", "_GrafoNarrativo/Tobi", "NonInteractable/Nina_Prologo",
        "Assets/Prefabs/_WILL_ORIGINAL.prefab",
        "AldeaDelMar/Posadero", "AldeaDelMar/Remedios", "AldeaDelMar/Anselmo", "AldeaDelMar/Engracia", "AldeaDelMar/Eusebio", "AldeaDelMar/Fermin",
    };

    private const string WILL = "Assets/Prefabs/_WILL.prefab", LIAM = "Assets/Prefabs/_LIAM.prefab", ESTELA = "Assets/Prefabs/_ESTELA.prefab";

    /// Pone un personaje (sin scripts, en reposo) dentro de «padre», a «x» metros a su derecha.
    private static (GameObject inst, Animator anim, EmotionProfile perfil, List<Renderer> piezas) Poner(GameObject prefab, Transform padre, float x, Scene escena, StringBuilder informe,
        (string estado, AnimationClip clip)? cuerpo = null)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        inst.hideFlags = HideFlags.DontSave;
        inst.transform.SetParent(padre, false);
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        var anim = inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a != null && a.runtimeAnimatorController != null);
        AnimationClip idle = anim == null ? null
            : anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name.Contains("Idle_Normal"))
              ?? anim.runtimeAnimatorController.animationClips.FirstOrDefault(c => c != null && c.name.Contains("Idle"));
        if (idle != null)
        {
            try { Horno.MuestrearEstado(inst, idle, idle.name, 0.3f); }
            catch (System.Exception ex) { informe.Append($"{prefab.name} sin pose ({ex.Message}) · "); }
        }
        if (cuerpo != null)
        {
            try { Horno.MuestrearEstado(inst, cuerpo.Value.clip, cuerpo.Value.estado, 0.5f); }
            catch (System.Exception ex) { informe.Append($"{prefab.name} sin {cuerpo.Value.estado} ({ex.Message}) · "); }
        }
        inst.transform.localPosition = new Vector3(x, 0f, 0f); inst.transform.localRotation = Quaternion.identity;
        var emo = inst.GetComponentInChildren<NPCEmotionController>(true);
        var piezas = inst.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name.StartsWith("Eye") || r.name.StartsWith("Mouth") || r.name.StartsWith("Fx")).ToList();
        return (inst, anim, emo != null ? emo.EmotionProfile : null, piezas);
    }

    private static Renderer Cara(List<Renderer> piezas, string ojos, string boca, string fx)
    {
        Renderer ojo = null;
        foreach (var r in piezas)
        {
            bool on = r.name == ojos || r.name == boca || r.name == fx;
            r.gameObject.SetActive(on);
            if (on) { r.enabled = true; if (r.name == ojos) ojo = r; }
        }
        return ojo;
    }

    private static float? Suelo(Vector2 xz, float cerca)
    {
        foreach (var hit in Physics.RaycastAll(new Vector3(xz.x, cerca + 6f, xz.y), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore)
                     .OrderBy(h => h.distance))
            return hit.point.y;
        return null;
    }

    /// ¿Le da el sol a la cara en ese punto? (rayo hacia la luz direccional; solo ve lo que tiene collider)
    private static bool AlSol(Vector3 pie, Light sol) =>
        sol == null || !Physics.Raycast(pie + Vector3.up * 1.2f, -sol.transform.forward, 300f, ~0, QueryTriggerInteraction.Ignore);

    private static bool Libre(Vector3 pie) =>
        !Physics.CheckCapsule(pie + Vector3.up * 0.4f, pie + Vector3.up * 1.4f, 0.35f, ~0, QueryTriggerInteraction.Ignore);

    /// <param name="cuerpo">Estado del Animator (y su clip) que se pone a todos encima del reposo.</param>
    /// <param name="soloEmocion">Solo esa emoción, también en los compañeros, y en todos los planos.</param>
    public static void Hacer(string rutaPrefab, string nombre, string[] rutasCompis, bool hoja = false,
        (string estado, AnimationClip clip)? cuerpo = null, NPCEmotion? soloEmocion = null)
    {
        bool conCompis = rutasCompis != null && rutasCompis.Length > 0;
        var Compis = conCompis ? rutasCompis.Select((r, i) => (ruta: r, lado: i == 0 ? -1.4f : 1.4f)).ToArray() : new (string ruta, float lado)[0];
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Fotos de caras", "Hazlo fuera de Play."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (prefab == null) { OrdenesDelEditor.Avisar("Fotos de caras", "No encuentro " + rutaPrefab); return; }
        var escena = SceneManager.GetSceneByName("MainWorld");          // si está cargada (aunque no sea la activa)
        if (!escena.IsValid() || !escena.isLoaded) escena = SceneManager.GetActiveScene();
        bool mundo = escena.name == "MainWorld";
        Vector2 xz = mundo ? new Vector2(4.6f, -134.6f) : Vector2.zero;
        Vector2 haciaCam = mundo ? new Vector2(3.4f, -145f) - xz : new Vector2(0f, -1f);
        float y0 = mundo ? 26.3f : 0f, y = Suelo(xz, y0) ?? y0;
        string sitio = "base";
        // Sitio a mano: «Guiones/_fotos_caras/sitio.txt» con «x z». Si no, el primer sitio cerca de la base con sol
        // en la cara (y en la de los compañeros) y sin nada encima.
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        string fichero = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_fotos_caras", "sitio.txt");
        var sol = RenderSettings.sun;
        if (sol == null)
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var l in raiz.GetComponentsInChildren<Light>())
                    if (sol == null && l.type == LightType.Directional && l.isActiveAndEnabled) sol = l;
        Vector3 derecha = Vector3.Cross(Vector3.up, new Vector3(haciaCam.x, 0f, haciaCam.y).normalized);
        if (File.Exists(fichero))
        {
            var p = File.ReadAllText(fichero).Split(new[] { ' ', '\n', '\r', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            xz = new Vector2(float.Parse(p[0], ci), float.Parse(p[1], ci)); y = Suelo(xz, y0) ?? y0; sitio = "sitio.txt";
            // tercer dato opcional: «sol» (de cara al sol, para que le dé en la cara) o el rumbo en grados (0 = +Z)
            if (p.Length > 2 && p[2] == "sol" && sol != null) haciaCam = new Vector2(-sol.transform.forward.x, -sol.transform.forward.z).normalized;
            else if (p.Length > 2 && float.TryParse(p[2], System.Globalization.NumberStyles.Float, ci, out var grados))
                haciaCam = new Vector2(Mathf.Sin(grados * Mathf.Deg2Rad), Mathf.Cos(grados * Mathf.Deg2Rad));
            derecha = Vector3.Cross(Vector3.up, new Vector3(haciaCam.x, 0f, haciaCam.y).normalized);
        }
        else if (mundo)
        {
            bool Bueno(Vector2 q, float h)
            {
                var pie = new Vector3(q.x, h, q.y);
                if (!AlSol(pie, sol) || !Libre(pie)) return false;
                if (!conCompis) return true;
                foreach (var (_, lado) in Compis)
                    if (!AlSol(pie + derecha * lado, sol) || !Libre(pie + derecha * lado)) return false;
                return true;
            }
            bool hallado = Bueno(xz, y);
            for (int r = 2; r <= 10 && !hallado; r += 2)
                for (int k = 0; k < 12 && !hallado; k++)
                {
                    var cand = xz + new Vector2(Mathf.Cos(k * Mathf.PI / 6f), Mathf.Sin(k * Mathf.PI / 6f)) * r;
                    var gy = Suelo(cand, y0);
                    if (gy == null || Mathf.Abs(gy.Value - y0) > 1.5f || !Bueno(cand, gy.Value)) continue;
                    xz = cand; y = gy.Value; hallado = true; sitio = $"al sol a {r} m de la base";
                }
            if (!hallado) sitio = "base (no encontré sitio al sol)";
        }

        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_fotos_caras", hoja ? "NPCs" : nombre);
        Directory.CreateDirectory(carpeta);
        foreach (var f in Directory.GetFiles(carpeta, hoja ? nombre + "_*.png" : "*.png")) File.Delete(f);

        var informe = new StringBuilder();
        var soporte = new GameObject("Fotos de caras · " + nombre) { hideFlags = HideFlags.DontSave };
        SceneManager.MoveGameObjectToScene(soporte, escena);
        var camGo = new GameObject("Fotos de caras · cámara") { hideFlags = HideFlags.HideAndDontSave };
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var png = new Texture2D(W, H, TextureFormat.RGB24, false);
        int n = 0;
        try
        {
            soporte.transform.SetPositionAndRotation(new Vector3(xz.x, y, xz.y),
                Quaternion.LookRotation(new Vector3(haciaCam.x, 0f, haciaCam.y).normalized, Vector3.up));
            var (inst, anim, perfil, piezas) = Poner(prefab, soporte.transform, 0f, escena, informe, cuerpo);
            if (perfil == null) { informe.Append("sin NPCEmotionController o sin perfil"); return; }
            if (conCompis)
                foreach (var (ruta, lado) in Compis)
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                    if (pf == null) { informe.Append($"no encuentro {ruta} · "); continue; }
                    var c = Poner(pf, soporte.transform, lado, escena, informe, cuerpo);
                    if (c.perfil == null) continue;
                    var dc = soloEmocion != null ? c.perfil.GetEmotionData(soloEmocion.Value) : default;
                    if (soloEmocion != null && dc.emotion == soloEmocion.Value)
                        Cara(c.piezas, string.IsNullOrEmpty(dc.eyeMeshName) ? c.perfil.ojosDeReposoPorDefecto : dc.eyeMeshName,
                            string.IsNullOrEmpty(dc.mouthMeshName) ? c.perfil.bocaDeReposoPorDefecto : dc.mouthMeshName,
                            dc.simbolos != null && dc.simbolos.Length > 0 ? dc.simbolos[0] : null);
                    else Cara(c.piezas, c.perfil.ojosDeReposoPorDefecto, c.perfil.bocaDeReposoPorDefecto, null);
                }

            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt; cam.scene = escena; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1500f;
            var datos = camGo.AddComponent<UniversalAdditionalCameraData>();
            var pila = VolumeManager.instance.isInitialized ? VolumeManager.instance.stack : null;
            datos.renderPostProcessing = pila != null && pila.GetComponent<ColorAdjustments>() != null;
            if (!datos.renderPostProcessing) informe.Append("sin postproceso (volúmenes no listos) · ");

            // Centro de la cara: los ojos neutros
            var ojoNeutro = Cara(piezas, perfil.ojosDeReposoPorDefecto, perfil.bocaDeReposoPorDefecto, null);
            var cabeza = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            Vector3 cara = ojoNeutro != null ? ojoNeutro.bounds.center
                : (cabeza != null ? cabeza.position + Vector3.up * 0.15f : soporte.transform.position + Vector3.up * 1.3f);
            Vector3 fwd = soporte.transform.forward;
            var planos = new Dictionary<string, (Vector3 pos, Vector3 mira, float lente)>
            {
                ["cerca"] = (cara + fwd * 1.1f + Vector3.up * 0.02f, cara - Vector3.up * 0.04f, 28f),
                ["medio"] = (cara + fwd * 2.4f + Vector3.up * 0.1f, cara - Vector3.up * 0.45f, 35f),
                ["juego"] = (cara + fwd * 5f + Vector3.up * 1.5f, cara - Vector3.up * 0.5f, 50f),
                ["juego_lejos"] = (cara + fwd * 7.5f + Vector3.up * 2.5f, cara - Vector3.up * 0.5f, 50f),
            };
            if (cuerpo != null)
            {
                Vector3 lado = soporte.transform.right;
                planos["tres_cuartos"] = (cara + (fwd * 2.2f + lado * 1.5f) + Vector3.up * 0.1f, cara - Vector3.up * 0.4f, 35f);
            }

            // Las emociones del perfil (todas las que tenga, en orden) y, después, los ojos del juego que no usa ninguna
            // emoción (p. ej. el conjuro de Estela), con la boca de reposo.
            var tomas = new List<(string nombre, int orden, string ojos, string boca, string fx, bool lejos)>();
            var usados = new HashSet<string>();
            var lista = Emociones.Concat(perfil.emotions == null ? new NPCEmotion[0] : perfil.emotions.Select(x => x.emotion)).Distinct();
            foreach (var e in lista)
            {
                if (e == NPCEmotion.None) continue;
                string ojos = perfil.ojosDeReposoPorDefecto, boca = perfil.bocaDeReposoPorDefecto, fx = null;
                if (e != NPCEmotion.Neutral)
                {
                    var d = perfil.GetEmotionData(e);
                    if (d.emotion != e) continue;   // no la tiene
                    if (!string.IsNullOrEmpty(d.eyeMeshName)) ojos = d.eyeMeshName;
                    if (!string.IsNullOrEmpty(d.mouthMeshName)) boca = d.mouthMeshName;
                    if (d.simbolos != null && d.simbolos.Length > 0) fx = d.simbolos[Mathf.Min(2, d.simbolos.Length - 1)];
                }
                usados.Add(ojos);
                tomas.Add(($"{(int)e:00}_{e}", (int)e, ojos, boca, fx, e == NPCEmotion.Neutral || e == NPCEmotion.Crying || e == NPCEmotion.Happy));
            }
            int Num(string s) { int v; return s != null && s.StartsWith("Eye") && int.TryParse(s.Substring(3), out v) ? v : -1; }
            int baseOjos = Num(perfil.ojosDeReposoPorDefecto);
            int topeOjos = usados.Select(Num).DefaultIfEmpty(-1).Max();   // el último ojo de su juego que usa alguna emoción (llorar)
            if (baseOjos >= 0)
                foreach (var r in piezas.Where(r => Num(r.name) >= baseOjos && Num(r.name) <= topeOjos && !usados.Contains(r.name)).OrderBy(r => Num(r.name)))
                {
                    usados.Add(r.name);
                    tomas.Add(($"ojo_{r.name}", 100 + Num(r.name), r.name, perfil.bocaDeReposoPorDefecto, null, false));
                }
            if (hoja) tomas.RemoveAll(x => x.nombre != "00_Neutral");
            if (soloEmocion != null)
            {
                tomas.RemoveAll(x => x.orden != (int)soloEmocion.Value);
                for (int i = 0; i < tomas.Count; i++) { var tm = tomas[i]; tm.lejos = true; tomas[i] = tm; }
            }
            foreach (var toma in tomas)
            {
                Cara(piezas, toma.ojos, toma.boca, toma.fx);
                informe.Append($"{toma.nombre}={toma.ojos}/{toma.boca}{(toma.fx != null ? "/" + toma.fx : "")} ");
                foreach (var kv in planos)
                {
                    if (kv.Key != "cerca" && (!toma.lejos || hoja)) continue;
                    cam.transform.SetPositionAndRotation(kv.Value.pos, Quaternion.LookRotation(kv.Value.mira - kv.Value.pos, Vector3.up));
                    cam.fieldOfView = kv.Value.lente;
                    var peticion = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, peticion)) RenderPipeline.SubmitRenderRequest(cam, peticion);
                    else cam.Render();
                    var previo = RenderTexture.active;
                    RenderTexture.active = rt;
                    png.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    png.Apply();
                    RenderTexture.active = previo;
                    File.WriteAllBytes(Path.Combine(carpeta, $"{nombre}_{toma.nombre}_{kv.Key}.png"), png.EncodeToPNG());
                    n++;
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(soporte);
            Object.DestroyImmediate(camGo);
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(png);
            if (hoja) OrdenesDelEditor.Avisar("Hoja", $"{nombre}: {n} foto(s){(n == 0 ? " · " + informe : "")}");
            else OrdenesDelEditor.Avisar("Fotos de caras", $"{n} fotos en {carpeta} (escena {escena.name}, {sitio}: x={xz.x:F1} z={xz.y:F1} y={y:F2}, sol={(sol != null ? sol.name + " hacia " + sol.transform.forward.ToString("F2") : "ninguno")}, mira hacia {haciaCam.x:F2},{haciaCam.y:F2}, compañeros={conCompis}) · {informe}");
        }
    }
}
#endif
