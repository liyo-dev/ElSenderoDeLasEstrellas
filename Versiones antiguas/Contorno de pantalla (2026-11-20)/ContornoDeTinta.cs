using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Ajustes del contorno de tinta (línea oscura de siluetas y pliegues) que dibuja
/// <see cref="ContornoDeTintaFeature"/>. Sus valores por defecto son el look del juego: rige en
/// todas las escenas aunque ningún perfil lo mencione, y un perfil o un PostprocesoBeat lo
/// cambia o lo apaga (intensidad 0) igual que cualquier otro efecto de Volume. Ver INC-865.
[Serializable, VolumeComponentMenu("El Sendero/Contorno de tinta")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class ContornoDeTinta : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Cuánto se oscurece el borde. 0 apaga el efecto y no cuesta nada.")]
    public ClampedFloatParameter intensidad = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Color por el que se multiplica lo que hay debajo del borde. Más claro = línea más suave.")]
    public ColorParameter tinta = new ColorParameter(new Color(0.20f, 0.15f, 0.22f, 1f), false, false, true);

    [Tooltip("Grosor de la línea en píxeles a 1080p; se escala con la resolución.")]
    public ClampedFloatParameter grosor = new ClampedFloatParameter(2f, 1f, 3f);

    [Tooltip("Salto de profundidad, relativo a la distancia, a partir del cual hay silueta. Más alto = menos líneas.")]
    public ClampedFloatParameter umbralDeSilueta = new ClampedFloatParameter(0.06f, 0.005f, 0.5f);

    [Tooltip("Cambio de orientación de la superficie a partir del cual se marca un pliegue. Más alto = menos líneas.")]
    public ClampedFloatParameter umbralDePliegue = new ClampedFloatParameter(0.45f, 0.05f, 1f);

    [Tooltip("Suavidad del borde de la línea: 1 = se aclara poco a poco (trazo difuso), 0,1 = trazo neto de tinta.")]
    public ClampedFloatParameter suavidadDelTrazo = new ClampedFloatParameter(1f, 0.1f, 1f);

    [Tooltip("Peso de los pliegues frente a las siluetas.")]
    public ClampedFloatParameter pesoDePliegues = new ClampedFloatParameter(0.55f, 0f, 1f);

    [Tooltip("Escalón (m) entre dos capas casi pegadas (peto sobre blusa, falda, hombreras) a partir del cual se marca su borde con una línea fija. Más alto = menos líneas.")]
    public ClampedFloatParameter umbralDeCapa = new ClampedFloatParameter(0.0015f, 0.0005f, 0.02f);

    [Tooltip("Peso de la línea de los bordes de capa (0 = sin ella). Solo de cerca: se va entre 8 y 15 m.")]
    public ClampedFloatParameter pesoDeCapas = new ClampedFloatParameter(0.55f, 0f, 1f);

    [Tooltip("Distancia (m) a la que el contorno empieza a desvanecerse.")]
    public MinFloatParameter inicioDelDesvanecido = new MinFloatParameter(22f, 0f);

    [Tooltip("Distancia (m) a la que el contorno ya no se ve.")]
    public MinFloatParameter finDelDesvanecido = new MinFloatParameter(60f, 0f);

    public bool IsActive() => intensidad.value > 0f;
}
