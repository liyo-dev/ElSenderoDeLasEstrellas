using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Zona de enemigos del mundo del LAB (sureste, al sur de las Pruebas): una estación por enemigo
/// nuevo de INC-722, cada una con su rótulo de «qué pide»:
/// <list type="bullet">
/// <item>Fantasmas: dos que salen siempre y uno solo de noche (pestaña Clima).</item>
/// <item>Mímicos: uno guarda un tesoro (se abre al vencerlo) y otro suelto que reaparece.</item>
/// <item>Imps ladrones con una placa de +100 Esencia para tener algo que robar.</item>
/// <item>Beholder en un pedestal.</item>
/// <item>Roca detrás de una fila de muros rompibles.</item>
/// </list>
/// Suelo y muros con su propio NavMesh. Destino en el viaje rápido. Se rehace sola al regenerar
/// el LAB o con su menú. Ver INC-722.
public static partial class CombatLabBuilder
{
    private const string RaizBestiario = "LAB_ENEMIGOS_DEL_MUNDO (fantasma, beholder, mímico, imp, roca)";
    private const string NombreDestinoBestiario = "Enemigos del mundo (sureste)";
    private static readonly Vector3 CentroBestiario = new Vector3(90f, 0f, -95f);
    private const float AnchoBestiario = 80f;
    private const float FondoBestiario = 60f;
    private static readonly Vector3 EntradaBestiario = new Vector3(90f, 0.1f, -70f);

