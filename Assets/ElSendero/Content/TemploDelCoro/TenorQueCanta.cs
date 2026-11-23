using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// El Tenor canta de verdad: la boca se abre con cada sílaba de la canción y, en las frases fuertes y
/// sostenidas, se lleva las manos a la boca (con el micro en la derecha). Lee el volumen de la fuente
/// del <see cref="TempoClock"/>: un seguidor rápido (las sílabas) contra uno lento (la frase), así la boca
/// se mueve aunque la mezcla vaya siempre alta. Mueve los pivotes de la mandíbula y las manos; los
/// golpes de <see cref="PulseMotion"/> van en los recortes de dentro y se suman encima.
/// </summary>
[DisallowMultipleComponent]
public sealed class TenorQueCanta : MonoBehaviour
{
    [SerializeField] private TempoClock reloj;
    [Header("Golpe del regalo")]
    [SerializeField] private Transform siguienteMarca;
    [SerializeField] private Collider pasoQueBloquea;
    private Transform _cuerpo;
    private Vector3 _posicionInicial;
    private Quaternion _giroInicial;
    private bool _desplazado;
    private float _inicioDelEmpujon;

    [Header("Boca")]
    [Tooltip("Pivote de la mandíbula (el recorte, dentro).")]
    [SerializeField] private Transform mandibula;
    [Tooltip("Lo que baja la mandíbula con la boca abierta del todo (en ejes de la cara).")]
    [SerializeField] private Vector3 bajadaDeLaBoca = new Vector3(0f, -0.55f, 0f);
    [Tooltip("Lo que gira la mandíbula con la boca abierta del todo (grados).")]
    [SerializeField] private float giroDeLaBoca = -3f;
    [Tooltip("Apertura mínima mientras suena la canción (nunca canta con la boca cerrada del todo).")]
    [SerializeField, Range(0f, 0.6f)] private float aperturaMinima = 0.15f;
    [Tooltip("Sensibilidad a las sílabas: más alto, abre más con menos.")]
    [SerializeField, Min(0.1f)] private float sensibilidad = 2.5f;

    [Header("Manos a la boca")]
    [Tooltip("Pivotes de las manos (los recortes, dentro).")]
    [SerializeField] private Transform[] manos = System.Array.Empty<Transform>();
    [Tooltip("Cuánto se acercan las manos a la boca: 0, en el centro de la boca; 1, donde están en reposo.")]
    [SerializeField, Range(0f, 1f)] private float separacionEnLaBoca = 0.4f;
    [Tooltip("Grados que se giran hacia dentro al llevárselas a la boca.")]
    [SerializeField] private float giroEnLaBoca = 18f;
    [Tooltip("Volumen de la frase (respecto al máximo de la canción) a partir del cual se lleva las manos a la boca.")]
    [SerializeField, Range(0.3f, 1f)] private float frasesFuertes = 0.72f;
    [Tooltip("Segundos que tarda en llevarse las manos a la boca (y en bajarlas).")]
    [SerializeField, Min(0.05f)] private float segundosDeLasManos = 0.35f;

    private readonly float[] _muestras = new float[256];
    private Vector3 _bocaEnReposo;
    private Quaternion _giroDeLaBocaEnReposo;
    private Vector3[] _manosEnReposo, _manosEnLaBoca;
    private Quaternion[] _giroDeLasManosEnReposo, _giroDeLasManosEnLaBoca;
    private float _rapido, _lento, _maximo = 0.05f, _boca, _manosArriba;

    private void Awake()
    {
        _cuerpo = transform;
        _posicionInicial = _cuerpo.position;
        _giroInicial = _cuerpo.rotation;
        if (mandibula != null)
        {
            _bocaEnReposo = mandibula.localPosition;
            _giroDeLaBocaEnReposo = mandibula.localRotation;
        }
        Vector3 boca = mandibula != null ? transform.InverseTransformPoint(mandibula.position) : Vector3.zero;
        _manosEnReposo = new Vector3[manos.Length];
        _manosEnLaBoca = new Vector3[manos.Length];
        _giroDeLasManosEnReposo = new Quaternion[manos.Length];
        _giroDeLasManosEnLaBoca = new Quaternion[manos.Length];
        for (int i = 0; i < manos.Length; i++)
        {
            if (manos[i] == null) continue;
            Vector3 reposo = manos[i].localPosition;
            float lado = Mathf.Sign(reposo.x - boca.x);
            _manosEnReposo[i] = reposo;
            _manosEnLaBoca[i] = new Vector3(boca.x + (reposo.x - boca.x) * separacionEnLaBoca, boca.y, reposo.z);
            _giroDeLasManosEnReposo[i] = manos[i].localRotation;
            _giroDeLasManosEnLaBoca[i] = Quaternion.Euler(0f, 0f, lado * giroEnLaBoca) * manos[i].localRotation;
        }
    }

