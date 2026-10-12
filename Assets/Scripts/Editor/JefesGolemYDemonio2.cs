using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using M = MomentoDeCombate;

/// Monta lo de INC-723 en los assets (idempotente, no pisa lo que ya esté puesto):
///  - Gólem: FuegoQueAlimenta (el fuego de frente le hace crecer), con su llamarada.
///  - Demonio 2: AroDeTresRunas (una runa por personaje) y la guerra en el aire en su IA.
///  - Guion de Estela en el Gólem: fuego de frente, barro y pista del barro.
///  - Guion nuevo de Estela para el Demonio 2, puesto en Encuentro_Demonio2.
/// Pasar a El Sendero/Archivo/ tras ejecutarlo.
public static class JefesGolemYDemonio2
{
    const string Golem = "Assets/Prefabs/Enemy/PBR_Golem.prefab";
    const string Demonio2 = "Assets/Prefabs/Enemy/Demon2.prefab";
    const string CarpetaGuiones = "Assets/BOSSBATTLES/Guiones";
    const string GuionGolem = CarpetaGuiones + "/Guion_Golem1_Estela.asset";
    const string GuionDemonio2 = CarpetaGuiones + "/Guion_Demonio2_Estela.asset";
    const string EncuentroDemonio2 = "Assets/BOSSBATTLES/Encuentro_Demonio2.asset";
    const string Vfx = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/";

    [MenuItem("El Sendero/Combate/Jefes: Gólem con fuego y barro, Demonio 2 con tres runas y aire (INC-723)")]
    public static void Aplicar()
    {
        var informe = new List<string>();

        EditarPrefab(Golem, informe, raiz =>
        {
            var cuerpo = raiz.GetComponentInChildren<Damageable>(true).gameObject;
            var fuego = Asegurar<FuegoQueAlimenta>(cuerpo, out bool nuevo);
            var so = new SerializedObject(fuego);
            PonerSiVacio(so, "vfxAlCrecer", AssetDatabase.LoadAssetAtPath<GameObject>(Vfx + "Explosion.prefab"));
            so.ApplyModifiedPropertiesWithoutUndo();
            return nuevo ? "fuego que le alimenta añadido" : "fuego que le alimenta ya estaba";
        });

        EditarPrefab(Demonio2, informe, raiz =>
        {
            var cuerpo = raiz.GetComponentInChildren<Damageable>(true).gameObject;
            var aro = Asegurar<AroDeTresRunas>(cuerpo, out bool nuevo);
            var so = new SerializedObject(aro);
            PonerSiVacio(so, "vfxRuptura", AssetDatabase.LoadAssetAtPath<GameObject>(Vfx + "Star hit.prefab"));
            PonerSiVacio(so, "vfxRecomponer", AssetDatabase.LoadAssetAtPath<GameObject>(Vfx + "Holy hit.prefab"));
            so.ApplyModifiedPropertiesWithoutUndo();

            string aire = "sin ImpDemonAI";
            var ia = raiz.GetComponentInChildren<ImpDemonAI>(true);
            if (ia != null)
            {
                var soIA = new SerializedObject(ia);
                var p = soIA.FindProperty("guerraEnElAire");
                aire = p != null && p.boolValue ? "guerra en el aire ya estaba" : "guerra en el aire activada";
                if (p != null) p.boolValue = true;
                soIA.ApplyModifiedPropertiesWithoutUndo();
            }
            return $"aro de tres runas {(nuevo ? "añadido" : "ya estaba")}, {aire}";
        });

        // Guion del Gólem: se añade lo que falte (por clave), sin tocar lo demás.
        var guionGolem = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(GuionGolem);
        if (guionGolem == null) informe.Add($"⚠ {GuionGolem}: no existe (pasar antes el menú de INC-489).");
        else
        {
            int n = 0;
            n += Anadir(guionGolem, new(M.Inicio, "JEFES_GOLEM_BARRO_PISTA", null));
            n += Anadir(guionGolem, new(M.JefeSeFortalece, "JEFES_GOLEM_FUEGO_01", null, interrumpe: true, duracion: 3f));
            n += Anadir(guionGolem, new(M.JefeSeFortalece, "JEFES_GOLEM_FUEGO_02", null, vecesMax: 2, enfriamiento: 15f, caducidad: 2f));
            n += Anadir(guionGolem, new(M.JefeAtrapado, "JEFES_GOLEM_BARRO_01", null, interrumpe: true, duracion: 2.5f));
            n += Anadir(guionGolem, new(M.JefeAtrapado, "JEFES_GOLEM_BARRO_02", null, vecesMax: 0, enfriamiento: 12f, caducidad: 1.5f, interrumpe: true, duracion: 2f));
            n += Anadir(guionGolem, new(M.SinAcertar, "JEFES_GOLEM_RECUERDA_BARRO", null, vecesMax: 2, enfriamiento: 25f, caducidad: 3f));
            if (n > 0) EditorUtility.SetDirty(guionGolem);
            informe.Add(n > 0 ? $"✓ {GuionGolem}: {n} frases nuevas (fuego y barro)." : $"{GuionGolem}: ya tenía las frases.");
        }

        // Guion del Demonio 2
        if (!AssetDatabase.IsValidFolder(CarpetaGuiones))
            AssetDatabase.CreateFolder("Assets/BOSSBATTLES", "Guiones");
        var guionDemonio2 = AssetDatabase.LoadAssetAtPath<GuionDeCombate>(GuionDemonio2);
        if (guionDemonio2 == null)
        {
            guionDemonio2 = ScriptableObject.CreateInstance<GuionDeCombate>();
            LlenarDemonio2(guionDemonio2);
            AssetDatabase.CreateAsset(guionDemonio2, GuionDemonio2);
            informe.Add($"✓ {GuionDemonio2} creado.");
        }
        else informe.Add($"{GuionDemonio2}: ya existía (no se toca).");

        var encuentro = AssetDatabase.LoadAssetAtPath<BattleEncounterSO>(EncuentroDemonio2);
        if (encuentro == null) informe.Add($"⚠ {EncuentroDemonio2}: no existe (lo crea «Crear o regenerar CombatLab»); vuelve a pasar este menú después.");
        else if (encuentro.guion == null)
        {
            encuentro.guion = guionDemonio2;
            EditorUtility.SetDirty(encuentro);
            informe.Add("✓ Encuentro_Demonio2: guía de Estela.");
        }
        else informe.Add("Encuentro_Demonio2: ya tenía guion (no se toca).");

        AssetDatabase.SaveAssets();
        Debug.Log("[JefesGolemYDemonio2]\n- " + string.Join("\n- ", informe));
    }

