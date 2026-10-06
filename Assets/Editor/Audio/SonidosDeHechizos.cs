using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SFX propios de todos los hechizos (INC-600, 5 oct 2026).
///
/// Cada hechizo tiene su sonido de lanzamiento (clave «Hechizo_&lt;Asset&gt;»), hecho a la medida de
/// cómo se lanza: carga durante el gesto (castDelaySeconds), salida, y cola según la velocidad;
/// las zonas suenan lo que dura la zona. Los impactos van por elemento y tamaño
/// («Impacto_&lt;Elemento&gt;_&lt;Pequeno|Grande&gt;»). Las zonas y el Paso Sombrío no llevan impacto
/// (los golpes de cada 0,5 s de una zona saturarían el oído).
///
/// Los audios salen de Herramientas/SFX/disenar_hechizos.py y viven en Assets/Audio/SFX_Hechizos/.
/// El menú da de alta las claves en el perfil de audio y las pone en cada hechizo. Se puede pasar
/// las veces que haga falta.
/// </summary>
public static class SonidosDeHechizos
{
    private const string Carpeta = "Assets/Audio/SFX_Hechizos/";
    private const string CarpetaHechizos = "Assets/_SPELLS/";
    private const string RutaPerfil = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";

    // Hechizo (nombre del asset) → impacto ("" = sin impacto).
    private static readonly (string hechizo, string impacto)[] Tabla =
    {
        ("BolaFuego",        "Impacto_Fuego_Pequeno"),
        ("ChispaIgnea",      "Impacto_Fuego_Pequeno"),
        ("LlamaAstral",      "Impacto_Fuego_Pequeno"),
        ("Rafaga",           "Impacto_Viento_Pequeno"),
        ("Tornado",          "Impacto_Viento_Grande"),
        ("Huracan",          "Impacto_Viento_Grande"),
        ("AuraEstelar",      "Impacto_Viento_Grande"),
        ("BolaPrisma",       "Impacto_Luz_Grande"),
        ("EstrellaFugaz",    "Impacto_Luz_Pequeno"),
        ("LluviaDeChispas",  "Impacto_Luz_Pequeno"),
        ("CorazonEstelar",   "Impacto_Luz_Grande"),
        ("GarraDelPacto",    "Impacto_Mente_Grande"),
        ("DardoMental",      "Impacto_Mente_Pequeno"),
        ("Eco",              "Impacto_Mente_Grande"),
        ("MagoOscuroGolpe",  "Impacto_Oscuro_Grande"),
        ("PasoSombrio",      ""),
        ("Levitation",       "Impacto_Levitacion"),
        ("BrisaSanadora",    ""),
        ("Remolino",         ""),
        ("MuroDeFuego",      ""),
        ("TormentaDeFuego",  ""),
        ("ChispaIgneaFuego", ""),
        ("Meteoro",          ""),
        ("NovaDeLuz",        ""),
        ("CupulaEstelar",    ""),
        ("SelloDelPacto",    ""),
        ("CadenasDelPacto",  ""),
        ("JuicioDelPacto",   ""),
        ("MagoOscuroGrieta", ""),
    };

    private static readonly string[] Impactos =
    {
        "Impacto_Fuego_Pequeno", "Impacto_Fuego_Grande", "Impacto_Viento_Pequeno", "Impacto_Viento_Grande",
        "Impacto_Luz_Pequeno", "Impacto_Luz_Grande", "Impacto_Mente_Pequeno", "Impacto_Mente_Grande",
        "Impacto_Oscuro_Pequeno", "Impacto_Oscuro_Grande", "Impacto_Levitacion",
    };

    [MenuItem("El Sendero/Audio/SFX propios de los hechizos (INC-600)")]
    public static void Aplicar()
    {
        var log = new StringBuilder("[SFX de hechizos] ");
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfil);
        if (perfil == null) { Debug.LogError($"[SFX de hechizos] No encuentro el perfil de audio en {RutaPerfil}."); return; }

        int altas = 0, hechizos = 0, faltan = 0;
        var claves = new List<(string clave, string archivo)>();
        foreach (var (h, _) in Tabla) claves.Add(("Hechizo_" + h, "SFX_Hechizo_" + h + ".wav"));
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

        foreach (var (h, impacto) in Tabla)
        {
            var hechizo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(CarpetaHechizos + h + ".asset");
            if (hechizo == null) { faltan++; Debug.LogWarning($"[SFX de hechizos] No encuentro el hechizo {h}."); continue; }
            var so = new SerializedObject(hechizo);
            so.FindProperty("castSFXKey").stringValue = "Hechizo_" + h;
            so.FindProperty("impactSFXKey").stringValue = impacto;
            if (so.ApplyModifiedPropertiesWithoutUndo()) hechizos++;
        }

        AssetDatabase.SaveAssets();
        log.Append($"{altas} claves dadas de alta o cambiadas, {hechizos} hechizos actualizados, {faltan} faltas.");
        if (faltan > 0) Debug.LogWarning(log.ToString()); else Debug.Log(log.ToString());
    }
}
