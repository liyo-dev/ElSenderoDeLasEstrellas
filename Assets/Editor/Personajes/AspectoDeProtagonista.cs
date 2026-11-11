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
        public bool DeSerie = true;        // la lleva puesta de partida; si no, solo queda en su armario (la chaqueta de Will)
        public string NombreEnArmario;     // prenda del armario (WardrobeItemSO, solo para él); vacío: no es ropa (el pelo)
        public string Descripcion;
    }

    public sealed class Ficha
    {
        public string Nombre;              // para los mensajes
        public PartyControlManager.CharacterSlot Personaje;   // de quién es su ropa en el armario
        public string Carpeta;             // donde están los FBX (<pieza>.fbx) y donde van sus materiales
        public string CampoDePartida;      // estelaDefault, liamDefault… (vacío: el de Will sale del propio modelo del jugador)
        public string PrefabPropio;        // su NPC (de él sale el material de estilo)
        public string CuerpoDeAntes;       // pieza de cuerpo de la que sale el material de estilo
        public string[] Modelos;           // todos los que pueden enseñarlo
        public string[] Propios;           // los que lo enseñan siempre a él: se les cambia lo que llevan puesto
        public Pieza[] Piezas;
        public string[] Apagar = new string[0];   // piezas de antes que deja de llevar (además de las de las mismas categorías)
        public (string material, Color color)[] Colores;
        public string[] CadenaConMuelle = new string[0];   // huesos de sus piezas que se mueven con MuelleDeHuesos (la coleta), de la raíz a la punta
    }

    const string EscenaDelRegistro = "Assets/Scenes/Systems/Start.unity";

    public static void Aplicar(Ficha f)
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar(f.Nombre, "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Aplicar(f, informe);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AspectoDeProtagonista] {f.Nombre}: " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar($"{f.Nombre}: aspecto nuevo", informe.ToString());   // desde el buzón va al registro (un diálogo lo bloquearía)
    }

    public static void Aplicar(Ficha f, StringBuilder informe)
    {
        if (!PonerPiezas(f, informe)) return;
        ArmarioDeLosProtagonistas.PrendasDe(f, informe);
        if (!string.IsNullOrEmpty(f.CampoDePartida)) CambiarAspectoDePartida(f, informe);
        else
        {
            // Will: su aspecto de partida es el de los presets del jugador (PlayerPresetSO.appearance)
            var poner = f.Piezas.Where(p => p.DeSerie).ToDictionary(p => p.Categoria, p => p.Nombre);
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

        var nombres = f.Piezas.Where(p => p.DeSerie).Select(p => p.Nombre).ToArray();
        var todas = f.Piezas.Select(p => p.Nombre).ToArray();
        foreach (var ruta in f.Modelos)
        {
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                bool ok = true;
                foreach (var p in f.Piezas) ok &= PiezaNuevaDelModelo.Anadir(raiz, modelos[p.Nombre], p.Nombre, p.Vecina, informe);
                if (ok) PonerMuelle(raiz, f.CadenaConMuelle, informe);
                if (ok && f.Propios.Contains(ruta))
                {
                    // Apaga lo que llevaba en esas categorías (y lo que pida la ficha) y enciende lo nuevo
                    var apagar = new List<string>(f.Apagar);
                    foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
                        if (r.gameObject.activeSelf && !nombres.Contains(r.name) && f.Piezas.Any(p => p.DeSerie && r.name.StartsWith(Prefijo(p.Categoria))))
                            apagar.Add(r.name);
                    apagar.AddRange(todas.Except(nombres));   // lo que no es de serie, guardado en el armario
                    PiezaNuevaDelModelo.Encender(raiz, nombres, apagar);
                }
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        return true;
    }

    /// <summary>Pone (o deja) en la raíz del modelo el <see cref="MuelleDeHuesos"/> con esa cadena, si el modelo tiene sus huesos.</summary>
    static void PonerMuelle(GameObject raiz, string[] cadena, StringBuilder informe)
    {
        if (cadena == null || cadena.Length == 0) return;
        var nombres = new HashSet<string>(raiz.GetComponentsInChildren<Transform>(true).Select(t => t.name));
        if (!cadena.All(nombres.Contains)) { informe.AppendLine($"{raiz.name}: le faltan los huesos de {string.Join(", ", cadena)}; sin muelle."); return; }
        // Un personaje puede llevar varios muelles (la coleta, la capa): el de esta cadena es el que empieza por su primer hueso.
        var muelle = raiz.GetComponents<MuelleDeHuesos>().FirstOrDefault(m => m.huesos != null && m.huesos.Length > 0 && m.huesos[0] == cadena[0]);
        if (muelle == null) muelle = raiz.AddComponent<MuelleDeHuesos>();
        muelle.huesos = cadena.ToArray();
        informe.AppendLine($"{raiz.name}: muelle en {string.Join(", ", cadena)}.");
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
            var deSerie = f.Piezas.Where(p => p.DeSerie).ToArray();
            entradas.RemoveAll(e => deSerie.Any(p => (int)p.Categoria == e.cat) || f.Apagar.Contains(e.pieza));
            foreach (var p in deSerie) entradas.Add(((int)p.Categoria, p.Nombre));
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
        Personaje = PartyControlManager.CharacterSlot.Estela,
        Carpeta = "Assets/Art/Characters/Protagonistas/Estela",
        CampoDePartida = "estelaDefault",
        PrefabPropio = "Assets/Prefabs/_ESTELA.prefab",
        CuerpoDeAntes = "Body09",
        Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_ESTELA.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" },
        Propios = new[] { "Assets/Prefabs/_ESTELA.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" },
        Piezas = new[]
        {
            new Pieza { Nombre = "Hair14_Coleta", Categoria = PartCategory.Hair, Vecina = "Hair08" },
            new Pieza { Nombre = "Body21_Hechicera", Categoria = PartCategory.Body, Vecina = "Body09",
                        NombreEnArmario = "Traje de hechicera", Descripcion = "Túnica corta azul con ribete dorado, hombreras y botas a juego." },
            new Pieza { Nombre = "Cloak04_Hechicera", Categoria = PartCategory.Cloak, Vecina = "Cloak01|Body09",
                        NombreEnArmario = "Capa de hechicera", Descripcion = "La capa crema de Estela, de los hombros a las rodillas." },
        },
        Apagar = new[] { "AC09_Ribbon" },   // la coleta ya lleva su lazo
        CadenaConMuelle = new[] { "ColetaBone01", "ColetaBone02" },   // la coleta se levanta al correr y no se mete en la capa
        Colores = new[]
        {
            ("Medias", new Color(0.1f, 0.12f, 0.28f)),   // el trozo de pierna entre la túnica y las botas
            ("Botas", new Color(0.09f, 0.11f, 0.3f)),   // a juego con los guantes, con la vuelta dorada
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
        Personaje = PartyControlManager.CharacterSlot.Will,
        Carpeta = "Assets/Art/Characters/Protagonistas/Will",
        CampoDePartida = null,   // Will empieza con lo que lleva puesto el modelo del jugador
        PrefabPropio = "Assets/Prefabs/_WILL.prefab",
        CuerpoDeAntes = "Body05",
        Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab" },
        Propios = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab" },
        Piezas = new[]
        {
            new Pieza { Nombre = "Hair16_Heroe", Categoria = PartCategory.Hair, Vecina = "Hair01" },
            new Pieza { Nombre = "Body23_Aventurero", Categoria = PartCategory.Body, Vecina = "Body05",
                        NombreEnArmario = "Ropa de aventurero", Descripcion = "Camiseta azul marino con ribete dorado y el pañuelo de siempre al cuello." },
            // Raúl (8 nov): la chaqueta deja de ir puesta y queda en su armario. Es otro cuerpo (la misma ropa con la
            // chaqueta encima), no una pieza de capa: así se puede llevar con la capa.
            new Pieza { Nombre = "Body24_AventureroChaqueta", Categoria = PartCategory.Body, Vecina = "Body05", DeSerie = false,
                        NombreEnArmario = "Ropa de aventurero con chaqueta", Descripcion = "La camiseta de siempre con la chaqueta corta azul marino encima, abierta y remangada." },
        },
        Apagar = new[] { "Cloak01", "Cloak02", "Cloak03" },   // empieza sin capa
        Colores = new[]
        {
            ("Pelo", new Color(0.72f, 0.44f, 0.16f)), ("PeloClaro", new Color(0.86f, 0.6f, 0.28f)),   // rubio miel
            ("Chaqueta", new Color(0.1f, 0.16f, 0.38f)), ("Camiseta", new Color(0.1f, 0.16f, 0.38f)), ("Pantalon", new Color(0.3f, 0.24f, 0.18f)),
            ("Cuero", new Color(0.42f, 0.25f, 0.12f)), ("Dorado", new Color(1f, 0.8f, 0.3f)), ("Panuelo", new Color(0.3f, 0.62f, 0.92f)),
        },
    };

    [MenuItem("El Sendero/Personajes/Liam: aspecto nuevo")]
    public static void Liam() => Aplicar(FichaLiam);
    public static Ficha FichaLiam => new Ficha
    {
        Nombre = "Liam",
        Personaje = PartyControlManager.CharacterSlot.Liam,
        Carpeta = "Assets/Art/Characters/Protagonistas/Liam",
        CampoDePartida = "liamDefault",
        PrefabPropio = "Assets/Prefabs/_LIAM.prefab",
        CuerpoDeAntes = "Body12",
        Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_LIAM.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab" },
        Propios = new[] { "Assets/Prefabs/_LIAM.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab" },
        Piezas = new[]
        {
            new Pieza { Nombre = "Hair15_Flequillo", Categoria = PartCategory.Hair, Vecina = "Hair02" },
            new Pieza { Nombre = "Body22_Tunica", Categoria = PartCategory.Body, Vecina = "Body12",
                        NombreEnArmario = "Túnica de viaje", Descripcion = "Túnica marrón corta con cinturón, pantalón y botas." },
            // La capucha es un accesorio: el jugador se la puede quitar en el armario (sale el pelo entero)
            new Pieza { Nombre = "AC12_Capucha", Categoria = PartCategory.Accessory, Vecina = "AC03_BlackGlass|AC01_Heart|Body12",
                        NombreEnArmario = "Capucha", Descripcion = "La capucha de Liam. Sin ella se le ve el flequillo." },
        },
        Colores = new[]
        {
            ("Pelo", new Color(0.24f, 0.12f, 0.06f)), ("PeloClaro", new Color(0.36f, 0.2f, 0.1f)),
            ("Tunica", new Color(0.52f, 0.33f, 0.17f)), ("TunicaOscura", new Color(0.27f, 0.17f, 0.09f)), ("Cuero", new Color(0.2f, 0.12f, 0.07f)),
            ("Pantalon", new Color(0.27f, 0.27f, 0.3f)), ("Dorado", new Color(1f, 0.8f, 0.3f)),
        },
    };
}
