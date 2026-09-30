using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using M = MomentoDeCombate;

/// Monta lo de INC-489 en los assets (idempotente, no pisa lo que ya esté puesto):
///  - TransicionDeFaseDeJefe en los tres jefes (Demonio, Demonio 2, Gólem), con la onda del Gólem.
///  - La cámara de presentación de los tres jefes, apagada en el prefab (la enciende la presentación).
///  - Gólem: ArmaduraFrontal (daño completo solo por la espalda) y RemateObligatorio (al 10 % solo
///    cae con un dúo, con un círculo mágico de sello).
///  - SenueloDeCombate en Estela y Liam (pueden provocar al Gólem sin recibir daño).
///  - Guiones de combate: Eldran en el Demonio 1 (las frases de siempre) y Estela en el Gólem.
///  - Encuentro del Gólem (Encuentro_Golem1) y guion en el del Demonio 1 y en el laboratorio.
///  - Quita la guía antigua de los prefabs de Eldran: ahora la arranca la arena.
/// Pasar a El Sendero/Archivo/ tras ejecutarlo.
public static class JefesMontarGuiasYGolem
{
    const string Demonio1 = "Assets/Prefabs/Enemy/Demon.prefab";
    const string Demonio2 = "Assets/Prefabs/Enemy/Demon2.prefab";
    const string Golem = "Assets/Prefabs/Enemy/PBR_Golem.prefab";
    const string Estela = "Assets/Prefabs/_ESTELA.prefab";
    const string Liam = "Assets/Prefabs/_LIAM.prefab";
    static readonly string[] Eldrans = { "Assets/_NPCs/Eldran.prefab", "Assets/_NPCs/_GrafoNarrativo/Eldran.prefab" };
    const string CarpetaGuiones = "Assets/BOSSBATTLES/Guiones";
    const string EncuentroDemonio1 = "Assets/BOSSBATTLES/Encuentro_Demonio1.asset";
    const string EncuentroGolem = "Assets/BOSSBATTLES/Encuentro_Golem1.asset";
    const string CirculoSello = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab";
    const string ConfigLab = "Assets/Resources/CombatLab/CombatLabConfig.asset";

    [MenuItem("El Sendero/Combate/Jefes: montar guías, transición de fase y Gólem (INC-489)")]
    public static void Aplicar()
    {
        var informe = new List<string>();

        var golemAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Golem);
        var golemIA = golemAsset != null ? golemAsset.GetComponentInChildren<GolemBossAI>(true) : null;
        var golemSO = golemIA != null ? new SerializedObject(golemIA) : null;
        Object onda = golemSO?.FindProperty("shockwaveVFX")?.objectReferenceValue;
        Object polvo = golemSO?.FindProperty("rockPickupVFX")?.objectReferenceValue;
        float desfase = golemSO?.FindProperty("modelRotationOffset")?.floatValue ?? 0f;
        var circulo = AssetDatabase.LoadAssetAtPath<GameObject>(CirculoSello);

        // 1. Transición compartida en los tres jefes
        foreach (var ruta in new[] { Demonio1, Demonio2, Golem })
            EditarPrefab(ruta, informe, raiz =>
            {
                var t = Asegurar<TransicionDeFaseDeJefe>(raiz.GetComponentInChildren<Damageable>(true)?.gameObject ?? raiz, out bool nuevo);
                var so = new SerializedObject(t);
                PonerSiVacio(so, "vfxOnda", onda);
                so.ApplyModifiedPropertiesWithoutUndo();
                return nuevo ? "transición de fase añadida" : "transición de fase ya estaba";
            });

