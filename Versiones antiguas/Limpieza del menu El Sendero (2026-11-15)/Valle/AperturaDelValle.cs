using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Apertura del prólogo (INC-1200): prepara lo que usan DeseoEnElSenderoBeat, MapaDeAperturaBeat y
/// VueloDeAperturaBeat y mete la apertura al principio del guion del prólogo.
/// · Escenario: copia del altar del Sendero (plaza, último tramo de camino, altar, cielo, polvo y luz) en un prefab.
/// · Música: «Amanecer en el Valle» sale del archivo y se da de alta como PROLOGO_AMANECER_VALLE.
/// · Textos: las dos frases del Mago Oscuro.
/// · Guion: la apertura entra antes de la grúa y se quitan las nubes que se abrían (el vuelo las sustituye).
public static class AperturaDelValle
{
    const string Carpeta = "Assets/Art/Cinematic/Apertura";
    const string RutaEscenario = Carpeta + "/SenderoDeLaApertura.prefab";
    const string RutaMapa = Carpeta + "/MapaDeLaApertura.png";
    const string RutaGuion = "Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt";
    const string MusicaArchivada = "Assets/Audio/Music/_Archivo/Amanecer en el Valle.mp3";
    const string MusicaNueva = "Assets/Audio/Music/Cinematicas/Amanecer en el Valle.mp3";
    const string IdMusica = "PROLOGO_AMANECER_VALLE";

    static readonly string[] DelSendero =
    {
        "SENDERO_WORLD/Vestido del Sendero (generado)/Plazas/Plaza — Altar",
        "SENDERO_WORLD/Vestido del Sendero (generado)/Plazas/Plaza — Ruptura",
        "SENDERO_WORLD/Vestido del Sendero (generado)/Camino/Camino 09",
        "SENDERO_WORLD/Vestido del Sendero (generado)/Altar de piedra blanca",
        "SENDERO_WORLD/Vestido del Sendero (generado)/Cielos cosidos (primer tramo)",
        "SENDERO_WORLD/Vestido del Sendero (generado)/Polvo de luz/Polvo 09",
        "Luz Primordial Astral",
    };