    [MenuItem("El Sendero/Combate/CombatLab: montar enemigos del mundo")]
    public static void MontarBestiario()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("CombatLab", "Sal de Play para montar la zona.", "Aceptar");
            return;
        }
        if (!File.Exists(ScenePath))
        {
            EditorUtility.DisplayDialog("CombatLab", "No existe el LAB. Créalo antes con «Crear o regenerar el LAB».", "Aceptar");
            return;
        }

        var escena = SceneManager.GetSceneByPath(ScenePath);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        var avisos = CrearZonaBestiario(escena);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);

        EditorUtility.DisplayDialog("CombatLab", avisos.Count == 0
            ? "Zona de enemigos del mundo montada (viaje rápido: «" + NombreDestinoBestiario + "»)."
            : "Zona montada con avisos:\n- " + string.Join("\n- ", avisos), "Aceptar");
    }

    /// Crea (o rehace) la zona dentro de la escena indicada. Crea los prefabs si aún no existen.
    private static List<string> CrearZonaBestiario(Scene escena)
    {
        var avisos = new List<string>();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemigosDelMundoBuilder.RutaRoca) == null)
            avisos.AddRange(EnemigosDelMundoBuilder.CrearPrefabs());

        foreach (var raizExistente in escena.GetRootGameObjects())
            if (raizExistente.name == RaizBestiario) Object.DestroyImmediate(raizExistente);
        var raiz = new GameObject(RaizBestiario);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        var r = raiz.transform;

        // Suelo y muros, con su propio NavMesh (el mímico anda por él).
        var geometria = new GameObject("Geometría (con NavMesh)");
        geometria.transform.SetParent(r);
        var suelo = GetMaterial("Lab_Suelo", new Color(0.19f, 0.22f, 0.28f));
        var muro = GetMaterial("Lab_Cobertura", new Color(0.35f, 0.40f, 0.47f));
        var plataforma = GetMaterial("Lab_Plataforma", new Color(0.85f, 0.55f, 0.20f));
        Vector3 c = CentroBestiario;
        float mx = AnchoBestiario * 0.5f, mz = FondoBestiario * 0.5f;
        var g = geometria.transform;
        CrearCubo("Suelo", g, c + new Vector3(0f, -0.25f, 0f), new Vector3(AnchoBestiario, 0.5f, FondoBestiario), suelo, CapaSuelo);
        CrearCubo("Muro norte", g, c + new Vector3(0f, 1.5f, mz), new Vector3(AnchoBestiario, 3f, 0.5f), muro);
        CrearCubo("Muro sur", g, c + new Vector3(0f, 1.5f, -mz), new Vector3(AnchoBestiario, 3f, 0.5f), muro);
        CrearCubo("Muro este", g, c + new Vector3(mx, 1.5f, 0f), new Vector3(0.5f, 3f, FondoBestiario), muro);
        CrearCubo("Muro oeste", g, c + new Vector3(-mx, 1.5f, 0f), new Vector3(0.5f, 3f, FondoBestiario), muro);
        Pilar("Pedestal del Beholder", g, new Vector3(122f, 0f, -100f), new Vector2(3f, 3f), 1f, plataforma);
        var navMesh = geometria.AddComponent<NavMeshSurface>();
        navMesh.collectObjects = CollectObjects.Children;
        navMesh.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        navMesh.BuildNavMesh();

        CrearTexto("Enemigos del mundo: cada uno pide una respuesta", r, EntradaBestiario + new Vector3(0f, 3.6f, -3f), 180f);
        CrearVueltaAlLaboratorio(r, new Vector3(94f, 0f, -70f), EntradaBestiario);

        // ── Fantasmas ──
        var fantasma = AssetDatabase.LoadAssetAtPath<GameObject>(EnemigosDelMundoBuilder.RutaFantasma);
        if (fantasma != null)
        {
            EnemigoDelLab(fantasma, "Fantasma (siempre) 1", r, new Vector3(58f, 0f, -92f), 0f, ("soloDeNoche", false));
            EnemigoDelLab(fantasma, "Fantasma (siempre) 2", r, new Vector3(64f, 0f, -95f), 0f, ("soloDeNoche", false));
            var noche = new GameObject("Fantasma de noche (reaparece)");
            noche.transform.SetParent(r);
            noche.transform.position = new Vector3(60f, 0f, -114f);
            noche.AddComponent<CorralDeEnemigosDelLab>().Configurar(fantasma, 1, 1f);
            CrearTexto("Fantasmas: intangibles hasta que les da la luz\n(Bola Prisma, Nova de Luz…)", r, new Vector3(61f, 3.4f, -80f), 180f);
            CrearTexto("Este solo sale de noche (pestaña Clima)", r, new Vector3(60f, 3f, -108f), 180f);
        }
        else avisos.Add("Falta " + EnemigosDelMundoBuilder.RutaFantasma);

        // ── Mímicos ──
        var mimico = AssetDatabase.LoadAssetAtPath<GameObject>(EnemigosDelMundoBuilder.RutaMimico);
        if (mimico != null)
        {
            var conTesoro = EnemigoDelLab(mimico, "Mímico con tesoro", r, new Vector3(75f, 0f, -90f), 0f);
            TesoroDelMimico(conTesoro, avisos);
            var suelto = new GameObject("Mímico suelto (reaparece)");
            suelto.transform.SetParent(r);
            suelto.transform.SetPositionAndRotation(new Vector3(82f, 0f, -90f), Quaternion.identity);
            suelto.AddComponent<CorralDeEnemigosDelLab>().Configurar(mimico, 1, 0.5f);
            CrearTexto("Mímicos: el cofre que tiembla muerde.\nGolpéalo antes de abrirlo (o para el mordisco con B)", r, new Vector3(78f, 3.4f, -82f), 180f);
        }
        else avisos.Add("Falta " + EnemigosDelMundoBuilder.RutaMimico);

        // ── Imps ladrones ──
        var imp = AssetDatabase.LoadAssetAtPath<GameObject>(EnemigosDelMundoBuilder.RutaImp);
        if (imp != null)
        {
            var nido = new GameObject("Imps ladrones (reaparecen)");
            nido.transform.SetParent(r);
            nido.transform.position = new Vector3(98f, 2f, -96f);
            nido.AddComponent<CorralDeEnemigosDelLab>().Configurar(imp, 2, 3f);
            Placa("Placa: +100 Esencia", "Assets/_ITEMS/IT_Esencia.asset", 100, r, new Vector3(98f, 0f, -80f),
                GetMaterial("Lab_PlacaEsencia", new Color(0.62f, 0.38f, 0.95f)), avisos);
            CrearTexto("Imps: roban Esencia y huyen.\nPara el tirón con B o alcánzalos para recuperarla", r, new Vector3(98f, 3.6f, -84f), 180f);
        }
        else avisos.Add("Falta " + EnemigosDelMundoBuilder.RutaImp);

        // ── Beholder ──
        var beholder = AssetDatabase.LoadAssetAtPath<GameObject>(EnemigosDelMundoBuilder.RutaBeholder);
        if (beholder != null)
        {
            EnemigoDelLab(beholder, "Beholder", r, new Vector3(122f, 1f, -100f), -30f);
            CrearTexto("Beholder: devuélvele su rayo con B.\nAturdido, cualquier golpe entra", r, new Vector3(116f, 3.4f, -84f), 180f);
        }
        else avisos.Add("Falta " + EnemigosDelMundoBuilder.RutaBeholder);

        // ── Roca y muros rompibles ──
        var roca = AssetDatabase.LoadAssetAtPath<GameObject>(EnemigosDelMundoBuilder.RutaRoca);
        if (roca != null)
        {
            var corral = new GameObject("Roca (reaparece)");
            corral.transform.SetParent(r);
            corral.transform.position = new Vector3(90f, 0f, -118f);
            corral.AddComponent<CorralDeEnemigosDelLab>().Configurar(roca, 1, 0.5f);
            var piedra = GetMaterial("Lab_MuroRompible", new Color(0.55f, 0.47f, 0.38f));
            for (int i = 0; i < 7; i++)
                MuroRompibleDelLab($"Muro rompible {i + 1}", r, new Vector3(84f + 2f * i, 0f, -110f), piedra, avisos);
            CrearTexto("Roca: apártate de su embestida.\nCon un muro delante, lo rompe y se aturde", r, new Vector3(90f, 3.4f, -100f), 180f);
        }
        else avisos.Add("Falta " + EnemigosDelMundoBuilder.RutaRoca);

        AnadirDestinoBestiario(escena, r, avisos);
        return avisos;
    }

    private static GameObject EnemigoDelLab(GameObject prefab, string nombre, Transform padre, Vector3 pie, float giro,
                                            params (string campo, object valor)[] cambios)
    {
        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        instancia.name = nombre;
        instancia.transform.SetPositionAndRotation(pie, Quaternion.Euler(0f, giro, 0f));
        if (cambios.Length > 0)
        {
            var ia = instancia.GetComponent<EnemigoDelMundo>();
            foreach (var (campo, valor) in cambios) Asignar(ia, campo, valor);
        }
        return instancia;
    }

    /// El mímico guarda un tesoro del LAB: al vencerlo se abre y da 25 de Esencia.
    private static void TesoroDelMimico(GameObject mimico, List<string> avisos)
    {
        var esencia = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_ITEMS/IT_Esencia.asset");
        if (esencia == null) { avisos.Add("Falta IT_Esencia: el mímico no guarda tesoro."); return; }
        if (!mimico.TryGetComponent(out Tesoro tesoro)) tesoro = mimico.AddComponent<Tesoro>();
        var so = new SerializedObject(tesoro);
        so.FindProperty("idManual").stringValue = PuzlesDelLab.PrefijoDeIds + "MIMICO";
        so.FindProperty("region").stringValue = "LAB";
        so.FindProperty("forma").enumValueIndex = (int)Tesoro.Forma.Cofre;
        so.FindProperty("sfxAlAbrir").stringValue = "";
        var lista = so.FindProperty("recompensas");
        lista.arraySize = 1;
        var premio = lista.GetArrayElementAtIndex(0);
        premio.FindPropertyRelative("effectType").enumValueIndex = (int)PickupEffectType.Currency;
        premio.FindPropertyRelative("item").objectReferenceValue = esencia;
        premio.FindPropertyRelative("quantity").intValue = 25;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// Tramo de muro de 2 m hecho de cuatro bloques que salen despedidos al embestirlo.
    private static void MuroRompibleDelLab(string nombre, Transform padre, Vector3 pie, Material material, List<string> avisos)
    {
        var muro = new GameObject(nombre);
        muro.transform.SetParent(padre);
        muro.transform.position = pie;
        for (int fila = 0; fila < 2; fila++)
            for (int col = 0; col < 2; col++)
                CrearCubo($"Bloque {fila * 2 + col + 1}", muro.transform,
                    pie + new Vector3(-0.5f + col, 0.6f + 1.2f * fila, 0f), new Vector3(0.98f, 1.18f, 0.8f), material, estatico: false);
        var rompible = muro.AddComponent<MuroRompible>();
        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust ground mountain.prefab");
        if (vfx != null) Asignar(rompible, "vfxRotura", vfx);
        else if (!avisos.Contains("Falta el polvo de los muros rompibles.")) avisos.Add("Falta el polvo de los muros rompibles.");
    }

    /// Añade (o actualiza) el destino de la zona en el viaje rápido del LAB.
    private static void AnadirDestinoBestiario(Scene escena, Transform raiz, List<string> avisos)
    {
        ViajeDelLab viaje = null;
        foreach (var go in escena.GetRootGameObjects())
            if ((viaje = go.GetComponentInChildren<ViajeDelLab>(true)) != null) break;
        if (viaje == null) { avisos.Add("No hay viaje rápido en el LAB: la zona no tiene destino."); return; }

        var punto = PuntoDeDestino(raiz, NombreDestinoBestiario);
        punto.SetPositionAndRotation(EntradaBestiario, Quaternion.Euler(0f, 180f, 0f));

        var so = new SerializedObject(viaje);
        var destinos = so.FindProperty("destinos");
        int indice = -1;
        for (int i = 0; i < destinos.arraySize; i++)
            if (destinos.GetArrayElementAtIndex(i).FindPropertyRelative("nombre").stringValue == NombreDestinoBestiario) indice = i;
        if (indice < 0)
        {
            indice = destinos.arraySize;
            destinos.arraySize++;
        }
        Destino(destinos.GetArrayElementAtIndex(indice), NombreDestinoBestiario,
            "Fantasmas, mímicos, imps ladrones, Beholder y Roca con muros rompibles.", punto);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
