using UnityEngine;

/// <summary>
/// Cielo estrellado de las noches de la cinemática final (la Piedra Ancestral y el río), que se
/// ruedan en una escena propia sin <see cref="DayNightCycle"/>: el domo oficial
/// (<see cref="NightSkyStarSpawner"/>) y las estrellas fugaces oficiales (<see cref="EstrellasFugaces"/>)
/// dependen del ciclo, así que aquí se dibujan con el mismo criterio (estrellas al fondo, siempre
/// centradas en la cámara que rueda) en una sola malla.
///
/// Tiene dos modos: noche normal (polvo de estrellas) y noche especial (más estrellas, la Vía
/// Láctea cruzando el cielo y estrellas fugaces de vez en cuando).
/// </summary>
[DefaultExecutionOrder(1100)]
public class CieloEstrelladoDelFinal : MonoBehaviour
{
    private const int EstrellasNormales = 700;
    private const int EstrellasEspeciales = 1500;
    private const int EstrellasDeLaViaLactea = 1300;
    private const int NubesDeLaViaLactea = 46;
    private const float IntervaloDeTitileo = 0.06f;

    private static readonly Color Blanca = new(1f, 0.98f, 0.94f, 1f);
    private static readonly Color Azulada = new(0.78f, 0.86f, 1f, 1f);
    private static readonly Color Dorada = new(1f, 0.88f, 0.62f, 1f);

    private Camera _camara;
    private float _radio = 140f;
    private Mesh _malla;
    private Material _material;
    private Material _materialDeNubes;
    private Texture2D _textura;
    private Transform _domo;
    private Transform _nubes;
    private Color[] _colores;
    private Color[] _coloresBase;
    private float[] _fase;
    private float[] _velocidad;
    private float _titileo;
    private float _brillo = 1f;
    private float _brilloObjetivo = 1f;
    private float _velocidadDeBrillo = 1f;
    private bool _especial;

    private TrailRenderer[] _fugaces;
    private Vector3[] _fugazDesde, _fugazHasta;
    private float[] _fugazT, _fugazDura;
    private float _siguienteFugaz;

    /// <summary>Crea el cielo colgado de <paramref name="padre"/>. Empieza apagado (brillo 0).</summary>
    public static CieloEstrelladoDelFinal Crear(Transform padre, bool especial, int semilla)
    {
        var go = new GameObject(especial ? "Cielo_Estrellado_Especial" : "Cielo_Estrellado");
        go.transform.SetParent(padre, false);
        var cielo = go.AddComponent<CieloEstrelladoDelFinal>();
        cielo.Construir(especial, semilla);
        return cielo;
    }

    /// <summary>Lleva el brillo del cielo a <paramref name="objetivo"/> (0 apagado, 1 normal).</summary>
    public void FundirA(float objetivo, float segundos)
    {
        _brilloObjetivo = Mathf.Max(0f, objetivo);
        _velocidadDeBrillo = segundos > 0.01f ? Mathf.Abs(_brilloObjetivo - _brillo) / segundos : 1000f;
    }

    public void BrilloYa(float valor)
    {
        _brillo = _brilloObjetivo = Mathf.Max(0f, valor);
        AplicarBrillo();
    }

