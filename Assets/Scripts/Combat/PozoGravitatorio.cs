using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// Pozo que atrae al jugador hacia su centro mientras se carga y al final implosiona: hace daño y
/// aparta a quien siga cerca. La atracción es más lenta que correr, así que escapa quien corre en
/// contra. Mientras se carga, pisar su núcleo quema (daño periódico). Sirve para cualquier ataque
/// de enemigo de este tipo (el agujero negro del Mago Oscuro). El daño va por DanoEnZona: alcanza
/// al que va al mando y a los compañeros, cada uno con su vida (GDD § 6.1); la atracción solo
/// mueve al cuerpo del jugador (a los compañeros los mueve su navegación). Se crea con Crear() y
/// se destruye solo al acabar.
public sealed class PozoGravitatorio : MonoBehaviour
{
    public struct Config
    {
        public float carga;              // segundos atrayendo antes de implosionar
        public float radioAtraccion;     // más lejos no atrae
        public float velocidadAtraccion; // m/s cerca del centro; en el borde, un 35 %
        public float radioImplosion;     // radio del golpe final
        public float dano;
        public float empuje;             // fuerza con la que aparta al implosionar (0 = nada)
        public float alturaMax;          // por encima (volando) no atrae ni golpea; 0 = 4 m
        public GameObject vfxPozo;       // crece de escalaInicial a escalaFinal durante la carga
        public float escalaInicial;
        public float escalaFinal;
        public float alturaVfx;          // altura del efecto sobre el centro
        public GameObject vfxImplosion;
        public string sfxCarga;
        public string sfxImplosion;
        public float danoPorSegundoEnElNucleo; // daño mientras se pisa el núcleo durante la carga (0 = no quema)
        public float radioDelNucleo;           // radio del núcleo que quema (0 = 1,8 m)

    }

    /// Daño por segundo del núcleo cuando quien lanza el pozo no lo indica con un número.
    public const float DanoDelNucleoPorDefecto = 30f;

    private const float DuracionImplosion = 0.18f;
    private const float RadioDelNucleoPorDefecto = 1.8f;
    // El núcleo quema a golpes, no en cada paso de física: cada golpe pasa por las reglas de daño
    // (invulnerabilidad tras un golpe incluida) y se ve y se siente.
    private const float IntervaloDelNucleo = 0.25f;
    // Por debajo del centro, más allá de esto (otro piso) no alcanza.
    private const float ProfundidadMax = 2f;

    private Config _c;
    private Transform _jugador;
    private Rigidbody _rb;
    private PlayerHealthSystem _salud;
    private Transform _vfx;
    private bool _atrayendo;
    private float _siguienteGolpeDelNucleo;

    /// Ya ha implosionado.
    public bool Terminado { get; private set; }

    private float AlturaMax => _c.alturaMax > 0f ? _c.alturaMax : 4f;
    private float RadioDelNucleo => _c.radioDelNucleo > 0f ? _c.radioDelNucleo : RadioDelNucleoPorDefecto;

    public static PozoGravitatorio Crear(Vector3 centro, Config config)
    {
        var go = new GameObject("PozoGravitatorio");
        go.transform.position = centro;
        var pozo = go.AddComponent<PozoGravitatorio>();
        pozo._c = config;
        pozo.StartCoroutine(pozo.Vivir());
        return pozo;
    }

