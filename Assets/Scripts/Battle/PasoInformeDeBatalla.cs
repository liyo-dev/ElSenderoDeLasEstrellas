using System.Collections;
using UnityEngine;

/// Paso del cierre de batalla: enseña lo que se ha ganado (lo que han anotado los pasos de
/// premios en el ResultadoDeBatalla) en el informe de victoria (InformeDeVictoriaUI), con la
/// cámara aún sobre la foto de grupo, y espera a que el jugador lo cierre. Si no se ha ganado
/// nada, no enseña nada. Ver INC-470 e INC-543.
public sealed class PasoInformeDeBatalla : IPasoDeCierre
{
    public int Orden => 200;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoInformeDeBatalla());

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
    {
        if (!resultado.HayPremios) yield break;

        var informe = InformeDeVictoriaUI.Obtener();
        if (informe == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[PasoInformeDeBatalla] No existe el prefab Resources/" + InformeDeVictoriaUI.RutaEnResources +
                             ": ejecuta «El Sendero/UI/Crear o regenerar el informe de victoria».");
#endif
            yield break;
        }

        yield return informe.Mostrar(resultado);
    }

    public void Terminar(ResultadoDeBatalla resultado) { }
}
