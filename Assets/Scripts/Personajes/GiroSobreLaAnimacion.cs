using UnityEngine;

/// Protege los huesos que se giran por código encima de la animación (mirada de DialogueHeadLook,
/// PosturaDeSentado, abrazos de GestosProcedurales…) de que el giro se acumule.
///
/// Esos sistemas multiplican la rotación del hueso en LateUpdate, contando con que el Animator la acaba
/// de escribir. Si un fotograma no la escribe (fuera de cámara con «Cull Update Transforms», lejos con
/// «Cull Completely», animador parado), el hueso conserva el giro del fotograma anterior y el sistema
/// vuelve a girarlo encima: la cabeza da la vuelta poco a poco y, cuando el personaje sale en el plano,
/// está boca abajo o mirando atrás (INC-1308, Liora en el prólogo).
///
/// Corre el primero del LateUpdate: si un hueso que el fotograma anterior se giró por código sigue
/// idéntico, bit a bit, a como lo dejó el último (<see cref="GiroSobreLaAnimacionAlFinal"/>), el Animator
/// no lo ha tocado, y se devuelve a la pose de la animación de la que se partió. La comparación es exacta
/// (no el == de Quaternion, que da por iguales giros de menos de ~0,1°): con ese, una animación lenta
/// (respirar, escuchar) se quedaba congelada a saltos y el personaje temblaba. Vale para cualquier humanoide; lo ponen solos
/// NPCSimpleAnimator y PlayerDialogueAnimator con <see cref="Asegurar"/>.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-32000)]
public sealed class GiroSobreLaAnimacion : MonoBehaviour
{
    private static readonly HumanBodyBones[] Vigilados =
    {
        HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head,
    };

    private Transform[] _huesos;
    private Quaternion[] _deLaAnimacion, _alFinal;
    private bool _hayFinal;

    /// Se lo pone (con su pareja del final del fotograma) a quien no lo tenga.
    public static void Asegurar(GameObject quien)
    {
        if (quien == null) return;
        if (!quien.TryGetComponent<GiroSobreLaAnimacion>(out _)) quien.AddComponent<GiroSobreLaAnimacion>();
        if (!quien.TryGetComponent<GiroSobreLaAnimacionAlFinal>(out _)) quien.AddComponent<GiroSobreLaAnimacionAlFinal>();
    }

    private void Awake()
    {
        var animador = GetComponent<Animator>();
        if (animador == null) animador = GetComponentInChildren<Animator>(true);
        if (animador == null || !animador.isHuman) { enabled = false; return; }
        int n = 0;
        var huesos = new Transform[Vigilados.Length];
        foreach (var h in Vigilados)
        {
            var t = animador.GetBoneTransform(h);
            if (t != null) huesos[n++] = t;
        }
        _huesos = new Transform[n];
        System.Array.Copy(huesos, _huesos, n);
        _deLaAnimacion = new Quaternion[n];
        _alFinal = new Quaternion[n];
    }

    private void OnDisable() => _hayFinal = false;

    private void LateUpdate()
    {
        if (_huesos == null) return;
        for (int i = 0; i < _huesos.Length; i++)
        {
            var h = _huesos[i];
            var actual = h.localRotation;
            if (_hayFinal && !Identicos(_alFinal[i], _deLaAnimacion[i]) && Identicos(actual, _alFinal[i]))
                h.localRotation = actual = _deLaAnimacion[i];
            _deLaAnimacion[i] = actual;
        }
    }

    private static bool Identicos(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

    /// Lo llama GiroSobreLaAnimacionAlFinal, el último del LateUpdate: cómo quedan los huesos este fotograma.
    public void ApuntarFinal()
    {
        if (_huesos == null || !isActiveAndEnabled) return;
        for (int i = 0; i < _huesos.Length; i++) _alFinal[i] = _huesos[i].localRotation;
        _hayFinal = true;
    }
}
