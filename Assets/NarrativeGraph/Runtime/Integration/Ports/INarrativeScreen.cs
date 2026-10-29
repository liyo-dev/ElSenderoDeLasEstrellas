using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// La pantalla completa: fundidos de color y el «telón», que la mantiene tapada mientras alguien
/// lo pida (una carga, un rótulo) aunque otro sistema quiera destaparla.
/// </summary>
public interface INarrativeScreen
{
    /// <summary>true si la pantalla está tapada por un fundido.</summary>
    bool IsCovered { get; }

    /// <summary>Tapa la pantalla con <paramref name="color"/> (cover = true) o la destapa (cover = false).</summary>
    IEnumerator Fade(Color color, float seconds, bool cover);

    /// <summary>Tapa con un color opaco o destapa con <see cref="Color.clear"/>, al instante.</summary>
    void SetImmediate(Color color);

    /// <summary>
    /// Destapa con una transición propia del juego (<paramref name="transition"/> es un asset que el
    /// juego sabe interpretar). Antes pasa a <paramref name="color"/> en <paramref name="recolorSeconds"/>
    /// si estaba tapada de otro color. Llama a <paramref name="onUncovered"/> en cuanto el fundido
    /// deja de tapar (en el corte de la transición, o al terminar si se destapa con fundido normal
    /// porque la transición no está disponible). La rutina acaba cuando la pantalla está destapada.
    /// </summary>
    IEnumerator UncoverWithTransition(Color color, float recolorSeconds, ScriptableObject transition, Action onUncovered);

    /// <summary>Cierra el telón a nombre de <paramref name="key"/>.</summary>
    void CloseCurtain(string key);

    /// <summary>Si el telón ya está cerrado, lo mantiene cerrado también a nombre de <paramref name="key"/>; si no, no hace nada.</summary>
    void KeepCurtainClosed(string key);

    /// <summary>Retira la petición de <paramref name="key"/>; el telón se abre cuando no queda ninguna.</summary>
    void ReleaseCurtain(string key);
}
