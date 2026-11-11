#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Pone el aro de runas al cuello del Demonio 1: mide el cuello sobre la malla del propio
/// prefab, cuelga el aro del hueso del cuello (sigue la animación) y le hace el modelo: una
/// banda de hierro forjado con runas en relieve, partida en dos piezas (arco y trozo) que encajan
/// mientras lo lleva puesto y se separan al caer el jefe (RuneCollar + PiezaQueCae).
/// Es el mismo aro que Eldran enseña roto en el Capítulo 2. Idempotente: rehace el modelo y la luz.
/// Ver INC-923, INC-1067.
public static class AroDeRunasEnElCuello
{
    const string Prefab = "Assets/Prefabs/Enemy/Demon.prefab";
    /// Eldran recoge el aro roto del camino mientras Will duerme: las piezas caídas se retiran al
    /// empezar el Cap. 2 (la escena de Liam o, si se arranca más tarde, la mañana después). Ver INC-962.
    public static readonly string[] SenalesQueRecogenElAro = { "LIAM_CRYSTAL_START", "MANANA_DESPUES_START" };
    const string Carpeta = "Assets/Art/Items/AroDelDemonio";
    const string NombreModelo = "Modelo del aro";

    // Proporciones del aro respecto a su radio (línea media de la banda).
    const float Grosor = 0.15f;      // medio grosor radial
    const float Alto = 0.42f;        // media altura de la banda
    const float Holgura = 1.12f;     // el borde interior queda un 12 % por fuera de la piel: se ve entero desde la cámara de combate

    // Piezas: el trozo es el que Will se lleva en el Capítulo 2.
    const float TrozoDesde = -15f, TrozoHasta = 32f;
    const float ArcoDesde = 32f, ArcoHasta = 345f;

    static readonly Color RunasApagadas = new(0.32f, 0.1f, 0.04f);
    // HDR por encima del umbral del bloom (1): las runas y los filos se leen como luz. Ver INC-1067.
    static readonly Color RunasEncendidas = new(6f, 2.4f, 0.8f);
    static readonly Color ColorDeLaLuz = new(1f, 0.55f, 0.25f);
    const string NombreLuz = "Luz del aro";

