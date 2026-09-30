using UnityEngine;
using Core;

/// <summary>
/// Único lector de los botones de combate del jugador. Traduce cada botón a su sistema:
/// <list type="bullet">
/// <item>X: serie básica (<see cref="MagicCaster.CastBasic"/>). Si el hechizo admite modo preciso,
/// sale al soltar: toque = normal, mantenido = preciso.</item>
/// <item>LB: pasa al siguiente hechizo básico equipado.</item>
/// </list>
/// Una X pulsada mientras aún sale el hechizo anterior se guarda un instante y sale en cuanto se
/// puede (buffer de entrada). La levitación lee sus propios botones (PlayerLevitationController).
/// Ver INC-486.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCombatInput : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MagicCaster magicCaster;

    [Header("Serie básica (X)")]
    [Tooltip("Segundos que se guarda una X pulsada mientras el hechizo anterior aún está saliendo.")]
    [SerializeField, Min(0f)] private float inputBufferSeconds = 0.35f;

    [Header("Rotación de hechizos (LB)")]
    [Tooltip("Clave de SFX al pasar al siguiente hechizo básico. Vacío = sin sonido.")]
    [SerializeField] private string rotateSfxKey = "UI_Navigate";

    private bool _xDown;
    private float _xPressTime;
    private float _bufferedUntil = -1f;
    private bool _bufferedPrecise;

    void Awake()
    {
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
    }

    void OnDisable()
    {
        _xDown = false;
        _bufferedUntil = -1f;
    }

    void Update()
    {
        if (!magicCaster) return;

        // Tecleando un combo (Y), la X y LB son botones de la secuencia (INC-494).
        if (ComboCastController.IsComposing)
        {
            _xDown = false;
            _bufferedUntil = -1f;
            return;
        }

        HandleRotation();
        HandleBasicAttack();
    }

    private void HandleRotation()
    {
        if (!GamepadInputReader.LeftShoulderPressed) return;
        if (magicCaster.RotateBasic() && !string.IsNullOrEmpty(rotateSfxKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(rotateSfxKey);
    }

    private void HandleBasicAttack()
    {
        var spell = magicCaster.ActiveBasic;

        // La levitación gestiona la X ella misma (mantener para agarrar, soltar para lanzar).
        if (spell != null && spell.kind == MagicKind.Levitation)
        {
            _xDown = false;
            _bufferedUntil = -1f;
            return;
        }

        bool supportsPrecise = spell != null && spell.supportsPreciseMode;

        if (GamepadInputReader.AttackMagicLeftPressed)
        {
            if (supportsPrecise)
            {
                _xDown = true;
                _xPressTime = Time.time;
            }
            else
            {
                Request(precise: false);
            }
        }

        // Modo preciso: se decide al soltar. "Ya no está pulsado" cuenta como soltar, por si una
        // supresión de entrada a mitad de fotograma se come el evento exacto de soltar.
        if (_xDown && !GamepadInputReader.AttackMagicLeftHeld)
        {
            _xDown = false;
            bool precise = supportsPrecise && Time.time - _xPressTime >= Mathf.Max(0f, spell.preciseHoldThreshold);
            Request(precise);
        }

        if (_bufferedUntil >= 0f)
        {
            if (Time.time > _bufferedUntil) _bufferedUntil = -1f;
            else TryCastBuffered();
        }
    }

    private void Request(bool precise)
    {
        _bufferedPrecise = precise;
        _bufferedUntil = Time.time + inputBufferSeconds;
        TryCastBuffered();
    }

    private void TryCastBuffered()
    {
        // Mientras sale el hechizo anterior, la pulsación espera en el buffer.
        if (magicCaster.IsCasting) return;
        if (magicCaster.CastBasic(_bufferedPrecise))
            _bufferedUntil = -1f;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
    }
#endif
}
