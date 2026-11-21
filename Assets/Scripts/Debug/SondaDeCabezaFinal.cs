#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Sonda de diagnóstico (solo Editor): la pose del cuello y la cabeza al final del LateUpdate, con todo lo
/// que la mueve por código ya aplicado. Compara con las otras dos sondas del mismo personaje y con la pose de
/// reposo del avatar, y guarda cada fotograma cuánto gira la cabeza por cada fuente:
///   clip    pose del Animator frente al reposo del avatar;
///   quieta  lo que cambia CabezaQuietaAlHablar;
///   resto   lo que cambia el código que corre después (mirada, sentado, gestos…);
///   total   pose final frente al reposo (y lo mismo del cuello).
/// Ver SondaDeCabeza.
[DefaultExecutionOrder(32000)]
public sealed class SondaDeCabezaFinal : SondaDeCabeza
{
    public struct Muestra
    {
        public float tiempo, clip, quieta, resto, total, cuello;
        public string estados;
    }

    /// Lo medido; vive fuera del componente, así que sigue ahí si el personaje se destruye antes de escribir el informe.
    public sealed class Datos
    {
        public readonly List<Muestra> Registro = new();
        public Muestra Peor, PeorClip, PeorQuieta, PeorResto;
        public bool Medido;
    }

    private const float CadaCuanto = 0.25f;   // una muestra del registro cada tanto (s)

    public readonly Datos D = new();

    private SondaDeCabeza _animada, _media;
    private Quaternion _reposoCabeza = Quaternion.identity, _reposoCuello = Quaternion.identity;
    private float _siguiente;

    private void Start()
    {
        foreach (var s in GetComponents<SondaDeCabeza>())
        {
            if (s is SondaDeCabezaFinal) continue;
            if (s is SondaDeCabezaTrasQuieta) _media = s; else _animada = s;
        }
        if (Animador == null || Animador.avatar == null || Cabeza == null) return;
        foreach (var hueso in Animador.avatar.humanDescription.skeleton)
        {
            if (hueso.name == Cabeza.name) _reposoCabeza = hueso.rotation;
            if (Cuello != null && hueso.name == Cuello.name) _reposoCuello = hueso.rotation;
        }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (Cabeza == null || _animada == null || _media == null) return;
        var m = new Muestra
        {
            tiempo = Time.time,
            clip = Quaternion.Angle(_reposoCabeza, _animada.CabezaLocal),
            quieta = Quaternion.Angle(_animada.CabezaLocal, _media.CabezaLocal),
            resto = Quaternion.Angle(_media.CabezaLocal, CabezaLocal),
            total = Quaternion.Angle(_reposoCabeza, CabezaLocal),
            cuello = Quaternion.Angle(_reposoCuello, CuelloLocal),
        };
        bool guardar = Time.time >= _siguiente;
        bool nuevo = !D.Medido;
        bool peor = nuevo || m.total > D.Peor.total || m.clip > D.PeorClip.clip || m.quieta > D.PeorQuieta.quieta || m.resto > D.PeorResto.resto;
        if (guardar || peor) m.estados = Estados();
        if (nuevo || m.total > D.Peor.total) D.Peor = m;
        if (nuevo || m.clip > D.PeorClip.clip) D.PeorClip = m;
        if (nuevo || m.quieta > D.PeorQuieta.quieta) D.PeorQuieta = m;
        if (nuevo || m.resto > D.PeorResto.resto) D.PeorResto = m;
        D.Medido = true;
        if (guardar) { D.Registro.Add(m); _siguiente = Time.time + CadaCuanto; }
    }

    private string Estados()
    {
        if (Animador == null) return "";
        var sb = new StringBuilder();
        for (int i = 0; i < Animador.layerCount; i++)
        {
            if (i > 0 && Animador.GetLayerWeight(i) <= 0.01f) continue;
            var clips = Animador.GetCurrentAnimatorClipInfo(i);
            if (clips.Length > 0 && clips[0].clip != null) sb.Append(Animador.GetLayerName(i)).Append(':').Append(clips[0].clip.name).Append(' ');
        }
        return sb.ToString().TrimEnd();
    }
}
#endif
