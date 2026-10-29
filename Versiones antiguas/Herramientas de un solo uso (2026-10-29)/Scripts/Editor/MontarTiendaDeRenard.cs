#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.NPC;
using Game.NPC.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>Monta contenido de tienda y narrativa mediante las herramientas oficiales.</summary>
public static class MontarTiendaDeRenard
{
    const string Fuente = "Assets/_SHOPS/ShopMenu_TestWardrobe.prefab";
    const string CanvasFuente = "Assets/_SHOPS/Shop_Canvas_TestWardrobe.prefab";
    const string Tienda = "Assets/_SHOPS/ShopMenu_Renard.prefab";
    const string CanvasTienda = "Assets/_SHOPS/Shop_Canvas_Renard.prefab";
    const string Npc = "Assets/_NPCs/Pueblo/Generados/Renard.prefab";
    const string Script = "Assets/Scripts/Editor/MontarTiendaDeRenard.cs";
    const string Mundo = "Assets/Scenes/Worlds/MainWorld.unity";
    const string Identidad = "NPC_Renard";
    const string Spawn = "SPAWN_Renard";

    [MenuItem("El Sendero/Archivo/Montar la tienda de Renard")]
    public static void Montar()
    {
        ExigirEdicion();
        var esencia = Exigir<ItemData>("Assets/_ITEMS/IT_Esencia.asset");
        var moneda = Exigir<ItemData>("Assets/_ITEMS/IT_Coin.asset");
        bool fuenteUsada = TieneReferenciasExternas();
        var candidatos = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/_ITEMS" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemData>)
            .Where(i => i != null && i.name.StartsWith("IT_W_", StringComparison.Ordinal) && i.wardrobeUnlock != null)
            .OrderBy(i => i.name, StringComparer.Ordinal).ToList();
        var excluidos = AuditarExclusividad(fuenteUsada);
        moneda.pluralDisplayNameId = "ITEM_COIN_NAME_PLURAL";
        moneda.pluralDisplayName = "Monedas";
        esencia.pluralDisplayNameId = "ITEM_ESENCIA_NAME_PLURAL";
        esencia.pluralDisplayName = "Esencias";
        EditorUtility.SetDirty(moneda);
        EditorUtility.SetDirty(esencia);
        var catalogo = new List<ItemData>();
        foreach (var item in candidatos)
        {
            if (excluidos.ContainsKey(item.wardrobeUnlock.WardrobeId)) continue;
            item.buyPrice = Precio(item.wardrobeUnlock);
            item.sellValue = 0;
            EditorUtility.SetDirty(item);
            catalogo.Add(item);
        }
        catalogo.Add(Bolsa("Pequena", 15, 20, moneda));
        catalogo.Add(Bolsa("Grande", 50, 75, moneda));
        var tienda = CrearTienda(esencia, catalogo);
        var introduccion = Dialogo("DG_RENARD_INTRO", Enumerable.Range(1, 5).Select(i => $"DLG_RENARD_INTRO_{i:00}").ToArray());
        var saludo = Dialogo("DG_RENARD_SALUDO", new[] { "DLG_RENARD_SALUDO" });
        var npc = CrearNpc(tienda, saludo);
        RegistrarNpc(npc);
        MontarGrafo(introduccion);
        ItemRegistrySincronizador.Sincronizar();
        AssetDatabase.SaveAssets();
        if (!fuenteUsada) ArchivarFuente();
        ArchivarMenu();
        string informe = string.Join("\n", excluidos.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value}"));
        Debug.Log($"[Tienda de Renard] {catalogo.Count - 2} prendas y dos bolsas. Exclusiones:\n{informe}\n" +
            (fuenteUsada ? "La tienda de prueba conserva referencias y permanece en Assets." : "La tienda de prueba sin referencias se archiva fuera de Assets."));
        Selection.activeObject = npc;
        EditorGUIUtility.PingObject(npc);
    }

    static T Exigir<T>(string ruta) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(ruta) ?? throw new InvalidOperationException($"Falta {ruta}. Ejecuta primero el montaje de Esencia si corresponde.");

