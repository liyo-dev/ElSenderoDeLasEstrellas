using System.Collections;
using UnityEngine;

/// <summary>
/// La estrella de madera de Will hecha constelación: cinco estrellas brillantes en las puntas de una
/// estrella de cinco puntas un poco torcida (como la que compró en el pueblo), otras cinco más
/// pequeñas en los huecos entre punta y punta, y el contorno de la estrella trazado con líneas de luz. Se construye en tiempo de ejecución, mirando al punto desde el que se ve,
/// y se puede dibujar poco a poco (aparecen las estrellas y se trazan las líneas) o mostrar ya hecha.
///
/// La usan el final del Archimago (se convierte en ella) y el cielo del epílogo (sigue ahí: «se la
/// quedó él»), así que la forma vive aquí una sola vez.
/// </summary>
public class ConstelacionDeLaEstrella : MonoBehaviour
{
    // Puntas en el plano de la constelación (x derecha, y arriba), radio 1. Torcida a propósito,
    // como la estrella tallada a mano: la punta de arriba vencida a la derecha, la de abajo a la
    // izquierda corta y gorda, la de abajo a la derecha larga y la de la izquierda subida.
    private static readonly Vector2[] Puntas =
    {
        new Vector2( 0.30f,  1.05f),
        new Vector2( 1.02f,  0.10f),
        new Vector2( 0.40f, -0.98f),
        new Vector2(-0.46f, -0.56f),
        new Vector2(-1.00f,  0.50f),
    };
    // Distancia al centro de cada hueco entre la punta i y la siguiente, y cuánto se desvía de la
    // bisectriz (grados): desigual, como la talla.
    private static readonly float[] Huecos = { 0.35f, 0.50f, 0.31f, 0.47f, 0.38f };
    private static readonly float[] GiroDeHuecos = { 9f, -11f, 7f, -5f, 13f };

    private static readonly Color ColorDeEstrella = new Color(3.2f, 2.8f, 1.9f, 1f);
    private static readonly Color ColorDeLinea = new Color(2.2f, 1.9f, 1.2f, 0.85f);

    private Transform[] _estrellas;
    private Transform[] _huecos;
    private LineRenderer _linea;
    private Vector3[] _puntosDelTrazo;
    private float _radio = 1f;
    private float _brillo = 1f;
    private float _visible = 1f;   // 0..1: para que aparezca poco a poco en el cielo
    private Material _matEstrella, _matLinea;

    /// <summary>Puntas de la constelación en coordenadas del mundo (para que algo vuele a ellas).</summary>
    public Vector3 Punta(int i) => _estrellas[Mathf.Clamp(i, 0, _estrellas.Length - 1)].position;
    public int CuantasPuntas => Puntas.Length;

    /// <summary>
    /// Crea la constelación con su centro en <paramref name="centro"/>, de <paramref name="radio"/>
    /// metros, de cara a <paramref name="desde"/> (el punto desde el que se mira). Empieza invisible.
    /// </summary>
    public static ConstelacionDeLaEstrella Crear(Vector3 centro, Vector3 desde, float radio, Transform padre = null)
    {
        var go = new GameObject("Constelacion_EstrellaDeWill");
        if (padre != null) go.transform.SetParent(padre, true);
        go.transform.position = centro;
        Vector3 haciaElQueMira = desde - centro;
        if (haciaElQueMira.sqrMagnitude < 1e-4f) haciaElQueMira = Vector3.back;
        go.transform.rotation = Quaternion.LookRotation(-haciaElQueMira.normalized, Vector3.up);
        var c = go.AddComponent<ConstelacionDeLaEstrella>();
        c.Construir(radio);
        return c;
    }

