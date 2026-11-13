#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using F = VestidoDeFachadas;

/// <summary>
/// Viste el pueblo del prólogo (Prologo_Valle): puertas y ventanas del Fantasy Kingdom Pack en las
/// casas (que vienen sin ellas), contraventanas que se abren, jardineras, macetas junto a las
/// puertas y el cartel de la panadería. Todo cuelga de
/// «Vestido del pueblo (generado)» y se rehace entero en cada ejecución; se retoca en las tablas
/// de abajo, no a mano en la escena. Las piezas no llevan colisionadores (no tocan el NavMesh).
/// </summary>
public static partial class VestidoDelPrologo
{
    const string Escena = "Prologo_Valle";
    const string RutaEscena = "Assets/Scenes/Worlds/Prologo_Valle.unity";
    const string Raiz = "Vestido del pueblo (generado)";
    const string Modulo = "ISLA_POSTGAME_MODULO";
    /// <summary>Grupo de las contraventanas y puertas que se mueven en el guion.</summary>
    public const string Grupo = "Pueblo del prólogo";
    /// Grupo de las puertas que se abren (aparte: la tormenta cierra las contraventanas, no las puertas).
    public const string GrupoPuertas = "Pueblo del prólogo · puertas";
    const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    static readonly Vector3 Plaza = new(6001.5f, 100f, 6000.5f);

    // ── Tablas ──────────────────────────────────────────────────────────────────────────────

    sealed class Casa
    {
        public string nombre;
        public string puerta;          // prefab de la puerta (vacío: sin puerta)
        public string[] ventanas;      // prefabs que se reparten por las ventanas de la casa
        public bool jardineras = true; // jardinera bajo las ventanas que no traen alféizar
        public int caras = 4;          // fachadas con ventanas: 1 = la de la plaza; 4 = todas
        public bool contraventanas = true;
        public string colorContraventanas = "Verde";   // ver ColoresDeContraventana
        public bool puertaQueSeAbre;   // PuertaDeCasa (Blender) en vez de la del pack: alguien entra o sale
        public string mirador;         // quien se asoma a un Mirador (Blender) en la fachada de la plaza; vacío = sin mirador
        public bool cartel;            // cartel colgado de la tienda (soporte, tabla e icono del pack)
    }

    static string W(string n) => Pack + "Main Structures/Window/" + n + ".prefab";
    static string D(string n) => Pack + "Main Structures/Door/" + n + ".prefab";

    static readonly Casa[] Casas =
    {
        new Casa { nombre = "Casa_Norte_02", puerta = D("Door04"), ventanas = new[] { W("Window01_a01"), W("Window03_c02") }, mirador = "Vecino", colorContraventanas = "Azul" },
        new Casa { nombre = "Casa_Norte_03", puerta = D("Door15"), ventanas = new[] { W("Window02_a01"), W("Window05_b03") }, colorContraventanas = "Rojo" },
        new Casa { nombre = "Casa_Norte_04", puerta = D("Door06"), ventanas = new[] { W("Window10_a01"), W("Window04_d01") }, mirador = "Abuela", colorContraventanas = "Verde" },
        new Casa { nombre = "Casa_Sur_01",   puerta = D("Door13"), ventanas = new[] { W("Window12_a01"), W("Window06_a03") }, puertaQueSeAbre = true, colorContraventanas = "Azul" },
        new Casa { nombre = "Casa_Sur_02",   puerta = D("Door05"), ventanas = new[] { W("Window01_a01"), W("Window03_b04") } },
        new Casa { nombre = "Casa_Sur_03",   puerta = D("Door16_c02"), ventanas = new[] { W("Window11_a01"), W("Window05_d02") }, mirador = "Vecina", colorContraventanas = "Ocre" },
        new Casa { nombre = "Casa_Sur_04",   puerta = D("Door02"), ventanas = new[] { W("Window13_a01"), W("Window04_c05") } },
        new Casa { nombre = "Panaderia",     puerta = D("Door14"), ventanas = new[] { W("Window07_a01"), W("Window06_b02") }, cartel = true, colorContraventanas = "Rojo" },
        new Casa { nombre = "Iglesia",       puerta = D("Door16_a01"), ventanas = new[] { W("Window08_a01") }, jardineras = false, contraventanas = false },
    };

    /// Ventanas que ya traen alféizar o tejadillo propio: sin jardinera ni contraventanas.
    static bool TraeAlfeizar(string prefab) => prefab.Contains("Window03") || prefab.Contains("Window04")
        || prefab.Contains("Window05") || prefab.Contains("Window06") || prefab.Contains("Window08");

