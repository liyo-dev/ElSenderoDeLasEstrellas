using System.Collections.Generic;
using Kits.Puzzles;
using UnityEngine;

/// <summary>
/// El regalito del Tenor: aparece en <see cref="segundoDeEnsenar"/> (en «un regalito…») y tiembla, y en
/// <see cref="segundoDeLanzar"/> (el «-ad» de «¡tomad!») sale disparado hacia el jugador, a donde va a
/// estar, como un proyectil que hay que esquivar o saltar. Con <see cref="prefabDevolvible"/> (una bola
/// de enemigo de las que para la defensa) sale esa bola con el regalo dentro: se devuelve con B en el
/// momento justo y vuela a <see cref="destino"/>. Con <see cref="lanzarConSuForma"/> sale una copia de lo
/// que se enseña (una clave de sol de piedra, que gira con <see cref="giro"/>). Con <see cref="choques"/>, en vez de uno
/// sale una ráfaga: un regalo por cada segundo de la lista, que sale lo que tarda en volar antes para llegar en
/// ese segundo (al ritmo, contra el escudo mantenido). Su estado sale del segundo de la canción, así que al
/// reintentar vuelve a estar en su sitio. Ver INC-1393.
/// </summary>
public sealed class RegaloQueSeLanza : MonoBehaviour
{
    [SerializeField] private TempoClock reloj;
    [SerializeField] private float segundoDeEnsenar;
    [SerializeField] private float segundoDeLanzar;
    [Tooltip("El regalo que se ve antes de lanzarlo.")]
    [SerializeField] private Transform regalo;
    [SerializeField, Min(0.1f)] private float segundosDeVuelo = 0.6f;
    [Tooltip("Cuánto se adelanta a donde va el jugador (0 = a donde está; 1 = a donde estará si sigue igual).")]
    [SerializeField, Range(0f, 1f)] private float anticipacion = 0.5f;
    [Tooltip("Si se pone, el regalo sale como esta bola de enemigo (devolvible con la defensa).")]
    [SerializeField] private GameObject prefabDevolvible;
    [Tooltip("Objetivo fijo al que vuelve la bola con la defensa.")]
    [SerializeField] private GameObject destino;
    [Tooltip("Si se pone, va a este sitio y no a por el jugador (una coreografía fija).")]
    [SerializeField] private Transform sitio;
    [Tooltip("Altura sobre los pies del jugador a la que va (baja: se puede saltar).")]
    [SerializeField] private float alturaDelTiro = 0.6f;
    [SerializeField, Min(0f)] private float dano = 10f;
    [SerializeField, Min(0.1f)] private float tamano = 0.8f;
    [SerializeField] private Material material;
    [Tooltip("Cuánto tiembla mientras se enseña.")]
    [SerializeField, Min(0f)] private float temblor = 0.1f;
    [Tooltip("Sale volando una copia de lo que se enseña en lugar de un cubo.")]
    [SerializeField] private bool lanzarConSuForma;
    [Tooltip("Grados por segundo que gira, enseñándose y en el aire (0 = no gira).")]
    [SerializeField] private float giro;
    [Tooltip("Ráfaga: segundos de la canción en que llega cada regalo (para pararlos con el escudo mantenido). Vacío: uno solo, en segundoDeLanzar.")]
    [SerializeField] private float[] choques = System.Array.Empty<float>();
    private Vector3 _base;
    private Quaternion _giroBase = Quaternion.identity;
    private bool _visible = true;
    private bool _lanzado;
    // Se ha visto antes de enseñarse: si la canción entra ya enseñándolo (un reintento que empieza
    // tarde), no se lanza, que el grupo aún está quieto esperando la salida.
    private bool _esperado;
    private Vector3 _antes;
    private Vector3 _velocidad;
    private int _siguiente;
    private readonly List<GameObject> _enVuelo = new List<GameObject>();

    public void LimpiarRegalosEnVuelo()
    {
        for (int i = 0; i < _enVuelo.Count; i++)
        {
            var bola = _enVuelo[i];
            if (bola == null) continue;
            bola.SetActive(false);
            Destroy(bola);
        }
        _enVuelo.Clear();
    }

    private void Awake()
    {
        if (regalo != null) { _base = regalo.localPosition; _giroBase = regalo.localRotation; }
        Ver(false);
    }

    private void Update()
    {
        if (reloj == null || !reloj.IsRunning || regalo == null) return;
        double t = reloj.SongTime;
        if (t < segundoDeEnsenar)
        {
            // Antes de enseñarlo (o la canción ha vuelto atrás): escondido y por lanzar.
            Ver(false);
            _lanzado = false;
            _esperado = true;
            _siguiente = 0;
            return;
        }
        if (choques.Length > 0) { Rafaga(t); return; }
        if (t < segundoDeLanzar)
        {
            SeguirAlJugador();
            Ver(true);
            float s = Mathf.Sin(Time.time * 45f) * temblor;
            regalo.localPosition = _base + new Vector3(s, Mathf.Abs(s) * 0.5f, 0f);
            if (giro != 0f) regalo.localRotation = _giroBase * Quaternion.Euler(0f, 0f, (float)t * giro);
            return;
        }
        Ver(false);
        if (_lanzado) return;
        _lanzado = true;
        if (_esperado) Lanzar();
        _esperado = false;
    }

