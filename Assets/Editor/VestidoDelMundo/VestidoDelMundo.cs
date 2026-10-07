using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Vestido de MainWorld: suelos de calle al estilo del pueblo de Will en el resto de pueblos, ciudad del
/// castillo más rica y parajes con ruinas y hallazgos entre zonas para que explorar no sea aburrido.
///
/// No toca nada que ya estuviera en la escena salvo, si se pide, el giro de las casas generadas para que la
/// puerta dé a su calle (se guarda la posición original y «Quitar» la repone). Todo lo nuevo cuelga de una
/// sola raíz, WORLD/«Vestido del mundo (generado)», que se borra y se rehace en cada ejecución. El suelo se
/// pinta sobre el TerrainData con copia previa exacta en _ClaudeBackups/VestidoDelMundo.
///
/// El pueblo de Will no se toca: es la referencia de estilo. No usa Undo (son miles de objetos y el
/// terreno): para deshacer está el menú «quitar el vestido».
public static partial class VestidoDelMundo
{
    private const string RutaEscena = "Assets/Scenes/Worlds/MainWorld.unity";
    private const string RutaTerreno = "Assets/Scenes/Worlds/MainWorld_data/Recursos/Terreno.asset";
    private const string NombreRaiz = "Vestido del mundo (generado)";
    private const string NombrePadre = "WORLD";
    private const string Etiqueta = "[VestidoDelMundo]";

    [MenuItem("El Sendero/Escenario/MainWorld: vestir (suelos, castillo y parajes)", priority = 30)]
    private static void MenuVestirTodo() => Ejecutar(suelo: true, detalles: true);

    [MenuItem("El Sendero/Escenario/MainWorld: vestir solo los suelos", priority = 31)]
    private static void MenuSoloSuelos() => Ejecutar(suelo: true, detalles: false);

    [MenuItem("El Sendero/Escenario/MainWorld: vestir solo los detalles", priority = 32)]
    private static void MenuSoloDetalles() => Ejecutar(suelo: false, detalles: true);

    [MenuItem("El Sendero/Escenario/MainWorld: quitar el vestido (repone suelo y casas)", priority = 33)]
    private static void MenuQuitar()
    {
        if (!Preparar(out Scene escena, out Terrain terreno)) return;
        if (!EditorUtility.DisplayDialog("Quitar el vestido de MainWorld",
                "Se borra «" + NombreRaiz + "», las casas vuelven a su giro original y el suelo vuelve a la copia guardada.\n\n" +
                "Si has pintado el terreno a mano después del vestido, esa pintura se pierde.", "Quitar", "Cancelar"))
            return;

        var informe = new List<string>();
        try
        {
            BorrarRaiz(escena, informe);
            ReponerCasas(escena, informe);
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            if (HayCopia())
            {
                string error = ReponerCopia(terreno.terrainData);
                if (error != null) informe.Add("Suelo: " + error);
                else
                {
                    AssetDatabase.SaveAssets();
                    EscribirEstado(HuellaSuelo(terreno.terrainData));
                    informe.Add("Suelo: repuesta la copia original.");
                }
            }
            else informe.Add("Suelo: no había copia, no se toca.");
        }
        catch (Exception e)
        {
            informe.Add("ERROR: " + e.Message);
            Debug.LogException(e);
        }
        Terminar("Quitar el vestido", informe);
    }

