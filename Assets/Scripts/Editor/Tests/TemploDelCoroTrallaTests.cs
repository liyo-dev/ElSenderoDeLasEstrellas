using System.Collections.Generic;
using Kits.Puzzles;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TemploDelCoroTrallaTests
{
    [Test]
    public void EscenaGuardada_AvisaUnaFilaYLuegoDos()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TemploDelCoroBuilder.RutaEscena);
        bool cerrar = !escena.IsValid() || !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(TemploDelCoroBuilder.RutaEscena, OpenSceneMode.Additive);
        try
        {
            var golpes = new SortedDictionary<float, int>();
            var centros = new HashSet<float>();
            TramoDelCoro tramo = null;
            TempoClock relojDelCoro = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                if (tramo == null) tramo = raiz.GetComponentInChildren<TramoDelCoro>();
                foreach (var caida in raiz.GetComponentsInChildren<CueDrop>(true))
                {
                    var datos = new SerializedObject(caida);
                    var voces = datos.FindProperty("voices");
                    if (voces.arraySize != 1) continue;
                    string voz = voces.GetArrayElementAtIndex(0).stringValue;
                    if (!voz.StartsWith("CORO_FILA_", System.StringComparison.Ordinal)) continue;
                    var sitio = (Transform)datos.FindProperty("landingPoints").GetArrayElementAtIndex(0).objectReferenceValue;
                    Assert.IsTrue(centros.Add(sitio.position.z));
                    Assert.AreEqual(new Vector2(10f, 3f), datos.FindProperty("strikeSize").vector2Value);
                    Assert.GreaterOrEqual(datos.FindProperty("fallSeconds").floatValue, 0.7f);
                    // La tralla cae en ola: varias semicorcheas en línea a lo ancho de la fila.
                    Assert.AreEqual(5, datos.FindProperty("wavePieces").intValue);
                    Assert.AreEqual(7.2f, datos.FindProperty("waveWidth").floatValue, 0.01f);
                    Assert.Greater(datos.FindProperty("waveSeconds").floatValue, 0f);
                    Assert.IsTrue(datos.FindProperty("waveToTheRight").boolValue);
                    float paso = datos.FindProperty("waveWidth").floatValue / (datos.FindProperty("wavePieces").intValue - 1);
                    float anchoNota = ((GameObject)datos.FindProperty("fallingTemplate").objectReferenceValue).transform.localScale.x * 2.5f;
                    Assert.Greater(paso, anchoNota, "Las semicorcheas de la ola no se pueden pisar.");
                    relojDelCoro = (TempoClock)datos.FindProperty("clock").objectReferenceValue;
                    var tiempos = new List<float>();
                    relojDelCoro.Score.CollectCueTimes(new[] { voz }, 0f, float.MaxValue, tiempos);
                    Assert.IsNotEmpty(tiempos);
                    foreach (float segundo in tiempos)
                    {
                        golpes.TryGetValue(segundo, out int cantidad);
                        golpes[segundo] = cantidad + 1;
                    }
                }
            }
            Assert.AreEqual(3, centros.Count);
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 2, 2, 2 }, golpes.Values);
            Assert.IsTrue(relojDelCoro.SchedulesCueAudio);
            var tiemposDeAudio = new List<float>();
            relojDelCoro.Score.CollectCueTimes(new[] { "GOLPE_FILAS_CORO" }, 0f, float.MaxValue, tiemposDeAudio);
            CollectionAssert.AreEqual(golpes.Keys, tiemposDeAudio);
            Assert.NotNull(tramo);
            var datosDelTramo = new SerializedObject(tramo);
            var recinto = (Transform)datosDelTramo.FindProperty("recintoDelCoro").objectReferenceValue;
            Assert.NotNull(recinto);
            Assert.AreEqual(4, recinto.GetComponentsInChildren<BoxCollider>(true).Length);

            Assert.AreEqual(3, datosDelTramo.FindProperty("focosDeRuta").arraySize);
            var recorrido = datosDelTramo.FindProperty("recorridoLateralDelFoco").animationCurveValue;
            var centrosDelEstribillo = new List<float>();
            var partitura = relojDelCoro.Score;
            partitura.CollectCueTimes(new[] { "GOLPE_CENTRAL" }, 0f, float.MaxValue, centrosDelEstribillo);
            Assert.GreaterOrEqual(centrosDelEstribillo.Count, 2);
            foreach (float segundo in centrosDelEstribillo)
            {
                int tiempo = partitura.BeatIndexAt(segundo);
                Assert.AreEqual(partitura.BeatTime(tiempo), segundo, 0.001f);
                Assert.AreEqual(0, (tiempo - partitura.FirstDownbeatIndex) % partitura.BeatsPerMeasure);
                Assert.Greater(Mathf.Abs(recorrido.Evaluate(segundo)), 2.5f, "En el golpe central, el foco tiene que estar al otro lado.");
            }

            var marcasDeLaLetra = new List<float>();
            partitura.CollectCueTimes(new[] { "ACENTO_DE_LA_LETRA" }, 0f, float.MaxValue, marcasDeLaLetra);
            CollectionAssert.AreEqual(new[] { 67.889f, 68.667f, 94.232f, 95.016f, 95.823f }, marcasDeLaLetra);

            var tenor = (TenorQueCanta)datosDelTramo.FindProperty("tenorDeLosRegalos").objectReferenceValue;
            Assert.NotNull(tenor);
            var datosDelTenor = new SerializedObject(tenor);
            Assert.NotNull(datosDelTenor.FindProperty("siguienteMarca").objectReferenceValue);
            var receptorDelGuardian = tenor.GetComponent<ReceptorDeMagia>();
            Assert.NotNull(receptorDelGuardian);
            Assert.AreEqual(tenor, receptorDelGuardian.alActivarse.GetPersistentTarget(0));
            Assert.AreEqual(nameof(TenorQueCanta.EmpujarConElRegalo), receptorDelGuardian.alActivarse.GetPersistentMethodName(0));
            var lanzadores = datosDelTramo.FindProperty("lanzadoresDeRegalos");
            Assert.AreEqual(3, lanzadores.arraySize);
            for (int i = 0; i < lanzadores.arraySize; i++)
            {
                var datosDelRegalo = new SerializedObject(lanzadores.GetArrayElementAtIndex(i).objectReferenceValue);
                Assert.IsNull(datosDelRegalo.FindProperty("sitio").objectReferenceValue);
                Assert.Greater(datosDelRegalo.FindProperty("dano").floatValue, 0f);
                if (i != 2) continue;
                Assert.AreEqual(tenor.gameObject, datosDelRegalo.FindProperty("destino").objectReferenceValue);
                var prefab = (GameObject)datosDelRegalo.FindProperty("prefabDevolvible").objectReferenceValue;
                Assert.NotNull(prefab);
                var proyectil = prefab.GetComponentInChildren<EnemyProjectile>();
                Assert.NotNull(proyectil);
                var elemento = proyectil.TryGetElemento(out var suyo) ? suyo : MagicElement.Dark;
                var punto = tenor.transform.position;
                try
                {
                    receptorDelGuardian.RecibirMagia(new ImpactoDeMagia(elemento, punto, null, false));
                    Assert.IsFalse(receptorDelGuardian.Encendido, "El regalo de ida no abre el paso.");
                    receptorDelGuardian.RecibirMagia(new ImpactoDeMagia(elemento, punto, null, true));
                    Assert.IsTrue(receptorDelGuardian.Encendido, "El impacto del regalo devuelto debe activar al guardián.");
                }
                finally { receptorDelGuardian.Reiniciar(); }
            }
        }
        finally { if (cerrar) EditorSceneManager.CloseScene(escena, true); }
    }

    [MenuItem("El Sendero/Pruebas/Templo del Coro: tralla en las tres filas")]
    public static void Ejecutar()
    {
        string carpeta = System.IO.Path.Combine(Application.dataPath, "../Guiones/_pruebas");
        System.IO.Directory.CreateDirectory(carpeta);
        string informe = System.IO.Path.Combine(carpeta, "tralla-coro.txt");
        try
        {
            new TemploDelCoroTrallaTests().EscenaGuardada_AvisaUnaFilaYLuegoDos();
            System.IO.File.WriteAllText(informe, "CORRECTO: arena, foco, acentos de la letra y devolución al guardián.\n");
        }
        catch (System.Exception e)
        {
            System.IO.File.WriteAllText(informe, "FALLO: " + e + "\n");
            throw;
        }
    }
}
