using System;
using Kits.StylizedVfx;
using UnityEditor;
using UnityEngine;
using B = Kits.StylizedVfx.Editor.StylizedVfxBuilder;
using R = Kits.StylizedVfx.Editor.EnergyRecipes;

/// La lectura del combate contra el Demonio (INC-1333): monta sus efectos propios con la receta
/// «dibujo animado con volumen» (golpe que entra, golpe rechazado, invocación de la lluvia, salida,
/// aviso y llegada del paso bajo tierra, y cómo se esfuma al caer), da de alta sus sonidos en el
/// perfil de audio y deja Demon y Demon2 con ReaccionAlGolpe, EsfumarseAlCaer y PlanoDeAviso
/// conectados. Se puede pasar las veces que haga falta: rehace los efectos en su sitio.
public static class DemonioConVolumen
{
    private const string Carpeta = "Assets/_VFX/Hechizos";
    private const string RutaPerfil = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private const string CarpetaDeSonidos = "Assets/Audio/SFX_Hechizos/Combate";
    private static readonly string[] Jefes = { "Assets/Prefabs/Enemy/Demon.prefab", "Assets/Prefabs/Enemy/Demon2.prefab" };

    public const string Golpe = "Demonio_Golpe";
    public const string Rechazo = "Demonio_Rechazo";
    public const string Invocacion = "Demonio_Invocacion";
    public const string TeleSalida = "Demonio_TeleSalida";
    public const string TeleAviso = "Demonio_TeleAviso";
    public const string TeleLlegada = "Demonio_TeleLlegada";
    public const string Esfumarse = "Demonio_Esfumarse";

    private static readonly string[] Sonidos = { Golpe, Rechazo, Invocacion, TeleSalida, TeleLlegada, Esfumarse };

    [MenuItem("El Sendero/Magia/Demonio: lectura del combate (INC-1333)")]
    public static void Menu()
    {
        var log = new System.Text.StringBuilder();
        Construir(log);
        Debug.Log("[Demonio] Lectura del combate: " + log);
    }

