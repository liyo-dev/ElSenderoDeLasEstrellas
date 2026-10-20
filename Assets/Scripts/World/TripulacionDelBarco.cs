using System.Collections;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Los compañeros que suben al barco con el jugador (Estela y Liam) y sus sitios a bordo.
///
/// Cada uno entra a su manera, que es lo que da vida: Will salta y da una voltereta (lo pone
/// <see cref="VolteretaDelJugador"/>), Estela da un salto corto y Liam trepa con más esfuerzo.
/// Los tres van a su sitio, así que no se ven apegotonados.
///
/// Para moverlos se usa el sistema oficial: <see cref="NPCPartyMember.MoveToDialoguePosition"/>
/// los lleva andando hasta el barco y, al llegar, el agente deja de llevar su posición (el
/// estado los deja quietos), así que la red se puede llevar al sitio a mano.
/// </summary>
[DisallowMultipleComponent]
public class TripulacionDelBarco : MonoBehaviour
{
    [Header("Puestos a bordo")]
    [SerializeField] private Transform puestoDeEstela;
    [SerializeField] private Transform puestoDeLiam;

    [Header("Embarque")]
    [Tooltip("Punto del casco donde se suben (al costado, a la altura del agua).")]
    [SerializeField] private Transform puntoDeEmbarque;
    [Tooltip("Segundos que se espera a que el compañero llegue andando antes de subirlo igualmente.")]
    [SerializeField] private float esperaDeLlegada = 1.5f;
    [Tooltip("Distancia a la que se considera que el compañero ya está en el barco.")]
    [SerializeField] private float distanciaDeArribada = 0.7f;

    [Header("Entradas de cada uno")]
    [Tooltip("Estado del Animator con el que saltan los dos.")]
    [SerializeField] private string estadoDelSalto = "JumpFull_InPlace_NoWeapon";
    [Tooltip("Altura del salto (m).")]
    [SerializeField] private float alturaDelSalto = 0.9f;
    [Tooltip("Segundos que dura el salto.")]
    [SerializeField] private float duracionDelSalto = 0.4f;

    /// <summary>Cómo se quedan en el barco cuando ya han subido.</summary>
    private enum Postura
    {
        /// De pie, apoyado (el idle normal: una pose estable, sin bucle raro).
        Apoyado,
        /// Sentado (la actividad ambiental de sentarse del proyecto).
        Sentado,
    }

    private Coroutine _subida;
    private readonly System.Collections.Generic.Dictionary<Slot, GameObject> _aBordo = new();

    /// <summary>Sitio de Estela a bordo.</summary>
    public Transform PuestoDeEstela => puestoDeEstela;

    /// <summary>Sitio de Liam a bordo.</summary>
    public Transform PuestoDeLiam => puestoDeLiam;

    /// <summary>El punto del casco donde se suben.</summary>
    public Transform PuntoDeEmbarque => puntoDeEmbarque;

    /// <summary>Companeros que ya están en el barco.</summary>
    public int CuantosABordo => _aBordo.Count;

    /// <summary>Cuántos compañeros deberían subir (los que hay en el grupo).</summary>
    public int CompanerosEsperados
    {
        get
        {
            int n = 0;
            if (ObtenerNpc(Slot.Estela) != null) n++;
            if (ObtenerNpc(Slot.Liam) != null) n++;
            return n;
        }
    }

    /// <summary>True mientras la tripulación está subiendo: el barco espera a que suban.</summary>
    public bool Subiendo => _subida != null && CuantosABordo < CompanerosEsperados;

    /// <summary>Asigna los sitios y el punto de embarque (lo llama el generador del mundo).</summary>
    public void Configurar(Transform estela, Transform liam, Transform embarque)
    {
        puestoDeEstela = estela;
        puestoDeLiam = liam;
        puntoDeEmbarque = embarque;
    }

    /// <summary>Llama a los compañeros al barco: andan hasta él y suben a su sitio.</summary>
    public void SubirATripulacion()
    {
        if (_subida != null) return;
        _subida = StartCoroutine(Co_Subir());
    }

    /// <summary>Baja a los compañeros del barco (aún no los baja del barco, solo los suelta).</summary>
    public void BajarATripulacion()
    {
        if (_subida != null)
        {
            StopCoroutine(_subida);
            _subida = null;
        }

        // Se les deja en el punto de desembarque, al lado del jugador.
        Vector3 salida = puntoDeEmbarque != null ? puntoDeEmbarque.position : transform.position;
        foreach (var kv in _aBordo)
        {
            var npc = ObtenerNpc(kv.Key);
            if (npc == null) continue;
            npc.transform.position = salida;
            npc.ReleaseDialoguePosition();
        }
        _aBordo.Clear();
    }

