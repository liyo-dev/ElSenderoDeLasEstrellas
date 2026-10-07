using UnityEngine;

/// Enciende de noche las ventanas, vidrieras y faroles pintados en las texturas del mundo.
///
/// Cada material de la lista lleva en `_EmissionMap` una máscara de sus cristales (negro = no
/// brilla; ver Assets/Art/Noche) y la palabra clave `_EMISSION` activada. Este componente sube su
/// `_EmissionColor` con `DayNightCycle.NocheActual`, así que todas las casas y faroles que usan
/// esos materiales se encienden a la vez sin tocar cada objeto. Fuera de Play y al apagarse deja
/// la emisión en negro: el asset del material queda como estaba.
///
/// Los materiales y las máscaras los prepara «El Sendero/Mundo/Noche: luces de casas, faroles y
/// luciérnagas». Ver TDD § 16 (Parte D) e INC-657.
[DisallowMultipleComponent]
public sealed class VentanasIluminadas : MonoBehaviour
{
    [Tooltip("Materiales compartidos con la máscara de ventanas en _EmissionMap y _EMISSION activada.")]
    public Material[] materiales;

    [Tooltip("Luz de las ventanas a plena noche. La máscara ya trae el tono cálido (y el color de las vidrieras); por encima de 1 brilla con el Bloom.")]
    [ColorUsage(false, true)] public Color colorDeNoche = new Color(2.2f, 2.0f, 1.8f);

    [Tooltip("Segundos que tarda la emisión en alcanzar a la hora del ciclo (suaviza los saltos de hora inmediatos).")]
    [Min(0.01f)] public float suavizado = 1.5f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private float _nivel;
    private float _aplicado = -1f;

    private void OnEnable()
    {
        _nivel = DayNightCycle.NocheActual;
        _aplicado = -1f;
        Aplicar(_nivel);
    }

    private void Update()
    {
        _nivel = Mathf.MoveTowards(_nivel, DayNightCycle.NocheActual, Time.deltaTime / suavizado);
        if (Mathf.Abs(_nivel - _aplicado) > 0.002f || (_nivel == 0f) != (_aplicado == 0f))
            Aplicar(_nivel);
    }

    private void OnDisable() => Aplicar(0f);

    private void Aplicar(float nivel)
    {
        _aplicado = nivel;
        if (materiales == null) return;
        // Curva suave: las ventanas apenas se notan al empezar a anochecer y lucen del todo de noche.
        Color color = colorDeNoche * (nivel * nivel * (3f - 2f * nivel));
        color.a = 1f;
        foreach (var material in materiales)
            if (material != null) material.SetColor(EmissionColorId, color);
    }
}
