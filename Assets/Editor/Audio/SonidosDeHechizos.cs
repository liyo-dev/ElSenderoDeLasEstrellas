using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sonidos de todos los hechizos: una sola tabla y un solo menú (Ver INC-600, INC-801).
///
/// Cada hechizo tiene su sonido de lanzamiento (clave «Hechizo_&lt;Asset&gt;»), hecho a la medida de
/// cómo se lanza: carga durante el gesto (castDelaySeconds), salida, y cola según la velocidad;
/// las zonas suenan lo que dura la zona. Los impactos van por elemento y tamaño
/// («Impacto_&lt;Elemento&gt;_&lt;Pequeno|Grande&gt;»), salvo Amanecer, que lleva el suyo
/// (Herramientas/SFX/disenar_amanecer.py, Ver INC-724). Las zonas y el Paso Sombrío no llevan impacto
/// (los golpes de cada 0,5 s de una zona saturarían el oído).
///
/// Los hechizos con columna «capas» llevan además eventos de audio por capas (AudioEventSO en
/// Assets/_SPELLS/Eventos): un impacto con un solo clip suena a nota; con transitorio + cuerpo +
/// sub + cola suena a golpe. Las capas por elemento las genera
/// Herramientas/Audio/disenar_capas_de_hechizo.py.
///
/// Los audios salen de Herramientas/SFX/disenar_hechizos.py y viven en Assets/Audio/SFX_Hechizos/.
/// El menú da de alta las claves en el perfil de audio, las pone en cada hechizo y rehace los
/// eventos por capas. Se puede pasar las veces que haga falta: el resultado es siempre la tabla.
/// </summary>
public static class SonidosDeHechizos
{
    private const string Carpeta = "Assets/Audio/SFX_Hechizos/";
    private const string CarpetaCapas = "Assets/Audio/SFX_Hechizos/Capas/";
    private const string CarpetaHechizos = "Assets/_SPELLS/";
    private const string CarpetaEventos = "Assets/_SPELLS/Eventos";
    private const string RutaPerfil = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";

    // Hechizo (nombre del asset) → impacto ("" = sin impacto) y elemento de sus capas (null = sin
    // eventos por capas). El cuerpo del impacto por capas es el clip del propio impacto.
    private static readonly (string hechizo, string impacto, string capas)[] Tabla =
    {
        ("BolaFuego",        "Impacto_Fuego_Pequeno",  "Fuego"),
        ("ChispaIgnea",      "Impacto_Fuego_Pequeno",  "Fuego"),
        ("LlamaAstral",      "Impacto_Fuego_Pequeno",  null),
        ("Rafaga",           "Impacto_Viento_Pequeno", null),
        ("Tornado",          "Impacto_Viento_Grande",  null),
        ("Huracan",          "Impacto_Viento_Grande",  "Viento"),
        ("AuraEstelar",      "Impacto_Viento_Grande",  null),
        ("BolaPrisma",       "Impacto_Luz_Grande",     null),
        ("EstrellaFugaz",    "Impacto_Luz_Pequeno",    null),
        ("LluviaDeChispas",  "Impacto_Luz_Pequeno",    null),
        ("CorazonEstelar",   "Impacto_Luz_Grande",     null),
        ("Amanecer",         "Impacto_Amanecer",       null),
        ("GarraDelPacto",    "Impacto_Mente_Grande",   null),
        ("DardoMental",      "Impacto_Mente_Pequeno",  null),
        ("Eco",              "Impacto_Mente_Grande",   null),
        ("MagoOscuroGolpe",  "Impacto_Oscuro_Grande",  null),
        ("PasoSombrio",      "",                       null),
        ("Levitation",       "Impacto_Levitacion",     null),
        ("BrisaSanadora",    "",                       null),
        ("Remolino",         "",                       null),
        ("MuroDeFuego",      "",                       null),
        ("TormentaDeFuego",  "",                       null),
        ("ChispaIgneaFuego", "",                       null),
        ("Meteoro",          "Impacto_Luz_Grande",     "Luz"),
        ("NovaDeLuz",        "Impacto_Luz_Grande",     "Luz"),
        ("CupulaEstelar",    "",                       null),
        ("SelloDelPacto",    "",                       null),
        ("CadenasDelPacto",  "",                       null),
        ("JuicioDelPacto",   "",                       null),
        ("MagoOscuroGrieta", "",                       null),
    };

    private static readonly string[] Impactos =
    {
        "Impacto_Fuego_Pequeno", "Impacto_Fuego_Grande", "Impacto_Viento_Pequeno", "Impacto_Viento_Grande",
        "Impacto_Luz_Pequeno", "Impacto_Luz_Grande", "Impacto_Mente_Pequeno", "Impacto_Mente_Grande",
        "Impacto_Oscuro_Pequeno", "Impacto_Oscuro_Grande", "Impacto_Levitacion",
        "Impacto_Amanecer",
    };

