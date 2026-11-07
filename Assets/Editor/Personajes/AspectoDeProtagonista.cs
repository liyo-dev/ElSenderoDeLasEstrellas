using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pone a un protagonista un aspecto nuevo hecho en Blender (Tools/Modelos/&lt;Nombre&gt;/): sus piezas
/// (pelo, cuerpo, capa…) se añaden a los modelos que pueden enseñarlo (el del jugador, su NPC y su
/// retrato) con <see cref="PiezaNuevaDelModelo"/>, se encienden en los suyos y pasan a ser su aspecto de
/// partida (CharacterAppearanceRegistry de Start). Lo de antes se queda en el modelo, sin usar.
/// Los colores van en materiales lisos con el estilo del material de su cuerpo de siempre; la parte del
/// cuerpo de debajo (ranura «lambert1» del FBX) usa ese mismo material. Se puede repetir.
/// Ojo: una partida ya empezada guarda la ropa que llevaba; el aspecto nuevo sale en partidas nuevas.
/// </summary>
public static class AspectoDeProtagonista
{
    public sealed class Pieza
    {
        public string Nombre;              // nombre de la malla en el FBX y de la pieza en el modelo
        public PartCategory Categoria;
        public string Vecina;              // pieza que ya tiene el modelo, a cuyo lado va («A|B»: la primera que exista)
    }

    public sealed class Ficha
    {
        public string Nombre;              // para los mensajes
        public string Carpeta;             // donde están los FBX (<pieza>.fbx) y donde van sus materiales
        public string CampoDePartida;      // estelaDefault, liamDefault… (vacío: el de Will sale del propio modelo del jugador)
        public string PrefabPropio;        // su NPC (de él sale el material de estilo)
        public string CuerpoDeAntes;       // pieza de cuerpo de la que sale el material de estilo
        public string[] Modelos;           // todos los que pueden enseñarlo
        public string[] Propios;           // los que lo enseñan siempre a él: se les cambia lo que llevan puesto
        public Pieza[] Piezas;
        public string[] Apagar = new string[0];   // piezas de antes que deja de llevar (además de las de las mismas categorías)
        public (string material, Color color)[] Colores;
    }

    const string EscenaDelRegistro = "Assets/Scenes/Systems/Start.unity";

