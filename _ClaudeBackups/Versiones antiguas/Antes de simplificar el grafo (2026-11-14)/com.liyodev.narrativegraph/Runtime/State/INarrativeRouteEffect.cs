/// Effect of a decision node that depends on the branch the story took. A flag check implies the
/// flag's value; an item check consumes the item only on success. The projector calls it with the
/// port it followed, after the node's INarrativeStateEffect (if any).
public interface INarrativeRouteEffect
{
    void ProjectRoute(INarrativeStateWriter state, int chosenPort);
}
