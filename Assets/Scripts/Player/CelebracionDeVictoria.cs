using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// La celebración de una victoria, que es de toda la escena y no de un cuerpo: el plano de
    /// cámara que se acerca girando hasta el grupo, la música de victoria y la foto del grupo en
    /// fila (CelebracionEnGrupo). Lo que hace el cuerpo del personaje al mando (dejar el control,
    /// la voltereta, su pose y el gesto final) lo pide a su PlayerBattleModeController. Va en
    /// GrupoDelJugador, uno por escena; registra su paso en el cierre de batalla (CierreDeBatalla).
    /// Ver INC-470, INC-542 e INC-717.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CelebracionDeVictoria : MonoBehaviour
    {
        [Header("Música")]
        [Tooltip("Clave del evento de audio para victoria (configurado en AudioGraphProfile)")]
        [SerializeField] private string victorySfxKey = "Npc_Battle_Victory";

        [Header("Cámara de victoria")]
        [Tooltip("Si está activo, durante la celebración la cámara encuadra al grupo en vez de dejar la cámara de juego tal cual.")]
        [SerializeField] private bool enableVictoryCamera = true;
        [Tooltip("Distancia final de la cámara de victoria al centro de la foto")]
        [SerializeField] private float victoryCamDistance = 2.8f;
        [Tooltip("Altura final de la cámara de victoria respecto al suelo")]
        [SerializeField] private float victoryCamHeight = 1.6f;
        [Tooltip("Ángulo final (grados) respecto al frente del personaje al mando, solo. Negativo = frente-izquierda, dejando el lado derecho de la pantalla libre para el informe.")]
        [SerializeField] private float victoryCamYawOffsetDeg = -35f;
        [Tooltip("Altura del punto al que mira la cámara (relativa a los pies)")]
        [SerializeField] private float victoryCamLookHeight = 1.3f;
        [Tooltip("FOV de la cámara de victoria")]
        [SerializeField] private float victoryCamFOV = 38f;
        [Tooltip("Duración del blend de entrada y salida de la cámara de victoria")]
        [SerializeField] private float victoryCamBlendSeconds = 0.4f;
        [Tooltip("La cámara arranca más lejos y más alta, de lado, y se acerca girando hasta el plano final en estos segundos.")]
        [SerializeField] private float victoryCamMoveSeconds = 3f;
        [SerializeField] private float victoryCamStartDistance = 6.5f;
        [SerializeField] private float victoryCamStartHeight = 3f;
        [Tooltip("Ángulo de salida respecto al frente del personaje al mando (el de llegada es victoryCamYawOffsetDeg).")]
        [SerializeField] private float victoryCamStartYawOffsetDeg = -110f;
        [Tooltip("En las batallas con cierre (jefes), segundos que se deja ver la derrota del enemigo antes de celebrarlo.")]
        [SerializeField] private float victoryDelayAfterDefeat = 1.5f;

        [Header("Foto de victoria en grupo (INC-542)")]
        [SerializeField] private AjustesCelebracionEnGrupo celebracionEnGrupo = new AjustesCelebracionEnGrupo();
        [Tooltip("Metros que se aleja la cámara por cada compañero en la foto.")]
        [SerializeField] private float victoryCamExtraDistancePerMember = 1.1f;
        [Tooltip("Ángulo final de la cámara respecto al frente cuando hay compañeros: más de frente que con uno solo, para que la fila no se vea en diagonal.")]
        [SerializeField] private float victoryCamGroupYawOffsetDeg = -15f;
        [Tooltip("Con compañeros, metros que se desplaza el punto de mira hacia la derecha de la pantalla: el grupo queda a la izquierda y el informe de la derecha no lo tapa.")]
        [SerializeField] private float victoryCamGroupScreenShift = 0.8f;
        [Tooltip("Segundos que se mantiene el gesto final de todos antes de pasar al informe.")]
        [SerializeField] private float victoryFinalHoldSeconds = 0.4f;

        private PasoCelebracionDeVictoria _pasoDeCierre;
        private CelebracionEnGrupo _grupo;
        private PlayerBattleModeController _cuerpo;
        private Vector3 _focoVictoria;
        private Coroutine _victoryCamMove;
        private readonly List<PartyControlManager.CharacterSlot> _enLaFoto = new();

        // Cámara de victoria (se crea la primera vez que hace falta).
        private CinemachineCamera _victoryVcam;
        private CinemachineCamera _gameplayVcam;
        private int _gameplayVcamOriginalPriority;
        private CinemachineBrain _mainBrain;
        private Camera _camaraPrincipal;
        private float _mainBrainOriginalBlendTime;
        private bool _victoryCameraActive;

        /// Se está celebrando una victoria.
        public bool IsPlayingVictory { get; private set; }

        /// Quién ha salido en la última foto de victoria (el personaje al mando primero). Lo lee
        /// el informe de fin de batalla para poner los retratos.
        public IReadOnlyList<PartyControlManager.CharacterSlot> PersonajesEnLaFoto => _enLaFoto;

        private void OnEnable()
        {
            _pasoDeCierre ??= new PasoCelebracionDeVictoria(this, victoryDelayAfterDefeat);
            CierreDeBatalla.Registrar(_pasoDeCierre);
        }

        private void OnDisable()
        {
            if (_pasoDeCierre != null) CierreDeBatalla.Quitar(_pasoDeCierre);
            // Una corrutina parada no llega a su final: se devuelve aquí lo que tomó la celebración.
            TerminarVictoria();
        }

        /// Celebra una victoria fuera del cierre de batalla (combates de NPCs). Si ya se está
        /// celebrando, no hace nada.
        public void PlayVictory(string battleId = null)
        {
            if (IsPlayingVictory || !isActiveAndEnabled) return;
            StartCoroutine(Co_CelebrarYTerminar(battleId));
        }

        private IEnumerator Co_CelebrarYTerminar(string battleId)
        {
            yield return CelebrarVictoria(battleId);
            TerminarVictoria();
        }

        /// <summary>
        /// La celebración sin devolver nada: el personaje al mando deja el control y se pone de cara
        /// a la cámara, los compañeros cercanos se ponen en fila a su lado, la cámara se acerca
        /// girando hasta el centro de la foto, suena la música, el personaje salta con voltereta y
        /// hace su pose, cada compañero la suya al llegar, y al final todos el mismo gesto. Termina
        /// con la cámara aún encuadrándolos; lo devuelve todo <see cref="TerminarVictoria"/>, para
        /// que el cierre de batalla enseñe el informe con este mismo plano.
        /// </summary>
        public IEnumerator CelebrarVictoria(string battleId)
        {
            if (!PlayerService.TryGetComponent(out _cuerpo, allowSceneLookup: false) || _cuerpo == null) yield break;
            IsPlayingVictory = true;
            GameplayEventLog.Log("Victoria", battleId);

            var jugador = _cuerpo.transform;
            _cuerpo.EmpezarCelebracion();

            // De cara a la cámara y el grupo en fila a su lado; la cámara encuadra el centro.
            if (enableVictoryCamera) OrientarHaciaLaCamara(jugador);
            var slot = ControlDelJugador.Activo;
            _grupo ??= new CelebracionEnGrupo(celebracionEnGrupo);
            _focoVictoria = _grupo.Preparar(jugador, slot);
            if (_grupo.Companeros == 0) _focoVictoria = jugador.position;
            _grupo.PersonajesEnLaFoto(_enLaFoto);

            ActivateVictoryCamera(jugador);
            if (_victoryCameraActive) _victoryCamMove = StartCoroutine(Co_MoverCamaraDeVictoria(jugador));

            // El jingle suena una vez y, al acabar, devuelve él mismo la música del lugar.
            if (!string.IsNullOrEmpty(victorySfxKey) && AudioService.Instance != null)
                AudioService.Instance.PlayVictoryForBattle(battleId ?? "", victorySfxKey);

            // Los compañeros van a su sitio mientras el personaje al mando salta.
            _grupo.Empezar(this);
            yield return _cuerpo.SaltoConVoltereta();

            float empiezaPose = Time.time;
            yield return _cuerpo.Gesto(_grupo.PoseDe(slot), _cuerpo.DuracionDeLaPose);

            float topeGrupo = Time.time + celebracionEnGrupo.topeParaLlegar + celebracionEnGrupo.topeDePose;
            while (!_grupo.TodosListos && Time.time < topeGrupo) yield return null;

            // Todos a la vez: el puño arriba.
            if (_grupo.Companeros > 0 && !string.IsNullOrEmpty(_grupo.GestoFinal))
            {
                _grupo.HacerGestoFinal();
                yield return _cuerpo.Gesto(_grupo.GestoFinal, celebracionEnGrupo.topeDePose);
                if (victoryFinalHoldSeconds > 0f) yield return new WaitForSeconds(victoryFinalHoldSeconds);
            }

            // Lo que dura la pose como mínimo, y que la cámara haya llegado a su sitio.
            float fin = Mathf.Max(empiezaPose + _cuerpo.DuracionDeLaPose, Time.time);
            float topeCamara = Time.time + victoryCamMoveSeconds + 2f;
            while (Time.time < fin || (_victoryCamMove != null && Time.time < topeCamara)) yield return null;
        }

        /// <summary>
        /// Devuelve lo que tomó <see cref="CelebrarVictoria"/>: cámara de juego, compañeros y el
        /// control del cuerpo. Idempotente.
        /// </summary>
        public void TerminarVictoria()
        {
            if (!IsPlayingVictory) return;
            IsPlayingVictory = false;

            if (_victoryCamMove != null) { StopCoroutine(_victoryCamMove); _victoryCamMove = null; }
            _grupo?.Soltar();
            DeactivateVictoryCamera();
            if (_cuerpo != null) _cuerpo.TerminarCelebracion();
            _cuerpo = null;
        }

        // ── Cámara ──────────────────────────────────────────────────────────────────────────

        private int CompanerosEnLaFoto => _grupo != null ? _grupo.Companeros : 0;
        private float DistanciaFinalDeCamara => victoryCamDistance + victoryCamExtraDistancePerMember * CompanerosEnLaFoto;
        private float YawFinalDeCamara => CompanerosEnLaFoto > 0 ? victoryCamGroupYawOffsetDeg : victoryCamYawOffsetDeg;

        private static void OrientarHaciaLaCamara(Transform jugador)
        {
            Camera refCam = Camera.main;
            if (refCam == null) return;
            Vector3 camForward = refCam.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.0001f) return;
            jugador.rotation = Quaternion.LookRotation(-camForward.normalized, Vector3.up);
        }

        private void EnsureVictoryCamera()
        {
            if (_victoryVcam != null) return;
            _gameplayVcam = ServiceLocator.Get<CinemachineCamera>(logIfMissing: false);

            var camGo = new GameObject("CamaraDeVictoria");
            camGo.transform.SetParent(transform);
            _victoryVcam = camGo.AddComponent<CinemachineCamera>();
            _victoryVcam.Priority.Value = 0; // inactiva hasta que se celebre
            _victoryVcam.Lens.FieldOfView = victoryCamFOV;
            _victoryVcam.Lens.NearClipPlane = 0.1f;
            _victoryVcam.Lens.FarClipPlane = 1000f;
        }

        private void ActivateVictoryCamera(Transform jugador)
        {
            if (!enableVictoryCamera) return;
            EnsureVictoryCamera();
            _camaraPrincipal = Camera.main;
            _mainBrain = _camaraPrincipal != null ? _camaraPrincipal.GetComponent<CinemachineBrain>() : null;

            Vector3 flatForward = jugador.forward;
            flatForward.y = 0f;
            if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
            flatForward.Normalize();

            Vector3 offsetDir = Quaternion.AngleAxis(YawFinalDeCamara, Vector3.up) * flatForward;
            Vector3 camPos = _focoVictoria + offsetDir * DistanciaFinalDeCamara + Vector3.up * victoryCamHeight;
            Vector3 lookAt = PuntoDeMira(camPos);

            _victoryVcam.transform.position = camPos;
            _victoryVcam.transform.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            _victoryVcam.Target.TrackingTarget = jugador;

            if (_gameplayVcam != null) _gameplayVcamOriginalPriority = _gameplayVcam.Priority.Value;
            if (_mainBrain != null)
            {
                _mainBrainOriginalBlendTime = _mainBrain.DefaultBlend.Time;
                _mainBrain.DefaultBlend.Time = victoryCamBlendSeconds;
            }

            _victoryVcam.Priority.Value = (_gameplayVcam != null ? _gameplayVcam.Priority.Value : 10) + 10;
            _victoryCameraActive = true;
            // Sin reclamar la cámara, vThirdPersonCamera sigue escribiéndola y el plano no se ve. Ver INC-1103.
            CameraDirectorService.Claim(this);
        }

        /// El punto al que mira la cámara de victoria: el centro de la foto a la altura del pecho y,
        /// con compañeros, algo a la derecha, para dejar sitio al informe.
        private Vector3 PuntoDeMira(Vector3 posCamara)
        {
            Vector3 mira = _focoVictoria + Vector3.up * victoryCamLookHeight;
            if (CompanerosEnLaFoto == 0 || Mathf.Approximately(victoryCamGroupScreenShift, 0f)) return mira;
            Vector3 alFoco = mira - posCamara;
            alFoco.y = 0f;
            if (alFoco.sqrMagnitude < 0.0001f) return mira;
            return mira + Vector3.Cross(Vector3.up, alFoco.normalized) * victoryCamGroupScreenShift;
        }

        private void DeactivateVictoryCamera()
        {
            if (!_victoryCameraActive) return;
            if (_victoryVcam != null) _victoryVcam.Priority.Value = 0;
            if (_gameplayVcam != null) _gameplayVcam.Priority.Value = _gameplayVcamOriginalPriority;
            if (_mainBrain != null) _mainBrain.DefaultBlend.Time = _mainBrainOriginalBlendTime;
            _victoryCameraActive = false;
            CameraDirectorService.Release(this);
        }

        private IEnumerator Co_MoverCamaraDeVictoria(Transform jugador)
        {
            if (_victoryVcam == null) { _victoryCamMove = null; yield break; }

            Vector3 frente = jugador.forward; frente.y = 0f;
            if (frente.sqrMagnitude < 0.0001f) frente = Vector3.forward;
            frente.Normalize();

            float dur = Mathf.Max(0.1f, victoryCamMoveSeconds);
            float t = 0f;
            while (t < 1f && _victoryVcam != null && _victoryCameraActive)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / dur);
                float k = Mathf.SmoothStep(0f, 1f, t);

                float extra = victoryCamExtraDistancePerMember * CompanerosEnLaFoto;
                float yaw = Mathf.Lerp(victoryCamStartYawOffsetDeg, YawFinalDeCamara, k);
                float dist = Mathf.Lerp(victoryCamStartDistance + extra, DistanciaFinalDeCamara, k);
                float alto = Mathf.Lerp(victoryCamStartHeight, victoryCamHeight, k);

                Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * frente;
                Vector3 pos = _focoVictoria + dir * dist + Vector3.up * alto;
                var rot = Quaternion.LookRotation((PuntoDeMira(pos) - pos).normalized, Vector3.up);
                _victoryVcam.transform.SetPositionAndRotation(pos, rot);
                // Sin Cinemachine al mando de la cámara principal, el plano se escribe en ella
                // directamente (la cámara ya está reclamada con CameraDirectorService).
                if (_camaraPrincipal != null && (_mainBrain == null || !_mainBrain.isActiveAndEnabled))
                {
                    _camaraPrincipal.transform.SetPositionAndRotation(pos, rot);
                    _camaraPrincipal.fieldOfView = victoryCamFOV;
                }
                yield return null;
            }
            _victoryCamMove = null;
        }
    }
}