    static void ExigirEdicion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Sal de Play para montar los assets.");
    }

    static void Carpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        Carpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    static ItemData Bolsa(string tamano, int precio, int cantidad, ItemData moneda)
    {
        string ruta = $"Assets/_ITEMS/IT_BolsaMonedas{tamano}.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(item, ruta);
        }
        string clave = "ITEM_BOLSA_MONEDAS_" + tamano.ToUpperInvariant();
        item.itemId = "bolsa_monedas_" + tamano.ToLowerInvariant();
        item.displayNameId = clave + "_NAME";
        item.useDescriptionId = clave + "_DESC";
        item.displayName = tamano == "Pequena" ? "Bolsa pequeña de monedas" : "Bolsa grande de monedas";
        item.useDescription = $"Contiene {cantidad} monedas.";
        if (item.icon == null) item.icon = Exigir<Sprite>("Assets/Art/UI/Items/coin.png");
        item.usageKind = ItemData.ItemUsageKind.Currency;
        item.usableFromInventory = false;
        item.buyPrice = precio;
        item.sellValue = 0;
        item.useEffects = new List<PickupEffect> { new PickupEffect { effectType = PickupEffectType.Currency, item = moneda, quantity = cantidad } };
        EditorUtility.SetDirty(item);
        return item;
    }

    static int Precio(WardrobeItemSO prenda)
    {
        if (prenda.Category == PartCategory.Body) return 100;
        if (prenda.Category == PartCategory.Cloak) return 150;
        string pieza = prenda.PartName;
        if (pieza.StartsWith("AC10_", StringComparison.Ordinal) || pieza.StartsWith("AC11_", StringComparison.Ordinal)) return 30;
        if (pieza.Contains("Crown") || pieza.Contains("Horn") || pieza.Contains("Angel")) return 120;
        return 50;
    }

    static ShopUI CrearTienda(ItemData moneda, List<ItemData> catalogo)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Tienda) == null && !AssetDatabase.CopyAsset(Fuente, Tienda))
            throw new InvalidOperationException("No se puede copiar la tienda de armario.");
        var raiz = PrefabUtility.LoadPrefabContents(Tienda);
        try
        {
            raiz.name = "ShopMenu_Renard";
            var controlador = raiz.GetComponentInChildren<ShopController>(true);
            if (controlador == null) throw new InvalidOperationException("La tienda necesita ShopController.");
            var so = new SerializedObject(controlador);
            so.FindProperty("currencyItem").objectReferenceValue = moneda;
            var stock = so.FindProperty("stock");
            stock.arraySize = catalogo.Count;
            for (int i = 0; i < catalogo.Count; i++)
            {
                var entrada = stock.GetArrayElementAtIndex(i);
                entrada.FindPropertyRelative("item").objectReferenceValue = catalogo[i];
                entrada.FindPropertyRelative("buyPriceOverride").intValue = -1;
                entrada.FindPropertyRelative("sellPriceOverride").intValue = -1;
                entrada.FindPropertyRelative("limitedStock").boolValue = catalogo[i].wardrobeUnlock != null;
                entrada.FindPropertyRelative("startingStock").intValue = 1;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, Tienda);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasTienda) == null &&
            !AssetDatabase.CopyAsset("Assets/_SHOPS/Shop_Canvas.prefab", CanvasTienda))
            throw new InvalidOperationException("No se puede copiar la interfaz de tienda.");
        var canvas = PrefabUtility.LoadPrefabContents(CanvasTienda);
        try
        {
            canvas.name = "Shop_Canvas_Renard";
            var ui = canvas.GetComponentInChildren<ShopUI>(true);
            if (ui == null) throw new InvalidOperationException("La interfaz necesita ShopUI.");
            var su = new SerializedObject(ui);
            su.FindProperty("shopController").objectReferenceValue = Exigir<GameObject>(Tienda).GetComponent<ShopController>();
            su.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasTienda);
        }
        finally { PrefabUtility.UnloadPrefabContents(canvas); }
        return Exigir<GameObject>(CanvasTienda).GetComponentInChildren<ShopUI>(true);
    }

    static DialogueAsset Dialogo(string nombre, string[] claves)
    {
        const string carpeta = "Assets/_DIALOGUES/Renard";
        Carpeta(carpeta);
        string ruta = carpeta + "/" + nombre + ".asset";
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dialogo == null)
        {
            dialogo = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dialogo, ruta);
        }
        dialogo.lines = claves.Select(c => new DialogueLine { speakerNameId = "CHAR_RENARD", textId = c }).ToArray();
        dialogo.isGroupConversation = false;
        dialogo.maxLinesPerPage = 3;
        EditorUtility.SetDirty(dialogo);
        return dialogo;
    }

    static GameObject CrearNpc(ShopUI tienda, DialogueAsset saludo)
    {
        bool nuevo = AssetDatabase.LoadAssetAtPath<GameObject>(Npc) == null;
        return GeneradorDeNpcs.CrearDesdePlantilla("Assets/_NPCs/Sofia.prefab", Npc, "Renard",
            "Assets/_NPCs/Pueblo/Generados/Tomasa.prefab", raiz =>
            {
                // Se copia el servicio de vendedor, sin los encargos propios de la plantilla.
                foreach (var componente in raiz.GetComponents<MonoBehaviour>())
                    if (componente is SenalDeCompra || componente is QuestObjectiveMarker) UnityEngine.Object.DestroyImmediate(componente);
                if (nuevo)
                {
                    GeneradorDeNpcs.SeleccionarPiezas(raiz, "Head01_Male", "Body04", "Cloak03", "Hair01", "AC10_Mustache04", "Eye01", "Eyebrow01", "Mouth02");
                    Teñir(raiz, "Body04", new Color(0.42f, 0.30f, 0.18f), false);
                    Teñir(raiz, "Cloak03", new Color(0.18f, 0.25f, 0.15f), false);
                    foreach (string pieza in new[] { "Hair01", "AC10_Mustache04", "Eyebrow01" })
                        Teñir(raiz, pieza, new Color(0.65f, 0.65f, 0.60f), true);
                }
                var modular = raiz.GetComponent<ModularAutoBuilder>();
                if (modular != null) { modular.randomizeAtAwake = false; modular.preserveActivePartsOnAwake = true; }
                var gestor = raiz.GetComponent<NPCBehaviourManagerV2>() ?? raiz.AddComponent<NPCBehaviourManagerV2>();
                var sg = new SerializedObject(gestor);
                sg.FindProperty("persistenceId").stringValue = Identidad;
                sg.FindProperty("dialogueCharacterId").stringValue = "CHAR_RENARD";
                sg.FindProperty("disableWander").boolValue = true;
                var config = sg.FindProperty("configuration");
                config.FindPropertyRelative("behaviourType").intValue = (int)NPCBehaviourType.Ambient;
                foreach (string campo in new[] { "questConfig", "interactiveNarrativeConfig", "combatConfig", "partyConfig", "socialConfig" })
                    config.FindPropertyRelative(campo).objectReferenceValue = null;
                sg.ApplyModifiedPropertiesWithoutUndo();
                var interactuable = raiz.GetComponent<Interactable>() ?? raiz.AddComponent<Interactable>();
                interactuable.SetMode(InteractableMode.OpenDialogue);
                interactuable.SetDialogue(saludo);
                var si = new SerializedObject(interactuable);
                si.FindProperty("dialogueCharacterId").stringValue = "CHAR_RENARD";
                si.FindProperty("singleUse").boolValue = false;
                si.ApplyModifiedPropertiesWithoutUndo();
                var vendedor = raiz.GetComponent<ShopVendor>() ?? raiz.AddComponent<ShopVendor>();
                var sv = new SerializedObject(vendedor);
                sv.FindProperty("shopUIPrefab").objectReferenceValue = tienda;
                sv.ApplyModifiedPropertiesWithoutUndo();
                if (raiz.GetComponent<NarrativeActor>() == null) raiz.AddComponent<NarrativeActor>();
            }, quitarSobrantes: true);
    }

    static void Teñir(GameObject raiz, string pieza, Color color, bool liso)
    {
        const string carpeta = "Assets/_NPCs/Pueblo/Generados/Materiales";
        Carpeta(carpeta);
        var objeto = raiz.GetComponentsInChildren<Transform>(true).First(t => t.name == pieza);
        int indice = 0;
        foreach (var renderer in objeto.GetComponentsInChildren<Renderer>(true))
        {
            var materiales = renderer.sharedMaterials;
            for (int i = 0; i < materiales.Length; i++)
            {
                if (materiales[i] == null) continue;
                string ruta = $"{carpeta}/Renard_{pieza}_{indice++}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (material == null) { material = new Material(materiales[i]); AssetDatabase.CreateAsset(material, ruta); }
                foreach (string propiedad in new[] { "_BaseColor", "_Color" })
                    if (material.HasProperty(propiedad)) material.SetColor(propiedad, color);
                if (liso) foreach (string propiedad in new[] { "_BaseMap", "_MainTex" })
                    if (material.HasProperty(propiedad)) material.SetTexture(propiedad, null);
                EditorUtility.SetDirty(material);
                materiales[i] = material;
            }
            renderer.sharedMaterials = materiales;
        }
    }

    static void RegistrarNpc(GameObject npc)
    {
        var roster = Exigir<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset");
        var entrada = roster.entries.Find(e => e.spawnId == Spawn);
        if (entrada == null) { entrada = new NpcRosterSO.Entry(); roster.entries.Add(entrada); }
        entrada.enabled = true;
        entrada.spawnId = Spawn;
        entrada.prefab = npc;
        entrada.gameObjectName = "Renard";
        entrada.persistenceId = Identidad;
        entrada.vozDeReacciones = "Vecino";
        entrada.startActive = true;
        entrada.requireNavMesh = true;
        entrada.requiredFlag = "NARRATIVE_FLAG:CAP2_INICIADO";
        entrada.invertCondition = false;
        EditorUtility.SetDirty(roster);
    }

    static void MontarGrafo(DialogueAsset introduccion)
    {
        var grafo = Exigir<NarrativeGraph>("Assets/NarrativeGraph/Secundary.asset");
        var inicio = grafo.FindNode(grafo.startNodeGuid) ?? throw new InvalidOperationException("El grafo secundario necesita un inicio válido.");
        if (inicio.HasNamedOutputs) throw new InvalidOperationException("El inicio debe admitir ramas paralelas.");
        var conocido = Nodo<BranchFlagNode>(grafo, "25b810b1-9ddd-48bd-9474-773389932a01", "Renard: ¿ya lo conozco?", 0);
        var esperar = Nodo<WaitNpcInteractionNode>(grafo, "25b810b1-9ddd-48bd-9474-773389932a02", "Hablar con Renard", 1);
        var charla = Nodo<PlayDialogueNode>(grafo, "25b810b1-9ddd-48bd-9474-773389932a03", "Renard se presenta", 2);
        var recordar = Nodo<SetFlagNode>(grafo, "25b810b1-9ddd-48bd-9474-773389932a04", "Renard conocido", 3);
        conocido.flagKey = "RENARD_CONOCIDO";
        conocido.outputs = new List<string> { "", esperar.guid };
        esperar.npcId = Identidad;
        esperar.acceptEarlierInteraction = false;
        esperar.outputs = new List<string> { charla.guid };
        charla.npcId = Identidad;
        charla.dialogue = introduccion;
        charla.outputs = new List<string> { recordar.guid };
        recordar.flagKey = "RENARD_CONOCIDO";
        recordar.value = true;
        if (!inicio.outputs.Contains(conocido.guid)) inicio.outputs.Add(conocido.guid);
        EditorUtility.SetDirty(grafo);
    }

    static T Nodo<T>(NarrativeGraph grafo, string guid, string titulo, int columna) where T : NarrativeNode, new()
    {
        var existente = grafo.FindNode(guid);
        if (existente != null) return existente as T ?? throw new InvalidOperationException($"El GUID {guid} pertenece a otro tipo de nodo.");
        var nodo = new T { guid = guid, displayTitle = titulo, chapter = "Renard", position = new Vector2(400 + columna * 330, 2700) };
        grafo.nodes.Add(nodo);
        return nodo;
    }

    // La lectura de referencias es una auditoría; toda escritura de assets pasa por AssetDatabase.
    static Dictionary<string, string> AuditarExclusividad(bool fuenteUsada)
    {
        var excluidos = new Dictionary<string, string>(StringComparer.Ordinal);
        var guids = new Dictionary<string, string>(StringComparer.Ordinal);
        var definiciones = new HashSet<string>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:WardrobeItemSO"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var prenda = Exigir<WardrobeItemSO>(ruta);
            guids[guid] = prenda.WardrobeId;
            definiciones.Add(ruta);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var item = Exigir<ItemData>(ruta);
            definiciones.Add(ruta);
            if (item.wardrobeUnlock != null) guids[guid] = item.wardrobeUnlock.WardrobeId;
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ItemRegistrySO")) definiciones.Add(AssetDatabase.GUIDToAssetPath(guid));
        foreach (string ruta in AssetDatabase.GetAllAssetPaths())
        {
            if (ruta == Tienda || (!fuenteUsada && (ruta == Fuente || ruta == CanvasFuente)) || definiciones.Contains(ruta) || !File.Exists(ruta)) continue;
            string extension = Path.GetExtension(ruta);
            if (extension != ".asset" && extension != ".prefab" && extension != ".unity") continue;
            string texto = File.ReadAllText(ruta);
            foreach (Match referencia in Regex.Matches(texto, @"guid: ([a-f0-9]{32})"))
                if (guids.TryGetValue(referencia.Groups[1].Value, out string id)) AñadirExclusion(excluidos, id, ruta);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PlayerPresetSO"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var preset = Exigir<PlayerPresetSO>(ruta);
            foreach (string id in preset.unlockedWardrobeIds) AñadirExclusion(excluidos, id, ruta + " (desbloqueada al iniciar)");
        }
        return excluidos;
    }

    static void AñadirExclusion(Dictionary<string, string> mapa, string id, string ruta)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!mapa.TryGetValue(id, out string anterior)) mapa[id] = ruta;
        else if (!anterior.Contains(ruta)) mapa[id] = anterior + "; " + ruta;
    }

    static bool TieneReferenciasExternas()
    {
        string guid = AssetDatabase.AssetPathToGUID(Fuente);
        string guidCanvas = AssetDatabase.AssetPathToGUID(CanvasFuente);
        foreach (string asset in AssetDatabase.GetAllAssetPaths())
        {
            if (asset == Fuente || asset == CanvasFuente || !File.Exists(asset)) continue;
            string extension = Path.GetExtension(asset);
            if (extension != ".asset" && extension != ".prefab" && extension != ".unity") continue;
            string texto = File.ReadAllText(asset);
            if ((!string.IsNullOrEmpty(guid) && texto.Contains("guid: " + guid)) ||
                (!string.IsNullOrEmpty(guidCanvas) && texto.Contains("guid: " + guidCanvas))) return true;
        }
        return false;
    }

    static void ArchivarFuente()
    {
        // La interfaz de prueba solo apunta a su catálogo; se retiran juntas para no dejar referencias rotas.
        ArchivarPrefab(CanvasFuente);
        ArchivarPrefab(Fuente);
    }

    static void ArchivarPrefab(string origen)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(origen) == null) return;
        string raiz = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string destino = Path.GetFullPath(Path.Combine(raiz, "Versiones antiguas/Tiendas", Path.GetFileName(origen)));
        if (!destino.StartsWith(raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El archivo debe permanecer dentro del proyecto.");
        Directory.CreateDirectory(Path.GetDirectoryName(destino));
        File.Copy(origen, destino, true);
        File.Copy(origen + ".meta", destino + ".meta", true);
        if (!AssetDatabase.DeleteAsset(origen)) throw new InvalidOperationException("No se pudo retirar la tienda de prueba.");
    }

    static void ArchivarMenu()
    {
        string texto = File.ReadAllText(Script);
        texto = texto.Replace("[MenuItem(\"El Sendero/Tiendas/Montar la tienda de Renard\")]", "[MenuItem(\"El Sendero/Archivo/Montar la tienda de Renard\")]");
        File.WriteAllText(Script, texto, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(Script);
    }

    [MenuItem("El Sendero/Tiendas/Colocar a Renard junto a la entrada del bosque")]
    public static void Colocar()
    {
        ExigirEdicion();
        var escena = SceneManager.GetSceneByPath(Mundo);
        if (!escena.isLoaded) escena = EditorSceneManager.OpenScene(Mundo, OpenSceneMode.Additive);
        var objetos = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (var objeto in objetos)
        {
            var punto = objeto.GetComponent<NpcSpawnPoint>();
            if (punto != null && punto.spawnId == Spawn) { Selection.activeGameObject = punto.gameObject; return; }
        }
        if (escena.isDirty) throw new InvalidOperationException("Guarda MainWorld antes de añadir el marcador.");
        var entrada = objetos.FirstOrDefault(t => t.name == "Woods_Entrance_SavePoint") ??
            throw new InvalidOperationException("No se encuentra Woods_Entrance_SavePoint en MainWorld.");
        Vector3 posicion = entrada.position + entrada.right * 3f;
        if (NavMesh.SamplePosition(posicion, out var nav, 5f, NavMesh.AllAreas)) posicion = nav.position;
        else if (Physics.Raycast(posicion + Vector3.up * 10f, Vector3.down, out var suelo, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            posicion = suelo.point;
        else throw new InvalidOperationException("No hay NavMesh ni suelo junto a la entrada del bosque.");
        var marcador = new GameObject(Spawn);
        Undo.RegisterCreatedObjectUndo(marcador, "Colocar a Renard");
        SceneManager.MoveGameObjectToScene(marcador, escena);
        marcador.transform.position = posicion;
        Vector3 mirada = Vector3.ProjectOnPlane(entrada.position - posicion, Vector3.up);
        marcador.transform.rotation = Quaternion.LookRotation(mirada.sqrMagnitude > 0.01f ? mirada : entrada.forward, Vector3.up);
        marcador.AddComponent<NpcSpawnPoint>().spawnId = Spawn;
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Selection.activeGameObject = marcador;
        SceneView.lastActiveSceneView?.FrameSelected();
    }
}
#endif
