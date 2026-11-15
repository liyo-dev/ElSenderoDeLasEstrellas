using NarrativeGraphEditor.Story;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// Los retratos del grupo (Assets/Art/UI/Retratos/Grupo/<nombre>_normal.png) para las tarjetas
    /// y el mapa del grafo: la misma cara que enseña el HUD. Quien no tiene retrato propio sale con
    /// la miniatura de su prefab.
    public sealed class RetratosParaElGrafo : INarrativePortraitSource
    {
        const string Carpeta = "Assets/Art/UI/Retratos/Grupo/";

        public Texture2D PortraitOf(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return null;
            var nombre = actorId == "Player" ? "will" : actorId.Replace("NPC_", "").Trim('_').ToLowerInvariant();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(Carpeta + nombre + "_normal.png");
        }
    }
}
