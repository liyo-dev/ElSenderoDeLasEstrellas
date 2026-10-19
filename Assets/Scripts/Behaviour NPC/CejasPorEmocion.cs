using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// Mueve las cejas del personaje según la emoción. En este modelo las cejas son dos islas
/// sueltas dentro de la malla del pelo: este componente copia la malla por instancia y deforma
/// solo esos vértices, sin tocar la cabeza ni el resto del pelo.
[DisallowMultipleComponent]
public class CejasPorEmocion : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Controlador de emociones del personaje. Si se deja vacío se busca en los padres.")]
    [SerializeField] private NPCEmotionController controlador;
    [Tooltip("Malla del pelo que contiene las cejas. Si se deja vacío se usa la de este objeto.")]
    [SerializeField] private MeshFilter mallaDelPelo;
    [Tooltip("Ojos que sirven de referencia para localizar las cejas. Si se deja vacío se buscan.")]
    [SerializeField] private Renderer ojosDeReferencia;

    [Header("Ajustes")]
    [Tooltip("Multiplica el movimiento de todas las poses. 1 es el valor de diseño.")]
    [SerializeField, Range(0f, 2f)] private float intensidad = 1f;
    [Tooltip("Velocidad con la que las cejas alcanzan la pose de la emoción.")]
    [SerializeField, Min(0.1f)] private float velocidad = 14f;

    private readonly struct PoseDeCeja
    {
        public readonly float altura;
        public readonly float avance;
        public readonly float apertura;
        public readonly float giroInterior;

        public PoseDeCeja(float altura, float avance, float apertura, float giroInterior)
        {
            this.altura = altura;
            this.avance = avance;
            this.apertura = apertura;
            this.giroInterior = giroInterior;
        }

        public static PoseDeCeja Neutra => new PoseDeCeja(0f, 0f, 0f, 0f);

        public static PoseDeCeja Mezclar(PoseDeCeja a, PoseDeCeja b, float t)
            => new PoseDeCeja(Mathf.Lerp(a.altura, b.altura, t),
                Mathf.Lerp(a.avance, b.avance, t),
                Mathf.Lerp(a.apertura, b.apertura, t),
                Mathf.Lerp(a.giroInterior, b.giroInterior, t));

        public bool CercaDe(PoseDeCeja otra)
            => Mathf.Abs(altura - otra.altura) < 0.00005f &&
               Mathf.Abs(avance - otra.avance) < 0.00005f &&
               Mathf.Abs(apertura - otra.apertura) < 0.00005f &&
               Mathf.Abs(giroInterior - otra.giroInterior) < 0.02f;
    }

    private readonly struct PosePar
    {
        public readonly PoseDeCeja izquierda;
        public readonly PoseDeCeja derecha;

        public PosePar(PoseDeCeja izquierda, PoseDeCeja derecha)
        {
            this.izquierda = izquierda;
            this.derecha = derecha;
        }

        public static PosePar Neutra => new PosePar(PoseDeCeja.Neutra, PoseDeCeja.Neutra);

        public static PosePar Simetrica(PoseDeCeja pose) => new PosePar(pose, pose);

        public static PosePar Mezclar(PosePar a, PosePar b, float t)
            => new PosePar(PoseDeCeja.Mezclar(a.izquierda, b.izquierda, t),
                PoseDeCeja.Mezclar(a.derecha, b.derecha, t));

        public bool CercaDe(PosePar otra)
            => izquierda.CercaDe(otra.izquierda) && derecha.CercaDe(otra.derecha);
    }

    private Mesh _mallaOriginal;
    private Mesh _mallaCopia;
    private Vector3[] _verticesBase;
    private Vector3[] _verticesActuales;
    private int[] _indicesIzquierda;
    private int[] _indicesDerecha;
    private Vector3 _centroIzquierdo;
    private Vector3 _centroDerecho;
    private PosePar _poseActual = PosePar.Neutra;
    private PosePar _poseObjetivo = PosePar.Neutra;
    private bool _preparado;
    private bool _enMovimiento;

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    private bool _avisadoSinCejas;
#endif

    private void Awake()
    {
        ResolverReferencias();
        Preparar();
    }

    private void OnEnable()
    {
        ResolverReferencias();
        if (controlador != null) controlador.OnEmotionChanged += AlCambiarEmocion;
        Preparar();
        if (_preparado)
        {
            _poseObjetivo = PosePara(controlador != null ? controlador.CurrentEmotion : NPCEmotion.Neutral);
            _poseActual = _poseObjetivo;
            _enMovimiento = false;
            ActualizarMalla();
        }
    }

    private void OnDisable()
    {
        if (controlador != null) controlador.OnEmotionChanged -= AlCambiarEmocion;
        if (!_preparado) return;
        if (mallaDelPelo != null && mallaDelPelo.sharedMesh == _mallaCopia)
            mallaDelPelo.sharedMesh = _mallaOriginal;
        Destruir(_mallaCopia);
        _mallaCopia = null;
        _mallaOriginal = null;
        _verticesBase = null;
        _verticesActuales = null;
        _indicesIzquierda = null;
        _indicesDerecha = null;
        _preparado = false;
        _enMovimiento = false;
    }

    private void OnDestroy()
    {
        Destruir(_mallaCopia);
        _mallaCopia = null;
    }

    /// Prepara la copia de la malla y localiza las dos islas de cejas. Es idempotente.
    private void Preparar()
    {
        if (_preparado) return;
        ResolverReferencias();
        if (mallaDelPelo == null || mallaDelPelo.sharedMesh == null)
        {
            Avisar("No hay una malla de pelo que contenga las cejas.");
            return;
        }

        _mallaOriginal = mallaDelPelo.sharedMesh;
        if (!CopiarMalla(_mallaOriginal, out _mallaCopia, out _verticesBase, out int[] todosLosIndices))
        {
            Avisar("No se pudo copiar la malla del pelo.");
            return;
        }

        if (!BuscarIslasDeCejas(_verticesBase, todosLosIndices, BoundsDeOjosEnEspacioDelPelo(),
                out _indicesIzquierda, out _indicesDerecha, out _centroIzquierdo, out _centroDerecho))
        {
            Destruir(_mallaCopia);
            _mallaCopia = null;
            _verticesBase = null;
            Avisar("No encuentro el par de cejas dentro de la malla del pelo.");
            return;
        }

        _verticesActuales = (Vector3[])_verticesBase.Clone();
        mallaDelPelo.sharedMesh = _mallaCopia;
        _preparado = true;
    }

    private void ResolverReferencias()
    {
        if (controlador == null) controlador = GetComponentInParent<NPCEmotionController>(true);
        if (mallaDelPelo == null) mallaDelPelo = GetComponent<MeshFilter>();
        if (ojosDeReferencia == null && mallaDelPelo != null)
            ojosDeReferencia = BuscarOjosDeReferencia();
    }

    private Renderer BuscarOjosDeReferencia()
    {
        Transform raiz = controlador != null ? controlador.transform : transform.root;
        foreach (var renderer in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
            if (renderer.name.StartsWith("Eye")) return renderer;
        }
        return null;
    }

    private Bounds BoundsDeOjosEnEspacioDelPelo()
    {
        if (ojosDeReferencia == null || mallaDelPelo == null) return default;
        var filtro = ojosDeReferencia.GetComponent<MeshFilter>();
        if (filtro == null || filtro.sharedMesh == null) return default;
        Bounds enOjos = filtro.sharedMesh.bounds;
        Vector3 centro = mallaDelPelo.transform.InverseTransformPoint(
            ojosDeReferencia.transform.TransformPoint(enOjos.center));
        Vector3 tam = mallaDelPelo.transform.InverseTransformVector(
            ojosDeReferencia.transform.TransformVector(enOjos.size));
        return new Bounds(centro, new Vector3(Mathf.Abs(tam.x), Mathf.Abs(tam.y), Mathf.Abs(tam.z)));
    }

    private void AlCambiarEmocion(NPCEmotion emocion)
    {
        if (!_preparado) Preparar();
        if (!_preparado) return;
        _poseObjetivo = PosePara(emocion);
        _enMovimiento = true;
    }

    private void LateUpdate()
    {
        if (!_preparado || !_enMovimiento) return;
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, velocidad) * dt);
        _poseActual = PosePar.Mezclar(_poseActual, _poseObjetivo, t);
        if (_poseActual.CercaDe(_poseObjetivo))
        {
            _poseActual = _poseObjetivo;
            _enMovimiento = false;
        }
        ActualizarMalla();
    }

    private void ActualizarMalla()
    {
        if (!_preparado) return;
        Array.Copy(_verticesBase, _verticesActuales, _verticesBase.Length);
        AplicarPose(_indicesIzquierda, _centroIzquierdo, _poseActual.izquierda);
        AplicarPose(_indicesDerecha, _centroDerecho, _poseActual.derecha);
        _mallaCopia.SetVertices(_verticesActuales);
    }

    private void AplicarPose(int[] indices, Vector3 centro, PoseDeCeja pose)
    {
        float signoX = Mathf.Sign(centro.x);
        float giro = pose.giroInterior * -signoX * intensidad;
        Quaternion rotacion = Quaternion.Euler(0f, 0f, giro);
        Vector3 desplazamiento = new Vector3(pose.apertura * signoX * intensidad,
            pose.altura * intensidad, pose.avance * intensidad);
        for (int k = 0; k < indices.Length; k++)
        {
            int i = indices[k];
            _verticesActuales[i] = centro + rotacion * (_verticesBase[i] - centro) + desplazamiento;
        }
    }

    private static PosePar PosePara(NPCEmotion emocion)
    {
        switch (emocion)
        {
            case NPCEmotion.Happy:
                return PosePar.Simetrica(new PoseDeCeja(0.005f, 0.002f, 0.001f, -2f));
            case NPCEmotion.Sad:
                return PosePar.Simetrica(new PoseDeCeja(0.004f, 0.001f, -0.001f, 8f));
            case NPCEmotion.Angry:
                return PosePar.Simetrica(new PoseDeCeja(-0.004f, 0f, -0.002f, -10f));
            case NPCEmotion.Surprised:
                return PosePar.Simetrica(new PoseDeCeja(0.010f, 0.003f, 0.005f, 4f));
            case NPCEmotion.Scared:
                return PosePar.Simetrica(new PoseDeCeja(0.008f, 0.002f, -0.001f, 8f));
            case NPCEmotion.Thinking:
                return new PosePar(new PoseDeCeja(0.008f, 0.002f, 0f, 6f),
                    new PoseDeCeja(-0.003f, 0f, 0f, -4f));
            case NPCEmotion.Tired:
                return PosePar.Simetrica(new PoseDeCeja(-0.003f, 0f, 0f, -3f));
            case NPCEmotion.Smirk:
                return new PosePar(new PoseDeCeja(0.006f, 0.001f, 0.001f, 3f),
                    new PoseDeCeja(-0.003f, 0f, 0f, -4f));
            case NPCEmotion.Worried:
                return PosePar.Simetrica(new PoseDeCeja(0.005f, 0.001f, -0.001f, 6f));
            case NPCEmotion.Determined:
                return PosePar.Simetrica(new PoseDeCeja(-0.003f, 0f, -0.002f, -7f));
            case NPCEmotion.Relieved:
                return PosePar.Simetrica(new PoseDeCeja(0.003f, 0.001f, 0.001f, 2f));
            case NPCEmotion.Confused:
                return new PosePar(new PoseDeCeja(0.008f, 0.002f, 0f, 7f),
                    new PoseDeCeja(-0.004f, 0f, 0f, -7f));
            case NPCEmotion.Excited:
                return PosePar.Simetrica(new PoseDeCeja(0.008f, 0.002f, 0.002f, 0f));
            case NPCEmotion.Annoyed:
                return PosePar.Simetrica(new PoseDeCeja(-0.003f, 0f, -0.001f, -6f));
            case NPCEmotion.Grateful:
                return PosePar.Simetrica(new PoseDeCeja(0.004f, 0.001f, 0f, 4f));
            default:
                return PosePar.Neutra;
        }
    }

    private static bool CopiarMalla(Mesh original, out Mesh copia, out Vector3[] vertices, out int[] indices)
    {
        copia = null;
        vertices = null;
        indices = null;
        Mesh.MeshDataArray datos = Mesh.AcquireReadOnlyMeshData(original);
        try
        {
            Mesh.MeshData d = datos[0];
            int n = d.vertexCount;
            vertices = new Vector3[n];
            var verticesNativos = new NativeArray<Vector3>(n, Allocator.Temp);
            d.GetVertices(verticesNativos);
            verticesNativos.CopyTo(vertices);
            verticesNativos.Dispose();

            Vector3[] normales = null;
            Vector4[] tangentes = null;
            Vector2[] uvs = null;
            if (original.HasVertexAttribute(VertexAttribute.Normal))
            {
                normales = new Vector3[n];
                var nativos = new NativeArray<Vector3>(n, Allocator.Temp);
                d.GetNormals(nativos);
                nativos.CopyTo(normales);
                nativos.Dispose();
            }
            if (original.HasVertexAttribute(VertexAttribute.Tangent))
            {
                tangentes = new Vector4[n];
                var nativos = new NativeArray<Vector4>(n, Allocator.Temp);
                d.GetTangents(nativos);
                nativos.CopyTo(tangentes);
                nativos.Dispose();
            }
            if (original.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                uvs = new Vector2[n];
                var nativos = new NativeArray<Vector2>(n, Allocator.Temp);
                d.GetUVs(0, nativos);
                nativos.CopyTo(uvs);
                nativos.Dispose();
            }

            copia = new Mesh { name = original.name + " (cejas)" };
            copia.indexFormat = original.indexFormat;
            copia.SetVertices(vertices);
            if (normales != null) copia.SetNormals(normales);
            if (tangentes != null) copia.SetTangents(tangentes);
            if (uvs != null) copia.SetUVs(0, uvs);

            int subMeshes = d.subMeshCount;
            var indicesPorSubmesh = new List<int[]>(subMeshes);
            var todos = new List<int>();
            for (int s = 0; s < subMeshes; s++)
            {
                int cuenta = d.GetSubMesh(s).indexCount;
                var nativos = new NativeArray<int>(cuenta, Allocator.Temp);
                d.GetIndices(nativos, s);
                var delSubmesh = new int[cuenta];
                nativos.CopyTo(delSubmesh);
                nativos.Dispose();
                indicesPorSubmesh.Add(delSubmesh);
                todos.AddRange(delSubmesh);
            }
            copia.subMeshCount = subMeshes;
            for (int s = 0; s < subMeshes; s++) copia.SetTriangles(indicesPorSubmesh[s], s, false);
            copia.bounds = original.bounds;
            copia.MarkDynamic();
            indices = todos.ToArray();
            return true;
        }
        finally
        {
            datos.Dispose();
        }
    }

    private static bool BuscarIslasDeCejas(Vector3[] vertices, int[] indices, Bounds ojos,
        out int[] izquierda, out int[] derecha, out Vector3 centroIzquierdo, out Vector3 centroDerecho)
    {
        izquierda = null;
        derecha = null;
        centroIzquierdo = Vector3.zero;
        centroDerecho = Vector3.zero;

        var representante = new int[vertices.Length];
        var porPosicion = new Dictionary<Vector3Int, int>(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            var clave = new Vector3Int(Mathf.RoundToInt(v.x * 10000f),
                Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
            if (!porPosicion.TryGetValue(clave, out int r))
            {
                r = i;
                porPosicion[clave] = i;
            }
            representante[i] = r;
        }

        var padre = new int[vertices.Length];
        for (int i = 0; i < padre.Length; i++) padre[i] = i;

        int Raiz(int x)
        {
            while (padre[x] != x)
            {
                padre[x] = padre[padre[x]];
                x = padre[x];
            }
            return x;
        }
        void Unir(int a, int b)
        {
            a = Raiz(a);
            b = Raiz(b);
            if (a != b) padre[b] = a;
        }

        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Unir(representante[indices[i]], representante[indices[i + 1]]);
            Unir(representante[indices[i]], representante[indices[i + 2]]);
        }

        var grupos = new Dictionary<int, List<int>>();
        for (int i = 0; i < vertices.Length; i++)
        {
            int raiz = Raiz(representante[i]);
            if (!grupos.TryGetValue(raiz, out var grupo))
            {
                grupo = new List<int>();
                grupos[raiz] = grupo;
            }
            grupo.Add(i);
        }

        bool hayOjos = ojos.size.sqrMagnitude > 0.0001f;
        var candidatos = new List<(List<int> vertices, Bounds caja)>();
        foreach (var grupo in grupos.Values)
        {
            if (grupo.Count < 8) continue;
            var caja = new Bounds(vertices[grupo[0]], Vector3.zero);
            for (int i = 1; i < grupo.Count; i++) caja.Encapsulate(vertices[grupo[i]]);
            if (caja.size.x > 0.30f || caja.size.y > 0.25f || caja.size.z > 0.25f) continue;
            if (Mathf.Abs(caja.center.x) < 0.005f) continue;
            if (hayOjos)
            {
                // Las cejas van por encima de los ojos y por delante de la cara.
                if (caja.center.y < ojos.max.y - 0.005f) continue;
                if (caja.center.z < ojos.center.z - 0.03f) continue;
                if (caja.size.x > ojos.size.x * 0.85f) continue;
            }
            candidatos.Add((grupo, caja));
        }

        float mejor = float.PositiveInfinity;
        (List<int> vertices, Bounds caja) mejorA = default;
        (List<int> vertices, Bounds caja) mejorB = default;
        for (int i = 0; i < candidatos.Count; i++)
        {
            for (int j = i + 1; j < candidatos.Count; j++)
            {
                Bounds a = candidatos[i].caja, b = candidatos[j].caja;
                if (Mathf.Sign(a.center.x) == Mathf.Sign(b.center.x)) continue;
                float dx = Mathf.Abs(Mathf.Abs(a.center.x) - Mathf.Abs(b.center.x));
                float dy = Mathf.Abs(a.center.y - b.center.y);
                float dz = Mathf.Abs(a.center.z - b.center.z);
                float dtam = Mathf.Abs(a.size.x - b.size.x) + Mathf.Abs(a.size.y - b.size.y) + Mathf.Abs(a.size.z - b.size.z);
                if (dx > 0.03f || dy > 0.02f || dz > 0.03f || dtam > 0.10f) continue;
                float nota = dx + dy + dz + dtam * 0.5f;
                if (nota >= mejor) continue;
                mejor = nota;
                mejorA = candidatos[i];
                mejorB = candidatos[j];
            }
        }

        if (mejorA.vertices == null || mejorB.vertices == null) return false;
        if (mejorA.caja.center.x < mejorB.caja.center.x)
        {
            izquierda = mejorA.vertices.ToArray();
            derecha = mejorB.vertices.ToArray();
            centroIzquierdo = mejorA.caja.center;
            centroDerecho = mejorB.caja.center;
        }
        else
        {
            izquierda = mejorB.vertices.ToArray();
            derecha = mejorA.vertices.ToArray();
            centroIzquierdo = mejorB.caja.center;
            centroDerecho = mejorA.caja.center;
        }
        return true;
    }

    private void Avisar(string mensaje)
    {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        if (_avisadoSinCejas) return;
        _avisadoSinCejas = true;
        Debug.LogWarning($"[CejasPorEmocion:{name}] {mensaje}", this);
#endif
    }

    private static void Destruir(Mesh malla)
    {
        if (malla == null) return;
        if (Application.isPlaying) Destroy(malla);
        else DestroyImmediate(malla);
    }
}
