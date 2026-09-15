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
///
/// Opcionalmente (campo "chestToReveal") también puede ocultar el cofre premio hasta que el
/// puzle se resuelve, y hacerlo aparecer con un pequeño efecto (VFX opcional + "pop" de escala
/// vía DOTween que no depende de ningún asset) en vez de dejarlo siempre visible detrás de la
/// roca bloqueadora.
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

    [Header("Cofre premio (opcional)")]
    [Tooltip("Si se asigna, este objeto (normalmente el cofre premio) se mantiene oculto/" +
             "inactivo hasta que el puzle se resuelve, y aparece justo cuando se llama a Open() " +
             "en vez de estar siempre visible detrás de la roca bloqueadora. Si se deja vacío, " +
             "el comportamiento es el de siempre (cofre visible desde el principio).")]
    [SerializeField] private GameObject chestToReveal;
    [Tooltip("VFX opcional a instanciar sobre el cofre al revelarse (p. ej. alguno de " +
             "Assets/VFX/100BestEffectPack/Effects/OtherMagicEffect u HolyEffect, línea " +
             "violeta/mágica igual que el brillo de las piedras). Se deja sin asignar a " +
             "propósito — mismo criterio que con el ItemData de la recompensa: mejor elegirlo " +
             "a mano viendo el resultado en el Editor que a ciegas por GUID. Sin VFX asignado, " +
             "el cofre igualmente aparece con la animación de escala de más abajo.")]
    [SerializeField] private GameObject revealVfxPrefab;
    [SerializeField] private float revealVfxLifetime = 3f;
    [Tooltip("Duración del 'pop' de aparición del cofre (animación de escala). No depende de " +
             "ningún VFX — es la señal visual mínima garantizada de que el cofre acaba de " +
             "aparecer.")]
    [SerializeField] private float revealScaleDuration = 0.5f;
    [SerializeField] private Ease revealScaleEase = Ease.OutBack;

    bool _opened;
    Vector3 _chestBaseScale = Vector3.one;
    bool _chestScaleCached;

    void Awake()
    {
        if (chestToReveal != null)
        {
            _chestBaseScale = chestToReveal.transform.localScale;
            _chestScaleCached = true;
            chestToReveal.SetActive(false);
        }
    }

    /// <summary>Idempotente: llamar más de una vez no repite la animación ni el SFX.</summary>
    public void Open()
    {
        if (_opened) return;
        _opened = true;

        if (blocker != null)
        {
            var col = blocker.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            blocker.DOKill();
            var blockerRef = blocker;
            blocker.DOMove(blocker.position + moveOffset, moveDuration)
                .SetEase(moveEase)
                .OnComplete(() =>
                {
                    // El desplazamiento (por defecto, hundirse 3 unidades) puede no bastar para
                    // que la roca quede realmente bajo el nivel visible del suelo según su
                    // tamaño real (aquí se instancia a escala x1.8 sobre un terreno irregular,
                    // sin ver la geometría real desde aquí) — quedando a medio hundir y todavía
                    // visible en vez de "desaparecer". Para que el resultado final sea siempre
                    // fiable pase lo que pase con esa distancia, al terminar la animación se
                    // desactiva también el propio GameObject: la roca ya ha hecho su recorrido
                    // de hundimiento (feedback visual de que algo pasó) y al final simplemente
                    // deja de existir en escena, sin depender de acertar la profundidad exacta.
                    if (blockerRef != null) blockerRef.gameObject.SetActive(false);
                });
        }

        if (!string.IsNullOrEmpty(openSfxKey))
        {
            Vector3 sfxPos = blocker != null ? blocker.position : transform.position;
            AudioService.Instance?.PlaySFX(openSfxKey, 1f, sfxPos);
        }

        RevealChest();
    }

    void RevealChest()
    {
        if (chestToReveal == null) return;
        if (chestToReveal.activeSelf) return; // ya estaba visible (chestToReveal sin usar / partida ya resuelta)

        chestToReveal.SetActive(true);

        if (revealVfxPrefab != null)
        {
            var vfx = Instantiate(revealVfxPrefab, chestToReveal.transform.position, Quaternion.identity);
            Destroy(vfx, revealVfxLifetime);
        }

        var t = chestToReveal.transform;
        Vector3 targetScale = _chestScaleCached ? _chestBaseScale : t.localScale;
        t.DOKill();
        t.localScale = Vector3.zero;
        t.DOScale(targetScale, revealScaleDuration).SetEase(revealScaleEase);
    }
}
