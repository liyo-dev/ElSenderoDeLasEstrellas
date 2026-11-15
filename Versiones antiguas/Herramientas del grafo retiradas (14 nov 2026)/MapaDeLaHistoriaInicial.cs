using NarrativeGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Primer esquema del mundo de El Sendero para el mapa de la historia: la isla vista desde
    /// arriba de fondo, las zonas encima (con sus marcas) y los personajes con su sitio de siempre.
    /// Después se retoca a mano en el mapa («Editar mapa»).
    public static class MapaDeLaHistoriaInicial
    {
        const string Ruta = "Assets/ElSendero/Data/Mapa de la historia.asset";

        [MenuItem("El Sendero/Archivo/Narrativa/Mapa de la historia: primer esquema")]
        public static void Crear()
        {
            var mapa = AssetDatabase.LoadAssetAtPath<NarrativeWorldMap>(Ruta);
            if (mapa == null) { mapa = ScriptableObject.CreateInstance<NarrativeWorldMap>(); AssetDatabase.CreateAsset(mapa, Ruta); }
            mapa.zones.Clear();
            // Fondo: la vista desde arriba de MainWorld (900×900 dentro del lienzo de 1400×900, centrada).
            mapa.background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ElSendero/Data/Mapa de la historia - vista.png");
            mapa.characters.Clear();

            Zona(mapa, "casa", "Casa de Will", 525, 535, 170, 110, false, "#378ADD", "Bedroom", "M_Manana_EldranPuerta", "Eldran_JuntoACasaDeWill");
            Zona(mapa, "pueblo", "El pueblo", 615, 440, 170, 110, false, "#1D9E75", "Cap2_VictoriaCharla", "Cap2_OliverCharla", "Eldran_PuntoGuardado");
            Zona(mapa, "bosque", "Bosque prohibido", 380, 345, 170, 110, true, "#639922", "Eldran_Bosque");
            Zona(mapa, "salida", "Salida del pueblo", 725, 560, 170, 110, true, "#BA7517", "Eldran_SalidaDelPueblo", "Will_SalidaDelPueblo");
            Zona(mapa, "torre", "Torre de Liam", 805, 190, 170, 110, false, "#7F77DD");
            Zona(mapa, "cruce", "El cruce", 485, 645, 170, 110, false, "#888780", "Cap3_Puesto");
            Zona(mapa, "puerto", "Camino del puerto", 805, 675, 170, 110, true, "#888780", "Cap3_AcampadaCamino", "Cap3_Acampada", "Cap3_Amanecer", "Cap3_Sigilo_Fin");
            Zona(mapa, "aldea", "Aldea del mar", 855, 435, 170, 110, true, "#185FA5");
            Zona(mapa, "capital", "La capital", 620, 140, 170, 110, false, "#D85A30");
            Zona(mapa, "reino", "Camino del Reino", 985, 470, 170, 110, true, "#5F5E5A", "Cap4_Campana_Salida", "Cap4_LiamLoma", "Cap4_Golem_Entrada", "Cap4_LiamSaliente", "Cap4_LiamBifurcacion", "Cap4_Manzana", "Cap4_Bota");
            Zona(mapa, "chuche", "Chuchelandia", 1010, 705, 170, 110, true, "#D4537E");
            Zona(mapa, "sendero", "El Sendero", 320, 95, 170, 110, true, "#534AB7");

            Pj(mapa, "Player", "Will", "#378ADD", "casa", true);
            Pj(mapa, "NPC_Eldran", "Eldran", "#7F77DD", "casa", true);
            Pj(mapa, "_LIAM", "Liam", "#639922", "torre", true);
            Pj(mapa, "NPC_Erika", "Erika", "#D85A30", "pueblo", true);
            Pj(mapa, "NPC_Oliver", "Oliver", "#1D9E75", "pueblo", true);
            Pj(mapa, "NPC_Victoria", "Victoria", "#D4537E", "pueblo", false);
            Pj(mapa, "NPC_Boticaria", "Boticaria", "#97C459", "pueblo", false);
            Pj(mapa, "NPC_Tomasa", "Tomasa", "#EF9F27", "pueblo", false);
            Pj(mapa, "NPC_Lucia", "Lucía", "#ED93B1", "aldea", false);
            Pj(mapa, "NPC_Posadero", "Posadero", "#BA7517", "aldea", false);
            foreach (var v in new[] { "Anselmo", "Benito", "Eusebio", "Fermin", "Remedios", "Rosario", "Engracia" })
                Pj(mapa, "NPC_" + v, v == "Fermin" ? "Fermín" : v, "#85B7EB", "aldea", false);
            Pj(mapa, "NPC_Sargento", "Sargento", "#5F5E5A", "cruce", false);
            Pj(mapa, "NPC_GuardiaCruce1", "Guardia 1", "#888780", "cruce", false);
            Pj(mapa, "NPC_GuardiaCruce2", "Guardia 2", "#888780", "cruce", false);
            Pj(mapa, "NPC_ContrabandistaJefe", "Jefe contrabandista", "#A32D2D", "puerto", false);
            Pj(mapa, "NPC_Contrabandista1", "Contrabandista 1", "#E24B4A", "puerto", false);
            Pj(mapa, "NPC_Contrabandista2", "Contrabandista 2", "#E24B4A", "puerto", false);
            Pj(mapa, "NPC_Renard", "Renard", "#993C1D", "capital", false);

            EditorUtility.SetDirty(mapa);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mapa;
        }

        static void Zona(NarrativeWorldMap m, string id, string nombre, float x, float y, float w, float h, bool redonda, string color, params string[] marcas)
        {
            ColorUtility.TryParseHtmlString(color, out var c);
            var z = new NarrativeWorldMap.Zone { id = id, name = nombre, rect = new Rect(x, y, w, h), round = redonda, color = c };
            z.marks.AddRange(marcas);
            m.zones.Add(z);
        }

        static void Pj(NarrativeWorldMap m, string actor, string nombre, string color, string zona, bool principal)
        {
            ColorUtility.TryParseHtmlString(color, out var c);
            m.characters.Add(new NarrativeWorldMap.Character { actorId = actor, name = nombre, color = c, homeZone = zona, main = principal });
        }
    }
}