    private void Construir(bool especial, int semilla)
    {
        _especial = especial;
        _textura = CrearTexturaDeEstrella();
        _material = new Material(Shader.Find("Sprites/Default")) { name = "Cielo del final (estrellas)", mainTexture = _textura };
        // Después del cielo (que URP pinta tras lo opaco) y detrás de todo por profundidad.
        _material.renderQueue = 2900;

        var estado = Random.state;
        Random.InitState(semilla);

        int normales = especial ? EstrellasEspeciales : EstrellasNormales;
        int banda = especial ? EstrellasDeLaViaLactea : 0;
        int total = normales + banda;
        var vertices = new Vector3[total * 4];
        var uvs = new Vector2[total * 4];
        _colores = new Color[total * 4];
        _coloresBase = new Color[total];
        _fase = new float[total];
        _velocidad = new float[total];
        var triangulos = new int[total * 6];

        // Eje de la Vía Láctea: un círculo máximo inclinado que cruza el cielo de lado a lado.
        Vector3 polo = Quaternion.Euler(28f, 35f, 0f) * Vector3.up;

        for (int i = 0; i < total; i++)
        {
            bool deBanda = i >= normales;
            Vector3 dir = deBanda ? DireccionEnLaBanda(polo) : DireccionAlta();
            float tamano = deBanda ? Random.Range(0.25f, 0.7f) : Mathf.Lerp(0.35f, 1.9f, Mathf.Pow(Random.value, 4f));
            float intensidad = deBanda ? Random.Range(0.35f, 0.9f) : Random.Range(0.55f, 1.6f);
            if (!deBanda && Random.value < 0.025f) { tamano *= 1.8f; intensidad *= 1.6f; }
            Color c = Random.value < 0.18f ? Azulada : (Random.value < 0.12f ? Dorada : Blanca);
            _coloresBase[i] = c * intensidad;
            _fase[i] = Random.value * 10f;
            _velocidad[i] = Random.Range(0.6f, 2.4f);
            PonerCuadro(vertices, uvs, triangulos, i, dir * _radio, tamano * _radio / 140f);
        }
        Random.state = estado;

        _malla = new Mesh { name = "Cielo del final" };
        _malla.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _malla.vertices = vertices;
        _malla.uv = uvs;
        _malla.triangles = triangulos;
        _malla.bounds = new Bounds(Vector3.zero, Vector3.one * _radio * 2.5f);

        _domo = new GameObject("Estrellas").transform;
        _domo.SetParent(transform, false);
        _domo.gameObject.AddComponent<MeshFilter>().sharedMesh = _malla;
        var mr = _domo.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (especial)
        {
            ConstruirNubesDeLaViaLactea(polo, semilla);
            ConstruirFugaces();
        }

        _brillo = _brilloObjetivo = 0f;
        AplicarBrillo();
    }

    private static Vector3 DireccionAlta()
    {
        // Más estrellas arriba que en el horizonte, y ninguna por debajo.
        Vector3 d = Random.onUnitSphere;
        d.y = Mathf.Abs(d.y);
        d.y = Mathf.Lerp(0.04f, 1f, Mathf.Pow(d.y, 0.75f));
        return d.normalized;
    }

    private static Vector3 DireccionEnLaBanda(Vector3 polo)
    {
        for (int intento = 0; intento < 8; intento++)
        {
            Vector3 d = Random.onUnitSphere;
            float desvio = (Random.value + Random.value + Random.value - 1.5f) * 0.32f;
            d = Vector3.ProjectOnPlane(d, polo).normalized + polo * desvio;
            d.Normalize();
            if (d.y > 0.03f) return d;
        }
        return DireccionAlta();
    }

    private static void PonerCuadro(Vector3[] v, Vector2[] uv, int[] t, int i, Vector3 centro, float tamano)
    {
        Vector3 haciaFuera = centro.normalized;
        Vector3 derecha = Vector3.Cross(Mathf.Abs(haciaFuera.y) > 0.95f ? Vector3.forward : Vector3.up, haciaFuera).normalized;
        Vector3 arriba = Vector3.Cross(haciaFuera, derecha);
        derecha *= tamano * 0.5f;
        arriba *= tamano * 0.5f;
        int b = i * 4;
        v[b] = centro - derecha - arriba;
        v[b + 1] = centro + derecha - arriba;
        v[b + 2] = centro + derecha + arriba;
        v[b + 3] = centro - derecha + arriba;
        uv[b] = new Vector2(0, 0); uv[b + 1] = new Vector2(1, 0); uv[b + 2] = new Vector2(1, 1); uv[b + 3] = new Vector2(0, 1);
        int k = i * 6;
        t[k] = b; t[k + 1] = b + 2; t[k + 2] = b + 1;
        t[k + 3] = b; t[k + 4] = b + 3; t[k + 5] = b + 2;
    }

