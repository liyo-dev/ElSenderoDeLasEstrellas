using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Inspector del Sequence Player: resumen de señales y panel de ajuste de planos.
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
        var player = (SequencePlayer)target;
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

        DibujarAjusteDePlanos(player);
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

    private void DibujarAjusteDePlanos(SequencePlayer player)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ajuste de planos", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Dale a Play y lanza la secuencia. Cuando haya un plano calculado en pantalla, aquí " +
                "saldrán los mandos para ajustarlo mientras lo ves.\n\n" +
                "Acuérdate de marcar 'Live Preview Shots' en el Sequence Stage: sin eso, un plano de " +
                "corte seco no se entera de los cambios hasta el corte siguiente.",
                MessageType.Info);
            return;
        }

        var framing = player.CurrentFraming;
        if (framing == null)
        {
            EditorGUILayout.HelpBox("Todavía no hay ningún plano calculado en pantalla.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("Plano actual", player.CurrentShotLabel ?? "—");

        EditorGUI.BeginChangeCheck();

        float distancia = EditorGUILayout.Slider(
            new GUIContent("Distancia", "Aleja (>1) o acerca (<1) este plano concreto, sin cambiar su " +
                "composición. Se multiplica con el ajuste general del Sequence Stage."),
            framing.distanceScale <= 0.01f ? 1f : framing.distanceScale, 0.4f, 3f);

        float altura = EditorGUILayout.Slider(
            new GUIContent("Altura", "Sube (+) o baja (-) la cámara, en metros. Bajarla mira al " +
                "personaje desde abajo y lo hace parecer más imponente; subirla lo empequeñece."),
            framing.heightBias, -2f, 2f);

        if (EditorGUI.EndChangeCheck())
        {
            framing.distanceScale = distancia;
            framing.heightBias = altura;
            player.RefreshCurrentShot();
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Guardar en el asset"))
            {
                var def = serializedObject.FindProperty("_definition")?.objectReferenceValue;
                if (def != null)
                {
                    EditorUtility.SetDirty(def);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Ajuste de planos] Guardado: '{player.CurrentShotLabel}' → " +
                              $"distancia ×{distancia:F2}, altura {altura:+0.00;-0.00;0}.");
                }
            }

            if (GUILayout.Button("Volver a 1 / 0"))
            {
                framing.distanceScale = 1f;
                framing.heightBias = 0f;
                player.RefreshCurrentShot();
            }
        }

        EditorGUILayout.HelpBox(
            "Los cambios ya están puestos en el asset aunque no guardes: al ser un ScriptableObject " +
            "sobreviven a salir del Play. El botón solo los escribe a disco.",
            MessageType.None);

        Repaint();
    }
}
