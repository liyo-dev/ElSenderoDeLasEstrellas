using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AplicarIncidenciasNoviembre
{
    [MenuItem("El Sendero/Pruebas/Verificar cruces de noviembre")]
    public static void Verificar()
    {
        const string ruta = "Assets/Scenes/Worlds/TemploDelCoro.unity";
        var escena = SceneManager.GetSceneByPath(ruta);
        bool abiertaParaProbar = !escena.isLoaded;
        if (abiertaParaProbar) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        try
        {
            TramoDelCoro tramo = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                tramo = raiz.GetComponentInChildren<TramoDelCoro>(true);
                if (tramo != null) break;
            }
            if (tramo == null) throw new InvalidOperationException("Falta el tramo del Coro.");
            var datos = new SerializedObject(tramo);
            var filas = datos.FindProperty("sueloQueSePierde");
            float finAnterior = float.NegativeInfinity;
            var cruces = new System.Collections.Generic.List<float>();
            for (int i = 0; i < filas.arraySize; i++)
            {
                var fila = (Transform)filas.GetArrayElementAtIndex(i).objectReferenceValue;
                var suelo = fila.GetComponentInChildren<BoxCollider>();
                if (!suelo.enabled || suelo.isTrigger || fila.position.z <= finAnterior)
                    throw new InvalidOperationException($"Fila inválida: {fila.name}.");
                finAnterior = fila.position.z;
                if (suelo.bounds.size.x < 4f &&
                    (cruces.Count == 0 || !Mathf.Approximately(cruces[cruces.Count - 1], suelo.bounds.center.x)))
                    cruces.Add(suelo.bounds.center.x);
            }
            float[] esperado = { 3.5f, -3.5f, 0f, 3.5f, -3.5f };
            if (cruces.Count != esperado.Length) throw new InvalidOperationException("Faltan cruces.");
            for (int i = 0; i < esperado.Length; i++)
                if (!Mathf.Approximately(cruces[i], esperado[i])) throw new InvalidOperationException($"Cruce incorrecto: {i + 1}.");
            if (datos.FindProperty("recuadro").objectReferenceValue == null ||
                !Mathf.Approximately(datos.FindProperty("metrosAdelante").floatValue, 2f) ||
                !Mathf.Approximately(datos.FindProperty("metrosDeVentaja").floatValue, 12f))
                throw new InvalidOperationException("Los límites guardados no coinciden.");
            OrdenesDelEditor.Avisar("Cruces del Coro", $"Correcto: {filas.arraySize} filas ordenadas y cinco cruces alternos con sus colisiones. Límites 2 m delante y 12 m detrás.");
        }
        finally
        {
            if (abiertaParaProbar) EditorSceneManager.CloseScene(escena, true);
        }
    }
}
