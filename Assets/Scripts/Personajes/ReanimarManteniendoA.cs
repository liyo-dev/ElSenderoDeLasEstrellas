using Core;
using Sendero.UI;
using UnityEngine;

/// Reanimar a un compañero caído: el personaje al mando se pone a su lado y mantiene A unos
/// segundos (sin protección: se le puede golpear mientras tanto). Al lado de un caído, A no salta.
/// Va en GrupoDelJugador junto a SaludDelGrupo. Ver INC-717 y GDD § 6.1.
[DisallowMultipleComponent]
public sealed class ReanimarManteniendoA : MonoBehaviour
{
    [Tooltip("Distancia (m) al caído a la que se le puede reanimar.")]
    [SerializeField, Min(0.5f)] private float radio = 1.8f;
    [Tooltip("Segundos manteniendo A para levantarlo.")]
    [SerializeField, Min(0.2f)] private float segundos = 2f;
    [Tooltip("Anillo del aviso. Vacío = el del aviso de combate.")]
    [SerializeField] private Sprite anillo;
    [Tooltip("Estado del Animator que sostiene el personaje al mando mientras mantiene A (pose de canalizar).")]
    [SerializeField] private string poseAlReanimar = "MagicSpecial";
    [Tooltip("Efecto de un uso sobre el caído al levantarse (prefab de la Brisa Sanadora). Vacío = sin efecto.")]
    [SerializeField] private GameObject vfxAlReanimar;
    [Tooltip("Segundos que dura vfxAlReanimar antes de volver al pool.")]
    [SerializeField, Min(0.1f)] private float duracionDelVfx = 2f;
    [Tooltip("Event Key del AudioGraphProfile que suena al levantarse el caído.")]
    [SerializeField] private string sfxAlReanimar = "Hechizo_BrisaSanadora";

    private AvisoDeReanimarUI _aviso;
    private CuerpoDelGrupo _objetivo;
    private float _progreso;
    private PlayerHealthSystem _salud;
    private PlayerActionManager _modos;
    private GameObject _cuerpoCacheado;
    private NPCSimpleAnimator _animador;
    private NPCSimpleAnimator _animadorEnPose;

    private void OnDisable()
    {
        _progreso = 0f;
        _objetivo = null;
        SoltarPose();
        if (_aviso != null) _aviso.Ocultar();
    }

    private void Update()
    {
        var grupo = SaludDelGrupo.Instance;
        if (grupo == null || grupo.Caidos.Count == 0) { Soltar(); return; }

        var jugador = ControlDelJugador.CuerpoDelJugador;
        if (jugador == null || !PuedeReanimarAhora(jugador)) { Soltar(); return; }

        var caido = grupo.CaidoMasCercano(jugador.transform.position, radio);
        if (caido == null) { Soltar(); return; }
        if (caido != _objetivo) { _objetivo = caido; _progreso = 0f; }

        // Al lado de un caído, A reanima en vez de saltar.
        GamepadInputReader.IgnoreJumpButton(0.2f);
        float paso = Time.deltaTime / segundos;
        bool pulsado = GamepadInputReader.JumpHeld;
        _progreso = pulsado ? _progreso + paso : Mathf.Max(0f, _progreso - paso * 2f);

        // Pose de canalizar: se pone una vez al empezar a mantener A y se suelta al dejar de hacerlo.
        if (pulsado) SostenerPose();
        else SoltarPose();

        if (_aviso == null) _aviso = AvisoDeReanimarUI.Crear(transform, anillo);
        _aviso.Mostrar(caido.transform, _progreso);

        if (_progreso < 1f) return;
        _progreso = 0f;
        grupo.Reanimar(caido);

        // Efectos de reanimación completada
        SoltarPose();
        if (AudioService.Instance != null && !string.IsNullOrEmpty(sfxAlReanimar))
            AudioService.Instance.PlaySFX(sfxAlReanimar, 1f, caido.transform.position);
        if (vfxAlReanimar != null && VfxPoolService.Instance != null)
            VfxPoolService.Instance.Play(vfxAlReanimar, caido.transform.position, Quaternion.identity, duracionDelVfx);

        _objetivo = null;
        _aviso.Ocultar();
    }

    private void Soltar()
    {
        _objetivo = null;
        _progreso = 0f;
        SoltarPose();
        if (_aviso != null) _aviso.Ocultar();
    }

    private void SostenerPose()
    {
        if (_animadorEnPose != null || _animador == null || string.IsNullOrEmpty(poseAlReanimar)) return;
        _animadorEnPose = _animador;
        _animadorEnPose.HoldPose(poseAlReanimar, congelarAlFinal: true);
    }

    // Suelta solo la parte de arriba del cuerpo (SoltarPoseDeCarga): las piernas siguen con su
    // locomoción, igual que con el gesto de hechizo.
    private void SoltarPose()
    {
        if (_animadorEnPose == null) return;
        _animadorEnPose.SoltarPoseDeCarga();
        _animadorEnPose = null;
    }

    private bool PuedeReanimarAhora(GameObject jugador)
    {
        if (jugador != _cuerpoCacheado)
        {
            _cuerpoCacheado = jugador;
            SoltarPose();
            _animador = jugador.GetComponent<NPCSimpleAnimator>();
            _salud = jugador.GetComponentInChildren<PlayerHealthSystem>(true);
            _modos = jugador.GetComponentInParent<PlayerActionManager>();
            if (_modos == null) _modos = jugador.GetComponentInChildren<PlayerActionManager>(true);
        }
        if (_salud != null && !_salud.IsAlive) return false;
        if (ComboCastController.IsComposing) return false;
        var entrada = Core.PlayerInputManager.Instance;
        if (entrada != null && entrada.IsInUIMode) return false;
        if (_modos != null && _modos.Top != ActionMode.Default && _modos.Top != ActionMode.Combat) return false;
        return true;
    }
}
