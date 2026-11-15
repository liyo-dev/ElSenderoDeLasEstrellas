using UnityEngine;
using UnityEngine.Rendering;

namespace Kits.WorldWrap
{
    /// <summary>
    /// Plano de mar que no se acaba: justo antes de que cada cámara de juego dibuje, el plano se
    /// coloca bajo ella (solo en X y Z, encajado a una rejilla), así su borde nunca entra en cuadro.
    /// Pensado para un agua cuyo shader trabaja en coordenadas del mundo: el dibujo del agua no se
    /// mueve con el plano. Solo actúa en Play; en el Editor el plano se queda donde está.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    public class EndlessSea : MonoBehaviour
    {
        [Tooltip("Lado del mar en metros. Tiene que ser bastante mayor que el doble del plano lejano de la cámara.")]
        [SerializeField, Min(10f)] private float size = 3000f;

        [Tooltip("Paso de la rejilla a la que se encaja el plano al seguir a la cámara, en metros. 0 = sin encajar.")]
        [SerializeField, Min(0f)] private float snap = 10f;

        private Vector3 _home;
        private bool _subscribed;

        public float Size => size;

        private void Awake() => ApplySize();

        private void OnEnable()
        {
            _home = transform.position;
            if (!Application.isPlaying || _subscribed) return;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                _subscribed = false;
            }
            if (Application.isPlaying) transform.position = _home;
        }

        /// <summary>Escala el plano para que mida <see cref="Size"/> de lado según su malla.</summary>
        public void ApplySize()
        {
            var mesh = GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null) return;
            Vector3 meshSize = mesh.bounds.size;
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = meshSize.x * parentScale.x;
            float sz = meshSize.z * parentScale.z;
            if (sx <= 0f || sz <= 0f) return;
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(size / sx, scale.y, size / sz);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType != CameraType.Game) return;
            Vector3 c = cam.transform.position;
            float x = snap > 0f ? Mathf.Round(c.x / snap) * snap : c.x;
            float z = snap > 0f ? Mathf.Round(c.z / snap) * snap : c.z;
            Vector3 current = transform.position;
            if (current.x == x && current.z == z) return;
            transform.position = new Vector3(x, _home.y, z);
        }
    }
}
