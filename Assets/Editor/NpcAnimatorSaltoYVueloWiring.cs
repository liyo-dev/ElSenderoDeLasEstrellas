#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// Da de alta los estados de SALTO y de VUELO en los dos controllers de personaje del juego.
/// (20 sep 2026. Sustituye a `PrologoAnimatorSaltoWiring`, que solo daba de alta los tres estados
/// de salto que pedía el final nuevo del prólogo — INC-271 — y solo en el controller de NPCs.)
///
/// ── Por qué esto es barato ────────────────────────────────────────────────────────────────────
/// No hay animación nueva que hacer: los clips llevan en el proyecto desde el primer día y
/// simplemente no estaban cableados. Es el mismo caso que `Reverence01` el 19 sep.
///
///   · Salto  — el pack de personajes (RPG Tiny Hero Duo) trae la familia completa para el rig sin
///              arma en `Animation/NoWeapon/InPlace/`. Se usan las versiones EN SITIO a propósito:
///              en una secuencia el desplazamiento lo conduce `WalkPathBeat`, no el root motion.
///   · Vuelo  — `Art/Fly Animations/` son los clips que YA usa Will cuando vuela. Son los mismos
///              exactos, no una alternativa parecida.
///
/// ── Qué recibe cada controller ────────────────────────────────────────────────────────────────
/// Los dos reparten distinto porque cada uno tenía ya una mitad:
///
///   · `NPC_NoWeapon`          — no tenía NI salto, NI vuelo, NI escalada/natación. Lo recibe todo.
///   · Y el jugador recibe además los dos estados que le faltaban del vocabulario de emociones,
///     que es un asset de datos compartido por los dos — ver `VocabularioDeEmociones`.
///   · `Invector@BasicLocomotion` (Will/Liam/Estela) — ya trae `fly_idle`, `fly_dive` y `Landing`
///                               cableados para el vuelo del jugador, pero NO los saltos en sitio
///                               del pack. Recibe solo la familia de salto, para poder pedirlos
///                               desde una cinemática igual que a cualquier NPC.
///
/// El script comprueba qué falta de verdad en cada uno, así que este reparto no está escrito a
/// mano en ningún sitio: es consecuencia de lo que ya haya. Si mañana uno de los dos gana un
/// estado por otro camino, aquí deja de crearse solo.
///
/// ── Lo que este script NO hace, a propósito ───────────────────────────────────────────────────
/// NO añade los parámetros `IsGrounded` / `GroundDistance` / `isFlying` a `NPC_NoWeapon`, ni su
/// Any State → `Falling`. Ese Any State es lo que hoy rompe el sentado de Will/Liam/Estela (ver
/// `NPCSimpleAnimator.ForceGroundedForSit()`): interrumpe CUALQUIER estado activo en cuanto el
/// personaje deja de constar como apoyado en el suelo, y un NPC de `NavMeshAgent` no tiene quién
/// le mantenga esos parámetros al día frame a frame. Importarlo a los ~52 NPCs sería llevarse el
/// bug a todo el juego a cambio de nada: el vuelo de los compañeros ya funciona sin él, porque
/// `FollowPlayerState` reproduce `fly_idle`/`fly_dive` con un Play directo por hash.
///
/// ── Por qué un script de Editor y no tocar el .controller a mano ──────────────────────────────
/// La regla de INC-249. Un asset que Unity puede tener cargado no se escribe por fuera: la copia
/// en memoria manda sobre el archivo y el cambio se pierde (o peor, se guarda a medias). El
/// AnimatorController tiene API de Editor, así que se usa.
///
/// Es idempotente: un estado que ya existe no se duplica ni se toca. La comprobación la hace
/// `AnimatorControllerEstados`, que mira TODAS las capas y sus sub-grafos y no solo la raíz de la
/// capa base — en `Invector@BasicLocomotion` media docena de estados viven dentro de sub-máquinas,
/// y crear un duplicado dejaría a `CrossFade` eligiendo entre dos estados con el mismo nombre.
///
/// ── Cómo se usan después ──────────────────────────────────────────────────────────────────────
/// Desde una secuencia, como cualquier otro gesto:
///
///     new GestureBeat { actorId = "NPC_Archimago", gesture = "JumpStart_InPlace_NoWeapon", ... }
///
/// `NPCSimpleAnimator.PlaySocialGesture` resuelve solo en qué capa vive el estado
/// (`AnimatorLayerUtil.ResolveLayer`). Todos estos van a la capa base porque son de cuerpo entero:
/// en UpperBody el personaje saltaría de cintura para arriba y seguiría andando con las piernas.
///
/// Y el vuelo de los NPCs compañeros no hay que pedirlo desde ninguna parte: `FollowPlayerState`
/// ya lo reproduce solo cuando el jugador entra en `ActionMode.Flying`, y su `DetectFlightLayer()`
/// busca `fly_idle` capa por capa. Hoy no lo encuentra y el NPC sigue al jugador por el aire con
/// la animación de andar; en cuanto exista, vuela.
public static class NpcAnimatorSaltoYVueloWiring
{
    private const string ControllerNpcs =
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller";

