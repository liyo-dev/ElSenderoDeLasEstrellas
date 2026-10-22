using System.Collections;
using System.Collections.Generic;
using Game.NPC;
using Game.NPC.States;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// La tripulación que sube al barco con el jugador: todos los personajes del grupo que no van al
/// timón, cada uno en uno de los dos puestos de cubierta.
///
/// Da igual quién conduzca: quien lleva el mando se queda en el puesto de conducción y los otros
/// dos se reparten babor y estribor. Estela se sienta y Will y Liam se quedan de pie, apoyados.
///
/// Para llevarlos hasta el casco se usa el sistema oficial
/// <see cref="NPCPartyMember.MoveToDialoguePosition"/>; al llegar se les toma el control (agente
/// parado, rotación automática apagada y estado de diálogo anclado), se les cuelga de su puesto
/// —que es hijo del barco, así que se mueven con él— y miran hacia la proa. Mientras están a bordo quedan
/// anclados (<see cref="Game.NPC.Common.NPCStateContext.IsPinnedByParty"/>), para que ni el
/// seguimiento del grupo ni el cambio de personaje los muevan de su sitio.
/// </summary>
[DisallowMultipleComponent]
public class TripulacionDelBarco : MonoBehaviour
{
    [Header("Puestos a bordo")]
    [Tooltip("Puesto de babor: lo ocupa el primer compañero que sube (Estela, si está en el grupo).")]
    [SerializeField, FormerlySerializedAs("puestoDeEstela")] private Transform puestoDeBabor;
    [Tooltip("Puesto de estribor: lo ocupa el segundo compañero que sube.")]
    [SerializeField, FormerlySerializedAs("puestoDeLiam")] private Transform puestoDeEstribor;

    [Header("Embarque")]
    [Tooltip("Punto del casco donde se suben (al costado, a la altura del agua).")]
    [SerializeField] private Transform puntoDeEmbarque;
    [Tooltip("Segundos que se espera a que el compañero llegue andando antes de subirlo igualmente.")]
    [SerializeField] private float esperaDeLlegada = 1.5f;
    [Tooltip("Distancia a la que se considera que el compañero ya está en el barco.")]
    [SerializeField] private float distanciaDeArribada = 0.7f;

    [Header("Entradas de cada uno")]
    [Tooltip("Estado del Animator con el que saltan los compañeros.")]
    [SerializeField] private string estadoDelSalto = "JumpFull_InPlace_NoWeapon";
    [Tooltip("Altura del salto (m).")]
    [SerializeField] private float alturaDelSalto = 0.9f;
    [Tooltip("Segundos que dura el salto.")]
    [SerializeField] private float duracionDelSalto = 0.4f;

    /// <summary>Un compañero que sube al barco y todo lo que hay que devolverle al bajar.</summary>
    private sealed class Pasajero
    {
        public NPCPartyMember Npc;
        public NavMeshAgent Agente;
        public NPCSimpleAnimator Animador;
        public Transform Puesto;
        public Transform PadreOriginal;
        public bool Sentado;
        public bool AgenteEstabaActivo;
        public bool EstabaAnclado;
    }

    /// <summary>Orden de embarque: Estela primero para que conserve siempre babor.</summary>
    private static readonly Slot[] OrdenDeEmbarque = { Slot.Estela, Slot.Will, Slot.Liam };

    private BarcoNavegable _barco;
    private Coroutine _subida;
    private bool _subiendo;
    private readonly List<Pasajero> _pasajeros = new();

    /// <summary>Puesto de babor.</summary>
    public Transform PuestoDeBabor => puestoDeBabor;

    /// <summary>Puesto de estribor.</summary>
    public Transform PuestoDeEstribor => puestoDeEstribor;

    /// <summary>El punto del casco donde se suben.</summary>
    public Transform PuntoDeEmbarque => puntoDeEmbarque;

    /// <summary>True mientras la tripulación está subiendo: el barco espera a que suban.</summary>
    public bool Subiendo => _subiendo;

    void Awake()
    {
        _barco = GetComponent<BarcoNavegable>();
    }

