using Sendero.Narrative.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Prueba de humo del arranque de una escena suelta (temporal): deja la escena del templo activa,
/// prepara el arranque con «Todo desbloqueado» y comprueba lo que ha quedado escrito, sin entrar en
/// Play. Deja el GameBootProfile como estaba. Ver INC-1212.
public static class PruebaDeArranqueDeEscena
{
    private const string Escena = "Assets/Scenes/Worlds/TemploQueCanta.unity";
    private const string Perfil = "Assets/_BootProfile/GameBootProfile.asset";
    private const string Jugador = "Assets/ElSendero/Data/Jugadores de prueba/Todo desbloqueado.asset";

    [MenuItem("El Sendero/Escenario/Prueba temporal: preparar el arranque de una escena")]
    public static void Probar()
    {
        var activa = SceneManager.GetActiveScene();
        var escena = EditorSceneManager.OpenScene(Escena, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(escena);

        var perfil = AssetDatabase.LoadAssetAtPath<GameBootProfile>(Perfil);
        var presetOriginal = perfil != null ? perfil.bootPreset : null;
        bool usoOriginal = perfil != null && perfil.usePresetInsteadOfSave;

        var jugador = ScriptableObject.CreateInstance<PlayerPresetSO>();
        jugador.name = "Jugador de la prueba";
        jugador.abilities = new PlayerAbilities { swim = true, jump = true, magic = true, fly = true, sprint = true, shield = true };
        jugador.unlockedSpells = new System.Collections.Generic.List<SpellId> { (SpellId)1 };
        string mensaje = "(no se ha llamado)";
        bool ok = NarrativeTestLauncher.PrepararEscenaAbierta(jugador, out mensaje);

        var temporal = AssetDatabase.LoadAssetAtPath<PlayerPresetSO>("Assets/Editor/QuickTestPreset_Temp.asset");
        string informe = "[PruebaArranque] ok=" + ok + " mensaje=" + mensaje +
                  " | perfil.usePreset=" + perfil.usePresetInsteadOfSave +
                  " | perfil.bootPreset=" + (perfil.bootPreset != null ? perfil.bootPreset.name : "null") +
                  " | ancla=" + (temporal != null ? temporal.spawnAnchorId : "sin temporal") +
                  " | hechizos=" + (temporal != null && temporal.unlockedSpells != null ? temporal.unlockedSpells.Count : -1) +
                  " | vuela=" + (temporal != null && temporal.abilities != null && temporal.abilities.fly) +
                  " | jugador=" + (jugador != null ? jugador.name : "NO ENCONTRADO");
        Debug.Log(informe);
        OrdenesDelEditor.Avisar("Prueba de arranque", informe);

        if (perfil != null)
        {
            perfil.bootPreset = presetOriginal;
            perfil.usePresetInsteadOfSave = usoOriginal;
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();
        }

        if (activa.IsValid() && activa.isLoaded) SceneManager.SetActiveScene(activa);
        EditorSceneManager.CloseScene(escena, true);
    }
}
