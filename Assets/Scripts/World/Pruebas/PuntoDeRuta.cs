using UnityEngine;

/// <summary>
/// Un punto de una <see cref="RutaDeRecorrido"/> (hijo suyo, en orden). Dice cómo se llega a él
/// desde el anterior (andando, de un salto o trepando) y, si es un punto de control o la meta, la
/// caja que hay que pisar. El eje Z del punto es hacia donde se mira al reaparecer en él.
/// </summary>
public sealed class PuntoDeRuta : MonoBehaviour
{
    public enum Llegada
    {
        /// <summary>Andando o corriendo por el suelo.</summary>
        Andando,
        /// <summary>De un salto en parábola (huecos, escalones altos, trampolines).</summary>
        Saltando,
        /// <summary>Trepando en vertical (paredes, escaleras de mano).</summary>
        Trepando,
    }

    [Tooltip("Cómo se llega a este punto desde el anterior.")]
    [SerializeField] private Llegada llegada = Llegada.Andando;
    [Tooltip("Saltando: metros que sube el salto por encima del más alto de los dos puntos.")]
    [SerializeField, Min(0.2f)] private float alturaDelSalto = 1.5f;
    [Tooltip("Trampolín de este punto: se aplasta cuando un corredor sale de aquí de un salto.")]
    [SerializeField] private LanzadorDeSalto trampolin;

    [Header("Punto de control")]
    [Tooltip("Hay que pasar por aquí (en orden) y es donde se reaparece al caerse. El último punto de la ruta es siempre la meta.")]
    [SerializeField] private bool esPuntoDeControl;
    [Tooltip("Caja que hay que pisar (metros, en los ejes del punto, desde el suelo hacia arriba).")]
    [SerializeField] private Vector3 tamanoDeLaZona = new Vector3(6f, 4f, 3f);

    public Llegada ComoSeLlega => llegada;
    public float AlturaDelSalto => alturaDelSalto;
    public LanzadorDeSalto Trampolin => trampolin;
    public bool EsPuntoDeControl => esPuntoDeControl;
    public Vector3 TamanoDeLaZona => tamanoDeLaZona;

    /// <summary>El punto está dentro de su caja de control.</summary>
    public bool Contiene(Vector3 punto)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (punto - transform.position);
        local.y -= tamanoDeLaZona.y * 0.5f;
        return Mathf.Abs(local.x) <= tamanoDeLaZona.x * 0.5f
            && Mathf.Abs(local.y) <= tamanoDeLaZona.y * 0.5f
            && Mathf.Abs(local.z) <= tamanoDeLaZona.z * 0.5f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + transform.up * tamanoDeLaZona.y * 0.5f, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, tamanoDeLaZona);
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
