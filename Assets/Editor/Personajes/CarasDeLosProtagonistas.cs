using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Las caras nuevas de los protagonistas (Tools/Modelos/Caras/): un juego de ojos (con sus cejas) y de bocas
/// por emoción, como piezas del modelo modular (Eye&lt;NN&gt;, Mouth&lt;NN&gt;), en tres FBX:
///   Caras_chico_marron: Eye20-28 + Mouth20-29 (Liam; las bocas también las usa Will)
///   Caras_chico_verde:  Eye30-38 (Will)
///   Caras_chica_marron: Eye40-51 + Mouth40-50 (Estela: ojos de anime «noventa», con pestañas; además
///                       Eye49 guiño, Eye50 conjuro, Eye51 vergüenza y Mouth50 risa con colmillo)
/// Las mejillas y las cejas de cada juego las elige Tools/Modelos/Caras/kit_npcs.py.
/// Orden de los ojos: normal, feliz, triste, enfado, sorpresa, miedo, pensar, cansado, cerrados, (extras,) llorar.
/// Cada juego trae también los símbolos de anime (lágrimas, sudor, vena, susto) como fotogramas Fx&lt;ojos×10 + i&gt;.
/// Orden de las bocas: neutra, sonrisa, risa, triste, o, enfado, ondulada, pícara, entreabierta, abierta.
/// El menú añade todas las piezas a los modelos de los protagonistas, crea un perfil de emociones por
/// personaje (copia de NpcEmotionProfile con estas piezas), se lo pone a sus caras (y al registro, para
/// el cuerpo del jugador, que cambia de personaje) y les deja la cara neutra puesta. Se puede repetir.
/// </summary>
public static class CarasDeLosProtagonistas
{
    const string Carpeta = "Assets/Art/Characters/Protagonistas/Caras";
    const string CarpetaPerfiles = "Assets/_EmotionProfile";
    const string PerfilBase = CarpetaPerfiles + "/NpcEmotionProfile.asset";
    const string EscenaDelRegistro = "Assets/Scenes/Systems/Start.unity";

    sealed class Juego
    {
        public Slot Personaje; public string Nombre; public int Ojos, Bocas;
        public int Llorar = 9;       // índice de los ojos de llorar (el último del juego: después de los extras, si los hay)
        public string[] Modelos;     // los suyos: se les pone su perfil y su cara neutra
    }