    /// <summary>Asigna los puestos y el punto de embarque (lo llama el generador del mundo).</summary>
    public void Configurar(Transform babor, Transform estribor, Transform embarque)
    {
        puestoDeBabor = babor;
        puestoDeEstribor = estribor;
        puntoDeEmbarque = embarque;
    }

    /// <summary>Llama a los compañeros al barco: andan hasta él y suben a su puesto.</summary>
    public void SubirATripulacion()
    {
        if (_subida != null) return;

        PrepararPasajeros();
        if (_pasajeros.Count == 0) return;

        _subida = StartCoroutine(Co_Subir());
    }

    /// <summary>
    /// Baja a los compañeros del barco y los deja en <paramref name="destino"/>, el mismo sitio al
    /// que baja el jugador. Devuelve a cada uno su agente, su anclaje y su seguimiento.
    /// </summary>
    public void BajarATripulacion(Vector3 destino)
    {
        if (_subida != null)
        {
            StopCoroutine(_subida);
            _subida = null;
        }
        _subiendo = false;

        for (int i = 0; i < _pasajeros.Count; i++)
            Soltar(_pasajeros[i], destino, i);

        _pasajeros.Clear();
    }

    /// <summary>Rellena la lista de pasajeros con los compañeros presentes y les asigna puesto.</summary>
    void PrepararPasajeros()
    {
        _pasajeros.Clear();

        Transform[] puestos = { puestoDeBabor, puestoDeEstribor };
        int siguiente = 0;

        for (int i = 0; i < OrdenDeEmbarque.Length; i++)
        {
            Slot personaje = OrdenDeEmbarque[i];
            if (personaje == ControlDelJugador.Activo) continue;
            if (!ControlDelJugador.EnElGrupo(personaje)) continue;

            var npc = ControlDelJugador.NpcDe(personaje);
            if (npc == null || npc.EstaCaido) continue;
            if (siguiente >= puestos.Length || puestos[siguiente] == null) continue;

            var agente = npc.GetComponent<NavMeshAgent>();
            _pasajeros.Add(new Pasajero
            {
                Npc = npc,
                Agente = agente,
                Animador = npc.GetComponentInChildren<NPCSimpleAnimator>(),
                Puesto = puestos[siguiente],
                PadreOriginal = npc.transform.parent,
                Sentado = personaje == Slot.Estela,
                AgenteEstabaActivo = agente != null && agente.enabled,
                EstabaAnclado = npc.NPCManager?.Context?.IsPinnedByParty ?? false,
            });
            siguiente++;
        }
    }

    IEnumerator Co_Subir()
    {
        _subiendo = true;
        try
        {
            // De uno en uno: el barco espera a que cada uno esté en su puesto antes del siguiente.
            for (int i = 0; i < _pasajeros.Count; i++)
            {
                var pasajero = _pasajeros[i];
                if (pasajero.Npc == null) continue;
                yield return Co_SubirUno(pasajero);
            }
        }
        finally
        {
            _subiendo = false;
            _subida = null;
        }
    }

