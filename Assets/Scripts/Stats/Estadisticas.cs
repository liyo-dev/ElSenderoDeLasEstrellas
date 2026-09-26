using System;
using UnityEngine;

/// Las cuatro estadísticas de un personaje: vida, magia, ataque y defensa. Sirve igual para lo
/// que tiene Will de base, para lo que da un premio de combate y para lo que suma una pieza de
/// equipo: todo se suma con el operador +. Ver INC-470.
[Serializable]
public struct Estadisticas
{
    [Tooltip("Vida máxima.")] public float vida;
    [Tooltip("Magia (maná) máxima.")] public float magia;
    [Tooltip("Ataque: multiplica el daño de los hechizos (ver FormulasDeCombate).")] public float ataque;
    [Tooltip("Defensa: reduce el daño recibido (ver FormulasDeCombate).")] public float defensa;

    public Estadisticas(float vida, float magia, float ataque, float defensa)
    {
        this.vida = vida; this.magia = magia; this.ataque = ataque; this.defensa = defensa;
    }

    /// Ninguna de las cuatro tiene valor (un premio vacío, o unas estadísticas sin inicializar).
    public bool EsCero => vida == 0f && magia == 0f && ataque == 0f && defensa == 0f;

    public static Estadisticas operator +(Estadisticas a, Estadisticas b) =>
        new Estadisticas(a.vida + b.vida, a.magia + b.magia, a.ataque + b.ataque, a.defensa + b.defensa);

    public static Estadisticas operator -(Estadisticas a, Estadisticas b) =>
        new Estadisticas(a.vida - b.vida, a.magia - b.magia, a.ataque - b.ataque, a.defensa - b.defensa);

    public override string ToString() => $"vida {vida:0.#}, magia {magia:0.#}, ataque {ataque:0.#}, defensa {defensa:0.#}";
}

/// Algo que suma estadísticas a Will mientras está activo: una pieza de equipo, un efecto
/// temporal... Se registra en EstadisticasDeWill y sale en el total sin tocar lo que Will tiene
/// de base (que es lo que suben los combates y lo que se guarda). Ver INC-470.
public interface IFuenteDeBonos
{
    Estadisticas Bonos { get; }
}

/// Algo que ataca y cuyo daño depende de sus estadísticas. MagicProjectile y MagicZoneEffect lo
/// buscan en quien lanzó el hechizo (el instigador) para aplicarle el ataque.
public interface IPoderDeAtaque
{
    float Aplicar(float danoBase);
}

/// Las cuentas de combate en un solo sitio.
public static class FormulasDeCombate
{
    /// Con este ataque, un hechizo hace exactamente su daño base.
    public const float AtaqueDeReferencia = 10f;

    /// Cuanto mayor, menos protege cada punto de defensa. Con 50: defensa 5 quita un 9 % del
    /// daño, 25 un 33 %, 50 la mitad. Nunca llega al 100 %.
    public const float ConstanteDeDefensa = 50f;

    public static float DanoConAtaque(float danoBase, float ataque) =>
        danoBase * Mathf.Max(0f, ataque) / AtaqueDeReferencia;

    public static float DanoTrasDefensa(float dano, float defensa) =>
        dano * ConstanteDeDefensa / (ConstanteDeDefensa + Mathf.Max(0f, defensa));

    /// El daño de un golpe según quien lo lanza: si tiene IPoderDeAtaque, con su ataque; si no,
    /// el daño base tal cual.
    public static float DanoDe(GameObject instigador, float danoBase)
    {
        if (instigador == null) return danoBase;
        var poder = instigador.GetComponentInParent<IPoderDeAtaque>();
        return poder != null ? poder.Aplicar(danoBase) : danoBase;
    }
}
