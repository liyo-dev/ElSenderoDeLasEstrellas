using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;

/// Demonio 2, «guerra en el aire» (GDD § 3, INC-723): al empezar la fase 2 sube a las almenas de
/// la arena (AlmenaDelDemonio cercanas) y pelea desde arriba con bolas y lluvia de sombras; cada
/// poco cambia de almena volando en arco. Si la arena no tiene almenas, vuela en círculo sobre
/// ella a 'alturaDeVuelo'. Al llegar a la fase 3 baja al suelo antes del rugido. En el aire no
/// usa el NavMeshAgent (se apaga y se recoloca al bajar). Se le alcanza con hechizos a distancia
/// (el apuntado va al centro del cuerpo) o subiendo a las torres, levitando o volando.
public partial class ImpDemonAI
{
    [Header("Guerra en el aire (Demonio 2, INC-723)")]
    [Tooltip("En la fase 2 sube a las almenas y ataca desde arriba; en la 3 baja.")]
    [SerializeField] private bool guerraEnElAire = false;
    [Tooltip("Metros a los que busca almenas desde donde despega.")]
    [SerializeField, Min(1f)] private float radioDeAlmenas = 40f;
    [Tooltip("Sin almenas: altura sobre el suelo a la que vuela.")]
    [SerializeField, Min(1f)] private float alturaDeVuelo = 6f;
    [Tooltip("Sin almenas: radio del círculo sobre la arena.")]
    [SerializeField, Min(2f)] private float radioDeVuelo = 11f;
    [Tooltip("Segundos en cada almena antes de cambiar a otra.")]
    [SerializeField, Min(1f)] private float segundosEnCadaAlmena = 6f;
    [Tooltip("Segundos que tarda en volar de una almena a otra.")]
    [SerializeField, Min(0.2f)] private float duracionDelVuelo = 1.3f;
    [Tooltip("Metros que sube el arco del vuelo sobre la línea recta.")]
    [SerializeField, Min(0f)] private float alturaDelArco = 2.5f;

    private bool _enElAire;
    private Vector3 _centroDelAire;
    private int _almenaActual = -1;
    private float _cambiarDeAlmenaEn;
    private readonly List<Vector3> _puntosDeAire = new List<Vector3>(8);

    /// Está peleando desde el aire.
    public bool EnElAire => _enElAire;

    private bool DebeSubirAlAire()
        => guerraEnElAire && !isDead && !_enElAire && currentPhase == BossPhase.Phase2;

    /// En el aire decide aquí (true = este fotograma ya está decidido). Ataca solo a distancia y,
    /// cumplido su tiempo en la almena, vuela a otra.
    private bool ActualizarGuerraEnElAire()
    {
        if (!_enElAire)
        {
            // Por si la fase 2 llegó sin pasar por la transición (p. ej. con la IA apagada).
            if (!DebeSubirAlAire()) return false;
            Lanzar(Despegar());
            return true;
        }

        currentState = BossState.Idle;
        LookAtPlayer();
        PlayAnimation(AnimIdle);
        if (!CanStartNewAttack()) return true;

        if (_puntosDeAire.Count > 1 && Time.time >= _cambiarDeAlmenaEn)
        {
            Lanzar(VolarA(ElegirAlmena()));
            return true;
        }
        if (rainShadowPrefab != null && Time.time >= lastRainTime + rainCooldown)
        {
            Lanzar(RainAttack());
            return true;
        }
        if (Time.time >= lastProjectileTime + _effProjectileCooldown)
        {
            Lanzar(ProjectileAttack());
            return true;
        }
        if (spellEffectPrefab != null && Time.time >= lastSpellTime + _effSpellCooldown)
        {
            Lanzar(CastSpellAttack());
            return true;
        }
        return true;
    }

    /// Sube a la primera almena.
    private IEnumerator Despegar()
    {
        _centroDelAire = player ? player.position : transform.position;
        RecogerPuntosDeAire();
        if (_puntosDeAire.Count == 0) yield break;

        if (agent)
        {
            if (agent.isOnNavMesh) agent.isStopped = true;
            agent.enabled = false;
        }
        _enElAire = true;
        FeedbackService.CameraShake(0.3f, 0.4f);
        yield return VolarA(ElegirAlmena());
    }