        // 2. La cámara de presentación de cada jefe va apagada en el prefab: solo la enciende la
        //    presentación del jefe. Encendida pinta encima de la del jugador (más profundidad) en
        //    cuanto el jefe aparece, y se queda ahí donde no hay presentación. Ver INC-490.
        foreach (var ruta in new[] { Demonio1, Demonio2, Golem })
            EditarPrefab(ruta, informe, raiz =>
            {
                var camara = raiz.GetComponentInChildren<Camera>(true);
                if (camara == null) return "sin cámara de presentación";
                if (camara.gameObject == raiz) return "⚠ la cámara está en la raíz: apagarla a mano";
                if (!camara.gameObject.activeSelf) return "cámara de presentación ya apagada";
                camara.gameObject.SetActive(false);
                return "cámara de presentación apagada";
            });

        // 3. Gólem: coraza frontal y remate obligatorio (en este orden: el remate va después)
        EditarPrefab(Golem, informe, raiz =>
        {
            var cuerpo = raiz.GetComponentInChildren<Damageable>(true).gameObject;
            var armadura = Asegurar<ArmaduraFrontal>(cuerpo, out bool a);
            var so = new SerializedObject(armadura);
            if (a) so.FindProperty("desfaseFrente").floatValue = desfase;
            PonerSiVacio(so, "vfxRebote", polvo);
            so.ApplyModifiedPropertiesWithoutUndo();

            var remate = Asegurar<RemateObligatorio>(cuerpo, out bool r);
            var so2 = new SerializedObject(remate);
            PonerSiVacio(so2, "vfxSello", circulo);
            so2.ApplyModifiedPropertiesWithoutUndo();
            return $"coraza frontal {(a ? "añadida" : "ya estaba")}, remate con dúo {(r ? "añadido" : "ya estaba")}";
        });

        // 4. Estela y Liam pueden hacer de señuelo
        foreach (var ruta in new[] { Estela, Liam })
            EditarPrefab(ruta, informe, raiz =>
            {
                Asegurar<SenueloDeCombate>(raiz, out bool nuevo);
                return nuevo ? "señuelo añadido" : "señuelo ya estaba";
            }, necesitaIA: false);

        // 5. La guía antigua de Eldran sale de sus prefabs
        foreach (var ruta in Eldrans)
            EditarPrefab(ruta, informe, raiz =>
            {
                int n = 0;
                foreach (var g in raiz.GetComponentsInChildren<GuiaDeCombate>(true)) { Object.DestroyImmediate(g, true); n++; }
                return n > 0 ? "guía antigua quitada (ahora la arranca la arena)" : "sin guía antigua";
            }, necesitaIA: false);

        // 6. Guiones
        if (!AssetDatabase.IsValidFolder(CarpetaGuiones))
            AssetDatabase.CreateFolder("Assets/BOSSBATTLES", "Guiones");
        var guionEldran = CrearGuion($"{CarpetaGuiones}/Guion_Demonio1_Eldran.asset", LlenarEldran, informe);
        var guionEstela = CrearGuion($"{CarpetaGuiones}/Guion_Golem1_Estela.asset", LlenarEstela, informe);

        // 7. Encuentros y laboratorio
        var demonio1 = AssetDatabase.LoadAssetAtPath<BattleEncounterSO>(EncuentroDemonio1);
        if (demonio1 != null && demonio1.guion == null)
        {
            demonio1.guion = guionEldran;
            EditorUtility.SetDirty(demonio1);
            informe.Add("✓ Encuentro_Demonio1: guion de Eldran.");
        }

        var golem = AssetDatabase.LoadAssetAtPath<BattleEncounterSO>(EncuentroGolem);
        if (golem == null && golemAsset != null)
        {
            golem = ScriptableObject.CreateInstance<BattleEncounterSO>();
            golem.enemyPrefab = golemAsset;
            golem.displayName = "GÓLEM";
            golem.arenaRadius = 25f;
            if (demonio1 != null)
            {
                golem.floorLayer = demonio1.floorLayer;
                golem.spawnProfiles = demonio1.spawnProfiles;
            }
            golem.premioEstadisticas = new Estadisticas(15f, 10f, 3f, 3f);
            golem.guion = guionEstela;
            AssetDatabase.CreateAsset(golem, EncuentroGolem);
            informe.Add("✓ Encuentro_Golem1 creado (premio +15 vida, +10 magia, +3 ataque, +3 defensa; guía Estela).");
        }

