using System.Collections.Generic;
using System.Linq;
using Kits.StylizedVfx;
using Kits.StylizedVfx.Editor;
using UnityEditor;
using UnityEngine;
using B = Kits.StylizedVfx.Editor.StylizedVfxBuilder;
using Look = Kits.StylizedVfx.Editor.StylizedVfxAssets.EnergyLook;

/// Los efectos del prólogo con el aspecto «dibujo animado con volumen» (piloto de INC-1136): piezas
/// 3D con sombreado de dibujo, que se cruzan por delante y por detrás, giran en varios ejes y dan luz.
/// Cada mago tiene su color en todo lo que lanza: el Archimago, blanco y oro con borde cian («Luz»);
/// el Mago Oscuro, violeta casi negro con borde magenta («Sombra»). Los prefabs son nuevos (en
/// «Piloto/») y el guion del prólogo los nombra; los de los paquetes siguen como estaban para el
/// resto del juego. El orbe de la plegaria lo hace OrbeDelArchimago. Ver INC-1136
public static class VfxDelPrologo
{
    public const string Carpeta = "Assets/_VFX/Prologo/Piloto";
    private const string CarpetaMateriales = Carpeta + "/Materiales";
    private const string CarpetaMallas = Carpeta + "/Mallas";
    private const string RutaEstrella = "Assets/_VFX/Mallas/VFX_Estrella4.fbx";
    private const string RutaBolita = "Assets/_VFX/Mallas/VFX_Bolita.fbx";

    private sealed class Paleta
    {
        public string Nombre;
        public Color Brillo, Cuerpo, Sombra, Borde, Luz;
        public Material Energia, Pieza, Chispa, Burbuja;
    }

    private static Mesh _estrella, _bolita, _esfera, _aro, _aroDeOrbe, _pincho;

    [MenuItem("El Sendero/Magia/Piloto del prólogo/Todos los efectos del prólogo (INC-1136)")]
    public static void Menu()
    {
        var log = new System.Text.StringBuilder();
        Construir(log);
        OrbeDelArchimago.Construir();
        Debug.Log("[VFX] Efectos del prólogo: " + log);
    }

    public static void Construir(System.Text.StringBuilder log)
    {
        StylizedVfxAssets.EnsureFolder(Carpeta);
        _estrella = AssetDatabase.LoadAllAssetsAtPath(RutaEstrella).OfType<Mesh>().FirstOrDefault();
        _bolita = AssetDatabase.LoadAllAssetsAtPath(RutaBolita).OfType<Mesh>().FirstOrDefault();
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _esfera = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        _aro = StylizedVfxAssets.Torus(CarpetaMallas + "/Aro.asset", 1f, 0.03f, 64, 10);
        _aroDeOrbe = StylizedVfxAssets.Torus(CarpetaMallas + "/AroDeOrbe.asset", 1f, 0.065f, 48, 10);
        _pincho = StylizedVfxAssets.Cone(CarpetaMallas + "/Pincho.asset", 0.18f, 1f, 6);
        if (_estrella == null || _bolita == null) { Debug.LogError("[VFX] Faltan las mallas de estrella o bolita en Assets/_VFX/Mallas."); return; }

        var luz = Paleta_("Luz", new Color(2.2f, 2.1f, 1.8f), new Color(1.5f, 0.9f, 0.18f), new Color(1.0f, 0.45f, 0.1f),
                          new Color(0.35f, 1.2f, 1.7f), new Color(1f, 0.93f, 0.8f));
        var sombra = Paleta_("Sombra", new Color(1.3f, 0.45f, 1.9f), new Color(0.16f, 0.06f, 0.28f), new Color(0.05f, 0.02f, 0.09f),
                             new Color(1.7f, 0.3f, 1.4f), new Color(0.85f, 0.35f, 1f), corteDelBrillo: 0.82f);
        var blanco = Paleta_("Blanco", new Color(3f, 3f, 3f), new Color(2.2f, 2.1f, 2.0f), new Color(1.5f, 1.35f, 1.6f),
                             new Color(1.5f, 1.6f, 2.2f), Color.white);
        var polvo = Paleta_("Polvo", new Color(0.95f, 0.9f, 0.8f), new Color(0.78f, 0.7f, 0.58f), new Color(0.55f, 0.45f, 0.38f),
                            new Color(0.78f, 0.7f, 0.58f), Color.white, sinBorde: true);
        var polvoOscuro = Paleta_("PolvoOscuro", new Color(0.5f, 0.38f, 0.6f), new Color(0.25f, 0.18f, 0.32f), new Color(0.11f, 0.07f, 0.15f),
                                  new Color(0.25f, 0.18f, 0.32f), Color.white, sinBorde: true);

        foreach (var p in new[] { luz, sombra })
        {
            Guardar(p.Nombre + "_Destello", r => Destello(r, p), log);
            Guardar(p.Nombre + "_Proyectil", r => Proyectil(r, p), log);
            Guardar(p.Nombre + "_Impacto", r => Impacto(r, p, p == sombra ? polvoOscuro : null), log);
            Guardar(p.Nombre + "_Escudo", r => Escudo(r, p, p == sombra ? 0.95f : 0.7f, p == sombra ? 2.1f : 1.6f), log);
            Guardar(p.Nombre + "_Circulo", r => Circulo(r, p), log);
            Guardar(p.Nombre + "_CargaEnLasManos", r => CargaEnLasManos(r, p), log);
        }
        Guardar("Choque", r => Choque(r, luz, sombra, blanco, polvoOscuro), log);
        Guardar("Pilar", r => Pilar(r, luz, blanco), log);
        Guardar("Sombra_Grieta", r => Grieta(r, sombra, polvoOscuro), log);
        Guardar("Sombra_Rastro", r => Rastro(r, sombra, polvoOscuro), log);
        Guardar("Forcejeo", r => Forcejeo(r, luz, sombra, blanco), log);
        Guardar("EstallidoFinal", r => EstallidoFinal(r, luz, blanco, polvoOscuro), log);
        Guardar("Sombra_Onda", r => OndaEnElSuelo(r, sombra, polvoOscuro), log);
        Guardar("Sombra_CargaGrande", r => CargaGrande(r, sombra, polvoOscuro), log);
        Guardar("Luz_Remolino", r => Remolino(r, luz), log);
        Guardar("Polvo", r => Polvo(r, polvo), log);
        // Punto invisible al que apuntan los hechizos del choque del duelo.
        Guardar("Marca", r => { }, log);
        AssetDatabase.SaveAssets();
    }

