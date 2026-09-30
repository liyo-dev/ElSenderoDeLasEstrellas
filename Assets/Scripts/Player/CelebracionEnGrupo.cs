using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Slot = PartyControlManager.CharacterSlot;

/// Pose de victoria de un personaje del grupo: el estado del Animator que hace al celebrar.
[Serializable]
public struct PoseDeVictoria
{
    public Slot personaje;
    [Tooltip("Estado del Animator (el de los NPCs y el del jugador comparten nombres).")]
    public string estado;

    public PoseDeVictoria(Slot personaje, string estado)
    {
        this.personaje = personaje;
        this.estado = estado;
    }
}

/// Cómo se hace la foto de victoria del grupo. Vive en PlayerBattleModeController.
[Serializable]
public sealed class AjustesCelebracionEnGrupo
{
    [Tooltip("Orden de izquierda a derecha en pantalla. Con los tres, el del medio queda en el centro; con dos, uno a cada lado.")]
    public Slot[] orden = { Slot.Will, Slot.Estela, Slot.Liam };

    [Tooltip("La pose de victoria de cada uno, que hace al llegar a su sitio.")]
    public PoseDeVictoria[] poses =
    {
        new PoseDeVictoria(Slot.Will, "Victory_NoWeapon"),
        new PoseDeVictoria(Slot.Estela, "HandClap01"),
        new PoseDeVictoria(Slot.Liam, "Cheer02"),
    };

    [Tooltip("Gesto que hacen todos a la vez al final (el puño arriba). Vacío = sin gesto final.")]
    public string gestoFinal = "Cheer01";

    [Tooltip("Metros entre un personaje y el siguiente en la fila.")]
    public float separacion = 1.2f;

    [Tooltip("Solo entran en la foto los compañeros a menos de esta distancia del jugador.")]
    public float radioDeBusqueda = 25f;

    [Tooltip("Más lejos que esto, el compañero aparece en su sitio en vez de ir corriendo.")]
    public float distanciaParaColocarDeGolpe = 15f;

    [Tooltip("Segundos máximos para llegar a su sitio.")]
    public float topeParaLlegar = 3.5f;

    [Tooltip("Segundos máximos de la pose de cada uno.")]
    public float topeDePose = 3f;
}

/// La foto de victoria del grupo: los compañeros que están cerca se ponen en fila junto al jugador
/// (en el orden de los ajustes, así que con los tres Estela queda en el medio), mirando hacia donde
/// mira él, cada uno hace su pose al llegar y al final todos el mismo gesto a la vez.
///
/// Los compañeros se mueven con el sistema de secuencias (SequenceActor + SequenceMovement): se
/// retienen al empezar y se sueltan en Soltar(). El jugador no se mueve de su sitio; lo que hace él
/// lo lleva PlayerBattleModeController. Ver INC-542.
public sealed class CelebracionEnGrupo
{
    private sealed class Integrante
    {
        public Slot slot;
        public SequenceActor actor;   // null en el jugador
        public Vector3 destino;
        public bool listo;
        public Coroutine rutina;
    }

    private readonly AjustesCelebracionEnGrupo _ajustes;
    private readonly List<Integrante> _integrantes = new List<Integrante>(3);
    private readonly List<GrupoCercano.Miembro> _cercanos = new List<GrupoCercano.Miembro>(3);
    private MonoBehaviour _host;
    private Vector3 _frente;

    public CelebracionEnGrupo(AjustesCelebracionEnGrupo ajustes)
    {
        _ajustes = ajustes ?? new AjustesCelebracionEnGrupo();
    }

    /// Compañeros que salen en la foto (sin contar al jugador).
    public int Companeros { get; private set; }

    public bool TodosListos
    {
        get
        {
            for (int i = 0; i < _integrantes.Count; i++)
                if (_integrantes[i].actor != null && !_integrantes[i].listo) return false;
            return true;
        }
    }

    public string GestoFinal => _ajustes.gestoFinal;

    /// Quién sale en la foto: primero el personaje al mando, después los compañeros por su orden.
    public void PersonajesEnLaFoto(List<Slot> destino)
    {
        destino.Clear();
        for (int i = 0; i < _integrantes.Count; i++)
            if (_integrantes[i].actor == null) destino.Add(_integrantes[i].slot);
        for (int i = 0; i < _integrantes.Count; i++)
            if (_integrantes[i].actor != null) destino.Add(_integrantes[i].slot);
    }

    public string PoseDe(Slot slot)
    {
        if (_ajustes.poses == null) return null;
        for (int i = 0; i < _ajustes.poses.Length; i++)
            if (_ajustes.poses[i].personaje == slot) return _ajustes.poses[i].estado;
        return null;
    }

