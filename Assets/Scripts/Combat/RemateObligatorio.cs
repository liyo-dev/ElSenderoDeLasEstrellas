using UnityEngine;

/// Un jefe que no cae con golpes normales: al llegar a 'umbral' de vida se queda ahí, se enciende
/// su sello y solo un golpe especial de remate (un dúo, ver GolpeDeRemate) puede acabar con él.
/// Al llegar pide el remate (AvisosDeCombate): la guía lo explica y el sistema de dúos se prepara.
/// Solo se aplica si hay algún aliado en combate (sin compañero no hay dúo). Va junto al
/// Damageable, después de las demás reglas de daño. Ver INC-489.
[DisallowMultipleComponent]
[RequireComponent(typeof(Damageable))]
public sealed class RemateObligatorio : MonoBehaviour, IFiltroDeDano
{
    [Tooltip("Vida (0..1 del máximo) a la que se queda esperando el remate.")]
    [SerializeField, Range(0.01f, 0.9f)] private float umbral = 0.1f;
    [Tooltip("Efecto del sello que se enciende al llegar al umbral (se queda puesto hasta el final).")]
    [SerializeField] private GameObject vfxSello;
    [Tooltip("Dónde va el sello. Vacío = encima del cuerpo.")]
    [SerializeField] private Transform puntoSello;

    private Damageable _vida;
    private bool _pedido;

    public bool EsperandoRemate => _pedido;

    void Awake() => _vida = GetComponent<Damageable>();

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (_vida == null || GolpeDeRemate.EnCurso) return cantidad;
        // Sin ningún aliado en combate no hay dúo posible: la regla no se aplica (si no, el
        // jefe no podría caer nunca). Los aliados que pelean son los señuelos activos.
        if (!_pedido && SenueloDeCombate.Activos.Count == 0) return cantidad;

        float margen = _vida.Current - _vida.Max * umbral;
        if (cantidad < margen) return cantidad;

        if (!_pedido)
        {
            _pedido = true;
            EncenderSello();
            AvisosDeCombate.PedirRemate(gameObject);
        }
        // Lo deja justo en el umbral; a partir de ahí los golpes normales no hacen nada.
        return margen > 0.01f ? margen : 0f;
    }

    private void EncenderSello()
    {
        if (vfxSello == null) return;

        Transform donde = puntoSello;
        Vector3 pos;
        if (donde != null) pos = donde.position;
        else
        {
            var col = GetComponentInChildren<Collider>();
            pos = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z)
                              : transform.position + Vector3.up * 3f;
        }
        var sello = Instantiate(vfxSello, pos, Quaternion.identity, donde != null ? donde : transform);
        sello.name = "Sello (remate)";
    }
}
