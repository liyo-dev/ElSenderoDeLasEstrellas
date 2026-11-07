using UnityEngine;

/// <summary>
/// <see cref="IAnimacionDeCorredor"/> para cualquier modelo con un Animator de locomoción: una
/// velocidad (float), un salto (trigger) y trepar (bool), con los nombres de sus parámetros como
/// datos. Si el corredor va más rápido que su clip de carrera, acelera la animación para que no
/// patine. Los parámetros que el controlador no tenga se ignoran. Los parámetros se resuelven la
/// primera vez que hacen falta, así que también funciona con el componente apagado (el storyboard
/// del horneado de guiones lo usa sobre copias sin scripts activos).
/// </summary>
public sealed class AnimacionDeCorredorPorAnimator : MonoBehaviour, IAnimacionDeCorredor
{
    [SerializeField] private Animator animator;
    [Tooltip("Parámetro float de la velocidad de locomoción.")]
    [SerializeField] private string parametroDeVelocidad = "Speed";
    [Tooltip("Valor del parámetro de velocidad con el que el modelo corre.")]
    [SerializeField, Min(0.01f)] private float valorAlCorrer = 2f;
    [Tooltip("Metros por segundo a los que su clip de carrera no patina.")]
    [SerializeField, Min(0.1f)] private float metrosPorSegundoDelClip = 2f;
    [Tooltip("Acelera la animación cuando el corredor va más rápido que su clip.")]
    [SerializeField] private bool acelerarLaAnimacion = true;
    [Tooltip("Trigger del salto.")]
    [SerializeField] private string disparadorDeSalto = "Jump";
    [Tooltip("Bool de trepar.")]
    [SerializeField] private string parametroDeTrepar = "ClimbUp";

    private int _velocidad, _salto, _trepar;
    private bool _hayVelocidad, _haySalto, _hayTrepar;
    private bool _resuelto;

    private void Awake() => Resolver();

    /// <summary>Busca el Animator y los parámetros que tiene su controlador (una vez).</summary>
    private bool Resolver()
    {
        if (_resuelto) return animator != null;
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        _resuelto = true;
        _velocidad = Animator.StringToHash(parametroDeVelocidad);
        _salto = Animator.StringToHash(disparadorDeSalto);
        _trepar = Animator.StringToHash(parametroDeTrepar);
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == _velocidad && p.type == AnimatorControllerParameterType.Float) _hayVelocidad = true;
            else if (p.nameHash == _salto && p.type == AnimatorControllerParameterType.Trigger) _haySalto = true;
            else if (p.nameHash == _trepar && p.type == AnimatorControllerParameterType.Bool) _hayTrepar = true;
        }
        return true;
    }

    public void Avanzar(float metrosPorSegundo, bool alInstante = false)
    {
        if (!Resolver()) return;
        float proporcion = metrosPorSegundo / metrosPorSegundoDelClip;
        float valor = Mathf.Min(proporcion, 1f) * valorAlCorrer;
        if (_hayVelocidad)
        {
            if (alInstante) animator.SetFloat(_velocidad, valor);
            else animator.SetFloat(_velocidad, valor, 0.1f, Time.deltaTime);
        }
        animator.speed = acelerarLaAnimacion ? Mathf.Max(1f, proporcion) : 1f;
    }

    public void Saltar()
    {
        if (Resolver() && _haySalto) animator.SetTrigger(_salto);
    }

    public void Trepar(bool trepando)
    {
        if (!Resolver()) return;
        if (_hayTrepar) animator.SetBool(_trepar, trepando);
        if (trepando) animator.speed = 1f;
    }
}
