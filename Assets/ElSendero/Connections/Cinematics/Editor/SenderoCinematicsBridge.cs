using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Liyodev.Cinematics;
using Sendero.Narrative.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Liyodev.NarrativeGraph;

/// El enchufe del Director con El Sendero: le contesta lo que la mesa necesita (qué cinemáticas hay,
/// sus fotos, sus frases, sus voces y el parte del último horneado) y hace lo que sabe hacer el juego
/// (hornear con el horneador de siempre, abrir el taller, la página anotable, el storyboard).
///
/// El paquete `Liyodev.Cinematics` no conoce nada de esto: solo ve el contrato.
public sealed class SenderoCinematicsBridge : ICinematicsEditorBridge, ICinematicsLivePreview, ICinematicsPlayControl
{
    private const string CarpetaGuiones = "Assets/_SEQUENCES/Guiones";

    private static Dictionary<string, string> s_textos;

    /// Menú de casa para llegar a la misma mesa sin buscar el de Liyodev.
    [MenuItem("El Sendero/Cinemáticas/Director", false, 18)]
    private static void AbrirDirector() => DirectorWindow.Abrir();

    // ── Lo que se le enseña a la mesa ────────────────────────────────────────────────────────

    public List<CinemaEntry> Listar()
    {
        var lista = new List<CinemaEntry>();
        var porNombre = new Dictionary<string, CinemaEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in AssetDatabase.FindAssets("t:GuionHorneado", new[] { CarpetaGuiones }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var guion = AssetDatabase.LoadAssetAtPath<GuionHorneado>(ruta);
            if (guion == null || string.IsNullOrEmpty(guion.nombre)) continue;

            var entrada = new CinemaEntry
            {
                id = guion.nombre,
                nombre = guion.nombre,
                escena = guion.escena,
                rutaHorneado = ruta,
                rutaGuion = GuionTextoEditable.RutaDelTexto(guion) ?? string.Empty,
                duracion = guion.duracion,
                planos = guion.planos.Count,
                frases = guion.lineas.Count + FrasesDeFueraDeEscena(guion),
            };
            RellenarParte(entrada);
            lista.Add(entrada);
            porNombre[guion.nombre] = entrada;
        }

        // Guiones escritos y aún sin hornear: también son cinemáticas del proyecto.
        foreach (string ruta in GuionesDeTexto())
        {
            string nombre = Path.GetFileName(ruta);
            nombre = nombre.Substring(0, nombre.Length - ".guion.txt".Length);
            if (porNombre.ContainsKey(nombre)) continue;

            var entrada = new CinemaEntry
            {
                id = nombre,
                nombre = nombre,
                escena = EscenaDelTexto(ruta),
                rutaGuion = ruta,
                estado = "Sin hornear",
            };
            RellenarParte(entrada);
            lista.Add(entrada);
        }

        return lista.OrderBy(e => e.nombre, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public CinemaDetail Leer(string id)
    {
        var guion = HorneadoDe(id);
        var entrada = Listar().Find(e => e.id == id) ?? new CinemaEntry { id = id, nombre = id };

        var detalle = new CinemaDetail { entrada = entrada };
        if (guion == null) return detalle;

        detalle.subtitulos = guion.presentacion != PresentacionDeTexto.Bocadillo;
        detalle.carpetaDelStoryboard = CarpetaDelStoryboard(id);

        var textos = Textos();
        var carpeta = CarpetaDeDatos(id);
        float duracion = guion.duracion;

        for (int i = 0; i < guion.planos.Count; i++)
        {
            var plano = guion.planos[i];
            var fotos = FotogramasDe(carpeta, id, i + 1, out string origen);
            detalle.planos.Add(new CinemaShot
            {
                t0 = plano.t0,
                t1 = plano.t1 > plano.t0 ? plano.t1 : duracion,
                descripcion = plano.descripcion,
                imagen = fotos.Count > 0 ? fotos[0] : null,
                fotogramas = fotos,
                elementos = ElementosDe(guion, plano),
                origen = origen,
            });
        }

        foreach (var linea in guion.lineas)
        {
            textos.TryGetValue(linea.clave ?? string.Empty, out string texto);
            detalle.lineas.Add(new CinemaLine
            {
                t0 = linea.t0,
                t1 = linea.t1,
                quien = Quien(guion, linea.actor),
                texto = texto ?? linea.clave,
                clave = linea.clave,
                elemento = linea.actor,
                linea = linea.linea,
            });
        }

        // Las voces que llegan sin que se vea a quien habla (SayBeat fuera de escena) no son
        // LineaHorneada: si no se añaden aquí, no salen en el timeline ni suenan en el flipbook.
        foreach (var efecto in guion.efectos)
        {
            if (!(efecto.beat is SayBeat dice) || string.IsNullOrEmpty(dice.textKey)) continue;

            textos.TryGetValue(dice.textKey, out string dicho);
            string quien = !string.IsNullOrEmpty(dice.speakerNameKey) ? dice.speakerNameKey : dice.markName;
            detalle.lineas.Add(new CinemaLine
            {
                t0 = efecto.t,
                t1 = efecto.t + Mathf.Max(1f, dice.pageDuration),
                quien = quien ?? string.Empty,
                texto = dicho ?? dice.textKey,
                clave = string.IsNullOrEmpty(dice.voiceKey) ? dice.textKey : dice.voiceKey,
                deFueraDeEscena = dice.fueraDeEscena,
                elemento = string.IsNullOrEmpty(dice.markName) ? dice.speakerNameKey : dice.markName,
                linea = efecto.linea,
            });
        }
        detalle.lineas.Sort((a, b) => a.t0.CompareTo(b.t0));
        detalle.pistas = ConstruirPistas(guion, detalle.lineas, PerfilDeAudio());

        var (errores, avisos) = ParteDelInforme(carpeta);
        detalle.errores = errores;
        detalle.avisos = avisos;
        return detalle;
    }

    public AudioClip VozDe(string clave)
    {
        if (string.IsNullOrEmpty(clave)) return null;
        foreach (string extension in new[] { ".mp3", ".wav", ".ogg" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/Voices/es/{clave}{extension}");
            if (clip != null) return clip;
        }
        return Resources.Load<AudioClip>($"Voices/es/{clave}");
    }

    // ── Lo que se le puede pedir ─────────────────────────────────────────────────────────────

    public void Hornear(string id, bool storyboard)
    {
        var entrada = Listar().Find(e => e.id == id);
        if (entrada == null || string.IsNullOrEmpty(entrada.rutaGuion))
            throw new Exception($"No encuentro el .guion.txt de «{id}».");

        // El horneado abre la escena del guion en aditivo; desde la ventana no hay nadie mirándola:
        // se cierra al acabar, igual que hace el buzón de órdenes.
        var abiertasAntes = new HashSet<string>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            abiertasAntes.Add(SceneManager.GetSceneAt(i).path);

        string resumen = HorneadorDeGuion.Hornear(entrada.rutaGuion, storyboard) != null
            ? HorneadorDeGuion.UltimoResumen
            : "no se pudo hornear";

        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (abiertasAntes.Contains(escena.path) || SceneManager.sceneCount < 2) continue;
            EditorSceneManager.CloseScene(escena, true);
        }

        s_textos = null;
        CinematicsEditorServices.AvisarCambio();
        Avisar("Director", $"«{id}»: {resumen}");
    }

    public void AbrirTaller(string id)
    {
        var guion = HorneadoDe(id);
        if (guion == null) throw new Exception($"«{id}» no está horneado: no hay planos que enseñar en el taller.");
        TallerDeRodajeWindow.AbrirCon(guion);
    }

    public void AbrirGuion(string id)
    {
        var entrada = Listar().Find(e => e.id == id);
        if (entrada == null || string.IsNullOrEmpty(entrada.rutaGuion))
            throw new Exception($"No encuentro el .guion.txt de «{id}».");
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(entrada.rutaGuion);
        if (asset != null) AssetDatabase.OpenAsset(asset);
        else EditorUtility.RevealInFinder(RutaFisica(entrada.rutaGuion));
    }

    public void GenerarPaginaAnotable(string id)
    {
        var guion = HorneadoDe(id);
        if (guion == null) throw new Exception($"«{id}» no está horneado: no hay fotos para la página.");
        string hecho = StoryboardAnotable.Generar(guion);
        if (!string.IsNullOrEmpty(hecho)) EditorUtility.RevealInFinder(RutaFisica("Tools/Storyboard"));
    }

    public void AbrirStoryboard(string id)
    {
        string carpeta = CarpetaDelStoryboard(id);
        if (!string.IsNullOrEmpty(carpeta))
        {
            string html = Path.Combine(carpeta, "storyboard.html");
            if (File.Exists(html)) { EditorUtility.RevealInFinder(html); return; }
            if (Directory.Exists(carpeta)) { EditorUtility.RevealInFinder(carpeta); return; }
        }
        throw new Exception($"«{id}» no tiene fotos horneadas: hornéalo con storyboard.");
    }

    public void Avisar(string titulo, string mensaje) => OrdenesDelEditor.Avisar(titulo, mensaje);

    /// La pizarra vive en el proyecto, junto a las correcciones que guarda la página.
    public CinemaPizarra LeerPizarra(string id)
    {
        string ruta = RutaPizarra(id);
        try
        {
            if (File.Exists(ruta))
            {
                var guardada = JsonUtility.FromJson<CinemaPizarra>(File.ReadAllText(ruta));
                if (guardada != null) { guardada.guion = id; return guardada; }
            }
        }
        catch (Exception e) { Debug.LogWarning("[Director] Pizarra ilegible de " + id + ": " + e.Message); }

        return new CinemaPizarra { guion = id };
    }

    public void GuardarPizarra(string id, CinemaPizarra pizarra)
    {
        string ruta = RutaPizarra(id);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        File.WriteAllText(ruta, JsonUtility.ToJson(pizarra, true));
    }

    private static string RutaPizarra(string id)
        => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_revisiones", "pizarra-" + Slug(id) + ".json");

    /// Lo que se puede anotar en un plano: quien sale, quien habla (aunque sea desde fuera de
    /// escena) y la cámara.
    private static List<CinemaElemento> ElementosDe(GuionHorneado guion, PlanoHorneado plano)
    {
        var elementos = new List<CinemaElemento>();

        void Anadir(string id, string etiqueta)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (elementos.Exists(e => e.id == id)) return;
            elementos.Add(new CinemaElemento { id = id, etiqueta = etiqueta });
        }

        foreach (string sujeto in plano.sujetos) Anadir(sujeto, Quien(guion, sujeto));

        foreach (var linea in guion.lineas)
            if (linea.t1 > plano.t0 && linea.t0 < plano.t1) Anadir(linea.actor, Quien(guion, linea.actor));

        foreach (var efecto in guion.efectos)
            if (efecto.beat is SayBeat dice && !string.IsNullOrEmpty(dice.textKey) &&
                efecto.t >= plano.t0 && efecto.t < plano.t1)
                Anadir(string.IsNullOrEmpty(dice.markName) ? dice.speakerNameKey : dice.markName,
                       string.IsNullOrEmpty(dice.speakerNameKey) ? "Voz" : dice.speakerNameKey);

        Anadir("camara", "Cámara");
        return elementos;
    }

    public string[] Escenas()
    {
        var nombres = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            if (!nombres.Contains(nombre)) nombres.Add(nombre);
        }
        nombres.Sort(StringComparer.OrdinalIgnoreCase);
        return nombres.ToArray();
    }

