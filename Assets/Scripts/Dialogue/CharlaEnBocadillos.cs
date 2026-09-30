using System;
using System.Collections;
using Game.NPC;
using UnityEngine;

/// Una conversación (DialogueAsset) dicha en bocadillos sobre la cabeza de quien habla mientras el
/// jugador sigue jugando: lo que se cuentan por el camino al seguir a un NPC, por ejemplo. Se
/// escribe como cualquier diálogo (speakerNameId + textId por línea). Solo avanza mientras
/// 'puedeHablar' lo permite (el jugador va cerca, no hay un menú abierto); si una línea se corta a
/// medias, se repite cuando se puede volver a hablar. Ver INC-537.
public sealed class CharlaEnBocadillos
{
    private const float PausaEntreLineas = 0.5f;

    private readonly DialogueAsset _dialogo;
    private readonly NPCBehaviourManagerV2 _principal;
    private int _linea;
    private bool _mostrando;

    /// <param name="principal">NPC con quien es la charla: dice las líneas sin hablante y las suyas.</param>
    public CharlaEnBocadillos(DialogueAsset dialogo, NPCBehaviourManagerV2 principal)
    {
        _dialogo = dialogo;
        _principal = principal;
    }

    public bool Terminada => _dialogo == null || _dialogo.lines == null || _linea >= _dialogo.lines.Length;

    public IEnumerator Co_Hablar(Func<bool> puedeHablar, float pausaInicial)
    {
        if (pausaInicial > 0f) yield return new WaitForSecondsRealtime(pausaInicial);

        while (!Terminada)
        {
            var ui = SpeechBubbleUI.Instance;
            if (ui == null) yield break;
            if (puedeHablar != null && !puedeHablar()) { yield return null; continue; }

            var linea = _dialogo.lines[_linea];
            Transform quien = Hablante(linea);
            string texto = Texto(linea);
            if (quien == null || string.IsNullOrWhiteSpace(texto))
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[CharlaEnBocadillos] {_dialogo.name}, línea {_linea + 1}: " +
                                 $"{(quien == null ? $"no encuentro a '{linea.speakerNameId}'" : "sin texto")}. Se salta.");
#endif
                _linea++;
                continue;
            }

            float duracion = 0f;
            foreach (var pagina in ui.Paginar(texto)) duracion += ui.TiempoDeLectura(pagina);
            ui.Show(quien, texto, duracion);
            _mostrando = true;

            float fin = Time.unscaledTime + duracion;
            bool cortada = false;
            while (Time.unscaledTime < fin)
            {
                if (puedeHablar != null && !puedeHablar()) { cortada = true; break; }
                yield return null;
            }
            _mostrando = false;

            if (cortada) { ui.Hide(); continue; }
            _linea++;
            if (!Terminada) yield return new WaitForSecondsRealtime(PausaEntreLineas);
        }
    }

    /// Quita el bocadillo si lo tiene puesto esta charla (al cortarla desde fuera).
    public void Callar()
    {
        if (!_mostrando) return;
        _mostrando = false;
        SpeechBubbleUI.Instance?.Hide();
    }

    private static string Texto(DialogueLine linea)
    {
        if (string.IsNullOrEmpty(linea.textId)) return linea.text;
        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(linea.textId, linea.text)
            : linea.text;
    }

    /// El jugador si la línea es suya; si no, el NPC principal, un compañero del grupo o cualquier
    /// NPC registrado cuyo DialogueCharacterId o PersistenceId sea el speakerNameId de la línea.
    private Transform Hablante(DialogueLine linea)
    {
        if (linea.isPlayerSpeaking) return PlayerService.PlayerTransform;

        string id = linea.speakerNameId;
        if (string.IsNullOrEmpty(id) || Es(_principal, id))
            return _principal != null ? _principal.transform : null;

        if (PlayerParty.HasInstance)
            foreach (var m in PlayerParty.Instance.Members)
                if (m != null && Es(m.NPCManager, id)) return m.transform;

        if (NPCRegistry.HasInstance)
            foreach (var registrado in NPCRegistry.Instance.GetAllRegisteredIDs())
            {
                var npc = NPCRegistry.Instance.GetNPCByID(registrado);
                if (Es(npc, id)) return npc.transform;
            }
        return null;
    }

    private static bool Es(NPCBehaviourManagerV2 npc, string id)
        => npc != null && (npc.DialogueCharacterId == id || npc.PersistenceId == id);
}