namespace Sendero.Narrative.Editor
{
    /// Rehace el fondo del mapa de la historia con la vista desde arriba de las escenas abiertas
    /// (MainWorld), que además deja cada marca en su sitio exacto.
    public static class VistaDelMapaDeLaHistoria
    {
        [UnityEditor.MenuItem("El Sendero/Archivo/Narrativa/Mapa de la historia: vista desde arriba (escena abierta)")]
        public static void Hacer()
        {
            var mapa = UnityEditor.AssetDatabase.LoadAssetAtPath<NarrativeGraphEditor.NarrativeWorldMap>("Assets/ElSendero/Data/Mapa de la historia.asset");
            if (mapa != null) NarrativeGraphEditor.NarrativeWorldMapBaker.TopDown(mapa);
        }
    }
}

namespace Sendero.Narrative.Editor
{
    /// Pone de fondo del mapa la planta de MainWorld de la captura de escenario
    /// (Guiones/MainWorld_Mundo/escenario/planta_general.png, X −640…640, Z −700…580).
    public static class FondoDelMapaDeLaHistoria
    {
        [System.Serializable] sealed class Marca { public string nombre; public float[] pos; }
        [System.Serializable] sealed class Personaje { public string id; public string objeto; public float[] pos; }
        [System.Serializable] sealed class Escenario { public Marca[] marcas = new Marca[0]; public Personaje[] personajes = new Personaje[0]; }

        [UnityEditor.MenuItem("El Sendero/Archivo/Narrativa/Mapa de la historia: fondo con la planta de MainWorld")]
        public static void Poner()
        {
            var mapa = UnityEditor.AssetDatabase.LoadAssetAtPath<NarrativeGraphEditor.NarrativeWorldMap>("Assets/ElSendero/Data/Mapa de la historia.asset");
            if (mapa == null) return;
            UnityEditor.AssetDatabase.ImportAsset("Assets/ElSendero/Data/Mapa de la historia - vista.png");
            mapa.background = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>("Assets/ElSendero/Data/Mapa de la historia - vista.png");
            mapa.hasWorldRect = true;
            mapa.worldRect = new UnityEngine.Rect(-640f, -700f, 1280f, 1280f);
            // Dónde está cada marca y cada personaje, de la misma captura (así sirve sin MainWorld abierta).
            var json = System.IO.File.ReadAllText("Guiones/MainWorld_Mundo/escenario/escenario.json");
            var escenario = UnityEngine.JsonUtility.FromJson<Escenario>(json);
            mapa.markSpots = new System.Collections.Generic.List<NarrativeGraphEditor.NarrativeWorldMap.Spot>();
            foreach (var m in escenario.marcas)
                mapa.markSpots.Add(new NarrativeGraphEditor.NarrativeWorldMap.Spot { id = m.nombre, position = new UnityEngine.Vector3(m.pos[0], m.pos[1], m.pos[2]) });
            mapa.actorSpots = new System.Collections.Generic.List<NarrativeGraphEditor.NarrativeWorldMap.Spot>();
            foreach (var p in escenario.personajes)
            {
                var id = !string.IsNullOrEmpty(p.id) ? p.id : "NPC_" + p.objeto;
                mapa.actorSpots.Add(new NarrativeGraphEditor.NarrativeWorldMap.Spot { id = id, position = new UnityEngine.Vector3(p.pos[0], p.pos[1], p.pos[2]) });
            }
            UnityEditor.EditorUtility.SetDirty(mapa);
            UnityEditor.AssetDatabase.SaveAssets();
        }
    }
}
