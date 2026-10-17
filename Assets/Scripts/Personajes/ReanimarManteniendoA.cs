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

    private AvisoDeReanimarUI _aviso;
    private CuerpoDelGrupo _objetivo;
    private float _progreso;
    private PlayerHealthSystem _salud;
    private PlayerActionManager _modos;
    private GameObject _cuerpoCacheado;

    private void OnDisable()
    {
        _progreso = 0f;
        _objetivo = null;
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

        // Gesto del personaje al canalizar la reanimación
        if (pulsado && _progreso > 0.1f)
        {
            var actor = jugador.GetComponent<NPCSimpleAnimator>();
            if (actor != null) actor.PlaySpellCastSpecial();
        }

        if (_aviso == null) _aviso = AvisoDeReanimarUI.Crear(transform, anillo);
        _aviso.Mostrar(caido.transform, _progreso);

        if (_progreso < 1f) return;
        _progreso = 0f;
        grupo.Reanimar(caido);

        // Efectos de reanimación completada
        if (AudioService.Instance != null)
            AudioService.Instance.PlaySFX("Hechizo_BrisaSanadora", 1f, caido.transform.position);

        var vfxBrisa = Resources.Load<GameObject>("BrisaSanadora");
#if UNITY_EDITOR
        if (vfxBrisa == null)
            vfxBrisa = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_SPELLS/Prefabs/BrisaSanadora.prefab");
#endif
        if (vfxBrisa != null)
            VfxPoolService.Instance.Play(vfxBrisa, caido.transform.position, Quaternion.identity, 2f);

        _objetivo = null;
        _aviso.Ocultar();
    }

    private void Soltar()
    {
        _objetivo = null;
        _progreso = 0f;
        if (_aviso != null) _aviso.Ocultar();
    }

    private bool PuedeReanimarAhora(GameObject jugador)
    {
        if (jugador != _cuerpoCacheado)
        {
            _cuerpoCacheado = jugador;
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
