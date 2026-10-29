using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Verificación del mecanismo de cejas sin entrar en Play: monta el componente a mano (su
/// Awake y su OnEnable), cambia la emoción del controlador y comprueba que los vértices de las
/// cejas se mueven y que el resto de la malla no se toca.
public static class VerificarCejasEnRuntime
{
    private const BindingFlags Privado = BindingFlags.NonPublic | BindingFlags.Instance;

    [MenuItem("El Sendero/Archivo/Cejas/Verificar el mecanismo")]
    public static void Ejecutar()
    {
        var rutas = new List<string>();
        foreach (var carpeta in new[] { "Assets/_NPCs", "Assets/Prefabs" })
        {
            if (!AssetDatabase.IsValidFolder(carpeta)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { carpeta }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (ruta.Contains("/UI/") || ruta.Contains("/Enemy/")) continue;
                if (ruta.Contains("Versiones antiguas")) continue;
                rutas.Add(ruta);
            }
        }
        rutas.Sort();

        var texto = new StringBuilder();
        int ok = 0, conProblema = 0;
        foreach (var ruta in rutas)
        {
            string linea = Verificar(ruta);
            texto.AppendLine(linea);
            if (linea.Contains("OK")) ok++; else conProblema++;
        }
        texto.Insert(0, $"Correctos: {ok} | con problemas: {conProblema} | total: {rutas.Count}\n\n");

        const string salida = "Logs/Cejas/VerificacionDeCejas.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(salida));
        File.WriteAllText(salida, texto.ToString(), Encoding.UTF8);
        Debug.Log($"[Cejas] Verificación: {ok} correctos, {conProblema} con problemas. Informe en {salida}\n{texto}");
    }

    private static string Verificar(string ruta)
    {
        GameObject instancia = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null) return $"{ruta}: no se pudo cargar el prefab";
            instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instancia.name = "PruebaCejas";

            var cejas = instancia.GetComponentInChildren<CejasPorEmocion>(true);
            if (cejas == null) return $"{ruta}: SIN COMPONENTE CejasPorEmocion";

            var controlador = ObtenerControlador(instancia);
            if (controlador == null) return $"{ruta}: sin NPCEmotionController";
            if (controlador.EmotionProfile == null) return $"{ruta}: el controlador no tiene EmotionProfile";

            // Se despiertan a mano los componentes que lo hacen en Play.
            Invocar(controlador, "Awake");
            Invocar(controlador, "Start");
            Invocar(cejas, "Awake");
            Invocar(cejas, "OnEnable");

            var preparado = Leer(cejas, "_preparado");
            if (!(preparado is bool b) || !b) return $"{ruta}: el componente no se pudo preparar (no encuentra el par de cejas)";

            // La malla de las cejas tiene que seguir encendida en Play: si el controlador de
            // emociones la apaga, el movimiento no se vería.
            var filtro = cejas.GetComponent<MeshFilter>();
            if (filtro == null) return $"{ruta}: el componente no tiene malla";
            if (!filtro.gameObject.activeSelf) return $"{ruta}: la malla de las cejas «{filtro.name}» está apagada: no se vería el movimiento";
            var renderer = filtro.GetComponent<MeshRenderer>();
            if (renderer != null && !renderer.enabled) return $"{ruta}: el renderer de las cejas está deshabilitado";

            var antes = Leer(cejas, "_poseObjetivo");
            controlador.SetEmotion(NPCEmotion.Angry);
            var despues = Leer(cejas, "_poseObjetivo");

            if (EsMismaPose(antes, despues))
                return $"{ruta}: la emoción NO llegó al componente de cejas";

            // Se aplica la pose objetivo de golpe y se mide el desplazamiento de la malla.
            Escribir(cejas, "_poseActual", despues);
            Invocar(cejas, "ActualizarMalla");

            var baseVertices = Leer(cejas, "_verticesBase") as Vector3[];
            var actuales = Leer(cejas, "_verticesActuales") as Vector3[];
            var izq = Leer(cejas, "_indicesIzquierda") as int[];
            var der = Leer(cejas, "_indicesDerecha") as int[];
            if (baseVertices == null || actuales == null || izq == null || der == null)
                return $"{ruta}: no se pudieron leer los datos de la malla";

            float mayor = 0f;
            var juntos = new HashSet<int>(izq);
            foreach (int i in der) juntos.Add(i);
            foreach (int i in juntos)
                mayor = Mathf.Max(mayor, Vector3.Distance(baseVertices[i], actuales[i]));

            float fuera = 0f;
            for (int i = 0; i < baseVertices.Length; i++)
            {
                if (juntos.Contains(i)) continue;
                fuera = Mathf.Max(fuera, Vector3.Distance(baseVertices[i], actuales[i]));
            }

            // La escala de la malla local respecto al mundo: lo que se ve en pantalla.
            float escala = filtro.transform.lossyScale.y;

            string texto = $"{ruta}: OK cejas movidas {mayor.ToString("F5", CultureInfo.InvariantCulture)} " +
                $"(mundo {mayor * escala:F5}) | resto de la malla movido {fuera:F5} | " +
                $"islas {izq.Length}+{der.Length}";
            if (mayor < 0.0005f) return $"{ruta}: la emoción llegó pero las cejas apenas se mueven ({mayor:F5})";
            if (fuera > 0.00001f) return $"{ruta}: las cejas se mueven pero también el resto de la malla ({fuera:F5})";
            return texto;
        }
        catch (System.Exception ex)
        {
            return $"{ruta}: EXCEPCIÓN {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            if (instancia != null) UnityEngine.Object.DestroyImmediate(instancia);
        }
    }

    private static NPCEmotionController ObtenerControlador(GameObject instancia)
    {
        var todos = instancia.GetComponentsInChildren<NPCEmotionController>(true);
        foreach (var c in todos)
            if (c != null && c.EmotionProfile != null) return c;
        return todos.Length > 0 ? todos[0] : null;
    }

    private static void Invocar(object objetivo, string metodo)
    {
        var info = objetivo.GetType().GetMethod(metodo, Privado);
        info?.Invoke(objetivo, null);
    }

    private static object Leer(object objetivo, string campo)
        => objetivo.GetType().GetField(campo, Privado)?.GetValue(objetivo);

    private static void Escribir(object objetivo, string campo, object valor)
        => objetivo.GetType().GetField(campo, Privado)?.SetValue(objetivo, valor);

    /// La pose es una estructura anidada con dos lados de cuatro valores: se aplana para
    /// poder compararla sin depender de su representación.
    private static float[] Aplanar(object pose)
    {
        if (pose == null) return null;
        var valores = new List<float>();
        foreach (var lado in pose.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            object valor = lado.GetValue(pose);
            if (valor == null) continue;
            foreach (var campo in valor.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                valores.Add(Convert.ToSingle(campo.GetValue(valor)));
        }
        return valores.ToArray();
    }

    private static bool EsMismaPose(object a, object b)
    {
        float[] x = Aplanar(a);
        float[] y = Aplanar(b);
        if (x == null || y == null || x.Length != y.Length) return true;
        for (int i = 0; i < x.Length; i++)
            if (Mathf.Abs(x[i] - y[i]) > 0.0000001f) return false;
        return true;
    }
}
