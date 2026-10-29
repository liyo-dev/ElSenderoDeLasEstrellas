using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Herramienta de un solo uso: tras ejecutarla, el menú pasa a El Sendero/Archivo/. Ver INC-605.
public static class RestaurarAnclasDeMainWorld
{
    private const string RutaEscena = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string CarpetaTemporal = "Assets/__RestauracionTemporal";
    private const string RutaTemporal = CarpetaTemporal + "/MainWorld_dbc3a09bf.unity";
    private const string Titulo = "Recuperar el ancla House_FrontDoor de MainWorld";
    private static readonly string[] AnclasARecuperar = { "House_FrontDoor" };

    [MenuItem("El Sendero/Escenas/Recuperar el ancla House_FrontDoor de MainWorld")]
    public static void Restaurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(Titulo, "La recuperación solo se ejecuta fuera de Play Mode.", "Aceptar");
            return;
        }

        Scene mainWorld = SceneManager.GetSceneByPath(RutaEscena);
        if (!mainWorld.IsValid() || !mainWorld.isLoaded)
        {
            EditorUtility.DisplayDialog(Titulo, "Abre y carga MainWorld en el Editor antes de recuperar House_FrontDoor.", "Aceptar");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string raiz = Directory.GetParent(Application.dataPath).FullName;
        string carpeta = Path.Combine(raiz, CarpetaTemporal);
        // Una carpeta ajena no se sobrescribe ni se borra durante la limpieza.
        if (Directory.Exists(carpeta) || File.Exists(carpeta + ".meta"))
        {
            EditorUtility.DisplayDialog(Titulo, "La carpeta temporal ya existe. Retírala antes de ejecutar la recuperación.", "Aceptar");
            return;
        }

        Scene copia = default;
        var restauradas = new List<string>();
        try
        {
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder("Assets", "__RestauracionTemporal")))
                throw new IOException("No se puede crear la carpeta temporal.");
            var inicio = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "show dbc3a09bf:" + RutaEscena,
                WorkingDirectory = raiz,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var proceso = Process.Start(inicio))
            {
                if (proceso == null) throw new InvalidOperationException("No se puede iniciar git.");
                // Copia los bytes originales y lee stderr en paralelo para evitar bloqueos del proceso.
                var error = proceso.StandardError.ReadToEndAsync();
                using (var archivo = File.Create(Path.Combine(raiz, RutaTemporal)))
                    proceso.StandardOutput.BaseStream.CopyTo(archivo);
                proceso.WaitForExit();
                string detalle = error.GetAwaiter().GetResult();
                if (proceso.ExitCode != 0)
                    throw new InvalidOperationException("git show falla: " + detalle);
            }

            AssetDatabase.ImportAsset(RutaTemporal, ImportAssetOptions.ForceSynchronousImport);
            copia = EditorSceneManager.OpenScene(RutaTemporal, OpenSceneMode.Additive);
            Transform destino = BuscarAnclas(mainWorld);
            Transform origen = BuscarAnclas(copia);
            if (destino == null || origen == null)
                throw new InvalidOperationException("No se encuentra GAMEPLAY/ANCHORS en ambas escenas.");

            var existentes = new HashSet<string>(StringComparer.Ordinal);
            foreach (Transform hijo in destino) existentes.Add(hijo.name);
            // Captura el orden original porque mover un hijo cambia los índices de la copia.
            var hijos = new List<Transform>();
            foreach (Transform hijo in origen) hijos.Add(hijo);
            for (int indice = 0; indice < hijos.Count; indice++)
            {
                Transform hijo = hijos[indice];
                // Solo recupera los destinos explícitos; las demás anclas se retiran a propósito.
                if (Array.IndexOf(AnclasARecuperar, hijo.name) < 0) continue;
                if (!existentes.Add(hijo.name)) continue;
                Transform anterior = null;
                if (indice > 0)
                    foreach (Transform candidato in destino)
                        if (candidato.name == hijos[indice - 1].name)
                        {
                            anterior = candidato;
                            break;
                        }
                Vector3 posicion = hijo.localPosition;
                Quaternion rotacion = hijo.localRotation;
                Vector3 escala = hijo.localScale;
                hijo.SetParent(null);
                SceneManager.MoveGameObjectToScene(hijo.gameObject, mainWorld);
                hijo.SetParent(destino, false);
                hijo.localPosition = posicion;
                hijo.localRotation = rotacion;
                hijo.localScale = escala;
                // Respeta al hermano anterior si existe; sin él, coloca el ancla al final.
                if (anterior != null) hijo.SetSiblingIndex(anterior.GetSiblingIndex() + 1);
                else hijo.SetAsLastSibling();
                restauradas.Add(hijo.name);
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog(Titulo, "No se completa la recuperación de House_FrontDoor: " + ex.Message +
                "\nDestinos trasladados: " + restauradas.Count + ". Revisa la escena antes de guardar.", "Aceptar");
            return;
        }
        finally
        {
            // Cierra la copia sin guardar y elimina sus assets incluso si la recuperación falla.
            try
            {
                if (copia.IsValid() && copia.isLoaded) EditorSceneManager.CloseScene(copia, true);
            }
            finally
            {
                AssetDatabase.DeleteAsset(CarpetaTemporal);
            }
        }

        if (restauradas.Count == 0)
        {
            EditorUtility.DisplayDialog(Titulo, "No se recupera House_FrontDoor: ya existe en MainWorld o no está en la copia. No se guarda la escena.", "Aceptar");
            return;
        }
        EditorSceneManager.MarkSceneDirty(mainWorld);
        if (!EditorSceneManager.SaveScene(mainWorld))
        {
            EditorUtility.DisplayDialog(Titulo, "Se recupera House_FrontDoor, pero no se puede guardar MainWorld. Guarda la escena manualmente.", "Aceptar");
            return;
        }
        Debug.Log("[RestaurarAnclasDeMainWorld] Destinos recuperados: " + string.Join(", ", restauradas));
        EditorUtility.DisplayDialog(Titulo, "Se recupera y guarda House_FrontDoor (" + restauradas.Count + " destino).", "Aceptar");
    }

    private static Transform BuscarAnclas(Scene escena)
    {
        foreach (GameObject raiz in escena.GetRootGameObjects())
            if (raiz.name == "GAMEPLAY") return raiz.transform.Find("ANCHORS");
        return null;
    }
}
