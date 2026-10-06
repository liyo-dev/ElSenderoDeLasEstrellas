#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// Reconstruye SEQ_Prologo_UltimaNoche desde codigo, con el AssetDatabase.
///
/// -- Por que existe -----------------------------------------------------------------------------
/// Es la regla de INC-249, aprendida ahora por tercera vez en el mismo archivo: un .asset que Unity
/// puede tener cargado NO se escribe a mano. Generando su YAML por fuera se colaron, uno detras de
/// otro, un ': ' dentro de un mapa en linea, un espacio de mas delante de un bloque anidado, y un
/// tercer fallo que ni siquiera daba error de parseo -- el asset se leia, las 11 fases salian con
/// sus cuentas correctas, y los 152 beats estaban a null, asi que la secuencia entera se recorria
/// en dos segundos sin ejecutar nada.
///
/// Construyendo los objetos aqui, el YAML lo escribe Unity, que es quien sabe escribirlo. Y como
/// son tipos de verdad, cada nombre de campo se comprueba al compilar en vez de fallar en silencio.
///
/// Es idempotente: reemplaza las fases enteras cada vez que se ejecuta.
public static class ConstruirPrologoUltimaNoche
{
    private const string Ruta = "Assets/_SEQUENCES/SEQ_Prologo_UltimaNoche.asset";
    private const string RutaHechizoSostenido = "Assets/_VFX/Prologo/VFX_MagoOscuro_HechizoSostenido.prefab";
    /// El .inputactions se localiza por su guid, no por una ruta escrita a mano: moverlo de
    /// carpeta no puede romper esto.
    private const string GuidInputActions = "fc9f9f1bd7ce1874fb152afa8758ce88";

    [MenuItem("El Sendero/Secuencias/Prologo: reconstruir la secuencia")]
    public static void Ejecutar()
    {
        var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(Ruta);
        if (def == null)
        {
            Debug.LogError($"[ConstruirPrologo] No encuentro la secuencia en '{Ruta}'.");
            return;
        }

        // INC-577: el agujero negro de verdad (CrearAgujeroNegro, INC-576) sustituye a la esfera clara;
        // la esfera solo queda como reserva si el prefab nuevo no se puede crear.
        var agujeroNegro = AssetDatabase.LoadAssetAtPath<GameObject>(CrearAgujeroNegro.Asegurar());
        var hechizoSostenido = agujeroNegro != null ? agujeroNegro : PrepararHechizoSostenido();
        if (hechizoSostenido == null) return;
        // INC-585: la protección del Archimago que crece contra el agujero negro en el clímax.
        try { s_luzProtectora = AssetDatabase.LoadAssetAtPath<GameObject>(CrearLuzProtectora.Asegurar()); }
        catch (System.Exception ex) { s_luzProtectora = null; OmitirCambio(ex.Message, "luz protectora del clímax"); }

        Undo.RecordObject(def, "Reconstruir el prologo");
        // Los que nunca se retiran de un plano al despejar figurantes (INC-582). Nina es la Aldeano_05.
        def.protagonistas = new List<string> { "NPC_Archimago", "NPC_Liora", "NPC_Aldeano_05", "NPC_MagoOscuro", SequenceActor.PlayerId };
        def.musicId = "PROLOGUE_DREAM";
        def.endStayBlack = true;
        def.presentacionDeTexto = PresentacionDeTexto.Subtitulo;
        PrologoPostprocesoSueno.CrearPerfiles();
        AsegurarSonidos();
        def.phases = Fases(hechizoSostenido);
        PrepararManana(def.phases);
        PrepararNoche(def.phases);

        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int total = 0;
        foreach (var f in def.phases) total += f.beats.Count;
        Debug.Log($"[ConstruirPrologo] Hecho: {def.phases.Count} fases, {total} beats. " +
            "El YAML lo ha escrito Unity, asi que esta bien formado por construccion.");
    }

    private static GameObject s_luzProtectora;

    // -- Sonidos del prologo ---------------------------------------------------------------------

    private const string RutaPerfilDeAudio = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private const string CarpetaPack = "Assets/Audio/FREE SOUND PACK_TM(355)/";