    /// Alturas (sobre la base de la casa) a las que se prueban filas de ventanas: planta baja,
    /// primera planta y el hastial. Cada fila solo se pone donde hay pared plana.
    static readonly float[] Filas = { 1.3f, 3.3f, 4.6f };
    const float Hueco = 1.1f;              // separación mínima entre piezas de una fachada
    static readonly string Jardinera = Pack + "Vegetation/Flowerpot/Flowerpot02_a01.prefab";
    static readonly string[] Macetas =
    {
        Pack + "Vegetation/Flowerpot/Flowerpot03_a01.prefab", Pack + "Vegetation/Flowerpot/Flowerpot03_c01.prefab",
        Pack + "Vegetation/Flowerpot/Flowerpot03_d01.prefab", Pack + "Vegetation/Flowerpot/Flowerpot03_g01.prefab",
        Pack + "Vegetation/Flowerpot/Flowerpot01_b01.prefab", Pack + "Vegetation/Flowerpot/Flowerpot01_e01.prefab",
    };
    /// Piezas hechas en Blender (Tools/Modelos/Pueblo/pueblo.py), con el pivote en la pared o en la bisagra.
    const string CarpetaPueblo = "Assets/Art/World/Pueblo";
    /// Una hoja de 1×1 m con la bisagra en x=0.
    const string Contraventana = CarpetaPueblo + "/Contraventana.fbx";
    const string Mirador = CarpetaPueblo + "/Mirador.fbx";
    const float EscalonDelMirador = 0.3f;
    /// Distancia a la pared de quien se asoma al mirador (el antepecho está a 0,55 m por dentro).
    const float FondoDelQueSeAsoma = 0.16f;
    const string PuertaDeCasa = CarpetaPueblo + "/PuertaDeCasa.fbx";
    /// Estilo de los materiales lisos de esas piezas: el del pack, para que se iluminen igual que las casas.
    const string Estilo = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials/FK01.mat";
    static readonly (string material, Color color)[] ColoresDePiezas =
    {
        ("Madera", new Color(0.24f, 0.52f, 0.36f)), ("Hierro", new Color(0.17f, 0.15f, 0.14f)), ("Marco", new Color(0.55f, 0.33f, 0.16f)),
        ("Yeso", new Color(0.95f, 0.82f, 0.55f)), ("Teja", new Color(0.18f, 0.48f, 0.58f)), ("Fondo", new Color(0.09f, 0.06f, 0.05f)),
        ("Tierra", new Color(0.35f, 0.22f, 0.12f)), ("Hojas", new Color(0.32f, 0.64f, 0.26f)), ("Flor0", new Color(0.95f, 0.38f, 0.48f)),
        ("Flor1", new Color(0.98f, 0.86f, 0.32f)), ("Flor2", new Color(0.97f, 0.97f, 0.97f)), ("Flor3", new Color(0.7f, 0.42f, 0.9f)),
        ("Puerta", new Color(0.5f, 0.29f, 0.15f)), ("Piedra", new Color(0.64f, 0.63f, 0.6f)),
    };
    /// Colores pintados de las contraventanas (uno por casa).
    static readonly (string nombre, Color color)[] ColoresDeContraventana =
    {
        ("Verde", new Color(0.24f, 0.52f, 0.36f)), ("Azul", new Color(0.22f, 0.45f, 0.66f)),
        ("Rojo", new Color(0.66f, 0.24f, 0.2f)), ("Ocre", new Color(0.8f, 0.58f, 0.22f)),
    };
    /// El cartel de la panadería (la tienda del horno): tabla e icono del pan (del pack).
    static readonly string[] Cartel = { Pack + "Props/Sign/Shop_sign01_a03.prefab", Pack + "Props/Sign/Sign12_a01.prefab" };
    const float CartelEscala = 0.55f;

    /// Marcas para el guion (puntos con nombre «M_…» que lee el horneado).
    /// Marca de quien se asoma a un mirador (M_Mirador_Vecino, M_Mirador_Abuela…); las hojas del mirador
    /// se llaman «Mirador · quien» (HojasConBisagraBeat nombre=…).
    public static string MarcaMirador(string quien) => "M_Mirador_" + quien;
    public static string NombreDelMirador(string quien) => "Mirador · " + quien;
    public const string MarcaPuertaDentro = "M_Puerta_Madre_Dentro";
    public const string MarcaPuertaFuera = "M_Puerta_Madre_Fuera";

