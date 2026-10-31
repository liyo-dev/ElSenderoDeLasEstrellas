#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// El arte del Sendero (diseño: doc «El Sendero — diseño del arte del nivel», 30 oct 2026). Viste
/// Sendero.unity con un camino de luz suspendido entre estrellas: cielo propio, suelo de luz con
/// constelaciones que se encienden al pisar, cielos cosidos en el primer tramo, las tres puertas
/// repartidas por el camino y el altar que respira al final. Idempotente: todo lo generado cuelga de
/// «Vestido del Sendero (generado)» y se rehace entero; se retoca en las tablas de abajo.
///
/// También coloca lo que el recorrido necesita de este trazado: los disparadores de
/// SenderoProgressionController, sus puntos de rescate, el ancla de llegada, a los tres del grupo y
/// el NavMesh del camino.
public static class VestidoDelSendero
{
    const string RutaEscena = "Assets/Scenes/Worlds/Sendero.unity";
    const string Carpeta = "Assets/Art/World/Sendero";
    const string CarpetaMallas = Carpeta + "/Mallas";
    const string CarpetaVfx = "Assets/_VFX/Sendero";
    const string NombreRaiz = "Vestido del Sendero (generado)";
    const float AnchoCamino = 6f, Grosor = 0.6f;

    // ── El trazado ─────────────────────────────────────────────────────────────────────────
    // Puntos de control del camino (x, altura, z). La llegada está en z 0; el altar, en z 650.
    static readonly Vector3[] Camino =
    {
        new(0, 0, -6), new(0, 0, 10), new(5, 0.5f, 40), new(-4, 1, 80), new(0, 1.5f, 118), new(0, 1.5f, 150),
        new(0, 1.5f, 172), new(8, 2, 205), new(-6, 2.5f, 248), new(0, 3, 285), new(0, 3, 300),
        new(0, 3, 322), new(6, 3.5f, 352), new(-8, 4, 396), new(0, 4.5f, 432), new(0, 4.5f, 450),
        new(0, 4.5f, 490), new(0, 4.5f, 510), new(-6, 5, 548), new(4, 5.5f, 592), new(0, 6, 625), new(0, 6, 650),
    };

    struct Plaza { public string nombre; public Vector3 centro; public float radio; }
    static readonly Plaza[] Plazas =
    {
        new() { nombre = "Llegada", centro = new(0, 0, 0), radio = 9f },
        new() { nombre = "Puerta de Will", centro = new(0, 1.5f, 150), radio = 9f },
        new() { nombre = "Puerta de Estela y descanso", centro = new(0, 3, 300), radio = 11f },
        new() { nombre = "Puerta de Liam", centro = new(0, 4.5f, 450), radio = 9f },
        new() { nombre = "Ruptura", centro = new(0, 4.5f, 490), radio = 13f },
        new() { nombre = "Altar", centro = new(0, 6, 650), radio = 15f },
    };

    struct Puerta { public string nombre; public Vector3 plaza; public Color color; public Color arco; }
    static readonly Puerta[] Puertas =
    {
        new() { nombre = "Will", plaza = new(0, 1.5f, 150), color = new Color(2.4f, 1.6f, 0.45f), arco = new Color(1.0f, 0.62f, 0.15f) },
        new() { nombre = "Estela", plaza = new(0, 3, 300), color = new Color(2.4f, 0.75f, 1.4f), arco = new Color(1.0f, 0.3f, 0.6f) },
        new() { nombre = "Liam", plaza = new(0, 4.5f, 450), color = new Color(0.9f, 1.05f, 1.35f), arco = new Color(0.45f, 0.5f, 0.6f) },
    };

    // Resplandor del horizonte según la puerta que viene (TinteDelSendero).
    static readonly (float desdeZ, Color color, float fuerza)[] Tintes =
    {
        (-100f, new Color(1.0f, 0.70f, 0.28f), 0.3f),
        (165f, new Color(1.0f, 0.42f, 0.70f), 0.3f),
        (315f, new Color(0.60f, 0.70f, 0.95f), 0.25f),
        (500f, new Color(1.0f, 0.97f, 0.9f), 0.35f),
    };

    static readonly Vector3 Altar = new(0, 6, 656);

