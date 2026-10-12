using UnityEngine;

/// <summary>
/// Torreta del laboratorio que dispara proyectiles enemigos (<see cref="EnemyProjectile"/>, los
/// del Demonio) al jugador cada pocos segundos cuando está a su alcance, para practicar la
/// devolución con la B, el choque de hechizos y el aviso de combate. Solo para escenas de prueba.
/// Ver INC-667.
/// </summary>
[DisallowMultipleComponent]
public sealed class LanzaderaDelLab : MonoBehaviour
{
    [Tooltip("Prefab del proyectil (con EnemyProjectile en la raíz o en un hijo).")]
    [SerializeField] private GameObject proyectil;
    [Tooltip("Punto de salida. Vacío = un metro por encima de la lanzadera.")]
    [SerializeField] private Transform boca;
    [SerializeField, Min(0.5f)] private float cadencia = 2.5f;
    [Tooltip("Metros a los que empieza a disparar.")]
    [SerializeField, Min(1f)] private float alcance = 16f;
    [SerializeField, Min(0f)] private float dano = 10f;
    [Tooltip("Velocidad del proyectil (m/s). 0 = la del prefab.")]
    [SerializeField, Min(0f)] private float velocidad = 12f;

    private float _siguiente;
    private CargaDeDisparo _carga;
    private System.Action<GameObject, Vector3> _inicializar;

    private void Awake()
    {
        _carga = GetComponent<CargaDeDisparo>() ?? gameObject.AddComponent<CargaDeDisparo>();
        _inicializar = Inicializar;
    }

    private void OnDisable()
    {
        if (_carga != null) _carga.Cancelar();
    }

    public void Configurar(GameObject prefab) => proyectil = prefab;

    private void Update()
    {
        if (proyectil == null || _carga.EnCarga || Time.time < _siguiente) return;
        var jugador = PlayerService.Player;
        if (jugador == null) return;

        Vector3 origen = boca != null ? boca.position : transform.position + Vector3.up;
        Vector3 hacia = jugador.transform.position + Vector3.up - origen;
        if (hacia.sqrMagnitude > alcance * alcance || hacia.sqrMagnitude < 0.01f) return;

        if (_carga.Cargar(proyectil, boca != null ? boca : transform, boca != null ? Vector3.zero : Vector3.up,
            jugador.transform, velocidad > 0f ? velocidad : 12f, _inicializar))
            _siguiente = Time.time + Mathf.Max(cadencia, _carga.SegundosDeCarga + 0.1f);
    }

    private void Inicializar(GameObject disparo, Vector3 direccion)
    {
        var p = disparo.GetComponentInChildren<EnemyProjectile>();
        if (p == null) { Destroy(disparo); return; }
        var rb = p.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        p.AsignarLanzador(gameObject);
        if (velocidad > 0f) p.SetSpeed(velocidad);
        p.Initialize(direccion, dano);
    }
}
