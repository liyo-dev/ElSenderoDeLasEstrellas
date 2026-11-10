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
/// La base (Sofía) trae su misión y su narrativa interactiva: aquí se quitan siempre, para que nadie
/// herede encargos ajenos. Lo que haga en el mundo (pasear, sentarse, un oficio) va en su
/// <see cref="Game.NPC.Modules.NPCAmbientConfig"/> propio, que se crea con el personaje.
/// Tras crear o rehacer prefabs hay que pasar *Caras de los NPCs* (las piezas de cara se reinician).
/// </summary>
public static class PersonajesConAspectoFijo
{
    private const string BaseCompleta = "Assets/_NPCs/Sofia.prefab";
    private const string CarpetaDialogos = "Assets/_DIALOGUES/DIALOGUE NPCS";
    private const string CarpetaAmbiente = "Assets/_NPCs/Ambient";

    private sealed class Personaje
    {
        public string Nombre, Ruta, IdPersonaje, IdPersistencia, Voz, Roster, SpawnId;
        public string[] Piezas;
        public string[] Lineas; // claves de texto del diálogo al hablarle (dialogues_*.json); vacío = sin diálogo
        public bool Pasea = true;
        public bool EsNino;
        /// <summary>Prefab del que se copia el comportamiento (p. ej. una tienda); vacío = ninguno.</summary>
        public string Plantilla;
        /// <summary>Lo que hace parado (sentarse…); requiere <see cref="Pasea"/> = false.</summary>
        public NPCAmbientActivity Actividad = NPCAmbientActivity.None;
        /// <summary>Oficio: el puestoId de su <see cref="NPCWorkRoute"/>; vacío = sin oficio.</summary>
        public string Puesto;
        public float RadioDePaseo = 6f;
        public bool BuscaRefugio = true;
        public bool TieneAmbiente => Pasea || Actividad != NPCAmbientActivity.None || !string.IsNullOrEmpty(Puesto);
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