    [MenuItem("El Sendero/Combate/Demonio 1: poner el aro de runas al cuello")]
    public static void Poner()
    {
        if (Application.isPlaying) { Debug.LogError("[AroDelCuello] Hay que salir de Play."); return; }

        var hierro = AssetDatabase.LoadAssetAtPath<Material>($"{Carpeta}/Hierro del aro.mat");
        if (hierro == null) { Debug.LogError($"[AroDelCuello] Falta {Carpeta}/Hierro del aro.mat"); return; }
        var runasMat = MaterialDeRunas();

        var raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var aro = raiz.GetComponentInChildren<RuneCollar>(true);
            if (aro == null) { Debug.LogError("[AroDelCuello] Demon.prefab no tiene RuneCollar."); return; }
            var cuello = Buscar(raiz.transform, "RigNeck") ?? Buscar(raiz.transform, "Neck");
            var cabeza = Buscar(raiz.transform, "RigHead") ?? Buscar(raiz.transform, "Head");
            if (cuello == null) { Debug.LogError("[AroDelCuello] No encuentro el hueso del cuello."); return; }

            // ── Medir el cuello ──
            Vector3 eje = Vector3.up;
            float largo = 0.25f;
            if (cabeza != null && (cabeza.position - cuello.position).sqrMagnitude > 1e-6f)
            {
                eje = (cabeza.position - cuello.position).normalized;
                largo = Vector3.Distance(cabeza.position, cuello.position);
            }
            if (!MedirCuello(raiz, cuello.position, eje, largo, out var centro, out float radioPiel))
            {
                radioPiel = 0.18f * raiz.transform.lossyScale.x;
                centro = cuello.position + eje * largo * 0.4f;
                Debug.LogWarning("[AroDelCuello] No he podido medir el cuello en la malla: radio por defecto.");
            }
            float radio = radioPiel * Holgura / (1f - Grosor);

            // ── Colgar el aro del cuello ──
            // Los brillos sueltos (el aro de luces azules de antes) se quitan: la ventana de daño la
            // enseñan las runas del propio modelo, que se encienden. Ver INC-985.
            var so = new SerializedObject(aro);
            var glowProp = so.FindProperty("glowVisuals");
            for (int i = 0; i < glowProp.arraySize; i++)
                if (glowProp.GetArrayElementAtIndex(i).objectReferenceValue is GameObject g)
                {
                    var instancia = PrefabUtility.GetOutermostPrefabInstanceRoot(g);
                    Object.DestroyImmediate(instancia != null && instancia != raiz ? instancia : g);
                }
            glowProp.arraySize = 0;

            var t = aro.transform;
            t.SetParent(cuello, false);
            Vector3 delante = Vector3.ProjectOnPlane(raiz.transform.forward, eje);
            if (delante.sqrMagnitude < 1e-4f) delante = Vector3.ProjectOnPlane(raiz.transform.right, eje);
            t.SetPositionAndRotation(centro, Quaternion.LookRotation(delante.normalized, eje));
            float escalaHueso = (cuello.lossyScale.x + cuello.lossyScale.y + cuello.lossyScale.z) / 3f;
            t.localScale = Vector3.one / Mathf.Max(1e-5f, escalaHueso);

            var esfera = aro.GetComponent<SphereCollider>();
            if (esfera) { esfera.center = Vector3.zero; esfera.radius = radio * (1f + Grosor) * 1.25f; }

            // ── El modelo ──
            var viejo = t.Find(NombreModelo);
            if (viejo) Object.DestroyImmediate(viejo.gameObject);
            var modelo = new GameObject(NombreModelo).transform;
            modelo.SetParent(t, false);
            modelo.gameObject.layer = aro.gameObject.layer;

            var arco = Pieza(modelo, "Arco", radio, ArcoDesde, ArcoHasta, hierro, runasMat, out var runasArco);
            var trozo = Pieza(modelo, "Trozo", radio, TrozoDesde, TrozoHasta, hierro, runasMat, out var runasTrozo);

            so.FindProperty("runas").arraySize = 2;
            so.FindProperty("runas").GetArrayElementAtIndex(0).objectReferenceValue = runasArco;
            so.FindProperty("runas").GetArrayElementAtIndex(1).objectReferenceValue = runasTrozo;
            so.FindProperty("piezas").arraySize = 2;
            so.FindProperty("piezas").GetArrayElementAtIndex(0).objectReferenceValue = arco;
            so.FindProperty("piezas").GetArrayElementAtIndex(1).objectReferenceValue = trozo;
            so.FindProperty("runasApagadas").colorValue = RunasApagadas;
            so.FindProperty("runasEncendidas").colorValue = RunasEncendidas;

            // Luz que se enciende con las runas: en la ventana de daño ilumina cuello y cara.
            var luzVieja = t.Find(NombreLuz);
            if (luzVieja) Object.DestroyImmediate(luzVieja.gameObject);
            var luzGo = new GameObject(NombreLuz);
            luzGo.layer = aro.gameObject.layer;
            luzGo.transform.SetParent(t, false);
            luzGo.transform.localPosition = Vector3.forward * radio * 1.6f;
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = ColorDeLaLuz;
            luz.range = Mathf.Max(1.5f, radio * 12f);
            luz.intensity = 0f;
            luz.shadows = LightShadows.None;
            luz.enabled = false;
            so.FindProperty("luzDelBrillo").objectReferenceValue = luz;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AroDelCuello] Aro puesto en '{cuello.name}': radio {radio:0.000} m (cuello {radioPiel:0.000} m).");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // ── Medida ──