    /// Vecinos de fondo del guion: dónde salen (NpcSpawnPoint) y hacia dónde miran.
    /// Con mirador: sale en la marca de ese mirador (la pone el vestido).
    static readonly (string spawnId, Vector3 pos, float rumbo, string mirador)[] Sitios =
    {
        ("SPAWN_PanaderaDelValle", new Vector3(5986.8f, 100f, 5996.4f), 0f, null),
        ("SPAWN_MadreDelValle", new Vector3(5983.0f, 100f, 6000.8f), 90f, null),
        ("SPAWN_LucasDelValle", new Vector3(5983.0f, 100f, 6000.2f), 90f, null),
        ("SPAWN_VecinoDelMirador", Vector3.zero, 0f, "Vecino"),
        ("SPAWN_AbuelaDelMirador", Vector3.zero, 0f, "Abuela"),
        ("SPAWN_VecinaDelMirador", Vector3.zero, 0f, "Vecina"),
    };

    // ── Menús ───────────────────────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Escenario/Prólogo: vestir el pueblo (puertas, ventanas, tablón, campo)")]
    public static void Vestir()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Vestido del prólogo", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder($"Vestido del pueblo del prólogo · {System.DateTime.Now:yyyy-MM-dd HH:mm}\n\n");
        bool abierta = Abrir(out var escena);
        if (!escena.IsValid()) { OrdenesDelEditor.Avisar("Vestido del prólogo", "No encuentro " + RutaEscena); return; }
        try
        {
            Quitar(escena);
            F.OlvidarMedidas();
            var raiz = new GameObject(Raiz);
            SceneManager.MoveGameObjectToScene(raiz, escena);
            PrepararPiezasDeBlender(informe);
            int piezas = 0;
            foreach (var c in Casas) piezas += VestirCasa(escena, raiz.transform, c, informe);
            PonerSitios(raiz.transform, informe);
            piezas += PonerPuestoDelPan(raiz.transform, informe);
            foreach (var go in raiz.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(go.gameObject, 0);
            piezas += PonerTablon(raiz.transform, informe);
            piezas += PonerCampo(raiz.transform, informe);
            piezas += PintarSuelo(escena, raiz.transform, informe);
            informe.Insert(0, $"{piezas} piezas.\n");
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }
        finally { if (abierta) EditorSceneManager.CloseScene(escena, true); }
        Escribir(informe);
        OrdenesDelEditor.Avisar("Vestido del prólogo", informe.ToString().Split('\n')[0]);
    }

