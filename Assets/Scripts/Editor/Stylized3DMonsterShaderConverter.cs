using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Conversión de los materiales del pack "Stylized3DMonster" (Fantasy Monster Evolution Pack
/// 37-38-39, PixeliusVita) al shader unificado del proyecto, Quibli/Stylized Lit — mismo criterio
/// que FantasyKingdomShaderConverter.cs con el resto de packs comprados.
///
/// Historial del diagnóstico (10 sept 2026, Monster39 se veía totalmente blanco):
///
/// 1) El shader que trae el pack (PVFX/PVFX_URP_Low_CelShader1.0) está escrito en CGPROGRAM al
///    estilo Built-in Render Pipeline (usa _WorldSpaceLightPos0, que el Built-in rellena pero URP
///    no — el proyecto corre en URP), así que su cel-shading no funciona de verdad aquí aunque se
///    etiquete compatible con URP. Primer paso: reasignar a Quibli/Stylized Lit.
///
/// 2) Con el shader ya en Quibli y la textura bien enlazada, seguía blanco. Causa encontrada:
///    `_TextureImpact` estaba en 0 con el modo "Multiply" activo — mismo bug ya documentado en
///    `revision-cohesion-visual-assets-2026-08-27.md` con el Golem (color *= lerp(blanco, albedo,
///    _TextureImpact); bajarlo no suaviza la textura, la apaga hacia blanco puro).
///
/// 3) Con `_TextureImpact` ya en 1, SEGUÍA blanco. Causa real, encontrada al revisar los tres
///    materiales del pack: estos `.mat` no son materiales nuevos — se crearon (antes de que
///    tocásemos nada) copiando/heredando una tanda enorme de valores de otro material Quibli ya
///    afinado a mano para otro personaje del juego (de ahí propiedades como `_MetallicColor`
///    azulado, `_RimColor` naranja, `_DirectionalLightIntensity: 3`, y dos campos con valores que
///    ni siquiera son 0/1 como cabría esperar de un toggle: `_UseDirectionalLight: 12` y
///    `_DirectionalLightToggle: 48.2`). Esa combinación de valores ajena, pensada para otro
///    material, es la que sobreexpone/lava el resultado a blanco — no basta con tocar un campo
///    suelto. La única forma fiable de arreglarlo es partir de un material Quibli LIMPIO (con los
///    valores por defecto del shader) y aplicar encima solo lo que de verdad queremos (textura,
///    tinte, emisión), en vez de heredar campo a campo de lo que hubiera antes. Por eso esta
///    herramienta ahora resetea el material a los valores por defecto de Quibli
///    (`EditorUtility.CopySerialized` desde un material Quibli nuevo y limpio) antes de aplicar
///    nada — así no queda ni rastro de configuraciones ajenas heredadas por accidente.
///
/// 4) Tras el reset con CopySerialized, Unity avisaba en consola "Main Object Name '...' does not
///    match filename '...'" para cada material, y además la textura dejaba de encontrarse para
///    TODOS ellos (incluidos los tres monstruos reales). Causa: `CopySerialized` copia también el
///    campo interno `name` del objeto Unity, así que cada material se quedaba literalmente
///    renombrado a "Quibli/Stylized Lit" (el nombre por defecto del material temporal usado como
///    "plantilla limpia"). Como la búsqueda de textura usaba ese `mat.name` corrompido para
///    encontrar el PNG homónimo, fallaba siempre. Además, el rastreo por carpeta
///    (`Assets/Art/Characters/Stylized3DMonster` completo) alcanzaba también dos materiales de demostración
///    del propio paquete del shader (`ShaderMaster/MonsterCelMaterial_Low.mat` y
///    `MonsterCelMaterial_Lowest.mat`), que no son de ningún monstruo y no tienen textura homónima.
///    Arreglado así: (a) el nombre base para buscar la textura se calcula SIEMPRE a partir de la
///    ruta del archivo (`Path.GetFileNameWithoutExtension(matPath)`), nunca de `mat.name` — inmune
///    a que CopySerialized lo corrompa; (b) tras el CopySerialized se restaura `mat.name` a ese
///    mismo nombre correcto; (c) el rastreo de materiales se restringe a las subcarpetas
///    Monster37/Monster38/Monster39 (se excluye explícitamente ShaderMaster).
///
/// 5) La ruta base usada por el rastreo de materiales (`MaterialsFolders`) era
///    `Assets/Art/Stylized3DMonster/...`, y la real es `Assets/Art/Characters/Stylized3DMonster/...`
///    (con "Characters" en medio, igual que Fuego Fatuo y el resto de personajes) — Unity avisaba
///    "Folder not found" y no procesaba ningún material. Corregido.
///
/// 6) Con la ruta ya corregida, Monster39 pasó de verse blanco a verse NEGRO. Causa: la textura
///    SEGUÍA sin encontrarse (confirmado leyendo el .mat resultante: `_BaseMap` quedaba con
///    `m_Texture: {fileID: 0}`, es decir, vacío) pese a que el archivo homónimo existe de verdad en
///    la misma carpeta — el filtro `AssetDatabase.FindAssets("t:Texture2D", ...)` no lo estaba
///    encontrando por alguna razón no determinada (posible desajuste del índice de búsqueda de
///    Unity). Y sin `_BaseMap`, el shader Quibli/Stylized Lit no usa blanco como textura por
///    defecto — su propio archivo `.shader` declara
///    `[MainTexture] _BaseMap(..., 2D) = "black" {}`, o sea que el hueco se rellena con NEGRO, no
///    blanco (visto en `Assets/Plugins/Quibli/Shaders/StylizedLit.shader`). De ahí el cambio de
///    blanco a negro: seguía siendo el mismo problema de fondo (sin textura), solo que el relleno
///    por defecto del hueco es distinto. Arreglado probando primero la ruta exacta y determinista
///    del archivo (`<carpeta>/<nombre>.png`) con `AssetDatabase.LoadAssetAtPath`, en vez de fiarse
///    del filtro de búsqueda por tipo — es la misma convención de nombres ya confirmada a mano
///    mirando las carpetas del pack, así que no hace falta "buscar", solo cargar la ruta exacta.
///    De paso, se vuelve a forzar `_TextureImpact = 1` explícitamente tras el reset (por si acaso
///    `CopySerialized` no trae de fábrica el valor por defecto de 1.0 que declara el shader — se
///    observó en disco que quedaba en 0 pese a partir de un material "limpio").
///
/// 7) Con `_TextureImpact` ya forzado a 1 (confirmado en el .mat resultante), Monster39 SEGUÍA sin
///    textura — `_BaseMap` seguía vacío tras el arreglo del punto 6, así que la ruta directa
///    tampoco la estaba encontrando, por una razón aún sin determinar del todo (puede que el
///    Editor tuviera el índice de AssetDatabase desactualizado en ese momento). Para no seguir
///    adivinando a ciegas, esta versión añade: (a) un `AssetDatabase.Refresh()` al principio de
///    `ConvertAll`, por si el índice estaba resagado; (b) diagnóstico explícito en el log cuando no
///    se encuentra la textura — qué ruta exacta se intentó, si la carpeta se reconoce como válida,
///    y qué tipo de asset (si alguno) hay realmente en esa ruta — para que la próxima vez que falle
///    (si falla) tengamos información concreta en vez de tener que teorizar otra vez.
///
/// 8) El diagnóstico del punto 7 dio la respuesta: el archivo SÍ tiene guid (existe de verdad, es el
///    mismo de siempre), pero Unity lo tenía cacheado internamente como "DefaultAsset" en vez de
///    Texture2D — pese a que su `.meta` en disco declara `TextureImporter` correctamente. O sea que
///    no era un problema de RUTA ni de convención de nombres (esas partes ya estaban bien desde el
///    punto 6): es la caché de importación del Editor la que está desincronizada para estos archivos
///    en concreto, probablemente porque la carpeta se movió en algún momento fuera de Unity (ver
///    punto 5) y el Editor se quedó con el tipo de asset viejo para esas rutas.
///
/// 9) El primer intento de arreglar el punto 8 (forzar `AssetDatabase.ImportAsset(ruta,
///    ImportAssetOptions.ForceUpdate)` desde el propio script, justo después de un
///    `AssetDatabase.Refresh()` al principio de `ConvertAll`) empeoró las cosas: Unity empezó a dar
///    errores de verdad — "File could not be read" e "Importer generated inconsistent result" — para
///    los tres PNG a la vez. Se comprobó a mano que los archivos en sí están perfectamente sanos
///    (se pudo leer y ver el contenido real de Monster37_01.png sin ningún problema): no es
///    corrupción de archivo, es que forzar una reimportación por script justo después de un
///    `Refresh()`, dentro de la misma ejecución síncrona, choca con el propio proceso de
///    importación de Unity en segundo plano (el `Refresh()` ya estaba reimportando esos mismos
///    archivos "de oficio" al detectar la discrepancia, y nuestra llamada explícita se solapaba con
///    esa reimportación en curso). Quitados ambos (`AssetDatabase.Refresh()` inicial y el
///    `ImportAsset(ForceUpdate)` automático): esta herramienta ya NO intenta reimportar nada por su
///    cuenta. Si el diagnóstico vuelve a decir "tipo de asset ahí: DefaultAsset", la solución pasa
///    por hacerlo a mano UNA VEZ desde el Editor (clic derecho sobre la carpeta
///    `Assets/Art/Characters/Stylized3DMonster` en el Project window → Reimport, esperar a que
///    termine) y DESPUÉS ejecutar este menú — nunca las dos cosas encadenadas por script.
///
/// Idempotente: se puede ejecutar varias veces sin duplicar nada — cada vez vuelve a dejar el
/// material en limpio + la textura/tinte correctos.
/// </summary>
public static class Stylized3DMonsterShaderConverter
{
    // Solo las carpetas de los personajes reales — el pack también trae, en ShaderMaster/, un par
    // de materiales de demostración del propio shader (MonsterCelMaterial_Low/Lowest) que no
    // pertenecen a ningún monstruo y no tienen textura homónima; no deben tocarse.
    private static readonly string[] MaterialsFolders =
    {
        "Assets/Art/Characters/Stylized3DMonster/Monster37",
        "Assets/Art/Characters/Stylized3DMonster/Monster38",
        "Assets/Art/Characters/Stylized3DMonster/Monster39",
    };

    private const string QuibliShaderGuid = "2a230514c860643f69b6a4d1871d3825";
    private const string PvfxShaderPrefix = "PVFX/PVFX_URP_";

    [MenuItem("El Sendero/Materiales/Convertir Stylized3DMonster (37-38-39) a Quibli StylizedLit")]
    public static void ConvertAll()
    {
        // NO se llama aquí a AssetDatabase.Refresh() a propósito — ver punto 9 de la cabecera:
        // hacerlo justo antes de tocar estos mismos assets provocó una carrera con la propia
        // reimportación en segundo plano de Unity ("File could not be read"). Si algún material sale
        // "sin textura de origen" con "tipo de asset ahí: DefaultAsset", hace falta reimportar la
        // carpeta a mano UNA VEZ desde el Editor antes de volver a ejecutar este menú.

        string shaderPath = AssetDatabase.GUIDToAssetPath(QuibliShaderGuid);
        Shader quibliShader = string.IsNullOrEmpty(shaderPath) ? null : AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (quibliShader == null)
        {
            Debug.LogError("[Stylized3DMonsterShaderConverter] No se encontró el shader Quibli/Stylized Lit (guid " + QuibliShaderGuid + "). Abortando sin tocar nada.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", MaterialsFolders);
        int procesados = 0, omitidos = 0;
        var log = new StringBuilder();
        log.AppendLine("=== Stylized3DMonster -> Quibli/Stylized Lit (reset a valores por defecto) ===");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            bool esPvfx = mat.shader != null && mat.shader.name.StartsWith(PvfxShaderPrefix);
            bool esQuibliYaSucio = mat.shader == quibliShader; // puede venir de una pasada anterior de esta misma herramienta, antes del fix del reset limpio

            if (!esPvfx && !esQuibliYaSucio)
            {
                omitidos++;
                log.AppendLine($"  OMITIDO (no es el cel-shader del pack ni Quibli, revisar a mano si hace falta): {mat.name} — shader actual: {(mat.shader != null ? mat.shader.name : "null")}");
                continue;
            }

            ResetAndApply(mat, path, quibliShader, log);
            procesados++;
        }

        // Los dos materiales de demo del propio paquete del shader (ShaderMaster/) quedaron con el
        // nombre interno corrompido a "Quibli/Stylized Lit" en una pasada muy anterior de esta
        // misma herramienta (antes del arreglo del punto 4) y, como están fuera del rastreo a
        // propósito (no son de ningún monstruo, ver punto 4), nunca se les llegó a restaurar. Solo
        // les tocamos el nombre — nada de su shader ni sus propiedades — para que dejen de salir
        // los avisos de "Main Object Name" en consola.
        FixStrayNameOnly("Assets/Art/Characters/Stylized3DMonster/ShaderMaster/MonsterCelMaterial_Low.mat", log);
        FixStrayNameOnly("Assets/Art/Characters/Stylized3DMonster/ShaderMaster/MonsterCelMaterial_Lowest.mat", log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine($"--- Resumen: {procesados} materiales reseteados a Quibli limpio + textura/tinte aplicados, {omitidos} omitidos ---");
        Debug.Log(log.ToString());
    }

    /// <summary>
    /// Solo corrige el nombre interno del objeto Unity si no coincide con el nombre del archivo —
    /// no toca shader ni propiedades. Pensado para materiales fuera del rastreo normal que quedaron
    /// con el nombre corrompido por una pasada antigua (ver comentario en ConvertAll).
    /// </summary>
    private static void FixStrayNameOnly(string path, StringBuilder log)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) return;

        string nombreReal = System.IO.Path.GetFileNameWithoutExtension(path);
        if (mat.name == nombreReal) return;

        string nombreViejo = mat.name;
        mat.name = nombreReal;
        EditorUtility.SetDirty(mat);
        log.AppendLine($"  Nombre corregido (sin tocar shader/propiedades): '{nombreViejo}' -> '{nombreReal}'");
    }

    private static void ResetAndApply(Material mat, string matPath, Shader quibliShader, StringBuilder log)
    {
        // El nombre "de verdad" del material es el nombre de su archivo, no mat.name — mat.name se
        // puede corromper (ver punto 4 de la cabecera) al hacer CopySerialized desde otro material.
        // Calculándolo aquí, a partir de la ruta, es inmune a eso.
        string nombreReal = System.IO.Path.GetFileNameWithoutExtension(matPath);

        // --- Leer lo que queremos conservar ANTES de resetear nada ---
        // La fuente de verdad para la textura es el propio archivo, por convención del pack: cada
        // material "NombreXX_01.mat" vive en la misma carpeta "ShaderTexture" que su textura
        // "NombreXX_01.png" homónima — más fiable que fiarse de lo que el Material tenga cargado en
        // memoria ahora mismo (con el shader PVFX, en _MainTex; con Quibli ya puesto en una pasada
        // anterior, en _BaseMap).
        Texture mainTex = FindTextureNextToMaterial(matPath, nombreReal, out string diagnostico);
        bool mainTexFromDisk = mainTex != null;
        if (mainTex == null)
        {
            if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
            else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");
        }

        Color tint = Color.white;
        if (mat.HasProperty("_Color")) tint = mat.GetColor("_Color");
        else if (mat.HasProperty("_BaseColor")) tint = mat.GetColor("_BaseColor");

        Color emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
        bool hasEmission = emissionColor.maxColorComponent > 0.001f;

        // --- Resetear el material a los valores POR DEFECTO de Quibli ---
        // mat.shader = quibliShader (por sí solo) NO limpia propiedades heredadas de un shader
        // anterior que compartan nombre — por eso estos materiales seguían "lavados" a blanco pese
        // a tener la textura bien puesta y _TextureImpact en 1 (ver comentario de cabecera, punto 3).
        // CopySerialized desde un material Quibli nuevo (con los defaults de fábrica del shader)
        // sustituye TODA la configuración del material de golpe, sin dejar restos de packs
        // anteriores ni de otros personajes del juego. OJO: esto también copia el campo interno
        // `name` del objeto Unity — por eso se restaura justo debajo (ver punto 4 de la cabecera).
        var quibliDefaults = new Material(quibliShader);
        EditorUtility.CopySerialized(quibliDefaults, mat);
        Object.DestroyImmediate(quibliDefaults);
        mat.name = nombreReal;

        // --- Aplicar encima solo lo que de verdad queremos ---
        if (mainTex != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", mainTex);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);

        // _TextureImpact = 1 a propósito, sin fiarse del default que traiga CopySerialized (ver
        // punto 6 de la cabecera: se observó en disco que quedaba en 0 en vez del 1.0 que declara
        // el propio shader) — con impact 0 y modo Multiply, la textura no pinta nada.
        if (mat.HasProperty("_TextureImpact")) mat.SetFloat("_TextureImpact", 1f);

        if (hasEmission && mat.HasProperty("_EmissionColor"))
        {
            mat.SetColor("_EmissionColor", emissionColor);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(mat);
        string fuenteTex = mainTex == null
            ? $" ⚠️ sin textura de origen — revisar a mano [{diagnostico}]"
            : (mainTexFromDisk ? " (textura tomada de la carpeta, por nombre)" : " (textura tomada del material)");
        log.AppendLine($"  Reseteado a Quibli limpio: {mat.name}{(hasEmission ? " (con emisión)" : "")}{fuenteTex}");
    }

    /// <summary>
    /// Busca, en la misma carpeta que el material, una textura con el mismo nombre base (p.ej.
    /// "Monster39_01.mat" -> "Monster39_01.png") — la convención real de este pack, confirmada a
    /// mano mirando las carpetas del proyecto. El nombre base se pasa ya calculado desde la ruta
    /// del archivo (nunca desde mat.name, que puede estar corrompido tras un CopySerialized — ver
    /// punto 4 de la cabecera). Si no encuentra nada, `diagnostico` explica exactamente qué se
    /// probó y qué se encontró en su lugar (ver punto 7 de la cabecera), para no tener que
    /// teorizar a ciegas si esto vuelve a fallar.
    /// </summary>
    private static Texture FindTextureNextToMaterial(string matPath, string nombreReal, out string diagnostico)
    {
        string folder = System.IO.Path.GetDirectoryName(matPath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(folder))
        {
            diagnostico = $"no se pudo calcular la carpeta a partir de '{matPath}'";
            return null;
        }

        // Ruta directa y determinista primero (ver punto 6 de la cabecera: el filtro de búsqueda
        // por tipo `t:Texture2D` no la estaba encontrando pese a que el archivo existe de verdad
        // ahí, así que cargamos la ruta exacta en vez de "buscarla").
        string directPath = $"{folder}/{nombreReal}.png";
        Texture2D direct = AssetDatabase.LoadAssetAtPath<Texture2D>(directPath);

        // OJO: aquí NO se fuerza una reimportación por script (`AssetDatabase.ImportAsset(...,
        // ForceUpdate)`) — se probó y provocó errores reales de lectura por chocar con la propia
        // reimportación en segundo plano de Unity (ver punto 9 de la cabecera). Si el archivo existe
        // (tiene guid) pero no carga como Texture2D, el diagnóstico de abajo lo señala y la solución
        // es reimportar la carpeta A MANO, una vez, desde el Editor.
        if (direct != null)
        {
            diagnostico = null;
            return direct;
        }

        // Por si el pack trajera alguna vez otra extensión, se deja también la búsqueda por tipo
        // como red de seguridad.
        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (string texGuid in texGuids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(texGuid);
            if (System.IO.Path.GetFileNameWithoutExtension(texPath) == nombreReal)
            {
                diagnostico = null;
                return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            }
        }

        // Nada encontrado por ninguna vía — se arma un diagnóstico concreto en vez de un simple
        // "no encontrada", para saber de una vez qué está pasando de verdad.
        bool carpetaValida = AssetDatabase.IsValidFolder(folder);
        string guidRutaDirecta = AssetDatabase.AssetPathToGUID(directPath);
        Object loQueHayAhi = string.IsNullOrEmpty(guidRutaDirecta) ? null : AssetDatabase.LoadMainAssetAtPath(directPath);
        string tipoEncontrado = loQueHayAhi != null ? loQueHayAhi.GetType().Name : "nada";
        string listaTexturasCarpeta = texGuids.Length == 0
            ? "ninguna"
            : string.Join(", ", System.Array.ConvertAll(texGuids, g => System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(g))));

        string sugerencia = tipoEncontrado == "DefaultAsset"
            ? " — sugerencia: clic derecho sobre la carpeta Stylized3DMonster en el Project window -> Reimport, y volver a ejecutar este menú después"
            : "";
        diagnostico = $"ruta probada: {directPath} — carpeta válida: {carpetaValida} — guid en esa ruta: " +
                      $"{(string.IsNullOrEmpty(guidRutaDirecta) ? "ninguno" : guidRutaDirecta)} — tipo de asset ahí: {tipoEncontrado} — " +
                      $"texturas t:Texture2D vistas en la carpeta: {listaTexturasCarpeta}{sugerencia}";
        return null;
    }
}
