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
        private GameObject _iconoYo, _iconoOtro;
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

            _iconoYo = Icono(_yo.Transform);
            _iconoOtro = Icono(_otro.Transform);
            BarrerIconosSueltos("al empezar");

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
            Quitar(ref _iconoYo);
            Quitar(ref _iconoOtro);
            BarrerIconosSueltos("al terminar");
            _yo?.SetEmotion(NPCEmotion.Neutral);
            _otro?.SetEmotion(NPCEmotion.Neutral);
            _yo?.Release();
            _otro?.Release();
            _yo = _otro = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DiscusionEnBucle] Se acabó la discusión ({terminaCon}).");
#endif
        }

        /// El bocadillo de pelea, COLGADO DEL NPC (INC-381).
        ///
        /// Antes se pedía prestado NPCAlertIconController, que instancia el icono SIN PADRE y lo
        /// va siguiendo desde una corrutina suya. Puesto en un hijo vacío recién creado, ese
        /// componente no encuentra ni hueso de cabeza ni nada que seguir, así que los dos iconos
        /// se quedaban flotando en mitad de la plaza — y cuando la discusión terminaba, huérfanos
        /// (es el mismo patrón de INC-184). Aquí el icono es hijo del NPC: va donde va él, y se
        /// destruye cuando esto se apaga, pase lo que pase.
        private GameObject Icono(Transform npc)
        {
            if (iconoDePelea == null || npc == null) return null;

            // Por si quedara alguno de un arranque anterior colgado de este mismo NPC.
            for (int i = npc.childCount - 1; i >= 0; i--)
                if (npc.GetChild(i).name == NombreDelIcono) Destroy(npc.GetChild(i).gameObject);

            var go = Instantiate(iconoDePelea, npc, false);
            go.name = NombreDelIcono;
            go.transform.localPosition = new Vector3(0f, AlturaDeLaCabeza(npc) + 0.35f, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.SetActive(true);
            return go;
        }

        private static void Quitar(ref GameObject icono)
        {
            if (icono != null) Destroy(icono);
            icono = null;
        }

        private const string NombreDelIcono = "_BocadilloDePelea";

        /// Barre CUALQUIER bocadillo de pelea que haya suelto por la escena, venga de donde venga
        /// (INC-388). En la grabación 15 se veían tres y cuatro a la vez flotando sobre el mercado:
        /// con esto, al empezar y al terminar la discusión no puede quedar ninguno que no sea de
        /// los dos que están discutiendo ahora mismo. El aviso dice cuántos había y de quién
        /// colgaban, que es lo que falta para saber quién los está creando.
        private void BarrerIconosSueltos(string cuando)
        {
            int n = 0;
            string dueños = "";

            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (t == null || t.name != NombreDelIcono) continue;
                if (_iconoYo != null && t.gameObject == _iconoYo) continue;
                if (_iconoOtro != null && t.gameObject == _iconoOtro) continue;

                n++;
                dueños += (t.parent != null ? t.parent.name : "SIN PADRE") + " ";
                Destroy(t.gameObject);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (n > 0)
                Debug.Log($"[DiscusionEnBucle] Barridos {n} bocadillo(s) de pelea sueltos {cuando}. " +
                          $"Colgaban de: {dueños}");
#endif
        }

        /// La cabeza del NPC, o el alto de lo que se vea de él si no hay esqueleto humanoide.
        private static float AlturaDeLaCabeza(Transform npc)
        {
            var animator = npc.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                var cabeza = animator.GetBoneTransform(HumanBodyBones.Head);
                if (cabeza != null) return cabeza.position.y - npc.position.y;
            }

            var r = npc.GetComponentInChildren<Renderer>();
            if (r != null) return r.bounds.max.y - npc.position.y;

            return 1.7f;
        }
    }
}
