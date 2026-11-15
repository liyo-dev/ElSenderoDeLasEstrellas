using UnityEngine;

/// <summary>
/// Suelo o volumen que hace daño mientras se está dentro: lava, pinchos, agua hirviendo. Da el daño
/// en tandas a los cuerpos del grupo que estén dentro (el que lleva el jugador y los compañeros)
/// por el mismo camino que el resto de golpes del juego.
///
/// Va con un colisionador de trigger y una malla a la vista; la zona de daño puede ser más alta que
/// la malla para que alcanzar a quien está de pie dentro.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class ZonaDeDano : MonoBehaviour
{
    [Tooltip("Daño por segundo a quien esté dentro.")]
    [SerializeField, Min(0f)] private float danoPorSegundo = 25f;
    [Tooltip("Cada cuánto se aplica una tanda (s).")]
    [SerializeField, Min(0.05f)] private float cada = 0.4f;

    private BoxCollider _zona;
    private float _siguiente;

    private void Awake()
    {
        _zona = GetComponent<BoxCollider>();
        _zona.isTrigger = true;
    }

    private void Update()
    {
        if (danoPorSegundo <= 0f || Time.time < _siguiente) return;
        _siguiente = Time.time + cada;

        int cuantos = DanoEnZona.CuerposDelGrupo(out var cuerpos);
        Bounds dentro = _zona.bounds;
        for (int i = 0; i < cuantos; i++)
        {
            if (!dentro.Contains(cuerpos[i].Posicion)) continue;
            DanoEnZona.Danar(cuerpos[i], danoPorSegundo * cada);
        }
    }
}