    private void ConstruirNubesDeLaViaLactea(Vector3 polo, int semilla)
    {
        var estado = Random.state;
        Random.InitState(semilla * 7 + 3);
        var vertices = new Vector3[NubesDeLaViaLactea * 4];
        var uvs = new Vector2[NubesDeLaViaLactea * 4];
        var colores = new Color[NubesDeLaViaLactea * 4];
        var triangulos = new int[NubesDeLaViaLactea * 6];
        Vector3 eje = Vector3.Cross(polo, Vector3.right).normalized;
        for (int i = 0; i < NubesDeLaViaLactea; i++)
        {
            float a = (i / (float)NubesDeLaViaLactea) * Mathf.PI * 2f;
            Vector3 d = Quaternion.AngleAxis(a * Mathf.Rad2Deg, polo) * eje;
            d += polo * Random.Range(-0.08f, 0.08f);
            d.Normalize();
            if (d.y < 0.02f) d.y = Mathf.Abs(d.y) + 0.02f;
            d.Normalize();
            PonerCuadro(vertices, uvs, triangulos, i, d * _radio * 1.02f, _radio * Random.Range(0.22f, 0.38f));
            Color c = Color.Lerp(new Color(0.55f, 0.45f, 0.85f), new Color(0.45f, 0.62f, 0.95f), Random.value) * Random.Range(0.10f, 0.2f);
            c.a = 1f;
            for (int k = 0; k < 4; k++) colores[i * 4 + k] = c;
        }
        Random.state = estado;

        var malla = new Mesh { name = "Vía Láctea del final", vertices = vertices, uv = uvs, colors = colores, triangles = triangulos };
        malla.bounds = new Bounds(Vector3.zero, Vector3.one * _radio * 2.5f);
        _materialDeNubes = new Material(Shader.Find("Sprites/Default")) { name = "Cielo del final (Vía Láctea)", mainTexture = CrearTexturaDeNube() };
        _materialDeNubes.renderQueue = 2899;
        _nubes = new GameObject("Via_Lactea").transform;
        _nubes.SetParent(transform, false);
        _nubes.gameObject.AddComponent<MeshFilter>().sharedMesh = malla;
        var mr = _nubes.gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _materialDeNubes;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private void ConstruirFugaces()
    {
        _fugaces = new TrailRenderer[2];
        _fugazDesde = new Vector3[2];
        _fugazHasta = new Vector3[2];
        _fugazT = new float[2];
        _fugazDura = new float[2];
        var mat = new Material(Shader.Find("Sprites/Default")) { name = "Estrella fugaz del final", color = new Color(4f, 3.4f, 2f, 1f) };
        for (int i = 0; i < _fugaces.Length; i++)
        {
            var go = new GameObject("Estrella_Fugaz_" + i);
            go.transform.SetParent(transform, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = 0.45f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            tr.widthMultiplier = _radio * 0.004f;
            tr.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.8f, 0.5f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.emitting = false;
            tr.enabled = false;
            _fugaces[i] = tr;
            _fugazT[i] = -1f;
        }
        _siguienteFugaz = Time.time + 4f;
    }

    private void LateUpdate()
    {
        if (_camara == null || !_camara.isActiveAndEnabled) _camara = SolYLunaEnElCielo.CamaraActual();
        if (_camara != null)
        {
            float lejos = _camara.farClipPlane * 0.9f;
            float escala = Mathf.Min(1f, lejos / (_radio * 1.05f));
            transform.position = _camara.transform.position;
            transform.localScale = Vector3.one * escala;
        }

        if (!Mathf.Approximately(_brillo, _brilloObjetivo))
        {
            _brillo = Mathf.MoveTowards(_brillo, _brilloObjetivo, _velocidadDeBrillo * Time.unscaledDeltaTime);
            AplicarBrillo();
        }

        bool visible = _brillo > 0.001f;
        if (_domo.gameObject.activeSelf != visible) _domo.gameObject.SetActive(visible);
        if (_nubes != null && _nubes.gameObject.activeSelf != visible) _nubes.gameObject.SetActive(visible);
        if (!visible) return;

        _titileo += Time.unscaledDeltaTime;
        if (_titileo >= IntervaloDeTitileo)
        {
            _titileo = 0f;
            Titilar();
        }
        if (_fugaces != null) MoverFugaces();
    }

    private void Titilar()
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < _coloresBase.Length; i++)
        {
            float k = 0.72f + 0.28f * Mathf.Sin(t * _velocidad[i] + _fase[i]);
            Color c = _coloresBase[i] * k;
            c.a = 1f;
            int b = i * 4;
            _colores[b] = _colores[b + 1] = _colores[b + 2] = _colores[b + 3] = c;
        }
        _malla.colors = _colores;
    }