    public static void Construir(System.Text.StringBuilder log)
    {
        if (!HechizosConVolumen.PrepararPaletas()) { log.Append("faltan paletas o mallas"); return; }
        var m = HechizosConVolumen.Mallas;
        var rojo = HechizosConVolumen.Paleta("Demonio");
        var oscuro = HechizosConVolumen.Paleta("Oscuro");
        var humo = HechizosConVolumen.Paleta("PolvoOscuro");
        var polvo = HechizosConVolumen.Paleta("Polvo");
        var blanco = HechizosConVolumen.Paleta("Blanco");
        var ceniza = HechizosConVolumen.Paleta("Ceniza");
        if (ceniza == null) { log.Append("falta la paleta Ceniza"); return; }

        // Le entra un golpe: destello blanco que salta y chispas rojas que salen hacia quien golpea.
        Rehacer(Golpe, r =>
        {
            Hijo(r, "Destello", g => R.Flash(g, blanco, m, 0.55f, 0, 2.2f));
            var chis = R.Burst(7, 3f, 5f, 0.08f, 0.13f, 0.25f, 0.4f);
            chis.Shape = ParticleSystemShapeType.Cone; chis.Angle = 35f; chis.Drag = 2.5f;
            var g2 = B.Group(r.transform, "Chispas", Vector3.zero, Quaternion.Euler(180f, 0f, 0f));
            B.Particles(g2, "Chispas", m.Star, rojo.Spark, chis);
        }, log);

        // Golpe que no entra (aro apagado): bocanada de polvo gris y chispitas que se apagan.
        Rehacer(Rechazo, r =>
        {
            Hijo(r, "Bocanada", g => R.Flash(g, polvo, m, 0.35f, 4, 0.6f));
            var bolas = R.Burst(5, 0.8f, 1.4f, 0.1f, 0.16f, 0.3f, 0.45f);
            bolas.Drag = 3f;
            B.Particles(r.transform, "Polvo", m.Puff, polvo.Energy, bolas);
        }, log);

        // Invoca la lluvia: círculo rojo a sus pies, columna hacia el cielo y humo que sube.
        Rehacer(Invocacion, r =>
        {
            Hijo(r, "Círculo", g => R.MagicCircle(g, rojo, m, 2.0f, true, 1.4f));
            Hijo(r, "Columna", g => R.Pillar(g, rojo, oscuro, m, 0.22f, 0.65f, 10f, false));
            Hijo(r, "Humo", g => R.RisingMotes(g, humo, m, 1.3f, m.Puff, 14f, 1.2f, 0.3f));
        }, log);

        // Se hunde en su sombra: charco oscuro que se abre y se cierra, aros que se cierran y bocanada.
        Rehacer(TeleSalida, r =>
        {
            var c = B.Group(r.transform, "Charco", new Vector3(0f, 0.03f, 0f));
            B.Animate(c, B.Flash(), 0.9f);
            B.Piece(c, "Sombra", m.Sphere, humo.Energy, Vector3.zero, Quaternion.identity, new Vector3(2.4f, 0.06f, 2.4f));
            Hijo(r, "Implosión", g => R.Implosion(g, rojo, m, 1.2f, humo));
        }, log);

        // Dónde va a salir: lenguas de fuego que brotan del suelo y un aro rojo que se abre.
        Rehacer(TeleAviso, r =>
        {
            r.AddComponent<GroundMark>().FootprintRadius = 1.1f;
            Hijo(r, "Llamas", g => R.GroundFlames(g, rojo, m, 1.0f, 0.7f, 26f));
            var a = B.Group(r.transform, "Aro", new Vector3(0f, 0.03f, 0f));
            B.Animate(a, B.Expand(0.2f), 0.6f);
            B.Piece(a, "Borde", m.Ring, rojo.Piece, Vector3.zero, Quaternion.identity, new Vector3(1.2f, 0.3f, 1.2f));
        }, log);

        // Brota del suelo: onda roja y pinchos de piedra que saltan a su alrededor.
        Rehacer(TeleLlegada, r =>
        {
            Hijo(r, "Onda", g => R.Nova(g, rojo, m, 2.2f));
            Hijo(r, "Pinchos", g => R.Spikes(g, rojo, humo, m, 6, 1.3f, 0.9f, 1333));
        }, log);

        // Se esfuma: un golpe de humo violeta, ceniza de dibujo que sube girando y chispas violeta que se apagan.
        Rehacer(Esfumarse, r =>
        {
            Hijo(r, "Destello", g => { g.transform.localPosition = new Vector3(0f, 0.9f, 0f); R.Flash(g, oscuro, m, 0.9f, 10, 2f); });

            var bocanada = R.Burst(10, 1f, 1.8f, 0.3f, 0.45f, 0.6f, 0.9f);
            bocanada.Drag = 3f; bocanada.Shape = ParticleSystemShapeType.Circle; bocanada.Radius = 0.5f; bocanada.Duration = 30f;
            B.Particles(r.transform, "Bocanada", m.Puff, humo.Energy, bocanada);

            var cen = R.Settings(1.3f, 1.9f, 0f, 0.2f, 0.25f, 0.45f);
            cen.Loop = false; cen.Duration = 1.6f; cen.Rate = 22f; cen.Rise = 1.4f; cen.Orbital = 0.6f; cen.Tumble = true;
            cen.Shape = ParticleSystemShapeType.Circle; cen.Radius = 0.6f; cen.Volume = true; cen.MaxParticles = 60;
            cen.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(0.7f, 0.8f), new Keyframe(1f, 0f));
            B.Particles(r.transform, "Ceniza que sube", m.Puff, ceniza.Energy, cen);

            var chis = R.Settings(0.9f, 1.4f, 0f, 0.3f, 0.08f, 0.13f);
            chis.Loop = false; chis.Duration = 1.4f; chis.Rate = 16f; chis.Rise = 2.2f; chis.Orbital = 1.2f; chis.Tumble = true;
            chis.Shape = ParticleSystemShapeType.Circle; chis.Radius = 0.5f; chis.Volume = true; chis.MaxParticles = 40;
            chis.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.9f), new Keyframe(1f, 0f));
            B.Particles(r.transform, "Chispas que se apagan", m.Star, oscuro.Spark, chis);
        }, log);

        DarDeAltaLosSonidos(log);
        foreach (var ruta in Jefes) Conectar(ruta, log);
        AssetDatabase.SaveAssets();
    }

    // ── Sonidos ─────────────────────────────────────────────────────────────────────────────

    private static void DarDeAltaLosSonidos(System.Text.StringBuilder log)
    {
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (perfil == null) { log.Append("(no encuentro el perfil de audio) "); return; }
        Undo.RecordObject(perfil, "Sonidos del Demonio");
        foreach (var clave in Sonidos)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CarpetaDeSonidos}/SFX_{clave}.wav");
            if (clip == null) { log.Append("(falta SFX_").Append(clave).Append(".wav) "); continue; }
            var entrada = perfil.eventSfx.Find(e => e != null && string.Equals(e.eventKey, clave, StringComparison.OrdinalIgnoreCase));
            if (entrada == null) perfil.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = clave, sfx = clip });
            else entrada.sfx = clip;
        }
        EditorUtility.SetDirty(perfil);
        log.Append("sonidos dados de alta; ");
    }

    // ── Prefabs de los jefes ────────────────────────────────────────────────────────────────

    private static void Conectar(string ruta, System.Text.StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) { log.Append("(no está ").Append(ruta).Append(") "); return; }
        var root = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            var ia = root.GetComponent<ImpDemonAI>();
            if (ia == null) { log.Append("(").Append(root.name).Append(" sin ImpDemonAI) "); return; }

            var reaccion = Asegurar<ReaccionAlGolpe>(root);
            var so = new SerializedObject(reaccion);
            Poner(so, "modelo", Buscar(root.transform, "RigPelvis"));
            Poner(so, "vfxGolpe", Vfx(Golpe));
            Poner(so, "vfxRechazo", Vfx(Rechazo));
            so.FindProperty("sonidoGolpe").stringValue = Golpe;
            so.FindProperty("sonidoRechazo").stringValue = Rechazo;
            so.FindProperty("alturaDelGolpe").floatValue = 1.3f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var esfumarse = Asegurar<EsfumarseAlCaer>(root);
            so = new SerializedObject(esfumarse);
            Poner(so, "vfx", Vfx(Esfumarse));
            so.FindProperty("sonido").stringValue = Esfumarse;
            so.ApplyModifiedPropertiesWithoutUndo();

            Asegurar<PlanoDeAviso>(root);

            so = new SerializedObject(ia);
            Poner(so, "vfxInvocacion", Vfx(Invocacion));
            Poner(so, "vfxTeleSalida", Vfx(TeleSalida));
            Poner(so, "vfxTeleAviso", Vfx(TeleAviso));
            Poner(so, "vfxTeleLlegada", Vfx(TeleLlegada));
            so.ApplyModifiedPropertiesWithoutUndo();

            // Al morir ya se esfuma con su efecto propio: fuera el efecto de muerte genérico.
            var vida = root.GetComponent<Damageable>();
            if (vida != null)
            {
                so = new SerializedObject(vida);
                var muerte = so.FindProperty("deathVFX");
                if (muerte.objectReferenceValue != null)
                {
                    log.Append("(").Append(root.name).Append(": quito deathVFX ").Append(muerte.objectReferenceValue.name).Append(") ");
                    muerte.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, ruta);
            log.Append(root.name).Append(" conectado; ");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Ayudas ──────────────────────────────────────────────────────────────────────────────

    private static string RutaDe(string nombre) => $"{Carpeta}/VFX_Hechizo_{nombre}.prefab";

    private static GameObject Vfx(string nombre) => AssetDatabase.LoadAssetAtPath<GameObject>(RutaDe(nombre));

    private static void Rehacer(string nombre, Action<GameObject> montar, System.Text.StringBuilder log)
    {
        R.RebuildInPlace(RutaDe(nombre), "VFX_Hechizo_" + nombre, montar);
        log.Append(nombre).Append(' ');
    }

    /// Cada receta en su hijo: cada una lleva su propio latido (EnergyPulse, uno por objeto).
    private static void Hijo(GameObject r, string nombre, Action<GameObject> montar)
    {
        var g = new GameObject(nombre);
        g.transform.SetParent(r.transform, false);
        montar(g);
    }

    // En el editor, GetComponent de algo que falta no es null de C# (es un objeto «vacío»): se
    // compara con el operador de Unity, no con ??.
    private static T Asegurar<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void Poner(SerializedObject so, string campo, UnityEngine.Object valor)
    {
        var p = so.FindProperty(campo);
        if (p != null) p.objectReferenceValue = valor;
    }

    private static Transform Buscar(Transform t, string nombre)
    {
        if (t.name == nombre) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = Buscar(t.GetChild(i), nombre);
            if (r != null) return r;
        }
        return null;
    }
}
