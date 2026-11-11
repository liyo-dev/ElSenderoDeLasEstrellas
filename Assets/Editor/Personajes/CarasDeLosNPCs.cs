using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Caras nuevas por emoción para los NPCs (ojos de anime con cejas, bocas, mejillas), con el mismo sistema que
/// los protagonistas (<see cref="CarasDeLosProtagonistas"/>). Un kit de juegos de ojos (Tools/Modelos/Caras/kit_npcs.py →
/// Assets/Art/Characters/NPCs/Caras/) y una tabla que dice qué juego lleva cada NPC. Las bocas son las de los
/// protagonistas: Mouth20-29 en cabeza de hombre, Mouth40-49 en cabeza de mujer; los niños llevan las suyas.
/// Por cada NPC: quita las cejas que traen dentro el pelo o el sombrero del pack (malla sin cejas, guardada aparte)
/// y pinta las cejas de la cara nueva de su color; quita <see cref="CejasPorEmocion"/>; añade las piezas de su juego,
/// le pone su perfil de emociones y la cara neutra. Se puede repetir (también tras cambiar la tabla).
/// </summary>
public static class CarasDeLosNPCs
{
    const string CarpetaKit = "Assets/Art/Characters/NPCs/Caras";
    const string CarpetaSinCejas = "Assets/Art/Characters/NPCs/SinCejas";
    const string CarpetaCejas = CarpetaKit + "/Materiales/Cejas";
    const string CarasProtas = "Assets/Art/Characters/Protagonistas/Caras";
    const string BocasDeHombre = CarasProtas + "/Caras_chico_marron.fbx";   // Mouth20-29
    const string BocasDeMujer = CarasProtas + "/Caras_chica_marron.fbx";    // Mouth40-49
    static readonly Color CejaPorDefecto = new Color(0.25f, 0.14f, 0.08f);

    enum Cabeza { Hombre, Mujer }

    sealed class Juego
    {
        public string Clave, Fbx; public Color Iris; public int Ojos, Bocas; public Cabeza Cabeza;
        public string FbxDeBocas;      // null = las de su cabeza (protagonistas)
        public bool DeProtagonista;    // FBX que prepara CarasDeLosProtagonistas
    }

    static readonly Color Marron = new Color(0.35f, 0.2f, 0.1f), Verde = new Color(0.2f, 0.55f, 0.25f),
        Azul = new Color(0.22f, 0.42f, 0.72f), Gris = new Color(0.36f, 0.43f, 0.52f),
        Oscuro = new Color(0.2f, 0.12f, 0.08f), Ambar = new Color(0.68f, 0.42f, 0.14f);

    static Juego J(string clave, Color iris, int ojos, Cabeza cabeza, int bocas = 0) => new Juego
    {
        Clave = clave, Fbx = $"{CarpetaKit}/Caras_NPC_{clave}.fbx", Iris = iris, Ojos = ojos, Cabeza = cabeza,
        Bocas = bocas, FbxDeBocas = bocas != 0 ? $"{CarpetaKit}/Caras_NPC_{clave}.fbx" : null,
    };