    public string CrearCinematica(string nombre, string escena)
    {
        string limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0) return "Ponle un nombre a la cinemática.";
        foreach (char c in limpio)
            if (!char.IsLetterOrDigit(c) && c != '_') return "El nombre solo puede tener letras, números y guion bajo.";

        string ruta = $"{CarpetaGuiones}/{limpio}.guion.txt";
        if (File.Exists(ruta) || AssetDatabase.LoadAssetAtPath<TextAsset>(ruta) != null)
            return $"Ya existe «{limpio}».";

        string texto =
            $"# {limpio}\n" +
            "# Cinemática nueva: escribe aquí, debajo, lo que pasa. El horneado pone las cámaras y los caminos.\n" +
            $"GUION {limpio}\n" +
            $"ESCENA {(string.IsNullOrEmpty(escena) ? "MainWorld" : escena)}\n" +
            $"SECUENCIA Assets/_SEQUENCES/SEQ_{limpio}.asset\n" +
            "TEXTO subtitulo\n" +
            "RITMO entre_frases=0.4 mismo_hablante=0.25\n" +
            "\n" +
            "REPARTO\n" +
            "will  Player  nombre=Will color=FFD27A prefab=Assets/Prefabs/_WILL.prefab\n" +
            "\n" +
            "PUNTOS\n" +
            "# Un punto por sitio donde se coloca alguien o una cámara (x z), o el nombre de una marca de la escena.\n" +
            "\n" +
            "HILO\n" +
            "@inicio\n" +
            "# will en PUNTO mirando PUNTO\n" +
            "plano medio will\n" +
            "espera 1.0\n";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ruta), texto,
                new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(ruta);
        }
        catch (Exception e)
        {
            return "No pude escribir el guion: " + e.Message;
        }

        s_textos = null;
        CinematicsEditorServices.AvisarCambio();
        return null;
    }

    public ICinematicLiveView AbrirVistaEnVivo(string id, out string error)
    {
        var guion = HorneadoDe(id);
        if (guion == null)
        {
            error = "Esta cinemática aún no está horneada: hornéala para verla en vivo.";
            return null;
        }
        return VistaEnVivoDelGuion.Abrir(guion, out error);
    }

    public bool EnMarcha(out string id, out float segundo, out float duracion, out bool pausada)
    {
        var reproductor = GuionEnMarcha.Actual;
        bool rueda = EditorApplication.isPlaying && reproductor != null && reproductor.EnMarcha && reproductor.Guion != null;
        id = rueda ? reproductor.Guion.nombre : null;
        segundo = rueda ? reproductor.Tiempo : 0f;
        duracion = rueda ? reproductor.Duracion : 0f;
        pausada = rueda && reproductor.Pausado;
        return rueda;
    }

    public void Saltar(float segundo) => GuionEnMarcha.Actual?.PedirSalto(segundo);

    public void Pausar(bool pausar)
    {
        var reproductor = GuionEnMarcha.Actual;
        if (reproductor == null) return;
        if (pausar) reproductor.Pausar();
        else reproductor.Reanudar();
    }

    public bool RodarEnPlay(string id, float desde)    {
        var guion = HorneadoDe(id);
        if (guion == null) { Avisar("Director", $"No encuentro «{id}»."); return false; }

        var definicion = DefinicionDe(guion);
        if (definicion == null)
        {
            Avisar("Director", $"«{guion.nombre}» no tiene SequenceDefinition que lo monte en Play.");
            return false;
        }

        string escena = EscenaDe(guion.escena);
        if (string.IsNullOrEmpty(escena))
        {
            Avisar("Director", $"No encuentro la escena «{guion.escena}» de «{guion.nombre}».");
            return false;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(escena, OpenSceneMode.Single);

        // La escena se prueba como escena, no como partida: el jugador entra por una de sus anclas
        // y la historia no arranca por su cuenta (la apaga ArranqueEnPlayDelDirector con
        // NarrativeQuickStart.SuppressAutoStart). Sin esto, Play en MainWorld carga el guardado
        // (la habitación de Will) y Cap1 levanta el prólogo justo encima de la secuencia que se
        // está probando.
        if (!NarrativeTestLauncher.PrepararEscenaAbierta(out string motivo))
        {
            Avisar("Director", motivo ?? "No pude preparar la prueba de la escena.");
            return false;
        }

        // El arranque lo hace un runner de Play (ArranqueEnPlayDelDirector): emite la señal de
        // entrada de la secuencia, igual que haría el grafo.
        SessionState.SetString("Director.Play.Senal", definicion.signalIn);
        SessionState.SetString("Director.Play.Nombre", guion.nombre);
        SessionState.SetFloat("Director.Play.Desde", Mathf.Max(0f, desde));
        EditorApplication.EnterPlaymode();
        return true;
    }

    /// La secuencia montada para este guion: la que lleva un GuionBeat que lo apunta.
    private static SequenceDefinition DefinicionDe(GuionHorneado guion)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:SequenceDefinition"))
        {
            var definicion = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definicion == null || definicion.phases == null) continue;

            foreach (var fase in definicion.phases)
            {
                if (fase?.beats == null) continue;
                foreach (var beat in fase.beats)
                    if (beat is GuionBeat guionBeat && guionBeat.guion == guion) return definicion;
            }
        }
        return null;
    }

    private static string EscenaDe(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;

        foreach (string guid in AssetDatabase.FindAssets($"{nombre} t:Scene"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(ruta), nombre, StringComparison.OrdinalIgnoreCase))
                return ruta;
        }
        return null;
    }

    /// <summary>
    /// Las correcciones que la página del storyboard ya sabe decir en palabras («alejamos un poco
    /// la cámara») se traducen a lo único que el guion entiende: el `lente=` de la línea del plano.
    /// Lo que no se sabe traducir no se tira: queda como encargo para una sesión de IA, con el plano,
    /// la línea del guion y el fotograma, hasta que se cierra con `revisiones-hechas`.
    /// </summary>
    public CinemaAplicado AplicarRevisiones(string id, List<CinemaNota> notas)
    {
        var respuesta = new CinemaAplicado();
        var guion = HorneadoDe(id);
        if (guion == null)
        {
            var porSlug = Listar().Find(e => Slug(e.nombre) == Slug(id));
            if (porSlug != null) guion = HorneadoDe(porSlug.id);
        }
        if (guion == null) { respuesta.resumen = $"No encuentro el horneado de «{id}»."; return respuesta; }

        string ruta = GuionTextoEditable.RutaDelTexto(guion);
        if (string.IsNullOrEmpty(ruta))
        {
            respuesta.resumen = $"«{guion.nombre}» no tiene .guion.txt: no hay dónde escribir.";
            return respuesta;
        }

        string[] lineasDelGuion = File.Exists(ruta) ? File.ReadAllLines(ruta) : Array.Empty<string>();

        // Cada nota se aplica una sola vez: la página puede volver a mandar el mismo fichero.
        var yaAplicadas = LeerAplicadas(guion.nombre);
        var cola = LeerColaDeEncargos(guion.nombre);
        var hechasAhora = new List<string>();
        var hechos = new List<string>();
        int aplicados = 0;
        int repetidas = 0;

        foreach (var nota in notas ?? new List<CinemaNota>())
        {
            if (nota == null) continue;
            string clave = nota.Clave();
            if (!string.IsNullOrEmpty(clave) && yaAplicadas.Contains(clave))
            {
                repetidas++;
                cola.encargos.RemoveAll(e => e.clave == clave);
                continue;
            }

            int numero = NumeroDePanel(nota.panel);
            var plano = numero >= 1 && numero <= guion.planos.Count ? guion.planos[numero - 1] : null;
            string dicho = (string.IsNullOrEmpty(nota.texto) ? nota.TextoEfectivo() : nota.texto).ToLowerInvariant();
            bool esCamara = string.Equals(nota.elemento, "camara", StringComparison.OrdinalIgnoreCase);

            float? lente = null;
            if (esCamara && plano != null)
            {
                if (TieneAlguna(dicho, "alejar", "aleja", "abrir", "abre", "atras", "atrás", "mas plano", "más plano"))
                    lente = Mathf.Max(12f, plano.lente - 6f);
                else if (TieneAlguna(dicho, "acercar", "acerca", "cerrar", "cierra", "cerca", "primer plano", "detalle"))
                    lente = Mathf.Min(80f, plano.lente + 8f);
            }

            if (lente == null)
            {
                if (string.IsNullOrEmpty(clave))
                {
                    hechos.Add($"{nota.panel}: sin texto ni dibujo, no hay nada que encargar");
                    continue;
                }

                Encolar(cola, guion, ruta, lineasDelGuion, numero, plano, nota);
                hechos.Add($"{nota.panel}: para la IA");
                continue;
            }

            if (plano.linea <= 0)
            {
                hechos.Add($"{nota.panel}: horneado viejo, sin línea (rehornea primero)");
                continue;
            }

            string valor = lente.Value.ToString("0.#", CultureInfo.InvariantCulture);
            var cambios = new Dictionary<string, string> { ["lente"] = valor };
            if (GuionTextoEditable.ReescribirOpciones(ruta, plano.linea, cambios, out _))
            {
                aplicados++;
                hechos.Add($"{nota.panel} L{plano.linea}: lente {plano.lente:0.#} → {valor}");
                if (!string.IsNullOrEmpty(clave)) { hechasAhora.Add(clave); respuesta.claves.Add(clave); }
                cola.encargos.RemoveAll(e => e.clave == clave);
            }
            else
            {
                Encolar(cola, guion, ruta, lineasDelGuion, numero, plano, nota);
                hechos.Add($"{nota.panel}: no pude escribir la línea; para la IA");
            }
        }

        if (hechasAhora.Count > 0)
        {
            GuardarAplicadas(guion.nombre, yaAplicadas, hechasAhora);
            QuitarDeLaPizarra(guion.nombre, hechasAhora);
        }

        if (aplicados > 0)
        {
            Hornear(guion.nombre, true);
            var recien = HorneadoDe(guion.nombre);
            if (recien != null)
            {
                StoryboardAnotable.Generar(recien);

                // La página vieja del navegador ya no vale: se abre la recién hecha para seguir ahí.
                string pagina = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "Tools/Storyboard", recien.nombre + ".html");
                if (File.Exists(pagina)) Application.OpenURL("file:///" + pagina.Replace('\\', '/'));
            }
        }

        cola.guion = guion.nombre;
        cola.escena = guion.escena;
        cola.rutaGuion = ruta;
        GuardarColaDeEncargos(cola);

        respuesta.pendientes = cola.encargos.Count;
        respuesta.rutaEncargos = cola.encargos.Count > 0 ? RutaEncargosMd(guion.nombre) : string.Empty;

        string resumen = aplicados > 0 ? $"{aplicados} corrección(es) aplicadas y rehorneado" : string.Empty;
        if (repetidas > 0)
            resumen += (resumen.Length > 0 ? " · " : string.Empty) + $"{repetidas} ya aplicada(s) antes";
        if (cola.encargos.Count > 0)
        {
            string donde = "Guiones/_revisiones/encargos-" + Slug(guion.nombre) + ".md";
            resumen += (resumen.Length > 0 ? " · " : string.Empty) +
                       $"{cola.encargos.Count} para la IA en {donde} (buzón: «encargos {guion.nombre}» · cerrar: «revisiones-hechas {guion.nombre}»)";
        }
        if (hechos.Count > 0)
            resumen += (resumen.Length > 0 ? " · " : string.Empty) + string.Join(" · ", hechos);

        respuesta.resumen = resumen.Length > 0 ? resumen : "No había nada que aplicar.";
        return respuesta;
    }

    // ── Lectura para el asistente de IA ───────────────────────────────────────────────────────

    public string ManualDeGuion() => s_manualDeGuion ??= GuionManual.Generar();

    private static string s_manualDeGuion;

    public string EstadoParaAsistente(string id, int plano = -1, float segundo = -1f)
    {
        var guion = HorneadoDe(id);
        if (guion == null) return $"No encuentro la cinemática «{id}».";

        var detalle = Leer(id);
        var sb = new StringBuilder();
        sb.AppendLine($"# Cinemática «{guion.nombre}»");
        sb.AppendLine();
        sb.AppendLine($"- Escena: {guion.escena}");
        string rutaGuion = GuionTextoEditable.RutaDelTexto(guion);
        if (!string.IsNullOrEmpty(rutaGuion)) sb.AppendLine($"- Guion: {rutaGuion}");
        string estado = detalle?.entrada?.estado;
        if (!string.IsNullOrEmpty(estado)) sb.AppendLine($"- Estado: {estado}");
        sb.AppendLine($"- Duración: {guion.duracion:0.0} s · {guion.planos.Count} planos");
        if (plano >= 0 && plano < guion.planos.Count)
        {
            var p = guion.planos[plano];
            sb.AppendLine($"- Foco actual en el Director: P{plano + 1:00} ({p.t0:0.0}–{p.t1:0.0} s, línea L{p.linea})");
        }
        if (segundo >= 0f) sb.AppendLine($"- Cabezal del Director: {segundo:0.0} s");

        var cola = LeerColaDeEncargos(id);
        if (cola.encargos.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## Encargos pendientes de la IA ({cola.encargos.Count})");
            foreach (var encargo in cola.encargos)
            {
                string cuando = encargo.t >= 0f
                    ? $" t={encargo.t:0.0}"
                    : $" {encargo.t0:0.0}–{encargo.t1:0.0}";
                sb.AppendLine($"- {encargo.panel} · {encargo.etiquetaElemento} · «{encargo.texto}»{cuando} · L{encargo.linea}");
                if (!string.IsNullOrEmpty(encargo.ordenGuion))
                    sb.AppendLine($"  - orden: {encargo.ordenGuion.Trim()}");
            }
        }

        var pizarra = LeerPizarra(id);
        var notas = pizarra?.notas;
        if (notas != null && notas.Count > 0)
        {
            var pendientes = notas.Where(n => !string.IsNullOrWhiteSpace(n.Describir())).ToList();
            if (pendientes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"## Notas y marcas de la mesa ({pendientes.Count})");
                sb.AppendLine("Las marcas van en coordenadas de la imagen del plano (0..1, (0, 0) arriba a la izquierda), " +
                              "en el segundo que dicen: «señala» es un punto («aquí»), «dirección» va de dónde a dónde y " +
                              "«dibujo» es una forma a mano (un efecto, un camino). Son el contexto de lo que se pide.");
                foreach (var nota in pendientes)
                {
                    string cuando = nota.t >= 0f ? $" t={nota.t:0.0}" : string.Empty;
                    sb.AppendLine($"- {nota.panel} · {nota.elemento} · «{nota.Describir()}»{cuando}");
                }
            }
        }

        string carpeta = CarpetaDeDatos(id);
        if (!string.IsNullOrEmpty(carpeta))
        {
            string informe = Path.Combine(carpeta, "informe.md");
            if (File.Exists(informe))
            {
                string texto = File.ReadAllText(informe);
                int corte = texto.IndexOf("## Qué hace cada personaje", StringComparison.Ordinal);
                if (corte > 0) texto = texto.Substring(0, corte);
                sb.AppendLine();
                sb.AppendLine("## Último horneado");
                sb.AppendLine(texto.Trim());
            }
        }

        if (guion.actores != null && guion.actores.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Estados de animación disponibles");
            foreach (var actor in guion.actores)
            {
                var estados = EstadosDe(actor.prefab);
                if (estados.Count == 0) continue;
                sb.AppendLine($"- {actor.alias ?? actor.id}: {string.Join(", ", estados)}");
            }
        }

        if (!string.IsNullOrEmpty(rutaGuion))
        {
            string fisica = RutaFisica(rutaGuion);
            if (File.Exists(fisica))
            {
                sb.AppendLine();
                sb.AppendLine("## Guion (números de línea)");
                string[] lineas = File.ReadAllLines(fisica);
                for (int i = 0; i < lineas.Length; i++)
                    sb.AppendLine($"L{i + 1}: {lineas[i]}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public CinemaEdicionResultado AplicarEdicionDeGuion(string id, string jsonOps)
    {
        var respuesta = new CinemaEdicionResultado();
        var guion = HorneadoDe(id);
        if (guion == null)
        {
            respuesta.errores.Add($"No encuentro «{id}».");
            respuesta.resumen = "No se aplicó nada.";
            return respuesta;
        }

        var resultado = GuionBatchEdit.Aplicar(guion, jsonOps);
        respuesta.aplicado = resultado.Applied;
        respuesta.resumen = resultado.Resumen;
        respuesta.hechos.AddRange(resultado.Done);
        respuesta.diff.AddRange(resultado.Diff);
        respuesta.avisos.AddRange(resultado.Warnings);
        respuesta.errores.AddRange(resultado.Errors);
        return respuesta;
    }

    private static readonly Dictionary<string, List<string>> s_estadosDePrefab = new();

    private static List<string> EstadosDe(string ruta)
    {
        if (string.IsNullOrEmpty(ruta)) return new List<string>();
        if (s_estadosDePrefab.TryGetValue(ruta, out var estados)) return estados;

        estados = new List<string>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if (prefab != null)
        {
            foreach (var estado in CapturaDeEscenario.Catalogo(prefab))
            {
                if (estado.capa != 0 || estado.mezcla || string.IsNullOrEmpty(estado.estado)) continue;
                if (!estados.Contains(estado.estado)) estados.Add(estado.estado);
                if (estados.Count >= 60) break;
            }
        }
        s_estadosDePrefab[ruta] = estados;
        return estados;
    }

    // ── Encargos para la IA ──────────────────────────────────────────────────────────────────

    /// <summary>Lo que no se aplica solo no se pierde: se queda aquí, con el plano y la línea.</summary>
    private static void Encolar(CinemaColaDeEncargos cola, GuionHorneado guion, string ruta,
        string[] lineas, int numero, PlanoHorneado plano, CinemaNota nota)
    {
        string clave = nota.Clave();
        if (string.IsNullOrEmpty(clave)) return;

        var encargo = cola.encargos.Find(e => e.clave == clave);
        if (encargo == null)
        {
            encargo = new CinemaEncargo { clave = clave, fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            cola.encargos.Add(encargo);
        }

        encargo.panel = plano != null ? $"P{numero:00}" : (string.IsNullOrEmpty(nota.panel) ? "P00" : nota.panel);
        encargo.elemento = string.IsNullOrEmpty(nota.elemento) ? "general" : nota.elemento;
        encargo.etiquetaElemento = EtiquetaDeElemento(guion, plano, encargo.elemento);
        encargo.tipo = string.IsNullOrEmpty(nota.tipo) ? "nota" : nota.tipo;
        encargo.texto = nota.TextoEfectivo();
        encargo.t = nota.t;
        encargo.t0 = plano?.t0 ?? 0f;
        encargo.t1 = plano?.t1 ?? 0f;
        encargo.linea = plano?.linea ?? LineaDelEfectoEn(guion, nota.t);
        encargo.ordenGuion = LineaDeTexto(lineas, encargo.linea);
        encargo.rutaGuion = ruta;
        encargo.imagen = FotoDelPlano(guion.nombre, numero);
    }

    private static string EtiquetaDeElemento(GuionHorneado guion, PlanoHorneado plano, string elemento)
    {
        if (string.IsNullOrEmpty(elemento) || string.Equals(elemento, "general", StringComparison.OrdinalIgnoreCase))
            return "Plano / general";
        if (plano != null)
        {
            var encontrado = ElementosDe(guion, plano).Find(e => e.id == elemento);
            if (encontrado != null) return encontrado.etiqueta;
        }
        return elemento;
    }

    private static string LineaDeTexto(string[] lineas, int numero)
        => numero > 0 && lineas != null && numero <= lineas.Length
            ? $"L{numero}: {lineas[numero - 1].Trim()}"
            : string.Empty;

    private static string FotoDelPlano(string guion, int numero)
    {
        if (numero < 1) return string.Empty;
        string carpeta = CarpetaDelStoryboard(guion);
        if (string.IsNullOrEmpty(carpeta)) return string.Empty;

        string archivo = $"{numero:000}.png";
        try
        {
            string[] fotos = Directory.GetFiles(carpeta, $"{numero:000}*.png");
            Array.Sort(fotos, StringComparer.Ordinal);
            if (fotos.Length > 0) archivo = Path.GetFileName(fotos[fotos.Length - 1]);
        }
        catch
        {
            // Sin permiso o carpeta rara: se queda el póster de siempre.
        }

        string ruta = Path.Combine(carpeta, archivo);
        if (!File.Exists(ruta)) return string.Empty;

        string raiz = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetRelativePath(raiz, ruta).Replace('\\', '/');
    }

    private static string CarpetaRevisiones
        => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_revisiones");

    private static string RutaEncargosJson(string guion)
        => Path.Combine(CarpetaRevisiones, "encargos-" + Slug(guion) + ".json");

    private static string RutaEncargosMd(string guion)
        => Path.Combine(CarpetaRevisiones, "encargos-" + Slug(guion) + ".md");

    public int EncargosPendientes(string id) => LeerColaDeEncargos(id).encargos.Count;

    public void AbrirEncargos(string id)
    {
        var cola = LeerColaDeEncargos(id);
        if (cola.encargos.Count == 0)
        {
            Avisar("Director · encargos", $"«{id}» no tiene correcciones pendientes para la IA.");
            return;
        }

        GuardarColaDeEncargos(cola);
        string ruta = RutaEncargosMd(cola.guion);
        if (File.Exists(ruta)) EditorUtility.RevealInFinder(ruta);
    }

    public string TextoDeEncargos(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            var listado = new StringBuilder();
            if (Directory.Exists(CarpetaRevisiones))
                foreach (string archivo in Directory.GetFiles(CarpetaRevisiones, "encargos-*.json"))
                {
                    var cola = LeerColaDeUnArchivo(archivo);
                    if (cola == null || cola.encargos.Count == 0) continue;
                    listado.Append("«").Append(cola.guion).Append("»: ")
                           .Append(cola.encargos.Count).Append(" pendiente(s)\n");
                }
            return listado.Length > 0 ? listado.ToString().TrimEnd() : "No hay encargos pendientes para la IA.";
        }

        var pedida = LeerColaDeEncargos(id);
        if (pedida.encargos.Count == 0) return $"«{id}»: 0 encargos pendientes.";

        GuardarColaDeEncargos(pedida);
        string ruta = RutaEncargosMd(pedida.guion);
        return File.Exists(ruta) ? File.ReadAllText(ruta) : TextoDeLaCola(pedida);
    }

    private static CinemaColaDeEncargos LeerColaDeEncargos(string id)
    {
        string ruta = RutaEncargosJson(id);
        var cola = File.Exists(ruta) ? LeerColaDeUnArchivo(ruta) : null;
        if (cola == null) return new CinemaColaDeEncargos { guion = id };
        if (string.IsNullOrEmpty(cola.guion)) cola.guion = id;
        return cola;
    }

    private static CinemaColaDeEncargos LeerColaDeUnArchivo(string ruta)
    {
        try
        {
            var cola = JsonUtility.FromJson<CinemaColaDeEncargos>(File.ReadAllText(ruta));
            if (cola == null) return null;

            cola.encargos ??= new List<CinemaEncargo>();
            if (string.IsNullOrEmpty(cola.guion))
                cola.guion = Path.GetFileNameWithoutExtension(ruta).Replace("encargos-", string.Empty);

            var aplicadas = LeerAplicadas(cola.guion);
            cola.encargos.RemoveAll(e => e == null || string.IsNullOrEmpty(e.clave) || aplicadas.Contains(e.clave));
            return cola;
        }
        catch
        {
            return null;
        }
    }

    private static void GuardarColaDeEncargos(CinemaColaDeEncargos cola)
    {
        if (cola == null) return;

        string guion = string.IsNullOrEmpty(cola.guion) ? "guion" : cola.guion;
        string json = RutaEncargosJson(guion);
        string md = RutaEncargosMd(guion);

        var aplicadas = LeerAplicadas(guion);
        var vistas = new HashSet<string>();
        cola.encargos ??= new List<CinemaEncargo>();
        cola.encargos.RemoveAll(e => e == null || string.IsNullOrEmpty(e.clave) ||
                                     aplicadas.Contains(e.clave) || !vistas.Add(e.clave));

        try
        {
            if (cola.encargos.Count == 0)
            {
                if (File.Exists(json)) File.Delete(json);
                if (File.Exists(md)) File.Delete(md);
                return;
            }

            cola.actualizado = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            Directory.CreateDirectory(CarpetaRevisiones);
            File.WriteAllText(json, JsonUtility.ToJson(cola, true), new UTF8Encoding(false));
            File.WriteAllText(md, TextoDeLaCola(cola), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Director] No pude guardar los encargos de " + guion + ": " + e.Message);
        }
    }

    private static string TextoDeLaCola(CinemaColaDeEncargos cola)
    {
        var sb = new StringBuilder();
        sb.Append("# Encargos para la IA · ").Append(cola.guion ?? "cinemática").Append("\n\n");
        if (!string.IsNullOrEmpty(cola.rutaGuion)) sb.Append("- **Guion:** `").Append(cola.rutaGuion).Append("`\n");
        if (!string.IsNullOrEmpty(cola.escena)) sb.Append("- **Escena:** `").Append(cola.escena).Append("`\n");
        sb.Append("- **Pendientes:** ").Append(cola.encargos.Count).Append('\n');
        sb.Append("- **Actualizado:** ").Append(cola.actualizado ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\n\n");
        sb.Append("> Vista generada desde `encargos-").Append(Slug(cola.guion ?? "guion"))
          .Append(".json` (no se edita a mano).\n");
        sb.Append("> Al terminar, se cierran por el buzón: `revisiones-hechas ")
          .Append(cola.guion).Append("`\n");
        sb.Append("> (o `revisiones-hechas ").Append(cola.guion)
          .Append(" P06 P02` para cerrar solo esos planos).\n\n");

        for (int i = 0; i < cola.encargos.Count; i++)
        {
            var e = cola.encargos[i];
            sb.Append("## ").Append(i + 1).Append(". ").Append(e.panel).Append(" · ").Append(e.etiquetaElemento);
            if (e.t0 > 0f || e.t1 > 0f)
                sb.Append(" · ").Append(Reloj(e.t0)).Append(" – ").Append(Reloj(e.t1));
            sb.Append('\n');

            if (e.t >= 0f) sb.Append("- **Segundo en el timeline:** ").Append(Reloj(e.t)).Append('\n');
            sb.Append("- **Pedido:** ").Append(e.texto).Append('\n');
            if (!string.IsNullOrEmpty(e.ordenGuion)) sb.Append("- **Línea del guion:** `").Append(e.ordenGuion).Append("`\n");
            if (!string.IsNullOrEmpty(e.imagen)) sb.Append("- **Fotograma:** `").Append(e.imagen).Append("`\n");
            sb.Append("- **Clave:** `").Append(e.clave).Append("`\n");
            if (!string.IsNullOrEmpty(e.fecha)) sb.Append("- **Fecha:** ").Append(e.fecha).Append('\n');
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string Reloj(float segundos)
    {
        int minutos = (int)(segundos / 60f);
        float resto = segundos - minutos * 60f;
        return $"{minutos}:{resto.ToString("00.0", CultureInfo.GetCultureInfo("es-ES"))}";
    }

    /// <summary>Cierra encargos sin aplicar nada: los apunta para que no vuelvan a salir.</summary>
    private static void QuitarDeLaPizarra(string guion, List<string> claves)
    {
        if (claves == null || claves.Count == 0) return;

        try
        {
            string ruta = RutaPizarra(guion);
            if (!File.Exists(ruta)) return;

            var pizarra = JsonUtility.FromJson<CinemaPizarra>(File.ReadAllText(ruta));
            if (pizarra?.notas == null || pizarra.notas.Count == 0) return;

            int antes = pizarra.notas.Count;
            var conjunto = new HashSet<string>(claves);
            pizarra.notas.RemoveAll(n => conjunto.Contains(CinemaNota.CalcularClave(n.panel, n.elemento, n.Describir()))
                                     || conjunto.Contains(CinemaNota.CalcularClave(n.panel, n.elemento, n.TextoEfectivo())));
            if (pizarra.notas.Count == antes) return;

            if (pizarra.notas.Count == 0) File.Delete(ruta);
            else File.WriteAllText(ruta, JsonUtility.ToJson(pizarra, true), new UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Director] No pude limpiar la pizarra de " + guion + ": " + e.Message);
        }
    }

    [Serializable]
    private class LoteDeRevisiones
    {
        public string storyboard;
        public string guion;
        public List<CinemaNota> anotaciones;
        public List<CinemaEncargo> encargos;
    }

    /// <summary>
    /// Cierra encargos ya hechos: los apunta en el libro de aplicadas para que no vuelvan a salir ni
    /// en la cola ni en la pizarra. Lo usa el buzón al terminar una tanda de la IA. Acepta un guion,
    /// o un .json de correcciones/encargos, y claves o números de plano sueltos.
    /// </summary>
    public static string MarcarRevisionesHechas(string argumento)
    {
        if (string.IsNullOrWhiteSpace(argumento))
            return "uso: revisiones-hechas <guion|ruta.json> [clave|P01…]";

        var partes = new List<string>(argumento.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        for (int i = 0; i < partes.Count; i++) partes[i] = partes[i].Trim('"');

        string primero = partes[0];
        partes.RemoveAt(0);

        string guion = primero;
        var delFichero = new List<(string clave, string panel)>();
        if (primero.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(primero))
        {
            try
            {
                var lote = JsonUtility.FromJson<LoteDeRevisiones>(File.ReadAllText(primero));
                if (lote != null)
                {
                    if (lote.anotaciones != null)
                        foreach (var nota in lote.anotaciones)
                        {
                            string clave = nota?.Clave();
                            if (!string.IsNullOrEmpty(clave)) delFichero.Add((clave, nota.panel));
                        }
                    if (lote.encargos != null)
                        foreach (var encargo in lote.encargos)
                            if (encargo != null && !string.IsNullOrEmpty(encargo.clave))
                                delFichero.Add((encargo.clave, encargo.panel));

                    guion = !string.IsNullOrEmpty(lote.storyboard) ? lote.storyboard : lote.guion;
                }
            }
            catch (Exception e)
            {
                return "No puedo leer ese fichero: " + e.Message;
            }
        }

        if (string.IsNullOrEmpty(guion)) return "No sé de qué cinemática es ese fichero.";

        var cola = LeerColaDeEncargos(guion);
        var marcadas = new List<string>();

        bool Coincide(string clave, string panel, string filtro)
            => string.Equals(clave, filtro, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(panel, filtro, StringComparison.OrdinalIgnoreCase);

        if (partes.Count == 0)
        {
            foreach (var encargo in cola.encargos) marcadas.Add(encargo.clave);
            foreach (var (clave, _) in delFichero) marcadas.Add(clave);
        }
        else
        {
            foreach (var encargo in cola.encargos)
                if (partes.Any(p => Coincide(encargo.clave, encargo.panel, p))) marcadas.Add(encargo.clave);

            foreach (var (clave, panel) in delFichero)
                if (partes.Any(p => Coincide(clave, panel, p))) marcadas.Add(clave);
        }

        marcadas = marcadas.Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        if (marcadas.Count == 0)
            return $"«{guion}»: no había encargos que coincidan con eso.";

        GuardarAplicadas(guion, LeerAplicadas(guion), marcadas);
        QuitarDeLaPizarra(guion, marcadas);

        cola.encargos.RemoveAll(e => marcadas.Contains(e.clave));
        if (string.IsNullOrEmpty(cola.guion)) cola.guion = guion;
        GuardarColaDeEncargos(cola);

        CinematicsEditorServices.AvisarCambio();
        return $"«{guion}»: {marcadas.Count} revisión(es) marcada(s) como hecha(s) · " +
               $"{cola.encargos.Count} pendiente(s).";
    }


    [Serializable]
    private class NotasAplicadas
    {
        public List<string> ids = new();
    }

    private static string RutaAplicadas(string guion)
        => Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_revisiones", "aplicadas-" + Slug(guion) + ".json");

    private static List<string> LeerAplicadas(string guion)
    {
        try
        {
            string ruta = RutaAplicadas(guion);
            if (!File.Exists(ruta)) return new List<string>();
            var leidas = JsonUtility.FromJson<NotasAplicadas>(File.ReadAllText(ruta));
            return leidas?.ids ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void GuardarAplicadas(string guion, List<string> antes, List<string> nuevas)
    {
        try
        {
            string ruta = RutaAplicadas(guion);
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var todas = new NotasAplicadas { ids = new List<string>(antes) };
            foreach (string id in nuevas)
                if (!todas.ids.Contains(id)) todas.ids.Add(id);
            File.WriteAllText(ruta, JsonUtility.ToJson(todas, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Director] No pude apuntar las notas aplicadas de " + guion + ": " + e.Message);
        }
    }

    private static bool TieneAlguna(string texto, params string[] palabras)
    {
        foreach (string palabra in palabras)
            if (texto.Contains(palabra, StringComparison.Ordinal)) return true;
        return false;
    }

    /// El número de plano de «P06». Otros nombres (los tramos sin plano de la mesa, «T01») no son
    /// planos: devuelven 0.
    /// Una nota sin plano (un tramo que monta un efecto): la línea del último efecto que empieza
    /// antes de ese segundo, para que el encargo diga dónde mirar en el guion.
    private static int LineaDelEfectoEn(GuionHorneado guion, float t)
    {
        if (t < 0f || guion?.efectos == null) return 0;
        int linea = 0;
        float mejor = float.MinValue;
        foreach (var efecto in guion.efectos)
            if (efecto != null && efecto.t <= t + 0.01f && efecto.t >= mejor && efecto.linea > 0)
            {
                mejor = efecto.t;
                linea = efecto.linea;
            }
        return linea;
    }

    private static int NumeroDePanel(string panel)
    {
        if (string.IsNullOrEmpty(panel) || char.ToUpperInvariant(panel[0]) != 'P') return 0;
        string digitos = new string(panel.Where(char.IsDigit).ToArray());
        return int.TryParse(digitos, out int numero) ? numero : 0;
    }

    private static string Slug(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var limpio = new System.Text.StringBuilder(texto.Length);
        foreach (char c in texto.ToLowerInvariant())
            limpio.Append(char.IsLetterOrDigit(c) ? c : '-');
        return System.Text.RegularExpressions.Regex.Replace(limpio.ToString(), "-+", "-").Trim('-');
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    private static GuionHorneado HorneadoDe(string id)
    {
        string directa = $"{CarpetaGuiones}/{id}_Horneado.asset";
        var guion = AssetDatabase.LoadAssetAtPath<GuionHorneado>(directa);
        if (guion != null) return guion;

        foreach (string guid in AssetDatabase.FindAssets("t:GuionHorneado", new[] { CarpetaGuiones }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var candidato = AssetDatabase.LoadAssetAtPath<GuionHorneado>(ruta);
            if (candidato != null && string.Equals(candidato.nombre, id, StringComparison.OrdinalIgnoreCase))
                return candidato;
        }
        return null;
    }

    private static IEnumerable<string> GuionesDeTexto()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { CarpetaGuiones }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.EndsWith(".guion.txt", StringComparison.OrdinalIgnoreCase)) yield return ruta;
        }
    }

    private static string EscenaDelTexto(string rutaAsset)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(rutaAsset);
        if (asset == null) return string.Empty;

        foreach (string cruda in asset.text.Split('\n'))
        {
            string linea = cruda.Trim();
            if (linea.StartsWith("ESCENA ", StringComparison.Ordinal)) return linea.Substring(7).Trim();
            if (linea.StartsWith("GUION ", StringComparison.Ordinal)) break;
        }
        return string.Empty;
    }

    private void RellenarParte(CinemaEntry entrada)
    {
        if (string.IsNullOrEmpty(entrada.estado))
            entrada.estado = Estado(entrada.rutaGuion, entrada.rutaHorneado);

        var (errores, avisos) = ParteDelInforme(CarpetaDeDatos(entrada.id));
        entrada.errores = errores.Count;
        entrada.avisos = avisos.Count;
        entrada.encargos = EncargosPendientes(entrada.id);
    }

    private static string Estado(string rutaGuion, string rutaHorneado)
    {
        if (string.IsNullOrEmpty(rutaHorneado)) return "Sin hornear";
        if (string.IsNullOrEmpty(rutaGuion)) return "Al día";

        try
        {
            var texto = new FileInfo(RutaFisica(rutaGuion)).LastWriteTimeUtc;
            var horneado = new FileInfo(RutaFisica(rutaHorneado)).LastWriteTimeUtc;
            return texto > horneado.AddSeconds(1) ? "Texto cambiado" : "Al día";
        }
        catch
        {
            return string.Empty;
        }
    }

    /// La carpeta donde el horneado dejó sus cosas: `Guiones/<escena>/<guion>/`.
    private static string CarpetaDeDatos(string nombre)
    {
        string raiz = CapturaDeEscenario.CarpetaDeGuiones;
        try
        {
            foreach (string carpeta in Directory.GetDirectories(raiz, nombre, SearchOption.AllDirectories))
                if (File.Exists(Path.Combine(carpeta, "informe.md"))) return carpeta;
            foreach (string carpeta in Directory.GetDirectories(raiz, nombre, SearchOption.AllDirectories))
                if (Directory.Exists(Path.Combine(carpeta, "storyboard"))) return carpeta;
        }
        catch
        {
            // Sin permiso en alguna carpeta: se sigue sin parte.
        }
        return null;
    }

    private static string CarpetaDelStoryboard(string nombre)
    {
        string datos = CarpetaDeDatos(nombre);
        if (string.IsNullOrEmpty(datos)) return null;
        string storyboard = Path.Combine(datos, "storyboard");
        return Directory.Exists(storyboard) ? storyboard : null;
    }

    /// Las fotos del plano: el póster y los fotogramas del animatic que dejó el horneado.
    private static List<string> FotogramasDe(string carpeta, string nombre, int numero, out string origen)
    {
        var fotos = new List<string>();
        origen = "storyboard";

        if (!string.IsNullOrEmpty(carpeta))
        {
            string carpetaFotos = Path.Combine(carpeta, "storyboard");
            string poster = Path.Combine(carpetaFotos, $"{numero:000}.png");
            if (File.Exists(poster))
            {
                fotos.Add(poster);
                for (int k = 2; k <= 9; k++)
                {
                    string extra = Path.Combine(carpetaFotos, $"{numero:000}_{k:00}.png");
                    if (File.Exists(extra)) fotos.Add(extra);
                }
                return fotos;
            }
        }

        origen = "previz";
        string previz = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "previz", nombre, $"{numero:000}.png");
        if (File.Exists(previz)) fotos.Add(previz);
        return fotos;
    }

    private static (List<string> errores, List<string> avisos) ParteDelInforme(string carpeta)
    {
        var errores = new List<string>();
        var avisos = new List<string>();
        if (string.IsNullOrEmpty(carpeta)) return (errores, avisos);

        string ruta = Path.Combine(carpeta, "informe.md");
        if (!File.Exists(ruta)) return (errores, avisos);

        List<string> destino = null;
        foreach (string cruda in File.ReadAllLines(ruta))
        {
            string linea = cruda.TrimEnd();
            if (linea.StartsWith("## ", StringComparison.Ordinal))
            {
                if (linea.StartsWith("## Errores", StringComparison.Ordinal)) destino = errores;
                else if (linea.StartsWith("## Avisos", StringComparison.Ordinal)) destino = avisos;
                else destino = null;
                continue;
            }
            if (destino == null) continue;
            if (linea.StartsWith("- ", StringComparison.Ordinal))
                destino.Add(linea.Substring(2).Trim());
        }
        return (errores, avisos);
    }

    // ── El timeline ─────────────────────────────────────────────────────────────────────────

    /// Una fila por cosa: la cámara, cada personaje (voz, movimiento, gestos, presencia), y los
    /// efectos agrupados por lo que son. Es lo que el horneado sabe de verdad, sin inventar nada.
    private static List<CinemaTrack> ConstruirPistas(GuionHorneado guion, List<CinemaLine> lineas, AudioGraphProfile perfil)
    {
        var pistas = new List<CinemaTrack>();

        var camara = new CinemaTrack { nombre = "Cámara", tipo = CinemaTrackKind.Camara };
        for (int i = 0; i < guion.planos.Count; i++)
            camara.clips.Add(new CinemaClip
            {
                t0 = guion.planos[i].t0,
                t1 = guion.planos[i].t1,
                etiqueta = $"P{i + 1:00} {guion.planos[i].descripcion}",
                linea = guion.planos[i].linea,
                elemento = "camara",
            });
        if (camara.clips.Count > 0) pistas.Add(camara);

        foreach (var grupo in lineas.GroupBy(l => string.IsNullOrEmpty(l.quien) ? "Voz" : l.quien))
        {
            var pista = new CinemaTrack { nombre = "Voz · " + grupo.Key, tipo = CinemaTrackKind.Voz };
            foreach (var linea in grupo)
                pista.clips.Add(new CinemaClip
                {
                    t0 = linea.t0,
                    t1 = linea.t1,
                    etiqueta = Recortar(linea.texto, 70),
                    linea = linea.linea,
                    elemento = linea.elemento,
                });
            pistas.Add(pista);
        }

        foreach (var actor in guion.actores)
        {
            string nombre = Mayuscula(actor.alias ?? actor.id ?? "actor");

            var mover = new CinemaTrack { nombre = "Movimiento · " + nombre, tipo = CinemaTrackKind.Movimiento };
            for (int i = 1; i < actor.posiciones.Count; i++)
            {
                var desde = actor.posiciones[i - 1];
                var hasta = actor.posiciones[i];
                if ((hasta.p - desde.p).sqrMagnitude < 0.0004f) continue;
                mover.clips.Add(new CinemaClip { t0 = desde.t, t1 = hasta.t, etiqueta = "anda", elemento = actor.id });
            }
            if (actor.deslizando != null)
                foreach (var tramo in actor.deslizando)
                    mover.clips.Add(new CinemaClip { t0 = tramo.desde, t1 = tramo.hasta, etiqueta = "se desliza", elemento = actor.id });
            if (mover.clips.Count > 0) pistas.Add(mover);

            var animacion = new CinemaTrack { nombre = "Animación · " + nombre, tipo = CinemaTrackKind.Gesto };
            for (int i = 0; i < actor.animacion.Count; i++)
            {
                var orden = actor.animacion[i];
                float fin = i + 1 < actor.animacion.Count ? actor.animacion[i + 1].t : guion.duracion;
                if (orden.tipo == TipoDeAnimacion.Gesto || orden.tipo == TipoDeAnimacion.HablaEmpieza ||
                    orden.tipo == TipoDeAnimacion.HablaTermina || orden.tipo == TipoDeAnimacion.FinDeGesto ||
                    orden.tipo == TipoDeAnimacion.SoltarPoseAlta)
                    fin = Mathf.Min(fin, orden.t + 0.4f);

                string que;
                if (!string.IsNullOrEmpty(orden.estado)) que = orden.estado;
                else if (orden.tipo == TipoDeAnimacion.HablaEmpieza) que = "habla";
                else if (orden.tipo == TipoDeAnimacion.HablaTermina) que = "calla";
                else if (orden.tipo == TipoDeAnimacion.Reposo) que = "reposo";
                else continue;

                animacion.clips.Add(new CinemaClip
                {
                    t0 = orden.t,
                    t1 = Mathf.Max(orden.t + 0.25f, fin),
                    etiqueta = que,
                    elemento = actor.id,
                    clase = ClaseDe(orden),
                });
            }
            if (animacion.clips.Count > 0) pistas.Add(animacion);

            var caras = new CinemaTrack { nombre = "Cara · " + nombre, tipo = CinemaTrackKind.Gesto };
            foreach (var cara in actor.caras)
                caras.clips.Add(new CinemaClip
                {
                    t0 = cara.t,
                    t1 = cara.t + (cara.dura > 0f ? cara.dura : 0.5f),
                    etiqueta = cara.emocion,
                    elemento = actor.id,
                });
            if (caras.clips.Count > 0) pistas.Add(caras);

            if (actor.visible != null && actor.visible.Count > 0)
            {
                var presencia = new CinemaTrack { nombre = "En escena · " + nombre, tipo = CinemaTrackKind.Escena };
                foreach (var tramo in actor.visible)
                    presencia.clips.Add(new CinemaClip { t0 = tramo.desde, t1 = tramo.hasta, etiqueta = "se le ve", elemento = actor.id });
                pistas.Add(presencia);
            }
        }

        var porTipo = new Dictionary<CinemaTrackKind, CinemaTrack>();
        foreach (var efecto in guion.efectos)
        {
            if (efecto.beat == null || efecto.beat is SayBeat) continue;

            var tipo = TipoDeEfecto(efecto.beat);
            if (!porTipo.TryGetValue(tipo, out var pista))
            {
                pista = new CinemaTrack { nombre = NombreDePista(tipo), tipo = tipo };
                porTipo[tipo] = pista;
                pistas.Add(pista);
            }
            pista.clips.Add(new CinemaClip
            {
                t0 = efecto.t,
                t1 = efecto.t + 0.6f,
                etiqueta = Recortar(efecto.beat.Describe(), 60),
                linea = efecto.linea,
                clip = SonidoDe(efecto.beat, perfil),
                volumen = VolumenDe(efecto.beat),
            });
        }

        if (guion.etiquetas != null && guion.etiquetas.Count > 0)
        {
            var marcas = new CinemaTrack { nombre = "Marcas", tipo = CinemaTrackKind.Marca };
            foreach (var marca in guion.etiquetas)
                marcas.clips.Add(new CinemaClip { t0 = marca.t, t1 = marca.t + 0.15f, etiqueta = "@" + marca.nombre });
            pistas.Add(marcas);
        }

        return pistas;
    }

    private static AudioGraphProfile PerfilDeAudio()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioGraphProfile"))
        {
            var perfil = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (perfil != null) return perfil;
        }
        return null;
    }

    /// El sonido que toca en un efecto: el clip del propio beat o, si va por clave, el del perfil.
    private static AudioClip SonidoDe(SequenceBeat beat, AudioGraphProfile perfil)
    {
        if (beat is SfxBeat sfx)
        {
            if (sfx.clip != null) return sfx.clip;
            if (perfil != null && !string.IsNullOrEmpty(sfx.eventKey))
                foreach (var evento in perfil.eventSfx)
                    if (string.Equals(evento.eventKey, sfx.eventKey, StringComparison.OrdinalIgnoreCase))
                        return evento.sfx;
        }
        else if (beat is MusicBeat musica && perfil != null && !string.IsNullOrEmpty(musica.musicId))
        {
            var regla = perfil.GetSequenceRule(musica.musicId);
            if (regla != null) return regla.music;
        }
        return null;
    }

    private static float VolumenDe(SequenceBeat beat)
        => beat is SfxBeat sfx ? Mathf.Clamp01(sfx.volume) : 1f;

    /// Para la vista de pistas: lo que pone el horno solo y, de lo escrito, si es acción o reacción.
    private static readonly HashSet<string> GestosDeReaccion = new()
    {
        "TakeDamage", "TakeDamage_2", "DefendHit_NoWeapon", "Defend_NoWeapon", "Sobresalto01", "Fear01", "SenseSomethingStart_NoWeapon",
        "CastingDamage01", "Knockdown01_Ground", "Knockdown01_StandUp", "Pain01", "Stun01", "Dizzy_NoWeapon", "HeadNod01",
        "Landing", "GetUp_NoWeapon", "Cry01", "Cry01_Loop",
    };

    private static CinemaClipClase ClaseDe(OrdenDeAnimacion orden)
    {
        if (orden.origen == 1) return CinemaClipClase.Automatica;
        if (orden.origen == 2) return CinemaClipClase.Vida;
        return !string.IsNullOrEmpty(orden.estado) && GestosDeReaccion.Contains(orden.estado) ? CinemaClipClase.Reaccion : CinemaClipClase.Accion;
    }

    private static CinemaTrackKind TipoDeEfecto(SequenceBeat beat)    {
        string clase = beat.GetType().Name;
        if (clase.Contains("Music")) return CinemaTrackKind.Musica;
        if (clase.Contains("Sound")) return CinemaTrackKind.Sonido;
        if (clase.Contains("Prop")) return CinemaTrackKind.Prop;
        if (clase.Contains("Vfx") || clase.Contains("Conjuro")) return CinemaTrackKind.Vfx;
        if (clase.Contains("Bandas") || clase.Contains("Wait") || clase.Contains("Espera")) return CinemaTrackKind.Escena;
        return CinemaTrackKind.Vfx;
    }

    private static string NombreDePista(CinemaTrackKind tipo) => tipo switch
    {
        CinemaTrackKind.Musica => "Música",
        CinemaTrackKind.Sonido => "SFX",
        CinemaTrackKind.Vfx => "Efectos",
        CinemaTrackKind.Prop => "Props",
        _ => "Bandas y esperas",
    };

    private static string Recortar(string texto, int largo)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.Length <= largo ? texto : texto.Substring(0, largo).TrimEnd() + "…";
    }

    private static string Mayuscula(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return char.ToUpperInvariant(texto[0]) + texto.Substring(1);
    }

    private static int FrasesDeFueraDeEscena(GuionHorneado guion)
    {
        int cuantas = 0;
        foreach (var efecto in guion.efectos)
            if (efecto.beat is SayBeat dice && !string.IsNullOrEmpty(dice.textKey)) cuantas++;
        return cuantas;
    }

    private static string Quien(GuionHorneado guion, string id)
    {
        var actor = guion.actores.Find(a => a.id == id);
        string nombre = actor != null && !string.IsNullOrEmpty(actor.alias) ? actor.alias : id;
        if (string.IsNullOrEmpty(nombre)) return string.Empty;
        return char.ToUpperInvariant(nombre[0]) + nombre.Substring(1);
    }

    private static Dictionary<string, string> Textos()
    {
        if (s_textos != null) return s_textos;

        s_textos = new Dictionary<string, string>();
        foreach (string ruta in new[]
                 {
                     "Assets/Resources/Localization/cinematics_es.json",
                     "Assets/Resources/Localization/dialogues_es.json",
                 })
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ruta);
            if (asset == null) continue;
            foreach (Match m in Regex.Matches(asset.text,
                         "\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"text\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
                s_textos[m.Groups[1].Value] = Regex.Unescape(m.Groups[2].Value);
        }
        return s_textos;
    }

    private static string RutaFisica(string rutaAsset)
    {
        if (Path.IsPathRooted(rutaAsset)) return rutaAsset;
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, rutaAsset);
    }
}
