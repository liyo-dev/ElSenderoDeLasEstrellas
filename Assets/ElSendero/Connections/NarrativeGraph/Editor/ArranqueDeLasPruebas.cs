using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NarrativeGraphEditor;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Lo que El Sendero enchufa al arranque de pruebas del grafo: la ficha del jugador es el
    /// PlayerPresetSO de partida nueva, el ancla dice dónde aparece y, en Play, la partida en curso.
    public static class ArranqueDeLasPruebas
    {
        public const string RutaDeLaFicha = "Assets/ElSendero/Data/FichaDelJugador.asset";
        public const string CampoDelAncla = nameof(PlayerPresetSO.spawnAnchorId);

        /// Campos de la partida que pueden fijar los puntos de arranque y retocarse al probar.
        static readonly string[] Campos =
        {
            nameof(PlayerPresetSO.spawnAnchorId),
            nameof(PlayerPresetSO.maxHP), nameof(PlayerPresetSO.currentHP),
            nameof(PlayerPresetSO.maxMP), nameof(PlayerPresetSO.currentMP),
            nameof(PlayerPresetSO.ataque), nameof(PlayerPresetSO.defensa),
            nameof(PlayerPresetSO.abilities),
            nameof(PlayerPresetSO.unlockedSpells), nameof(PlayerPresetSO.basicSpellIds),
            nameof(PlayerPresetSO.companionBasics), nameof(PlayerPresetSO.companionHealth),
            nameof(PlayerPresetSO.appearance), nameof(PlayerPresetSO.unlockedWardrobeIds),
        };

        /// El punto de arranque deja al jugador en este ancla.
        public static void FijarAncla(CheckpointNode punto, string ancla)
        {
            if (punto == null || string.IsNullOrEmpty(ancla)) return;
            punto.player ??= new NarrativePlayerValues();
            NarrativePlayerValuesEditor.SetValue(punto.player, typeof(PlayerPresetSO), CampoDelAncla, p => p.stringValue = ancla);
        }

        /// Crea la ficha del jugador (si falta) y pasa a ella el ancla de los puntos de arranque
        /// que la tenían en su campo de antes (spawnAnchorId del propio nodo). La escena sale del ancla.
        [MenuItem("El Sendero/Archivo/Narrativa/Grafo: ficha del jugador y anclas de los puntos de arranque")]
        public static void Montar()
        {
            var informe = new StringBuilder("[Arranque de pruebas]\n");
            var ficha = AssetDatabase.LoadAssetAtPath<NarrativePlayerSheet>(RutaDeLaFicha);
            if (ficha == null)
            {
                var perfil = AssetDatabase.LoadAssetAtPath<GameBootProfile>("Assets/_BootProfile/GameBootProfile.asset");
                ficha = ScriptableObject.CreateInstance<NarrativePlayerSheet>();
                ficha.template = perfil != null ? perfil.defaultPlayerPreset : null;
                ficha.fields = Campos.ToList();
                ficha.positionField = CampoDelAncla;
                AssetDatabase.CreateAsset(ficha, RutaDeLaFicha);
                informe.AppendLine($"Ficha creada en {RutaDeLaFicha} (partida nueva: {(ficha.template != null ? ficha.template.name : "FALTA")}).");
            }

            var bloque = new Regex(@"type: \{class: CheckpointNode[^\n]*\n(?<datos>(?:(?!\n    - rid:)[\s\S])*)");
            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                var texto = File.ReadAllText(ruta);
                var anclas = new Dictionary<string, string>();
                foreach (Match m in bloque.Matches(texto))
                {
                    var datos = m.Groups["datos"].Value;
                    var id = Regex.Match(datos, @"\n\s+guid: (.+)").Groups[1].Value.Trim();
                    var ancla = Regex.Match(datos, @"\n\s+spawnAnchorId: (.*)").Groups[1].Value.Trim();
                    if (id.Length > 0 && ancla.Length > 0) anclas[id] = ancla;
                }
                if (anclas.Count == 0) continue;

                var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(ruta);
                foreach (var par in anclas)
                {
                    if (grafo.FindNode(par.Key) is not CheckpointNode punto) continue;
                    if (punto.player != null && punto.player.Has(CampoDelAncla)) continue;
                    FijarAncla(punto, par.Value);
                    informe.AppendLine($"{grafo.name} · {punto.displayTitle}: ancla {par.Value}");
                }
                EditorUtility.SetDirty(grafo);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(informe.ToString());
        }
    }

    /// En Play, la partida tal y como va ahora (para fijar un punto de arranque con ella).
    public sealed class PartidaEnCursoParaElGrafo : INarrativePlayerLive
    {
        public Object CaptureLive()
        {
            if (!Application.isPlaying) return null;
            Partida.VolcarEstadoDelJuego();
            return Partida.PresetEnMemoria;
        }
    }
}