        // ── La aldea del mar (pueblo pesquero). Su sitio y lo que cuentan, en MontajeDeLosCapitulos.AldeaDelMar.
        const string Aldea = "Assets/_NPCs/AldeaDelMar/";
        yield return new Personaje
        {
            // El posadero: pelo castaño, bigote y ropa de faena marrón.
            Nombre = "Posadero", Ruta = Aldea + "Posadero.prefab", IdPersonaje = "CHAR_POSADERO", IdPersistencia = "NPC_Posadero",
            Piezas = new[] { "Head01_Male", "Hair02", "Body05", "AC10_Mustache02", "Eye01", "Mouth02" },
            Lineas = new[] { "DLG_POSADERO_01" }, Pasea = false,
        };
        yield return new Personaje
        {
            // Pescadora: lleva las cajas del muelle al puesto de pescado.
            Nombre = "Remedios", Ruta = Aldea + "Remedios.prefab", IdPersonaje = "CHAR_REMEDIOS", IdPersistencia = "NPC_Remedios", Voz = "Vecina",
            Piezas = new[] { "Head02_Female", "Hair08", "Body08", "Eye07", "Mouth03" },
            Lineas = new[] { "DLG_REMEDIOS_01" }, Pasea = false, Puesto = "puerto_remedios", BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // Pescador viejo al final del muelle, mirando el mar.
            Nombre = "Anselmo", Ruta = Aldea + "Anselmo.prefab", IdPersonaje = "CHAR_ANSELMO", IdPersistencia = "NPC_Anselmo",
            Piezas = new[] { "Head01_Male", "Hair13", "Body03", "AC11_Mustache02", "Eye01", "Mouth02" },
            Lineas = new[] { "DLG_ANSELMO_01" }, Pasea = false, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // La tendera del puerto: remedios y provisiones (la tienda de Patricia).
            Nombre = "Engracia", Ruta = Aldea + "Engracia.prefab", IdPersonaje = "CHAR_ENGRACIA", IdPersistencia = "NPC_Engracia", Voz = "Vecina",
            Piezas = new[] { "Head02_Female", "Hair11", "Body17", "Eye07", "Mouth03" },
            Lineas = new[] { "DLG_ENGRACIA_01" }, Pasea = false, BuscaRefugio = false, Plantilla = "Assets/_NPCs/Patricia.prefab",
        };
        yield return new Personaje
        {
            // Abuelo canoso sentado en el banco de la plaza.
            Nombre = "Eusebio", Ruta = Aldea + "Eusebio.prefab", IdPersonaje = "CHAR_EUSEBIO", IdPersistencia = "NPC_Eusebio",
            Piezas = new[] { "Head01_Male", "Hair13", "Body13", "Eye01", "Mouth02" },
            Lineas = new[] { "DLG_EUSEBIO_01" }, Pasea = false, Actividad = NPCAmbientActivity.SitMedium, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // Vecino que pasea por la plaza.
            Nombre = "Fermin", Ruta = Aldea + "Fermin.prefab", IdPersonaje = "CHAR_FERMIN", IdPersistencia = "NPC_Fermin",
            Piezas = new[] { "Head01_Male", "Hair12", "Body15", "Eye01", "Mouth02" },
            Lineas = new[] { "DLG_FERMIN_01" }, Pasea = true, RadioDePaseo = 8f,
        };
        yield return new Personaje
        {
            // Pescador comiendo en la terraza de la posada.
            Nombre = "Benito", Ruta = Aldea + "Benito.prefab", IdPersonaje = "CHAR_BENITO", IdPersistencia = "NPC_Benito",
            Piezas = new[] { "Head01_Male", "Hair11", "Body03", "AC11_Mustache02", "Eye01", "Mouth02" },
            Lineas = new[] { "DLG_BENITO_01" }, Pasea = false, Actividad = NPCAmbientActivity.SitMedium, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // Vecina comiendo en la terraza de la posada, enfrente de Benito.
            Nombre = "Rosario", Ruta = Aldea + "Rosario.prefab", IdPersonaje = "CHAR_ROSARIO", IdPersistencia = "NPC_Rosario", Voz = "Vecina",
            Piezas = new[] { "Head02_Female", "Hair02", "Body05", "Eye07", "Mouth03" },
            Lineas = new[] { "DLG_ROSARIO_01" }, Pasea = false, Actividad = NPCAmbientActivity.SitMedium, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // Lucía, la niña de la posada (la que intenta subir la cuchara): coletas rubias y ropa de diario.
            // Su cuerpo de niña (talla pequeña) y sus caras los pone NinosHechosEnBlender.
            Nombre = "Lucia", Ruta = Aldea + "Lucia.prefab", IdPersonaje = "CHAR_LUCIA", IdPersistencia = "NPC_Lucia", Voz = "Vecina",
            Piezas = new[] { "Head02_Female", "Hair08", "Body04", "Eye07", "Mouth03" },
            Lineas = new[] { "DLG_LUCIA_01" }, Pasea = false, BuscaRefugio = false, EsNino = true,
        };

