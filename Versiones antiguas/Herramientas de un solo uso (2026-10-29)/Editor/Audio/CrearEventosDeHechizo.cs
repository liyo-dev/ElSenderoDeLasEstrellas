#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Crea los eventos de audio por capas de los primeros hechizos y los engancha al hechizo
/// (INC-801).
///
/// Un impacto con un solo clip suena a nota; un impacto con transitorio + cuerpo + sub +
/// cola suena a golpe. Las capas nuevas las genera `Herramientas/Audio/disenar_capas_de_hechizo.py`.
public static class CrearEventosDeHechizo
{
    const string CARPETA = "Assets/_SPELLS/Eventos";
    const string CARPETA_CAPAS = "Assets/Audio/SFX_Hechizos/Capas";

    struct Hechizo
    {
        public string nombre;       // nombre del asset del hechizo
        public string elemento;     // Fuego, Viento, Luz, Mente, Oscuro
        public string cuerpoImpacto;// clip de impacto que ya existe
        public string impactoKey;   // clave del perfil para el impacto
    }

    static readonly Hechizo[] Hechizos =
    {
        new Hechizo { nombre = "BolaFuego",    elemento = "Fuego",  cuerpoImpacto = "SFX_Impacto_Fuego_Pequeno.wav",  impactoKey = "Impacto_Fuego_Pequeno" },
        new Hechizo { nombre = "ChispaIgnea",  elemento = "Fuego",  cuerpoImpacto = "SFX_Impacto_Fuego_Pequeno.wav",  impactoKey = "Impacto_Fuego_Pequeno" },
        new Hechizo { nombre = "Huracan",      elemento = "Viento", cuerpoImpacto = "SFX_Impacto_Viento_Grande.wav", impactoKey = "Impacto_Viento_Grande" },
        new Hechizo { nombre = "NovaDeLuz",    elemento = "Luz",    cuerpoImpacto = "SFX_Impacto_Luz_Grande.wav",    impactoKey = "Impacto_Luz_Grande" },
        new Hechizo { nombre = "Meteoro",      elemento = "Luz",    cuerpoImpacto = "SFX_Impacto_Luz_Grande.wav",    impactoKey = "Impacto_Luz_Grande" },
    };

    [MenuItem("El Sendero/Audio/Crear los eventos por capas de los hechizos")]
    public static void Aplicar()
    {
        if (!AssetDatabase.IsValidFolder(CARPETA))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_SPELLS"))
            {
                Debug.LogError("[Audio] No existe Assets/_SPELLS");
                return;
            }
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Eventos");
        }

        int creados = 0;
        foreach (var h in Hechizos)
        {
            var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"Assets/_SPELLS/{h.nombre}.asset");
            if (spell == null)
            {
                Debug.LogWarning($"[Audio] No se encontró el hechizo {h.nombre}");
                continue;
            }

            var cuerpoLanzamiento = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"Assets/Audio/SFX_Hechizos/SFX_Hechizo_{h.nombre}.wav");
            var cuerpoImpacto = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"Assets/Audio/SFX_Hechizos/{h.cuerpoImpacto}");
            var transitorio = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CARPETA_CAPAS}/Capa_Transitorio_{h.elemento}.wav");
            var sub = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CARPETA_CAPAS}/Capa_Sub_{h.elemento}.wav");
            var cola = AssetDatabase.LoadAssetAtPath<AudioClip>($"{CARPETA_CAPAS}/Capa_Cola_{h.elemento}.wav");

            if (cuerpoImpacto == null || transitorio == null || sub == null || cola == null)
            {
                Debug.LogWarning($"[Audio] Faltan clips de {h.nombre} (¿se generaron las capas?)");
                continue;
            }

            // Lanzamiento: el cuerpo de siempre + el sub que le da peso al final de la carga.
            var lanzamiento = CrearOActualizar($"{CARPETA}/{h.nombre}_Lanzamiento.asset");
            lanzamiento.capas = new List<AudioEventSO.Capa>();
            if (cuerpoLanzamiento != null)
                lanzamiento.capas.Add(Capa(cuerpoLanzamiento, 0f, 0f, 1f, 1f));
            lanzamiento.capas.Add(Capa(sub, -6f, 0.3f, 1f, 1f));
            EditorUtility.SetDirty(lanzamiento);

            // Impacto: la grieta, el cuerpo, el sub que entra tarde y la cola.
            var impacto = CrearOActualizar($"{CARPETA}/{h.nombre}_Impacto.asset");
            impacto.capas = new List<AudioEventSO.Capa>();
            impacto.capas.Add(Capa(transitorio, 0f, 0f, 1f, 1f));
            impacto.capas.Add(Capa(cuerpoImpacto, 0f, 0.005f, 1f, 1f));
            impacto.capas.Add(Capa(sub, 2f, 0.05f, 1f, 1f));
            impacto.capas.Add(Capa(cola, -4f, 0.03f, 1f, 1f));
            EditorUtility.SetDirty(impacto);

            Undo.RecordObject(spell, "Eventos de audio del hechizo");
            spell.castEvent = lanzamiento;
            spell.impactEvent = impacto;
            // Los hechizos sin clave de impacto (Nova de Luz, Meteorito) se quedan mudos.
            if (string.IsNullOrEmpty(spell.impactSFXKey)) spell.impactSFXKey = h.impactoKey;
            EditorUtility.SetDirty(spell);
            creados += 2;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] Eventos de hechizo listos: {creados} assets (lanzamiento e impacto).");
    }

    static AudioEventSO CrearOActualizar(string ruta)
    {
        var evento = AssetDatabase.LoadAssetAtPath<AudioEventSO>(ruta);
        if (evento != null) return evento;
        evento = ScriptableObject.CreateInstance<AudioEventSO>();
        AssetDatabase.CreateAsset(evento, ruta);
        return evento;
    }

    static AudioEventSO.Capa Capa(AudioClip clip, float gananciaDb, float retardo, float probabilidad, float paneo)
    {
        return new AudioEventSO.Capa
        {
            clips = new List<AudioClip> { clip },
            gananciaDb = gananciaDb,
            retardo = retardo,
            probabilidad = probabilidad,
            paneo = paneo,
            rangoPitch = new Vector2(1f, 1f),
            espacial = true
        };
    }
}
#endif
