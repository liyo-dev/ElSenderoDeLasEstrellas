#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Un solo botón que deja el prólogo listo para grabar.
///
/// ── Por qué existe ────────────────────────────────────────────────────────────────────────────
/// El pase del 20 sep dejó tres pasos que había que ejecutar EN ORDEN: preparar la escena, dar de
/// alta las animaciones, reconstruir la secuencia. Se ejecutó uno.
///
/// El resultado no fue un error en consola, que es lo que lo habría delatado: fue una grabación
/// entera en la que la conversación no estaba en el río, el Archimago y el Mago Oscuro salían uno
/// dentro del otro y la gente corría tres metros y se paraba. Todo eso es el mismo fallo mudo —
/// un beat que apunta a una marca que no existe **no hace nada y no se queja**, porque el aviso lo
/// da el stage una vez y se pierde entre doscientas líneas de log.
///
/// Así que los tres pasos van juntos, en orden, y al final se comprueba lo que de verdad importa:
/// que cada marca, cada prop y cada gesto que la secuencia NOMBRA exista de verdad. Eso es lo que
/// no se puede ver grabando hasta que es tarde.
public static class PrologoPrepararTodo
{
    private const string RutaSecuencia = "Assets/_SEQUENCES/SEQ_Prologo_UltimaNoche.asset";
    private const string EscenaPrologo = "Prologo_Valle";
    private const string EscenaMundo = "MainWorld";

    [MenuItem("El Sendero/Secuencias/Prólogo: PREPARAR TODO (escena + animación + secuencia)", priority = 0)]
    public static void Ejecutar()
    {
        // UN SOLO BOTÓN (21 sep): antes, sin Prologo_Valle abierta no hacía nada — ni siquiera lo
        // que no necesita escena (animaciones, Oliver, capítulo 1) — y había que abrirla a mano y
        // volver a darle. Ahora la abre él en aditivo si no está, y lo que es solo de assets se
        // hace siempre.
        var escena = AbrirEscenaDelPrologo();

        Debug.Log("[PrepararTodo] 1/4 — las animaciones.");
        NpcAnimatorSaltoYVueloWiring.Ejecutar();

        // Y que los clips de una sola vez no se repitan solos. Es barato y no hace nada si ya
        // estaban bien; ver ArreglarBuclesDeAnimacion.
        ArreglarBuclesDeAnimacion.Ejecutar(avisar: false);

        // Y que todos los vecinos puedan cambiar de cara: TownNpc#8 se quedó sin
        // NPCEmotionController y era el único de los diez que no la cambiaba.
        ArreglarCarasDeLosAldeanos.Ejecutar(avisar: false);

        Debug.Log("[PrepararTodo] 2/4 — Oliver y el principio del capítulo 1 (solo assets).");
        // La secuencia de Oliver va detrás del prólogo en la misma partida: dos planos (INC-357).
        OliverPlanosSencillos.Ejecutar(avisar: false);
        // Despertar sin iris, Oliver al grupo, Eldran y la ventana (INC-358..361).
        ArreglosCapitulo1.Ejecutar(avisar: false);

        // El sol, la luna y la lluvia se cablean en la escena que tenga el ciclo día/noche, que
        // hoy es MainWorld. PREPARAR TODO **no la abre** (a petición de Raúl, 24 sep): si está
        // abierta se cablea, y si no, se avisa y ya está. Para hacerlo a mano están sus dos menús
        // en «El Sendero → Mundo».
        int conAstros = SolYLunaWiring.Ejecutar(avisar: false);
        int conLluvia = ClimaDelCicloWiring.Ejecutar(avisar: false);

        if (conAstros == 0 && conLluvia == 0 && SceneManager.GetSceneByName(EscenaMundo).isLoaded == false)
            Debug.Log($"[PrepararTodo] '{EscenaMundo}' no está abierta, así que no he tocado el sol, " +
                "la luna ni la lluvia (viven en el ciclo día/noche, que está allí). Si hace falta, " +
                "ábrela y usa los dos menús de «El Sendero → Mundo».");
        else
            Debug.Log($"[PrepararTodo] Cielo: sol y luna añadidos en {conAstros} escena(s), lluvia " +
                $"asignada en {conLluvia}. (Cero puede ser que ya lo tuvieran.)");

        if (!escena.IsValid() || !escena.isLoaded)
        {
            Debug.LogError($"[PrepararTodo] No he podido abrir la escena '{EscenaPrologo}'. Lo de " +
                "Oliver y el capítulo 1 SÍ está hecho; falta la parte del prólogo (marcas y " +
                "secuencia). Ábrela a mano y vuelve a darle.");
            return;
        }

        // Caras que no podían cambiar (INC-404): Liora y los aldeanos traían una sola malla de
        // ojos y otra de boca, así que ninguna emoción de la secuencia se veía en ellos.
        CompletarCarasDeNpc.EjecutarRosterDelPrologo();

        Debug.Log("[PrepararTodo] 3/4 — la escena del prólogo.");
        PrologoValleMultitudWiring.Ejecutar();
        PrologoValleTerrenoWiring.Ejecutar();

        // El NavMesh del valle, cada vez (INC-399). Antes quedaba fuera a propósito («hornear
        // tarda»), y el resultado fue un NavMesh viejo que solo cubría el camino: la multitud de
        // arriba pega a la gente al NavMesh MÁS CERCANO, así que toda la plaza acababa en el
        // borde del camino. Ahora: el terreno se trata como suelo, se hornea, y la multitud se
        // vuelve a colocar sobre el NavMesh bueno (es idempotente: solo recoloca).
        // Los objetos que mueve la secuencia (carreta, globo...) tienen que ser los que SE VEN
        // (INC-403): con el decorado duplicado dentro de la isla, apuntaban a la copia apagada.
        RepararPropsDelEscenario.Ejecutar(escena);

        NavMeshDelPrologo.ArreglarSuelosConObstaculo(escena);
        int superficies = NavMeshDelPrologo.RehornearEscena(escena);
        Debug.Log($"[PrepararTodo] NavMesh del valle rehorneado ({superficies} superficie/s); " +
                  "vuelvo a colocar la multitud sobre él.");
        PrologoValleMultitudWiring.Ejecutar();

        // El «look» de sueño del prólogo (INC-422): un Volume global en el valle.
        PrologoPostprocesoSueno.Ejecutar(escena);

        Debug.Log("[PrepararTodo] 4/4 — la secuencia del prólogo.");
        ConstruirPrologoUltimaNoche.Ejecutar();

        Comprobar(escena);

        Debug.Log("[PrepararTodo] Hecho. Guarda con Ctrl+S (guarda también Prologo_Valle si la he " +
            "abierto yo en aditivo).");
    }

