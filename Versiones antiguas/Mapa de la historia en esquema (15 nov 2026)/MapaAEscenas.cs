using System.Linq;
using NarrativeGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Pasa el mapa de la historia al esquema por escenas: reparte los sitios que ya existen entre
    /// MainWorld, Chuchelandia y El Sendero. Menú de una sola vez.
    public static class MapaAEscenas
    {
        const string Ruta = "Assets/ElSendero/Data/Mapa de la historia.asset";

        [MenuItem("Liyodev/Grafo narrativo/Mapa: pasar a escenas (una vez)")]
        public static void Pasar()
        {
            var mapa = AssetDatabase.LoadAssetAtPath<NarrativeWorldMap>(Ruta);
            if (mapa == null)
            {
                Debug.LogError("[Mapa] No encuentro " + Ruta);
                return;
            }

            mapa.scenes.Clear();
            mapa.scenes.Add(new NarrativeWorldMap.Scene { id = "mainworld", name = "MainWorld", order = 0, color = new Color(0.16f, 0.20f, 0.27f) });
            mapa.scenes.Add(new NarrativeWorldMap.Scene { id = "chuchelandia", name = "Chuchelandia", order = 1, color = new Color(0.30f, 0.16f, 0.22f) });
            mapa.scenes.Add(new NarrativeWorldMap.Scene { id = "sendero", name = "El Sendero", order = 2, color = new Color(0.18f, 0.16f, 0.32f) });

            foreach (var zone in mapa.zones)
            {
                zone.scene = zone.id == "chuche" ? "chuchelandia"
                           : zone.id == "sendero" ? "sendero"
                           : "mainworld";
            }

            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssets();
            Debug.Log("[Mapa] Escenas asignadas: " + string.Join(", ", mapa.scenes.Select(s => s.name)));
        }
    }
}