    [MenuItem("El Sendero/Escenario/Valle del prólogo: preparar la apertura (escenario, música, textos y guion)", priority = 65)]
    public static void Preparar()
    {
        var informe = new StringBuilder();
        if (!AssetDatabase.IsValidFolder("Assets/Art/Cinematic")) AssetDatabase.CreateFolder("Assets/Art", "Cinematic");
        if (!AssetDatabase.IsValidFolder(Carpeta)) AssetDatabase.CreateFolder("Assets/Art/Cinematic", "Apertura");
        HacerEscenario(informe);
        PrepararMapa(informe);
        Musica(informe);
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Cinematicas, "PROLOGO_DESEO", "Que ningún poder pueda destruirme.", "Let no power ever destroy me.");
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Cinematicas, "PROLOGO_DESEO_PODER", "…¿Y ese poder? ¿De dónde sale?", "…And that power? Where does it come from?");
        int textos = TextosDeLaHistoria.Escribir();
        informe.Append($"Textos: {textos} escritos. ");
        Guion(informe);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Apertura] " + informe);
        OrdenesDelEditor.Avisar("Apertura del prólogo", informe.ToString());
    }

    static void HacerEscenario(StringBuilder informe)
    {
        Scene s = SceneManager.GetSceneByPath("Assets/Scenes/Worlds/Sendero.unity");
        bool abierta = false;
        if (!s.IsValid() || !s.isLoaded) { s = EditorSceneManager.OpenScene("Assets/Scenes/Worlds/Sendero.unity", OpenSceneMode.Additive); abierta = true; }
        var raiz = new GameObject("SenderoDeLaApertura");
        int n = 0;
        try
        {
            foreach (string ruta in DelSendero)
            {
                Transform t = Buscar(s, ruta);
                if (t == null) { informe.Append($"no está «{ruta}»; "); continue; }
                var copia = Object.Instantiate(t.gameObject, t.position, t.rotation, raiz.transform);
                copia.name = t.name;
                copia.transform.localScale = t.lossyScale;
                foreach (var c in copia.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                n++;
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaEscenario);
        }
        finally
        {
            Object.DestroyImmediate(raiz);
            if (abierta) EditorSceneManager.CloseScene(s, true);
        }
        informe.Append($"Escenario del Sendero: {n} piezas en {RutaEscenario}. ");
    }

    static void PrepararMapa(StringBuilder informe)
    {
        var imp = AssetImporter.GetAtPath(RutaMapa) as TextureImporter;
        if (imp == null) { informe.Append($"falta el mapa {RutaMapa}; "); return; }
        if (imp.textureType != TextureImporterType.Default || imp.mipmapEnabled || imp.textureCompression != TextureImporterCompression.Uncompressed)
        {
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        informe.Append("Mapa listo. ");
    }

    static void Musica(StringBuilder informe)
    {
        if (AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaNueva) == null && AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaArchivada) != null)
        {
            string e = AssetDatabase.MoveAsset(MusicaArchivada, MusicaNueva);
            if (!string.IsNullOrEmpty(e)) informe.Append("Música: " + e + "; ");
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaNueva);
        if (clip == null) { informe.Append("Música: no está «Amanecer en el Valle»; "); return; }
        var perfil = AssetDatabase.LoadAssetAtPath<Object>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        var so = new SerializedObject(perfil);
        var lista = so.FindProperty("sequences");
        SerializedProperty el = null;
        for (int i = 0; i < lista.arraySize; i++)
            if (lista.GetArrayElementAtIndex(i).FindPropertyRelative("sequenceId").stringValue == IdMusica) el = lista.GetArrayElementAtIndex(i);
        if (el == null)
        {
            lista.InsertArrayElementAtIndex(lista.arraySize);
            el = lista.GetArrayElementAtIndex(lista.arraySize - 1);
        }
        el.FindPropertyRelative("sequenceId").stringValue = IdMusica;
        el.FindPropertyRelative("music").objectReferenceValue = clip;
        el.FindPropertyRelative("fadeIn").floatValue = 1.5f;
        el.FindPropertyRelative("fadeOut").floatValue = 3f;
        var sinBucle = el.FindPropertyRelative("sinBucle");
        if (sinBucle != null) sinBucle.boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(perfil);
        informe.Append($"Música: {IdMusica} = {MusicaNueva}. ");
    }

    const string MarcaDelGuion = "# ── 0. LA APERTURA (INC-1200)";

    static void Guion(StringBuilder informe)
    {
        string ruta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", RutaGuion);
        string texto = File.ReadAllText(ruta);
        if (texto.Contains(MarcaDelGuion)) { informe.Append("Guion: la apertura ya estaba. "); return; }
        string sendero =
            MarcaDelGuion + " ─────────────────────────────────────────────────────────────\n" +
            "# El Sendero, siglos antes: el Mago Oscuro pide su deseo, nota un poder lejano y la estrella llena la\n" +
            "# pantalla; el mapa del mundo; el vuelo hasta el valle con el logo. Cada beat lleva su propia cámara.\n" +
            "silencio 0.5\n" +
            "efecto DeseoEnElSenderoBeat escenario=" + RutaEscenario + " mago=Assets/Prefabs/_MAGO_OSCURO.prefab segundos=16.5 dura=16.5\n" +
            "musica " + IdMusica + "\n" +
            "# (el «mapa» es el propio mundo desde el aire) efecto MapaDeAperturaBeat mapa=" + RutaMapa + "\n";
        string vuelo =
            "# El vuelo acaba donde empieza la grúa (cam_cielo), así que el corte no se nota.\n" +
            "efecto VueloDeAperturaBeat logo=Assets/Art/Cinematic/Apertura/liyodev-logo-transparente.png segundos=35 dura=34.6\n";
        // La parte del Sendero y el mapa, antes del ambiente del pueblo; el vuelo, justo antes de la grúa.
        int iAmbiente = texto.IndexOf("ambiente AMBIENTE eventKey=Ambience_Prologo_Pueblo");
        int iEntra = texto.IndexOf("\n@entra");
        if (iAmbiente < 0 || iEntra < 0) { informe.Append("Guion: no encuentro dónde meter la apertura; no se toca. "); return; }
        texto = texto.Substring(0, iEntra + 1) + vuelo + texto.Substring(iEntra + 1);
        texto = texto.Substring(0, iAmbiente) + sendero + texto.Substring(iAmbiente);
        // Las nubes que se abrían: las sustituye el vuelo.
        var lineas = texto.Split('\n');
        for (int i = 0; i < lineas.Length; i++)
            if (lineas[i].StartsWith("efecto NubesDeAperturaBeat")) lineas[i] = "# (INC-1200: las nubes de apertura las sustituye el vuelo) " + lineas[i];
        File.WriteAllText(ruta, string.Join("\n", lineas), new UTF8Encoding(false));
        informe.Append("Guion: apertura metida. ");
    }

    static Transform Buscar(Scene escena, string ruta)
    {
        string[] partes = ruta.Split('/');
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            if (r.name != partes[0]) continue;
            Transform t = r.transform;
            for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
            if (t != null) return t;
        }
        return null;
    }
}