        var lab = AssetDatabase.LoadAssetAtPath<CombatLabConfig>(ConfigLab);
        if (lab != null && lab.guionJefe == null)
        {
            lab.guionJefe = guionEstela;
            EditorUtility.SetDirty(lab);
            informe.Add("✓ Laboratorio de combate: guía de Estela en la estación del jefe.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[JefesMontarGuiasYGolem]\n- " + string.Join("\n- ", informe));
    }

    // ── Contenido de los guiones ──────────────────────────────────────────

    static void LlenarEldran(GuionDeCombate g)
    {
        g.hablanteId = "NPC_Eldran";
        g.nombreHablante = "Eldran";
        g.mirarAlJugador = true;
        g.comentarios = new List<ComentarioDeCombate>
        {
            new(M.Inicio, "EVT_10", "FoundSomething_NoWeapon"),
            new(M.Inicio, "EVT_08", "Challenging_NoWeapon"),
            new(M.Inicio, "EVT_ELDRAN_HINT_ARO", "FoundSomething_NoWeapon"),
            new(M.Inicio, "EVT_ELDRAN_GUIA_RITMO", "Cheer02"),
            new(M.JefeExpuesto, "EVT_ELDRAN_AHORA_01", "Challenging_NoWeapon", vecesMax: 2, caducidad: 0.8f, interrumpe: true, duracion: 1.6f),
            new(M.JefeExpuesto, "EVT_ELDRAN_AHORA_02", "Challenging_NoWeapon", vecesMax: 1, caducidad: 0.8f, interrumpe: true, duracion: 1.6f),
            new(M.GolpeValido, "EVT_ELDRAN_ACIERTO_01", "Cheer01"),
            new(M.GolpeValido, "EVT_ELDRAN_CHEER_01", "Cheer01", vecesMax: 0, enfriamiento: 12f, caducidad: 2f),
            new(M.GolpeValido, "EVT_ELDRAN_CHEER_02", "Cheer01", vecesMax: 0, enfriamiento: 12f, caducidad: 2f),
            new(M.GolpeMalDado, "EVT_ELDRAN_HINT_SIN_ARO", "HeadShake01", interrumpe: true),
            new(M.GolpeMalDado, "EVT_ELDRAN_SIN_ARO_02", "HeadShake01", enfriamiento: 20f, caducidad: 3f),
            new(M.JugadorHerido, "EVT_ELDRAN_WILL_HERIDO", "Challenging_NoWeapon", caducidad: 4f),
            new(M.JugadorPocaVida, "EVT_ELDRAN_POCA_VIDA", "FoundSomething_NoWeapon", vecesMax: 2, enfriamiento: 30f, caducidad: 4f),
            new(M.OrbesSueltos, "EVT_ELDRAN_HINT_ORBES", "FoundSomething_NoWeapon", caducidad: 6f),
            new(M.CambioDeFase, "EVT_ELDRAN_FASE_2", "FoundSomething_NoWeapon", interrumpe: true, duracion: 3.5f, fase: 1),
            new(M.CambioDeFase, "EVT_ELDRAN_FASE_3", "FoundSomething_NoWeapon", interrumpe: true, duracion: 3.5f, fase: 2),
            new(M.JefeCasiVencido, "EVT_ELDRAN_CASI", "Cheer02", caducidad: 4f),
            new(M.SinAcertar, "EVT_ELDRAN_RECUERDA_ARO", "HeadShake01", vecesMax: 2, enfriamiento: 25f, caducidad: 3f),
        };
    }

