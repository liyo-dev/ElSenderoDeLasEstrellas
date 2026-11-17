using UnityEngine;

/// Un ricito de viento: una línea fina que recorre una cicloide (con lazadas) en la dirección del
/// viento. Se dibuja sobre la marcha y se desvanece, como el trazo de una racha de aire.
[DisallowMultipleComponent]
public sealed class RizoDeViento : MonoBehaviour
{
    private const int Puntos = 48;

    private LineRenderer _linea;
    private Vector3[] _trayectoria;
    private float _vida, _tiempo, _ancho;
    private Color _color = Color.white;
    private bool _activo;

    public bool Activo => _activo;

    public static RizoDeViento Crear(Transform padre, Material material)
    {
        var go = new GameObject("Rizo");
        go.transform.SetParent(padre, false);
        var rizo = go.AddComponent<RizoDeViento>();
        rizo.Preparar(material);
        go.SetActive(false);
        return rizo;
    }

    private void Preparar(Material material)
    {
        _linea = gameObject.AddComponent<LineRenderer>();
        _linea.useWorldSpace = true;
        _linea.alignment = LineAlignment.View;
        _linea.textureMode = LineTextureMode.Stretch;
        _linea.numCapVertices = 2;
        _linea.numCornerVertices = 3;
        _linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _linea.receiveShadows = false;
        _linea.sharedMaterial = material;
        _linea.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(1f, 1f));
        _linea.positionCount = 0;
        _trayectoria = new Vector3[Puntos];
    }

    /// Dibuja un ricito desde `origen` avanzando en `direccion`, con `vueltas` lazadas.
    public void Lanzar(Vector3 origen, Vector3 direccion, float longitud, float radio, int vueltas,
        float vida, float ancho)
    {
        _vida = Mathf.Max(0.2f, vida);
        _tiempo = 0f;
        _ancho = Mathf.Max(0.005f, ancho);
        _activo = true;

        Vector3 dir = direccion;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
        dir.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, dir).normalized;

        for (int i = 0; i < Puntos; i++)
        {
            float t = i / (Puntos - 1f);
            float angulo = t * Mathf.Max(1, vueltas) * Mathf.PI * 2f;
            _trayectoria[i] = origen
                + dir * (t * longitud)
                + lado * (Mathf.Sin(angulo) * radio)
                + Vector3.up * ((Mathf.Cos(angulo) - 1f) * radio * 0.65f);
        }

        _linea.widthMultiplier = _ancho;
        _linea.positionCount = 2;
        _linea.SetPosition(0, _trayectoria[0]);
        _linea.SetPosition(1, _trayectoria[1]);
        gameObject.SetActive(true);
        AplicarColor(0f);
    }

    private void Update()
    {
        if (!_activo) return;

        _tiempo += Time.deltaTime;
        float progreso = Mathf.Clamp01(_tiempo / _vida);
        int visibles = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(progreso * 1.7f, 1f) * Puntos), 2, Puntos);
        _linea.positionCount = visibles;
        for (int i = 0; i < visibles; i++)
            _linea.SetPosition(i, _trayectoria[i]);

        // La cola se apaga y la cabeza va nítida; al final el ricito se desvanece entero.
        float vidaUtil = Mathf.Clamp01(1f - Mathf.InverseLerp(0.60f, 1f, progreso));
        AplicarColor(vidaUtil);

        if (progreso >= 1f)
        {
            _activo = false;
            gameObject.SetActive(false);
        }
    }

    private void AplicarColor(float alpha)
    {
        _linea.startColor = new Color(_color.r, _color.g, _color.b, alpha * 0.05f);
        _linea.endColor = new Color(_color.r, _color.g, _color.b, alpha);
    }
}
