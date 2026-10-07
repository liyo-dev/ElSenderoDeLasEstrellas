using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// Pozo que atrae al jugador hacia su centro mientras se carga y al final implosiona: hace daño y
/// aparta a quien siga cerca. La atracción es más lenta que correr, así que escapa quien corre en
/// contra. Sirve para cualquier ataque de enemigo de este tipo (el agujero negro del Mago Oscuro).
/// Hace daño al jugador (PlayerHealthSystem), nunca a los aliados. Se crea con Crear() y se
/// destruye solo al acabar.
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
    }

    private const float DuracionImplosion = 0.18f;

    private Config _c;
    private Transform _jugador;
    private Rigidbody _rb;
    private PlayerHealthSystem _salud;
    private Transform _vfx;
    private bool _atrayendo;

    /// Ya ha implosionado.
    public bool Terminado { get; private set; }

    private float AlturaMax => _c.alturaMax > 0f ? _c.alturaMax : 4f;

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

        Vector3 puntoVfx = transform.position + Vector3.up * _c.alturaVfx;
        if (_c.vfxPozo != null)
        {
            _vfx = Instantiate(_c.vfxPozo, puntoVfx, Quaternion.identity, transform).transform;
            _vfx.localScale = Vector3.one * _c.escalaInicial;
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

    private void Golpear()
    {
        if (_salud == null || _jugador == null) return;
        Vector3 fuera = _jugador.position - transform.position;
        if (fuera.y > AlturaMax || fuera.y < -2f) return;
        fuera.y = 0f;
        if (fuera.sqrMagnitude > _c.radioImplosion * _c.radioImplosion) return;

        _salud.TakeDamage(_c.dano);
        if (_c.empuje > 0f)
            _salud.Empujar(fuera.sqrMagnitude > 0.01f ? fuera.normalized : -_jugador.forward, _c.empuje);
    }
}
