#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Game.NPC;

/// Comprueba que TODOS los gestos que piden las secuencias existen de verdad, en el Animator
/// Controller que le toca a cada actor.
///
/// ── El fallo que esto caza ────────────────────────────────────────────────────────────────────
/// El campo `gesture` de un beat es un string libre y nadie lo comprueba antes de partida. Pedir
/// un estado que no existe no da error de compilación ni excepción: es un no-op silencioso. El
/// beat se da por reproducido, la secuencia sigue su curso, y el personaje simplemente no hace
/// nada. Solo se ve grabando, y ni siquiera siempre.
///
/// Ha pasado dos veces, y las dos costaron una grabación entera:
///
///   · INC-214 — un beat le pedía a Will `Attack2`. Existe, pero en el controller de los NPCs, no
///               en el suyo. El gag empezaba sin que nadie lanzara ningún hechizo.
///   · INC-264 — diez beats por fase pedían `Fidget`, que es un PARÁMETRO de `NPC_NoWeapon` y no
///               un estado. «La gente toda quieta», tres grabaciones seguidas.
///
/// Los dos son el mismo fallo con distinta cara: un nombre que existe EN ALGUNA PARTE y que no es
/// un estado de ESE controller.
///
/// ── Qué mira, y por qué no basta con mirar un controller ──────────────────────────────────────
/// Cada actor tiene el suyo. `Player` usa `Invector@BasicLocomotion` y los NPCs `NPC_NoWeapon`, y
/// comparten casi todo el vocabulario pero no todo — que es exactamente lo que hace que INC-214
/// sea tan fácil de cometer. Así que el controller no se elige por convenio: se busca el prefab
/// del actor por su Persistence ID y se lee el controller que lleva puesto de verdad. Si mañana un
/// NPC estrena controller propio, esto se entera solo.
///
/// ── Tres avisos distintos ─────────────────────────────────────────────────────────────────────
///   ERROR  — el estado no existe en ese controller. Se dice si el nombre es un PARÁMETRO (el caso
///            de `Fidget`) o si existe en el OTRO controller (el caso de `Attack2`), porque el
///            arreglo es distinto. Y si no es ninguno de los dos, se sugiere el más parecido.
///   AVISO  — el estado existe en DOS capas a la vez. En runtime gana UpperBody
///            (`AnimatorLayerUtil.ResolveLayer` prueba esa primero), así que un gesto de cuerpo
///            entero saldría de cintura para arriba con las piernas andando por su cuenta.
///   NOTA   — no hay prefab para ese actorId. No es un fallo (puede ser un actor de escena o un
///            Transform suelto), pero significa que ese gesto se ha validado contra el controller
///            por defecto y no contra el suyo.
///
/// ── Cómo recorre los beats ────────────────────────────────────────────────────────────────────
/// Por reflexión, no con una lista de tipos escrita a mano. Busca cualquier campo llamado
/// `gesture` (string) o `gestures` (string[]) esté donde esté, y lo valida contra el `actorId` que
/// tenga más cerca — el del propio objeto si lo tiene, y si no el que venga heredado. Así cubre de
/// golpe `GestureBeat`, el gesto de una frase de `SayBeat`, los gestos de reacción de
/// `DialogueReaction` dentro de un `DialogueBeat`, y los hijos de un `ParallelBeat`. Y cubrirá
/// también el beat nuevo que se invente alguien dentro de seis meses, sin tocar este archivo,
/// siempre que llame `gesture` a su campo de gesto.
public static class ValidarGestosDeSecuencias
{
    private const string ControllerNpcs =
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller";

    private const string ControllerJugador =
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";

    private const string PrefabJugador = "Assets/Prefabs/_WILL.prefab";

    /// El actorId que las secuencias usan para Will. Ver el tooltip de `GestureBeat.actorId`.
    private const string IdJugador = "Player";

    private static readonly string[] CarpetasDePersonajes = { "Assets/_NPCs", "Assets/Prefabs" };