    /// El kit (los mismos juegos que Tools/Modelos/Caras/kit_npcs.py). h = hombre, m = mujer, hf = hombre con
    /// cabeza de mujer, mh = mujer con cabeza de hombre; la forma y el color van en el nombre.
    static readonly Juego[] Kit =
    {
        J("h_chico_azul", Azul, 80, Cabeza.Hombre), J("h_chico_gris", Gris, 90, Cabeza.Hombre),
        J("h_chico_oscuro", Oscuro, 100, Cabeza.Hombre), J("h_almendra_marron", Marron, 110, Cabeza.Hombre),
        J("h_almendra_verde", Verde, 120, Cabeza.Hombre), J("h_almendra_ambar", Ambar, 130, Cabeza.Hombre),
        J("m_almendra_marron", Marron, 140, Cabeza.Mujer), J("m_almendra_azul", Azul, 150, Cabeza.Mujer),
        J("m_afilado_verde", Verde, 160, Cabeza.Mujer), J("m_almendra_oscuro", Oscuro, 170, Cabeza.Mujer),
        J("hf_chico_marron", Marron, 180, Cabeza.Mujer), J("hf_almendra_gris", Gris, 190, Cabeza.Mujer),
        J("mh_almendra_marron", Marron, 200, Cabeza.Hombre),
        J("nino_chico_ambar", Ambar, 210, Cabeza.Hombre, 210), J("nina_almendra_azul", Azul, 220, Cabeza.Mujer, 220),
        // Mayores (rasgos de edad y cejas pobladas o gruesas), gente del campo (tostada) y jóvenes con pecas
        J("h_mayor_ambar", Ambar, 230, Cabeza.Hombre), J("hf_mayor_marron", Marron, 240, Cabeza.Mujer),
        J("m_mayor_azul", Azul, 250, Cabeza.Mujer), J("h_tostado_marron", Marron, 260, Cabeza.Hombre),
        J("h_pecas_azul", Azul, 270, Cabeza.Hombre), J("m_pecas_marron", Marron, 280, Cabeza.Mujer),
        new Juego { Clave = "will", Fbx = CarasProtas + "/Caras_chico_verde.fbx", Iris = Verde, Ojos = 30, Cabeza = Cabeza.Hombre, DeProtagonista = true },
    };

    /// Qué juego lleva cada NPC (Assets/_NPCs/…, o la ruta entera si empieza por «Assets/»). Si la cabeza del modelo
    /// no casa con el juego, se avisa y no se toca.
    /// Tobías y Nina tienen los suyos (NinosHechosEnBlender).
    static readonly (string npc, string juego)[] Reparto =
    {
        // Hombres
        ("_GrafoNarrativo/Eldran", "h_mayor_ambar"), ("Eldran", "h_mayor_ambar"),
        ("_GrafoNarrativo/Oliver", "h_pecas_azul"), ("Oliver", "h_pecas_azul"),
        ("Leonardo", "h_chico_gris"), ("Manuel", "h_almendra_marron"), ("WoodsGuard", "h_tostado_marron"),
        ("Pueblo/Amigo Eldran", "h_chico_gris"), ("Pueblo/Generados/Renard", "h_almendra_marron"),
        ("Pueblo/MC01 (1)", "h_chico_azul"), ("Pueblo/MC01 (1) 1", "h_almendra_ambar"), ("Pueblo/MC01 (1) 2", "h_chico_oscuro"),
        ("Pueblo/MC01 (1) 3", "h_almendra_verde"), ("Pueblo/MC01 (1) 5", "h_chico_gris"),
        ("Combat/Boy_Pirate", "h_chico_oscuro"), ("Combat/Mago #1", "h_almendra_verde"),
        ("NonInteractable/Guerrero#2", "h_tostado_marron"), ("NonInteractable/TownNpc#1", "h_chico_oscuro"),
        ("NonInteractable/TownNpc#3", "h_almendra_ambar"), ("NonInteractable/TownNpc#5", "h_chico_oscuro"),
        ("NonInteractable/TownNpc#6", "h_almendra_marron"), ("NonInteractable/TownNpc#8", "h_almendra_verde"),
        ("NonInteractable/TownNpc#9", "h_chico_azul"), ("NonInteractable/TownNpc#10", "h_chico_gris"),
        ("Ladron1", "h_chico_oscuro"), ("Ladron2", "h_almendra_ambar"),
        ("AldeaDelMar/Posadero", "h_almendra_ambar"), ("AldeaDelMar/Anselmo", "h_tostado_marron"),
        ("AldeaDelMar/Eusebio", "h_mayor_ambar"), ("AldeaDelMar/Fermin", "h_chico_azul"),
        ("AldeaDelMar/Benito", "h_tostado_marron"),
        ("NonInteractable/VecinoDelMirador", "h_tostado_marron"),
        ("NonInteractable/AbuelaDelMirador", "m_mayor_azul"), ("NonInteractable/VecinaDelMirador", "m_pecas_marron"),
        // Hombres con cabeza de mujer
        ("King", "hf_mayor_marron"), ("Pueblo/Jorge", "hf_chico_marron"), ("Rudolfo", "hf_chico_marron"),
        ("Roberto", "hf_almendra_gris"), ("Combat/Mago #2", "hf_almendra_gris"),
        // Mujeres
        ("Victoria", "m_mayor_azul"), ("_GrafoNarrativo/Boticaria", "m_afilado_verde"), ("_GrafoNarrativo/Selene", "m_almendra_oscuro"),
        ("Combat/Erika", "m_afilado_verde"), ("Combat/Lety", "m_almendra_azul"), ("Combat/Vicky", "m_pecas_marron"),
        ("Nora", "m_almendra_azul"), ("Patricia", "m_almendra_marron"), ("Pueblo/Aldona", "m_almendra_oscuro"),
        ("Pueblo/Generados/Tomasa", "m_almendra_marron"), ("Pueblo/MC01 (1) 6", "m_afilado_verde"),
        ("Sara", "m_almendra_azul"), ("Sofia", "m_almendra_oscuro"), ("Tabernera", "m_almendra_marron"),
        ("Tendera", "m_afilado_verde"), ("Verónica", "m_almendra_azul"),
        ("AldeaDelMar/Remedios", "m_pecas_marron"), ("AldeaDelMar/Engracia", "m_almendra_azul"),
        ("AldeaDelMar/Rosario", "m_almendra_marron"),
        ("NonInteractable/Guerrero#1", "m_almendra_oscuro"), ("NonInteractable/Guerrero#3", "m_almendra_azul"),
        ("NonInteractable/TownNpc#2", "m_almendra_marron"), ("NonInteractable/TownNpc#7", "m_almendra_oscuro"),
        ("NonInteractable/PanaderaDelValle", "m_mayor_azul"), ("NonInteractable/MadreDelValle", "m_pecas_marron"),
        // Mujer con cabeza de hombre
        ("NonInteractable/TownNpc#4", "mh_almendra_marron"),
        // Niños (con su cuerpo de adulto encogido hasta que tengan cuerpo de niño propio)
        ("NiñoPez", "nino_chico_ambar"), ("AmigaDelNiñoPez", "nina_almendra_azul"), ("Recuerdos/WillNino", "will"),
        // El Archimago (prólogo y cinemática final): el modelo antiguo de Will, con los ojos de Will
        ("Assets/Prefabs/_WILL_ORIGINAL.prefab", "will"),
    };

