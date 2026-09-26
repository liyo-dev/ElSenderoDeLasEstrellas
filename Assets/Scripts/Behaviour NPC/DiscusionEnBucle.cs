using Game.NPC.Common;
using UnityEngine;

namespace Game.NPC
{
    /// Dos NPCs discutiendo en bucle mientras la historia lo pide (INC-363).
    ///
    /// «Durante la secuencia de Oliver, mientras hablan, veamos a Eldran y a Victoria haciendo en
    /// bucle las dos animaciones de enfado, alternadas con la de hablar, y sobre sus cabezas un
    /// bocadillo con el típico circulito de pelea. Entonces empieza la misión, y solo al acercarnos
    /// empezaría la secuencia siguiente.»
    ///
    /// Va en el prefab de Eldran del roster y maneja a los dos: se ponen de cara, se turnan (uno se
    /// enfada mientras el otro le habla, y al revés) y los dos llevan encima el icono de pelea.
    ///
    /// CUÁNDO: desde que arranca el saludo de Oliver (o si la misión «Busca a Eldran» ya está en
    /// marcha, que es lo que hay al cargar una partida a medias) hasta PERAS_START, que es cuando
    /// Will llega y arranca la secuencia de las peras. Las señales se CONSULTAN (HasEverRaised), no
    /// se escuchan con OnCustom: una señal que nadie ha consumido todavía se la quedaría el primero
    /// que se suscribe, y aquí eso sería robársela al SequencePlayer que la espera.
    ///
    /// Mientras discuten están retenidos como en una cinemática (SequenceActor.Hold): no pasean,
    /// no se apartan y no enseñan el icono de interactuar.
    public class DiscusionEnBucle : MonoBehaviour
    {
        [SerializeField] private string miId = "NPC_Eldran";
        [SerializeField] private string conQuienId = "NPC_Victoria";

        [Header("Cuándo")]
        [SerializeField] private string empiezaCon = "OLIVER_GREETING_START";
        [SerializeField] private string oConLaMision = "ELDRAN_MISSION1";
        [SerializeField] private string terminaCon = "PERAS_START";

        [Header("Cómo")]
        [SerializeField] private string[] enfado = { "Angry01", "Angry02" };
        [SerializeField] private string[] hablar = { "Talk01", "Talk02" };
        [Tooltip("Segundos de cada turno: en uno se enfada él y habla ella, en el siguiente al revés.")]
        [SerializeField] private float turno = 1.8f;
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

        private bool TocaDiscutir()
        {
            var s = DefaultNarrativeSignals.Instance;
            if (s == null || YaSeAcabo()) return false;

            bool empezo = s.HasEverRaised(empiezaCon);
            bool misionActiva = !string.IsNullOrEmpty(oConLaMision)
                                && s.GetQuestState(oConLaMision) == NarrativeQuestState.Active;
            return empezo || misionActiva;
        }

        private bool YaSeAcabo()
        {
            var s = DefaultNarrativeSignals.Instance;
            if (s == null) return false;
            if (s.HasEverRaised(terminaCon)) return true;
            return !string.IsNullOrEmpty(oConLaMision)
                   && s.GetQuestState(oConLaMision) == NarrativeQuestState.Completed;
        }

        private void Empezar()
        {
            if (!SequenceActor.TryResolve(miId, out _yo) || !SequenceActor.TryResolve(conQuienId, out _otro))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            _yo.SetEmotion(NPCEmotion.Angry);
            _otro.SetEmotion(NPCEmotion.Angry);

            _iconoYo = MostrarIcono(_yo.Transform);
            _iconoOtro = MostrarIcono(_otro.Transform);

            _paso = 0;
            _siguienteTurno = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DiscusionEnBucle] {miId} y {conQuienId} empiezan a discutir (hasta {terminaCon}).");
#endif
        }

        private void Turno()
        {
            bool leTocaAEl = _paso % 2 == 0;
            string e = enfado[(_paso / 2) % enfado.Length];
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DiscusionEnBucle] Se acabó la discusión ({terminaCon}).");
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
