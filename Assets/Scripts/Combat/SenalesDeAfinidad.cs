using System;
using UnityEngine;

/// <summary>
/// Cómo se enseña un golpe que entra con debilidad o resistencia elemental (ver
/// <see cref="AfinidadElemental"/>): sin números en pantalla, con un destello del cuerpo del color
/// del elemento, su sonido de impacto grande, un efecto opcional y una pausa mínima del golpe. La
/// resistencia, con un destello apagado. Es un asset (Resources/Combate/SenalesDeAfinidad) para
/// afinarlo sin tocar código; si no está, se usa la tabla de abajo.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Combate/Señales de afinidad elemental", fileName = "SenalesDeAfinidad")]
public sealed class SenalesDeAfinidad : ScriptableObject
{
    [Serializable]
    public struct Senal
    {
        public MagicElement elemento;
        [Tooltip("Multiplicador del color del cuerpo en el destello (por encima de 1 aclara; con bloom brilla).")]
        [ColorUsage(false, true)] public Color destello;
        [Min(0.02f)] public float duracionDelDestello;
        [Tooltip("Clave del sonido en el perfil de audio (vacío = sin sonido).")]
        public string sonido;
        [Range(0f, 1f)] public float volumen;
        [Tooltip("Efecto de un solo uso sobre la criatura (por el pool; opcional).")]
        public GameObject vfx;
    }

    private const string Ruta = "Combate/SenalesDeAfinidad";

    [Tooltip("Señal de cada elemento cuando le duele más.")]
    public Senal[] debilidad = PorDefecto();
    [Tooltip("Señal cuando la criatura resiste (vale para cualquier elemento).")]
    public Senal resistencia = ResistenciaPorDefecto();

    [Header("Común")]
    [Tooltip("Segundos mínimos entre dos señales en la misma criatura (una zona que hace daño por ticks no repite el destello en cada uno).")]
    [Min(0f)] public float separacionEntreSenales = 0.35f;
    [Tooltip("Altura sobre los pies a la que sale el efecto y el sonido.")]
    public float alturaDelEfecto = 1.1f;
    [Min(0.1f)] public float vidaDelEfecto = 1.2f;
    [Tooltip("Pausa del golpe en la debilidad (segundos reales; 0 = sin pausa).")]
    [Range(0f, 0.2f)] public float pausaEnLaDebilidad = 0.05f;
    [Range(0.02f, 0.5f)] public float escalaDeTiempoEnLaPausa = 0.1f;

    /// Tabla de partida: cada elemento con el color de su magia y su impacto grande.
    public static Senal[] PorDefecto() => new[]
    {
        new Senal { elemento = MagicElement.Fire,  destello = new Color(4.2f, 1.9f, 0.7f), duracionDelDestello = 0.28f, sonido = "Impacto_Fuego_Grande",  volumen = 0.95f },
        new Senal { elemento = MagicElement.Storm, destello = new Color(1.6f, 3.6f, 2.8f), duracionDelDestello = 0.28f, sonido = "Impacto_Viento_Grande", volumen = 0.95f },
        new Senal { elemento = MagicElement.Light, destello = new Color(4.0f, 3.6f, 1.8f), duracionDelDestello = 0.28f, sonido = "Impacto_Luz_Grande",    volumen = 0.95f },
        new Senal { elemento = MagicElement.Mind,  destello = new Color(2.8f, 1.8f, 4.2f), duracionDelDestello = 0.28f, sonido = "Impacto_Mente_Grande",  volumen = 0.95f },
        new Senal { elemento = MagicElement.Dark,  destello = new Color(2.4f, 1.4f, 3.4f), duracionDelDestello = 0.28f, sonido = "Impacto_Oscuro_Grande", volumen = 0.95f },
    };

    public static Senal ResistenciaPorDefecto() =>
        new Senal { destello = new Color(0.55f, 0.55f, 0.62f), duracionDelDestello = 0.16f, sonido = "", volumen = 0.7f };

    private static SenalesDeAfinidad s_tabla;
    private static bool s_buscada;

    /// La tabla del juego (el asset de Resources, o una con los valores de partida si no está).
    public static SenalesDeAfinidad Tabla
    {
        get
        {
            if (!s_buscada)
            {
                s_tabla = Resources.Load<SenalesDeAfinidad>(Ruta);
                if (s_tabla == null) { s_tabla = CreateInstance<SenalesDeAfinidad>(); s_tabla.hideFlags = HideFlags.DontSave; }
                s_buscada = true;
            }
            return s_tabla;
        }
    }

    /// La señal para este elemento y esta afinidad.
    public Senal De(MagicElement elemento, AfinidadAlElemento afinidad)
    {
        if (afinidad == AfinidadAlElemento.Resiste) return resistencia;
        if (debilidad != null)
            for (int i = 0; i < debilidad.Length; i++)
                if (debilidad[i].elemento == elemento) return debilidad[i];
        return new Senal { elemento = elemento, destello = new Color(3f, 3f, 3f), duracionDelDestello = 0.25f, volumen = 0.9f };
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { s_tabla = null; s_buscada = false; }
#endif
}
