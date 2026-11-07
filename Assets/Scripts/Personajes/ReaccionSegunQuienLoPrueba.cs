using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Algo del mundo que reacciona distinto según quién del grupo lo pruebe (el que va al mando) y
/// cómo: con el botón de interactuar (<see cref="Interactable.OnInteract"/> →
/// <see cref="AlInteractuar"/>) o con su magia (<see cref="ReceptorDeMagia.alActivarse"/> →
/// <see cref="AlRecibirMagia"/>). Cada reacción, como datos: gesto de quien prueba, efecto, sonido,
/// un objeto que aparece un rato, una sacudida y frases del grupo. Las que cuentan como intento se
/// apuntan; al haberlas probado todas salta <see cref="alProbarTodas"/> (una vez por activación).
/// Para que solo un personaje pueda usar algo está <see cref="WillOnlyInteractable"/>; esto es para
/// que cada uno lo intente a su manera.
/// </summary>
public sealed class ReaccionSegunQuienLoPrueba : MonoBehaviour
{
    public enum Como { Interactuando, ConMagia }

    [Serializable]
    public sealed class Frase
    {
        public Slot hablante;
        public string clave;
    }

    [Serializable]
    public sealed class Reaccion
    {
        public Slot personaje;
        public Como como;
        [Tooltip("Es uno de los intentos que hay que probar para alProbarTodas.")]
        public bool cuenta = true;
        [Tooltip("Gesto de quien lo prueba (estado de su Animator).")]
        public string gesto;
        public GameObject vfx;
        public AudioClip sonido;
        [Range(0f, 1f)] public float volumen = 0.9f;
        [Tooltip("Objeto de la escena que aparece un rato (y se vuelve a esconder).")]
        public GameObject aparece;
        [Min(0.2f)] public float segundosQueAparece = 3f;
        [Tooltip("Se sacude como si se riera.")]
        public bool sacudir;
        public List<Frase> frases = new List<Frase>();
        public UnityEvent alReaccionar = new UnityEvent();
    }

    [SerializeField] private List<Reaccion> reacciones = new List<Reaccion>();
    [Tooltip("Dónde salen los efectos y qué se sacude (vacío = este objeto).")]
    [SerializeField] private Transform centro;
    [Tooltip("Aviso breve al activarse (clave): qué hay que hacer. Vacío = sin aviso.")]
    [SerializeField] private string claveDePista;
    [SerializeField, Min(1f)] private float segundosDeLaPista = 6f;
    [SerializeField, Min(0f)] private float pausaEntreFrases = 0.25f;

    [Tooltip("Al haber probado todas las reacciones que cuentan.")]
    public UnityEvent alProbarTodas = new UnityEvent();

    private readonly HashSet<int> _probadas = new HashSet<int>();
    private Coroutine _reaccion;
    private bool _todas;
    private Vector3 _escala;
    private Quaternion _giro;

    private Transform Centro => centro != null ? centro : transform;

    private void Awake()
    {
        _escala = Centro.localScale;
        _giro = Centro.localRotation;
    }

    private void OnEnable()
    {
        _probadas.Clear();
        _todas = false;
        if (!string.IsNullOrEmpty(claveDePista)) HudToastService.Instance?.Show(claveDePista, segundosDeLaPista);
    }

    private void OnDisable()
    {
        _reaccion = null;
        Centro.localScale = _escala;
        Centro.localRotation = _giro;
        foreach (var r in reacciones)
            if (r?.aparece != null) r.aparece.SetActive(false);
    }

    /// <summary>Para <see cref="Interactable.OnInteract"/>.</summary>
    public void AlInteractuar(GameObject quien) => Probar(Como.Interactuando);

    /// <summary>Para <see cref="ReceptorDeMagia.alActivarse"/>.</summary>
    public void AlRecibirMagia() => Probar(Como.ConMagia);

    private void Probar(Como como)
    {
        if (_reaccion != null || !isActiveAndEnabled) return;
        var activo = ControlDelJugador.Activo;
        int indice = reacciones.FindIndex(r => r != null && r.personaje == activo && r.como == como);
        if (indice >= 0) _reaccion = StartCoroutine(Co_Reaccionar(indice));
    }

    private IEnumerator Co_Reaccionar(int indice)
    {
        var r = reacciones[indice];
        var cuerpo = ControlDelJugador.CuerpoDe(r.personaje);
        if (!string.IsNullOrEmpty(r.gesto) && cuerpo != null)
            cuerpo.GetComponentInChildren<NPCSimpleAnimator>(true)?.PlayOneShot(r.gesto);
        if (r.vfx != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(r.vfx, Centro.position, Quaternion.identity, 3f);
        if (r.sonido != null) AudioService.Instance?.PlaySFXAt(r.sonido, Centro.position, r.volumen);
        if (r.aparece != null) StartCoroutine(Co_Aparecer(r.aparece, r.segundosQueAparece));
        if (r.sacudir) StartCoroutine(Co_Sacudir());
        r.alReaccionar?.Invoke();

        foreach (var frase in r.frases)
        {
            var linea = FrasesDelGrupo.Decir(frase.hablante, frase.clave);
            if (linea.Dicha) yield return new WaitForSeconds(linea.Duracion + pausaEntreFrases);
        }

        _reaccion = null;
        if (r.cuenta) _probadas.Add(indice);
        if (_todas || !TodasProbadas()) yield break;
        _todas = true;
        alProbarTodas.Invoke();
    }

    private bool TodasProbadas()
    {
        for (int i = 0; i < reacciones.Count; i++)
            if (reacciones[i] != null && reacciones[i].cuenta && !_probadas.Contains(i)) return false;
        return true;
    }

    private static IEnumerator Co_Aparecer(GameObject objeto, float segundos)
    {
        objeto.SetActive(true);
        yield return new WaitForSeconds(segundos);
        if (objeto != null) objeto.SetActive(false);
    }

    private IEnumerator Co_Sacudir()
    {
        var t = Centro;
        const float dura = 0.9f;
        for (float e = 0f; e < dura; e += Time.deltaTime)
        {
            float k = 1f - e / dura;
            t.localScale = _escala * (1f + 0.08f * k * Mathf.Abs(Mathf.Sin(e * 22f)));
            t.localRotation = _giro * Quaternion.Euler(0f, 0f, 6f * k * Mathf.Sin(e * 30f));
            yield return null;
        }
        t.localScale = _escala;
        t.localRotation = _giro;
    }
}
