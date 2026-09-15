using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Prueba de Will (GDD escena 17) — maqueta del laberinto de espejos como ISLAS FLOTANTES (10 sep 2026).
///
/// Decisión de Raúl tras ver el laberinto de paneles procedurales ("yo no puedo entregar esto"): darle arte real
/// con el pack `LWRP Floating Islands` (importado el 8 sep). Cada celda del laberinto es una isla flotante sobre
/// un cielo de estrellas; cada pasillo, un puente de cuerda del pack; los espejos reales (componente
/// `MirrorReflection` + shader `Sendero/PlanarMirror`, ya existentes) van como marcos/portales en las celdas
/// clave (inicio, meta, callejones de los ecos y cruces). Encaja con el Sendero de la biblia: "un espacio
/// suspendido entre estrellas reales".
///
/// Es una MAQUETA: crea una escena nueva (`Sendero_PruebaWill_Islas.unity`) y no toca `Sendero_PruebaWill.unity`
/// ni su cableado narrativo. Todo se mide por bounds (islas, puentes, marcos), nada a ojo; cada paso va en
/// `Paso(...)` para que un fallo no impida guardar; el `Informe.txt` de la carpeta anota medidas y avisos.
/// Revisión 2 (tras la primera captura): el pack está en unidades minúsculas (isla 2,3 m) — islas normalizadas a
/// 22 m, puentes a la escala que hace que el grande mida el hueco típico; marco de espejo procedural (pilares +
/// dintel) porque `Portal02` era un anillo de suelo; skybox solo entre materiales con shader Skybox.
/// Mismo grafo que `WillTrialMazeBuilder` (laberinto perfecto por backtracking + conexiones extra para tener
/// caminos distintos; meta = celda más lejana por BFS; callejones = ecos).</summary>
public static class WillTrialIslandsBuilder
{
    const string Pack = "Assets/Art/World/LWRP Floating Islands/Prefabs/";
    const string Tiny = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Prefab/";
    const string RutaEscena = "Assets/Scenes/Worlds/Sendero_PruebaWill_Islas.unity";
    const string Raiz = "PRUEBA_WILL_ISLAS";
    public const string AnclaInicio = "SENDERO_PRUEBAWILL_START";

