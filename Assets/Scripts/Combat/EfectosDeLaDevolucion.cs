using System;
using UnityEngine;

/// <summary>
/// Qué le pasa a quien recibe su propio hechizo devuelto con la B, según el elemento del hechizo
/// (INC-1165). Siempre quita vida; además puede aturdir, dormir, empujar, ralentizar o pegar más
/// fuerte. Es un asset (Resources/Combate/EfectosDeLaDevolucion) para afinarlo sin tocar código;
/// si no está, se usa la tabla de abajo. Los jefes lo sufren a la mitad (EstadosDeCombate).
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Combate/Efectos de la devolución", fileName = "EfectosDeLaDevolucion")]
public sealed class EfectosDeLaDevolucion : ScriptableObject
{
    [Serializable]
    public struct Efecto
    {
        public MagicElement elemento;
        [Tooltip("Estado que pone además del daño (Ninguno = solo daño).")]
        public EstadoDeCombate estado;
        [Min(0f)] public float duracion;
        [Tooltip("Ralentizar: fracción de velocidad que le queda. Empujar/Atraer: metros por segundo.")]
        [Min(0f)] public float fuerza;
        [Tooltip("Multiplica el daño del contraataque (1 = igual).")]
        [Min(0f)] public float multiplicadorDeDano;
        [Tooltip("Efecto que se le queda encima mientras dura el estado (opcional).")]
        public GameObject vfx;
    }

    private const string Ruta = "Combate/EfectosDeLaDevolucion";

    public Efecto[] efectos = PorDefecto();

    /// Tabla de partida: el fuego pega más, el viento aparta, la luz deslumbra, la mente duerme y
    /// la magia oscura (la de casi todos los enemigos) hace tambalearse un momento.
    public static Efecto[] PorDefecto() => new[]
    {
        new Efecto { elemento = MagicElement.Fire,  estado = EstadoDeCombate.Ninguno, multiplicadorDeDano = 1.3f },
        new Efecto { elemento = MagicElement.Storm, estado = EstadoDeCombate.Empujar, duracion = 0.6f, fuerza = 7f, multiplicadorDeDano = 1f },
        new Efecto { elemento = MagicElement.Light, estado = EstadoDeCombate.Aturdir, duracion = 1.6f, multiplicadorDeDano = 1f },
        new Efecto { elemento = MagicElement.Mind,  estado = EstadoDeCombate.Dormir,  duracion = 3f,   multiplicadorDeDano = 0.8f },
        new Efecto { elemento = MagicElement.Dark,  estado = EstadoDeCombate.Aturdir, duracion = 0.7f, multiplicadorDeDano = 1f },
    };

    private static EfectosDeLaDevolucion s_tabla;
    private static bool s_buscada;

    public static Efecto De(MagicElement elemento)
    {
        if (!s_buscada) { s_tabla = Resources.Load<EfectosDeLaDevolucion>(Ruta); s_buscada = true; }
        var lista = s_tabla != null && s_tabla.efectos != null ? s_tabla.efectos : PorDefecto();
        foreach (var e in lista)
            if (e.elemento == elemento) return e;
        return new Efecto { elemento = elemento, estado = EstadoDeCombate.Ninguno, multiplicadorDeDano = 1f };
    }

    /// Pone el estado del elemento al que recibió el golpe.
    public static void Aplicar(Damageable receptor, MagicElement elemento, Vector3 punto)
    {
        var e = De(elemento);
        if (receptor == null || e.estado == EstadoDeCombate.Ninguno) return;
        EstadosDeCombate.Aplicar(receptor, e.estado, e.duracion, e.fuerza, punto, e.vfx);
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_tabla = null; s_buscada = false; }
#endif
}
