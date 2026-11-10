using System.Text;
using UnityEditor;
using UnityEngine;
using Pieza = AspectoDeProtagonista.Pieza;
using Ficha = AspectoDeProtagonista.Ficha;

/// <summary>
/// Los niños hechos en Blender (Tools/Modelos/Ninos/): cuerpo de niño propio en vez de encogerlos
/// (<see cref="CuerpoDeNino"/>, una talla por esqueleto), su pelo y su ropa como piezas del modelo modular
/// (<see cref="AspectoDeProtagonista.PonerPiezas"/>) y su juego de caras por emoción
/// (<see cref="CarasDeLosProtagonistas"/>). Cada niño es una entrada de datos. Se puede repetir.
/// </summary>
public static class NinosHechosEnBlender
{
    const string Carpeta = "Assets/Art/Characters/Ninos";
    /// <summary>Talla de 11-13 años.</summary>
    public const string EsqueletoMayor = Carpeta + "/Esqueleto_NinoMayor.fbx";
    /// <summary>Talla de 5-8 años.</summary>
    public const string EsqueletoPequeno = Carpeta + "/Esqueleto_NinoPequeno.fbx";

    sealed class Nino
    {
        public Ficha Ficha;          // pelo y ropa; PrefabPropio es su prefab
        public string Esqueleto;     // su talla
        public Color Iris;
        public int Ojos, Bocas;      // su juego de caras: Eye<Ojos>…, Mouth<Bocas>… en Caras_<Nombre>.fbx
        public float BrazosAbiertos; // 0 = como el pack; ver CuerpoDeNino.AbrirLosBrazos
    }

    static Nino[] Ninos => new[]
    {
        // Tobías, el hermano pequeño de Liam (12 años): castaño, pelo revuelto, túnica de estar en casa en tonos tierra
        new Nino
        {
            Esqueleto = EsqueletoMayor, Iris = new Color(0.35f, 0.2f, 0.1f), Ojos = 60, Bocas = 60,
            Ficha = new Ficha
            {
                Nombre = "Tobias", Carpeta = Carpeta + "/Tobias",
                PrefabPropio = "Assets/_NPCs/_GrafoNarrativo/Tobi.prefab", CuerpoDeAntes = "Body03",
                Modelos = new[] { "Assets/_NPCs/_GrafoNarrativo/Tobi.prefab" }, Propios = new[] { "Assets/_NPCs/_GrafoNarrativo/Tobi.prefab" },
                Piezas = new[]
                {
                    new Pieza { Nombre = "Hair17_Tobias", Categoria = PartCategory.Hair, Vecina = "Hair02" },
                    new Pieza { Nombre = "Body24_Tobias", Categoria = PartCategory.Body, Vecina = "Body03" },
                },
                Apagar = new[] { "Arrow01" },
                Colores = new[]
                {
                    ("Pelo", new Color(0.3f, 0.16f, 0.08f)), ("PeloClaro", new Color(0.43f, 0.25f, 0.12f)),
                    ("Tunica", new Color(0.78f, 0.6f, 0.36f)), ("TunicaOscura", new Color(0.55f, 0.38f, 0.2f)), ("Remiendo", new Color(0.62f, 0.5f, 0.36f)),
                    ("Cinto", new Color(0.6f, 0.27f, 0.17f)), ("Cordon", new Color(0.38f, 0.24f, 0.13f)), ("Pantalon", new Color(0.38f, 0.27f, 0.18f)),
                    ("Zapatilla", new Color(0.3f, 0.21f, 0.15f)), ("Suela", new Color(0.18f, 0.12f, 0.08f)),
                },
            },
        },
        // Nina, la hija de Liora y el Archimago (niña pequeña, el prólogo): coletas cobrizas con lazos y vestido de aldea
        new Nino
        {
            Esqueleto = EsqueletoPequeno, Iris = new Color(0.22f, 0.42f, 0.72f), Ojos = 70, Bocas = 70, BrazosAbiertos = 0.35f,
            Ficha = new Ficha
            {
                Nombre = "Nina", Carpeta = Carpeta + "/Nina",
                PrefabPropio = "Assets/_NPCs/NonInteractable/Nina_Prologo.prefab", CuerpoDeAntes = "Body04",
                Modelos = new[] { "Assets/_NPCs/NonInteractable/Nina_Prologo.prefab" }, Propios = new[] { "Assets/_NPCs/NonInteractable/Nina_Prologo.prefab" },
                Piezas = new[]
                {
                    new Pieza { Nombre = "Hair18_Coletas", Categoria = PartCategory.Hair, Vecina = "Hair08" },
                    new Pieza { Nombre = "Body25_Vestido", Categoria = PartCategory.Body, Vecina = "Body04" },
                },
                Colores = new[]
                {
                    ("Pelo", new Color(0.56f, 0.26f, 0.11f)), ("Lazo", new Color(0.9f, 0.32f, 0.4f)),
                    ("Vestido", new Color(0.86f, 0.47f, 0.42f)), ("VestidoOscuro", new Color(0.66f, 0.3f, 0.28f)), ("Blusa", new Color(0.97f, 0.93f, 0.84f)),
                    ("Delantal", new Color(0.96f, 0.92f, 0.82f)), ("Piel", new Color(1f, 0.82f, 0.7f)), ("Media", new Color(0.97f, 0.97f, 0.95f)),
                    ("Zapato", new Color(0.42f, 0.24f, 0.14f)), ("Suela", new Color(0.2f, 0.12f, 0.08f)),
                },
            },
        },
        // Lucas, el hijo pequeño que va de la mano de su madre a por el pan en el prólogo (talla de Nina):
        // pelo y ropa de crío del pack (Hair02, Body03), que ya se ajustan a su cuerpo de niño. Ver RecursosDelPrologo.
        new Nino
        {
            Esqueleto = EsqueletoPequeno, Iris = new Color(0.3f, 0.5f, 0.75f), Ojos = 70, Bocas = 70,
            Ficha = new Ficha
            {
                Nombre = "Lucas", Carpeta = Carpeta + "/Lucas",
                PrefabPropio = "Assets/_NPCs/NonInteractable/LucasDelValle.prefab", CuerpoDeAntes = "Body03",
                Modelos = new[] { "Assets/_NPCs/NonInteractable/LucasDelValle.prefab" }, Propios = new[] { "Assets/_NPCs/NonInteractable/LucasDelValle.prefab" },
                Piezas = new Pieza[0],
                Colores = new (string, Color)[0],
            },
        },
        // Lucía, la niña de la posada de la aldea del mar (talla de Nina): coletas y ropa de diario del pack
        // (Hair08, Body04), que se ajustan a su cuerpo de niña. Prefab de PersonajesConAspectoFijo.
        new Nino
        {
            Esqueleto = EsqueletoPequeno, Iris = new Color(0.36f, 0.52f, 0.3f), Ojos = 70, Bocas = 70, BrazosAbiertos = 0.35f,
            Ficha = new Ficha
            {
                Nombre = "Lucia", Carpeta = Carpeta + "/Lucia",
                PrefabPropio = "Assets/_NPCs/AldeaDelMar/Lucia.prefab", CuerpoDeAntes = "Body04",
                Modelos = new[] { "Assets/_NPCs/AldeaDelMar/Lucia.prefab" }, Propios = new[] { "Assets/_NPCs/AldeaDelMar/Lucia.prefab" },
                Piezas = new Pieza[0],
                Colores = new (string, Color)[0],
            },
        },
    };

