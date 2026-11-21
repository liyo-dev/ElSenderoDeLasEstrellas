using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.NPC;
using Game.NPC.Combate;
using Game.NPC.Modules;

/// Zona del LAB para pelear contra personas en grupo (oeste, al sur de los jefes): cuatro placas
/// en la entrada que empiezan una oleada de dos, tres, muchos o mezcla con criaturas. Los luchadores
/// son magos, Lety, Vicky y el pirata con el cerebro por puntuación y un escudo elemental cada uno
/// (se rompe con la magia de su mismo color; rótulo encima con el elemento). La mezcla añade
/// arañas (débiles al fuego) y un fantasma (le hace falta luz) para probar debilidades de daño.
/// Crea sus configs y perfiles en ElSendero/Data/Lab. Se rehace al regenerar el LAB o con su menú.
/// Ver INC-1357.
public static partial class CombatLabBuilder
{
    private const string RaizLuchaNpcs = "LAB_LUCHA_CONTRA_PERSONAS (oleadas y escudos elementales)";
    private const string NombreDestinoLuchaNpcs = "Lucha contra personas (oeste)";
    private const string CarpetaDatosLab = "Assets/ElSendero/Data/Lab";
    private const string ConfigBaseDeLucha = "Assets/_NPCs/Combat/NPC_Combat_Config_Mago#1.asset";
    private static readonly Vector3 CentroLuchaNpcs = new Vector3(-90f, 0f, -80f);
    private static readonly Vector3 EntradaLuchaNpcs = new Vector3(-90f, 0.1f, -98f);
    private const float LadoLuchaNpcs = 44f;

