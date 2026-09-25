using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta de un tirón la escena del catálogo de animaciones y escribe el índice en texto.
///
/// EL PROBLEMA. Los nombres del pack no dicen qué hace cada animación (`Flourish`,
/// `Challenging_NoWeapon`, `FoundSomething_NoWeapon`…), así que al montar una secuencia no hay
/// forma de pedir la correcta sin ir probando una por una. Igual con `Eye01…Eye12` y
/// `Mouth01…Mouth12`.
///
/// LA SOLUCIÓN. Esta herramienta crea una escena de prueba en la que el personaje hace TODAS las
/// animaciones seguidas con su nombre real escrito en pantalla, y después enseña todas las piezas
/// de cara en primer plano. Se graba una vez y se rellena el fichero `docs/catalogo-animaciones.md`
/// con lo que hace cada una. A partir de ahí el catálogo existe y no hay que volver a adivinar.
///
/// El índice se escribe AQUÍ, no en el runner, y con el mismo orden con el que se van a reproducir:
/// eso es lo que permite emparejar el vídeo con la lista línea a línea.
///
/// Se descartan a propósito los estados cuyo movimiento es un Blend Tree o está vacío: son de
/// locomoción (mezclas de andar/correr según la velocidad) y con el personaje quieto no enseñan
/// nada. Salen listados al final del fichero para que conste que no se han olvidado.
/// </summary>
public static class AnimationCatalogueBuilder
{
    private const string PersonajePath = "Assets/_NPCs/Eldran.prefab";
    private const string EscenaPath    = "Assets/Scenes/Test/CatalogoAnimaciones.unity";
    private const string IndicePath    = "docs/catalogo-animaciones.md";

