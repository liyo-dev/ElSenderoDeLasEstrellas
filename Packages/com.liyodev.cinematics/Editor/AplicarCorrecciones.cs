using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// Lee el fichero que guarda la página del storyboard (`correcciones-<guion>.json`) y pide al
    /// proyecto que lo aplique: escribe el cambio en el `.guion.txt`, rehornea y vuelve a generar la
    /// página. El texto sigue siendo la única verdad; esto solo ahorra el viaje de copiar y pegar.
    public static class AplicarCorrecciones
    {
        private const string PrefCarpeta = "Director.CarpetaDeCorrecciones";

        [Serializable]
        private class LoteDeCorrecciones
        {
            public string storyboard;
            public CinemaNota[] anotaciones;
        }

        [MenuItem("Liyodev/Director/Aplicar correcciones del storyboard…", false, 2)]
        public static void Abrir()
        {
            string raiz = Directory.GetParent(Application.dataPath).FullName;
            string carpeta = EditorPrefs.GetString(PrefCarpeta, Path.Combine(raiz, "Guiones/_revisiones"));
            if (!Directory.Exists(carpeta)) carpeta = raiz;

            string ruta = EditorUtility.OpenFilePanel("Correcciones del storyboard", carpeta, "json");
            if (string.IsNullOrEmpty(ruta)) return;
            EditorPrefs.SetString(PrefCarpeta, Path.GetDirectoryName(ruta));

            var puente = CinematicsEditorServices.Bridge;
            string resumen = AplicarRuta(ruta);
            if (puente != null) puente.Avisar("Director · correcciones", resumen);
            else EditorUtility.DisplayDialog("Director", resumen, "Aceptar");
        }

        /// <summary>El trabajo, sin ventanas: vale para el menú y para el buzón de órdenes.</summary>
        public static string AplicarRuta(string ruta)
        {
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null) return "Este proyecto no tiene puente de cinemáticas: no hay a quién aplicar la corrección.";

            LoteDeCorrecciones lote;
            try { lote = JsonUtility.FromJson<LoteDeCorrecciones>(File.ReadAllText(ruta)); }
            catch (Exception e) { return "No puedo leer ese fichero: " + e.Message; }

            if (lote == null || lote.anotaciones == null || lote.anotaciones.Length == 0)
                return "Ese fichero no trae anotaciones.";

            try
            {
                var resultado = puente.AplicarRevisiones(lote.storyboard, new List<CinemaNota>(lote.anotaciones));
                CinematicsEditorServices.AvisarCambio();
                return resultado.resumen;
            }
            catch (Exception e) { return "Falla al aplicar: " + e.Message; }
        }
    }
}
