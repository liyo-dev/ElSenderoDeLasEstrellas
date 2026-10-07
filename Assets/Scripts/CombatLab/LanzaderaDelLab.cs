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

    public void Configurar(GameObject prefab) => proyectil = prefab;

    private void Update()
    {
        if (proyectil == null || Time.time < _siguiente) return;
        var jugador = PlayerService.Player;
        if (jugador == null) return;

        Vector3 origen = boca != null ? boca.position : transform.position + Vector3.up;
        Vector3 hacia = jugador.transform.position + Vector3.up - origen;
        if (hacia.sqrMagnitude > alcance * alcance || hacia.sqrMagnitude < 0.01f) return;

        _siguiente = Time.time + cadencia;
        var disparo = Instantiate(proyectil, origen, Quaternion.LookRotation(hacia));
        var p = disparo.GetComponentInChildren<EnemyProjectile>();
        if (p == null) { Destroy(disparo); return; }
        if (velocidad > 0f) p.SetSpeed(velocidad);
        p.Initialize(hacia.normalized, dano);
    }
}
