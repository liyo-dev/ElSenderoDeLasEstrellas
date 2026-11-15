using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC
{
    /// <summary>
    /// Las órdenes del modo Libre sobre un compañero del grupo: «Sígueme» y «Deja de seguir».
    /// Enseña el cartel sobre su cabeza y, al pulsar A, se lo pide a <see cref="PartyControlManager"/>,
    /// que es el dueño de la pareja (líder y seguidor) y del cambio de personaje con ella.
    ///
    /// La orden solo existe con el grupo en Libre, fuera de combate y cinemáticas y con el compañero
    /// de frente al jugador: el seguidor va detrás, así que no le quita la A al salto mientras
    /// sigue; para despedirlo hay que darse la vuelta y mirarle. Entre personajes del grupo no hay
    /// ninguna otra interacción (INC-754). <see cref="Interactable"/> le pregunta a
    /// <see cref="CanShowFollowPrompt"/> si el compañero se puede enfocar. Ver INC-1200.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompanionFollowPrompt : MonoBehaviour
    {
        [Tooltip("Ángulo máximo, desde donde mira el jugador, al que tiene que estar el compañero para darle una orden.")]
        [SerializeField, Range(10f, 180f)] private float anguloDeFrente = 60f;
        [Tooltip("Intervalo de comprobación del cartel (segundos).")]
        [SerializeField] private float checkInterval = 0.15f;

        private NPCPartyMember _partyMember;
        private NPCBehaviourManagerV2 _npcManager;
        private NPCAlertIconController _iconController;
        private Interactable _interactable;
        private GameObject _followIconPrefab;
        private GameObject _stopFollowIconPrefab;
        private bool _listenerAdded;
        private float _cosDeFrente;

        private enum PromptMode { None, Follow, StopFollow }
        private PromptMode _currentMode;
        private float _nextCheck;

        public bool IsShowingPrompt => _currentMode != PromptMode.None;

        /// <summary>
        /// Hay una orden que darle ahora a este compañero. No depende de que su icono esté visible:
        /// lo pregunta <see cref="Interactable.CanInteract"/> para decidir si se puede enfocar.
        /// </summary>
        public bool CanShowFollowPrompt() => OrdenDisponible() != PromptMode.None;

        public void SetFollowIcon(GameObject iconPrefab)     => _followIconPrefab = iconPrefab;
        public void SetStopFollowIcon(GameObject iconPrefab) => _stopFollowIconPrefab = iconPrefab;

        private void Awake()
        {
            _npcManager   = GetComponent<NPCBehaviourManagerV2>();
            _interactable = GetComponent<Interactable>() ?? GetComponentInParent<Interactable>();
            _cosDeFrente  = Mathf.Cos(anguloDeFrente * Mathf.Deg2Rad);

            const string childName = "_FollowPromptIcon";
            var child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }
            _iconController = child.GetComponent<NPCAlertIconController>()
                           ?? child.gameObject.AddComponent<NPCAlertIconController>();
        }

        private void Start()
        {
            _partyMember = GetComponent<NPCPartyMember>();
            if (_partyMember == null)
            {
                enabled = false;
                return;
            }

            PartyControlManager.OnFollowModeChanged      += AlCambiarElGrupo;
            PartyControlManager.OnActiveCharacterChanged += AlCambiarElGrupo;
            PartyControlManager.OnParejaChanged          += AlCambiarLaPareja;

            if (_interactable != null)
            {
                _interactable.OnInteract.AddListener(OnPlayerInteracted);
                _listenerAdded = true;
            }
        }

        private void OnDisable() => HidePrompt();

        private void OnDestroy()
        {
            PartyControlManager.OnFollowModeChanged      -= AlCambiarElGrupo;
            PartyControlManager.OnActiveCharacterChanged -= AlCambiarElGrupo;
            PartyControlManager.OnParejaChanged          -= AlCambiarLaPareja;
            if (_listenerAdded && _interactable != null)
                _interactable.OnInteract.RemoveListener(OnPlayerInteracted);
        }

        // Al cambiar el grupo se reevalúa en el mismo frame, sin esperar al siguiente intervalo.
        private void AlCambiarElGrupo(bool _) => _nextCheck = 0f;
        private void AlCambiarElGrupo(int _) => _nextCheck = 0f;
        private void AlCambiarLaPareja() => _nextCheck = 0f;

        private void OnPlayerInteracted(GameObject _)
        {
            var pcm = PartyControlManager.Instance;
            if (pcm != null && ControlDelJugador.PersonajeDelNpc(_partyMember, out var personaje))
            {
                // Se decide con el estado de ahora, no con el cartel que había: el cartel puede
                // llevar hasta un intervalo de retraso.
                if (pcm.PuedeDejarDeSeguir(personaje)) pcm.DejarDeSeguir();
                else if (pcm.PuedePedirQueSiga(personaje)) pcm.PedirQueSiga(personaje);
            }
            HidePrompt();
            _nextCheck = 0f;
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + checkInterval;

            PromptMode desired = OrdenDisponible();

            // Solo con el compañero enfocado (con la A visible): así nunca hay dos carteles ni un
            // cartel sin botón.
            if (desired != PromptMode.None && (_interactable == null || !_interactable.IsHintVisible))
                desired = PromptMode.None;

            if (desired != _currentMode)
                ApplyMode(desired);
        }

        private PromptMode OrdenDisponible()
        {
            var pcm = PartyControlManager.Instance;
            if (pcm == null || pcm.IsPartyFollowing || _partyMember == null) return PromptMode.None;
            if (!ControlDelJugador.PersonajeDelNpc(_partyMember, out var personaje)) return PromptMode.None;

            // Los secuenciadores sientan a los NPCs sin pasar por la FSM (Context.IsInCinematic
            // no llega a ponerse): se mira también CinematicSequencerBase. Ver INC-059.
            if (CinematicSequencerBase.AnySequenceActive) return PromptMode.None;
            var contexto = _npcManager != null ? _npcManager.Context : null;
            if (contexto != null && (contexto.IsInCinematic || contexto.IsInCombat)) return PromptMode.None;

            if (!DeFrenteAlJugador()) return PromptMode.None;

            if (pcm.PuedeDejarDeSeguir(personaje)) return PromptMode.StopFollow;
            if (pcm.PuedePedirQueSiga(personaje)) return PromptMode.Follow;
            return PromptMode.None;
        }

        private bool DeFrenteAlJugador()
        {
            var jugador = ControlDelJugador.CuerpoDelJugador;
            if (jugador == null) return false;
            Vector3 hacia = transform.position - jugador.transform.position;
            hacia.y = 0f;
            if (hacia.sqrMagnitude < 0.0001f) return true;
            Vector3 frente = jugador.transform.forward;
            frente.y = 0f;
            return Vector3.Dot(frente.normalized, hacia.normalized) >= _cosDeFrente;
        }

        private void ApplyMode(PromptMode mode)
        {
            _currentMode = mode;
            _iconController?.HideAlertIcon();

            if (mode == PromptMode.Follow && _followIconPrefab != null)
                _iconController?.ShowPersistentIcon(_followIconPrefab);
            else if (mode == PromptMode.StopFollow && _stopFollowIconPrefab != null)
                _iconController?.ShowPersistentIcon(_stopFollowIconPrefab);
        }

        private void HidePrompt()
        {
            if (_currentMode == PromptMode.None) return;
            _currentMode = PromptMode.None;
            _iconController?.HideAlertIcon();
        }
    }
}