    private const string ControllerJugador =
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller";

    private const string CarpetaSalto =
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/InPlace/";

    private const string CarpetaVuelo =
        "Assets/Art/Fly Animations/Animations/Fly/";

    private const string CarpetaNoWeapon =
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/";

    private const string CarpetaRootMotion =
        "Assets/Art/Characters/RPG Tiny Hero Duo/Animation/NoWeapon/RootMotion/";

    /// Familia de salto. Los tres primeros son los que pedía el final del prólogo; los cuatro
    /// siguientes son variantes del mismo pack que estaban igual de sin usar y que dan de dónde
    /// elegir en futuras secuencias sin volver a pasar por aquí.
    ///
    /// EL ESTADO SE LLAMA IGUAL QUE SU CLIP. No es manía: es la convención del resto del
    /// controller (`Idle_Normal_NoWeapon`, `Dance_NoWeapon`, `Victory_NoWeapon`... todos llevan el
    /// nombre exacto de su clip) y aquí sostiene tres cosas a la vez:
    ///
    ///   · `NPCSimpleAnimator.GetClipLength()` busca la duración del gesto quedándose con el primer
    ///     clip cuyo nombre CONTENGA el del estado. Con nombres distintos no casa y `PlayOneShot`
    ///     cae a su fallback de 1 segundo para un clip que dura 0,27 s.
    ///   · El sufijo `_InPlace_` DISTINGUE, no sobra: de dos de estos siete saltos existe también
    ///     la versión con root motion (`JumpFull_RM_NoWeapon`, `JumpFullSpin_RM_NoWeapon`). Un
    ///     estado llamado `JumpFull_NoWeapon` no dice cuál de las dos es.
    ///   · El controller YA usa ese criterio de sufijo: `RollFWD_Battle_RM_NoWeapon` y sus tres
    ///     hermanos llevan el `_RM_` en el nombre del estado desde siempre.
    ///
    /// La única excepción es `Landing`, más abajo, y tiene contrato propio.
    private static readonly (string estado, string ruta, Vector3 posicion)[] Salto =
    {
        ("JumpStart_InPlace_NoWeapon",         CarpetaSalto + "JumpStart_InPlace_NoWeapon.fbx",         new Vector3(480f,  40f, 0f)),
        ("JumpAir_InPlace_NoWeapon",           CarpetaSalto + "JumpAir_InPlace_NoWeapon.fbx",           new Vector3(480f, 100f, 0f)),
        ("JumpEnd_InPlace_NoWeapon",           CarpetaSalto + "JumpEnd_InPlace_NoWeapon.fbx",           new Vector3(480f, 160f, 0f)),
        ("JumpFull_InPlace_NoWeapon",          CarpetaSalto + "JumpFull_InPlace_NoWeapon.fbx",          new Vector3(480f, 220f, 0f)),
        ("JumpAirSpin_InPlace_NoWeapon",       CarpetaSalto + "JumpAirSpin_InPlace_NoWeapon.fbx",       new Vector3(480f, 280f, 0f)),
        ("JumpFullSpin_InPlace_NoWeapon",      CarpetaSalto + "JumpFullSpin_InPlace_NoWeapon.fbx",      new Vector3(480f, 340f, 0f)),
        ("JumpAirDoubleJump_InPlace_NoWeapon", CarpetaSalto + "JumpAirDoubleJump_InPlace_NoWeapon.fbx", new Vector3(480f, 400f, 0f)),
    };

