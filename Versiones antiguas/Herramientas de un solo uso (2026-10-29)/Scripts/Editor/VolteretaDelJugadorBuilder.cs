using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Montaje de la voltereta del jugador y sus usos (INC-651 a INC-656): añade
/// <see cref="VolteretaDelJugador"/> a _WILL.prefab, crea las zonas del remate aéreo (una por
/// elemento) y se las pone a los básicos, activa la voltereta en el dúo Will + Estela y crea el
/// prefab del lanzador de salto. Idempotente: se puede ejecutar varias veces.
/// </summary>
public static class VolteretaDelJugadorBuilder
{
    private const string RutaJugador = "Assets/Prefabs/_WILL.prefab";
    private const string CarpetaExploracion = "Assets/Prefabs/Exploracion";
    public const string RutaLanzador = CarpetaExploracion + "/LanzadorDeSalto.prefab";
    private const string VfxDeEntradaAlVuelo = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Shockwave_01.prefab";

    private const string CarpetaRemates = "Assets/_SPELLS/Remates aéreos";

    /// Remate aéreo de cada elemento: plantilla (zona existente cuyo efecto visual se reutiliza),
    /// nombre, radio, duración, intervalo, daño por golpe y estado que pone.
    private struct Remate
    {
        public MagicElement elemento;
        public string nombre, plantilla, sfx;
        public float radio, duracion, intervalo, dano, duracionEstado, fuerzaEstado;
        public EstadoDeCombate estado;
    }

    private static readonly Remate[] Remates =
    {
        new Remate { elemento = MagicElement.Fire,  nombre = "Fuego",  plantilla = "MuroDeFuego",   sfx = "Impacto_Fuego_Grande",
                     radio = 2.5f, duracion = 2.5f, intervalo = 0.5f, dano = 5f,  estado = EstadoDeCombate.Ninguno },
        new Remate { elemento = MagicElement.Storm, nombre = "Viento", plantilla = "Remolino",      sfx = "Impacto_Viento_Grande",
                     radio = 3.5f, duracion = 0.8f, intervalo = 0.4f, dano = 6f,  estado = EstadoDeCombate.Empujar, duracionEstado = 0.35f, fuerzaEstado = 12f },
        new Remate { elemento = MagicElement.Light, nombre = "Luz",    plantilla = "NovaDeLuz",     sfx = "Impacto_Luz_Grande",
                     radio = 3f,   duracion = 0.8f, intervalo = 0.5f, dano = 10f, estado = EstadoDeCombate.Inmovilizar, duracionEstado = 0.8f },
        new Remate { elemento = MagicElement.Mind,  nombre = "Mente",  plantilla = "SelloDelPacto", sfx = "Impacto_Mente_Grande",
                     radio = 3.5f, duracion = 1.2f, intervalo = 0.4f, dano = 4f,  estado = EstadoDeCombate.Atraer, duracionEstado = 0.6f, fuerzaEstado = 5f },
    };

    private static readonly string[] MaterialesDeRuna =
    {
        "Assets/VFX/Hovl Studio/Magic effects pack/Materials/MagicCircle.mat",
        "Assets/VFX/100BestEffectPack/Texture&Material/MagicCircle Texture&Mateiral/MagicCircle2.mat",
    };

    [MenuItem("El Sendero/Combate/Montar voltereta del jugador y sus usos (INC-651)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Voltereta", "Sal de Play para montarla.", "Aceptar");
            return;
        }

        var hecho = new List<string>();
        var avisos = new List<string>();
        AnadirAlJugador(hecho, avisos);
        CrearRematesAereos(hecho, avisos);
        ActivarVolteretaEnElDuoWillEstela(hecho, avisos);
        CrearPrefabDelLanzador(hecho, avisos);
        AssetDatabase.SaveAssets();