    [MenuItem("El Sendero/Animación/Validar gestos de todas las secuencias")]
    public static void Ejecutar()
    {
        var porDefecto = Cargar(ControllerNpcs);
        var delJugador = Cargar(ControllerJugador);
        if (porDefecto == null) return;

        Dictionary<string, AnimatorController> porActor = MapaDeActores(delJugador);

        string[] guids = AssetDatabase.FindAssets("t:SequenceDefinition");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[ValidarGestos] No he encontrado ningún SequenceDefinition en el proyecto.");
            return;
        }

        int errores = 0, avisos = 0, revisados = 0, secuencias = 0;

        foreach (string guid in guids)
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ruta);
            if (def == null) continue;

            secuencias++;
            var hallazgos = new List<Hallazgo>();
            Recorrer(def, null, hallazgos, new HashSet<object>(), 0);

            // Los avisos se AGRUPAN antes de imprimirlos. Un prólogo con diez aldeanos que
            // gesticulan cien veces generaba cien líneas idénticas diciendo lo mismo de los
            // mismos diez actores: eso no es un informe, es un muro que nadie lee dos veces (y
            // el muro de log es justo lo que escondió el aviso de INC-274).
            var ambiguos = new SortedDictionary<string, int>();
            var desconocidos = new SortedSet<string>();

            foreach (var h in hallazgos)
            {
                revisados++;

                bool conocido = !string.IsNullOrEmpty(h.ActorId)
                    && porActor.TryGetValue(h.ActorId, out var suyo)
                    && suyo != null;

                AnimatorController controller = conocido ? porActor[h.ActorId] : porDefecto;

                var capas = AnimatorControllerEstados.CapasConElEstado(controller, h.Gesto);

                // Un error SÍ se imprime uno a uno: son pocos, y cada uno necesita su sitio
                // exacto y su pista para poder arreglarlo.
                if (capas.Count == 0)
                {
                    errores++;
                    Debug.LogError(
                        $"[ValidarGestos] {def.name} · {h.Donde}: el gesto '{h.Gesto}' NO existe en " +
                        $"'{controller.name}', que es el controller de '{h.ActorId}'. {Pista(h, controller, porDefecto, delJugador)}",
                        def);
                }
                else if (capas.Count > 1)
                {
                    string clave = $"'{h.Gesto}' en '{controller.name}' " +
                                   $"({string.Join(" + ", capas.Select(i => controller.layers[i].name))})";
                    ambiguos.TryGetValue(clave, out int n);
                    ambiguos[clave] = n + 1;
                }

                if (!conocido && !string.IsNullOrEmpty(h.ActorId))
                    desconocidos.Add(h.ActorId);
            }

            if (ambiguos.Count > 0)
            {
                avisos += ambiguos.Count;
                Debug.LogWarning(
                    $"[ValidarGestos] {def.name}: {ambiguos.Count} gesto(s) que existen en DOS capas. " +
                    "En runtime gana UpperBody, así que si alguno es de cuerpo entero saldrá de cintura " +
                    "para arriba y las piernas seguirán a lo suyo.\n    " +
                    string.Join("\n    ", ambiguos.Select(kv => $"{kv.Key} — {kv.Value} vez/veces")),
                    def);
            }

            if (desconocidos.Count > 0)
            {
                avisos++;
                Debug.LogWarning(
                    $"[ValidarGestos] {def.name}: {desconocidos.Count} actor(es) de los que no encuentro " +
                    $"ni prefab ni instancia en las escenas abiertas, validados contra '{porDefecto.name}'. " +
                    "Si alguno lleva otro controller, de ese no se dice nada. Un actor que se crea en " +
                    "tiempo de ejecución aparecerá aquí salvo que su escena esté abierta.\n    " +
                    string.Join(", ", desconocidos),
                    def);
            }
        }

        string resumen = $"[ValidarGestos] {secuencias} secuencia(s), {revisados} gesto(s) revisado(s): " +
                         $"{errores} error(es), {avisos} aviso(s).";

        if (errores > 0) Debug.LogError(resumen);
        else if (avisos > 0) Debug.LogWarning(resumen + " Ningún gesto falta; los avisos son de matiz.");
        else Debug.Log(resumen + " Todo existe y ninguno es ambiguo.");
    }

    /// El texto que convierte un «no existe» en un arreglo. Los tres casos que se han dado de
    /// verdad en el proyecto, por orden de probabilidad.
    private static string Pista(Hallazgo h, AnimatorController suyo,
                                AnimatorController npcs, AnimatorController jugador)
    {
        if (AnimatorControllerEstados.EsParametro(suyo, h.Gesto))
            return $"OJO: '{h.Gesto}' es un PARÁMETRO de ese controller, no un estado — el caso de INC-264. " +
                   "Un parámetro no se puede reproducir; hace falta el nombre de un estado.";

        AnimatorController otro = suyo == jugador ? npcs : jugador;
        if (otro != null && AnimatorControllerEstados.Existe(otro, h.Gesto))
            return $"Existe, pero en '{otro.name}' — el caso de INC-214. O el actor está equivocado, " +
                   "o hay que dar de alta ese estado también en el controller de este.";

        string parecido = MasParecido(h.Gesto, AnimatorControllerEstados.TodosLosEstados(suyo));
        return parecido != null ? $"¿Querías decir '{parecido}'?" : "No hay ningún estado con un nombre parecido.";
    }

    // ── Tercer validador: el vocabulario de emociones ───────────────────────
    //
    // `EmotionProfile` es un asset de datos COMPARTIDO: lo leen `NPCSimpleAnimator.PlayBodyEmotion()`
    // y `PlayerDialogueAnimator.PlayBodyEmotion()`, los dos. Su contrato implícito es «esto lo
    // entiende cualquier personaje», y hasta el 20 sep 2026 no lo comprobaba nada.
    //
    // Por eso `Challenging_NoWeapon` (el gesto de la emoción `Determined`) y
    // `SenseSomethingSearching_NoWeapon` llevaban sin existir en el controller del jugador sin que
    // saltara ninguna alarma: al ponerle esa emoción a Will le cambiaba la cara y el cuerpo se
    // quedaba quieto. Los otros 20 estados del perfil sí estaban en los dos, así que la regla ya
    // existía de hecho — solo que nadie la estaba comprobando.
    //
    // Se miran DOS cosas, y la segunda es la sutil:
    //   1. Que el estado exista en los dos controllers.
    //   2. Que viva en la MISMA capa en los dos. Un gesto que en los NPCs está en UpperBody y en
    //      el jugador en la capa base se ve distinto según quién sienta la emoción: al NPC le
    //      mueve el torso mientras camina y al jugador le congela las piernas. Mismo dato, dos
    //      resultados, y ningún error por ninguna parte.

    [MenuItem("El Sendero/Animación/Validar el vocabulario de emociones")]
    public static void ValidarEmociones()
    {
        var npcs = Cargar(ControllerNpcs);
        var jugador = Cargar(ControllerJugador);
        if (npcs == null || jugador == null) return;

        string[] guids = AssetDatabase.FindAssets("t:EmotionProfile");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[ValidarEmociones] No he encontrado ningún EmotionProfile en el proyecto.");
            return;
        }

        int faltan = 0, descolocados = 0, revisados = 0;

        foreach (string guid in guids)
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var perfil = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ruta);
            if (perfil == null) continue;

            foreach (string estado in EstadosDelPerfil(perfil))
            {
                revisados++;

                var enNpcs = AnimatorControllerEstados.CapasConElEstado(npcs, estado);
                var enJugador = AnimatorControllerEstados.CapasConElEstado(jugador, estado);

                if (enNpcs.Count == 0 || enJugador.Count == 0)
                {
                    faltan++;
                    string quien = enNpcs.Count == 0 ? npcs.name : jugador.name;
                    Debug.LogError(
                        $"[ValidarEmociones] {perfil.name}: el estado '{estado}' NO existe en " +
                        $"'{quien}'. El perfil de emociones lo comparten NPCs y jugador, así que a " +
                        "quien no lo tenga se le cambia la cara y el cuerpo se le queda quieto.",
                        perfil);
                    continue;
                }

                string capaNpcs = npcs.layers[enNpcs[0]].name;
                string capaJugador = jugador.layers[enJugador[0]].name;

                if (capaNpcs != capaJugador)
                {
                    descolocados++;
                    Debug.LogWarning(
                        $"[ValidarEmociones] {perfil.name}: el estado '{estado}' existe en los dos, " +
                        $"pero en capas distintas — '{capaNpcs}' en los NPCs y '{capaJugador}' en el " +
                        "jugador. La misma emoción se verá distinta según quién la sienta.",
                        perfil);
                }
            }
        }

        string resumen = $"[ValidarEmociones] {guids.Length} perfil(es), {revisados} estado(s) " +
                         $"revisado(s): {faltan} que falta(n), {descolocados} en capa distinta.";

        if (faltan > 0) Debug.LogError(resumen);
        else if (descolocados > 0) Debug.LogWarning(resumen);
        else Debug.Log(resumen + " Todo el vocabulario existe en los dos controllers y en la misma capa.");
    }

    /// Todos los nombres de estado que nombra un perfil: el de cada emoción, sus variantes, y las
    /// animaciones neutras de hablar. Se lee por `SerializedObject` y no por la clase, para que
    /// siga funcionando si al perfil le añaden campos.
    private static IEnumerable<string> EstadosDelPerfil(ScriptableObject perfil)
    {
        var so = new SerializedObject(perfil);
        var vistos = new HashSet<string>();

        void Anotar(SerializedProperty p)
        {
            if (p == null) return;
            string v = p.stringValue;
            if (!string.IsNullOrWhiteSpace(v)) vistos.Add(v.Trim());
        }

        var emociones = so.FindProperty("emotions");
        if (emociones != null && emociones.isArray)
        {
            for (int i = 0; i < emociones.arraySize; i++)
            {
                var e = emociones.GetArrayElementAtIndex(i);
                Anotar(e.FindPropertyRelative("bodyAnimStateName"));

                var variantes = e.FindPropertyRelative("bodyAnimVariants");
                if (variantes != null && variantes.isArray)
                    for (int j = 0; j < variantes.arraySize; j++)
                        Anotar(variantes.GetArrayElementAtIndex(j));
            }
        }

        var neutras = so.FindProperty("neutralBodyAnims");
        if (neutras != null && neutras.isArray)
            for (int i = 0; i < neutras.arraySize; i++)
                Anotar(neutras.GetArrayElementAtIndex(i));

        return vistos;
    }

    // ── Segundo validador: los nombres escritos a pelo en C# ────────────────
    //
    // El validador de arriba cubre los `SequenceDefinition`, que es donde vive el 90% de los
    // gestos. Pero no todos: hay nombres de estado escritos directamente en el código, como
    // defaults de `[SerializeField]` o como hashes cacheados. Ese es exactamente el hueco por el
    // que `Attack2` sobrevivió en `OliverSaludoSequencer` después de que INC-214 lo arreglara en
    // el asset — el default de C# se quedó como estaba, y cualquier escena nueva nacía rota.
    //
    // Aquí no se puede saber a qué actor va cada literal (es un string suelto en un fichero), así
    // que esto NO dice "esto está mal": dice "este nombre existe en UN SOLO controller, mira si
    // quien lo usa es del otro". El juicio lo pone quien lo lee. A cambio, se cubre todo el
    // proyecto de una pasada y sale en segundos.

    /// Nombres de estado que además son palabras corrientes de C# o del proyecto, y que como
    /// literales no significan casi nunca "este estado del Animator". Sin esta lista el informe
    /// es tres cuartas partes ruido y nadie lo lee dos veces.
    private static readonly HashSet<string> Ambiguos = new HashSet<string>
    {
        "null", "idle", "Jump", "Walk", "Run", "Dive", "Blend Tree", "Sliding", "GrabItem",
        "Swimming", "Falling", "Landing", "Roll", "StepUp", "QuickClimb", "Type_Equip",
    };

    [MenuItem("El Sendero/Animación/Validar nombres de estado escritos en C#")]
    public static void ValidarCodigo()
    {
        var npcs = Cargar(ControllerNpcs);
        var jugador = Cargar(ControllerJugador);
        if (npcs == null || jugador == null) return;

        HashSet<string> deNpcs = AnimatorControllerEstados.TodosLosEstados(npcs);
        HashSet<string> deJugador = AnimatorControllerEstados.TodosLosEstados(jugador);

        var literal = new Regex("\"([^\"\\n]{2,60})\"");
        int sueltos = 0, ficheros = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", CarpetasDeCodigo))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (!ruta.EndsWith(".cs")) continue;

            string[] lineas;
            try { lineas = System.IO.File.ReadAllLines(ruta); } catch { continue; }

            bool alguno = false;

            for (int i = 0; i < lineas.Length; i++)
            {
                string recortada = lineas[i].TrimStart();

                // Los comentarios hablan DE los estados constantemente ("Attack2 SÍ es un estado
                // real del Base Layer", y media docena más). Nombrar uno no es usarlo.
                if (recortada.StartsWith("//") || recortada.StartsWith("*")) continue;

                foreach (Match m in literal.Matches(lineas[i]))
                {
                    string nombre = m.Groups[1].Value;
                    if (Ambiguos.Contains(nombre)) continue;

                    bool enNpcs = deNpcs.Contains(nombre);
                    bool enJugador = deJugador.Contains(nombre);
                    if (enNpcs == enJugador) continue; // en los dos, o en ninguno: aquí no se dice nada

                    string tiene = enNpcs ? npcs.name : jugador.name;
                    string noTiene = enNpcs ? jugador.name : npcs.name;

                    Debug.LogWarning(
                        $"[ValidarGestosC#] {ruta}:{i + 1} — '{nombre}' existe SOLO en '{tiene}', " +
                        $"no en '{noTiene}'. Si quien lo usa puede ser un personaje del otro, ahí es " +
                        "un no-op silencioso (el caso de INC-214).\n    " + recortada.Trim());

                    sueltos++;
                    alguno = true;
                }
            }

            if (alguno) ficheros++;
        }

        string resumen = $"[ValidarGestosC#] {sueltos} literal(es) de un solo controller en {ficheros} fichero(s). " +
                         "No todos son fallos: el informe dice dónde mirar, no qué está mal.";

        if (sueltos > 0) Debug.LogWarning(resumen);
        else Debug.Log("[ValidarGestosC#] Ningún nombre de estado de un solo controller escrito en C#.");
    }

    private static readonly string[] CarpetasDeCodigo = { "Assets/Scripts", "Assets/Editor" };

    // ── Recorrido de los beats ───────────────────────────────────────────────

    private readonly struct Hallazgo
    {
        public readonly string ActorId, Gesto, Donde;
        public Hallazgo(string actorId, string gesto, string donde)
        { ActorId = actorId; Gesto = gesto; Donde = donde; }
    }

    private const BindingFlags Campos = BindingFlags.Public | BindingFlags.Instance;

    /// Baja por el objeto buscando campos `gesture` / `gestures`, arrastrando el `actorId` que
    /// tenga más cerca. `visitados` evita dar vueltas si alguna vez hay una referencia circular.
    private static void Recorrer(object obj, string actorHeredado, List<Hallazgo> salida,
                                 HashSet<object> visitados, int profundidad)
    {
        if (obj == null || profundidad > 12) return;
        if (obj is string || obj.GetType().IsPrimitive || obj.GetType().IsEnum) return;
        // La raíz (el propio SequenceDefinition) sí se abre; cualquier OTRA referencia a un
        // asset, no — un DialogueAsset enlazado desde un beat tiene sus propios gestos, pero los
        // valida su propia pasada, no esta, y seguir el enlace sería recorrerlo una vez por cada
        // beat que lo nombre.
        if (obj is UnityEngine.Object && profundidad > 0) return;
        if (!visitados.Add(obj)) return;

        // Una lista o un array: cada elemento se recorre con el mismo actor heredado.
        if (obj is IEnumerable lista && !(obj is string))
        {
            foreach (var elemento in lista)
                Recorrer(elemento, actorHeredado, salida, visitados, profundidad + 1);
            return;
        }

        Type tipo = obj.GetType();
        FieldInfo[] campos = tipo.GetFields(Campos);

        // Primero el actorId del propio objeto: manda sobre el heredado. Un DialogueReaction dentro
        // de un DialogueBeat reacciona con SU actor, no con el que habla.
        string actor = actorHeredado;
        var campoActor = campos.FirstOrDefault(f => f.Name == "actorId" && f.FieldType == typeof(string));
        if (campoActor != null)
        {
            string propio = campoActor.GetValue(obj) as string;
            if (!string.IsNullOrEmpty(propio)) actor = propio;
        }

        string donde = tipo.Name;

        foreach (var campo in campos)
        {
            object valor;
            try { valor = campo.GetValue(obj); } catch { continue; }
            if (valor == null) continue;

            if (campo.Name == "gesture" && valor is string uno)
            {
                if (!string.IsNullOrEmpty(uno)) salida.Add(new Hallazgo(actor, uno, donde));
                continue;
            }

            if (campo.Name == "gestures" && valor is string[] varios)
            {
                foreach (string g in varios)
                    if (!string.IsNullOrEmpty(g)) salida.Add(new Hallazgo(actor, g, $"{donde}.gestures"));
                continue;
            }

            if (campo.FieldType == typeof(string) || campo.FieldType.IsPrimitive || campo.FieldType.IsEnum)
                continue;

            Recorrer(valor, actor, salida, visitados, profundidad + 1);
        }
    }

    // ── De actorId a su Animator Controller de verdad ────────────────────────

    /// Recorre los prefabs del proyecto que llevan un NPC y monta el mapa Persistence ID →
    /// controller. Es una pasada por todos los prefabs, que en el Editor es barato y se hace una
    /// vez por ejecución del menú.
    private static Dictionary<string, AnimatorController> MapaDeActores(AnimatorController delJugador)
    {
        var mapa = new Dictionary<string, AnimatorController>();

        var will = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabJugador);
        var suyo = will != null ? ControllerDe(will.GetComponentInChildren<PlayerDialogueAnimator>(true)) : null;
        mapa[IdJugador] = suyo != null ? suyo : delJugador;

        // Solo donde viven los personajes del juego. Barrer "t:Prefab" a secas recorrería
        // también los miles de prefabs de los packs de la Asset Store, que no tienen NPCs y
        // convertirían medio segundo en minutos.
        // Muchos actores de una secuencia NO existen como prefab con su Persistence ID: nacen en
        // tiempo de ejecución. Los diez aldeanos del prólogo son el caso claro — en la escena solo
        // hay puntos de spawn, y quién sale de cada uno lo dice un `NpcRosterSO`, que empareja
        // `persistenceId` con el `prefab` que se instancia. Sin leer el roster, los diez salían
        // como «no encuentro prefab» una vez por cada gesto que hacen: cien líneas para decir lo
        // mismo de las mismas diez personas.
        //
        // Se lee por `SerializedObject` y no por la clase, para que esto siga funcionando si al
        // roster le cambian los campos de sitio.
        foreach (string guid in AssetDatabase.FindAssets("t:NpcRosterSO"))
        {
            var roster = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (roster == null) continue;

            var entradas = new SerializedObject(roster).FindProperty("entries");
            if (entradas == null || !entradas.isArray) continue;

            for (int i = 0; i < entradas.arraySize; i++)
            {
                var e = entradas.GetArrayElementAtIndex(i);
                string id = e.FindPropertyRelative("persistenceId")?.stringValue;
                var prefab = e.FindPropertyRelative("prefab")?.objectReferenceValue as GameObject;
                if (string.IsNullOrEmpty(id) || prefab == null || mapa.ContainsKey(id)) continue;

                var controller = ControllerDe(prefab.GetComponentInChildren<NPCSimpleAnimator>(true));
                if (controller != null) mapa[id] = controller;
            }
        }

        // Y las ESCENAS ABIERTAS, para los actores que sí están puestos a mano. Van después del
        // roster porque el roster es quien manda sobre lo que se instancia al jugar.
        foreach (var npc in UnityEngine.Object.FindObjectsByType<NPCBehaviourManagerV2>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string id = npc.PersistenceId;
            if (string.IsNullOrEmpty(id) || mapa.ContainsKey(id)) continue;

            var controller = ControllerDe(npc.GetComponent<NPCSimpleAnimator>());
            if (controller != null) mapa[id] = controller;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", CarpetasDePersonajes))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (go == null) continue;

            foreach (var npc in go.GetComponentsInChildren<NPCBehaviourManagerV2>(true))
            {
                string id = npc.PersistenceId;
                if (string.IsNullOrEmpty(id) || mapa.ContainsKey(id)) continue;

                var controller = ControllerDe(npc.GetComponent<NPCSimpleAnimator>());
                if (controller != null) mapa[id] = controller;
            }
        }

        return mapa;
    }

    /// Saca el controller del `Animator` que ese componente tiene puesto. Se lee del campo
    /// serializado (aunque sea privado) en vez de por `GetComponent`, porque es lo que de verdad
    /// se va a usar en partida: si alguien apuntó el campo a otro Animator del prefab —Will lleva
    /// varios, uno por la capa, otro por el arco—, esto lo ve y `GetComponent` no.
    private static AnimatorController ControllerDe(Component componente)
    {
        if (componente == null) return null;

        var so = new SerializedObject(componente);
        var prop = so.FindProperty("animator");
        var anim = prop != null ? prop.objectReferenceValue as Animator : null;

        if (anim == null) anim = componente.GetComponent<Animator>();
        if (anim == null) return null;

        return anim.runtimeAnimatorController as AnimatorController;
    }

    private static AnimatorController Cargar(string ruta)
    {
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ruta);
        if (c == null) Debug.LogError($"[ValidarGestos] No encuentro el controller en '{ruta}'.");
        return c;
    }

    // ── Sugerencia de nombre ─────────────────────────────────────────────────

    /// Distancia de edición, para decir «¿querías decir X?» cuando el nombre está mal escrito.
    /// Solo sugiere si el parecido es razonable: a partir de un tercio del nombre mal, callarse es
    /// más útil que proponer cualquier cosa.
    private static string MasParecido(string nombre, HashSet<string> candidatos)
    {
        string mejor = null;
        int mejorDistancia = int.MaxValue;

        foreach (string c in candidatos)
        {
            int d = Distancia(nombre.ToLowerInvariant(), c.ToLowerInvariant());
            if (d < mejorDistancia) { mejorDistancia = d; mejor = c; }
        }

        return mejorDistancia <= Mathf.Max(2, nombre.Length / 3) ? mejor : null;
    }

    private static int Distancia(string a, string b)
    {
        var fila = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) fila[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = fila[0];
            fila[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int anterior = fila[j];
                fila[j] = Mathf.Min(
                    Mathf.Min(fila[j] + 1, fila[j - 1] + 1),
                    diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = anterior;
            }
        }
        return fila[b.Length];
    }
}
#endif
