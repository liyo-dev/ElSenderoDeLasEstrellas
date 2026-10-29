using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// Herramienta temporal de validación: en Play localiza el componente de cejas de Will, cambia de
/// emoción y comprueba que los vértices de las cejas se mueven y vuelven a su sitio. Deja fotos de
/// la cara por emoción en «Logs/Cejas/». Se retira al cerrar la incidencia.
public static class ProbarCejasEnPlay
{
    private const string Menu = "El Sendero/Archivo/Cejas/Probar en Play";
    private const string MenuLectura = "El Sendero/Archivo/Cejas/Habilitar lectura de Hair01";
    private const string RutaHair = "Assets/Art/Characters/RPG Tiny Hero Duo/Mesh/HeadParts/Hair01.fbx";
    private const int Lado = 512;

    private static readonly NPCEmotion[] Guion =
    {
        NPCEmotion.Angry, NPCEmotion.Surprised, NPCEmotion.Happy, NPCEmotion.Scared, NPCEmotion.Neutral
    };

    private static double _inicio;
    private static int _paso;
    private static CejasPorEmocion _cejas;
    private static NPCEmotionController _controlador;
    private static NPCEmotionController _controladorEnganchado;
    private static MeshFilter _filtro;
    private static GameObject _instanda;
    private static Vector3[] _base;
    private static int _eventosEncontrado;
    private static int _eventosEnganchado;
    private static int _frameArranque;
    private static int _frameAplicado;
    private static readonly List<string> Informe = new List<string>();

    [MenuItem(Menu, true)]
    private static bool Disponible() => Application.isPlaying;

    [MenuItem(MenuLectura)]
    private static void HabilitarLectura()
    {
        var importador = AssetImporter.GetAtPath(RutaHair) as ModelImporter;
        if (importador == null)
        {
            Debug.LogError("[CejasPlay] No encuentro el importador de " + RutaHair);
            return;
        }
        importador.isReadable = true;
        importador.SaveAndReimport();
        Debug.Log($"[CejasPlay] Lectura habilitada en {RutaHair}: isReadable={importador.isReadable}.");
    }

    [MenuItem(Menu)]
    private static void Arrancar()
    {
        EditorApplication.update -= Paso;
        EditorApplication.update += Paso;
        _inicio = EditorApplication.timeSinceStartup;
        _paso = 0;
        _frameArranque = Time.frameCount;
        Informe.Clear();
        Informe.Add("=== Cejas en Play ===");
        Debug.Log("[CejasPlay] Prueba arrancada.");
    }

    private static void CuentaEncontrado(NPCEmotion emocion) => _eventosEncontrado++;
    private static void CuentaEnganchado(NPCEmotion emocion) => _eventosEnganchado++;

    private static void Paso()
    {
        if (!Application.isPlaying)
        {
            Terminar("Play terminó antes de acabar la prueba.");
            return;
        }

        double t = EditorApplication.timeSinceStartup - _inicio;
        try
        {
            switch (_paso)
            {
                case 0 when t > 0.5 && Time.frameCount > _frameArranque + 5:
                    Localizar();
                    _paso = 1;
                    Aplicar(Guion[0]);
                    _inicio = EditorApplication.timeSinceStartup;
                    break;
                case 1 when t > 0.7 && Time.frameCount >= _frameAplicado + 5:
                    Medir(Guion[0], 0);
                    Aplicar(Guion[1]);
                    _paso = 2;
                    _inicio = EditorApplication.timeSinceStartup;
                    break;
                case 2 when t > 0.7 && Time.frameCount >= _frameAplicado + 5:
                    Medir(Guion[1], 1);
                    Aplicar(Guion[2]);
                    _paso = 3;
                    _inicio = EditorApplication.timeSinceStartup;
                    break;
                case 3 when t > 0.7 && Time.frameCount >= _frameAplicado + 5:
                    Medir(Guion[2], 2);
                    Aplicar(Guion[3]);
                    _paso = 4;
                    _inicio = EditorApplication.timeSinceStartup;
                    break;
                case 4 when t > 0.7 && Time.frameCount >= _frameAplicado + 5:
                    Medir(Guion[3], 3);
                    Aplicar(Guion[4]);
                    _paso = 5;
                    _inicio = EditorApplication.timeSinceStartup;
                    break;
                case 5 when t > 0.7 && Time.frameCount >= _frameAplicado + 5:
                    Medir(Guion[4], 4);
                    _paso = 6;
                    break;
                case 6 when t > 0.3:
                    Forzar();
                    _paso = 7;
                    _inicio = EditorApplication.timeSinceStartup;
                    break;
                case 7 when t > 0.3:
                    Terminar(null);
                    break;
            }
        }
        catch (System.Exception ex)
        {
            Terminar("Excepción: " + ex);
        }
    }