    IEnumerator Co_Subir()
    {
        // De uno en uno: primero Estela y, cuando está dentro, Liam.
        SubirUno(Slot.Estela, puestoDeEstela, Postura.Sentado);
        while (_aBordo.Count == 0 && HayAlguienPendiente()) yield return null;

        SubirUno(Slot.Liam, puestoDeLiam, Postura.Apoyado);
        while (_aBordo.Count < 2 && HayAlguienPendiente()) yield return null;

        // Ya a bordo: se quedan en su sitio (que se mueve con el barco) mirando al frente.
        while (_aBordo.Count > 0)
        {
            foreach (var kv in _aBordo)
            {
                var puesto = PuestoDe(kv.Key);
                if (puesto == null) continue;
                var cuerpo = kv.Value != null ? kv.Value.transform : null;
                if (cuerpo == null) continue;
                cuerpo.position = puesto.position;
                cuerpo.rotation = puesto.rotation;
            }
            yield return null;
        }

        _subida = null;
    }

    void SubirUno(Slot personaje, Transform puesto, Postura postura)
    {
        if (puesto == null) return;
        var npc = ObtenerNpc(personaje);
        if (npc == null || npc.gameObject == null) return;

        Vector3 embarque = puntoDeEmbarque != null ? puntoDeEmbarque.position : puesto.position;

        // El compañero anda hasta el barco por su cuenta (su IA), pero poco: si no llega en
        // un par de segundos, se le sube igualmente (subir es casi instantáneo).
        npc.MoveToDialoguePosition(embarque, esperaDeLlegada);

        StartCoroutine(Co_Saltar(personaje, npc, puesto, postura, embarque));
    }

    IEnumerator Co_Saltar(Slot personaje, NPCPartyMember npc, Transform puesto, Postura postura,
                          Vector3 embarque)
    {
        // Espera a que llegue al casco (o a que se agote la espera).
        float t0 = Time.time;
        while (Time.time - t0 < esperaDeLlegada)
        {
            var cuerpo = npc != null ? npc.transform : null;
            if (cuerpo == null) yield break;
            if (Vector3.Distance(cuerpo.position, embarque) <= distanciaDeArribada) break;
            yield return null;
        }

        var animator = npc != null ? npc.GetComponentInChildren<NPCSimpleAnimator>() : null;
        if (animator != null && !string.IsNullOrEmpty(estadoDelSalto)) animator.PlayOneShot(estadoDelSalto);

        // El salto al sitio: una parábola corta con el pico por encima del borde.
        var cuerpo2 = npc != null ? npc.transform : null;
        if (cuerpo2 == null) yield break;

        Vector3 inicio = cuerpo2.position;
        Vector3 fin = puesto.position;
        Quaternion giro = cuerpo2.rotation;

        for (float s = 0; s < duracionDelSalto; s += Time.deltaTime)
        {
            if (npc == null) yield break;
            float u = Mathf.Clamp01(s / duracionDelSalto);
            Vector3 p = Vector3.Lerp(inicio, fin, u);
            p.y += alturaDelSalto * 4f * u * (1f - u);
            cuerpo2.position = p;
            cuerpo2.rotation = giro;
            yield return null;
        }

        cuerpo2.position = fin;
        cuerpo2.rotation = puesto.rotation;
        _aBordo[personaje] = npc.gameObject;

        // Y se queda en su postura: el salto se queda en bucle si no se le cambia de estado
        // (parece que están convulsando).
        yield return null;
        var animator2 = npc != null ? npc.GetComponentInChildren<NPCSimpleAnimator>() : null;
        if (animator2 != null)
        {
            if (postura == Postura.Sentado) animator2.PlayAmbientActivity(NPCAmbientActivity.SitLow);
            else animator2.PlayIdleNormal();
        }
    }

    bool HayAlguienPendiente()
    {
        return ObtenerNpc(Slot.Estela) != null && !_aBordo.ContainsKey(Slot.Estela)
            || ObtenerNpc(Slot.Liam) != null && !_aBordo.ContainsKey(Slot.Liam);
    }

    Transform PuestoDe(Slot personaje)
    {
        if (personaje == Slot.Estela) return puestoDeEstela;
        if (personaje == Slot.Liam) return puestoDeLiam;
        return null;
    }

    static NPCPartyMember ObtenerNpc(Slot personaje)
    {
        var npc = ControlDelJugador.NpcDe(personaje);
        return npc;
    }
}
