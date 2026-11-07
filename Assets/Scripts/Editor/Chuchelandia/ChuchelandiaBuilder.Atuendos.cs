using System.Text;
using Invector.vCharacterController;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Lo que Chuchelandia cambia en el grupo: los atuendos de escena «Candidata» y «Victoria»
/// (<see cref="AtuendoDeEscena"/>, con prendas del armario del proyecto), la transformación de la
/// magia en purpurina y corazoncitos (<see cref="TransformacionDeMagia"/>) y el planeo en _WILL.prefab
/// (<see cref="PlaneoDelJugador"/>, que concede el lazo). Los atuendos se crean con la tabla de abajo
/// si no existen; en cada montaje, las prendas y prendas sueltas de cada personaje son las de la tabla
/// (sin tintes); los rasgos se retocan en el Inspector y el menú no los pisa.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string CarpetaAtuendos = "Assets/_WARDROBE ITEMS/Atuendos";
    private const string RutaCandidata = CarpetaAtuendos + "/Atuendo_Chuchelandia_Candidata.asset";
    private const string RutaVictoria = CarpetaAtuendos + "/Atuendo_Chuchelandia_Victoria.asset";
    private const string RutaTransformacion = "Assets/_SPELLS/Transformaciones/TransformacionDeMagia_Chuchelandia.asset";
    private const string RutaWill = "Assets/Prefabs/_WILL.prefab";
    private const string Prenda = "Assets/_WARDROBE ITEMS/WardrobeItem_";

    private sealed class Atuendos
    {
        public AtuendoDeEscena Candidata, Victoria;
    }

    /// <summary>La parte de un atuendo para un personaje: prendas del armario, prendas sueltas y rasgos.</summary>
    private sealed class Parte
    {
        public Slot Personaje;
        public string[] Prendas;
        /// <summary>Prendas hechas aparte (FBX con el esqueleto de RPG Tiny Hero) que se ponen encima (PrendasSueltas).</summary>
        public string[] Sueltas = new string[0];
        /// <summary>Categorías que el atuendo quita (p. ej. la capa bajo un vestido).</summary>
        public PartCategory[] Quitar = new PartCategory[0];
        public bool Planear;
        public float ReboteExtra;
    }

    // ── Tabla de atuendos ───────────────────────────────────────────────────────────────────
    // Estela: debajo, su ropa de antes (Body09, más fina que el traje de hechicera, que no cabe bajo el
    // vestido), y encima el vestido de candidata (siete volantes, mangas de
    // farol, lazo gigante a la espalda, que deja planear, y zapatos de caramelo); en la victoria, además,
    // la tiara (AC06_Crown02) en lugar del lazo de la cabeza. Will y Liam conservan su ropa y llevan encima
    // su disfraz (1 nov: con el cuerpo fino de antes debajo —Body05 / Body12—, que es sobre el que se
    // hicieron los disfraces: la chaqueta de Will y la túnica y la capucha de Liam nuevas asomaban por encima): Will la armadura de gominola con orejitas de osito (rebota de más), Liam el traje de paje
    // con el corazón en el pecho, boina y cuerno de unicornio.
    // La tabla manda en las prendas, las prendas sueltas y los tintes (ninguno) de cada personaje; los
    // rasgos se ponen al crear el atuendo y después se retocan en el Inspector.
    private const string CarpetaPrendas = "Assets/Art/Characters/Chuchelandia/Prendas";
    private const string VestidoDeCandidata = CarpetaPrendas + "/Vestido_Candidata.fbx";
    private const string ArmaduraDeGominolaDeWill = CarpetaPrendas + "/Armadura_Gominola.fbx";
    private const string TrajeDePajeDeLiam = CarpetaPrendas + "/Traje_Paje.fbx";

    private static Parte[] TablaCandidata() => new[]
    {
        new Parte { Personaje = Slot.Estela, Prendas = new[] { "Body09", "AC09_Ribbon" }, Sueltas = new[] { VestidoDeCandidata }, Quitar = new[] { PartCategory.Cloak }, Planear = true },
        new Parte { Personaje = Slot.Will, Prendas = new[] { "Body05" }, Sueltas = new[] { ArmaduraDeGominolaDeWill }, Quitar = new[] { PartCategory.Cloak }, ReboteExtra = 0.6f },
        new Parte { Personaje = Slot.Liam, Prendas = new[] { "Body12" }, Sueltas = new[] { TrajeDePajeDeLiam }, Quitar = new[] { PartCategory.Accessory } },
    };

    private static Parte[] TablaVictoria() => new[]
    {
        new Parte { Personaje = Slot.Estela, Prendas = new[] { "Body09", "AC06_Crown02" }, Sueltas = new[] { VestidoDeCandidata }, Quitar = new[] { PartCategory.Cloak } },
        new Parte { Personaje = Slot.Will, Prendas = new[] { "Body05" }, Sueltas = new[] { ArmaduraDeGominolaDeWill }, Quitar = new[] { PartCategory.Cloak }, ReboteExtra = 0.6f },
        new Parte { Personaje = Slot.Liam, Prendas = new[] { "Body12" }, Sueltas = new[] { TrajeDePajeDeLiam }, Quitar = new[] { PartCategory.Accessory } },
    };

    private static readonly (string material, Color color)[] ColoresDeLaArmadura =
    {
        ("Gominola", new Color(0.35f, 0.9f, 0.3f)), ("GominolaClara", new Color(0.7f, 1f, 0.55f)), ("GominolaOscura", new Color(0.15f, 0.65f, 0.2f)),
    };

    private static readonly (string material, Color color)[] ColoresDelPaje =
    {
        ("Paje", new Color(0.55f, 0.78f, 1f)), ("Corazon", new Color(1f, 0.35f, 0.6f)), ("Blanco", new Color(1f, 0.97f, 0.95f)),
        ("Dorado", new Color(1f, 0.8f, 0.3f)), ("Boina", new Color(0.95f, 0.45f, 0.7f)),
    };

    /// <summary>Colores lisos del vestido (los nombres son los materiales del FBX).</summary>
    private static readonly (string material, Color color)[] ColoresDelVestido =
    {
        ("Vestido", new Color(1f, 0.5f, 0.72f)), ("Volante", new Color(1f, 0.93f, 0.96f)), ("Lazo", new Color(0.95f, 0.22f, 0.48f)),
        ("Caramelo", new Color(0.86f, 0.12f, 0.2f)), ("Dorado", new Color(1f, 0.8f, 0.3f)),
    };

    private static Atuendos AsegurarAtuendos(StringBuilder informe)
    {
        // El tinte de la armadura se pone al día en cada montaje: los atuendos ya creados nombran ese material.
        PersonajeHechoEnBlender.PrepararPrenda(VestidoDeCandidata, CarpetaReparto + "/Materiales", EstiloDeLosPersonajes, ColoresDelVestido, informe);
        PersonajeHechoEnBlender.PrepararPrenda(ArmaduraDeGominolaDeWill, CarpetaReparto + "/Materiales", EstiloDeLosPersonajes, ColoresDeLaArmadura, informe);
        PersonajeHechoEnBlender.PrepararPrenda(TrajeDePajeDeLiam, CarpetaReparto + "/Materiales", EstiloDeLosPersonajes, ColoresDelPaje, informe);
        return new Atuendos
        {
            Candidata = AsegurarAtuendo(RutaCandidata, TablaCandidata(), informe),
            Victoria = AsegurarAtuendo(RutaVictoria, TablaVictoria(), informe),
        };
    }

    private static AtuendoDeEscena AsegurarAtuendo(string ruta, Parte[] partes, StringBuilder informe)
    {
        var atuendo = AssetDatabase.LoadAssetAtPath<AtuendoDeEscena>(ruta);
        if (atuendo != null)
        {
            PonerPrendasDeLaTabla(atuendo, partes, informe);
            return atuendo;
        }

        AsegurarCarpeta(CarpetaAtuendos);
        atuendo = ScriptableObject.CreateInstance<AtuendoDeEscena>();
        AssetDatabase.CreateAsset(atuendo, ruta);

        var so = new SerializedObject(atuendo);
        var lista = so.FindProperty("personajes");
        lista.arraySize = partes.Length;
        for (int i = 0; i < partes.Length; i++)
        {
            var p = partes[i];
            var e = lista.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("personaje").intValue = (int)p.Personaje;

            EscribirPrendas(e, p, atuendo.name, informe);

            e.FindPropertyRelative("tintes").arraySize = 0;

            var rasgos = e.FindPropertyRelative("rasgos");
            rasgos.FindPropertyRelative("planear").boolValue = p.Planear;
            rasgos.FindPropertyRelative("reboteExtra").floatValue = p.ReboteExtra;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(atuendo);
        informe.AppendLine($"Atuendo «{atuendo.name}» creado.");
        return atuendo;
    }

    /// <summary>
    /// En un atuendo que ya existe, la tabla manda en las prendas y las prendas sueltas de cada personaje
    /// que nombra (se quitan las que ya no están) y le quita los tintes; los rasgos no se tocan.
    /// </summary>
    private static void PonerPrendasDeLaTabla(AtuendoDeEscena atuendo, Parte[] partes, StringBuilder informe)
    {
        var so = new SerializedObject(atuendo);
        var lista = so.FindProperty("personajes");
        foreach (var p in partes)
        {
            SerializedProperty e = null;
            for (int i = 0; i < lista.arraySize && e == null; i++)
                if (lista.GetArrayElementAtIndex(i).FindPropertyRelative("personaje").intValue == (int)p.Personaje)
                    e = lista.GetArrayElementAtIndex(i);
            if (e == null) { informe.AppendLine($"Atuendo {atuendo.name}: no tiene parte de {p.Personaje}; se deja como está."); continue; }
            EscribirPrendas(e, p, atuendo.name, informe);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(atuendo);
    }

    private static void EscribirPrendas(SerializedProperty e, Parte p, string atuendo, StringBuilder informe)
    {
        var prendas = e.FindPropertyRelative("prendas");
        prendas.arraySize = 0;
        foreach (string id in p.Prendas)
        {
            var prenda = AssetDatabase.LoadAssetAtPath<WardrobeItemSO>(Prenda + id + ".asset");
            if (prenda == null) { informe.AppendLine($"Atuendo {atuendo}: no encuentro la prenda {id}."); continue; }
            prendas.arraySize++;
            prendas.GetArrayElementAtIndex(prendas.arraySize - 1).objectReferenceValue = prenda;
        }
        e.FindPropertyRelative("tintes").arraySize = 0;   // los disfraces van como prendas sueltas, con sus colores
        var otras = e.FindPropertyRelative("otrasPiezas");   // nombre vacío = quita esa categoría
        otras.arraySize = p.Quitar.Length;
        for (int i = 0; i < p.Quitar.Length; i++)
        {
            otras.GetArrayElementAtIndex(i).FindPropertyRelative("category").intValue = (int)p.Quitar[i];
            otras.GetArrayElementAtIndex(i).FindPropertyRelative("partName").stringValue = "";
        }
        var sueltas = e.FindPropertyRelative("prendasSueltas");
        sueltas.arraySize = 0;
        foreach (string ruta in p.Sueltas)
        {
            var prenda = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prenda == null) { informe.AppendLine($"Atuendo {atuendo}: no encuentro la prenda suelta {ruta}."); continue; }
            sueltas.arraySize++;
            sueltas.GetArrayElementAtIndex(sueltas.arraySize - 1).objectReferenceValue = prenda;
        }
    }

    private static TransformacionDeMagia AsegurarTransformacionDeMagia(StringBuilder informe)
    {
        var t = AssetDatabase.LoadAssetAtPath<TransformacionDeMagia>(RutaTransformacion);
        if (t != null) return t;
        AsegurarCarpeta(System.IO.Path.GetDirectoryName(RutaTransformacion).Replace('\\', '/'));
        t = ScriptableObject.CreateInstance<TransformacionDeMagia>();
        // Los valores de partida (destellos, corazones y confeti) son los de crearlo desde el menú Create.
        Unsupported.SmartReset(t);
        AssetDatabase.CreateAsset(t, RutaTransformacion);
        informe.AppendLine("Transformación de la magia creada (purpurina y corazoncitos, sin daño).");
        return t;
    }

    /// <summary>_WILL.prefab lleva <see cref="PlaneoDelJugador"/> junto a su vThirdPersonController.</summary>
    private static void AsegurarPlaneoEnWill(StringBuilder informe)
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaWill);
        try
        {
            var controlador = raiz.GetComponentInChildren<vThirdPersonController>(true);
            if (controlador == null) { informe.AppendLine("AVISO: _WILL.prefab no tiene vThirdPersonController; no se añade el planeo."); return; }
            if (controlador.GetComponent<PlaneoDelJugador>() != null) return;
            controlador.gameObject.AddComponent<PlaneoDelJugador>();
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaWill);
            informe.AppendLine("_WILL.prefab: añadido PlaneoDelJugador.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