    private void Construir(float radio)
    {
        _radio = radio;
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        _matEstrella = new Material(unlit) { name = "Constelacion_Estrella" };
        _matEstrella.SetColor("_BaseColor", ColorDeEstrella);
        _matLinea = new Material(Shader.Find("Sprites/Default")) { name = "Constelacion_Linea" };
        _matLinea.color = ColorDeLinea;

        _estrellas = new Transform[Puntas.Length];
        for (int i = 0; i < Puntas.Length; i++)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            e.name = "Estrella_" + i;
            Destroy(e.GetComponent<Collider>());
            e.transform.SetParent(transform, false);
            e.transform.localPosition = new Vector3(Puntas[i].x, Puntas[i].y, 0f) * radio;
            e.transform.localScale = Vector3.zero;
            var mr = e.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _matEstrella;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _estrellas[i] = e.transform;
        }

        _huecos = new Transform[Puntas.Length];
        for (int i = 0; i < Puntas.Length; i++)
        {
            var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            e.name = "Hueco_" + i;
            Destroy(e.GetComponent<Collider>());
            e.transform.SetParent(transform, false);
            e.transform.localPosition = Hueco(i) * radio;
            e.transform.localScale = Vector3.zero;
            var mr = e.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _matEstrella;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _huecos[i] = e.transform;
        }

        var goLinea = new GameObject("Trazo");
        goLinea.transform.SetParent(transform, false);
        _linea = goLinea.AddComponent<LineRenderer>();
        _linea.useWorldSpace = false;
        _linea.sharedMaterial = _matLinea;
        _linea.widthMultiplier = radio * 0.022f;
        _linea.numCapVertices = 4;
        _linea.numCornerVertices = 4;
        _linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _linea.positionCount = 0;

