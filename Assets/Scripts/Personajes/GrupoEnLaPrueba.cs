using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// El grupo en una prueba del jugador (<see cref="PruebaDelJugador"/>, como etapas suyas): quién la
/// juega (le pasa el mando), dónde esperan los demás mientras tanto y qué hacen allí (quietos,
/// botando o paseando), y qué dicen al presentarla, al reintentarla, al ganarla y cuando el jugador
/// pasa a su lado. Va en el mismo objeto que la prueba.
///
/// A los compañeros los saca de seguir al jugador (<see cref="SequenceActor.Hold"/>) y los ancla
/// (no se teletransportan con el grupo) mientras dura la prueba; al soltarla vuelven a seguirle.
/// Las frases salen por <see cref="FrasesDelGrupo"/>: en bocadillo si quien habla está cerca del
/// jugador y, si no, en subtítulo con su nombre. La prueba también le pide frases sueltas por el
/// nombre del personaje (<see cref="IHablantesDePrueba"/>).
/// </summary>
public sealed class GrupoEnLaPrueba : MonoBehaviour, IEtapasDePrueba, IHablantesDePrueba
{
    private const float Gravedad = 18f;

    public enum Numerito
    {
        /// <summary>Espera en su sitio.</summary>
        Quieto,
        /// <summary>Bota en el sitio una y otra vez (en un trampolín, si lo hay).</summary>
        Botar,
        /// <summary>Pasea despacio de su sitio al otro y vuelta (necesita NavMesh).</summary>
        Pasear,
    }

    [Serializable]
    public sealed class Frase
    {
        public Slot hablante;
        public string clave;
    }

    [Serializable]
    public sealed class Puesto
    {
        public Slot personaje;
        [Tooltip("Donde espera (mira hacia su eje Z).")]
        public Transform sitio;
        public Numerito numerito;
        [Tooltip("Botar: trampolín que se aplasta en cada bote (opcional).")]
        public LanzadorDeSalto trampolin;
        [Min(0.5f)] public float alturaDelBote = 5f;
        [Min(0f)] public float pausaEntreBotes = 0.5f;
        [Tooltip("Pasear: el otro extremo del paseo.")]
        public Transform otroSitio;
        [Min(0.3f)] public float velocidadAlPasear = 0.9f;
        [Tooltip("Frases cuando el jugador pasa cerca: una al azar, una vez por intento.")]
        public string[] frasesAlPasar = new string[0];
        [Min(1f)] public float radioParaHablar = 7f;
    }

    [Header("Quién juega")]
    [SerializeField] private bool pasarElMando = true;
    [SerializeField] private Slot alMando = Slot.Estela;
    [Tooltip("Vida y maná a tope al empezar cada intento.")]
    [SerializeField] private bool curarAlEmpezar = true;

    [Header("Los demás")]
    [SerializeField] private List<Puesto> puestos = new List<Puesto>();

    [Header("Frases")]
    [Tooltip("En orden, antes de la cuenta atrás del primer intento.")]
    [SerializeField] private List<Frase> alEmpezar = new List<Frase>();
    [Tooltip("Una al azar antes de la cuenta atrás de cada reintento.")]
    [SerializeField] private List<Frase> alReintentar = new List<Frase>();
    [Tooltip("En orden, al ganar.")]
    [SerializeField] private List<Frase> alGanar = new List<Frase>();
    [Tooltip("Metros al jugador hasta los que la frase sale en bocadillo sobre quien habla.")]
    [SerializeField, Min(2f)] private float distanciaParaBocadillo = FrasesDelGrupo.DistanciaParaBocadillo;
    [SerializeField, Min(0f)] private float pausaEntreFrases = 0.3f;

    private sealed class Ocupado
    {
        public Puesto puesto;
        public NPCPartyMember npc;
        public SequenceActor actor;
        public Coroutine numerito;
        public bool agenteEncendido;
        public bool dicho;
        public readonly List<Collider> apagados = new List<Collider>();
    }

    private readonly List<Ocupado> _ocupados = new List<Ocupado>();
    private float _siguienteMirada;

    // ── Etapas de la prueba ──────────────────────────────────────────────────────────────────

    public IEnumerator Colocar(PruebaDelJugador prueba)
    {
        if (pasarElMando && ControlDelJugador.Activo != alMando && ControlDelJugador.PasarElMandoA(alMando))
        {
            float limite = Time.unscaledTime + 2f;
            while (ControlDelJugador.Activo != alMando && Time.unscaledTime < limite) yield return null;
        }
        if (curarAlEmpezar && SaludDelGrupo.Instance != null) SaludDelGrupo.Instance.CurarATodos();

        SoltarCompaneros();
        foreach (var puesto in puestos)
        {
            if (puesto?.sitio == null) continue;
            NPCPartyMember npc;
            float limite = Time.unscaledTime + 1.5f;
            while ((npc = ControlDelJugador.NpcDe(puesto.personaje)) == null && Time.unscaledTime < limite) yield return null;
            if (npc != null) Ocupar(puesto, npc);
        }
    }

    public IEnumerator Presentar(PruebaDelJugador prueba)
    {
        if (prueba.Intento <= 1)
        {
            foreach (var frase in alEmpezar) yield return Co_Decir(frase);
        }
        else if (alReintentar.Count > 0)
        {
            yield return Co_Decir(alReintentar[UnityEngine.Random.Range(0, alReintentar.Count)]);
        }
    }