    private static void Ejecutar(bool suelo, bool detalles)
    {
        if (!Preparar(out Scene escena, out Terrain terreno)) return;
        var informe = new List<string> { $"Vestido de MainWorld — {DateTime.Now:yyyy-MM-dd HH:mm}" };

        try
        {
            EditorUtility.DisplayProgressBar("Vestido de MainWorld", "Preparando el suelo…", 0.05f);
            if (suelo)
            {
                if (!PrepararSuelo(terreno, informe)) { Terminar("Vestido de MainWorld", informe); return; }
            }

            var lienzo = new Lienzo(terreno);
            if (!lienzo.TieneCapas(new[] { CapaHierba, CapaFlores, CapaTierraPiedras, CapaTierra, CapaAdoquin, CapaBaldosa }, out string falta))
            {
                informe.Add($"El terreno no tiene la capa «{falta}». No se ha cambiado nada.");
                Terminar("Vestido de MainWorld", informe);
                return;
            }

            // Las casas se giran y las nuevas se colocan antes de pintar: el suelo se pinta según lo que de
            // verdad queda en la escena (puertas, sendas y patios).
            Obra obra = null;
            if (detalles)
            {
                EditorUtility.DisplayProgressBar("Vestido de MainWorld", "Girando casas y poniendo las nuevas…", 0.15f);
                BorrarRaiz(escena, informe);
                ReponerCasas(escena, informe);
                obra = new Obra
                {
                    Terreno = terreno,
                    Suelo = lienzo,
                    Raiz = CrearRaiz(escena),
                };
                obra.Libres.AddRange(ZonasLibres);
                IndexarEjemplares(escena, obra);
                GirarCasasHaciaSuCalle(escena, obra);
                PonerCasasNuevas(obra);
            }

            if (suelo)
            {
                EditorUtility.DisplayProgressBar("Vestido de MainWorld", "Pintando calles y plazas…", 0.3f);
                PintarZonas(lienzo, escena, informe);
                lienzo.Normalizar();
                terreno.terrainData.SetAlphamaps(0, 0, lienzo.Pesos);
                int quitadas = QuitarHierbaDeSueloDuro(lienzo);
                EditorUtility.SetDirty(terreno.terrainData);
                AssetDatabase.SaveAssets();
                EscribirEstado(HuellaSuelo(terreno.terrainData));
                informe.Add($"Suelo: pintado y guardado ({quitadas} celdas de hierba de detalle retiradas de calles y sendas).");
            }

            if (obra != null)
            {
                EditorUtility.DisplayProgressBar("Vestido de MainWorld", "Colocando detalles…", 0.5f);
                PoblarZonas(obra);
                int obstaculos = NavMeshAutoSetup.ClasificarBajo(obra.Raiz);
                informe.AddRange(obra.Informe);
                informe.Add($"Navegación: {obstaculos} obstáculos (Carve) añadidos a lo nuevo; lo menor de 1 m en planta no talla el NavMesh.");
                informe.Add($"Detalles: {obra.Puestas} piezas colocadas, {obra.Descartadas} descartadas por comprobaciones.");
                foreach (KeyValuePair<string, int> m in obra.Motivos.OrderByDescending(m => m.Value))
                    informe.Add($"  {m.Value,4} × {m.Key}");
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                informe.Add("Escena guardada. Comprueba el paseo de Eldran: El Sendero ▸ Navegación ▸ Diagnóstico: ¿dónde se corta el camino?");
                informe.Add("Las farolas, braseros y casas nuevas reciben su luz y su humo de noche al ejecutar El Sendero ▸ Mundo ▸ Noche: luces de casas, faroles y luciérnagas (lo que ya estaba no se toca).");
            }
        }
        catch (Exception e)
        {
            informe.Add("ERROR: " + e.Message);
            Debug.LogException(e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Terminar("Vestido de MainWorld", informe);
    }

    /// Comprueba que no se está en Play, abre MainWorld si hace falta y localiza su Terrain.
    private static bool Preparar(out Scene escena, out Terrain terreno)
    {
        escena = default;
        terreno = null;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Vestido de MainWorld", "Sal de Play antes de usar esta herramienta.", "Vale");
            return false;
        }
        escena = SceneManager.GetSceneByPath(RutaEscena);
        if (!escena.IsValid() || !escena.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Additive);
        }
        else if (escena.isDirty)
        {
            if (!EditorUtility.DisplayDialog("Vestido de MainWorld",
                    "MainWorld tiene cambios sin guardar. La herramienta guarda la escena al terminar, con tus cambios incluidos.",
                    "Seguir", "Cancelar"))
                return false;
        }
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Terrain t in raiz.GetComponentsInChildren<Terrain>(true))
                if (t.terrainData != null && AssetDatabase.GetAssetPath(t.terrainData) == RutaTerreno) { terreno = t; break; }
        if (terreno == null)
        {
            EditorUtility.DisplayDialog("Vestido de MainWorld", "No encuentro en MainWorld el Terrain que usa " + RutaTerreno + ". No se ha cambiado nada.", "Vale");
            return false;
        }
        return true;
    }

    /// Decide la base sobre la que pintar: la copia original (si el suelo actual es el que dejó el vestido)
    /// o el estado actual (si es la primera vez o alguien ha pintado a mano después).
    private static bool PrepararSuelo(Terrain terreno, List<string> informe)
    {
        TerrainData datos = terreno.terrainData;
        if (!HayCopia())
        {
            GuardarCopia(datos);
            informe.Add("Suelo: copia del original guardada en " + CarpetaCopias + ".");
            return true;
        }
        string estado = LeerEstado();
        string actual = HuellaSuelo(datos);
        if (estado == actual)
        {
            string error = ReponerCopia(datos);
            if (error != null)
            {
                informe.Add("Suelo: no se pudo reponer la copia (" + error + "). No se ha cambiado nada.");
                return false;
            }
            informe.Add("Suelo: se parte de la copia original para repintar.");
            return true;
        }
        EditorUtility.ClearProgressBar();
        int r = EditorUtility.DisplayDialogComplex("Vestido de MainWorld",
            "El suelo del terreno ha cambiado desde la última vez que se vistió (¿pintado a mano?).\n\n" +
            "· «Partir de lo actual» pinta encima de lo que hay ahora. La copia del suelo original no cambia: «Quitar» volverá a ella.\n" +
            "· «Volver a la copia» repone el suelo original (se pierde lo pintado a mano) y pinta encima.",
            "Partir de lo actual", "Cancelar", "Volver a la copia");
        if (r == 1) { informe.Add("Suelo: cancelado."); return false; }
        if (r == 0)
        {
            informe.Add("Suelo: se pinta encima de lo actual; la copia original se conserva.");
            return true;
        }
        string err = ReponerCopia(datos);
        if (err != null) { informe.Add("Suelo: " + err); return false; }
        informe.Add("Suelo: repuesta la copia antigua.");
        return true;
    }

    /// Índice de instancias de prefab de árboles ya presentes en la escena (ver CopiarMaterialesDelVecino).
    private static void IndexarEjemplares(Scene escena, Obra o)
    {
        var buscados = new HashSet<string>(ArbolesDeJardin);
        foreach (GameObject raiz in escena.GetRootGameObjects())
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                string ruta = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if (!buscados.Contains(ruta)) continue;
                if (!o.EjemplaresPorPrefab.TryGetValue(ruta, out List<GameObject> l)) o.EjemplaresPorPrefab[ruta] = l = new List<GameObject>();
                l.Add(t.gameObject);
            }
    }

    private static Transform CrearRaiz(Scene escena)
    {
        Transform padre = escena.GetRootGameObjects().FirstOrDefault(g => g.name == NombrePadre)?.transform;
        var raiz = new GameObject(NombreRaiz);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        if (padre != null) raiz.transform.SetParent(padre, false);
        return raiz.transform;
    }

    /// Raíz de lo generado: bajo WORLD o, si no hay WORLD, en la raíz de la escena.
    private static Transform BuscarRaiz(Scene escena)
    {
        foreach (GameObject g in escena.GetRootGameObjects())
        {
            if (g.name == NombreRaiz) return g.transform;
            Transform t = g.transform.Find(NombreRaiz);
            if (t != null) return t;
        }
        return null;
    }

    private static void BorrarRaiz(Scene escena, List<string> informe)
    {
        Transform raiz = BuscarRaiz(escena);
        if (raiz == null) return;
        UnityEngine.Object.DestroyImmediate(raiz.gameObject);
        informe.Add("Borrado el vestido anterior.");
    }

    private static void Terminar(string titulo, List<string> informe)
    {
        EditorUtility.ClearProgressBar();
        string texto = string.Join("\n", informe);
        Debug.Log($"{Etiqueta} {titulo}\n{texto}");
        try
        {
            Directory.CreateDirectory(RutaCopias);
            File.WriteAllText(Path.Combine(RutaCopias, "Informe.txt"), texto, Encoding.UTF8);
        }
        catch (IOException) { }
        string resumen = informe.Count > 18 ? string.Join("\n", informe.Take(18)) + "\n… (informe completo en la consola y en " + CarpetaCopias + "/Informe.txt)" : texto;
        EditorUtility.DisplayDialog(titulo, resumen, "Vale");
    }
}
