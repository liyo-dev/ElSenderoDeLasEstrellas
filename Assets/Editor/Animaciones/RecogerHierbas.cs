#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Recoger hierbas en el campo, para NPCs humanoides (la boticaria): de pie y agachada sobre la mata
/// (el tronco se inclina), la mano derecha baja a la mata, arranca, sube y deja lo cogido en el lado
/// izquierdo (la cesta), mientras la izquierda sujeta la cesta junto a la cadera. Parte del reposo
/// Idle01 del pack: con las cabezas grandes, las poses de rodillas del pack (Loot01) quedan como una
/// bola. Lo hace <see cref="PoseSobreClip.CrearConMusculos"/> y queda como estado
/// «RecogerHierbas_Loop» de la capa base de NPC_NoWeapon: la faena en el sitio de NPCWorkRoute lo
/// sostiene. Ver INC-1034.
public static class RecogerHierbas
{
    public const string Estado = "RecogerHierbas_Loop";
    public const string RutaDelClip = "Assets/Art/Animations/Propias/RecogerHierbas.anim";
    private const string Controlador = "Assets/Art/Characters/Animator/NPC_NoWeapon.controller";
    private const string ClipBase = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx";
    private const string ModeloDeLasFotos = "Assets/_NPCs/_GrafoNarrativo/Boticaria.prefab";
    private const float Duracion = 3.4f;

    // Claves del gesto (segundos): quieta, baja a la mata, la sujeta, tira, la echa en la cesta, vuelve.
    private const float Mata = 0.8f, Sujeta = 1.2f, Tira = 1.55f, Cesta = 2.2f, Suelta = 2.6f;

    /// Cuánto se mueve cada músculo respecto al reposo Idle01 en cada clave. Front-Back negativo es hacia
    /// delante (comprobado en las fotos); Down-Up positivo, hacia arriba; Stretch positivo, estirado; Twist Left-Right negativo, a su
    /// izquierda.
    private static readonly Dictionary<string, (float t, float delta)[]> Musculos = new()
    {
        // El tronco se inclina sobre la mata y gira un poco hacia la cesta al echar lo cogido.
        ["Spine Front-Back"] = new[] { (0f, -0.15f), (Mata, -0.6f), (Tira, -0.5f), (Cesta, -0.2f), (Duracion, -0.15f) },
        ["Chest Front-Back"] = new[] { (0f, -0.1f), (Mata, -0.45f), (Tira, -0.35f), (Cesta, -0.15f), (Duracion, -0.1f) },
        ["Chest Twist Left-Right"] = new[] { (0f, 0f), (Tira, 0f), (Cesta, -0.35f), (Suelta, -0.3f), (Duracion, 0f) },
        ["Head Nod Down-Up"] = new[] { (0f, -0.15f), (Mata, -0.35f), (Cesta, -0.2f), (Duracion, -0.15f) },
        // Mano derecha: baja a la mata, arranca y sube, la lleva a la cesta (lado izquierdo).
        ["Right Arm Front-Back"] = new[] { (0f, -0.1f), (Mata, -0.75f), (Sujeta, -0.75f), (Tira, -0.6f), (Cesta, -0.55f), (Suelta, -0.5f), (Duracion, -0.1f) },
        ["Right Arm Down-Up"] = new[] { (0f, 0f), (Mata, 0.1f), (Sujeta, 0.1f), (Tira, 0.35f), (Cesta, 0.15f), (Suelta, 0.15f), (Duracion, 0f) },
        ["Right Forearm Stretch"] = new[] { (0f, 0f), (Mata, 0.6f), (Sujeta, 0.6f), (Tira, -0.2f), (Cesta, -0.5f), (Suelta, -0.4f), (Duracion, 0f) },
        ["Right Arm Twist In-Out"] = new[] { (0f, 0f), (Tira, 0f), (Cesta, 0.4f), (Suelta, 0.4f), (Duracion, 0f) },
        ["Right Hand Down-Up"] = new[] { (0f, 0f), (Mata, -0.4f), (Tira, 0.3f), (Cesta, -0.2f), (Duracion, 0f) },
        // Mano izquierda: sujeta la cesta junto a la cadera, con el codo doblado.
        ["Left Arm Front-Back"] = new[] { (0f, -0.35f), (Cesta, -0.45f), (Duracion, -0.35f) },
        ["Left Forearm Stretch"] = new[] { (0f, -0.55f), (Cesta, -0.45f), (Duracion, -0.55f) },
    };

