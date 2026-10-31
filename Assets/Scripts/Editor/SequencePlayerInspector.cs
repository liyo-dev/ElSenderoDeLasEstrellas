using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Inspector del Sequence Player: resumen de las señales que usa de verdad.
///
/// ── Por qué no se dibuja el Inspector por defecto tal cual ────────────────────────────────────
/// El SequencePlayer hereda de CinematicSequencerBase, que trae sus propios campos de señal de
/// entrada/salida y de música. Pero cuando hay una SequenceDefinition asignada, esos campos NO se
/// usan: mandan los del asset, y los del componente se quedan ahí como restos que dicen otra cosa
/// — el 17 sep la pantalla mostraba "Signal Out: (vacío)" y "Sequence Music Id: AWAKEN" mientras el
/// asset decía "AWAKEN_DONE" y música vacía. Dos fuentes de verdad visibles a la vez, y la que se
/// lee no es la que manda.
///
/// Así que se ocultan los que estén siendo ignorados y se muestra, arriba del todo, por dónde
/// entra y por dónde sale la secuencia DE VERDAD — incluidas las salidas alternativas de las ramas,
/// que no aparecen en ningún campo porque viven dentro de los beats.
[CustomEditor(typeof(SequencePlayer))]
public class SequencePlayerInspector : Editor
{
    private static readonly string[] CamposQueElAssetPisa =
        { "_signalIn", "_signalOut", "_sequenceMusicId" };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var definicion = serializedObject.FindProperty("_definition")?.objectReferenceValue as SequenceDefinition;

        DibujarResumenDeSeniales(definicion);

        // El Inspector normal, saltándose los campos que el asset deja sin efecto.
        var prop = serializedObject.GetIterator();
        bool primero = true;
        while (prop.NextVisible(primero))
        {
            primero = false;
            if (prop.name == "m_Script") { using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(prop); continue; }
            if (definicion != null && System.Array.IndexOf(CamposQueElAssetPisa, prop.name) >= 0) continue;
            EditorGUILayout.PropertyField(prop, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DibujarResumenDeSeniales(SequenceDefinition def)
    {
        if (def == null) return;

        var salidas = new List<string>();
        if (!string.IsNullOrWhiteSpace(def.signalOut)) salidas.Add(def.signalOut + "  (final normal)");

        // Las ramas salen por su cuenta, con un EndSequenceBeat que lleva su propia señal. Eso no
        // aparece en ningún campo del Inspector, así que se busca en los beats.
        if (def.phases != null)
        {
            foreach (var fase in def.phases)
            {
                if (fase?.beats == null) continue;
                foreach (var beat in fase.beats)
                {
                    if (beat is EndSequenceBeat fin && !string.IsNullOrWhiteSpace(fin.signalOutOverride))
                        salidas.Add(fin.signalOutOverride + "  (desde '" + fase.name + "')");
                }
            }
        }

        EditorGUILayout.LabelField("Señales que usa de verdad", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("Entra por",
                string.IsNullOrWhiteSpace(def.signalIn) ? "— (nada la arranca)" : def.signalIn);

            if (salidas.Count == 0) EditorGUILayout.LabelField("Sale por", "— NINGUNA: el grafo se quedaría esperando");
            else
            {
                EditorGUILayout.LabelField("Sale por", salidas[0]);
                for (int i = 1; i < salidas.Count; i++) EditorGUILayout.LabelField(" ", salidas[i]);
            }

            EditorGUILayout.LabelField("Música",
                string.IsNullOrWhiteSpace(def.musicId) ? "— (no cambia la música)" : def.musicId);
        }

        EditorGUILayout.HelpBox(
            "Esto sale del asset, que es quien manda. Los campos de señal y de música del componente " +
            "están ocultos porque no se usan mientras haya una definición asignada.",
            MessageType.None);
        EditorGUILayout.Space();
    }
}