    // ── Paletas ──────────────────────────────────────────────────────────────────────────────

    private static Paleta Paleta_(string nombre, Color brillo, Color cuerpo, Color sombra, Color borde, Color luz, bool sinBorde = false,
                                  float corteDelBrillo = 0.6f)
    {
        var p = new Paleta { Nombre = nombre, Brillo = brillo, Cuerpo = cuerpo, Sombra = sombra, Borde = borde, Luz = luz };
        string m = $"{CarpetaMateriales}/{nombre}_";
        p.Energia = StylizedVfxAssets.EnergyMaterial(m + "Energia.mat",
            new Look(brillo, cuerpo, sombra, borde, coreBand: corteDelBrillo, shadowBand: -0.2f, rimBand: sinBorde ? 1.01f : 0.72f, noiseAmount: 0.12f, noiseScale: 9f));
        p.Pieza = StylizedVfxAssets.EnergyMaterial(m + "Pieza.mat",
            new Look(brillo, cuerpo, sombra, borde, coreBand: 0.75f, shadowBand: -0.3f, rimBand: 1.01f, noiseAmount: 0.03f, noiseScale: 20f));
        p.Chispa = StylizedVfxAssets.EnergyMaterial(m + "Chispa.mat",
            new Look(brillo, sinBorde ? cuerpo : Color.Lerp(cuerpo, brillo, 0.35f), cuerpo, cuerpo, coreBand: 0.25f, shadowBand: -0.6f, rimBand: 1.01f, noiseAmount: 0f));
        p.Burbuja = StylizedVfxAssets.ShellMaterial(m + "Burbuja.mat",
            new Look(brillo, cuerpo, sombra, borde, coreBand: 0.82f, rimBand: 0.75f, noiseAmount: 0.08f, noiseScale: 4f), 0.1f, 0.3f, 0.45f);
        return p;
    }

    // ── Efectos ──────────────────────────────────────────────────────────────────────────────

    /// En la mano al lanzar: una bola que salta y se va, con estrellitas.
    private static void Destello(GameObject root, Paleta p)
    {
        var t = B.Group(root.transform, "Destello");
        B.Animate(t, B.Flash(), 0.35f);
        var bola = B.Sphere(t, "Bola", 0.3f, p.Energia);
        B.Particles(root.transform, "Estrellitas", _estrella, p.Chispa, Estallido(6, 1.5f, 2.5f, 0.06f, 0.1f, 0.3f, 0.45f));
        Pulso(root, new Renderer[] { bola }, B.PointLight(root.transform, p.Luz), null, 3f, 2.5f, 0f, 0f, 0f,
              0f, 0f, B.LightFlash(0.35f));
    }