    /// Familia de vuelo. Los NOMBRES NO SE PUEDEN CAMBIAR: `FollowPlayerState` los tiene cacheados
    /// como hash (`fly_idle`, `fly_dive`, `Landing`) y `PlayerFlyingController` usa los mismos.
    /// Renombrar uno aquí no daría error de compilación — el vuelo simplemente dejaría de verse.
    ///
    /// `Landing` sale del clip de caída del salto (`JumpEnd`), que es la toma de tierra del pack;
    /// no hay un clip de aterrizaje de vuelo propio y este es el gesto correcto.
    private static readonly (string estado, string ruta, Vector3 posicion)[] Vuelo =
    {
        ("fly_idle", CarpetaVuelo + "fly_idle.anim",                  new Vector3(800f,  40f, 0f)),
        ("fly_dive", CarpetaVuelo + "fly_dive.anim",                  new Vector3(800f, 100f, 0f)),
        ("Landing",  CarpetaSalto + "JumpEnd_InPlace_NoWeapon.fbx",   new Vector3(800f, 160f, 0f)),
    };

    /// Escalada y natación de los NPCs compañeros. Mismo caso exacto que el vuelo, encontrado el
    /// mismo día al comparar los dos controllers estado por estado: `FollowPlayerState` se los pide
    /// al NPC cuando el jugador entra en `ActionMode.Climbing` o `ActionMode.Swimming`, existen en
    /// el controller del jugador, y en `NPC_NoWeapon` no estaban. Un compañero escalando o nadando
    /// detrás de ti seguía con la animación de andar.
    ///
    /// Los NOMBRES TAMPOCO SE PUEDEN CAMBIAR: `FollowPlayerState` los tiene cacheados como hash,
    /// igual que los de vuelo.
    ///
    /// El `_RM_` del nombre es el del clip, que viene con root motion. Que el root motion se
    /// aplique o no lo decide `NPCSimpleAnimator.useRootMotionForSpecialAnims` en cada prefab; aquí
    /// da igual, porque durante el seguimiento especial la posición del NPC la conduce
    /// `FollowPlayerState` a mano.
    private static readonly (string estado, string ruta, Vector3 posicion)[] EscaladaYNatacion =
    {
        ("ClimbUp_RM_NoWeapon",        CarpetaRootMotion + "ClimbUp_RM_NoWeapon.fbx",        new Vector3(1120f,  40f, 0f)),
        ("ClimbDown_RM_NoWeapon",      CarpetaRootMotion + "ClimbDown_RM_NoWeapon.fbx",      new Vector3(1120f, 100f, 0f)),
        ("Swimming_Floating_NoWeapon", CarpetaNoWeapon   + "Swimming_Floating_NoWeapon.fbx", new Vector3(1120f, 160f, 0f)),
    };