    static void LlenarEstela(GuionDeCombate g)
    {
        g.hablanteId = "NPC_Estela";
        g.nombreHablante = "Estela";
        g.mirarAlJugador = false; // pelea: si se la girara a mano se le rompería el movimiento
        g.umbralJefeCasiVencido = 0.2f;
        g.comentarios = new List<ComentarioDeCombate>
        {
            new(M.Inicio, "EVT_ESTELA_GOLEM_01", null),
            new(M.Inicio, "EVT_ESTELA_GOLEM_02", null),
            new(M.JefeVaAPorElHablante, "EVT_ESTELA_GOLEM_PROVOCA_01", null, vecesMax: 0, enfriamiento: 8f, caducidad: 1.5f, interrumpe: true, duracion: 2.2f),
            new(M.JefeVaAPorElHablante, "EVT_ESTELA_GOLEM_PROVOCA_02", null, vecesMax: 0, enfriamiento: 8f, caducidad: 1.5f, interrumpe: true, duracion: 2.2f),
            new(M.JefeVaAPorElJugador, "EVT_ESTELA_GOLEM_VUELVE", null, vecesMax: 2, enfriamiento: 12f, caducidad: 2f),
            new(M.GolpeMalDado, "EVT_ESTELA_GOLEM_REBOTA_01", null, interrumpe: true),
            new(M.GolpeMalDado, "EVT_ESTELA_GOLEM_REBOTA_02", null, vecesMax: 2, enfriamiento: 15f, caducidad: 2f),
            new(M.GolpeValido, "EVT_ESTELA_GOLEM_ACIERTO", null),
            new(M.GolpeValido, "EVT_ESTELA_GOLEM_ANIMO", null, vecesMax: 0, enfriamiento: 14f, caducidad: 2f),
            new(M.JugadorHerido, "EVT_ESTELA_GOLEM_HERIDO", null, caducidad: 4f),
            new(M.JugadorPocaVida, "EVT_ESTELA_GOLEM_POCA_VIDA", null, vecesMax: 2, enfriamiento: 30f, caducidad: 4f),
            new(M.OrbesSueltos, "EVT_ESTELA_GOLEM_ORBES", null, caducidad: 6f),
            new(M.CambioDeFase, "EVT_ESTELA_GOLEM_FASE_2", null, interrumpe: true, duracion: 3.5f, fase: 1),
            new(M.CambioDeFase, "EVT_ESTELA_GOLEM_FASE_3", null, interrumpe: true, duracion: 3.5f, fase: 2),
            new(M.SePideRemate, "EVT_ESTELA_GOLEM_REMATE", null, interrumpe: true, duracion: 5f),
            new(M.JefeCasiVencido, "EVT_ESTELA_GOLEM_CASI", null, caducidad: 4f),
            new(M.SinAcertar, "EVT_ESTELA_GOLEM_RECUERDA", null, vecesMax: 2, enfriamiento: 25f, caducidad: 3f),
        };
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    static GuionDeCombate CrearGuion(string ruta, System.Action<GuionDeCombate> llenar, List<string> informe)
    {
        var g = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(ruta);
        if (g != null) { informe.Add($"{ruta}: ya existía (no se toca)."); return g; }
        g = ScriptableObject.CreateInstance<GuionDeCombate>();
        llenar(g);
        AssetDatabase.CreateAsset(g, ruta);
        informe.Add($"✓ {ruta} creado.");
        return g;
    }

    static T Asegurar<T>(GameObject go, out bool nuevo) where T : Component
    {
        var c = go.GetComponent<T>();
        nuevo = c == null;
        return nuevo ? go.AddComponent<T>() : c;
    }

    static void PonerSiVacio(SerializedObject so, string campo, Object valor)
    {
        var p = so.FindProperty(campo);
        if (p != null && p.objectReferenceValue == null && valor != null) p.objectReferenceValue = valor;
    }

    static void EditarPrefab(string ruta, List<string> informe, System.Func<GameObject, string> cambio, bool necesitaIA = true)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
        {
            informe.Add($"⚠ {ruta}: no existe.");
            return;
        }
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            if (necesitaIA && raiz.GetComponentInChildren<Damageable>(true) == null)
            {
                informe.Add($"⚠ {ruta}: no tiene Damageable.");
                return;
            }
            string hecho = cambio(raiz);
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
            informe.Add($"✓ {ruta}: {hecho}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }
}
