#if UNITY_EDITOR
using UnityEngine;

/// Sonda de diagnóstico (solo Editor): apunta cómo están el cuello y la cabeza de un personaje humanoide
/// en un momento del LateUpdate. Esta, al principio: la pose tal cual la deja el Animator (tras
/// GiroSobreLaAnimacion, que deshace un giro del fotograma anterior si el Animator no la ha escrito).
/// SondaDeCabezaTrasQuieta (justo después de CabezaQuietaAlHablar) y SondaDeCabezaFinal (la última)
/// apuntan lo mismo más tarde; la diferencia entre ellas dice qué parte del giro pone cada sistema.
/// Las pone y las quita el registro «Quién mueve la cabeza» (Editor/Personajes/SondaDeCabezaMenu); no se
/// guardan en escenas ni prefabs.
[DefaultExecutionOrder(-31990)]
public class SondaDeCabeza : MonoBehaviour
{
    public Transform Cuello { get; private set; }
    public Transform Cabeza { get; private set; }
    public Animator Animador { get; private set; }
    public Quaternion CuelloLocal { get; private set; } = Quaternion.identity;
    public Quaternion CabezaLocal { get; private set; } = Quaternion.identity;

    protected void Awake()
    {
        Animador = GetComponent<Animator>();
        if (Animador == null || !Animador.isHuman) return;
        Cuello = Animador.GetBoneTransform(HumanBodyBones.Neck);
        Cabeza = Animador.GetBoneTransform(HumanBodyBones.Head);
    }

    protected virtual void LateUpdate()
    {
        if (Cabeza == null) return;
        CabezaLocal = Cabeza.localRotation;
        if (Cuello != null) CuelloLocal = Cuello.localRotation;
    }
}
#endif
