using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Las cinemáticas de Chuchelandia: siete guiones horneados (Assets/_SEQUENCES/Guiones/Chuche_*.guion.txt,
/// TDD § 22), cada uno con su <see cref="SequenceDefinition"/>, su <see cref="SequencePlayer"/> en la
/// escena y su <see cref="PlayCinematicNode"/> en el grafo (guid «chuche-cine-*», con las señales y la
/// secuencia de la tabla). Seis van dentro del juego, con bocadillos; «¡TE QUIERO!» es de cine (bandas
/// y subtítulos) y lleva su atrezo (<see cref="AtrezoDeLasCinematicas"/>). Al montar la prueba se colocan también las marcas que solo usan los guiones
/// (M_Cine_*) y cuatro gominolas figurantes (el ejército que se lleva a Estela y el público del salón),
/// que esperan ocultas bajo el reino hasta que un guion las hace aparecer.
/// El horneado es un menú aparte: tarda, escribe storyboards y necesita Chuchelandia abierta.
/// </summary>
public static partial class ChuchelandiaBuilder
{
    private const string CarpetaGuionesCine = "Assets/_SEQUENCES/Guiones";
    private const string CarpetaSecuenciasCine = "Assets/_SEQUENCES";
    private const string PerfilDeAudioCine = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";
    private const string TituloHorneado = "Chuchelandia: hornear las cinemáticas";

    /// <summary>Profundidad a la que esperan los figurantes: ni se ven ni estorban.</summary>
    private const float ProfundidadDeFigurantes = -200f;

    private sealed class Cine
    {
        public string Nodo, Clave, Guion, Secuencia, Nombre, Resumen;
        /// <summary>Bocadillo (dentro del juego) o Subtitulo (de cine, con bandas).</summary>
        public PresentacionDeTexto Texto;
        public string RutaGuion => $"{CarpetaGuionesCine}/{Guion}.guion.txt";
        public string RutaSecuencia => $"{CarpetaSecuenciasCine}/{Secuencia}.asset";
        public string Entrada => $"CHUCHE_CINE_{Clave}_START";
        public string Salida => $"CHUCHE_CINE_{Clave}_DONE";
    }

    // ── Tabla de cinemáticas ────────────────────────────────────────────────────────────────
    // Nodo del grafo, clave de sus señales, guion, secuencia, nombre y resumen.
    private static readonly Cine[] TablaDeCinematicas =
    {
        C("cine-llegada", "LLEGADA", "Chuche_01_Llegada", "SEQ_Chuche_Llegada", "Chuchelandia: la llegada",
          "La puerta rosa, la purpurina y la fanfarria que desafina. «Oh, no. De verdad que no». «A mí me gusta»."),
        C("cine-heraldo", "PERGAMINO", "Chuche_02_ElPergamino", "SEQ_Chuche_Pergamino", "Chuchelandia: el pergamino del heraldo",
          "El heraldo lee el pergamino ante la Puerta del Corazón; Will y Liam miran a Estela. «Ni lo penséis»."),
        C("cine-ejercito", "CANDIDATA", "Chuche_03_UnaCandidata", "SEQ_Chuche_Candidata", "Chuchelandia: ¡una candidata!",
          "«Solo una princesa». El ejército de gominolas se lleva a Estela en volandas («¡Noooooo! ¡Dejadmeeee!») y viste a Will y a Liam."),
        C("cine-vestidor", "VESTIDOR", "Chuche_04_ElVestidor", "SEQ_Chuche_Vestidor", "Chuchelandia: el vestidor real",
          "Voces desde dentro, «Le han puesto el lazo», Estela de rosa. «Os lleno de purpurina. Pero con mucha rabia»."),
        C("cine-pompon", "POMPON", "Chuche_05_SuAltezaPompon", "SEQ_Chuche_Pompon", "Chuchelandia: Su Alteza Pompón",
          "«Es un oso» hasta que el heraldo explota en confeti; el suplente anuncia de espaldas y la Condesa explica el duelo."),
        C("cine-te-quiero", "TEQUIERO", "Chuche_06_TeQuiero", "SEQ_Chuche_TeQuiero", "Chuchelandia: ¡TE QUIERO!",
          "Will y Liam caen en la fuente de sirope bajo la pancarta; a Estela se le escapa la risa y gana el duelo.",
          PresentacionDeTexto.Subtitulo),
        C("cine-victoria", "VICTORIA", "Chuche_07_Victoria", "SEQ_Chuche_Victoria", "Chuchelandia: victoria",
          "Pompón y la tiara, «Uno. Y no se lo contéis a nadie», la Puerta hace clic y las collejas pegadas."),
    };

    // ── Tabla de marcas de las cinemáticas ──────────────────────────────────────────────────
    // Nombre, marca de referencia, desplazamiento en el marco de la referencia (x a su derecha, z hacia
    // donde mira; ver EnElMarcoDe) y marca hacia la que mira. Se asientan en el suelo sin alejarse más
    // de 1,5 m de la altura de su referencia. Los personajes de un grupo, a 2 m o más unos de otros.
    private static readonly (string nombre, string referencia, Vector3 desplazamiento, string mira)[] MarcasDeCinematica =
    {
        // 1 · Llegada: cámara a la espalda del grupo, con el valle delante.
        ("M_Cine_Llegada_CamEspalda", "M_Llegada_Will", new Vector3(0f, 0f, -8f), "M_Llegada_Will"),
        // 2-3 · La Puerta del Corazón (mira al oeste; el grupo llega desde el oeste).
        ("M_Cine_Puerta_Cam", "M_Puerta_Corazon", new Vector3(1.5f, 0f, 12.5f), "M_Puerta_Corazon"),
        ("M_Cine_Puerta_Ejercito1", "M_Puerta_Corazon", new Vector3(10f, 0f, 9f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Ejercito2", "M_Puerta_Corazon", new Vector3(9f, 0f, 12f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Ejercito3", "M_Puerta_Corazon", new Vector3(-10f, 0f, 9f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Ejercito4", "M_Puerta_Corazon", new Vector3(-9f, 0f, 12f), "M_Puerta_Estela"),
        // Las cuatro que levantan a Estela en volandas: la rodean de cerca y se la llevan en formación.
        ("M_Cine_Puerta_Rodea1", "M_Puerta_Estela", new Vector3(0.8f, 0f, 0.8f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Rodea2", "M_Puerta_Estela", new Vector3(0.8f, 0f, -0.8f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Rodea3", "M_Puerta_Estela", new Vector3(-0.8f, 0f, 0.8f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Rodea4", "M_Puerta_Estela", new Vector3(-0.8f, 0f, -0.8f), "M_Puerta_Estela"),
        ("M_Cine_Puerta_Huida", "M_Puerta_Corazon", new Vector3(-18f, 0f, 30f), null),
        ("M_Cine_Puerta_Umbral_Estela", "M_Puerta_Corazon", new Vector3(0f, 0f, 1.2f), "M_Puerta_Corazon"),
        ("M_Cine_Puerta_Umbral_Will", "M_Puerta_Corazon", new Vector3(-1.8f, 0f, 2f), "M_Puerta_Corazon"),
        ("M_Cine_Puerta_Umbral_Liam", "M_Puerta_Corazon", new Vector3(1.8f, 0f, 2f), "M_Puerta_Corazon"),
        // 3 · El vestidor: dentro (detrás de la puerta) y el sitio al que sale Estela.
        ("M_Cine_Vestidor_Dentro", "M_Vestidor_Puerta", new Vector3(0f, 0f, -1.5f), "M_Vestidor_Puerta"),
        ("M_Cine_Vestidor_Estela", "M_Vestidor_Puerta", new Vector3(0f, 0f, 1.1f), "M_Vestidor_Will"),
        // 7 · El salón: M_Duelo_Centro mira al trono (al oeste); su derecha es el norte.
        ("M_Cine_Salon_CamLado", "M_Duelo_Centro", new Vector3(-3.5f, 0f, 0.6f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Estela", "M_Duelo_Centro", new Vector3(0f, 0f, -1.5f), "M_Duelo_Trono"),
        ("M_Cine_Salon_Will", "M_Duelo_Centro", new Vector3(-1.4f, 0f, -3f), "M_Duelo_Trono"),
        ("M_Cine_Salon_Liam", "M_Duelo_Centro", new Vector3(1.4f, 0f, -3f), "M_Duelo_Trono"),
        ("M_Cine_Salon_WillAtras", "M_Duelo_Centro", new Vector3(-1.9f, 0f, -4.2f), "M_Duelo_Trono"),
        ("M_Cine_Salon_LiamAtras", "M_Duelo_Centro", new Vector3(1.9f, 0f, -4f), "M_Duelo_Trono"),
        ("M_Cine_Salon_WillTapa", "M_Duelo_Centro", new Vector3(-0.45f, 0f, -1.85f), "M_Cine_Salon_Estela"),
        ("M_Cine_Salon_Condesa", "M_Duelo_Centro", new Vector3(-1.9f, 0f, -0.9f), "M_Cine_Salon_Estela"),
        ("M_Cine_Salon_Heraldo", "M_Duelo_Centro", new Vector3(-1.5f, 0f, 0.9f), "M_Cine_Salon_Estela"),
        ("M_Cine_Salon_Suplente", "M_Duelo_Centro", new Vector3(-1.3f, 0f, 0.4f), "M_Duelo_Trono"),
        ("M_Cine_Salon_Pompon", "M_Duelo_Centro", new Vector3(0f, 0f, -0.35f), "M_Cine_Salon_Estela"),
        ("M_Cine_Salon_Publico1", "M_Duelo_Centro", new Vector3(4.6f, 0f, 2f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Publico2", "M_Duelo_Centro", new Vector3(4.9f, 0f, -0.2f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Publico3", "M_Duelo_Centro", new Vector3(4.7f, 0f, -2.2f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Publico4", "M_Duelo_Centro", new Vector3(-4.6f, 0f, -3.4f), "M_Duelo_Centro"),
        // 9 · «¡TE QUIERO!» (M_Duelo_Fuente mira al centro; el pilón mide 1,5 m de radio): las sillas justo
        // delante del borde, dónde caen de espaldas, dónde salen pegados y dos cámaras (de frente y de lado).
        ("M_Cine_Salon_CamFuente", "M_Duelo_Fuente", new Vector3(0f, 0f, 5f), "M_Duelo_Fuente"),
        ("M_Cine_Salon_CamFuenteLado", "M_Duelo_Fuente", new Vector3(4.5f, 0f, 1.4f), "M_Duelo_Fuente"),
        ("M_Cine_Salon_TeQuiero_Will", "M_Duelo_Fuente", new Vector3(-0.9f, 0f, 1.95f), "M_Duelo_Centro"),
        ("M_Cine_Salon_TeQuiero_Liam", "M_Duelo_Fuente", new Vector3(0.9f, 0f, 1.95f), "M_Duelo_Centro"),
        ("M_Cine_Salon_TeQuiero_LiamJunto", "M_Duelo_Fuente", new Vector3(1.9f, 0f, 2.1f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Fuente_Will", "M_Duelo_Fuente", new Vector3(-0.45f, 0f, 0.55f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Fuente_Liam", "M_Duelo_Fuente", new Vector3(0.45f, 0f, 0.55f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Novios_Will", "M_Duelo_Fuente", new Vector3(-0.3f, 0f, 0.35f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Novios_Liam", "M_Duelo_Fuente", new Vector3(0.3f, 0f, 0.35f), "M_Duelo_Centro"),
        ("M_Cine_Salon_TeQuiero_Pompon", "M_Duelo_Centro", new Vector3(-0.9f, 0f, 1.6f), "M_Duelo_Palco"),
        // 10 · Victoria: Pompón delante de Estela (se le pega a la pierna); Will y Liam, todavía pegados (a 1 m).
        ("M_Cine_Salon_Pierna", "M_Duelo_Centro", new Vector3(0.3f, 0f, 1.2f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Victoria_Will", "M_Duelo_Centro", new Vector3(0.9f, 0f, -1.8f), "M_Duelo_Centro"),
        ("M_Cine_Salon_Victoria_Liam", "M_Duelo_Centro", new Vector3(1.9f, 0f, -1.8f), "M_Duelo_Centro"),
    };

    /// <summary>Compañeros de la escena (nombre del objeto) y su id en los guiones.</summary>
    private static readonly (string nombre, string id)[] CompanerosDeLosGuiones =
    {
        ("_ESTELA", "Actor_Estela"),
        ("_LIAM", "Actor_Liam"),
    };

    /// <summary>Gominolas figurantes: el ejército de la Puerta y el público del salón.</summary>
    private static Papel[] Figurantes()
    {
        var lista = new Papel[4];
        for (int i = 0; i < lista.Length; i++)
            lista[i] = new Papel
            {
                Nombre = $"Gominola {i + 1}", ActorId = $"Actor_Gominola{i + 1}",
                Prefab = $"{Pack}/Characters/Prefabs/Grummy_0{i + 1}.prefab",
                Controlador = RutaControladorDeLasGominolas,
            };
        return lista;
    }

    // ── Secuencias y grafo ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Crea o actualiza la definición de cada cinemática: señales y una fase con el GuionBeat que el
    /// horneado rellena. Si ya existe, solo se tocan los datos de cabecera.
    /// </summary>
    private static void AsegurarSecuencias(StringBuilder informe)
    {
        int nuevas = 0;
        foreach (var c in TablaDeCinematicas)
        {
            var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<SequenceDefinition>();
                AssetDatabase.CreateAsset(def, c.RutaSecuencia);
                nuevas++;
            }
            def.displayName = c.Nombre;
            def.summary = c.Resumen;
            def.signalIn = c.Entrada;
            def.signalOut = c.Salida;
            def.presentacionDeTexto = c.Texto;
            if (def.phases.Count == 0)
                def.phases.Add(new SequencePhase { name = "Guion", beats = new List<SequenceBeat> { new GuionBeat { note = "Guion: " + c.Guion } } });
            EditorUtility.SetDirty(def);
        }
        informe.AppendLine($"Cinemáticas: {TablaDeCinematicas.Length} secuencias ({nuevas} nuevas).");
    }

    /// <summary>El nodo del grafo que lanza la cinemática de ese nodo (señales y secuencia de la tabla).</summary>
    private static NarrativeNode Cinematica(string nodo)
    {
        var c = System.Array.Find(TablaDeCinematicas, x => x.Nodo == nodo);
        if (c == null) return new PlayCinematicNode { cinematicName = nodo + " (sin cinemática en la tabla)" };
        return new PlayCinematicNode
        {
            cinematicName = c.Nombre,
            signalIn = c.Entrada,
            signalDone = c.Salida,
            secuencia = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia),
        };
    }

    // ── La escena ───────────────────────────────────────────────────────────────────────────

    /// <summary>Coloca las marcas de los guiones y las añade a <paramref name="marcas"/>.</summary>
    private static void ColocarMarcasDeCinematica(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var padre = Hijo(raiz, "Marcas de las cinemáticas");
        int n = 0;
        foreach (var (nombre, referencia, desplazamiento, _) in MarcasDeCinematica)
        {
            if (!marcas.TryGetValue(referencia, out var origen)) { informe.AppendLine($"{nombre}: falta la marca {referencia}."); continue; }
            Vector3 pos = AlSuelo(origen.position + EnElMarcoDe(origen, desplazamiento));
            if (Mathf.Abs(pos.y - origen.position.y) > 1.5f) pos.y = origen.position.y;
            marcas[nombre] = KitDeLaberintoDeEspejos.Punto(padre, nombre, pos, Vector3.forward);
            n++;
        }
        foreach (var (nombre, _, _, mira) in MarcasDeCinematica)
        {
            if (string.IsNullOrEmpty(mira) || !marcas.TryGetValue(nombre, out var t) || !marcas.TryGetValue(mira, out var objetivo)) continue;
            Vector3 d = objetivo.position - t.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(d);
        }
        informe.AppendLine($"Marcas de las cinemáticas: {n} de {MarcasDeCinematica.Length}.");
    }

    /// <summary>
    /// Los figurantes y un SequencePlayer + SequenceStage + CinematicCameraDriver por cinemática, con
    /// todo el reparto de Chuchelandia y todas las marcas. Estela y Liam son los compañeros de la escena
    /// (_ESTELA, _LIAM): van en el escenario con el id de los guiones (Actor_Estela, Actor_Liam), porque el
    /// registro de NPCs los conoce por su nombre y no por ese id. A Will lo resuelve el jugador.
    /// </summary>
    private static void MontarCinematicas(Transform raiz, Dictionary<string, Transform> marcas, StringBuilder informe)
    {
        var padre = Hijo(raiz, "Cinemáticas");
        var actores = new List<(string id, Transform t)>();

        var reparto = raiz.Find("Reparto");
        if (reparto != null)
            foreach (Transform hijo in reparto)
            {
                int a = hijo.name.LastIndexOf('('), b = hijo.name.LastIndexOf(')');
                if (a >= 0 && b > a) actores.Add((hijo.name.Substring(a + 1, b - a - 1), hijo));
            }

        foreach (var (nombre, id) in CompanerosDeLosGuiones)
        {
            var companero = TodosLosObjetos(raiz.gameObject.scene).Find(t => t.name == nombre);
            if (companero != null) actores.Add((id, companero));
            else informe.AppendLine($"Cinemáticas: no encuentro a {nombre} en la escena; en juego {id} no saldrá en los guiones.");
        }

        var figurantes = Hijo(padre, "Figurantes (ocultos hasta que un guion los saca)");
        Vector3 bajoElReino = marcas.TryGetValue("M_Puerta_Corazon", out var puerta) ? puerta.position : ZonaDelReino.center;
        var papeles = Figurantes();
        for (int i = 0; i < papeles.Length; i++)
        {
            var papel = papeles[i];
            var go = PonerPapel(raiz.gameObject.scene, figurantes, papel, bajoElReino + new Vector3(2f * i, ProfundidadDeFigurantes, 0f),
                                Quaternion.identity, informe);
            if (go != null) actores.Add((papel.ActorId, go.transform));
        }

        var perfil = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PerfilDeAudioCine);
        int montadas = 0;
        foreach (var c in TablaDeCinematicas)
        {
            var def = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia);
            if (def == null) { informe.AppendLine($"Cinemática {c.Guion}: falta {c.RutaSecuencia}."); continue; }
            var go = new GameObject(c.Secuencia);
            go.transform.SetParent(padre, false);
            var driver = go.AddComponent<CinematicCameraDriver>();
            var stage = go.AddComponent<SequenceStage>();
            var player = go.AddComponent<SequencePlayer>();

            var so = new SerializedObject(stage);
            var lista = so.FindProperty("_actors");
            lista.arraySize = actores.Count;
            for (int i = 0; i < actores.Count; i++)
            {
                var e = lista.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = actores[i].id;
                e.FindPropertyRelative("target").objectReferenceValue = actores[i].t;
                // Los personajes del pack solo tienen Animator; los compañeros lo resuelve su propio animador.
                bool esNpc = actores[i].t.GetComponent<Game.NPC.NPCBehaviourManagerV2>() != null;
                e.FindPropertyRelative("animator").objectReferenceValue = esNpc ? null : actores[i].t.GetComponentInChildren<Animator>(true);
            }
            var m = so.FindProperty("_marks");
            m.arraySize = marcas.Count;
            int k = 0;
            foreach (var marca in marcas)
            {
                var e = m.GetArrayElementAtIndex(k++);
                e.FindPropertyRelative("name").stringValue = marca.Key;
                e.FindPropertyRelative("target").objectReferenceValue = marca.Value;
            }
            var atrezo = AtrezoDe(c, raiz, informe);
            var props = so.FindProperty("_props");
            props.arraySize = atrezo.Count;
            for (int i = 0; i < atrezo.Count; i++)
            {
                var e = props.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = atrezo[i].id;
                e.FindPropertyRelative("target").objectReferenceValue = atrezo[i].objeto;
                e.FindPropertyRelative("eyeHeight").floatValue = atrezo[i].altura;
                e.FindPropertyRelative("esObstaculo").boolValue = false;
            }
            so.FindProperty("_cameraDriver").objectReferenceValue = driver;
            so.ApplyModifiedPropertiesWithoutUndo();

            var sp = new SerializedObject(player);
            sp.FindProperty("_definition").objectReferenceValue = def;
            sp.FindProperty("_stage").objectReferenceValue = stage;
            sp.FindProperty("_cinematicCamera").objectReferenceValue = driver;
            sp.FindProperty("_audioProfile").objectReferenceValue = perfil;
            sp.ApplyModifiedPropertiesWithoutUndo();
            montadas++;
        }
        informe.AppendLine($"Cinemáticas: {montadas} reproductores, {actores.Count} actores en su escenario (4 figurantes y los compañeros).");
    }

    // ── El horneado ─────────────────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Escenario/Chuchelandia: hornear las cinemáticas")]
    public static void HornearCinematicas()
    {
        if (Application.isPlaying)
        {
            OrdenesDelEditor.Avisar(TituloHorneado, "Sal de Play antes de hornear.");
            return;
        }
        if (SceneManager.GetActiveScene().path != RutaEscena)
        {
            OrdenesDelEditor.Avisar(TituloHorneado,
                $"Abre {RutaEscena} y déjala como escena activa antes de hornear. No se ha cambiado nada.");
            return;
        }
        if (!OrdenesDelEditor.Confirmar(TituloHorneado,
                $"Se hornean los {TablaDeCinematicas.Length} guiones de Chuchelandia con storyboard (tarda unos minutos). " +
                "Antes hay que haber montado la prueba (marcas, reparto y reproductores).",
                "Hornear", "Cancelar"))
            return;

        var informe = new StringBuilder();
        foreach (var c in TablaDeCinematicas)
        {
            if (AssetDatabase.LoadAssetAtPath<SequenceDefinition>(c.RutaSecuencia) == null)
            {
                informe.AppendLine($"{c.Guion}: falta {c.RutaSecuencia}; monta antes la prueba.");
                continue;
            }
            HorneadorDeGuion.UltimoResumen = null;
            HorneadorDeGuion.Hornear(c.RutaGuion, storyboard: true);
            informe.AppendLine(HorneadorDeGuion.UltimoResumen ?? $"{c.Guion}: no se ha horneado.");
        }

        var escena = SceneManager.GetSceneByPath(RutaEscena);
        if (escena.IsValid() && escena.isDirty) EditorSceneManager.SaveScene(escena);
        AssetDatabase.SaveAssets();
        Debug.Log("[ChuchelandiaBuilder] " + informe.ToString().Replace('\n', ' '));
        OrdenesDelEditor.Avisar(TituloHorneado, informe.ToString());
    }

    private static Cine C(string nodo, string clave, string guion, string secuencia, string nombre, string resumen,
                          PresentacionDeTexto texto = PresentacionDeTexto.Bocadillo)
        => new Cine { Nodo = nodo, Clave = clave, Guion = guion, Secuencia = secuencia, Nombre = nombre, Resumen = resumen, Texto = texto };
}
