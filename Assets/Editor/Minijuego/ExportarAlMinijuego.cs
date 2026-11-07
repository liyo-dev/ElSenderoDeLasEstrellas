#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Saca del juego lo que necesita el minijuego web «Cruza el portal» (proyecto SenderoMinijuego):
/// Will, Estela y Liam solo con su aspecto (sin lógica de juego, con un animador propio de cinco
/// estados) y los materiales del Sendero, todo en un .unitypackage. Las copias se montan en
/// Assets/_MinijuegoWeb y se borran al terminar, así que el juego no se queda con nada nuevo.
/// </summary>
public static class ExportarAlMinijuego
{
    const string Temporal = "Assets/_MinijuegoWeb";
    const string CarpetaSendero = "Assets/Art/World/Sendero";

    static readonly (string nombre, string prefab)[] Personajes =
    {
        ("Will", "Assets/Prefabs/_WILL.prefab"),
        ("Estela", "Assets/Prefabs/_ESTELA.prefab"),
        ("Liam", "Assets/Prefabs/_LIAM.prefab"),
    };

    static readonly string[] MaterialesDelSendero =
    {
        "Sendero_Cielo", "Sendero_SueloDeLuz", "Sendero_PolvoDeLuz",
        "Sendero_Puerta_Will", "Sendero_Puerta_Estela", "Sendero_Puerta_Liam",
        "Sendero_Arco_Will", "Sendero_Arco_Estela", "Sendero_Arco_Liam",
    };

    // Estados del animador del minijuego y palabras con las que se busca su clip en el animador
    // original (por orden de preferencia; se descartan los clips con palabras de «evitar»).
    static readonly (string estado, string[] buscar, bool bucle)[] Estados =
    {
        ("Quieto", new[] { "idle" }, true),
        ("Correr", new[] { "run", "sprint", "jog" }, true),
        ("Saltar", new[] { "jump" }, false),
        ("Celebrar", new[] { "victory", "cheer", "celebrat", "happy", "win" }, false),
        // Una muerte que acabe en el suelo (el clip no se repite: se queda en el último fotograma).
        ("Caer", new[] { "die", "death", "defeat", "knock", "fall" }, false),
    };
    static readonly string[] Evitar = { "weapon_", "sword", "bow", "crouch", "swim", "climb", "_start", "_end", "land", "back", "left", "right", "strafe" };

    static string Destino => Path.GetFullPath(Path.Combine(Application.dataPath, "../../SenderoMinijuego/Paquetes/DelJuego.unitypackage"));

