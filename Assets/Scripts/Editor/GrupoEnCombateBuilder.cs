using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Montaje del grupo en combate (INC-717): pone SaludDelGrupo y ReanimarManteniendoA en
/// GrupoDelJugador.prefab (si faltan, GrupoDelJugador los añade en juego, pero así se ajustan en
/// el Inspector) y crea la Pluma estelar, el objeto que levanta a un compañero caído.
/// Idempotente.
/// </summary>
public static class GrupoEnCombateBuilder
{
    private const string RutaGrupo = "Assets/Prefabs/GrupoDelJugador.prefab";
    private const string SpriteAnillo = "Assets/Art/UI/HUD/ability_socket_ring.png";
    private const string RutaPluma = "Assets/_ITEMS/IT_PlumaEstelar.asset";
    private const string IconoProvisional = "Assets/_ITEMS/IT_PocionVida.asset";

    [MenuItem("El Sendero/Grupo/Rehacer efectos de ataques de equipo")]
    public static void RehacerEfectosDeEquipo()
    {
        const string carpeta = "Assets/Art/VFX/Equipo";
        VfxProcedural.CrearCarpeta(carpeta);
        var texturas = new VfxProcedural.Texturas(carpeta + "/Texturas", carpeta + "/Materiales", "Equipo");

        const string gabriel = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/";
        var meteor02 = AssetDatabase.LoadAssetAtPath<GameObject>(gabriel + "vfx_MeteorRain_02.prefab");
        var portal01 = AssetDatabase.LoadAssetAtPath<GameObject>(gabriel + "vfx_Portal_01.prefab");
        var implosion01 = AssetDatabase.LoadAssetAtPath<GameObject>(gabriel + "vfx_Implosion_01.prefab");
        var shockwave01 = AssetDatabase.LoadAssetAtPath<GameObject>(gabriel + "vfx_Shockwave_01.prefab");
        var flames01 = AssetDatabase.LoadAssetAtPath<GameObject>(gabriel + "vfx_Flames_01.prefab");

        const string hovlAoe = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/";
        const string hovlCircles = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/";
        const string hovlFire = "Assets/VFX/Hovl Studio/Procedural fire/Prefabs/";
        const string hovlAuras = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Character auras/";

        var meteorsHovl = AssetDatabase.LoadAssetAtPath<GameObject>(hovlAoe + "Meteors AOE.prefab");
        var laserAoeHovl = AssetDatabase.LoadAssetAtPath<GameObject>(hovlAoe + "Laser AOE.prefab");
        var groundExplosionHovl = AssetDatabase.LoadAssetAtPath<GameObject>(hovlAoe + "Ground AOE explosion.prefab");
        var circleHovl = AssetDatabase.LoadAssetAtPath<GameObject>(hovlCircles + "Magic circle 2.prefab");
        var magicFire0 = AssetDatabase.LoadAssetAtPath<GameObject>(hovlFire + "Magic fire 0.prefab");
        var starAura = AssetDatabase.LoadAssetAtPath<GameObject>(hovlAuras + "Star aura.prefab");
        var plexusHovl = AssetDatabase.LoadAssetAtPath<GameObject>(hovlAuras + "Plexus.prefab");
        var explosion01 = AssetDatabase.LoadAssetAtPath<GameObject>(gabriel + "vfx_Explosion_01.prefab");
        var agujeroNegroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_VFX/Prologo/VFX_AgujeroNegro.prefab");

        // 1. Trío: Cataclismo Celestial de las Tres Estrellas (16m de radio, 250 de daño, pilares masivos de rayos celestiales láser, lluvia de meteoros, runas gigantes e implosión)
        ConstruirVfxAtaque("SpecialAttack_Trio", root =>
        {
            if (laserAoeHovl != null)
            {
                var l = (GameObject)PrefabUtility.InstantiatePrefab(laserAoeHovl, root.transform);
                l.transform.localPosition = Vector3.zero;
                l.transform.localScale = Vector3.one * 2.2f;
                var allPs = l.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < allPs.Length; i++)
                {
                    var ps = allPs[i];
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.loop = true;
                    main.playOnAwake = true;
                }
            }
            if (meteorsHovl != null)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(meteorsHovl, root.transform);
                m.transform.localPosition = Vector3.zero;
                m.transform.localScale = Vector3.one * 2.5f;
                var allPs = m.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < allPs.Length; i++)
                {
                    var ps = allPs[i];
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.loop = true;
                    main.playOnAwake = true;
                }
            }
            if (circleHovl != null)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(circleHovl, root.transform);
                c.transform.localPosition = Vector3.up * 0.05f;
                c.transform.localScale = Vector3.one * 4.0f;
            }
            if (implosion01 != null)
            {
                var imp = (GameObject)PrefabUtility.InstantiatePrefab(implosion01, root.transform);
                imp.transform.localPosition = Vector3.up * 1.5f;
                imp.transform.localScale = Vector3.one * 2.5f;
            }
            if (shockwave01 != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(shockwave01, root.transform);
                s.transform.localPosition = Vector3.zero;
                s.transform.localScale = Vector3.one * 2.5f;
            }
            var anillo = VfxProcedural.Sistema(root.transform, "Runas del Trío", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 4.2f, true);
            var mainAnillo = anillo.main;
            mainAnillo.startLifetime = 4.2f;
            mainAnillo.startSize = 16f;
            mainAnillo.startColor = new Color(1f, 0.85f, 0.4f, 1f);
            VfxProcedural.Emitir(anillo, 2f);
            VfxProcedural.Crecer(anillo, 0.8f);
            VfxProcedural.Desvanecer(anillo, 0.05f, 0.4f);
        }, 16f, 250f, 30f, 4.2f, 0.9f);

        // 2. Will + Estela: Tormenta de Meteoros Ígneos (Lluvia visible y masiva de meteoros de Hovl Studio + círculo rúnico + brasas)
        ConstruirVfxAtaque("SpecialAttack_Estela", root =>
        {
            if (meteorsHovl != null)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(meteorsHovl, root.transform);
                m.transform.localPosition = Vector3.zero;
                m.transform.localScale = Vector3.one * 1.8f;
                var allPs = m.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < allPs.Length; i++)
                {
                    var ps = allPs[i];
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.loop = true;
                    main.playOnAwake = true;
                }
            }
            if (circleHovl != null)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(circleHovl, root.transform);
                c.transform.localPosition = Vector3.up * 0.05f;
                c.transform.localScale = Vector3.one * 2.5f;
            }
            var anillo = VfxProcedural.Sistema(root.transform, "Onda de Fuego", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 3.8f, true);
            var mainAnillo = anillo.main;
            mainAnillo.startLifetime = 3.8f;
            mainAnillo.startSize = 10f;
            mainAnillo.startColor = new Color(1f, 0.55f, 0.1f, 0.85f);
            VfxProcedural.Emitir(anillo, 1f);
            VfxProcedural.Crecer(anillo, 0.5f);
            VfxProcedural.Desvanecer(anillo, 0.05f, 0.4f);
        }, 10f, 120f, 22f, 3.8f, 0.8f);

        // 3. Will + Liam: Singularidad Cósmica y Supernova Gravitacional
        // (Agujero Negro 3D real en el centro que atrae a todos los enemigos + Supernova cósmica al impactar Will)
        ConstruirVfxAtaque("SpecialAttack_Liam", root =>
        {
            if (agujeroNegroPrefab != null)
            {
                var an = (GameObject)PrefabUtility.InstantiatePrefab(agujeroNegroPrefab, root.transform);
                an.transform.localPosition = Vector3.up * 1.8f;
                an.transform.localScale = Vector3.one * 1.6f;
            }
            if (circleHovl != null)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(circleHovl, root.transform);
                c.transform.localPosition = Vector3.up * 0.05f;
                c.transform.localScale = Vector3.one * 2.5f;
            }
            var anillo = VfxProcedural.Sistema(root.transform, "Horizonte de Sucesos", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 3.8f, true);
            var mainAnillo = anillo.main;
            mainAnillo.startLifetime = 3.8f;
            mainAnillo.startSize = 10f;
            mainAnillo.startColor = new Color(0.65f, 0.25f, 1f, 0.9f);
            VfxProcedural.Emitir(anillo, 1f);
            VfxProcedural.Crecer(anillo, 0.4f);
            VfxProcedural.Desvanecer(anillo, 0.05f, 0.4f);
        }, 10f, 130f, 25f, 3.8f, 0.8f);

        // 4. Estela + Liam: Supernova del Pacto (Llamas rugientes masivas, implosión y shockwave doble)
        ConstruirVfxAtaque("SpecialAttack_EstelaLiam", root =>
        {
            if (flames01 != null)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(flames01, root.transform);
                f.transform.localPosition = Vector3.zero;
                f.transform.localScale = Vector3.one * 1.5f;
            }
            if (shockwave01 != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(shockwave01, root.transform);
                s.transform.localPosition = Vector3.zero;
                s.transform.localScale = Vector3.one * 1.2f;
            }
            var anillo = VfxProcedural.Sistema(root.transform, "Onda del Pacto", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 1f, false);
            VfxProcedural.Unico(anillo, 0.85f, 9.5f, new Color(0.9f, 0.2f, 0.6f, 1f));
            VfxProcedural.Crecer(anillo, 0.7f);
            VfxProcedural.Desvanecer(anillo, 0.03f, 0.3f);
        }, 9.5f, 110f, 20f, 3.0f, 0.7f);

        // 5. Impactos de remate final terrestre:
        // A) Remate de Estela (detonación sísmica)
        VfxProcedural.GuardarPrefab("ImpactoRemate_Estela", carpeta + "/ImpactoRemate_Estela.prefab", root =>
        {
            if (groundExplosionHovl != null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(groundExplosionHovl, root.transform);
                g.transform.localPosition = Vector3.zero;
                g.transform.localScale = Vector3.one * 1.5f;
            }
            if (shockwave01 != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(shockwave01, root.transform);
                s.transform.localPosition = Vector3.zero;
                s.transform.localScale = Vector3.one * 1.4f;
            }
        });

        // B) Remate de Liam (Supernova cósmica de colapso gravitacional)
        VfxProcedural.GuardarPrefab("ImpactoRemate_Liam", carpeta + "/ImpactoRemate_Liam.prefab", root =>
        {
            if (groundExplosionHovl != null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(groundExplosionHovl, root.transform);
                g.transform.localPosition = Vector3.zero;
                g.transform.localScale = Vector3.one * 1.6f;
            }
            if (implosion01 != null)
            {
                var imp = (GameObject)PrefabUtility.InstantiatePrefab(implosion01, root.transform);
                imp.transform.localPosition = Vector3.up * 1.2f;
                imp.transform.localScale = Vector3.one * 1.8f;
            }
            if (shockwave01 != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(shockwave01, root.transform);
                s.transform.localPosition = Vector3.zero;
                s.transform.localScale = Vector3.one * 1.8f;
            }
        });

        // C) Remate del Trío (Cataclismo Celestial supremo: explosión terrestre masiva Hovl + explosión Gabriel Aguiar + onda expansiva cósmica)
        VfxProcedural.GuardarPrefab("ImpactoRemate_Trio", carpeta + "/ImpactoRemate_Trio.prefab", root =>
        {
            if (groundExplosionHovl != null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(groundExplosionHovl, root.transform);
                g.transform.localPosition = Vector3.zero;
                g.transform.localScale = Vector3.one * 2.5f;
            }
            if (explosion01 != null)
            {
                var exp = (GameObject)PrefabUtility.InstantiatePrefab(explosion01, root.transform);
                exp.transform.localPosition = Vector3.up * 0.5f;
                exp.transform.localScale = Vector3.one * 2.2f;
            }
            if (shockwave01 != null)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(shockwave01, root.transform);
                s.transform.localPosition = Vector3.zero;
                s.transform.localScale = Vector3.one * 2.8f;
            }
        });

        // 6. Auras corporales de acción durante la canalización:
        // A) Aura de formación del Trío: círculo rúnico tripartito dorado a los pies de la formación espalda con espalda
        VfxProcedural.GuardarPrefab("AuraFormacion_Trio", carpeta + "/AuraFormacion_Trio.prefab", root =>
        {
            if (circleHovl != null)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(circleHovl, root.transform);
                c.transform.localPosition = Vector3.up * 0.05f;
                c.transform.localScale = Vector3.one * 1.3f;
            }
            var glifoTriple = VfxProcedural.Sistema(root.transform, "Glifo Tripartito", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 3.5f, true);
            var main = glifoTriple.main;
            main.startLifetime = 1.4f;
            main.startSize = 2.6f;
            main.startColor = new Color(1f, 0.9f, 0.4f, 0.95f);
            VfxProcedural.Emitir(glifoTriple, 6f);
            VfxProcedural.Desvanecer(glifoTriple, 0.1f, 0.5f);
        });

        // B) Levitación de Estela: plataforma de fuego en los pies
        VfxProcedural.GuardarPrefab("AuraLevitacion_Estela", carpeta + "/AuraLevitacion_Estela.prefab", root =>
        {
            if (magicFire0 != null)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(magicFire0, root.transform);
                f.transform.localPosition = Vector3.zero;
                f.transform.localScale = Vector3.one * 0.75f;
            }
            var anilloPies = VfxProcedural.Sistema(root.transform, "Anillo Fuego Pies", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 3f, true);
            var main = anilloPies.main;
            main.startLifetime = 1.2f;
            main.startSize = 1.4f;
            main.startColor = new Color(1f, 0.6f, 0.15f, 0.9f);
            VfxProcedural.Emitir(anilloPies, 5f);
            VfxProcedural.Desvanecer(anilloPies, 0.1f, 0.5f);
        });

        // C) Foco de mano de Estela alzada al cielo
        VfxProcedural.GuardarPrefab("FocoMano_Estela", carpeta + "/FocoMano_Estela.prefab", root =>
        {
            if (magicFire0 != null)
            {
                var f = (GameObject)PrefabUtility.InstantiatePrefab(magicFire0, root.transform);
                f.transform.localPosition = Vector3.zero;
                f.transform.localScale = Vector3.one * 0.35f;
            }
            var brilloMano = VfxProcedural.Sistema(root.transform, "Destello Invocador", texturas.Mat("Brillo", true), ParticleSystemRenderMode.Billboard, 3f, true);
            var main = brilloMano.main;
            main.startLifetime = 0.4f;
            main.startSize = 0.6f;
            main.startColor = new Color(1f, 0.9f, 0.5f, 1f);
            VfxProcedural.Emitir(brilloMano, 15f);
        });

        // D) Concentración de Will (Aura dorada estelar)
        VfxProcedural.GuardarPrefab("AuraConcentracion_Will", carpeta + "/AuraConcentracion_Will.prefab", root =>
        {
            if (starAura != null)
            {
                var a = (GameObject)PrefabUtility.InstantiatePrefab(starAura, root.transform);
                a.transform.localPosition = Vector3.zero;
                a.transform.localScale = Vector3.one * 0.85f;
            }
        });

        // E) Concentración de Liam (Aura psíquica/cósmica violeta)
        VfxProcedural.GuardarPrefab("AuraConcentracion_Liam", carpeta + "/AuraConcentracion_Liam.prefab", root =>
        {
            if (plexusHovl != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(plexusHovl, root.transform);
                p.transform.localPosition = Vector3.zero;
                p.transform.localScale = Vector3.one * 0.85f;
            }
            var anilloPies = VfxProcedural.Sistema(root.transform, "Anillo Psíquico", texturas.Mat("Anillo", true), ParticleSystemRenderMode.HorizontalBillboard, 3f, true);
            var main = anilloPies.main;
            main.startLifetime = 1.2f;
            main.startSize = 1.4f;
            main.startColor = new Color(0.6f, 0.2f, 1f, 0.9f);
            VfxProcedural.Emitir(anilloPies, 5f);
            VfxProcedural.Desvanecer(anilloPies, 0.1f, 0.5f);
        });

        // 7. Actualizar AudioGraphProfile con la orquestación sonora épica
        ActualizarAudioProfile();

        // 8. Asignar los nuevos prefabs en GrupoDelJugador.prefab
        AsignarPrefabsEnGrupo();

        AssetDatabase.SaveAssets();
        Debug.Log("[GrupoEnCombate] Ataques de equipo reconstruidos: Trío celestial de 3 estrellas, Lluvia de meteoros de Estela, Singularidad de Liam, auras y audio orquestado.");
    }

    private static void ActualizarAudioProfile()
    {
        const string rutaProfile = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(rutaProfile);
        if (profile == null) return;

        // Trío: Subida cósmica y trueno rodante masivo (sin coro)
        AsignarOAnadirSfx(profile, "SpecialAttack_Trio", "Assets/Audio/SFX_Prologo/SFX_Prologo_Subida.wav");
        AsignarOAnadirSfx(profile, "SpecialAttack_Trio_Bucle", "Assets/Audio/SFX_Prologo/Disenados/SFX_Trueno_Lejano_Rodante.wav");

        // Estela
        AsignarOAnadirSfx(profile, "SpecialAttack_Estela", "Assets/Audio/SFX_Prologo/Disenados/SFX_Ki_Carga.wav");
        AsignarOAnadirSfx(profile, "SpecialAttack_Meteoros", "Assets/Audio/SFX_Prologo/Disenados/SFX_Hechizo_Grande.wav");

        // Liam
        AsignarOAnadirSfx(profile, "SpecialAttack_Liam", "Assets/Audio/SFX_Prologo/SFX_AgujeroNegro_Carga.mp3");
        AsignarOAnadirSfx(profile, "SpecialAttack_AgujeroNegro", "Assets/Audio/SFX_Prologo/SFX_AgujeroNegro_Bucle.mp3");

        // Impacto general
        AsignarOAnadirSfx(profile, "SpecialAttack_Impacto", "Assets/Audio/SFX_Prologo/Disenados/SFX_Explosion_Final_DBZ.wav");

        EditorUtility.SetDirty(profile);
    }

    private static void AsignarOAnadirSfx(AudioGraphProfile profile, string key, string clipPath)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) return;
        for (int i = 0; i < profile.eventSfx.Count; i++)
        {
            if (profile.eventSfx[i] != null && string.Equals(profile.eventSfx[i].eventKey, key, System.StringComparison.OrdinalIgnoreCase))
            {
                profile.eventSfx[i].sfx = clip;
                return;
            }
        }
        profile.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = key, sfx = clip });
    }

    private static void AsignarPrefabsEnGrupo()
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaGrupo);
        if (raiz == null) return;
        try
        {
            var ataques = raiz.GetComponent<DuoSpecialAttackSystem>();
            if (ataques != null)
            {
                var so = new SerializedObject(ataques);
                const string carpeta = "Assets/Art/VFX/Equipo";
                var pLevitacion = so.FindProperty("auraLevitacionEstela");
                if (pLevitacion != null) pLevitacion.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/AuraLevitacion_Estela.prefab");

                var pFoco = so.FindProperty("focoManoEstela");
                if (pFoco != null) pFoco.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/FocoMano_Estela.prefab");

                var pAuraWill = so.FindProperty("auraConcentracionWill");
                if (pAuraWill != null) pAuraWill.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/AuraConcentracion_Will.prefab");

                var pAuraLiam = so.FindProperty("auraConcentracionLiam");
                if (pAuraLiam != null) pAuraLiam.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/AuraConcentracion_Liam.prefab");

                var pAuraTrio = so.FindProperty("auraFormacionTrio");
                if (pAuraTrio != null) pAuraTrio.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/AuraFormacion_Trio.prefab");

                var pImpactoEstela = so.FindProperty("impactoRemateEstela");
                if (pImpactoEstela != null) pImpactoEstela.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/ImpactoRemate_Estela.prefab");

                var pImpactoLiam = so.FindProperty("impactoRemateLiam");
                if (pImpactoLiam != null) pImpactoLiam.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/ImpactoRemate_Liam.prefab");

                var pImpactoTrio = so.FindProperty("impactoRemateTrio");
                if (pImpactoTrio != null) pImpactoTrio.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(carpeta + "/ImpactoRemate_Trio.prefab");

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(raiz, RutaGrupo);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static void ConstruirVfxAtaque(string nombreAtaque, System.Action<GameObject> constructor, float radio, float dano, float empuje, float lifetime, float delay)
    {
        const string carpeta = "Assets/Art/VFX/Equipo";
        string prefabPath = carpeta + "/" + nombreAtaque + ".prefab";
        VfxProcedural.GuardarPrefab(nombreAtaque, prefabPath, constructor);

        string assetPath = "Assets/Scripts/Attacks/SO/" + nombreAtaque + ".asset";
        var ataque = AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(assetPath);
        if (ataque != null)
        {
            ataque.vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            ataque.aoeRadius = radio;
            ataque.damage = dano;
            ataque.knockbackForce = empuje;
            ataque.vfxLifetime = lifetime;
            ataque.damageDelay = delay;
            ataque.elevacionDelVfx = 0.15f;

            if (nombreAtaque == "SpecialAttack_Liam")
            {
                ataque.volteretaDelActivo = true;
                ataque.alturaDeLaVoltereta = 1.4f;
                ataque.avanceDeLaVoltereta = 4.5f;
                ataque.gestureSeconds = 0.7f;
                ataque.castSFXKey = "SpecialAttack_Liam";
            }
            else if (nombreAtaque == "SpecialAttack_Trio")
            {
                ataque.gestureSeconds = 0.8f;
                ataque.castSFXKey = "SpecialAttack_Trio";
            }

            EditorUtility.SetDirty(ataque);
        }
    }

    [MenuItem("El Sendero/Grupo/Activar ataques de equipo")]
    public static void ActivarAtaquesDeEquipo()
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaGrupo);
        try
        {
            var ataques = raiz.GetComponent<DuoSpecialAttackSystem>();
            if (ataques == null) throw new System.InvalidOperationException("El grupo no tiene DuoSpecialAttackSystem.");
            ataques.enabled = true;
            EditorUtility.SetDirty(ataques);
            PrefabUtility.SaveAsPrefabAsset(raiz, RutaGrupo);
            AssetDatabase.SaveAssets();
            foreach (var grupo in Object.FindObjectsByType<GrupoDelJugador>(FindObjectsInactive.Exclude))
            {
                var actual = grupo.GetComponent<DuoSpecialAttackSystem>();
                if (actual != null) actual.enabled = true;
            }
            Debug.Log("[GrupoEnCombate] Ataques de equipo activos en el prefab compartido y en los grupos cargados.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    [MenuItem("El Sendero/Grupo/Montar caídas y revivir (INC-717)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Grupo en combate", "Sal de Play para montarlo.", "Aceptar");
            return;
        }

        var hecho = new List<string>();
        var avisos = new List<string>();
        MontarGrupo(hecho, avisos);
        CrearPluma(hecho, avisos);
        AssetDatabase.SaveAssets();
        ItemRegistrySincronizador.Sincronizar();

        string texto = string.Join("\n", hecho);
        if (avisos.Count > 0) texto += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Grupo en combate", texto, "Aceptar");
    }

    private static void MontarGrupo(List<string> hecho, List<string> avisos)
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaGrupo);
        if (raiz == null) { avisos.Add($"No encuentro {RutaGrupo}."); return; }
        try
        {
            bool cambiado = false;
            var ataques = raiz.GetComponent<DuoSpecialAttackSystem>();
            if (ataques != null && !ataques.enabled)
            {
                ataques.enabled = true;
                cambiado = true;
                hecho.Add("Activados los ataques de equipo.");
            }
            if (raiz.GetComponent<SaludDelGrupo>() == null)
            {
                raiz.AddComponent<SaludDelGrupo>();
                hecho.Add("Añadido SaludDelGrupo a GrupoDelJugador.");
                cambiado = true;
            }
            else hecho.Add("GrupoDelJugador ya tenía SaludDelGrupo.");

            var reanimar = raiz.GetComponent<ReanimarManteniendoA>();
            if (reanimar == null)
            {
                reanimar = raiz.AddComponent<ReanimarManteniendoA>();
                hecho.Add("Añadido ReanimarManteniendoA a GrupoDelJugador.");
                cambiado = true;
            }
            else hecho.Add("GrupoDelJugador ya tenía ReanimarManteniendoA.");

            var so = new SerializedObject(reanimar);
            var anillo = so.FindProperty("anillo");
            if (anillo != null && anillo.objectReferenceValue == null)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteAnillo);
                if (sprite != null)
                {
                    anillo.objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    cambiado = true;
                }
                else avisos.Add($"No encuentro {SpriteAnillo}: el aviso usará el anillo del aviso de combate.");
            }

            if (cambiado) PrefabUtility.SaveAsPrefabAsset(raiz, RutaGrupo);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static void CrearPluma(List<string> hecho, List<string> avisos)
    {
        var pluma = AssetDatabase.LoadAssetAtPath<ItemData>(RutaPluma);
        bool nueva = pluma == null;
        if (nueva) pluma = ScriptableObject.CreateInstance<ItemData>();

        pluma.itemId = "plumaEstelar";
        pluma.displayNameId = "ITEM_PLUMAESTELAR_NAME";
        pluma.displayName = "Pluma estelar";
        pluma.pluralDisplayNameId = "ITEM_PLUMAESTELAR_NAME_PLURAL";
        pluma.pluralDisplayName = "Plumas estelares";
        pluma.useDescriptionId = "ITEM_PLUMAESTELAR_DESC";
        pluma.useDescription = "Levanta al compañero caído más cercano con la mitad de su vida.";
        pluma.usableFromInventory = true;
        pluma.usageKind = ItemData.ItemUsageKind.Consumable;
        pluma.buyPrice = 120;
        pluma.sellValue = 30;
        pluma.useEffects = new List<PickupEffect>
        {
            new PickupEffect { effectType = PickupEffectType.Revivir, quantity = 1, amount = 0.5f, consumeEvenIfNoChange = false }
        };
        if (pluma.icon == null)
        {
            var referencia = AssetDatabase.LoadAssetAtPath<ItemData>(IconoProvisional);
            if (referencia != null) pluma.icon = referencia.icon;
            avisos.Add("La Pluma estelar lleva de momento el icono de la Poción de vida.");
        }

        if (nueva)
        {
            AssetDatabase.CreateAsset(pluma, RutaPluma);
            hecho.Add($"Creada la Pluma estelar: {RutaPluma}.");
        }
        else
        {
            EditorUtility.SetDirty(pluma);
            hecho.Add("Pluma estelar actualizada.");
        }
    }
}
