using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reparto con nombre y cara fija: los personajes del guion que no salen del generador al azar
/// (Aldona, Will de niño, y los que vengan). Cada uno es una entrada de datos: piezas del pack
/// modular que lleva encendidas, identidad, si es un niño, diálogo al hablarle y, si vive en una
/// escena con reparto, su sitio en el roster. El menú es
/// idempotente: vuelve a aplicar la entrada sobre el prefab si ya existe (para retocar la cara se
/// cambian las piezas y se vuelve a ejecutar). Usa la creación de <see cref="GeneradorDeNpcs"/>.
/// </summary>
public static class PersonajesConAspectoFijo
{
    private const string BaseCompleta = "Assets/_NPCs/Sofia.prefab";
    private const string CarpetaDialogos = "Assets/_DIALOGUES/DIALOGUE NPCS";

    private sealed class Personaje
    {
        public string Nombre, Ruta, IdPersonaje, IdPersistencia, Voz, Roster, SpawnId;
        public string[] Piezas;
        public string[] Lineas; // claves de texto del diálogo al hablarle (dialogues_*.json); vacío = sin diálogo
        public bool Pasea = true;
        public bool EsNino;
    }

    private static IEnumerable<Personaje> Reparto()
    {
        yield return new Personaje
        {
            Nombre = "Aldona",
            Ruta = "Assets/_NPCs/Pueblo/Aldona.prefab",
            IdPersonaje = "CHAR_ALDONA",
            IdPersistencia = "npc_aldona",
            Voz = "Vecina",
            Roster = "Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset",
            SpawnId = "SPAWN_Aldona",
            // Anciana: cara de mujer, pelo cano, ropa sencilla y toquilla.
            Piezas = new[] { "Head02_Female", "Hair13", "Body10", "Cloak01", "Eye07", "Mouth03" },
            Lineas = new[] { "DLG_ALDONA_01", "DLG_ALDONA_02" },
            Pasea = false,
        };
        yield return new Personaje
        {
            // Will con 8 y 9 años, para los recuerdos de la Prueba de Will: la cara y el pelo de _WILL,
            // con ropa de crío (camiseta naranja y pantalón corto) para que no parezca el Will de ahora en pequeño.
            Nombre = "WillNino",
            Ruta = "Assets/_NPCs/Recuerdos/WillNino.prefab",
            IdPersonaje = "CHAR_WILL",
            IdPersistencia = "actor_will_nino",
            Piezas = new[] { "Head01_Male", "Hair01", "Body03", "Eye01", "Mouth02" },
            Pasea = false,
            EsNino = true,
        };
    }

