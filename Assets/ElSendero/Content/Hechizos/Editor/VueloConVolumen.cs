using System;
using System.Collections.Generic;
using Kits.StylizedVfx;
using Kits.StylizedVfx.Editor;
using UnityEditor;
using UnityEngine;
using B = Kits.StylizedVfx.Editor.StylizedVfxBuilder;
using R = Kits.StylizedVfx.Editor.EnergyRecipes;

/// Efectos del movimiento de Will con la receta «dibujo animado con volumen»: despegar, volar
/// (estela en los pies), aterrizar y planear. Sustituyen a los de los paquetes (la onda de humo al
/// despegar y las chispas amarillas en los pies) y se asignan en _WILL.prefab. Ver INC-1154
/// - Despegue y aterrizaje van por VfxPoolService (un solo uso, se apagan solos).
/// - Pies: PlayerFlyingController lo instancia en los pies y lo enciende y apaga con SetActive
///   (no hace falta un ParticleSystem en la raíz).
/// - Planeo: PlaneoDelJugador lo cuelga del personaje a «posicionDelEfecto» (0, 1,4, -0,2), así que
///   aquí se monta alrededor del origen.
/// - Compañeros: el mismo efecto de pies en sus NPCPartyConfig (FollowPlayerState lo cuelga de la raíz).
/// Ver INC-1166
public static class VueloConVolumen
{
    private const string Carpeta = "Assets/_VFX/Movimiento";
    private const string RutaWill = "Assets/Prefabs/_WILL.prefab";

    [MenuItem("El Sendero/Magia/Vuelo y planeo con volumen (INC-1154)")]
    public static void Menu()
    {
        var log = new System.Text.StringBuilder();
        Construir(log);
        Debug.Log("[VFX] Vuelo y planeo con volumen: " + log);
    }

    public static void Construir(System.Text.StringBuilder log)
    {
        if (!HechizosConVolumen.PrepararPaletas()) return;
        var m = HechizosConVolumen.Mallas;
        var cielo = HechizosConVolumen.Paleta("Cielo");
        var luz = HechizosConVolumen.Paleta("Luz");
        var viento = HechizosConVolumen.Paleta("Viento");

        // Despegar: aro de luz que se abre bajo los pies y estrellitas que suben con él.
        Rehacer("VFX_Vuelo_Despegue", r =>
        {
            var rs = new List<Renderer>();
            var d = B.Group(r.transform, "Destello", new Vector3(0f, 0.3f, 0f));
            B.Animate(d, B.Flash(), 0.35f);
            rs.Add(B.Sphere(d, "Bola", 0.5f, cielo.Energy));
            rs.Add(R.Wave(r.transform, m, "Aro", 1.6f, Quaternion.identity, cielo.Piece, 0.45f, 0.05f));
            rs.Add(R.Wave(r.transform, m, "Aro de dentro", 1.0f, Quaternion.identity, luz.Piece, 0.4f, 0.05f, 0.06f));
            var suben = R.Burst(14, 0.5f, 1.2f, 0.08f, 0.14f, 0.6f, 0.9f);
            suben.Rise = 3f; suben.Shape = ParticleSystemShapeType.Circle; suben.Radius = 0.6f;
            B.Particles(r.transform, "Estrellitas que suben", m.Star, cielo.Spark, suben);
            R.Pulse(r, rs.ToArray(), B.PointLight(r.transform, cielo.Light, new Vector3(0f, 0.5f, 0f)), null, 3f, 3f, 0, 0, 0, 0.5f, 0.3f, B.LightFlash(0.5f));
        }, log);

        // Volar: estela corta de luz en los pies (estrellitas y bolitas que se quedan atrás).
        Rehacer("VFX_Vuelo_Pies", r =>
        {
            var est = R.Settings(0.3f, 0.45f, 0f, 0f, 0.07f, 0.12f);
            est.World = true; est.Loop = true; est.Rate = 10f; est.RatePerMeter = 4f; est.Tumble = true; est.MaxParticles = 80;
            est.Shape = ParticleSystemShapeType.Sphere; est.Radius = 0.15f;
            est.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            B.Particles(r.transform, "Estrellitas", m.Star, cielo.Spark, est);
            var bol = est; bol.Rate = 6f; bol.RatePerMeter = 3f; bol.SizeMin = 0.08f; bol.SizeMax = 0.14f;
            B.Particles(r.transform, "Bolitas", m.Sphere, cielo.Energy, bol);
        }, log);

        // Aterrizar: aro que corre por el suelo y estrellitas a ras de suelo. Sin humo.
        Rehacer("VFX_Vuelo_Aterrizaje", r =>
        {
            var rs = new List<Renderer>();
            rs.Add(R.Wave(r.transform, m, "Aro", 1.4f, Quaternion.identity, cielo.Piece, 0.4f, 0.04f));
            var salen = R.Burst(10, 2f, 3f, 0.07f, 0.12f, 0.3f, 0.45f);
            salen.Drag = 4f; salen.Shape = ParticleSystemShapeType.Circle; salen.Radius = 0.3f;
            B.Particles(r.transform, "Estrellitas", m.Star, cielo.Spark, salen);
            R.Pulse(r, rs.ToArray(), B.PointLight(r.transform, cielo.Light, new Vector3(0f, 0.3f, 0f)), null, 2f, 2.5f, 0, 0, 0, 0.4f, 0.25f, B.LightFlash(0.4f));
        }, log);

        // Planear: dos medias lunas de viento a los lados de los hombros y hojas que se quedan atrás.
        // El origen ya está a la altura del pecho (posicionDelEfecto de PlaneoDelJugador).
        Rehacer("VFX_Planeo", r =>
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            for (int lado = -1; lado <= 1; lado += 2)
            {
                var g = B.Group(r.transform, lado < 0 ? "Viento izquierdo" : "Viento derecho", new Vector3(lado * 0.55f, -0.2f, 0.1f),
                                Quaternion.Euler(0f, 0f, lado * 25f));
                B.Animate(g, B.PopIn(), 0.25f);
                rs.Add(B.Piece(g, "Media luna", m.Arc ?? m.OrbitRing, viento.Piece, Vector3.zero, Quaternion.identity, Vector3.one * 0.5f));
            }
            var hojas = R.Settings(0.5f, 0.8f, 0f, 0f, 0.1f, 0.16f);
            hojas.World = true; hojas.Loop = true; hojas.Rate = 6f; hojas.RatePerMeter = 1.5f; hojas.Tumble = true;
            hojas.Shape = ParticleSystemShapeType.Sphere; hojas.Radius = 0.4f;
            var h = B.Group(r.transform, "Hojas", new Vector3(0f, -0.3f, 0.2f));
            B.Particles(h, "Hojas", m.Petal, viento.Energy, hojas);
            R.Pulse(r, rs.ToArray(), B.PointLight(r.transform, viento.Light), spins.ToArray(), 0.8f, 2f, 0.8f, 0.3f, 0.2f);
        }, log);

