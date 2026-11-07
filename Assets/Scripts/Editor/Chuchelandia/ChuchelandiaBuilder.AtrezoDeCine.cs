using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// El atrezo de la cinemática de cine «¡TE QUIERO!» (Chuche_06_TeQuiero), en «Salón del duelo»:
/// - La fuente de sirope: el pilón redondo del parque (fountain_004) con una lámina de sirope dentro
///   (el shader de agua del pack, en rosa fresa: Mat_Sirope).
/// - PROP_Pancarta: el mantel con «¡ESTELA, TE QUIERO VER GANAR!» entre las dos sillas.
/// - PROP_MantelNovios: el mantel enrollado a los dos como una sábana, con «¡TE QUIERO!» chorreando.
/// - PROP_Velo (la punta del mantel en la cabeza de Liam), PROP_CorazonDeLiam (en la frente de Will) y
///   PROP_LazoDeEstela (el lazo torcido): el guion se los pone con «coge … mano=cabeza». Su pose dentro
///   del hueso de la cabeza se mide en el prefab de cada personaje, así que no hay números a mano.
/// - Los atuendos que les quitan el corazón a Liam y el lazo a Estela mientras dura (AtuendoBeat) y la
///   salpicadura de sirope (SalpicaduraDeAgua teñida, para la orden «vfx»).
/// Los textos del mundo se traducen con LocalizedText. Todo empieza apagado; lo enciende el guion.
/// Las sillas y la fuente son las del salón (<see cref="AtrezoDelSalon"/>), dadas de alta como props.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string PilonDeLaFuente = "Assets/Art/World/ithappy/Amusement_Park/Prefabs/fountain_004.prefab";
    private const float DiametroDeLaFuente = 3f;
    /// <summary>Altura de la lámina de sirope sobre el suelo (el borde del pilón queda a unos 0,42 m) y su radio.</summary>
    private const float AlturaDelSirope = 0.3f, RadioDelSirope = 1.25f;

    private const string CarpetaAtrezoCine = "Assets/_NPCs/Chuchelandia/Atrezo";
    private const string AguaDelPack = Pack + "/Materials/Water.mat";
    private const string RutaMatSirope = CarpetaAtrezoCine + "/Mat_Sirope.mat";
    private const string RutaMatMantel = CarpetaAtrezoCine + "/Mat_Mantel.mat";
    private const string RutaMatGotas = CarpetaAtrezoCine + "/Mat_GotasDeSirope.mat";
    private const string RutaSalpicadura = CarpetaAtrezoCine + "/VFX_SalpicaduraDeSirope.prefab";
    private const string RutaNovios = CarpetaAtuendos + "/Atuendo_Chuchelandia_Novios.asset";
    private const string RutaLazoTorcido = CarpetaAtuendos + "/Atuendo_Chuchelandia_LazoTorcido.asset";
    private const string RutaPrefabEstela = "Assets/Prefabs/_ESTELA.prefab";
    private const string RutaPrefabLiam = "Assets/Prefabs/_LIAM.prefab";
    private const string AtrezoTeQuiero = "Atrezo de «¡TE QUIERO!»";

    private static readonly Color ColorSirope = new Color(0.86f, 0.16f, 0.36f);
    private static readonly Color ColorMantel = new Color(0.98f, 0.95f, 0.9f);

    // ── Tabla de atrezo de las cinemáticas ──────────────────────────────────────────────────
    // Guion, id del prop en su SequenceStage, objeto (ruta bajo «Chuchelandia (generado)») y altura,
    // sobre su pivote, a la que apuntan los planos que lo nombran.
    private static readonly (string guion, string id, string ruta, float altura)[] AtrezoDeLasCinematicas =
    {
        ("Chuche_06_TeQuiero", "PROP_Fuente", "Salón del duelo/Fuente de sirope", 0.4f),
        ("Chuche_06_TeQuiero", "PROP_SillaWill", "Salón del duelo/Silla de Will", 0.3f),
        ("Chuche_06_TeQuiero", "PROP_SillaLiam", "Salón del duelo/Silla de Liam", 0.3f),
        ("Chuche_06_TeQuiero", "PROP_Pancarta", "Salón del duelo/" + AtrezoTeQuiero + "/PROP_Pancarta", AlturaDeLaPancarta),
        ("Chuche_06_TeQuiero", "PROP_MantelNovios", "Salón del duelo/" + AtrezoTeQuiero + "/PROP_MantelNovios", 0.7f),
        ("Chuche_06_TeQuiero", "PROP_Velo", "Salón del duelo/" + AtrezoTeQuiero + "/PROP_Velo", 0f),
        ("Chuche_06_TeQuiero", "PROP_CorazonDeLiam", "Salón del duelo/" + AtrezoTeQuiero + "/PROP_CorazonDeLiam", 0f),
        ("Chuche_06_TeQuiero", "PROP_LazoDeEstela", "Salón del duelo/" + AtrezoTeQuiero + "/PROP_LazoDeEstela", 0f),
    };

    /// <summary>Largo de la trompetilla que el heraldo lleva siempre en la mano (a la escala de su mano).</summary>
    private const float TamanoDeLaTrompetillaDelHeraldo = 0.55f;

    /// <summary>Centro de la pancarta sobre el suelo: a la altura de las manos en alto de Will y Liam subidos a las sillas.</summary>
    private const float AlturaDeLaPancarta = 1.72f;

    /// <summary>Los props de esa cinemática (tabla <see cref="AtrezoDeLasCinematicas"/>) que hay en la escena.</summary>
    private static List<(string id, Transform objeto, float altura)> AtrezoDe(Cine c, Transform raiz, StringBuilder informe)
    {
        var lista = new List<(string, Transform, float)>();
        foreach (var (guion, id, ruta, altura) in AtrezoDeLasCinematicas)
        {
            if (guion != c.Guion) continue;
            var objeto = raiz.Find(ruta);
            if (objeto != null) lista.Add((id, objeto, altura));
            else informe.AppendLine($"Cinemática {c.Guion}: no encuentro «{ruta}» para {id}.");
        }
        return lista;
    }

    // ── Montaje ─────────────────────────────────────────────────────────────────────────────

    private static void MontarAtrezoDeTeQuiero(Transform salon, Dictionary<string, Transform> marcas, Dictionary<string, GameObject> atrezo, StringBuilder informe)
    {
        AsegurarCarpeta(CarpetaAtrezoCine);
        var mantel = MaterialLiso(RutaMatMantel, ColorMantel, 0.15f);
        var gotas = MaterialLiso(RutaMatGotas, ColorSirope, 0.85f);
        if (atrezo.TryGetValue("Fuente de sirope", out var fuente)) PonerElSirope(fuente);
        else informe.AppendLine("«¡TE QUIERO!»: falta la fuente del salón; sin sirope.");

        var padre = Hijo(salon, AtrezoTeQuiero);
        int hechos = 0;
        if (marcas.TryGetValue("M_Cine_Salon_TeQuiero_Will", out var sillaW) && marcas.TryGetValue("M_Cine_Salon_TeQuiero_Liam", out var sillaL)
            && marcas.TryGetValue("M_Duelo_Fuente", out var centroFuente))
        {
            Pancarta(padre, sillaW, sillaL, centroFuente, mantel, gotas);
            hechos++;
        }
        if (marcas.TryGetValue("M_Cine_Salon_Novios_Will", out var noviaW) && marcas.TryGetValue("M_Cine_Salon_Novios_Liam", out var noviaL))
        {
            MantelDeLosNovios(padre, noviaW, noviaL, mantel, gotas);
            hechos++;
        }
        Vector3 aparte = padre.position;
        if (EnLaCabeza(padre, "PROP_Velo", RutaPrefabLiam, aparte, informe, (copia, cabeza, prop) => Velo(cabeza, prop, mantel))) hechos++;
        if (EnLaCabeza(padre, "PROP_CorazonDeLiam", RutaWill, aparte, informe, (copia, cabeza, prop) => CorazonEnLaFrente(copia, cabeza, prop))) hechos++;
        if (EnLaCabeza(padre, "PROP_LazoDeEstela", RutaPrefabEstela, aparte, informe, (copia, cabeza, prop) => LazoTorcido(copia, cabeza, prop))) hechos++;
        foreach (Transform hijo in padre) hijo.gameObject.SetActive(false);

        AtuendoSinAccesorio(RutaNovios, Slot.Liam, informe);
        AtuendoSinAccesorio(RutaLazoTorcido, Slot.Estela, informe);
        SalpicaduraDeSirope();
        informe.AppendLine($"«¡TE QUIERO!»: fuente de sirope, {hechos} de 5 piezas de atrezo de la cinemática (apagadas hasta que el guion las saca), " +
                           "atuendos Novios y LazoTorcido y la salpicadura de sirope.");
    }

    /// <summary>La lámina de sirope dentro del pilón: un disco con el agua del pack teñida de fresa.</summary>
    private static void PonerElSirope(GameObject fuente)
    {
        var caja = BoundsDe(fuente);
        var disco = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disco.name = "Sirope";
        Object.DestroyImmediate(disco.GetComponent<Collider>());
        disco.transform.position = new Vector3(caja.center.x, caja.min.y + AlturaDelSirope, caja.center.z);
        disco.transform.localScale = new Vector3(RadioDelSirope * 2f, 0.01f, RadioDelSirope * 2f);
        disco.transform.SetParent(fuente.transform, true);
        disco.GetComponent<MeshRenderer>().sharedMaterial = MaterialDeSirope();
    }

    /// <summary>
    /// El mantel con la pancarta entre las dos sillas, un poco hacia la fuente (las cabezas quedan delante).
    /// Mira al centro como la fuente: los vuelcos van en sus ejes (PropMoveBeat enSusEjes).
    /// </summary>
    private static void Pancarta(Transform padre, Transform sillaW, Transform sillaL, Transform fuente, Material mantel, Material gotas)
    {
        Vector3 medio = (sillaW.position + sillaL.position) * 0.5f;
        var raiz = new GameObject("PROP_Pancarta").transform;
        raiz.SetParent(padre, false);
        raiz.SetPositionAndRotation(medio + EnElMarcoDe(fuente, V(0f, 0f, -0.12f)), Quaternion.Euler(0f, fuente.eulerAngles.y, 0f));
        float ancho = Mathf.Max(0.8f, Vector3.Distance(sillaW.position, sillaL.position) - 0.5f);
        Caja(raiz, "Tela", V(0f, AlturaDeLaPancarta, 0f), V(ancho, 0.5f, 0.02f), Vector3.zero, mantel);
        Letras(raiz, "CHUCHELANDIA_TEQUIERO_PANCARTA_ENTERA", "¡ESTELA, TE QUIERO VER GANAR!",
               V(0f, AlturaDeLaPancarta, 0.012f), new Vector2(ancho - 0.08f, 0.44f));
        Goteras(raiz, gotas, AlturaDeLaPancarta - 0.2f, 0.013f, ancho - 0.2f, 5, 0.05f);
    }

    /// <summary>
    /// El mantel enrollado a los dos (de la cintura a los hombros, por encima del sirope), con «¡TE QUIERO!»
    /// en letras enormes que chorrean. Sale con ellos de la fuente (el guion lo sube con PropMoveBeat).
    /// </summary>
    private static void MantelDeLosNovios(Transform padre, Transform noviaW, Transform noviaL, Material mantel, Material gotas)
    {
        var raiz = new GameObject("PROP_MantelNovios").transform;
        raiz.SetParent(padre, false);
        raiz.SetPositionAndRotation((noviaW.position + noviaL.position) * 0.5f, Quaternion.Euler(0f, noviaW.eulerAngles.y, 0f));
        float ancho = Vector3.Distance(noviaW.position, noviaL.position) + 0.7f;
        Caja(raiz, "Sábana", V(0f, 0.62f, 0f), V(ancho, 0.62f, 0.62f), Vector3.zero, mantel);
        Caja(raiz, "Vuelta", V(-ancho * 0.18f, 0.85f, 0.06f), V(ancho * 0.7f, 0.12f, 0.66f), V(0f, 0f, 8f), mantel);
        Letras(raiz, "CHUCHELANDIA_TEQUIERO_PANCARTA", "¡TE QUIERO!", V(0f, 0.6f, 0.318f), new Vector2(ancho - 0.1f, 0.4f));
        Goteras(raiz, gotas, 0.4f, 0.32f, ancho - 0.3f, 7, 0.09f);
    }

    /// <summary>La punta del mantel sobre la cabeza de Liam, como un velo de novia: encima y cayendo por detrás.</summary>
    private static void Velo(Bounds cabeza, Transform prop, Material mantel)
    {
        float arriba = cabeza.max.y, atras = cabeza.min.z, fondo = cabeza.size.z;
        CajaEnElMundo(prop, "Encima", new Vector3(cabeza.center.x, arriba - 0.01f, cabeza.center.z - 0.05f),
                      new Vector3(cabeza.size.x * 0.95f, 0.03f, fondo * 0.8f), Quaternion.Euler(-12f, 0f, 0f), mantel);
        CajaEnElMundo(prop, "Caída", new Vector3(cabeza.center.x, arriba - 0.3f, atras - 0.02f),
                      new Vector3(cabeza.size.x * 0.9f, 0.6f, 0.03f), Quaternion.Euler(8f, 0f, 0f), mantel);
    }

    /// <summary>El corazón de Liam (la pieza AC01_Heart del modelo) pegado en la frente de Will.</summary>
    private static void CorazonEnLaFrente(Transform copia, Bounds cabeza, Transform prop)
    {
        var pieza = PiezaDelModelo(copia, "AC01_Heart");
        var cara = CajaDePiezas(copia, "Head");
        if (pieza == null || cara == null) return;
        var corazon = CopiaDePieza(pieza, prop, "Corazón");
        corazon.localScale *= 0.8f;
        var c = BoundsDe(corazon.gameObject);
        var frente = new Vector3(cara.Value.center.x, cara.Value.center.y + cara.Value.size.y * 0.12f, cara.Value.max.z + 0.02f);
        corazon.position += frente - c.center;
    }

    /// <summary>El lazo de Estela (la pieza AC09_Ribbon que lleva puesta) caído hacia un lado.</summary>
    private static void LazoTorcido(Transform copia, Bounds cabeza, Transform prop)
    {
        var pieza = PiezaDelModelo(copia, "AC09_Ribbon");
        if (pieza == null) return;
        var lazo = CopiaDePieza(pieza, prop, "Lazo");
        lazo.RotateAround(cabeza.center, Vector3.forward, -32f);
        lazo.position += new Vector3(0.05f, -0.06f, 0f);
    }

    // ── Piezas en la cabeza ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hace el prop <paramref name="nombre"/> para ponerlo en la cabeza con «coge … mano=cabeza»: instancia el
    /// personaje un momento en el origen (mirando a +Z), coloca el prop en el hueso de su cabeza, deja que
    /// <paramref name="vestir"/> ponga las piezas en coordenadas del mundo y las cuelga del prop, que así
    /// guarda su pose relativa al hueso. Después el prop se aparca en <paramref name="aparte"/>.
    /// </summary>
    private static bool EnLaCabeza(Transform padre, string nombre, string rutaPersonaje, Vector3 aparte, StringBuilder informe,
                                   System.Action<Transform, Bounds, Transform> vestir)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPersonaje);
        if (modelo == null) { informe.AppendLine($"{nombre}: no encuentro {rutaPersonaje}."); return false; }
        var copia = (GameObject)Object.Instantiate(modelo, Vector3.zero, Quaternion.identity);
        try
        {
            var animador = copia.GetComponentInChildren<Animator>(true);
            var hueso = animador != null && animador.isHuman ? animador.GetBoneTransform(HumanBodyBones.Head) : null;
            var cabeza = CajaDePiezas(copia.transform, "Head", "Hair");
            if (hueso == null || cabeza == null) { informe.AppendLine($"{nombre}: {modelo.name} no tiene cabeza humanoide medible."); return false; }

            var prop = new GameObject(nombre).transform;
            prop.SetPositionAndRotation(hueso.position, hueso.rotation);
            var piezas = new GameObject("piezas").transform;
            vestir(copia.transform, cabeza.Value, piezas);
            if (piezas.childCount == 0) { Object.DestroyImmediate(prop.gameObject); Object.DestroyImmediate(piezas.gameObject); informe.AppendLine($"{nombre}: sin piezas."); return false; }
            while (piezas.childCount > 0) piezas.GetChild(0).SetParent(prop, true);
            Object.DestroyImmediate(piezas.gameObject);

            prop.SetParent(padre, true);
            prop.position = aparte;
            return true;
        }
        finally
        {
            Object.DestroyImmediate(copia);
        }
    }

    /// <summary>La pieza del modelo con ese nombre (encendida para medirla).</summary>
    private static Renderer PiezaDelModelo(Transform copia, string nombre)
    {
        foreach (var r in copia.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name == nombre) { r.gameObject.SetActive(true); return r; }
        return null;
    }

    /// <summary>Una copia suelta (malla y materiales) de una pieza del modelo, con su misma pose en el mundo.</summary>
    private static Transform CopiaDePieza(Renderer pieza, Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        go.AddComponent<MeshFilter>().sharedMesh = pieza.GetComponent<MeshFilter>().sharedMesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = pieza.sharedMaterials;
        go.transform.SetPositionAndRotation(pieza.transform.position, pieza.transform.rotation);
        go.transform.localScale = pieza.transform.lossyScale;
        go.transform.SetParent(padre, true);
        return go.transform;
    }

    /// <summary>Caja que envuelve las piezas encendidas del modelo cuyo nombre empieza por alguno de los prefijos.</summary>
    private static Bounds? CajaDePiezas(Transform copia, params string[] prefijos)
    {
        Bounds? caja = null;
        foreach (var r in copia.GetComponentsInChildren<Renderer>(false))
        {
            if (!System.Array.Exists(prefijos, p => r.name.StartsWith(p))) continue;
            if (caja == null) caja = r.bounds;
            else { var b = caja.Value; b.Encapsulate(r.bounds); caja = b; }
        }
        return caja;
    }

    // ── Piezas sueltas ──────────────────────────────────────────────────────────────────────

    private static void Caja(Transform padre, string nombre, Vector3 local, Vector3 tamano, Vector3 giro, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(giro);
        go.transform.localScale = tamano;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void CajaEnElMundo(Transform padre, string nombre, Vector3 pos, Vector3 tamano, Quaternion giro, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(pos, giro);
        go.transform.localScale = tamano;
        go.transform.SetParent(padre, true);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>
    /// Texto del mundo en la cara de delante (+Z) de <paramref name="padre"/>, legible desde delante,
    /// traducido con <see cref="LocalizedText"/> (el texto escrito es el del Editor y el storyboard).
    /// </summary>
    private static void Letras(Transform padre, string clave, string texto, Vector3 local, Vector2 tamano)
    {
        var go = new GameObject("Letras: " + texto);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = texto;
        tmp.rectTransform.sizeDelta = tamano;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.2f;
        tmp.fontSizeMax = 6f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.color = ColorSirope;
        go.AddComponent<LocalizedText>().key = clave;
    }

    /// <summary>Gotas de sirope que cuelgan de las letras, delante de la tela.</summary>
    private static void Goteras(Transform padre, Material gotas, float altura, float delante, float ancho, int cuantas, float largo)
    {
        for (int i = 0; i < cuantas; i++)
        {
            float x = Mathf.Lerp(-ancho * 0.5f, ancho * 0.5f, (i + 0.5f) / cuantas) + (i % 2 == 0 ? 0.03f : -0.02f);
            float l = largo * (0.6f + 0.4f * ((i * 7) % 5) / 4f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Gota";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(padre, false);
            go.transform.localPosition = V(x, altura - l * 0.5f, delante);
            go.transform.localScale = V(0.035f, l * 0.5f, 0.02f);
            go.GetComponent<MeshRenderer>().sharedMaterial = gotas;
        }
    }

    // ── Materiales, atuendos y VFX ──────────────────────────────────────────────────────────

    /// <summary>Material liso de URP (sin textura): para las telas y las gotas. Se crea una vez; se retoca en su Inspector.</summary>
    private static Material MaterialLiso(string ruta, Color color, float brillo)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", brillo);
        AssetDatabase.CreateAsset(mat, ruta);
        return mat;
    }

    /// <summary>El agua del pack teñida de sirope de fresa (los colores del shader de agua). Se crea una vez; se retoca en su Inspector.</summary>
    private static Material MaterialDeSirope()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMatSirope);
        if (mat != null) return mat;
        var agua = AssetDatabase.LoadAssetAtPath<Material>(AguaDelPack);
        mat = agua != null ? new Material(agua) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        foreach (var (propiedad, color) in new[]
                 {
                     ("_ColorDeep", new Color(0.55f, 0.05f, 0.2f)), ("_ColorShallow", new Color(0.9f, 0.28f, 0.45f)),
                     ("_ColorSurface", new Color(0.95f, 0.42f, 0.56f)), ("_ColorAmbient", new Color(0.97f, 0.55f, 0.66f)),
                     ("_ColorFoam", new Color(1f, 0.86f, 0.9f)), ("_BaseColor", ColorSirope),
                 })
            if (mat.HasProperty(propiedad)) mat.SetColor(propiedad, color);
        AssetDatabase.CreateAsset(mat, RutaMatSirope);
        return mat;
    }

    /// <summary>Atuendo de escena que quita el accesorio de un personaje (lo pone y lo quita el guion con AtuendoBeat).</summary>
    private static void AtuendoSinAccesorio(string ruta, Slot personaje, StringBuilder informe)
    {
        var atuendo = AssetDatabase.LoadAssetAtPath<AtuendoDeEscena>(ruta);
        if (atuendo == null)
        {
            atuendo = ScriptableObject.CreateInstance<AtuendoDeEscena>();
            AssetDatabase.CreateAsset(atuendo, ruta);
            informe.AppendLine($"Atuendo «{atuendo.name}» creado.");
        }
        var so = new SerializedObject(atuendo);
        var lista = so.FindProperty("personajes");
        lista.arraySize = 1;
        var parte = lista.GetArrayElementAtIndex(0);
        parte.FindPropertyRelative("personaje").intValue = (int)personaje;
        parte.FindPropertyRelative("prendas").arraySize = 0;
        parte.FindPropertyRelative("tintes").arraySize = 0;
        var otras = parte.FindPropertyRelative("otrasPiezas");
        otras.arraySize = 1;
        otras.GetArrayElementAtIndex(0).FindPropertyRelative("category").intValue = (int)PartCategory.Accessory;
        otras.GetArrayElementAtIndex(0).FindPropertyRelative("partName").stringValue = "";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(atuendo);
    }

    /// <summary>La salpicadura de sirope: <see cref="SalpicaduraDeAgua"/> con los colores del sirope, que salpica al activarse.</summary>
    private static void SalpicaduraDeSirope()
    {
        var go = new GameObject("VFX_SalpicaduraDeSirope");
        try
        {
            var salpicadura = go.AddComponent<SalpicaduraDeAgua>();
            Fijar(salpicadura, ("colorGotas", new Color(ColorSirope.r, ColorSirope.g, ColorSirope.b, 0.95f)),
                  ("colorOndas", new Color(1f, 0.7f, 0.8f, 0.85f)), ("colorBurbujas", new Color(1f, 0.6f, 0.72f, 0.85f)),
                  ("salpicarAlActivarse", 1f));
            PrefabUtility.SaveAsPrefabAsset(go, RutaSalpicadura);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
