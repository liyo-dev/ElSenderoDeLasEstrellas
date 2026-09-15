using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    public class NarrativeGraphView : GraphView
    {
        public System.Action<Edge> OnEdgeLinked;
        public System.Action<Edge> OnEdgeUnlinked;
        public System.Action<NodeView> OnNodeDeleted;
        public System.Action<List<NodeView>> OnNodesMoved;
        public System.Action<Vector2> OnRequestCreateAt; // doble clic en lienzo vacío

        public NarrativeGraphView()
        {
            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            SetupZoom(0.15f, 2.5f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var mini = new MiniMap { anchored = true };
            mini.SetPosition(new Rect(10, 10, 200, 130));
            mini.AddToClassList("narrative-minimap");
            Add(mini);

            style.flexGrow = 1f;
            graphViewChanged = GraphChanged;

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2 && evt.button == 0 && evt.target == this)
                {
                    OnRequestCreateAt?.Invoke(contentViewContainer.WorldToLocal(evt.mousePosition));
                    evt.StopPropagation();
                }
            });
        }

        GraphViewChange GraphChanged(GraphViewChange changes)
        {
            if (changes.edgesToCreate != null)
            {
                foreach (var e in changes.edgesToCreate)
                {
                    AddElement(e);
                    OnEdgeLinked?.Invoke(e);
                }
                changes.edgesToCreate = null;
            }

            if (changes.elementsToRemove != null)
            {
                // Primero aristas, luego nodos (los nodos borrados ya limpian sus referencias).
                foreach (var el in changes.elementsToRemove)
                    if (el is Edge ed) OnEdgeUnlinked?.Invoke(ed);
                foreach (var el in changes.elementsToRemove)
                    if (el is NodeView nv) OnNodeDeleted?.Invoke(nv);
            }

            if (changes.movedElements != null && changes.movedElements.Count > 0)
            {
                var moved = new List<NodeView>();
                foreach (var el in changes.movedElements)
                    if (el is NodeView nv) moved.Add(nv);
                if (moved.Count > 0) OnNodesMoved?.Invoke(moved);
            }

            return changes;
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = new List<Port>();
            ports.ForEach(port =>
            {
                if (port == startPort) return;
                if (port.node == startPort.node) return;
                if (port.direction == startPort.direction) return;
                result.Add(port);
            });
            return result;
        }
    }
}
