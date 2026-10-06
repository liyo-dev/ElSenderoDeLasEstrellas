#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Lo que el guion del prólogo necesita que exista antes de hornearse: sonidos dados de alta en el
/// perfil de audio, los VFX propios (agujero negro y luz protectora), los perfiles de
/// post-procesado y el aspecto de Nina. Todo es idempotente: lo que ya está bien no se toca.
///
/// Lo llama el horneado gracias a la cabecera «PREPARA RecursosDelPrologo» del guion. Sale del
/// constructor viejo del prólogo (retirado el 5 oct 2026), sin las capas de parches.
public static class RecursosDelPrologo
{
    private const string RutaPerfilDeAudio = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private const string CarpetaPack = "Assets/Audio/FREE SOUND PACK_TM(355)/";
    public const string RutaAtardecer = "Assets/Art/World/Prologo_Valle/Prologo_Atardecer.asset";
    public const string RutaNina = "Assets/_NPCs/NonInteractable/Nina_Prologo.prefab";

    private static readonly (string clave, string ruta)[] Sonidos =
    {
        ("Ambience_Prologo_Pueblo", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Pueblo.mp3"),
        ("Ambience_Prologo_Rio", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Rio.mp3"),
        ("Ambience_Prologo_Tormenta", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Tormenta.mp3"),
        ("Ambience_Prologo_Panico", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Panico.mp3"),
        ("SFX_Prologo_AgujeroNegro_Carga", "Assets/Audio/SFX_Prologo/SFX_AgujeroNegro_Carga.mp3"),
        ("SFX_Prologo_AgujeroNegro_Bucle", "Assets/Audio/SFX_Prologo/SFX_AgujeroNegro_Bucle.mp3"),
        ("SFX_Prologo_Choque", "Assets/Audio/SFX_Prologo/SFX_Choque_Final.mp3"),
        ("SFX_Prologo_GritoMago", "Assets/Audio/SFX_Prologo/Voz_MagoOscuro_Grito.mp3"),
        ("SFX_Prologo_CutIn", CarpetaPack + "Weapons(25)/Weapon_Whoosh 06.wav"),
        ("SFX_Prologo_GolpeCine", CarpetaPack + "Horror(13)/Cinematic_Hits-013.wav"),
        ("SFX_Prologo_Explosion", CarpetaPack + "Misc(58)/Big_Explosion-002.wav"),
        ("SFX_Prologo_Globo", CarpetaPack + "Misc(58)/Balloon_Pop-003.wav"),
        ("SFX_Prologo_Viento", CarpetaPack + "Ambiences(43)/Wind-009.wav"),
        ("SFX_Prologo_Grito", CarpetaPack + "Horror(13)/SCREAM- Woman 3.wav"),
        ("SFX_Prologo_Impacto", CarpetaPack + "Horror(13)/Cinematic_Hits-017.wav"),
        ("SFX_Prologo_Latigazo", CarpetaPack + "Weapons(25)/Weapon_Whoosh 09.wav"),
        ("SFX_Prologo_Madera_Golpe1", CarpetaPack + "Misc(58)/Wood_Hit-022.wav"),
        ("SFX_Prologo_Madera_Golpe2", CarpetaPack + "Misc(58)/Wood_Hit-023.wav"),
        ("SFX_Prologo_Madera_Rascar", CarpetaPack + "Misc(58)/Wood_Scratching-001.wav"),
        ("SFX_Prologo_Subida", "Assets/Audio/SFX_Prologo/SFX_Prologo_Subida.wav"),
        ("SFX_Prologo_ForcejeoBucle", "Assets/Audio/SFX_Prologo/SFX_Prologo_ForcejeoBucle.wav"),
        ("SFX_Prologo_CoroBucle", "Assets/Audio/SFX_Prologo/SFX_Prologo_CoroBucle.wav"),
        ("SFX_Prologo_EscudoNace", "Assets/Audio/SFX_Prologo/SFX_Prologo_EscudoNace.wav"),
        // Diseñados para el prólogo (5 oct 2026, INC-592): truenos y hechizos con más cuerpo.
        ("SFX_Prologo_Trueno_1", "Assets/Audio/SFX_Prologo/Disenados/SFX_Trueno_Cercano_1.wav"),
        ("SFX_Prologo_Trueno_2", "Assets/Audio/SFX_Prologo/Disenados/SFX_Trueno_Cercano_2.wav"),
        ("SFX_Prologo_Trueno_3", "Assets/Audio/SFX_Prologo/Disenados/SFX_Trueno_Cercano_3.wav"),
        ("SFX_Prologo_Trueno_Lejano", "Assets/Audio/SFX_Prologo/Disenados/SFX_Trueno_Lejano_Rodante.wav"),
        ("Prologo_BolaDeFuego", "Assets/Audio/SFX_Prologo/Disenados/SFX_Ki_Disparo_Oscuro.wav"),
        ("Prologo_HechizoArchimago", "Assets/Audio/SFX_Prologo/Disenados/SFX_Ki_Disparo_Luz.wav"),
        ("Prologo_ImpactoHechizo", "Assets/Audio/SFX_Prologo/Disenados/SFX_Impacto_DBZ.wav"),
        ("Prologo_EscudoBloquea", "Assets/Audio/SFX_Prologo/Disenados/SFX_Escudo_Bloquea.wav"),
        ("Prologo_Carga", "Assets/Audio/SFX_Prologo/Disenados/SFX_Ki_Carga.wav"),
        ("Prologo_Choque", "Assets/Audio/SFX_Prologo/Disenados/SFX_Choque_Hechizos_DBZ.wav"),
        ("Prologo_SueloSeAbre", "Assets/Audio/SFX_Prologo/Disenados/SFX_Suelo_Se_Abre.wav"),
        ("Prologo_Despegue", "Assets/Audio/SFX_Prologo/Disenados/SFX_Despegue.wav"),
        ("Prologo_HechizoGrande", "Assets/Audio/SFX_Prologo/Disenados/SFX_Hechizo_Grande.wav"),
        ("Prologo_ProteccionAbsoluta", "Assets/Audio/SFX_Prologo/Disenados/SFX_Proteccion_Absoluta.wav"),
        ("Prologue_Explosion", "Assets/Audio/SFX_Prologo/Disenados/SFX_Explosion_Final.wav"),
        ("Prologo_Golpe", "Assets/Audio/SFX_Prologo/Disenados/SFX_Golpe_Mago.wav"),
        // Estilo anime clásico (Dragon Ball / Slayers), 5 oct 2026, INC-593.
        ("SFX_Prologo_LuchaFinal", "Assets/Audio/SFX_Prologo/Disenados/SFX_Lucha_Final.wav"),
        ("SFX_Prologo_ExplosionFinal", "Assets/Audio/SFX_Prologo/Disenados/SFX_Explosion_Final_DBZ.wav"),
        ("SFX_Prologo_SilencioBlanco", "Assets/Audio/SFX_Prologo/Disenados/SFX_Silencio_Blanco.wav"),
    };

    public static void Asegurar()
    {
        AsegurarSonidos();
        try { CrearAgujeroNegro.Asegurar(); } catch (System.Exception ex) { Debug.LogWarning("[Prólogo] Agujero negro: " + ex.Message); }
        try { CrearLuzProtectora.Asegurar(); } catch (System.Exception ex) { Debug.LogWarning("[Prólogo] Luz protectora: " + ex.Message); }
        try { PrologoPostprocesoSueno.CrearPerfiles(); } catch (System.Exception ex) { Debug.LogWarning("[Prólogo] Perfiles: " + ex.Message); }
        AsegurarAtardecer();
        AsegurarNina();
        AssetDatabase.SaveAssets();
    }

    private static void AsegurarSonidos()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfilDeAudio);
        if (perfil == null) { Debug.LogWarning($"[Prólogo] No encuentro el perfil de audio en '{RutaPerfilDeAudio}'."); return; }
        bool cambiado = false;
        foreach (var (clave, ruta) in Sonidos)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (clip == null) { Debug.LogWarning($"[Prólogo] Falta el sonido '{ruta}' para la clave '{clave}'."); continue; }
            var existente = perfil.eventSfx.Find(e => e != null && string.Equals(e.eventKey, clave, System.StringComparison.OrdinalIgnoreCase));
            if (existente != null) { if (existente.sfx == clip) continue; existente.sfx = clip; }
            else perfil.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = clave, sfx = clip });
            cambiado = true;
        }
        if (cambiado) EditorUtility.SetDirty(perfil);
    }

    private static void AsegurarAtardecer()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaAtardecer);
        if (perfil == null)
        {
            if (!AssetDatabase.CopyAsset(PrologoPostprocesoSueno.Dia, RutaAtardecer)) { Debug.LogWarning("[Prólogo] No puedo copiar el perfil de día para el atardecer."); return; }
            perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaAtardecer);
        }
        T Componente<T>() where T : VolumeComponent
        {
            if (!perfil.TryGet<T>(out var c)) { c = perfil.Add<T>(true); AssetDatabase.AddObjectToAsset(c, perfil); }
            c.active = true;
            EditorUtility.SetDirty(c);
            return c;
        }
        var blanco = Componente<UnityEngine.Rendering.Universal.WhiteBalance>();
        blanco.temperature.Override(48f);
        blanco.tint.Override(14f);
        var color = Componente<UnityEngine.Rendering.Universal.ColorAdjustments>();
        color.colorFilter.Override(new Color(1f, 0.8f, 0.62f));
        color.saturation.Override(14f);
        color.contrast.Override(10f);
        color.postExposure.Override(0.05f);
        var tonos = Componente<UnityEngine.Rendering.Universal.SplitToning>();
        tonos.shadows.Override(new Color(0.42f, 0.25f, 0.55f));
        tonos.highlights.Override(new Color(1f, 0.62f, 0.32f));
        tonos.balance.Override(-15f);
        var brillo = Componente<UnityEngine.Rendering.Universal.Bloom>();
        brillo.intensity.Override(0.7f);
        brillo.threshold.Override(0.85f);
        brillo.tint.Override(new Color(1f, 0.75f, 0.5f));
        var vineta = Componente<UnityEngine.Rendering.Universal.Vignette>();
        vineta.intensity.Override(0.3f);
        vineta.color.Override(new Color(0.25f, 0.08f, 0.04f));
        EditorUtility.SetDirty(perfil);
    }

    /// Nina (NPC_Aldeano_05) es una niña del pueblo: coletas (Hair08), ropa de diario (Body04),
    /// sin capa ni sombrero. Se monta sobre la aldeana genérica activando piezas del propio pack.
    private static void AsegurarNina()
    {
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset");
        var nina = roster != null ? roster.entries.Find(e => e != null && e.persistenceId == "NPC_Aldeano_05") : null;
        if (nina == null) { Debug.LogWarning("[Prólogo] No encuentro a Nina (NPC_Aldeano_05) en el roster del valle."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaNina);
        if (prefab == null)
        {
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("232061d19d07fce4a801b15fcf95b75c"));
            if (original == null) { Debug.LogWarning("[Prólogo] Falta la aldeana genérica MC01 (1) 6 para hacer a Nina."); return; }
            var instancia = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(original));
            try
            {
                bool pelo = false, cuerpo = false;
                foreach (var t in instancia.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name;
                    if (n.StartsWith("Hair") && n.Length == 6) { t.gameObject.SetActive(n == "Hair08"); pelo |= n == "Hair08"; }
                    else if (n.StartsWith("Body") && n.Length == 6) { t.gameObject.SetActive(n == "Body04"); cuerpo |= n == "Body04"; }
                    else if (n.StartsWith("Cloak") || n.StartsWith("Hat") || n.StartsWith("HeadArmor") || n.StartsWith("AC0"))
                        t.gameObject.SetActive(false);
                }
                if (!pelo || !cuerpo) { Debug.LogWarning("[Prólogo] La aldeana genérica no tiene Hair08 o Body04."); return; }
                prefab = PrefabUtility.SaveAsPrefabAsset(instancia, RutaNina);
            }
            finally { PrefabUtility.UnloadPrefabContents(instancia); }
        }
        if (prefab != null && nina.prefab != prefab)
        {
            Undo.RecordObject(roster, "Nina con su aspecto propio");
            nina.prefab = prefab;
            EditorUtility.SetDirty(roster);
        }
    }
}
#endif