        Asignar(log);
        AssetDatabase.SaveAssets();
    }

    /// Engancha los efectos nuevos en _WILL.prefab (por el editor, con su API: nada de YAML a mano).
    private static void Asignar(System.Text.StringBuilder log)
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaWill);
        try
        {
            bool cambiado = false;
            foreach (var vuelo in raiz.GetComponentsInChildren<PlayerFlyingController>(true))
            {
                var so = new SerializedObject(vuelo);
                cambiado |= Poner(so, "vfxDeEntrada", "VFX_Vuelo_Despegue");
                cambiado |= Poner(so, "footVfxPrefab", "VFX_Vuelo_Pies");
                cambiado |= Poner(so, "landingVfxPrefab", "VFX_Vuelo_Aterrizaje");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var planeo in raiz.GetComponentsInChildren<PlaneoDelJugador>(true))
            {
                var so = new SerializedObject(planeo);
                cambiado |= Poner(so, "efectoMientrasPlanea", "VFX_Planeo");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (cambiado)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, RutaWill);
                log.Append("(_WILL: efectos de vuelo asignados) ");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }

        // Los compañeros vuelan con el mismo efecto de pies que Will.
        var pies = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/VFX_Vuelo_Pies.prefab");
        int compañeros = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:NPCPartyConfig"))
        {
            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (config == null) continue;
            var so = new SerializedObject(config);
            var prop = so.FindProperty("footVfxPrefab");
            if (prop == null || pies == null || prop.objectReferenceValue == pies) continue;
            prop.objectReferenceValue = pies;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            compañeros++;
        }
        if (compañeros > 0) log.Append($"(compañeros con el efecto de pies: {compañeros}) ");
    }

    private static bool Poner(SerializedObject so, string campo, string prefab)
    {
        var prop = so.FindProperty(campo);
        var nuevo = AssetDatabase.LoadAssetAtPath<GameObject>($"{Carpeta}/{prefab}.prefab");
        if (prop == null || nuevo == null || prop.objectReferenceValue == nuevo) return false;
        prop.objectReferenceValue = nuevo;
        return true;
    }

    private static void Rehacer(string nombre, Action<GameObject> montar, System.Text.StringBuilder log)
    {
        R.RebuildInPlace($"{Carpeta}/{nombre}.prefab", nombre, montar);
        log.Append(nombre).Append(' ');
    }
}
