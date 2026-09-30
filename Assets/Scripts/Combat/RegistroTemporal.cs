using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Graba dónde estaba cada cuerpo durante los últimos segundos para poder rebobinarlo: el
/// Hechizo del Tiempo de Will (novela, «Tiempo 2»: retroceder unos segundos para encontrar el
/// resquicio). Solo mueve posiciones y giros; la vida, la magia y el cansancio no vuelven atrás
/// (canon: «conserva el agotamiento y las heridas»). Mientras rebobina, cada cuerpo queda quieto
/// (sin agente de navegación, sin física y con la animación congelada). Ver INC-509.
public sealed class RegistroTemporal : MonoBehaviour
{
    [Tooltip("Segundos que se guardan (lo que se puede retroceder como mucho).")]
    [SerializeField] private float segundos = 10f;
    [Tooltip("Cada cuánto se toma una muestra.")]
    [SerializeField] private float intervalo = 0.1f;

    private struct Muestra { public Vector3 pos; public Quaternion rot; }

    private sealed class Pista
    {
        public Transform t;
        public Muestra[] muestras;
        public int escritas;   // total escritas (el anillo guarda las últimas)
    }

    private readonly List<Pista> _pistas = new();
    private float _siguiente;
    private int _capacidad;

    public bool Rebobinando { get; private set; }

    void Awake() => _capacidad = Mathf.Max(2, Mathf.CeilToInt(segundos / Mathf.Max(0.02f, intervalo)));

    public void Seguir(Transform t)
    {
        if (t == null || _pistas.Exists(p => p.t == t)) return;
        _pistas.Add(new Pista { t = t, muestras = new Muestra[_capacidad] });
    }

    public void Dejar(Transform t) => _pistas.RemoveAll(p => p.t == t);

    void Update()
    {
        if (Rebobinando || Time.time < _siguiente) return;
        _siguiente = Time.time + intervalo;
        for (int i = 0; i < _pistas.Count; i++)
        {
            var p = _pistas[i];
            if (p.t == null) continue;
            p.muestras[p.escritas % _capacidad] = new Muestra { pos = p.t.position, rot = p.t.rotation };
            p.escritas++;
        }
    }

    /// Lleva a todos hacia atrás, a la vista, en 'duracionReal' segundos de reloj real. Al acabar
    /// cada uno está donde estaba hace (como mucho) 'segundos' y la grabación empieza de nuevo.
    public IEnumerator Rebobinar(float duracionReal)
    {
        Rebobinando = true;

        var estados = new List<(Pista p, NavMeshAgent agente, bool agenteActivo, Rigidbody rb, bool cinematico, Animator anim, float velAnim)>();
        foreach (var p in _pistas)
        {
            if (p.t == null || p.escritas == 0) continue;
            var agente = p.t.GetComponentInChildren<NavMeshAgent>();
            var rb = p.t.GetComponent<Rigidbody>();
            var anim = p.t.GetComponentInChildren<Animator>();
            estados.Add((p, agente, agente != null && agente.enabled, rb, rb != null && rb.isKinematic, anim, anim != null ? anim.speed : 1f));
            if (agente != null) agente.enabled = false;
            if (rb != null) { rb.isKinematic = true; }
            if (anim != null) anim.speed = 0f;
        }

        float t = 0f;
        while (t < duracionReal)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracionReal);
            foreach (var e in estados) Colocar(e.p, k);
            yield return null;
        }

        foreach (var e in estados)
        {
            Colocar(e.p, 1f);
            if (e.rb != null)
            {
                e.rb.isKinematic = e.cinematico;
                if (!e.rb.isKinematic) { e.rb.linearVelocity = Vector3.zero; e.rb.angularVelocity = Vector3.zero; }
            }
            if (e.agente != null && e.agenteActivo)
            {
                e.agente.enabled = true;
                if (e.agente.isOnNavMesh) e.agente.Warp(e.p.t.position);
            }
            if (e.anim != null) e.anim.speed = e.velAnim;
            e.p.escritas = 0;
        }

        Rebobinando = false;
    }

    /// k = 0 → la muestra más reciente; k = 1 → la más antigua que se conserva.
    private void Colocar(Pista p, float k)
    {
        int disponibles = Mathf.Min(p.escritas, _capacidad);
        if (disponibles == 0 || p.t == null) return;
        float atras = k * (disponibles - 1);
        int a = Mathf.FloorToInt(atras);
        int b = Mathf.Min(a + 1, disponibles - 1);
        var ma = p.muestras[(p.escritas - 1 - a + _capacidad * 4) % _capacidad];
        var mb = p.muestras[(p.escritas - 1 - b + _capacidad * 4) % _capacidad];
        float f = atras - a;
        p.t.SetPositionAndRotation(Vector3.Lerp(ma.pos, mb.pos, f), Quaternion.Slerp(ma.rot, mb.rot, f));
    }
}
