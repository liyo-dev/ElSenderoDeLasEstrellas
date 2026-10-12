using System;
using UnityEngine;

/// <summary>
/// Preparación visible de un disparo: la bola aparece en la boca creciendo durante la carga, sin
/// dañar ni contar como amenaza en vuelo, y el disparo se anuncia en <see cref="AmenazasAlJugador"/>
/// para que el aviso de combate salga ya mientras carga. Al acabar, entrega la bola al tirador para
/// que la inicialice. Ver INC-689.
/// </summary>
[DisallowMultipleComponent]
public sealed class CargaDeDisparo : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float segundosDeCarga = 0.6f;
    [SerializeField] private GameObject vfxDeCarga;
    private GameObject _bola;
    private Transform _boca;
    private Vector3 _desplazamiento, _escala;
    private float _inicio;
    private Collider[] _colliders;
    private bool[] _collidersActivos;
    private MonoBehaviour[] _comportamientos;
    private bool[] _comportamientosActivos;
    private Action<GameObject, Vector3> _disparar;
    private Transform _objetivo;
    private Transform _vfx;
    private ulong _usoVfx;
    private Rigidbody[] _cuerpos;
    private bool[] _cinematicos;
    public bool EnCarga => _bola != null;
    public float SegundosDeCarga => segundosDeCarga;

    public bool Cargar(GameObject prefab, Transform boca, Vector3 desplazamiento, Transform objetivo, float velocidad, Action<GameObject, Vector3> disparar)
    {
        if (EnCarga || prefab == null || objetivo == null || disparar == null) return false;
        _boca = boca != null ? boca : transform; _desplazamiento = desplazamiento;
        _objetivo = objetivo; _disparar = disparar; _inicio = Time.time;
        Vector3 origen = _boca.position + desplazamiento;
        _bola = Instantiate(prefab, origen, Quaternion.identity);
        _escala = _bola.transform.localScale;
        _colliders = _bola.GetComponentsInChildren<Collider>(true);
        _collidersActivos = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++) { _collidersActivos[i] = _colliders[i].enabled; _colliders[i].enabled = false; }
        _comportamientos = _bola.GetComponentsInChildren<MonoBehaviour>(true);
        _comportamientosActivos = new bool[_comportamientos.Length];
        for (int i = 0; i < _comportamientos.Length; i++)
        {
            var c = _comportamientos[i];
            if (!(c is IAmenazaEntrante)) continue;
            _comportamientosActivos[i] = c.enabled;
            c.CancelInvoke(); c.enabled = false;
            AmenazasAlJugador.Quitar((IAmenazaEntrante)c);
        }
        _cuerpos = _bola.GetComponentsInChildren<Rigidbody>(true);
        _cinematicos = new bool[_cuerpos.Length];
        for (int i = 0; i < _cuerpos.Length; i++)
        {
            var rb = _cuerpos[i];
            _cinematicos[i] = rb.isKinematic;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        _bola.transform.localScale = Vector3.zero;
        float vuelo = Vector3.Distance(origen, objetivo.position + Vector3.up) / Mathf.Max(1f, velocidad);
        AmenazasAlJugador.Anunciar(transform, segundosDeCarga + vuelo, TipoDeAmenaza.Devolvible);
        if (vfxDeCarga != null && VfxPoolService.Instance != null)
        {
            _vfx = VfxPoolService.Instance.Play(vfxDeCarga, origen, Quaternion.identity, segundosDeCarga + 0.1f);
            _usoVfx = VfxPoolService.Instance.ObtenerUso(_vfx);
        }
        return true;
    }

    private void Update()
    {
        if (!EnCarga) return;
        if (_boca == null || _objetivo == null) { Cancelar(); return; }
        Vector3 origen = _boca.position + _desplazamiento;
        _bola.transform.position = origen;
        if (_vfx != null) _vfx.position = origen;
        float k = Mathf.Clamp01((Time.time - _inicio) / segundosDeCarga);
        _bola.transform.localScale = _escala * k;
        if (k < 1f) return;
        var bola = _bola; _bola = null;
        for (int i = 0; i < _cuerpos.Length; i++) _cuerpos[i].isKinematic = _cinematicos[i];
        for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = _collidersActivos[i];
        for (int i = 0; i < _comportamientos.Length; i++)
            if (_comportamientos[i] is IAmenazaEntrante) _comportamientos[i].enabled = _comportamientosActivos[i];
        AmenazasAlJugador.RetirarAnuncio(transform);
        var disparar = _disparar; _disparar = null;
        disparar(bola, (_objetivo.position + Vector3.up - origen).normalized);
        RecogerVfx();
    }

    private void OnDisable() => Cancelar();
    public void Cancelar()
    {
        AmenazasAlJugador.RetirarAnuncio(transform);
        if (_bola != null) Destroy(_bola);
        _bola = null; _disparar = null;
        RecogerVfx();
    }

    private void RecogerVfx()
    {
        if (_vfx != null && VfxPoolService.Instance != null) VfxPoolService.Instance.Recoger(_vfx, _usoVfx);
        _vfx = null;
        _usoVfx = 0;
    }
}
