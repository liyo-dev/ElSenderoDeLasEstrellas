using UnityEngine;

/// <summary>
/// Zona (trigger) donde la magia del jugador sale de otra forma mientras está dentro: la que
/// describe su <see cref="TransformacionDeMagia"/> (p. ej. purpurina y corazoncitos que no hacen
/// daño). Se apunta en <see cref="MagicCaster"/>, el único punto de salida de los hechizos del
/// jugador, y se suelta al salir o al desactivarse. El hechizo no sabe nada de la zona.
/// </summary>
public sealed class ZonaQueTransformaLaMagia : ZonaDelJugador
{
    [Tooltip("Cómo sale la magia dentro de la zona.")]
    [SerializeField] private TransformacionDeMagia transformacion;

    public TransformacionDeMagia Transformacion => transformacion;

    protected override void AlEntrar(PlayerActionManager jugador)
    {
        if (transformacion == null) return;
        var lanzador = jugador.GetComponentInChildren<MagicCaster>(true);
        if (lanzador != null) lanzador.AnadirTransformacion(transformacion, this);
    }

    protected override void AlSalir(PlayerActionManager jugador)
    {
        var lanzador = jugador.GetComponentInChildren<MagicCaster>(true);
        if (lanzador != null) lanzador.QuitarTransformacion(this);
    }
}
