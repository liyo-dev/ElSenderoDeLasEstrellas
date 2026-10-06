using System;
using System.Collections.Generic;
using UnityEngine;

/// Resuelve reacciones sin idioma y alterna sus variantes sin repetir la última toma.
public static class VocalReactions
{
    /// Memoria por voz compartida, independiente del número de actores de la secuencia.
    public sealed class EstadoDeSecuencia
    {
        private readonly HashSet<string> _animosGastados = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _ultimaVoz = new(StringComparer.OrdinalIgnoreCase);
        private float _ultimoAnimo = float.NegativeInfinity;

        public bool AnimoGastado(string voz) => _animosGastados.Contains(voz);

        public bool PuedeSonar(string voz, string tipo, float ahora, bool limitarAnimo)
        {
            if (_ultimaVoz.TryGetValue(voz, out float ultima) && ahora - ultima < 4f) return false;
            return tipo != "cheer" || (ahora - _ultimoAnimo >= 20f &&
                (!limitarAnimo || !AnimoGastado(voz)));
        }

        public void Registrar(string voz, string tipo, float ahora)
        {
            _ultimaVoz[voz] = ahora;
            if (tipo != "cheer") return;
            _animosGastados.Add(voz);
            _ultimoAnimo = ahora;
        }
    }

    private sealed class Variants
    {
        public AudioClip[] clips;
        public int last = -1;
    }

    private static readonly Dictionary<string, AudioClip[]> Characters = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Variants> Reactions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] CharacterNames =
    {
        "Will", "Oliver", "Victoria", "Eldran", "Liam", "Archimago", "MagoOscuro", "Liora", "Nina", "Vecino", "Vecina"
    };
#if UNITY_EDITOR
    private static readonly HashSet<string> Missing = new(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Characters.Clear();
        Reactions.Clear();
        Missing.Clear();
    }
#endif

    public static bool TryGet(string character, string kind, out AudioClip clip)
    {
        clip = null;
        if (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(kind)) return false;
        character = character.Trim();
        kind = kind.Trim().ToLowerInvariant();
        foreach (string name in CharacterNames)
        {
            if (!string.Equals(character, name, StringComparison.OrdinalIgnoreCase)) continue;
            character = name;
            break;
        }

        string key = $"{character}/{kind}";
        if (!Reactions.TryGetValue(key, out var variants))
        {
            if (!Characters.TryGetValue(character, out var all))
            {
                all = Resources.LoadAll<AudioClip>($"Voices/reactions/{character}");
                Characters.Add(character, all);
            }
            string prefix = kind + "_";
            var matches = new List<AudioClip>();
            foreach (var candidate in all)
            {
                // El sufijo numérico distingue laugh_01 de laugh_short_01.
                if (candidate == null || !candidate.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string suffix = candidate.name.Substring(prefix.Length);
                if (suffix.Length != 2 || suffix[0] < '0' || suffix[0] > '9'
                    || suffix[1] < '0' || suffix[1] > '9') continue;
                matches.Add(candidate);
            }
            variants = new Variants { clips = matches.ToArray() };
            Reactions.Add(key, variants);
        }

        int count = variants.clips.Length;
        if (count == 0)
        {
#if UNITY_EDITOR
            if (Missing.Add(key))
                Debug.LogWarning($"[VocalReactions] Sin clips para '{key}' en Resources/Voices/reactions/{character}/{kind}_nn.");
#endif
            return false;
        }

        int index = UnityEngine.Random.Range(0, count > 1 && variants.last >= 0 ? count - 1 : count);
        if (count > 1 && variants.last >= 0 && index >= variants.last) index++;
        variants.last = index;
        clip = variants.clips[index];
        return clip != null;
    }
}
