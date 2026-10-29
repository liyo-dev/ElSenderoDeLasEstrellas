using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// El despertar de la magia con guion horneado (INC-598, 5 oct 2026).
///
/// SEQ_StarAwakening mezcla cine y jugabilidad: el cine pasa a dos guiones horneados y la
/// jugabilidad (el botón a tiempo y las dos ramas) se queda como estaba.
///
///   1 · La amenaza (guion «Despertar_1_Amenaza»): del aviso de Eldran a la pose de combate.
///   5 · El botón (como siempre): plano general, la bola se reanuda y el panic input.
///   6 · El contraataque (guion «Despertar_2_Contraataque»), solo si el jugador acierta.
///   8 · Si no reacciona a tiempo (como siempre): la bola le alcanza y Game Over.
///
/// Se puede pasar varias veces: la primera vez guarda la versión de beats en Versiones antiguas y
/// rehace las fases; después solo vuelve a enlazar los horneados. Mientras no exista el horneado
/// del contraataque, se quedan las fases de beats 6 y 7 de siempre.
/// </summary>
public static class MontarDespertarConGuion
{
    private const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_StarAwakening.asset";
    private const string Amenaza = "Despertar_1_Amenaza";
    private const string Contraataque = "Despertar_2_Contraataque";
    private const string Marca = "panicSuperado";

    [MenuItem("El Sendero/Secuencias/Despertar: montar con guion (INC-598)")]
    public static void Montar()
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        if (def == null) { Debug.LogError($"[Despertar] No encuentro {RutaSecuencia}."); return; }

        var hA = Horneado(Amenaza);
        var hB = Horneado(Contraataque);

        GuardarCopia();
        Undo.RecordObject(def, "Despertar con guion");

        var fases = def.phases ?? new List<SequencePhase>();
        var faseBoton = fases.FirstOrDefault(f => f.name.StartsWith("5 -"));
        var faseFallo = fases.FirstOrDefault(f => f.name.StartsWith("8 -"));
        var faseContra = fases.FirstOrDefault(f => f.name.StartsWith("6 -") && !EsDeGuion(f));
        var faseDespues = fases.FirstOrDefault(f => f.name.StartsWith("7 -"));
        var faseContraGuion = fases.FirstOrDefault(f => EsDeGuion(f, Contraataque));

        if (faseBoton == null || faseFallo == null)
        {
            Debug.LogError("[Despertar] No encuentro las fases «5 - …» y «8 - …» de la jugabilidad; no toco nada.");
            return;
        }

        var nuevas = new List<SequencePhase>
        {
            new SequencePhase
            {
                name = "1 - La amenaza (guion)",
                beats = new List<SequenceBeat> { new GuionBeat { guion = hA, note = "Guion: " + Amenaza } },
            },
            faseBoton,
        };

        if (hB != null || faseContraGuion != null)
        {
            if (faseContraGuion != null) ((GuionBeat)faseContraGuion.beats[0]).guion = hB;
            nuevas.Add(faseContraGuion ?? new SequencePhase
            {
                name = "6 - El contraataque (guion)",
                onlyIfFlag = Marca,
                beats = new List<SequenceBeat> { new GuionBeat { guion = hB, note = "Guion: " + Contraataque } },
            });
        }
        else
        {
            if (faseContra != null) nuevas.Add(faseContra);
            if (faseDespues != null) nuevas.Add(faseDespues);
        }
        nuevas.Add(faseFallo);

        def.phases = nuevas;
        // Cómo salen los textos lo decide la línea TEXTO de los guiones (subtítulos, como el prólogo).
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Despertar] Montado: {string.Join(" · ", nuevas.Select(f => f.name))}. " +
                  $"Amenaza {(hA != null ? "horneada" : "SIN hornear")}, contraataque {(hB != null ? "horneado" : "sin hornear (siguen las fases de beats)")}.");
    }

    private static bool EsDeGuion(SequencePhase f, string nombre = null)
        => f?.beats != null && f.beats.Count == 1 && f.beats[0] is GuionBeat gb
           && (nombre == null || gb.note == "Guion: " + nombre || (gb.guion != null && gb.guion.name == nombre + "_Horneado"));

    private static GuionHorneado Horneado(string nombre)
        => AssetDatabase.LoadAssetAtPath<GuionHorneado>($"Assets/_SEQUENCES/Guiones/{nombre}_Horneado.asset");

    private static void GuardarCopia()
    {
        string raiz = Directory.GetParent(Application.dataPath).FullName;
        string destino = Path.Combine(raiz, "Versiones antiguas", "Secuencias de beats (antes del guion)");
        Directory.CreateDirectory(destino);
        string copia = Path.Combine(destino, Path.GetFileName(RutaSecuencia));
        if (!File.Exists(copia)) File.Copy(Path.Combine(raiz, RutaSecuencia), copia);
    }
}