    static readonly Regex PiezaDeCara = new Regex(@"^(Eye|Mouth|Fx)(\d+)$");   // Fx: los símbolos de anime (lágrimas, sudor…)

    [MenuItem("El Sendero/Personajes/Caras de los NPCs")]
    public static void Aplicar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Caras de los NPCs", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Aplicar(informe);
        AssetDatabase.SaveAssets();
        Debug.Log("[CarasDeLosNPCs] " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar("Caras de los NPCs", informe.ToString());   // desde el buzón va al registro (un diálogo lo bloquearía)
    }

    public static void Aplicar(StringBuilder informe)
    {
        string rutaEstilo = CarasDeLosProtagonistas.RutaDeEstilo();
        var estilo = PersonajeHechoEnBlender.MaterialDeEstilo(rutaEstilo);
        if (estilo == null) { informe.AppendLine("No encuentro el material de estilo (cuerpo de Estela)."); return; }

        // 1 · El kit: materiales lisos de cada FBX y perfil de emociones de cada juego
        var piezas = new Dictionary<string, List<(GameObject modelo, string nombre)>>();
        var perfiles = new Dictionary<string, EmotionProfile>();
        var bocas = new Dictionary<Cabeza, List<(GameObject modelo, string nombre)>>
        {
            [Cabeza.Hombre] = Bocas(BocasDeHombre, 20, informe),
            [Cabeza.Mujer] = Bocas(BocasDeMujer, 40, informe),
        };
        foreach (var j in Kit)
        {
            var deEsteFbx = j.DeProtagonista ? PiezasDe(j.Fbx) : CarasDeLosProtagonistas.PrepararFbx(j.Fbx, j.Iris, rutaEstilo, informe);
            var ojos = deEsteFbx.Where(p => p.nombre.StartsWith("Eye")).ToList();
            var simbolos = deEsteFbx.Where(p => p.nombre.StartsWith("Fx")).ToList();
            var susBocas = j.Bocas != 0 ? deEsteFbx.Where(p => p.nombre.StartsWith("Mouth")).ToList() : bocas[j.Cabeza];
            if (ojos.Count == 0 || susBocas.Count == 0) { informe.AppendLine($"Juego {j.Clave}: faltan piezas en {j.Fbx}."); continue; }
            piezas[j.Clave] = ojos.Concat(susBocas).Concat(simbolos).ToList();
            var perfil = CarasDeLosProtagonistas.Perfil($"NPC_{j.Clave}", j.Ojos, j.Bocas != 0 ? j.Bocas : (j.Cabeza == Cabeza.Hombre ? 20 : 40), new StringBuilder());
            if (perfil != null) perfiles[j.Clave] = perfil;
        }

        // 2 · Cada NPC
        int hechos = 0;
        foreach (var (npc, clave) in Reparto)
        {
            string ruta = npc.StartsWith("Assets/") ? npc : $"Assets/_NPCs/{npc}.prefab";
            var j = Kit.FirstOrDefault(k => k.Clave == clave);
            if (j == null || !piezas.ContainsKey(clave) || !perfiles.ContainsKey(clave)) { informe.AppendLine($"{npc}: el juego {clave} no está listo."); continue; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null) { informe.AppendLine($"{npc}: no encuentro {ruta}."); continue; }
            var raiz = PrefabUtility.LoadPrefabContents(ruta);
            try
            {
                if (Vestir(raiz, j, piezas[clave], perfiles[clave], estilo, npc, informe))
                {
                    PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
                    hechos++;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
        informe.Insert(0, $"{hechos} de {Reparto.Length} NPCs con cara nueva.\n");
        // Las piezas de la cara llegan con la piel del pack: después, cada NPC a su tono.
        TonosDePiel.Aplicar(informe, RutasDeLosNPCs());
    }

    /// Los prefabs de los NPCs del reparto.
    public static IEnumerable<string> RutasDeLosNPCs() =>
        Reparto.Select(r => r.npc.StartsWith("Assets/") ? r.npc : $"Assets/_NPCs/{r.npc}.prefab");

    static bool Vestir(GameObject raiz, Juego j, List<(GameObject modelo, string nombre)> piezas, EmotionProfile perfil,
                       Material estilo, string npc, StringBuilder informe)
    {
        var renderers = raiz.GetComponentsInChildren<Renderer>(true);
        var cabeza = renderers.FirstOrDefault(r => r.gameObject.activeSelf && r.name.StartsWith("Head0"));
        if (cabeza == null) { informe.AppendLine($"{npc}: sin cabeza del pack a la vista; no se toca."); return false; }
        var suCabeza = cabeza.name.StartsWith("Head02") ? Cabeza.Mujer : Cabeza.Hombre;
        if (suCabeza != j.Cabeza) { informe.AppendLine($"{npc}: tiene {cabeza.name} y el juego {j.Clave} es para cabeza de {j.Cabeza}; no se toca."); return false; }

        // Las cejas: las que traen el pelo o el sombrero del pack salen de su malla; su color pasa a las cejas nuevas
        Material ceja = renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name.StartsWith("Mat_Ceja_"));
        var ojosDelPack = renderers.FirstOrDefault(r => r is MeshRenderer && r.gameObject.activeSelf && PiezaDeCara.IsMatch(r.name) && r.name.StartsWith("Eye"));
        foreach (var r in renderers)
        {
            if (!(r is MeshRenderer) || !r.gameObject.activeSelf) continue;
            var filtro = r.GetComponent<MeshFilter>();
            if (filtro == null || filtro.sharedMesh == null) continue;
            if (r.name.StartsWith("Eyebrow"))
            {
                if (TodosLosVertices(filtro.sharedMesh, out var todos)) ceja = MaterialDeCeja(filtro.sharedMesh.name, filtro.sharedMesh, todos, r, estilo) ?? ceja;
                continue;
            }
            if (ojosDelPack == null || !(r.name.StartsWith("Hair") || r.name.StartsWith("Hat"))) continue;   // las cejas del pack van en pelos y sombreros
            var sinCejas = QuitarCejas(filtro, ojosDelPack, r, estilo, out var suCeja);
            if (sinCejas == null) continue;
            filtro.sharedMesh = sinCejas;
            if (suCeja != null) ceja = suCeja;
        }
        if (ceja == null) ceja = PersonajeHechoEnBlender.Liso(estilo, $"{CarpetaCejas}/Mat_Ceja_Defecto.mat", CejaPorDefecto);
        foreach (var c in raiz.GetComponentsInChildren<CejasPorEmocion>(true)) Object.DestroyImmediate(c, true);

        // Fuera las piezas de cara nuevas de otro juego (si cambió la tabla); dentro las del suyo
        var suyas = new HashSet<string>(piezas.Select(p => p.nombre));
        foreach (var r in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var m = PiezaDeCara.Match(r.name);
            if (m.Success && int.Parse(m.Groups[2].Value) >= 20 && !suyas.Contains(r.name)) Object.DestroyImmediate(r.gameObject, true);
        }
        int puestas = 0;
        foreach (var (modelo, nombre) in piezas)
        {
            string vecina = !nombre.StartsWith("Mouth") ? "Eye01|Eye02|Eye03|Eye04|Eye05|Eye06|Eye07|Eye08|Eye09|Eye10|Eye11|Eye12"
                                                        : "Mouth01|Mouth02|Mouth03|Mouth04|Mouth05|Mouth06|Mouth07|Mouth08|Mouth09|Mouth10|Mouth11|Mouth12";
            if (PiezaNuevaDelModelo.Anadir(raiz, modelo, nombre, vecina, new StringBuilder())) puestas++;
        }
        foreach (var r in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!r.name.StartsWith("Eye") || !suyas.Contains(r.name)) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.EndsWith("_Ceja")) mats[i] = ceja;
            r.sharedMaterials = mats;
        }
        CarasDeLosProtagonistas.PonerCara(raiz, perfil, j.Ojos, j.Bocas != 0 ? j.Bocas : (j.Cabeza == Cabeza.Hombre ? 20 : 40));
        informe.AppendLine($"{npc}: {j.Clave} ({puestas} piezas), cejas {ceja.name.Replace("Mat_Ceja_", "")}.");
        return true;
    }

