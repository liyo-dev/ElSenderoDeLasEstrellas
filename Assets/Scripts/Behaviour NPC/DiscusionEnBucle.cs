using Game.NPC.Common;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.NPC
{
    /// Dos NPCs que discuten en bucle, en tono amistoso, mientras dura su tramo de la historia
    /// (<see cref="TramoDeHistoria"/>). Ver INC-363 e INC-696.
    ///
    /// Se ponen de cara, se turnan (uno replica con un gesto mientras el otro le habla, y al revés),
    /// llevan la cara de `animo` y los dos tienen encima el bocadillo con el circulito de discusión.
    /// Va en uno de los dos NPCs y maneja a ambos. Mientras discuten están retenidos como en una
    /// cinemática (SequenceActor.Hold): no pasean, no se apartan y no enseñan el icono de interactuar.
    /// En cuanto acaba el tramo se les suelta, para que lo que venga después (una secuencia) los coja
    /// sin que nadie los retenga.
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

        private SequenceActor _yo, _otro;
        private NPCAlertIconController _iconoYo, _iconoOtro;
        private bool _discutiendo;
        private float _siguienteTurno;
        private int _paso;
        private float _siguienteComprobacion;

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

            if (Time.unscaledTime >= _siguienteTurno) Turno();
        }

        private void OnDisable()
        {
            if (_discutiendo) Terminar();
        }

        private bool TocaDiscutir() => tramo.EnCurso();

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
            _yo.Hold();
            _otro.Hold();
            _yo.StopMovement();
            _otro.StopMovement();
            _yo.Face(_otro.Transform.position);
            _otro.Face(_yo.Transform.position);
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

        private void Terminar()
        {
            _discutiendo = false;
            if (_iconoYo != null) _iconoYo.HideAlertIcon();
            if (_iconoOtro != null) _iconoOtro.HideAlertIcon();
            _iconoYo = _iconoOtro = null;
            _yo?.SetEmotion(NPCEmotion.Neutral);
            _otro?.SetEmotion(NPCEmotion.Neutral);
            _yo?.Release();
            _otro?.Release();
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
            icono.OcultarDuranteDialogos = false; // se discute mientras Oliver habla
            icono.OcultarDuranteSecuencias = false; // y la discusión forma parte de su secuencia
            icono.ShowPersistentIcon(iconoDePelea);
            return icono;
        }
    }
}
