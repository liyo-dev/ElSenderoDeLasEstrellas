using UnityEditor;
using UnityEngine;

/// Lanza el prólogo a mano, sin pasar por el arranque del juego (INC-393).
///
/// ── Por qué ───────────────────────────────────────────────────────────────────────────────────
/// La secuencia del prólogo NO se lanza sola al dar a Play: espera la señal `PROLOGUE_START`, y
/// quien la emite es el grafo narrativo de Cap1, que a su vez solo corre si la partida arranca
/// como es debido (Start → MainWorld → el nodo que carga el valle en aditivo). Si se le da a Play
/// con el valle abierto y poco más, en la consola se ve exactamente esto:
///
///     [Secuencia:SEQ_Prologo_UltimaNoche] A la espera de 'PROLOGUE_START' …
///
/// …y ahí se queda para siempre, porque no hay nadie que la emita. No está roto: es que falta el
/// que da la orden.
///
/// Esto sirve para probar el decorado sin arrancar la partida entera: se le da a Play con el valle
/// abierto y luego a este menú. Ojo: sin el arranque normal no hay jugador, ni HUD, ni casa de
/// Will, así que lo que se comprueba aquí es la puesta en escena (marcas, caminos, planos), no el
/// flujo del capítulo.
public static class LanzarPrologoAhora
{
    private const string SeñalPrologo = "PROLOGUE_START";

    [MenuItem("El Sendero/Secuencias/Prólogo: lanzarlo AHORA (en Play)", priority = 1)]
    public static void Lanzar()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Prólogo",
                "Esto es para probar en marcha: dale primero a Play (con Prologo_Valle abierta) y " +
                "después a este menú.", "Vale");
            return;
        }

        var señales = DefaultNarrativeSignals.EnsureInstance();
        if (señales == null)
        {
            Debug.LogError("[Prólogo] No hay DefaultNarrativeSignals, así que no puedo emitir nada.");
            return;
        }

        Debug.Log($"[Prólogo] Emitiendo '{SeñalPrologo}' a mano desde el menú.");
        señales.RaiseCustom(SeñalPrologo, "menú: lanzar el prólogo a mano");
    }

    [MenuItem("El Sendero/Secuencias/Prólogo: lanzarlo AHORA (en Play)", validate = true)]
    private static bool PuedeLanzar() => Application.isPlaying;
}
