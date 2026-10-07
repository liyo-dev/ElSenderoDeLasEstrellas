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
                "Si has pintado el terreno a mano después del vestido, esa pintura se pierde." + AvisosAntesDeBorrar(escena), "Quitar", "Cancelar"))
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
                    BorrarCopia(Copia.Base);
                    BorrarCopia(Copia.Vestido);
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
        if (detalles)
        {
            string avisos = AvisosAntesDeBorrar(escena);
            if (avisos.Length > 0 && !EditorUtility.DisplayDialog("Vestido de MainWorld", avisos.TrimStart(), "Rehacer igualmente", "Cancelar"))
                return;
        }
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
                GuardarCopia(terreno.terrainData, Copia.Vestido);
                EscribirEstado(HuellaSuelo(terreno.terrainData));
                informe.Add($"Suelo: pintado y guardado ({quitadas} celdas de hierba de detalle retiradas de calles y sendas).");
            }

            if (obra != null)
            {
                EditorUtility.DisplayProgressBar("Vestido de MainWorld", "Colocando detalles…", 0.5f);
                PoblarZonas(obra);
                int obstaculos = NavMeshAutoSetup.ClasificarBajo(obra.Raiz);
                GuardarHuellaRaiz(obra.Raiz);
                informe.AddRange(obra.Informe);
                informe.Add($"Navegación: {obstaculos} obstáculos (Carve) añadidos a lo nuevo; lo menor de 1 m en planta no talla el NavMesh.");
                informe.Add($"Detalles: {obra.Puestas} piezas colocadas, {obra.Descartadas} descartadas por comprobaciones.");
                foreach (KeyValuePair<string, int> m in obra.Motivos.OrderByDescending(m => m.Value))
                    informe.Add($"  {m.Value,4} × {m.Key}");
                EditorSceneManager.MarkSceneDirty(escena);
                EditorSceneManager.SaveScene(escena);
                informe.Add("Escena guardada. Comprueba el paseo de Eldran: El Sendero ▸ Navegación ▸ Diagnóstico: ¿dónde se corta el camino?");
                informe.Add("Las farolas y las casas nuevas reciben su luz y su humo de noche al ejecutar El Sendero ▸ Mundo ▸ Noche: luces de casas, faroles y luciérnagas (lo que ya estaba no se toca).");
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
            Copia copia = HayCopia(Copia.Base) ? Copia.Base : Copia.Original;
            string error = ReponerCopia(datos, copia);
            if (error != null)
            {
                informe.Add("Suelo: no se pudo reponer la copia (" + error + "). No se ha cambiado nada.");
                return false;
            }
            informe.Add(copia == Copia.Base
                ? "Suelo: se parte de la base guardada (con lo pintado a mano) para repintar."
                : "Suelo: se parte de la copia original para repintar.");
            return true;
        }
        EditorUtility.ClearProgressBar();
        int r = EditorUtility.DisplayDialogComplex("Vestido de MainWorld",
            "El suelo del terreno ha cambiado desde la última vez que se vistió (¿pintado a mano?).\n\n" +
            "· «Partir de lo actual» conserva lo pintado a mano (las celdas que ya no son las que dejó el vestido) y repinta el resto; las próximas veces se parte de esa base. La copia del suelo original no cambia: «Quitar» volverá a ella.\n" +
            "· «Volver a la copia» repone el suelo original (se pierde lo pintado a mano) y pinta encima.",
            "Partir de lo actual", "Cancelar", "Volver a la copia");
        if (r == 1) { informe.Add("Suelo: cancelado."); return false; }
        if (r == 0)
        {
            string e = GuardarBaseConRetoques(datos, out int retocadas);
            if (e == null) e = ReponerCopia(datos, Copia.Base);
            if (e != null) { informe.Add("Suelo: " + e); return false; }
            informe.Add(retocadas >= 0
                ? $"Suelo: se conservan {retocadas} celdas pintadas a mano; se repinta desde la base guardada. La copia original no cambia."
                : "Suelo: lo actual queda como base para repintar (no había copia de la última pintura). La copia original no cambia.");
            return true;
        }
        string err = ReponerCopia(datos);
        if (err != null) { informe.Add("Suelo: " + err); return false; }
        BorrarCopia(Copia.Base);
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

    private const string PrefijoHuellaRaiz = "Huella del vestido: ";

    private const string AvisoDeRetoques =
        "Hay cambios hechos a mano dentro de «" + NombreRaiz + "» (piezas movidas, borradas o añadidas, o componentes nuevos). " +
        "Rehacer o quitar el vestido borra esa raíz entera y lo que se haya enganchado a ella queda sin referencia. " +
        "Si quieres conservar algo, sácalo antes de esa raíz.";

    /// Huella de lo generado: nombre, pose y componentes de todo lo que cuelga de la raíz. No cuenta lo que
    /// añade el menú de Noche (hijos «LuzNocturna…» y «Humo»), que se rehace solo.
    private static string HuellaRaiz(Transform raiz)
    {
        uint h = 2166136261u;
        void Mezclar(string texto)
        {
            unchecked { foreach (char c in texto) { h ^= c; h *= 16777619u; } }
        }

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t == raiz || t.name.StartsWith(PrefijoHuellaRaiz) || EsDeLaNoche(t, raiz)) continue;
            Vector3 p = t.position, e = t.eulerAngles, s = t.lossyScale;
            Mezclar(t.name);
            Mezclar(FormattableString.Invariant($"{p.x:F1},{p.y:F1},{p.z:F1},{e.x:F0},{e.y:F0},{e.z:F0},{s.x:F1},{s.y:F1},{s.z:F1},{t.gameObject.activeSelf}"));
            foreach (Component c in t.GetComponents<Component>())
                if (c != null) Mezclar(c.GetType().Name);
        }
        return h.ToString("x8");
    }

    private static bool EsDeLaNoche(Transform t, Transform raiz)
    {
        for (; t != null && t != raiz; t = t.parent)
            if (t.name.StartsWith("LuzNocturna") || t.name == "Humo") return true;
        return false;
    }

    private static void GuardarHuellaRaiz(Transform raiz)
    {
        var marca = new GameObject(PrefijoHuellaRaiz + HuellaRaiz(raiz)) { tag = "EditorOnly" };
        marca.transform.SetParent(raiz, false);
    }

    /// Avisos para el diálogo antes de borrar lo generado: retoques a mano y referencias desde fuera.
    private static string AvisosAntesDeBorrar(Scene escena)
    {
        var sb = new StringBuilder();
        if (RaizRetocada(escena)) sb.Append("\n\n").Append(AvisoDeRetoques);
        Transform raiz = BuscarRaiz(escena);
        List<string> refs = raiz != null ? ReferenciasExternas(escena, raiz) : new List<string>();
        if (refs.Count > 0)
        {
            sb.Append("\n\nAlgo de fuera apunta a piezas del vestido y se quedaría sin referencia:");
            foreach (string r in refs.Take(8)) sb.Append("\n · ").Append(r);
            if (refs.Count > 8) sb.Append($"\n · … y {refs.Count - 8} más");
        }
        return sb.ToString();
    }

    private static readonly HashSet<string> TiposSinReferencias = new()
    {
        "float", "int", "bool", "string", "double", "long", "byte", "char", "short", "uint",
        "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Rect", "Bounds", "Matrix4x4", "AnimationCurve", "Keyframe",
    };

    /// Scripts y directores de la escena (fuera de la raíz) con algún campo que apunte a algo de la raíz.
    private static List<string> ReferenciasExternas(Scene escena, Transform raiz)
    {
        var r = new List<string>();
        foreach (GameObject g in escena.GetRootGameObjects())
            foreach (Component c in g.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.transform.IsChildOf(raiz)) continue;
                if (!(c is MonoBehaviour) && !(c is UnityEngine.Playables.PlayableDirector)) continue;
                var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool entrar = true;
                while (it.Next(entrar))
                {
                    entrar = it.propertyType == SerializedPropertyType.Generic &&
                             !(it.isArray && TiposSinReferencias.Contains(it.arrayElementType));
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    UnityEngine.Object v = it.objectReferenceValue;
                    Transform t = v is GameObject go ? go.transform : v is Component k ? k.transform : null;
                    if (t == null || !t.IsChildOf(raiz)) continue;
                    r.Add($"{c.gameObject.name} ({c.GetType().Name}.{it.displayName}) → {t.name}");
                    break;
                }
            }
        return r;
    }

    /// Si lo generado ha cambiado desde que se generó (retoques a mano dentro de la raíz).
    private static bool RaizRetocada(Scene escena)
    {
        Transform raiz = BuscarRaiz(escena);
        if (raiz == null) return false;
        foreach (Transform t in raiz)
            if (t.name.StartsWith(PrefijoHuellaRaiz))
                return t.name.Substring(PrefijoHuellaRaiz.Length) != HuellaRaiz(raiz);
        return false;
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
