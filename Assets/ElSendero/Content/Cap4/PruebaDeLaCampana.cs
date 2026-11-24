using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La prueba de la campana del Cap. 4 (GDD § 5.8, bloque 4.3): cuatro tandas de fuego mientras Will
/// cruza el claro hasta la campana. Estela lanza bolas de verdad, con poco daño: se pasa esquivando o
/// bajo la Cúpula Estelar, que es lo que la prueba enseña a usar. Si el fuego de Will quema una figura
/// de barro, la tanda se repite y las figuras se rehacen (como en la novela). Al aguantar las cuatro,
/// la campana se puede tocar.
///
/// Vive en el grupo de las figuras: el tramo de la historia (<see cref="ActivoEnTramo"/>) lo enciende
/// al empezar la prueba y lo apaga al acabar, así que no hace falta que nadie lo llame.
/// </summary>
[DisallowMultipleComponent]
public class PruebaDeLaCampana : MonoBehaviour
{
    [Header("Quién y qué")]
    [Tooltip("persistenceId de Estela, la que lanza.")]
    [SerializeField] private string idDeEstela = "_ESTELA";
    [Tooltip("Prefab de la bola (con EnemyProjectile).")]
    [SerializeField] private GameObject proyectil;
    [Tooltip("Las figuras de barro: no se puede chamuscar ninguna.")]
    [SerializeField] private Burnable[] figuras;
    [Tooltip("La campana: no se puede tocar hasta aguantar las tandas.")]
    [SerializeField] private Interactable campana;

    [Header("Las tandas")]
    [Tooltip("Cuántas tandas hay que aguantar.")]
    [SerializeField, Min(1)] private int tandas = 4;
    [Tooltip("Bolas de cada tanda, de la primera a la última. Si se acaban, la última se repite.")]
    [SerializeField] private int[] bolasPorTanda = { 2, 3, 4, 5 };
    [Tooltip("Segundos entre una bola y la siguiente.")]
    [SerializeField, Min(0.1f)] private float segundosEntreBolas = 1.4f;
    [Tooltip("Segundos desde que empieza la prueba hasta la primera bola: tiempo para colocarse.")]
    [SerializeField, Min(0f)] private float segundosDePreparacion = 2.5f;
    [Tooltip("Segundos de respiro entre tanda y tanda.")]
    [SerializeField, Min(0f)] private float segundosEntreTandas = 1.8f;
    [Tooltip("Velocidad de las bolas.")]
    [SerializeField, Min(1f)] private float velocidad = 13f;
    [Tooltip("Daño de cada bola. Bajo a propósito: molestan, no matan.")]
    [SerializeField, Min(0f)] private float danoDeCadaBola = 5f;
    [Tooltip("Lo que se repite una tanda si se chamusca una figura, para que no se quede en bucle.")]
    [SerializeField, Min(0)] private int repeticionesMaximas = 3;

    [Header("Lo que dice Estela")]
    [Tooltip("Clave de traducción de cada tanda, de la primera a la última.")]
    [SerializeField] private string[] clavesDeTanda = { "CAP4_CAMPANA_RONDA_1", "CAP4_CAMPANA_RONDA_2", "CAP4_CAMPANA_RONDA_3", "CAP4_CAMPANA_RONDA_4" };
    [SerializeField] private string[] textosDeTanda = { "¡Una! ¡Y no te quedes quieto!", "¡Dos! ¡Y no me quemes a la gente!", "¡Tres! ¿Ya te has cansado?", "¡Cuatro! ¡La campana es tuya!" };
    [Tooltip("Cuando el fuego de Will chamusca una figura y la tanda se repite.")]
    [SerializeField] private string claveAlChamuscar = "CAP4_CAMPANA_CHAMUSCADA";
    [SerializeField] private string textoAlChamuscar = "¡Otra vez! ¡Las figuras son gente!";
    [Tooltip("Al aguantar la última tanda.")]
    [SerializeField] private string claveAlAcabar = "CAP4_CAMPANA_ACABADA";
    [SerializeField] private string textoAlAcabar = "Ya está. Ahora tócala.";

    private SequenceActor _estela;
    private Coroutine _prueba;
    private bool _chamusco;
    private bool _acabada;
    private int _turnoAviso = -1;
    private MaterialPropertyBlock _quemado;

    void Start()
    {
        // La campana no se toca hasta el final: el disparador se enciende al aguantar las tandas.
        if (campana != null) campana.enabled = false;
        foreach (var figura in figuras)
        {
            if (figura == null) continue;
            figura.onBurned ??= new UnityEngine.Events.UnityEvent();
            var laFigura = figura;
            figura.onBurned.AddListener(() => AlChamuscar(laFigura));
        }
        _prueba = StartCoroutine(Co_Prueba());
    }

    void OnDisable()
    {
        if (_prueba != null) StopCoroutine(_prueba);
        _prueba = null;
        if (_turnoAviso >= 0) { PresentadorDeLinea.Esconder(_turnoAviso); _turnoAviso = -1; }
        _estela = null;
    }

    // ── La prueba ──────────────────────────────────────────────────────────────