    private static void Localizar()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_WILL.prefab");
        if (asset != null)
        {
            _instanda = Object.Instantiate(asset, new Vector3(0f, 500f, 0f), Quaternion.identity);
            _instanda.name = "Will (prueba cejas)";
            Informe.Add("Se instancia _WILL para la prueba (los Will de escena son decorativos).");
            _cejas = _instanda.GetComponentInChildren<CejasPorEmocion>(true);
        }

        if (_cejas == null)
        {
            Informe.Add("No encuentro ningún CejasPorEmocion en las escenas cargadas.");
            return;
        }

        _controlador = _cejas.GetComponentInParent<NPCEmotionController>(true);
        _filtro = _cejas.GetComponent<MeshFilter>();
        var so = new SerializedObject(_cejas);
        _controladorEnganchado = so.FindProperty("controlador").objectReferenceValue as NPCEmotionController;
        _eventosEncontrado = 0;
        _eventosEnganchado = 0;
        if (_controlador != null) _controlador.OnEmotionChanged += CuentaEncontrado;
        if (_controladorEnganchado != null && !ReferenceEquals(_controladorEnganchado, _controlador))
            _controladorEnganchado.OnEmotionChanged += CuentaEnganchado;

        Informe.Add($"objeto: {RutaDe(_cejas.transform)}");
        Informe.Add($"controlador del padre: «{Nombre(_controlador)}» id={Id(_controlador)}; " +
                    $"enganchado: «{Nombre(_controladorEnganchado)}» id={Id(_controladorEnganchado)}; " +
                    $"mismo={ReferenceEquals(_controlador, _controladorEnganchado)}; " +
                    $"controladores en la raíz={_cejas.transform.root.GetComponentsInChildren<NPCEmotionController>(true).Length}");
        Informe.Add($"estado: preparado={Campo("_preparado")} enMovimiento={Campo("_enMovimiento")} " +
                    $"cejas={Largo("_indicesIzquierda")}+{Largo("_indicesDerecha")}");
        Informe.Add("reloj: " + Reloj());

        if (_filtro == null || _filtro.sharedMesh == null)
        {
            Informe.Add("El objeto no tiene malla de pelo.");
            return;
        }

