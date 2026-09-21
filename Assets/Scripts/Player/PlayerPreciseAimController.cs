using UnityEngine;
using Core;

/// Detecta toque corto vs mantenido en los botones de magia izquierda/derecha, para los hechizos
/// que tengan MagicSpellSO.supportsPreciseMode activado (Paso 5 del refactor Tramo 1, análisis
/// claude/analisis-refactor-tramo1-hasta-demonio-2026-09-17.md §6 y §8).
///
/// Para un hechizo NORMAL (supportsPreciseMode = false, el caso de todos los hechizos existentes
/// del proyecto), este componente no interviene en nada: MagicCaster.TryCastSpell sigue
/// lanzándolo al pulsar, exactamente igual que siempre.
///
/// Para un hechizo con modo preciso, TryCastSpell devuelve false sin hacer nada al pulsar (mismo
/// early-out que ya usa MagicKind.Levitation) y es ESTE componente quien decide el lanzamiento
/// real, en el momento de soltar el botón: toque corto (menos de
/// MagicSpellSO.preciseHoldThreshold) = hechizo normal; mantenido = variante precisa (más fina,
/// más rápida, más débil, ver MagicSpellSO.BuildPreciseVariant).
///
/// Sondea GamepadInputReader directamente frame a frame -- mismo patrón que ya usa
/// PlayerLevitationController para su propio hold-to-levitate -- en vez de engancharse a los
/// eventos "Pressed" de vThirdPersonInput, que ya disparan el cast normal al instante y no sirven
/// para decidir nada que dependa de cuánto se mantiene pulsado el botón.
///
/// Solo Left/Right por ahora: GamepadInputReader no expone Held/Released para Special (Y/
/// Triangle), que además ya tiene su propio mecanismo de carga (chargeTime, ver MagicSpellSO)
/// sin depender de que el jugador siga pulsando -- añadirlo aquí sería una superposición de dos
/// sistemas de carga distintos sobre el mismo botón, mejor no mezclarlos sin poder probarlo.
[DisallowMultipleComponent]
public class PlayerPreciseAimController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private MagicCaster magicCaster;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private bool _leftDown, _rightDown;
    private float _leftPressTime, _rightPressTime;

    void Awake()
    {
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
    }

    void Update()
    {
        if (!magicCaster) return;

        UpdateSlot(MagicSlot.Left, GamepadInputReader.AttackMagicLeftHeld, ref _leftDown, ref _leftPressTime);
        UpdateSlot(MagicSlot.Right, GamepadInputReader.AttackMagicRightHeld, ref _rightDown, ref _rightPressTime);
    }

    private void UpdateSlot(MagicSlot slot, bool held, ref bool wasDown, ref float pressTime)
    {
        var spell = magicCaster.GetSpellForSlot(slot);
        bool eligible = spell != null && spell.supportsPreciseMode;

        if (!eligible)
        {
            // No es un hechizo de modo preciso: no interceptar nada. El cast normal ya lo hace
            // TryCastSpell al pulsar, como siempre.
            wasDown = false;
            return;
        }

        if (held)
        {
            if (!wasDown)
            {
                wasDown = true;
                pressTime = Time.time;
            }
            return;
        }

        // held == false aquí: si veníamos de estar pulsado, es el momento de resolver el
        // lanzamiento. Se trata "ya no está pulsado" como equivalente a "soltado" (igual que
        // PlayerLevitationController.CheckForRelease trata '!stillHeld' igual que 'released'),
        // más robusto que depender solo del frame exacto de wasReleasedThisFrame ante una
        // supresión de input a mitad de frame (cambio de escena, cinemática, etc.).
        if (wasDown)
        {
            wasDown = false;
            bool precise = (Time.time - pressTime) >= Mathf.Max(0f, spell.preciseHoldThreshold);
            bool cast = magicCaster.CastResolvedSpell(slot, precise);

            if (showDebugLogs)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[PlayerPreciseAimController] {slot}: {(precise ? "PRECISO" : "normal")} " +
                          $"(mantenido {Time.time - pressTime:F2}s) -> {(cast ? "lanzado" : "fallido")}");
#endif
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
    }
#endif
}