        // ── El pueblo del prólogo: la vida de fondo de la mañana (guion Prologo_UltimaNoche). No hablan;
        // los mueve el guion. Su sitio de salida lo pone *Prólogo: vestir el pueblo*. El niño que va de la
        // mano de la madre (Lucas) tiene cuerpo de niño: lo monta RecursosDelPrologo, como a Nina.
        const string Valle = "Assets/_NPCs/NonInteractable/";
        const string RosterValle = "Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset";
        yield return new Personaje
        {
            // La panadera, detrás del mostrador del horno: moño, delantal de faena.
            Nombre = "PanaderaDelValle", Ruta = Valle + "PanaderaDelValle.prefab", IdPersonaje = "CHAR_PANADERA_VALLE",
            IdPersistencia = "PanaderaDelValle", Voz = "Vecina", Roster = RosterValle, SpawnId = "SPAWN_PanaderaDelValle",
            Piezas = new[] { "Head02_Female", "Hair11", "Body10", "Eye07", "Mouth03" }, Pasea = false, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // La madre que va a por el pan con su hijo de la mano.
            Nombre = "MadreDelValle", Ruta = Valle + "MadreDelValle.prefab", IdPersonaje = "CHAR_MADRE_VALLE",
            IdPersistencia = "MadreDelValle", Voz = "Vecina", Roster = RosterValle, SpawnId = "SPAWN_MadreDelValle",
            Piezas = new[] { "Head02_Female", "Hair08", "Body08", "Eye07", "Mouth03" }, Pasea = false, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // El vecino que abre la contraventana de su mirador y saluda al maestro.
            Nombre = "VecinoDelMirador", Ruta = Valle + "VecinoDelMirador.prefab", IdPersonaje = "CHAR_VECINO_MIRADOR",
            IdPersistencia = "VecinoDelMirador", Voz = "Vecino", Roster = RosterValle, SpawnId = "SPAWN_VecinoDelMirador",
            Piezas = new[] { "Head01_Male", "Hair12", "Body15", "AC10_Mustache02", "Eye01", "Mouth02" }, Pasea = false, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // La abuela que se asoma a su mirador con la mañana y aplaude al globo.
            Nombre = "AbuelaDelMirador", Ruta = Valle + "AbuelaDelMirador.prefab", IdPersonaje = "CHAR_ABUELA_MIRADOR",
            IdPersistencia = "AbuelaDelMirador", Voz = "Vecina", Roster = RosterValle, SpawnId = "SPAWN_AbuelaDelMirador",
            Piezas = new[] { "Head02_Female", "Hair13", "Body10", "Cloak01", "Eye07", "Mouth03" }, Pasea = false, BuscaRefugio = false,
        };
        yield return new Personaje
        {
            // La vecina joven que se asoma a ver la carreta y el globo.
            Nombre = "VecinaDelMirador", Ruta = Valle + "VecinaDelMirador.prefab", IdPersonaje = "CHAR_VECINA_MIRADOR",
            IdPersistencia = "VecinaDelMirador", Voz = "Vecina", Roster = RosterValle, SpawnId = "SPAWN_VecinaDelMirador",
            Piezas = new[] { "Head02_Female", "Hair02", "Body17", "Eye07", "Mouth03" }, Pasea = false, BuscaRefugio = false,
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
        var ambiente = p.TieneAmbiente ? Ambiente(p) : null;
        // Con plantilla se añade su comportamiento sin quitar el de la base (el NPC sigue teniendo su cerebro).
        var prefab = GeneradorDeNpcs.CrearDesdePlantilla(BaseCompleta, p.Ruta, p.Nombre, p.Plantilla, raiz =>
        {
            GeneradorDeNpcs.SeleccionarPiezas(raiz, p.Piezas);
            Identidad(raiz, p, dialogo, ambiente);
            if (p.EsNino && raiz.GetComponent<TallaDeNino>() == null) raiz.AddComponent<TallaDeNino>();
        }, quitarSobrantes: false);
        enRoster = !string.IsNullOrEmpty(p.Roster) && AsegurarEnRoster(p, prefab);
        return prefab;
    }

    private static void Identidad(GameObject raiz, Personaje p, DialogueAsset dialogo, Game.NPC.Modules.NPCAmbientConfig ambiente)
    {
        var manager = raiz.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("persistenceId").stringValue = p.IdPersistencia;
            so.FindProperty("dialogueCharacterId").stringValue = p.IdPersonaje;
            so.FindProperty("disableWander").boolValue = !p.Pasea;
            // Nada de encargos heredados de la base: misiones y narrativa van por el grafo.
            so.FindProperty("configuration.questConfig").objectReferenceValue = null;
            so.FindProperty("configuration.interactiveNarrativeConfig").objectReferenceValue = null;
            so.FindProperty("configuration.ambientConfig").objectReferenceValue = ambiente;
            so.FindProperty("configuration.behaviourType").intValue = ambiente != null ? (int)Game.NPC.Common.NPCBehaviourType.Ambient : 0;
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

    /// <summary>Su configuración de ambiente (pasear, sentarse, oficio), creada o puesta al día.</summary>
    private static Game.NPC.Modules.NPCAmbientConfig Ambiente(Personaje p)
    {
        string ruta = $"{CarpetaAmbiente}/NPC_Ambient_{p.Nombre}.asset";
        var a = AssetDatabase.LoadAssetAtPath<Game.NPC.Modules.NPCAmbientConfig>(ruta);
        if (a == null)
        {
            a = ScriptableObject.CreateInstance<Game.NPC.Modules.NPCAmbientConfig>();
            AssetDatabase.CreateAsset(a, ruta);
        }
        a.enableWander = p.Pasea;
        a.wanderRadius = p.RadioDePaseo;
        a.ambientActivity = p.Pasea ? NPCAmbientActivity.None : p.Actividad;
        a.permitirTrabajo = !string.IsNullOrEmpty(p.Puesto);
        a.puestoId = p.Puesto ?? "";
        a.canSeekShelter = p.BuscaRefugio;
        EditorUtility.SetDirty(a);
        return a;
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