    /// Baja al suelo más cercano bajo él y vuelve a usar el NavMeshAgent.
    private IEnumerator Aterrizar()
    {
        if (!_enElAire) yield break;
        Vector3 suelo = PuntoDeSuelo(transform.position);
        yield return VolarA(suelo, aterrizando: true);

        _enElAire = false;
        if (agent)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(suelo);
        }
        FeedbackService.CameraShake(0.5f, 0.35f);
        if (spellEffectPrefab && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(spellEffectPrefab, suelo, Quaternion.identity, 3f);
    }

    /// Vuela en arco hasta 'destino'. Mientras vuela no se le ve el punto débil ni ataca.
    private IEnumerator VolarA(Vector3 destino, bool aterrizando = false)
    {
        isAttacking = true;
        _expuesto = false;
        currentState = BossState.Chasing;
        PlayAnimation(AnimFlyForward);

        Vector3 desde = transform.position;
        Vector3 rumbo = destino - desde;
        float t = 0f;
        while (t < duracionDelVuelo)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duracionDelVuelo);
            float arco = aterrizando ? 0f : Mathf.Sin(k * Mathf.PI) * alturaDelArco;
            transform.position = Vector3.Lerp(desde, destino, Mathf.SmoothStep(0f, 1f, k)) + Vector3.up * arco;
            if (k < 0.8f) LookAtDirection(rumbo);
            else LookAtPlayer();
            yield return null;
        }
        transform.position = destino;

        PlayAnimation(AnimIdle);
        _cambiarDeAlmenaEn = Time.time + segundosEnCadaAlmena;
        EndAttack();
    }

    /// Almenas de la arena o, si no hay al menos dos, un círculo en el aire sobre ella.
    private void RecogerPuntosDeAire()
    {
        _puntosDeAire.Clear();
        _almenaActual = -1;
        var almenas = AlmenaDelDemonio.Activas;
        float r2 = radioDeAlmenas * radioDeAlmenas;
        for (int i = 0; i < almenas.Count; i++)
        {
            var a = almenas[i];
            if (a == null) continue;
            Vector3 d = a.transform.position - _centroDelAire;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) _puntosDeAire.Add(a.transform.position);
        }
        if (_puntosDeAire.Count >= 2) return;

        _puntosDeAire.Clear();
        const int puntos = 4;
        float inicio = Random.Range(0f, 360f);
        for (int i = 0; i < puntos; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, inicio + i * 360f / puntos, 0f) * Vector3.forward;
            _puntosDeAire.Add(_centroDelAire + dir * radioDeVuelo + Vector3.up * alturaDeVuelo);
        }
    }

    /// Otra almena distinta de la actual, al azar.
    private Vector3 ElegirAlmena()
    {
        int n = _puntosDeAire.Count;
        int i = n <= 1 ? 0 : Random.Range(0, n);
        if (n > 1 && i == _almenaActual) i = (i + 1 + Random.Range(0, n - 1)) % n;
        _almenaActual = i;
        return _puntosDeAire[i];
    }

    private Vector3 PuntoDeSuelo(Vector3 desde)
    {
        Vector3 abajo = new Vector3(desde.x, _centroDelAire.y, desde.z);
        if (NavMesh.SamplePosition(abajo, out NavMeshHit hit, 6f, NavMesh.AllAreas)) return hit.position;
        if (NavMesh.SamplePosition(_centroDelAire, out hit, 6f, NavMesh.AllAreas)) return hit.position;
        return abajo;
    }

    /// Si muere en el aire, cae al suelo en vez de quedarse colgado (lo llama la parada común
    /// después de StopAllCoroutines).
    private void CaerSiEstabaEnElAire()
    {
        if (!_enElAire) return;
        _enElAire = false;
        StartCoroutine(Caer(PuntoDeSuelo(transform.position)));
    }

    private IEnumerator Caer(Vector3 suelo)
    {
        Vector3 desde = transform.position;
        float t = 0f;
        const float duracion = 0.6f;
        while (t < duracion)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duracion);
            transform.position = Vector3.Lerp(desde, suelo, k * k);
            yield return null;
        }
        transform.position = suelo;
    }
}
