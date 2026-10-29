using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Aplica los valores serializados de la defensa y comprueba las rutas de sus gestos.</summary>
public static class AjustesDeLaB
{
    private const string Prefab = "Assets/Prefabs/_WILL.prefab";
    [MenuItem("El Sendero/Combate/Aplicar ajustes nuevos de la B (INC-689)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Sal de Play para ajustar la B."); return; }
        var raiz = PrefabUtility.LoadPrefabContents(Prefab);
        var informe = new StringBuilder("Ajustes de la B (INC-689):\n");
        try
        {
            var escudo = raiz.GetComponentInChildren<PlayerShieldController>(true);
            if (escudo == null) throw new System.InvalidOperationException("_WILL no contiene PlayerShieldController.");
            var so = new SerializedObject(escudo);
            Fijar(so, "parryWindow", 0.25f, informe);
            Fijar(so, "segundosDeAtrapado", 0.3f, informe);
            Fijar(so, "parryRadius", 1.2f, informe);
            Fijar(so, "margenLateral", 1.2f, informe);
            Fijar(so, "preparacionAnimation", "UpperBody.Magic.HumanM@MagicAttackDirect2H01 - Load", informe);
            Fijar(so, "locomotionAnimation", "UpperBody.UpperIdle", informe);
            Fijar(so, "defendAnimation", "UpperBody.Defend_NoWeapon", informe);
            Fijar(so, "defendHitAnimation", "UpperBody.DefendHit_NoWeapon", informe);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(escudo);
            var caster = raiz.GetComponentInChildren<MagicCaster>(true);
            if (caster != null)
            {
                var magia = new SerializedObject(caster);
                Fijar(magia, "twoHandedState", "UpperBody.Magic.HumanM@MagicAttackDirect2H01 - Cast", informe);
                magia.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(caster);
            }
            var animator = escudo.GetComponent<Animator>();
            var controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
            if (controller != null)
                foreach (var ruta in new[] { "UpperBody.Magic.HumanM@MagicAttackDirect2H01 - Load", "UpperBody.Magic.HumanM@MagicAttackDirect2H01 - Cast", "UpperBody.UpperIdle", "UpperBody.Defend_NoWeapon", "UpperBody.DefendHit_NoWeapon" })
                {
                    bool existe = false;
                    foreach (var capa in controller.layers) existe |= Existe(capa.stateMachine, capa.name, ruta);
                    if (!existe) throw new System.InvalidOperationException("Estado ausente: " + ruta);
                    informe.AppendLine("Estado comprobado: " + ruta);
                }
            else throw new System.InvalidOperationException("No se puede comprobar el Animator del escudo.");
            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            AssetDatabase.SaveAssets();
            Debug.Log(informe.ToString());
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    private static bool Existe(AnimatorStateMachine maquina, string prefijo, string ruta)
    {
        foreach (var estado in maquina.states) if (prefijo + "." + estado.state.name == ruta) return true;
        foreach (var hija in maquina.stateMachines) if (Existe(hija.stateMachine, prefijo + "." + hija.stateMachine.name, ruta)) return true;
        return false;
    }
    private static void Fijar(SerializedObject so, string campo, float valor, StringBuilder informe)
    {
        var p = so.FindProperty(campo);
        informe.AppendLine(campo + ": " + p.floatValue + " → " + valor);
        p.floatValue = valor;
    }
    private static void Fijar(SerializedObject so, string campo, string valor, StringBuilder informe)
    {
        var p = so.FindProperty(campo);
        informe.AppendLine(campo + ": " + p.stringValue + " → " + valor);
        p.stringValue = valor;
    }
}
