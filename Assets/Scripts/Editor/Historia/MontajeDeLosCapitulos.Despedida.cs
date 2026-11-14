using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// La despedida del pueblo y el viaje a pie hasta la aldea del mar (INC-967).
///
/// Cap. 2: tras la charla de los motivos, Eldran dice «ve a despedirte, te espero en la salida del
/// pueblo». Empieza la misión «Hora de partir»: despedirse de Oliver es obligatorio; Victoria,
/// Erika, la boticaria y Tomasa tienen frase de despedida si se les habla. Eldran espera en la salida.
///
/// Cap. 3: en la salida, la cinemática de la despedida (Eldran explica que Estela no acepta
/// aprendices y que en la aldea del mar reúna pruebas de los aros; él se va al Reino: «hasta
/// pronto»). Will va andando por el camino del puerto; a mitad de camino acampa y, mientras
/// duerme, Liam habla con Tobías en la torre. A la mañana siguiente sigue hasta la aldea.
/// Ya no hay rótulo de «dos días» ni salto a la costa.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    public const string MisionPartir = "CAP2_HORA_DE_PARTIR";
    public const string SenalADespedirse = "CAP2_A_DESPEDIRSE";
    private const string RutaCap2 = "Assets/NarrativeGraph/Cap2.asset";
    private const string Eldran = "NPC_Eldran", Oliver = "NPC_Oliver";

    private static class SD
    {
        public const string Camino = "CAP3_CAMINO_EMPIEZA";
        public const string Acampada = "CAP3_ACAMPADA_CAMINO";
    }

    // Marcas: la salida del pueblo es el arranque del camino del puerto (VestidoDelMundo.CaminoDelPuerto).
    private const string MarcaEldranSalida = "Eldran_SalidaDelPueblo";
    private const string MarcaWillSalida = "Will_SalidaDelPueblo";
    private const string MarcaAcampadaCamino = "Cap3_AcampadaCamino";

    private static void MontarAssetsDespedida(StringBuilder informe)
    {
        Mision("Q_CAP2_HORA_DE_PARTIR", MisionPartir, "Hora de partir", "Time to leave",
            "Despídete de Oliver y reúnete con Eldran en la salida del pueblo.",
            "Say goodbye to Oliver and meet Eldran at the edge of the village.",
            ("CAP2_ADIOS_OLIVER", "Despídete de Oliver", "Say goodbye to Oliver"),
            ("CAP2_ADIOS_ELDRAN", "Reúnete con Eldran en la salida del pueblo", "Meet Eldran at the edge of the village"));

        // ── La charla de los motivos y la de Oliver ganan su final ───────────────────────
        AmpliarDialogo("Assets/_Dialogues/DIALOGUE NPCS/Eldran/DLG_ELDRAN_MOTIVOS.asset", informe, null,
            ("CHAR_ELDRAN", "DLG_ELDRAN_MOTIVOS_05", "Anda, ve a despedirte de la gente. Oliver no me perdonaría que te fueras sin decirle nada.",
                "Go on, say goodbye to everyone. Oliver would never forgive me if you left without a word.", NPCEmotion.Happy),
            ("CHAR_ELDRAN", "DLG_ELDRAN_MOTIVOS_06", "Yo te espero en la salida del pueblo.",
                "I'll wait for you at the edge of the village.", NPCEmotion.Neutral));
        AmpliarDialogo("Assets/_Dialogues/DIALOGUE NPCS/Oliver/DLG_OLIVER_DESPEDIDA.asset", informe, "DLG_OLIVER_DESPEDIDA_04",
            ("CHAR_OLIVER", "DLG_OLIVER_DESPEDIDA_05", "Y si por ahí ves algo raro, tráemelo. Para la colección.",
                "And if you see anything odd out there, bring it to me. For the collection.", NPCEmotion.Excited));

        // ── Frases mientras dura la despedida ───────────────────────────────────────────
        string fEldran = DialogoDeCaja("DLG_CAP2_ELDRAN_ESPERA", "DLG_CAP2_ELDRAN_ESPERA",
            L("CHAR_ELDRAN", "¿Ya te has despedido de Oliver? Ve, anda. Yo no me muevo de aquí.", "Have you said goodbye to Oliver yet? Go on. I'm not going anywhere."));
        string fOliver = DialogoDeCaja("DLG_CAP2_OLIVER_TRAS_ADIOS", "DLG_CAP2_OLIVER_TRAS_ADIOS",
            L("CHAR_OLIVER", "¿Sigues aquí? Eldran te espera en la salida del pueblo. ¡Y acuérdate de lo raro!", "Still here? Eldran is waiting at the edge of the village. And remember: anything odd!", NPCEmotion.Happy));
        string fVictoria = DialogoDeCaja("DLG_CAP2_ADIOS_VICTORIA", "DLG_CAP2_ADIOS_VICTORIA",
            L("CHAR_VICTORIA", "¿Ya te vas? Ay, Will… Come bien y abrígate por las noches.", "Leaving already? Oh, Will… Eat properly and keep warm at night.", NPCEmotion.Sad),
            L("CHAR_VICTORIA", "Y no le hagas caso a Eldran si dice que no se preocupa. Se preocupa, y mucho.", "And don't listen to Eldran if he says he isn't worried. He is, a lot.", NPCEmotion.Happy),
            W("Lo haré, Victoria. Gracias por todo.", "I will, Victoria. Thanks for everything.", NPCEmotion.Happy));
        string fErika = DialogoDeCaja("DLG_CAP2_ADIOS_ERIKA", "DLG_CAP2_ADIOS_ERIKA",
            L("CHAR_ERIKA", "Guardia alta y no te lances el primero. Y vuelve con ese pañuelo, que es mío.", "Keep your guard up and don't strike first. And bring that scarf back, it's mine.", NPCEmotion.Determined));
        string fBoticaria = DialogoDeCaja("DLG_CAP2_ADIOS_BOTICARIA", "DLG_CAP2_ADIOS_BOTICARIA",
            L("CHAR_BOTICARIA", "La poción es para un apuro de verdad, ¿eh? No para una rozadura.", "That potion is for real trouble, mind. Not for a scrape."));
        string fTomasa = DialogoDeCaja("DLG_CAP2_ADIOS_TOMASA", "DLG_CAP2_ADIOS_TOMASA",
            L("CHAR_TOMASA", "¿Te vas de viaje? Si encuentras por ahí algo que no sirva para nada, ya sabes dónde venderlo.", "Off on a trip? If you find anything useless out there, you know where to sell it.", NPCEmotion.Happy));

        var despedida = Tramo(SenalADespedirse, SD.Camino);
        s_personajes.Add(new SituacionesDe { Actor = Eldran, Entradas = { ("Cap. 2–3 · espera en la salida del pueblo", despedida, ActorPresence.AtMark, MarcaEldranSalida, fEldran) } });
        s_personajes.Add(new SituacionesDe { Actor = Oliver, Entradas = { ("Cap. 2 · tras despedirse de Will", despedida, ActorPresence.Unchanged, null, fOliver) } });
        foreach (var (actor, frase) in new[] { ("NPC_Victoria", fVictoria), ("NPC_Erika", fErika), ("NPC_Boticaria", fBoticaria), ("NPC_Tomasa", fTomasa) })
            s_personajes.Add(new SituacionesDe { Actor = actor, Entradas = { ("Cap. 2 · despedida de Will", despedida, ActorPresence.Unchanged, null, frase) } });

        // ── La cinemática de la despedida ───────────────────────────────────────────────
        s_cines.Add(new Cine { Cap = Cap3, Clave = "CAP3_DESPEDIDA", Guion = "Cap3_00_LaDespedida", Texto = PresentacionDeTexto.Subtitulo,
            Nombre = "Cap. 3: la despedida en la salida del pueblo",
            Resumen = "Eldran: Estela no acepta aprendices; en la aldea del mar, reúne pruebas de los aros. Él se va al Reino. «Es un hasta pronto»." });
        TextoDeCine("CAP3_DESPEDIDA_01", "Ya estás aquí. ¿Te has despedido de todos?", "There you are. Have you said goodbye to everyone?");
        TextoDeCine("CAP3_DESPEDIDA_02", "De casi todos. Tomasa quería venderme otra cosa inútil.", "Almost everyone. Tomasa wanted to sell me another useless thing.");
        TextoDeCine("CAP3_DESPEDIDA_03", "Esa mujer no descansa. Escucha, que esto es importante.", "That woman never rests. Listen, this is important.");
        TextoDeCine("CAP3_DESPEDIDA_04", "Estela no acepta aprendices. Si te presentas con un trozo de hierro y buena cara, te cerrará la puerta.", "Estela doesn't take apprentices. Turn up with a piece of iron and a nice smile and she'll shut the door on you.");
        TextoDeCine("CAP3_DESPEDIDA_05", "Pero en la aldea del mar han visto más animales con aros como ese. Ve allí primero, pregunta y apúntalo todo.", "But at the seaside village they've seen more animals with collars like that one. Go there first, ask around and write it all down.");
        TextoDeCine("CAP3_DESPEDIDA_06", "Llévale algo que no pueda ignorar. Así te escuchará.", "Bring her something she can't ignore. Then she'll listen.");
        TextoDeCine("CAP3_DESPEDIDA_07", "¿Y tú?", "And you?");
        TextoDeCine("CAP3_DESPEDIDA_08", "Yo me voy al Reino, a contarle al Rey lo de los aros. Cuando tengas algo, búscame allí.", "I'm going to the Kingdom, to tell the King about the collars. When you have something, find me there.");
        TextoDeCine("CAP3_DESPEDIDA_09", "Entonces no es una despedida.", "So this isn't goodbye.");
        TextoDeCine("CAP3_DESPEDIDA_10", "Claro que no. Es un «hasta pronto». Anda, tira, que se te va el día.", "Of course not. It's a \"see you soon\". Go on, off you go, the day's slipping away.");
        TextoDeCine("CAP3_DESPEDIDA_11", "¡Hasta pronto, Eldran!", "See you soon, Eldran!");

        // ── Marcas, acampada y llegadas ────────────────────────────────────────────────
        // El camino del puerto arranca en (50,8, −183,8) y baja hacia el sureste (rumbo ~137°).
        M(Cap3, MarcaEldranSalida, 55.5f, -187f, 317f);
        M(Cap3, MarcaWillSalida, 53.4f, -185f, 137f);
        M(Cap3, "Cap3_Despedida_Camino", 58.2f, -190.1f, 137f);
        // Punto de guardado a un lado del camino, a mitad de viaje (entre 138,−261 y 157,−278).
        M(Cap3, MarcaAcampadaCamino, 150f, -266.5f, 45f);
        // A mitad del camino del puerto salta el interludio de Tobías; radio ancho para que no se escape quien vaya campo a través.
        s_llegadas.Add(new Llegada { Cap = Cap3, Nombre = "A mitad del camino del puerto", Senal = SD.Acampada, Mision = MisionCap3,
            Pos = new Vector3(130f, float.NaN, -253f), Radio = 22f, Tramo = Tramo(SD.Camino, SD.Acampada) });
        informe.AppendLine("Despedida: misión «Hora de partir», frases de despedida, cinemática e interludio de Tobías a mitad del camino.");
    }

    /// <summary>Los pasos del Cap. 3 desde la salida del pueblo hasta que Will sigue hacia la aldea.</summary>
    private static List<Paso> PasosDeLaDespedidaYElCamino() => new()
    {
        // La despedida
        P("adios-hablar", "3.0 · Hablar con Eldran en la salida del pueblo (probar desde aquí)", Hablar(Eldran)),
        P("adios", "3.0 · Cinemática: la despedida («es un hasta pronto»)", Cinematica("CAP3_DESPEDIDA")),
        P("adios-paso", "Paso: reunirse con Eldran (cierra «Hora de partir»)", Completar(MisionPartir, "CAP2_ADIOS_ELDRAN", cerrar: true)),
        P("mision", "Misión: las marcas de hierro (aviso de misión nueva)", new StartQuestNode { questId = MisionCap3, noGiver = true }),
        P("camino", "3.1 · Camino del puerto, a pie (probar desde aquí)", Emitir(SD.Camino)),
        P("camino-punto", "3.1 · Punto de guardado: la salida del pueblo", Punto("CAP3_SALIDA", MarcaWillSalida)),
        // A mitad de camino salta el interludio de Tobías y se sigue andando, sin rótulos ni cambio de hora.
        P("acampada-llega", "3.1 · Pasa por la mitad del camino", Esperar(SD.Acampada)),
        P("espejo-carga", "3.1 · Interludio: carga la torre de Liam (probar desde aquí)", new AdditiveSceneNode { operacion = AdditiveSceneNode.Operacion.Cargar, sceneName = "TorreDeLiam", waitForCompletion = true }),
        P("espejo", "3.1 · Cinemática: Tobías en el espejo", Cinematica("CAP3_ESPEJO")),
        P("espejo-descarga", "3.1 · Descarga la torre de Liam", new AdditiveSceneNode { operacion = AdditiveSceneNode.Operacion.Descargar, sceneName = "TorreDeLiam", waitForCompletion = true }),
        P("camino-destapa", "3.1 · Se destapa la pantalla y se sigue andando", Destapar()),
        P("camino-punto2", "3.1 · Punto de guardado: a mitad del camino", Punto("CAP3_ACAMPADA_CAMINO", MarcaAcampadaCamino)),
    };

    /// <summary>
    /// El final del Cap. 2: tras los motivos, «ve a despedirte»; la despedida de Oliver es un paso de
    /// «Hora de partir» y la de Eldran pasa a la salida del pueblo (Cap. 3). Se rehace en cada montaje.
    /// </summary>
    private static void AjustarCap2(StringBuilder informe)
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(RutaCap2);
        if (g == null) { informe.AppendLine("AVISO: falta " + RutaCap2); return; }
        var completar = g.FindNode("cap2-prepara-viaje-completar");
        var oliverDlg = g.FindNode("cap2-despedida-oliver-dlg");
        var join = g.FindNode("cap2-despedida-join");
        var punto = g.FindNode("cap2-final-checkpoint") as CheckpointNode;
        if (completar == null || oliverDlg == null || join == null || punto == null)
        { informe.AppendLine("AVISO: el Cap. 2 no tiene los nodos de la despedida que esperaba; no lo toco."); return; }

        var cap2 = new Capitulo { Etiqueta = "Cap2", Ruta = RutaCap2, Prefijo = "", Nombre = completar.chapter };
        Vector2 sitio = completar.position;
        Poner(g, cap2, "cap2-adios-senal", "26d.- «Ve a despedirte» (señal)", sitio + new Vector2(0f, 200f),
            Emitir(SenalADespedirse), new List<string> { "cap2-adios-mision" });
        Poner(g, cap2, "cap2-adios-mision", "26e.- Misión: hora de partir", sitio + new Vector2(380f, 200f),
            new StartQuestNode { questId = MisionPartir, noGiver = true }, new List<string> { "cap2-despedida-oliver-talk" });
        Poner(g, cap2, "cap2-adios-paso-oliver", "29.- Paso: despedirse de Oliver", oliverDlg.position + new Vector2(260f, 0f),
            Completar(MisionPartir, "CAP2_ADIOS_OLIVER"), new List<string> { join.guid });
        completar.outputs = new List<string> { "cap2-adios-senal" };
        oliverDlg.outputs = new List<string> { "cap2-adios-paso-oliver" };
        oliverDlg.displayTitle = "28.- Oliver: nos vemos pronto";
        join.displayTitle = "33.- Cap. 2 completado - Eldran espera en la salida del pueblo";
        Sendero.Narrative.Editor.ArranqueDeLasPruebas.FijarAncla(punto, MarcaWillSalida);

        // La despedida de Eldran ya no es aquí: es la cinemática de la salida (Cap. 3).
        var fuera = new[] { "cap2-despedida-fork", "cap2-despedida-espera", "cap2-despedida-oliver-hecha",
                            "cap2-despedida-eldran-talk", "cap2-despedida-eldran-dlg", "cap2-despedida-eldran-hecha" };
        int quitados = g.nodes.RemoveAll(n => n != null && fuera.Contains(n.guid));
        EditorUtility.SetDirty(g);
        foreach (var n in g.nodes)
            foreach (var s in n.outputs)
                if (!string.IsNullOrEmpty(s) && g.FindNode(s) == null)
                    informe.AppendLine($"ERROR · Cap2: «{n.displayTitle}» lleva a «{s}», que no existe.");
        informe.AppendLine($"Cap. 2: despedida en la salida del pueblo ({quitados} nodos de la despedida anterior retirados).");
    }

    /// <summary>
    /// Añade líneas a un diálogo existente (si ya están, solo actualiza el texto). Con
    /// <paramref name="antesDe"/>, las nuevas van delante de esa línea; si no, al final.
    /// </summary>
    private static void AmpliarDialogo(string ruta, StringBuilder informe, string antesDe,
                                       params (string hablante, string clave, string es, string en, NPCEmotion emocion)[] nuevas)
    {
        var d = AssetDatabase.LoadAssetAtPath<DialogueAsset>(ruta);
        if (d == null) { informe.AppendLine("AVISO: falta " + ruta); return; }
        var lineas = new List<DialogueLine>();
        if (d.lines != null) lineas.AddRange(d.lines.Where(l => !nuevas.Any(n => n.clave == l.textId)));
        int sitio = string.IsNullOrEmpty(antesDe) ? -1 : lineas.FindIndex(l => l.textId == antesDe);
        if (sitio < 0) sitio = lineas.Count;
        var añadir = nuevas.Select(n => new DialogueLine
        {
            speakerNameId = n.hablante, textId = n.clave, text = n.es,
            isPlayerSpeaking = n.hablante == "CHAR_WILL", emotion = n.emocion,
        }).ToList();
        lineas.InsertRange(sitio, añadir);
        d.lines = lineas.ToArray();
        foreach (var n in nuevas) TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, n.clave, n.es, n.en);
        EditorUtility.SetDirty(d);
    }
}
