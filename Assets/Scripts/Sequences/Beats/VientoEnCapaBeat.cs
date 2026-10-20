using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica un efecto procedural de viento sobre los huesos de la capa de un actor (p. ej. el Archimago o Will)
/// durante una secuencia narrativa. Ideal para momentos de acumulación de poder mágico, levitación o tormentas.
/// </summary>
[Serializable]
public class VientoEnCapaBeat : SequenceBeat
{
    [Tooltip("ID o alias del actor (p. ej. 'archimago' o 'NPC_Archimago').")]
    public string actor = "archimago";
    public string actorId = "archimago";

    [Tooltip("Activa o desactiva el viento sobre la capa.")]
    public bool activo = true;

    [Tooltip("Intensidad del viento (1.0 = valor normal de conjuro, mayor = vendaval místico).")]
    [Range(0f, 3f)]
    public float intensidad = 1.0f;

    [Tooltip("Ángulo base de elevación de la capa hacia atrás en grados.")]
    [Range(5f, 75f)]
    public float anguloElevacion = 32f;

    [Tooltip("Frecuencia de la ondulación/aleteo de la tela.")]
    [Range(1f, 20f)]
    public float frecuenciaOndulacion = 8.5f;

    [Tooltip("Duración en segundos si es temporal (0 = permanece hasta orden contraria o fin de secuencia).")]
    public float duracion = 0f;

    public override string Describe()
    {
        string objetivo = !string.IsNullOrEmpty(actor) ? actor : actorId;
        return activo
            ? $"Viento en capa: {objetivo} (intensidad={intensidad:F1}, elevación={anguloElevacion:F0}°)"
            : $"Viento en capa: apagar en {objetivo}";
    }

    public override IEnumerator Run(SequenceContext ctx)
    {
        string objetivo = !string.IsNullOrEmpty(actor) ? actor : actorId;
        var act = ctx?.GetActor(objetivo);
        if (act?.Transform == null) yield break;

        var comp = act.Transform.GetComponent<VientoEnCapaActor>();
        if (comp == null && activo)
            comp = act.Transform.gameObject.AddComponent<VientoEnCapaActor>();

        if (comp != null)
        {
            comp.Configurar(activo, intensidad, anguloElevacion, frecuenciaOndulacion, duracion);
            ctx.Player?.RegisterCleanup(() =>
            {
                if (comp != null) comp.DesactivarInmediato();
            });
        }

        yield break;
    }
}

/// <summary>
/// Componente de soporte que manipula proceduralmente en LateUpdate los huesos de capa del actor.
/// </summary>
[DisallowMultipleComponent]
public class VientoEnCapaActor : MonoBehaviour
{
    private readonly List<Transform> _huesosCapa = new();
    private bool _huesosBuscados = false;
    private bool _activo = false;
    private float _intensidadObjetivo = 0f;
    private float _intensidadActual = 0f;
    private float _anguloElevacion = 32f;
    private float _frecuencia = 8.5f;
    private float _tiempoRestante = 0f;

    public void Configurar(bool activo, float intensidad, float anguloElevacion, float frecuencia, float duracion)
    {
        AsegurarHuesos();
        _activo = activo;
        _intensidadObjetivo = activo ? Mathf.Max(0.1f, intensidad) : 0f;
        _anguloElevacion = anguloElevacion;
        _frecuencia = Mathf.Max(0.5f, frecuencia);
        _tiempoRestante = duracion > 0f ? duracion : -1f;
    }

    public void DesactivarInmediato()
    {
        _activo = false;
        _intensidadObjetivo = 0f;
        _intensidadActual = 0f;
        _tiempoRestante = 0f;
    }

    private void OnDisable()
    {
        DesactivarInmediato();
    }

    private void AsegurarHuesos()
    {
        if (_huesosBuscados) return;
        _huesosBuscados = true;
        _huesosCapa.Clear();

        // Buscar huesos por nomenclatura estándar del rig (CloakBone01..03)
        var todos = GetComponentsInChildren<Transform>(true);
        var candidatos = new List<Transform>();
        foreach (var t in todos)
        {
            if (t.name.IndexOf("CloakBone", StringComparison.OrdinalIgnoreCase) >= 0)
                candidatos.Add(t);
        }

        // Ordenar alfabéticamente (CloakBone01, CloakBone02, CloakBone03)
        candidatos.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        _huesosCapa.AddRange(candidatos);
    }

    private void Update()
    {
        if (_tiempoRestante > 0f)
        {
            _tiempoRestante -= Time.deltaTime;
            if (_tiempoRestante <= 0f)
            {
                _activo = false;
                _intensidadObjetivo = 0f;
            }
        }

        float velocidadFade = _activo ? 3.0f : 2.0f;
        _intensidadActual = Mathf.MoveTowards(_intensidadActual, _intensidadObjetivo, Time.deltaTime * velocidadFade);
    }

    private void LateUpdate()
    {
        if (_intensidadActual <= 0.001f || _huesosCapa.Count == 0) return;

        float k = Mathf.Clamp01(_intensidadActual);
        Vector3 ejeTransversal = transform.right; // Eje de rotación sagital (empuja hacia atrás y arriba)
        Vector3 ejeVertical = transform.up;
        float tiempo = Time.time * _frecuencia;

        for (int i = 0; i < _huesosCapa.Count; i++)
        {
            var bone = _huesosCapa[i];
            if (bone == null) continue;

            // Cada hueso de la cadena contribuye a la curvatura aerodinámica de la capa
            float factorHueso = (i + 1f) / _huesosCapa.Count;
            float ondulacion = Mathf.Sin(tiempo + i * 0.75f) * (6.5f * k);
            float anguloSube = (_anguloElevacion * factorHueso + ondulacion) * k;

            // Rotación ascendente hacia la espalda
            bone.Rotate(ejeTransversal, anguloSube, Space.World);

            // Aleteo lateral suave para simular la turbulencia del flujo ascendente
            float flutter = Mathf.Cos(tiempo * 0.85f + i * 0.6f) * (3.5f * k);
            bone.Rotate(ejeVertical, flutter, Space.World);
        }
    }
}