    /// Derribo: salir despedido, quedarse en el suelo y levantarse (INC-356).
    ///
    /// «Lo que yo haría sería romperle el escudo y tirarle al suelo: hacia arriba, que caiga en
    /// parábola con la animación de falling. Él se recupera y se pone de pie.» El pack NoWeapon no
    /// trae un clip de caída en el aire; el jugador usa `HumanM@Fall01` (Kevin Iglesias, humanoide,
    /// igual que los `HumanM@MagicAttack…` que este controller ya tiene), así que el estado se
    /// llama `Falling` como el del jugador. `Die01Stay` es la pose de tirado en el suelo y
    /// `GetUp` arranca justo de esa pose. Capa base: son de cuerpo entero.
    private static readonly (string estado, string ruta, Vector3 posicion)[] Derribo =
    {
        ("Falling",             "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Movement/Jump/HumanM@Fall01.fbx", new Vector3(1440f,  40f, 0f)),
        ("Die01Stay_NoWeapon",  CarpetaNoWeapon + "Die01Stay_NoWeapon.fbx", new Vector3(1440f, 100f, 0f)),
        ("GetUp_NoWeapon",      CarpetaNoWeapon + "GetUp_NoWeapon.fbx",     new Vector3(1440f, 160f, 0f)),
        // Caerse de verdad (INC-362): el clip de derribo del pack, de pie a tirado en medio segundo.
        // Se sostiene durante el vuelo y se queda en su último fotograma al tocar el suelo.
        ("Die01_NoWeapon",      CarpetaNoWeapon + "Die01_NoWeapon.fbx",     new Vector3(1440f, 220f, 0f)),
    };

    /// Los dos estados que le faltaban al jugador del VOCABULARIO DE EMOCIONES.
    ///
    /// `Assets/_EmotionProfile/NpcEmotionProfile.asset` es un asset de datos COMPARTIDO: lo leen
    /// `NPCSimpleAnimator.PlayBodyEmotion()` y `PlayerDialogueAnimator.PlayBodyEmotion()`, los dos.
    /// Su contrato implícito es «esto lo entiende cualquier personaje», y de los 22 estados que
    /// nombra, 20 ya existían en los dos controllers. Estos dos eran la fuga, no el criterio:
    ///
    ///   · `Challenging_NoWeapon`             — es el gesto de la emoción `Determined` (INC-223).
    ///   · `SenseSomethingSearching_NoWeapon` — y su pareja `SenseSomethingStart_NoWeapon` SÍ
    ///                                          estaba en el jugador. El par se quedó partido por
    ///                                          la mitad, que es la firma de un descuido y no de
    ///                                          una decisión.
    ///
    /// La alternativa era remapear `Determined` a un estado que tuvieran los dos, y eso es darle
    /// el gesto de otra emoción — justo lo que INC-223 vino a arreglar («media docena que acaben
    /// todas en el mismo HeadShake no son variedad»).
    ///
    /// VAN A LA CAPA UPPERBODY, no a la base: es donde viven en `NPC_NoWeapon`, y una emoción no
    /// debe congelar las piernas de quien la siente. Darlos de alta en la base haría que el mismo
    /// dato se viera distinto según quién lo interprete, que es el problema que se está cerrando.
    private static readonly (string estado, string ruta, Vector3 posicion)[] VocabularioDeEmociones =
    {
        ("Challenging_NoWeapon",             CarpetaNoWeapon + "Challenging_NoWeapon.fbx",             new Vector3(1440f,  40f, 0f)),
        ("SenseSomethingSearching_NoWeapon", CarpetaNoWeapon + "SenseSomethingSearching_NoWeapon.fbx", new Vector3(1440f, 100f, 0f)),
    };

