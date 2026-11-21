using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// (20 nov, 18:00: la bola ya no se sostiene en una esfera sobre la cabeza; salta hacia arriba y se
/// atrapa en el aire. La esfera y el parámetro de sostener quedan sin usar.)
/// Deja lista la coreografía del contraataque (B en el momento justo contra lo lanzado): el
/// parámetro que congela la pose de sostener en el Animator del grupo, los efectos (esfera que
/// envuelve la bola y polvo en los pies) y el componente <see cref="ContraataqueCoreografiado"/>
/// en el cuerpo común de los tres, con el polvo en lugar de la nube del paquete en
/// <see cref="PresentacionDelContraataque"/>. Se puede repetir. Ver INC-1352.
/// </summary>
public static class MontarContraataqueCoreografiado
{
    private const string Controlador = "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";
    private const string Cuerpo = "Assets/Prefabs/Personajes/_CUERPO_DEL_GRUPO.prefab";
    private const string EstadoDeSostener = "FoundSomething_NoWeapon";

    [MenuItem("El Sendero/Combate/Montar contraataque con coreografía (INC-1352)")]
    public static void Montar()
    {
        var informe = new List<string>();
        PrepararAnimator(informe);

        var log = new System.Text.StringBuilder();
        HechizosConVolumen.ConstruirContraataque(log);
        informe.Add("✓ Efectos: " + log);

        PrepararCuerpo(informe);
        AssetDatabase.SaveAssets();

        string texto = string.Join("\n", informe);
        Debug.Log("[Contraataque] " + texto.Replace("\n", " · "));
        EditorUtility.DisplayDialog("Contraataque con coreografía", texto, "Vale");
    }

    private static void PrepararAnimator(List<string> informe)
    {
        var controlador = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        if (controlador == null) { informe.Add("✗ No encuentro el Animator del grupo."); return; }

        string parametro = ContraataqueCoreografiado.ParametroDeSostenerPorDefecto;
        bool cambiado = false;
        if (!controlador.parameters.Any(p => p.name == parametro))
        {
            controlador.AddParameter(new AnimatorControllerParameter
            {
                name = parametro, type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });
            cambiado = true;
        }

        var estado = AnimatorControllerEstados.Buscar(controlador, EstadoDeSostener);
        if (estado == null) { informe.Add("✗ No encuentro el estado " + EstadoDeSostener + "."); }
        else if (!estado.speedParameterActive || estado.speedParameter != parametro)
        {
            estado.speedParameter = parametro;
            estado.speedParameterActive = true;
            cambiado = true;
        }

        if (cambiado) EditorUtility.SetDirty(controlador);
        informe.Add(cambiado ? $"✓ Animator: la pose de sostener se puede congelar ({parametro})." : "· Animator: ya estaba.");
    }

    private static void PrepararCuerpo(List<string> informe)
    {
        var polvo = AssetDatabase.LoadAssetAtPath<GameObject>(HechizosConVolumen.RutaPolvoDelContraataque);
        var raiz = PrefabUtility.LoadPrefabContents(Cuerpo);
        if (raiz == null) { informe.Add("✗ No encuentro el cuerpo del grupo."); return; }
        try
        {
            var voltereta = raiz.GetComponentInChildren<VolteretaDelJugador>(true);
            if (voltereta == null) { informe.Add("✗ El cuerpo del grupo no tiene VolteretaDelJugador."); return; }

            var coreografia = voltereta.GetComponent<ContraataqueCoreografiado>() ?? voltereta.gameObject.AddComponent<ContraataqueCoreografiado>();
            var so = new SerializedObject(coreografia);
            so.FindProperty("alturaDeLaBola").floatValue = ContraataqueCoreografiado.AlturaDeLaBolaPorDefecto;
            so.FindProperty("estadoDeParar").stringValue = ContraataqueCoreografiado.EstadoDePararPorDefecto;
            so.FindProperty("estadoDeImpulso").stringValue = ContraataqueCoreografiado.EstadoDeImpulsoPorDefecto;
            so.FindProperty("burbujaAlParar").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HechizosConVolumen.RutaBurbujaDelContraataque);
            so.FindProperty("golpeAlParar").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(HechizosConVolumen.RutaGolpeEnElEscudoDeWill);
            so.FindProperty("estadoDeLanzar").stringValue = ContraataqueCoreografiado.EstadoDeLanzarPorDefecto;
            so.ApplyModifiedPropertiesWithoutUndo();

            var presentacion = raiz.GetComponentInChildren<PresentacionDelContraataque>(true);
            if (presentacion != null)
            {
                var sp = new SerializedObject(presentacion);
                sp.FindProperty("ondaEnLosPies").objectReferenceValue = polvo;
                sp.ApplyModifiedPropertiesWithoutUndo();
            }
            else informe.Add("· No hay PresentacionDelContraataque en el cuerpo: el polvo no se ha puesto.");

            PrefabUtility.SaveAsPrefabAsset(raiz, Cuerpo);
            informe.Add("✓ Cuerpo del grupo: coreografía puesta (Will, Estela y Liam); polvo en vez de la nube.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