    static readonly Juego[] Juegos =
    {
        new Juego { Personaje = Slot.Will, Nombre = "Will", Ojos = 30, Bocas = 20,
                    Modelos = new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab" } },
        new Juego { Personaje = Slot.Liam, Nombre = "Liam", Ojos = 20, Bocas = 20,
                    Modelos = new[] { "Assets/Prefabs/_LIAM.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab" } },
        new Juego { Personaje = Slot.Estela, Nombre = "Estela", Ojos = 40, Bocas = 40, Llorar = 12,
                    Modelos = new[] { "Assets/Prefabs/_ESTELA.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab" } },
    };

    static readonly string[] TodosLosModelos =
    {
        "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_WILL_NPC.prefab", "Assets/Prefabs/_LIAM.prefab", "Assets/Prefabs/_ESTELA.prefab",
        "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Will.prefab", "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Liam.prefab",
        "Assets/Prefabs/UI/RetratosVivos/RetratoVivo_Estela.prefab",
    };

    static readonly (string fbx, Color iris)[] Fbx =
    {
        ("Caras_chico_marron", new Color(0.35f, 0.2f, 0.1f)),
        ("Caras_chico_verde", new Color(0.2f, 0.55f, 0.25f)),
        ("Caras_chica_marron", new Color(0.35f, 0.2f, 0.1f)),
    };

    /// Índice de ojo que vale «los ojos de llorar de este juego» (cada juego los tiene en su sitio).
    public const int OjosDeLlorar = -2;

    // Ojo y boca de cada emoción (índices en el orden de arriba; -1 = la cara de reposo)
    static readonly (NPCEmotion emocion, int ojo, int boca)[] Emociones =
    {
        (NPCEmotion.Crying, OjosDeLlorar, 3), (NPCEmotion.HappyTears, 1, 1), (NPCEmotion.Asleep, 8, 0),
        (NPCEmotion.Neutral, -1, -1), (NPCEmotion.Happy, 1, 2), (NPCEmotion.Sad, 2, 3), (NPCEmotion.Angry, 3, 5),
        (NPCEmotion.Surprised, 4, 4), (NPCEmotion.Scared, 5, 6), (NPCEmotion.Thinking, 6, 0), (NPCEmotion.Tired, 7, 0),
        (NPCEmotion.Smirk, -1, 7), (NPCEmotion.Worried, 2, 6), (NPCEmotion.Determined, 3, 0), (NPCEmotion.Relieved, 1, 1),
        (NPCEmotion.Confused, 6, 6), (NPCEmotion.Excited, 4, 2), (NPCEmotion.Annoyed, 7, 3), (NPCEmotion.Grateful, 1, 1),
    };

    // Los símbolos de anime de cada juego: fotogramas Fx<ojos×10 + primero…>, en el orden de SIMBOLOS de Tools/Modelos/Caras
    static readonly (string simbolo, int primero, int fotogramas, float segundos)[] Simbolos =
    {
        ("lagrimas", 0, 6, 0.11f), ("sudor", 6, 3, 0.35f), ("vena", 9, 2, 0.22f), ("susto", 11, 2, 0.07f),
    };
    static readonly (NPCEmotion emocion, string simbolo)[] SimboloDeCadaEmocion =
    {
        (NPCEmotion.Angry, "vena"), (NPCEmotion.Scared, "susto"), (NPCEmotion.Worried, "sudor"),
        (NPCEmotion.Crying, "lagrimas"), (NPCEmotion.HappyTears, "lagrimas"),
    };
    // Si el perfil base aún no trae una emoción nueva, toma el gesto del cuerpo de su pariente
    static readonly (NPCEmotion nueva, NPCEmotion pariente)[] GestoDePariente =
        { (NPCEmotion.Crying, NPCEmotion.Sad), (NPCEmotion.HappyTears, NPCEmotion.Relieved) };

    [MenuItem("El Sendero/Personajes/Caras de los protagonistas")]
    public static void Aplicar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Caras", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Aplicar(informe);
        Debug.Log("[CarasDeLosProtagonistas] " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar("Caras de los protagonistas", informe.ToString());   // desde el buzón va al registro (un diálogo lo bloquearía)
    }

    public static void Aplicar(StringBuilder informe)
    {

        // 1 · Los FBX: materiales lisos (Quibli, como el cuerpo de Estela) y piezas
        string rutaEstilo = RutaDeEstilo();
        var piezas = new List<(GameObject modelo, string nombre)>();
        foreach (var (fbx, iris) in Fbx) piezas.AddRange(PrepararFbx($"{Carpeta}/{fbx}.fbx", iris, rutaEstilo, informe));

        // 2 · Las piezas en todos los modelos de los protagonistas (apagadas)
        foreach (var ruta in TodosLosModelos)
        {
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                int n = 0;
                var sb = new StringBuilder();
                foreach (var (modelo, nombre) in piezas)
                    if (PiezaNuevaDelModelo.Anadir(raiz, modelo, nombre, nombre.StartsWith("Mouth") ? "Mouth01|Mouth02" : "Eye01|Eye02", sb)) n++;
                PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                informe.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(ruta)}: {n} piezas de cara.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        // 3 · Un perfil por personaje, puesto en sus modelos con la cara neutra encendida
        var perfiles = new Dictionary<Slot, EmotionProfile>();
        foreach (var j in Juegos)
        {
            var extras = j.Personaje == Slot.Estela   // sus extra: pícara con guiño y colmillo, emocionada con colmillo
                ? new[] { (NPCEmotion.Smirk, 9, 10), (NPCEmotion.Excited, 4, 10) } : null;
            var perfil = Perfil(j.Nombre, j.Ojos, j.Bocas, informe, extras, j.Llorar);
            if (perfil == null) continue;
            perfiles[j.Personaje] = perfil;
            foreach (var ruta in j.Modelos)
            {
                var raiz = PrefabUtility.LoadPrefabContents(ruta);
                try
                {
                    PonerCara(raiz, perfil, j.Ojos, j.Bocas);
                    PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }
        }

        // 4 · El registro (Start): el cuerpo del jugador cambia de cara al cambiar de personaje; Liam y Estela empiezan con la suya
        Registro(perfiles, informe);
        var will = Juegos.First(j => j.Personaje == Slot.Will);
        AspectoDeProtagonista.ActualizarPresetsDelJugador(new Dictionary<PartCategory, string>
            { { PartCategory.Eyes, $"Eye{will.Ojos}" }, { PartCategory.Mouth, $"Mouth{will.Bocas}" } }, new[] { PartCategory.Eyebrow }, informe);
        AssetDatabase.SaveAssets();
    }

    /// <summary>El material del que copian el estilo las caras (el cuerpo de Estela, Quibli).</summary>
    public static string RutaDeEstilo()
    {
        var estela = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/_ESTELA.prefab");
        var estilo = estela?.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.StartsWith("Body"))?.sharedMaterial;
        return estilo != null ? AssetDatabase.GetAssetPath(estilo) : null;
    }

    /// <summary>
    /// Prepara un FBX de caras (materiales lisos con el estilo dado, en «Materiales» junto al FBX) y devuelve
    /// sus piezas (Eye…, Mouth…) para <see cref="PiezaNuevaDelModelo"/>.
    /// </summary>
    public static List<(GameObject modelo, string nombre)> PrepararFbx(string ruta, Color iris, string rutaEstilo, StringBuilder informe)
    {
        var piezas = new List<(GameObject modelo, string nombre)>();
        var colores = new[]
        {
            ("Negro", new Color(0.08f, 0.05f, 0.06f)), ("Iris", iris), ("Blanco", Color.white), ("Boca", new Color(0.45f, 0.1f, 0.12f)),
            ("Lengua", new Color(0.95f, 0.45f, 0.45f)), ("Ceja", new Color(0.25f, 0.14f, 0.08f)), ("Piel", new Color(1f, 0.82f, 0.7f)),
            ("Rubor", new Color(1f, 0.55f, 0.6f)), ("RuborFuerte", new Color(0.95f, 0.3f, 0.38f)), ("Brilla", new Color(1f, 0.85f, 0.2f)),
            ("Tostado", new Color(0.93f, 0.6f, 0.47f)), ("Peca", new Color(0.72f, 0.44f, 0.3f)), ("Arruga", new Color(0.78f, 0.56f, 0.48f)),
            ("Lagrima", new Color(0.4f, 0.7f, 1f)), ("Vena", new Color(0.86f, 0.16f, 0.2f)), ("Susto", new Color(0.42f, 0.48f, 0.86f)),
            ("IrisOscuro", new Color(iris.r * 0.45f, iris.g * 0.45f, iris.b * 0.45f)),
        };
        string materiales = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/') + "/Materiales";
        if (!PersonajeHechoEnBlender.PrepararPrenda(ruta, materiales, rutaEstilo, colores, informe)) return piezas;
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        foreach (var r in modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true)) piezas.Add((modelo, r.name));
        return piezas;
    }