        string texto = string.Join("\n", hecho);
        if (avisos.Count > 0) texto += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Voltereta", texto, "Aceptar");
    }

    private static void AnadirAlJugador(List<string> hecho, List<string> avisos)
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaJugador);
        if (raiz == null) { avisos.Add($"No encuentro {RutaJugador}."); return; }
        try
        {
            var controller = raiz.GetComponentInChildren<vThirdPersonController>(true);
            if (controller == null) { avisos.Add("_WILL no tiene vThirdPersonController."); return; }
            bool cambiado = false;
            if (controller.GetComponent<VolteretaDelJugador>() == null)
            {
                controller.gameObject.AddComponent<VolteretaDelJugador>();
                hecho.Add("Añadida VolteretaDelJugador a _WILL.");
                cambiado = true;
            }
            else hecho.Add("_WILL ya tenía VolteretaDelJugador.");

            var receptor = controller.GetComponent<AerialKnockbackReceiver>();
            if (receptor == null)
            {
                receptor = controller.gameObject.AddComponent<AerialKnockbackReceiver>();
                cambiado = true;
                hecho.Add("Añadido AerialKnockbackReceiver a _WILL.");
            }
            var efecto = AssetDatabase.LoadAssetAtPath<GameObject>(VfxDeEntradaAlVuelo);
            var receptorSerializado = new SerializedObject(receptor);
            var recuperacion = receptorSerializado.FindProperty("vfxDeRecuperacion");
            if (recuperacion.objectReferenceValue == null && efecto != null)
            {
                recuperacion.objectReferenceValue = efecto;
                receptorSerializado.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(receptor);
                cambiado = true;
            }
            MontarPoseDeDerribo(controller, hecho, avisos);

            // Efecto de la entrada al vuelo (INC-665), si no tiene ninguno puesto.
            var vuelo = controller.GetComponent<PlayerFlyingController>();
            var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(VfxDeEntradaAlVuelo);
            if (vuelo != null && vfx != null)
            {
                var so = new SerializedObject(vuelo);
                var campo = so.FindProperty("vfxDeEntrada");
                if (campo != null && campo.objectReferenceValue == null)
                {
                    campo.objectReferenceValue = vfx;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    hecho.Add("Efecto de entrada al vuelo puesto en _WILL.");
                    cambiado = true;
                }
            }
            else if (vfx == null) avisos.Add($"No encuentro {VfxDeEntradaAlVuelo}: la entrada al vuelo queda sin efecto.");

            if (cambiado)
            {
                EditorUtility.SetDirty(raiz);
                PrefabUtility.SaveAsPrefabAsset(raiz, RutaJugador);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static void MontarPoseDeDerribo(vThirdPersonController jugador, List<string> hecho, List<string> avisos)
    {
        var animator = jugador.GetComponent<Animator>();
        if (animator == null) animator = jugador.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController runtime = animator != null ? animator.runtimeAnimatorController : null;
        while (runtime is AnimatorOverrideController variante) runtime = variante.runtimeAnimatorController;
        var controller = runtime as AnimatorController;
        if (controller == null || controller.layers.Length == 0)
        {
            avisos.Add("No encuentro el AnimatorController principal de _WILL: falta Derribo_Vuelo.");
            return;
        }
        const string rutaClip = "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/Die01_NoWeapon.fbx";
        AnimationClip clip = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(rutaClip))
            if (asset is AnimationClip candidato && candidato.name == "Die01_NoWeapon") { clip = candidato; break; }
        if (clip == null) { avisos.Add("No encuentro Die01_NoWeapon: falta Derribo_Vuelo."); return; }
        var maquina = controller.layers[0].stateMachine;
        var estado = BuscarEstado(maquina, "Derribo_Vuelo");
        if (estado == null) estado = maquina.AddState("Derribo_Vuelo");
        estado.motion = clip;
        estado.speed = 0f; // El receptor recorre solo el tramo de caída del clip durante el arco.
        estado.writeDefaultValues = false;
        EditorUtility.SetDirty(estado);
        EditorUtility.SetDirty(maquina);
        EditorUtility.SetDirty(controller);
        hecho.Add($"Derribo_Vuelo con Die01_NoWeapon en {AssetDatabase.GetAssetPath(controller)}.");
    }

    private static AnimatorState BuscarEstado(AnimatorStateMachine maquina, string nombre)
    {
        foreach (var estado in maquina.states) if (estado.state.name == nombre) return estado.state;
        foreach (var submaquina in maquina.stateMachines)
        {
            var estado = BuscarEstado(submaquina.stateMachine, nombre);
            if (estado != null) return estado;
        }
        return null;
    }

    /// Crea (o actualiza) una zona de remate por elemento y se la pone a los básicos de ese
    /// elemento (proyectiles), sin tocar los que ya tienen otra asignada a mano.
    private static void CrearRematesAereos(List<string> hecho, List<string> avisos)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaRemates))
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Remates aéreos");

        var porElemento = new Dictionary<MagicElement, MagicSpellSO>();
        foreach (var r in Remates)
        {
            string ruta = $"{CarpetaRemates}/RemateAereo_{r.nombre}.asset";
            var zona = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(ruta);
            var plantilla = AssetDatabase.LoadAssetAtPath<MagicSpellSO>($"Assets/_SPELLS/{r.plantilla}.asset");
            if (zona == null)
            {
                if (plantilla == null || plantilla.kind != MagicKind.Zone)
                {
                    avisos.Add($"No encuentro la zona {r.plantilla}: no hay remate de {r.nombre}.");
                    continue;
                }
                zona = Object.Instantiate(plantilla);
                AssetDatabase.CreateAsset(zona, ruta);
            }

            float radioDePlantilla = plantilla != null ? Mathf.Max(0.1f, plantilla.zoneRadius) : r.radio;
            zona.spellId = SpellId.None;
            zona.displayName = $"Remate aéreo ({r.nombre})";
            zona.displayNameId = "";
            zona.lore = "";
            zona.loreId = "";
            zona.element = r.elemento;
            zona.manaCost = 0f;
            zona.damage = r.dano;
            zona.zoneRadius = r.radio;
            zona.zoneDuration = r.duracion;
            zona.zoneTickInterval = r.intervalo;
            zona.zoneOnCaster = false;
            zona.statusEffect = r.estado;
            zona.statusDuration = r.duracionEstado;
            zona.statusStrength = r.fuerzaEstado;
            zona.useScaleOverride = true;
            zona.scaleOverride = Vector3.one * (r.radio / radioDePlantilla);
            zona.castSFXKey = r.sfx;
            zona.impactSFXKey = "";
            zona.impactZone = null;
            zona.remateAereo = null;
            EditorUtility.SetDirty(zona);
            porElemento[r.elemento] = zona;
        }

        int puestos = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:MagicSpellSO", new[] { "Assets/_SPELLS" }))
        {
            var hechizo = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (hechizo == null || hechizo.kind != MagicKind.Projectile || hechizo.remateAereo != null) continue;
            if (!porElemento.TryGetValue(hechizo.element, out var zona)) continue;
            hechizo.remateAereo = zona;
            EditorUtility.SetDirty(hechizo);
            puestos++;
        }
        hecho.Add($"Remates aéreos: {porElemento.Count} zonas en {CarpetaRemates}; asignados a {puestos} hechizo(s) más.");
    }

    private static void ActivarVolteretaEnElDuoWillEstela(List<string> hecho, List<string> avisos)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:SpecialAttackSO"))
        {
            var ataque = AssetDatabase.LoadAssetAtPath<SpecialAttackSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (ataque == null || !ataque.IsDuoOf(PartyControlManager.CharacterSlot.Will, PartyControlManager.CharacterSlot.Estela)) continue;
            if (!ataque.volteretaDelActivo)
            {
                ataque.volteretaDelActivo = true;
                EditorUtility.SetDirty(ataque);
            }
            hecho.Add($"Dúo Will + Estela ({ataque.name}) con voltereta.");
            return;
        }
        avisos.Add("No encuentro el dúo Will + Estela.");
    }

    private static void CrearPrefabDelLanzador(List<string> hecho, List<string> avisos)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaExploracion))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Exploracion");

        var raiz = new GameObject("LanzadorDeSalto");
        try
        {
            var caja = raiz.AddComponent<BoxCollider>();
            caja.isTrigger = true;
            caja.size = new Vector3(1.6f, 1f, 1.6f);
            caja.center = new Vector3(0f, 0.5f, 0f);

            var lanzador = raiz.AddComponent<LanzadorDeSalto>();
            var so = new SerializedObject(lanzador);
            so.FindProperty("sfxDeImpulso").stringValue = "RuneActivate";
            so.ApplyModifiedPropertiesWithoutUndo();

            var runa = GameObject.CreatePrimitive(PrimitiveType.Quad);
            runa.name = "Runa";
            Object.DestroyImmediate(runa.GetComponent<Collider>());
            runa.transform.SetParent(raiz.transform, false);
            runa.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            runa.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            runa.transform.localScale = new Vector3(2f, 2f, 1f);
            Material material = null;
            foreach (var ruta in MaterialesDeRuna)
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (material != null) break;
            }
            if (material != null) runa.GetComponent<Renderer>().sharedMaterial = material;
            else avisos.Add("No encuentro un material de círculo mágico: la runa queda con el material por defecto.");

            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(raiz.transform, false);
            luz.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            luz.type = LightType.Point;
            luz.range = 3.5f;
            luz.intensity = 2f;
            luz.color = new Color(0.55f, 0.85f, 1f);

            PrefabUtility.SaveAsPrefabAsset(raiz, RutaLanzador);
            hecho.Add($"Prefab del lanzador: {RutaLanzador}.");
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }
}
