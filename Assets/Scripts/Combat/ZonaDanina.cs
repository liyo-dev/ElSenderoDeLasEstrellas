using System.Collections;
using UnityEngine;

/// Una zona del suelo que primero avisa (una sombra que crece) y después hace daño al jugador
/// mientras esté dentro. Sirve para cualquier ataque de área de un enemigo: grietas, novas,
/// lluvias, suelo corrompido. Hace daño al jugador (PlayerHealthSystem), nunca a los aliados.
/// Se crea con Crear(); se destruye sola al acabar. Ver INC-509.
public sealed class ZonaDanina : MonoBehaviour
{
    public struct Config
    {
        public float radio;
        public float aviso;          // segundos de aviso antes de hacer daño
        public float duracion;       // segundos activa; 0 = un solo golpe al activarse
        public float dano;           // daño por golpe
        public float intervalo;      // segundos entre golpes mientras dura
        public float empuje;         // fuerza con la que aparta al jugador al golpear (0 = nada)
        public float alturaMax;      // por encima de esto (volando) no alcanza; 0 = 3 m
        public GameObject vfxAviso;  // sombra/marca que crece durante el aviso
        public GameObject vfxActiva; // efecto mientras hace daño
        public float escalaVfx;      // escala del VFX por metro de radio (0 = 1)
    }

    private Config _c;
    private PlayerHealthSystem _salud;
    private Transform _jugador;

    /// La zona puede estar ya haciendo daño (pasó el aviso).
    public bool Activa { get; private set; }

    public static ZonaDanina Crear(Vector3 centro, Config config)
    {
        var go = new GameObject("ZonaDanina");
        go.transform.position = centro;
        var zona = go.AddComponent<ZonaDanina>();
        zona._c = config;
        zona.StartCoroutine(zona.Vivir());
        return zona;
    }

    private IEnumerator Vivir()
    {
        if (PlayerService.TryGetPlayer(out var jugador) && jugador != null)
        {
            _jugador = jugador.transform;
            _salud = jugador.GetComponent<PlayerHealthSystem>();
        }

        float escala = _c.radio * (_c.escalaVfx > 0f ? _c.escalaVfx : 1f);
        var pool = VfxPoolService.Instance;

        // Disco indicador a ras de suelo para que el área de daño sea siempre 100% legible
        GameObject discoSuelo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        discoSuelo.name = "ZonaDanina_AvisoPeligro";
        discoSuelo.transform.SetParent(transform, false);
        discoSuelo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        discoSuelo.transform.localScale = new Vector3(_c.radio * 0.4f, 0.005f, _c.radio * 0.4f);
        Destroy(discoSuelo.GetComponent<Collider>());
        var shaderZona = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material matAviso = null;
        if (shaderZona != null)
        {
            matAviso = new Material(shaderZona) { color = new Color(0.85f, 0.15f, 0.25f, 0.5f) };
            if (matAviso.HasProperty("_BaseColor")) matAviso.SetColor("_BaseColor", new Color(0.85f, 0.15f, 0.25f, 0.5f));
            if (matAviso.HasProperty("_EmissionColor"))
            {
                matAviso.EnableKeyword("_EMISSION");
                matAviso.SetColor("_EmissionColor", new Color(0.6f, 0.05f, 0.15f));
            }
            discoSuelo.GetComponent<Renderer>().sharedMaterial = matAviso;
        }

        // Aviso: la marca crece hasta el tamaño real.
        if (_c.aviso > 0f)
        {
            Transform marca = null;
            if (_c.vfxAviso != null && pool != null)
                marca = pool.Play(_c.vfxAviso, transform.position + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f), _c.aviso + 0.1f);
            float t = 0f;
            while (t < _c.aviso)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / _c.aviso);
                if (discoSuelo != null)
                    discoSuelo.transform.localScale = new Vector3(_c.radio * 2f * Mathf.Lerp(0.3f, 1f, k), 0.005f, _c.radio * 2f * Mathf.Lerp(0.3f, 1f, k));
                if (marca != null) marca.localScale = Vector3.one * (escala * Mathf.SmoothStep(0.2f, 1f, k));
                yield return null;
            }
        }
        if (discoSuelo != null) Destroy(discoSuelo);

        Activa = true;
        if (_c.vfxActiva != null && pool != null)
        {
            var efecto = pool.Play(_c.vfxActiva, transform.position, Quaternion.identity, Mathf.Max(0.6f, _c.duracion + 0.3f));
            if (efecto != null) efecto.localScale = Vector3.one * Mathf.Max(0.5f, escala * 0.5f);
        }

        if (_c.duracion <= 0f)
        {
            Golpear();
        }
        else
        {
            float fin = Time.time + _c.duracion;
            float siguiente = 0f;
            while (Time.time < fin)
            {
                if (Time.time >= siguiente)
                {
                    Golpear();
                    siguiente = Time.time + Mathf.Max(0.1f, _c.intervalo);
                }
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    /// El jugador está dentro: en el círculo y sin haberse elevado por encima de la zona.
    public bool Contiene(Vector3 punto)
    {
        Vector3 d = punto - transform.position;
        float alto = _c.alturaMax > 0f ? _c.alturaMax : 3f;
        if (d.y > alto || d.y < -2f) return false;
        d.y = 0f;
        return d.sqrMagnitude <= _c.radio * _c.radio;
    }

    private void Golpear()
    {
        if (_salud == null || _jugador == null || !Contiene(_jugador.position)) return;
        _salud.TakeDamage(_c.dano);
        if (_c.empuje > 0f)
        {
            Vector3 fuera = _jugador.position - transform.position;
            fuera.y = 0f;
            if (fuera.sqrMagnitude < 0.01f) fuera = -_jugador.forward;
            _salud.Empujar(fuera.normalized, _c.empuje);
        }
    }
}
