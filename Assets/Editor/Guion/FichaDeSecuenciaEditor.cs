#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Inspector de una secuencia hecha con guion. En vez de enseñar la lista de beats —que la escribe
/// el horneado y no se toca a mano—, enseña el resumen del guion y los botones para abrirlo, volver
/// a hornearlo o pasar al taller. Las secuencias mixtas (guion + jugabilidad) siguen con el
/// inspector normal. Ver INC-1230 y TDD § 33.
[CustomEditor(typeof(SequenceDefinition))]
public class FichaDeSecuenciaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var definicion = (SequenceDefinition)target;

        if (!EsSoloGuion(definicion, out GuionBeat beat))
        {
            DrawDefaultInspector();
            return;
        }

        EditorGUILayout.HelpBox(
            "Esta secuencia se edita en su guion de texto. Los beats no se tocan a mano: los escribe " +
            "el horneado. Debajo quedan los enganches con el grafo y la puesta en escena.",
            MessageType.Info);

        DibujarResumen(beat?.guion);
        EditorGUILayout.Space();

        serializedObject.Update();
        DibujarPropiedad("displayName");
        DibujarPropiedad("summary");
        DibujarPropiedad("signalIn");
        DibujarPropiedad("signalOut");
        DibujarPropiedad("musicId");
        DibujarPropiedad("presentacionDeTexto");
        DibujarPropiedad("endStayBlack");
        DibujarPropiedad("skipMusicRestore");
        DibujarPropiedad("modulos");
        serializedObject.ApplyModifiedProperties();
    }

    private static bool EsSoloGuion(SequenceDefinition definicion, out GuionBeat beat)
    {
        beat = null;
        if (definicion?.phases == null || definicion.phases.Count != 1) return false;

        var fase = definicion.phases[0];
        if (fase?.beats == null || fase.beats.Count != 1) return false;

        beat = fase.beats[0] as GuionBeat;
        return beat != null;
    }

    private void DibujarResumen(GuionHorneado guion)
    {
        if (guion == null)
        {
            EditorGUILayout.HelpBox("No tiene guion horneado asignado. Escribe el .guion.txt y hornéalo.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.ObjectField("Guion horneado", guion, typeof(GuionHorneado), false);
        EditorGUILayout.LabelField("Duración", $"{guion.duracion:0.0} s");
        EditorGUILayout.LabelField("Planos / frases / efectos",
            $"{guion.planos.Count} / {guion.lineas.Count} / {guion.efectos.Count}");

        string ruta = GuionTextoEditable.RutaDelTexto(guion);
        EditorGUILayout.LabelField("Guion de texto", string.IsNullOrEmpty(ruta) ? "(no encontrado)" : ruta);

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(ruta)))
        {
            if (GUILayout.Button("Abrir el guion")) AbrirTexto(ruta);
            if (GUILayout.Button("Hornear")) HorneadorDeGuion.Hornear(ruta, false);
        }
        if (GUILayout.Button("Abrir el taller")) TallerDeRodajeWindow.AbrirCon(guion);
        EditorGUILayout.EndHorizontal();
    }

    private void DibujarPropiedad(string nombre)
    {
        SerializedProperty propiedad = serializedObject.FindProperty(nombre);
        if (propiedad != null) EditorGUILayout.PropertyField(propiedad, true);
    }

    private static void AbrirTexto(string ruta)
    {
        if (string.IsNullOrEmpty(ruta)) return;
        EditorUtility.OpenWithDefaultApp(ruta);
    }
}
#endif
