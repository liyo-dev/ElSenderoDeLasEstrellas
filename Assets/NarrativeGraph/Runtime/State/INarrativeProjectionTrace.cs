/// Optional: a state writer that wants to know which node caused each change implements this. The
/// projector calls it right before projecting each node, in story order.
public interface INarrativeProjectionTrace
{
    void OnProjectNode(NarrativeNode node);
}
