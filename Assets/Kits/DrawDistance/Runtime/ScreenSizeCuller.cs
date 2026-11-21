using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kits.DrawDistance
{
    /// <summary>
    /// Deja de dibujar las piezas de malla que, vistas desde la cámara de juego, saldrían más pequeñas que
    /// unos pocos píxeles. Lo pequeño y lejano (flores, setas, piedras, cachivaches) cuesta en la tarjeta
    /// gráfica casi lo mismo que de cerca (sus pases de profundidad, normales, sombra y color) y no se
    /// distingue. Las piezas se reparten en clases de tamaño, cada una con un <see cref="CullingGroup"/> y su
    /// distancia de corte: el motor mide las distancias y aquí solo llegan los cambios. La distancia de una
    /// clase sale del radio más grande de la clase, así que nada deja de dibujarse mientras ocupe
    /// <see cref="minimumPixels"/> o más.
    ///
    /// Usa <see cref="Renderer.forceRenderingOff"/>: no pisa a quien enciende o apaga un renderer o su objeto.
    /// No toca lo que cuelga de un Rigidbody o de un Animator (se mueve solo), lo marcado con
    /// <see cref="ScreenSizeCullerIgnore"/> ni lo que mide más de <see cref="maximumRadius"/>. Lo que se mueve
    /// por otros medios se sigue con una ronda corta por fotograma (<c>Transform.hasChanged</c>).
    /// La cámara es la primera cámara de juego que dibuja en pantalla en cada fotograma; con una cámara
    /// ortográfica no se corta nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenSizeCuller : MonoBehaviour
    {
        [Tooltip("Alto en pantalla (píxeles) por debajo del cual una pieza deja de dibujarse.")]
        [SerializeField, Range(1f, 12f)] private float minimumPixels = 3f;

        [Tooltip("Las piezas de radio (m) mayor que este se dibujan siempre.")]
        [SerializeField, Min(0.1f)] private float maximumRadius = 8f;

        [Tooltip("Radio (m) de la clase de tamaño más pequeña.")]
        [SerializeField, Min(0.01f)] private float smallestRadius = 0.15f;

        [Tooltip("Clases de tamaño (una distancia de corte cada una). Con más clases el corte se ajusta más a cada pieza.")]
        [SerializeField, Range(2, 24)] private int sizeClasses = 12;

        [Tooltip("Recoge las piezas de toda la escena de este objeto, no solo las de sus hijos.")]
        [SerializeField] private bool wholeScene;

        [Tooltip("Piezas por fotograma que se revisan para seguir a las que se han movido.")]
        [SerializeField, Min(16)] private int movedChecksPerFrame = 300;

        sealed class SizeClass
        {
            public float radius;
            public CullingGroup group;
            public readonly float[] distance = new float[1];
            public readonly List<Renderer> renderers = new List<Renderer>();
            public readonly List<Transform> transforms = new List<Transform>();
            public BoundingSphere[] spheres;
            public bool[] culled;
        }

        SizeClass[] _classes;
        bool _started;
        Camera _camera;
        int _cameraFrame = -1;
        float _fieldOfView = -1f;
        int _pixelHeight = -1;
        bool _orthographic;
        int _cursorClass;
        int _cursorIndex;

        /// <summary>Piezas que están sin dibujar ahora mismo.</summary>
        public int CulledCount { get; private set; }

        /// <summary>Piezas que vigila.</summary>
        public int TrackedCount { get; private set; }

        /// <summary>Recoge las piezas de toda su escena. Hay que ponerlo antes de su primer fotograma.</summary>
        public bool WholeScene
        {
            get => wholeScene;
            set => wholeScene = value;
        }

        void Start()
        {
            _started = true;
            Build();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            if (_started) Build();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Teardown();
        }

        /// <summary>Vuelve a recoger las piezas (tras cambiar mucho la escena).</summary>
        public void Rebuild()
        {
            if (!isActiveAndEnabled) return;
            Teardown();
            Build();
        }

        void Build()
        {
            var found = new List<MeshRenderer>(1024);
            if (wholeScene)
            {
                var some = new List<MeshRenderer>(256);
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    root.GetComponentsInChildren(true, some);
                    found.AddRange(some);
                }
            }
            else
            {
                GetComponentsInChildren(true, found);
            }

            int count = Mathf.Max(2, sizeClasses);
            float smallest = Mathf.Min(smallestRadius, maximumRadius);
            float ratio = Mathf.Pow(maximumRadius / smallest, 1f / (count - 1));
            float logRatio = Mathf.Log(Mathf.Max(ratio, 1.0001f));
            _classes = new SizeClass[count];
            for (int k = 0; k < count; k++)
                _classes[k] = new SizeClass { radius = k == count - 1 ? maximumRadius : smallest * Mathf.Pow(ratio, k) };

            var radii = new List<float>[count];
            for (int k = 0; k < count; k++) radii[k] = new List<float>();

            foreach (var r in found)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                float radius = r.bounds.extents.magnitude;
                if (radius <= 0f || radius > maximumRadius) continue;
                if (r.GetComponentInParent<Rigidbody>(true) != null) continue;
                if (r.GetComponentInParent<Animator>(true) != null) continue;
                if (r.GetComponentInParent<ScreenSizeCullerIgnore>(true) != null) continue;

                int k = radius <= smallest ? 0 : Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(radius / smallest) / logRatio - 1e-4f), 0, count - 1);
                while (k < count - 1 && radius > _classes[k].radius) k++;
                var c = _classes[k];
                c.renderers.Add(r);
                c.transforms.Add(r.transform);
                radii[k].Add(radius);
            }

            TrackedCount = 0;
            CulledCount = 0;
            for (int k = 0; k < count; k++)
            {
                var c = _classes[k];
                int n = c.renderers.Count;
                if (n == 0) continue;
                TrackedCount += n;
                c.spheres = new BoundingSphere[n];
                c.culled = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    c.spheres[i] = new BoundingSphere(c.renderers[i].bounds.center, radii[k][i]);
                    c.transforms[i].hasChanged = false;
                }
                c.group = new CullingGroup();
                c.group.SetBoundingSpheres(c.spheres);
                c.group.SetBoundingSphereCount(n);
                int index = k;
                c.group.onStateChanged = e => OnStateChanged(index, e);
            }

            _camera = null;
            _cameraFrame = -1;
            _cursorClass = 0;
            _cursorIndex = 0;
        }

        void Teardown()
        {
            if (_classes == null) return;
            foreach (var c in _classes)
            {
                if (c.group == null) continue;
                c.group.onStateChanged = null;
                c.group.Dispose();
                c.group = null;
                for (int i = 0; i < c.culled.Length; i++)
                {
                    if (!c.culled[i]) continue;
                    var r = c.renderers[i];
                    if (r != null) r.forceRenderingOff = false;
                }
            }
            _classes = null;
            _camera = null;
            TrackedCount = 0;
            CulledCount = 0;
        }

        void OnStateChanged(int classIndex, CullingGroupEvent e)
        {
            var c = _classes[classIndex];
            bool off = e.currentDistance >= 1;
            if (c.culled[e.index] == off) return;
            c.culled[e.index] = off;
            CulledCount += off ? 1 : -1;
            var r = c.renderers[e.index];
            if (r != null) r.forceRenderingOff = off;
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (_classes == null || camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
            int frame = Time.frameCount;
            if (_cameraFrame == frame) return;
            _cameraFrame = frame;
            if (camera == _camera) return;

            _camera = camera;
            foreach (var c in _classes)
            {
                if (c.group == null) continue;
                c.group.targetCamera = camera;
                c.group.SetDistanceReferencePoint(camera.transform);
            }
            UpdateDistances();
        }

        void LateUpdate()
        {
            if (_classes == null) return;
            if (_camera != null && (_camera.orthographic != _orthographic
                || !Mathf.Approximately(_camera.fieldOfView, _fieldOfView) || _camera.pixelHeight != _pixelHeight))
                UpdateDistances();
            FollowMovedPieces();
        }

        void UpdateDistances()
        {
            _orthographic = _camera.orthographic;
            _fieldOfView = _camera.fieldOfView;
            _pixelHeight = _camera.pixelHeight;
            float perMetre = _orthographic ? 0f
                : _pixelHeight / (Mathf.Max(minimumPixels, 0.5f) * Mathf.Tan(Mathf.Max(_fieldOfView, 1f) * 0.5f * Mathf.Deg2Rad));
            foreach (var c in _classes)
            {
                if (c.group == null) continue;
                c.distance[0] = _orthographic ? 1e9f : c.radius * perMetre + c.radius;
                c.group.SetBoundingDistances(c.distance);
            }
        }

        // Ronda corta por fotograma: lo que se ha movido desde la última vuelta lleva su esfera a donde está.
        void FollowMovedPieces()
        {
            if (TrackedCount == 0) return;
            int checks = Mathf.Min(movedChecksPerFrame, TrackedCount);
            int guard = _classes.Length + checks;
            while (checks > 0 && guard-- > 0)
            {
                var c = _classes[_cursorClass];
                int n = c.group != null ? c.renderers.Count : 0;
                for (; _cursorIndex < n && checks > 0; _cursorIndex++, checks--)
                {
                    var t = c.transforms[_cursorIndex];
                    if (t == null || !t.hasChanged) continue;
                    t.hasChanged = false;
                    var r = c.renderers[_cursorIndex];
                    if (r != null) c.spheres[_cursorIndex].position = r.bounds.center;
                }
                if (_cursorIndex >= n)
                {
                    _cursorIndex = 0;
                    _cursorClass = (_cursorClass + 1) % _classes.Length;
                }
            }
        }
    }
}