    // ── Cejas del pack ────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Malla sin cejas de un pelo o sombrero del pack (las dos islas de las cejas fuera; se localizan como en
    /// <see cref="CejasPorEmocion.BuscarIslasDeCejas"/>), guardada en SinCejas y compartida por todos los que lo
    /// llevan; y el material del color de esas cejas. Null si la malla no tiene cejas o ya es la de sin cejas.
    /// </summary>
    static Mesh QuitarCejas(MeshFilter filtro, Renderer ojos, Renderer renderer, Material estilo, out Material ceja)
    {
        ceja = null;
        var original = filtro.sharedMesh;
        if (original.name.EndsWith("_SinCejas")) return null;
        string ruta = $"{CarpetaSinCejas}/{original.name}_SinCejas.asset";
        var hecha = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (hecha != null)
        {
            ceja = AssetDatabase.LoadAssetAtPath<Material>($"{CarpetaCejas}/Mat_Ceja_{original.name}.mat");
            return hecha;
        }
        var malla = Legible(original);
        if (malla == null) return null;
        var vertices = malla.vertices;
        var indices = Enumerable.Range(0, malla.subMeshCount).SelectMany(s => malla.GetTriangles(s)).ToArray();
        var caja = CejasPorEmocion.BoundsDeOjosEnEspacio(ojos, filtro.transform);
        if (!CejasPorEmocion.BuscarIslasDeCejas(vertices, indices, caja, out var izq, out var der, out _, out _)) return null;
        var fuera = new HashSet<int>(izq.Concat(der));

        var nueva = Object.Instantiate(malla);
        nueva.name = original.name + "_SinCejas";
        for (int s = 0; s < malla.subMeshCount; s++)
        {
            var tri = malla.GetTriangles(s);
            var quedan = new List<int>(tri.Length);
            for (int i = 0; i + 2 < tri.Length; i += 3)
                if (!fuera.Contains(tri[i]) && !fuera.Contains(tri[i + 1]) && !fuera.Contains(tri[i + 2]))
                    quedan.AddRange(new[] { tri[i], tri[i + 1], tri[i + 2] });
            nueva.SetTriangles(quedan, s);
        }
        Directory.CreateDirectory(CarpetaSinCejas);
        AssetDatabase.CreateAsset(nueva, ruta);
        ceja = MaterialDeCeja(original.name, malla, fuera, renderer, estilo);
        return nueva;
    }