    [MenuItem("El Sendero/Combate/LAB: zona de lucha contra personas (INC-1357)")]
    public static void MontarZonaLuchaNpcs()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("LAB", "Sal de Play antes de tocar la escena."); return; }
        if (!File.Exists(ScenePath)) { OrdenesDelEditor.Avisar("LAB", $"No existe {ScenePath}: crea antes el LAB."); return; }

        Scene escena = SceneManager.GetSceneByPath(ScenePath);
        bool abiertaAqui = false;
        if (!escena.IsValid() || !escena.isLoaded)
        {
            escena = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            abiertaAqui = true;
        }
        try
        {
            var avisos = CrearZonaLuchaNpcs(escena);
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            AssetDatabase.SaveAssets();
            foreach (var a in avisos) Debug.LogWarning("[LAB] " + a);
            Debug.Log("[LAB] Zona de lucha contra personas montada (viaje rápido: «" + NombreDestinoLuchaNpcs + "»).");
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static List<string> CrearZonaLuchaNpcs(Scene escena)
    {
        var avisos = new List<string>();
        foreach (var raizExistente in escena.GetRootGameObjects())
            if (raizExistente.name == RaizLuchaNpcs) Object.DestroyImmediate(raizExistente);
        var raiz = new GameObject(RaizLuchaNpcs);
        SceneManager.MoveGameObjectToScene(raiz, escena);
        var r = raiz.transform;

        // ── Suelo y muros con su propio NavMesh ──
        var geometria = new GameObject("Geometría (con NavMesh)");
        geometria.transform.SetParent(r);
        var g = geometria.transform;
        var suelo = GetMaterial("Lab_Suelo", new Color(0.19f, 0.22f, 0.28f));
        var muro = GetMaterial("Lab_Cobertura", new Color(0.35f, 0.40f, 0.47f));
        Vector3 c = CentroLuchaNpcs;
        float m = LadoLuchaNpcs * 0.5f;
        CrearCubo("Suelo", g, c + new Vector3(0f, -0.25f, 0f), new Vector3(LadoLuchaNpcs, 0.5f, LadoLuchaNpcs), suelo, CapaSuelo);
        CrearCubo("Muro norte", g, c + new Vector3(0f, 1.5f, m), new Vector3(LadoLuchaNpcs, 3f, 0.5f), muro);
        CrearCubo("Muro sur", g, c + new Vector3(0f, 1.5f, -m), new Vector3(LadoLuchaNpcs, 3f, 0.5f), muro);
        CrearCubo("Muro este", g, c + new Vector3(m, 1.5f, 0f), new Vector3(0.5f, 3f, LadoLuchaNpcs), muro);
        CrearCubo("Muro oeste", g, c + new Vector3(-m, 1.5f, 0f), new Vector3(0.5f, 3f, LadoLuchaNpcs), muro);
        // Coberturas para que el cerebro nuevo tenga dónde recargar y el jugador dónde esquivar.
        CrearCubo("Cobertura 1", g, c + new Vector3(-7f, 0.65f, 4f), new Vector3(2.5f, 1.3f, 1.2f), muro);
        CrearCubo("Cobertura 2", g, c + new Vector3(7f, 0.65f, 6f), new Vector3(2.5f, 1.3f, 1.2f), muro);
        CrearCubo("Pilar", g, c + new Vector3(0f, 1.5f, 12f), new Vector3(1.5f, 3f, 1.5f), muro);
        var navMesh = geometria.AddComponent<NavMeshSurface>();
        navMesh.collectObjects = CollectObjects.Children;
        navMesh.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        navMesh.BuildNavMesh();

        CrearVueltaAlLaboratorio(r, EntradaLuchaNpcs + new Vector3(-8f, -0.1f, -1f), EntradaLuchaNpcs);
        CrearTexto("Lucha contra personas: pisa una placa.\nCada escudo se rompe con la magia de su mismo color", r,
            EntradaLuchaNpcs + new Vector3(0f, 3.8f, 3f), 180f);

        // ── Luchadores: plantillas apagadas ──
        var plantillas = new GameObject("Plantillas (apagadas; se clonan al empezar)");
        plantillas.transform.SetParent(r);
        plantillas.SetActive(false);
        var p = plantillas.transform;

        GameObject Luchador(string prefab, MagicElement elemento)
        {
            var config = ConfigDeLucha(elemento, avisos);
            return PlantillaDePersona(prefab, elemento, config, p, avisos);
        }

        var mago1Fuego = Luchador("Assets/_NPCs/Combat/Mago #1.prefab", MagicElement.Fire);
        var mago2Viento = Luchador("Assets/_NPCs/Combat/Mago #2.prefab", MagicElement.Storm);
        var mago3Luz = Luchador("Assets/_NPCs/Combat/Mago #3.prefab", MagicElement.Light);
        var letyMente = Luchador("Assets/_NPCs/Combat/Lety.prefab", MagicElement.Mind);
        var vickyOscuro = Luchador("Assets/_NPCs/Combat/Vicky.prefab", MagicElement.Dark);
        var pirataFuego = Luchador("Assets/_NPCs/Combat/Boy_Pirate.prefab", MagicElement.Fire);
        var arana = PlantillaDeCriatura("Assets/Prefabs/Enemy/Spider1.prefab", "Araña (débil al fuego)", p, avisos);
        var fantasma = PlantillaDeCriatura(EnemigosDelMundoBuilder.RutaFantasma, "Fantasma (hace falta luz)", p, avisos);

        // ── Zona y placas ──
        var zonaGo = new GameObject("Zona de lucha");
        zonaGo.transform.SetParent(r);
        zonaGo.transform.position = c;
        var zona = zonaGo.AddComponent<PeleaContraNpcsDelLab>();
        var corro = Punto("Centro del corro", zonaGo.transform, c + new Vector3(0f, 0.1f, 4f));
        var estado = RotuloDeEstado(r, c + new Vector3(0f, 4.5f, -6f));

        var oleadas = new (string nombre, GameObject[] luchadores)[]
        {
            ("Dos", new[] { mago1Fuego, mago2Viento }),
            ("Tres", new[] { mago3Luz, letyMente, vickyOscuro }),
            ("Muchos", new[] { mago1Fuego, mago2Viento, mago3Luz, letyMente, vickyOscuro, pirataFuego }),
            ("Mezcla con criaturas", new[] { letyMente, vickyOscuro, arana, arana, fantasma }),
        };
        var so = new SerializedObject(zona);
        var lista = so.FindProperty("oleadas");
        lista.arraySize = oleadas.Length;
        for (int i = 0; i < oleadas.Length; i++)
        {
            var o = lista.GetArrayElementAtIndex(i);
            o.FindPropertyRelative("nombre").stringValue = oleadas[i].nombre;
            var ls = o.FindPropertyRelative("luchadores");
            var validos = new List<GameObject>();
            foreach (var l in oleadas[i].luchadores) if (l != null) validos.Add(l);
            ls.arraySize = validos.Count;
            for (int k = 0; k < validos.Count; k++) ls.GetArrayElementAtIndex(k).objectReferenceValue = validos[k];
        }
        so.FindProperty("centro").objectReferenceValue = corro;
        so.FindProperty("rotulo").objectReferenceValue = estado;
        so.ApplyModifiedPropertiesWithoutUndo();

        var colores = new[]
        {
            new Color(0.35f, 0.75f, 0.95f), new Color(0.45f, 0.85f, 0.45f),
            new Color(0.95f, 0.65f, 0.25f), new Color(0.85f, 0.40f, 0.75f)
        };
        var textos = new[]
        {
            "Dos\n(Fuego, Viento)", "Tres\n(Luz, Mente, Oscuro)",
            "Muchos\n(seis, todos los colores)", "Mezcla\n(personas, arañas y fantasma)"
        };
        for (int i = 0; i < oleadas.Length; i++)
        {
            Vector3 pos = EntradaLuchaNpcs + new Vector3(-6f + 4f * i, -0.08f, 5f);
            var placa = CrearCubo("Placa: " + oleadas[i].nombre, r, pos, new Vector3(2.4f, 0.15f, 2.4f),
                GetMaterial("Lab_PlacaOleada" + i, colores[i]), estatico: false);
            var col = placa.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            placa.AddComponent<PlacaDeOleadaDelLab>().Configurar(zona, i);
            CrearTexto(textos[i], r, pos + new Vector3(0f, 1.6f, 0f), 180f);
        }

        AnadirDestinoLuchaNpcs(escena, r, avisos);
        return avisos;
    }

    /// Config de combate del LAB para un escudo de 'elemento': copia de la del mago con el cerebro
    /// por puntuación y un perfil que se protege con ese escudo en vez de esquivar.
    private static NPCCombatConfig ConfigDeLucha(MagicElement elemento, List<string> avisos)
    {
        AsegurarCarpeta(CarpetaDatosLab);
        string nombre = NombreDelElemento(elemento);
        string rutaPerfil = $"{CarpetaDatosLab}/LAB_Perfil_Escudo_{nombre}.asset";
        var perfil = AssetDatabase.LoadAssetAtPath<PerfilDeCombate>(rutaPerfil);
        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<PerfilDeCombate>();
            AssetDatabase.CreateAsset(perfil, rutaPerfil);
        }
        perfil.defensas.esquivar = false;
        perfil.defensas.escudo = true;
        perfil.defensas.escudoElemental = true;
        perfil.defensas.elementoDelEscudo = elemento;
        perfil.defensas.duracionDelEscudo = 3f;
        perfil.defensas.enfriamientoDelEscudo = 2.5f;
        perfil.caracter.cautela = 0.8f;
        perfil.caracter.agresividad = 0.6f;
        perfil.dificultad = 0.5f;
        EditorUtility.SetDirty(perfil);

        string rutaConfig = $"{CarpetaDatosLab}/LAB_Combate_Escudo_{nombre}.asset";
        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(rutaConfig);
        if (config == null)
        {
            if (AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(ConfigBaseDeLucha) == null)
            {
                avisos.Add("Falta " + ConfigBaseDeLucha + ": sin configs de lucha.");
                return null;
            }
            AssetDatabase.CopyAsset(ConfigBaseDeLucha, rutaConfig);
            config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(rutaConfig);
        }
        var so = new SerializedObject(config);
        so.FindProperty("cerebro").enumValueIndex = (int)TipoDeCerebroDeCombate.PorPuntuacion;
        so.FindProperty("perfilDeCombate").objectReferenceValue = perfil;
        so.FindProperty("isAggressive").boolValue = true;
        so.FindProperty("useShield").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (config.shieldPrefab == null) avisos.Add($"{rutaConfig}: sin prefab de escudo, no se verá el escudo.");

        // Sus hechizos son de su color (rompen el escudo del jugador de ese color) y el fuerte, el
        // tercero, no se puede devolver: con él se nota la regla («¿quién de mi grupo puede parar esto?»).
        perfil.acciones.Clear();
        PerfilEnUso.AnadirHuecosDeLaConfig(perfil.acciones, config, perfil.caracter);
        for (int i = 0; i < perfil.acciones.Count; i++)
        {
            var accion = perfil.acciones[i];
            accion.conElemento = true;
            accion.elemento = elemento;
            accion.nombre = $"{nombre} {i + 1}";
            if (i == 2)
            {
                accion.sePuedeDevolver = false;
                accion.nombre += " (no se devuelve)";
            }
        }
        EditorUtility.SetDirty(perfil);
        return config;
    }

    private static GameObject PlantillaDePersona(string rutaPrefab, MagicElement elemento, NPCCombatConfig config,
                                                 Transform padre, List<string> avisos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (prefab == null) { avisos.Add("Falta " + rutaPrefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        string nombre = NombreDelElemento(elemento);
        go.name = $"Plantilla — {prefab.name} (escudo de {nombre})";
        var npc = go.GetComponent<NPCBehaviourManagerV2>();
        if (npc == null) avisos.Add(rutaPrefab + " no tiene NPCBehaviourManagerV2: no pelea.");
        else if (config != null)
        {
            var so = new SerializedObject(npc);
            var campo = so.FindProperty("configuration.combatConfig");
            if (campo != null) { campo.objectReferenceValue = config; so.ApplyModifiedPropertiesWithoutUndo(); }
            else avisos.Add("No encuentro configuration.combatConfig en " + go.name);
        }

        // Rótulo del escudo sobre la cabeza, en su color.
        var rotulo = new GameObject("Rótulo del escudo");
        rotulo.transform.SetParent(go.transform, false);
        rotulo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        rotulo.transform.localScale = Vector3.one * 0.14f;
        var texto = rotulo.AddComponent<TextMesh>();
        texto.text = "Escudo de " + nombre;
        texto.anchor = TextAnchor.MiddleCenter;
        texto.alignment = TextAlignment.Center;
        texto.fontSize = 32;
        texto.characterSize = 0.5f;
        texto.color = RuptoresDeEscudo.ColorDe(elemento);
        rotulo.AddComponent<CaraALaCamaraDelLab>();
        return go;
    }

    private static GameObject PlantillaDeCriatura(string rutaPrefab, string nombre, Transform padre, List<string> avisos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
        if (prefab == null) { avisos.Add("Falta " + rutaPrefab); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
        go.name = "Plantilla — " + nombre;
        return go;
    }

    private static TextMesh RotuloDeEstado(Transform padre, Vector3 pos)
    {
        var go = new GameObject("Rótulo de la oleada");
        go.transform.SetParent(padre);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 180f, 0f));
        go.transform.localScale = Vector3.one * 0.22f;
        var t = go.AddComponent<TextMesh>();
        t.text = "Pisa una placa para empezar";
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontSize = 32;
        t.characterSize = 0.5f;
        return t;
    }

    private static string NombreDelElemento(MagicElement e) => e switch
    {
        MagicElement.Fire => "Fuego",
        MagicElement.Storm => "Viento",
        MagicElement.Light => "Luz",
        MagicElement.Mind => "Mente",
        MagicElement.Dark => "Oscuro",
        _ => e.ToString()
    };

    private static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(ruta));
    }

    private static void AnadirDestinoLuchaNpcs(Scene escena, Transform raiz, List<string> avisos)
    {
        ViajeDelLab viaje = null;
        foreach (var go in escena.GetRootGameObjects())
            if ((viaje = go.GetComponentInChildren<ViajeDelLab>(true)) != null) break;
        if (viaje == null) { avisos.Add("No hay viaje rápido en el LAB: la zona de lucha no tiene destino."); return; }

        var punto = PuntoDeDestino(raiz, NombreDestinoLuchaNpcs);
        punto.SetPositionAndRotation(EntradaLuchaNpcs, Quaternion.identity);

        var so = new SerializedObject(viaje);
        var destinos = so.FindProperty("destinos");
        int indice = -1;
        for (int i = 0; i < destinos.arraySize; i++)
            if (destinos.GetArrayElementAtIndex(i).FindPropertyRelative("nombre").stringValue == NombreDestinoLuchaNpcs) indice = i;
        if (indice < 0)
        {
            indice = destinos.arraySize;
            destinos.arraySize++;
        }
        Destino(destinos.GetArrayElementAtIndex(indice), NombreDestinoLuchaNpcs,
            "Oleadas de 2, 3, muchos y mezcla; escudos de colores que se rompen con su mismo color.", punto);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