    /// Corta la malla del cuerpo en rodajas a lo largo del cuello y se queda con la más estrecha:
    /// su centro y su radio (la mediana de las distancias al centro, para no contar cuernos o alas).
    static bool MedirCuello(GameObject raiz, Vector3 cuello, Vector3 eje, float largo, out Vector3 centro, out float radio)
    {
        centro = cuello; radio = 0f;
        var puntos = new List<Vector3>();
        var malla = new Mesh();
        foreach (var smr in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            smr.BakeMesh(malla, true);
            var tr = smr.transform;
            foreach (var v in malla.vertices) puntos.Add(tr.position + tr.rotation * v);
        }
        Object.DestroyImmediate(malla);
        if (puntos.Count == 0) return false;

        float media = largo * 0.12f;
        bool hay = false;
        var rodaja = new List<Vector3>();
        var dist = new List<float>();
        for (float k = -0.1f; k <= 0.81f; k += 0.1f)
        {
            Vector3 p = cuello + eje * largo * k;
            rodaja.Clear();
            foreach (var v in puntos)
                if (Mathf.Abs(Vector3.Dot(v - p, eje)) <= media) rodaja.Add(Vector3.ProjectOnPlane(v - p, eje));
            if (rodaja.Count < 8) continue;

            // Centro: media de los puntos cercanos al hueso (los más alejados son alas o brazos).
            dist.Clear();
            foreach (var d in rodaja) dist.Add(d.magnitude);
            dist.Sort();
            float corte = dist[(int)(dist.Count * 0.6f)];
            Vector3 c = Vector3.zero; int n = 0;
            foreach (var d in rodaja) if (d.magnitude <= corte) { c += d; n++; }
            c /= Mathf.Max(1, n);

            dist.Clear();
            foreach (var d in rodaja) dist.Add((d - c).magnitude);
            dist.Sort();
            // Percentil 70: la mediana deja el aro metido en la piel; más alto cuenta pelo, papada o
            // cuernos y lo deja holgado. Ver INC-985 e INC-1067.
            float r = dist[(int)(dist.Count * 0.7f)];
            if (!hay || r < radio) { hay = true; radio = r; centro = p + c; }
        }
        return hay && radio > 1e-4f;
    }

    // ── Modelo ──

    static PiezaQueCae Pieza(Transform padre, string nombre, float radio, float desde, float hasta,
                             Material hierro, Material runas, out Renderer rendererRunas)
    {
        var go = new GameObject(nombre);
        go.layer = padre.gameObject.layer;
        go.transform.SetParent(padre, false);
        var mallaHierro = Guardar($"Malla aro del cuello ({nombre.ToLowerInvariant()})", Banda(radio, desde, hasta));
        go.AddComponent<MeshFilter>().sharedMesh = mallaHierro;
        go.AddComponent<MeshRenderer>().sharedMaterial = hierro;

        var r = new GameObject("Runas");
        r.layer = go.layer;
        r.transform.SetParent(go.transform, false);
        r.AddComponent<MeshFilter>().sharedMesh = Guardar($"Malla runas del cuello ({nombre.ToLowerInvariant()})", Runas(radio, desde, hasta));
        var mr = r.AddComponent<MeshRenderer>();
        mr.sharedMaterial = runas;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        rendererRunas = mr;

        var pieza = go.AddComponent<PiezaQueCae>();
        pieza.Configurar(mallaHierro, new Renderer[] { mr }, new Color(0.06f, 0.03f, 0.02f));
        pieza.RetirarConSenales(SenalesQueRecogenElAro);
        return pieza;
    }

