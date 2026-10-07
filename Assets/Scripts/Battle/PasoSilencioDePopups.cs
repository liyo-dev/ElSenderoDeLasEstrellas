using System.Collections;
using UnityEngine;

/// <summary>
/// Lo que se gana durante una batalla (orbes, botín) lo cuenta el informe de victoria, así que
/// los pop-ups de objetos callan desde que empieza la batalla hasta que acaba su cierre o se
/// cancela. Ver INC-642.
/// </summary>
public sealed class PasoSilencioDePopups : IPasoDeCierre, IPasoConInicioDeBatalla
{
    public int Orden => 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Registrar() => CierreDeBatalla.Registrar(new PasoSilencioDePopups());

    public void Iniciar(ResultadoDeBatalla resultado) => CollectiblePopupQueue.Silenciar(this);

    public IEnumerator Ejecutar(ResultadoDeBatalla resultado) => null;

    public void Terminar(ResultadoDeBatalla resultado) => CollectiblePopupQueue.Reanudar(this);

    public void Cancelar(ResultadoDeBatalla resultado) => CollectiblePopupQueue.Reanudar(this);
}
