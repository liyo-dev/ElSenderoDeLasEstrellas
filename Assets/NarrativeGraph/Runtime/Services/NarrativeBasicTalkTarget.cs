using UnityEngine;

/// Algo o alguien con quien se puede hablar con los servicios básicos. Cuando un paso del grafo
/// espera hablar con este id, sale un botón sobre él; al pulsarlo se emite la charla
/// (la misma señal que emitiría la interacción de un juego).
public sealed class NarrativeBasicTalkTarget : MonoBehaviour
{
    [Tooltip("Id del actor: el que pone el paso «Esperar hablar con».")]
    public string actorId = "NPC";
    [Tooltip("Texto del botón.")]
    public string label = "Hablar";
    [Tooltip("Altura sobre el objeto a la que sale el botón.")]
    public float height = 2f;

    void OnGUI()
    {
        if (!WaitNpcInteractionNode.IsWaitingFor(actorId) || NarrativeBasicServices.Busy) return;
        var cam = Camera.main;
        Vector2 at;
        if (cam != null)
        {
            var p = cam.WorldToScreenPoint(transform.position + Vector3.up * height);
            if (p.z < 0f) return;
            at = new Vector2(p.x, Screen.height - p.y);
        }
        else at = new Vector2(Screen.width * 0.5f, Screen.height * 0.4f);
        var r = new Rect(at.x - 80f, at.y - 18f, 160f, 36f);
        if (GUI.Button(r, label, NarrativeBasicServices.ButtonStyle))
            DefaultNarrativeSignals.EnsureInstance().RaiseCustom(WaitNpcInteractionNode.SignalKeyFor(actorId), name);
    }
}