    /// Banda de hierro entre dos ángulos (grados, alrededor del eje Y local), con sección de
    /// rectángulo redondeado y tapas en los cortes.
    static Mesh Banda(float radio, float desde, float hasta)
    {
        const int lados = 20;
        float a = radio * Grosor, b = radio * Alto;
        var v = new List<Vector3>(); var t = new List<int>();
        int anillos = Mathf.Max(2, Mathf.CeilToInt(72 * (hasta - desde) / 360f) + 1);
        var seccion = new Vector2[lados];
        for (int j = 0; j < lados; j++)
        {
            float ang = j * Mathf.PI * 2f / lados;
            float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
            // Superelipse: casi plana por fuera, con los cantos redondeados, como una pieza forjada.
            seccion[j] = new Vector2(a * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.45f), b * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.3f));
        }
        for (int i = 0; i < anillos; i++)
        {
            float ang = Mathf.Deg2Rad * Mathf.Lerp(desde, hasta, i / (float)(anillos - 1));
            var fuera = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            for (int j = 0; j < lados; j++)
                v.Add(fuera * (radio + seccion[j].x) + Vector3.up * seccion[j].y);
        }
        for (int i = 0; i < anillos - 1; i++)
            for (int j = 0; j < lados; j++)
            {
                int a0 = i * lados + j, a1 = i * lados + (j + 1) % lados, b0 = a0 + lados, b1 = a1 + lados;
                t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
        var m = new Mesh();
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals();
        // Tapas (vértices propios para que tengan normal plana).
        var vs = new List<Vector3>(v); var ns = new List<Vector3>(m.normals);
        foreach (int fila in new[] { 0, anillos - 1 })
        {
            float ang = Mathf.Deg2Rad * (fila == 0 ? desde : hasta);
            var tang = new Vector3(-Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (fila == 0 ? -1f : 1f);
            int c0 = vs.Count;
            Vector3 centro = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radio;
            vs.Add(centro); ns.Add(tang);
            for (int j = 0; j < lados; j++) { vs.Add(v[fila * lados + j]); ns.Add(tang); }
            for (int j = 0; j < lados; j++)
            {
                int p = c0 + 1 + j, q = c0 + 1 + (j + 1) % lados;
                if (fila == 0) t.AddRange(new[] { c0, q, p }); else t.AddRange(new[] { c0, p, q });
            }
        }
        m.SetVertices(vs); m.SetNormals(ns); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    // Trazos de cada runa en un cuadro de -0,5..0,5 (x a lo largo del aro, y hacia arriba).
    static readonly float[][] Glifos =
    {
        new[] { 0f, -.5f, 0f, .5f,   0f, .5f, .32f, .15f },
        new[] { -.18f, -.5f, -.18f, .5f,   .18f, -.5f, .18f, .5f,   -.18f, .22f, .18f, -.22f },
        new[] { -.25f, -.5f, .25f, .5f,   .25f, -.5f, -.25f, .5f },
        new[] { 0f, -.5f, 0f, .5f,   0f, .5f, -.26f, .18f,   0f, .5f, .26f, .18f },
        new[] { 0f, .5f, .26f, 0f,   .26f, 0f, 0f, -.5f,   0f, -.5f, -.26f, 0f,   -.26f, 0f, 0f, .5f },
        new[] { -.2f, -.5f, -.2f, .5f,   -.2f, .5f, .2f, .25f,   .2f, .25f, -.2f, 0f,   -.2f, 0f, .2f, -.5f },
    };

    /// Runas en relieve en la cara de fuera de la banda, cada 30°, solo las que caen enteras en
    /// la pieza (así ninguna queda partida entre el arco y el trozo), y dos filos a lo largo de
    /// los cantos de arriba y abajo: con ellos el brillo dibuja el aro entero y se lee de lejos.
    static Mesh Runas(float radio, float desde, float hasta)
    {
        float a = radio * Grosor, b = radio * Alto;
        float alto = b * 1.25f, trazo = alto * 0.2f, relieve = a * 0.6f;
        float superficie = radio + a;
        float mediaAnchura = Mathf.Rad2Deg * (alto * 0.35f) / superficie;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        int k = 0;
        for (float ang = 8.5f - 360f; ang < 360f; ang += 30f, k++)
        {
            if (ang - mediaAnchura < desde || ang + mediaAnchura > hasta) continue;
            float rad = ang * Mathf.Deg2Rad;
            var fuera = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            var tang = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            var glifo = Glifos[((k % Glifos.Length) + Glifos.Length) % Glifos.Length];
            Vector3 origen = fuera * (superficie - relieve * 0.3f);
            for (int s = 0; s + 3 < glifo.Length; s += 4)
            {
                var p0 = new Vector2(glifo[s], glifo[s + 1]) * alto;
                var p1 = new Vector2(glifo[s + 2], glifo[s + 3]) * alto;
                Caja(v, n, t, origen, tang, Vector3.up, fuera, p0, p1, trazo, relieve);
            }
        }
        // Filos de los cantos, en tramos cortos que siguen la curva.
        const float Paso = 6f;
        float filo = b * 0.14f, yFilo = b * 0.86f;
        for (float ang = desde; ang < hasta - 0.01f; ang += Paso)
        {
            float tramo = Mathf.Min(Paso, hasta - ang);
            float rad = (ang + tramo * 0.5f) * Mathf.Deg2Rad;
            var fuera = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            var tang = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            float media = superficie * tramo * Mathf.Deg2Rad * 0.5f;
            Vector3 origen = fuera * (superficie - relieve * 0.3f);
            foreach (float y in new[] { yFilo, -yFilo })
                Caja(v, n, t, origen, tang, Vector3.up, fuera, new Vector2(-media, y), new Vector2(media, y), filo, relieve * 0.7f);
        }

        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    /// Un trazo: caja entre p0 y p1 (en el plano tangente-arriba), de anchura 'w' y que sale 'd' hacia fuera.
    static void Caja(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 o, Vector3 x, Vector3 y, Vector3 z,
                     Vector2 p0, Vector2 p1, float w, float d)
    {
        Vector2 dir = p1 - p0;
        float largo = dir.magnitude + w;
        dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector2.up;
        Vector2 mid = (p0 + p1) * 0.5f;
        Vector3 c = o + x * mid.x + y * mid.y + z * (d * 0.5f);
        Vector3 ex = (x * dir.x + y * dir.y) * (largo * 0.5f);
        Vector3 ey = (x * -dir.y + y * dir.x) * (w * 0.5f);
        Vector3 ez = z * (d * 0.5f);
        Cara(v, n, t, c + ez, ex, ey, z);
        Cara(v, n, t, c - ez, ey, ex, -z);
        Cara(v, n, t, c + ex, ey, ez, ex.normalized);
        Cara(v, n, t, c - ex, ez, ey, -ex.normalized);
        Cara(v, n, t, c + ey, ez, ex, ey.normalized);
        Cara(v, n, t, c - ey, ex, ez, -ey.normalized);
    }

    static void Cara(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 c, Vector3 u, Vector3 w, Vector3 normal)
    {
        int i = v.Count;
        v.Add(c - u - w); v.Add(c + u - w); v.Add(c + u + w); v.Add(c - u + w);
        for (int k = 0; k < 4; k++) n.Add(normal);
        // Orden según la normal, para que la cara mire hacia fuera.
        if (Vector3.Dot(Vector3.Cross(u, w), normal) > 0f) t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        else t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
    }

    // ── Assets ──

    static Mesh Guardar(string nombre, Mesh nueva)
    {
        string ruta = $"{Carpeta}/{nombre}.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, ruta); }
        m.Clear();
        m.name = nombre;
        m.SetVertices(nueva.vertices); m.SetNormals(nueva.normals);
        m.SetTriangles(nueva.triangles, 0); m.RecalculateBounds();
        Object.DestroyImmediate(nueva);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MaterialDeRunas()
    {
        string ruta = $"{Carpeta}/Runas del aro del cuello.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, ruta); }
        m.shader = shader;
        m.SetColor("_BaseColor", RunasApagadas);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name == nombre) return t;
        return null;
    }
}
#endif