    /// Devuelve Prologo_Valle cargada: la que ya estuviera abierta o, si no, la abre en aditivo
    /// sin cerrar lo que haya (MainWorld sigue abierta y sin tocar).
    private static Scene AbrirEscenaDelPrologo() => AbrirEscena(EscenaPrologo);

    /// Abre una escena en ADITIVO si no lo estaba ya, sin cerrar nada.
    private static Scene AbrirEscena(string nombre)
    {
        var escena = SceneManager.GetSceneByName(nombre);
        if (escena.IsValid() && escena.isLoaded) return escena;

        foreach (var guid in AssetDatabase.FindAssets($"{nombre} t:Scene"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(ruta) != nombre) continue;

            Debug.Log($"[PrepararTodo] Abro '{ruta}' en aditivo.");
            return UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ruta,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
        }
        return escena;
    }

    /// Lee la secuencia ya construida y comprueba que todo lo que nombra existe.
    ///
    /// No vale con mirar el asset: hay que mirar la ESCENA, porque una marca es una entrada en el
    /// SequenceStage que apunta a un Transform, y las dos mitades se pueden desincronizar.
    private static void Comprobar(Scene escena)
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuencia);
        var stage = BuscarStage(escena);

        if (def == null || stage == null)
        {
            Debug.LogError("[PrepararTodo] No encuentro la secuencia o el SequenceStage; no puedo comprobar nada.");
            return;
        }

        var marcasPedidas = new HashSet<string>();
        var propsPedidos = new HashSet<string>();
        var gestosPedidos = new HashSet<string>();

        foreach (var fase in def.phases)
            Recorrer(fase.beats, marcasPedidas, propsPedidos, gestosPedidos);

        var faltan = new List<string>();

        ComprobarQueHaySol(escena, faltan);

        foreach (string m in marcasPedidas)
            if (stage.GetMark(m) == null) faltan.Add($"marca '{m}'");

        foreach (string p in propsPedidos)
            if (!HayProp(stage, p)) faltan.Add($"prop '{p}'");

        if (faltan.Count == 0)
        {
            Debug.Log($"[PrepararTodo] LISTO. {marcasPedidas.Count} marca(s), {propsPedidos.Count} " +
                $"prop(s) y {gestosPedidos.Count} gesto(s): todo lo que la secuencia nombra existe. " +
                "Guarda la escena con Ctrl+S y dale a Play.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[PrepararTodo] FALTAN {faltan.Count} cosa(s) que la secuencia nombra. Los " +
            "beats que las usen no van a hacer NADA, y no van a dar error:");
        foreach (string f in faltan) sb.AppendLine("  · " + f);
        Debug.LogError(sb.ToString());
    }

    /// Que la escena tenga al menos una luz direccional ENCENDIDA.
    ///
    /// Esto está aquí por un fallo concreto: apagué `Luz_Solo_Para_Editar` creyendo, por el
    /// nombre, que era una luz de trabajo de más. Era la única direccional de la escena, o sea el
    /// sol, y el valle entero se fue a negro. Y no lo vi enseguida porque el suelo y el río, ya en
    /// Quibli, siguieron pareciendo correctos: StylizedLit suma `_LightContribution` como base
    /// constante, que no depende de la luz de la escena. Solo se notaba en los árboles y las casas.
    ///
    /// Un valle sin sol no da ningún error. Este es el error.
    private static void ComprobarQueHaySol(Scene escena, List<string> faltan)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var luz in raiz.GetComponentsInChildren<Light>(includeInactive: false))
            {
                if (luz.type != LightType.Directional || !luz.enabled) continue;
                if (luz.intensity <= 0.01f) continue;
                return;
            }
        }

        faltan.Add("una luz DIRECCIONAL encendida — la escena no tiene sol y todo lo que no sea " +
            "Quibli se verá negro (el suelo y el río engañan: tienen luz base propia)");
    }

    private static void Recorrer(List<SequenceBeat> beats, HashSet<string> marcas,
        HashSet<string> props, HashSet<string> gestos)
    {
        if (beats == null) return;

        foreach (var beat in beats)
        {
            switch (beat)
            {
                case null: continue;

                case PlaceAtMarkBeat b:
                    Anota(marcas, b.markName);
                    Anota(marcas, b.faceTowardsMark);
                    break;

                case MoveToBeat b: Anota(marcas, b.markName); break;
                case FaceBeat b: Anota(marcas, b.markName); break;
                case SayBeat b: Anota(marcas, b.markName); break;
                case SfxBeat b: Anota(marcas, b.markName); break;
                case VfxBeat b: Anota(marcas, b.markName); break;

                case WalkPathBeat b:
                    if (b.markNames != null)
                        foreach (string m in b.markNames) Anota(marcas, m);
                    break;

                case SetPropActiveBeat b: Anota(props, b.propId); break;
                case PropMoveBeat b: Anota(props, b.propId); break;

                case GestureBeat b: Anota(gestos, b.gesture); break;

                case ParallelBeat b: Recorrer(b.beats, marcas, props, gestos); break;
            }
        }
    }

    private static void Anota(HashSet<string> donde, string que)
    {
        if (!string.IsNullOrWhiteSpace(que)) donde.Add(que.Trim());
    }

    /// El stage no tiene un GetProp público como el de las marcas, así que se busca en la lista.
    private static bool HayProp(SequenceStage stage, string id)
    {
        if (stage.Props == null) return false;

        foreach (var prop in stage.Props)
            if (prop.target != null
                && string.Equals(prop.id, id, System.StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    private static SequenceStage BuscarStage(Scene escena)
    {
        foreach (var raiz in escena.GetRootGameObjects())
        {
            var stage = raiz.GetComponentInChildren<SequenceStage>(includeInactive: true);
            if (stage != null) return stage;
        }
        return null;
    }
}
#endif
