using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Añade el componente que mueve las cejas al pelo o a la pieza de ceja de un prefab de
/// personaje y enlaza sus referencias. El componente hace el resto en tiempo de ejecución.
public static class PrepararCejasPorEmocion
{
    private const string RutaWill = "Assets/Prefabs/_WILL.prefab";
    private static readonly string[] CarpetasDePersonajes = { "Assets/_NPCs", "Assets/Prefabs" };
    private const string SinCejas = "sin-cejas";

    [MenuItem("El Sendero/Personajes/Cejas/Preparar en Will")]
    private static void PrepararWill() => Preparar(RutaWill, null);

    [MenuItem("El Sendero/Personajes/Cejas/Preparar en el prefab seleccionado")]
    private static void PrepararSeleccionado()
    {
        GameObject seleccionado = Selection.activeGameObject;
        if (seleccionado == null)
        {
            Debug.LogWarning("[Cejas] Selecciona un prefab de personaje en el Project.");
            return;
        }
        string ruta = AssetDatabase.GetAssetPath(seleccionado);
        if (string.IsNullOrEmpty(ruta) || !ruta.EndsWith(".prefab"))
        {
            Debug.LogWarning("[Cejas] El objeto seleccionado no es un prefab.");
            return;
        }
        Preparar(ruta, null);
    }

    [MenuItem("El Sendero/Personajes/Cejas/Preparar en todos los personajes")]
    public static void PrepararTodos()
    {
        var rutas = Personajes();
        var informe = new StringBuilder();
        int montados = 0, sinCejas = 0, conProblema = 0;
        foreach (var ruta in rutas)
        {
            string problema = Preparar(ruta, informe);
            if (problema == null) montados++;
            else if (problema == SinCejas) sinCejas++;
            else conProblema++;
        }
        informe.Insert(0, $"Montadas: {montados} | sin cejas en la malla: {sinCejas} | con problemas: {conProblema} | total: {rutas.Count}\n\n");
        EscribirInforme(informe.ToString());
        Debug.Log($"[Cejas] {montados} personaje(s) con cejas montadas, {sinCejas} sin cejas en la malla, {conProblema} con problemas.");
    }

    [MenuItem("El Sendero/Personajes/Cejas/Comprobar todos los personajes")]
    public static void ComprobarTodos()
    {
        var rutas = Personajes();
        var informe = new StringBuilder();
        int conCejas = 0, montadas = 0;
        foreach (var ruta in rutas)
        {
            var resultado = Analizar(ruta);
            if (resultado.tieneCejas) conCejas++;
            if (resultado.montadas) montadas++;
            informe.AppendLine($"{ruta}: {resultado.descripcion}");
        }
        informe.Insert(0, $"Con el par de cejas en su malla: {conCejas} de {rutas.Count} | ya montadas: {montadas}\n\n");
        EscribirInforme(informe.ToString());
        Debug.Log($"[Cejas] {conCejas} de {rutas.Count} personajes tienen el par de cejas; {montadas} ya lo tienen montado.");
    }