    private IEnumerator Co_Prueba()
    {
        if (proyectil == null || proyectil.GetComponentInChildren<EnemyProjectile>(true) == null)
        {
            Debug.LogWarning("[PruebaDeLaCampana] Sin proyectil con EnemyProjectile: la prueba no puede correr. Se deja la campana abierta para no bloquear.");
            AbrirLaCampana();
            yield break;
        }

        // Estela lanza desde donde esté: va en el grupo y sigue al jugador, así que siempre está cerca.
        SequenceActor.TryResolve(idDeEstela, out _estela);

        if (segundosDePreparacion > 0f) yield return new WaitForSeconds(segundosDePreparacion);

        for (int tanda = 0; tanda < tandas; tanda++)
        {
            int bolas = BolasDe(tanda);
            int repeticiones = 0;
            while (true)
            {
                _chamusco = false;
                Decir(ClaveDeTanda(tanda), TextoDeTanda(tanda));

                for (int i = 0; i < bolas; i++)
                {
                    if (!PlayerService.TryGetPlayer(out var jugador) || jugador == null) yield break;
                    Lanzar(jugador.transform.position);
                    yield return new WaitForSeconds(segundosEntreBolas);
                    // El humo y el aviso se ven antes de volver a lanzar: si ya ha ardido una figura, se corta.
                    if (_chamusco) break;
                }

                if (!_chamusco) break;
                if (repeticiones >= repeticionesMaximas) break;
                repeticiones++;
                RehacerLasFiguras();
                Decir(claveAlChamuscar, textoAlChamuscar);
                yield return new WaitForSeconds(segundosEntreTandas);
            }

            RehacerLasFiguras();
            if (tanda < tandas - 1) yield return new WaitForSeconds(segundosEntreTandas);
        }

        Decir(claveAlAcabar, textoAlAcabar);
        AbrirLaCampana();
        _estela = null;
    }

    private int BolasDe(int tanda) =>
        bolasPorTanda == null || bolasPorTanda.Length == 0 ? 2 : Mathf.Max(1, bolasPorTanda[Mathf.Min(tanda, bolasPorTanda.Length - 1)]);

    private string ClaveDeTanda(int tanda) =>
        clavesDeTanda == null || clavesDeTanda.Length == 0 ? "" : clavesDeTanda[Mathf.Min(tanda, clavesDeTanda.Length - 1)];

    private string TextoDeTanda(int tanda) =>
        textosDeTanda == null || textosDeTanda.Length == 0 ? "" : textosDeTanda[Mathf.Min(tanda, textosDeTanda.Length - 1)];

    private void Lanzar(Vector3 piesDelJugador)
    {
        Transform desde = _estela != null ? _estela.Transform : transform;
        if (desde == null) return;

        Vector3 origen = desde.position + Vector3.up * 1.4f + desde.forward * 0.6f;
        Vector3 destino = piesDelJugador + Vector3.up * 1.1f;
        Vector3 direccion = (destino - origen).normalized;

        var bola = Instantiate(proyectil, origen, Quaternion.LookRotation(direccion));
        var proyectilDeLaBola = bola.GetComponentInChildren<EnemyProjectile>();
        if (proyectilDeLaBola == null) { Destroy(bola); return; }
        var rb = proyectilDeLaBola.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        proyectilDeLaBola.AsignarLanzador(_estela != null ? _estela.Transform.gameObject : gameObject);
        proyectilDeLaBola.PermitirDevolucion(false);
        proyectilDeLaBola.AsignarElemento(MagicElement.Fire);
        EnemyProjectile.TenirConElemento(bola, MagicElement.Fire);
        proyectilDeLaBola.SetSpeed(velocidad);
        proyectilDeLaBola.Initialize(direccion, danoDeCadaBola);

        _estela?.Face(piesDelJugador);
    }

    // ── Las figuras ────────────────────────────────────────────────────────────

    private void AlChamuscar(Burnable figura)
    {
        _chamusco = true;
        // El barro se queda negro mientras la tanda se repite; RehacerLasFiguras le devuelve el color.
        _quemado ??= new MaterialPropertyBlock();
        _quemado.SetColor("_BaseColor", new Color(0.16f, 0.11f, 0.09f));
        foreach (var r in figura.GetComponentsInChildren<Renderer>(true))
            if (r != null) r.SetPropertyBlock(_quemado);
    }

    /// <summary>
    /// Las figuras vuelven a estar enteras: se les quita lo quemado y se les devuelve el color y el
    /// choque. El <see cref="Burnable"/> se reinicia para que puedan volver a arder en la tanda
    /// siguiente.
    /// </summary>
    private void RehacerLasFiguras()
    {
        foreach (var figura in figuras)
        {
            if (figura == null) continue;
            figura.Resetear();
            foreach (var r in figura.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.SetPropertyBlock(null);
        }
    }

    private void AbrirLaCampana()
    {
        if (_acabada) return;
        _acabada = true;
        if (campana != null) campana.enabled = true;
    }

    private void Decir(string clave, string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (_turnoAviso >= 0) PresentadorDeLinea.Esconder(_turnoAviso);
        var frase = PresentadorDeLinea.Decir(new FraseADecir
        {
            clave = clave,
            textoPorDefecto = texto,
            ancla = _estela != null ? _estela.Transform : transform,
            duracion = 2.2f
        });
        _turnoAviso = frase.Turno;
    }
}
