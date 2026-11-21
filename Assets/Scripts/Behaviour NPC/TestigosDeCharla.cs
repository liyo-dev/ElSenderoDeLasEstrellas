using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// Que nadie se quede plantado sin hacer nada junto a una charla en la que no toma parte.
    /// Cuando se abre una conversación con un personaje (caja de diálogo, con o sin cámara de
    /// diálogo), mira qué vecinos andan cerca y les pone a ceder el sitio
    /// (<see cref="CederSitioALaCharlaState"/>): quien estorba en el escenario de la charla se
    /// aparta fuera del plano por el NavMesh; los de alrededor curiosean, dan la espalda o siguen a lo
    /// suyo, según las reglas de <see cref="ReaccionesAnteCharlas"/> (tipo de vecino y personalidad).
    /// Mientras dura la charla vuelve a mirar cada poco, por si alguien entra en el escenario. Al
    /// cerrarse, cada uno retoma lo suyo.
    ///
    /// No toca a quien toma parte en la charla (el personaje principal, el jugador, quien habla en
    /// alguna línea, el grupo), ni a quien tiene una cinemática en su reparto, ni a quien está en
    /// combate, siguiendo al jugador, anclado o sentado en su sitio. Dentro de una cinemática no
    /// actúa: la cinemática decide su escena. Se crea solo al arrancar el juego.
    /// </summary>
    public sealed class TestigosDeCharla : MonoBehaviour
    {
        public static TestigosDeCharla Instance { get; private set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            s_camino = null;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("[TestigosDeCharla]");
            DontDestroyOnLoad(go);
            go.AddComponent<TestigosDeCharla>();
        }

        private static readonly float[] s_angulos = { 0f, 40f, -40f, 80f, -80f, 120f, -120f, 180f };
        private static NavMeshPath s_camino;
        private static readonly Vector3[] s_esquinas = new Vector3[32];

        private ReaccionesAnteCharlas _ajustes;
        private bool _enCharla;
        private Transform _principal;
        private Transform _jugador;
        private Transform _camara;
        private float _temporizador;

        private readonly HashSet<string> _idsQueHablan = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<Transform> _escenario = new List<Transform>(8);
        private readonly List<NPCBehaviourManagerV2> _cerca = new List<NPCBehaviourManagerV2>(32);
        private readonly Dictionary<NPCBehaviourManagerV2, CederSitioALaCharlaState> _cediendo =
            new Dictionary<NPCBehaviourManagerV2, CederSitioALaCharlaState>();
        private readonly HashSet<NPCBehaviourManagerV2> _yaReaccionaron = new HashSet<NPCBehaviourManagerV2>();
        private readonly List<CederSitioALaCharlaState> _avisar = new List<CederSitioALaCharlaState>(16);
        private readonly List<NPCBehaviourManagerV2> _avisarNpc = new List<NPCBehaviourManagerV2>(16);

        /// Punto medio de quienes toman parte en la charla (a ras de suelo del primero).
        public Vector3 Centro { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _ajustes = ReaccionesAnteCharlas.Cargadas;
            // Se escucha desde Awake: el componente solo se enciende (Update) mientras hay charla.
            DialogueManager.OnDialogueStarted += AlEmpezarCharla;
            DialogueManager.OnDialogueClosed += AlAcabarCharla;
            enabled = false;
        }

        void OnDestroy()
        {
            DialogueManager.OnDialogueStarted -= AlEmpezarCharla;
            DialogueManager.OnDialogueClosed -= AlAcabarCharla;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var dialogos = DialogueManager.Instance;
            if (dialogos == null || !dialogos.IsOpen)
            {
                AlAcabarCharla(null);
                return;
            }

            _temporizador -= Time.deltaTime;
            if (_temporizador > 0f) return;
            _temporizador = _ajustes.comprobarCada;
            Revisar();
        }

        // ── Charla ──────────────────────────────────────────────────────────────

        private void AlEmpezarCharla(Transform principal)
        {
            // Solo las charlas con un personaje: una carta o un cartel no tienen escena que despejar.
            if (principal == null || principal.GetComponentInParent<NPCSimpleAnimator>() == null) return;

            var dialogos = DialogueManager.Instance;
            if (!_ajustes.tambienDentroDeCinematicas
                && (Kits.Core.GameContext.Has(Situaciones.Cinematica)
                    || (dialogos != null && dialogos.ConservarCamaraDeSecuencia)))
                return;

            _enCharla = true;
            _principal = principal;
            var jugador = PlayerService.Player;
            _jugador = jugador != null ? jugador.transform : null;
            var camara = Camera.main;
            _camara = camara != null ? camara.transform : null;

            _idsQueHablan.Clear();
            var dialogo = dialogos != null ? dialogos.DialogoAbierto : null;
            if (dialogo != null && dialogo.lines != null)
            {
                for (int i = 0; i < dialogo.lines.Length; i++)
                {
                    var linea = dialogo.lines[i];
                    if (!string.IsNullOrEmpty(linea.speakerNameId)) _idsQueHablan.Add(linea.speakerNameId);
                    if (!string.IsNullOrEmpty(linea.lookAtOverrideId)) _idsQueHablan.Add(linea.lookAtOverrideId);
                }
            }

            _escenario.Clear();
            _escenario.Add(_principal);
            if (_jugador != null) _escenario.Add(_jugador);
            _yaReaccionaron.Clear();

            // Quien seguía cediendo el sitio de una charla anterior se queda esperando; si ahora
            // toma parte en esta, deja de ceder ya.
            CopiarCediendo();
            for (int i = 0; i < _avisar.Count; i++)
            {
                if (EsParticipante(_avisarNpc[i])) _avisar[i].AcabarYa();
                else _avisar[i].SeguirEnLaCharla();
            }

            _temporizador = _ajustes.comprobarCada;
            enabled = true;
            Revisar();
        }

        private void AlAcabarCharla(Transform _)
        {
            if (!_enCharla) return;
            _enCharla = false;
            enabled = false;
            _yaReaccionaron.Clear();

            CopiarCediendo();
            for (int i = 0; i < _avisar.Count; i++)
                _avisar[i].AcabarLaCharla(ReglaDeReaccion.Entre(_ajustes.esperaAlAcabar));
        }

        /// Lo llama el estado al salir: ese vecino ya no está cediendo el sitio.
        public void Olvidar(NPCBehaviourManagerV2 npc, CederSitioALaCharlaState estado)
        {
            if (npc != null && _cediendo.TryGetValue(npc, out var actual) && actual == estado)
                _cediendo.Remove(npc);
        }

        private void CopiarCediendo()
        {
            _avisar.Clear();
            _avisarNpc.Clear();
            foreach (var par in _cediendo)
            {
                if (par.Key == null || par.Value == null) continue;
                _avisarNpc.Add(par.Key);
                _avisar.Add(par.Value);
            }
        }

        // ── Quién anda cerca ────────────────────────────────────────────────────

        private void Revisar()
        {
            if (!_enCharla) return;
            CalcularCentro();

            float radio = _ajustes.radioDeInteres + RadioDelEscenarioDesdeElCentro();
            NPCAmbientRegistry.GetActiveNPCsInRadius(Centro, radio, _cerca);

            for (int i = 0; i < _cerca.Count; i++)
            {
                var npc = _cerca[i];
                if (npc == null || !npc.isActiveAndEnabled) continue;

                if (EsParticipante(npc))
                {
                    // Quien habla en la charla también es escenario: no se le pone delante nadie.
                    if (!_escenario.Contains(npc.transform)) _escenario.Add(npc.transform);
                    continue;
                }

                bool estorba = DistanciaAlEscenario(npc.transform.position) < _ajustes.radioDelEscenario;

                if (_cediendo.TryGetValue(npc, out var cediendo))
                {
                    if (estorba) cediendo.Apartarse();
                    continue;
                }

                if (!estorba && _yaReaccionaron.Contains(npc)) continue;
                if (!PuedeCeder(npc, estorba)) continue;

                var tipo = TipoDe(npc);
                var personalidad = npc.Configuration?.socialConfig != null
                    ? npc.Configuration.socialConfig.personality
                    : NPCPersonality.Default;
                var regla = _ajustes.ReglaPara(tipo, personalidad);
                var reaccion = estorba ? regla.siEstorba : regla.SortearCerca();
                if (!estorba) _yaReaccionaron.Add(npc);
                if (reaccion == ReaccionAnteCharla.SeguirConLoSuyo) continue;

                var estado = new CederSitioALaCharlaState(this, npc, regla, reaccion, _ajustes,
                    _ajustes.volverASuSitio && tipo == TipoDeVecino.Quieto);
                _cediendo[npc] = estado;
                npc.Brain.ForceState(estado);
            }
        }

        /// Toma parte en la charla: el personaje principal, el jugador, quien habla o es mirado en
        /// alguna línea (por id de diálogo, de persistencia o nombre) o el grupo del jugador.
        private bool EsParticipante(NPCBehaviourManagerV2 npc)
        {
            if (npc == null) return false;
            var t = npc.transform;
            if (t == _principal || t == _jugador) return true;
            if (_principal != null && _principal.IsChildOf(t)) return true;
            if (npc.IsAlly) return true;
            if (_idsQueHablan.Count == 0) return false;

            if (!string.IsNullOrEmpty(npc.DialogueCharacterId) && _idsQueHablan.Contains(npc.DialogueCharacterId)) return true;
            if (!string.IsNullOrEmpty(npc.PersistenceId) && _idsQueHablan.Contains(npc.PersistenceId)) return true;
            string nombre = npc.name;
            if (_idsQueHablan.Contains(nombre)) return true;
            foreach (var id in _idsQueHablan)
            {
                // «CHAR_Oliver» habla por el objeto «Oliver» (mismo criterio que DialogueManager).
                if (id.Length == nombre.Length + 5 && id.StartsWith("CHAR_", StringComparison.Ordinal)
                    && string.Compare(id, 5, nombre, 0, nombre.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;
            }
            return false;
        }

        /// Puede ceder el sitio: está a lo suyo (parado, paseando, en su oficio o yendo a su
        /// actividad) y nadie más manda en él. Los que van de camino, con faena o con una
        /// actividad fija (comer, beber…) solo se apartan si estorban.
        private static bool PuedeCeder(NPCBehaviourManagerV2 npc, bool estorba)
        {
            var ctx = npc.Context;
            var brain = npc.Brain;
            if (ctx == null || brain == null) return false;
            if (ctx.IsInCombat || ctx.IsInCinematic || ctx.IsInteracting || ctx.WasDefeatedInCombat) return false;
            if (ctx.ShouldSeekShelter || ctx.CurrentShelter != null) return false;
            if (ctx.IsActivelyFollowingPlayer || ctx.EstaAnclado) return false;
            if (npc.IsExternalMovementOverrideActive || npc.SentadoFijo) return false;
            if (RepartoDeEscena.EstaTomado(npc.PersistenceId) || RepartoDeEscena.EstaTomado(npc.DialogueCharacterId))
                return false;

            var estado = brain.CurrentState;
            if (estado is WanderState) return true;
            if (estado is IdleState)
            {
                var actividad = npc.Configuration?.ambientConfig != null
                    ? npc.Configuration.ambientConfig.ambientActivity
                    : NPCAmbientActivity.None;
                return estorba || actividad == NPCAmbientActivity.None;
            }
            if (estado is WorkState || estado is WalkToActivityState) return estorba;
            return false;
        }

        private static TipoDeVecino TipoDe(NPCBehaviourManagerV2 npc)
        {
            var ambiente = npc.Configuration?.ambientConfig;
            if (ambiente == null) return TipoDeVecino.Paseante; // sin config: pasea con los valores por defecto
            if (ambiente.permitirTrabajo) return TipoDeVecino.ConOficio;
            if (ambiente.enableWander) return TipoDeVecino.Paseante;
            return TipoDeVecino.Quieto;
        }

        // ── Escenario ───────────────────────────────────────────────────────────

        private void CalcularCentro()
        {
            Vector3 suma = Vector3.zero;
            int n = 0;
            for (int i = _escenario.Count - 1; i >= 0; i--)
            {
                var t = _escenario[i];
                if (t == null) { _escenario.RemoveAt(i); continue; }
                suma += t.position;
                n++;
            }
            if (n > 0) Centro = suma / n;
        }

        private float RadioDelEscenarioDesdeElCentro()
        {
            float max = 0f;
            for (int i = 0; i < _escenario.Count; i++)
            {
                var t = _escenario[i];
                if (t == null) continue;
                Vector3 d = t.position - Centro;
                d.y = 0f;
                max = Mathf.Max(max, d.magnitude);
            }
            return max;
        }

        /// Distancia en el suelo desde un punto a quien toma parte en la charla más cercano.
        private float DistanciaAlEscenario(Vector3 punto)
        {
            float min = float.MaxValue;
            for (int i = 0; i < _escenario.Count; i++)
            {
                var t = _escenario[i];
                if (t == null) continue;
                Vector3 d = t.position - punto;
                d.y = 0f;
                min = Mathf.Min(min, d.sqrMagnitude);
            }
            return min == float.MaxValue ? float.MaxValue : Mathf.Sqrt(min);
        }

        /// Busca por el NavMesh un sitio fuera del escenario de la charla al que apartarse desde
        /// <paramref name="desde"/>: hacia fuera, sin ir hacia la cámara, con camino completo y sin
        /// rodeos largos. False si no hay ninguno.
        public bool BuscarSitioFuera(NavMeshAgent agente, Vector3 desde, out Vector3 destino)
        {
            destino = desde;
            if (agente == null || !agente.enabled || !agente.isOnNavMesh) return false;
            if (s_camino == null) s_camino = new NavMeshPath();

            Vector3 haciaFuera = desde - Centro;
            haciaFuera.y = 0f;
            Vector3 haciaLaCamara = Vector3.zero;
            if (_camara != null)
            {
                haciaLaCamara = _camara.position - Centro;
                haciaLaCamara.y = 0f;
                if (haciaLaCamara.sqrMagnitude > 0.01f) haciaLaCamara.Normalize();
            }
            if (haciaFuera.sqrMagnitude < 0.01f)
                haciaFuera = haciaLaCamara.sqrMagnitude > 0.01f ? -haciaLaCamara : agente.transform.forward;
            haciaFuera.Normalize();

            float distancia = RadioDelEscenarioDesdeElCentro() + _ajustes.radioDelEscenario + _ajustes.separacionAlApartarse;
            float minimoFuera = _ajustes.radioDelEscenario + _ajustes.separacionAlApartarse * 0.5f;

            for (int i = 0; i < s_angulos.Length; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, s_angulos[i], 0f) * haciaFuera;
                if (Vector3.Dot(dir, haciaLaCamara) > 0.6f) continue; // se metería delante de la cámara

                Vector3 objetivo = Centro + dir * distancia;
                objetivo.y = desde.y;
                if (!NavMesh.SamplePosition(objetivo, out var punto, 2f, agente.areaMask)) continue;
                if (DistanciaAlEscenario(punto.position) < minimoFuera) continue;
                if (!NavMesh.CalculatePath(desde, punto.position, agente.areaMask, s_camino)) continue;
                if (s_camino.status != NavMeshPathStatus.PathComplete) continue;

                int n = s_camino.GetCornersNonAlloc(s_esquinas);
                float largo = 0f;
                for (int c = 1; c < n; c++) largo += Vector3.Distance(s_esquinas[c - 1], s_esquinas[c]);
                if (largo > Vector3.Distance(desde, punto.position) * 2.5f + 2f) continue; // rodeo largo

                destino = punto.position;
                return true;
            }
            return false;
        }
    }
}
