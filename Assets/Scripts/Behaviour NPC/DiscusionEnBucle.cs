using Game.NPC.Common;
using Game.NPC.States;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.NPC
{
    /// Dos NPCs que discuten o charlan en bucle, en tono amistoso, mientras dura su tramo de la
    /// historia (<see cref="TramoDeHistoria"/>). Ver INC-363, INC-696 e INC-958.
    ///
    /// Se ponen de cara, se turnan (uno replica con un gesto mientras el otro le habla, y al revés),
    /// llevan la cara de `animo` y los dos tienen encima el bocadillo con el circulito de discusión.
    /// Va en uno de los dos NPCs y maneja a ambos. Mientras discuten están retenidos como en una
    /// cinemática (SequenceActor.Hold): no pasean, no se apartan y no enseñan el icono de interactuar.
    /// En cuanto acaba el tramo se les suelta, para que lo que venga después (una secuencia) los coja
    /// sin que nadie los retenga.
    ///
    /// Con `seLesPuedeHablar` es una charla de ambiente: no se les retiene, así que el jugador puede
    /// hablar con cualquiera de los dos (y el grafo esperar esa charla). Solo charlan si están a
    /// `distanciaParaCharlar` el uno del otro y los dos están libres (en reposo, fuera del grupo y de
    /// cinemáticas, sin diálogo abierto); mientras no lo están, la charla se pausa, y al volver se
    /// giran despacio el uno hacia el otro.
    public class DiscusionEnBucle : MonoBehaviour
    {
        [Tooltip("Id del NPC que lleva este componente (SequenceActor).")]
        [SerializeField] private string miId = "";
        [Tooltip("Id del NPC con el que discute.")]
        [SerializeField] private string conQuienId = "";

        [Header("Cuándo")]
        [SerializeField] private TramoDeHistoria tramo = new();

        [Header("Cómo")]
        [Tooltip("Gestos con los que replica el que escucha, por turnos.")]
        [FormerlySerializedAs("enfado")]
        [SerializeField] private string[] replica = { "Laugh01", "HeadShake01" };
        [SerializeField] private string[] hablar = { "Talk01", "Talk02" };
        [Tooltip("Cara de los dos mientras discuten.")]
        [SerializeField] private NPCEmotion animo = NPCEmotion.Happy;
        [Tooltip("Segundos de cada turno: en uno replica él y habla ella, en el siguiente al revés.")]
        [SerializeField] private float turno = 1.8f;
        [Tooltip("Icono que llevan los dos sobre la cabeza mientras discuten (Canvas Charla, animado).")]
        [SerializeField] private GameObject iconoDePelea;

        [Header("Charla de ambiente")]
        [Tooltip("No los retiene: se puede hablar con ellos mientras charlan, y la charla se pausa mientras alguno está ocupado.")]
        [SerializeField] private bool seLesPuedeHablar = false;
        [Tooltip("Solo charlan si están a esta distancia o menos (metros). Solo con «Se les puede hablar».")]
        [SerializeField] private float distanciaParaCharlar = 3.5f;
        [Tooltip("Grados por segundo con los que se giran el uno hacia el otro al retomar la charla.")]
        [SerializeField] private float giroPorSegundo = 240f;

        private SequenceActor _yo, _otro;
        private NPCAlertIconController _iconoYo, _iconoOtro;
        private bool _discutiendo;
        private float _siguienteTurno;
        private int _paso;
        private float _siguienteComprobacion;
        private bool _enPausa;

        private void Update()
        {
            if (!_discutiendo)
            {
                // Comprobar dos veces por segundo basta: es para arrancar, no para reaccionar.
                if (Time.unscaledTime < _siguienteComprobacion) return;
                _siguienteComprobacion = Time.unscaledTime + 0.5f;
                if (TocaDiscutir()) Empezar();
                return;
            }

            // Terminar sí se mira cada fotograma: en cuanto arranca la secuencia de las peras,
            // se les suelta para que la secuencia los coja sin que nadie los esté reteniendo.
            if (YaSeAcabo() || _yo?.Transform == null || _otro?.Transform == null)
            {
                Terminar();
                return;
            }

            if (seLesPuedeHablar)
            {
                if (!Cerca(_yo, _otro)) { Terminar(); return; }
                bool libres = Libre(_yo) && Libre(_otro);
                if (!libres) { Pausar(); return; }
                if (_enPausa) Reanudar();
                bool mirandose = GirarHacia(_yo, _otro) & GirarHacia(_otro, _yo);
                if (!mirandose) return;
            }

            if (Time.unscaledTime >= _siguienteTurno) Turno();
        }

        private void OnDisable()
        {
            if (_discutiendo) Terminar();
        }

        private bool TocaDiscutir()
        {
            if (!tramo.EnCurso()) return false;
            if (!seLesPuedeHablar) return true;
            // En la charla de ambiente se comprueba antes de empezar que estén juntos y libres.
            return SequenceActor.TryResolve(miId, out var yo) && SequenceActor.TryResolve(conQuienId, out var otro)
                   && Cerca(yo, otro) && Libre(yo) && Libre(otro);
        }

        private bool YaSeAcabo() => tramo.HaAcabado();

        private void Empezar()
        {
            if (!SequenceActor.TryResolve(miId, out _yo) || !SequenceActor.TryResolve(conQuienId, out _otro))
            {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
                Debug.LogWarning($"[DiscusionEnBucle] No encuentro a '{miId}' o a '{conQuienId}': no discuten.");
#endif
                _siguienteComprobacion = Time.unscaledTime + 3f;
                return;
            }

            _discutiendo = true;
            _enPausa = false;
            if (!seLesPuedeHablar)
            {
                _yo.Hold();
                _otro.Hold();
                _yo.StopMovement();
                _otro.StopMovement();
                _yo.Face(_otro.Transform.position);
                _otro.Face(_yo.Transform.position);
            }
            _yo.SetEmotion(animo);
            _otro.SetEmotion(animo);

            _iconoYo = MostrarIcono(_yo.Transform);
            _iconoOtro = MostrarIcono(_otro.Transform);

            _paso = 0;
            _siguienteTurno = 0f;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[DiscusionEnBucle] {miId} y {conQuienId} empiezan a discutir.");
#endif
        }

        private void Turno()
        {
            bool leTocaAEl = _paso % 2 == 0;
            string e = replica[(_paso / 2) % replica.Length];
            string h = hablar[(_paso / 2) % hablar.Length];

            (leTocaAEl ? _yo : _otro).PlayGesture(e);
            (leTocaAEl ? _otro : _yo).PlayGesture(h);

            _paso++;
            _siguienteTurno = Time.unscaledTime + turno;
        }

        private void Pausar()
        {
            if (_enPausa) return;
            _enPausa = true;
            if (_iconoYo != null) _iconoYo.HideAlertIcon();
            if (_iconoOtro != null) _iconoOtro.HideAlertIcon();
            _iconoYo = _iconoOtro = null;
        }

        private void Reanudar()
        {
            _enPausa = false;
            _yo.SetEmotion(animo);
            _otro.SetEmotion(animo);
            _iconoYo = MostrarIcono(_yo.Transform);
            _iconoOtro = MostrarIcono(_otro.Transform);
            _siguienteTurno = Time.unscaledTime + 0.4f;
        }

        private bool Cerca(SequenceActor a, SequenceActor b) =>
            a?.Transform != null && b?.Transform != null
            && (a.Transform.position - b.Transform.position).sqrMagnitude <= distanciaParaCharlar * distanciaParaCharlar;

        /// En reposo y sin nada más que hacer: ni en el grupo, ni en una cinemática, ni hablando.
        private static bool Libre(SequenceActor a)
        {
            var m = a?.Manager;
            if (m == null || !m.isActiveAndEnabled) return false;
            if (m.IsInCinematic || m.IsAlly) return false;
            if (!(m.Brain?.CurrentState is IdleState)) return false;
            var dialogo = DialogueManager.Instance;
            return dialogo == null || !dialogo.IsOpen;
        }

        /// Gira poco a poco a `a` hacia `b`. Devuelve true cuando ya lo tiene de frente.
        private bool GirarHacia(SequenceActor a, SequenceActor b)
        {
            Vector3 dir = b.Transform.position - a.Transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return true;
            var objetivo = Quaternion.LookRotation(dir.normalized, Vector3.up);
            if (Quaternion.Angle(a.Transform.rotation, objetivo) < 2f) return true;
            a.Transform.rotation = Quaternion.RotateTowards(a.Transform.rotation, objetivo, giroPorSegundo * Time.deltaTime);
            a.SyncRotation();
            return false;
        }

        private void Terminar()
        {
            _discutiendo = false;
            _enPausa = false;
            if (_iconoYo != null) _iconoYo.HideAlertIcon();
            if (_iconoOtro != null) _iconoOtro.HideAlertIcon();
            _iconoYo = _iconoOtro = null;
            _yo?.SetEmotion(NPCEmotion.Neutral);
            _otro?.SetEmotion(NPCEmotion.Neutral);
            if (!seLesPuedeHablar)
            {
                _yo?.Release();
                _otro?.Release();
            }
            _yo = _otro = null;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.Log($"[DiscusionEnBucle] Se acabó la discusión.");
#endif
        }

        /// El bocadillo de pelea, con el sistema de iconos de siempre (INC-429).
        ///
        /// Mismo patrón que NPCQuestIconManager: un NPCAlertIconController propio en un hijo
        /// dedicado del NPC, para no pisarse con el del raíz (NPCInteractiveNarrativeExecutor,
        /// AlertState…), y SeguirA(npc) para que coja la cabeza del NPC y no la del hijo vacío —
        /// que era por lo que en INC-381 se sacó de aquí: sin cabeza, el icono no sabía dónde ponerse.
        /// La altura, el aparecer con rebote, el flotar, el mirar a cámara, el encogerse al irse y
        /// la limpieza si el NPC se apaga o se destruye (INC-184) son todos del controlador.
        private NPCAlertIconController MostrarIcono(Transform npc)
        {
            if (iconoDePelea == null || npc == null) return null;

            const string nombreHijo = "_IconoDePelea";
            var hijo = npc.Find(nombreHijo);
            if (hijo == null)
            {
                hijo = new GameObject(nombreHijo).transform;
                hijo.SetParent(npc, false);
            }
            if (!hijo.TryGetComponent(out NPCAlertIconController icono))
                icono = hijo.gameObject.AddComponent<NPCAlertIconController>();

            icono.SeguirA(npc);
            icono.OcultarDuranteDialogos = seLesPuedeHablar; // la discusión de Eldran sigue mientras Oliver habla
            icono.OcultarDuranteSecuencias = false; // y la discusión forma parte de su secuencia
            icono.ShowPersistentIcon(iconoDePelea);
            return icono;
        }
    }
}
