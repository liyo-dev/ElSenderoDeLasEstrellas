using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rellena el campo 'actionRef' (y 'directionActionRef' en los prompts de modo Direction) de todos
/// los InputPromptBeat de las SequenceDefinition del proyecto.
///
/// POR QUÉ EXISTE: un InputActionReference es un sub-asset generado por el importador del Input
/// System dentro de PlayerControls.inputactions. Su fileID lo calcula Unity al importar y no se
/// puede escribir a mano en el YAML de forma fiable -- hay que resolverlo con AssetDatabase. Hasta
/// ahora la única forma de rellenarlo era arrastrar la acción a mano en el Inspector, uno por uno:
/// los 8 prompts del prólogo (SEQ_Prologo_UltimaNoche) llevaban desde su construcción, el 17 sep
/// 2026, con 'actionRef: {fileID: 0}'. Sin esta referencia el PanicInputDetector no escucha ninguna
/// pulsación real y el prompt se limita a expirar solo (es 'tolerant', así que no cuelga nada --
/// simplemente no se puede acertar nunca).
///
/// QUÉ HACE (idempotente -- se puede volver a ejecutar sin efectos):
///   1) Resuelve GamePlay/Interact y GamePlay/Move entre los sub-assets de
///      Assets/Scripts/Core/PlayerControls.inputactions.
///   2) Recorre TODAS las SequenceDefinition del proyecto con SerializedObject, que es lo que
///      permite entrar en los beats [SerializeReference] -- incluidos los que están anidados
///      dentro de un ParallelBeat.
///   3) A cada InputPromptBeat con 'actionRef' vacío le pone Interact. A los de modo Direction con
///      'directionActionRef' vacío les pone Move.
///   4) NO pisa nada que ya esté relleno, y deja un resumen en consola de qué asset y qué beat ha
///      tocado.
///
/// Se usa SerializedObject en vez de tocar los campos del objeto directamente porque 'mode' es
/// privado y porque así el Undo y el marcado de sucio del asset los gestiona el propio Editor.
/// </summary>
public static class SequenceInputActionWiring
{
    private const string ControlsPath = "Assets/Scripts/Core/PlayerControls.inputactions";
    private const string MapName = "GamePlay";
    private const string ButtonAction = "Interact";
    private const string DirectionAction = "Move";

    [MenuItem("El Sendero/Secuencias/Asignar acciones de input a los prompts (Interact / Move)")]
    public static void AsignarAcciones()
    {
        var interact = ResolverAccion(MapName, ButtonAction);
        var move = ResolverAccion(MapName, DirectionAction);

        if (interact == null)
        {
            Debug.LogError($"[SequenceInputActionWiring] No se ha encontrado la acción " +
                $"'{MapName}/{ButtonAction}' entre los sub-assets de {ControlsPath}. " +
                "¿Se ha renombrado el mapa o la acción?");
            return;
        }
        if (move == null)
        {
            Debug.LogWarning($"[SequenceInputActionWiring] No se ha encontrado '{MapName}/{DirectionAction}'. " +
                "Los prompts de modo Direction se quedarán sin 'directionActionRef'.");
        }

        var guids = AssetDatabase.FindAssets("t:SequenceDefinition");
        var resumen = new StringBuilder();
        int assetsTocados = 0, botones = 0, direcciones = 0, yaPuestos = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(path);
            if (def == null) continue;

            var so = new SerializedObject(def);
            var it = so.GetIterator();
            bool cambiado = false;
            var lineas = new List<string>();

            // Next(true) entra dentro de los [SerializeReference], que es justo lo que hace falta
            // para llegar a los beats (y a los que cuelgan de un ParallelBeat).
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                bool esBoton = it.propertyPath.EndsWith(".actionRef") || it.propertyPath == "actionRef";
                bool esDir = it.propertyPath.EndsWith(".directionActionRef") || it.propertyPath == "directionActionRef";
                if (!esBoton && !esDir) continue;

                if (it.objectReferenceValue != null) { yaPuestos++; continue; }

                if (esBoton)
                {
                    it.objectReferenceValue = interact;
                    botones++; cambiado = true;
                    lineas.Add($"      {it.propertyPath} → {MapName}/{ButtonAction}");
                }
                else if (move != null && EsModoDireccion(so, it.propertyPath))
                {
                    it.objectReferenceValue = move;
                    direcciones++; cambiado = true;
                    lineas.Add($"      {it.propertyPath} → {MapName}/{DirectionAction}");
                }
            }

            if (!cambiado) continue;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(def);
            assetsTocados++;
            resumen.AppendLine($"   {System.IO.Path.GetFileNameWithoutExtension(path)}:");
            foreach (var l in lineas) resumen.AppendLine(l);
        }

        if (assetsTocados > 0) AssetDatabase.SaveAssets();

        Debug.Log($"[SequenceInputActionWiring] Hecho. {botones} 'actionRef' + {direcciones} " +
            $"'directionActionRef' asignados en {assetsTocados} secuencia(s). " +
            $"{yaPuestos} campo(s) ya estaban rellenos y no se han tocado.\n" +
            (resumen.Length > 0 ? resumen.ToString() : "   (sin cambios)"));
    }

    /// El campo 'mode' del beat es privado, así que se mira por su ruta serializada hermana:
    /// '...data[3].directionActionRef' → '...data[3].mode'. PanicInputMode.Direction es el valor 4.
    private static bool EsModoDireccion(SerializedObject so, string propertyPath)
    {
        int corte = propertyPath.LastIndexOf('.');
        if (corte < 0) return false;
        var modo = so.FindProperty(propertyPath.Substring(0, corte) + ".mode");
        return modo != null && modo.enumValueIndex == (int)PanicInputMode.Direction;
    }

    private static InputActionReference ResolverAccion(string mapa, string accion)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ControlsPath))
        {
            if (obj is not InputActionReference r || r.action == null) continue;
            if (r.action.name == accion && r.action.actionMap != null && r.action.actionMap.name == mapa)
                return r;
        }
        return null;
    }
}