    [MenuItem("El Sendero/Personajes/Niños hechos en Blender (Tobías, Nina, Lucas, Lucía)")]
    public static void Aplicar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Niños", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Aplicar(informe);
        AssetDatabase.SaveAssets();
        Debug.Log("[NinosHechosEnBlender] " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar("Niños hechos en Blender", informe.ToString());   // desde el buzón va al registro (un diálogo lo bloquearía)
    }

    public static void Aplicar(StringBuilder informe)
    {
        string estiloCaras = CarasDeLosProtagonistas.RutaDeEstilo();
        foreach (var n in Ninos)
        {
            var f = n.Ficha;
            string prefab = f.PrefabPropio;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null) { informe.AppendLine($"{f.Nombre}: no encuentro {prefab}."); continue; }

            // 1 · El cuerpo de niño (el esqueleto con sus proporciones y su Avatar)
            // preserveHierarchy: el FBX de Blender trae el objeto «Armature» girado -90° en X (de Z arriba a Y arriba).
            // Si Unity lo funde con la raíz del asset, ese giro se queda en la raíz y, medido en su espacio, el
            // esqueleto sale tumbado (la cabeza a altura ~0). Como hijo, la raíz queda limpia y el esqueleto de pie.
            if (AssetImporter.GetAtPath(n.Esqueleto) is ModelImporter esq
                && (esq.importAnimation || esq.avatarSetup != ModelImporterAvatarSetup.NoAvatar || !esq.preserveHierarchy))
            {
                esq.animationType = ModelImporterAnimationType.Generic;
                esq.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                esq.importAnimation = false;
                esq.preserveHierarchy = true;
                esq.SaveAndReimport();
            }
            bool ok;
            var raiz = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                ok = CuerpoDeNino.Poner(raiz, n.Esqueleto, $"{f.Carpeta}/Avatar_{f.Nombre}.asset", informe, n.BrazosAbiertos);
                // Las cejas van en las caras nuevas: fuera las del pelo del pack, que movía CejasPorEmocion
                foreach (var c in raiz.GetComponentsInChildren<CejasPorEmocion>(true)) Object.DestroyImmediate(c, true);
                if (ok) PrefabUtility.SaveAsPrefabAsset(raiz, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
            if (!ok) continue;

            // 2 · Pelo y ropa, hechos sobre su talla
            if (!AspectoDeProtagonista.PonerPiezas(f, informe)) continue;

            // 3 · Su juego de caras y su perfil de emociones, con la cara neutra puesta
            var piezas = CarasDeLosProtagonistas.PrepararFbx($"{f.Carpeta}/Caras_{f.Nombre}.fbx", n.Iris, estiloCaras, informe);
            var perfil = CarasDeLosProtagonistas.Perfil(f.Nombre, n.Ojos, n.Bocas, informe);
            if (perfil == null || piezas.Count == 0) continue;
            raiz = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                int puestas = 0;
                foreach (var (modelo, nombre) in piezas)
                    if (PiezaNuevaDelModelo.Anadir(raiz, modelo, nombre, nombre.StartsWith("Mouth") ? "Mouth02|Mouth09|Mouth01" : "Eye01|Eye09|Eye02", new StringBuilder())) puestas++;
                CarasDeLosProtagonistas.PonerCara(raiz, perfil, n.Ojos, n.Bocas);
                PrefabUtility.SaveAsPrefabAsset(raiz, prefab);
                informe.AppendLine($"{f.Nombre}: {puestas} piezas de cara, cara neutra Eye{n.Ojos} + Mouth{n.Bocas}.");
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }
    }
}
