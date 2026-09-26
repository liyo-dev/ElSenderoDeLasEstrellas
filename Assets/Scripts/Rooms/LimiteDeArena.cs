using UnityEngine;

/// Marca en el suelo el borde de una arena en modo radio (BattleEncounterSO): un anillo fino y
/// difuso, con el color de la luz de las estrellas, que apenas se ve desde el centro y se aviva
/// cuando el jugador se acerca al límite. Sustituye a la pared de antes («era un horror, algo más
/// sutil», Raúl, 26 sep 2026). Lo crea y lo retira BossArenaController; vale para todas las
/// arenas en modo radio.
///
/// Sigue el terreno: cada punto del anillo se apoya en el suelo (capa Floor). No colisiona con
/// nada: el límite de verdad lo sigue poniendo BossArenaController (empuje + «no puedes huir»).
[DisallowMultipleComponent]
public sealed class LimiteDeArena : MonoBehaviour
{
    private const string RutaMaterial = "Materials/Mat_LimiteArena";
    private const int Segmentos = 160;
    private const float Ancho = 0.45f;
    private const float AlturaSobreSuelo = 0.07f;

    // Desde el centro casi no se ve; a menos de DistanciaAviso del borde, se aviva.
    private const float AlfaLejos = 0.18f;
    private const float AlfaCerca = 0.8f;
    private const float DistanciaAviso = 6f;
    private const float VelocidadAlfa = 1.5f;      // unidades de alfa por segundo
    private const float IntervaloComprobacion = 0.1f;
    private static readonly Color ColorLuz = new Color(1f, 0.87f, 0.58f);

    private LineRenderer _linea;
    private Material _material;
    private Texture2D _degradado;
    private Vector3 _centro;
    private float _radio;
    private float _alfa;
    private float _alfaObjetivo;
    private float _siguienteComprobacion;
    private bool _retirando;

    /// Pone el anillo del borde de la arena. Devuelve null si no encuentra el material.
    public static LimiteDeArena Crear(Transform padre, Vector3 centro, float radio)
    {
        var material = Resources.Load<Material>(RutaMaterial);
        if (material == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[LimiteDeArena] Falta el material Resources/{RutaMaterial}: la arena no marca su borde.");
#endif
            return null;
        }

        var go = new GameObject("LimiteDeArena");
        go.transform.SetParent(padre, false);
        go.transform.position = centro;
        var limite = go.AddComponent<LimiteDeArena>();
        limite.Montar(material, centro, radio);
        return limite;
    }

    /// Lo apaga poco a poco y se destruye.
    public void Retirar()
    {
        _retirando = true;
        _alfaObjetivo = 0f;
    }

    private void Montar(Material material, Vector3 centro, float radio)
    {
        _centro = centro;
        _radio = radio;

        // Degradado a lo ancho de la línea: los bordes se funden con el suelo.
        _degradado = new Texture2D(4, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[4 * 32];
        for (int y = 0; y < 32; y++)
        {
            float v = (y + 0.5f) / 32f;
            float a = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 2f);
            for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a);
        }
        _degradado.SetPixels32(px);
        _degradado.Apply(false, true);

        _material = new Material(material);
        _material.SetTexture("_BaseMap", _degradado);

        _linea = gameObject.AddComponent<LineRenderer>();
        _linea.sharedMaterial = _material;
        _linea.useWorldSpace = true;
        _linea.loop = true;
        _linea.alignment = LineAlignment.TransformZ;   // tumbada en el suelo, no mirando a cámara
        _linea.textureMode = LineTextureMode.Stretch;
        _linea.widthMultiplier = Ancho;
        _linea.numCornerVertices = 0;
        _linea.numCapVertices = 0;
        _linea.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _linea.receiveShadows = false;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // TransformZ hacia abajo: la cinta queda horizontal

        int suelo = LayerMask.GetMask("Floor");
        var puntos = new Vector3[Segmentos];
        for (int i = 0; i < Segmentos; i++)
        {
            float ang = i * Mathf.PI * 2f / Segmentos;
            Vector3 p = centro + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radio;
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, suelo, QueryTriggerInteraction.Ignore))
                p.y = hit.point.y;
            puntos[i] = p + Vector3.up * AlturaSobreSuelo;
        }
        _linea.positionCount = Segmentos;
        _linea.SetPositions(puntos);

        _alfa = 0f;
        _alfaObjetivo = AlfaLejos;
        Pintar();
    }

    private void Update()
    {
        if (!_retirando && Time.time >= _siguienteComprobacion)
        {
            _siguienteComprobacion = Time.time + IntervaloComprobacion;
            var jugador = PlayerService.Player;
            if (jugador != null)
            {
                Vector3 d = jugador.transform.position - _centro;
                d.y = 0f;
                float alBorde = _radio - d.magnitude;
                _alfaObjetivo = Mathf.Lerp(AlfaCerca, AlfaLejos, Mathf.Clamp01(alBorde / DistanciaAviso));
            }
        }

        if (!Mathf.Approximately(_alfa, _alfaObjetivo))
        {
            _alfa = Mathf.MoveTowards(_alfa, _alfaObjetivo, VelocidadAlfa * Time.deltaTime);
            Pintar();
        }
        else if (_retirando)
        {
            Destroy(gameObject);
        }
    }

    private void Pintar()
    {
        var c = ColorLuz;
        c.a = _alfa;
        _linea.startColor = c;
        _linea.endColor = c;
    }

    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
        if (_degradado != null) Destroy(_degradado);
    }
}