        // El contorno: punta, hueco, punta, hueco... y vuelta a la primera punta.
        _puntosDelTrazo = new Vector3[Puntas.Length * 2 + 1];
        for (int i = 0; i < Puntas.Length; i++)
        {
            _puntosDelTrazo[i * 2] = new Vector3(Puntas[i].x, Puntas[i].y, 0f) * radio;
            _puntosDelTrazo[i * 2 + 1] = Hueco(i) * radio;
        }
        _puntosDelTrazo[_puntosDelTrazo.Length - 1] = _puntosDelTrazo[0];
    }

    private static Vector3 Hueco(int i)
    {
        Vector2 a = Puntas[i], b = Puntas[(i + 1) % Puntas.Length];
        Vector2 d = (Vector2)(Quaternion.Euler(0f, 0f, GiroDeHuecos[i]) * (a + b).normalized) * Huecos[i];
        return new Vector3(d.x, d.y, 0f);
    }

    /// <summary>Enciende la estrella <paramref name="i"/> (con un pequeño destello).</summary>
    public void EncenderPunta(int i) => StartCoroutine(Co_Encender(i));

    private IEnumerator Co_Encender(int i)
    {
        if (i < 0 || i >= _estrellas.Length) yield break;
        float tamano = _radio * 0.07f;
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            float k = t / 0.45f;
            float s = Mathf.Lerp(0f, tamano * 2.2f, Mathf.Sin(Mathf.Min(k, 0.6f) / 0.6f * Mathf.PI * 0.5f));
            if (k > 0.6f) s = Mathf.Lerp(tamano * 2.2f, tamano, (k - 0.6f) / 0.4f);
            _estrellas[i].localScale = Vector3.one * s;
            yield return null;
        }
        _estrellas[i].localScale = Vector3.one * tamano;
    }

    /// <summary>Traza las líneas del pentagrama en <paramref name="segundos"/>.</summary>
    public IEnumerator TrazarLineas(float segundos)
    {
        int tramos = _puntosDelTrazo.Length - 1;
        float t = 0f;
        while (t < segundos)
        {
            t += Time.deltaTime;
            float avance = Mathf.Clamp01(t / segundos) * tramos;
            int completos = Mathf.FloorToInt(avance);
            float parcial = avance - completos;
            for (int h = 0; h < _huecos.Length; h++)
                if (completos >= h * 2 + 1 && _huecos[h].localScale.x <= 0f) StartCoroutine(Co_Crecer(_huecos[h], _radio * 0.04f));
            int n = Mathf.Min(completos + 2, _puntosDelTrazo.Length);
            _linea.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                if (i <= completos) _linea.SetPosition(i, _puntosDelTrazo[i]);
                else _linea.SetPosition(i, Vector3.Lerp(_puntosDelTrazo[i - 1], _puntosDelTrazo[i], parcial));
            }
            yield return null;
        }
        _linea.positionCount = _puntosDelTrazo.Length;
        _linea.SetPositions(_puntosDelTrazo);
        foreach (var h in _huecos) if (h.localScale.x <= 0f) h.localScale = Vector3.one * _radio * 0.04f;
    }

    private IEnumerator Co_Crecer(Transform estrella, float tamano)
    {
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            estrella.localScale = Vector3.one * Mathf.Lerp(0.0001f, tamano, t / 0.3f);
            yield return null;
        }
        estrella.localScale = Vector3.one * tamano;
    }

    /// <summary>Dibuja la constelación entera: primero las estrellas, luego las líneas.</summary>
    public IEnumerator Dibujar(float segundos)
    {
        float porEstrella = segundos * 0.45f / _estrellas.Length;
        for (int i = 0; i < _estrellas.Length; i++)
        {
            EncenderPunta(i);
            yield return new WaitForSeconds(porEstrella);
        }
        yield return TrazarLineas(segundos * 0.55f);
    }

    /// <summary>La constelación ya hecha, sin animación.</summary>
    public void MostrarYa()
    {
        float tamano = _radio * 0.07f;
        foreach (var e in _estrellas) e.localScale = Vector3.one * tamano;
        foreach (var h in _huecos) h.localScale = Vector3.one * _radio * 0.04f;
        _linea.positionCount = _puntosDelTrazo.Length;
        _linea.SetPositions(_puntosDelTrazo);
    }

    /// <summary>Multiplica el brillo (1 = normal). Con bloom, más de 1 resplandece más.</summary>
    public void Brillo(float multiplicador)
    {
        _brillo = Mathf.Max(0f, multiplicador);
        AplicarColores();
    }

    /// <summary>La constelación ya hecha, que va saliendo en el cielo en <paramref name="segundos"/>.</summary>
    public void Aparecer(float segundos)
    {
        MostrarYa();
        _visible = 0f;
        AplicarColores();
        StartCoroutine(Co_Aparecer(Mathf.Max(0.1f, segundos)));
    }

    private IEnumerator Co_Aparecer(float segundos)
    {
        for (float t = 0f; t < segundos; t += Time.deltaTime)
        {
            _visible = Mathf.SmoothStep(0f, 1f, t / segundos);
            AplicarColores();
            float tamano = _radio * 0.07f * Mathf.Lerp(0.2f, 1f, _visible);
            foreach (var e in _estrellas) e.localScale = Vector3.one * tamano;
            foreach (var h in _huecos) h.localScale = Vector3.one * _radio * 0.04f * Mathf.Lerp(0.2f, 1f, _visible);
            yield return null;
        }
        _visible = 1f;
        AplicarColores();
        MostrarYa();
    }

    private void AplicarColores()
    {
        _matEstrella.SetColor("_BaseColor", ColorDeEstrella * (_brillo * _visible));
        var c = ColorDeLinea * _brillo;
        c.a = ColorDeLinea.a * _visible;
        _matLinea.color = c;
    }

    private void Update()
    {
        // Titileo suave, distinto en cada estrella.
        float tamano = _radio * 0.07f;
        for (int i = 0; i < _estrellas.Length; i++)
        {
            if (_estrellas[i].localScale.x < tamano * 0.9f || _estrellas[i].localScale.x > tamano * 1.6f) continue;
            float s = tamano * (1f + Mathf.Sin(Time.time * (1.7f + i * 0.37f) + i) * 0.12f);
            _estrellas[i].localScale = Vector3.one * s;
        }
    }

    private void OnDestroy()
    {
        if (_matEstrella != null) Destroy(_matEstrella);
        if (_matLinea != null) Destroy(_matLinea);
    }
}
