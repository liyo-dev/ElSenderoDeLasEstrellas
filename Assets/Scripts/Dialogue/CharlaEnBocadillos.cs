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
    private const float MargenDeCierre = 0.25f;
    private const float EsperaMaximaAlBocadilloAjeno = 4f;

    private readonly DialogueAsset _dialogo;
    private readonly NPCBehaviourManagerV2 _principal;
    private int _linea;
    private bool _mostrando;
    private int _turno;
    private AudioClip _voz;

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
            float voz = VoiceLines.TryPlay(linea.textId);
            if (voz > 0f)
            {
                VoiceLines.TryGet(linea.textId, out _voz);
                duracion = Mathf.Max(duracion, voz + 0.3f);
            }
            _turno = ui.Show(quien, texto, duracion);
            _mostrando = true;

            // Si otro sistema pone su bocadillo encima (o quita el nuestro), la línea no se ha
            // leído: se repite cuando el bocadillo vuelva a estar libre. Ver INC-613.
            float fin = Time.unscaledTime + duracion;
            bool cortada = false;
            bool quitada = false;
            while (Time.unscaledTime < fin)
            {
                if (puedeHablar != null && !puedeHablar()) { cortada = true; break; }
                // El cierre propio por tiempo puede llegar un fotograma antes que 'fin' (otro reloj):
                // solo cuenta como quitado si faltaba un buen trozo.
                if (ui.Turno != _turno || (!ui.Mostrando && fin - Time.unscaledTime > MargenDeCierre))
                { quitada = true; break; }
                yield return null;
            }
            _mostrando = false;

            if (cortada)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                // Diagnóstico de INC-613: se quita al cerrar la incidencia.
                Debug.Log($"[CharlaEnBocadillos] {_dialogo.name}, línea {_linea + 1}: se corta (jugador lejos, " +
                          $"menú abierto o el NPC le está llamando) a los {duracion - (fin - Time.unscaledTime):F1} s de {duracion:F1} s.");
#endif
                PararVoz();
                ui.Hide(_turno);
                continue;
            }
            if (quitada)
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                // Diagnóstico de INC-613: se quita al cerrar la incidencia.
                Debug.LogWarning($"[CharlaEnBocadillos] {_dialogo.name}, línea {_linea + 1}: otro sistema " +
                                 $"{(ui.Turno != _turno ? "ha puesto su bocadillo" : "ha quitado el bocadillo")} " +
                                 $"a los {duracion - (fin - Time.unscaledTime):F1} s de {duracion:F1} s. Se repite.");
#endif
                PararVoz();
                // Se espera a que el otro bocadillo acabe, con tope: uno sin duración no puede
                // dejar la charla (y al nodo que la espera) parada para siempre.
                float limite = Time.unscaledTime + EsperaMaximaAlBocadilloAjeno;
                while (ui.Mostrando && Time.unscaledTime < limite) yield return null;
                yield return new WaitForSecondsRealtime(PausaEntreLineas);
                continue;
            }
            _linea++;
            if (!Terminada) yield return new WaitForSecondsRealtime(PausaEntreLineas);
        }
    }

    /// Quita el bocadillo si lo tiene puesto esta charla (al cortarla desde fuera).
    public void Callar()
    {
        if (!_mostrando) return;
        _mostrando = false;
        PararVoz();
        SpeechBubbleUI.Instance?.Hide(_turno);
    }

    private void PararVoz()
    {
        var audio = AudioService.Instance;
        if (audio != null && audio.IsVoicePlaying(_voz)) audio.StopVoice();
        _voz = null;
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