    /// <summary>Se enseña temblando hasta que sale el último; cada regalo sale para llegar en su choque.</summary>
    private void Rafaga(double t)
    {
        SeguirAlJugador();
        while (_siguiente < choques.Length && t >= choques[_siguiente] - segundosDeVuelo)
        {
            // Solo el que toca ahora: si la canción entra tarde (un reintento), los de antes no salen.
            if (_esperado && t - (choques[_siguiente] - segundosDeVuelo) < 0.25) Lanzar();
            _siguiente++;
        }
        bool quedan = _siguiente < choques.Length;
        Ver(quedan);
        if (!quedan) { _esperado = false; return; }
        float s = Mathf.Sin(Time.time * 45f) * temblor;
        regalo.localPosition = _base + new Vector3(s, Mathf.Abs(s) * 0.5f, 0f);
        if (giro != 0f) regalo.localRotation = _giroBase * Quaternion.Euler(0f, 0f, (float)t * giro);
    }

    private void Lanzar()
    {
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo == null) return;
        Vector3 desde = regalo.position;
        // A donde va a estar cuando llegue, si sigue igual: hay que apartarse o saltar.
        Vector3 hacia = (sitio != null ? sitio.position : cuerpo.transform.position + _velocidad * (segundosDeVuelo * anticipacion))
                        + Vector3.up * alturaDelTiro;
        Vector3 recorrido = hacia - desde;
        float distancia = recorrido.magnitude;
        if (distancia < 0.1f) return;

        if (prefabDevolvible != null)
        {
            LanzarDevolvible(desde, recorrido, distancia);
            return;
        }
        var bola = lanzarConSuForma ? ConSuForma(desde) : Cubo(desde, recorrido);
        _enVuelo.Add(bola);
        var proyectil = bola.AddComponent<EnemyProjectile>();
        var cuerpoFisico = bola.GetComponent<Rigidbody>();
        if (cuerpoFisico != null)
        {
            cuerpoFisico.useGravity = false;
            if (giro != 0f)
            {
                cuerpoFisico.freezeRotation = false;
                cuerpoFisico.angularVelocity = recorrido / distancia * (giro * Mathf.Deg2Rad);
            }
        }
        proyectil.PermitirDevolucion(false);
        proyectil.SetSpeed(distancia / segundosDeVuelo);
        proyectil.AsignarLanzador(gameObject);
        proyectil.Initialize(recorrido, dano);
    }

    private GameObject Cubo(Vector3 desde, Vector3 recorrido)
    {
        var bola = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bola.name = "Regalito (lanzado)";
        bola.transform.SetPositionAndRotation(desde, Quaternion.LookRotation(recorrido));
        bola.transform.localScale = Vector3.one * tamano;
        Destroy(bola.GetComponent<Collider>());
        bola.AddComponent<SphereCollider>();
        if (material != null) bola.GetComponent<MeshRenderer>().sharedMaterial = material;
        return bola;
    }

    private GameObject ConSuForma(Vector3 desde)
    {
        var bola = new GameObject(regalo.name + " (lanzado)");
        bola.transform.SetPositionAndRotation(desde, regalo.rotation);
        bola.AddComponent<SphereCollider>().radius = tamano * 0.5f;
        var forma = Instantiate(regalo.gameObject, bola.transform);
        forma.transform.localPosition = Vector3.zero;
        forma.transform.localRotation = Quaternion.identity;
        forma.transform.localScale = regalo.lossyScale;
        forma.SetActive(true);
        return bola;
    }

    private void LanzarDevolvible(Vector3 desde, Vector3 recorrido, float distancia)
    {
        var bola = Instantiate(prefabDevolvible, desde, Quaternion.LookRotation(recorrido));
        var proyectil = bola.GetComponentInChildren<EnemyProjectile>();
        if (proyectil == null) { Destroy(bola); return; }
        _enVuelo.Add(bola);
        // El regalo, dentro de la bola: se ve qué es lo que hay que devolver.
        if (lanzarConSuForma)
        {
            var forma = Instantiate(regalo.gameObject, proyectil.transform);
            forma.transform.localPosition = Vector3.zero;
            forma.transform.localRotation = Quaternion.identity;
            forma.transform.localScale = regalo.lossyScale;
            forma.SetActive(true);
        }
        else
        {
            var caja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(caja.GetComponent<Collider>());
            caja.transform.SetParent(proyectil.transform, false);
            caja.transform.localScale = Vector3.one * tamano;
            if (material != null) caja.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        var cuerpoFisico = proyectil.GetComponent<Rigidbody>();
        if (cuerpoFisico != null) cuerpoFisico.isKinematic = false;
        proyectil.AsignarLanzador(destino != null ? destino : gameObject);
        if (destino != null) proyectil.FijarObjetivoDeDevolucion(destino.transform);
        proyectil.SetSpeed(distancia / segundosDeVuelo);
        proyectil.Initialize(recorrido, dano);
    }

    private void SeguirAlJugador()
    {
        if (sitio != null || anticipacion <= 0f) return;
        var cuerpo = ControlDelJugador.CuerpoDelJugador;
        if (cuerpo == null || Time.deltaTime <= 0f) return;
        Vector3 p = cuerpo.transform.position;
        Vector3 v = (p - _antes) / Time.deltaTime;
        v.y = 0f;
        if (v.sqrMagnitude > 400f) v = Vector3.zero;
        _velocidad = Vector3.Lerp(_velocidad, v, 1f - Mathf.Exp(-10f * Time.deltaTime));
        _antes = p;
    }

    private void Ver(bool si)
    {
        if (si == _visible || regalo == null) return;
        _visible = si;
        regalo.gameObject.SetActive(si);
        if (!si) regalo.localPosition = _base;
    }
}