    public static void Aplicar(Ficha f)
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog(f.Nombre, "Hazlo fuera de Play.", "Vale"); return; }
        var informe = new StringBuilder();
        Aplicar(f, informe);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AspectoDeProtagonista] {f.Nombre}: " + informe.ToString().Replace('\n', ' '));
        EditorUtility.DisplayDialog($"{f.Nombre}: aspecto nuevo", informe.ToString(), "Vale");
    }

    public static void Aplicar(Ficha f, StringBuilder informe)
    {
        if (!PonerPiezas(f, informe)) return;
        if (!string.IsNullOrEmpty(f.CampoDePartida)) CambiarAspectoDePartida(f, informe);
        else
        {
            // Will: su aspecto de partida es el de los presets del jugador (PlayerPresetSO.appearance)
            var poner = f.Piezas.ToDictionary(p => p.Categoria, p => p.Nombre);
            var quitar = f.Apagar.Select(n => (PartCategory?)CategoriaDe(n)).Where(c => c.HasValue && !poner.ContainsKey(c.Value)).Select(c => c.Value);
            ActualizarPresetsDelJugador(poner, quitar, informe);
        }
    }

    /// <summary>
    /// Prepara los FBX de la ficha y pone sus piezas en sus modelos (encendidas en los propios). No toca el
    /// aspecto de partida del registro ni los presets del jugador: vale también para NPCs.
    /// </summary>
    public static bool PonerPiezas(Ficha f, StringBuilder informe)
    {
        var propio = AssetDatabase.LoadAssetAtPath<GameObject>(f.PrefabPropio);
        var materialDelCuerpo = propio?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == f.CuerpoDeAntes)?.sharedMaterial;
        if (materialDelCuerpo == null) { informe.AppendLine($"{f.Nombre}: no encuentro {f.CuerpoDeAntes} en {f.PrefabPropio}; no se hace nada."); return false; }
        string estilo = AssetDatabase.GetAssetPath(materialDelCuerpo);

        var modelos = new Dictionary<string, GameObject>();
        foreach (var p in f.Piezas)
        {
            string fbx = $"{f.Carpeta}/{p.Nombre}.fbx";
            PersonajeHechoEnBlender.PrepararPrenda(fbx, f.Carpeta + "/Materiales", estilo, f.Colores, informe);
            if (AssetImporter.GetAtPath(fbx) is ModelImporter imp)
            {
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "lambert1"), materialDelCuerpo);
                imp.SaveAndReimport();
            }
            modelos[p.Nombre] = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        }

        var nombres = f.Piezas.Select(p => p.Nombre).ToArray();
        foreach (var ruta in f.Modelos)
        {
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                bool ok = true;
                foreach (var p in f.Piezas) ok &= PiezaNuevaDelModelo.Anadir(raiz, modelos[p.Nombre], p.Nombre, p.Vecina, informe);
                if (ok && f.Propios.Contains(ruta))
                {
                    // Apaga lo que llevaba en esas categorías (y lo que pida la ficha) y enciende lo nuevo
                    var apagar = new List<string>(f.Apagar);
                    foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
                        if (r.gameObject.activeSelf && !nombres.Contains(r.name) && f.Piezas.Any(p => r.name.StartsWith(Prefijo(p.Categoria))))
                            apagar.Add(r.name);
                    PiezaNuevaDelModelo.Encender(raiz, nombres, apagar);
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        return true;
    }

    static PartCategory? CategoriaDe(string pieza)
    {
        foreach (PartCategory c in System.Enum.GetValues(typeof(PartCategory)))
            if (c != PartCategory.Eyebrow && pieza.StartsWith(Prefijo(c))) return c;
        return null;
    }

    /// <summary>
    /// El aspecto con el que empieza el personaje del jugador en cada preset (partida nueva, pruebas rápidas):
    /// pone esas piezas por categoría y quita esas categorías.
    /// </summary>
    public static void ActualizarPresetsDelJugador(Dictionary<PartCategory, string> poner, IEnumerable<PartCategory> quitar, StringBuilder informe)
    {
        var fuera = new HashSet<PartCategory>(quitar ?? Enumerable.Empty<PartCategory>());
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:PlayerPresetSO"))
        {
            var preset = AssetDatabase.LoadAssetAtPath<PlayerPresetSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (preset == null || preset.appearance == null) continue;
            preset.appearance.RemoveAll(e => poner.ContainsKey(e.category) || fuera.Contains(e.category));
            foreach (var kv in poner) preset.appearance.Add(new AppearanceEntry { category = kv.Key, partName = kv.Value });
            EditorUtility.SetDirty(preset);
            n++;
        }
        informe.AppendLine($"Presets del jugador: {n} puestos al día ({string.Join(", ", poner.Values)}).");
    }

    /// <summary>Cómo empiezan los nombres de las piezas de esa categoría en el modelo modular.</summary>
    public static string Prefijo(PartCategory c) => c switch
    {
        PartCategory.Accessory => "AC",
        PartCategory.Eyes => "Eye",
        _ => c.ToString(),
    };

    /// <summary>Su aspecto de partida en el CharacterAppearanceRegistry de Start: las categorías de las piezas nuevas pasan a ellas.</summary>
    public static void CambiarAspectoDePartida(Ficha f, StringBuilder informe)
    {
        var abierta = EditorSceneManager.GetSceneByPath(EscenaDelRegistro);
        bool cerrar = !abierta.isLoaded;
        if (cerrar && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { informe.AppendLine("Aspecto de partida: cancelado (escenas sin guardar)."); return; }
        var escena = cerrar ? EditorSceneManager.OpenScene(EscenaDelRegistro, OpenSceneMode.Additive) : abierta;
        try
        {
            var registro = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterAppearanceRegistry>(true)).FirstOrDefault();
            if (registro == null) { informe.AppendLine($"Aspecto de partida: no encuentro CharacterAppearanceRegistry en {EscenaDelRegistro}."); return; }
            var so = new SerializedObject(registro);
            var lista = so.FindProperty(f.CampoDePartida);
            if (lista == null) { informe.AppendLine($"Aspecto de partida: el registro no tiene «{f.CampoDePartida}»."); return; }
            var entradas = new List<(int cat, string pieza)>();
            for (int i = 0; i < lista.arraySize; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                entradas.Add((e.FindPropertyRelative("category").intValue, e.FindPropertyRelative("partName").stringValue));
            }
            entradas.RemoveAll(e => f.Piezas.Any(p => (int)p.Categoria == e.cat) || f.Apagar.Contains(e.pieza));
            foreach (var p in f.Piezas) entradas.Add(((int)p.Categoria, p.Nombre));
            lista.arraySize = entradas.Count;
            for (int i = 0; i < entradas.Count; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("category").intValue = entradas[i].cat;
                e.FindPropertyRelative("partName").stringValue = entradas[i].pieza;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            informe.AppendLine($"Aspecto de partida de {f.Nombre}: {string.Join(", ", entradas.Select(e => e.pieza))}.");
        }
        finally { if (cerrar) EditorSceneManager.CloseScene(escena, true); }
    }

    // ── Los protagonistas ────────────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Personajes/Estela: aspecto de hechicera")]
    public static void Estela() => Aplicar(FichaEstela);
    public static Ficha FichaEstela => new Ficha
    {
        Nombre = "Estela",
        Carpeta = "Assets/Art/Characters/Protagonistas/Estela",
        CampoDePartida = "estelaDefault",
        PrefabPropio = "Assets/Prefabs/_ESTELA.prefab",
        CuerpoDeAntes = "Body09",
        Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_ESTELA.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" },
        Propios = new[] { "Assets/Prefabs/_ESTELA.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" },
        Piezas = new[]
        {
            new Pieza { Nombre = "Hair14_Coleta", Categoria = PartCategory.Hair, Vecina = "Hair08" },
            new Pieza { Nombre = "Body21_Hechicera", Categoria = PartCategory.Body, Vecina = "Body09" },
            new Pieza { Nombre = "Cloak04_Hechicera", Categoria = PartCategory.Cloak, Vecina = "Cloak01|Body09" },
        },
        Apagar = new[] { "AC09_Ribbon" },   // la coleta ya lleva su lazo
        Colores = new[]
        {
            ("Medias", new Color(0.1f, 0.12f, 0.28f)),   // el trozo de pierna entre la túnica y las botas
            ("Pelo", new Color(0.9f, 0.2f, 0.05f)), ("PeloClaro", new Color(1f, 0.34f, 0.1f)), ("LazoAzul", new Color(0.2f, 0.45f, 0.95f)),   // pelo zanahoria (Raúl, 1 nov)
            ("Tunica", new Color(0.18f, 0.32f, 0.72f)), ("Capa", new Color(0.93f, 0.86f, 0.7f)), ("Hombrera", new Color(0.32f, 0.34f, 0.42f)),
            ("Gema", new Color(0.9f, 0.08f, 0.15f)), ("Cuero", new Color(0.48f, 0.28f, 0.14f)), ("Dorado", new Color(1f, 0.8f, 0.3f)),
        },
    };

    [MenuItem("El Sendero/Personajes/Will: aspecto nuevo")]
    public static void Will() => Aplicar(FichaWill);
    public static Ficha FichaWill => new Ficha
    {
        Nombre = "Will",
        Carpeta = "Assets/Art/Characters/Protagonistas/Will",
        CampoDePartida = null,   // Will empieza con lo que lleva puesto el modelo del jugador
        PrefabPropio = "Assets/Prefabs/_WILL.prefab",
        CuerpoDeAntes = "Body05",
        Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab" },
        Propios = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab" },
        Piezas = new[]
        {
            new Pieza { Nombre = "Hair16_Heroe", Categoria = PartCategory.Hair, Vecina = "Hair01" },
            new Pieza { Nombre = "Body23_Aventurero", Categoria = PartCategory.Body, Vecina = "Body05" },
        },
        Apagar = new[] { "Cloak01", "Cloak02", "Cloak03" },   // la capa roja de antes no va con la chaqueta
        Colores = new[]
        {
            ("Pelo", new Color(0.72f, 0.44f, 0.16f)), ("PeloClaro", new Color(0.86f, 0.6f, 0.28f)),   // rubio miel
            ("Chaqueta", new Color(0.1f, 0.16f, 0.38f)), ("Camisa", new Color(0.95f, 0.9f, 0.78f)), ("Pantalon", new Color(0.3f, 0.24f, 0.18f)),
            ("Cuero", new Color(0.42f, 0.25f, 0.12f)), ("Dorado", new Color(1f, 0.8f, 0.3f)),
        },
    };

    [MenuItem("El Sendero/Personajes/Liam: aspecto nuevo")]
    public static void Liam() => Aplicar(FichaLiam);
    public static Ficha FichaLiam => new Ficha
    {
        Nombre = "Liam",
        Carpeta = "Assets/Art/Characters/Protagonistas/Liam",
        CampoDePartida = "liamDefault",
        PrefabPropio = "Assets/Prefabs/_LIAM.prefab",
        CuerpoDeAntes = "Body12",
        Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_LIAM.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab" },
        Propios = new[] { "Assets/Prefabs/_LIAM.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab" },
        Piezas = new[]
        {
            new Pieza { Nombre = "Hair15_Flequillo", Categoria = PartCategory.Hair, Vecina = "Hair02" },
            new Pieza { Nombre = "Body22_Tunica", Categoria = PartCategory.Body, Vecina = "Body12" },
            // La capucha es un accesorio: el jugador se la puede quitar en el armario (sale el pelo entero)
            new Pieza { Nombre = "AC12_Capucha", Categoria = PartCategory.Accessory, Vecina = "AC03_BlackGlass|AC01_Heart|Body12" },
        },
        Colores = new[]
        {
            ("Pelo", new Color(0.24f, 0.12f, 0.06f)), ("PeloClaro", new Color(0.36f, 0.2f, 0.1f)),
            ("Tunica", new Color(0.52f, 0.33f, 0.17f)), ("TunicaOscura", new Color(0.27f, 0.17f, 0.09f)), ("Cuero", new Color(0.2f, 0.12f, 0.07f)),
            ("Camisa", new Color(0.93f, 0.88f, 0.76f)), ("Pantalon", new Color(0.27f, 0.27f, 0.3f)), ("Dorado", new Color(1f, 0.8f, 0.3f)),
        },
    };
}