    [MenuItem("El Sendero/Escenario/Prólogo: quitar el vestido del pueblo")]
    public static void QuitarMenu()
    {
        if (Application.isPlaying) return;
        bool abierta = Abrir(out var escena);
        try { Quitar(escena); EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena); }
        finally { if (abierta) EditorSceneManager.CloseScene(escena, true); }
    }

    /// Fotos del pueblo para revisar el vestido sin hornear: abre la escena (aditiva) si hace falta y
    /// hace las fotos de «Guiones/Prologo_Valle/escenario/fotos.txt» (una por línea:
    /// «nombre x y z mira_x mira_y mira_z [lente]») en «Guiones/_fotos/».
    [MenuItem("El Sendero/Escenario/Prólogo: fotos del pueblo")]
    public static void Fotos()
    {
        if (Application.isPlaying) return;
        string lista = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, Escena, "escenario", "fotos.txt");
        if (!File.Exists(lista)) { OrdenesDelEditor.Avisar("Fotos del pueblo", "Falta " + lista); return; }
        bool abierta = Abrir(out var escena);
        int n = 0;
        try
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var l in File.ReadAllLines(lista))
            {
                var p = l.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 7 || p[0].StartsWith("#")) continue;
                float F(int i) => float.Parse(p[i], ci);
                if (CapturaDeEscenario.FotoDesde(p[0], new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), p.Length > 7 ? F(7) : 50f) != null) n++;
            }
        }
        finally { if (abierta) EditorSceneManager.CloseScene(escena, true); }
        OrdenesDelEditor.Avisar("Fotos del pueblo", $"{n} fotos.");
    }

    /// Abre la escena aditiva si no lo estaba (devuelve true si la ha abierto aquí).
    static bool Abrir(out Scene escena)
    {
        escena = SceneManager.GetSceneByName(Escena);
        if (escena.IsValid() && escena.isLoaded) return false;
        escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        return true;
    }

    static void Quitar(Scene escena)
    {
        DevolverSuelo(escena);
        foreach (var r in escena.GetRootGameObjects())
            if (r.name == Raiz) Object.DestroyImmediate(r);
    }

    static Transform Buscar(Scene escena, string nombre)
    {
        foreach (var r in escena.GetRootGameObjects())
        {
            if (r.name != Modulo) continue;
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
        }
        return null;
    }

    // ── Una casa ────────────────────────────────────────────────────────────────────────────

    static int VestirCasa(Scene escena, Transform raiz, Casa c, StringBuilder informe)
    {
        var casa = Buscar(escena, c.nombre);
        if (casa == null) { informe.AppendLine($"{c.nombre}: no está en la escena."); return 0; }
        var e = F.Medir(casa);
        if (e.tris.Count == 0) { informe.AppendLine($"{c.nombre}: sin mallas."); return 0; }
        var grupo = new GameObject(c.nombre).transform;
        grupo.SetParent(raiz, false);
        var fachadas = F.Fachadas(e, Plaza);
        var rnd = new System.Random(c.nombre.GetHashCode() & 0x7fffffff);
        int n = 0;
        var ocupado = new Dictionary<string, List<(float u0, float u1, float y0, float y1)>>();

        // El mirador va antes que la puerta: necesita más sitio, y la puerta se aparta.
        float uPuerta = float.NaN;
        if (!string.IsNullOrEmpty(c.mirador)) n += PonerMirador(e, casa, grupo, fachadas[0], c.mirador, ocupado, informe);

        // Puerta en la fachada de la plaza, lo más centrada posible.
        if (!string.IsNullOrEmpty(c.puerta))
        {
            var pz = c.puertaQueSeAbre ? F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(PuertaDeCasa), true)
                                       : F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(c.puerta));
            if (pz != null)
            for (int fp = 0; fp < 3 && float.IsNaN(uPuerta); fp++)
            {
                var f = fachadas[fp];
                foreach (float u in Desplazamientos(1.6f))
                {
                    if (Mathf.Abs(u) + pz.Ancho * 0.5f > f.ancho * 0.5f - 0.3f) continue;
                    if (Choca(ocupado, f.nombre, u, pz.Ancho - Hueco * 0.6f, 0f, pz.Alto)) continue;
                    float suelo = Suelo(f.centro + f.lateral * u + f.fuera * 0.6f, e.BaseY);
                    float h = Mathf.Max(0f, suelo - e.BaseY);
                    if (!F.ParedPlana(e, f, u, h + 0.25f, pz.Ancho, pz.Alto - 0.3f, 0.3f, out var enPared)) continue;
                    if (!FrenteLibre(casa, f, u, h, pz.Ancho, pz.Alto)) continue;
                    enPared.y = e.BaseY + h;
                    var go = F.Poner(pz, f, enPared, grupo, c.puertaQueSeAbre ? "Puerta de la madre" : "Puerta", c.puertaQueSeAbre ? 0.02f : 0.04f);
                    F.SinColisiones(go);
                    if (c.puertaQueSeAbre)
                    {
                        Bisagras(go, f, false, 100f, GrupoPuertas);
                        Pintar(go, null);
                        Marca(grupo, MarcaPuertaDentro, enPared + f.fuera * 0.12f, -f.fuera);
                        Marca(grupo, MarcaPuertaFuera, enPared + f.fuera * 1.1f, f.fuera);
                    }
                    Ocupar(ocupado, f.nombre, u, pz.Ancho, h, pz.Alto);
                    uPuerta = u; n++;
                    // Macetas a los lados de la puerta.
                    n += Macetas2(casa, grupo, f, u, pz.Ancho, e.BaseY + h, rnd);
                    break;
                }
            }
            if (float.IsNaN(uPuerta)) informe.AppendLine($"{c.nombre}: no hay pared plana y libre para la puerta.");
        }

        if (c.cartel) n += PonerCartel(e, casa, grupo, fachadas, ocupado, informe);

        // Ventanas.
        int k = 0;
        for (int fi = 0; fi < Mathf.Min(c.caras, fachadas.Count - 1); fi++)
        {
            var f = fachadas[fi];
            foreach (float alt in Filas)
            {
                float util = f.ancho - 0.8f;
                int huecos = Mathf.Max(1, Mathf.FloorToInt(util / (fi == 3 ? 2.6f : 1.9f)));
                for (int s = 0; s < huecos; s++)
                {
                    string ruta = c.ventanas[(k + s) % c.ventanas.Length];
                    var pz = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(ruta));
                    if (pz == null) continue;
                    float u0 = -util * 0.5f + (s + 0.5f) * util / huecos;
                    foreach (float du in Desplazamientos(0.6f))
                    {
                        float u = u0 + du;
                        if (Mathf.Abs(u) + pz.Ancho * 0.5f > f.ancho * 0.5f - 0.25f) continue;
                        if (Choca(ocupado, f.nombre, u, pz.Ancho, alt, pz.Alto)) continue;
                        if (!F.ParedPlana(e, f, u, alt, pz.Ancho, pz.Alto, 0.08f, out var enPared)) continue;
                        if (!FrenteLibre(casa, f, u, alt, pz.Ancho, pz.Alto)) continue;
                        var go = F.Poner(pz, f, enPared, grupo, $"Ventana {f.nombre} {alt:0.#} {s}", 0.03f);
                        F.SinColisiones(go);
                        Ocupar(ocupado, f.nombre, u, pz.Ancho, alt, pz.Alto);
                        n++;
                        if (!TraeAlfeizar(ruta))
                        {
                            if (c.jardineras && alt < 2f) n += JardineraBajo(grupo, go, f, enPared, pz);
                            if (c.contraventanas) n += Contraventanas(grupo, go, f, enPared, pz, c.colorContraventanas);
                        }
                        break;
                    }
                }
                k++;
            }
        }
        informe.AppendLine($"{c.nombre}: {n} piezas (puerta {(float.IsNaN(uPuerta) ? "no" : "sí")}, fachada de la plaza {fachadas[0].nombre}, {fachadas[0].ancho:0.0} m).");
        return n;
    }

    static IEnumerable<float> Desplazamientos(float max)
    {
        yield return 0f;
        for (float d = 0.1f; d <= max + 1e-3f; d += 0.1f) { yield return d; yield return -d; }
    }

    static void Ocupar(Dictionary<string, List<(float, float, float, float)>> o, string f, float u, float ancho, float y, float alto)
    {
        if (!o.TryGetValue(f, out var l)) o[f] = l = new List<(float, float, float, float)>();
        l.Add((u - ancho * 0.5f, u + ancho * 0.5f, y, y + alto));
    }

    static bool Choca(Dictionary<string, List<(float u0, float u1, float y0, float y1)>> o, string f, float u, float ancho, float y, float alto)
    {
        if (!o.TryGetValue(f, out var l)) return false;
        float a0 = u - ancho * 0.5f - Hueco * 0.5f, a1 = u + ancho * 0.5f + Hueco * 0.5f;
        foreach (var r in l)
            if (a0 < r.u1 && a1 > r.u0 && y < r.y1 + 0.3f && y + alto > r.y0 - 0.3f) return true;
        return false;
    }

    /// Altura del suelo (colisionadores que no son casas) bajo un punto.
    static float Suelo(Vector3 p, float porDefecto)
    {
        var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MinValue;
        foreach (var h in hits)
            if (h.collider.bounds.size.x > 30f || h.collider.GetComponent<Terrain>() != null || h.collider.name.StartsWith("Suelo"))
                mejor = Mathf.Max(mejor, h.point.y);
        return mejor > float.MinValue ? mejor : porDefecto;
    }

    /// Delante de la pieza no hay nada de la escena (carros, barriles, otra casa) que la tape.
    static bool FrenteLibre(Transform casa, F.Fachada f, float u, float y, float ancho, float alto)
    {
        Vector3 c = f.centro + f.lateral * u + Vector3.up * (y + alto * 0.5f) + f.fuera * 0.55f;
        var cols = Physics.OverlapBox(c, new Vector3(ancho * 0.5f, alto * 0.5f, 0.4f), Quaternion.LookRotation(f.fuera), ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            if (col.transform.IsChildOf(casa)) continue;
            if (col.bounds.size.x > 30f || col.bounds.size.z > 30f) continue;   // el suelo
            if (col.GetComponentInParent<Animator>() != null) continue;         // personajes de la escena
            return false;
        }
        return true;
    }

    // ── Adornos ─────────────────────────────────────────────────────────────────────────────

    static int JardineraBajo(Transform grupo, GameObject ventana, F.Fachada f, Vector3 enPared, F.Pieza ventanaPz)
    {
        var pz = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(Jardinera));
        if (pz == null) return 0;
        float escala = Mathf.Clamp((ventanaPz.Ancho + 0.1f) / pz.Ancho, 0.5f, 1.2f);
        Vector3 p = enPared + Vector3.down * (pz.Alto * escala * 0.55f);
        var go = F.Poner(pz, f, p, grupo, ventana.name + " · jardinera", 0.0f, 0.02f, escala);
        F.SinColisiones(go);
        return 1;
    }

    static int Contraventanas(Transform grupo, GameObject ventana, F.Fachada f, Vector3 enPared, F.Pieza pz, string color)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Contraventana);
        if (fbx == null) return 0;
        float alto = pz.Alto * 0.92f, anchoHoja = pz.Ancho * 0.5f;
        // Delante del marco de la ventana: la hoja cerrada la tapa.
        float delante = pz.Fondo + 0.01f;
        Vector3 basePto = enPared + Vector3.up * (pz.Alto * 0.04f) + f.fuera * (delante - 0.03f);
        var ventanaT = new GameObject(ventana.name + " · contraventanas").transform;
        ventanaT.SetParent(grupo, true);
        int n = 0;
        foreach (int lado in new[] { -1, 1 })
        {
            // Bisagra en el canto exterior; la hoja cerrada va hacia el centro de la ventana.
            Vector3 bisagra = basePto + f.lateral * (lado * pz.Ancho * 0.5f);
            Vector3 haciaDentro = -lado * f.lateral;
            var hoja = (GameObject)PrefabUtility.InstantiatePrefab(fbx, grupo.gameObject.scene);
            hoja.name = lado < 0 ? "Hoja izquierda" : "Hoja derecha";
            hoja.transform.SetParent(ventanaT, true);
            // El modelo: la hoja se extiende por +X y sube por +Y (sus dos caras están pintadas).
            hoja.transform.SetPositionAndRotation(bisagra, Quaternion.LookRotation(Vector3.Cross(haciaDentro, Vector3.up), Vector3.up));
            hoja.transform.localScale = new Vector3(anchoHoja, alto, 1f);
            var h = hoja.AddComponent<HojaConBisagra>();
            h.grupo = Grupo;
            // Se abre hacia fuera y queda casi pegada a la pared: el signo que lleva la hoja hacia «fuera».
            Vector3 prueba = Quaternion.AngleAxis(90f, Vector3.up) * haciaDentro;
            h.anguloAbierta = Vector3.Dot(prueba, f.fuera) > 0f ? 165f : -165f;
            h.abiertaAlEmpezar = true;
            F.SinColisiones(hoja);
            Pintar(hoja, color);
            n++;
        }
        return n;
    }

    // ── Piezas de Blender ───────────────────────────────────────────────────────────────────

    /// Cambia los materiales de los FBX de Blender por lisos con el estilo del pack (una vez).
    static void PrepararPiezasDeBlender(StringBuilder informe)
    {
        foreach (var fbx in new[] { Contraventana, Mirador, PuertaDeCasa })
            PiezaDeBlender.PrepararMateriales(fbx, CarpetaPueblo + "/Materiales", "Mat_Pueblo_", ColoresDePiezas, informe, Estilo);
    }

    /// Pinta la madera de una pieza (contraventanas) del color dado; null = el de serie.
    static void Pintar(GameObject go, string color)
    {
        if (string.IsNullOrEmpty(color)) return;
        var estilo = AssetDatabase.LoadAssetAtPath<Material>(Estilo);
        Color c = ColoresDeContraventana[0].color;
        foreach (var (n, col) in ColoresDeContraventana) if (n == color) c = col;
        var mat = PersonajeHechoEnBlender.Liso(estilo, $"{CarpetaPueblo}/Materiales/Mat_Pueblo_Madera_{color}.mat", c);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) if (ms[i] != null && ms[i].name == "Mat_Pueblo_Madera") ms[i] = mat;
            r.sharedMaterials = ms;
        }
    }

    /// Las hojas («Hoja…») de una pieza de Blender giran sobre su bisagra hacia fuera.
    static void Bisagras(GameObject pieza, F.Fachada f, bool abiertas, float angulo, string grupo = Grupo)
    {
        foreach (var t in pieza.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Hoja")) continue;
            var r = t.GetComponent<Renderer>();
            if (r == null) continue;
            Vector3 haciaDentro = r.bounds.center - t.position; haciaDentro.y = 0f;
            var h = t.gameObject.AddComponent<HojaConBisagra>();
            h.grupo = grupo;
            h.abiertaAlEmpezar = abiertas;
            Vector3 prueba = Quaternion.AngleAxis(90f, t.up) * haciaDentro;
            h.anguloAbierta = Vector3.Dot(prueba, f.fuera) > 0f ? angulo : -angulo;
        }
    }

    static void Marca(Transform padre, string nombre, Vector3 pos, Vector3 mira)
    {
        var m = new GameObject(nombre).transform;
        m.SetParent(padre, true);
        m.SetPositionAndRotation(pos, Quaternion.LookRotation(mira, Vector3.up));
    }

    /// Un mirador en la fachada de la plaza, lejos de la puerta: alguien se asoma a él.
    static int PonerMirador(F.Edificio e, Transform casa, Transform grupo, F.Fachada f, string quien,
        Dictionary<string, List<(float u0, float u1, float y0, float y1)>> ocupado, StringBuilder informe)
    {
        var pz = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(Mirador), true);
        if (pz == null) { informe.AppendLine("Mirador: falta el FBX."); return 0; }
        var candidatos = new List<float>();
        float borde = f.ancho * 0.5f - pz.Ancho * 0.5f - 0.25f;
        for (float du = 0f; du <= borde; du += 0.1f) { candidatos.Add(borde - du); candidatos.Add(-(borde - du)); }
        foreach (float u in candidatos)
        {
            if (Mathf.Abs(u) + pz.Ancho * 0.5f > f.ancho * 0.5f - 0.2f) continue;
            if (Choca(ocupado, f.nombre, u, pz.Ancho, 0f, pz.Alto)) continue;
            float suelo = Suelo(f.centro + f.lateral * u + f.fuera * 0.9f, e.BaseY);
            float h = Mathf.Max(0f, suelo - e.BaseY);
            if (!F.ParedPlana(e, f, u, h + 0.3f, pz.Ancho, pz.Alto - 0.4f, 0.3f, out var enPared)) continue;
            if (!FrenteLibre(casa, f, u, h, pz.Ancho, pz.Alto)) continue;
            enPared.y = e.BaseY + h;
            var go = F.Poner(pz, f, enPared, grupo, NombreDelMirador(quien), 0.03f);
            F.SinColisiones(go);
            Bisagras(go, f, false, 165f);
            Pintar(go, null);
            Ocupar(ocupado, f.nombre, u, pz.Ancho, 0f, pz.Alto);
            // Quien se asoma: dentro del mirador, pegado a la pared para que las manos no asomen por el
            // antepecho al hacer gestos (Ver INC-1079), mirando a la plaza, subido a un escalón para
            // que la cara asome por encima de la jardinera (Ver INC-1007).
            Marca(grupo, MarcaMirador(quien), enPared + f.fuera * FondoDelQueSeAsoma + Vector3.up * EscalonDelMirador, f.fuera);
            return 1;
        }
        informe.AppendLine($"{casa.name}: no cabe el mirador.");
        return 0;
    }

    /// El cartel de la panadería: una tabla del pack plana en la pared, sobre el mostrador, con el icono
    /// del pan delante. En la fachada de la plaza (o en la siguiente si no cabe).
    static int PonerCartel(F.Edificio e, Transform casa, Transform grupo, List<F.Fachada> fachadas,
        Dictionary<string, List<(float u0, float u1, float y0, float y1)>> ocupado, StringBuilder informe)
    {
        var tabla = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(Cartel[0]));
        var icono = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(Cartel[1]));
        if (tabla == null || icono == null) { informe.AppendLine("Cartel de la panadería: faltan las piezas."); return 0; }
        foreach (var f in fachadas.GetRange(0, 2))
            foreach (float alt in new[] { 2.5f, 2.8f, 2.2f, 3.1f })
                foreach (float u in Desplazamientos(f.ancho * 0.5f - 0.5f))
                {
                    float k = CartelEscala;
                    if (Choca(ocupado, f.nombre, u, tabla.Ancho * k - Hueco * 0.8f, alt, tabla.Alto * k)) continue;
                    if (!F.ParedPlana(e, f, u, alt, tabla.Ancho * k, tabla.Alto * k, 0.15f, out var enPared)) continue;
                    var go = F.Poner(tabla, f, enPared, grupo, "Cartel de la panadería", 0.02f, 0f, k);
                    F.SinColisiones(go);
                    float centro = (tabla.Alto - icono.Alto) * 0.5f * k;
                    var ic = F.Poner(icono, f, enPared + Vector3.up * centro, grupo, "Cartel de la panadería · pan", 0f, tabla.Fondo * k + 0.005f, k);
                    F.SinColisiones(ic);
                    Ocupar(ocupado, f.nombre, u, tabla.Ancho * k, alt, tabla.Alto * k);
                    return 2;
                }
        informe.AppendLine("Cartel de la panadería: no hay pared.");
        return 0;
    }

    /// El puesto del pan: un mostrador del pack con hogazas encima, en la calle de la entrada oeste,
    /// delante de la casa de la madre. Detrás, la panadera (SPAWN_PanaderaDelValle).
    static readonly Vector3 PuestoDelPan = new(5986.8f, 100f, 5997.4f);
    const float PuestoRumbo = 0f;   // el mostrador da a la calle (al norte)
    static readonly string Mostrador = Pack + "Props/Goods/Counter01_a01.prefab";
    static readonly string[] Panes = { Pack + "Props/Food/Bread01_a01.prefab", Pack + "Props/Food/Bread02_a01.prefab" };

    static int PonerPuestoDelPan(Transform raiz, StringBuilder informe)
    {
        var pz = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(Mostrador));
        if (pz == null) { informe.AppendLine("Puesto del pan: falta el mostrador."); return 0; }
        var grupo = new GameObject("Puesto del pan").transform;
        grupo.SetParent(raiz, false);
        Vector3 suelo = PuestoDelPan; suelo.y = Suelo(PuestoDelPan, 100f);
        var mira = Quaternion.Euler(0f, PuestoRumbo, 0f) * Vector3.forward;
        var f = new F.Fachada { fuera = mira, lateral = -Vector3.Cross(Vector3.up, mira).normalized, centro = suelo, ancho = 3f, fondo = 1f, nombre = "puesto" };
        // Poner apoya la espalda de la pieza en el punto: se retrasa medio fondo para centrarla.
        var go = F.Poner(pz, f, suelo - mira * (pz.Fondo * 0.5f), grupo, "Mostrador", 0f);
        F.SinColisiones(go);
        int n = 1;
        float arriba = suelo.y + pz.Alto;
        for (int i = 0; i < 5; i++)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Panes[i % Panes.Length]);
            if (pf == null) continue;
            var pan = (GameObject)PrefabUtility.InstantiatePrefab(pf, raiz.gameObject.scene);
            pan.name = "Pan " + (i + 1);
            pan.transform.SetParent(grupo, true);
            Vector3 donde = suelo + f.lateral * ((i - 2) * pz.Ancho * 0.17f) + Vector3.up * pz.Alto + mira * ((i % 2) * 0.12f - 0.06f);
            pan.transform.SetPositionAndRotation(donde, Quaternion.Euler(0f, 25f * i, 0f));
            // Apoyar el pan por su base sobre el mostrador.
            var rs = pan.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                float minY = float.MaxValue;
                foreach (var r in rs) minY = Mathf.Min(minY, r.bounds.min.y);
                pan.transform.position += Vector3.up * (arriba - minY);
            }
            F.SinColisiones(pan);
            n++;
        }
        return n;
    }

    /// Los NpcSpawnPoint de los vecinos de fondo del guion.
    static void PonerSitios(Transform raiz, StringBuilder informe)
    {
        var grupo = new GameObject("Sitios de los vecinos de fondo").transform;
        grupo.SetParent(raiz, false);
        foreach (var (id, pos, rumbo, quien) in Sitios)
        {
            var go = new GameObject("SpawnPoint_" + id.Replace("SPAWN_", ""));
            go.transform.SetParent(grupo, false);
            if (!string.IsNullOrEmpty(quien))
            {
                Transform mirador = null;
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) if (t.name == MarcaMirador(quien)) mirador = t;
                if (mirador == null) { informe.AppendLine($"{id}: no hay mirador; sale en la plaza."); go.transform.position = Plaza; }
                else go.transform.SetPositionAndRotation(mirador.position, mirador.rotation);
            }
            else go.transform.SetPositionAndRotation(new Vector3(pos.x, Suelo(pos, pos.y), pos.z), Quaternion.Euler(0f, rumbo, 0f));
            go.AddComponent<NpcSpawnPoint>().spawnId = id;
        }
    }

    static int Macetas2(Transform casa, Transform grupo, F.Fachada f, float u, float anchoPuerta, float y, System.Random rnd)
    {
        int n = 0;
        foreach (int lado in new[] { -1, 1 })
        {
            if (rnd.NextDouble() < 0.35) continue;
            var pz = F.MedirPieza(AssetDatabase.LoadAssetAtPath<GameObject>(Macetas[rnd.Next(Macetas.Length)]));
            if (pz == null) continue;
            float uu = u + lado * (anchoPuerta * 0.5f + 0.25f + pz.Ancho * 0.5f);
            if (Mathf.Abs(uu) > f.ancho * 0.5f - 0.3f) continue;
            if (!FrenteLibre(casa, f, uu, 0.05f, pz.Ancho, 0.6f)) continue;
            // De pie en el suelo, apoyada en la pared (mide la pared a media altura).
            Vector3 suelo = f.centro + f.lateral * uu; suelo.y = Suelo(suelo + f.fuera * 0.4f, y);
            var go = F.Poner(pz, f, suelo + f.fuera * 0.08f, grupo, "Maceta junto a la puerta", 0f);
            F.SinColisiones(go);
            n++;
        }
        return n;
    }

    static void Escribir(StringBuilder informe)
    {
        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, Escena, "escenario");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "vestido_informe.txt"), informe.ToString());
    }
}
#endif