    [MenuItem("El Sendero/Minijuego web/Exportar personajes y Sendero")]
    static void Exportar()
    {
        var informe = new StringBuilder();
        try
        {
            if (AssetDatabase.IsValidFolder(Temporal)) AssetDatabase.DeleteAsset(Temporal);
            AssetDatabase.CreateFolder("Assets", "_MinijuegoWeb");

            var rutas = new List<string>();
            foreach (var (nombre, prefab) in Personajes)
            {
                EditorUtility.DisplayProgressBar("Minijuego web", "Preparando a " + nombre, 0.2f);
                rutas.Add(PrepararPersonaje(nombre, prefab, informe));
            }

            foreach (var m in MaterialesDelSendero)
            {
                string ruta = $"{CarpetaSendero}/{m}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(ruta) != null) rutas.Add(ruta);
                else informe.AppendLine("No encuentro el material " + ruta + " (se exporta sin él).");
            }

            rutas.AddRange(IncludesDeShaders(rutas));

            // Todo lo generado va explícito: ExportPackage no siempre sigue las animaciones nuevas
            // que cuelgan de un controlador recién creado.
            AssetDatabase.SaveAssets();
            var generados = AssetDatabase.FindAssets("", new[] { Temporal }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            rutas.AddRange(generados);
            int anims = generados.Count(r => r.EndsWith(".anim"));
            informe.AppendLine($"Animaciones incluidas: {anims} (deberían ser 18).");

            EditorUtility.DisplayProgressBar("Minijuego web", "Exportando el paquete", 0.8f);
            Directory.CreateDirectory(Path.GetDirectoryName(Destino));
#pragma warning disable CS0618 // ExportPackage sigue funcionando; la API nueva aún no está documentada.
            AssetDatabase.ExportPackage(rutas.Distinct().ToArray(), Destino,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
#pragma warning restore CS0618

            long bytes = File.Exists(Destino) ? new FileInfo(Destino).Length : 0;
            informe.Insert(0, $"Paquete exportado: {Destino} ({bytes / 1048576f:0.0} MB)\n");
        }
        catch (Exception e)
        {
            informe.Insert(0, "No se ha podido exportar: " + e.Message + "\n");
            Debug.LogException(e);
        }
        finally
        {
            if (AssetDatabase.IsValidFolder(Temporal)) AssetDatabase.DeleteAsset(Temporal);
            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
        }
        Debug.Log("[ExportarAlMinijuego]\n" + informe);
        EditorUtility.DisplayDialog("Minijuego web", informe.ToString(), "Vale");
    }

    /// Copia del prefab con solo su aspecto: piezas visibles, esqueleto y un animador nuevo.
    static string PrepararPersonaje(string nombre, string rutaPrefab, StringBuilder informe)
    {
        var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var animador = AnimadorPrincipal(raiz);
            if (animador == null) throw new InvalidOperationException($"{rutaPrefab} no tiene un Animator humanoide.");
            var original = animador.runtimeAnimatorController;

            QuitarPiezasApagadas(raiz);
            QuitarLogica(raiz, animador);

            animador.runtimeAnimatorController = CrearAnimador(nombre, original, informe);
            animador.applyRootMotion = false;
            animador.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            raiz.name = "Minijuego_" + nombre;
            raiz.transform.localPosition = Vector3.zero;
            raiz.transform.localRotation = Quaternion.identity;
            string destino = $"{Temporal}/Minijuego_{nombre}.prefab";
            PrefabUtility.SaveAsPrefabAsset(raiz, destino);
            return destino;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    static Animator AnimadorPrincipal(GameObject raiz) =>
        raiz.GetComponentsInChildren<Animator>(true)
            .Where(a => a.avatar != null && a.avatar.isHuman)
            .OrderByDescending(a => a.runtimeAnimatorController != null ? a.runtimeAnimatorController.animationClips.Length : 0)
            .FirstOrDefault();

    /// Borra los objetos apagados (ropa y caras que no lleva puestas) salvo si contienen huesos
    /// que usan las piezas visibles.
    static void QuitarPiezasApagadas(GameObject raiz)
    {
        var usados = new HashSet<Transform>();
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.activeInHierarchy && r.enabled))
        {
            for (var t = r.transform; t != null; t = t.parent) usados.Add(t);
            if (r is SkinnedMeshRenderer smr)
            {
                foreach (var hueso in smr.bones.Append(smr.rootBone).Where(h => h != null))
                    for (var t = hueso; t != null; t = t.parent) usados.Add(t);
            }
        }
        var apagados = raiz.GetComponentsInChildren<Transform>(true)
            .Where(t => t != raiz.transform && !t.gameObject.activeSelf)
            .ToList();
        foreach (var t in apagados)
        {
            if (t == null) continue;
            bool contieneAlgoUsado = t.GetComponentsInChildren<Transform>(true).Any(usados.Contains);
            if (!contieneAlgoUsado) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    /// Quita todo lo que no sea aspecto: scripts, colisionadores, partículas, audio, otros animadores.
    static void QuitarLogica(GameObject raiz, Animator principal)
    {
        for (int pasada = 0; pasada < 12; pasada++)
        {
            var sobran = raiz.GetComponentsInChildren<Component>(true)
                .Where(c => c != null && !(c is Transform) && !(c is SkinnedMeshRenderer) && !(c is MeshRenderer) && !(c is MeshFilter) && c != principal)
                // Primero los scripts: son los que suelen exigir otros componentes (RequireComponent).
                .OrderBy(c => c is MonoBehaviour ? 0 : 1)
                .ToList();
            if (sobran.Count == 0) break;
            foreach (var c in sobran)
            {
                // Uno que otro aún exige ([RequireComponent]) se deja para la siguiente pasada,
                // cuando el que lo exige ya no esté.
                if (c == null || LoExigeOtro(c)) continue;
                UnityEngine.Object.DestroyImmediate(c);
            }
        }
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    }

    static bool LoExigeOtro(Component c)
    {
        var tipo = c.GetType();
        foreach (var otro in c.GetComponents<Component>())
        {
            if (otro == null || otro == c) continue;
            foreach (RequireComponent r in otro.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                if ((r.m_Type0 != null && r.m_Type0.IsAssignableFrom(tipo)) ||
                    (r.m_Type1 != null && r.m_Type1.IsAssignableFrom(tipo)) ||
                    (r.m_Type2 != null && r.m_Type2.IsAssignableFrom(tipo)))
                    return true;
            }
        }
        return false;
    }

    static AnimatorController CrearAnimador(string nombre, RuntimeAnimatorController original, StringBuilder informe)
    {
        var clips = original != null ? original.animationClips.Where(c => c != null).Distinct().ToArray() : Array.Empty<AnimationClip>();
        var controlador = AnimatorController.CreateAnimatorControllerAtPath($"{Temporal}/Minijuego_{nombre}.controller");
        var maquina = controlador.layers[0].stateMachine;

        informe.Append(nombre).Append(':');
        foreach (var (estado, buscar, bucle) in Estados)
        {
            // Correr y quieto salen de la locomoción del animador del juego (lo mismo que hace en
            // partida); el resto, por nombre.
            AnimationClip clip = null;
            if (estado == "Correr") clip = DeLaLocomocion(original, 1f);
            if (estado == "Quieto") clip = DeLaLocomocion(original, 0f);
            if (clip == null) clip = Elegir(clips, buscar);
            var st = maquina.AddState(estado);
            if (clip == null) { informe.Append($" {estado}=(ninguno)"); continue; }

            var copia = UnityEngine.Object.Instantiate(clip);
            copia.name = $"Minijuego_{nombre}_{estado}";
            var ajustes = AnimationUtility.GetAnimationClipSettings(copia);
            ajustes.loopTime = bucle;
            AnimationUtility.SetAnimationClipSettings(copia, ajustes);
            string rutaClip = $"{Temporal}/{copia.name}.anim";
            AssetDatabase.CreateAsset(copia, rutaClip);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(rutaClip) == null) informe.Append($" (¡no se pudo guardar {estado}!)");
            st.motion = copia;
            if (estado == "Quieto") maquina.defaultState = st;
            informe.Append($" {estado}={clip.name}");
        }
        CrearCapaDeMagia(nombre, original, controlador, informe);
        informe.AppendLine();
        return controlador;
    }

    /// Segunda capa, solo de cintura para arriba: el gesto de magia del juego (estado MagicRight
    /// de la capa UpperBody), para lanzar el hechizo al aro sin dejar de correr.
    static void CrearCapaDeMagia(string nombre, RuntimeAnimatorController original, AnimatorController controlador, StringBuilder informe)
    {
        var fuente = original as AnimatorController
            ?? (original as AnimatorOverrideController)?.runtimeAnimatorController as AnimatorController;
        AnimationClip clip = null;
        if (fuente != null)
        {
            foreach (var nombreEstado in new[] { "MagicRight", "MagicLeft", "MagicSpecial" })
            {
                clip = fuente.layers.SelectMany(c => EstadosDe(c.stateMachine))
                    .Where(e => e.name == nombreEstado).Select(e => e.motion).OfType<AnimationClip>().FirstOrDefault();
                if (clip != null) break;
            }
        }
        if (clip == null)
        {
            var todos = original != null ? original.animationClips : Array.Empty<AnimationClip>();
            clip = todos.Where(c => c != null && c.name.Contains("MagicAttackDirect")).OrderBy(c => c.name.Length).FirstOrDefault();
        }
        if (clip == null) { informe.Append(" Magia=(ninguno)"); return; }

        var copia = UnityEngine.Object.Instantiate(clip);
        copia.name = $"Minijuego_{nombre}_Magia";
        var ajustes = AnimationUtility.GetAnimationClipSettings(copia);
        ajustes.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(copia, ajustes);
        AssetDatabase.CreateAsset(copia, $"{Temporal}/{copia.name}.anim");

        // Máscara propia: brazos, tronco y cabeza; las piernas siguen corriendo.
        var mascara = new AvatarMask { name = $"Minijuego_{nombre}_Brazos" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var parte = (AvatarMaskBodyPart)i;
            bool piernas = parte == AvatarMaskBodyPart.Root || parte == AvatarMaskBodyPart.LeftLeg || parte == AvatarMaskBodyPart.RightLeg
                || parte == AvatarMaskBodyPart.LeftFootIK || parte == AvatarMaskBodyPart.RightFootIK;
            mascara.SetHumanoidBodyPartActive(parte, !piernas);
        }
        AssetDatabase.CreateAsset(mascara, $"{Temporal}/{mascara.name}.mask");

        controlador.AddLayer("Brazos");
        var capas = controlador.layers;
        var capa = capas[capas.Length - 1];
        capa.defaultWeight = 1f;
        capa.avatarMask = mascara;
        capas[capas.Length - 1] = capa;
        controlador.layers = capas;

        var maquina = capa.stateMachine;
        var vacio = maquina.AddState("Vacio");
        maquina.defaultState = vacio;
        var magia = maquina.AddState("Magia");
        magia.motion = copia;
        var vuelta = magia.AddTransition(vacio);
        vuelta.hasExitTime = true;
        vuelta.exitTime = 0.85f;
        vuelta.duration = 0.15f;
        informe.Append($" Magia={clip.name}");
    }

    /// El clip que pondría el animador del juego con el jugador avanzando a `avance`
    /// (InputVertical: 0 quieto, 1 corriendo), en el árbol de mezcla «Free Locomotion» de
    /// Invector. Los demás parámetros, con su valor por defecto.
    static AnimationClip DeLaLocomocion(RuntimeAnimatorController original, float avance)
    {
        var controlador = original as AnimatorController
            ?? (original as AnimatorOverrideController)?.runtimeAnimatorController as AnimatorController;
        if (controlador == null) return null;

        var valores = controlador.parameters.ToDictionary(p => p.name, p =>
            p.type == AnimatorControllerParameterType.Int ? p.defaultInt :
            p.type == AnimatorControllerParameterType.Bool ? (p.defaultBool ? 1f : 0f) : p.defaultFloat);
        valores["InputVertical"] = avance;
        valores["InputMagnitude"] = avance;
        valores["IsGrounded"] = 1f;

        foreach (var capa in controlador.layers)
        {
            foreach (var arbol in EstadosDe(capa.stateMachine).Select(s => s.motion).OfType<BlendTree>())
            {
                if (!arbol.name.ToLowerInvariant().Contains("locomotion")) continue;
                var clip = Evaluar(arbol, valores);
                if (clip != null) return clip;
            }
        }
        return null;
    }

    static IEnumerable<AnimatorState> EstadosDe(AnimatorStateMachine maquina)
    {
        foreach (var s in maquina.states) yield return s.state;
        foreach (var sub in maquina.stateMachines)
            foreach (var s in EstadosDe(sub.stateMachine)) yield return s;
    }

    /// Baja por el árbol de mezcla quedándose con el hijo que más pesa con esos parámetros.
    static AnimationClip Evaluar(Motion m, Dictionary<string, float> valores)
    {
        if (m is AnimationClip clip) return clip;
        if (!(m is BlendTree arbol) || arbol.children.Length == 0) return null;
        float V(string nombre) => nombre != null && valores.TryGetValue(nombre, out var v) ? v : 0f;
        ChildMotion hijo;
        if (arbol.blendType == BlendTreeType.Simple1D)
        {
            float x = V(arbol.blendParameter);
            hijo = arbol.children.OrderBy(c => Mathf.Abs(c.threshold - x)).First();
        }
        else if (arbol.blendType == BlendTreeType.Direct)
        {
            hijo = arbol.children.OrderByDescending(c => V(c.directBlendParameter)).First();
        }
        else
        {
            var punto = new Vector2(V(arbol.blendParameter), V(arbol.blendParameterY));
            hijo = arbol.children.OrderBy(c => (c.position - punto).sqrMagnitude).First();
        }
        return Evaluar(hijo.motion, valores);
    }

    static AnimationClip Elegir(AnimationClip[] clips, string[] buscar)
    {
        foreach (var palabra in buscar)
        {
            var candidatos = clips
                .Where(c => c.name.ToLowerInvariant().Contains(palabra))
                .Where(c => !Evitar.Any(e => c.name.ToLowerInvariant().Contains(e)))
                .OrderBy(c => c.name.Length)
                .ToList();
            if (candidatos.Count > 0) return candidatos[0];
        }
        return null;
    }

    /// AssetDatabase no sigue los #include de los shaders: se añaden a mano los que viven en Assets.
    static IEnumerable<string> IncludesDeShaders(IEnumerable<string> rutas)
    {
        var shaders = AssetDatabase.GetDependencies(rutas.ToArray(), true)
            .Where(r => r.StartsWith("Assets/") && (r.EndsWith(".shader") || r.EndsWith(".hlsl") || r.EndsWith(".cginc")));
        var pendientes = new Queue<string>(shaders);
        var vistos = new HashSet<string>(pendientes);
        var include = new Regex("#include\\s+\"([^\"]+)\"");
        while (pendientes.Count > 0)
        {
            string ruta = pendientes.Dequeue();
            if (!File.Exists(ruta)) continue;
            foreach (Match m in include.Matches(File.ReadAllText(ruta)))
            {
                string ref_ = m.Groups[1].Value;
                if (ref_.StartsWith("Packages/")) continue;
                string completa = ref_.StartsWith("Assets/") ? ref_ : Path.Combine(Path.GetDirectoryName(ruta), ref_).Replace('\\', '/');
                completa = Path.GetFullPath(completa).Replace('\\', '/');
                string proyecto = Path.GetFullPath(".").Replace('\\', '/') + "/";
                if (!completa.StartsWith(proyecto)) continue;
                completa = completa.Substring(proyecto.Length);
                if (vistos.Add(completa)) pendientes.Enqueue(completa);
            }
        }
        return vistos;
    }
}
#endif
