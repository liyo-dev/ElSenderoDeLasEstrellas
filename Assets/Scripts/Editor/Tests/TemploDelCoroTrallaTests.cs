using System.Collections.Generic;
using Kits.Puzzles;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TemploDelCoroTrallaTests
{
    [Test]
    public void EscenaGuardada_TieneTrallaSimultaneaEnTresFilas()
    {
        var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TemploDelCoroBuilder.RutaEscena);
        bool cerrar = !escena.IsValid() || !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(TemploDelCoroBuilder.RutaEscena, OpenSceneMode.Additive);
        try
        {
            var filas = new List<SerializedObject>();
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var caida in raiz.GetComponentsInChildren<CueDrop>(true))
                {
                    var datos = new SerializedObject(caida);
                    var voces = datos.FindProperty("voices");
                    if (voces.arraySize == 1 && voces.GetArrayElementAtIndex(0).stringValue == "TRALLA")
                        filas.Add(datos);
                }
            Assert.AreEqual(3, filas.Count, "El montaje guardado necesita una caída por cada fila, no una escala que alterna filas.");
            var alturas = new HashSet<float>();
            int puntos = filas[0].FindProperty("landingPoints").arraySize;
            Assert.Greater(puntos, 0);
            foreach (var fila in filas)
            {
                Assert.AreEqual(filas[0].FindProperty("fromSeconds").floatValue, fila.FindProperty("fromSeconds").floatValue);
                Assert.AreEqual(filas[0].FindProperty("toSeconds").floatValue, fila.FindProperty("toSeconds").floatValue);
                var sitios = fila.FindProperty("landingPoints");
                Assert.AreEqual(puntos, sitios.arraySize, "Todas las filas reciben cada golpe de la misma voz.");
                var primero = (Transform)sitios.GetArrayElementAtIndex(0).objectReferenceValue;
                Assert.NotNull(primero);
                Assert.IsTrue(alturas.Add(primero.position.z), "Cada emisor ocupa una fila distinta.");
                for (int i = 0; i < sitios.arraySize; i++)
                {
                    var sitio = (Transform)sitios.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.NotNull(sitio);
                    Assert.AreEqual(primero.position.z, sitio.position.z, 0.001f);
                    Assert.That(sitio.position.x, Is.InRange(-4f, 4f));
                }
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
            new TemploDelCoroTrallaTests().EscenaGuardada_TieneTrallaSimultaneaEnTresFilas();
            System.IO.File.WriteAllText(informe, "CORRECTO: tralla simultánea en tres filas en la escena guardada.\n");
        }
        catch (System.Exception e)
        {
            System.IO.File.WriteAllText(informe, "FALLO: " + e + "\n");
            throw;
        }
    }
}
