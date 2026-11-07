using UnityEngine;

/// Endereza a cualquier personaje sentado. Las animaciones de sentado (SitGround, SitLow, SitMedium,
/// SitHigh: una distinta según el asiento) están hechas para cuerpos de proporciones normales; con
/// la cabeza grande de los chibis la espalda curvada y la cabeza gacha se leen como «encorvados».
///
/// Después de la animación (LateUpdate) lleva el tronco y la cabeza hacia la vertical una parte.
/// Al ser «hacia la vertical» y no un giro fijo, la corrección se adapta sola a cada animación de
/// sentado: la que ya va derecha apenas se toca. Entra y sale con el Begin y el Exit del sentado,
/// así que no hay tirón al sentarse ni al levantarse.
///
/// Lo añaden solos NPCSimpleAnimator (todos los NPCs) y PlayerAmbientActivityHandler (Will): vale
/// en juego y en cinemáticas, sin que nadie tenga que acordarse. Antes solo lo hacía la cinemática
/// final, y solo con la cabeza y dos de los cuatro asientos (INC-866).
[DisallowMultipleComponent]
public sealed class PosturaDeSentado : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Cuánto se endereza la espalda (0 = como la animación, 1 = vertical).")]
    private float espalda = 0.45f;
    [SerializeField, Range(0f, 1f), Tooltip("Cuánto se levanta la cabeza hacia el frente.")]
    private float cabeza = 0.7f;

    private static readonly int[] Sentados =
    {
        Animator.StringToHash("SitGround_Loop"), Animator.StringToHash("SitLow_Loop"),
        Animator.StringToHash("SitMedium_Loop"), Animator.StringToHash("SitHigh_Loop"),
    };
    private static readonly int[] Sentandose =
    {
        Animator.StringToHash("SitGround_Begin"), Animator.StringToHash("SitLow_Begin"),
        Animator.StringToHash("SitMedium_Begin"), Animator.StringToHash("SitHigh_Begin"),
    };
    private static readonly int[] Levantandose =
    {
        Animator.StringToHash("SitGround_Exit"), Animator.StringToHash("SitLow_Exit"),
        Animator.StringToHash("SitMedium_Exit"), Animator.StringToHash("SitHigh_Exit"),
    };

    private Animator _animador;
    private Transform _cadera, _espalda, _pecho, _cuello, _cabeza;
    private bool _preparado;

    /// Se lo pone a quien no lo tenga. Barato: si el personaje no se sienta, no hace nada.
    public static void Asegurar(GameObject quien)
    {
        if (quien != null && quien.GetComponent<PosturaDeSentado>() == null) quien.AddComponent<PosturaDeSentado>();
    }

    private void Preparar()
    {
        _preparado = true;
        _animador = GetComponentInChildren<Animator>(true);
        if (_animador == null || !_animador.isHuman) { _animador = null; return; }
        _cadera = _animador.GetBoneTransform(HumanBodyBones.Hips);
        _espalda = _animador.GetBoneTransform(HumanBodyBones.Spine);
        _pecho = _animador.GetBoneTransform(HumanBodyBones.UpperChest) ?? _animador.GetBoneTransform(HumanBodyBones.Chest);
        _cuello = _animador.GetBoneTransform(HumanBodyBones.Neck);
        _cabeza = _animador.GetBoneTransform(HumanBodyBones.Head);
    }

    private void LateUpdate()
    {
        if (!_preparado) Preparar();
        if (_animador == null || !_animador.isActiveAndEnabled || _animador.runtimeAnimatorController == null) return;

        float peso = PesoDeSentado();
        if (peso <= 0f) return;

        // Tronco: de la cadera al cuello. Se reparte entre la espalda y el pecho para que se curve
        // como una columna y no gire de golpe en un solo hueso.
        if (_cadera != null && _cuello != null && espalda > 0f)
        {
            Quaternion giro = HaciaLaVertical(_cuello.position - _cadera.position, espalda * peso);
            if (_espalda != null && _pecho != null && _pecho != _espalda)
            {
                Quaternion mitad = Quaternion.Slerp(Quaternion.identity, giro, 0.5f);
                _espalda.rotation = mitad * _espalda.rotation;
                _pecho.rotation = mitad * _pecho.rotation;
            }
            else if (_espalda != null) _espalda.rotation = giro * _espalda.rotation;
        }

        // Cabeza: el eje cuello → cabeza hacia la vertical, repartido entre los dos huesos.
        if (_cuello != null && _cabeza != null && cabeza > 0f)
        {
            Quaternion giro = HaciaLaVertical(_cabeza.position - _cuello.position, cabeza * peso);
            Quaternion mitad = Quaternion.Slerp(Quaternion.identity, giro, 0.5f);
            _cuello.rotation = mitad * _cuello.rotation;
            _cabeza.rotation = mitad * _cabeza.rotation;
        }
    }

    /// 1 sentado del todo, entre 0 y 1 mientras se sienta o se levanta, 0 de pie.
    private float PesoDeSentado()
    {
        var info = _animador.GetCurrentAnimatorStateInfo(0);
        float peso = PesoDe(info);
        if (_animador.IsInTransition(0))
        {
            float t = _animador.GetAnimatorTransitionInfo(0).normalizedTime;
            peso = Mathf.Lerp(peso, PesoDe(_animador.GetNextAnimatorStateInfo(0)), t);
        }
        return peso;
    }

    private static float PesoDe(AnimatorStateInfo info)
    {
        int h = info.shortNameHash;
        if (Contiene(Sentados, h)) return 1f;
        if (Contiene(Sentandose, h)) return Mathf.Clamp01(info.normalizedTime);
        if (Contiene(Levantandose, h)) return 1f - Mathf.Clamp01(info.normalizedTime);
        return 0f;
    }

    private static bool Contiene(int[] lista, int h)
    {
        for (int i = 0; i < lista.Length; i++) if (lista[i] == h) return true;
        return false;
    }

    private static Quaternion HaciaLaVertical(Vector3 eje, float cuanto)
    {
        if (eje.sqrMagnitude < 1e-6f) return Quaternion.identity;
        return Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(eje.normalized, Vector3.up), cuanto);
    }
}
