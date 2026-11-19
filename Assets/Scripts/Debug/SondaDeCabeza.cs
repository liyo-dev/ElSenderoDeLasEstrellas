#if UNITY_EDITOR
using UnityEngine;

/// Sonda de diagnóstico (solo Editor): apunta cómo están el cuello y la cabeza de un personaje humanoide
/// en un momento del LateUpdate. Esta, la primera de todas: la pose tal cual la deja el Animator.
/// SondaDeCabezaTrasQuieta (justo después de CabezaQuietaAlHablar) y SondaDeCabezaFinal (la última)
/// apuntan lo mismo más tarde; la diferencia entre ellas dice qué parte del giro pone cada sistema.
/// Las pone y las quita el menú «Quién mueve la cabeza» (Editor/Personajes/SondaDeCabezaMenu); no se
/// guardan en escenas ni prefabs.
[DefaultExecutionOrder(-32000)]
public class SondaDeCabeza : MonoBehaviour
{
    public Transform Cuello { get; private set; }
    public Transform Cabeza { get; private set; }
    public Quaternion CuelloLocal { get; private set; } = Quaternion.identity;
    public Quaternion CabezaLocal { get; private set; } = Quaternion.identity;
    public int Medidas { get; private set; }

    protected void Awake()
    {
        var animador = GetComponent<Animator>();
        if (animador == null || !animador.isHuman) return;
        Cuello = animador.GetBoneTransform(HumanBodyBones.Neck);
        Cabeza = animador.GetBoneTransform(HumanBodyBones.Head);
    }

    protected void LateUpdate()
    {
        if (Cabeza == null) return;
        CabezaLocal = Cabeza.localRotation;
        if (Cuello != null) CuelloLocal = Cuello.localRotation;
        Medidas++;
    }
}
#endif
