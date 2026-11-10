// Assets/Scripts/UI/LoadingShowcaseCharacter.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Personaje decorativo del escenario de la pantalla de carga: corre en el sitio (mirando
/// hacia la derecha de cámara) mientras dura la carga, y al terminar se gira hacia cámara y
/// reproduce un gesto gracioso propio (Animator Controller ligero "LoadingShowcase.controller",
/// con estados Run / Gesture — nada que ver con el controller de gameplay del personaje real).
///
/// El modelo es un hijo enlazado a RetratoVivo_&lt;nombre&gt;.prefab (malla + Animator + cara, sin scripts de
/// gameplay), el mismo de los retratos vivos: lo mantienen al día los menús de Personajes, así que
/// aquí sale siempre el aspecto actual. No usar _WILL/_ESTELA/_LIAM.prefab: su Awake() real pisaría
/// registros del jugador. Montaje: El Sendero ▸ Personajes ▸ Pantalla de carga: modelos al día.
///
/// La cara acompaña al movimiento (INC-930): mientras corre va cambiando por <see cref="carasCorriendo"/>
/// en bucle, y al girarse sigue <see cref="carasDelGesto"/> desde el principio, quedándose con la última.
/// Las caras las pone el <see cref="NPCEmotionController"/> del modelo, así que salen del perfil de cara
/// del personaje (con sus símbolos de anime). Las listas son datos de cada personaje en la escena.
/// </summary>
[DisallowMultipleComponent]
public class LoadingShowcaseCharacter : MonoBehaviour
{
    [Tooltip("0 = gesto de Will (Dance), 1 = gesto de Estela (Victory), 2 = gesto de Liam (Greeting) " +
             "— debe coincidir con el parámetro GestureIndex de LoadingShowcase.controller.")]
    public int gestureIndex;

    // La orientación "de fábrica" del rig no es la misma para los 3 héroes (ver MainMenuFlyingCompanion,
    // mismo problema ya documentado allí). runYaw/revealYaw son la mejor estimación a partir de cómo
    // quedaban Will/Estela/Liam en MainMenu.unity, pero conviene comprobarlos a ojo la primera vez que se
    // abra esta escena en el editor y corregirlos aquí si el personaje mira para el lado que no toca.
    [Header("Orientación (AJUSTAR A OJO EN EDITOR)")]
    [Tooltip("Rotación Y (grados, mundo) mientras corre: debe dejarlo mirando hacia la derecha de cámara.")]
    public float runYaw = 90f;
    [Tooltip("Rotación Y (grados, mundo) al girarse hacia cámara para el gesto final.")]
    public float revealYaw = 180f;
    [Tooltip("Duración del giro hacia cámara al terminar la carga.")]
    public float turnDuration = 0.25f;

    [Tooltip("Layer a la que se pasa este personaje (y todos sus hijos) para que solo lo vea la StageCamera " +
             "del escenario de carga, no ninguna otra cámara de la escena.")]
    public string renderLayerName = "UI_Portrait";

    /// Una cara durante un rato.
    [System.Serializable]
    public struct CaraDurante
    {
        public NPCEmotion emocion;
        [Min(0.05f)] public float segundos;
    }

    [Header("Caras (INC-930)")]
    [Tooltip("Caras mientras corre, en bucle. Vacío = cara de reposo.")]
    public CaraDurante[] carasCorriendo = new CaraDurante[0];
    [Tooltip("Caras desde que se gira hacia cámara; la última se queda puesta. Vacío = no cambia.")]
    public CaraDurante[] carasDelGesto = new CaraDurante[0];

    Animator _animator;
    Coroutine _turnRoutine;
    NPCEmotionController _cara;
    CaraDurante[] _carasActuales;
    bool _carasEnBucle;
    int _indiceDeCara = -1;
    float _proximaCara;

    static readonly int HashReveal = Animator.StringToHash("Reveal");
    static readonly int HashGestureIndex = Animator.StringToHash("GestureIndex");

    void Awake()
    {
        ApplyRenderLayerRecursive();

        // Sin EmpezarCaras: el NPCEmotionController del modelo (hijo) puede no haber hecho aún su Awake.
        _cara = GetComponentInChildren<NPCEmotionController>(true);
        _carasActuales = carasCorriendo;
        _carasEnBucle = true;

        _animator = GetComponentInChildren<Animator>(true);
        if (!_animator)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[LoadingShowcaseCharacter] {name}: no se encontró Animator en la jerarquía.");
#endif
            return;
        }
        _animator.applyRootMotion = false;
        _animator.SetInteger(HashGestureIndex, gestureIndex);

        transform.localRotation = Quaternion.Euler(0f, runYaw, 0f);
    }

    void ApplyRenderLayerRecursive()
    {
        int layer = LayerMask.NameToLayer(renderLayerName);
        if (layer < 0)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[LoadingShowcaseCharacter] {name}: la layer '{renderLayerName}' no existe en el proyecto.");
#endif
            return;
        }
        SetLayerRecursive(transform, layer);
    }

    static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }

    /// <summary>Vuelve a correr en el sitio mirando a la derecha. Llamado al mostrar la pantalla de carga.</summary>
    public void ResetToRunning()
    {
        if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
        transform.localRotation = Quaternion.Euler(0f, runYaw, 0f);
        if (_animator)
        {
            _animator.Rebind();
            _animator.Update(0f);
            _animator.SetInteger(HashGestureIndex, gestureIndex);
        }
        EmpezarCaras(carasCorriendo, true);
    }

    /// <summary>Se gira hacia cámara y dispara su gesto gracioso.</summary>
    public void PlayReveal()
    {
        if (_animator) _animator.SetTrigger(HashReveal);
        if (carasDelGesto != null && carasDelGesto.Length > 0) EmpezarCaras(carasDelGesto, false);
        if (_turnRoutine != null) StopCoroutine(_turnRoutine);
        _turnRoutine = StartCoroutine(TurnToCamera());
    }

    // La primera cara se pone en el primer Update y no en Awake: el Start del NPCEmotionController
    // vuelve a la cara de reposo y la pisaría.
    void EmpezarCaras(CaraDurante[] caras, bool enBucle)
    {
        _carasActuales = caras;
        _carasEnBucle = enBucle;
        _indiceDeCara = -1;
        _proximaCara = 0f;
        if (_cara && (caras == null || caras.Length == 0)) _cara.VolverAReposo();
    }

    void Update()
    {
        if (!_cara || _carasActuales == null || _carasActuales.Length == 0) return;
        float ahora = Time.unscaledTime;
        if (_indiceDeCara >= 0 && ahora < _proximaCara) return;

        int siguiente = _indiceDeCara + 1;
        if (siguiente >= _carasActuales.Length)
        {
            if (!_carasEnBucle) { _proximaCara = float.PositiveInfinity; return; }
            siguiente = 0;
        }
        _indiceDeCara = siguiente;
        var cara = _carasActuales[siguiente];
        _proximaCara = ahora + Mathf.Max(0.05f, cara.segundos);
        _cara.SetEmotion(cara.emocion);
    }

    IEnumerator TurnToCamera()
    {
        Quaternion from = transform.localRotation;
        Quaternion to = Quaternion.Euler(0f, revealYaw, 0f);
        float t = 0f;
        while (t < turnDuration)
        {
            t += Time.unscaledDeltaTime;
            transform.localRotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / turnDuration));
            yield return null;
        }
        transform.localRotation = to;
        _turnRoutine = null;
    }
}
