using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Ajustes del contorno de tinta (borde suave de siluetas y pliegues) que dibuja
/// <see cref="ContornoDeTintaFeature"/>. Sus valores por defecto son el look del juego: rige en
/// todas las escenas aunque ningún perfil lo mencione, y un perfil o un PostprocesoBeat lo
/// cambia o lo apaga (intensidad 0) igual que cualquier otro efecto de Volume. Ver INC-865.
[Serializable, VolumeComponentMenu("El Sendero/Contorno de tinta")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class ContornoDeTinta : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Cuánto se oscurece el borde. 0 apaga el efecto y no cuesta nada.")]
    public ClampedFloatParameter intensidad = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Tooltip("Color por el que se multiplica lo que hay debajo del borde. Más claro = línea más suave.")]
    public ColorParameter tinta = new ColorParameter(new Color(0.36f, 0.38f, 0.5f, 1f), false, false, true);

    [Tooltip("Grosor de la línea en píxeles a 1080p; se escala con la resolución.")]
    public ClampedFloatParameter grosor = new ClampedFloatParameter(1f, 1f, 3f);

    [Tooltip("Salto de profundidad, relativo a la distancia, a partir del cual hay silueta. Más alto = menos líneas.")]
    public ClampedFloatParameter umbralDeSilueta = new ClampedFloatParameter(0.06f, 0.005f, 0.5f);

    [Tooltip("Cambio de orientación de la superficie a partir del cual se marca un pliegue. Más alto = menos líneas.")]
    public ClampedFloatParameter umbralDePliegue = new ClampedFloatParameter(0.45f, 0.05f, 1f);

    [Tooltip("Peso de los pliegues frente a las siluetas.")]
    public ClampedFloatParameter pesoDePliegues = new ClampedFloatParameter(0.55f, 0f, 1f);

    [Tooltip("Distancia (m) a la que el contorno empieza a desvanecerse.")]
    public MinFloatParameter inicioDelDesvanecido = new MinFloatParameter(22f, 0f);

    [Tooltip("Distancia (m) a la que el contorno ya no se ve.")]
    public MinFloatParameter finDelDesvanecido = new MinFloatParameter(60f, 0f);

    public bool IsActive() => intensidad.value > 0f;
}