    /// Lo que viaja: núcleo con anillos que vuelcan y estela de bolitas que se quedan atrás.
    private static void Proyectil(GameObject root, Paleta p)
    {
        var giros = new List<Spin>();
        var rs = new List<Renderer>();
        var nucleo = B.Sphere(root.transform, "Núcleo", 0.26f, p.Energia);
        giros.Add(new Spin(nucleo.transform, new Vector3(0.2f, 1f, 0.4f).normalized, 220f));
        rs.Add(nucleo);
        int anillos = p.Nombre == "Sombra" ? 2 : 1;
        for (int i = 0; i < anillos; i++)
            AnilloQueVuelca(root.transform, ((char)('A' + i)).ToString(), 0.2f + i * 0.04f, 70f - i * 25f, i * 90f, 260f * (i == 0 ? 1 : -1), 420f, p, 0.03f, giros, rs);

        var estela = Ajustes(0.25f, 0.35f, 0f, 0f, 0.12f, 0.18f);
        estela.World = true; estela.Loop = true; estela.RatePerMeter = 14f; estela.MaxParticles = 80;
        estela.Shape = ParticleSystemShapeType.Sphere; estela.Radius = 0.02f;
        estela.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        B.Particles(root.transform, "Estela", _esfera, p.Energia, estela);

        var chispas = Ajustes(0.35f, 0.45f, 0.3f, 0.6f, 0.05f, 0.08f);
        chispas.World = true; chispas.Loop = true; chispas.RatePerMeter = 4f; chispas.Tumble = true;
        chispas.Shape = ParticleSystemShapeType.Sphere; chispas.Radius = 0.1f;
        B.Particles(root.transform, "Chispas", _estrella, p.Chispa, chispas);

        Pulso(root, rs.ToArray(), B.PointLight(root.transform, p.Luz), giros.ToArray(), 2.5f, 2.5f, 0.5f, 0.3f, 0.1f);
    }

    /// Donde da: destello, dos ondas que se abren y estrellitas que salen despedidas.
    private static void Impacto(GameObject root, Paleta p, Paleta polvo)
    {
        var rs = new List<Renderer>();
        var d = B.Group(root.transform, "Destello");
        B.Animate(d, B.Flash(), 0.3f);
        rs.Add(B.Sphere(d, "Bola", 0.7f, p.Energia));

        rs.Add(Onda(root.transform, "Onda", 0.9f, Quaternion.identity, p.Pieza, 0.45f));
        rs.Add(Onda(root.transform, "Onda inclinada", 0.75f, Quaternion.Euler(60f, 30f, 0f), p.Pieza, 0.45f));

        var est = Estallido(8, 3f, 4.5f, 0.08f, 0.13f, 0.35f, 0.55f);
        est.Drag = 3f; est.Gravity = 0.3f;
        B.Particles(root.transform, "Estrellitas", _estrella, p.Chispa, est);
        if (polvo != null)
        {
            var bolas = Estallido(6, 1.5f, 2.2f, 0.2f, 0.3f, 0.45f, 0.6f);
            bolas.Drag = 3f;
            B.Particles(root.transform, "Bolas", _bolita, polvo.Energia, bolas);
        }
        Pulso(root, rs.ToArray(), B.PointLight(root.transform, p.Luz), null, 4f, 3.5f, 0f, 0f, 0f,
              0.35f, 0.25f, B.LightFlash(0.5f));
    }

    /// Burbuja que salta alrededor de quien se cubre (la raíz va a sus pies; el Mago es más alto).
    private static void Escudo(GameObject root, Paleta p, float centro, float diametro)
    {
        var t = B.Group(root.transform, "Burbuja", new Vector3(0f, centro, 0f));
        B.Animate(t, B.PopIn(), 0.22f);
        var burbuja = B.Sphere(t, "Esfera", diametro, p.Burbuja);
        var est = Estallido(6, 1.2f, 2f, 0.06f, 0.1f, 0.3f, 0.45f);
        est.Shape = ParticleSystemShapeType.Sphere; est.Radius = diametro * 0.47f;
        B.Particles(t, "Estrellitas", _estrella, p.Chispa, est);
        Pulso(root, new Renderer[] { burbuja }, B.PointLight(t, p.Luz), null, 1.6f, 2.5f, 0.6f, 0.3f, 0.12f, 0.55f, 0.3f);
    }