    /// Clave del AudioGraphProfile -> clip. Los ambientes empiezan por "Ambience" para ir a su bus.
    private static readonly (string clave, string ruta)[] Sonidos =
    {
        ("Ambience_Prologo_Pueblo", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Pueblo.mp3"),
        ("Ambience_Prologo_Rio", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Rio.mp3"),
        ("Ambience_Prologo_Tormenta", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Tormenta.mp3"),
        ("Ambience_Prologo_Panico", "Assets/Audio/SFX_Prologo/Ambience_Prologo_Panico.mp3"),
        ("SFX_Prologo_AgujeroNegro_Carga", "Assets/Audio/SFX_Prologo/SFX_AgujeroNegro_Carga.mp3"),
        ("SFX_Prologo_AgujeroNegro_Bucle", "Assets/Audio/SFX_Prologo/SFX_AgujeroNegro_Bucle.mp3"),
        ("SFX_Prologo_Choque", "Assets/Audio/SFX_Prologo/SFX_Choque_Final.mp3"),
        ("SFX_Prologo_GritoMago", "Assets/Audio/SFX_Prologo/Voz_MagoOscuro_Grito.mp3"),
        ("SFX_Prologo_CutIn", CarpetaPack + "Weapons(25)/Weapon_Whoosh 06.wav"),
        ("SFX_Prologo_GolpeCine", CarpetaPack + "Horror(13)/Cinematic_Hits-013.wav"),
        ("SFX_Prologo_Explosion", CarpetaPack + "Misc(58)/Big_Explosion-002.wav"),
        ("SFX_Prologo_Globo", CarpetaPack + "Misc(58)/Balloon_Pop-003.wav"),
        ("SFX_Prologo_Viento", CarpetaPack + "Ambiences(43)/Wind-009.wav"),
        ("SFX_Prologo_Grito", CarpetaPack + "Horror(13)/SCREAM- Woman 3.wav"),
        ("SFX_Prologo_Impacto", CarpetaPack + "Horror(13)/Cinematic_Hits-017.wav"),
        ("SFX_Prologo_Latigazo", CarpetaPack + "Weapons(25)/Weapon_Whoosh 09.wav"),
        ("SFX_Prologo_Madera_Golpe1", CarpetaPack + "Misc(58)/Wood_Hit-022.wav"),
        ("SFX_Prologo_Madera_Golpe2", CarpetaPack + "Misc(58)/Wood_Hit-023.wav"),
        ("SFX_Prologo_Madera_Rascar", CarpetaPack + "Misc(58)/Wood_Scratching-001.wav"),
        ("SFX_Prologo_Subida", "Assets/Audio/SFX_Prologo/SFX_Prologo_Subida.wav"),
        ("SFX_Prologo_ForcejeoBucle", "Assets/Audio/SFX_Prologo/SFX_Prologo_ForcejeoBucle.wav"),
        ("SFX_Prologo_CoroBucle", "Assets/Audio/SFX_Prologo/SFX_Prologo_CoroBucle.wav"),
        ("SFX_Prologo_EscudoNace", "Assets/Audio/SFX_Prologo/SFX_Prologo_EscudoNace.wav"),
    };

    /// Da de alta en el AudioGraphProfile las claves que usa la secuencia; las que ya existen no se tocan.
    private static void AsegurarSonidos()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(RutaPerfilDeAudio);
        if (perfil == null)
        {
            Debug.LogWarning($"[ConstruirPrologo] No encuentro el perfil de audio en '{RutaPerfilDeAudio}'.");
            return;
        }
        bool cambiado = false;
        foreach (var (clave, ruta) in Sonidos)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (clip == null)
            {
                Debug.LogWarning($"[ConstruirPrologo] Falta el sonido '{ruta}' para la clave '{clave}'.");
                continue;
            }
            var existente = perfil.eventSfx.Find(e => e != null && string.Equals(e.eventKey, clave, System.StringComparison.OrdinalIgnoreCase));
            if (existente != null)
            {
                if (existente.sfx == clip) continue;
                existente.sfx = clip;
            }
            else perfil.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = clave, sfx = clip });
            cambiado = true;
        }
        var baile = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Pueblecito.mp3");
        if (baile == null) throw new System.InvalidOperationException("Falta Pueblecito.mp3 para el baile del prólogo.");
        var regla = perfil.sequences.Find(r => r != null && r.sequenceId == "PROLOGUE_DANCE");
        if (regla == null)
        {
            regla = new AudioGraphProfile.SequenceRule { sequenceId = "PROLOGUE_DANCE" };
            perfil.sequences.Add(regla);
        }
        if (regla.music != baile || regla.fadeIn != 1f || regla.fadeOut != 1f) cambiado = true;
        regla.music = baile;
        regla.fadeIn = regla.fadeOut = 1f;
        if (!cambiado) return;
        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
    }

    // -- Utilidades de referencia ---------------------------------------------------------------

    /// Crea una variante de la esfera sin movimiento, colisiones ni vida propia; conserva sus mallas y materiales.
    private static GameObject PrepararHechizoSostenido()
    {
        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(RutaHechizoSostenido);
        if (existente != null) return existente;

        var original = Prefab("5ee28f65ca127db42a9a46da980189d4");
        if (original == null) return null;
        if (!AssetDatabase.IsValidFolder("Assets/_VFX"))
            AssetDatabase.CreateFolder("Assets", "_VFX");
        if (!AssetDatabase.IsValidFolder("Assets/_VFX/Prologo"))
            AssetDatabase.CreateFolder("Assets/_VFX", "Prologo");

        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(original, escena);
            foreach (var proyectil in instancia.GetComponentsInChildren<SlowMotionFireProjectile>(true))
                Object.DestroyImmediate(proyectil);
            foreach (var collider in instancia.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (var cuerpo in instancia.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(cuerpo);

            // Los scripts adicionales requieren revisar si alteran la carga sostenida.
            if (instancia.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new System.InvalidOperationException("La esfera contiene scripts adicionales que requieren revisar su movimiento y vida.");

            var variante = PrefabUtility.SaveAsPrefabAsset(instancia, RutaHechizoSostenido);
            if (variante == null)
                throw new System.InvalidOperationException("No se puede guardar la variante del hechizo sostenido.");
            EditorUtility.SetDirty(variante);
            AssetDatabase.SaveAssets();
            return variante;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(escena);
        }
    }

    private static GameObject Prefab(string guid)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(guid);
        var go = string.IsNullOrEmpty(ruta) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (go == null) Debug.LogWarning($"[ConstruirPrologo] No encuentro el prefab de guid {guid}. Ese VfxBeat se queda vacio.");
        return go;
    }

    /// Un InputActionReference es un sub-asset que genera el importador del Input System, y su
    /// fileID no es derivable desde fuera (ver INC-246). Aqui se resuelve por nombre, que es lo que
    /// ya hacia SequenceInputActionWiring.
    private static InputActionReference Accion(string mapa, string accion)
    {
        string ruta = AssetDatabase.GUIDToAssetPath(GuidInputActions);

        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(ruta))
        {
            if (a is not InputActionReference r || r.action == null) continue;
            if (r.action.name == accion && r.action.actionMap != null && r.action.actionMap.name == mapa)
                return r;
        }

        Debug.LogWarning($"[ConstruirPrologo] No encuentro la accion '{mapa}/{accion}'. El prompt se queda sin ella.");
        return null;
    }

    /// InputPromptBeat.mode es [SerializeField] private, asi que no se puede poner en el
    /// inicializador. Se pone aqui por reflexion, que es la unica forma y es una sola vez.
    private static InputPromptBeat ConModo(InputPromptBeat beat, PanicInputMode modo)
    {
        var campo = typeof(InputPromptBeat).GetField("mode",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (campo != null) campo.SetValue(beat, modo);
        else Debug.LogWarning("[ConstruirPrologo] InputPromptBeat ya no tiene el campo 'mode'; el prompt se queda en su modo por defecto.");

        return beat;
    }

    private static Color ColorDelHablante(string speakerNameKey)
    {
        string hexadecimal = speakerNameKey switch
        {
            "Archimago" => "#FFD27A",
            "Mago Oscuro" => "#B48CFF",
            "Liora" => "#FFB3C7",
            _ => "#E8E8E8"
        };
        ColorUtility.TryParseHtmlString(hexadecimal, out Color color);
        return color;
    }
    private static SequencePhase Fase(string nombre, string only, string skip, List<SequenceBeat> beats)
        => new() { name = nombre, onlyIfFlag = only, skipIfFlag = skip, beats = beats };

    // -- Las fases ------------------------------------------------------------------------------

    private static VolumeProfile Perfil(string ruta) => AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);

    /// Mixamo es opcional: el FBX y el estado deben estar disponibles en el personaje.
    private static string EstadoDisponible(string actorId, string mixamo, string alternativa)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art/Animations/Mixamo")) return alternativa;
        bool existe = false;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Animations/Mixamo" }))
            if (System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == mixamo)
                existe = true;
        var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset");
        if (roster == null || roster.entries == null) { OmitirCambio("el roster del prólogo", $"animación {mixamo} de {actorId}"); return alternativa; }
        var entrada = roster.entries.Find(e => e != null && e.persistenceId == actorId);
        if (!existe || entrada?.prefab == null) return alternativa;
        var animator = entrada.prefab.GetComponentInChildren<Animator>(true);
        RuntimeAnimatorController runtime = animator != null ? animator.runtimeAnimatorController : null;
        while (runtime is AnimatorOverrideController variante) runtime = variante.runtimeAnimatorController;
        if (runtime is UnityEditor.Animations.AnimatorController controller)
            foreach (var capa in controller.layers)
                if (TieneEstado(capa.stateMachine, mixamo)) return mixamo;
        return alternativa;
    }

    private static bool TieneEstado(UnityEditor.Animations.AnimatorStateMachine maquina, string nombre)
    {
        if (maquina == null) return false;
        foreach (var hijo in maquina.states) if (hijo.state != null && hijo.state.name == nombre) return true;
        foreach (var hijo in maquina.stateMachines) if (TieneEstado(hijo.stateMachine, nombre)) return true;
        return false;
    }

    /// La ausencia de una pieza del montaje no impide aplicar los demás cambios.
    private static void OmitirCambio(string pieza, string cambio)
        => Debug.LogWarning($"[Prólogo] No encuentro {pieza}; se omite el cambio {cambio}.");

    private static void ModificarBeat<T>(List<SequenceBeat> beats, System.Predicate<T> coincide,
        string pieza, string cambio, System.Action<int, T> aplicar, bool ultimo = false) where T : SequenceBeat
    {
        if (beats != null)
            for (int paso = 0; paso < beats.Count; paso++)
            {
                int indice = ultimo ? beats.Count - 1 - paso : paso;
                if (beats[indice] is T beat && coincide(beat)) { aplicar(indice, beat); return; }
            }
        OmitirCambio(pieza, cambio);
    }

    private static List<SequenceBeat> BuscarFase(List<SequencePhase> fases, string nombre, string cambio)
    {
        if (fases != null)
            foreach (var fase in fases)
                if (fase != null && fase.name == nombre && fase.beats != null) return fase.beats;
        OmitirCambio($"la fase «{nombre}»", cambio);
        return null;
    }

    private static bool EsBloqueDeAplausos(ParallelBeat bloque)
    {
        if (bloque.beats == null || bloque.beats.Count != 6 || bloque.beats[0] is not WaitBeat) return false;
        string[] actores = { "NPC_Aldeano_02", "NPC_Aldeano_03", "NPC_Aldeano_04", "NPC_Aldeano_05", "NPC_Aldeano_06" };
        string[] gestos = { "Laugh01", "HandClap01", "HeadNod01", "Laugh01", "Question01" };
        for (int i = 0; i < actores.Length; i++)
            if (bloque.beats[i + 1] is not GestureBeat gesto || gesto.actorId != actores[i] || gesto.gesture != gestos[i]) return false;
        return true;
    }

    private static bool RetirarAplausos(List<SequenceBeat> beats, int respuesta)
    {
        if (beats != null && respuesta >= 0 && respuesta <= beats.Count - 3
            && beats[respuesta] is SayBeat linea && linea.textKey == "PROLOGO_BAILE_RESPUESTA"
            && beats[respuesta + 1] is ShotBeat plano && plano.framing != null
            && plano.framing.type == ShotType.Medium && plano.framing.subjectId == "NPC_Archimago"
            && plano.framing.secondaryId == "NPC_Aldeano_03"
            && beats[respuesta + 2] is ParallelBeat palmas && EsBloqueDeAplausos(palmas))
        {
            beats.RemoveRange(respuesta + 1, 2);
            return true;
        }
        OmitirCambio("el plano Medium Archimago/Aldeano_03 y su bloque de aplausos", "retirada de los aplausos sin diálogo");
        return false;
    }

    private static ShotBeat Cara(string actor, string interlocutor) => new()
    {
        note = "La cara del hablante queda visible y conserva el eje de la conversación.",
        duration = 0.8f, waitForArrival = true,
        framing = new ShotFraming { type = ShotType.Medium, subjectId = actor,
            secondaryId = interlocutor, encara = true, heightBias = 0f, distanceScale = 1f }
    };

    private static SayBeat LineaDeManana(string actor, string clave, string nombre, string gesto,
        params string[] oyentes) => new()
    {
        note = "El saludo y la respuesta se dirigen a los presentes, que miran y reaccionan.",
        actorId = actor, textKey = clave, speakerNameKey = nombre,
        colorDelNombre = ColorDelHablante(nombre), gesture = gesto,
        oyentes = new List<string>(oyentes), pageDuration = 2.8f
    };

    /// Completa solo las cinco fases de mañana; las fases de la amenaza conservan su montaje.
    private static void PrepararManana(List<SequencePhase> fases)
    {
        bool casaDisponible = PrepararCasaYNina();
        var manana = BuscarFase(fases, "1 - Un dia cualquiera", "puesta en escena de la mañana");
        if (manana != null)
        {
            manana.Insert(0, new EfectoDeVozBeat { note = "La voz cotidiana suena en el exterior del valle.", preset = PresetDeVoz.ExteriorDia });
            ModificarBeat<PlaceAtMarkBeat>(manana, p => p.actorId == "NPC_Archimago", "la colocación del Archimago", "salida desde su casa", (_, colocacion) =>
            {
                if (!casaDisponible) return;
                colocacion.markName = "M_TrasCasa_Archimago";
                colocacion.faceTowardsMark = "M_Apertura";
                colocacion.note = "Sale de la puerta de su casa y recorre a pie la entrada a la plaza.";
            });
            ModificarBeat<SayBeat>(manana, s => s.textKey == "PROLOGO_MANANA_INTRO", "PROLOGO_MANANA_INTRO", "entrada y saludos", (intro, saludo) =>
            {
                saludo.playGestures = false;
                saludo.oyentes = new List<string> { "NPC_Aldeano_01", "NPC_Aldeano_02" };
                // El seguimiento conserva la cara en cuadro durante todo el recorrido.
                ModificarBeat<ShotBeat>(manana.GetRange(0, intro), p => p.framing != null,
                    "el plano anterior a PROLOGO_MANANA_INTRO", "seguimiento de la entrada", (_, plano) =>
                {
                    // Walk-and-talk (INC-586): la cámara le recoge al doblar la esquina y le acompaña
                    // de frente mientras entra en la plaza saludando. Antes era un general fijo de la
                    // calle durante diez segundos mientras él andaba fuera de cuadro.
                    plano.live = true;
                    plano.framing.subjectId = "NPC_Archimago";
                    plano.framing.secondaryId = "";
                    plano.framing.type = ShotType.Medium;
                    plano.framing.distanceScale = 1.7f;
                    plano.framing.heightBias = 0f;
                    plano.framing.encara = false;
                }, ultimo: true);
                var recorrido = new List<string> { "M_Apertura" };
                if (casaDisponible) recorrido.Insert(0, "M_Esquina_Archimago");
                manana[intro] = new ParallelBeat
                {
                    note = "Dobla la esquina, saluda con la mano y da los buenos días andando, siempre de cara a la cámara que le acompaña.",
                    waitForAll = true,
                    beats = new List<SequenceBeat>
                    {
                        new WalkPathBeat { note = "De detrás de su casa a la plaza, rodeando la farola.", actorId = "NPC_Archimago", speed = 1.3f, esquivar = true, markNames = recorrido },
                        new SerieBeat { beats = new List<SequenceBeat> {
                            new WaitBeat { seconds = casaDisponible ? 2.0f : 0.4f, unscaled = true },
                            new GestureBeat { note = "Saluda sin activar la pose estática de conversación.", actorId = "NPC_Archimago", gesture = "HandWave01", repeats = 2, holdSeconds = 1.2f } } },
                        new SerieBeat { beats = new List<SequenceBeat> {
                            new WaitBeat { seconds = casaDisponible ? 2.4f : 0.6f, unscaled = true },
                            saludo } }
                    }
                };
                manana.InsertRange(intro + 1, new SequenceBeat[]
                {
                    new EmotionBeat { note = "Recibe los saludos con una sonrisa.", actorId = "NPC_Archimago", emotion = NPCEmotion.Happy },
                    Cara("NPC_Aldeano_01", "NPC_Archimago"),
                    LineaDeManana("NPC_Aldeano_01", "PROLOGO_MANANA_SALUDO_1", "Vecino", "HandWave01", "NPC_Archimago"),
                    Cara("NPC_Aldeano_02", "NPC_Archimago"),
                    LineaDeManana("NPC_Aldeano_02", "PROLOGO_MANANA_SALUDO_2", "Vecina", "HandWave02", "NPC_Archimago"),
                    new MoveToBeat { note = "El dueño de la carreta llega con urgencia antes de pedir ayuda.", actorId = "NPC_Aldeano_06", towardsActorId = "NPC_Archimago", speedOverride = 3.5f, stopDistance = 1.5f, timeout = 12f, faceEachOtherOnArrival = true }
                });
            });
            foreach (string clave in new[] { "PROLOGO_MANANA_SALUDO_1", "PROLOGO_MANANA_SALUDO_2" })
            {
                ModificarBeat<SayBeat>(manana, s => s.textKey == clave, clave, "asentimiento al saludo", (_, linea) => linea.reaccionDeOyentes = ReaccionDeOyente.Asentir);
            }
            ModificarBeat<MoveToBeat>(manana, m => m.actorId == "NPC_Archimago" && m.markName == "M_Carreta", "el paseo a M_Carreta", "acompañamiento del vecino", (haciaCarreta, paseoCarreta) =>
            {
                paseoCarreta.markName = "M_Carreta_Maestro";
                paseoCarreta.esquivar = true;
                manana[haciaCarreta] = new ParallelBeat
                {
                    note = "El vecino vuelve con el maestro hasta la carreta después de alcanzarlo.", waitForAll = true,
                    beats = new List<SequenceBeat>
                    {
                        paseoCarreta,
                        new MoveToBeat { note = "Ocupa el lado opuesto de la carreta.", actorId = "NPC_Aldeano_06", markName = "M_Carreta_Dueno", speedOverride = 1.8f, esquivar = true, timeout = 14f }
                    }
                };
                manana.InsertRange(haciaCarreta + 1, new SequenceBeat[] {
                    new FaceBeat { actorId = "NPC_Archimago", targetActorId = "PROP_Carreta", turnDuration = 0.3f },
                    new FaceBeat { actorId = "NPC_Aldeano_06", targetActorId = "PROP_Carreta", turnDuration = 0.3f }
                });
            });

            ModificarBeat<GestureBeat>(manana, g => g.actorId == "NPC_Archimago" && g.gesture == "MagicLeft", "MagicLeft del Archimago", "reparación agachado", (reparar, _) =>
            {
                manana.InsertRange(reparar, new SequenceBeat[]
                {
                    new GestureBeat { note = "Se agacha para revisar la madera antes del hechizo.", actorId = "NPC_Archimago", gesture = EstadoDisponible("NPC_Archimago", "Agacharse", "Loot01_Begin"), holdSeconds = 1f },
                    new PoseBeat { note = "Trabaja agachado en bucle, sin volver al reposo entre golpes.", actorId = "NPC_Archimago", pose = EstadoDisponible("NPC_Archimago", "ArreglarAgachado", "Loot01_Loop") },
                    new SfxBeat { note = "La madera cruje bajo sus manos.", eventKey = "SFX_Prologo_Madera_Rascar", atActorId = "PROP_Carreta", volume = 0.6f },
                    new WaitBeat { note = "Deja leer el trabajo manual.", seconds = 1f },
                    new SfxBeat { note = "Ajusta una pieza de la carreta.", eventKey = "SFX_Prologo_Madera_Golpe1", atActorId = "PROP_Carreta", volume = 0.7f },
                    new WaitBeat { note = "Separa los golpes para que el arreglo tenga ritmo.", seconds = 0.7f },
                    new SfxBeat { note = "Termina de encajar la madera.", eventKey = "SFX_Prologo_Madera_Golpe2", atActorId = "PROP_Carreta", volume = 0.7f },
                    new PoseBeat { note = "Suelta el bucle antes de incorporarse.", actorId = "NPC_Archimago", soltar = true, volverAIdle = false },
                    new GestureBeat { note = "Se incorpora y queda libre para levantar la carreta con magia.", actorId = "NPC_Archimago", gesture = EstadoDisponible("NPC_Archimago", "LevantarseDeRodillas", "Loot01_Stop"), holdSeconds = 1.2f, returnToNormalAfter = true },
                    new FaceBeat { note = "El vecino sigue la reparación y la magia con la mirada.", actorId = "NPC_Aldeano_06", targetActorId = "NPC_Archimago", turnDuration = 0.3f }
                });
            });

            ModificarBeat<MoveToBeat>(manana, m => m.markName == "M_Baile", "el paseo a M_Baile", "invitación y música del baile", (baile, paseo) =>
            {
                manana[baile] = new WalkPathBeat { actorId = paseo.actorId, speed = 1.4f, esquivar = true, markNames = new List<string> { "M_Rodeo_Abrevadero", "M_Baile" } };
                var corro = new List<SequenceBeat>();
                for (int i = 1; i <= 10; i++)
                    if (i != 5) corro.Add(new PlaceAtMarkBeat { actorId = $"NPC_Aldeano_{i:00}", markName = $"M_Baile_Corro_{i:00}", faceTowardsMark = "M_Baile" });
                manana.InsertRange(baile + 1, new SequenceBeat[]
                {
                    new ParallelBeat { waitForAll = true, beats = corro },
                    Cara("NPC_Aldeano_03", "NPC_Archimago"),
                    LineaDeManana("NPC_Aldeano_03", "PROLOGO_BAILE_INVITA", "Vecino", EstadoDisponible("NPC_Aldeano_03", "Senalar", "HandWave02"), "NPC_Archimago", "NPC_Aldeano_01", "NPC_Aldeano_02"),
                    new MusicBeat { note = "El baile cambia al tema del pueblo con el fundido del perfil.", musicId = "PROLOGUE_DANCE" },
                    new AccionDeEscenaBeat { accion = "BAILE", animo = AnimoDeAccion.Alegre, origenId = "NPC_Aldeano_01", implicados = new List<string> { "NPC_Archimago", "NPC_Aldeano_03", "NPC_Aldeano_04", "NPC_Aldeano_06", "NPC_Aldeano_07", "NPC_Aldeano_08", "NPC_Aldeano_09", "NPC_Aldeano_10" }, maxVoces = 1 }
                });
            });
            ModificarBeat<SayBeat>(manana, s => s.textKey == "PROLOGO_BAILE", "PROLOGO_BAILE", "promesa nocturna y risas", (elogio, _) =>
            {
                manana.InsertRange(elogio, new SequenceBeat[]
                {
                    Cara("NPC_Aldeano_02", "NPC_Archimago"),
                    LineaDeManana("NPC_Aldeano_02", "PROLOGO_BAILE_NOCHE", "Vecina", "", "NPC_Archimago", "NPC_Aldeano_01", "NPC_Aldeano_03"),
                    RisaDelCorro(),
                    Cara("NPC_Archimago", "NPC_Aldeano_03")
                });
            });
            ModificarBeat<SayBeat>(manana, s => s.textKey == "PROLOGO_BAILE_RESPUESTA", "PROLOGO_BAILE_RESPUESTA", "cierre del baile", (respuesta, _) =>
            {
                // Solo se retira el plano conocido con su bloque concreto de palmas.
                RetirarAplausos(manana, respuesta);
                manana.Insert(respuesta + 1, new MusicBeat { note = "Recupera el tema del prólogo al salir del baile.", musicId = "PROLOGUE_DREAM", continuarDondeIba = true });
            });
            CambiarBailes(manana);
            PrepararMontajeC1(manana);
            if (casaDisponible)
            {
                int planoInicial = manana.FindIndex(b => b is ShotBeat);
                if (planoInicial >= 0)
                {
                    var posiciones = new List<SequenceBeat> {
                        new PlaceAtMarkBeat { actorId = "NPC_Aldeano_01", markName = "M_Saludo_01", faceTowardsMark = "M_Apertura" },
                        new PlaceAtMarkBeat { actorId = "NPC_Aldeano_02", markName = "M_Saludo_02", faceTowardsMark = "M_Apertura" }
                    };
                    for (int i = 3; i <= 10; i++)
                        if (i != 5) posiciones.Add(new PlaceAtMarkBeat { actorId = $"NPC_Aldeano_{i:00}", markName = $"M_Fondo_Manana_{i:00}", faceTowardsMark = "M_Apertura" });
                    manana.InsertRange(planoInicial, posiciones);
                }
            }
        }

        var rio = BuscarFase(fases, "2 - El rio", "puesta en escena del río");
        if (rio != null)
        {
            rio.Insert(0, new PostprocesoBeat { perfil = PrepararPerfilAtardecer(), transicion = 0f });
            foreach (var beat in rio)
            {
                // Inmediato y sin esperar: esperar la transición dejaba 10 s de plano muerto al salir del pueblo.
                if (beat is TimeOfDayBeat hora) { hora.immediate = true; hora.waitForTransition = false; }
                if (beat is SolDeFondoBeat sol) { sol.puesta = 90f; sol.alturaDePuesta = 0.65f; }
                if (beat is ShotBeat plano && plano.framing != null && plano.framing.type == ShotType.Wide) plano.framing.heightBias = 0f;
            }
            ModificarBeat<GestureBeat>(rio, g => g.gesture == "Laugh01", "la risa del río", "general y contacto de afecto", (risa, _) =>
            {
                rio.InsertRange(risa, new SequenceBeat[]
                {
                    new ShotBeat { note = "Vuelve al general con el sol antes de compartir la risa.", waitForArrival = true, duration = 1f, framing = new ShotFraming { type = ShotType.Wide, subjectId = "NPC_Archimago", secondaryId = "NPC_Liora", heightBias = 0f, distanceScale = 1.6f } },
                    new GestureBeat { note = "El gesto de afecto cierra la conversación; el asentimiento sirve si falta el contacto importado.", actorId = "NPC_Archimago", gesture = EstadoDisponible("NPC_Archimago", "PalmadaEnLaEspalda", "HeadNod01"), holdSeconds = 0.8f }
                });
            });
            ModificarBeat<GestureBeat>(rio, g => g.actorId == "NPC_Liora" && g.gesture == "Laugh01", "Laugh01 de Liora", "risa simultánea", (reir, _) =>
            {
                if (reir + 1 >= rio.Count || rio[reir + 1] is not GestureBeat segunda
                || segunda.actorId != "NPC_Archimago" || segunda.gesture != "Laugh01")
                {
                    OmitirCambio("la risa del Archimago detrás de la de Liora", "risa simultánea");
                    return;
                }
                var risas = new List<SequenceBeat> { rio[reir], rio[reir + 1] };
                rio.RemoveRange(reir, 2);
                rio.Insert(reir, new ParallelBeat { note = "Ambos ríen juntos en el plano abierto con el sol.", waitForAll = true, beats = risas });
            });
            // El general permanece durante ambas risas, sin un corte cerrado posterior.
            ModificarBeat<ShotBeat>(rio, p => p.framing != null, "el último plano del río", "general de cierre", (_, plano) =>
            {
                plano.framing.type = ShotType.Wide;
                plano.framing.heightBias = 0f;
            }, ultimo: true);
        }
        foreach (string nombre in new[] { "1 - Un dia cualquiera", "1b - El globo se libera bien", "1c - El globo revienta", "2 - El rio", "3 - Algo cambia en el cielo" })
        {
            var beats = BuscarFase(fases, nombre, "planos y oyentes de la mañana");
            if (beats != null) PrepararLineas(beats, nombre.StartsWith("2 -") || nombre.StartsWith("3 -"));
        }
    }


    private static ParallelBeat RisaDelCorro()
    {
        var beats = new List<SequenceBeat>();
        foreach (string actor in new[] { "NPC_Archimago", "NPC_Aldeano_01", "NPC_Aldeano_02", "NPC_Aldeano_03", "NPC_Aldeano_04", "NPC_Aldeano_05", "NPC_Aldeano_06", "NPC_Aldeano_07", "NPC_Aldeano_08", "NPC_Aldeano_09", "NPC_Aldeano_10" })
            beats.Add(new GestureBeat { note = "Comparte la risa con todo el corro.", actorId = actor, gesture = "Laugh01", holdSeconds = 1.3f, voz = actor == "NPC_Archimago" || actor == "NPC_Aldeano_01" || actor == "NPC_Aldeano_02" ? VozDelGesto.Automatica : VozDelGesto.Ninguna });
        return new ParallelBeat { note = "La promesa del baile nocturno hace reír a todos a la vez.", waitForAll = true, beats = beats };
    }

    private static void CambiarBailes(List<SequenceBeat> beats)
    {
        if (beats == null) return;
        foreach (var beat in beats)
        {
            if (beat is ParallelBeat paralelo) CambiarBailes(paralelo.beats);
            if (beat is GestureBeat gesto && gesto.gesture == "Dance_NoWeapon")
            {
                gesto.gesture = EstadoDisponible(gesto.actorId, gesto.actorId == "NPC_Aldeano_01" ? "Baile_B" : "Baile_A", "Dance_NoWeapon");
                gesto.returnToNormalAfter = true;
                gesto.note = "Baila con el repertorio disponible y libera la pose al terminar.";
            }
        }
    }

    private static void PrepararLineas(List<SequenceBeat> beats, bool juntoALiora)
    {
        if (beats == null) return;
        for (int i = 0; i < beats.Count; i++)
        {
            if (beats[i] is ParallelBeat paralelo) { PrepararLineas(paralelo.beats, juntoALiora); continue; }
            if (beats[i] is SerieBeat serie) { PrepararLineas(serie.beats, juntoALiora); continue; }
            if (beats[i] is not SayBeat linea || string.IsNullOrEmpty(linea.actorId)) continue;
            linea.oyentes ??= new List<string>();
            if (linea.oyentes.Count == 0)
            {
                string interlocutor = "NPC_Archimago";
                if (linea.actorId == "NPC_Archimago")
                {
                    interlocutor = juntoALiora ? "NPC_Liora" : "NPC_Aldeano_05";
                    for (int j = i - 1; j >= 0; j--)
                        if (beats[j] is ShotBeat plano && plano.framing != null)
                        {
                            if (!string.IsNullOrEmpty(plano.framing.secondaryId) && plano.framing.secondaryId.StartsWith("NPC_", System.StringComparison.Ordinal))
                                interlocutor = plano.framing.secondaryId;
                            break;
                        }
                }
                linea.oyentes.Add(interlocutor);
            }
            if (linea.textKey != null && linea.textKey.StartsWith("PROLOGO_BAILE", System.StringComparison.Ordinal))
                foreach (string id in new[] { "NPC_Archimago", "NPC_Aldeano_01", "NPC_Aldeano_02", "NPC_Aldeano_03" })
                    if (id != linea.actorId && !linea.oyentes.Contains(id)) linea.oyentes.Add(id);
            if (linea.textKey != null && linea.textKey.Contains("GLOBO"))
                foreach (string id in new[] { "NPC_Archimago", "NPC_Aldeano_05", "NPC_Aldeano_03", "NPC_Aldeano_08" })
                    if (id != linea.actorId && !linea.oyentes.Contains(id)) linea.oyentes.Add(id);
            string oyente = linea.oyentes[0];
            if (linea.playGestures)
            {
                beats.Insert(i++, new FaceBeat { note = "Habla mirando a su interlocutor.", actorId = linea.actorId, targetActorId = oyente, turnDuration = 0.3f });
                beats.Insert(i++, Cara(linea.actorId, oyente));
            }
            else if (linea.textKey != "PROLOGO_MANANA_INTRO") beats.Insert(i++, Cara(linea.actorId, oyente));
            // Los gestos concretos se reservan para acciones que requieren una intención explícita.
            if (!string.IsNullOrEmpty(linea.gesture) && linea.gesture.StartsWith("Talk", System.StringComparison.Ordinal)) linea.gesture = "";
        }
    }

    /// Mantiene el montaje de movimiento y completa la interpretación de las fases nocturnas.
    private static void PrepararNoche(List<SequencePhase> fases)
    {
        var llegada = BuscarFase(fases, "4 - La llegada", "voz exterior nocturna");
        if (llegada != null)
        {
            llegada.Insert(0, new EfectoDeVozBeat { note = "La llegada abre el tratamiento de voz del exterior nocturno.", preset = PresetDeVoz.ExteriorNoche });
            ModificarBeat<PlaceAtMarkBeat>(llegada, p => p.actorId == "NPC_MagoOscuro", "la aparición del Mago Oscuro", "miradas de los vecinos", (indice, _) =>
            {
                llegada.Insert(indice + 1, VecinosAnteLaAmenaza());
                // Ánimo de amenaza sostenido (INC-578/586): Liora, Nina y el Archimago le miran con miedo
                // y ninguna cara vuelve al reposo sonriente mientras él esté en el valle.
                llegada.Insert(indice + 2, new AccionDeEscenaBeat { note = "Aparece el Mago Oscuro: amenaza para todos los presentes.", accion = "APARECE_VILLANO", animo = AnimoDeAccion.Amenaza, origenId = "NPC_MagoOscuro", implicados = new List<string> { "NPC_Liora", "NPC_Archimago", "NPC_Aldeano_05" }, sostenerCara = true, voces = true, maxVoces = 1 });
            });
        }
        var evacuacion = BuscarFase(fases, "5 - La orden de evacuar", "despedida y evacuación");
        if (evacuacion != null)
        {
            // Antes de «¿Quién es? ¿Qué quiere?» no hay despedida ni reverencia: Liora pregunta asustada (INC-586).
            ModificarBeat<SayBeat>(evacuacion, s => s.textKey == "PROLOGO_DESPEDIDA_LIORA", "PROLOGO_DESPEDIDA_LIORA", "ánimo de despedida", (indice, _) =>
                evacuacion.Insert(indice, new AccionDeEscenaBeat { note = "La despedida entristece a Liora.", accion = "DESPEDIDA", animo = AnimoDeAccion.Triste, origenId = "NPC_Archimago", implicados = new List<string> { "NPC_Liora" }, sostenerCara = true, voces = false }));
            ModificarBeat<SayBeat>(evacuacion, s => s.textKey == "PROLOGO_EVACUACION_LIORA", "PROLOGO_EVACUACION_LIORA", "gesto de ánimo antes de partir", (indice, _) =>
            {
                evacuacion.Insert(indice, ContactoDeDespedida(true));
                evacuacion.Insert(indice + 1, new AccionDeEscenaBeat { note = "Huida: miedo en todos los que se van.", accion = "PELIGRO", animo = AnimoDeAccion.Miedo, origenId = "NPC_MagoOscuro", implicados = new List<string> { "NPC_Liora", "NPC_Aldeano_05", "NPC_Aldeano_07", "NPC_Aldeano_08", "NPC_Aldeano_09", "NPC_Aldeano_10" }, mirarAlOrigen = false, sostenerCara = true, voces = true, maxVoces = 1 });
            });
            // El giro sucede después de que arranque la marcha, sin esperar a que crucen el puente.
            ModificarBeat<ParallelBeat>(evacuacion, p => p.beats != null && p.beats.Exists(b => b is WalkPathBeat w
                    && w.actorId == "NPC_Liora" && w.markNames != null && w.markNames.Contains("M_Puente_Ent")),
                "la salida de Liora y los vecinos hacia M_Puente_Ent", "mirada al puente y desafío", (indice, _) =>
                evacuacion.Insert(indice + 1, new SerieBeat
                {
                    note = "Los sigue con la mirada y se vuelve para proteger su retirada.",
                    beats = new List<SequenceBeat>
                    {
                        new FaceBeat { note = "Vigila a los que se alejan hacia el puente.", actorId = "NPC_Archimago", markName = "M_Puente_Ent", turnDuration = 0.4f },
                        new ShotBeat { note = "Tres cuartos de espalda, con la retirada como fondo.", duration = 0.8f, waitForArrival = true,
                            framing = new ShotFraming { type = ShotType.Medium, subjectId = "NPC_Archimago", secondaryId = "NPC_MagoOscuro", heightBias = 0f, distanceScale = 1.4f, encara = false } },
                        new WaitBeat { note = "Comprueba que avanzan antes de volver a la amenaza.", seconds = 1.2f, unscaled = true },
                        new ParallelBeat
                        {
                            note = "El giro, el desafío y la decisión de su cara arrancan juntos.", waitForAll = true,
                            beats = new List<SequenceBeat>
                            {
                                new FaceBeat { note = "Se planta ante el invasor.", actorId = "NPC_Archimago", targetActorId = "NPC_MagoOscuro", turnDuration = 0.45f },
                                new GestureBeat { note = "El desafío declara que se queda a defender el valle.", actorId = "NPC_Archimago", gesture = "Challenging_NoWeapon", holdSeconds = 0.9f },
                                new EmotionBeat { note = "La preocupación se convierte en decisión.", actorId = "NPC_Archimago", emotion = NPCEmotion.Determined, mantener = true },
                                Cara("NPC_Archimago", "NPC_MagoOscuro")
                            }
                        }
                    }
                }));
        }
        var duelo = BuscarFase(fases, "7 - El duelo", "cargas y reacciones del duelo");
        if (duelo != null)
        {
            PrepararCargas(duelo);
            ModificarBeat<SfxBeat>(duelo, s => s.eventKey == "Prologo_Choque", "Prologo_Choque", "reacción al choque de magias", (indice, _) =>
                duelo.Insert(indice + 1, new ParallelBeat
                {
                    note = "Ambos encajan el choque antes del retroceso de la coreografía.", waitForAll = true,
                    beats = new List<SequenceBeat>
                    {
                        new GestureBeat { note = "El protector acusa el impacto de la onda.", actorId = "NPC_Archimago", gesture = "DefendHit_NoWeapon", holdSeconds = 0.35f, returnToNormalAfter = true },
                        new GestureBeat { note = "El invasor también recibe la sacudida del choque.", actorId = "NPC_MagoOscuro", gesture = "TakeDamage", holdSeconds = 0.35f, returnToNormalAfter = true }
                    }
                }));
        }
        bool primeraVozOscura = true;
        if (llegada != null) PrepararLineasDeNoche(llegada, false, ref primeraVozOscura);
        if (evacuacion != null) PrepararLineasDeNoche(evacuacion, false, ref primeraVozOscura);
        if (duelo != null) PrepararLineasDeNoche(duelo, true, ref primeraVozOscura);
        if (primeraVozOscura) OmitirCambio("la primera frase del Mago Oscuro en las fases nocturnas", "atenuación de la música durante su voz");
    }

    private static ParallelBeat VecinosAnteLaAmenaza()
    {
        var beats = new List<SequenceBeat>();
        for (int i = 1; i <= 10; i++)
        {
            string actor = $"NPC_Aldeano_{i:00}";
            beats.Add(new SerieBeat
            {
                note = "El miedo tiene un destinatario visible: la figura que acaba de llegar.",
                beats = new List<SequenceBeat>
                {
                    new FaceBeat { note = "Mira al invasor antes de reaccionar.", actorId = actor, targetActorId = "NPC_MagoOscuro", turnDuration = 0.25f },
                    new EmotionBeat { note = "La aparición inquieta a los vecinos.", actorId = actor, emotion = NPCEmotion.Scared, mantener = true },
                    new GestureBeat { note = "Cada vecino expresa el susto con un gesto distinto.", actorId = actor,
                        gesture = i % 3 == 0 ? "SenseSomethingStart_NoWeapon" : i % 2 == 0 ? "Beg01" : "HeadShake01", holdSeconds = 0.8f }
                }
            });
        }
        return new ParallelBeat { note = "Los vecinos miran a la amenaza y reaccionan juntos.", waitForAll = true, beats = beats };
    }

    private static SerieBeat ContactoDeDespedida(bool alPartir)
    {
        string archimago = EstadoDisponible("NPC_Archimago", "ApretonDeManos_A", "Reverence01");
        string liora = EstadoDisponible("NPC_Liora", "ApretonDeManos_B", "HeadNod01");
        bool apreton = !alPartir && archimago == "ApretonDeManos_A" && liora == "ApretonDeManos_B";
        if (!apreton)
        {
            archimago = alPartir ? EstadoDisponible("NPC_Archimago", "PalmadaEnLaEspalda", "HeadNod01") : "Reverence01";
            liora = alPartir ? "Beg01" : "HeadNod01";
        }
        return new SerieBeat
        {
            note = apreton ? "Las dos mitades del apretón se reproducen juntas, cara a cara." : "Sin el par completo de apretón, la despedida usa palmada disponible o reverencia, asentimiento y súplica; no representa un abrazo.",
            beats = new List<SequenceBeat>
            {
                new MoveToBeat { note = "Acerca a Liora sin superponer los cuerpos; la distancia permite que coincidan las manos.", actorId = "NPC_Liora", towardsActorId = "NPC_Archimago", stopDistance = apreton ? 0.85f : 1.1f, approachAngle = 0f, speedOverride = 1.2f, timeout = 5f, faceEachOtherOnArrival = true },
                new FaceBeat { note = "Ambos se miran durante el contacto.", actorId = "NPC_Archimago", targetActorId = "NPC_Liora", mutual = true, turnDuration = 0.25f },
                new ShotBeat { note = "Las manos y las caras comparten el plano de despedida.", duration = 0.7f, waitForArrival = true,
                    framing = new ShotFraming { type = ShotType.TwoShot, subjectId = "NPC_Archimago", secondaryId = "NPC_Liora", heightBias = 0f, distanceScale = 1.2f, encara = true } },
                new ParallelBeat
                {
                    note = apreton ? "Las manos se encuentran con las animaciones complementarias." : "Los gestos alternativos expresan afecto y preocupación sin fingir contacto inexistente.",
                    waitForAll = true,
                    beats = new List<SequenceBeat>
                    {
                        new GestureBeat { note = "Da ánimo a Liora antes de separarse.", actorId = "NPC_Archimago", gesture = archimago, holdSeconds = 1.3f, returnToNormalAfter = true },
                        new GestureBeat { note = "Acepta la despedida con preocupación.", actorId = "NPC_Liora", gesture = liora, holdSeconds = 1.3f, returnToNormalAfter = true }
                    }
                }
            }
        };
    }

    private static void PrepararCargas(List<SequenceBeat> beats)
    {
        if (beats == null) return;
        for (int i = 0; i < beats.Count; i++)
        {
            if (beats[i] is ParallelBeat paralelo) { PrepararCargas(paralelo.beats); continue; }
            if (beats[i] is SerieBeat serie) { PrepararCargas(serie.beats); continue; }
            if (beats[i] is not GestureBeat lanzamiento || (lanzamiento.gesture != "MagicLeft"
                && lanzamiento.gesture != "MagicRight" && lanzamiento.gesture != "MagicSpecial")) continue;
            beats[i] = new SerieBeat
            {
                note = "La carga precede al gesto de lanzamiento original; los hechizos y saltos conservan su coreografía.",
                beats = new List<SequenceBeat>
                {
                    new PoseBeat { note = "Libera la guardia sostenida para permitir la carga.", actorId = lanzamiento.actorId, soltar = true, volverAIdle = false },
                    new GestureBeat { note = "Acumula magia antes de extender la mano.", actorId = lanzamiento.actorId, gesture = "MagicAttackOmni01_Load", holdSeconds = 0.65f },
                    lanzamiento
                }
            };
        }
    }

    private static void PrepararLineasDeNoche(List<SequenceBeat> beats, bool enDuelo, ref bool primeraVozOscura, bool enParalelo = false)
    {
        if (beats == null) return;
        for (int i = 0; i < beats.Count; i++)
        {
            if (beats[i] is ParallelBeat paralelo) { PrepararLineasDeNoche(paralelo.beats, enDuelo, ref primeraVozOscura, true); continue; }
            if (beats[i] is SerieBeat serie) { PrepararLineasDeNoche(serie.beats, enDuelo, ref primeraVozOscura); continue; }
            if (beats[i] is not SayBeat linea || string.IsNullOrEmpty(linea.actorId)) continue;
            linea.oyentes ??= new List<string>();
            string oyente = linea.actorId == "NPC_MagoOscuro" ? "NPC_Archimago"
                : linea.actorId == "NPC_Liora" ? "NPC_Archimago" : enDuelo ? "NPC_MagoOscuro" : "NPC_Liora";
            if (linea.textKey == "PROLOGO_EVACUACION_LIORA")
            {
                oyente = "NPC_Aldeano_07";
                foreach (string id in new[] { "NPC_Aldeano_07", "NPC_Aldeano_08", "NPC_Aldeano_09", "NPC_Aldeano_10" })
                    if (!linea.oyentes.Contains(id)) linea.oyentes.Add(id);
            }
            else if (!linea.oyentes.Contains(oyente)) linea.oyentes.Insert(0, oyente);
            var montaje = new List<SequenceBeat>();
            bool imponer = primeraVozOscura && linea.actorId == "NPC_MagoOscuro";
            if (imponer)
            {
                primeraVozOscura = false;
                montaje.Add(new MezclaBeat { note = "Su primera voz se impone por encima del tema musical.", fuente = "primeraVozOscura", musicaDb = -10f, ambienteDb = -3f, fundido = 0.35f });
            }
            if (!enDuelo && linea.actorId == "NPC_Liora")
                montaje.Add(new EmotionBeat { note = "La despedida y la retirada mantienen la preocupación de Liora.", actorId = linea.actorId, emotion = NPCEmotion.Worried, mantener = true });
            if (!enDuelo && linea.actorId == "NPC_Archimago")
                montaje.Add(new EmotionBeat { note = "La tristeza íntima deja paso a la decisión de protegerlos.", actorId = linea.actorId,
                    emotion = linea.textKey == "PROLOGO_DESPEDIDA_ARCHIMAGO_1" ? NPCEmotion.Sad : NPCEmotion.Determined, mantener = true });
            if (linea.textKey == "PROLOGO_HORA_ARCHIMAGO")
            {
                // «¡Al puente! ¡Todos, ahora!»: mira y señala el puente durante toda la frase; no se
                // vuelve hacia Liora (INC-586).
                linea.oyentes = new List<string> { "NPC_Aldeano_07", "NPC_Aldeano_08", "NPC_Aldeano_09" };
                linea.playGestures = false;
                montaje.Add(new FaceBeat { note = "Mira hacia el puente.", actorId = linea.actorId, markName = "M_Puente_Ent", turnDuration = 0.3f });
                montaje.Add(new ShotBeat { note = "De frente, con el gesto hacia el puente.", duration = 0.6f, waitForArrival = true,
                    framing = new ShotFraming { type = ShotType.Medium, subjectId = linea.actorId, secondaryId = "", heightBias = 0f, distanceScale = 1.3f, encara = false } });
                string senalar = EstadoDisponible(linea.actorId, "Senalar", "");
                if (!string.IsNullOrEmpty(senalar))
                    montaje.Add(new ParallelBeat { note = "Señala el puente mientras lo grita.", waitForAll = true, beats = new List<SequenceBeat> {
                        new GestureBeat { actorId = linea.actorId, gesture = senalar, holdSeconds = 1.2f, returnToNormalAfter = true }, linea } });
                else montaje.Add(linea);
            }
            else if (!enDuelo && linea.actorId == "NPC_Liora")
            {
                // Liora solo gesticula con intención; nada de gestos automáticos al azar (INC-586).
                linea.playGestures = false;
                montaje.Add(new FaceBeat { note = "Habla mirando a quien se dirige.", actorId = linea.actorId, targetActorId = oyente, turnDuration = 0.25f });
                montaje.Add(Cara(linea.actorId, oyente));
                string gestoDeLiora = linea.textKey switch
                {
                    "PROLOGO_DESPEDIDA_LIORA_1" => EstadoDisponible(linea.actorId, "SenseSomethingStart_NoWeapon", ""),
                    "PROLOGO_DESPEDIDA_LIORA_2" => EstadoDisponible(linea.actorId, "Beg01", ""),
                    "PROLOGO_EVACUACION_LIORA" => EstadoDisponible(linea.actorId, "Senalar", "HandWave02"),
                    _ => ""
                };
                if (!string.IsNullOrEmpty(gestoDeLiora))
                    montaje.Add(new ParallelBeat { note = "Un gesto con intención acompaña la frase.", waitForAll = true, beats = new List<SequenceBeat> {
                        new GestureBeat { actorId = linea.actorId, gesture = gestoDeLiora, holdSeconds = 0.9f, returnToNormalAfter = true }, linea } });
                else montaje.Add(linea);
            }
            else
            {
                if (linea.playGestures)
                    montaje.Add(new FaceBeat { note = "Dirige la frase a quienes la escuchan.", actorId = linea.actorId, targetActorId = oyente, turnDuration = 0.25f });
                montaje.Add(Cara(linea.actorId, oyente));
                montaje.Add(linea);
            }
            if (imponer) montaje.Add(new MezclaBeat { note = "Libera la mezcla al acabar su primera intervención.", fuente = "primeraVozOscura", quitar = true, fundido = 0.7f });
            if (enParalelo) beats[i] = new SerieBeat { note = "El plano precede a la frase también dentro de una rama paralela.", beats = montaje };
            else
            {
                beats.RemoveAt(i);
                beats.InsertRange(i, montaje);
                i += montaje.Count - 1;
            }
        }
    }

    /// La escena y el roster se guardan desde el Editor, sin sobrescribir su YAML externamente.
    private static bool PrepararCasaYNina()
    {
        const string rutaEscena = "Assets/Scenes/Worlds/Prologo_Valle.unity";
        var escena = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(rutaEscena);
        bool cerrar = !escena.isLoaded;
        bool casaDisponible = false;
        try
        {
            if (cerrar) escena = EditorSceneManager.OpenScene(rutaEscena, OpenSceneMode.Additive);
            if (escena.isDirty && !EditorSceneManager.SaveScene(escena))
                OmitirCambio("el guardado de Prologo_Valle", "salida desde la casa del Archimago");
            else
            {
                casaDisponible = PrepararMarcaCasa(escena);
                casaDisponible = PrepararMarcasC1(escena) && casaDisponible;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Prólogo] Se omite la marca de la casa del Archimago: {ex.Message}");
        }
        finally
        {
            if (cerrar && escena.isLoaded)
                try { EditorSceneManager.CloseScene(escena, true); }
                catch (System.Exception ex) { Debug.LogWarning($"[Prólogo] No se puede cerrar Prologo_Valle: {ex.Message}"); }
        }
        try
        {
            var roster = AssetDatabase.LoadAssetAtPath<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_PrologoValle.asset");
            var nina = roster != null && roster.entries != null ? roster.entries.Find(e => e != null && e.persistenceId == "NPC_Aldeano_05") : null;
            // Aldeana genérica: cabeza femenina y pelo visibles, sin identidad de tienda ni personaje con nombre.
            var prefab = PrepararPeloDeNina(Prefab("232061d19d07fce4a801b15fcf95b75c"));
            if (nina == null || prefab == null) Debug.LogWarning("[Prólogo] Falta Nina o el prefab de la aldeana genérica MC01 (1) 6.");
            else if (roster.entries.Exists(e => e != nina && e != null && e.prefab == prefab))
                Debug.LogWarning("[Prólogo] La aldeana genérica MC01 (1) 6 ya está utilizada en el roster; elige otro prefab para Nina.");
            else if (nina.prefab != prefab)
            {
                Undo.RecordObject(roster, "Dar a Nina una cara visible");
                nina.prefab = prefab;
                EditorUtility.SetDirty(roster);
            }
            if (roster != null && roster.entries != null)
            {
                Undo.RecordObject(roster, "Asignar voces del reparto del prólogo");
                foreach (var entrada in roster.entries)
                {
                    if (entrada?.prefab == null || string.IsNullOrEmpty(entrada.persistenceId) || !entrada.persistenceId.StartsWith("NPC_Aldeano_", System.StringComparison.Ordinal)) continue;
                    bool femenina = false;
                    foreach (var t in entrada.prefab.GetComponentsInChildren<Transform>(true))
                        if (t.name == "Head02_Female" && t.gameObject.activeSelf) femenina = true;
                    entrada.vozDeReacciones = femenina ? "Vecina" : "Vecino";
                }
                EditorUtility.SetDirty(roster);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Prólogo] Se omite el cambio de Nina: {ex.Message}");
        }
        return casaDisponible;
    }

    private static bool PrepararMarcaCasa(UnityEngine.SceneManagement.Scene escena)
    {
        SequenceStage stage = null;
        var casas = new List<Transform>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (stage == null) stage = t.GetComponent<SequenceStage>();
                if (t.name.StartsWith("Casa_", System.StringComparison.Ordinal)) casas.Add(t);
            }
        if (stage == null) { Debug.LogWarning("[Prólogo] Prologo_Valle no contiene SequenceStage."); return false; }
        var serializado = new SerializedObject(stage);
        var marcas = serializado.FindProperty("_marks");
        if (marcas == null || !marcas.isArray) { OmitirCambio("las marcas de SequenceStage", "marca de la casa del Archimago"); return false; }
        Transform apertura = null;
        bool existe = false;
        for (int i = 0; i < marcas.arraySize; i++)
        {
            var marca = marcas.GetArrayElementAtIndex(i);
            var nombreMarca = marca.FindPropertyRelative("name");
            var destino = marca.FindPropertyRelative("target");
            if (nombreMarca == null || destino == null) { OmitirCambio("el nombre o destino de una marca", "lectura de esa marca"); continue; }
            string nombre = nombreMarca.stringValue;
            if (nombre == "M_Apertura") apertura = destino.objectReferenceValue as Transform;
            if (nombre == "M_Casa_Archimago") existe = destino.objectReferenceValue != null;
        }
        if (!existe)
        {
            if (apertura == null || casas.Count == 0) { Debug.LogWarning("[Prólogo] Faltan M_Apertura o las casas del valle."); return false; }
            casas.Sort((a, b) => (a.position - apertura.position).sqrMagnitude.CompareTo((b.position - apertura.position).sqrMagnitude));
            Transform casa = casas[0], puerta = null;
            foreach (var t in casa.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf("door", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("puerta", System.StringComparison.OrdinalIgnoreCase) >= 0) { puerta = t; break; }
            Vector3 posicion;
            if (puerta != null) posicion = puerta.position + (apertura.position - puerta.position).normalized * 0.9f;
            else
            {
                // Las casas de malla única no exponen una puerta: el frente hacia la plaza queda fuera de su volumen.
                var bounds = new Bounds(casa.position, Vector3.zero);
                foreach (var renderer in casa.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                posicion = bounds.ClosestPoint(apertura.position) + (apertura.position - bounds.center).normalized * 0.9f;
            }
            posicion.y = apertura.position.y;
            var objeto = new GameObject("M_Casa_Archimago");
            Undo.RegisterCreatedObjectUndo(objeto, "Crear marca de la casa del Archimago");
            objeto.transform.SetParent(stage.transform);
            objeto.transform.position = posicion;
            marcas.InsertArrayElementAtIndex(marcas.arraySize);
            var nueva = marcas.GetArrayElementAtIndex(marcas.arraySize - 1);
            nueva.FindPropertyRelative("name").stringValue = objeto.name;
            nueva.FindPropertyRelative("target").objectReferenceValue = objeto.transform;
            serializado.ApplyModifiedProperties();
            EditorUtility.SetDirty(stage);
            EditorSceneManager.MarkSceneDirty(escena);
            if (!EditorSceneManager.SaveScene(escena)) { OmitirCambio("el guardado de Prologo_Valle", "salida desde la casa del Archimago"); return false; }
        }
        return true;
    }

    /// Nina es una niña del pueblo: coletas (Hair08), ropa sencilla de diario (Body04) y sin capa.
    /// Se monta sobre la aldeana genérica activando las piezas del propio pack modular, que trae
    /// todas las variantes como hijos apagados. Ver INC-586.
    private static GameObject PrepararPeloDeNina(GameObject original)
    {
        if (original == null) return null;
        const string ruta = "Assets/_NPCs/NonInteractable/Nina_Prologo.prefab";
        string fuente = AssetDatabase.GetAssetPath(original);
        var instancia = PrefabUtility.LoadPrefabContents(fuente);
        try
        {
            bool pelo = false, cuerpo = false;
            foreach (var t in instancia.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.StartsWith("Hair", System.StringComparison.Ordinal) && n.Length == 6)
                { t.gameObject.SetActive(n == "Hair08"); pelo |= n == "Hair08"; }
                else if (n.StartsWith("Body", System.StringComparison.Ordinal) && n.Length == 6)
                { t.gameObject.SetActive(n == "Body04"); cuerpo |= n == "Body04"; }
                else if (n.StartsWith("Cloak", System.StringComparison.Ordinal) || n.StartsWith("Hat", System.StringComparison.Ordinal)
                    || n.StartsWith("HeadArmor", System.StringComparison.Ordinal) || n.StartsWith("AC0", System.StringComparison.Ordinal))
                    t.gameObject.SetActive(false);
            }
            if (!pelo || !cuerpo) { OmitirCambio("Hair08 o Body04 en la aldeana genérica", "aspecto de Nina"); return original; }
            var resultado = PrefabUtility.SaveAsPrefabAsset(instancia, ruta);
            if (resultado == null) { OmitirCambio("el guardado del prefab de Nina", "aspecto de Nina"); return original; }
            return resultado;
        }
        finally { PrefabUtility.UnloadPrefabContents(instancia); }
    }

    private static VolumeProfile PrepararPerfilAtardecer()
    {
        const string ruta = "Assets/Art/World/Prologo_Valle/Prologo_Atardecer.asset";
        try
        {
            var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
            if (perfil == null)
            {
                if (!AssetDatabase.CopyAsset(PrologoPostprocesoSueno.Dia, ruta))
                { OmitirCambio("el perfil Día", "copia de atardecer"); return Perfil(PrologoPostprocesoSueno.Dia); }
                perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
            }
            T Componente<T>() where T : VolumeComponent
            {
                if (!perfil.TryGet<T>(out var c))
                {
                    c = perfil.Add<T>(true);
                    AssetDatabase.AddObjectToAsset(c, perfil);
                }
                c.active = true;
                EditorUtility.SetDirty(c);
                return c;
            }
            // Atardecer que se lee a primera vista (INC-586): luz y cielo anaranjados, sombras
            // malvas y el sol brillando. El valor anterior (+24 de temperatura) apenas se notaba.
            var blanco = Componente<UnityEngine.Rendering.Universal.WhiteBalance>();
            blanco.temperature.Override(48f);
            blanco.tint.Override(14f);
            var color = Componente<UnityEngine.Rendering.Universal.ColorAdjustments>();
            color.colorFilter.Override(new Color(1f, 0.8f, 0.62f));
            color.saturation.Override(14f);
            color.contrast.Override(10f);
            color.postExposure.Override(0.05f);
            var tonos = Componente<UnityEngine.Rendering.Universal.SplitToning>();
            tonos.shadows.Override(new Color(0.42f, 0.25f, 0.55f));
            tonos.highlights.Override(new Color(1f, 0.62f, 0.32f));
            tonos.balance.Override(-15f);
            var brillo = Componente<UnityEngine.Rendering.Universal.Bloom>();
            brillo.intensity.Override(0.7f);
            brillo.threshold.Override(0.85f);
            brillo.tint.Override(new Color(1f, 0.75f, 0.5f));
            var vineta = Componente<UnityEngine.Rendering.Universal.Vignette>();
            vineta.intensity.Override(0.3f);
            vineta.color.Override(new Color(0.25f, 0.08f, 0.04f));
            EditorUtility.SetDirty(perfil);
            return perfil;
        }
        catch (System.Exception ex)
        {
            OmitirCambio(ex.Message, "perfil de atardecer");
            return Perfil(PrologoPostprocesoSueno.Dia);
        }
    }

    /// Crea destinos separados y recortes de navegación sin escribir el YAML de la escena.
    private static bool PrepararMarcasC1(UnityEngine.SceneManagement.Scene escena)
    {
        SequenceStage stage = null;
        var objetos = new List<Transform>();
        foreach (var raiz in escena.GetRootGameObjects())
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            { objetos.Add(t); if (stage == null) stage = t.GetComponent<SequenceStage>(); }
        if (stage == null) { OmitirCambio("SequenceStage", "marcas C1"); return false; }
        var so = new SerializedObject(stage);
        var marcas = so.FindProperty("_marks");
        if (marcas == null || !marcas.isArray) { OmitirCambio("las marcas", "montaje C1"); return false; }
        Transform Marca(string nombre)
        {
            for (int i = 0; i < marcas.arraySize; i++)
            {
                var m = marcas.GetArrayElementAtIndex(i);
                if (m.FindPropertyRelative("name").stringValue == nombre) return m.FindPropertyRelative("target").objectReferenceValue as Transform;
            }
            return null;
        }
        void Poner(string nombre, Vector3 posicion)
        {
            var t = Marca(nombre);
            if (t == null)
            {
                var go = new GameObject(nombre);
                Undo.RegisterCreatedObjectUndo(go, "Crear destino del prólogo");
                go.transform.SetParent(stage.transform);
                t = go.transform;
                marcas.InsertArrayElementAtIndex(marcas.arraySize);
                var m = marcas.GetArrayElementAtIndex(marcas.arraySize - 1);
                m.FindPropertyRelative("name").stringValue = nombre;
                m.FindPropertyRelative("target").objectReferenceValue = t;
            }
            Undo.RecordObject(t, "Colocar destino del prólogo");
            if (UnityEngine.AI.NavMesh.SamplePosition(posicion, out var suelo, 0.35f, UnityEngine.AI.NavMesh.AllAreas)) posicion = suelo.position;
            t.position = posicion;
        }
        var apertura = Marca("M_Apertura");
        var puerta = Marca("M_Casa_Archimago");
        var carreta = Marca("M_Carreta");
        var baile = Marca("M_Baile");
        if (apertura == null || puerta == null || carreta == null || baile == null)
        { OmitirCambio("las marcas base de mañana", "marcas C1"); return false; }
        Transform casa = null;
        foreach (var t in objetos)
            if (t.name.StartsWith("Casa_", System.StringComparison.Ordinal) && (casa == null || (t.position - puerta.position).sqrMagnitude < (casa.position - puerta.position).sqrMagnitude)) casa = t;
        if (casa == null) { OmitirCambio("la casa del Archimago", "recorrido desde detrás"); return false; }
        var volumen = new Bounds(casa.position, Vector3.zero);
        foreach (var r in casa.GetComponentsInChildren<Renderer>()) volumen.Encapsulate(r.bounds);
        Vector3 frente = apertura.position - volumen.center; frente.y = 0f; frente.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, frente);
        float Radio(Vector3 eje) => Mathf.Abs(eje.x) * volumen.extents.x + Mathf.Abs(eje.z) * volumen.extents.z;
        Vector3 esquina = volumen.center + lado * (Radio(lado) + 1.3f);
        Vector3 tras = esquina - frente * (Radio(frente) + 1.3f);
        esquina += frente * (Radio(frente) + 1.3f);
        tras.y = esquina.y = puerta.position.y;
        Poner("M_TrasCasa_Archimago", tras);
        Poner("M_Esquina_Archimago", esquina);
        Poner("M_Saludo_01", apertura.position + lado * 2f);
        Poner("M_Saludo_02", apertura.position - lado * 2f);
        Poner("M_Carreta_Maestro", carreta.position + lado * 1.8f);
        Poner("M_Carreta_Dueno", carreta.position - lado * 1.8f);
        Vector3 rodeo = baile.position;
        foreach (var t in objetos)
        {
            if (!t.name.StartsWith("Light03_a01", System.StringComparison.Ordinal) && !t.name.StartsWith("Trough01_a01", System.StringComparison.Ordinal)) continue;
            var bounds = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var obstacle = t.GetComponent<UnityEngine.AI.NavMeshObstacle>() ?? Undo.AddComponent<UnityEngine.AI.NavMeshObstacle>(t.gameObject);
            Undo.RecordObject(obstacle, "Recortar navegación alrededor del prop");
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = t.InverseTransformPoint(bounds.center);
            Vector3 escala = t.lossyScale;
            obstacle.size = new Vector3(bounds.size.x / Mathf.Max(Mathf.Abs(escala.x), .001f), bounds.size.y / Mathf.Max(Mathf.Abs(escala.y), .001f), bounds.size.z / Mathf.Max(Mathf.Abs(escala.z), .001f));
            obstacle.enabled = obstacle.carving = obstacle.carveOnlyStationary = true;
            EditorUtility.SetDirty(obstacle);
            if (t.name.StartsWith("Trough01_a01", System.StringComparison.Ordinal))
            {
                Vector3 direccion = baile.position - carreta.position; direccion.y = 0f; direccion.Normalize();
                Vector3 lateral = Vector3.Cross(Vector3.up, direccion);
                float radio = Mathf.Abs(lateral.x) * bounds.extents.x + Mathf.Abs(lateral.z) * bounds.extents.z;
                rodeo = bounds.center + lateral * (radio + 1.5f); rodeo.y = baile.position.y;
            }
        }
        Poner("M_Rodeo_Abrevadero", rodeo);
        for (int i = 1; i <= 10; i++)
        {
            float angulo = Mathf.Lerp(-75f, 75f, (i - 1) / 9f) * Mathf.Deg2Rad;
            Poner($"M_Baile_Corro_{i:00}", baile.position + frente * (2f + Mathf.Cos(angulo) * 4f) + lado * (Mathf.Sin(angulo) * 6f));
        }
        // El semicírculo deja libre el lado frontal de los hablantes.
        for (int i = 3; i <= 10; i++)
        {
            float angulo = Mathf.Lerp(-70f, 70f, (i - 3) / 7f) * Mathf.Deg2Rad;
            Poner($"M_Fondo_Manana_{i:00}", apertura.position + frente * (5f + Mathf.Cos(angulo) * 4f) + lado * (Mathf.Sin(angulo) * 6f));
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(stage);
        EditorSceneManager.MarkSceneDirty(escena);
        if (!EditorSceneManager.SaveScene(escena)) { OmitirCambio("el guardado de escena", "marcas C1"); return false; }
        return true;
    }

    private static void PrepararMontajeC1(List<SequenceBeat> beats)
    {
        for (int i = beats.Count - 1; i >= 0; i--)
        {
            if (beats[i] is ParallelBeat paralelo) PrepararMontajeC1(paralelo.beats);
            if (beats[i] is SerieBeat serie) PrepararMontajeC1(serie.beats);
            if (beats[i] is GestureBeat gesto && gesto.actorId != null && gesto.actorId.StartsWith("NPC_Aldeano_", System.StringComparison.Ordinal)
                && !string.IsNullOrEmpty(gesto.gesture)
                && (gesto.gesture.StartsWith("Talk", System.StringComparison.Ordinal) || gesto.gesture.StartsWith("Question", System.StringComparison.Ordinal)
                    || gesto.gesture.StartsWith("HandWave", System.StringComparison.Ordinal) || gesto.gesture.StartsWith("Greeting", System.StringComparison.Ordinal))) beats.RemoveAt(i);
            else if (beats[i] is WalkPathBeat paseo && paseo.actorId == "NPC_Aldeano_02") paseo.markNames = new List<string> { "M_Saludo_02" };
            else if (beats[i] is WalkPathBeat fondo && (fondo.actorId == "NPC_Aldeano_04" || fondo.actorId == "NPC_Aldeano_09" || fondo.actorId == "NPC_Aldeano_10"))
                fondo.markNames = new List<string> { "M_Fondo_Manana_" + fondo.actorId.Substring(fondo.actorId.Length - 2) };
        }
    }

    private static List<SequencePhase> Fases(GameObject hechizoSostenido) => new()
    {
        Fase("1 - Un dia cualquiera", "", "", new List<SequenceBeat>
        {
            new BandasDeCineBeat { mostrar = true, duracion = 0f },
            new AmbienteBeat { note = "La mañana del valle: pájaros, gallinas y charla lejana.", loopId = "AMBIENTE", eventKey = "Ambience_Prologo_Pueblo", volumen = 0.5f },
            new PostprocesoBeat { perfil = Perfil(PrologoPostprocesoSueno.Dia), transicion = 0 },
            new SetPropActiveBeat
            {
                note = "Fuera las nubes de escena del primer intento de apertura: la apertura ya las cuelga de la lente y estas se quedaban quietas en el cielo.",
                propId = "PROP_Nube_Oeste",
                active = false,
            },
            new SetPropActiveBeat
            {
                note = "",
                propId = "PROP_Nube_Este",
                active = false,
            },
            new PlaceAtMarkBeat
            {
                note = "FUERA DE ESCENA. Su SpawnPoint esta en mitad de la plaza, asi que sin esto se pasa los dos primeros minutos de pie entre los vecinos que bailan. Aparece en la fase 4, no antes.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Espera_Oscuro",
                faceTowardsMark = "",
                faceTowardsActor = "",
            },
            new PropMoveBeat
            {
                note = "La carreta empieza VOLCADA, al instante y antes del primer plano. Y APOYADA: al girarla sobre su base media carreta se metia en la tierra, asi que la altura no se pone a ojo -- se mide contra el suelo.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 0.0f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 62.0f),
                segundos = 0.0f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
                apoyarEnElSuelo = true,
            },
            new SetActionAxisBeat
            {
                note = "La camara entra por el este. Todo el decorado esta compuesto para ese lado, y ademas es el lado contrario por el que bajara el Mago Oscuro - baja de frente a la camara.",
                sideDegrees = 90.0f,
            },
            new TimeOfDayBeat
            {
                note = "Manana.",
                timeOfDay = DayNightCycle.TimeOfDay.Morning,
                immediate = true,
                waitForTransition = false,
                transitionSeconds = 2.0f,
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "M_Apertura",
                faceTowardsMark = "",
                faceTowardsActor = "",
            },
            new PlaceAtMarkBeat
            {
                note = "Liora entre la gente desde el primer plano.",
                actorId = "NPC_Liora",
                markName = "M_Plaza_Liora",
                faceTowardsMark = "",
                faceTowardsActor = "",
            },
            new ShotBeat
            {
                note = "ARRIBA, DE GOLPE (prologo17). Corte seco a veinte metros sobre el pueblo, con las nubes cerradas delante en el mismo fotograma. Antes era un viaje de 4,5 s desde donde estuviera la camara, mirando al cielo: ese era el tramo azul.",
                shotName = "",
                smooth = false,
                duration = 4.5f,
                waitForArrival = false,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 20.0f,
                    distanceScale = 1.8f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new NubesDeAperturaBeat
            {
                note = "Y SE ABREN, despacio, desde el primer fotograma y mientras la camara ya baja.",
                nube = Prefab("bca0e32ecbad95342a83b9dae7fb5a97"),
                distancia = 4.5f,
                separacionInicial = 1.6f,
                separacionFinal = 18.0f,
                escala = 9.0f,
                segundos = 5.5f,
                esperar = false,
            },
            new ParallelBeat
            {
                note = "La plaza se mueve desde el primer segundo.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "FoundSomething_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Talk03",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Question01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Cheer01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ParallelBeat
            {
                note = "La plaza PASEA, no solo gesticula.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "Se une al corro.",
                        actorId = "NPC_Aldeano_02",
                        speed = 1.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.4f,
                        markNames = new List<string> { "M_Duelo_Oscuro" },
                    },
                    new WalkPathBeat
                    {
                        note = "A la mesa del desayuno.",
                        actorId = "NPC_Aldeano_09",
                        speed = 1.1f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 1.3f,
                        markNames = new List<string> { "M_Mesa" },
                    },
                    new WalkPathBeat
                    {
                        note = "Cruza la plaza hacia el oeste.",
                        actorId = "NPC_Aldeano_04",
                        speed = 1.25f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 2.2f,
                        markNames = new List<string> { "M_Duelo_Mago", "M_Duelo_Choque" },
                    },
                    new WalkPathBeat
                    {
                        note = "Al horno.",
                        actorId = "NPC_Aldeano_10",
                        speed = 1.1f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 3.4f,
                        markNames = new List<string> { "M_Horno" },
                    },
                },
            },
            new ParallelBeat
            {
                note = "La plaza habla sola desde el primer segundo.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Cheer02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new EmotionBeat
            {
                note = "La manana es lo unico alegre de todo el prologo, y hay que verlo en la cara.",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_01",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_02",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_03",
                emotion = (NPCEmotion)15,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_04",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)15,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_07",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_08",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_09",
                emotion = (NPCEmotion)15,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_10",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new ShotBeat
            {
                note = "Y BAJA hasta el sin parar (prologo17): arranca en el mismo fotograma que las nubes, ya en marcha, y sigue bajando cuando ya se han abierto. El descenso ES el enganche con su frase.",
                shotName = "",
                smooth = true,
                duration = 8.5f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.5f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
                arrancaLanzado = true,
                entrarDesdeArriba = true,
            },
            new WaitBeat
            {
                note = "Lo justo para que la camara asiente. Habla YA.",
                seconds = 0.15f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Archimago",
                emotion = NPCEmotion.Happy,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "Saluda con energía; dos ciclos hacen visible el saludo tras la transición.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_INTRO",
                pageDuration = 3.2f,
                gesture = "HandWave01",
                gestureRepeats = 2,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "La plaza, quieta, con la carreta volcada y el vecino al lado. El Archimago entra andando en el cuadro: la camara no le persigue, le espera.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "NPC_Archimago",
                    heightBias = 1.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "Laugh01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Question02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "HeadShake02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Cheer02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new MoveToBeat
            {
                note = "Cruza la plaza hasta la carreta.",
                actorId = "NPC_Archimago",
                towardsActorId = "",
                markName = "M_Carreta",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.6f,
                timeout = 14.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new FaceBeat
            {
                note = "El vecino le ve llegar.",
                actorId = "NPC_Aldeano_06",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "Le llama.",
                actorId = "NPC_Aldeano_06",
                gesture = "HandWave01",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "Los dos, con la carreta volcada en cuadro.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_06",
                    heightBias = 0.7f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "La cara de quien habla permite leer su expresión y su boca.",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.8f,
                    distanceScale = 0.9f,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Aldeano_06",
                emotion = NPCEmotion.Sad,
                duracion = 0.0f,
            },
            new GestureBeat
            {
                note = "El gesto abatido se lanza aparte para evitar variaciones de hablar durante la queja.",
                actorId = "NPC_Aldeano_06",
                gesture = "Cry01",
                repeats = 1,
                holdSeconds = 0.0f,
                returnToNormalAfter = false,
            },
            new SayBeat
            {
                note = "El vecino expresa abatimiento por la carreta volcada.",
                actorId = "NPC_Aldeano_06",
                markName = "",
                textKey = "PROLOGO_FAVOR_CARRETA",
                pageDuration = 3.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Vecino",
                colorDelNombre = ColorDelHablante("Vecino"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Aldeano_06",
                emotion = NPCEmotion.Neutral,
                duracion = 0.0f,
            },
            new ShotBeat
            {
                note = "Recupera el plano de la carreta para mostrar cómo la endereza.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_06",
                    heightBias = 0.7f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new FaceBeat
            {
                note = "Mira a la carreta ANTES de levantar la mano. Sin esto lanzaba el hechizo de espaldas a la camara, mirando a donde hubiera acabado al llegar.",
                actorId = "NPC_Archimago",
                targetActorId = "PROP_Carreta",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "Extiende la mano.",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_MagiaLevitar",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "La magia nace en su mano. Es el mismo aura que ve el jugador cuando levita algo.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.1f, 0.0f),
                lifetime = 2.4f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShotBeat
            {
                note = "EL PLANO QUE FALTABA - la carreta, y solo la carreta. Camara QUIETA (nada de 'live'): si la camara la acompana mientras sube, no se ve que suba.",
                shotName = "",
                smooth = false,
                duration = 3.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "PROP_Carreta",
                    secondaryId = "NPC_Archimago",
                    heightBias = 1.0f,
                    distanceScale = 1.8f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new PropMoveBeat
            {
                note = "Y volcada OTRA VEZ, al instante, con la camara ya encima: asi lo que se ve es enderezarse. Antes, si algo la habia tocado antes, la camara la pillaba ya derecha y solo se veia un giro raro.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 0.0f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 62.0f),
                segundos = 0.0f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
                apoyarEnElSuelo = true,
            },
            new VfxBeat
            {
                note = "El aura prende en la carreta.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "PROP_Carreta",
                markName = "",
                offset = new Vector3(0.0f, 0.5f, 0.0f),
                lifetime = 2.6f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new PropMoveBeat
            {
                note = "SE LEVANTA. Sube metro y medio y se endereza a la vez, en 1,4 s -- despacio, que pese. El giro es ABSOLUTO: acaba derecha, no 62 grados menos de como estuviera.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 1.5f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 1.4f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_CarretaCae",
                clip = null,
                atActorId = "PROP_Carreta",
                markName = "",
                volume = 1.0f,
            },
            new WaitBeat
            {
                note = "Y se queda ahi flotando un segundo. Este segundo es la diferencia entre un truco de magia y un destello.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ParallelBeat
            {
                note = "Y los de mas alla siguen a lo suyo.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "Y otro mas. La carreta flotando es el truco entero de la escena.",
                seconds = 0.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El vecino Y la carreta (prologo18): la carreta baja al suelo en este plano, y sin ella en cuadro no se veia bajar. El vecino se gira hacia ella.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "PROP_Carreta",
                    heightBias = 0.6f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new PropMoveBeat
            {
                note = "Y la posa EN EL SUELO. Volver a «donde estaba» no bastaba: donde estaba es la pose con la que se guardo la escena, y esa flota un metro.",
                propId = "PROP_Carreta",
                deltaPosicion = new Vector3(0.0f, 0.0f, 0.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 0.9f,
                suavizar = true,
                esperar = true,
                desdeDondeEstaba = true,
                apoyarEnElSuelo = true,
            },
            new ShotBeat
            {
                note = "La cara de quien habla permite leer su expresión y su boca.",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Aldeano_06",
                emotion = NPCEmotion.Grateful,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "Agradece con una sonrisa y un asentimiento discreto.",
                actorId = "NPC_Aldeano_06",
                markName = "",
                textKey = "PROLOGO_CARRETA_GRACIAS",
                pageDuration = 2.2f,
                gesture = "HeadNod01",
                gestureRepeats = 1,
                speakerNameKey = "Vecino",
                colorDelNombre = ColorDelHablante("Vecino"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Aldeano_06",
                emotion = NPCEmotion.Happy,
                duracion = 0.0f,
            },
            new ShotBeat
            {
                note = "Recupera el plano de conjunto para la acción que sigue.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_06",
                    secondaryId = "PROP_Carreta",
                    heightBias = 0.6f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("dcd90c4976197424b9958a7c54b6bb8c"),
                atActorId = "PROP_Carreta",
                markName = "",
                offset = new Vector3(0.0f, 0.4f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_CarretaCae",
                clip = null,
                atActorId = "PROP_Carreta",
                markName = "",
                volume = 1.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)15,
                duracion = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                gesture = "HeadNod01",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El corro, de cerca (prologo18): antes era un general con el Archimago, que aun venia de lejos, y la camara se iba a la otra punta. El entra en cuadro al llegar.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_03",
                    secondaryId = "NPC_Aldeano_01",
                    heightBias = 0.3f,
                    distanceScale = 0.7f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk03",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "HandWave02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Laugh01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Question01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new MoveToBeat
            {
                note = "Hacia el corro que esta de celebracion.",
                actorId = "NPC_Archimago",
                towardsActorId = "",
                markName = "M_Baile",
                stopDistance = 0.4f,
                approachAngle = 0.0f,
                speedOverride = 1.6f,
                timeout = 14.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new FaceBeat
            {
                note = "Toca palmas MIRANDO al que baila (prologo18).",
                actorId = "NPC_Aldeano_08",
                targetActorId = "NPC_Aldeano_03",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new FaceBeat
            {
                note = "Y el vecino de la carreta igual.",
                actorId = "NPC_Aldeano_06",
                targetActorId = "NPC_Aldeano_03",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new ParallelBeat
            {
                note = "El corro ENTERO. Antes bailaban tres y el resto se quedaba de pie en medio del corro mirando al frente, que es lo que canta.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Dance_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 1.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Dance_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Dance_NoWeapon",
                        repeats = 3,
                        holdSeconds = 1.4f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Cheer02",
                        repeats = 3,
                        holdSeconds = 1.4f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HandClap01",
                        repeats = 3,
                        holdSeconds = 1.4f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "El se para a mirar.",
                seconds = 0.9f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new GestureBeat
            {
                note = "Se une un momento - y baila el, que es lo que hace que la frase siguiente tenga gracia.",
                actorId = "NPC_Archimago",
                gesture = "Dance_NoWeapon",
                repeats = 1,
                holdSeconds = 1.6f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El, en el corro. Plano medio de verdad, no un general.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_03",
                    heightBias = 0.0f,
                    distanceScale = 0.95f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Archimago",
                emotion = NPCEmotion.Happy,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "Anima a los bailarines con alegría.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_BAILE",
                pageDuration = 2.8f,
                gesture = "Cheer01",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Aldeano_01",
                gesture = "Cheer02",
                repeats = 1,
                holdSeconds = 0.8f,
                returnToNormalAfter = false,
            },
            new FaceBeat
            {
                note = "Se lo dice A EL (prologo18).",
                actorId = "NPC_Aldeano_01",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new ShotBeat
            {
                note = "La cara de quien habla permite leer su expresión y su boca.",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Aldeano_01",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Aldeano_01",
                emotion = NPCEmotion.Happy,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "Contesta con entusiasmo al ánimo del maestro.",
                actorId = "NPC_Aldeano_01",
                markName = "",
                textKey = "PROLOGO_BAILE_RESPUESTA",
                pageDuration = 2.2f,
                gesture = "Cheer02",
                gestureRepeats = 1,
                speakerNameKey = "Vecino",
                colorDelNombre = ColorDelHablante("Vecino"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },

            new ShotBeat
            {
                note = "Recupera el plano de conjunto para la acción que sigue.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_03",
                    heightBias = 0.0f,
                    distanceScale = 0.95f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ParallelBeat
            {
                note = "Los de la plaza, mientras tanto: se rien y aplauden. Nadie HABLA sin bocadillo (prologo18).",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ParallelBeat
            {
                note = "Y los que se fueron, vuelven.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        speed = 1.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.5f,
                        markNames = new List<string> { "M_Aldeano_04" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        speed = 1.1f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 1.8f,
                        markNames = new List<string> { "M_Aldeano_10" },
                    },
                },
            },
            new ParallelBeat
            {
                note = "",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Cheer01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "Talk01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Talk02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "FoundSomething_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new TimeOfDayBeat
            {
                note = "El sol sube mientras cruza la plaza. Doce segundos: la luz cambia sola, sin que nadie se pare a mirarla.",
                timeOfDay = DayNightCycle.TimeOfDay.AfterNoon,
                immediate = false,
                waitForTransition = false,
                transitionSeconds = 12.0f,
                esLaHoraDeVolver = false,
            },
            new ShotBeat
            {
                note = "AEREA del camino: el y la vecina que le espera al pie del campanario. Con el globo dentro, la camara se iba tan lejos que era el pueblo entero (prologo20).",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 6.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "Mientras cruza, a los 2,2 s, corte a la vecina: llega a su plano en vez de cruzar el pueblo entero en uno (prologo20).",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new MoveToBeat
                    {
                        note = "Hasta el campanario, a paso vivo y por debajo de una aerea: a ras de suelo este tramo eran quince segundos de fachadas.",
                        actorId = "NPC_Archimago",
                        towardsActorId = "",
                        markName = "M_Globo",
                        stopDistance = 0.4f,
                        approachAngle = 0.0f,
                        speedOverride = 3.2f,
                        timeout = 14.0f,
                        faceEachOtherOnArrival = false,
                        settleOnArrival = 0.0f,
                    },
                    new SerieBeat
                    {
                        note = "",
                        beats = new List<SequenceBeat>
                        {
                            new WaitBeat
                            {
                                note = "",
                                seconds = 2.2f,
                                unscaled = true,
                            },
                            new ShotBeat
                            {
                                note = "La vecina esperandole al pie del campanario; el entra en cuadro.",
                                shotName = "",
                                smooth = false,
                                duration = 1.4f,
                                waitForArrival = true,
                                live = false,
                                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.4f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
                            },
                        },
                    },
                },
            },
            new FaceBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new GestureBeat
            {
                note = "Senala hacia arriba.",
                actorId = "NPC_Aldeano_05",
                gesture = "Question01",
                repeats = 1,
                holdSeconds = 0.7f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "El vecino senalando hacia arriba. Es QUIEN HABLA: sin este plano la frase sale de un tejado, que es lo que se ve en la decima grabacion.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "PROP_Globo",
                    heightBias = -0.5f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                markName = "",
                textKey = "PROLOGO_FAVOR_GLOBO",
                pageDuration = 2.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Nina",
                colorDelNombre = ColorDelHablante("Nina"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "El globo, arriba del campanario, en contrapicado cerrado. Antes era un plano GENERAL con el Archimago dentro: para meter a los dos la camara se iba atras y lo que llenaba el cuadro eran los tejados de en medio. Ahora es un primer plano DEL GLOBO: la camara sube con el, a la altura del campanario, y lo que queda detras es cielo. El secundario no sale -- solo da el angulo.",
                shotName = "",
                smooth = false,
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "PROP_Globo",
                    secondaryId = "NPC_Archimago",
                    heightBias = -1.4f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "El, lanzando, con la vecina: general BAJO. Era un plano medio con el globo dentro y la camara acababa detras del tejado de enfrente: «se ve media casa» (prologo20).",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = -0.4f,
                    distanceScale = 0.9f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.6f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_MagiaLevitar",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.1f, 0.0f),
                lifetime = 1.8f,
                earlyDespawn = 0.0f,
            },
            new SayBeat
            {
                note = "'Con cuidado...' se dice AL HACER el hechizo, no despues de que el globo ya se haya ido. Es una advertencia, no un comentario.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_GLOBO_OK",
                pageDuration = 2.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new VfxBeat
            {
                note = "El aura prende tambien en el globo, igual que en la carreta - la magia del Archimago siempre se ve igual.",
                vfxPrefab = Prefab("895c6d094b6b213418cddcfb520298e9"),
                atActorId = "PROP_Globo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("dcd90c4976197424b9958a7c54b6bb8c"),
                atActorId = "PROP_Globo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 1.8f,
                earlyDespawn = 0.0f,
            },
            new PropMoveBeat
            {
                note = "El globo se suelta y SUBE. Sin esperar - sigue subiendo de fondo mientras la escena continua, y ahora le da tiempo a perderse de vista antes de que nadie lo apague.",
                propId = "PROP_Globo",
                deltaPosicion = new Vector3(2.0f, 22.0f, 1.0f),
                deltaRotacion = new Vector3(0.0f, 0.0f, 0.0f),
                segundos = 8.0f,
                suavizar = true,
                esperar = false,
            },
            new WaitBeat
            {
                note = "Que se le vea empezar a subir.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El globo subiendo, con la camara POR ENCIMA de los tejados y siguiendole: se va contra el cielo. Desde el suelo lo tapaba el alero de enfrente: «cuando sube el globo no se ve, le tapa algo» (prologo21).",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "PROP_Globo",
                    secondaryId = "",
                    heightBias = 4.0f,
                    distanceScale = 1.8f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "Y que se le vea IRSE. Este silencio mirando al cielo es el ultimo momento tonto del prologo.",
                seconds = 2.6f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                gesture = "Cheer01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new SetFlagBeat
            {
                note = "El globo se libera bien. Cambiar value a 0 para quedarse con la version en la que revienta.",
                flag = "manana_globo_ok",
                value = true,
            },
        }),
        Fase("1b - El globo se libera bien", "manana_globo_ok", "", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "La cara del vecino mirando subir el globo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "PROP_Globo",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)13,
                duracion = 0.0f,
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Y el corro entero, celebrandolo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_03",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 0.5f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "El corro celebra a la vez que los vecinos hablan por turnos; espera a que terminen.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new SerieBeat
                    {
                        note = "Los vecinos hablan uno tras otro para que cada bocadillo se pueda leer.",
                        beats = new List<SequenceBeat>
                        {
                            new ShotBeat
                            {
                                note = "La cara de quien habla permite leer su expresión y su boca.",
                                smooth = false,
                                duration = 0.8f,
                                waitForArrival = true,
                                framing = new ShotFraming
                                {
                                    type = ShotType.Medium,
                                    subjectId = "NPC_Aldeano_05",
                                    secondaryId = "NPC_Archimago",
                                    heightBias = 0.0f,
                                    distanceScale = 1.0f,
                                    encara = true,
                                },
                            },
                            new SayBeat
                            {
                                note = "",
                                actorId = "NPC_Aldeano_05",
                                markName = "",
                                textKey = "PROLOGO_GLOBO_VECINO_1",
                                pageDuration = 1.6f,
                                gesture = "",
                                gestureRepeats = 1,
                                speakerNameKey = "Vecina",
                                colorDelNombre = ColorDelHablante("Vecina"),
                                playGestures = true,
                                overrideBubbleOffset = false,
                                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                            },
                            new ShotBeat
                            {
                                note = "La cara de quien habla permite leer su expresión y su boca.",
                                smooth = false,
                                duration = 0.8f,
                                waitForArrival = true,
                                framing = new ShotFraming
                                {
                                    type = ShotType.Medium,
                                    subjectId = "NPC_Aldeano_03",
                                    secondaryId = "NPC_Archimago",
                                    heightBias = 0.0f,
                                    distanceScale = 1.0f,
                                    encara = true,
                                },
                            },
                            new SayBeat
                            {
                                note = "",
                                actorId = "NPC_Aldeano_03",
                                markName = "",
                                textKey = "PROLOGO_GLOBO_VECINO_2",
                                pageDuration = 1.6f,
                                gesture = "",
                                gestureRepeats = 1,
                                speakerNameKey = "Vecino",
                                colorDelNombre = ColorDelHablante("Vecino"),
                                playGestures = true,
                                overrideBubbleOffset = false,
                                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                            },
                            new ShotBeat
                            {
                                note = "La cara de quien habla permite leer su expresión y su boca.",
                                smooth = false,
                                duration = 0.8f,
                                waitForArrival = true,
                                framing = new ShotFraming
                                {
                                    type = ShotType.Medium,
                                    subjectId = "NPC_Aldeano_08",
                                    secondaryId = "NPC_Archimago",
                                    heightBias = 0.0f,
                                    distanceScale = 1.0f,
                                    encara = true,
                                },
                            },
                            new SayBeat
                            {
                                note = "",
                                actorId = "NPC_Aldeano_08",
                                markName = "",
                                textKey = "PROLOGO_GLOBO_VECINO_3",
                                pageDuration = 1.6f,
                                gesture = "",
                                gestureRepeats = 1,
                                speakerNameKey = "Vecino",
                                colorDelNombre = ColorDelHablante("Vecino"),
                                playGestures = true,
                                overrideBubbleOffset = false,
                                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                            },

                            new ShotBeat
                            {
                                note = "Recupera el plano de conjunto para la acción que sigue.",
                                shotName = "",
                                smooth = false,
                                duration = 1.4f,
                                waitForArrival = true,
                                live = false,
                                framing = new ShotFraming
                                {
                                    type = ShotType.Wide,
                                    subjectId = "NPC_Aldeano_03",
                                    secondaryId = "NPC_Aldeano_05",
                                    heightBias = 0.5f,
                                    distanceScale = 1.2f,
                                    fovOverride = 0.0f,
                                    crossTheLine = false,
                                    headroom = true,
                                    encara = true,
                                },
                            },
                        },
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Cheer01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "HandClap01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Cheer02",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Laugh01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new WaitBeat
            {
                note = "Que se oiga el corro.",
                seconds = 0.8f,
                unscaled = true,
            },
            new SetPropActiveBeat
            {
                note = "Y ahora si se apaga, con el ya fuera de plano.",
                propId = "PROP_Globo",
                active = false,
            },
        }),
        Fase("1c - El globo revienta", "", "manana_globo_ok", new List<SequenceBeat>
        {
            new ShotBeat
            {
                note = "La cara del vecino cuando revienta.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SfxBeat { note = "El estallido del globo.", eventKey = "SFX_Prologo_Globo", clip = null, atActorId = "", markName = "", volume = 0.8f },
            new SetPropActiveBeat
            {
                note = "El globo revienta.",
                propId = "PROP_Globo",
                active = false,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)2,
                duracion = 0.0f,
            },
            new ShotBeat
            {
                note = "Y el, disculpandose.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La expresión acompaña el tono de la frase.",
                actorId = "NPC_Archimago",
                emotion = NPCEmotion.Happy,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "La disculpa es torpe y alegre: el globo se escapa por accidente.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_MANANA_GLOBO_ROTO",
                pageDuration = 2.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.2f,
                unscaled = true,
            },
        }),
        Fase("2 - El rio", "", "", new List<SequenceBeat>
        {
            new AmbienteBeat { note = "El pueblo se queda atrás.", loopId = "AMBIENTE", sonar = false, fundido = 3f },
            new AmbienteBeat { note = "El agua del río y los pájaros de la orilla.", loopId = "AMBIENTE_RIO", eventKey = "Ambience_Prologo_Rio", volumen = 0.55f },
            new TimeOfDayBeat
            {
                note = "Y baja el sol mientras ellos bajan al rio.",
                timeOfDay = DayNightCycle.TimeOfDay.Sunset,
                immediate = false,
                waitForTransition = false,
                transitionSeconds = 10.0f,
                esLaHoraDeVolver = false,
            },
            new PlaceAtMarkBeat
            {
                note = "Elipsis: en el corte ya estan cerca de la orilla. Nueve segundos de aereo mientras cruzaban el pueblo eran «demasiado largo» (prologo20).",
                actorId = "NPC_Archimago",
                markName = "M_Rio_Mago",
                faceTowardsMark = "M_Rio_Fin_Mago",
                faceTowardsActor = "",
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "M_Rio_Liora",
                faceTowardsMark = "M_Rio_Fin_Liora",
                faceTowardsActor = "",
            },
            new ShotBeat
            {
                note = "Bajando hacia el rio: aereo FIJO y abierto (prologo19). El vivo les perseguia y cada tejado o arbol que se cruzaba lo movia: «hace saltos todavia».",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 6.0f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new ParallelBeat
            {
                note = "Bajan juntos y se plantan.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "Los ultimos seis metros, paseando (prologo20).",
                        actorId = "NPC_Archimago",
                        speed = 1.9f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Fin_Mago" },
                    },
                    new WalkPathBeat
                    {
                        note = "Los ultimos seis metros, paseando (prologo20).",
                        actorId = "NPC_Liora",
                        speed = 1.9f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Fin_Liora" },
                    },
                },
            },
            new SetActionAxisBeat
            {
                note = "La camara al oeste: el agua y el puente detras de ellos.",
                sideDegrees = 270.0f,
            },
            new SolDeFondoBeat
            {
                note = "El sol EN CUADRO, por encima de la cresta de detras, y poniendose mientras hablan (prologo20): a 7° en el mundo lo tapaban las montanas.",
                colocar = true,
                ladoDeLaCamara = 270.0f,
                elevacion = 7.0f,
                segundos = 2.0f,
                enCuadro = true,
                posicionX = 0.72f,
                alturaDeSalida = 0.88f,
                alturaDePuesta = 0.42f,
                puesta = 19.0f,
            },
            new ParallelBeat
            {
                note = "El pueblo sigue vivo detras, aunque no salga.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "Talk01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Talk02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Talk03",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadNod01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Laugh01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Question01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HandClap01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "InteractWithPeople_NoWeapon",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Cheer02",
                        repeats = 2,
                        holdSeconds = 2.2f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new FaceBeat
            {
                note = "Se encaran.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_Liora",
                markName = "",
                lookAway = false,
                mutual = true,
                turnDuration = 0.6f,
            },
            new ShotBeat
            {
                note = "Y ya en la orilla, el general: los dos, el agua y el puente. Este plano iba ANTES de que anduvieran, asi que encuadraba la plaza y ellos se iban de cuadro -- cuarenta segundos de casas.",
                shotName = "",
                smooth = false,
                duration = 3.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.0f,
                    distanceScale = 1.35f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "Y el agua, un momento, antes de que nadie hable.",
                seconds = 1.6f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos, quietos, encarados, con el rio detras.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new ShotBeat
            {
                note = "La cara de quien habla permite leer su expresión y su boca.",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_PLAZA_LIORA_1",
                pageDuration = 3.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Primer plano de quien habla, manteniendo el eje de la conversación.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)1,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLAZA_ARCHIMAGO",
                pageDuration = 3.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Primer plano de quien habla, manteniendo el eje de la conversación.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_PLAZA_LIORA_2",
                pageDuration = 3.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Liora",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "Los dos riendose con el rio detras. Es el ultimo momento tranquilo del prologo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.4f,
                    distanceScale = 1.45f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "Justo lo que dura la risa. El trueno entra encima, no despues: la pausa mataba el corte.",
                seconds = 0.35f,
                unscaled = true,
            },
        }),
        Fase("3 - Algo cambia en el cielo", "", "", new List<SequenceBeat>
        {
            new AmbienteBeat { note = "El río se apaga: el silencio anuncia que algo cambia.", loopId = "AMBIENTE_RIO", sonar = false, fundido = 2.5f },
            new PostprocesoBeat { perfil = Perfil(PrologoPostprocesoSueno.Amenaza), transicion = 6, esperar = false },
            new WaitBeat
            {
                note = "El silencio, sostenido. Aqui todavia no se ve nada.",
                seconds = 1.8f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "Preocupada desde que el cielo cambia: estaba poniendo cara de felicidad mientras caia el rayo.",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)9,
                duracion = 0.0f,
            },
            new SfxBeat { note = "Una ráfaga de viento cruza el valle.", eventKey = "SFX_Prologo_Viento", clip = null, atActorId = "", markName = "", volume = 0.7f },
            new WeatherBeat
            {
                note = "El viento agita la plaza.",
                fenomeno = WeatherBeat.Fenomeno.Viento,
                encender = true,
                immediate = false,
            },
            new WeatherBeat
            {
                note = "Las nubes cubren el sol. Sin transicion - la catastrofe no pide permiso.",
                fenomeno = WeatherBeat.Fenomeno.Tormenta,
                encender = true,
                immediate = true,
            },
            new TimeOfDayBeat
            {
                note = "Noche cerrada. Ya lo era antes del trueno; esto solo remata lo que la tormenta tapa.",
                timeOfDay = DayNightCycle.TimeOfDay.Night,
                immediate = false,
                waitForTransition = false,
                transitionSeconds = 3.0f,
            },
            new SolDeFondoBeat
            {
                note = "Y el sol se devuelve al ciclo DESPUES de pedir la noche, en ocho segundos: la luz se funde con la transicion y el disco ya lo tapan las nubes (prologo20: «sale el sol como si amaneciera»).",
                colocar = false,
                ladoDeLaCamara = 0.0f,
                elevacion = 0.0f,
                segundos = 8.0f,
            },
            new ShotBeat
            {
                note = "La gente de la plaza, no la montana. Antes esto se iba tan atras que el cuadro era una pared de roca gris y los vecinos no se veian.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_01",
                    secondaryId = "NPC_Aldeano_05",
                    heightBias = 1.2f,
                    distanceScale = 0.85f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new RayoBeat { note = "Dos rayos al fondo del general del valle antes de que aparezca él (Raúl). Cada uno con su trueno.", cantidad = 2, intervalo = 1.1f, alFondoDelPlano = true, conTrueno = true, volumen = 1f, destello = true },
            new ScreenFlashBeat
            {
                note = "Solo el parpadeo de la luz. El rayo no se ve: se oye.",
                color = new Color(1f, 1f, 1f, 0.5f),
                duration = 0.1f,
            },
            new ScreenFlashBeat
            {
                note = "Y el segundo parpadeo, mas flojo.",
                color = new Color(1f, 1f, 1f, 0.28f),
                duration = 0.08f,
            },
            new MusicBeat
            {
                note = "La musica de la manana se apaga con el trueno. Desde aqui hasta que aparece el, silencio: es lo que da tension.",
                musicId = "",
                fadeOut = 3.0f,
            },
            new SfxBeat
            {
                note = "Trueno seco.",
                eventKey = "Prologo_Trueno",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.5f,
                duration = 0.8f,
                waitForEnd = false,
            },
            new ParallelBeat
            {
                note = "Todo el pueblo se gira hacia la montana a la vez. Escalonado un poco (cada uno tarda algo distinto) para que no parezcan doce munecos con el mismo resorte.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.35f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Liora",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.45f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.35f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.45f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.5f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.55f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.6f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.65f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.7f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.75f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        targetActorId = "",
                        markName = "M_Cresta",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.8f,
                    },
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)9,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_01",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_02",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_03",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_04",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_05",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_06",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_07",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_08",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_09",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Aldeano_10",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new ShotBeat
            {
                note = "La cara del Archimago, todavia en la orilla, mirando arriba. Detras de el, el agua: es la ultima vez que ese fondo esta tranquilo.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new WaitBeat
            {
                note = "El silencio despues del trueno. Este es el beat mas importante de la fase.",
                seconds = 1.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos vuelven corriendo al pueblo. Plano FIJO y alto: se ve el camino entero. El vivo les perseguia entre casas y saltaba de angulo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 8.0f,
                    distanceScale = 1.7f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "Vuelven corriendo. No esperan a saber que es.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        speed = 4.0f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Rio_Camino", "M_Apertura" },
                    },
                    new WalkPathBeat
                    {
                        note = "Ella sale medio segundo despues y va mas despacio: detras de el, sin empujarse en el punto del camino.",
                        actorId = "NPC_Liora",
                        speed = 3.6f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.6f,
                        markNames = new List<string> { "M_Rio_Camino", "M_Plaza_Liora" },
                    },
                    new SerieBeat
                    {
                        note = "Al segundo y medio, a la plaza: si no, la camara se quedaba mirando el puente vacio (prologo19).",
                        beats = new List<SequenceBeat>
                        {
                            new WaitBeat
                            {
                                note = "",
                                seconds = 1.5f,
                                unscaled = true,
                            },
                            new ShotBeat
                            {
                                note = "La plaza, a donde llegan corriendo.",
                                shotName = "",
                                smooth = false,
                                duration = 1.4f,
                                waitForArrival = true,
                                live = false,
                                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Aldeano_05",
                    secondaryId = "NPC_Archimago",
                    heightBias = 1.5f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
                            },
                        },
                    },
                },
            },
            new FaceBeat
            {
                note = "Y se vuelve otra vez hacia la montana.",
                actorId = "NPC_Archimago",
                targetActorId = "",
                markName = "M_Cresta",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
        }),
        Fase("4 - La llegada", "", "", new List<SequenceBeat>
        {
            new PlaceAtMarkBeat
            {
                note = "Y AHORA aparece, en el sitio al que ya esta mirando todo el pueblo. El orden importa: primero el golpe y el susto, despues la figura.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Cresta",
                faceTowardsMark = "M_Apertura",
                faceTowardsActor = "",
            },
            new EmotionBeat
            {
                note = "Y con miedo en cuanto el se planta en la plaza.",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new SfxBeat
            {
                note = "EL GOLPE. Es esto lo que corta la musica -- no un fundido, un impacto.",
                eventKey = "Prologo_Golpe",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "Y se nota en el mando.",
                intensity = 0.55f,
                duration = 0.6f,
                waitForEnd = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_PresenciaOscura",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new MusicBeat
            {
                note = "Su tema entra con el, encima del golpe. Viene de silencio: la manana se apago con el trueno.",
                musicId = "MAGOOSCURO_REVEAL",
                fadeOut = 0.0f,
            },
            new FaceBeat
            {
                note = "Mira al valle desde el primer fotograma: si no, su entrada es la espalda de alguien parado en una loma.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.0f,
            },
            new ShotBeat
            {
                note = "El, en la cresta, desde abajo (prologo20). Hasta ahora nadie le encuadraba: el plano era el de la plaza, y a ochenta metros la niebla de la tormenta se lo comia.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -0.6f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.8f,
                unscaled = true,
            },
            new FaceBeat
            {
                note = "",
                actorId = "NPC_Liora",
                targetActorId = "",
                markName = "M_Cresta",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new ShotBeat
            {
                note = "Liora, con miedo, mirandole (prologo20). El corte que esconde que el baja de la cresta a la ladera.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Liora",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new PlaceAtMarkBeat
            {
                note = "Otro corte - ya esta al pie de la montana.",
                actorId = "NPC_MagoOscuro",
                markName = "M_Ladera_02",
                faceTowardsMark = "M_Apertura",
                faceTowardsActor = "",
            },
            new AmbienteBeat { note = "Tormenta, viento y un zumbido oscuro bajo la lluvia.", loopId = "TORMENTA", eventKey = "Ambience_Prologo_Tormenta", volumen = 0.5f },
            new WeatherBeat
            {
                note = "Y rompe a llover mientras baja. Con transicion: la lluvia arrecia con el, no aparece de golpe.",
                fenomeno = WeatherBeat.Fenomeno.Lluvia,
                encender = true,
                immediate = false,
            },
            new ParallelBeat
            {
                note = "Echa a andar y, un instante despues, el corte: cuando le vemos ya viene andando (prologo19).",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new WalkPathBeat
                    {
                        note = "Baja los ultimos metros andando, de frente a la camara. A 1,5 m/s eran trece segundos de figura pequena en una ladera marron.",
                        actorId = "NPC_MagoOscuro",
                        speed = 2.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        markNames = new List<string> { "M_Entrada_Villa" },
                    },
                    new SerieBeat
                    {
                        note = "",
                        beats = new List<SequenceBeat>
                        {
                            new WaitBeat
                            {
                                note = "",
                                seconds = 0.35f,
                                unscaled = true,
                            },
                            new ShotBeat
                            {
                                note = "Le SIGUE mientras baja, DE FRENTE: plano medio vivo sin secundario (mira hacia donde anda). Era un Tracking, que puede rodar hasta la nuca: salia de espaldas (prologo20). Sin hueco para bocadillo (aqui no habla), mas abierto y mas bajo: se salia por abajo del cuadro (prologo21).",
                                shotName = "",
                                smooth = false,
                                duration = 1.6f,
                                waitForArrival = true,
                                live = true,
                                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -0.3f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = false,
                },
                            },
                        },
                    },
                },
            },
            new FaceBeat
            {
                note = "El Archimago levanta la vista.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new FaceBeat
            {
                note = "Liora tambien.",
                actorId = "NPC_Liora",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.5f,
            },
            new FaceBeat
            {
                note = "Se queda mirando al pueblo, que es hacia donde venia. Sin esto acaba de perfil.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.5f,
            },
            new ShotBeat
            {
                note = "Su cara, de cerca y desde abajo. Primera vez en toda la partida que se le ve.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.9f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "Vineta 7 - desde detras de su espalda, el Archimago y el pueblo al fondo.",
                shotName = "",
                smooth = false,
                duration = 2.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.OverTheShoulder,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
        }),
        Fase("5 - La orden de evacuar", "", "", new List<SequenceBeat>
        {
            new EmotionBeat
            {
                note = "Sigue con miedo mientras saca a la gente.",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new TimeOfDayBeat
            {
                note = "De noche y ardiendo: el rojo del valle lo pone el incendio, no el atardecer.",
                timeOfDay = DayNightCycle.TimeOfDay.Night,
                immediate = false,
                waitForTransition = false,
                transitionSeconds = 2.0f,
            },
            new AmbienteBeat { note = "El pueblo entra en pánico.", loopId = "PANICO", eventKey = "Ambience_Prologo_Panico", volumen = 0.6f },
            new SetPropActiveBeat
            {
                note = "Una descarga golpea una casa y el valle empieza a arder.",
                propId = "PROP_Incendio",
                active = true,
            },
            new ShotBeat
            {
                note = "La plaza entera en el momento en que estalla. Hace falta verla LLENA para que vaciarse signifique algo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 2.2f,
                    distanceScale = 2.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_Trueno",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ScreenFlashBeat
            {
                note = "Destello del rayo, frío y breve. Un fotograma entero naranja se leía como un fallo (Prologo4, 3:14).",
                color = new Color(0.85f, 0.9f, 1f, 0.45f),
                duration = 0.12f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.6f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new SfxBeat { note = "Un grito en la plaza.", eventKey = "SFX_Prologo_Grito", clip = null, atActorId = "", markName = "", volume = 0.7f },
            new ParallelBeat
            {
                note = "El susto, los diez a la vez y sin esperar a nadie.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        emotion = (NPCEmotion)5,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        emotion = (NPCEmotion)9,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        emotion = (NPCEmotion)5,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        emotion = (NPCEmotion)9,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        emotion = (NPCEmotion)5,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        emotion = (NPCEmotion)9,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        emotion = (NPCEmotion)5,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        emotion = (NPCEmotion)9,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        emotion = (NPCEmotion)5,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        emotion = (NPCEmotion)9,
                        duracion = 0.0f,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ParallelBeat
            {
                note = "Se dispersan corriendo. No es todavia la evacuacion: es el panico, que es desordenado a proposito -- cada uno hacia un sitio distinto.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_01" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_02" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_03" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_04" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_05" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_06" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_07" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_08" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_09" },
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        speed = 3.4f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_10" },
                    },
                },
            },
            new WaitBeat
            {
                note = "Un segundo de gente corriendo antes de que nadie hable.",
                seconds = 1.2f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Ellos dos, en medio de la plaza que se vacia.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)5,
                duracion = 0.0f,
            },
            new ParallelBeat
            {
                note = "LA PLAZA NO ESPERA QUIETA. Mientras ellos se despiden: uno recoge lo suyo y vuelve a entrar, otro se asoma al puente y se vuelve, otra llama a los que faltan y el cuarto no quita ojo al cielo. Ninguno se va: esperan la orden.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "Para que el Parallel no retenga la escena.",
                        seconds = 0.1f,
                        unscaled = true,
                    },
                    new WalkPathBeat
                    {
                        note = "Se asoma al puente y se vuelve a por los suyos.",
                        actorId = "NPC_Aldeano_07",
                        speed = 2.6f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Puente_Ent", "M_Huida_07" },
                    },
                    new WalkPathBeat
                    {
                        note = "Entra a por lo que puede cargar y sale con ello.",
                        actorId = "NPC_Aldeano_08",
                        speed = 2.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.8f,
                        markNames = new List<string> { "M_Horno", "M_Huida_08" },
                    },
                    new WalkPathBeat
                    {
                        note = "Recoge lo de la mesa.",
                        actorId = "NPC_Aldeano_09",
                        speed = 2.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 1.6f,
                        markNames = new List<string> { "M_Mesa", "M_Huida_09" },
                    },
                    new GestureBeat
                    {
                        note = "Mira al cielo, que es de donde vino.",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Question01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new EmotionBeat
                    {
                        note = "Asustado.",
                        actorId = "NPC_Aldeano_10",
                        emotion = (NPCEmotion)5,
                        duracion = 0.0f,
                    },
                },
            },
            new ShotBeat
            {
                note = "La cara de quien habla permite leer su expresión y su boca.",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA_1",
                pageDuration = 2.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },

            new ShotBeat
            {
                note = "Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla era el pelo del otro.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.25f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)10,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ARCHIMAGO_1",
                pageDuration = 4.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Se buscan unos a otros: nadie se queda mirando al frente.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = true,
                    },
                    new GestureBeat
                    {
                        note = "Llama a los que faltan.",
                        actorId = "NPC_Aldeano_07",
                        gesture = "HandWave01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        gesture = "HeadShake01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        gesture = "Beg01",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "El, gritando a los que se van, DE FRENTE. Antes el plano ponia el puente al fondo, que es lo mismo que poner la camara a su espalda.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Aldeano_01",
                    heightBias = -0.4f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Senala al puente.",
                actorId = "NPC_Archimago",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 0.4f,
                returnToNormalAfter = false,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_HORA_ARCHIMAGO",
                pageDuration = 2.2f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Primera oleada: los seis que pueden andar solos. Se van AHORA y siguen andando por su cuenta mientras la escena continua.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "Con esto el Parallel termina enseguida y los demas siguen andando de fondo el resto de la secuencia.",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_01",
                        speed = 3.75f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_01", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_01" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_02",
                        speed = 3.98f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_02", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_02" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_03",
                        speed = 3.54f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_03", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_03" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_04",
                        speed = 3.87f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_04", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_04" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_05",
                        speed = 3.65f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_05", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_05" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_06",
                        speed = 4.09f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_06", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_06" },
                    },
                },
            },
            new ParallelBeat
            {
                note = "Y al llegar al otro lado se quedan MIRANDO. Cruzar y seguir de espaldas era lo que hacia que la plaza pareciera vacia justo cuando pasa lo importante.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                },
            },
            new ShotBeat
            {
                note = "La plaza vaciandose hacia el puente. -- FIJO (prologo15): vivo, la camara cambiaba de orbita cada vez que una casa se metia por medio.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.6f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 2.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Primer plano de quien habla, manteniendo el eje de la conversación.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.05f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA_2",
                pageDuration = 2.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla era el pelo del otro.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ARCHIMAGO",
                pageDuration = 3.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla era el pelo del otro.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Liora",
                emotion = (NPCEmotion)2,
                duracion = 0.0f,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_LIORA",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.9f,
                unscaled = true,
            },
            new SayBeat
            {
                note = "La broma del rio, devuelta. Es lo que convierte la despedida en una promesa en vez de en un discurso.",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_DESPEDIDA_ELEGIR",
                pageDuration = 3.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "Que se quede en el aire.",
                seconds = 1.4f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Ella se vuelve hacia los que quedan y les llama.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Aldeano_07",
                    heightBias = -0.4f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Les hace senas.",
                actorId = "NPC_Liora",
                gesture = "HandWave02",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Liora",
                markName = "",
                textKey = "PROLOGO_EVACUACION_LIORA",
                pageDuration = 2.4f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Liora",
                colorDelNombre = ColorDelHablante("Liora"),
                playGestures = true,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Y salen con ella: los cuatro que quedaban y Liora delante. Despacio -- son los que no pueden correr, y son los que van a seguir cruzando el puente cuando el Archimago diga que todavia estan cruzando, un minuto despues.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.1f,
                        unscaled = false,
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Liora",
                        speed = 4.2f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_09", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_09" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_07",
                        speed = 2.6f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_07", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_07" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_08",
                        speed = 2.77f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_08", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_08" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_09",
                        speed = 2.66f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_09", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_09" },
                    },
                    new WalkPathBeat
                    {
                        note = "Del puente, al este y fuera. Los puntos M_Puente_01..10 estan sobre el agua: salir a ellos era volver a meterse en el rio.",
                        actorId = "NPC_Aldeano_10",
                        speed = 2.88f,
                        stickToGround = true,
                        groundOffset = 0.0f,
                        faceTravelDirection = true,
                        animarAndando = true,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Huida_10", "M_Puente_Ent", "M_Puente_Sal", "M_Lejos_10" },
                    },
                },
            },
            new ParallelBeat
            {
                note = "Y al llegar al otro lado se quedan MIRANDO. Cruzar y seguir de espaldas era lo que hacia que la plaza pareciera vacia justo cuando pasa lo importante.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Liora",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.0f,
                    },
                },
            },
            new ShotBeat
            {
                note = "La plaza vaciandose hacia el puente, y el quieto en medio. -- FIJO (prologo15): vivo, la camara cambiaba de orbita cada vez que una casa se metia por medio.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_Liora",
                    heightBias = 1.8f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "Que se les vea irse.",
                seconds = 2.4f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "LA FILA EN EL PUENTE. El puente se nombra tres veces en la fase y no se veia ni una. Encuadrado sobre Liora, que va la primera, y desde cuatro metros y medio de alto: asi entran el tablero, el agua y los que todavia estan cruzando. VIVO, porque van andando. -- FIJO (prologo15): vivo, la camara cambiaba de orbita cada vez que una casa se metia por medio.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Liora",
                    secondaryId = "NPC_Aldeano_07",
                    heightBias = 4.5f,
                    distanceScale = 1.25f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new WaitBeat
            {
                note = "Que se les vea cruzar de verdad, no salir de cuadro.",
                seconds = 2.0f,
                unscaled = true,
            },
            new AmbienteBeat { note = "La plaza se vacía: el pánico se aleja.", loopId = "PANICO", sonar = false, fundido = 4f },
            new ShotBeat
            {
                note = "El, solo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = -0.3f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.6f,
                unscaled = true,
            },
        }),
        Fase("7 - El duelo", "", "", new List<SequenceBeat>
        {
            new EmotionBeat
            {
                note = "Enfadado desde que empieza el duelo: estaba con cara de contento (prologo19).",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)3,
                duracion = 0.0f,
            },
            new ParallelBeat
            {
                note = "Los de la otra orilla se vuelven hacia la plaza al empezar el duelo (prologo19).",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Liora",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                },
            },
            new MusicBeat
            {
                note = "El tema del duelo entra YA, cortando en seco el del Mago Oscuro. Antes entraba tres frases despues, con un silencio en medio que solo servia para que se notara el cambio.",
                musicId = "MAGOOSCURO_CLIMAX",
                fadeOut = 0.0f,
            },
            new SetActionAxisBeat
            {
                note = "El eje al norte. La linea que une a los dos va de oeste a este, asi que la perpendicular es esta -- y los planos sobre el hombro miran al este, que es donde estan el puente y la gente cruzandolo.",
                sideDegrees = 0.0f,
            },
            new ShotBeat
            {
                note = "Le vemos entrar en la plaza vacia. De frente: el Archimago de secundario fija la direccion de la camara, asi que no gira con el. -- VIVO otra vez (prologo16): desde INC-386 un plano vivo ya no cambia de angulo, solo se aparta; fijo se quedaba vacio al irse el personaje.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.9f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WalkPathBeat
            {
                note = "Los ultimos metros, andando. Nadie le ha visto cubrir la distancia hasta ahora y eso le hacia parecer que aparecia por corte; asi llega.",
                actorId = "NPC_MagoOscuro",
                speed = 2.4f,
                stickToGround = true,
                groundOffset = 0.0f,
                faceTravelDirection = true,
                animarAndando = true,
                encadenarCon = "",
                retraso = 0.0f,
                markNames = new List<string> { "M_Duelo3_Oscuro" },
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "M_Duelo3_Mago",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_MagoOscuro",
            },
            new PlaceAtMarkBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "M_Duelo3_Oscuro",
                faceTowardsMark = "",
                faceTowardsActor = "NPC_Archimago",
            },
            new ParallelBeat
            {
                note = "Los dos se ponen en guardia a la vez.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        gesture = "Idle_Battle_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        gesture = "Idle_Battle_NoWeapon",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "Plano general de los dos, con la villa vacia y ardiendo detras. Aqui se establece donde esta cada uno; a partir de ahora ya no hace falta volver a explicarlo.",
                shotName = "",
                smooth = false,
                duration = 2.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.5f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 1.0f,
                returnToNormalAfter = false,
            },
            new PoseBeat
            {
                note = "Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia como 'hace la animacion tres veces'.",
                actorId = "NPC_MagoOscuro",
                pose = "Challenging_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "",
                textKey = "PROLOGO_DUELO_MAGO",
                pageDuration = 2.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Mago Oscuro",
                colorDelNombre = ColorDelHablante("Mago Oscuro"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "El lanza.",
                shotName = "",
                smooth = false,
                duration = 1.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.45f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_BolaDeFuego",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "La bola se forma en su mano.",
                vfxPrefab = Prefab("5ee28f65ca127db42a9a46da980189d4"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.4f, 0.0f),
                lifetime = 1.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShotBeat
            {
                note = "CORTE. Desde detras del hombro del Archimago - lo que viene, viene hacia nosotros.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.OverTheShoulder,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new PoseBeat
            {
                note = "La guardia se SOSTIENE, no se dispara: el clip es ciclico y disparado se repetia solo dos o tres veces.",
                actorId = "NPC_Archimago",
                pose = "Defend_NoWeapon",
                soltar = false,
                volverAIdle = false,
            },
            new VfxBeat
            {
                note = "El escudo se enciende justo a tiempo.",
                vfxPrefab = Prefab("b99922c2f59b1a542bdd06ae8bee47ae"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, -0.1f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "MagoOscuroGolpe",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new SfxBeat { note = "Capa de golpe sobre el bloqueo.", eventKey = "SFX_Prologo_Impacto", clip = null, atActorId = "", markName = "", volume = 0.9f },
            new VfxBeat
            {
                note = "El impacto.",
                vfxPrefab = Prefab("df9374346b76e444dbbb2b019de4da25"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.2f, 0.0f),
                lifetime = 0.9f,
                earlyDespawn = 0.0f,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_EscudoBloquea",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new ScreenFlashBeat
            {
                note = "",
                color = new Color(0.75f, 0.7f, 1.0f, 1.0f),
                duration = 0.12f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.35f,
                duration = 0.4f,
                waitForEnd = false,
            },
            new GestureBeat
            {
                note = "Y al acabar vuelve a la normalidad: sin esto se quedaba encajando el golpe en bucle el resto del duelo.",
                actorId = "NPC_Archimago",
                gesture = "DefendHit_NoWeapon",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = true,
            },
            new WaitBeat
            {
                note = "Fin del asalto 1.",
                seconds = 1.3f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Su cara. Primera vez en todo el prologo que el Archimago ataca a alguien.",
                shotName = "",
                smooth = false,
                duration = 1.3f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)3,
                duracion = 0.0f,
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 0.45f,
                returnToNormalAfter = false,
            },
            new ShotBeat
            {
                note = "CORTE al que recibe, antes de que llegue nada - el hechizo entra en el plano ya empezado.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new SpellBeat
            {
                note = "Su bola de fuego: sale de la mano, cruza el plano y revienta en el. El golpe va DETRAS, cuando ha llegado.",
                lanzaId = "NPC_Archimago",
                objetivoId = "NPC_MagoOscuro",
                objetivoMarca = "",
                vfxEnLaMano = Prefab("895c6d094b6b213418cddcfb520298e9"),
                vfxProyectil = Prefab("eccbc655050af0b4f81d8db39f84a58e"),
                vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                sfxLanzamiento = "Prologo_HechizoArchimago",
                sfxImpacto = "Prologo_ImpactoHechizo",
                alturaDeLaMano = 1.15f,
                separacionDelCuerpo = 0.45f,
                velocidad = 14.0f,
                tiempoDeCarga = 0.25f,
                alturaDelImpacto = 1.1f,
                sacudidaAlImpacto = 0.60f,
                sfxImpactoExtra = "SFX_Prologo_Impacto",
                pausaAlImpacto = 0.08f,
                destelloAlImpacto = new Color(1f, 0.95f, 0.85f, 0.45f),
                esperarAlImpacto = true,
                registrarComo = "",
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("98c4704d0fd7211449bcf5c451095a60"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 1.2f, 0.0f),
                lifetime = 1.2f,
                earlyDespawn = 0.0f,
            },
            new GestureBeat
            {
                note = "Le da de lleno.",
                actorId = "NPC_MagoOscuro",
                gesture = "TakeDamage",
                repeats = 1,
                holdSeconds = 0.55f,
                returnToNormalAfter = true,
            },
            new ShotBeat
            {
                note = "Y se rie -- con el Archimago en cuadro, sin inmutarse. Two-shot y no otro primer plano - el corte anterior ya era un plano corto de el, y dos seguidos del mismo tamano y el mismo sujeto se ven como un salto. Ademas asi la risa tiene a quien ignorar.",
                shotName = "",
                smooth = false,
                duration = 1.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "Fin del asalto 2.",
                seconds = 1.3f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Plano general del choque, desde ARRIBA. A ras de suelo la calle es estrecha y siempre entra media fachada; cuatro metros y medio mas alto se sale por encima de los tejados y se ve el choque entero.",
                shotName = "",
                smooth = false,
                duration = 1.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 4.5f,
                    distanceScale = 1.35f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ParallelBeat
            {
                note = "Cargan los dos a la vez.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        gesture = "MagicSpecial",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        gesture = "MagicSpecial",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_Carga",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ParallelBeat
            {
                note = "Un circulo de invocacion a los pies de cada uno.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, 0.05f, 0.0f),
                        lifetime = 2.2f,
                        earlyDespawn = 0.0f,
                    },
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                        atActorId = "NPC_MagoOscuro",
                        markName = "",
                        offset = new Vector3(0.0f, 0.05f, 0.0f),
                        lifetime = 2.2f,
                        earlyDespawn = 0.0f,
                    },
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.7f,
                unscaled = true,
            },
            new SfxBeat { note = "El silbido de los dos hechizos al salir.", eventKey = "SFX_Prologo_Latigazo", clip = null, atActorId = "", markName = "", volume = 0.9f },
            new ParallelBeat
            {
                note = "Los dos sueltan.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("8b20002c2b69a9d44ab5bae8e1d923cd"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, 1.3f, 0.0f),
                        lifetime = 1.2f,
                        earlyDespawn = 0.0f,
                    },
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("df9374346b76e444dbbb2b019de4da25"),
                        atActorId = "NPC_MagoOscuro",
                        markName = "",
                        offset = new Vector3(0.0f, 1.3f, 0.0f),
                        lifetime = 1.2f,
                        earlyDespawn = 0.0f,
                    },
                },
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_Choque",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new LineasDeConcentracionBeat { note = "Las rayas marcan el choque de los dos hechizos.", mostrar = true, duracion = 1.0f, esperar = false },
            new SfxBeat { note = "Golpe de cine en el choque.", eventKey = "SFX_Prologo_GolpeCine", clip = null, atActorId = "", markName = "", volume = 1.0f },
            new VfxBeat
            {
                note = "Y chocan EN EL AIRE, en el punto medio exacto. Colgado de la marca y no de nadie - si se cuelga de uno de los dos, el choque parece que lo esta ganando el otro.",
                vfxPrefab = Prefab("867c572a5be680d42a042d2349f10143"),
                atActorId = "",
                markName = "M_Duelo3_Choque",
                offset = new Vector3(0.0f, 1.6f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new VfxBeat
            {
                note = "La columna de luz que sube del choque es lo mas cerca que estamos de la verticalidad que pide el guion - los dos personajes no pueden volar, pero lo que se lanzan si.",
                vfxPrefab = Prefab("6555300494081614fa6b6a823cca9f64"),
                atActorId = "",
                markName = "M_Duelo3_Choque",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.2f,
                earlyDespawn = 0.0f,
            },
            new ScreenFlashBeat
            {
                note = "",
                color = new Color(1.0f, 1.0f, 1.0f, 1.0f),
                duration = 0.28f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.75f,
                duration = 1.0f,
                waitForEnd = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_WarClashStinger_A",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ParallelBeat
            {
                note = "La onda les tira a los dos hacia atras.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        gesture = "Dizzy_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.1f,
                        returnToNormalAfter = true,
                    },
                    new GestureBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        gesture = "Dizzy_NoWeapon",
                        repeats = 1,
                        holdSeconds = 1.1f,
                        returnToNormalAfter = true,
                    },
                },
            },
            new ParallelBeat
            {
                note = "Polvo levantandose a los pies de los dos.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, 0.0f, 0.0f),
                        lifetime = 2.0f,
                        earlyDespawn = 0.0f,
                    },
                    new VfxBeat
                    {
                        note = "",
                        vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                        atActorId = "NPC_MagoOscuro",
                        markName = "",
                        offset = new Vector3(0.0f, 0.0f, 0.0f),
                        lifetime = 2.0f,
                        earlyDespawn = 0.0f,
                    },
                },
            },
            new WaitBeat
            {
                note = "Un segundo de nada despues del choque. Sin esto los cuatro asaltos se atropellan y no se lee que son cuatro.",
                seconds = 1.8f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos doblados, respirando, y la plaza destrozada alrededor. PLANO GENERAL, no two-shot - el anterior ya era un two-shot y hay que cambiar de tamano, no solo de angulo.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.8f,
                    distanceScale = 1.9f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new ShotBeat
            {
                note = "Contrapicado - el que esta mas alto en el cuadro es el que va a ganar este asalto.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.9f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicSpecial",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_SueloSeAbre",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new ShotBeat
            {
                note = "Y el suelo se abre BAJO EL. VIVO: justo despues sale despedido dos metros, y con el plano quieto la camara se quedaba encuadrando la carreta.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("d8087ea6f3f1a934e8f05e0bcded1494"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.4f,
                earlyDespawn = 0.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("3dd50886582244645be87adb42aa8528"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.7f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 2.0f,
                earlyDespawn = 0.0f,
            },
            new SaltoBeat
            {
                note = "El suelo se abre y le LANZA dos metros hacia atras, con el dolor puesto desde el primer fotograma (Pain01): nada de quedarse de pie.",
                actorId = "NPC_Archimago",
                altura = 0.6f,
                desplazamiento = -2.0f,
                haciaElLado = false,
                subida = 0.18f,
                sostener = 0.0f,
                caida = 0.3f,
                poseSubida = "Pain01",
                poseAire = "Pain01",
                poseCaida = "",
                parabola = true,
                poseEnElSuelo = "Pain01",
                alturaMaximaDeAterrizaje = 0.3f,
            },
            new WaitBeat
            {
                note = "Doblado, un momento. Sin esto el golpe no se siente.",
                seconds = 0.8f,
                unscaled = true,
            },
            new PoseBeat
            {
                note = "Se recompone.",
                actorId = "NPC_Archimago",
                pose = "",
                soltar = true,
                volverAIdle = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
            },
            new VfxBeat
            {
                note = "",
                vfxPrefab = Prefab("a164e676dbbd6e54587237e619372c98"),
                atActorId = "PROP_Escudo",
                markName = "",
                offset = new Vector3(0.0f, 0.0f, 0.0f),
                lifetime = 1.4f,
                earlyDespawn = 0.0f,
            },
            new ShotBeat
            {
                note = "El avanza sobre el caido: general FIJO de los dos. El vivo le seguia y se metia en una pared; con el del escudo delante eran tres cortes en dos segundos (prologo20).",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.6f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new MoveToBeat
            {
                note = "HACIA el Archimago, y se para delante. Iba a M_Duelo3_Mago — la marca del Archimago — con parada a 0,4 m: le atravesaba (prologo20).",
                actorId = "NPC_MagoOscuro",
                towardsActorId = "NPC_Archimago",
                markName = "",
                stopDistance = 2.4f,
                approachAngle = 0.0f,
                speedOverride = 1.3f,
                timeout = 6.0f,
                faceEachOtherOnArrival = false,
                settleOnArrival = 0.0f,
            },
            new FaceBeat
            {
                note = "Y le mira desde arriba.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.3f,
            },
            new WaitBeat
            {
                note = "Aire entre los dos cortes.",
                seconds = 0.5f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Los dos - el de pie, el otro en el suelo. La linea de sus cabezas dice el resultado sin una sola palabra.",
                shotName = "",
                smooth = false,
                duration = 2.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.TwoShot,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.8f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
            new PoseBeat
            {
                note = "Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia como 'hace la animacion tres veces'.",
                actorId = "NPC_MagoOscuro",
                pose = "Challenging_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_MagoOscuro",
                markName = "",
                textKey = "PROLOGO_DUELO_MAGO_2",
                pageDuration = 2.8f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Mago Oscuro",
                colorDelNombre = ColorDelHablante("Mago Oscuro"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ParallelBeat
            {
                note = "Cada uno a su marca en el mismo corte que lleva a la respuesta: primero se colocan, despues se resuelve el plano, y no se ve ningun salto.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new PlaceAtMarkBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        markName = "M_Duelo3_Oscuro",
                        faceTowardsMark = "",
                        faceTowardsActor = "NPC_Archimago",
                    },
                    new PlaceAtMarkBeat
                    {
                        note = "",
                        actorId = "NPC_Archimago",
                        markName = "M_Duelo3_Mago",
                        faceTowardsMark = "",
                        faceTowardsActor = "NPC_MagoOscuro",
                    },
                    new ShotBeat
                    {
                        note = "CORTE directo desde «Ni siquiera puedes salvarte tu» a su respuesta. De pie, y la camara mas baja que el.",
                        shotName = "",
                        smooth = false,
                        duration = 2.0f,
                        waitForArrival = true,
                        live = false,
                        framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -0.7f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
                    },
                },
            },
            new GestureBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                gesture = "Idle_Battle_NoWeapon",
                repeats = 1,
                holdSeconds = 0.0f,
                returnToNormalAfter = false,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)3,
                duracion = 0.0f,
            },
            new PoseBeat
            {
                note = "Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia como 'hace la animacion tres veces'.",
                actorId = "NPC_Archimago",
                pose = "Idle_Battle_NoWeapon",
                soltar = false,
                volverAIdle = true,
            },
            new SayBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_DUELO_ARCHIMAGO",
                pageDuration = 3.6f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Y el lo entiende un segundo antes de que pase.",
                shotName = "",
                smooth = false,
                duration = 1.6f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                },
            },
        }),
        Fase("8 - El ultimo hechizo", "", "", new List<SequenceBeat>
        {
            new ParallelBeat
            {
                note = "Los de la otra orilla se vuelven hacia la plaza al empezar el ultimo hechizo (prologo19).",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_01",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_02",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_03",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_04",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_05",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_06",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_07",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_08",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_09",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Aldeano_10",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                    new FaceBeat
                    {
                        note = "",
                        actorId = "NPC_Liora",
                        targetActorId = "NPC_Archimago",
                        markName = "",
                        lookAway = false,
                        mutual = false,
                        turnDuration = 0.4f,
                    },
                },
            },
            new ShotBeat
            {
                note = "El, desde abajo, antes de despegar.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -1.4f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Se planta.",
                actorId = "NPC_MagoOscuro",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 0.9f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.8f,
                unscaled = true,
            },
            new GestureBeat
            {
                note = "Flexiona y salta.",
                actorId = "NPC_MagoOscuro",
                gesture = "JumpStart_InPlace_NoWeapon",
                repeats = 1,
                holdSeconds = 0.3f,
                returnToNormalAfter = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_Despegue",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new VfxBeat
            {
                note = "Polvo a sus pies al despegar. Antes salia un rayo, que no venia de ningun sitio.",
                vfxPrefab = Prefab("41494896fc96c9748b81d3356632794e"),
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 0.2f, 0.0f),
                lifetime = 1.6f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new ShotBeat
            {
                note = "Y la camara sube con el, desde muy abajo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -2.8f,
                    distanceScale = 1.6f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "Sube echandose atras: siete metros al oeste por seis y medio de alto.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new PoseBeat
                    {
                        note = "La pose se sostiene todo el tramo.",
                        actorId = "NPC_MagoOscuro",
                        pose = "fly_idle",
                        soltar = false,
                        volverAIdle = true,
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        speed = 3.6f,
                        stickToGround = false,
                        groundOffset = 0.0f,
                        faceTravelDirection = false,
                        animarAndando = false,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Aire_Oscuro" },
                    },
                },
            },
            new FaceBeat
            {
                note = "Arriba, se vuelve a mirarle.",
                actorId = "NPC_MagoOscuro",
                targetActorId = "NPC_Archimago",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new ShotBeat
            {
                note = "EL PLANAZO, a su altura y no desde los pies: el valle ardiendo detras dice mas que el cielo vacio.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = 2.2f,
                    distanceScale = 1.35f,
                    fovOverride = 58.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new PoseBeat
            {
                note = "Flotando. Una pose sostenida, no tres disparos del mismo clip: eso es lo que se leia como que se habia quedado pillado.",
                actorId = "NPC_MagoOscuro",
                pose = "fly_idle",
                soltar = false,
                volverAIdle = true,
            },
            new WaitBeat
            {
                note = "Que se le vea ahi arriba antes de que haga nada.",
                seconds = 1.4f,
                unscaled = true,
            },
            new GestureBeat
            {
                note = "Dispara desde arriba.",
                actorId = "NPC_MagoOscuro",
                gesture = "MagicRight",
                repeats = 1,
                holdSeconds = 0.4f,
                returnToNormalAfter = false,
            },
            new ParallelBeat
            {
                note = "El disparo sale de su mano y baja.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new SpellBeat
                    {
                        note = "Nace en su mano, viaja, y revienta en el suelo.",
                        lanzaId = "NPC_MagoOscuro",
                        objetivoId = "",
                        objetivoMarca = "M_Duelo3_Mago",
                        vfxEnLaMano = Prefab("895c6d094b6b213418cddcfb520298e9"),
                        vfxProyectil = Prefab("232bdd92f4fb5f642bb0d7a40d53380f"),
                        vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                        sfxLanzamiento = "Prologo_BolaDeFuego",
                        sfxImpacto = "Prologo_ImpactoHechizo",
                        alturaDeLaMano = 1.15f,
                        separacionDelCuerpo = 0.45f,
                        velocidad = 13.0f,
                        tiempoDeCarga = 0.3f,
                        alturaDelImpacto = 1.1f,
                        sacudidaAlImpacto = 0.88f,
                        sfxImpactoExtra = "SFX_Prologo_Impacto",
                        pausaAlImpacto = 0.08f,
                        destelloAlImpacto = new Color(1f, 0.95f, 0.85f, 0.45f),
                        esperarAlImpacto = true,
                        registrarComo = "",
                    },
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.35f,
                        unscaled = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "El Archimago lo ve venir.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Reaction,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.15f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)4,
                duracion = 0.0f,
                mantener = true,
            },
            new ShotBeat
            {
                note = "La esquiva, siguiendole.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = -0.6f,
                    distanceScale = 1.4f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new SaltoBeat
            {
                note = "SE QUITA DE EN MEDIO. Salta hacia ARRIBA y de lado, desde donde este, y baja al suelo que tenga debajo. Antes esto eran dos viajes a marcas aereas puestas a mano: iba hacia la coordenada y no hacia arriba -- \"parece que salta para otro lado\" -- y aterrizaba donde dijera el numero, que en la sexta grabacion fue encima de un tejado.",
                actorId = "NPC_Archimago",
                altura = 2.8f,
                desplazamiento = 2.6f,
                haciaElLado = true,
                subida = 0.4f,
                sostener = 0.35f,
                caida = 0.45f,
                poseSubida = "JumpStart_InPlace_NoWeapon",
                poseAire = "JumpAirSpin_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
                alturaMaximaDeAterrizaje = 0.3f,
            },
            new WaitBeat
            {
                note = "Un respiro. Aqui es donde se entiende que ha esquivado.",
                seconds = 1.2f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El, desde abajo, contestando.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)10,
                duracion = 0.0f,
                mantener = true,
            },
            new GestureBeat
            {
                note = "Responde.",
                actorId = "NPC_Archimago",
                gesture = "MagicLeft",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new ParallelBeat
            {
                note = "El contraataque, siguiendole por el aire.",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new SpellBeat
                    {
                        note = "Nace en su mano y persigue al Mago Oscuro.",
                        lanzaId = "NPC_Archimago",
                        objetivoId = "NPC_MagoOscuro",
                        objetivoMarca = "",
                        vfxEnLaMano = Prefab("895c6d094b6b213418cddcfb520298e9"),
                        vfxProyectil = Prefab("eccbc655050af0b4f81d8db39f84a58e"),
                        vfxImpacto = Prefab("67a684e320da6e7439421a07e3fa265c"),
                        sfxLanzamiento = "Prologo_HechizoArchimago",
                        sfxImpacto = "Prologo_GolpeEnElAire",
                        alturaDeLaMano = 1.15f,
                        separacionDelCuerpo = 0.45f,
                        velocidad = 15.0f,
                        tiempoDeCarga = 0.3f,
                        alturaDelImpacto = 1.1f,
                        sacudidaAlImpacto = 0.64f,
                        sfxImpactoExtra = "SFX_Prologo_Impacto",
                        pausaAlImpacto = 0.08f,
                        destelloAlImpacto = new Color(1f, 0.95f, 0.85f, 0.45f),
                        esperarAlImpacto = true,
                        registrarComo = "HECHIZO_ARCHIMAGO",
                    },
                    new WaitBeat
                    {
                        note = "",
                        seconds = 0.3f,
                        unscaled = false,
                    },
                },
            },
            new ShotBeat
            {
                note = "Le da en el aire, y se le ve encajarlo. Contrapicado suave: con -2,2 m la cámara acababa bajo el suelo al seguirle en la caída (INC-586).",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.6f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new EsperarHechizoBeat
            {
                note = "Hasta que le llega. Sin esto se dolia antes de que la bola le alcanzara.",
                hechizo = "HECHIZO_ARCHIMAGO",
                topeDeSalida = 1.5f,
                topeDeVuelo = 6.0f,
            },
            new ParallelBeat
            {
                note = "Le alcanza de lleno y cae enseguida: sin el segundo congelado en el aire (Raúl: «hay como un segundo de pausa antes de caer»).",
                waitForAll = false,
                beats = new List<SequenceBeat>
                {
                    new GestureBeat
                    {
                        note = "Le alcanza de lleno.",
                        actorId = "NPC_MagoOscuro",
                        gesture = "TakeDamage",
                        repeats = 1,
                        holdSeconds = 0.0f,
                        returnToNormalAfter = false,
                    },
                    new WaitBeat { note = "Lo justo para que se lea el golpe.", seconds = 0.12f, unscaled = true },
                },
            },
            new SaltoBeat
            {
                note = "No baja volando: se cae. Es lo que hace el jugador cuando le alcanzan en el aire, y es lo que hace que el golpe cuente.",
                actorId = "NPC_MagoOscuro",
                altura = 0.0f,
                desplazamiento = 0.0f,
                haciaElLado = false,
                subida = 0.0f,
                sostener = 0.0f,
                caida = 0.75f,
                poseSubida = "",
                poseAire = "JumpAir_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
            },
            new GestureBeat
            {
                note = "Toma tierra de mala manera.",
                actorId = "NPC_MagoOscuro",
                gesture = "Landing",
                repeats = 1,
                holdSeconds = 0.5f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "Y AQUI se espera. Es el unico momento del prologo en que el Mago Oscuro no manda.",
                seconds = 1.1f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Su cara, desde abajo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.7f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat
            {
                note = "Y se rie. Desde el suelo, que es peor.",
                actorId = "NPC_MagoOscuro",
                gesture = "Laugh01",
                repeats = 1,
                holdSeconds = 1.3f,
                returnToNormalAfter = false,
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "VIVO desde que toma impulso: la camara le sigue en el despegue, la subida y el picado, sin cortes. Sustituye al general a ras de suelo de la foto del 21 sep, en el que se salia por arriba.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = true,
                framing = new ShotFraming
                {
                    type = ShotType.Tracking,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = -0.8f,
                    distanceScale = 1.3f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new GestureBeat
            {
                note = "Toma impulso.",
                actorId = "NPC_MagoOscuro",
                gesture = "Challenging_NoWeapon",
                repeats = 1,
                holdSeconds = 0.8f,
                returnToNormalAfter = false,
            },
            new SaltoBeat
            {
                note = "Y vuelve a subir, ahora que esta en el suelo.",
                actorId = "NPC_MagoOscuro",
                altura = 7.0f,
                desplazamiento = 0.0f,
                haciaElLado = false,
                subida = 0.6f,
                sostener = 0.2f,
                caida = 0.0f,
                poseSubida = "JumpStart_InPlace_NoWeapon",
                poseAire = "JumpAir_InPlace_NoWeapon",
                poseCaida = "JumpEnd_InPlace_NoWeapon",
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.7f,
                unscaled = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_SueloSeAbre",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new FaceBeat
            {
                note = "De cara a lo que baja: al romperse el escudo sale despedido HACIA ATRAS, y atras depende de adonde mire.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.25f,
            },
            new ParallelBeat
            {
                note = "Baja a por el, y el se cubre mientras baja.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new ParallelBeat
                    {
                        note = "Se tira en picado a por el: diez metros en poco mas de un segundo.",
                        waitForAll = true,
                        beats = new List<SequenceBeat>
                        {
                            new PoseBeat
                            {
                                note = "La pose se sostiene todo el tramo.",
                                actorId = "NPC_MagoOscuro",
                                pose = "fly_dive",
                                soltar = false,
                                volverAIdle = true,
                            },
                            new WalkPathBeat
                            {
                                note = "",
                                actorId = "NPC_MagoOscuro",
                                speed = 9.0f,
                                stickToGround = false,
                                groundOffset = 0.0f,
                                faceTravelDirection = false,
                                animarAndando = false,
                                encadenarCon = "",
                                retraso = 0.0f,
                                markNames = new List<string> { "M_Picado_Oscuro" },
                            },
                        },
                    },
                    new PoseBeat
                    {
                        note = "La guardia se SOSTIENE, no se dispara: el clip es ciclico y disparado se repetia solo dos o tres veces.",
                        actorId = "NPC_Archimago",
                        pose = "Defend_NoWeapon",
                        soltar = false,
                        volverAIdle = false,
                    },
                    new VfxBeat
                    {
                        note = "El escudo, encendido antes del choque.",
                        vfxPrefab = Prefab("b99922c2f59b1a542bdd06ae8bee47ae"),
                        atActorId = "NPC_Archimago",
                        markName = "",
                        offset = new Vector3(0.0f, -0.1f, 0.0f),
                        lifetime = 1.2f,
                        earlyDespawn = 0.0f,
                        seguirAlActor = true,
                    },
                },
            },
            new VfxBeat
            {
                note = "El choque, sobre el escudo.",
                vfxPrefab = Prefab("67a684e320da6e7439421a07e3fa265c"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.7f, 0.0f),
                lifetime = 1.2f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new VfxBeat
            {
                note = "El escudo, rompiendose.",
                vfxPrefab = Prefab("98c4704d0fd7211449bcf5c451095a60"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 1.0f, 0.0f),
                lifetime = 1.2f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "MagoOscuroGolpe",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 1.0f,
            },
            new ScreenFlashBeat
            {
                note = "El golpe, sin tapar la pantalla entera.",
                color = new Color(1f, 0.6f, 0.2f, 0.5f),
                duration = 0.14f,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.8f,
                duration = 0.9f,
                waitForEnd = false,
            },
            new ShotBeat
            {
                note = "El momento en que el escudo se rompe, de lado: se ve el golpe, el escudo saltando en pedazos y los tres metros que retrocede.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 1.2f,
                    distanceScale = 1.25f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ParallelBeat
            {
                note = "El escudo se rompe y le echa hacia atras; el otro toma tierra.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new SaltoBeat
                    {
                        note = "Le rompe el escudo y le hace RETROCEDER: encaja el golpe de pie y resbala tres metros hacia atras. Ni parabola ni derribo -- lo de caer tirado y levantarse duraba una eternidad y le quitaba la escena.",
                        actorId = "NPC_Archimago",
                        altura = 0.35f,
                        desplazamiento = -0.8f,
                        haciaElLado = false,
                        subida = 0.14f,
                        sostener = 0.0f,
                        caida = 0.3f,
                        poseSubida = "DefendHit_NoWeapon",
                        poseAire = "DefendHit_NoWeapon",
                        poseCaida = "",
                        parabola = false,
                        poseEnElSuelo = "",
                        alturaMaximaDeAterrizaje = 0.3f,
                    },
                    new GestureBeat
                    {
                        note = "Toma tierra al final del picado.",
                        actorId = "NPC_MagoOscuro",
                        gesture = "Landing",
                        repeats = 1,
                        holdSeconds = 0.5f,
                        returnToNormalAfter = false,
                    },
                },
            },
            new PoseBeat
            {
                note = "Le duele: doblado, la mano al estomago (prologo19).",
                actorId = "NPC_Archimago",
                pose = "IdleWounded01",
                soltar = false,
                volverAIdle = true,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = NPCEmotion.Sad,
                duracion = 0.0f,
                mantener = true,
            },
            new WaitBeat
            {
                note = "Lo que le cuesta rehacerse.",
                seconds = 1.4f,
                unscaled = true,
            },
            new PoseBeat
            {
                note = "Se endereza.",
                actorId = "NPC_Archimago",
                pose = "",
                soltar = true,
                volverAIdle = true,
            },
            new FaceBeat
            {
                note = "Y le MIRA.",
                actorId = "NPC_Archimago",
                targetActorId = "NPC_MagoOscuro",
                markName = "",
                lookAway = false,
                mutual = false,
                turnDuration = 0.35f,
            },
            new EmotionBeat
            {
                note = "",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)3,
                duracion = 0.0f,
                mantener = true,
            },
            new GestureBeat
            {
                note = "Con rabia.",
                actorId = "NPC_Archimago",
                gesture = "Angry01",
                repeats = 1,
                holdSeconds = 0.0f,
                returnToNormalAfter = false,
            },
            new GestureBeat
            {
                note = "Y vuelve a despegar.",
                actorId = "NPC_MagoOscuro",
                gesture = "JumpStart_InPlace_NoWeapon",
                repeats = 1,
                holdSeconds = 0.25f,
                returnToNormalAfter = false,
            },
            new ParallelBeat
            {
                note = "Y vuelve a subir, por el otro lado.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new PoseBeat
                    {
                        note = "La pose se sostiene todo el tramo.",
                        actorId = "NPC_MagoOscuro",
                        pose = "fly_idle",
                        soltar = false,
                        volverAIdle = true,
                    },
                    new WalkPathBeat
                    {
                        note = "",
                        actorId = "NPC_MagoOscuro",
                        speed = 5.5f,
                        stickToGround = false,
                        groundOffset = 0.0f,
                        faceTravelDirection = false,
                        animarAndando = false,
                        encadenarCon = "",
                        retraso = 0.0f,
                        markNames = new List<string> { "M_Aire_Oscuro_3" },
                    },
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 0.9f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "EL PLANAZO otra vez, mas cerrado. Las manos levantadas y el cielo detras.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -5.5f,
                    distanceScale = 0.95f,
                    fovOverride = 36.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new PoseBeat
            {
                note = "Brazos ARRIBA sosteniendo el agujero negro durante toda la carga (Raúl: «la pose de los brazos levantados con el hechizo siempre por encima de sus manos»).",
                actorId = "NPC_MagoOscuro",
                pose = EstadoDisponible("NPC_MagoOscuro", "InvocarBrazosArriba", "CastingIdle01"),
                soltar = false,
                volverAIdle = true,
            },
            new PostprocesoBeat { perfil = Perfil(PrologoPostprocesoSueno.Climax), transicion = 2.5f, esperar = false },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_HechizoGrande",
                clip = null,
                atActorId = "NPC_MagoOscuro",
                markName = "",
                volume = 1.0f,
            },
            new SfxBeat { note = "El agujero negro empieza a formarse.", eventKey = "SFX_Prologo_AgujeroNegro_Carga", clip = null, atActorId = "", markName = "", volume = 0.9f },
            new AmbienteBeat { note = "Zumbido del agujero negro mientras dura.", loopId = "AGUJERO_NEGRO", eventKey = "SFX_Prologo_AgujeroNegro_Bucle", volumen = 0.5f },
            new VfxBeat
            {
                note = "El agujero negro nace sobre su cabeza, entre sus brazos levantados, y no se mueve de ahí.",
                vfxPrefab = hechizoSostenido,
                registrarComo = "CARGA_ESFERA",
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 3.9f, 0.0f),
                lifetime = 160.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new EscalarActorBeat { note = "", actorId = "CARGA_ESFERA", escala = 0.35f, duracion = 0f },
            new EscalarActorBeat { note = "Nace pequeño sobre su cabeza y empieza a crecer.", actorId = "CARGA_ESFERA", escala = 1.0f, duracion = 3.5f, esperar = false },
            new WaitBeat
            {
                note = "",
                seconds = 1.3f,
                unscaled = true,
            },
            new VfxBeat
            {
                note = "Una segunda capa refuerza el crecimiento del hechizo.",
                vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                registrarComo = "CARGA_CRECE",
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 3.9f, 0.0f),
                lifetime = 160.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShakeBeat
            {
                note = "",
                intensity = 0.35f,
                duration = 1.6f,
                waitForEnd = false,
            },
            new WaitBeat
            {
                note = "Que dure. Es la unica amenaza de todo el prologo que se ve venir.",
                seconds = 1.6f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "Su cara, un segundo antes de soltarlo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "NPC_Archimago",
                    heightBias = 0.0f,
                    distanceScale = 1.1f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new WaitBeat
            {
                note = "",
                seconds = 1.0f,
                unscaled = true,
            },
            new CutInBeat { note = "El primer plano subraya la amenaza al valle.", actorId = "NPC_MagoOscuro", encuadre = EncuadreCutIn.Cara, lado = LadoCutIn.Derecha, duracion = 2.2f, esperar = false },
            new SayBeat
            {
                note = "Amenaza al valle con calma mientras sostiene el hechizo.",
                actorId = "NPC_MagoOscuro",
                markName = "",
                textKey = "PROLOGO_MAGO_VALLE",
                pageDuration = 3.0f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Mago Oscuro",
                colorDelNombre = ColorDelHablante("Mago Oscuro"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "El plano medio revela su desesperación.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La tristeza acompaña el comienzo de la súplica.",
                actorId = "NPC_Archimago",
                emotion = NPCEmotion.Sad,
                duracion = 0.0f,
                mantener = true,
            },
            new FaceBeat
            {
                note = "Mira al puente para recordar a quienes evacúan.",
                actorId = "NPC_Archimago",
                targetActorId = "",
                markName = "M_Puente_Ent",
                lookAway = false,
                mutual = false,
                turnDuration = 0.4f,
            },
            new CutInBeat { note = "Los ojos acercan la súplica al espectador.", actorId = "NPC_Archimago", encuadre = EncuadreCutIn.Cara, lado = LadoCutIn.Izquierda, duracion = 1.8f, esperar = false },
            new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Surprised, mantener = true },
            new SayBeat
            {
                note = "Pide ayuda por quienes todavía cruzan el puente.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLEGARIA_01",
                expresiones = new List<ExpresionDuranteLinea> { new ExpresionDuranteLinea { progreso = 0.55f, expresion = new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Scared, mantener = true } } },
                pageDuration = 4.5f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Contrapicado suave y cerrado: la invocación crece desde las manos juntas hacia todo el valle.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new GestureBeat { actorId = "NPC_Archimago", gesture = "HeadShake02", holdSeconds = .4f },
            new PoseBeat
            {
                note = "Reza con las manos juntas y los ojos cerrados (Raúl: «con las manos juntas y ojos cerrados»).",
                actorId = "NPC_Archimago",
                pose = EstadoDisponible("NPC_Archimago", "RezarDePie", "Beg01_Loop"),
                soltar = false,
                volverAIdle = true,
            },
            new VfxBeat
            {
                note = "El círculo mágico sostiene la invocación durante sus páginas sin añadir otro recurso.",
                vfxPrefab = Prefab("12a56ee4630040c4eb17ae9707ed4cef"),
                registrarComo = "CIRCULO_PLEGARIA",
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.05f, 0.0f),
                lifetime = 75.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologo_MagiaLevitar",
                clip = null,
                atActorId = "NPC_Archimago",
                markName = "",
                volume = 0.8f,
            },
            new EmotionBeat
            {
                note = "Tristeza con ojos cerrados durante la invocaci?n.",
                ojos = "Eye09",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)7,
                duracion = 0.0f,
                mantener = true,
            },
            new ShakeBeat
            {
                note = "Una vibración leve del recurso existente acompaña el inicio de la invocación.",
                intensity = 0.08f,
                duration = 1.2f,
                waitForEnd = false,
            },
            new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Sad, mantener = true, ojos = "Eye09" },
            new EfectoDeVozBeat { note = "La sala de la súplica va horneada en los clips: sin filtro encima.", preset = PresetDeVoz.Ninguno },
            new MezclaBeat { note = "La plegaria manda (Raúl: «suena demasiado de fondo»): bajan música, ambiente y efectos mientras reza.", fuente = "plegaria", musicaDb = -8f, sfxDb = -4f, ambienteDb = -10f, fundido = 1.0f },
            new AmbienteBeat { note = "Un coro suave sostiene la invocación hasta el lanzamiento.", loopId = "CORO", eventKey = "SFX_Prologo_CoroBucle", volumen = 0.3f },
            new SayBeat
            {
                note = "Con las manos juntas, profundiza en la súplica.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLEGARIA_02",
                pageDuration = 4.5f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "El contrapicado muestra cómo crece la amenaza.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -5.5f,
                    distanceScale = 0.95f,
                    fovOverride = 36.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new EscalarActorBeat { note = "El agujero negro sigue creciendo.", actorId = "CARGA_ESFERA", escala = 1.5f, duracion = 2f, esperar = false },
            new VfxBeat
            {
                note = "Una capa adicional refuerza el crecimiento sin cortar los bucles.",
                vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                registrarComo = "CARGA_REFUERZO",
                atActorId = "NPC_MagoOscuro",
                markName = "",
                offset = new Vector3(0.0f, 3.9f, 0.0f),
                lifetime = 160.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = true,
            },
            new ShakeBeat
            {
                note = "La vibración transmite la presión del hechizo.",
                intensity = 0.2f,
                duration = 1.0f,
                waitForEnd = false,
            },
            new WaitBeat
            {
                note = "La amenaza ocupa el plano antes de volver a la súplica.",
                seconds = 1.2f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El primer plano recoge la súplica entre lágrimas.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Sad, mantener = true },
            new ParallelBeat
            {
                note = "«Te lo ruego… ¡préstame tu fuego!»: a mitad de la línea abre los ojos y alza los brazos (Raúl: «ahí falta animación»).",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
        new SayBeat
                    {
                        note = "La petición hace crecer el círculo a sus pies.",
                        actorId = "NPC_Archimago",
                        markName = "",
                        textKey = "PROLOGO_PLEGARIA_03",
                        expresiones = new List<ExpresionDuranteLinea> { new ExpresionDuranteLinea { progreso = 0.87f, expresion = new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Determined, mantener = true } } },
                        pageDuration = 4.5f,
                        gesture = "",
                        gestureRepeats = 1,
                        speakerNameKey = "Archimago",
                        colorDelNombre = ColorDelHablante("Archimago"),
                        playGestures = false,
                        overrideBubbleOffset = false,
                        bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                    },
                    new SerieBeat
                    {
                        note = "Reza con los ojos cerrados y, al pedir el fuego, se abre.",
                        beats = new List<SequenceBeat>
                        {
                            new WaitBeat { note = "Hasta «te lo ruego».", seconds = 7.4f, unscaled = true },
                            new EmotionBeat { note = "Abre los ojos: lo pide de frente.", actorId = "NPC_Archimago", emotion = NPCEmotion.Determined, duracion = 0.0f, mantener = true },
                            new GestureBeat { note = "Abre los brazos para recibir el fuego.", actorId = "NPC_Archimago", gesture = "CastingEnter01", repeats = 1, holdSeconds = 0.5f, returnToNormalAfter = false },
                            new PoseBeat { note = "Brazos alzados, canalizando.", actorId = "NPC_Archimago", pose = EstadoDisponible("NPC_Archimago", "HechizoHaciaArriba", "CastingIdle01"), soltar = false, volverAIdle = true },
                        },
                    },
                },
            },
            new VfxBeat
            {
                note = "Un segundo anillo amplía el círculo durante el resto de la plegaria.",
                vfxPrefab = Prefab("a2a060732547fe64581bb0cb3c2bdf1d"),
                atActorId = "NPC_Archimago",
                markName = "",
                offset = new Vector3(0.0f, 0.08f, 0.0f),
                lifetime = 30.0f,
                earlyDespawn = 0.0f,
                seguirAlActor = false,
            },
            new ShotBeat
            {
                note = "El plano medio muestra el esfuerzo de sostener la plegaria.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La tristeza sostiene la súplica entre lágrimas.",
                actorId = "NPC_Archimago",
                emotion = NPCEmotion.Sad,
                duracion = 0.0f,
                mantener = true,
            },
            new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Sad, mantener = true },
            new SayBeat
            {
                note = "La súplica crece mientras sostiene las manos juntas.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLEGARIA_04",
                expresiones = new List<ExpresionDuranteLinea> { new ExpresionDuranteLinea { progreso = 0.72f, expresion = new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Angry, mantener = true } } },
                pageDuration = 4.5f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new ShotBeat
            {
                note = "Un contrapicado más cerrado aumenta la amenaza.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -6.0f,
                    distanceScale = 0.8f,
                    fovOverride = 32.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new ShakeBeat
            {
                note = "La vibración transmite la presión del hechizo.",
                intensity = 0.3f,
                duration = 1.2f,
                waitForEnd = false,
            },
            new EscalarActorBeat { note = "Y crece más.", actorId = "CARGA_ESFERA", escala = 2.0f, duracion = 2f, esperar = false },
            new WaitBeat
            {
                note = "El hechizo sigue creciendo mientras la súplica se interrumpe.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El corte y el encuadre cerrado acentúan su rabia.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 1.0f,
                    fovOverride = 22.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat
            {
                note = "La súplica se convierte en rabia entre lágrimas.",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)NPCEmotion.Angry, // Rabia que impulsa la decisión.
                duracion = 0.0f,
                mantener = true,
            },
            new ShakeBeat
            {
                note = "La vibración transmite la presión del hechizo.",
                intensity = 0.25f,
                duration = 2.0f,
                waitForEnd = false,
            },
            new LineasDeConcentracionBeat { note = "Las rayas acentúan el zoom y la rabia de la súplica.", mostrar = true, esperar = false },
            new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Angry, mantener = true },
            new PoseBeat { note = "«¡Toma mis años!»: se ofrece entero, brazos arriba.", actorId = "NPC_Archimago", pose = EstadoDisponible("NPC_Archimago", "InvocarBrazosArriba", "MagicAttackOmni01_Load"), soltar = false, volverAIdle = true, congelarAlFinal = true },
            new EmotionBeat { note = "Esfuerzo y dolor.", actorId = "NPC_Archimago", emotion = NPCEmotion.Angry, duracion = 0.0f, mantener = true },
            new SayBeat
            {
                note = "La rabia da fuerza a la petición.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLEGARIA_05",
                pageDuration = 4.5f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new LineasDeConcentracionBeat { note = "Retira las rayas para revelar el círculo.", mostrar = false, esperar = false },
            new ShotBeat
            {
                note = "El circulo entero, desde arriba: el primer plano de la plegaria no enseña el suelo.",
                shotName = "",
                smooth = false,
                duration = 1.4f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 3.2f,
                    distanceScale = 0.9f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = false,
                },
            },
            new WaitBeat
            {
                note = "El plano cenital revela el círculo completo.",
                seconds = 1.0f,
                unscaled = true,
            },
            new ShotBeat
            {
                note = "El primer plano íntimo recoge el final de la súplica.",
                shotName = "",
                smooth = false,
                duration = 0.8f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.CloseUp,
                    subjectId = "NPC_Archimago",
                    secondaryId = "",
                    heightBias = 0.0f,
                    distanceScale = 0.9f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Sad, mantener = true, ojos = "Eye09" },
            new PoseBeat { note = "«Perdóname, Liora…»: se encoge, rezando.", actorId = "NPC_Archimago", pose = EstadoDisponible("NPC_Archimago", "RezarEncogido", "Cry01_Loop"), soltar = false, volverAIdle = true },
            new EmotionBeat { note = "Llora.", actorId = "NPC_Archimago", emotion = NPCEmotion.Sad, ojos = "Eye09", duracion = 0.0f, mantener = true },
            new SayBeat
            {
                note = "Cierra la súplica antes del silencio y la decisión.",
                actorId = "NPC_Archimago",
                markName = "",
                textKey = "PROLOGO_PLEGARIA_06",
                pageDuration = 4.5f,
                gesture = "",
                gestureRepeats = 1,
                speakerNameKey = "Archimago",
                colorDelNombre = ColorDelHablante("Archimago"),
                playGestures = false,
                overrideBubbleOffset = false,
                bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
            },
            new WaitBeat
            {
                note = "Y el silencio justo antes de abrir los ojos.",
                seconds = 0.5f,
                unscaled = true,
            },
            new EmotionBeat
            {
                note = "Abre los ojos, decidido.",
                actorId = "NPC_Archimago",
                emotion = (NPCEmotion)10,
                duracion = 0.0f,
                mantener = true,
            },
            new PoseBeat
            {
                note = "Suelta las manos y echa a correr.",
                actorId = "NPC_Archimago",
                pose = "",
                soltar = true,
                volverAIdle = true,
            },
            new ShotBeat
            {
                note = "El Mago Oscuro desde abajo, con los brazos arriba y el agujero negro encima: la amenaza entera en un plano.",
                shotName = "",
                smooth = false,
                duration = 1.2f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Wide,
                    subjectId = "NPC_MagoOscuro",
                    secondaryId = "",
                    heightBias = -4.0f,
                    distanceScale = 1.3f,
                    fovOverride = 40.0f,
                    crossTheLine = false,
                    headroom = false,
                    encara = true,
                },
            },
            new EscalarActorBeat { note = "El agujero negro alcanza su tamaño máximo.", actorId = "CARGA_ESFERA", escala = 2.6f, duracion = 1.4f, esperar = false },
            new SfxBeat { note = "La subida de tensión arranca unos segundos antes del choque.", eventKey = "SFX_Prologo_Subida", clip = null, atActorId = "", markName = "", volume = 0.9f },
            new ShakeBeat { note = "La realidad cruje alrededor del hechizo.", intensity = 0.45f, duration = 1.6f, waitForEnd = false },
            new LineasDeConcentracionBeat { note = "Las rayas negras cargan de tensión el intento de descargarlo.", color = new Color(0f, 0f, 0f, .8f), huecoCentral = .3f, duracion = 1.4f, esperar = false },
            new WaitBeat { note = "", seconds = 1.0f, unscaled = true },
            new ShotBeat
            {
                note = "El Archimago NO sale del círculo (Raúl: «mejor que no se salga del círculo mágico»): plano medio a la altura de los ojos, con el agujero negro arriba.",
                shotName = "",
                smooth = false,
                duration = 1.0f,
                waitForArrival = true,
                live = false,
                framing = new ShotFraming
                {
                    type = ShotType.Medium,
                    subjectId = "NPC_Archimago",
                    secondaryId = "NPC_MagoOscuro",
                    heightBias = 0.0f,
                    distanceScale = 1.2f,
                    fovOverride = 0.0f,
                    crossTheLine = false,
                    headroom = true,
                    encara = true,
                },
            },
            new EmotionBeat { note = "Decidido: va a gastar todo lo que le queda.", actorId = "NPC_Archimago", emotion = NPCEmotion.Determined, duracion = 0.0f, mantener = true },
            new GestureBeat { note = "Recoge la luz del círculo con los brazos.", actorId = "NPC_Archimago", gesture = "CastingEnter01", repeats = 1, holdSeconds = 0.5f, returnToNormalAfter = false },
            new PoseBeat { note = "Brazos al frente, cargando, de pie dentro del círculo.", actorId = "NPC_Archimago", pose = EstadoDisponible("NPC_Archimago", "LanzamientoSostenido", "CastingIdle01"), soltar = false, volverAIdle = true },
            new PoseBeat { note = "El Mago Oscuro empuja el agujero negro hacia él.", actorId = "NPC_MagoOscuro", pose = EstadoDisponible("NPC_MagoOscuro", "LanzamientoSostenido", "CastingIdle01"), soltar = false, volverAIdle = true },
            new TimeScaleBeat
            {
                note = "Cámara lenta solo para el enfrentamiento. El choque va en tiempo real.",
                timeScale = 0.45f,
                rampDuration = 0.2f,
            },
            new EfectoDeVozBeat { note = "La reverb y el eco del grito van horneados en el clip: aquí solo se sube.", preset = PresetDeVoz.Ninguno, gananciaDb = 4f },
            new MezclaBeat { note = "La mezcla de la plegaria deja paso a la del grito.", fuente = "plegaria", quitar = true, fundido = 0.3f },
            new MezclaBeat { note = "Todo baja para que mande el grito.", fuente = "grito", musicaDb = -10f, sfxDb = -6f, ambienteDb = -6f, fundido = 0.3f },
            new AmbienteBeat { note = "El coro se apaga bajo los gritos.", loopId = "CORO", sonar = false, fundido = 1.5f },
            new ParallelBeat
            {
                note = "Primero el Mago Oscuro, solo y de cerca (Raúl: «primero uno y luego el otro, y más cerca»): primer plano de su cara en la franja mientras ruge.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                    new CutInBeat { note = "La cara del Mago Oscuro llenando la franja.", actorId = "NPC_MagoOscuro", encuadre = EncuadreCutIn.Cara, lado = LadoCutIn.Derecha, alturaEnPantalla = .5f, duracion = 1.7f, ranura = 0, sonido = "SFX_Prologo_CutIn", esperar = false },
                    new LineasDeConcentracionBeat { note = "Las rayas blancas envuelven los dos gritos.", mostrar = true, duracion = 3.8f, esperar = false },
                    new SfxBeat { note = "El grito del Mago Oscuro.", eventKey = "SFX_Prologo_GritoMago", clip = null, atActorId = "", markName = "", volume = 1.0f },
                    new WaitBeat { note = "Que se lea su cara antes de pasar al Archimago.", seconds = 1.8f, unscaled = true },
                },
            },
            new ParallelBeat
            {
                note = "Después el Archimago gritando «¡Protégelos a todos!» y, mientras grita, el agujero negro y su protección crecen a la vez hasta mezclarse y llenar la pantalla; suena la explosión y queda el blanco (INC-585). Nadie lanza nada.",
                waitForAll = true,
                beats = new List<SequenceBeat>
                {
                        new SayBeat
                        {
                            note = "«¡Protégelos a todos!» con el subtítulo de siempre (Raúl: «lo quitaría de la pantalla y dejaría todos los textos donde siempre»).",
                            actorId = "NPC_Archimago",
                            markName = "",
                            textKey = "PROLOGO_HECHIZO",
                            expresiones = new List<ExpresionDuranteLinea> { new ExpresionDuranteLinea { progreso = 0.3f, expresion = new EmotionBeat { actorId = "NPC_Archimago", emotion = NPCEmotion.Angry, mantener = true } } },
                            pageDuration = 4.0f,
                            gesture = "",
                            gestureRepeats = 1,
                            speakerNameKey = "Archimago",
                            colorDelNombre = ColorDelHablante("Archimago"),
                            playGestures = false,
                            overrideBubbleOffset = false,
                            bubbleOffset = new Vector3(0.0f, 0.0f, 0.0f),
                        },
                    new SerieBeat
                    {
                        note = "La franja del Archimago y, a continuación, el crecimiento de los dos hechizos.",
                        beats = new List<SequenceBeat>
                        {
                            new CutInBeat { note = "La cara del Archimago gritando.", actorId = "NPC_Archimago", encuadre = EncuadreCutIn.Cara, lado = LadoCutIn.Izquierda, alturaEnPantalla = .5f, duracion = 1.9f, ranura = 0, sonido = "SFX_Prologo_CutIn", esperar = false },
                            new WaitBeat { note = "", seconds = 2.0f, unscaled = true },
                            new TimeScaleBeat { note = "El crecimiento va en tiempo real.", timeScale = 1.0f, rampDuration = 0.3f },
                            new ShotBeat
                            {
                                note = "Plano general fijo con los dos y el aire entre ellos: ahí se van a encontrar los hechizos.",
                                shotName = "",
                                smooth = false,
                                duration = 0.6f,
                                waitForArrival = true,
                                live = false,
                                framing = new ShotFraming
                                {
                                    type = ShotType.Wide,
                                    subjectId = "NPC_Archimago",
                                    secondaryId = "NPC_MagoOscuro",
                                    heightBias = 1.0f,
                                    distanceScale = 1.9f,
                                    fovOverride = 0.0f,
                                    crossTheLine = false,
                                    headroom = false,
                                    encara = false,
                                    operador = OperadorDeCamara.Fijo,
                                },
                            },
                            new SfxBeat { note = "La protección nace de su pecho.", eventKey = "Prologo_ProteccionAbsoluta", clip = null, atActorId = "NPC_Archimago", markName = "", volume = 0.9f },
                            new ShakeBeat { note = "La tierra tiembla mientras crecen.", intensity = 0.35f, duration = 4.5f, waitForEnd = false },
                            new FusionDeHechizosBeat
                            {
                                note = "El agujero negro y la luz del Archimago crecen a la vez, se tocan, se mezclan y llenan la pantalla; explosión y blanco sostenido.",
                                efectoARegistrado = "CARGA_ESFERA",
                                prefabB = s_luzProtectora,
                                actorBId = "NPC_Archimago",
                                offsetB = new Vector3(0f, 1.1f, 0f),
                                duracionCrecimiento = 3.4f,
                                duracionMezcla = 2.2f,
                                duracionBlanco = 0.35f,
                                crecimientoA = 2.0f,
                                crecimientoB = 3.2f,
                                eventKeySubida = "SFX_Prologo_Subida",
                                eventKeyExplosion = "Prologue_Explosion",
                                esperar = true,
                            },
                        },
                    },
                },
            },
            new AmbienteBeat { note = "El zumbido del agujero negro muere con él.", loopId = "AGUJERO_NEGRO", sonar = false, fundido = 0.3f },
            new RecogerVfxBeat { note = "", ids = new List<string> { "CARGA_ESFERA", "CARGA_CRECE", "CARGA_REFUERZO" } },
            new MezclaBeat { note = "Vuelve la mezcla normal.", fuente = "grito", quitar = true, fundido = 0.6f },
            new LineasDeConcentracionBeat { note = "", mostrar = false, fundido = 0f, esperar = false },
            new ScreenFadeBeat
            {
                note = "La luz del escudo llena la pantalla y SE QUEDA (prologo24): el prologo acaba en blanco, y de ese blanco sale la manana del cuarto de Will, vista desde sus ojos.",
                fadeIn = true,
                color = Color.white,
                duration = 1.2f,
                waitForEnd = true,
            },
            new TimeScaleBeat
            {
                note = "",
                timeScale = 1.0f,
                rampDuration = 0.0f,
            },
            new WaitBeat
            {
                note = "Medio segundo de negro y de silencio.",
                seconds = 0.6f,
                unscaled = true,
            },
        }),
        Fase("9 - La explosion", "", "", new List<SequenceBeat>
        {
            new AmbienteBeat { note = "La explosión lo apaga todo.", loopId = "TORMENTA", sonar = false, fundido = 0.2f },
            new TimeOfDayBeat
            {
                note = "Y el mundo se queda AMANECIENDO: la pesadilla acaba de noche y Will se despierta con el dia empezando.",
                timeOfDay = DayNightCycle.TimeOfDay.Morning,
                immediate = true,
                waitForTransition = false,
                transitionSeconds = 2.0f,
                esLaHoraDeVolver = true,
            },
            new SfxBeat
            {
                note = "",
                eventKey = "Prologue_WarClashStinger_B",
                clip = null,
                atActorId = "",
                markName = "",
                volume = 1.0f,
            },
            new ShakeBeat
            {
                note = "El mando tiembla aunque no se vea nada. Es lo que hace que el negro sea la explosion y no un corte.",
                intensity = 0.9f,
                duration = 1.6f,
                waitForEnd = false,
            },
            new WaitBeat
            {
                note = "",
                seconds = 2.2f,
                unscaled = true,
            },
            new BandasDeCineBeat { mostrar = false, duracion = 0f },
        }),
    };
}
#endif