    [MenuItem("El Sendero/NPCs/Montar el reparto con aspecto fijo (Aldona, Will niño…)")]
    public static void Montar()
    {
        var hechos = new List<string>();
        foreach (var p in Reparto())
        {
            Crear(p, out bool enRoster);
            hechos.Add($"{p.Nombre}: prefab {p.Ruta}{(enRoster ? $", roster {p.SpawnId}" : "")}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Reparto] " + string.Join(" · ", hechos));
    }

    /// <summary>Crea o rehace el prefab de un personaje del reparto por su nombre y lo devuelve.</summary>
    public static GameObject Asegurar(string nombre)
    {
        foreach (var p in Reparto())
            if (p.Nombre == nombre) return Crear(p, out _);
        throw new System.ArgumentException($"No hay nadie llamado {nombre} en el reparto con aspecto fijo.");
    }

    private static GameObject Crear(Personaje p, out bool enRoster)
    {
        var dialogo = p.Lineas != null && p.Lineas.Length > 0 ? Dialogo(p) : null;
        var prefab = GeneradorDeNpcs.CrearDesdePlantilla(BaseCompleta, p.Ruta, p.Nombre, null, raiz =>
        {
            GeneradorDeNpcs.SeleccionarPiezas(raiz, p.Piezas);
            Identidad(raiz, p, dialogo);
            if (p.EsNino && raiz.GetComponent<TallaDeNino>() == null) raiz.AddComponent<TallaDeNino>();
        });
        enRoster = !string.IsNullOrEmpty(p.Roster) && AsegurarEnRoster(p, prefab);
        return prefab;
    }

    private static void Identidad(GameObject raiz, Personaje p, DialogueAsset dialogo)
    {
        var manager = raiz.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("persistenceId").stringValue = p.IdPersistencia;
            so.FindProperty("dialogueCharacterId").stringValue = p.IdPersonaje;
            so.FindProperty("disableWander").boolValue = !p.Pasea;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var interactable = raiz.GetComponent<Interactable>();
        if (interactable != null)
        {
            var so = new SerializedObject(interactable);
            so.FindProperty("dialogueCharacterId").stringValue = p.IdPersonaje;
            so.FindProperty("dialogue").objectReferenceValue = dialogo;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Sin frases no hay nada que decirle: no se le puede hablar.
            if (dialogo == null) interactable.enabled = false;
        }
    }

    /// <summary>Diálogo de cuando se le habla, una línea por clave.</summary>
    private static DialogueAsset Dialogo(Personaje p)
    {
        string carpeta = $"{CarpetaDialogos}/{p.Nombre}";
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder(CarpetaDialogos, p.Nombre);
        string ruta = $"{carpeta}/DLG_{p.Nombre.ToUpperInvariant()}.asset";
        var dialogo = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (dialogo == null)
        {
            dialogo = ScriptableObject.CreateInstance<DialogueAsset>();
            AssetDatabase.CreateAsset(dialogo, ruta);
        }
        dialogo.lines = new DialogueLine[p.Lineas.Length];
        for (int i = 0; i < p.Lineas.Length; i++)
            dialogo.lines[i] = new DialogueLine { speakerNameId = p.IdPersonaje, textId = p.Lineas[i], emotion = NPCEmotion.None };
        EditorUtility.SetDirty(dialogo);
        return dialogo;
    }

    /// <summary>Añade (o actualiza) su entrada en el roster de la escena. El NpcSpawnPoint con ese id tiene que estar en la escena.</summary>
    private static bool AsegurarEnRoster(Personaje p, GameObject prefab)
    {
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>(p.Roster);
        if (roster == null) { Debug.LogWarning($"[Reparto] No se encontró el roster {p.Roster}."); return false; }
        var so = new SerializedObject(roster);
        var entradas = so.FindProperty("entries");
        SerializedProperty entrada = null;
        for (int i = 0; i < entradas.arraySize; i++)
            if (entradas.GetArrayElementAtIndex(i).FindPropertyRelative("spawnId").stringValue == p.SpawnId)
                entrada = entradas.GetArrayElementAtIndex(i);
        if (entrada == null)
        {
            entradas.arraySize++;
            entrada = entradas.GetArrayElementAtIndex(entradas.arraySize - 1);
            entrada.FindPropertyRelative("enabled").boolValue = true;
            entrada.FindPropertyRelative("startActive").boolValue = true;
            entrada.FindPropertyRelative("requiredFlag").stringValue = "";
            entrada.FindPropertyRelative("invertCondition").boolValue = false;
        }
        entrada.FindPropertyRelative("spawnId").stringValue = p.SpawnId;
        entrada.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entrada.FindPropertyRelative("gameObjectName").stringValue = p.Nombre;
        entrada.FindPropertyRelative("persistenceId").stringValue = p.IdPersistencia;
        entrada.FindPropertyRelative("vozDeReacciones").stringValue = p.Voz;
        // Quien no pasea no necesita NavMesh: se queda en su sitio sin pedir caminos (como la tabernera).
        entrada.FindPropertyRelative("requireNavMesh").boolValue = p.Pasea;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(roster);
        return true;
    }
}