    /// Círculo mágico apoyado en el suelo: aro con rombos y estrella de seis puntas que giran al revés.
    private static void Circulo(GameObject root, Paleta p)
    {
        var giros = new List<Spin>();
        var rs = new List<Renderer>();
        var c = B.Group(root.transform, "Círculo", new Vector3(0f, 0.03f, 0f));
        B.Animate(c, B.PopIn(), 0.4f);

        var fuera = B.Group(c, "Aro y rombos");
        giros.Add(new Spin(fuera, Vector3.up, 25f));
        rs.Add(B.Piece(fuera, "Aro exterior", _aro, p.Pieza, Vector3.zero, Quaternion.identity, Vector3.one * 0.9f));
        rs.Add(B.Piece(fuera, "Aro interior", _aro, p.Pieza, Vector3.zero, Quaternion.identity, Vector3.one * 0.78f));
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f;
            var pos = Quaternion.Euler(0f, a, 0f) * new Vector3(0.98f, 0f, 0f);
            rs.Add(B.Box(fuera, "Rombo " + (i + 1), new Vector3(0.06f, 0.02f, 0.06f), p.Pieza, pos, Quaternion.Euler(0f, a + 45f, 0f)));
        }

        var estrella = B.Group(c, "Estrella de seis puntas");
        giros.Add(new Spin(estrella, Vector3.up, -35f));
        for (int tri = 0; tri < 2; tri++)
        {
            for (int k = 0; k < 3; k++)
            {
                Vector3 v0 = Vertice(tri * 60f + k * 120f), v1 = Vertice(tri * 60f + (k + 1) * 120f);
                var dir = v1 - v0;
                rs.Add(B.Box(estrella, $"Trazo {tri * 3 + k + 1}", new Vector3(0.03f, 0.02f, dir.magnitude), p.Pieza,
                             (v0 + v1) * 0.5f, Quaternion.LookRotation(dir.normalized, Vector3.up)));
            }
        }

        var suben = Ajustes(0.9f, 1.3f, 0f, 0f, 0.05f, 0.08f);
        suben.Loop = true; suben.Rate = 6f; suben.Rise = 0.7f; suben.Tumble = true;
        suben.Shape = ParticleSystemShapeType.Circle; suben.Radius = 0.85f;
        B.Particles(c, "Estrellitas que suben", _estrella, p.Chispa, suben);

