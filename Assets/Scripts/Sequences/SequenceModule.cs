using System.Collections;
using UnityEngine;

/// Mecánica de juego propia de UNA secuencia concreta, que el asset invoca por nombre.
///
/// ── Para qué es ───────────────────────────────────────────────────────────────────────────────
/// El catálogo de beats cubre lo que hacen todas las secuencias: hablar, gesticular, moverse,
/// cortar de plano, lanzar efectos. Alguna escena, además, tiene mecánica de JUEGO que no se
/// repite en ninguna otra — el panic input del Despertar de la Estrella, con su proyectil
/// entrante y el contraataque real de Will, es el único caso del proyecto hoy. Eso no puede ir en
/// el catálogo común: serían seis beats que usa una sola escena y que hay que mantener para
/// siempre.
///
/// Un módulo es ese hueco, con una frontera muy concreta.
///
/// ── Lo que SÍ hace un módulo ─────────────────────────────────────────────────────────────────
/// Reglas de juego: spawnear y conducir un proyectil, escuchar un input del jugador y decidir si
/// acertó, disparar el hechizo de verdad con el sistema del jugador, encender un efecto de shock.
///
/// ── Lo que NO hace un módulo ─────────────────────────────────────────────────────────────────
/// Puesta en escena. Un módulo no mueve la cámara, no mueve a un NPC, no muestra bocadillos, no
/// echa candados, no toca la música y no gestiona su propio cierre ni su propio skip. Todo eso lo
/// lleva el SequencePlayer, una sola vez, para todas las secuencias.
///
/// Esta frontera es la razón de ser del sistema entero. `Co_RunTo` nació exactamente así — como
/// "una cosita a mano solo para esta escena" — y acabó reintroduciendo cuatro defectos ya
/// resueltos en otro sitio (INC-209). Si falta un beat, la respuesta correcta es añadir el beat,
/// no meterlo aquí porque aquí no mira nadie.
///
/// ── Cómo se usa ───────────────────────────────────────────────────────────────────────────────
///   1. Un componente que herede de esta clase, en el GameObject de la secuencia.
///   2. Añadirlo a la lista de módulos del SequenceStage.
///   3. En el asset, un ModuleBeat con el nombre de la rutina.
public abstract class SequenceModule : MonoBehaviour
{
    /// Los nombres de rutina que este módulo sabe ejecutar. Se usa para encontrarlo desde el asset
    /// y para que un nombre mal escrito dé un aviso claro en vez de un silencio.
    public abstract string[] Routines { get; }

    /// Ejecuta una rutina. Devolver null si el nombre no es de este módulo.
    ///
    /// El contexto da acceso a los actores (por id), al escenario y al reproductor — lo mismo que
    /// tiene un beat. Para comunicar un resultado a las fases siguientes, dejar una marca:
    /// ctx.SetFlag("loQueSea", true).
    public abstract IEnumerator Run(string routine, SequenceContext ctx);

    /// Se llama SIEMPRE al cerrar la secuencia: al terminar bien, al saltarla, y si algo peta por
    /// el camino. Aquí se deshace lo que el módulo haya dejado a medias — objetos spawneados,
    /// suscripciones a eventos, sistemas del jugador desactivados.
    ///
    /// Tiene que ser idempotente: puede llamarse dos veces.
    public virtual void OnSequenceCleanup() { }

    /// ¿Es mío este nombre de rutina? Sin distinguir mayúsculas ni espacios sobrantes.
    public bool Handles(string routine)
    {
        if (string.IsNullOrWhiteSpace(routine) || Routines == null) return false;
        string wanted = routine.Trim();
        foreach (string r in Routines)
            if (string.Equals(r, wanted, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