    IEnumerator Co_SubirUno(Pasajero pasajero)
    {
        var npc = pasajero.Npc;

        Vector3 embarque = puntoDeEmbarque != null ? puntoDeEmbarque.position : pasajero.Puesto.position;

        // Se ancla desde el primer momento: el grupo deja de reclamarlo mientras sube y, si
        // venía de combate, deja de estarlo: a bordo manda el barco, no AllyCombatState.
        if (npc.NPCManager?.Context != null)
        {
            npc.NPCManager.Context.IsPinnedByParty = true;
            npc.NPCManager.Context.IsInCombat = false;
        }

        // El estado de diálogo lo lleva andando hasta el casco. Para Will (no está en el party)
        // hace falta el permiso explícito de mantenerse, o volvería a Idle al primer frame.
        npc.MoveToDialoguePosition(embarque, esperaDeLlegada, null, mantenerFueraDelParty: true);

        float inicioEspera = Time.time;
        while (Time.time - inicioEspera < esperaDeLlegada)
        {
            if (npc == null) yield break;
            if (Vector3.Distance(npc.transform.position, embarque) <= distanciaDeArribada) break;
            yield return null;
        }

        if (npc == null) yield break;

        // A partir de aquí manda el barco: el estado de diálogo se ancla (deja de mirar al
        // jugador y de moverse) y el agente deja de escribir la posición (si siguiera
        // navegando, pelearía con el arco y se quedaría andando en el sitio todo el viaje).
        if (npc.NPCManager?.Brain?.CurrentState is DialoguePositionState estadoDeDialogo)
            estadoDeDialogo.Anclar();
        else
            npc.NPCManager?.ForceIdle();

        var agente = pasajero.Agente;
        if (agente != null)
        {
            if (agente.isOnNavMesh)
            {
                agente.isStopped = true;
                agente.ResetPath();
            }
            agente.velocity = Vector3.zero;
            agente.updatePosition = false;
            agente.updateRotation = false;
            agente.enabled = false;
        }

        var animador = pasajero.Animador;
        if (animador != null)
        {
            animador.DisableAutoRotation();
            animador.StopMovement();
            animador.ForzarEnElSuelo();

            // La pose del puesto se pone al acabar el salto, no al aterrizar: si se cruzara antes,
            // cortaría el clip a media parábola. PlayOneShot avisa al terminar su clip. La
            // sentada no espera: su actividad ambiental lleva su propio tramo de entrada.
            if (!string.IsNullOrEmpty(estadoDelSalto))
                animador.PlayOneShot(estadoDelSalto, 0, () =>
                {
                    if (!pasajero.Sentado) DejarDePieEnElPuesto(pasajero);
                });
        }

        // El salto al puesto: una parábola corta con el pico por encima del borde.
        Vector3 desde = npc.transform.position;
        Vector3 hasta = pasajero.Puesto.position;
        Quaternion giro = npc.transform.rotation;
        float duracion = Mathf.Max(0.01f, duracionDelSalto);

        for (float s = 0f; s < duracion; s += Time.deltaTime)
        {
            if (npc == null) yield break;
            float u = Mathf.Clamp01(s / duracion);
            Vector3 posicion = Vector3.Lerp(desde, hasta, u);
            posicion.y += alturaDelSalto * 4f * u * (1f - u);
            npc.transform.position = posicion;
            npc.transform.rotation = giro;
            if (animador != null) animador.ForzarEnElSuelo();
            yield return null;
        }

        if (npc == null) yield break;

        // Se cuelga de su puesto: el barco se lo lleva sin escribir su transform cada frame.
        npc.transform.SetParent(pasajero.Puesto, false);
        npc.transform.localPosition = Vector3.zero;
        npc.transform.rotation = RotacionHaciaLaProa();

        if (animador != null)
        {
            // Cualquier actividad ambiental anterior (sentarse en el muelle, comer...) dejaría
            // viva su corrutina y volvería a pisar la pose del puesto al terminar su Begin.
            animador.DetenerActividadAmbiental();

            // El arco manual deja desfasado el chequeo de suelo; sin esto, el «Any State → Falling»
            // del Animator interrumpe la pose y parece que el compañero se mueve o convulsiona.
            animador.ForzarEnElSuelo();
            animador.QuitarRootMotion();

            // Con salto, la pose de pie la pone su one-shot al acabar (DejarDePieEnElPuesto); la
            // sentada se pone ya, como siempre.
            if (pasajero.Sentado) animador.PlayAmbientActivity(NPCAmbientActivity.SitLow);
            else if (string.IsNullOrEmpty(estadoDelSalto)) DejarDePieEnElPuesto(pasajero);
        }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        // Diagnóstico temporal de INC-808: deja en el log en qué estado acaba el puesto. Se quita
        // cuando la incidencia esté validada en Play.
        StartCoroutine(Co_RegistrarPoseDelPuesto(pasajero));
#endif
    }

#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
    /// <summary>Diagnóstico temporal de INC-808: estado del Animator un segundo y medio después de subir.</summary>
    IEnumerator Co_RegistrarPoseDelPuesto(Pasajero pasajero)
    {
        yield return new WaitForSeconds(1.5f);

        var npc = pasajero?.Npc;
        var animator = npc != null ? npc.GetComponent<Animator>() : null;
        if (animator == null) yield break;

        var actual = animator.GetCurrentAnimatorStateInfo(0);
        var siguiente = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : actual;
        Debug.Log($"[Barco] {npc.name} en su puesto ({pasajero.Puesto?.name}): " +
                  $"base={NombreDeEstado(actual)} -> {NombreDeEstado(siguiente)}, " +
                  $"InputMagnitude={animator.GetFloat(Animator.StringToHash("InputMagnitude")):F2}, " +
                  $"capa1={animator.GetLayerWeight(1):F2}, enTransicion={animator.IsInTransition(0)}");
    }

