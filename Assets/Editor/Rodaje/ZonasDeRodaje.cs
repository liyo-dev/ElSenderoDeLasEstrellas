#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Plantas cenitales (con rejilla en metros) de las zonas de MainWorld donde se ruedan las
/// fotos de escaparate y el tráiler. Salen en «Guiones/MainWorld_<zona>/escenario/».
public static class ZonasDeRodaje
{
    [MenuItem("El Sendero/Rodaje/Capturar la zona del pueblo y alrededores")]
    public static void PuebloYAlrededores()
        => CapturaDeEscenario.Capturar("MainWorld", new Rect(-90f, -220f, 200f, 200f), "Escaparate");
}
#endif
