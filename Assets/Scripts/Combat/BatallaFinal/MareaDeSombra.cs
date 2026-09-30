using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Una ola de sombra que sale de un punto y cruza toda la arena como un anillo que se ensancha.
/// Solo tiene un resquicio: un sector estrecho por donde no pasa. Quien esté fuera del resquicio
/// cuando le alcanza el frente recibe un golpe fuerte; volar por encima no sirve (es muy alta).
/// El resquicio puede no verse (la primera vez es inevitable: novela, «Tiempo 2») o marcarse con
/// una senda de luz (tras rebobinar con el Hechizo del Tiempo). Se puede pausar desde fuera.
/// Ver INC-509.
public sealed class MareaDeSombra : MonoBehaviour
{
    public struct Config
    {
        public float radioMax;
        public float velocidad;      // m/s del frente
        public float anguloHueco;    // grados (0 = +Z del mundo) hacia donde está el resquicio
        public float anchoHueco;     // grados de ancho del resquicio
        public float altura;         // por encima de esto no alcanza
        public float dano;
        public float empuje;
        public int piezas;           // trozos de ola alrededor del anillo
        public GameObject vfxPieza;
        public float escalaPieza;
        public bool mostrarHueco;
        public GameObject vfxHueco;  // marcas de luz a lo largo del resquicio
    }

    private Config _c;
    private readonly List<(Transform t, float angulo)> _piezas = new();
    private readonly List<Transform> _marcas = new();
    private Transform _jugador;
    private PlayerHealthSystem _salud;

    public float Radio { get; private set; }
    public bool Pausada { get; set; }
    public bool Terminada { get; private set; }
    public bool JugadorAlcanzado { get; private set; }
    public bool JugadorSalvado { get; private set; }

    public static MareaDeSombra Crear(Vector3 centro, Config config)
    {
        var go = new GameObject("MareaDeSombra");
        go.transform.position = centro;
        var marea = go.AddComponent<MareaDeSombra>();
        marea._c = config;
        marea.StartCoroutine(marea.Avanzar());
        return marea;
    }

    /// Distancia horizontal del jugador al centro de la ola.
    public float DistanciaAlJugador()
    {
        if (_jugador == null) return float.MaxValue;
        Vector3 d = _jugador.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    public bool EnElHueco(Vector3 punto)
    {
        Vector3 d = punto - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return false;
        float angulo = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(angulo, _c.anguloHueco)) <= _c.anchoHueco * 0.5f;
    }

    private IEnumerator Avanzar()
    {
        if (PlayerService.TryGetPlayer(out var j) && j != null)
        {
            _jugador = j.transform;
            _salud = j.GetComponent<PlayerHealthSystem>();
        }

        var pool = VfxPoolService.Instance;
        int n = Mathf.Max(8, _c.piezas);
        for (int i = 0; i < n; i++)
        {
            float angulo = i * 360f / n;
            if (Mathf.Abs(Mathf.DeltaAngle(angulo, _c.anguloHueco)) <= _c.anchoHueco * 0.5f) continue;
            if (_c.vfxPieza == null || pool == null) continue;
            var pieza = pool.Play(_c.vfxPieza, transform.position, Quaternion.Euler(0f, angulo, 0f), 60f);
            if (pieza == null) continue;
            pieza.localScale = Vector3.one * (_c.escalaPieza > 0f ? _c.escalaPieza : 1f);
            _piezas.Add((pieza, angulo));
        }

        if (_c.mostrarHueco && _c.vfxHueco != null && pool != null)
        {
            Vector3 dir = Quaternion.Euler(0f, _c.anguloHueco, 0f) * Vector3.forward;
            for (float r = 3f; r < _c.radioMax; r += 3.5f)
            {
                var marca = pool.Play(_c.vfxHueco, transform.position + dir * r + Vector3.up * 0.05f, Quaternion.identity, 60f);
                if (marca != null) _marcas.Add(marca);
            }
        }

        Radio = 0.5f;
        while (Radio < _c.radioMax)
        {
            if (!Pausada)
            {
                float antes = Radio;
                Radio += _c.velocidad * Time.deltaTime;
                ColocarPiezas();
                ComprobarJugador(antes, Radio);
            }
            yield return null;
        }

        Terminada = true;
        Recoger();
        Destroy(gameObject);
    }

    private void ColocarPiezas()
    {
        for (int i = 0; i < _piezas.Count; i++)
        {
            var (t, angulo) = _piezas[i];
            if (t == null) continue;
            t.position = transform.position + Quaternion.Euler(0f, angulo, 0f) * Vector3.forward * Radio;
        }
    }

    /// El frente ha pasado por donde está el jugador en este fotograma.
    private void ComprobarJugador(float antes, float ahora)
    {
        if (JugadorAlcanzado || JugadorSalvado || _jugador == null) return;
        float d = DistanciaAlJugador();
        if (d < antes - 0.5f || d > ahora + 0.5f) return;

        bool porEncima = _jugador.position.y - transform.position.y > _c.altura;
        if (porEncima || EnElHueco(_jugador.position))
        {
            JugadorSalvado = true;
            return;
        }

        JugadorAlcanzado = true;
        if (_salud == null) return;
        _salud.TakeDamage(_c.dano, ignoreInvulnerability: true);
        if (_c.empuje > 0f)
        {
            Vector3 fuera = _jugador.position - transform.position;
            fuera.y = 0f;
            _salud.Empujar(fuera.sqrMagnitude > 0.01f ? fuera.normalized : -_jugador.forward, _c.empuje);
        }
    }

    private void Recoger()
    {
        var pool = VfxPoolService.Instance;
        if (pool == null) return;
        foreach (var (t, _) in _piezas) if (t != null) pool.Recoger(t);
        foreach (var t in _marcas) if (t != null) pool.Recoger(t);
        _piezas.Clear();
        _marcas.Clear();
    }

    void OnDestroy() => Recoger();
}
