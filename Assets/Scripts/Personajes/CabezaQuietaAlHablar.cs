using UnityEngine;

/// Al hablar solo se mueve la boca: la cabeza se queda quieta.
///
/// Los gestos de charla del pack (Talk01, Talk02, Talk03) van en la capa UpperBody, cuya máscara
/// incluye la cabeza, y cabecean al ritmo de la frase. Con las cabezas grandes de los chibis ese
/// vaivén se lee como «mueve la cabeza para hablar» (INC-1144, Eldran en la charla del regalo).
///
/// Después de la animación (LateUpdate) sujeta el cuello y la cabeza en la postura que tenían justo
/// al empezar la charla. El tronco y los brazos siguen gesticulando; la boca la mueve
/// NPCEmotionController como siempre. Entra y sale con un fundido corto, así que no hay tirón.
///
/// No toca nada fuera de Talk01-03: los gestos que sí deben mover la cabeza (asentir, negar, reír,
/// preguntar…) se ven como antes, y si suena un gesto de la capa «Cabeza» a la vez que la charla,
/// manda el gesto. Corre antes que DialogueHeadLook, así la mirada hacia quien escucha se sigue
/// sumando encima.
///
/// Lo añaden solos NPCSimpleAnimator (todos los NPCs) y PlayerDialogueAnimator (Will), igual que
/// PosturaDeSentado: vale en juego, en la caja de diálogo y en las cinemáticas.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-20)]
public sealed class CabezaQuietaAlHablar : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Cuánto se sujeta la cabeza al hablar (1 = quieta del todo, 0 = como la animación).")]
    private float firmeza = 1f;
    [SerializeField, Tooltip("Velocidad del fundido al empezar y acabar de hablar (1/velocidad ≈ segundos).")]
    private float velocidadDelFundido = 6f;

    private static readonly int[] Charla =
    {
        Animator.StringToHash("Talk01"), Animator.StringToHash("Talk02"), Animator.StringToHash("Talk03"),
    };
    private static readonly int CabezaQuieta = Animator.StringToHash("Quieta");

    private Animator _animador;
    private Transform _cuello, _cabeza;
    private int _capaDeCabeza = -1;
    private bool _preparado;

    private float _peso;
    private bool _sujetando;
    private Quaternion _cuelloFijo, _cabezaFija;

    /// Se lo pone a quien no lo tenga. Barato: si el personaje no habla, no hace nada.
    public static void Asegurar(GameObject quien)
    {
        if (quien != null && quien.GetComponent<CabezaQuietaAlHablar>() == null) quien.AddComponent<CabezaQuietaAlHablar>();
    }

    private void Preparar()
    {
        _preparado = true;
        // GetComponent primero: el Animator del cuerpo está en el mismo objeto que NPCSimpleAnimator.
        // GetComponentInChildren podría encontrar antes un Animator de prop o capa hijo (no humanoid).
        _animador = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (_animador == null || !_animador.isHuman) { _animador = null; return; }
        _cuello = _animador.GetBoneTransform(HumanBodyBones.Neck);
        _cabeza = _animador.GetBoneTransform(HumanBodyBones.Head);
        if (_cabeza == null) { _animador = null; return; }
        _capaDeCabeza = _animador.runtimeAnimatorController != null ? _animador.GetLayerIndex("Cabeza") : -1;
    }

    private void LateUpdate()
    {
        if (!_preparado) Preparar();
        if (_animador == null || !_animador.isActiveAndEnabled || _animador.runtimeAnimatorController == null) return;

        bool hablando = EstaHablando() && !GestoDeCabezaSonando();
        float objetivo = hablando ? firmeza : 0f;

        if (objetivo > 0f && !_sujetando)
        {
            // Empieza la charla: la postura de este fotograma (aún la de antes, el fundido de la
            // charla acaba de empezar) es la que se sujeta mientras dure.
            _sujetando = true;
            if (_cuello != null) _cuelloFijo = _cuello.localRotation;
            _cabezaFija = _cabeza.localRotation;
        }

        _peso = Mathf.MoveTowards(_peso, objetivo, velocidadDelFundido * Time.deltaTime);
        if (_peso <= 0f)
        {
            if (objetivo <= 0f) _sujetando = false;
            return;
        }

        if (_cuello != null) _cuello.localRotation = Quaternion.Slerp(_cuello.localRotation, _cuelloFijo, _peso);
        _cabeza.localRotation = Quaternion.Slerp(_cabeza.localRotation, _cabezaFija, _peso);
    }

    private bool EstaHablando()
    {
        int capas = _animador.layerCount;
        for (int i = 0; i < capas; i++)
        {
            if (i > 0 && _animador.GetLayerWeight(i) <= 0.01f) continue;
            if (EsCharla(_animador.GetCurrentAnimatorStateInfo(i).shortNameHash)) return true;
            if (_animador.IsInTransition(i) && EsCharla(_animador.GetNextAnimatorStateInfo(i).shortNameHash)) return true;
        }
        return false;
    }

    private bool GestoDeCabezaSonando()
    {
        if (_capaDeCabeza < 0) return false;
        if (_animador.IsInTransition(_capaDeCabeza)) return true;
        return _animador.GetCurrentAnimatorStateInfo(_capaDeCabeza).shortNameHash != CabezaQuieta;
    }

    private static bool EsCharla(int hash)
    {
        for (int i = 0; i < Charla.Length; i++) if (Charla[i] == hash) return true;
        return false;
    }
}