    public IEnumerator Celebrar(PruebaDelJugador prueba)
    {
        foreach (var frase in alGanar) yield return Co_Decir(frase);
    }

    public IEnumerator Lamentar(PruebaDelJugador prueba)
    {
        yield break;
    }

    public void Soltar(PruebaDelJugador prueba) => SoltarCompaneros();

    private void OnDisable() => SoltarCompaneros();

    // ── Frases al pasar ──────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_ocupados.Count == 0 || Time.time < _siguienteMirada) return;
        _siguienteMirada = Time.time + 0.25f;
        var jugador = PlayerService.Player;
        if (jugador == null) return;
        Vector3 p = jugador.transform.position;
        foreach (var o in _ocupados)
        {
            if (o.dicho || o.npc == null || o.puesto.frasesAlPasar == null || o.puesto.frasesAlPasar.Length == 0) continue;
            float r = o.puesto.radioParaHablar;
            if ((o.npc.transform.position - p).sqrMagnitude > r * r) continue;
            o.dicho = true;
            Decir(o.puesto.personaje, o.puesto.frasesAlPasar[UnityEngine.Random.Range(0, o.puesto.frasesAlPasar.Length)]);
        }
    }

    private IEnumerator Co_Decir(Frase frase)
    {
        if (frase == null) yield break;
        var linea = Decir(frase.hablante, frase.clave);
        if (linea.Dicha) yield return new WaitForSeconds(linea.Duracion + pausaEntreFrases);
    }

    private LineaDicha Decir(Slot hablante, string clave) => FrasesDelGrupo.Decir(hablante, clave, distanciaParaBocadillo);

    /// <summary>Una frase de un personaje del grupo por su nombre (la piden las pruebas por <see cref="IHablantesDePrueba"/>).</summary>
    public LineaDicha Decir(string hablante, string clave) => FrasesDelGrupo.Decir(hablante, clave, distanciaParaBocadillo);

    // ── Compañeros en su puesto ──────────────────────────────────────────────────────────────

    private void Ocupar(Puesto puesto, NPCPartyMember npc)
    {
        var actor = SequenceActor.ForTransform("GrupoEnLaPrueba_" + puesto.personaje, npc.transform);
        var o = new Ocupado { puesto = puesto, npc = npc, actor = actor, agenteEncendido = actor.Agent != null && actor.Agent.enabled };
        actor.Hold();
        npc.NPCManager?.Context?.Anclar(this);
        SequenceMovement.PlaceAt(actor, puesto.sitio.position, puesto.sitio.rotation);

        if (puesto.numerito == Numerito.Botar)
        {
            // En el aire no choca con nadie: el jugador también usa el trampolín.
            foreach (var c in npc.GetComponentsInChildren<Collider>())
                if (c.enabled && !c.isTrigger) { c.enabled = false; o.apagados.Add(c); }
            if (actor.Agent != null) actor.Agent.enabled = false;
            o.numerito = StartCoroutine(Co_Botar(o));
        }
        else if (puesto.numerito == Numerito.Pasear && puesto.otroSitio != null &&
                 actor.Agent != null && actor.Agent.enabled && actor.Agent.isOnNavMesh)
        {
            o.numerito = StartCoroutine(Co_Pasear(o));
        }
        _ocupados.Add(o);
    }

    private IEnumerator Co_Botar(Ocupado o)
    {
        var t = o.npc.transform;
        Vector3 suelo = o.puesto.sitio.position;
        Quaternion giro = o.puesto.sitio.rotation;
        while (true)
        {
            float h = o.puesto.alturaDelBote;
            float duracion = 2f * Mathf.Sqrt(2f * h / Gravedad);
            if (o.puesto.trampolin != null) o.puesto.trampolin.Rebotar();
            for (float e = 0f; e < duracion; e += Time.deltaTime)
            {
                float k = e / duracion;
                t.SetPositionAndRotation(suelo + Vector3.up * (4f * h * k * (1f - k)), giro * Quaternion.Euler(0f, 360f * k, 0f));
                yield return null;
            }
            t.SetPositionAndRotation(suelo, giro);
            if (o.puesto.pausaEntreBotes > 0f)
                yield return new WaitForSeconds(o.puesto.pausaEntreBotes * UnityEngine.Random.Range(0.7f, 1.3f));
        }
    }

    private IEnumerator Co_Pasear(Ocupado o)
    {
        var espera = new WaitForSeconds(1.5f);
        while (true)
        {
            yield return SequenceMovement.MoveTo(o.actor, o.puesto.otroSitio.position, o.puesto.velocidadAlPasear, 20f);
            yield return espera;
            yield return SequenceMovement.MoveTo(o.actor, o.puesto.sitio.position, o.puesto.velocidadAlPasear, 20f);
            yield return espera;
        }
    }

    private void SoltarCompaneros()
    {
        foreach (var o in _ocupados)
        {
            if (o.numerito != null) StopCoroutine(o.numerito);
            foreach (var c in o.apagados) if (c != null) c.enabled = true;
            if (o.npc == null) continue;
            if (o.puesto.numerito == Numerito.Botar && o.puesto.sitio != null)
                o.npc.transform.SetPositionAndRotation(o.puesto.sitio.position, o.puesto.sitio.rotation);
            if (o.actor.Agent != null && o.agenteEncendido && !o.actor.Agent.enabled) o.actor.Agent.enabled = true;
            o.npc.NPCManager?.Context?.Soltar(this);
            o.actor.Release();
            o.actor.ForzarEnElSuelo();
        }
        _ocupados.Clear();
    }
}
