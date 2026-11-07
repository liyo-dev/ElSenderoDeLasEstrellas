#if UNITY_EDITOR
namespace Dustyroom {
[UnityEditor.CustomEditor(typeof(CurveRenderer))]
public class CurveRendererEditor : ExternalPropertyAttributes.Editor.ExternalCustomInspector { }
}
#endif
