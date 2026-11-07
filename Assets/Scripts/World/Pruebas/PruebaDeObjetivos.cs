using UnityEngine;

/// <summary>
/// Prueba de conseguir unos objetivos antes de que se acabe el tiempo (reventar dianas, encender
/// cristales…). Cuenta con el contador oficial (<see cref="ActivationCounter"/>): cada objetivo le
/// suma desde su propio evento y la prueba se gana cuando llega a lo que pide. Al empezar cada
/// intento se reinicia el contador (su <c>onReset</c> repone los objetivos). Mientras corre, el
/// rótulo breve enseña lo conseguido y el tiempo que queda.
/// </summary>
public sealed class PruebaDeObjetivos : PruebaDelJugador
{
    [Header("Objetivos")]
    [Tooltip("Contador al que suma cada objetivo; se gana al completarlo.")]
    [SerializeField] private ActivationCounter contador;
    [Tooltip("Segundos para conseguirlo (0 = sin límite).")]
    [SerializeField, Min(0f)] private float tiempoLimite = 40f;

    [Header("Marcador")]
    [Tooltip("Formato del marcador ({0} = conseguidos, {1} = necesarios, {2} = segundos que quedan). Vacío = sin marcador.")]
    [SerializeField] private string formatoDelMarcador = "{0}/{1} · {2}";

    private int _contados = -1;
    private int _segundosVistos = -1;

    protected override void PrepararIntento()
    {
        if (contador != null) contador.Reset();
        _contados = -1;
        _segundosVistos = -1;
    }

    protected override void Arrancar()
    {
        if (contador == null) return;
        contador.Reset();
        contador.onRequirementMet.AddListener(AlCompletar);
    }

    protected override void Detener()
    {
        if (contador != null) contador.onRequirementMet.RemoveListener(AlCompletar);
    }

    protected override void Actualizar(float dt)
    {
        if (contador == null) return;
        if (contador.IsComplete) { Ganar(); return; }
        int quedan = tiempoLimite > 0f ? Mathf.CeilToInt(tiempoLimite - TiempoEnCurso) : 0;
        if (tiempoLimite > 0f && quedan <= 0) { Fallar(); return; }

        if (string.IsNullOrEmpty(formatoDelMarcador)) return;
        if (contador.CurrentCount == _contados && quedan == _segundosVistos) return;
        _contados = contador.CurrentCount;
        _segundosVistos = quedan;
        HudToastService.Instance?.ShowRaw(string.Format(formatoDelMarcador, _contados, contador.RequiredCount, quedan), 1.2f);
    }

    private void AlCompletar() => Ganar();
}
