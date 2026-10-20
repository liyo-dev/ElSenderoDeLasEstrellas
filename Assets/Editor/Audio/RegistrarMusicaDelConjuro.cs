#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Registra en el `AudioGraphProfile` las piezas de música del conjuro (INC-798).
///
/// La pista "The Final Bell" se corta en cuatro piezas con duración exacta (el lecho del
/// cántico son 38 s, la estrofa 18 s, la campana 3 s y el golpe con cola 8 s) y se registran
/// con claves propias para poder colocarlas en el guion y bajarlas de nivel sin tocar el
/// resto de la música.
public static class RegistrarMusicaDelConjuro
{
    const string RUTA_PERFIL = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";

    struct Pieza
    {
        public string clave;
        public string ruta;
        public Pieza(string clave, string ruta) { this.clave = clave; this.ruta = ruta; }
    }

    static readonly Pieza[] Piezas =
    {
        new Pieza("Prologue_CoraCanto", "Assets/Audio/Music/Cinematicas/Cora del Cantico.mp3"),
        new Pieza("Prologue_EstrofaCanto", "Assets/Audio/Music/Cinematicas/Estrofa del Cantico.mp3"),
        new Pieza("Prologue_GolpeFinal", "Assets/Audio/SFX_Prologo/Disenados/SFX_Golpe_Final_Campana.wav"),
        new Pieza("Prologue_GolpeFinalCola", "Assets/Audio/SFX_Prologo/Disenados/SFX_Golpe_Final_Cola.wav"),
    };

    [MenuItem("El Sendero/Audio/Registrar la música del conjuro en el perfil")]
    public static void Aplicar()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RUTA_PERFIL);
        if (profile == null)
        {
            Debug.LogError($"[Audio] No se encontró {RUTA_PERFIL}");
            return;
        }

        Undo.RecordObject(profile, "Registrar la música del conjuro");

        int anadidas = 0, actualizadas = 0;
        foreach (var pieza in Piezas)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(pieza.ruta);
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] No se encontró el clip de '{pieza.clave}' en {pieza.ruta}");
                continue;
            }

            var existente = profile.eventSfx.Find(r => r != null &&
                string.Equals(r.eventKey, pieza.clave, System.StringComparison.OrdinalIgnoreCase));
            if (existente != null)
            {
                if (existente.sfx != clip) { existente.sfx = clip; actualizadas++; }
                continue;
            }
            profile.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = pieza.clave, sfx = clip });
            anadidas++;
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] Música del conjuro registrada: {anadidas} claves nuevas, {actualizadas} actualizadas.");
    }

    [MenuItem("El Sendero/Audio/Comprobar la música del conjuro en el perfil")]
    public static void Comprobar()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RUTA_PERFIL);
        if (profile == null) return;
        foreach (var pieza in Piezas)
        {
            var existente = profile.eventSfx.Find(r => r != null &&
                string.Equals(r.eventKey, pieza.clave, System.StringComparison.OrdinalIgnoreCase));
            string estado = existente == null ? "NO REGISTRADA"
                : (existente.sfx == null ? "SIN CLIP" : "ok -> " + existente.sfx.name);
            Debug.Log($"[Audio] {pieza.clave}: {estado}");
        }
    }
}
#endif
