using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// Suena un clip desde el Editor sin entrar en PlayMode: es lo que hace que el flipbook tenga
    /// voces y el animatic algo de música y efectos. Usa `UnityEditor.AudioUtil`, que es API interna
    /// (no documentada): si Unity cambia la firma, esto se queda en silencio y no rompe nada.
    ///
    /// Hay dos canales de previz: las **voces** van por `PlayPreviewClip` (un solo clip a la vez) y la
    /// **música y los efectos** por `PlayClip`. Si el Editor solo tiene un canal, la mesa da prioridad
    /// a la voz y recupera el fondo en el segundo que corresponde.
    internal static class EditorAudioPreview
    {
        private static MethodInfo s_play;
        private static MethodInfo s_stop;
        private static MethodInfo s_playFondo;
        private static MethodInfo s_stopFondo;
        private static bool s_buscado;

        public static bool Disponible
        {
            get { Buscar(); return s_play != null; }
        }

        public static bool HaySegundoCanal
        {
            get { Buscar(); return s_playFondo != null; }
        }

        /// Suena la voz desde `desde` segundos (corta la voz anterior).
        public static void Play(AudioClip clip, float desde, bool enBucle = false)
        {
            if (clip == null) return;
            Buscar();
            if (s_play == null) return;

            int muestra = Mathf.Clamp(Mathf.RoundToInt(desde * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
            Stop();
            try { s_play.Invoke(null, new object[] { clip, muestra, enBucle }); }
            catch { s_play = null; }
        }

        public static void Stop()
        {
            Buscar();
            if (s_stop == null) return;
            try { s_stop.Invoke(null, null); }
            catch { s_stop = null; }
        }

        /// Música y efectos, por el canal de al lado (no corta la voz).
        public static void PlayFondo(AudioClip clip, float desde, bool enBucle = false)
        {
            if (clip == null) return;
            Buscar();
            if (s_playFondo == null) return;

            int muestra = Mathf.Clamp(Mathf.RoundToInt(desde * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
            try { s_playFondo.Invoke(null, new object[] { clip, muestra, enBucle }); }
            catch { s_playFondo = null; }
        }

        public static void StopFondo()
        {
            Buscar();
            if (s_stopFondo == null) return;
            try { s_stopFondo.Invoke(null, null); }
            catch { s_stopFondo = null; }
        }

        private static void Buscar()
        {
            if (s_buscado) return;
            s_buscado = true;

            var tipo = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (tipo == null) return;

            s_play = tipo.GetMethod("PlayPreviewClip", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            s_stop = tipo.GetMethod("StopAllPreviewClips", BindingFlags.Public | BindingFlags.Static, null,
                new System.Type[0], null);

            s_playFondo = tipo.GetMethod("PlayClip", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            s_stopFondo = tipo.GetMethod("StopAllClips", BindingFlags.Public | BindingFlags.Static, null,
                new System.Type[0], null);
        }
    }
}
