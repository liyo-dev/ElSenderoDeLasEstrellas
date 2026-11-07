using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El reparto provisional de Chuchelandia con personajes del pack Sweet_Land, cada uno en su marca.
/// Los papeles que hablan llevan un id de actor estable (Actor_*) para el REPARTO de los guiones
/// (<c>alias Actor_X prefab=… nombre=…</c>) y el SequenceStage de cada secuencia. Cada papel sale de
/// una variante del modelo del pack, <c>Assets/_NPCs/Chuchelandia/Prefabs/&lt;Actor_X&gt;.prefab</c>, que
/// es también la que nombran los guiones (así el storyboard ve lo mismo que el juego): sin la IA de
/// demo (lo del espacio de nombres del pack y el NavMeshAgent), con Animator (si el modelo no trae,
/// uno con el avatar de su familia), con el controlador del pack o uno de reposo, con su tinte y con
/// <see cref="AnimacionDeCorredorPorAnimator"/> para andar cuando lo mueve un guion. Los tintes van en
/// materiales duplicados en <see cref="CarpetaReparto"/>; los originales del pack no se tocan.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string CarpetaReparto = "Assets/_NPCs/Chuchelandia";
    private const string Pack = "Assets/Art/World/ithappy/Sweet_Land";
    private const string Demo = Pack + "/Demonstration";
    private const string CarpetaPrefabsDelReparto = CarpetaReparto + "/Prefabs";

    /// <summary>
    /// Modelos del pack que no traen Avatar: comparten esqueleto con el primero de su familia, que sí
    /// lo trae (como en los prefabs de demostración del pack). Los que no están aquí van sin Avatar.
    /// </summary>
    private static readonly (string familia, string modeloConAvatar)[] AvataresDelPack =
    {
        ("Grummy", Pack + "/Characters/Meshes/Grummy_01.fbx"),
        ("Marshie", Pack + "/Characters/Meshes/Marshie_01.fbx"),
        ("Sweetie", Pack + "/Characters/Meshes/Sweetie_01.fbx"),
    };

    /// <summary>Parámetro de velocidad de los controladores del pack (0 quieto, 1 andar, 2 correr).</summary>
    private const string ParametroDeVelocidadDelPack = "Speed";

    private sealed class Papel
    {
        public string Nombre, ActorId, Prefab, Marca, Controlador, Reposo;
        public Color? Tinte;
        /// <summary>Objeto que lleva siempre en la mano derecha (modelo), su largo y sus materiales.</summary>
        public string EnLaMano;
        public float TamanoEnLaMano = 0.5f;
        public System.Func<StringBuilder, Material[]> MaterialesEnLaMano;
        public System.Func<(Vector3, Quaternion)?> PoseEnLaMano;
    }

    // ── Tabla de reparto ────────────────────────────────────────────────────────────────────
    // Nombre del papel, id de actor, prefab, marca de partida, controlador de animación si el modelo
    // no trae (o clip de reposo para crear uno), tinte (multiplica el color del material del pack).
    private static readonly Papel[] TablaDeReparto =
    {
        new Papel { Nombre = "Su Alteza Pompón", ActorId = "Actor_Pompon", Prefab = Demo + "/Prefabs/Characters/King's Grummy 02.prefab",
                    Marca = "M_Duelo_Trono", Tinte = new Color(1f, 0.16f, 0.2f) },
        new Papel { Nombre = "Conejo heraldo", ActorId = "Actor_Heraldo", Prefab = ModeloDelHeraldo,
                    Marca = "M_Puerta_Heraldo", Controlador = RutaControladorDelHeraldo,
                    EnLaMano = ModeloDeTrompetilla, TamanoEnLaMano = TamanoDeLaTrompetillaDelHeraldo, MaterialesEnLaMano = MaterialesDeTrompetilla, PoseEnLaMano = () => _trompetillaEnLaMano },
        new Papel { Nombre = "Heraldo suplente", ActorId = "Actor_HeraldoSuplente", Prefab = ModeloDelHeraldo,
                    Marca = "M_Duelo_Suplente", Controlador = RutaControladorDelHeraldo,
                    EnLaMano = ModeloDeTrompetilla, TamanoEnLaMano = TamanoDeLaTrompetillaDelHeraldo, MaterialesEnLaMano = MaterialesDeTrompetilla, PoseEnLaMano = () => _trompetillaEnLaMano },
        new Papel { Nombre = "Condesa Merengue", ActorId = "Actor_CondesaMerengue", Prefab = Pack + "/Characters/Prefabs/Marshie_01.prefab",
                    Marca = "M_Clases_Condesa", Controlador = Demo + "/Animation/Marshie_Controller.controller", Tinte = new Color(1f, 1f, 1f) },
        new Papel { Nombre = "Juez presidente", ActorId = "Actor_JuezPresidente", Prefab = Pack + "/Characters/Prefabs/Candy_King_01.prefab",
                    Marca = "M_Duelo_Juez2", Controlador = Demo + "/Animation/CandyKing_Controller.controller" },
        new Papel { Nombre = "Jueza Marshie", ActorId = "Actor_JuezMarshie", Prefab = Pack + "/Characters/Prefabs/Marshie_02.prefab",
                    Marca = "M_Duelo_Juez1", Controlador = Demo + "/Animation/Marshie_Controller.controller" },
        new Papel { Nombre = "Juez EyeBud", ActorId = "Actor_JuezEyeBud", Prefab = Pack + "/Characters/Prefabs/EyeBud_01.prefab",
                    Marca = "M_Duelo_Juez3", Reposo = Pack + "/Characters/Animations/Eye-Buds/Eye-Buds_Idle_1.anim" },
    };

    /// <summary>De este personaje del pack se copia el estilo (Quibli) de los materiales lisos (la trompetilla).</summary>
    private const string EstiloDeLosPersonajes = Pack + "/Characters/Prefabs/Grummy_01.prefab";

    private static void MontarReparto(Scene escena, Transform padre, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        AsegurarGestos(informe);
        int n = 0;
        foreach (var papel in TablaDeReparto)
        {
            if (!marcas.TryGetValue(papel.Marca, out var marca)) { informe.AppendLine($"Reparto: falta la marca {papel.Marca} ({papel.Nombre})."); continue; }
            if (PonerPapel(escena, padre, papel, marca.position, marca.rotation, informe) != null) n++;
        }
        informe.AppendLine($"Reparto: {n} de {TablaDeReparto.Length} personajes.");
    }

    /// <summary>
    /// Un personaje del pack en la escena, llamado «Papel (Actor_X)», instancia de la variante del papel
    /// (<see cref="PrefabDelPapel"/>). Null si no encuentra el modelo (lo apunta en el informe).
    /// </summary>
    private static GameObject PonerPapel(Scene escena, Transform padre, Papel papel, Vector3 posicion, Quaternion giro, StringBuilder informe)
    {
        var prefab = PrefabDelPapel(papel, informe);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
        go.name = $"{papel.Nombre} ({papel.ActorId})";
        go.transform.SetParent(padre, true);
        go.transform.SetPositionAndRotation(posicion, giro);
        return go;
    }

    /// <summary>Ruta de la variante de un papel: la que nombran los guiones en su REPARTO.</summary>
    private static string RutaDelPapel(Papel papel) => $"{CarpetaPrefabsDelReparto}/{papel.ActorId}.prefab";

    /// <summary>
    /// La variante del modelo del pack con la que sale el papel, rehecha en cada montaje (conserva su
    /// GUID): sin IA de demo, con animación, tinte y locomoción por datos. Null si falta el modelo.
    /// </summary>
    private static GameObject PrefabDelPapel(Papel papel, StringBuilder informe)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(papel.Prefab);
        if (modelo == null) { informe.AppendLine($"Reparto: no encuentro {papel.Prefab} ({papel.Nombre})."); return null; }
        AsegurarCarpeta(CarpetaPrefabsDelReparto);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        try
        {
            go.name = papel.ActorId;
            ApagarIaDeDemo(go);
            var animator = AsegurarAnimacion(go, papel, informe);
            if (papel.Tinte.HasValue) Tenir(go, papel);
            AsegurarLocomocion(go, animator);
            PonerEnLaMano(go, animator, papel, informe);
            return PrefabUtility.SaveAsPrefabAsset(go, RutaDelPapel(papel));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// El objeto que el papel lleva siempre en la mano derecha (<see cref="Papel.EnLaMano"/>), colgado del
    /// hueso de la mano (o de su punto de agarre, como el «RightHandProp» de los personajes del pack).
    /// </summary>
    private static void PonerEnLaMano(GameObject go, Animator animator, Papel papel, StringBuilder informe)
    {
        if (string.IsNullOrEmpty(papel.EnLaMano)) return;
        var mano = HuesoDelActor.De(animator, HumanBodyBones.RightHand);
        if (mano == null) { informe.AppendLine($"Reparto: {papel.Nombre} no tiene hueso de la mano derecha; va sin {System.IO.Path.GetFileNameWithoutExtension(papel.EnLaMano)}."); return; }
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(papel.EnLaMano);
        if (modelo == null) { informe.AppendLine($"Reparto: no encuentro {papel.EnLaMano} para la mano de {papel.Nombre}."); return; }
        var objeto = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        objeto.name = "En la mano: " + modelo.name;
        objeto.transform.SetParent(mano, false);
        foreach (var c in objeto.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        EscalarATamano(objeto, papel.TamanoEnLaMano);
        // Su pose en la mano (medida en un gesto, si la hay): posición sin escala y giro respecto al hueso.
        var (posicion, giro) = papel.PoseEnLaMano?.Invoke() ?? (Vector3.zero, Quaternion.identity);
        objeto.transform.SetPositionAndRotation(mano.position + mano.rotation * posicion, mano.rotation * giro);
        var materiales = papel.MaterialesEnLaMano?.Invoke(informe);
        if (materiales != null)
            foreach (var r in objeto.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = r.sharedMaterials.Length > 1 ? materiales : new[] { materiales[0] };
    }

    /// <summary>
    /// <see cref="AnimacionDeCorredorPorAnimator"/> con el parámetro de velocidad del pack, si su
    /// controlador lo tiene: es lo que anima el paso cuando un guion mueve al personaje.
    /// </summary>
    private static void AsegurarLocomocion(GameObject go, Animator animator)
    {
        bool tieneVelocidad = false;
        if (animator != null && animator.runtimeAnimatorController is AnimatorController controlador)
            foreach (var p in controlador.parameters)
                if (p.name == ParametroDeVelocidadDelPack && p.type == AnimatorControllerParameterType.Float) tieneVelocidad = true;
        var locomocion = go.GetComponent<AnimacionDeCorredorPorAnimator>();
        if (!tieneVelocidad)
        {
            if (locomocion != null) Object.DestroyImmediate(locomocion, true);
            return;
        }
        if (locomocion == null) locomocion = go.AddComponent<AnimacionDeCorredorPorAnimator>();
        // Los clips de andar del pack llevan eventos de paso (LeftStep/RightStep): su receptor del pack,
        // en el objeto del Animator, para que no salgan avisos de «no receiver».
        if (animator.GetComponent<CharacterAnimatorInvoker>() == null) animator.gameObject.AddComponent<CharacterAnimatorInvoker>();
        var so = new SerializedObject(locomocion);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("parametroDeVelocidad").stringValue = ParametroDeVelocidadDelPack;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Apaga la IA de demostración del pack (estados, recorridos, sonidos) y su NavMeshAgent.</summary>
    private static void ApagarIaDeDemo(GameObject go)
    {
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (c != null && c.GetType().Namespace == "ithappy") c.enabled = false;
        foreach (var agente in go.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))
            agente.enabled = false;
    }

    /// <summary>
    /// El Animator del modelo, con controlador. Si el modelo no trae Animator (los del pack sin Avatar
    /// propio), se le pone uno en la raíz con el Avatar de su familia; si no tiene controlador, el de la
    /// tabla o uno de reposo.
    /// </summary>
    private static Animator AsegurarAnimacion(GameObject go, Papel papel, StringBuilder informe)
    {
        var animator = go.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            animator = go.AddComponent<Animator>();
            animator.avatar = AvatarDeLaFamilia(papel.Prefab);
            animator.applyRootMotion = false;
        }
        // El controlador de la tabla manda (p. ej. una copia del del pack con gestos añadidos); si no hay, el que traiga el modelo.
        if (animator.runtimeAnimatorController != null && string.IsNullOrEmpty(papel.Controlador)) return animator;

        RuntimeAnimatorController controlador = null;
        if (!string.IsNullOrEmpty(papel.Controlador))
            controlador = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(papel.Controlador);
        else if (!string.IsNullOrEmpty(papel.Reposo))
            controlador = ControladorDeReposo(papel);
        if (controlador == null) { informe.AppendLine($"Reparto: sin controlador de animación para {papel.Nombre}."); return animator; }
        animator.runtimeAnimatorController = controlador;
        return animator;
    }

    /// <summary>El Avatar del modelo de su familia (tabla <see cref="AvataresDelPack"/>); null si no tiene.</summary>
    private static Avatar AvatarDeLaFamilia(string rutaDelModelo)
    {
        string nombre = System.IO.Path.GetFileNameWithoutExtension(rutaDelModelo);
        foreach (var (familia, modeloConAvatar) in AvataresDelPack)
        {
            if (!nombre.StartsWith(familia, System.StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modeloConAvatar))
                if (asset is Avatar avatar) return avatar;
        }
        return null;
    }

    /// <summary>Controlador de un solo estado con el clip de reposo del papel (se crea una vez).</summary>
    private static RuntimeAnimatorController ControladorDeReposo(Papel papel)
    {
        string ruta = $"{CarpetaReparto}/Animacion/{papel.ActorId}_Reposo.controller";
        var existente = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ruta);
        if (existente != null) return existente;
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(papel.Reposo);
        if (clip == null) return null;
        AsegurarCarpeta($"{CarpetaReparto}/Animacion");
        return AnimatorController.CreateAnimatorControllerAtPathWithClip(ruta, clip);
    }

    /// <summary>Cambia los materiales del personaje por copias teñidas (una por material original).</summary>
    private static void Tenir(GameObject go, Papel papel)
    {
        var copias = new Dictionary<Material, Material>();
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var materiales = r.sharedMaterials;
            for (int i = 0; i < materiales.Length; i++)
            {
                var original = materiales[i];
                if (original == null) continue;
                if (!copias.TryGetValue(original, out var copia))
                {
                    string sufijo = copias.Count == 0 ? "" : "_" + copias.Count;
                    copia = TinteDeMaterial.Copia(original, $"{CarpetaReparto}/Materiales/Mat_{papel.ActorId}{sufijo}.mat", papel.Tinte.Value);
                    copias[original] = copia;
                }
                materiales[i] = copia;
            }
            r.sharedMaterials = materiales;
        }
    }
}
