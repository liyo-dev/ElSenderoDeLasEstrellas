using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector personalizado para PorteoDeCompanero. Permite probar en vivo:
/// - La animación de Pick Up (agacharse a recogerlo y levantarse).
/// - El anclaje horizontal de Liam tumbado en brazos.
/// - Los ojos cerrados (Eye09).
/// - Sampling de poses en EditMode para previsualizar sin tener que pulsar Play.
/// </summary>
[CustomEditor(typeof(PorteoDeCompanero))]
public class PorteoDeCompaneroEditor : Editor
{
    private const string FbxCarryMove = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/CarryMoveIdle_NoWeapon.fbx";
    private const string FbxSleeping = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/Sleeping_NoWeapon.fbx";
    private const string FbxPickUp = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/CarryStart_NoWeapon.fbx";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var porteo = (PorteoDeCompanero)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Pruebas y Calibración en Vivo", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(Application.isPlaying ? "Probar Pick Up (Animado)" : "Cargar en Brazos", GUILayout.Height(32)))
        {
            if (Application.isPlaying)
            {
                porteo.RecogerYCargar();
            }
            else
            {
                porteo.CargarDirecto();
                PrevisualizarPosesEnEditMode(porteo);
            }
            SceneView.RepaintAll();
        }

        GUI.enabled = porteo.EstaCargando;
        if (GUILayout.Button("Soltar al Suelo", GUILayout.Height(32)))
        {
            porteo.Soltar();
            if (!Application.isPlaying && porteo.Cuerpo != null)
            {
                PorteoDeCompanero.RestaurarOjos(porteo.Cuerpo);
            }
            SceneView.RepaintAll();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Previsualizar Postura (Pose Brazos + Liam)"))
            {
                porteo.CargarDirecto();
                PrevisualizarPosesEnEditMode(porteo);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Forzar Ojos Cerrados (Eye09)"))
            {
                if (porteo.Cuerpo != null)
                {
                    PorteoDeCompanero.CerrarOjos(porteo.Cuerpo, "Eye09");
                    SceneView.RepaintAll();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        if (porteo.EstaCargando)
        {
            EditorGUILayout.HelpBox(
                "Liam está cargado. Si modificas 'Offset Posicion' u 'Offset Rotacion', pulsa 'Previsualizar Postura' para ver el ajuste al instante.",
                MessageType.Info);
        }
    }

    private static void PrevisualizarPosesEnEditMode(PorteoDeCompanero porteo)
    {
        if (porteo == null) return;

        // 1. Ojos cerrados
        if (porteo.Cuerpo != null)
        {
            PorteoDeCompanero.CerrarOjos(porteo.Cuerpo, "Eye09");
        }

        // 2. Samplear pose de carga en portador (CarryMoveIdle)
        var portador = porteo.Portador != null ? porteo.Portador.gameObject : porteo.gameObject;
        SamplearClip(portador, FbxCarryMove, 0f);

        // 3. Samplear pose de reposo en Liam (Sleeping)
        if (porteo.Cuerpo != null)
        {
            SamplearClip(porteo.Cuerpo.gameObject, FbxSleeping, 0f);
        }
    }

    private static void SamplearClip(GameObject obj, string fbxPath, float tiempo)
    {
        if (obj == null) return;
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (subAssets == null) return;

        foreach (var sub in subAssets)
        {
            if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                clip.SampleAnimation(obj, tiempo);
                return;
            }
        }
    }
}