    static bool TodosLosVertices(Mesh malla, out HashSet<int> todos)
    {
        todos = null;
        var legible = Legible(malla);
        if (legible == null) return false;
        todos = new HashSet<int>(Enumerable.Range(0, legible.vertexCount));
        return true;
    }

    /// <summary>Material liso del color medio de esos vértices en la textura del pack (algo más oscuro, como una ceja).</summary>
    static Material MaterialDeCeja(string nombre, Mesh malla, ICollection<int> vertices, Renderer renderer, Material estilo)
    {
        var mat = renderer.sharedMaterial;
        var tex = mat == null ? null : (mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null) ?? mat.mainTexture;
        var uv = Legible(malla)?.uv;
        if (tex == null || uv == null || uv.Length == 0 || vertices.Count == 0) return null;
        var pixeles = Pixeles(tex, out int ancho, out int alto);
        if (pixeles == null) return null;
        Color suma = Color.clear;
        foreach (int i in vertices)
        {
            float u = uv[i].x - Mathf.Floor(uv[i].x), v = uv[i].y - Mathf.Floor(uv[i].y);
            suma += pixeles[Mathf.Clamp((int)(v * alto), 0, alto - 1) * ancho + Mathf.Clamp((int)(u * ancho), 0, ancho - 1)];
        }
        var color = suma / vertices.Count * 0.85f;
        color.a = 1f;
        return PersonajeHechoEnBlender.Liso(estilo, $"{CarpetaCejas}/Mat_Ceja_{nombre}.mat", color);
    }

