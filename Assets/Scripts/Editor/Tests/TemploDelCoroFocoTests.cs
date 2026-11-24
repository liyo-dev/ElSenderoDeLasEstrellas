using System.Collections.Generic;
using Kits.Puzzles;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Guarda del montaje del Templo del Coro: tres focos, tramos que mienten y verbos en el camino.</summary>
public class TemploDelCoroFocoTests
{
    private static readonly (string voz, float[] tiempos)[] GolpesDelCamino =
    {
        ("SALTO_ANCHO", new[] { 31.945f, 33.994f, 70.722f, 79.778f, 122.282f }),
        ("DASH_ANCHO", new[] { 80.968f, 88.416f, 123.867f }),
        ("DOBLE_SALTO", new[] { 124.633f, 125.823f }),
        ("SENUELO_DEL_FOCO", new[] { 71.546f, 86.314f })
    };

    [Test]
    public void EscenaGuardada_TresFocosCaminoConVerbos()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TemploDelCoroBuilder.RutaEscena);
        bool cerrar = !escena.IsValid() || !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(TemploDelCoroBuilder.RutaEscena, OpenSceneMode.Additive);
        try
        {
            TramoDelCoro tramo = null;
            var focos = new List<FollowSpot>();
            var golpes = new Dictionary<string, int>();
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (tramo == null) tramo = raiz.GetComponentInChildren<TramoDelCoro>();
                focos.AddRange(raiz.GetComponentsInChildren<FollowSpot>(true));
                foreach (var caida in raiz.GetComponentsInChildren<CueDrop>(true))
                {
                    var datos = new SerializedObject(caida);
                    var voces = datos.FindProperty("voices");
                    if (voces.arraySize != 1) continue;
                    string voz = voces.GetArrayElementAtIndex(0).stringValue;
                    if (VozDelCamino(voz) == null) continue;
                    golpes.TryGetValue(voz, out int cantidad);
                    golpes[voz] = cantidad + 1;
                    int puntos = datos.FindProperty("landingPoints").arraySize;
                    float paso = datos.FindProperty("waveWidth").floatValue / Mathf.Max(1, datos.FindProperty("wavePieces").intValue - 1);
                    float anchoNota = ((GameObject)datos.FindProperty("fallingTemplate").objectReferenceValue).transform.localScale.x * 2.4f;
                    Assert.Greater(paso, anchoNota, "Las notas de la ola no se pueden pisar unas a otras.");
                    if (voz == "SENUELO_DEL_FOCO")
                    {
                        Assert.AreEqual(1, puntos, "Cada foco que miente cae en un sitio.");
                        continue;
                    }
                    Assert.GreaterOrEqual(datos.FindProperty("strikeSize").vector2Value.x, 9f, $"{voz} no ocupa todo el corredor.");
                    Assert.AreEqual(TiemposDe(voz).Length, puntos, $"Cada golpe de {voz} cae en su sitio del camino.");
                    var sitio = (Transform)datos.FindProperty("landingPoints").GetArrayElementAtIndex(0).objectReferenceValue;
                    Assert.AreEqual(0f, sitio.position.x, 0.01f, "La franja va por el centro para cubrir todo el corredor.");
                    float alto = datos.FindProperty("strikeHeight").floatValue;
                    if (voz == "DASH_ANCHO") Assert.AreEqual(0f, alto, 0.001f, "La rodada no se salta.");
                    else Assert.AreEqual(0.7f, alto, 0.001f, "Este golpe se salta.");
                }
            }

            Assert.NotNull(tramo);
            Assert.AreEqual(3, focos.Count, "Tienen que estar los tres focos de la ruta.");
            var datosDelTramo = new SerializedObject(tramo);
            Assert.AreEqual(3, datosDelTramo.FindProperty("focosDeRuta").arraySize);
            Assert.AreEqual(3.3f, datosDelTramo.FindProperty("separacionDeLosFocos").floatValue, 0.01f);
            var ventanas = datosDelTramo.FindProperty("tramosDelFoco");
            var reglas = datosDelTramo.FindProperty("reglasDeLosFocos");
            Assert.AreEqual(ventanas.arraySize, reglas.arraySize);
            bool miente = false, hayHonestos = false;
            for (int i = 0; i < reglas.arraySize; i++)
            {
                var regla = reglas.GetArrayElementAtIndex(i).vector2IntValue;
                if (regla.y == 0) { hayHonestos = true; Assert.AreEqual(1, regla.x); }
                else { miente = true; Assert.IsTrue(regla.x == 0 || regla.x == 2); }
            }
            Assert.IsTrue(miente, "Algún tramo tiene que dejar de seguir la luz.");
            Assert.IsTrue(hayHonestos, "Los demás tramos siguen guiando con el foco del centro.");

            var reloj = (TempoClock)datosDelTramo.FindProperty("reloj").objectReferenceValue;
            Assert.NotNull(reloj);
            foreach (var golpe in GolpesDelCamino)
            {
                var tiempos = new List<float>();
                reloj.Score.CollectCueTimes(new[] { golpe.voz }, 0f, float.MaxValue, tiempos);
                CollectionAssert.AreEqual(golpe.tiempos, tiempos, $"Los tiempos de {golpe.voz} tienen que caer en la partitura.");
                int esperados = golpe.voz == "SENUELO_DEL_FOCO" ? golpe.tiempos.Length : 1;
                Assert.AreEqual(esperados, golpes.TryGetValue(golpe.voz, out int n) ? n : 0, $"Faltan emisores de {golpe.voz}.");
            }
        }
        finally { if (cerrar) EditorSceneManager.CloseScene(escena, true); }
    }

    private static string VozDelCamino(string voz)
    {
        for (int i = 0; i < GolpesDelCamino.Length; i++)
            if (GolpesDelCamino[i].voz == voz) return voz;
        return null;
    }

    private static float[] TiemposDe(string voz)
    {
        for (int i = 0; i < GolpesDelCamino.Length; i++)
            if (GolpesDelCamino[i].voz == voz) return GolpesDelCamino[i].tiempos;
        return System.Array.Empty<float>();
    }

    [MenuItem("El Sendero/Pruebas/Templo del Coro: foco y verbos del camino")]
    public static void Ejecutar()
    {
        string carpeta = System.IO.Path.Combine(Application.dataPath, "../Guiones/_pruebas");
        System.IO.Directory.CreateDirectory(carpeta);
        string informe = System.IO.Path.Combine(carpeta, "foco-coro.txt");
        try
        {
            new TemploDelCoroFocoTests().EscenaGuardada_TresFocosCaminoConVerbos();
            System.IO.File.WriteAllText(informe, "CORRECTO: tres focos, tramos que mienten y golpes de salto, rodada, doble salto y señuelo.\n");
        }
        catch (System.Exception e)
        {
            System.IO.File.WriteAllText(informe, "FALLO: " + e + "\n");
            throw;
        }
    }
}