    [MenuItem("El Sendero/Sendero/2. Vestir el Sendero (arte)")]
    public static void Vestir()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Hace falta el Editor fuera de Play.");
        Carpetas();
        var m = Materiales();
        var vfx = AsegurarVfxDePolvo();

        var escena = SceneManager.GetSceneByPath(RutaEscena);
        bool cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);

        var mundo = Buscar(escena, "SENDERO_WORLD");
        foreach (var viejo in new[] { "COSMIC_ENVIRONMENT_3D", "SENDERO_CAMINO_ASTRAL", "PORTALES_CELESTIALES", "PLAZA_ALTAR_BLANCO", NombreRaiz })
        {
            var t = Buscar(escena, viejo);
            if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        var raiz = new GameObject(NombreRaiz).transform;
        if (mundo != null) raiz.SetParent(mundo, false);
        else SceneManager.MoveGameObjectToScene(raiz.gameObject, escena);

        // El cielo, la luz ambiente y la niebla son de la escena ACTIVA: se activa el Sendero solo
        // para ponerlos, sin tocar la escena que tenga abierta otra persona.
        var activa = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(escena);
        Ambiente(m);
        if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
        var muestras = Muestrear(Camino, 1f);
        Suelo(raiz, muestras, m);
        foreach (var p in Plazas) PlazaDeLuz(raiz, p, m);
        CielosCosidos(raiz, m);
        Polvo(raiz, muestras);
        var luces = new GameObject[Puertas.Length];
        for (int i = 0; i < Puertas.Length; i++) luces[i] = PuertaDeLuz(raiz, Puertas[i], m, i);
        AltarDePiedraBlanca(raiz, m);
        var umbral = Umbral(raiz);

        raiz.gameObject.AddComponent<PisadasEnElSendero>();
        var tinte = raiz.gameObject.AddComponent<TinteDelSendero>();
        foreach (var t in Tintes) tinte.Tramos.Add(new TinteDelSendero.Tramo { desdeZ = t.desdeZ, color = t.color, fuerza = t.fuerza });

        var rescate = Rescates(raiz);
        Recorrido(escena, umbral, luces, rescate, vfx);
        Grupo(escena);
        NavMesh(raiz);

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (cerrar) EditorSceneManager.CloseScene(escena, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[VestidoDelSendero] Sendero vestido: {muestras.Count} m de camino, {Plazas.Length} plazas, {Puertas.Length} puertas.");
    }

    // ── Materiales y carpetas ──────────────────────────────────────────────────────────────

    sealed class Mats
    {
        public Material cielo, suelo, cosidoAmanecer, cosidoNoche, altar, anilloAltar;
        public Material[] puerta, arco;
    }

    static void Carpetas()
    {
        foreach (var c in new[] { Carpeta, CarpetaMallas, CarpetaVfx, Carpeta + "/Texturas" })
        {
            if (AssetDatabase.IsValidFolder(c)) continue;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(c).Replace('\\', '/'), Path.GetFileName(c));
        }
    }

    static Material Mat(string nombre, string shader, Action<Material> ajustar)
    {
        string ruta = $"{Carpeta}/{nombre}.mat";
        var sh = Shader.Find(shader) ?? throw new InvalidOperationException("No encuentro el shader " + shader);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, ruta); }
        mat.shader = sh;
        ajustar?.Invoke(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mats Materiales()
    {
        var m = new Mats
        {
            cielo = Mat("Sendero_Cielo", "Sendero/CieloDelSendero", x =>
            {
                x.SetColor("_ColorFondo", new Color(0.010f, 0.018f, 0.045f));
                x.SetColor("_ColorNebulosa", new Color(0.05f, 0.22f, 0.30f));
                x.SetColor("_ColorNebulosa2", new Color(0.09f, 0.04f, 0.16f));
                x.SetFloat("_FuerzaNebulosa", 0.5f);
                x.SetFloat("_BrilloEstrellas", 2.2f);
                x.SetFloat("_AnchoTinte", 0.05f);
                x.SetFloat("_FuerzaTinte", 0.3f);
            }),
            suelo = Mat("Sendero_SueloDeLuz", "Sendero/SueloDeLuz", x =>
            {
                x.SetColor("_ColorBorde", new Color(2.2f, 1.45f, 0.5f));
                x.SetColor("_ColorPulso", new Color(0.22f, 0.18f, 0.08f));
            }),
            cosidoAmanecer = Mat("Sendero_CieloCosido_Amanecer", "Sendero/CieloCosido", x => { x.SetFloat("_Modo", 0); x.SetFloat("_Opacidad", 0.7f); }),
            cosidoNoche = Mat("Sendero_CieloCosido_NocheDeLunas", "Sendero/CieloCosido", x => { x.SetFloat("_Modo", 1); x.SetFloat("_Opacidad", 0.85f); }),
            anilloAltar = Mat("Sendero_AnilloDelAltar", "Sendero/LuzSimple", x => { x.SetColor("_Color", new Color(1.6f, 1.4f, 1.0f, 1)); x.SetFloat("_Pulso", 0.25f); }),
        };
        // Piedra blanca con un brillo propio muy suave: lo único sólido y cálido del Sendero.
        m.altar = Mat("Sendero_PiedraBlanca", "Universal Render Pipeline/Lit", x =>
        {
            x.SetColor("_BaseColor", new Color(0.93f, 0.92f, 0.88f));
            x.SetFloat("_Smoothness", 0.25f);
            x.SetColor("_EmissionColor", new Color(0.32f, 0.31f, 0.28f));
            x.EnableKeyword("_EMISSION");
            x.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        });
        m.puerta = new Material[Puertas.Length];
        m.arco = new Material[Puertas.Length];
        for (int i = 0; i < Puertas.Length; i++)
        {
            var p = Puertas[i];
            m.puerta[i] = Mat("Sendero_Puerta_" + p.nombre, "Sendero/PuertaDeLuz", x => x.SetColor("_Color", p.color));
            m.arco[i] = Mat("Sendero_Arco_" + p.nombre, "Sendero/LuzSimple", x => { x.SetColor("_Color", new Color(p.arco.r, p.arco.g, p.arco.b, 1)); x.SetFloat("_Pulso", 0.2f); });
        }
        return m;
    }

    static void Ambiente(Mats m)
    {
        RenderSettings.skybox = m.cielo;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.13f, 0.16f, 0.26f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.02f, 0.035f, 0.08f);
        RenderSettings.fogDensity = 0.0012f;
    }

    // ── Camino y plazas ────────────────────────────────────────────────────────────────────

    struct Muestra { public Vector3 p; public Vector3 lado; public float metros; }

    static List<Muestra> Muestrear(Vector3[] puntos, float paso)
    {
        var densas = new List<Vector3>();
        for (int i = 0; i < puntos.Length - 1; i++)
        {
            var p0 = puntos[Mathf.Max(i - 1, 0)]; var p1 = puntos[i]; var p2 = puntos[i + 1]; var p3 = puntos[Mathf.Min(i + 2, puntos.Length - 1)];
            for (int k = 0; k < 40; k++)
            {
                float t = k / 40f, t2 = t * t, t3 = t2 * t;
                densas.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        densas.Add(puntos[^1]);

        var r = new List<Muestra>();
        float metros = 0f, desdeUltima = paso;
        for (int i = 0; i < densas.Count; i++)
        {
            if (i > 0) { float d = Vector3.Distance(densas[i], densas[i - 1]); metros += d; desdeUltima += d; }
            if (desdeUltima < paso && i < densas.Count - 1) continue;
            desdeUltima = 0f;
            var antes = densas[Mathf.Max(i - 1, 0)]; var despues = densas[Mathf.Min(i + 1, densas.Count - 1)];
            var tangente = despues - antes; tangente.y = 0f;
            var lado = Vector3.Cross(Vector3.up, tangente.normalized).normalized;
            r.Add(new Muestra { p = densas[i], lado = lado, metros = metros });
        }
        return r;
    }

    static void Suelo(Transform raiz, List<Muestra> muestras, Mats m)
    {
        var padre = Hijo(raiz, "Camino");
        const int PorTrozo = 60;
        int trozo = 0;
        for (int i0 = 0; i0 < muestras.Count - 1; i0 += PorTrozo)
        {
            int i1 = Mathf.Min(i0 + PorTrozo, muestras.Count - 1);
            var malla = new Malla();
            for (int i = i0; i < i1; i++)
                if (!DentroDePlaza(muestras[i].p) || !DentroDePlaza(muestras[i + 1].p)) Tramo(malla, muestras[i], muestras[i + 1]);
            Pieza(padre, $"Camino {++trozo:00}", malla, m.suelo, true);
        }
    }

    /// Dentro de una plaza el camino no se dibuja: la plaza ya es el suelo (y así no asoma por encima).
    static bool DentroDePlaza(Vector3 p)
    {
        foreach (var pl in Plazas)
            if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pl.centro.x, pl.centro.z)) < pl.radio - 1f) return true;
        return false;
    }

    static void Tramo(Malla malla, Muestra a, Muestra b)
    {
        float w = AnchoCamino * 0.5f;
        Vector3 aL = a.p - a.lado * w, aR = a.p + a.lado * w, bL = b.p - b.lado * w, bR = b.p + b.lado * w;
        Vector3 abajo = Vector3.down * Grosor;
        malla.Quad(aL, aR, bR, bL, Vector3.up, new Vector2(0, a.metros), new Vector2(1, a.metros), new Vector2(1, b.metros), new Vector2(0, b.metros));
        malla.Quad(aL + abajo, aL, bL, bL + abajo, -a.lado, new Vector2(0, a.metros), new Vector2(0, a.metros), new Vector2(0, b.metros), new Vector2(0, b.metros));
        malla.Quad(aR, aR + abajo, bR + abajo, bR, a.lado, new Vector2(1, a.metros), new Vector2(1, a.metros), new Vector2(1, b.metros), new Vector2(1, b.metros));
        malla.Quad(aR + abajo, aL + abajo, bL + abajo, bR + abajo, Vector3.down, new Vector2(0.5f, a.metros), new Vector2(0.5f, a.metros), new Vector2(0.5f, b.metros), new Vector2(0.5f, b.metros));
    }

    static void PlazaDeLuz(Transform raiz, Plaza p, Mats m)
    {
        var malla = new Malla();
        const int Lados = 64;
        Vector3 c = p.centro + Vector3.up * 0.03f;
        for (int i = 0; i < Lados; i++)
        {
            float a0 = i * Mathf.PI * 2f / Lados, a1 = (i + 1) * Mathf.PI * 2f / Lados;
            Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 r0 = c + d0 * p.radio, r1 = c + d1 * p.radio;
            malla.Tri(c, r1, r0, Vector3.up, new Vector2(0.5f, c.z), new Vector2(0, r1.z), new Vector2(0, r0.z));
            Vector3 abajo = Vector3.down * Grosor;
            malla.Quad(r0, r1, r1 + abajo, r0 + abajo, (d0 + d1).normalized, new Vector2(0, r0.z), new Vector2(0, r1.z), new Vector2(0, r1.z), new Vector2(0, r0.z));
            malla.Tri(c + abajo, r0 + abajo, r1 + abajo, Vector3.down, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        }
        Pieza(Hijo(raiz, "Plazas"), "Plaza — " + p.nombre, malla, m.suelo, true);
    }

    // ── Cielos cosidos, polvo y umbral ─────────────────────────────────────────────────────

    static void CielosCosidos(Transform raiz, Mats m)
    {
        var padre = Hijo(raiz, "Cielos cosidos (primer tramo)");
        // A la izquierda un amanecer, a la derecha una noche llena de lunas (novela XIX).
        Panel(padre, "Amanecer", new Vector3(0, -4, 60), 95f, 145f, 215f, 48f, m.cosidoAmanecer);
        Panel(padre, "Noche de lunas", new Vector3(0, -4, 60), 95f, -35f, 35f, 48f, m.cosidoNoche);
    }

    static void Panel(Transform padre, string nombre, Vector3 centro, float radio, float desdeGrados, float hastaGrados, float alto, Material mat)
    {
        var malla = new Malla();
        const int Partes = 28;
        for (int i = 0; i < Partes; i++)
        {
            float a0 = Mathf.Deg2Rad * Mathf.Lerp(desdeGrados, hastaGrados, i / (float)Partes);
            float a1 = Mathf.Deg2Rad * Mathf.Lerp(desdeGrados, hastaGrados, (i + 1) / (float)Partes);
            Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 b0 = centro + d0 * radio, b1 = centro + d1 * radio, s0 = b0 + Vector3.up * alto, s1 = b1 + Vector3.up * alto;
            float u0 = i / (float)Partes, u1 = (i + 1) / (float)Partes;
            malla.Quad(b0, b1, s1, s0, -(d0 + d1).normalized, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1));
        }
        Pieza(padre, nombre, malla, mat, false);
    }

    static void Polvo(Transform raiz, List<Muestra> muestras)
    {
        var padre = Hijo(raiz, "Polvo de luz");
        for (int i = 0; i < muestras.Count; i += 60)
        {
            var go = new GameObject($"Polvo {i / 60 + 1:00}");
            go.transform.SetParent(padre, false);
            go.transform.position = muestras[i].p + Vector3.down * 2f;
            var polvo = go.AddComponent<PolvoDeLuzDelSendero>();
            polvo.tamano = new Vector3(18f, 8f, 60f);
            polvo.distanciaDeActivacion = 60f;
        }
    }

    static GameObject Umbral(Transform raiz)
    {
        var go = new GameObject("Umbral de la Piedra");
        go.transform.SetParent(raiz, false);
        go.transform.position = new Vector3(0, 0, -4.5f);
        // La misma ventana que en la Piedra, con la Piedra dentro: se cruza hacia atrás (−Z) y al
        // otro lado se aparece saliendo del recinto (hacia el sur).
        VistaDelPortal.Ventana(go.transform, VistaDelPortal.HaciaLaPiedra, Vector3.back, Vector3.back);
        return go;
    }

    // ── Puertas y altar ────────────────────────────────────────────────────────────────────

    const float MedioAncho = 1.35f, AltoRecto = 3.0f;

    static GameObject PuertaDeLuz(Transform raiz, Puerta p, Mats m, int i)
    {
        var go = new GameObject("Puerta de " + p.nombre);
        go.transform.SetParent(Hijo(raiz, "Puertas"), false);
        // Al lado de la plaza, de cara al camino: se ve desde lejos y el camino sigue por delante.
        go.transform.position = p.plaza + new Vector3(5.6f, 0.03f, 0);
        go.transform.rotation = Quaternion.LookRotation(Vector3.left);

        var arco = new Malla();
        Arco(arco, MedioAncho + 0.12f, AltoRecto, 0.14f);
        Pieza(go.transform, "Arco", arco, m.arco[i], false, local: true);

        var luz = new GameObject("Luz");
        luz.transform.SetParent(go.transform, false);
        var superficie = new Malla();
        Hueco(superficie, MedioAncho, AltoRecto);
        var sup = Pieza(luz.transform, "Superficie de " + p.nombre, superficie, m.puerta[i], false, local: true);
        var lgo = new GameObject("Brillo");
        lgo.transform.SetParent(luz.transform, false);
        lgo.transform.localPosition = new Vector3(0, 2.2f, 1.2f);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(Mathf.Clamp01(p.color.r / 2.4f), Mathf.Clamp01(p.color.g / 2.4f), Mathf.Clamp01(p.color.b / 2.4f));
        l.intensity = 3f;
        l.range = 12f;
        l.shadows = LightShadows.None;
        luz.AddComponent<AparecerConFundido>().Configurar(new[] { sup.GetComponent<Renderer>() }, l);
        return luz;
    }

    /// Tubo con forma de arco de medio punto: dos jambas rectas y la media vuelta de arriba.
    static void Arco(Malla malla, float medioAncho, float altoRecto, float radioTubo)
    {
        var linea = new List<Vector3>();
        for (int k = 0; k <= 6; k++) linea.Add(new Vector3(-medioAncho, altoRecto * k / 6f, 0));
        for (int k = 1; k < 24; k++) { float a = Mathf.PI - Mathf.PI * k / 24f; linea.Add(new Vector3(Mathf.Cos(a) * medioAncho, altoRecto + Mathf.Sin(a) * medioAncho, 0)); }
        for (int k = 6; k >= 0; k--) linea.Add(new Vector3(medioAncho, altoRecto * k / 6f, 0));
        const int Lados = 8;
        for (int i = 0; i < linea.Count - 1; i++)
        {
            Vector3 t = (linea[i + 1] - linea[i]).normalized;
            Vector3 n1 = Vector3.Cross(t, Vector3.forward).normalized;
            for (int s = 0; s < Lados; s++)
            {
                float a0 = s * Mathf.PI * 2f / Lados, a1 = (s + 1) * Mathf.PI * 2f / Lados;
                Vector3 o0 = (n1 * Mathf.Cos(a0) + Vector3.forward * Mathf.Sin(a0)) * radioTubo;
                Vector3 o1 = (n1 * Mathf.Cos(a1) + Vector3.forward * Mathf.Sin(a1)) * radioTubo;
                malla.Quad(linea[i] + o0, linea[i] + o1, linea[i + 1] + o1, linea[i + 1] + o0, o0.normalized,
                    Vector2.zero, Vector2.zero, Vector2.one, Vector2.one);
            }
        }
    }

    /// El hueco de la puerta (rectángulo con medio círculo encima), con UV de 0 a 1.
    static void Hueco(Malla malla, float medioAncho, float altoRecto)
    {
        float alto = altoRecto + medioAncho;
        Vector2 Uv(Vector3 v) => new(v.x / (2f * medioAncho) + 0.5f, v.y / alto);
        var borde = new List<Vector3> { new(-medioAncho, 0, 0), new(medioAncho, 0, 0) };
        for (int k = 0; k <= 24; k++) { float a = Mathf.PI * k / 24f; borde.Add(new Vector3(Mathf.Cos(a) * medioAncho, altoRecto + Mathf.Sin(a) * medioAncho, 0)); }
        var c = new Vector3(0, alto * 0.5f, 0);
        borde.Add(borde[0]);
        for (int i = 0; i < borde.Count - 1; i++)
            malla.Tri(c, borde[i], borde[i + 1], Vector3.forward, Uv(c), Uv(borde[i]), Uv(borde[i + 1]));
    }

    static void AltarDePiedraBlanca(Transform raiz, Mats m)
    {
        var go = new GameObject("Altar de piedra blanca");
        go.transform.SetParent(raiz, false);
        go.transform.position = Altar;
        go.transform.rotation = Quaternion.LookRotation(Vector3.back);

        var piedra = new Malla();
        piedra.Caja(new Vector3(0, 0.3f, 0), new Vector3(5.2f, 0.6f, 4.0f));
        piedra.Caja(new Vector3(0, 0.8f, 0), new Vector3(4.4f, 0.4f, 3.2f));
        piedra.Caja(new Vector3(0, 1.75f, 0), new Vector3(3.4f, 1.5f, 2.2f));
        var cuerpo = Pieza(go.transform, "Piedra", piedra, m.altar, false, local: true);
        cuerpo.AddComponent<BoxCollider>().center = new Vector3(0, 1.25f, 0);
        cuerpo.GetComponent<BoxCollider>().size = new Vector3(5.2f, 2.5f, 4.0f);

        var anillo = new Malla();
        for (int i = 0; i < 64; i++)
        {
            float a0 = i * Mathf.PI * 2f / 64, a1 = (i + 1) * Mathf.PI * 2f / 64;
            Vector3 d0 = new(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 y = Vector3.up * 0.05f;
            anillo.Quad(d0 * 4.2f + y, d1 * 4.2f + y, d1 * 4.34f + y, d0 * 4.34f + y, Vector3.up, Vector2.zero, Vector2.zero, Vector2.one, Vector2.one);
        }
        var anilloGo = Pieza(go.transform, "Anillo", anillo, m.anilloAltar, false, local: true);

        var lgo = new GameObject("Luz que respira");
        lgo.transform.SetParent(go.transform, false);
        lgo.transform.localPosition = new Vector3(0, 3.4f, 0);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.96f, 0.88f);
        l.range = 18f;
        l.shadows = LightShadows.None;
        go.AddComponent<RespiracionDeLuz>().Configurar(l, new[] { anilloGo.GetComponent<Renderer>() });
    }

    // ── El recorrido sobre este trazado ────────────────────────────────────────────────────

    static Transform[] Rescates(Transform raiz)
    {
        var padre = Hijo(raiz, "Puntos de rescate");
        var puntos = new[] { new Vector3(0, 0.5f, 4), new Vector3(0, 2f, 162), new Vector3(0, 3.5f, 312), new Vector3(0, 5f, 470), new Vector3(0, 6.5f, 630) };
        var r = new Transform[puntos.Length];
        for (int i = 0; i < puntos.Length; i++)
        {
            var t = new GameObject($"Rescate {i}").transform;
            t.SetParent(padre, false);
            t.position = puntos[i];
            t.rotation = Quaternion.LookRotation(Vector3.forward);
            r[i] = t;
        }
        return r;
    }

    static void Recorrido(Scene escena, GameObject umbral, GameObject[] luces, Transform[] rescate, GameObject vfx)
    {
        var ctrl = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SenderoProgressionController>(true)).FirstOrDefault();
        if (ctrl != null)
        {
            var so = new SerializedObject(ctrl);
            so.FindProperty("_portalOrigenTierra").objectReferenceValue = umbral;
            so.FindProperty("_portalWill").objectReferenceValue = luces[0];
            so.FindProperty("_portalEstela").objectReferenceValue = luces[1];
            so.FindProperty("_portalLiam").objectReferenceValue = luces[2];
            so.FindProperty("_vfxAparicionPrefab").objectReferenceValue = vfx;
            so.FindProperty("_escenaPruebaEstela").stringValue = "Chuchelandia";
            var arr = so.FindProperty("_puntosDeRescate");
            arr.arraySize = rescate.Length;
            for (int i = 0; i < rescate.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = rescate[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Cada puerta se enciende al empezar su tramo y se entra cruzándola.
        Disparador(escena, "Trigger_01_Manifestar_Will", new Vector3(0, 1, 20), new Vector3(18, 6, 4), Quaternion.identity);
        Disparador(escena, "Trigger_02_Manifestar_Estela", new Vector3(0, 2.5f, 166), new Vector3(18, 6, 4), Quaternion.identity);
        Disparador(escena, "Trigger_03_Manifestar_Liam", new Vector3(0, 4, 318), new Vector3(18, 6, 4), Quaternion.identity);
        for (int i = 0; i < Puertas.Length; i++)
        {
            var luz = luces[i].transform.parent;
            string nombre = i switch { 0 => "Trigger_01_Entrar_Will", 1 => "Trigger_02_Entrar_Estela", _ => "Trigger_03_Entrar_Liam" };
            Disparador(escena, nombre, luz.position + Vector3.up * 2.2f, new Vector3(2.4f, 4.4f, 1.2f), luz.rotation);
        }
        Disparador(escena, "Trigger_04_Avanzar_Altar", new Vector3(0, 5.5f, 560), new Vector3(18, 6, 4), Quaternion.identity);
        Disparador(escena, "Trigger_05_Aparicion_MagoOscuro", new Vector3(0, 7, 640), new Vector3(30, 8, 4), Quaternion.identity);

        var ancla = escena.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SpawnAnchor>(true)).FirstOrDefault(a => a.anchorId == UmbralDelSendero.AnclaLlegada);
        if (ancla != null)
        {
            ancla.transform.position = new Vector3(0, 0.6f, 3);
            ancla.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }
    }

    static void Disparador(Scene escena, string nombre, Vector3 pos, Vector3 tam, Quaternion rot)
    {
        var t = Buscar(escena, nombre);
        if (t == null) { Debug.LogWarning("[VestidoDelSendero] Falta el disparador " + nombre); return; }
        t.SetPositionAndRotation(pos, rot);
        t.localScale = Vector3.one;
        var col = t.GetComponent<BoxCollider>() ?? t.gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = Vector3.zero;
        col.size = tam;
        t.gameObject.SetActive(true);
    }

    static void Grupo(Scene escena)
    {
        foreach (var (nombre, pos) in new[] { ("_WILL", new Vector3(0, 0.1f, 3)), ("_ESTELA", new Vector3(1.6f, 0.1f, 1.6f)), ("_LIAM", new Vector3(-1.6f, 0.1f, 1.6f)) })
        {
            var t = escena.GetRootGameObjects().FirstOrDefault(g => g.name == nombre)?.transform;
            if (t == null) continue;
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.forward));
        }
    }

    static void NavMesh(Transform raiz)
    {
        var s = raiz.gameObject.AddComponent<NavMeshSurface>();
        s.collectObjects = CollectObjects.Children;
        s.layerMask = 1 << LayerMask.NameToLayer("Floor");
        s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        s.BuildNavMesh();
        if (s.navMeshData == null) return;
        string ruta = "Assets/Scenes/Worlds/Sendero/NavMesh-Vestido.asset";
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(s.navMeshData, ruta);
        EditorUtility.SetDirty(s);
    }

    // ── El polvo de luz de un solo uso (piezas que se deshacen, puertas que se encienden) ──

    static GameObject AsegurarVfxDePolvo()
    {
        string rutaTex = Carpeta + "/Texturas/PuntoSuave.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex) == null)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32f;
                    float a = Mathf.Clamp01(1f - d); a *= a;
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            File.WriteAllBytes(rutaTex, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(rutaTex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(rutaTex);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        var mat = Mat("Sendero_PolvoDeLuz", "Universal Render Pipeline/Particles/Unlit", x =>
        {
            x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTex));
            x.SetColor("_BaseColor", new Color(2f, 1.7f, 1.1f, 1f));
            x.SetFloat("_Surface", 1);
            x.SetFloat("_Blend", 2);
            x.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            x.SetFloat("_DstBlend", (float)BlendMode.One);
            x.SetFloat("_ZWrite", 0);
            x.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            x.renderQueue = (int)RenderQueue.Transparent;
        });

        string ruta = CarpetaVfx + "/VFX_PolvoDeLuz.prefab";
        var go = new GameObject("VFX_PolvoDeLuz");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.55f), new Color(0.6f, 0.8f, 1f));
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ruta);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    // ── Utilidades ─────────────────────────────────────────────────────────────────────────

    static Transform Buscar(Scene escena, string nombre)
    {
        foreach (var r in escena.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
        return null;
    }

    static Transform Hijo(Transform padre, string nombre)
    {
        var t = padre.Find(nombre);
        if (t != null) return t;
        t = new GameObject(nombre).transform;
        t.SetParent(padre, false);
        return t;
    }

    /// Guarda la malla como asset (para que la escena la referencie) y crea su objeto.
    static GameObject Pieza(Transform padre, string nombre, Malla malla, Material mat, bool suelo, bool local = false)
    {
        string archivo = $"{Limpio(padre.name)}_{Limpio(nombre)}";
        var mesh = malla.Crear(archivo);
        string ruta = $"{CarpetaMallas}/{archivo}.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente != null) { existente.Clear(); EditorUtility.CopySerialized(mesh, existente); mesh = existente; }
        else AssetDatabase.CreateAsset(mesh, ruta);

        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        if (!local) { go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity; }
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        if (suelo)
        {
            go.layer = LayerMask.NameToLayer("Floor");
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            r.receiveShadows = true;
        }
        return go;
    }

    static string Limpio(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

    sealed class Malla
    {
        readonly List<Vector3> v = new(); readonly List<Vector3> n = new(); readonly List<Vector2> uv = new(); readonly List<int> t = new();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); n.Add(normal); n.Add(normal); n.Add(normal); uv.Add(ua); uv.Add(ub); uv.Add(uc);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0) { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            else { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Tri(a, b, c, normal, ua, ub, uc);
            Tri(a, c, d, normal, ua, uc, ud);
        }

        public void Caja(Vector3 centro, Vector3 tam)
        {
            Vector3 h = tam * 0.5f;
            Vector3 P(float x, float y, float z) => centro + new Vector3(x * h.x, y * h.y, z * h.z);
            Vector2 a = Vector2.zero, b = Vector2.right, c = Vector2.one, d = Vector2.up;
            Quad(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), Vector3.up, a, b, c, d);
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1), Vector3.down, a, b, c, d);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), Vector3.back, a, b, c, d);
            Quad(P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), Vector3.forward, a, b, c, d);
            Quad(P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), Vector3.left, a, b, c, d);
            Quad(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.right, a, b, c, d);
        }

        public Mesh Crear(string nombre)
        {
            var m = new Mesh { name = nombre, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
#endif