    [MenuItem("El Sendero/Personajes/Catálogo de animaciones (crear escena de prueba)")]
    public static void Construir()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Si hay un prefab de personaje seleccionado en el Project, se usa ese. Importa para el
        // repaso de caras: un personaje con barba tapa la boca y no vale para catalogar bocas.
        var prefab = Selection.activeObject as GameObject;
        if (prefab == null || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PersonajePath);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Catálogo de animaciones",
                "No encuentro el personaje en:\n" + PersonajePath + "\n\n" +
                "Selecciona en el Project el prefab del personaje que quieras usar y vuelve a " +
                "ejecutar esto.", "Vale");
            return;
        }

        if (!EditorUtility.DisplayDialog("Catálogo de animaciones",
                $"Se va a montar el catálogo con '{prefab.name}'.\n\n" +
                "Para usar otro personaje, cancela, selecciona su prefab en el Project y vuelve a " +
                "ejecutar esto. Para el repaso de CARAS conviene uno sin barba.",
                "Adelante", "Cancelar"))
            return;

        // ── Escena nueva y limpia ────────────────────────────────────────────
        var escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                                                 NewSceneMode.Single);

        var personaje = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        personaje.transform.position = Vector3.zero;
        personaje.transform.rotation = Quaternion.identity;

        // Suelo, para que no parezca que flota y se vean bien los pies.
        var suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        suelo.name = "Suelo";
        suelo.transform.localScale = Vector3.one * 2f;

        DesactivarEstorbos(personaje);

        var animator = personaje.GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            EditorUtility.DisplayDialog("Catálogo de animaciones",
                "El personaje no tiene Animator con controller, así que no hay nada que catalogar.",
                "Vale");
            return;
        }

        // ── Recoger todo lo que hay que enseñar ──────────────────────────────
        var entradas = new List<AnimationCatalogueRunner.Entrada>();
        var descartados = new List<string>();

        RecogerAnimaciones(animator.runtimeAnimatorController as AnimatorController,
                           entradas, descartados);
        RecogerPiezasDeCara(personaje.transform, entradas);

        // ── El runner ────────────────────────────────────────────────────────
        // Va EN el personaje, no en un objeto aparte: busca las piezas de cara entre sus propios
        // hijos y usa su transform para encuadrar la cámara. Colgarlo de otro objeto lo dejaría
        // sin caras y con la cámara mirando a donde no toca.
        personaje.name = "CATÁLOGO — dale al Play";
        var runner = personaje.AddComponent<AnimationCatalogueRunner>();

        var so = new SerializedObject(runner);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("camera").objectReferenceValue   = Object.FindAnyObjectByType<Camera>();

        var lista = so.FindProperty("entradas");
        lista.arraySize = entradas.Count;
        for (int i = 0; i < entradas.Count; i++)
        {
            var el = lista.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("tipo").enumValueIndex = (int)entradas[i].tipo;
            el.FindPropertyRelative("nombre").stringValue = entradas[i].nombre;
            el.FindPropertyRelative("capa").intValue = entradas[i].capa;
            el.FindPropertyRelative("nombreCapa").stringValue = entradas[i].nombreCapa;
            el.FindPropertyRelative("duracion").floatValue = entradas[i].duracion;
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // ── Guardar escena e índice ──────────────────────────────────────────
        Directory.CreateDirectory(Path.GetDirectoryName(EscenaPath));
        EditorSceneManager.SaveScene(escena, EscenaPath);

        EscribirIndice(entradas, descartados);

        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Catálogo de animaciones",
            $"Listo.\n\n" +
            $"· {ContarTipo(entradas, AnimationCatalogueRunner.TipoEntrada.Animacion)} animaciones\n" +
            $"· {ContarTipo(entradas, AnimationCatalogueRunner.TipoEntrada.Ojos)} piezas de ojos\n" +
            $"· {ContarTipo(entradas, AnimationCatalogueRunner.TipoEntrada.Boca)} piezas de boca\n\n" +
            "La escena ya está abierta: dale al Play y grábalo.\n\n" +
            "Mientras corre: ESPACIO pausa, → siguiente, ← anterior, R repetir.\n\n" +
            $"El índice en el mismo orden está en:\n{IndicePath}",
            "Vale");
    }

    /// Apaga lo que estorbaría en una escena de prueba: navegación, IA, colliders de interacción…
    /// El personaje solo tiene que quedarse quieto y animarse.
    private static void DesactivarEstorbos(GameObject personaje)
    {
        // TODOS fuera, sin excepciones. El runner habla directamente con el Animator, así que
        // cualquier script del NPC que también lo toque (el animador simple, la IA, el controlador
        // de emociones) solo puede pelearse con él y falsear el catálogo. El runner se añade
        // DESPUÉS de esto, así que no se apaga a sí mismo.
        foreach (var comportamiento in personaje.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comportamiento != null) comportamiento.enabled = false;
        }

        var agente = personaje.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        if (agente != null) agente.enabled = false;
    }

    private static void RecogerAnimaciones(AnimatorController controller,
        List<AnimationCatalogueRunner.Entrada> entradas, List<string> descartados)
    {
        if (controller == null) return;

        var vistos = new HashSet<string>();

        for (int capa = 0; capa < controller.layers.Length; capa++)
        {
            var estados = new List<AnimatorState>();
            Recorrer(controller.layers[capa].stateMachine, estados);

            estados.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

            foreach (var estado in estados)
            {
                if (!(estado.motion is AnimationClip clip) || clip == null)
                {
                    descartados.Add($"{estado.name} (capa {controller.layers[capa].name}) — " +
                                    (estado.motion == null ? "sin animación" : "Blend Tree de locomoción"));
                    continue;
                }

                // Un mismo nombre puede estar en las dos capas; con verlo una vez basta.
                if (!vistos.Add(estado.name)) continue;

                entradas.Add(new AnimationCatalogueRunner.Entrada
                {
                    tipo = AnimationCatalogueRunner.TipoEntrada.Animacion,
                    nombre = estado.name,
                    capa = capa,
                    nombreCapa = controller.layers[capa].name,
                    duracion = clip.length,
                });
            }
        }
    }

    private static void Recorrer(AnimatorStateMachine maquina, List<AnimatorState> destino)
    {
        if (maquina == null) return;
        foreach (var hijo in maquina.states) if (hijo.state != null) destino.Add(hijo.state);
        foreach (var sub in maquina.stateMachines) Recorrer(sub.stateMachine, destino);
    }

    private static void RecogerPiezasDeCara(Transform raiz,
        List<AnimationCatalogueRunner.Entrada> entradas)
    {
        var ojos = new List<string>();
        var bocas = new List<string>();
        Buscar(raiz, ojos, bocas);

        ojos.Sort(System.StringComparer.Ordinal);
        bocas.Sort(System.StringComparer.Ordinal);

        foreach (string o in ojos)
            entradas.Add(new AnimationCatalogueRunner.Entrada
            { tipo = AnimationCatalogueRunner.TipoEntrada.Ojos, nombre = o });

        foreach (string b in bocas)
            entradas.Add(new AnimationCatalogueRunner.Entrada
            { tipo = AnimationCatalogueRunner.TipoEntrada.Boca, nombre = b });
    }

    private static void Buscar(Transform raiz, List<string> ojos, List<string> bocas)
    {
        foreach (Transform hijo in raiz)
        {
            if (hijo.name.StartsWith("Eye")   && !ojos.Contains(hijo.name))  ojos.Add(hijo.name);
            if (hijo.name.StartsWith("Mouth") && !bocas.Contains(hijo.name)) bocas.Add(hijo.name);
            Buscar(hijo, ojos, bocas);
        }
    }

    private static int ContarTipo(List<AnimationCatalogueRunner.Entrada> entradas,
        AnimationCatalogueRunner.TipoEntrada tipo)
    {
        int n = 0;
        foreach (var e in entradas) if (e.tipo == tipo) n++;
        return n;
    }

    private static void EscribirIndice(List<AnimationCatalogueRunner.Entrada> entradas,
        List<string> descartados)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Catálogo de animaciones y caras");
        sb.AppendLine();
        sb.AppendLine("Generado por *El Sendero ▸ Personajes ▸ Catálogo de animaciones*.");
        sb.AppendLine();
        sb.AppendLine("El orden de las tablas es EXACTAMENTE el orden en el que la escena de prueba");
        sb.AppendLine("`Assets/Scenes/Test/CatalogoAnimaciones.unity` las reproduce, así que se puede");
        sb.AppendLine("ir rellenando la columna **Qué hace** viendo la grabación de corrido.");
        sb.AppendLine();
        sb.AppendLine("La columna **Cuándo usarla** es la importante: es la que se consulta al montar");
        sb.AppendLine("una secuencia para no tener que acordarse de los nombres del pack.");
        sb.AppendLine();

        sb.AppendLine("## Animaciones");
        sb.AppendLine();
        sb.AppendLine("| # | Nombre real | Capa | Dura | Qué hace | Cuándo usarla |");
        sb.AppendLine("|---|---|---|---|---|---|");

        int n = 0;
        foreach (var e in entradas)
        {
            if (e.tipo != AnimationCatalogueRunner.TipoEntrada.Animacion) continue;
            n++;
            sb.AppendLine($"| {n} | `{e.nombre}` | {e.nombreCapa} | {e.duracion:0.0}s | | |");
        }

        sb.AppendLine();
        sb.AppendLine("## Ojos");
        sb.AppendLine();
        sb.AppendLine("| # | Nombre real | Qué expresa | Emoción a la que pega |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var e in entradas)
        {
            if (e.tipo != AnimationCatalogueRunner.TipoEntrada.Ojos) continue;
            n++;
            sb.AppendLine($"| {n} | `{e.nombre}` | | |");
        }

        sb.AppendLine();
        sb.AppendLine("## Bocas");
        sb.AppendLine();
        sb.AppendLine("| # | Nombre real | Qué expresa | Emoción a la que pega |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var e in entradas)
        {
            if (e.tipo != AnimationCatalogueRunner.TipoEntrada.Boca) continue;
            n++;
            sb.AppendLine($"| {n} | `{e.nombre}` | | |");
        }

        if (descartados.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Descartados del catálogo");
            sb.AppendLine();
            sb.AppendLine("Estados de locomoción (Blend Trees) o sin animación asignada. Con el");
            sb.AppendLine("personaje quieto no enseñan nada, así que no entran en la grabación.");
            sb.AppendLine("Se listan para que conste que no se han olvidado.");
            sb.AppendLine();
            foreach (string d in descartados) sb.AppendLine($"- {d}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(IndicePath));
        File.WriteAllText(IndicePath, sb.ToString(), new UTF8Encoding(false));
    }
}
