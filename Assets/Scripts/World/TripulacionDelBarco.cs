using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using UnityEngine.Serialization;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// La tripulación que sube al barco con el jugador: todos los personajes del grupo que no van al
/// timón, cada uno en uno de los dos puestos de cubierta.
///
/// Da igual quién conduzca: quien lleva el mando se queda en el puesto de conducción y los otros
/// dos se reparten babor y estribor en el orden de <see cref="tripulacion"/>, cada uno con su pose
/// (por defecto Estela se sienta y Will y Liam se quedan de pie, apoyados).
///
/// Para llevarlos hasta el casco se usa el sistema oficial
/// <see cref="NPCPartyMember.MoveToDialoguePosition"/>; al llegar, <see cref="PasajeroNpc"/> (lo mismo
/// que usan las atracciones montables) les toma el control, los ancla con la clave del barco y los
/// cuelga de su puesto —hijo del barco, así que se mueven con él— mirando hacia la proa.
/// </summary>
[DisallowMultipleComponent]
public class TripulacionDelBarco : MonoBehaviour
{
    [Header("Puestos a bordo")]
    [Tooltip("Puesto de babor: lo ocupa el primer compañero que sube (el primero de la tripulación que esté en el grupo).")]
    [SerializeField, FormerlySerializedAs("puestoDeEstela")] private Transform puestoDeBabor;
    [Tooltip("Puesto de estribor: lo ocupa el segundo compañero que sube.")]
    [SerializeField, FormerlySerializedAs("puestoDeLiam")] private Transform puestoDeEstribor;

    [Header("Tripulación")]
    [Tooltip("Orden de embarque: los que no van al timón ocupan babor y estribor en este orden, " +
             "cada uno con su pose en el puesto.")]
    [SerializeField] private List<Tripulante> tripulacion = new List<Tripulante>
    {
        new Tripulante { personaje = Slot.Estela, pose = NPCAmbientActivity.SitLow },
        new Tripulante { personaje = Slot.Will, pose = NPCAmbientActivity.None },
        new Tripulante { personaje = Slot.Liam, pose = NPCAmbientActivity.None },
    };

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

    /// <summary>Un personaje de la tripulación y la pose que adopta en su puesto.</summary>
    [Serializable]
    private sealed class Tripulante
    {
        [Tooltip("Personaje del grupo.")]
        public Slot personaje = Slot.Will;
        [Tooltip("Pose en su puesto a bordo. None = de pie, apoyado (idle normal).")]
        public NPCAmbientActivity pose = NPCAmbientActivity.None;
    }

    /// <summary>Un compañero que sube al barco y todo lo que hay que devolverle al bajar.</summary>
    private sealed class Pasajero
    {
        public PasajeroNpc Llevado;
        public Transform Puesto;
        public NPCAmbientActivity Pose;

        public NPCPartyMember Npc => Llevado.Npc;
        public NPCSimpleAnimator Animador => Llevado.Animador;
        public bool DePie => Pose == NPCAmbientActivity.None;
    }

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
    /// que baja el jugador. Devuelve a cada uno su agente y su seguimiento, y suelta el ancla del barco.
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
            _pasajeros[i].Llevado.Soltar(destino, i);

        _pasajeros.Clear();
    }

    /// <summary>Rellena la lista de pasajeros con los compañeros presentes y les asigna puesto.</summary>
    void PrepararPasajeros()
    {
        _pasajeros.Clear();

        Transform[] puestos = { puestoDeBabor, puestoDeEstribor };
        int siguiente = 0;

        for (int i = 0; i < tripulacion.Count; i++)
        {
            if (tripulacion[i] == null) continue;
            Slot personaje = tripulacion[i].personaje;
            if (personaje == ControlDelJugador.Activo) continue;
            if (!ControlDelJugador.EnElGrupo(personaje)) continue;

            var npc = ControlDelJugador.NpcDe(personaje);
            if (npc == null || npc.EstaCaido) continue;
            if (siguiente >= puestos.Length || puestos[siguiente] == null) continue;

            _pasajeros.Add(new Pasajero
            {
                Llevado = new PasajeroNpc(npc, this),
                Puesto = puestos[siguiente],
                Pose = tripulacion[i].pose,
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
        pasajero.Llevado.Anclar();

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

        // A partir de aquí manda el barco: el compañero deja de moverse por su cuenta.
        pasajero.Llevado.TomarControl();

        var animador = pasajero.Animador;
        if (animador != null)
        {
            // La pose del puesto se pone al acabar el salto, no al aterrizar: si se cruzara antes,
            // cortaría el clip a media parábola. PlayOneShot avisa al terminar su clip. La
            // sentada no espera: su actividad ambiental lleva su propio tramo de entrada.
            if (!string.IsNullOrEmpty(estadoDelSalto))
                animador.PlayOneShot(estadoDelSalto, 0, () =>
                {
                    if (pasajero.DePie) DejarDePieEnElPuesto(pasajero);
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
        // Con salto, la pose de pie la pone su one-shot al acabar (DejarDePieEnElPuesto); una
        // pose de actividad (sentarse) se pone ya: lleva su propio tramo de entrada.
        pasajero.Llevado.Colgar(pasajero.Puesto, RotacionHaciaLaProa(), pasajero.Pose, ponerPoseYa: false);
        if (animador != null)
        {
            if (!pasajero.DePie) pasajero.Llevado.PonerPose();
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

    /// <summary>Rotación de pie mirando a la proa (la proa del modelo no tiene por qué ser +Z).</summary>
    Quaternion RotacionHaciaLaProa()
    {
        Vector3 proa = _barco != null ? _barco.DireccionDeProa : transform.forward;
        proa.y = 0f;
        if (proa.sqrMagnitude < 0.0001f) proa = transform.forward;
        return Quaternion.LookRotation(proa.normalized, Vector3.up);
    }
}