    private void Update()
    {
        if (_desplazado && siguienteMarca != null)
        {
            float progreso = Mathf.Clamp01(((float)reloj.SongTime - _inicioDelEmpujon) / 0.85f);
            float viaje = 1f - (1f - progreso) * (1f - progreso);
            float vuelo = Mathf.Sin(progreso * Mathf.PI);
            Vector3 posicion = Vector3.Lerp(_posicionInicial, siguienteMarca.position, viaje);
            posicion.y += 4f * vuelo;
            Quaternion giro = Quaternion.Slerp(_giroInicial, siguienteMarca.rotation, viaje)
                * Quaternion.Euler(vuelo * 20f, 0f, 0f);
            _cuerpo.SetPositionAndRotation(posicion, giro);
        }
        float volumen = 0f;
        var fuente = reloj != null ? reloj.Source : null;
        bool suena = fuente != null && fuente.isPlaying;
        if (suena)
        {
            fuente.GetOutputData(_muestras, 0);
            float suma = 0f;
            for (int i = 0; i < _muestras.Length; i++) suma += _muestras[i] * _muestras[i];
            volumen = Mathf.Sqrt(suma / _muestras.Length);
        }

        float dt = Time.deltaTime;
        _rapido = Mathf.Lerp(_rapido, volumen, 1f - Mathf.Exp(-dt * (volumen > _rapido ? 40f : 14f)));
        _lento = Mathf.Lerp(_lento, volumen, 1f - Mathf.Exp(-dt * 2.5f));
        _maximo = Mathf.Max(_lento, _maximo - dt * 0.01f);

        // Sílabas: lo que el volumen de ahora asoma por encima de la frase.
        float silaba = Mathf.Clamp01((_rapido - _lento * 0.75f) / (_lento * 0.5f + 0.005f) * sensibilidad * 0.5f);
        float objetivo = suena && _lento > 0.005f ? Mathf.Lerp(aperturaMinima, 1f, silaba) : 0f;
        _boca = Mathf.Lerp(_boca, objetivo, 1f - Mathf.Exp(-dt * 22f));

        bool fuerte = suena && _lento >= _maximo * frasesFuertes;
        _manosArriba = Mathf.MoveTowards(_manosArriba, fuerte ? 1f : 0f, dt / segundosDeLasManos);

        if (mandibula != null)
        {
            mandibula.localPosition = _bocaEnReposo + bajadaDeLaBoca * _boca;
            mandibula.localRotation = _giroDeLaBocaEnReposo * Quaternion.Euler(0f, 0f, giroDeLaBoca * _boca);
        }
        float u = Mathf.SmoothStep(0f, 1f, _manosArriba);
        for (int i = 0; i < manos.Length; i++)
        {
            if (manos[i] == null) continue;
            manos[i].localPosition = Vector3.Lerp(_manosEnReposo[i], _manosEnLaBoca[i], u);
            manos[i].localRotation = Quaternion.Slerp(_giroDeLasManosEnReposo[i], _giroDeLasManosEnLaBoca[i], u);
        }
    }

    public void EmpujarConElRegalo()
    {
        if (_desplazado || siguienteMarca == null) return;
        _inicioDelEmpujon = (float)reloj.SongTime;
        _desplazado = true;
        if (pasoQueBloquea != null) pasoQueBloquea.enabled = false;
    }

    public void PrepararMarcaDelRegalo(float zDeSalida)
    {
        if (siguienteMarca == null) return;
        _desplazado = zDeSalida > _posicionInicial.z + 0.5f;
        _inicioDelEmpujon = float.NegativeInfinity;
        if (pasoQueBloquea != null) pasoQueBloquea.enabled = !_desplazado;
        if (_desplazado) _cuerpo.SetPositionAndRotation(siguienteMarca.position, siguienteMarca.rotation);
        else _cuerpo.SetPositionAndRotation(_posicionInicial, _giroInicial);
    }
}