    /// Monta el componente de cejas en el prefab. Devuelve null si todo va bien, o el motivo
    /// del fallo para poder recogerlo en el informe del lote.
    private static string Preparar(string ruta, StringBuilder informe)
    {
        if (informe != null) informe.AppendLine($"{ruta}");

        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        if (raiz == null)
        {
            Anotar(informe, "no se pudo abrir el prefab");
            return "no-abrir";
        }

        try
        {
            Transform cabeza = Buscar(raiz.transform, "head");
            if (cabeza == null)
            {
                Anotar(informe, "no tiene un hueso «head»");
                return "sin-cabeza";
            }

            NPCEmotionController controlador = BuscarControlador(raiz);
            if (controlador == null)
            {
                Anotar(informe, "no tiene NPCEmotionController");
                return "sin-controlador";
            }
            if (controlador.EmotionProfile == null)
            {
                Anotar(informe, "su NPCEmotionController no tiene EmotionProfile: no cambiaría de cara");
                return "sin-perfil";
            }

            Renderer ojos = BuscarOjos(cabeza);
            if (ojos == null)
            {
                Anotar(informe, "no tiene ninguna malla de ojos de referencia");
                return "sin-ojos";
            }

            var encontradas = BuscarCejas(cabeza, ojos);
            if (encontradas.soporte == null)
            {
                // Si el personaje ya traía el componente de antes, se quita: sus cejas ya no
                // están en la pieza que lo tenía.
                bool habia = QuitarComponentes(raiz);
                if (habia) PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                Anotar(informe, "ninguna pieza de la cabeza contiene el par de cejas");
                return SinCejas;
            }

            var filtro = encontradas.soporte.GetComponent<MeshFilter>();
            var componente = encontradas.soporte.GetComponent<CejasPorEmocion>();
            if (componente == null) componente = encontradas.soporte.gameObject.AddComponent<CejasPorEmocion>();

            // Una sola instancia por personaje: si la pieza cambió, la anterior sobra.
            bool haySobrante = QuitarComponentes(raiz, componente);

            // El pelo ya trae sus propias cejas: la pieza suelta sobraría y se verían dos.
            bool apagaSobrante = encontradas.sobrante != null;

            var so = new SerializedObject(componente);
            bool cambia = haySobrante || apagaSobrante
                || so.FindProperty("controlador").objectReferenceValue != controlador
                || so.FindProperty("mallaDeLasCejas").objectReferenceValue != filtro
                || so.FindProperty("ojosDeReferencia").objectReferenceValue != ojos;
            if (!cambia)
            {
                Anotar(informe, $"ya estaba montado en «{encontradas.soporte.name}» ({encontradas.izquierda}+{encontradas.derecha} vértices)");
                return null;
            }

            so.FindProperty("controlador").objectReferenceValue = controlador;
            so.FindProperty("mallaDeLasCejas").objectReferenceValue = filtro;
            so.FindProperty("ojosDeReferencia").objectReferenceValue = ojos;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(componente);

            if (apagaSobrante)
            {
                encontradas.sobrante.gameObject.SetActive(false);
                Anotar(informe, $"se apaga la pieza «{encontradas.sobrante.name}»: el pelo ya trae cejas");
            }

            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            Anotar(informe, $"cejas montadas en «{encontradas.soporte.name}» ({encontradas.izquierda}+{encontradas.derecha} vértices)");
            return null;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private struct Analisis
    {
        public bool tieneCejas;
        public bool montadas;
        public string descripcion;
    }

    /// Comprueba si el prefab tiene el par de cejas y si ya las tiene montadas, sin modificarlo.
    private static Analisis Analizar(string ruta)
    {
        var resultado = new Analisis { descripcion = "sin información" };
        GameObject raiz = PrefabUtility.LoadPrefabContents(ruta);
        if (raiz == null)
        {
            resultado.descripcion = "no se pudo abrir el prefab";
            return resultado;
        }
        try
        {
            Transform cabeza = Buscar(raiz.transform, "head");
            if (cabeza == null)
            {
                resultado.descripcion = "sin hueso «head»";
                return resultado;
            }
            Renderer ojos = BuscarOjos(cabeza);
            if (ojos == null)
            {
                resultado.descripcion = "sin ojos de referencia";
                return resultado;
            }
            var encontradas = BuscarCejas(cabeza, ojos);
            if (encontradas.soporte == null)
            {
                resultado.descripcion = "ninguna pieza contiene el par de cejas";
                return resultado;
            }
            resultado.tieneCejas = true;
            resultado.montadas = encontradas.soporte.GetComponent<CejasPorEmocion>() != null;
            resultado.descripcion = $"cejas en «{encontradas.soporte.name}» ({encontradas.izquierda}+{encontradas.derecha} vértices) — " +
                (resultado.montadas ? "montadas" : "SIN MONTAR");
            return resultado;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private struct CejasEncontradas
    {
        public MeshRenderer soporte;
        public int izquierda;
        public int derecha;
        /// Pieza de ceja suelta que sobraría porque el pelo ya trae las suyas.
        public MeshRenderer sobrante;
    }

    /// Localiza las cejas del personaje. Solo cuentan las piezas del rig modular que las
    /// llevan: el pelo y el sombrero (que traen sus propias cejas) y la pieza suelta de ceja
    /// (para los personajes sin pelo). Las mallas de ojos y de cabeza quedan fuera: sus islas
    /// se parecerían a un par de cejas sin serlo.
    private static CejasEncontradas BuscarCejas(Transform cabeza, Renderer ojos)
    {
        var resultado = new CejasEncontradas();
        MeshRenderer pelo = null;
        int peloIzq = 0, peloDer = 0;
        MeshRenderer suelta = null;
        int sueltaIzq = 0, sueltaDer = 0;

        foreach (var renderer in cabeza.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null) continue;
            var filtro = renderer.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null) continue;
            bool esPiezaSuelta = renderer.name.StartsWith("Eyebrow");
            bool esPelo = renderer.name.StartsWith("Hair") || renderer.name.StartsWith("Hat");
            if (!esPiezaSuelta && !esPelo) continue;
            if (!renderer.gameObject.activeSelf) continue;

            var bounds = CejasPorEmocion.BoundsDeOjosEnEspacio(ojos, filtro.transform);
            if (!CejasPorEmocion.ComprobarParesDeCejas(filtro.sharedMesh, bounds, out int izq, out int der))
                continue;

            if (esPelo)
            {
                if (pelo == null) { pelo = renderer; peloIzq = izq; peloDer = der; }
            }
            else if (suelta == null)
            {
                suelta = renderer; sueltaIzq = izq; sueltaDer = der;
            }
        }

        if (pelo != null)
        {
            resultado.soporte = pelo;
            resultado.izquierda = peloIzq;
            resultado.derecha = peloDer;
            resultado.sobrante = suelta;
            return resultado;
        }

        resultado.soporte = suelta;
        resultado.izquierda = sueltaIzq;
        resultado.derecha = sueltaDer;
        return resultado;
    }

    /// El controlador que puede cambiar de cara es el que tiene perfil: es el que emite el
    /// evento de cambio de emoción que mueve las cejas.
    private static NPCEmotionController BuscarControlador(GameObject raiz)
    {
        var todos = raiz.GetComponentsInChildren<NPCEmotionController>(true);
        foreach (var c in todos)
            if (c != null && c.EmotionProfile != null) return c;
        return todos.Length > 0 ? todos[0] : null;
    }

    private static List<string> Personajes()
    {
        var rutas = new List<string>();
        foreach (var carpeta in CarpetasDePersonajes)
        {
            if (!AssetDatabase.IsValidFolder(carpeta)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { carpeta }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (ruta.Contains("/UI/") || ruta.Contains("/Enemy/") || ruta.Contains("/OverHead/")) continue;
                if (ruta.Contains("Versiones antiguas")) continue;
                rutas.Add(ruta);
            }
        }
        rutas.Sort();
        return rutas;
    }

    private static void Anotar(StringBuilder informe, string mensaje)
    {
        if (informe != null) informe.AppendLine($"    → {mensaje}");
        else Debug.LogWarning($"[Cejas] {mensaje}");
    }

    private static void EscribirInforme(string contenido)
    {
        const string ruta = "Logs/Cejas/InformeDeCejas.txt";
        var carpeta = Path.GetDirectoryName(ruta);
        if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);
        File.WriteAllText(ruta, contenido, Encoding.UTF8);
        Debug.Log($"[Cejas] Informe escrito en {ruta}");
    }

    private static Transform Buscar(Transform padre, string nombre)
    {
        if (padre.name == nombre) return padre;
        foreach (Transform hijo in padre)
        {
            Transform encontrado = Buscar(hijo, nombre);
            if (encontrado != null) return encontrado;
        }
        return null;
    }

    /// Quita las instancias del componente que no sean la indicada. Devuelve si había alguna.
    private static bool QuitarComponentes(GameObject raiz, CejasPorEmocion conservar = null)
    {
        bool habia = false;
        foreach (var otro in raiz.GetComponentsInChildren<CejasPorEmocion>(true))
        {
            if (otro == conservar) continue;
            Object.DestroyImmediate(otro, true);
            habia = true;
        }
        return habia;
    }

    /// La malla de ojos que sirve de referencia. Las piezas de ceja empiezan igual que las de
    /// ojo («Eye…»), así que se descartan: el dígito detrás del prefijo es lo que las
    /// distingue. Si no hay ninguna encendida se usa una apagada: la caja que ocupa la malla
    /// es la misma en los dos casos y sirve igual de referencia.
    private static Renderer BuscarOjos(Transform cabeza)
    {
        Renderer apagado = null;
        foreach (var renderer in cabeza.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null) continue;
            if (!renderer.name.StartsWith("Eye") || renderer.name.Length <= 3) continue;
            if (!char.IsDigit(renderer.name[3])) continue;
            if (renderer.gameObject.activeInHierarchy && renderer.enabled) return renderer;
            if (apagado == null) apagado = renderer;
        }
        return apagado;
    }
}
