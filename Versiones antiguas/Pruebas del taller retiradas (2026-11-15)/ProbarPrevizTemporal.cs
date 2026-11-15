#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Prueba de humo de un solo uso: hornea un guion pequeño en seco (sin escribir el asset), saca su
/// previz y prueba el camino de aplicar un encuadre + diff. Se retira a «Versiones antiguas» al usarla.
public static class ProbarPrevizTemporal
{
    private const string RutaGuion = "Assets/_SEQUENCES/Guiones/Cap3_01_ElEspejo.guion.txt";
    private const string Escena = "TorreDeLiam";

    [MenuItem("El Sendero/Cinemáticas/Guion/zz Probar el previz (temporal)")]
    public static void Probar()
    {
        bool yaAbierta = SceneManager.GetSceneByName(Escena).isLoaded;

        GuionHorneado horneado = HorneadorDeGuion.HornearParaRevisar(RutaGuion, out Horno horno);
        if (horno == null)
        {
            Log("no se ha podido hornear");
            CerrarSiLaAbrimos(yaAbierta);
            return;
        }

        Log($"errores={horno.Errores.Count} avisos={horno.Avisos.Count} " +
            $"guion={(horneado != null)} nombre={horneado?.nombre} escena={horneado?.escena} " +
            $"ruta={horneado?.rutaDelTexto} duracion={horneado?.duracion:0.0} " +
            $"planos={horneado?.planos?.Count ?? 0} actores={horneado?.actores?.Count ?? 0} " +
            $"linea0={(horneado != null && horneado.planos.Count > 0 ? horneado.planos[0].linea : 0)}");
        foreach (string error in horno.Errores)
            Log("error: " + error);

        if (horneado != null)
        {
            var fotos = PrevizDeGuion.Generar(horneado, out string error);
            Log($"previz={fotos.Count} error={error} " +
                $"primera={(fotos.Count > 0 ? fotos[0].ruta : "-")}");

            ProbarDiff(horneado);
            UnityEngine.Object.DestroyImmediate(horneado);
        }

        CerrarSiLaAbrimos(yaAbierta);
    }

    /// Aplica un cambio de lente en memoria, vuelve a hornear y compara. No escribe ni el .txt ni el
    /// asset: es exactamente lo que hace el taller antes de enseñar el diff.
    private static void ProbarDiff(GuionHorneado antes)
    {
        PlanoHorneado plano = antes.planos[0];
        string texto = File.ReadAllText(RutaGuion);
        var cambios = new Dictionary<string, string> { ["lente"] = "28" };

        if (!GuionTextoEditable.ReescribirEnTexto(texto, plano.linea, cambios, out string nuevo))
        {
            Log("diff: no se ha podido reescribir la línea");
            return;
        }

        GuionHorneado despues = HorneadorDeGuion.HornearParaRevisar(RutaGuion, nuevo, out Horno horno);
        if (despues == null)
        {
            Log($"diff: el segundo horneado no salió ({horno?.Errores.Count ?? 0} errores)");
            return;
        }

        var diff = GuionDiff.Comparar(antes, despues);
        Log($"diff={diff.Count}: {string.Join(" | ", diff)}");
        UnityEngine.Object.DestroyImmediate(despues);
    }

    private static void CerrarSiLaAbrimos(bool yaAbierta)
    {
        if (yaAbierta) return;
        Scene escena = SceneManager.GetSceneByName(Escena);
        if (escena.IsValid() && escena.isLoaded)
            EditorSceneManager.CloseScene(escena, true);
    }

    private static void Log(string texto)
    {
        string carpeta = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes");
        Directory.CreateDirectory(carpeta);
        File.AppendAllText(Path.Combine(carpeta, "taller_smoke.log"),
            $"[{DateTime.Now:HH:mm:ss}] {texto}\n", new UTF8Encoding(false));
    }
}
#endif
