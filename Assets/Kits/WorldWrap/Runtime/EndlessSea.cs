using System.Collections.Generic;
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

        [Tooltip("Se puede nadar en este mar: quien nada lo consulta con TryGetSurface aunque no haya un volumen de agua debajo.")]
        [SerializeField] private bool swimmable = true;

        private static readonly List<EndlessSea> s_active = new List<EndlessSea>();

        private Vector3 _home;
        private bool _subscribed;

        public float Size => size;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_active.Clear();
#endif

        /// <summary>
        /// Altura de la superficie del mar más alto en el que se puede nadar, si hay alguno
        /// encendido. El mar no tiene borde, así que vale para cualquier punto en X y Z: quien
        /// pregunta decide con el fondo que tiene debajo si de verdad hay agua honda.
        /// </summary>
        public static bool TryGetSurface(out float surfaceY)
        {
            surfaceY = 0f;
            bool found = false;
            for (int i = 0; i < s_active.Count; i++)
            {
                var sea = s_active[i];
                if (sea == null || !sea.swimmable) continue;
                float y = sea._home.y;
                if (!found || y > surfaceY) { surfaceY = y; found = true; }
            }
            return found;
        }

        private void Awake() => ApplySize();

        private void OnEnable()
        {
            _home = transform.position;
            if (!Application.isPlaying) return;
            if (!s_active.Contains(this)) s_active.Add(this);
            if (_subscribed) return;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            _subscribed = true;
        }

        private void OnDisable()
        {
            s_active.Remove(this);
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
