using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// Comprueba que el paquete sigue siendo independiente y que el proyecto contesta: el Director no
    /// puede nombrar al grafo, ni a ningún ensamblado del juego, ni a sus datos. Se puede lanzar desde
    /// el buzón de órdenes (`menu Liyodev/Director/Herramientas/Comprobar el kit`).
    public static class CinematicsKitQa
    {
        private static readonly string[] Prohibidas =
        {
            "El Sendero",
            "ElSendero",
            "Liyodev.NarrativeGraph",
            "Liyodev.NarrativeGraph.Editor",
            "Assembly-CSharp",
            "SequencePlayer",
            "SequenceDefinition",
            "GuionHorneado",
        };

        [MenuItem("Liyodev/Director/Herramientas/Comprobar el kit", false, 100)]
        public static void Comprobar()
        {
            var problemas = new List<string>();
            string raiz = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Packages/com.liyodev.cinematics");

            if (!Directory.Exists(raiz))
            {
                Informe(new List<string> { "No encuentro el paquete en Packages/com.liyodev.cinematics." }, 0);
                return;
            }

            foreach (string archivo in Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories))
            {
                // El propio comprobador nombra lo prohibido para poder buscarlo.
                if (Path.GetFileName(archivo) == "CinematicsKitQa.cs") continue;

                string[] lineas;
                try { lineas = File.ReadAllLines(archivo); }
                catch { continue; }

                for (int i = 0; i < lineas.Length; i++)
                    foreach (string prohibida in Prohibidas)
                        if (lineas[i].Contains(prohibida, StringComparison.Ordinal))
                            problemas.Add($"{Path.GetFileName(archivo)}:{i + 1} nombra «{prohibida}»");
            }

            int cinematicas = 0;
            var puente = CinematicsEditorServices.Bridge;
            if (puente == null)
            {
                problemas.Add("No hay puente: este proyecto no sabrá listar sus cinemáticas.");
            }
            else
            {
                try { cinematicas = puente.Listar().Count; }
                catch (Exception e) { problemas.Add("El puente falla al listar: " + e.Message); }
            }

            Informe(problemas, cinematicas);
        }

        private static void Informe(List<string> problemas, int cinematicas)
        {
            string resumen = problemas.Count == 0
                ? $"Kit independiente. {cinematicas} cinemáticas a la vista."
                : $"{problemas.Count} problema(s). {cinematicas} cinemáticas a la vista.";

            string detalle = problemas.Count == 0 ? resumen : resumen + "\n" + string.Join("\n", problemas);
            CinematicsEditorServices.Bridge?.Avisar("Director · comprobar el kit", detalle);

            if (problemas.Count == 0) Debug.Log("[Director] " + resumen);
            else Debug.LogWarning("[Director] " + detalle);
        }
    }
}
