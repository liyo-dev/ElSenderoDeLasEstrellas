#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Buzón de órdenes para el Editor: deja un fichero «*.orden» en «Guiones/_ordenes/» y el Editor
/// lo ejecuta en cuanto puede, aunque su ventana no esté delante. Así Claude puede capturar,
/// hornear o probar sin tocar el ratón de Raúl.
///
/// Una orden por fichero (primera línea):
///   refrescar                               reimporta y recompila lo que haya cambiado
///   capturar Prologo_Valle                  captura del escenario
///   hornear Assets/.../X.guion.txt [storyboard]
///   menu El Sendero/Secuencias/...          ejecuta un menú del Editor
///   play / stop                             entra o sale del modo Play
///   cerca X Z [radio]                       lista lo que hay alrededor de un punto (todas las escenas)
///   donde texto                             lista dónde está cada objeto cuyo nombre contiene ese texto (todas las escenas)
///   rayo X Z                                lo que atraviesa un rayo vertical en ese punto, de arriba abajo
///                                           (colliders, triggers incluidos, con su capa y altura)
///   catalogo nombre carpeta regex           foto de los prefabs de la carpeta cuyo nombre cumple el regex (hasta 12, en fila,
///                                           con su nombre en el registro) → Guiones/_fotos/nombre.png; necesita una escena con luz abierta
///   escena Nombre [aditiva]                 abre una escena (sola, solo si no hay cambios sin guardar; aditiva, siempre)
///   cerrar Nombre [descartar]               cierra una escena abierta (con cambios sin guardar, solo con «descartar», que los tira)
///   camara_de_prueba                        en Play, crea una cámara principal si no hay ninguna
///   foto nombre x y z mx my mz [lente]       foto en perspectiva desde (x,y,z) mirando a (mx,my,mz),
///                                           con todas las escenas abiertas → Guiones/_fotos/nombre.png
///   gemini RUTA.mp4 [fps=2]                 manda un vídeo a Gemini (resultado en Logs/Gemini/)
///
/// El resultado queda en «Guiones/_ordenes/registro.log» y el fichero pasa a «*.hecha».
///
/// Mientras corre una orden no puede abrirse ningún diálogo (el Editor se quedaría esperando a un
/// clic que nadie va a dar). Los menús que preguntan o informan con un diálogo usan
/// <see cref="Confirmar"/> y <see cref="Avisar"/>: desde el buzón no preguntan (siguen adelante) y
/// lo que habrían enseñado va al registro, detrás del resultado de la orden.
[InitializeOnLoad]
public static class OrdenesDelEditor
{
    private static double s_siguiente;
    private static string Carpeta => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes");
    private static readonly System.Text.StringBuilder s_avisos = new();

    /// ¿Se está ejecutando una orden del buzón?
    public static bool EnOrden { get; private set; }

    /// Pregunta con un diálogo; desde el buzón no pregunta y responde que sí.
    public static bool Confirmar(string titulo, string mensaje, string aceptar, string cancelar = "Cancelar")
        => EnOrden || EditorUtility.DisplayDialog(titulo, mensaje, aceptar, cancelar);

    /// Informa con un diálogo; desde el buzón, el texto va al registro de la orden.
    public static void Avisar(string titulo, string mensaje)
    {
        if (EnOrden) s_avisos.Append(titulo).Append(": ").Append(mensaje.Trim().Replace('\n', ' ')).Append(' ');
        else EditorUtility.DisplayDialog(titulo, mensaje, "Aceptar");
    }

    static OrdenesDelEditor()
    {
        EditorApplication.update += Revisar;
    }

    private static void Revisar()
    {
        if (EditorApplication.timeSinceStartup < s_siguiente) return;
        s_siguiente = EditorApplication.timeSinceStartup + 1.0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!Directory.Exists(Carpeta)) return;

        string[] ordenes;
        try { ordenes = Directory.GetFiles(Carpeta, "*.orden"); }
        catch { return; }
        if (ordenes.Length == 0) return;
        Array.Sort(ordenes, StringComparer.Ordinal);
        string fichero = ordenes[0];

        string orden;
        try { orden = File.ReadAllText(fichero).Trim(); }
        catch { return; }
        string hecha = Path.ChangeExtension(fichero, ".hecha");
        try
        {
            if (File.Exists(hecha)) File.Delete(hecha);
            File.Move(fichero, hecha);
        }
        catch { return; }

        string resultado;
        s_avisos.Clear();
        EnOrden = true;
        try { resultado = Ejecutar(orden); }
        catch (Exception ex) { resultado = "ERROR: " + ex.Message + "\n" + ex.StackTrace; }
        finally { EnOrden = false; }
        if (s_avisos.Length > 0) resultado += " · " + s_avisos;
        try
        {
            File.AppendAllText(Path.Combine(Carpeta, "registro.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {Path.GetFileName(fichero)} «{orden}» → {resultado}\n");
        }
        catch { }
    }

