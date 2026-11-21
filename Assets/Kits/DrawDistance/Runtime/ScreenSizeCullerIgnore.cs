using UnityEngine;

namespace Kits.DrawDistance
{
    /// <summary>
    /// Marca: las piezas de este objeto y de sus hijos se dibujan siempre, aunque se vean diminutas
    /// (<see cref="ScreenSizeCuller"/> no las toca). Para lo que deba leerse de lejos aunque sea pequeño.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenSizeCullerIgnore : MonoBehaviour
    {
    }
}
