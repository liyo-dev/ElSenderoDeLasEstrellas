using System;
using UnityEngine;

/// <summary>Configuración reutilizable del golpe que lanza y derriba al jugador.</summary>
[Serializable]
public struct DerriboDelJugador
{
    [Tooltip("El golpe lanza y derriba al jugador si no se defiende.")]
    public bool activo;
    [Tooltip("Distancia horizontal del lanzamiento, en metros.")]
    [Min(0f)] public float distancia;
    [Tooltip("Altura del arco, en metros.")]
    [Min(0f)] public float altura;
    [Tooltip("Duración del lanzamiento, en segundos.")]
    [Min(0.1f)] public float duracion;

    public static DerriboDelJugador Estandar => new DerriboDelJugador
    { activo = true, distancia = 4f, altura = 2f, duracion = 0.7f };

    public bool Aplicar(GameObject objetivo, Vector3 origen) =>
        activo && AerialKnockbackReceiver.Derribar(objetivo, origen, distancia, altura, duracion);
}