    /// Busca a los compañeros, los retiene y calcula dónde va cada uno. El jugador ya tiene que
    /// estar girado hacia la cámara: la fila se forma a lo ancho de hacia donde mira. Devuelve el
    /// centro de la foto (el punto al que debe mirar la cámara).
    public Vector3 Preparar(Transform jugador, Slot slotJugador)
    {
        Soltar();
        if (jugador == null) return Vector3.zero;

        _frente = jugador.forward;
        _frente.y = 0f;
        if (_frente.sqrMagnitude < 0.0001f) _frente = Vector3.forward;
        _frente.Normalize();

        _integrantes.Add(new Integrante { slot = slotJugador, listo = true });

        GrupoCercano.Buscar(jugador.position, _ajustes.radioDeBusqueda, _cercanos);
        for (int i = 0; i < _cercanos.Count; i++)
        {
            var m = _cercanos[i];
            if (m.jugador != null || m.cuerpo == null) continue;

            var miembro = m.cuerpo.GetComponent<NPCPartyMember>();
            var manager = miembro != null ? miembro.NPCManager : m.cuerpo.GetComponent<NPCBehaviourManagerV2>();
            // Si otro sistema ya lo lleva (una cinemática) o se ha quedado anclado en modo Libre,
            // no sale en la foto.
            if (manager == null || manager.Context == null) continue;
            if (manager.Context.IsInCinematic || manager.Context.IsPinnedByParty) continue;
            if (!SequenceActor.TryResolve(manager.PersistenceId, out var actor)) continue;

            actor.Hold();
            actor.NpcAnimator?.CancelarCelebracionDeVictoria();
            _integrantes.Add(new Integrante { slot = m.slot, actor = actor });
        }
        Companeros = _integrantes.Count - 1;

        _integrantes.Sort((a, b) => Posicion(a.slot).CompareTo(Posicion(b.slot)));
        return CalcularSitios(jugador.position);
    }

    /// Echa a andar a cada compañero hacia su sitio; al llegar hace su pose.
    public void Empezar(MonoBehaviour host)
    {
        _host = host;
        if (_host == null) return;
        for (int i = 0; i < _integrantes.Count; i++)
        {
            var m = _integrantes[i];
            if (m.actor != null) m.rutina = _host.StartCoroutine(Co_Colocarse(m));
        }
    }

    /// Todos los compañeros hacen el gesto final a la vez (el jugador lo hace por su cuenta).
    public void HacerGestoFinal()
    {
        if (string.IsNullOrEmpty(_ajustes.gestoFinal)) return;
        for (int i = 0; i < _integrantes.Count; i++)
            _integrantes[i].actor?.NpcAnimator?.PlaySocialGesture(_ajustes.gestoFinal);
    }

    /// Devuelve a los compañeros a su comportamiento normal (seguir al jugador). Idempotente.
    public void Soltar()
    {
        for (int i = 0; i < _integrantes.Count; i++)
        {
            var m = _integrantes[i];
            if (m.rutina != null && _host != null) _host.StopCoroutine(m.rutina);
            m.rutina = null;
            if (m.actor == null) continue;
            m.actor.EndAgentOverride();
            if (!m.actor.LoHaTomadoOtroSistema) m.actor.ReturnToNormalPose();
            m.actor.Release();
        }
        _integrantes.Clear();
        Companeros = 0;
    }

    private IEnumerator Co_Colocarse(Integrante m)
    {
        var actor = m.actor;
        actor.NpcAnimator?.SetBattleMode(false);
        Vector3 d = m.destino - actor.Transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > _ajustes.distanciaParaColocarDeGolpe * _ajustes.distanciaParaColocarDeGolpe)
            SequenceMovement.PlaceAt(actor, m.destino, Quaternion.LookRotation(_frente, Vector3.up));
        else
            yield return SequenceMovement.MoveTo(actor, m.destino, 0f, _ajustes.topeParaLlegar);

        actor.Face(actor.Transform.position + _frente);

        string pose = PoseDe(m.slot);
        var anim = actor.NpcAnimator;
        if (anim != null && !string.IsNullOrEmpty(pose))
        {
            bool acabada = false;
            anim.PlaySocialGesture(pose, () => acabada = true);
            float tope = Time.time + _ajustes.topeDePose;
            while (!acabada && Time.time < tope) yield return null;
        }

        m.listo = true;
        m.rutina = null;
    }

    private int Posicion(Slot slot)
    {
        if (_ajustes.orden != null)
            for (int i = 0; i < _ajustes.orden.Length; i++)
                if (_ajustes.orden[i] == slot) return i;
        return int.MaxValue;
    }

    /// La fila va a lo ancho de la pantalla (el jugador mira a cámara, así que la derecha de la
    /// pantalla es su izquierda) y el jugador se queda donde está. Si por ese lado hay una pared o
    /// no hay suelo, la fila se forma hacia el otro: cambia quién queda a cada lado de la pantalla,
    /// pero el del medio sigue en el medio.
    private Vector3 CalcularSitios(Vector3 posJugador)
    {
        int iJugador = 0;
        for (int i = 0; i < _integrantes.Count; i++)
            if (_integrantes[i].actor == null) { iJugador = i; break; }

        Vector3 derechaDePantalla = -Vector3.Cross(Vector3.up, _frente).normalized;
        if (HayObstaculo(posJugador, derechaDePantalla, iJugador) && !HayObstaculo(posJugador, -derechaDePantalla, iJugador))
            derechaDePantalla = -derechaDePantalla;

        Vector3 centro = Vector3.zero;
        for (int i = 0; i < _integrantes.Count; i++)
        {
            _integrantes[i].destino = posJugador + derechaDePantalla * ((i - iJugador) * _ajustes.separacion);
            centro += _integrantes[i].destino;
        }
        return centro / _integrantes.Count;
    }

    private bool HayObstaculo(Vector3 posJugador, Vector3 direccion, int iJugador)
    {
        if (!NavMesh.SamplePosition(posJugador, out var origen, 1.5f, NavMesh.AllAreas)) return false;
        for (int i = 0; i < _integrantes.Count; i++)
        {
            if (i == iJugador) continue;
            Vector3 destino = posJugador + direccion * ((i - iJugador) * _ajustes.separacion);
            if (NavMesh.Raycast(origen.position, destino, out _, NavMesh.AllAreas)) return true;
        }
        return false;
    }
}
