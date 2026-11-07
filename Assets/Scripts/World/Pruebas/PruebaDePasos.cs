using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Prueba de hacer unas cosas en orden con el botón de interactuar (coger algo, usarlo en otro
/// sitio, subirse a algo): un <see cref="Interactable"/> por paso, y solo se puede usar el que toca.
/// Sin tiempo ni derrota: se gana al hacer el último. Cada paso puede avisar de lo que toca,
/// hacer un gesto del jugador, decir una frase, sonar, y enseñar o esconder objetos de la escena.
/// Las frases de personajes con nombre las dice un <see cref="IHablantesDePrueba"/> del mismo objeto
/// (el grupo); sin nombre, salen en bocadillo sobre el jugador.
/// </summary>
public sealed class PruebaDePasos : PruebaDelJugador
{
    [Serializable]
    public sealed class PasoConInteraccion
    {
        [Tooltip("Donde se hace el paso (modo Custom: lo que hace lo pone la prueba).")]
        public Interactable punto;
        [Tooltip("Aviso breve cuando le toca a este paso (clave). Vacío = sin aviso.")]
        public string claveDeAviso;
        [Tooltip("Gesto del jugador al hacerlo (estado de su Animator).")]
        public string gestoDelJugador;
        [Tooltip("Frase al hacerlo (clave).")]
        public string clave;
        [Tooltip("Quién la dice, por su nombre. Vacío = el jugador.")]
        public string hablante;
        [Tooltip("Objeto de la escena que desaparece al hacerlo (lo que se coge).")]
        public GameObject ocultarAlHacerlo;
        [Tooltip("Objeto de la escena que aparece al hacerlo.")]
        public GameObject mostrarAlHacerlo;
        public AudioClip sonido;
        [Range(0f, 1f)] public float volumen = 0.8f;
        public UnityEvent alHacerlo = new UnityEvent();
    }

    [Header("Pasos")]
    [SerializeField] private List<PasoConInteraccion> pasos = new List<PasoConInteraccion>();
    [Tooltip("Segundos entre el último paso y darla por ganada.")]
    [SerializeField, Min(0f)] private float pausaTrasElUltimo = 0.6f;
    [SerializeField, Min(0.5f)] private float segundosDelAviso = 4f;

    /// <summary>El paso que toca (0 el primero); igual al número de pasos cuando están todos hechos.</summary>
    public int Siguiente { get; private set; }

    private readonly List<IHablantesDePrueba> _hablantes = new List<IHablantesDePrueba>();
    private readonly List<UnityAction<GameObject>> _escuchas = new List<UnityAction<GameObject>>();
    private Coroutine _final;

    protected override void Awake()
    {
        base.Awake();
        GetComponents(_hablantes);
    }

    protected override void PrepararIntento()
    {
        Siguiente = 0;
        foreach (var paso in pasos)
        {
            if (paso == null) continue;
            if (paso.punto != null) paso.punto.EnableInteraction(false);
            if (paso.ocultarAlHacerlo != null) paso.ocultarAlHacerlo.SetActive(true);
            if (paso.mostrarAlHacerlo != null) paso.mostrarAlHacerlo.SetActive(false);
        }
    }

    protected override void Arrancar()
    {
        Escuchar(true);
        Habilitar(0);
    }

    protected override void Actualizar(float dt) { }

    protected override void Detener()
    {
        if (_final != null) StopCoroutine(_final);
        _final = null;
        Escuchar(false);
        foreach (var paso in pasos)
            if (paso?.punto != null) paso.punto.EnableInteraction(false);
    }

    private void Escuchar(bool escuchar)
    {
        if (!escuchar)
        {
            for (int i = 0; i < _escuchas.Count && i < pasos.Count; i++)
                if (pasos[i]?.punto != null) pasos[i].punto.OnInteract.RemoveListener(_escuchas[i]);
            _escuchas.Clear();
            return;
        }
        Escuchar(false);
        for (int i = 0; i < pasos.Count; i++)
        {
            int indice = i;
            UnityAction<GameObject> escucha = _ => Hacer(indice);
            _escuchas.Add(escucha);
            if (pasos[i]?.punto != null) pasos[i].punto.OnInteract.AddListener(escucha);
        }
    }

    private void Habilitar(int indice)
    {
        Siguiente = indice;
        if (indice >= pasos.Count) return;
        var paso = pasos[indice];
        if (paso?.punto != null) paso.punto.EnableInteraction(true);
        if (!string.IsNullOrEmpty(paso?.claveDeAviso)) HudToastService.Instance?.Show(paso.claveDeAviso, segundosDelAviso);
    }

    private void Hacer(int indice)
    {
        if (!EnCurso || indice != Siguiente || indice >= pasos.Count) return;
        var paso = pasos[indice];
        if (paso.punto != null) paso.punto.EnableInteraction(false);
        if (paso.ocultarAlHacerlo != null) paso.ocultarAlHacerlo.SetActive(false);
        if (paso.mostrarAlHacerlo != null) paso.mostrarAlHacerlo.SetActive(true);
        if (paso.sonido != null) AudioService.Instance?.PlaySFX(paso.sonido, paso.volumen);
        if (!string.IsNullOrEmpty(paso.gestoDelJugador) && PlayerService.Player != null)
            PlayerService.Player.GetComponentInChildren<NPCSimpleAnimator>(true)?.PlayOneShot(paso.gestoDelJugador);
        Decir(paso.hablante, paso.clave);
        paso.alHacerlo?.Invoke();

        Habilitar(indice + 1);
        if (Siguiente >= pasos.Count) _final = StartCoroutine(Co_Acabar());
    }

    private IEnumerator Co_Acabar()
    {
        if (pausaTrasElUltimo > 0f) yield return new WaitForSeconds(pausaTrasElUltimo);
        _final = null;
        Ganar();
    }

    private void Decir(string hablante, string clave)
    {
        if (string.IsNullOrEmpty(clave)) return;
        if (!string.IsNullOrEmpty(hablante))
            foreach (var hablantes in _hablantes)
                if (hablantes.Decir(hablante, clave).Dicha) return;
        var jugador = PlayerService.Player;
        if (jugador == null) return;
        PresentadorDeLinea.Decir(new FraseADecir { clave = clave, ancla = jugador.transform, presentacion = PresentacionDeTexto.Bocadillo });
    }
}