    [MenuItem("El Sendero/Personajes/Animaciones/Recoger hierbas: crear el clip y su estado", priority = 132)]
    public static void Crear()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controlador);
        var base_ = AssetDatabase.LoadAllAssetsAtPath(ClipBase).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (ctrl == null || base_ == null)
        {
            OrdenesDelEditor.Avisar("Recoger hierbas", $"falta {(ctrl == null ? Controlador : ClipBase)}");
            return;
        }

        PoseSobreClip.CrearConMusculos(base_, RutaDelClip, Duracion, Musculos);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RutaDelClip);
        bool nuevo = PoseSobreClip.EstadoEnBucle(ctrl, Estado, clip);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        string fotos = Fotografiar(clip);
        OrdenesDelEditor.Avisar("Recoger hierbas", $"clip {RutaDelClip} ({clip.length:0.00} s); " +
            $"{ctrl.name}: {Estado} {(nuevo ? "añadido" : "puesto al día")}. Fotos: {fotos}");
    }

    /// Fotos del gesto en cuatro momentos, de lado y de frente, con la boticaria y su controlador (el
    /// mismo camino que en juego: el estado del Animator), para revisarlo sin entrar en Play.
    private static string Fotografiar(AnimationClip clip)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloDeLasFotos);
        if (prefab == null) return "(falta " + ModeloDeLasFotos + ")";
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        var sitio = new Vector3(0f, 800f, 0f);
        inst.transform.SetPositionAndRotation(sitio, Quaternion.identity);
        var hechas = new System.Text.StringBuilder();
        try
        {
            // El controlador acaba de cambiar (estado nuevo): el Animator tiene que volver a leerlo.
            foreach (var a in inst.GetComponentsInChildren<Animator>(true)) { a.enabled = true; a.Rebind(); }
            // Fuera de Play las mallas con huesos no se recalculan en cada render: sin esto la foto
            // sale con el cuerpo de la primera pose (solo se mueven las piezas rígidas, como la cabeza).
            foreach (var malla in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                malla.forceMatrixRecalculationPerRender = true;
                malla.updateWhenOffscreen = true;
            }
            foreach (float t in new[] { 0f, 1.0f, 1.55f, 2.4f })
            {
                Horno.MuestrearEstado(inst, clip, Estado, t);
                // En juego la capa de tronco está apagada mientras no hay un gesto de tronco encima.
                var anim = inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.runtimeAnimatorController != null);
                if (anim != null)
                {
                    for (int capa = 1; capa < anim.layerCount; capa++) anim.SetLayerWeight(capa, 0f);
                    anim.Update(0f);
                }
                inst.transform.SetPositionAndRotation(sitio, Quaternion.identity);
                var centro = sitio + Vector3.up * 0.6f;
                string nombre = $"hierbas_{t:0.00}".Replace(',', '_').Replace('.', '_');
                hechas.Append(CapturaDeEscenario.FotoDesde(nombre + "_lado", centro + new Vector3(2.8f, 0.3f, 1.0f), centro, 35f)).Append(' ');
                hechas.Append(CapturaDeEscenario.FotoDesde(nombre + "_frente", centro + new Vector3(0.8f, 0.4f, 2.8f), centro, 35f)).Append(' ');
            }
        }
        finally { Object.DestroyImmediate(inst); }
        return hechas.ToString();
    }
}
#endif
