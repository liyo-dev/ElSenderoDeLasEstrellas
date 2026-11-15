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

        /// Campos de la partida que se enseñan al probar y puede cambiar un jugador de prueba.
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