    static Color[] Pixeles(Texture tex, out int ancho, out int alto)
    {
        ancho = tex.width; alto = tex.height;
        var rt = RenderTexture.GetTemporary(ancho, alto, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var antes = RenderTexture.active;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var copia = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
            copia.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            copia.Apply();
            var px = copia.GetPixels();
            Object.DestroyImmediate(copia);
            return px;
        }
        finally { RenderTexture.active = antes; RenderTexture.ReleaseTemporary(rt); }
    }

    /// <summary>La malla con sus datos legibles: si su FBX no tiene Read/Write, se le activa.</summary>
    static Mesh Legible(Mesh malla)
    {
        if (malla.isReadable) return malla;
        string ruta = AssetDatabase.GetAssetPath(malla);
        if (!(AssetImporter.GetAtPath(ruta) is ModelImporter imp)) return null;
        imp.isReadable = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Mesh>().FirstOrDefault(m => m.name == malla.name);
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────────────────────────

    static List<(GameObject modelo, string nombre)> PiezasDe(string fbx)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        return modelo == null ? new List<(GameObject, string)>()
            : modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => (modelo, r.name)).ToList();
    }

    /// Las diez bocas de un juego de los protagonistas (Mouth&lt;base&gt;…+9).
    static List<(GameObject modelo, string nombre)> Bocas(string fbx, int base_, StringBuilder informe)
    {
        var lista = PiezasDe(fbx).Where(p => PiezaDeCara.Match(p.nombre) is var m && m.Success && m.Groups[1].Value == "Mouth"
                                             && int.Parse(m.Groups[2].Value) is int n && n >= base_ && n < base_ + 10).ToList();
        if (lista.Count < 10) informe.AppendLine($"Bocas: en {fbx} hay {lista.Count} de 10 (pasa antes «Caras de los protagonistas»).");
        return lista;
    }
}