    private static string Cerca(string resto)
    {
        var p = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        float x = float.Parse(p[0], ci), z = float.Parse(p[1], ci), r = p.Length > 2 ? float.Parse(p[2], ci) : 2f;
        var sb = new System.Text.StringBuilder($"{x} {z} r={r}\n");
        for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount; k++)
        {
            var esc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(k);
            sb.Append($"  escena {esc.name} cargada={esc.isLoaded}\n");
            if (!esc.isLoaded) continue;
            foreach (var raiz in esc.GetRootGameObjects())
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    var rr = t.GetComponent<Renderer>();
                    Vector3 c = rr != null ? rr.bounds.center : t.position;
                    float dx = c.x - x, dz = c.z - z;
                    if (dx * dx + dz * dz > r * r) continue;
                    if (rr == null && t.GetComponent<Collider>() == null && t.GetComponent<MeshFilter>() == null) continue;
                    string ruta = t.name; for (var q = t.parent; q != null; q = q.parent) ruta = q.name + "/" + ruta;
                    sb.Append($"    {ruta} activo={t.gameObject.activeInHierarchy} capa={LayerMask.LayerToName(t.gameObject.layer)}");
                    if (rr != null) sb.Append($" renderer={rr.GetType().Name} on={rr.enabled} b={rr.bounds.center:F1}/{rr.bounds.size:F1}");
                    var col = t.GetComponent<Collider>();
                    if (col != null) sb.Append($" collider={col.GetType().Name} on={col.enabled}");
                    sb.Append("\n");
                }
        }
        return sb.ToString();
    }

    private static string Rayo(string resto)
    {
        var p = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        float x = float.Parse(p[0], ci), z = float.Parse(p[1], ci);
        var golpes = Physics.RaycastAll(new Vector3(x, 1000f, z), Vector3.down, 2000f, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));
        var sb = new System.Text.StringBuilder($"{x} {z}: {golpes.Length} golpes\n");
        foreach (var g in golpes)
        {
            var t = g.collider.transform;
            string ruta = t.name; for (var q = t.parent; q != null; q = q.parent) ruta = q.name + "/" + ruta;
            sb.Append($"    y={g.point.y:F2} {ruta} capa={LayerMask.LayerToName(t.gameObject.layer)} {g.collider.GetType().Name} trigger={g.collider.isTrigger}\n");
        }
        return sb.ToString();
    }

    private static string Donde(string texto)
    {
        var sb = new System.Text.StringBuilder($"«{texto}»\n");
        int n = 0;
        for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount && n < 200; k++)
        {
            var esc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(k);
            if (!esc.isLoaded) continue;
            foreach (var raiz in esc.GetRootGameObjects())
                foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string ruta = t.name; for (var q = t.parent; q != null; q = q.parent) ruta = q.name + "/" + ruta;
                    sb.Append($"    {esc.name}/{ruta} pos={t.position:F2} rumbo={t.eulerAngles.y:F0} activo={t.gameObject.activeInHierarchy}\n");
                    if (++n >= 200) break;
                }
        }
        return sb.ToString();
    }

    private static string Catalogo(string resto)
    {
        var p = resto.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (p.Length < 3) return "catalogo nombre carpeta regex";
        var regex = new System.Text.RegularExpressions.Regex(p[2]);
        var rutas = new System.Collections.Generic.List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { p[1] }))
        {
            string r = AssetDatabase.GUIDToAssetPath(guid);
            if (regex.IsMatch(Path.GetFileNameWithoutExtension(r))) rutas.Add(r);
        }
        rutas.Sort();
        if (rutas.Count > 12) rutas.RemoveRange(12, rutas.Count - 12);
        if (rutas.Count == 0) return "ningún prefab cumple " + p[2];
        // Lejos de todo, en fila: cada pieza ocupa su ancho y un poco más.
        var origen = new Vector3(9000f, 500f, 9000f);
        var puestos = new System.Collections.Generic.List<GameObject>();
        var sb = new System.Text.StringBuilder();
        float x = 0f, alto = 1f;
        try
        {
            foreach (var r in rutas)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(r));
                go.hideFlags = HideFlags.DontSave;
                var b = new Bounds(go.transform.position, Vector3.zero);
                foreach (var rr in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(rr.bounds);
                go.transform.position = origen + new Vector3(x + b.extents.x, 0f, 0f) - new Vector3(b.center.x - go.transform.position.x, b.min.y - go.transform.position.y, b.center.z - go.transform.position.z);
                x += b.size.x + 0.5f;
                alto = Mathf.Max(alto, b.size.y);
                puestos.Add(go);
                sb.Append(Path.GetFileNameWithoutExtension(r)).Append(' ');
            }
            float ancho = x;
            var centro = origen + new Vector3(ancho * 0.5f, alto * 0.5f, 0f);
            float dist = Mathf.Max(ancho * 0.75f, alto * 1.6f) + 2f;
            string foto = CapturaDeEscenario.FotoDesde(p[0], centro + new Vector3(0f, alto * 0.25f, -dist), centro, 50f);
            return $"foto en {foto ?? "(falló)"} · de izquierda a derecha: {sb}";
        }
        finally { foreach (var go in puestos) if (go != null) UnityEngine.Object.DestroyImmediate(go); }
    }

    private static string Ejecutar(string orden)
    {
        string linea = orden.Split('\n')[0].Trim();
        int espacio = linea.IndexOf(' ');
        string verbo = espacio < 0 ? linea : linea.Substring(0, espacio);
        string resto = espacio < 0 ? "" : linea.Substring(espacio + 1).Trim();
        switch (verbo)
        {
            case "gemini":
            {
                // La pregunta son las líneas que siguen a la primera (si las hay).
                int salto = orden.IndexOf('\n');
                string pregunta = salto < 0 ? null : orden.Substring(salto + 1).Trim();
                return GeminiDelBuzon.Empezar(resto, pregunta);
            }
            case "refrescar":
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                EditorUtility.RequestScriptReload();
                return "refresco pedido";
            case "capturar":
            {
                var pc = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (pc.Length >= 5)
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    float zx = float.Parse(pc[2], ci), zz = float.Parse(pc[3], ci), zm = float.Parse(pc[4], ci);
                    return "captura en " + (CapturaDeEscenario.Capturar(pc[0], Rect.MinMaxRect(zx - zm, zz - zm, zx + zm, zz + zm), pc[1]) ?? "(falló)");
                }
                return "captura en " + (CapturaDeEscenario.Capturar(string.IsNullOrEmpty(resto) ? "Prologo_Valle" : resto) ?? "(falló)");
            }
            case "hornear":
            {
                if (EditorApplication.isPlaying) return "no se puede hornear en Play: sale de Play y vuelve a pedirlo";
                bool storyboard = resto.EndsWith(" storyboard");
                string ruta = storyboard ? resto.Substring(0, resto.Length - " storyboard".Length).Trim() : resto;
                if (string.IsNullOrEmpty(ruta)) ruta = HorneadorDeGuion.RutaPrologo;
                HorneadorDeGuion.Hornear(ruta, storyboard);
                return HorneadorDeGuion.UltimoResumen ?? "horneado";
            }
            case "menu":
                return EditorApplication.ExecuteMenuItem(resto) ? "menú ejecutado" : "no existe ese menú";
            case "play":
                EditorApplication.EnterPlaymode();
                return "entrando en Play";
            case "stop":
                EditorApplication.ExitPlaymode();
                return "saliendo de Play";
            case "cerca":
                return Cerca(resto);
            case "donde":
                return Donde(resto);
            case "rayo":
                return Rayo(resto);
            case "catalogo":
                return Catalogo(resto);
            case "escena":
            {
                if (Application.isPlaying) return "no se puede en Play";
                var partes = resto.Split(' ');
                bool aditiva = partes.Length > 1 && partes[1] == "aditiva";
                // En aditivo no se cierra nada: los cambios sin guardar de las demás no corren peligro.
                for (int k = 0; k < UnityEngine.SceneManagement.SceneManager.sceneCount && !aditiva; k++)
                {
                    var esc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(k);
                    if (esc.isDirty) return $"la escena {esc.name} tiene cambios sin guardar; no la cierro";
                }
                foreach (var guid in AssetDatabase.FindAssets($"{partes[0]} t:Scene"))
                {
                    string ruta = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(ruta) != partes[0]) continue;
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ruta,
                        aditiva ? UnityEditor.SceneManagement.OpenSceneMode.Additive : UnityEditor.SceneManagement.OpenSceneMode.Single);
                    return "abierta " + ruta;
                }
                return "no encuentro la escena " + partes[0];
            }
            case "cerrar":
            {
                if (Application.isPlaying) return "no se puede en Play";
                var partesCerrar = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (partesCerrar.Length == 0) return "falta el nombre de la escena";
                bool descartar = partesCerrar.Length > 1 && partesCerrar[1] == "descartar";
                var esc = UnityEngine.SceneManagement.SceneManager.GetSceneByName(partesCerrar[0]);
                if (!esc.IsValid() || !esc.isLoaded) return "la escena " + partesCerrar[0] + " no está abierta";
                if (esc.isDirty && !descartar) return $"la escena {esc.name} tiene cambios sin guardar; no la cierro (añade «descartar» para cerrarla sin guardar)";
                if (UnityEngine.SceneManagement.SceneManager.sceneCount < 2) return "es la única escena abierta; no la cierro";
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(esc, true);
                return "cerrada " + esc.path;
            }
            case "foto":
            {
                var pf = resto.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (pf.Length < 7) return "foto nombre x y z mx my mz [lente]";
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                float F(int i) => float.Parse(pf[i], ci);
                float lente = pf.Length > 7 ? F(7) : 40f;
                return "foto en " + (CapturaDeEscenario.FotoDesde(pf[0], new Vector3(F(1), F(2), F(3)), new Vector3(F(4), F(5), F(6)), lente) ?? "(falló)");
            }
            case "camara_de_prueba":
            {
                if (!Application.isPlaying) return "solo en Play";
                if (Camera.main != null) return "ya hay cámara principal: " + Camera.main.name;
                var go = new GameObject("Cámara de prueba (orden)") { tag = "MainCamera" };
                go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                return "cámara de prueba creada";
            }
            default:
                return "orden desconocida";
        }
    }
}
#endif