    /// Nombres que este script llegó a dar de alta durante unas horas del 20 sep 2026, antes de
    /// unificarlos con los de su clip. Quien ejecutara el menú en esa ventana tiene los estados
    /// creados con el nombre corto; esto se los renombra en vez de dejarle siete duplicados.
    ///
    /// Renombrar un estado es seguro: las transiciones apuntan al OBJETO, no a su nombre, y estos
    /// además no tienen ninguna. Lo que sí depende del nombre es quien lo pide por string
    /// (`montaje.py` y `ConstruirPrologoUltimaNoche.cs`), y van en el mismo cambio.
    ///
    /// Esta tabla se puede borrar cuando ya no quede ningún proyecto con los nombres viejos.
    private static readonly (string viejo, string nuevo)[] Renombrados =
    {
        ("JumpStart_NoWeapon",         "JumpStart_InPlace_NoWeapon"),
        ("JumpAir_NoWeapon",           "JumpAir_InPlace_NoWeapon"),
        ("JumpEnd_NoWeapon",           "JumpEnd_InPlace_NoWeapon"),
        ("JumpFull_NoWeapon",          "JumpFull_InPlace_NoWeapon"),
        ("JumpAirSpin_NoWeapon",       "JumpAirSpin_InPlace_NoWeapon"),
        ("JumpFullSpin_NoWeapon",      "JumpFullSpin_InPlace_NoWeapon"),
        ("JumpAirDoubleJump_NoWeapon", "JumpAirDoubleJump_InPlace_NoWeapon"),
    };

