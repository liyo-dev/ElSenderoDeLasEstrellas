using UnityEngine;

/// <summary>
/// Marca decorado fino que cuelga sobre la escena (guirnaldas de banderines, cuerdas, cables) para
/// que el horneado de cámaras del guion no lo cuente como algo que tapa el plano: su caja es enorme,
/// pero lo que se ve es una línea fina. Vale para el objeto y todo lo que cuelga de él. No hace nada en juego.
/// </summary>
[DisallowMultipleComponent]
public class NoTapaLaCamara : MonoBehaviour { }