        Mesh malla = _filtro.sharedMesh;
        Informe.Add($"malla en uso: «{malla.name}» isReadable={malla.isReadable} vértices={malla.vertexCount}");
        if (!malla.isReadable)
        {
            Informe.Add("La malla no es legible: no se puede medir (habilita Read/Write en Hair01.fbx).");
            return;
        }
        _base = malla.vertices;
    }

    private static void Aplicar(NPCEmotion emocion)
    {
        _frameAplicado = Time.frameCount;
        if (_controlador != null) _controlador.SetEmotion(emocion);
        if (_controladorEnganchado != null && !ReferenceEquals(_controladorEnganchado, _controlador))
            _controladorEnganchado.SetEmotion(emocion);
    }

    private static void Medir(NPCEmotion emocion, int indice)
    {
        if (_cejas == null || _controlador == null || _filtro == null || _filtro.sharedMesh == null || _base == null)
        {
            Informe.Add($"{emocion}: sin componente o sin malla; no se puede medir.");
            return;
        }

        if (_filtro.sharedMesh.vertexCount != _base.Length)
        {
            Informe.Add($"{emocion}: la malla cambió de tamaño; no se puede medir.");
            return;
        }

        var actuales = _filtro.sharedMesh.vertices;
        float mayor = 0f;
        int movidos = 0;
        for (int i = 0; i < actuales.Length && i < _base.Length; i++)
        {
            float d = Vector3.Distance(actuales[i], _base[i]);
            if (d > mayor) mayor = d;
            if (d > 0.0005f) movidos++;
        }

        string foto;
        try { foto = _cejas.isActiveAndEnabled ? Fotografiar(emocion, indice) : "(componente apagado)"; }
        catch (System.Exception ex) { foto = "falló: " + ex.Message; }

        Informe.Add($"{emocion}: vértices movidos={movidos}, desplazamiento máximo={mayor:F4} m, " +
                    $"emoción={_controlador.CurrentEmotion}, eventos={_eventosEncontrado}/{_eventosEnganchado}, " +
                    $"enMovimiento={Campo("_enMovimiento")}");
        Informe.Add($"    pose actual={Describir(Campo("_poseActual"))}");
        Informe.Add($"    pose objetivo={Describir(Campo("_poseObjetivo"))}");
        Informe.Add("    reloj: " + Reloj());
        Informe.Add($"    foto={foto}");
        Debug.Log($"[CejasPlay] {emocion}: movidos={movidos} máx={mayor:F4} eventos={_eventosEncontrado}/{_eventosEnganchado}");
    }

    private static string Fotografiar(NPCEmotion emocion, int indice)
    {
        Transform cabeza = null;
        foreach (var t in _cejas.GetComponentsInParent<Transform>(true))
        {
            if (t.name == "head")
            {
                cabeza = t;
                break;
            }
        }

        Renderer ojos = null;
        if (cabeza != null)
        {
            foreach (var r in cabeza.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("Eye"))
                {
                    ojos = r;
                    break;
                }
            }
        }

        if (cabeza == null || ojos == null) return "(sin cabeza u ojos)";

        Vector3 centro = ojos.bounds.center;
        Vector3 frente = ojos.transform.forward;
        if (frente.sqrMagnitude < 0.0001f) frente = cabeza.forward;
        frente.Normalize();

        var camGo = new GameObject("CamaraCejasPlay");
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 22f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
        camGo.transform.position = centro + frente * 0.45f;
        camGo.transform.rotation = Quaternion.LookRotation(centro - camGo.transform.position, ojos.transform.up);

        var rt = new RenderTexture(Lado, Lado, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture anterior = RenderTexture.active;
        RenderTexture.active = rt;
        var tela = new Texture2D(Lado, Lado, TextureFormat.RGB24, false);
        tela.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0);
        tela.Apply();
        RenderTexture.active = anterior;

        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/Cejas");
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta, $"Play_{indice}_{emocion}.png");
        File.WriteAllBytes(ruta, tela.EncodeToPNG());

        cam.targetTexture = null;
        Object.Destroy(rt);
        Object.Destroy(tela);
        Object.Destroy(camGo);
        return ruta;
    }

    private static void Forzar()
    {
        if (_cejas == null || _filtro == null || _filtro.sharedMesh == null || _base == null)
        {
            Informe.Add("forzado: sin componente o sin malla.");
            return;
        }

        try
        {
            var metodoPose = typeof(CejasPorEmocion).GetMethod("PosePara", BindingFlags.NonPublic | BindingFlags.Static);
            var campoActual = typeof(CejasPorEmocion).GetField("_poseActual", BindingFlags.NonPublic | BindingFlags.Instance);
            var campoObjetivo = typeof(CejasPorEmocion).GetField("_poseObjetivo", BindingFlags.NonPublic | BindingFlags.Instance);
            var metodoMalla = typeof(CejasPorEmocion).GetMethod("ActualizarMalla", BindingFlags.NonPublic | BindingFlags.Instance);
            if (metodoPose == null || campoActual == null || campoObjetivo == null || metodoMalla == null)
            {
                Informe.Add("forzado: no encuentro los métodos privados.");
                return;
            }

            ProbarPose(metodoPose, campoActual, campoObjetivo, metodoMalla, NPCEmotion.Angry, 90);
            ProbarPose(metodoPose, campoActual, campoObjetivo, metodoMalla, NPCEmotion.Neutral, 91);
        }
        catch (System.Exception ex)
        {
            Informe.Add("forzado falló: " + ex.Message);
        }
    }

    private static void ProbarPose(MethodInfo metodoPose, FieldInfo campoActual, FieldInfo campoObjetivo,
        MethodInfo metodoMalla, NPCEmotion emocion, int indice)
    {
        object pose = metodoPose.Invoke(null, new object[] { emocion });
        campoObjetivo.SetValue(_cejas, pose);
        campoActual.SetValue(_cejas, pose);
        metodoMalla.Invoke(_cejas, null);

        var actuales = _filtro.sharedMesh.vertices;
        float mayor = 0f;
        int movidos = 0;
        for (int i = 0; i < actuales.Length && i < _base.Length; i++)
        {
            float d = Vector3.Distance(actuales[i], _base[i]);
            if (d > mayor) mayor = d;
            if (d > 0.0005f) movidos++;
        }

        string foto = "(sin foto)";
        try { foto = Fotografiar(emocion, indice); }
        catch (System.Exception ex) { foto = "falló: " + ex.Message; }

        Informe.Add($"forzado {emocion}: vértices movidos={movidos}, desplazamiento máximo={mayor:F4} m, foto={foto}");
        Debug.Log($"[CejasPlay] forzado {emocion}: movidos={movidos} máx={mayor:F4}");
    }

    private static string Reloj()
        => $"frame={Time.frameCount} unscaledTime={Time.unscaledTime:F2} dt={Time.unscaledDeltaTime:F4} " +
           $"timeScale={Time.timeScale:F2} pausado={EditorApplication.isPaused} foco={Application.isFocused}";

    private static void Terminar(string motivo)
    {
        EditorApplication.update -= Paso;
        if (_controlador != null) _controlador.OnEmotionChanged -= CuentaEncontrado;
        if (_controladorEnganchado != null) _controladorEnganchado.OnEmotionChanged -= CuentaEnganchado;
        Informe.Add(motivo ?? "prueba terminada.");
        string carpeta = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs/Cejas");
        Directory.CreateDirectory(carpeta);
        File.WriteAllText(Path.Combine(carpeta, "play.txt"), string.Join("\n", Informe));
        Debug.Log("[CejasPlay] " + string.Join("\n[CejasPlay] ", Informe));
        if (_instanda != null) Object.Destroy(_instanda);
        _instanda = null;
        _cejas = null;
        _controlador = null;
        _controladorEnganchado = null;
        _filtro = null;
        _base = null;
    }

    private static string RutaDe(Transform t)
    {
        string ruta = t.name;
        for (var p = t.parent; p != null; p = p.parent) ruta = p.name + "/" + ruta;
        return ruta;
    }

    private static object Campo(string nombre)
    {
        if (_cejas == null) return null;
        var campo = typeof(CejasPorEmocion).GetField(nombre, BindingFlags.NonPublic | BindingFlags.Instance);
        return campo != null ? campo.GetValue(_cejas) : null;
    }

    private static object Largo(string nombre)
        => Campo(nombre) is System.Array arreglo ? (object)arreglo.Length : "(no)";

    private static object Nombre(NPCEmotionController controlador)
        => controlador != null ? controlador.name : "(ninguno)";

    private static object Id(NPCEmotionController controlador)
        => controlador != null ? (object)controlador.GetEntityId() : "(no)";

    private static string Describir(object o)
    {
        if (o == null) return "(nulo)";
        var tipo = o.GetType();
        var campos = tipo.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var partes = new List<string>();
        foreach (var f in campos)
        {
            object v = f.GetValue(o);
            if (v is float fl)
                partes.Add($"{f.Name}={fl.ToString("F4", CultureInfo.InvariantCulture)}");
            else if (v == null)
                partes.Add($"{f.Name}=nulo");
            else if (v.GetType().IsPrimitive)
                partes.Add($"{f.Name}={v}");
            else
                partes.Add($"{f.Name}={{ {Describir(v)} }}");
        }
        return string.Join(" ", partes);
    }
}