    [MenuItem("El Sendero/Archivo/Animación/Dar de alta salto y vuelo en los controllers")]
    public static void Ejecutar()
    {
        bool algoCambiado = false;

        algoCambiado |= Procesar(ControllerNpcs,    "NPCs (NPC_NoWeapon)");
        algoCambiado |= Procesar(ControllerJugador, "Jugador (Invector@BasicLocomotion)");

        if (algoCambiado)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SaltoVueloWiring] Assets guardados. Ya se pueden pedir desde un GestureBeat por su nombre.");
        }
        else
        {
            Debug.Log("[SaltoVueloWiring] No había nada que dar de alta: los dos controllers ya lo tienen todo.");
        }
    }

    /// Da de alta en un controller todo lo que le falte de las dos familias. Devuelve si lo ha
    /// tocado, para no llamar a SaveAssets cuando no hace falta.
    private static bool Procesar(string rutaController, string etiqueta)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(rutaController);
        if (controller == null)
        {
            Debug.LogError($"[SaltoVueloWiring] No encuentro el controller de {etiqueta} en '{rutaController}'.");
            return false;
        }

        if (controller.layers == null || controller.layers.Length == 0)
        {
            Debug.LogError($"[SaltoVueloWiring] El controller de {etiqueta} no tiene ninguna capa.");
            return false;
        }

        // Capa base para los gestos de cuerpo entero, UpperBody para los de tronco. Se busca por
        // NOMBRE y no por índice 1: si algún día alguien reordena las capas, esto sigue acertando
        // y no mete una emoción en medio de la locomoción.
        AnimatorStateMachine maquina = controller.layers[0].stateMachine;
        AnimatorStateMachine upperBody = MaquinaDeCapa(controller, "UpperBody") ?? maquina;

        int creados = 0, yaEstaban = 0, sinClip = 0;

        // Primero migrar: si un estado está con su nombre viejo, renombrarlo. Si no se hiciera
        // aquí, el alta de abajo lo daría por ausente y crearía un duplicado al lado.
        int renombrados = Migrar(controller, etiqueta);

        Altas(controller, maquina, Salto,             ref creados, ref yaEstaban, ref sinClip, etiqueta);
        Altas(controller, maquina, Vuelo,             ref creados, ref yaEstaban, ref sinClip, etiqueta);
        Altas(controller, maquina, EscaladaYNatacion, ref creados, ref yaEstaban, ref sinClip, etiqueta);
        Altas(controller, maquina, Derribo,           ref creados, ref yaEstaban, ref sinClip, etiqueta);

        Altas(controller, upperBody, VocabularioDeEmociones, ref creados, ref yaEstaban, ref sinClip, etiqueta);

        if (creados > 0 || renombrados > 0)
            EditorUtility.SetDirty(controller);

        Debug.Log($"[SaltoVueloWiring] {etiqueta}: {creados} estado(s) nuevo(s), {yaEstaban} que ya estaban" +
            (renombrados > 0 ? $", {renombrados} renombrado(s) desde su nombre antiguo" : "") +
            (sinClip > 0 ? $", {sinClip} SIN CLIP (mira los avisos de arriba)." : "."));

        return creados > 0 || renombrados > 0;
    }

    /// La state machine de la capa con ese nombre, o null si no hay ninguna así.
    private static AnimatorStateMachine MaquinaDeCapa(AnimatorController controller, string nombre)
    {
        foreach (var capa in controller.layers)
            if (capa != null && capa.name == nombre) return capa.stateMachine;

        return null;
    }

    /// Renombra los estados que estén con su nombre antiguo. Devuelve cuántos.
    private static int Migrar(AnimatorController controller, string etiqueta)
    {
        int n = 0;

        foreach (var (viejo, nuevo) in Renombrados)
        {
            var estado = AnimatorControllerEstados.Buscar(controller, viejo);
            if (estado == null) continue;

            if (AnimatorControllerEstados.Existe(controller, nuevo))
            {
                // Los dos a la vez: alguien ejecutó una versión con cada tabla. No se toca ninguno
                // porque no se sabe cuál tiene las transiciones o los ajustes buenos.
                Debug.LogWarning($"[SaltoVueloWiring] ({etiqueta}) Existen A LA VEZ '{viejo}' y " +
                    $"'{nuevo}'. No renombro nada: borra a mano el que sobre (el que no tenga " +
                    "transiciones ni ajustes propios) antes de volver a ejecutar esto.");
                continue;
            }

            estado.name = nuevo;
            n++;

            Debug.Log($"[SaltoVueloWiring] ({etiqueta}) Renombrado '{viejo}' → '{nuevo}'.");
        }

        return n;
    }

    private static void Altas(
        AnimatorController controller,
        AnimatorStateMachine maquina,
        (string estado, string ruta, Vector3 posicion)[] familia,
        ref int creados, ref int yaEstaban, ref int sinClip,
        string etiqueta)
    {
        foreach (var (estado, ruta, posicion) in familia)
        {
            if (AnimatorControllerEstados.Existe(controller, estado))
            {
                yaEstaban++;
                continue;
            }

            AnimationClip clip = CargarClip(ruta, etiqueta);
            if (clip == null)
            {
                sinClip++;
                continue;
            }

            var nuevo = maquina.AddState(estado, posicion);
            nuevo.motion = clip;

            // Sin transiciones de salida a propósito. Estos estados se lanzan con un CrossFade
            // directo (PlayOneShot en los NPCs, Play por hash en el vuelo), y es quien los lanza
            // el que devuelve al personaje a su pose normal cuando el clip acaba. Una transición
            // automática a Idle aquí se comería el final del salto.
            //
            // Y writeDefaults a false por la misma razón que el resto del controller: con él
            // activado, un estado se trae de vuelta valores de otras capas al entrar.
            nuevo.writeDefaultValues = false;

            creados++;
        }
    }

    /// Saca el AnimationClip de una ruta, que puede ser un .anim suelto o un .fbx. Un FBX de
    /// animación trae varios objetos y uno de ellos es el clip de vista previa del importador, que
    /// empieza por '__preview__' y no sirve.
    private static AnimationClip CargarClip(string ruta, string etiqueta)
    {
        var todos = AssetDatabase.LoadAllAssetsAtPath(ruta);
        if (todos == null || todos.Length == 0)
        {
            Debug.LogWarning($"[SaltoVueloWiring] ({etiqueta}) No encuentro nada en '{ruta}'. Ese estado se queda sin crear.");
            return null;
        }

        foreach (var o in todos)
        {
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        Debug.LogWarning($"[SaltoVueloWiring] ({etiqueta}) '{ruta}' no contiene ningún AnimationClip utilizable.");
        return null;
    }
}
#endif
