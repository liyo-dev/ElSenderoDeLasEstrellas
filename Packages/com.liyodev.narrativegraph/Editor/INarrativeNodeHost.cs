namespace NarrativeGraphEditor
{
    /// Anything on the canvas that shows a node and can be edited from the inspector.
    public interface INarrativeNodeHost
    {
        NarrativeNode Model { get; }
    }
}