    // Laberinto: 6×5 celdas (menos que las 7×7 de los paneles — cada isla es grande y los puentes tienen que
    // leerse; 30 islas ya dan recorrido de sobra). Semilla fija = mismo laberinto en cada regeneración.
    const int Ancho = 6, Alto = 5, Semilla = 20260910, ConexionesExtra = 4, EspejosRealesMax = 8;
    // Separación entre centros de isla = tamaño medio de isla × este factor (el hueco lo salva el puente).
    const float FactorSeparacion = 1.45f;
    const float VariacionAltura = 1.2f; // ±m entre islas, para que no sea un tablero plano
    // Revisión 2: el pack viene en unidades minúsculas (islas de 2,3 m, puentes de 1,5-2,9 m según el informe de la
    // primera maqueta) — todo se normaliza: islas a `LadoIsla` m de lado y puentes a la escala que hace que el
    // puente grande mida justo el hueco entre islas.
    const float LadoIsla = 22f;
    static float escalaIsla = 1, escalaPuente = 1;
    static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };

    sealed class Celda { public bool visitada; public bool[] abierta = new bool[4]; public int x, z; public float y; public Transform isla; public float radio; }

    static Transform raiz; static System.Text.StringBuilder informe; static string carpeta;
    static readonly Dictionary<GameObject, Bounds> medidas = new Dictionary<GameObject, Bounds>();

    [MenuItem("El Sendero/Escena/Generar Prueba de Will — islas flotantes (maqueta)")]
    public static void Crear()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play antes de generar.");
        var anterior = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(escena);
        carpeta = "Assets/Scenes/Worlds/PruebaWillIslas_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(carpeta); AssetDatabase.Refresh();
        informe = new System.Text.StringBuilder("Prueba de Will — maqueta de islas flotantes.\n");
        medidas.Clear();
        try
        {
            raiz = new GameObject(Raiz).transform;
            var rejilla = GenerarLaberinto(out var inicio, out var meta, out var callejones, out var cruces);
            Paso("Islas", () => ColocarIslas(rejilla));
            Paso("Puentes", () => ColocarPuentes(rejilla));
            Paso("Espejos", () => ColocarEspejos(rejilla, inicio, meta, callejones, cruces));
            Paso("Vegetación", () => Vegetar(rejilla));
            Paso("Marcadores", () => Marcadores(rejilla, inicio, meta, callejones));
            Paso("Ambiente", Ambiente);
            Paso("NavMesh", () => HornearNavMesh());
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(escena, RutaEscena)) throw new IOException("No se pudo guardar " + RutaEscena);
            File.WriteAllText(carpeta + "/Informe.txt", informe.ToString());
            Debug.Log("Prueba de Will (islas) guardada: " + RutaEscena + "\n" + informe);
        }
        finally
        {
            if (anterior.IsValid() && anterior.isLoaded) SceneManager.SetActiveScene(anterior);
            EditorSceneManager.CloseScene(escena, true);
            AssetDatabase.Refresh();
        }
    }

    static void Paso(string nombre, Action accion)
    {
        try { accion(); }
        catch (Exception e) { informe.AppendLine($"ERROR en el paso '{nombre}': {e.GetType().Name}: {e.Message}"); Debug.LogException(e); }
    }

    // ---------- Laberinto ----------
    static Celda[,] GenerarLaberinto(out Celda inicio, out Celda meta, out List<Celda> callejones, out List<Celda> cruces)
    {
        var azar = new System.Random(Semilla);
        var g = new Celda[Ancho, Alto];
        for (int x = 0; x < Ancho; x++) for (int z = 0; z < Alto; z++) g[x, z] = new Celda { x = x, z = z };
        var pila = new Stack<Celda>(); inicio = g[0, 0]; inicio.visitada = true; pila.Push(inicio);
        while (pila.Count > 0)
        {
            var c = pila.Peek(); var opciones = new List<int>();
            for (int d = 0; d < 4; d++) { int nx = c.x + DX[d], nz = c.z + DZ[d]; if (nx >= 0 && nx < Ancho && nz >= 0 && nz < Alto && !g[nx, nz].visitada) opciones.Add(d); }
            if (opciones.Count == 0) { pila.Pop(); continue; }
            int dir = opciones[azar.Next(opciones.Count)]; var n = g[c.x + DX[dir], c.z + DZ[dir]];
            c.abierta[dir] = true; n.abierta[(dir + 2) % 4] = true; n.visitada = true; pila.Push(n);
        }
        // Conexiones extra: caminos distintos de verdad (pedido de Raúl), no un único recorrido.
        int extra = 0, intentos = 0;
        while (extra < ConexionesExtra && intentos++ < 500)
        {
            var c = g[azar.Next(Ancho), azar.Next(Alto)]; int d = azar.Next(4); int nx = c.x + DX[d], nz = c.z + DZ[d];
            if (nx < 0 || nx >= Ancho || nz < 0 || nz >= Alto || c.abierta[d]) continue;
            c.abierta[d] = true; g[nx, nz].abierta[(d + 2) % 4] = true; extra++;
        }
        // Meta = celda más lejana por BFS.
        var dist = new Dictionary<Celda, int> { [inicio] = 0 }; var cola = new Queue<Celda>(); cola.Enqueue(inicio);
        while (cola.Count > 0) { var c = cola.Dequeue(); for (int d = 0; d < 4; d++) if (c.abierta[d]) { var n = g[c.x + DX[d], c.z + DZ[d]]; if (!dist.ContainsKey(n)) { dist[n] = dist[c] + 1; cola.Enqueue(n); } } }
        meta = inicio; foreach (var kv in dist) if (kv.Value > dist[meta]) meta = kv.Key;
        callejones = new List<Celda>(); cruces = new List<Celda>();
        foreach (var c in g) { int salidas = 0; for (int d = 0; d < 4; d++) if (c.abierta[d]) salidas++; if (salidas == 1 && c != inicio && c != meta) callejones.Add(c); if (salidas >= 3) cruces.Add(c); }
        // Alturas: variación suave por celda, misma para vecinas cercanas (los puentes admiten poca pendiente).
        foreach (var c in g) c.y = (Mathf.PerlinNoise(c.x * .37f + 2.1f, c.z * .37f + 7.3f) - .5f) * 2 * VariacionAltura;
        informe.AppendLine($"Laberinto {Ancho}×{Alto}: inicio ({inicio.x},{inicio.z}), meta ({meta.x},{meta.z}) a {dist[meta]} pasos, {callejones.Count} callejones (ecos), {cruces.Count} cruces, {extra} conexiones extra.");
        return g;
    }

    // ---------- Medidas ----------
    static Bounds Medir(GameObject prefab)
    {
        if (medidas.TryGetValue(prefab, out var b)) return b;
        var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab); tmp.transform.position = Vector3.zero; tmp.transform.rotation = Quaternion.identity;
        var rs = tmp.GetComponentsInChildren<Renderer>(); b = rs.Length > 0 ? rs[0].bounds : new Bounds(Vector3.zero, Vector3.one);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        Object.DestroyImmediate(tmp); medidas[prefab] = b; return b;
    }
    static GameObject Cargar(string ruta) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(ruta); if (p == null) informe.AppendLine("Prefab no encontrado: " + ruta); return p; }
    static Bounds BoundsMundo(GameObject go) { var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
    static void AsegurarColisiones(GameObject go) { foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) if (mf.GetComponent<Collider>() == null && mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; }

    static float separacion;
    static Vector3 CentroCelda(Celda c) => new Vector3(c.x * separacion, c.y, c.z * separacion);

    // ---------- Islas ----------
    static void ColocarIslas(Celda[,] g)
    {
        var islas = new List<GameObject>();
        foreach (var n in new[] { "island 1", "island 2", "island 3" }) { var p = Cargar(Pack + n + ".prefab"); if (p != null) islas.Add(p); }
        if (islas.Count == 0) throw new FileNotFoundException("Sin prefabs de isla en " + Pack);
        float lado = 0; foreach (var p in islas) { var b = Medir(p); lado = Mathf.Max(lado, Mathf.Max(b.size.x, b.size.z)); informe.AppendLine($"Isla '{p.name}' (nativa): {b.size.x:0.0}×{b.size.z:0.0}×{b.size.y:0.0} m"); }
        escalaIsla = LadoIsla / lado; lado = LadoIsla;
        separacion = lado * FactorSeparacion;
        var grupo = new GameObject("Islas").transform; grupo.SetParent(raiz);
        var azar = new System.Random(Semilla + 1);
        foreach (var c in g)
        {
            var prefab = islas[azar.Next(islas.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo); go.name = $"Isla ({c.x},{c.z})";
            go.transform.rotation = Quaternion.Euler(0, azar.Next(4) * 90, 0);
            go.transform.localScale = go.transform.localScale * escalaIsla;
            var b = BoundsMundo(go);
            // La cara superior de la isla (hierba) queda a la cota de la celda; el centro en planta, en su sitio.
            var centro = CentroCelda(c);
            go.transform.position += new Vector3(centro.x - b.center.x, centro.y - (b.max.y - .25f), centro.z - b.center.z);
            AsegurarColisiones(go);
            c.isla = go.transform; c.radio = Mathf.Min(b.extents.x, b.extents.z);
        }
        informe.AppendLine($"Islas colocadas: {Ancho * Alto} a {LadoIsla} m de lado (escala ×{escalaIsla:0.0}), separación {separacion:0.0} m entre centros, hueco medio {separacion - lado:0.0} m.");
    }

    // ---------- Puentes ----------
    static void ColocarPuentes(Celda[,] g)
    {
        var puentes = new List<GameObject>();
        foreach (var n in new[] { "small bridge", "medium bridge", "big bridge" }) { var p = Cargar(Pack + n + ".prefab"); if (p != null) puentes.Add(p); }
        if (puentes.Count == 0) throw new FileNotFoundException("Sin prefabs de puente en " + Pack);
        float largoMayor = 0; foreach (var p in puentes) { var b = Medir(p); largoMayor = Mathf.Max(largoMayor, Mathf.Max(b.size.x, b.size.z)); informe.AppendLine($"Puente '{p.name}' (nativo): largo {Mathf.Max(b.size.x, b.size.z):0.0} m, ancho {Mathf.Min(b.size.x, b.size.z):0.0} m, alto {b.size.y:0.0} m"); }
        // Hueco típico entre bordes de isla ≈ separación − lado + 3 m de solape; el puente grande se escala a ese largo.
        float huecoTipico = separacion - LadoIsla + 3f; escalaPuente = huecoTipico / Mathf.Max(.01f, largoMayor);
        informe.AppendLine($"Puentes: escala uniforme ×{escalaPuente:0.0} (el grande mide ahora {largoMayor * escalaPuente:0.0} m; ancho ≈ {Medir(puentes[puentes.Count - 1]).size.z * escalaPuente:0.0} m).");
        var grupo = new GameObject("Puentes").transform; grupo.SetParent(raiz); int n2 = 0;
        foreach (var c in g) for (int d = 0; d < 2; d++) // solo N y E para no duplicar
        {
            if (!c.abierta[d]) continue;
            var v = g[c.x + DX[d], c.z + DZ[d]];
            var a = CentroCelda(c); var b2 = CentroCelda(v);
            var dir = (b2 - a); dir.y = 0; dir.Normalize();
            // Extremos del puente: borde de cada isla (radio) menos 1.5 m de solape para que apoye encima.
            var pa = a + dir * (c.radio - 1.5f); var pb = b2 - dir * (v.radio - 1.5f);
            float largo = Vector3.Distance(new Vector3(pa.x, 0, pa.z), new Vector3(pb.x, 0, pb.z));
            // El puente cuya longitud nativa esté más cerca; se estira SOLO a lo largo hasta encajar.
            GameObject mejor = puentes[0]; float dif = float.MaxValue;
            foreach (var p in puentes) { var mb = Medir(p); float l = Mathf.Max(mb.size.x, mb.size.z) * escalaPuente; if (Mathf.Abs(l - largo) < dif) { dif = Mathf.Abs(l - largo); mejor = p; } }
            var mb2 = Medir(mejor); bool largoEnX = mb2.size.x >= mb2.size.z; float nativo = (largoEnX ? mb2.size.x : mb2.size.z) * escalaPuente;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(mejor, grupo); go.name = $"Puente ({c.x},{c.z})→({v.x},{v.z})";
            go.transform.rotation = Quaternion.LookRotation(dir) * (largoEnX ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
            var esc = go.transform.localScale * escalaPuente; if (largoEnX) esc.x *= largo / nativo; else esc.z *= largo / nativo; go.transform.localScale = esc;
            var bw = BoundsMundo(go); var medio = (pa + pb) / 2;
            // Cota: la pasarela del puente (parte alta de sus bounds menos la barandilla ≈ 40% del alto) a la media de ambas islas.
            go.transform.position += new Vector3(medio.x - bw.center.x, medio.y - (bw.min.y + bw.size.y * .4f), medio.z - bw.center.z);
            AsegurarColisiones(go); n2++;
        }
        informe.AppendLine($"Puentes colocados: {n2}. Revisar en Unity que la pasarela apoya en la hierba (constante 0.4 del alto del prefab).");
    }

    // ---------- Espejos ----------
    static void ColocarEspejos(Celda[,] g, Celda inicio, Celda meta, List<Celda> callejones, List<Celda> cruces)
    {
        // Revisión 2: `Portal02` resultó ser un anillo en el suelo — el marco se hace con dos pilares del pack Tiny y
        // un dintel de piedra (cubo), medidos: 4.2 m de alto, 3.2 m de luz.
        var pilar = Cargar(Tiny + "BuildingUtilityDeco/Pillar03.prefab");
        var piedra = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Piedra del marco" };
        piedra.SetColor("_BaseColor", new Color(.42f, .45f, .55f)); piedra.SetFloat("_Smoothness", .15f); AssetDatabase.CreateAsset(piedra, carpeta + "/PiedraMarco.mat");
        var shaderReal = Shader.Find("Sendero/PlanarMirror");
        var reflejo = Type.GetType("MirrorReflection") ?? Type.GetType("MirrorReflection, Assembly-CSharp");
        var oscuro = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Espejo oscuro" };
        oscuro.SetColor("_BaseColor", new Color(.55f, .66f, .8f)); oscuro.SetFloat("_Smoothness", .95f); oscuro.SetFloat("_Metallic", .9f);
        AssetDatabase.CreateAsset(oscuro, carpeta + "/EspejoOscuro.mat");
        var grupo = new GameObject("Espejos").transform; grupo.SetParent(raiz);
        var claves = new List<Celda> { inicio, meta }; claves.AddRange(callejones); claves.AddRange(cruces);
        int reales = 0, total = 0;
        foreach (var c in claves)
        {
            if (c.isla == null) continue;
            // El espejo mira hacia el centro de la isla, colocado en el borde opuesto a la primera salida abierta.
            int salida = 0; for (int d = 0; d < 4; d++) if (c.abierta[d]) { salida = d; break; }
            var haciaFuera = -new Vector3(DX[salida], 0, DZ[salida]);
            var pos = CentroCelda(c) + haciaFuera * (c.radio * .55f);
            var frente = -haciaFuera;
            var contenedor = new GameObject($"Espejo ({c.x},{c.z})").transform; contenedor.SetParent(grupo); contenedor.position = pos; contenedor.rotation = Quaternion.LookRotation(frente);
            float altoMarco = 4.2f, luz = 3.2f;
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var basePilar = pos + contenedor.right * (lado * luz / 2);
                if (pilar != null)
                {
                    var m = (GameObject)PrefabUtility.InstantiatePrefab(pilar, contenedor); m.transform.rotation = contenedor.rotation;
                    var mb = Medir(pilar); float esc = mb.size.y > .01f ? altoMarco / mb.size.y : 1; m.transform.localScale = Vector3.one * esc;
                    var bw = BoundsMundo(m); m.transform.position += new Vector3(basePilar.x - bw.center.x, basePilar.y - bw.min.y, basePilar.z - bw.center.z);
                }
                else
                {
                    var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube); cubo.name = "Pilar"; cubo.transform.SetParent(contenedor);
                    cubo.transform.position = basePilar + Vector3.up * altoMarco / 2; cubo.transform.rotation = contenedor.rotation; cubo.transform.localScale = new Vector3(.4f, altoMarco, .4f);
                    cubo.GetComponent<Renderer>().sharedMaterial = piedra;
                }
            }
            var dintel = GameObject.CreatePrimitive(PrimitiveType.Cube); dintel.name = "Dintel"; dintel.transform.SetParent(contenedor);
            dintel.transform.position = pos + Vector3.up * (altoMarco + .25f); dintel.transform.rotation = contenedor.rotation; dintel.transform.localScale = new Vector3(luz + 1f, .5f, .6f);
            dintel.GetComponent<Renderer>().sharedMaterial = piedra;
            // Lámina de espejo: un quad dentro del marco.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Lámina"; quad.transform.SetParent(contenedor);
            quad.transform.localPosition = new Vector3(0, altoMarco * .5f, 0); // centrada entre los pilares quad.transform.localRotation = Quaternion.Euler(0, 180, 0); // el quad mira a -Z local; el frente del contenedor es +Z
            quad.transform.localScale = new Vector3(luz, altoMarco, 1);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            bool real = reales < EspejosRealesMax && shaderReal != null && reflejo != null;
            if (real)
            {
                var mat = new Material(shaderReal) { name = $"Espejo real {reales}" }; AssetDatabase.CreateAsset(mat, carpeta + $"/EspejoReal_{reales}.mat");
                quad.GetComponent<Renderer>().sharedMaterial = mat; quad.AddComponent(reflejo); reales++;
            }
            else quad.GetComponent<Renderer>().sharedMaterial = oscuro;
            total++;
        }
        informe.AppendLine($"Espejos: {total} marcos ({reales} con reflejo real vía MirrorReflection + Sendero/PlanarMirror{(shaderReal == null ? " — SHADER NO ENCONTRADO, todos oscuros" : "")}{(reflejo == null ? " — COMPONENTE MirrorReflection NO ENCONTRADO" : "")}).");
    }

    // ---------- Vegetación ----------
    static void Vegetar(Celda[,] g)
    {
        var arboles = new List<GameObject>();
        foreach (var n in new[] { "spruce tree 1", "spruce tree 2", "thin spruce tree 1", "thin spruce tree 2", "maple tree 1", "maple tree 2", "maple tree 3", "tree 1", "tree 2", "tree 3" })
        { var p = Cargar(Pack + n + ".prefab"); if (p != null) arboles.Add(p); }
        if (arboles.Count == 0) return;
        var grupo = new GameObject("Vegetación").transform; grupo.SetParent(raiz);
        var azar = new System.Random(Semilla + 2); int total = 0;
        foreach (var c in g)
        {
            if (c.isla == null) continue;
            int cuantos = 1 + azar.Next(3);
            for (int i = 0; i < cuantos; i++)
            {
                // Ángulo alejado (>35°) de cualquier salida abierta, para no tapar la entrada de los puentes.
                float ang = 0; bool ok = false;
                for (int intento = 0; intento < 12 && !ok; intento++)
                {
                    ang = (float)azar.NextDouble() * 360; ok = true;
                    for (int d = 0; d < 4; d++) if (c.abierta[d]) { float angSalida = Mathf.Atan2(DZ[d], DX[d]) * Mathf.Rad2Deg; if (Mathf.Abs(Mathf.DeltaAngle(ang, angSalida)) < 35) ok = false; }
                }
                if (!ok) continue;
                float r = c.radio * Mathf.Lerp(.45f, .7f, (float)azar.NextDouble());
                var pos = CentroCelda(c) + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, 0, Mathf.Sin(ang * Mathf.Deg2Rad) * r);
                var prefab = arboles[azar.Next(arboles.Count)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
                go.transform.rotation = Quaternion.Euler(0, (float)azar.NextDouble() * 360, 0);
                var mb = Medir(prefab); float esc = mb.size.y > .01f ? Mathf.Lerp(3.5f, 5.5f, (float)azar.NextDouble()) / mb.size.y : 1; go.transform.localScale = Vector3.one * esc;
                var bw = BoundsMundo(go); go.transform.position += new Vector3(pos.x - bw.center.x, pos.y - bw.min.y, pos.z - bw.center.z);
                total++;
            }
        }
        informe.AppendLine($"Árboles del pack en las islas: {total} (3.5-5.5 m, fuera de las bocas de los puentes).");
    }

    // ---------- Marcadores ----------
    static void Marcadores(Celda[,] g, Celda inicio, Celda meta, List<Celda> callejones)
    {
        var ancla = new GameObject(AnclaInicio); ancla.transform.SetParent(raiz); ancla.transform.position = CentroCelda(inicio) + Vector3.up * .1f;
        var cristal = Cargar(Tiny + "BuildingUtilityDeco/Crystal01.prefab");
        if (cristal != null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cristal, raiz); go.name = "META — cristal provisional";
            var mb = Medir(cristal); go.transform.localScale = Vector3.one * (mb.size.y > .01f ? 3f / mb.size.y : 1);
            var bw = BoundsMundo(go); var pos = CentroCelda(meta); go.transform.position += new Vector3(pos.x - bw.center.x, pos.y - bw.min.y, pos.z - bw.center.z);
        }
        int i = 1; foreach (var c in callejones) { var m = new GameObject($"ECO_{i++} ({c.x},{c.z})"); m.transform.SetParent(raiz); m.transform.position = CentroCelda(c) + Vector3.up * .1f; }
        informe.AppendLine($"Marcadores: ancla '{AnclaInicio}' en el inicio, cristal en la meta, {callejones.Count} marcadores ECO_n en los callejones (para el builder de contenido).");
    }

    // ---------- Ambiente ----------
    static void Ambiente()
    {
        var luz = new GameObject("Luz fría").AddComponent<Light>(); luz.transform.SetParent(raiz);
        luz.type = LightType.Directional; luz.intensity = .9f; luz.color = new Color(.82f, .88f, 1f); luz.transform.rotation = Quaternion.Euler(52, -30, 0); luz.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.35f, .42f, .62f); RenderSettings.ambientEquatorColor = new Color(.18f, .2f, .32f); RenderSettings.ambientGroundColor = new Color(.05f, .05f, .1f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = new Color(.09f, .1f, .2f); RenderSettings.fogDensity = .0045f;
        // Cielo: el skybox del Sendero si existe (mismo que el hub); si no, fondo azul noche.
        // Revisión 2: solo materiales cuyo shader sea de skybox (la primera búsqueda por nombre dio con un material de huellas).
        Material skybox = null;
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var ruta = AssetDatabase.GUIDToAssetPath(guid); if (!ruta.StartsWith("Assets/")) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(ruta); if (m == null || m.shader == null || !m.shader.name.StartsWith("Skybox")) continue;
            string n = ruta.ToLowerInvariant();
            if (n.Contains("star") || n.Contains("sendero") || n.Contains("night") || n.Contains("noche") || n.Contains("space")) { skybox = m; break; }
            if (skybox == null) skybox = m;
        }
        if (skybox != null) { RenderSettings.skybox = skybox; informe.AppendLine("Skybox: " + AssetDatabase.GetAssetPath(skybox) + " (si no es el del Sendero, cambiarlo a mano — se eligió por nombre)"); } else informe.AppendLine("Skybox: ningún material de skybox en Assets; fondo de color de cámara.");
        var cam = new GameObject("Cámara — vista de maqueta").AddComponent<Camera>(); cam.transform.SetParent(raiz);
        var centro = new Vector3((Ancho - 1) * separacion / 2, 0, (Alto - 1) * separacion / 2);
        cam.transform.position = centro + new Vector3(0, separacion * 4.5f, -separacion * 3.5f); cam.transform.LookAt(centro);
        cam.clearFlags = skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .06f, .14f); cam.farClipPlane = 3000;
    }

    static void HornearNavMesh()
    {
        var tipo = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (tipo == null) { informe.AppendLine("NavMesh: paquete Unity.AI.Navigation no encontrado; sin hornear."); return; }
        var comp = raiz.gameObject.AddComponent(tipo);
        var metodo = tipo.GetMethod("BuildNavMesh"); if (metodo != null) { metodo.Invoke(comp, null); informe.AppendLine("NavMesh horneado sobre islas y puentes."); }
    }
}
