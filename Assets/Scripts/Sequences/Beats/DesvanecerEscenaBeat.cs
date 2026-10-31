using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Hace desaparecer una escena cargada pieza a pieza, de lejos a cerca de un actor o marca
/// (DesvanecerEscena): luces que se apagan una a una y objetos que se deshacen en polvo de luz.
/// Es la ruptura del Sendero, cuando el parque se esfuma mientras discuten.
[Serializable]
public class DesvanecerEscenaBeat : SequenceBeat
{
    [Tooltip("Nombre de la escena que desaparece (cargada en aditivo).")]
    public string escena;
    [Tooltip("Actor alrededor del que se desvanece (lo último en irse es lo que tiene más cerca).")]
    public string centroActorId;
    [Tooltip("Marca que hace de centro si no hay actor.")]
    public string centroMarca;
    [Tooltip("Segundos desde la primera pieza hasta la última.")]
    [Min(0.5f)] public float duracion = 12f;
    [Tooltip("Efecto de un solo uso que sale en cada pieza que se deshace.")]
    public GameObject vfxPrefab;
    [Tooltip("Descargar la escena al terminar.")]
    public bool descargar = true;
    [Tooltip("Esperar a que acabe antes de seguir con la secuencia.")]
    public bool esperar = false;

    public override string Describe() => $"Desvanecer escena: {escena} ({duracion:0.#} s)";
    public override bool CambiaElEscenario => true;

    public override IEnumerator Run(SequenceContext ctx)
    {
        var s = SceneManager.GetSceneByName(escena);
        if (!s.IsValid() || !s.isLoaded)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning($"[DesvanecerEscenaBeat] La escena '{escena}' no está cargada.");
#endif
            yield break;
        }
        VfxBeat.TryResolvePoint(ctx, centroActorId, centroMarca, out Vector3 centro, out _);
        var d = DesvanecerEscena.Empezar(s, centro, duracion, vfxPrefab, descargar);
        if (!esperar) yield break;
        while (d != null && !d.Terminado) yield return null;
    }
}
