using UnityEngine;

/// <summary>
/// Escudo de grupo (Cúpula Estelar, INC-500): durante unos segundos el golpe que recibe su dueño se
/// reduce a una fracción. Es una regla de daño (IFiltroDeDano) que se añade sola al objeto de la
/// vida (PlayerHealthSystem del jugador o Damageable de un compañero) y se queda puesta, apagada,
/// cuando se acaba; volver a lanzarlo la reutiliza. Lleva encima el efecto del escudo mientras dura.
/// </summary>
[DisallowMultipleComponent]
public sealed class EscudoTemporal : MonoBehaviour, IFiltroDeDano
{
    private float _hasta;
    private float _factor = 1f;
    private GameObject _vfx;

    public bool Activo => Time.time < _hasta;

    /// <summary>Pone (o renueva) el escudo. 'factor' = parte del golpe que pasa (0,3 = el 30 %).</summary>
    public static EscudoTemporal Poner(GameObject objetoDeVida, Transform cuerpo, float segundos, float factor, GameObject vfxPrefab)
    {
        if (objetoDeVida == null || segundos <= 0f) return null;
        if (!objetoDeVida.TryGetComponent(out EscudoTemporal escudo))
        {
            escudo = objetoDeVida.AddComponent<EscudoTemporal>();
            // Damageable guarda sus reglas al despertar: hay que avisarle de la nueva.
            var d = objetoDeVida.GetComponent<Damageable>();
            if (d != null) d.RefrescarFiltros();
        }
        escudo.Activar(cuerpo != null ? cuerpo : objetoDeVida.transform, segundos, factor, vfxPrefab);
        return escudo;
    }

    private void Activar(Transform cuerpo, float segundos, float factor, GameObject vfxPrefab)
    {
        _factor = Mathf.Clamp01(factor);
        _hasta = Mathf.Max(_hasta, Time.time + segundos);
        if (_vfx == null && vfxPrefab != null)
        {
            // Se apoya en los pies: el prefab del escudo ya sube su esfera y su aro sobre su origen.
            _vfx = Instantiate(vfxPrefab, cuerpo.position, Quaternion.identity, cuerpo);
            // Es solo visual (el golpe lo reduce Filtrar). Su esfera es un disparador en Default que
            // cogerían los raycasts que no ignoran disparadores (cámara, suelo, objetivos).
            foreach (var c in _vfx.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }
        enabled = true;
    }

    public float Filtrar(float cantidad, GameObject instigador) => Activo ? cantidad * _factor : cantidad;

    private void Update()
    {
        if (Activo) return;
        if (_vfx != null) Destroy(_vfx);
        _vfx = null;
        enabled = false;
    }

    private void OnDestroy()
    {
        if (_vfx != null) Destroy(_vfx);
    }
}
