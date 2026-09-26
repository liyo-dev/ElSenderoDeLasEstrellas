using System.Collections;
using System.Text;
using UnityEngine;

/// Paso del cierre de batalla: enseña lo que se ha ganado (lo que han anotado los pasos de
/// premios en el ResultadoDeBatalla) en el popup de avisos del juego, con la cámara aún sobre
/// Will, y espera a que se cierre. Si no se ha ganado nada, no enseña nada. Ver INC-470.
public sealed class PasoInformeDeBatalla : IPasoDeCierre
{
    private const float SegundosBase = 2.5f;
    private const float SegundosPorLinea = 1.2f;

    public int Orden => 200;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoInformeDeBatalla());

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        if (resultado.Lineas.Count == 0) yield break;

        var popup = AbilityUnlockPopupUI.Instancia;
        if (popup == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PasoInformeDeBatalla] No hay popup de avisos en escena: el informe solo va a la consola.\n" +
                             Texto(resultado));
#endif
            yield break;
        }

        string titulo = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get("BATTLE_REPORT_TITLE", "¡Victoria!")
            : "¡Victoria!";
        Sprite icono = null;
        for (int i = 0; i < resultado.Lineas.Count && icono == null; i++) icono = resultado.Lineas[i].icono;

        float segundos = SegundosBase + SegundosPorLinea * resultado.Lineas.Count;
        popup.MostrarAviso(titulo, Texto(resultado), icono, segundos);

        // Hasta que se cierre (con tope, por si algo lo deja abierto).
        float tope = Time.realtimeSinceStartup + segundos + 2f;
        yield return null;
        while (popup != null && popup.EstaEnPantalla && Time.realtimeSinceStartup < tope)
            yield return null;
    }

    public void Terminar(ResultadoDeBatalla resultado) { }

    private static string Texto(ResultadoDeBatalla resultado)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < resultado.Lineas.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(resultado.Lineas[i].texto);
        }
        return sb.ToString();
    }
}
