using UnityEngine;

/// Las cuentas de tiempo que comparten el reproductor de guiones y los beats de estado. Están aquí,
/// en runtime y separadas, para poder probarlas sin montar una escena ni un AudioSource. Ver
/// INC-1234 y TDD § 34.
public static class TiempoDeGuion
{
    /// Dónde arranca una música al saltar a un segundo: lo que ya llevaba la pista desde que se
    /// pidió (si se pidió con un segundo concreto) más lo que ha pasado desde que empezó el efecto.
    /// Un «desdeSegundo» negativo significa «desde el principio de la pista».
    public static float DesplazamientoDeMusica(float desdeSegundo, float segundoActual, float tiempoDelEfecto)
        => Mathf.Max(0f, desdeSegundo) + Mathf.Max(0f, segundoActual - tiempoDelEfecto);

    /// Dónde arranca una frase al entrar en ella por la mitad: los segundos que ya lleva dichos.
    public static float DesplazamientoDeLinea(float segundoActual, float t0)
        => Mathf.Max(0f, segundoActual - t0);

    /// Lo que le queda a una frase desde el segundo actual, con el margen que usa el presentador.
    public static float DuracionRestanteDeLinea(float segundoActual, float t0, float t1, float margen = 0.35f)
        => Mathf.Max(0.05f, t1 - Mathf.Max(segundoActual, t0)) + margen;
}
