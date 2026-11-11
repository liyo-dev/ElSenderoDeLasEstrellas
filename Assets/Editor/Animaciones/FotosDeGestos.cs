#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tira de fotos de un gesto: el estado del Animator muestreado en varios momentos, de frente y de
/// lado, para revisar una animación sin entrar en Play. Los gestos se piden en
/// «Guiones/_fotos/gestos.txt», uno por línea: el nombre del estado y, si se quiere, la ruta del
/// prefab con el que se hace la foto (por defecto, Will de NPC). Las fotos van a «Guiones/_fotos/»
/// como gesto_&lt;Estado&gt;_&lt;n&gt;_frente.png y _lado.png.
/// </summary>
public static class FotosDeGestos
{
    private const string ModeloPorDefecto = "Assets/Prefabs/_WILL_NPC.prefab";
    private const int Momentos = 6;

    [MenuItem("El Sendero/Personajes/Animaciones/Tira de fotos de los gestos de gestos.txt (prueba)", priority = 140)]
    public static void Fotografiar()
    {
        string lista = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_fotos", "gestos.txt");
        if (!File.Exists(lista)) { OrdenesDelEditor.Avisar("Tira de gestos", "falta " + lista); return; }
        var informe = new StringBuilder();
        foreach (string linea in File.ReadAllLines(lista))
        {
            var partes = linea.Trim().Split(new[] { ' ' }, 2);
            if (partes[0].Length == 0 || partes[0].StartsWith("#")) continue;
            informe.Append(Tira(partes[0], partes.Length > 1 ? partes[1].Trim() : ModeloPorDefecto)).Append(' ');
        }
        OrdenesDelEditor.Avisar("Tira de gestos", informe.ToString());
    }

    private static string Tira(string estado, string rutaDelModelo)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaDelModelo);
        if (prefab == null) return $"{estado}: falta {rutaDelModelo}.";
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        var sitio = new Vector3(0f, 800f, 0f);
        inst.transform.SetPositionAndRotation(sitio, Quaternion.identity);
        try
        {
            var anim = inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController != null);
            var clip = anim != null ? ClipDelEstado(anim.runtimeAnimatorController, estado) : null;
            if (clip == null) return $"{estado}: {prefab.name} no tiene ese estado con un clip.";
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) { a.enabled = true; a.Rebind(); }
            // Fuera de Play las mallas con huesos no se recalculan en cada render.
            foreach (var malla in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                malla.forceMatrixRecalculationPerRender = true;
                malla.updateWhenOffscreen = true;
            }
            var centro = sitio + Vector3.up * 0.6f;
            for (int i = 0; i < Momentos; i++)
            {
                float t = clip.length * (i + 0.5f) / Momentos;
                Horno.MuestrearEstado(inst, clip, estado, t);
                inst.transform.SetPositionAndRotation(sitio, Quaternion.identity);
                CapturaDeEscenario.FotoDesde($"gesto_{estado}_{i}_frente", centro + new Vector3(0.5f, 0.3f, 2.6f), centro, 35f);
                CapturaDeEscenario.FotoDesde($"gesto_{estado}_{i}_lado", centro + new Vector3(2.6f, 0.3f, 0.5f), centro, 35f);
            }
            return $"{estado}: {Momentos} momentos de {clip.name} ({clip.length:0.00} s).";
        }
        finally { Object.DestroyImmediate(inst); }
    }

    /// El clip del estado con ese nombre, en cualquier capa del controlador (también si es un override).
    private static AnimationClip ClipDelEstado(RuntimeAnimatorController controlador, string estado)
    {
        var sustituciones = controlador as AnimatorOverrideController;
        var ctrl = (sustituciones != null ? sustituciones.runtimeAnimatorController : controlador) as AnimatorController;
        if (ctrl == null) return null;
        foreach (var capa in ctrl.layers)
            foreach (var s in capa.stateMachine.states)
                if (s.state.name == estado && s.state.motion is AnimationClip clip)
                    return sustituciones != null ? sustituciones[clip] : clip;
        return null;
    }
}
#endif