        // Luz suave: con el orbe y la luna encima, más fuerte blanquea a quien está dentro.
        Pulso(root, rs.ToArray(), B.PointLight(c, p.Luz, new Vector3(0f, 0.35f, 0f)), giros.ToArray(), 0.9f, 2.2f, 1.6f, 0.3f, 0.3f);
    }

    private static Vector3 Vertice(float grados) => Quaternion.Euler(0f, grados + 90f, 0f) * new Vector3(0.74f, 0f, 0f);

    /// Preparación de un hechizo, entre las manos (la pone ConjuroEnLasManosBeat con «entreLasManos»):
    /// una bolita que crece, aros pequeños que se cierran sobre ella y estrellitas que entran. Todo
    /// cabe entre las manos para que no tape la cara.
    private static void CargaEnLasManos(GameObject root, Paleta p)
    {
        var aros = Ajustes(0.45f, 0.45f, 0f, 0f, 0.45f, 0.55f);
        aros.Loop = true; aros.Rate = 5f; aros.Tumble = true; aros.Shape = ParticleSystemShapeType.Sphere; aros.Radius = 0.001f;
        aros.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f, -0.5f, 0f));
        B.Particles(root.transform, "Aros que se cierran", _aroDeOrbe, p.Pieza, aros);

        var entran = Ajustes(0.35f, 0.35f, -1f, -1f, 0.04f, 0.06f);
        entran.Loop = true; entran.Rate = 18f; entran.Tumble = true; entran.Shape = ParticleSystemShapeType.Sphere; entran.Radius = 0.35f;
        B.Particles(root.transform, "Estrellitas que entran", _estrella, p.Chispa, entran);

        var n = B.Group(root.transform, "Núcleo");
        B.Animate(n, B.PopIn(), 0.3f);
        var nucleo = B.Sphere(n, "Bola", 0.13f, p.Energia);
        Pulso(root, new Renderer[] { nucleo }, B.PointLight(root.transform, p.Luz), null, 1.8f, 1.8f, 0.3f, 0.5f, 0.12f);
    }

    /// Lo que corre por el suelo hasta quien recibe la grieta: viaja como un proyectil (SpellBeat) a la
    /// altura de la mano, pero su rastro va pegado al suelo y deja pinchos que asoman y se esconden.
    private static void Rastro(GameObject root, Paleta sombra, Paleta polvo)
    {
        var suelo = B.Group(root.transform, "En el suelo");
        suelo.gameObject.AddComponent<StickToGround>();
        var pinchos = Ajustes(0.45f, 0.6f, 0f, 0f, 0.35f, 0.6f);
        pinchos.World = true; pinchos.Loop = true; pinchos.RatePerMeter = 7f; pinchos.MaxParticles = 120;
        pinchos.Shape = ParticleSystemShapeType.Circle; pinchos.Radius = 0.25f; pinchos.Volume = true;
        pinchos.SizeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 9f), new Keyframe(0.18f, 1f), new Keyframe(0.7f, 0.9f), new Keyframe(1f, 0f));
        B.Particles(suelo, "Pinchos", _pincho, sombra.Energia, pinchos);
        var bolas = Ajustes(0.4f, 0.6f, 0.3f, 0.8f, 0.18f, 0.3f);
        bolas.World = true; bolas.Loop = true; bolas.RatePerMeter = 5f; bolas.Tumble = true; bolas.Rise = 0.5f;
        bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = 0.2f;
        B.Particles(suelo, "Polvo", _bolita, polvo.Energia, bolas);
        Pulso(root, new Renderer[0], B.PointLight(suelo, sombra.Luz, new Vector3(0f, 0.4f, 0f)), null, 2f, 2.5f, 0.2f, 0.4f, 0f);
    }

    /// Los dos orbes empujándose: chispazos de los dos colores, ondas que salen sin parar y una luz
    /// que tiembla cada vez más deprisa.
    private static void Forcejeo(GameObject root, Paleta luz, Paleta sombra, Paleta blanco)
    {
        foreach (var p in new[] { luz, sombra })
        {
            var est = Ajustes(0.35f, 0.55f, 4f, 7f, 0.08f, 0.14f);
            est.Loop = true; est.Rate = 26f; est.Tumble = true; est.Drag = 2f; est.MaxParticles = 60;
            B.Particles(root.transform, "Chispazos " + p.Nombre, _estrella, p.Chispa, est);
        }
        var ondas = Ajustes(0.4f, 0.4f, 0f, 0f, 1.6f, 2.2f);
        ondas.Loop = true; ondas.Rate = 4f; ondas.Tumble = true; ondas.Shape = ParticleSystemShapeType.Sphere; ondas.Radius = 0.001f;
        ondas.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f));
        B.Particles(root.transform, "Ondas", _aro, blanco.Pieza, ondas);
        Pulso(root, new Renderer[0], B.PointLight(root.transform, Color.white), null, 4f, 6f, 0.12f, 0.6f, 0f);
    }

    /// El estallido final, a lo Dragon Slave: destello enorme, una cúpula que se abre por el suelo y
    /// una columna de luz gigante que sube al cielo con aros que suben por ella.
    private static void EstallidoFinal(GameObject root, Paleta luz, Paleta blanco, Paleta polvo)
    {
        var rs = new List<Renderer>();
        var d = B.Group(root.transform, "Destello");
        B.Animate(d, B.Flash(), 0.6f);
        rs.Add(B.Sphere(d, "Bola", 3.2f, blanco.Energia, new Vector3(0f, 1.2f, 0f)));

        var cupula = B.Group(root.transform, "Cúpula");
        B.Animate(cupula, new AnimationCurve(new Keyframe(0f, 0.05f, 0f, 3f), new Keyframe(1f, 1f)), 1.1f);
        rs.Add(B.Sphere(cupula, "Burbuja", 16f, luz.Burbuja));

        var col = B.Group(root.transform, "Columna");
        B.Animate(col, B.PopIn(), 0.35f, 0.05f, new Vector3(0f, 1f, 0f));
        rs.Add(B.Cylinder(col, "Halo", 4.5f, 45f, luz.Burbuja));
        rs.Add(B.Cylinder(col, "Centro", 1.8f, 45f, blanco.Energia));

        var aros = Ajustes(1.6f, 1.6f, 0f, 0f, 5f, 6.5f);
        aros.Loop = true; aros.Rate = 7f; aros.Rise = 18f; aros.Shape = ParticleSystemShapeType.Sphere; aros.Radius = 0.001f;
        aros.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.8f));
        B.Particles(root.transform, "Aros que suben", _aro, luz.Pieza, aros);

        var est = Ajustes(1.2f, 1.6f, 0f, 0f, 0.2f, 0.35f);
        est.Loop = true; est.Rate = 40f; est.Rise = 12f; est.Tumble = true; est.Shape = ParticleSystemShapeType.Circle; est.Radius = 3f; est.Volume = true;
        est.MaxParticles = 120;
        B.Particles(root.transform, "Estrellas que suben", _estrella, luz.Chispa, est);

        var bolas = Estallido(24, 6f, 10f, 0.5f, 0.9f, 0.9f, 1.3f);
        bolas.Drag = 2.5f; bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = 1.5f;
        B.Particles(root.transform, "Polvo que se abre", _bolita, polvo.Energia, bolas);

        Pulso(root, rs.ToArray(), B.PointLight(root.transform, luz.Luz, new Vector3(0f, 3f, 0f)), null, 9f, 25f, 0f, 0f, 0f,
              2.6f, 0.6f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.05f, 1f), new Keyframe(2.4f, 0.8f), new Keyframe(3.2f, 0f)));
    }

    /// Los dos hechizos se encuentran: destello blanco, una onda de cada color y estrellas de los dos.
    private static void Choque(GameObject root, Paleta luz, Paleta sombra, Paleta blanco, Paleta polvo)
    {
        var rs = new List<Renderer>();
        var d = B.Group(root.transform, "Destello");
        B.Animate(d, B.Flash(), 0.5f);
        rs.Add(B.Sphere(d, "Bola", 1.3f, blanco.Energia));
        rs.Add(Onda(root.transform, "Onda de luz", 1.8f, Quaternion.Euler(20f, 0f, 0f), luz.Pieza, 0.55f));
        rs.Add(Onda(root.transform, "Onda de sombra", 1.6f, Quaternion.Euler(-25f, 40f, 0f), sombra.Pieza, 0.55f));

        foreach (var p in new[] { luz, sombra })
        {
            var est = Estallido(10, 4f, 6f, 0.1f, 0.16f, 0.5f, 0.8f);
            est.Drag = 2f;
            B.Particles(root.transform, "Estrellas " + p.Nombre, _estrella, p.Chispa, est);
        }
        var bolas = Estallido(8, 2f, 3f, 0.22f, 0.35f, 0.5f, 0.7f);
        bolas.Drag = 3f;
        B.Particles(root.transform, "Bolas", _bolita, polvo.Energia, bolas);
        Pulso(root, rs.ToArray(), B.PointLight(root.transform, Color.white), null, 6f, 6f, 0f, 0f, 0f,
              0.5f, 0.25f, B.LightFlash(0.8f));
    }

    /// Columna de luz que sube del suelo, con aros que suben por ella.
    private static void Pilar(GameObject root, Paleta luz, Paleta blanco)
    {
        var rs = new List<Renderer>();
        var col = B.Group(root.transform, "Columna");
        B.Animate(col, B.PopIn(), 0.25f, 0f, new Vector3(0f, 1f, 0f));
        rs.Add(B.Cylinder(col, "Halo", 0.9f, 7f, luz.Burbuja));
        rs.Add(B.Cylinder(col, "Centro", 0.3f, 7f, blanco.Energia));

        var aros = Ajustes(1.5f, 1.5f, 0f, 0f, 1.2f, 1.4f);
        aros.Loop = true; aros.Rate = 5f; aros.Rise = 4f; aros.Shape = ParticleSystemShapeType.Sphere; aros.Radius = 0.001f;
        aros.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.6f));
        B.Particles(root.transform, "Aros que suben", _aro, luz.Pieza, aros);

        var est = Ajustes(1f, 1f, 0f, 0f, 0.06f, 0.1f);
        est.Loop = true; est.Rate = 14f; est.Rise = 3f; est.Tumble = true; est.Shape = ParticleSystemShapeType.Circle; est.Radius = 0.7f;
        B.Particles(root.transform, "Estrellitas", _estrella, luz.Chispa, est);

        Pulso(root, rs.ToArray(), B.PointLight(root.transform, luz.Luz, new Vector3(0f, 1.5f, 0f)), null, 4f, 6f, 0.5f, 0.25f, 0.1f, 1.6f, 0.4f);
    }

    /// Pinchos de cristal oscuro que salen del suelo alrededor de quien recibe el golpe.
    private static void Grieta(GameObject root, Paleta sombra, Paleta polvo)
    {
        var rs = new List<Renderer>();
        var azar = new System.Random(1136);
        for (int i = 0; i < 7; i++)
        {
            float a = i * (360f / 7f) + (float)azar.NextDouble() * 25f;
            float radio = 0.35f + (float)azar.NextDouble() * 0.55f;
            var pos = Quaternion.Euler(0f, a, 0f) * new Vector3(radio, 0f, 0f);
            var g = B.Group(root.transform, "Pincho " + (i + 1), pos, Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(0f, 0f, -(20f + (float)azar.NextDouble() * 15f)));
            float alto = 0.7f + (float)azar.NextDouble() * 0.6f;
            g.localScale = new Vector3(1f, alto, 1f);
            B.Animate(g, B.PopIn(), 0.18f, i * 0.05f, new Vector3(0f, 1f, 0f));
            rs.Add(B.Piece(g, "Cristal", _pincho, sombra.Energia, Vector3.zero, Quaternion.identity, Vector3.one));
        }
        var bolas = Estallido(8, 1f, 2f, 0.2f, 0.32f, 0.5f, 0.7f);
        bolas.Drag = 3f; bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = 0.4f;
        B.Particles(root.transform, "Polvo", _bolita, polvo.Energia, bolas);
        var esquirlas = Estallido(8, 2f, 3.5f, 0.06f, 0.1f, 0.5f, 0.7f);
        esquirlas.Gravity = 1f;
        B.Particles(root.transform, "Esquirlas", _estrella, sombra.Chispa, esquirlas);
        Pulso(root, rs.ToArray(), B.PointLight(root.transform, sombra.Luz, new Vector3(0f, 0.5f, 0f)), null, 2.5f, 3f, 0.4f, 0.4f, 0.1f, 1.5f, 0.4f);
    }

    /// Onda que corre por el suelo con polvo oscuro.
    private static void OndaEnElSuelo(GameObject root, Paleta sombra, Paleta polvo)
    {
        var rs = new List<Renderer>
        {
            Onda(root.transform, "Onda", 2.0f, Quaternion.identity, sombra.Pieza, 0.6f, 0.05f),
            Onda(root.transform, "Onda de dentro", 1.5f, Quaternion.identity, sombra.Pieza, 0.6f, 0.05f, 0.12f),
        };
        var bolas = Estallido(12, 2.5f, 3.5f, 0.22f, 0.36f, 0.7f, 1f);
        bolas.Drag = 4f; bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = 0.5f;
        B.Particles(root.transform, "Polvo", _bolita, polvo.Energia, bolas);
        Pulso(root, rs.ToArray(), B.PointLight(root.transform, sombra.Luz, new Vector3(0f, 0.4f, 0f)), null, 3f, 4f, 0f, 0f, 0f,
              0.45f, 0.25f, B.LightFlash(0.6f));
    }

    /// La carga grande del Mago Oscuro: su orbe, gemelo oscuro del del Archimago, con restos que entran.
    private static void CargaGrande(GameObject root, Paleta sombra, Paleta polvo)
    {
        var giros = new List<Spin>();
        var rs = new List<Renderer>();
        var orbe = B.Sphere(root.transform, "Orbe", 0.9f, sombra.Energia);
        giros.Add(new Spin(orbe.transform, new Vector3(-0.3f, 1f, 0.2f).normalized, -30f));
        rs.Add(orbe);
        AnilloQueVuelca(root.transform, "A", 0.65f, 64f, 0f, -45f, -150f, sombra, 0.1f, giros, rs);
        AnilloQueVuelca(root.transform, "B", 0.72f, 58f, 120f, 60f, 130f, sombra, 0.1f, giros, rs);
        AnilloQueVuelca(root.transform, "C", 0.8f, 76f, 240f, -75f, 180f, sombra, 0.1f, giros, rs);

        var restos = Ajustes(0.6f, 0.7f, -2.4f, -2.4f, 0.12f, 0.22f);
        restos.Loop = true; restos.Rate = 10f; restos.Tumble = true; restos.Shape = ParticleSystemShapeType.Sphere; restos.Radius = 1.6f;
        B.Particles(root.transform, "Restos que entran", _bolita, polvo.Energia, restos);
        var est = Ajustes(0.6f, 0.6f, -2.4f, -2.4f, 0.08f, 0.12f);
        est.Loop = true; est.Rate = 12f; est.Tumble = true; est.Shape = ParticleSystemShapeType.Sphere; est.Radius = 1.5f;
        B.Particles(root.transform, "Estrellas que entran", _estrella, sombra.Chispa, est);

        Pulso(root, rs.ToArray(), B.PointLight(root.transform, sombra.Luz), giros.ToArray(), 3f, 4f, 1f, 0.35f, 0.5f);
    }

    /// El hechizo que levanta la carreta: embudo de aros que giran y estrellitas que suben en espiral.
    private static void Remolino(GameObject root, Paleta luz)
    {
        var giros = new List<Spin>();
        var rs = new List<Renderer>();
        float[] alturas = { 0.1f, 0.45f, 0.8f, 1.15f };
        float[] radios = { 0.35f, 0.5f, 0.65f, 0.8f };
        for (int i = 0; i < alturas.Length; i++)
        {
            var nivel = B.Group(root.transform, "Nivel " + (i + 1), new Vector3(0f, alturas[i], 0f));
            float sentido = (i & 1) == 0 ? 1f : -1f;
            AnilloQueVuelca(nivel, ((char)('A' + i)).ToString(), radios[i], 10f, i * 70f, sentido * (200f + i * 30f), sentido * 260f,
                            luz, 0.06f, giros, rs, _aro);
        }
        var est = Ajustes(1f, 1.1f, 0f, 0f, 0.06f, 0.1f);
        est.Loop = true; est.Rate = 18f; est.Rise = 1.4f; est.Orbital = 4f; est.Tumble = true;
        est.Shape = ParticleSystemShapeType.Circle; est.Radius = 0.4f;
        B.Particles(root.transform, "Estrellitas en espiral", _estrella, luz.Chispa, est);
        Pulso(root, rs.ToArray(), B.PointLight(root.transform, luz.Luz, new Vector3(0f, 0.6f, 0f)), giros.ToArray(), 1.5f, 3f, 0.8f, 0.3f, 0.3f, 3.2f, 0.5f);
    }

    /// Bocanadas de polvo de dibujo al caer o pisar fuerte.
    private static void Polvo(GameObject root, Paleta polvo)
    {
        var bolas = Estallido(10, 1.6f, 2.6f, 0.22f, 0.4f, 0.6f, 0.9f);
        bolas.Drag = 4f; bolas.Rise = 0.4f; bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = 0.25f;
        bolas.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f));
        B.Particles(root.transform, "Bocanadas", _bolita, polvo.Energia, bolas);
    }

    // ── Ayudas ───────────────────────────────────────────────────────────────────────────────

    /// Anillo fino que vuelca alrededor del centro, con una cuenta que corre por él (como en el orbe).
    private static void AnilloQueVuelca(Transform padre, string letra, float radio, float inclinacion, float rumbo,
                                        float vuelco, float cuenta, Paleta p, float tamCuenta,
                                        List<Spin> giros, List<Renderer> rs, Mesh malla = null)
    {
        var v = B.Group(padre, "Vuelco " + letra, Vector3.zero, Quaternion.Euler(0f, rumbo, 0f));
        giros.Add(new Spin(v, Vector3.up, vuelco));
        var inc = B.Group(v, "Inclinación " + letra, Vector3.zero, Quaternion.Euler(inclinacion, 0f, 0f));
        var aro = B.Piece(inc, "Anillo " + letra, malla != null ? malla : _aroDeOrbe, p.Pieza, Vector3.zero, Quaternion.identity, Vector3.one * radio);
        giros.Add(new Spin(aro.transform, Vector3.up, cuenta));
        rs.Add(aro);
        rs.Add(B.Sphere(aro.transform, "Cuenta " + letra, tamCuenta / radio, p.Chispa, new Vector3(1f, 0f, 0f)));
    }

    /// Aro que se abre (onda de choque).
    private static Renderer Onda(Transform padre, string nombre, float radio, Quaternion giro, Material mat, float segundos,
                                 float altura = 0f, float espera = 0f)
    {
        var g = B.Group(padre, nombre, new Vector3(0f, altura, 0f), giro);
        B.Animate(g, B.Expand(), segundos, espera);
        return B.Piece(g, "Aro", _aro, mat, Vector3.zero, Quaternion.identity, Vector3.one * radio);
    }

    private static B.MeshParticles Ajustes(float vidaMin, float vidaMax, float velMin, float velMax, float tamMin, float tamMax)
        => new B.MeshParticles
        {
            LifetimeMin = vidaMin, LifetimeMax = vidaMax, SpeedMin = velMin, SpeedMax = velMax,
            SizeMin = tamMin, SizeMax = tamMax, Shape = ParticleSystemShapeType.Sphere, Radius = 0.05f, Duration = 1f,
        };

    /// Salen todas a la vez hacia fuera, una sola vez.
    private static B.MeshParticles Estallido(int cuantas, float velMin, float velMax, float tamMin, float tamMax, float vidaMin, float vidaMax)
    {
        var s = Ajustes(vidaMin, vidaMax, velMin, velMax, tamMin, tamMax);
        s.Burst = cuantas; s.Loop = false; s.Tumble = true; s.MaxParticles = cuantas + 4;
        return s;
    }

    private static void Pulso(GameObject root, Renderer[] rs, Light luz, Spin[] giros, float intensidad, float alcance,
                              float periodo, float hondo, float aparece, float seVaA = 0f, float seVaEn = 0.3f, AnimationCurve curvaDeLuz = null)
    {
        var p = root.AddComponent<EnergyPulse>();
        p.Configure(rs, luz, giros ?? new Spin[0]);
        p.ConfigureTiming(intensidad, alcance, periodo, hondo, aparece);
        p.ConfigureLifetime(seVaA, seVaEn, curvaDeLuz);
    }

    private static void Guardar(string nombre, System.Action<GameObject> construir, System.Text.StringBuilder log)
    {
        string ruta = $"{Carpeta}/{nombre}.prefab";
        var root = new GameObject(nombre);
        try
        {
            construir(root);
            PrefabUtility.SaveAsPrefabAsset(root, ruta);
            log.Append(nombre).Append(' ');
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
