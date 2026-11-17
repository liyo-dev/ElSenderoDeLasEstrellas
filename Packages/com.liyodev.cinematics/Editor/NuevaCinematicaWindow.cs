using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// La ventanita de «nueva cinemática»: nombre, escena y a escribir. Lo que crea es el esqueleto
    /// del `.guion.txt`, que sigue siendo la única fuente de verdad.
    public class NuevaCinematicaWindow : EditorWindow
    {
        private string _nombre = "Nueva_Cinematica";
        private string _escena = "MainWorld";
        private string[] _escenas = { "MainWorld" };
        private string _error;
        private Vector2 _scroll;

        public static void Abrir()
        {
            var ventana = GetWindow<NuevaCinematicaWindow>(true, "Nueva cinemática", true);
            ventana.minSize = new Vector2(460, 210);
            ventana.maxSize = new Vector2(700, 300);
            ventana.CargarEscenas();
            ventana.ShowUtility();
        }

        private void CargarEscenas()
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente != null)
            {
                try
                {
                    var escenas = puente.Escenas();
                    if (escenas != null && escenas.Length > 0) _escenas = escenas;
                }
                catch { /* sin lista: se queda MainWorld */ }
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Se crea el esqueleto del guion; luego lo escribes y lo horneas.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            _nombre = EditorGUILayout.TextField("Nombre", _nombre);

            int actual = 0;
            for (int i = 0; i < _escenas.Length; i++)
                if (_escenas[i] == _escena) { actual = i; break; }
            actual = EditorGUILayout.Popup("Escena", actual, _escenas);
            if (_escenas.Length > 0) _escena = _escenas[Mathf.Clamp(actual, 0, _escenas.Length - 1)];

            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancelar", GUILayout.Width(90))) Close();
            if (GUILayout.Button("Crear", GUILayout.Width(90))) Crear();
            EditorGUILayout.EndHorizontal();
        }

        private void Crear()
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null) { _error = "No hay puente de cinemáticas."; return; }

            _error = puente.CrearCinematica(_nombre, _escena);
            if (_error == null) Close();
        }
    }
}