    [MenuItem("El Sendero/Audio/Sonidos de los hechizos (claves y eventos por capas)")]
    public static void Aplicar()
    {
        var log = new StringBuilder("[SFX de hechizos] ");
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (perfil == null) { Debug.LogError($"[SFX de hechizos] No encuentro el perfil de audio en {RutaPerfil}."); return; }

        int altas = 0, hechizos = 0, eventos = 0, faltan = 0;
        var claves = new List<(string clave, string archivo)>();
        foreach (var (h, _, _) in Tabla) claves.Add(("Hechizo_" + h, "SFX_Hechizo_" + h + ".wav"));
        foreach (var i in Impactos) claves.Add((i, "SFX_" + i + ".wav"));

        foreach (var (clave, archivo) in claves)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Carpeta + archivo);
            if (clip == null) { faltan++; Debug.LogWarning($"[SFX de hechizos] Falta {Carpeta}{archivo}."); continue; }
            var e = perfil.eventSfx.Find(x => x != null && string.Equals(x.eventKey, clave, System.StringComparison.OrdinalIgnoreCase));
            if (e == null) { perfil.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = clave, sfx = clip }); altas++; }
            else if (e.sfx != clip) { e.sfx = clip; altas++; }
        }
        EditorUtility.SetDirty(perfil);

        foreach (var (h, impacto, capas) in Tabla)
        {
            var hechizo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(CarpetaHechizos + h + ".asset");
            if (hechizo == null) { faltan++; Debug.LogWarning($"[SFX de hechizos] No encuentro el hechizo {h}."); continue; }
            var so = new SerializedObject(hechizo);
            so.FindProperty("castSFXKey").stringValue = "Hechizo_" + h;
            so.FindProperty("impactSFXKey").stringValue = impacto;
            if (so.ApplyModifiedPropertiesWithoutUndo()) hechizos++;

            if (!string.IsNullOrEmpty(capas))
            {
                if (CrearEventosPorCapas(hechizo, h, impacto, capas)) eventos += 2;
                else faltan++;
            }
        }

        AssetDatabase.SaveAssets();
        log.Append($"{altas} claves dadas de alta o cambiadas, {hechizos} hechizos actualizados, {eventos} eventos por capas, {faltan} faltas.");
        if (faltan > 0) Debug.LogWarning(log.ToString()); else Debug.Log(log.ToString());
    }

    // Crea o rehace los eventos de lanzamiento e impacto por capas del hechizo y se los asigna.
    private static bool CrearEventosPorCapas(MagicSpellSO hechizo, string nombre, string impacto, string elemento)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaEventos))
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Eventos");

        var cuerpoLanzamiento = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Carpeta}SFX_Hechizo_{nombre}.wav");
        var cuerpoImpacto = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Carpeta}SFX_{impacto}.wav");
        var transitorio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CarpetaCapas}Capa_Transitorio_{elemento}.wav");
        var sub = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CarpetaCapas}Capa_Sub_{elemento}.wav");
        var cola = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CarpetaCapas}Capa_Cola_{elemento}.wav");
        if (cuerpoImpacto == null || transitorio == null || sub == null || cola == null)
        {
            Debug.LogWarning($"[SFX de hechizos] Faltan clips de las capas de {nombre} (¿se generaron las capas de {elemento}?).");
            return false;
        }

        // Lanzamiento: el cuerpo de siempre + el sub que le da peso al final de la carga.
        var lanzamiento = CrearOCargarEvento($"{CarpetaEventos}/{nombre}_Lanzamiento.asset");
        lanzamiento.capas = new List<AudioEventSO.Capa>();
        if (cuerpoLanzamiento != null) lanzamiento.capas.Add(Capa(cuerpoLanzamiento, 0f, 0f));
        lanzamiento.capas.Add(Capa(sub, -6f, 0.3f));
        EditorUtility.SetDirty(lanzamiento);

        // Impacto: la grieta, el cuerpo, el sub que entra tarde y la cola.
        var golpe = CrearOCargarEvento($"{CarpetaEventos}/{nombre}_Impacto.asset");
        golpe.capas = new List<AudioEventSO.Capa>
        {
            Capa(transitorio, 0f, 0f),
            Capa(cuerpoImpacto, 0f, 0.005f),
            Capa(sub, 2f, 0.05f),
            Capa(cola, -4f, 0.03f),
        };
        EditorUtility.SetDirty(golpe);

        hechizo.castEvent = lanzamiento;
        hechizo.impactEvent = golpe;
        EditorUtility.SetDirty(hechizo);
        return true;
    }

    private static AudioEventSO CrearOCargarEvento(string ruta)
    {
        var evento = AssetDatabase.LoadAssetAtPath<AudioEventSO>(ruta);
        if (evento != null) return evento;
        evento = ScriptableObject.CreateInstance<AudioEventSO>();
        AssetDatabase.CreateAsset(evento, ruta);
        return evento;
    }

    private static AudioEventSO.Capa Capa(AudioClip clip, float gananciaDb, float retardo)
    {
        return new AudioEventSO.Capa
        {
            clips = new List<AudioClip> { clip },
            gananciaDb = gananciaDb,
            retardo = retardo,
            probabilidad = 1f,
            paneo = 0f, // centrado: el lado lo pone la posición 3D del impacto

            rangoPitch = new Vector2(1f, 1f),
            espacial = true
        };
    }
}