    static void LlenarDemonio2(GuionDeCombate g)
    {
        g.hablanteId = "NPC_Estela";
        g.nombreHablante = "Estela";
        g.mirarAlJugador = false; // pelea a la vez
        g.segundosSinAcertar = 18f;
        g.comentarios = new List<ComentarioDeCombate>
        {
            new(M.Inicio, "JEFES_DEMONIO2_INICIO_01", null),
            new(M.Inicio, "JEFES_DEMONIO2_INICIO_02", null, duracion: 4f),
            new(M.DefensaRota, "JEFES_DEMONIO2_RUNA_ROTA", null, vecesMax: 0, enfriamiento: 4f, caducidad: 2f, interrumpe: true, duracion: 2f),
            new(M.GolpeMalDado, "JEFES_DEMONIO2_YA_ROTA", null, vecesMax: 3, enfriamiento: 10f, caducidad: 2f, interrumpe: true),
            new(M.JefeExpuesto, "JEFES_DEMONIO2_EXPUESTO", null, vecesMax: 0, enfriamiento: 8f, caducidad: 1.5f, interrumpe: true, duracion: 2f),
            new(M.DefensaRecompuesta, "JEFES_DEMONIO2_RECOMPUESTO", null, vecesMax: 2, enfriamiento: 20f, caducidad: 2f),
            new(M.GolpeValido, "EVT_ESTELA_GOLEM_ACIERTO", null),
            new(M.GolpeValido, "EVT_ESTELA_GOLEM_ANIMO", null, vecesMax: 0, enfriamiento: 14f, caducidad: 2f),
            new(M.JugadorPocaVida, "EVT_ESTELA_GOLEM_POCA_VIDA", null, vecesMax: 2, enfriamiento: 30f, caducidad: 4f),
            new(M.OrbesSueltos, "EVT_ESTELA_GOLEM_ORBES", null, caducidad: 6f),
            new(M.CambioDeFase, "JEFES_DEMONIO2_AIRE", null, interrumpe: true, duracion: 3.5f, fase: 1),
            new(M.CambioDeFase, "JEFES_DEMONIO2_BAJA", null, interrumpe: true, duracion: 3.5f, fase: 2),
            new(M.JefeCasiVencido, "EVT_ESTELA_GOLEM_CASI", null, caducidad: 4f),
            new(M.SinAcertar, "JEFES_DEMONIO2_RECUERDA", null, vecesMax: 2, enfriamiento: 25f, caducidad: 3f),
        };
    }

    // ── Utilidades ────────────────────────────────────────────────────────

    /// Añade el comentario si su clave no está; los de Inicio van tras el último de Inicio.
    static int Anadir(GuionDeCombate g, ComentarioDeCombate c)
    {
        g.comentarios ??= new List<ComentarioDeCombate>();
        foreach (var x in g.comentarios) if (x != null && x.key == c.key) return 0;
        if (c.momento == M.Inicio)
        {
            int ultimo = -1;
            for (int i = 0; i < g.comentarios.Count; i++) if (g.comentarios[i] != null && g.comentarios[i].momento == M.Inicio) ultimo = i;
            g.comentarios.Insert(ultimo + 1, c);
        }
        else g.comentarios.Add(c);
        return 1;
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

    static void EditarPrefab(string ruta, List<string> informe, System.Func<GameObject, string> cambio)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ruta) == null)
        {
            informe.Add($"⚠ {ruta}: no existe.");
            return;
        }
        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            if (raiz.GetComponentInChildren<Damageable>(true) == null)
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