    private void AplicarBrillo()
    {
        // Sprites/Default multiplica el color por su alfa: el brillo va en el alfa y el HDR en el color.
        float extra = Mathf.Max(1f, _brillo);
        if (_material != null) _material.color = new Color(2.2f * extra, 2.2f * extra, 2.2f * extra, Mathf.Clamp01(_brillo));
        if (_materialDeNubes != null) _materialDeNubes.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_brillo));
        if (_malla != null && _colores != null && _brillo > 0f) Titilar();
    }

    private void MoverFugaces()
    {
        if (_brillo > 0.6f && Time.time >= _siguienteFugaz)
        {
            _siguienteFugaz = Time.time + Random.Range(3.5f, 9f);
            for (int i = 0; i < _fugaces.Length; i++)
            {
                if (_fugazT[i] >= 0f) continue;
                Vector3 desde = DireccionAlta();
                desde.y = Mathf.Clamp(desde.y, 0.35f, 0.85f);
                desde.Normalize();
                Vector3 lado = Vector3.Cross(desde, Vector3.up).normalized;
                Vector3 hasta = (desde + lado * Random.Range(0.18f, 0.32f) * (Random.value < 0.5f ? -1f : 1f) + Vector3.down * Random.Range(0.08f, 0.16f)).normalized;
                _fugazDesde[i] = desde * _radio * 0.97f;
                _fugazHasta[i] = hasta * _radio * 0.97f;
                _fugazT[i] = 0f;
                _fugazDura[i] = Random.Range(0.55f, 0.95f);
                _fugaces[i].enabled = true;
                _fugaces[i].Clear();
                _fugaces[i].transform.localPosition = _fugazDesde[i];
                _fugaces[i].emitting = true;
                break;
            }
        }

        for (int i = 0; i < _fugaces.Length; i++)
        {
            if (_fugazT[i] < 0f) continue;
            _fugazT[i] += Time.unscaledDeltaTime / _fugazDura[i];
            float k = Mathf.Clamp01(_fugazT[i]);
            _fugaces[i].transform.localPosition = Vector3.Lerp(_fugazDesde[i], _fugazHasta[i], 1f - (1f - k) * (1f - k));
            if (k >= 1f)
            {
                _fugaces[i].emitting = false;
                if (_fugazT[i] > 1f + _fugaces[i].time / _fugazDura[i])
                {
                    _fugaces[i].enabled = false;
                    _fugazT[i] = -1f;
                }
            }
        }
    }

    private static Texture2D CrearTexturaDeEstrella()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Estrella del final", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float nucleo = Mathf.Clamp01(1f - r * 3.2f);
            float halo = Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * 0.5f;
            float cruz = Mathf.Clamp01(1f - Mathf.Abs(dx) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dy) * 1.4f) * 0.35f
                       + Mathf.Clamp01(1f - Mathf.Abs(dy) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(dx) * 1.4f) * 0.35f;
            float a = Mathf.Clamp01(nucleo + halo + cruz);
            px[y * n + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D CrearTexturaDeNube()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Nube de la Vía Láctea", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ruido = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.6f + 0.4f;
            px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f) * ruido);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private void OnDestroy()
    {
        if (_malla != null) Destroy(_malla);
        if (_material != null) Destroy(_material);
        if (_materialDeNubes != null) { Destroy(_materialDeNubes.mainTexture); Destroy(_materialDeNubes); }
        if (_textura != null) Destroy(_textura);
        if (_nubes != null) { var mf = _nubes.GetComponent<MeshFilter>(); if (mf != null) Destroy(mf.sharedMesh); }
    }
}
