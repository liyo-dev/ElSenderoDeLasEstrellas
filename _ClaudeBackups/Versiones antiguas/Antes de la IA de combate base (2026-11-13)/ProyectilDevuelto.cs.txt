using UnityEngine;

/// <summary>
/// Bola hostil devuelta por el jugador con la B: toma el control del mismo objeto (conserva su
/// aspecto y sus efectos continuos), la tiñe y le pone estela, y la guía hasta el objetivo con el
/// daño del contraataque. Ver INC-689.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProyectilDevuelto : MonoBehaviour
{
    [SerializeField, Min(1f)] private float multiplicadorDeVelocidad = 1.3f;
    [SerializeField, Min(0f)] private float guiado = 4f;
    [SerializeField, Min(0.1f)] private float vidaMaxima = 5f;
    [SerializeField] private Color color = new Color(1f, 0.72f, 0.12f, 1f);
    [Tooltip("Segundos que vive el efecto de impacto al chocar la bola devuelta.")]
    [SerializeField, Min(0.1f)] private float vidaDelImpacto = 1.5f;
    /// <summary>
    /// True mientras una bola devuelta aplica su golpe: lo leen las reglas de daño que premian el
    /// contraataque (el Beholder se aturde con su propio rayo, INC-722).
    /// </summary>
    public static bool GolpeandoAhora { get; private set; }

    private GameObject _dueno;
    private Transform _objetivo;
    private Vector3 _direccion;
    private Vector3 _escala;
    private float _velocidad, _dano, _inicio;
    private GameObject _impacto;
    private MagicElement _elemento;
    private bool _terminado;
    private Rigidbody _rb;
    private readonly RaycastHit[] _barrido = new RaycastHit[32];

    // Un solo material para todas las estelas; el color va en los vértices (startColor/endColor).
    private static Material _materialDeEstela;
    private static Material MaterialDeEstela
    {
        get
        {
            if (_materialDeEstela == null)
                _materialDeEstela = new Material(Shader.Find("Sprites/Default")) { name = "EstelaDeBolaDevuelta" };
            return _materialDeEstela;
        }
    }
    private struct Contacto { public Collider collider; public int tipo; }
    private readonly Contacto[] _contactos = new Contacto[64];
    private int _siguienteContacto;

    public void TomarControl(MonoBehaviour original, GameObject dueno, Transform objetivo, Vector3 direccion, float velocidad, float dano, GameObject impacto, GameObject lanzador)
    {
        original.CancelInvoke();
        original.enabled = false;
        if (original is IAmenazaEntrante amenaza) AmenazasAlJugador.Quitar(amenaza);
        _dueno = dueno; _objetivo = objetivo; _dano = dano; _impacto = impacto;
        // Las bolas hostiles sin elemento propio cuentan como magia oscura.
        _elemento = original is MagicProjectile magia ? magia.Elemento : MagicElement.Dark;
        _direccion = direccion.sqrMagnitude > 0.001f ? direccion.normalized : -transform.forward;
        if (objetivo != null) _direccion = (objetivo.position + Vector3.up - transform.position).normalized;
        _velocidad = Mathf.Max(1f, velocidad) * multiplicadorDeVelocidad;
        _inicio = Time.time; _escala = transform.localScale;
        _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            if (!_rb.isKinematic) _rb.linearVelocity = Vector3.zero;
            _rb.useGravity = false; _rb.isKinematic = true;
        }
        int capa = LayerMask.NameToLayer("Projectile");
        foreach (Transform hijo in GetComponentsInChildren<Transform>(true))
            if (capa >= 0) hijo.gameObject.layer = capa;
        var propios = GetComponentsInChildren<Collider>(true);
        foreach (var col in propios) col.enabled = false; // El barrido evita impactos dobles y saltos de hitboxes.
        // El lanzador deja de estar excluido: la bola puede alcanzarlo al regresar.
        if (lanzador != null)
            foreach (var suyo in lanzador.GetComponentsInChildren<Collider>(true))
                foreach (var propio in propios) Physics.IgnoreCollision(propio, suyo, false);
        var bloque = new MaterialPropertyBlock();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.GetPropertyBlock(bloque);
            bloque.SetColor("_BaseColor", color); bloque.SetColor("_Color", color);
            bloque.SetColor("_EmissionColor", color * 2f);
            bloque.SetColor("_TintColor", color); bloque.SetColor("_MainColor", color);
            renderer.SetPropertyBlock(bloque);
        }
        var estela = GetComponentInChildren<TrailRenderer>() ?? gameObject.AddComponent<TrailRenderer>();
        estela.Clear(); estela.time = 0.22f; estela.startWidth = 0.28f; estela.endWidth = 0f;
        estela.startColor = color; estela.endColor = new Color(color.r, color.g, color.b, 0f);
        if (estela.sharedMaterial == null) estela.sharedMaterial = MaterialDeEstela;
        estela.emitting = true;
    }

    private void Update()
    {
        if (_terminado) return;
        float edad = Time.time - _inicio;
        transform.localScale = _escala * (1f + (edad < 0.12f ? Mathf.Sin(edad / 0.12f * Mathf.PI) * 0.25f : 0f));
        if (edad >= vidaMaxima) { Terminar(transform.position); return; }
        if (_objetivo != null)
        {
            Vector3 hacia = _objetivo.position + Vector3.up - transform.position;
            if (hacia.sqrMagnitude > 0.001f)
                _direccion = Vector3.RotateTowards(_direccion, hacia.normalized, guiado * Time.deltaTime, 0f);
        }
        float distancia = _velocidad * Time.deltaTime;
        int n = Physics.SphereCastNonAlloc(transform.position, 0.2f, _direccion, _barrido, distancia, ~0, QueryTriggerInteraction.Collide);
        Collider primero = null; float cercano = float.PositiveInfinity; Vector3 punto = default; int tipoPrimero = 0;
        for (int i = 0; i < n; i++)
        {
            var col = _barrido[i].collider;
            if (col == null) continue;
            int tipo = TipoDeContacto(col);
            if (tipo == 0) continue;
            if (_barrido[i].distance >= cercano) continue;
            cercano = _barrido[i].distance; primero = col; punto = _barrido[i].point; tipoPrimero = tipo;
        }
        if (primero != null)
        {
            if (tipoPrimero == 2)
            {
                GolpeandoAhora = true;
                try { MagicProjectile.AplicarImpactoDirecto(primero, punto, _dueno, _dano); }
                finally { GolpeandoAhora = false; }
            }
            else if (tipoPrimero == 3) ReaccionesAMagia.Notificar(primero, new ImpactoDeMagia(_elemento, punto, _dueno, true));
            Terminar(punto); return;
        }
        transform.SetPositionAndRotation(transform.position + _direccion * distancia, Quaternion.LookRotation(_direccion));
    }

    // Cada collider externo se clasifica al encontrarlo y se conserva en un buffer fijo.
    private int TipoDeContacto(Collider col)
    {
        for (int i = 0; i < _contactos.Length; i++)
            if (_contactos[i].collider == col) return _contactos[i].tipo;
        int tipo = 0;
        if (!col.transform.IsChildOf(transform) && (_dueno == null || !col.transform.IsChildOf(_dueno.transform)) &&
            !AmenazasAlJugador.EsDelGrupoDelJugador(col.gameObject))
        {
            if (MagicProjectile.EncontrarReceptor(col) != null) tipo = 2;
            else if (ReaccionesAMagia.Reacciona(col)) tipo = 3;
            else if (!col.isTrigger && col.GetComponentInParent<NPCSimpleAnimator>() == null) tipo = 1;
        }
        _contactos[_siguienteContacto] = new Contacto { collider = col, tipo = tipo };
        _siguienteContacto = (_siguienteContacto + 1) % _contactos.Length;
        return tipo;
    }

    private void Terminar(Vector3 punto)
    {
        if (_terminado) return;
        _terminado = true;
        if (_impacto != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(_impacto, punto, Quaternion.identity, vidaDelImpacto);
        Destroy(gameObject);
    }
}