    private IEnumerator Vivir()
    {
        if (PlayerService.TryGetPlayer(out var jugador) && jugador != null)
        {
            _jugador = jugador.transform;
            _rb = jugador.GetComponent<Rigidbody>();
            _salud = jugador.GetComponent<PlayerHealthSystem>();
        }

        Vector3 puntoVfx = transform.position + Vector3.up * Mathf.Max(0.04f, _c.alturaVfx);
        if (_c.vfxPozo != null)
        {
            _vfx = Instantiate(_c.vfxPozo, puntoVfx, Quaternion.identity, transform).transform;
            _vfx.localScale = Vector3.one * _c.escalaInicial;
        }

        // Anillo de peligro a ras de suelo para que el área de atracción y peligro sea 100% visible
        var anilloSuelo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anilloSuelo.name = "MarcaSuelo_Pozo";
        anilloSuelo.transform.SetParent(transform, false);
        anilloSuelo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        anilloSuelo.transform.localScale = new Vector3(_c.radioAtraccion * 2f, 0.01f, _c.radioAtraccion * 2f);
        Destroy(anilloSuelo.GetComponent<Collider>());
        var shaderLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shaderLit != null)
        {
            var mat = new Material(shaderLit) { color = new Color(0.25f, 0.04f, 0.35f, 0.45f) };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.25f, 0.04f, 0.35f, 0.45f));
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.4f, 0.05f, 0.55f));
            }
            anilloSuelo.GetComponent<Renderer>().sharedMaterial = mat;
        }

        if (!string.IsNullOrEmpty(_c.sfxCarga) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(_c.sfxCarga, 1f, puntoVfx);

        // Carga: atrae (FixedUpdate) mientras crece.
        _atrayendo = true;
        float t = 0f;
        while (t < _c.carga)
        {
            t += Time.deltaTime;
            if (_vfx != null)
                _vfx.localScale = Vector3.one * Mathf.Lerp(_c.escalaInicial, _c.escalaFinal, Mathf.SmoothStep(0f, 1f, t / _c.carga));
            if (_c.danoPorSegundoEnElNucleo > 0f && Time.time >= _siguienteGolpeDelNucleo)
            {
                _siguienteGolpeDelNucleo = Time.time + IntervaloDelNucleo;
                QuemarEnElNucleo();
            }
            yield return null;
        }
        _atrayendo = false;

        // Implosión: se encoge de golpe y estalla.
        float desde = _vfx != null ? _vfx.localScale.x : 0f;
        t = 0f;
        while (t < DuracionImplosion)
        {
            t += Time.deltaTime;
            if (_vfx != null) _vfx.localScale = Vector3.one * Mathf.Lerp(desde, 0.02f, t / DuracionImplosion);
            yield return null;
        }
        if (_vfx != null) Destroy(_vfx.gameObject);

        if (_c.vfxImplosion != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(_c.vfxImplosion, puntoVfx, Quaternion.identity, 2.5f);
        if (!string.IsNullOrEmpty(_c.sfxImplosion) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(_c.sfxImplosion, 1f, puntoVfx);
        FeedbackService.CameraShake(0.5f, 0.35f);
        Golpear();

        Terminado = true;
        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (!_atrayendo || _jugador == null) return;
        if (_salud != null && !_salud.IsAlive) return;

        Vector3 haciaElCentro = transform.position - _jugador.position;
        if (-haciaElCentro.y > AlturaMax) return;
        haciaElCentro.y = 0f;
        float distancia = haciaElCentro.magnitude;
        if (distancia > _c.radioAtraccion || distancia < 0.6f) return;

        float cercania = 1f - distancia / _c.radioAtraccion;
        float velocidad = _c.velocidadAtraccion * Mathf.Lerp(0.35f, 1f, cercania);
        Vector3 paso = haciaElCentro / distancia * Mathf.Min(velocidad * Time.fixedDeltaTime, distancia - 0.5f);
        if (_rb != null && !_rb.isKinematic) _rb.MovePosition(_rb.position + paso);
        else _jugador.position += paso;
    }

    /// Un golpe del núcleo a quien lo pise; el temblor, solo si le toca al que va al mando.
    private void QuemarEnElNucleo()
    {
        float dano = _c.danoPorSegundoEnElNucleo * IntervaloDelNucleo;
        int n = DanoEnZona.CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < n; i++)
        {
            var c = cuerpos[i];
            if (!DanoEnZona.EnCilindro(c.Posicion, transform.position, RadioDelNucleo, AlturaMax, ProfundidadMax)) continue;
            if (DanoEnZona.Danar(c, dano) && c.EsElJugador) FeedbackService.CameraShake(0.2f, 0.1f);
        }
    }

    private void Golpear() =>
        DanoEnZona.GolpearEnCilindro(transform.position, _c.radioImplosion, AlturaMax, ProfundidadMax, _c.dano, _c.empuje);
}
