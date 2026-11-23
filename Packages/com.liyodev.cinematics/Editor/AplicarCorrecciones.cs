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
        [Serializable]
        private class LoteDeCorrecciones
        {
            public string storyboard;
            public CinemaNota[] anotaciones;
        }

        /// Importa una revisión antigua por el buzón de órdenes.
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