    static string NombreDeEstado(AnimatorStateInfo estado)
    {
        if (estado.IsName("Free Locomotion")) return "Free Locomotion";
        if (estado.IsName("JumpFull_InPlace_NoWeapon")) return "JumpFull_InPlace_NoWeapon";
        if (estado.IsName("SitLow_Begin")) return "SitLow_Begin";
        if (estado.IsName("SitLow_Loop")) return "SitLow_Loop";
        return $"hash {estado.shortNameHash}";
    }
#endif

    /// <summary>Deja de pie al pasajero en su puesto, con el idle normal.</summary>
    void DejarDePieEnElPuesto(Pasajero pasajero)
    {
        var animador = pasajero?.Animador;
        if (animador == null) return;

        // Si el viaje ha terminado antes de que acabe el salto, ya no está colgado de su puesto:
        // la pose la decide Soltar (vuelve a seguir al jugador).
        if (pasajero.Npc == null || pasajero.Npc.transform.parent != pasajero.Puesto) return;

        animador.ForzarEnElSuelo();
        animador.QuitarRootMotion();
        animador.PlayIdleNormal();
    }

    /// <summary>Deja a un pasajero en tierra y le devuelve todo lo que se le quitó al subir.</summary>
    void Soltar(Pasajero pasajero, Vector3 destino, int indice)
    {
        var npc = pasajero.Npc;
        if (npc == null) return;

        // La postura a bordo se apaga antes de devolverlo al grupo: si no, se queda sentado.
        if (pasajero.Sentado && pasajero.Animador != null)
            pasajero.Animador.StopAmbientActivity(NPCAmbientActivity.SitLow);

        npc.transform.SetParent(pasajero.PadreOriginal, true);

        // Alrededor del punto de bajada, sobre el NavMesh más cercano (el muelle puede ser estrecho).
        Vector3 salida = destino + Quaternion.Euler(0f, 120f * indice, 0f) * Vector3.forward * 0.7f;
        if (NavMesh.SamplePosition(salida, out var navHit, 2f, NavMesh.AllAreas))
            salida = navHit.position;
        npc.transform.position = salida;

        var agente = pasajero.Agente;
        if (agente != null)
        {
            agente.enabled = pasajero.AgenteEstabaActivo;
            if (agente.enabled)
            {
                agente.updatePosition = true;
                agente.updateRotation = true;
                agente.velocity = Vector3.zero;
                if (agente.isOnNavMesh)
                {
                    agente.Warp(salida);
                    agente.isStopped = false;
                    agente.ResetPath();
                }
            }
        }

        if (pasajero.Animador != null)
        {
            pasajero.Animador.ForzarEnElSuelo();
            pasajero.Animador.PlayIdleNormal();
            pasajero.Animador.EnableAutoRotation();
        }

        if (npc.NPCManager?.Context != null)
            npc.NPCManager.Context.IsPinnedByParty = pasajero.EstabaAnclado;

        bool siguiendo = PartyControlManager.Instance == null
                         || PartyControlManager.Instance.IsPartyFollowing;

        if (!siguiendo || pasajero.EstabaAnclado)
        {
            npc.NPCManager?.ForceIdle();
            return;
        }

        if (npc.IsInParty) npc.StartFollowing();
        else npc.StartFollowingIgnorePartyCheck();
    }

    /// <summary>Rotación de pie mirando a la proa (la proa del modelo no tiene por qué ser +Z).</summary>
    Quaternion RotacionHaciaLaProa()
    {
        Vector3 proa = _barco != null ? _barco.DireccionDeProa : transform.forward;
        proa.y = 0f;
        if (proa.sqrMagnitude < 0.0001f) proa = transform.forward;
        return Quaternion.LookRotation(proa.normalized, Vector3.up);
    }
}