    /// <summary>
    /// Pone en un modelo abierto su perfil de emociones y la cara neutra (Eye&lt;ojos&gt;, Mouth&lt;bocas&gt;);
    /// apaga los demás ojos (también las cejas del pack) y bocas.
    /// </summary>
    public static void PonerCara(GameObject raiz, EmotionProfile perfil, int ojos, int bocas)
    {
        foreach (var c in raiz.GetComponentsInChildren<NPCEmotionController>(true))
        {
            var so = new SerializedObject(c);
            so.FindProperty("emotionProfile").objectReferenceValue = perfil;
            so.FindProperty("ojosDeReposo").objectReferenceValue = null;
            so.FindProperty("bocaDeReposo").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        string ojo = $"Eye{ojos}", boca = $"Mouth{bocas}";
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.StartsWith("Eye")) r.gameObject.SetActive(r.name == ojo);
            else if (r.name.StartsWith("Mouth")) r.gameObject.SetActive(r.name == boca);
            else if (r.name.StartsWith("Fx")) r.gameObject.SetActive(false);   // los símbolos los enciende cada emoción
        }
    }

    /// <summary>
    /// Perfil de emociones Cara_&lt;nombre&gt; (copia de NpcEmotionProfile) con un juego de caras: ojos
    /// Eye&lt;ojos&gt;…+8 (y los de llorar, Eye&lt;ojos + llorar&gt;) y bocas Mouth&lt;bocas&gt;…+9 en el orden de arriba,
    /// con los símbolos Fx&lt;ojos×10…&gt;; «extras» cambia el ojo y la boca de alguna emoción (índices dentro del juego).
    /// </summary>
    public static EmotionProfile Perfil(string nombre, int ojos, int bocas, StringBuilder informe, (NPCEmotion emocion, int ojo, int boca)[] extras = null, int llorar = 9)
    {
        var baseP = AssetDatabase.LoadAssetAtPath<EmotionProfile>(PerfilBase);
        if (baseP == null) { informe.AppendLine($"No encuentro {PerfilBase}."); return null; }
        string ruta = $"{CarpetaPerfiles}/Cara_{nombre}.asset";
        var p = AssetDatabase.LoadAssetAtPath<EmotionProfile>(ruta);
        if (p == null) { p = Object.Instantiate(baseP); AssetDatabase.CreateAsset(p, ruta); }
        else EditorUtility.CopySerialized(baseP, p);
        string O(int i) => $"Eye{ojos + i}";
        string B(int i) => $"Mouth{bocas + i}";
        p.ojosNeutros = new[] { O(0) };
        p.bocasNeutras = new[] { B(0), B(1) };
        p.ojosDeReposoPorDefecto = O(0);
        p.bocaDeReposoPorDefecto = B(0);
        p.bocaHablandoEntreabierta = B(8);
        p.bocaHablandoAbierta = B(9);
        p.ojosDelParpadeo = O(8);
        p.ojosYaCerrados = new[] { O(1), O(8), O(llorar) };
        var lista = new List<EmotionMeshData>(p.emotions ?? new EmotionMeshData[0]);
        foreach (var (emocion, ojo, boca) in Emociones)
        {
            int i = lista.FindIndex(e => e.emotion == emocion);
            var d = i >= 0 ? lista[i] : new EmotionMeshData { emotion = emocion };
            d.eyeMeshName = ojo == OjosDeLlorar ? O(llorar) : ojo < 0 ? "" : O(ojo);
            d.mouthMeshName = boca < 0 ? "" : B(boca);
            d.simbolos = null; d.segundosPorSimbolo = 0f;
            foreach (var (conSimbolo, simbolo) in SimboloDeCadaEmocion)
            {
                if (conSimbolo != emocion) continue;
                var s = Simbolos.First(x => x.simbolo == simbolo);
                d.simbolos = Enumerable.Range(s.primero, s.fotogramas).Select(k => $"Fx{ojos * 10 + k}").ToArray();
                d.segundosPorSimbolo = s.segundos;
            }
            if (i >= 0) lista[i] = d; else lista.Add(d);
        }
        foreach (var (nueva, pariente) in GestoDePariente)
        {
            int i = lista.FindIndex(e => e.emotion == nueva), j = lista.FindIndex(e => e.emotion == pariente);
            if (i < 0 || j < 0 || !string.IsNullOrEmpty(lista[i].bodyAnimStateName)) continue;
            var d = lista[i]; d.bodyAnimStateName = lista[j].bodyAnimStateName; d.bodyAnimVariants = lista[j].bodyAnimVariants; lista[i] = d;
        }
        if (extras != null)
            foreach (var (emocion, ojo, boca) in extras)
            {
                int i = lista.FindIndex(e => e.emotion == emocion);
                if (i >= 0) { var d = lista[i]; d.eyeMeshName = O(ojo); d.mouthMeshName = B(boca); lista[i] = d; }
            }
        p.emotions = lista.ToArray();
        p.name = $"Cara_{nombre}";
        EditorUtility.SetDirty(p);
        informe.AppendLine($"Perfil {ruta}: ojos {O(0)}–{O(8)} y {O(llorar)}, bocas {B(0)}–{B(9)}, símbolos Fx{ojos * 10}–Fx{ojos * 10 + Simbolos.Sum(s => s.fotogramas) - 1}.");
        return p;
    }

    static void Registro(Dictionary<Slot, EmotionProfile> perfiles, StringBuilder informe)
    {
        var abierta = EditorSceneManager.GetSceneByPath(EscenaDelRegistro);
        bool cerrar = !abierta.isLoaded;
        if (cerrar && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { informe.AppendLine("Registro: cancelado (escenas sin guardar)."); return; }
        var escena = cerrar ? EditorSceneManager.OpenScene(EscenaDelRegistro, OpenSceneMode.Additive) : abierta;
        try
        {
            var registro = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CharacterAppearanceRegistry>(true)).FirstOrDefault();
            if (registro == null) { informe.AppendLine($"No encuentro CharacterAppearanceRegistry en {EscenaDelRegistro}."); return; }
            var so = new SerializedObject(registro);
            var lista = so.FindProperty("perfilesDeCara");
            lista.arraySize = 3;
            foreach (var kv in perfiles) lista.GetArrayElementAtIndex((int)kv.Key).objectReferenceValue = kv.Value;
            foreach (var j in Juegos)
            {
                string campo = j.Personaje == Slot.Liam ? "liamDefault" : j.Personaje == Slot.Estela ? "estelaDefault" : null;
                if (campo == null) continue;
                var aspecto = so.FindProperty(campo);
                for (int i = aspecto.arraySize - 1; i >= 0; i--)
                {
                    int cat = aspecto.GetArrayElementAtIndex(i).FindPropertyRelative("category").intValue;
                    if (cat == (int)PartCategory.Eyes || cat == (int)PartCategory.Mouth || cat == (int)PartCategory.Eyebrow) aspecto.DeleteArrayElementAtIndex(i);
                }
                foreach (var (cat, pieza) in new[] { (PartCategory.Eyes, $"Eye{j.Ojos}"), (PartCategory.Mouth, $"Mouth{j.Bocas}") })
                {
                    aspecto.arraySize++;
                    var e = aspecto.GetArrayElementAtIndex(aspecto.arraySize - 1);
                    e.FindPropertyRelative("category").intValue = (int)cat;
                    e.FindPropertyRelative("partName").stringValue = pieza;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            informe.AppendLine("Registro: perfiles de cara por personaje y caras neutras de Liam y Estela.");
        }
        finally { if (cerrar) EditorSceneManager.CloseScene(escena, true); }
    }
}
