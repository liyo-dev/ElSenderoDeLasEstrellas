using DG.Tweening;
using UnityEngine;

/// <summary>
/// Recompensa/bloqueo del puzle "Sello de las Piedras": una roca (u otro objeto) que tapa el
/// paso al cofre premio hasta que el puzle se resuelve. Pensado para engancharse directamente
/// al evento RuneSequencePuzzle.OnSolved (ver ForbiddenForestPuzzleBuilder.cs, que lo conecta
/// automáticamente con UnityEventTools.AddPersistentListener).
///
/// Al llamar a Open(): desactiva el Collider de la roca (para que deje de bloquear el paso de
/// inmediato, sin esperar a que termine la animación) y la desplaza con DOTween. No destruye el
/// objeto — así el bloqueador puede añadirse a la lista "Disable On Collect" del WorldPickup del
/// cofre para que, en una partida ya guardada con el cofre recogido, aparezca directamente
/// desactivado (ver nota de persistencia en RuneSequencePuzzle.cs).
/// </summary>
[DisallowMultipleComponent]
public class PuzzleRewardGate : MonoBehaviour
{
    [Tooltip("Objeto que bloquea el paso (normalmente una roca). Se mueve, no se destruye.")]
    [SerializeField] private Transform blocker;

    [Tooltip("Desplazamiento local aplicado al bloqueador al abrir (por defecto: se hunde).")]
    [SerializeField] private Vector3 moveOffset = new Vector3(0f, -3f, 0f);
    [SerializeField] private float moveDuration = 1.2f;
    [SerializeField] private Ease moveEase = Ease.InOutSine;

    [Header("Audio (clave de AudioGraphProfile — añadir la clave real cuando exista el SFX)")]
    [SerializeField] private string openSfxKey = "RuneGateOpen";

    bool _opened;

    /// <summary>Idempotente: llamar más de una vez no repite la animación ni el SFX.</summary>
    public void Open()
    {
        if (_opened) return;
        _opened = true;

        if (blocker == null) return;

        var col = blocker.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (!string.IsNullOrEmpty(openSfxKey))
            AudioService.Instance?.PlaySFX(openSfxKey, 1f, blocker.position);

        blocker.DOKill();
        blocker.DOMove(blocker.position + moveOffset, moveDuration).SetEase(moveEase);
    }
}
