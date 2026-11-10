using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// La aldea del mar (pueblo pesquero) con vida: sus vecinos, que salen de
/// <see cref="PersonajesConAspectoFijo"/>; su sitio en el roster; lo que cuentan (agendas: lo del
/// Cap. 3 mientras dura y lo de siempre el resto del juego, una frase por charla); la ruta de cajas
/// de la pescadora y la tienda de la tendera. Y lo que se ve en las pistas de la madera del Cap. 3:
/// el pelo y las huellas en la cerca del huerto, los arañazos y el hierro en el cobertizo de las
/// redes, colocados sobre la madera de verdad (se busca con un rayo). Ver INC-969.
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    private const string Aldea = "Aldea del mar";
    private const string CarpetaAldea = "Assets/_NPCs/AldeaDelMar";
    private const string PuestoDeRemedios = "puerto_remedios";
    private static readonly string[] VecinosDeLaAldea = { "Posadero", "Remedios", "Anselmo", "Engracia", "Eusebio", "Fermin" };

    private static string RutaDeVecino(string nombre) => $"{CarpetaAldea}/{nombre}.prefab";

    /// <summary>Prefabs, textos, roster y agendas de la aldea. Se llama desde el montaje del Cap. 3.</summary>
    private static void MontarAldeaDelMar(StringBuilder informe)
    {
        // El nombre del pueblo sale como rótulo al entrar en su zona de ambiente.
        const string nombre = "LUGAR_ALDEA_DEL_MAR";
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, nombre, "Aldea del mar", "Seaside Village");
        var ambiente = Cargar<AmbientPreset>("Assets/AmbientPresets/AmbientPreset_VillageByTheSea.asset");
        if (ambiente != null && ambiente.nombreDelLugar != nombre)
        {
            ambiente.nombreDelLugar = nombre;
            EditorUtility.SetDirty(ambiente);
        }

        // Prefabs: solo los que faltan (rehacerlos reinicia las caras); con alguno nuevo, caras otra vez.
        int nuevos = 0;
        foreach (var n in VecinosDeLaAldea)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaDeVecino(n)) == null) { PersonajesConAspectoFijo.Asegurar(n); nuevos++; }
        if (nuevos > 0) CarasDeLosNPCs.Aplicar(informe);

        foreach (var (clave, es, en) in new[]
        {
            ("CHAR_REMEDIOS", "Remedios", "Remedios"), ("CHAR_ANSELMO", "Anselmo", "Anselmo"),
            ("CHAR_ENGRACIA", "Engracia", "Engracia"), ("CHAR_EUSEBIO", "Eusebio", "Eusebio"),
            ("CHAR_FERMIN", "Fermín", "Fermín"),
        }) TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, clave, es, en);

        // Lo que dicen si nada más les da frase (el diálogo propio del prefab).
        foreach (var (clave, es, en) in new[]
        {
            ("DLG_POSADERO_01", "Pasa, pasa. Aquí siempre hay sitio y sopa caliente.", "Come in, come in. There's always room here, and hot soup."),
            ("DLG_REMEDIOS_01", "Cuidado, que voy cargada.", "Careful, my hands are full."),
            ("DLG_ANSELMO_01", "El mar está tranquilo hoy.", "The sea is calm today."),
            ("DLG_ENGRACIA_01", "Remedios, pociones y algo de comer. ¿Qué te pongo?", "Remedies, potions and something to eat. What can I get you?"),
            ("DLG_EUSEBIO_01", "Buenos días, chico.", "Morning, lad."),
            ("DLG_FERMIN_01", "Buen día para pasear.", "Nice day for a walk."),
        }) TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, clave, es, en);

        // ── Dónde están (siempre; el posadero y Lucía, desde el Cap. 3, en la tabla del capítulo) ──
        Vecino("Remedios", 268.5f, -446.0f, 180f, navMesh: true, voz: "Vecina");
        Vecino("Anselmo", 270.0f, -459.5f, 180f, y: 3.76f);   // al final del muelle: la altura de los tablones, no la del fondo
        Vecino("Engracia", 256.0f, -440.8f, 90f, voz: "Vecina");
        Vecino("Eusebio", 255.3f, -425.8f, 90f);              // en el banco de la plaza
        Vecino("Fermin", 270.0f, -432.0f, 0f, navMesh: true);

        // ── Lo que cuentan: durante el Cap. 3 (animales con aro, carros de noche) y el resto del juego ──
        var enCap3 = Tramo(S3.Aldea, FinCap3);
        Charla("Remedios", enCap3,
            Frase("REMEDIOS_C3_1", "CHAR_REMEDIOS", "¿Lo de los animales con aro? Pregunta en la posada. Yo no tengo tiempo ni de rascarme.", "The collared animals? Ask at the inn. I don't even have time to scratch myself."),
            Frase("REMEDIOS_C3_2", "CHAR_REMEDIOS", "Un perro con collar raro se pasó la noche rascándose contra el cobertizo de las redes. Lo oí desde casa.", "A dog with a strange collar spent the whole night scratching against the net shed. I heard it from home."));
        Charla("Remedios", null,
            Frase("REMEDIOS_1", "CHAR_REMEDIOS", "Estas cajas no se van a llevar solas. Ojalá.", "These crates won't carry themselves. I wish."),
            Frase("REMEDIOS_2", "CHAR_REMEDIOS", "Si el mar está bueno, hay pescado. Si no, hay cajas igual.", "If the sea is kind, there's fish. If not, there are crates anyway."));
        Charla("Anselmo", enCap3,
            Frase("ANSELMO_C3_1", "CHAR_ANSELMO", "Hace semanas que las gaviotas no vuelan hacia el bosque. Ni ellas se fían.", "The gulls haven't flown toward the forest for weeks. Not even they trust it."),
            Frase("ANSELMO_C3_2", "CHAR_ANSELMO", "¿Luces entre los árboles? Yo solo me fío de las del faro.", "Lights between the trees? The only lights I trust are the lighthouse's."));
        Charla("Anselmo", null,
            Frase("ANSELMO_1", "CHAR_ANSELMO", "Cuarenta años en este muelle y el mar todavía me sorprende.", "Forty years on this pier and the sea still surprises me."),
            Frase("ANSELMO_2", "CHAR_ANSELMO", "No hagas ruido, que espantas a los peces. Bueno, a los que queden.", "Keep it down, you'll scare the fish. Well, whatever fish are left."));
        Charla("Engracia", null,
            Frase("ENGRACIA_1", "CHAR_ENGRACIA", "Remedios, pociones y algo de comer. ¿Qué te pongo?", "Remedies, potions and something to eat. What can I get you?"),
            Frase("ENGRACIA_2", "CHAR_ENGRACIA", "Lo que no tenga lo pido al Reino. Tarda, pero llega.", "Whatever I don't have, I order from the Kingdom. It takes a while, but it gets here."));
        Charla("Eusebio", enCap3,
            Frase("EUSEBIO_C3_1", "CHAR_EUSEBIO", "Lucía dice que vio un zorro con collar junto a la cerca del huerto. Esa niña ve muchas cosas… pero esta vez la creo.", "Lucía says she saw a fox with a collar by the garden fence. That girl sees all sorts… but this time I believe her."),
            Frase("EUSEBIO_C3_2", "CHAR_EUSEBIO", "En mis tiempos, los demonios se quedaban en los cuentos.", "In my day, demons stayed in fairy tales.", NPCEmotion.Worried));
        Charla("Eusebio", null,
            Frase("EUSEBIO_1", "CHAR_EUSEBIO", "Siéntate un rato, chico. Desde este banco se ve todo el puerto.", "Sit down a while, lad. You can see the whole harbour from this bench."),
            Frase("EUSEBIO_2", "CHAR_EUSEBIO", "Mi mujer decía que el mar huele a promesas. Yo digo que huele a sardina.", "My wife used to say the sea smells of promises. I say it smells of sardines.", NPCEmotion.Happy));
        Charla("Fermin", enCap3,
            Frase("FERMIN_C3_1", "CHAR_FERMIN", "Por el camino del bosque pasan carros de noche. Yo cierro la ventana y en paz.", "Carts go down the forest road at night. I shut my window and that's that."),
            Frase("FERMIN_C3_2", "CHAR_FERMIN", "¿Has probado la sopa de la posada? Da fuerzas hasta para mover una cuchara.", "Have you tried the inn's soup? It gives you strength enough to move a spoon.", NPCEmotion.Happy));
        Charla("Fermin", null,
            Frase("FERMIN_1", "CHAR_FERMIN", "Buen día para pasear. Aquí todos los días son buenos para pasear.", "Nice day for a walk. Every day's a nice day for a walk around here."),
            Frase("FERMIN_2", "CHAR_FERMIN", "Si buscas la tienda, Engracia tiene el puesto al oeste de la plaza.", "If you're after the shop, Engracia's stall is on the west side of the square."));

        // Las nasas junto al cobertizo de las redes (siempre).
        const string Nasa = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Fishingcage01_a01.prefab";
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Nasa del cobertizo 1", Prefab = Nasa, Pos = new Vector3(296.3f, float.NaN, -415.4f), Rumbo = 90f, Tamano = 0.9f, Colision = true });
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Nasa del cobertizo 2", Prefab = Nasa, Pos = new Vector3(296.4f, float.NaN, -414.3f), Rumbo = 75f, Tamano = 0.8f, Colision = true });

        informe.AppendLine($"Aldea del mar: {VecinosDeLaAldea.Length - 1} vecinos ({nuevos} prefabs nuevos), agendas y nasas.");
    }

    private static void Vecino(string nombre, float x, float z, float rumbo, float y = float.NaN, bool navMesh = false, string voz = "Vecino")
    {
        string spawn = "SPAWN_" + nombre;
        s_puntos.Add(new PuntoDeRoster { Cap = Aldea, SpawnId = spawn, Pos = new Vector3(x, y, z), Rumbo = rumbo });
        s_papeles.Add(new Papel { SpawnId = spawn, Prefab = RutaDeVecino(nombre), Nombre = nombre, Id = "NPC_" + nombre, Voz = voz, NavMesh = navMesh });
    }

    /// <summary>Una frase suelta de un vecino (un diálogo de caja de una línea).</summary>
    private static string Frase(string clave, string hablante, string es, string en, NPCEmotion e = NPCEmotion.Neutral) =>
        DialogoDeCaja("DLG_ALDEA_" + clave, "DLG_ALDEA_" + clave, L(hablante, es, en, e));

    /// <summary>Añade a la agenda del vecino una entrada con sus frases (una por charla). Sin tramo, siempre.</summary>
    private static void Charla(string nombre, TramoDeHistoria tramo, params string[] frases)
    {
        string actor = "NPC_" + nombre;
        var agenda = s_agendas.Find(a => a.Actor == actor);
        if (agenda == null) { agenda = new Agenda { Actor = actor }; s_agendas.Add(agenda); }
        string etiqueta = tramo == null ? "Aldea del mar · siempre" : "Cap. 3 · la aldea del mar";
        agenda.Entradas.Add((etiqueta, tramo ?? new TramoDeHistoria(), PresenciaEnAgenda.SinCambios, null, string.Join("|", frases)));
    }

    /// <summary>Lo que la aldea pone en MainWorld: la ruta de cajas y las marcas de las pistas.</summary>
    private static void PonerExtrasAldeaDelMar(Transform raiz, StringBuilder informe)
    {
        var padre = Hijo(raiz, Aldea + " · vida");

        // Remedios lleva cajas de pescado del muelle al puesto de pescado de la plaza.
        var ruta = new GameObject("Ruta de cajas de Remedios");
        ruta.transform.SetParent(padre, false);
        var r = ruta.AddComponent<NPCWorkRoute>();
        r.puestoId = PuestoDeRemedios;
        r.puntoRecogida = PuntoDeRuta(ruta.transform, "Recogida (muelle)", new Vector3(268.5f, float.NaN, -447.2f), 180f);
        r.puntoEntrega = PuntoDeRuta(ruta.transform, "Entrega (puesto de pescado)", new Vector3(283.8f, float.NaN, -438.6f), 90f);
        r.materialCaja = MaterialDeColor(new Color(0.55f, 0.38f, 0.22f), "Aldea_CajaDePescado");
        r.tamanoCaja = new Vector3(0.38f, 0.22f, 0.3f);
        r.cajasPorPila = 4;

        // Pistas de la madera: lo que se ve, dentro del punto de inspección (sale y se va con su tramo).
        var inspeccion = raiz.Find(Cap3 + " · inspección");
        var huerto = inspeccion != null ? inspeccion.Find("Pista · la cerca del huerto/Punto") : null;
        var cobertizo = inspeccion != null ? inspeccion.Find("Pista · el cobertizo de las redes/Punto") : null;
        if (huerto != null) MarcasDelHuerto(huerto); else informe.AppendLine("AVISO: falta el punto de la cerca del huerto.");
        if (cobertizo != null) MarcasDelCobertizo(cobertizo); else informe.AppendLine("AVISO: falta el punto del cobertizo de las redes.");
        informe.AppendLine("Aldea del mar: ruta de cajas de Remedios y marcas de las pistas de la madera.");
    }

    private static Transform PuntoDeRuta(Transform padre, string nombre, Vector3 pos, float rumbo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(AlSuelo(pos), Quaternion.Euler(0f, rumbo, 0f));
        return go.transform;
    }

    /// <summary>Pelo rojizo enganchado y arañazos en la cerca; huellas pequeñas en la tierra, delante.</summary>
    private static void MarcasDelHuerto(Transform punto)
    {
        var marcas = Hijo(punto, "Marcas");
        var suelo = punto.position;
        // La cara de la cerca que da al camino (norte): se busca con un rayo desde delante.
        var cara = Superficie(suelo + new Vector3(0f, 0.6f, 1.5f), Vector3.back, suelo + new Vector3(0f, 0.6f, -0.4f));
        var pelo = MaterialDeColor(new Color(0.78f, 0.33f, 0.12f), "Aldea_PeloDeZorro");
        var aranazo = MaterialDeColor(new Color(0.22f, 0.14f, 0.08f), "Aldea_Aranazo");
        var huella = MaterialDeColor(new Color(0.24f, 0.17f, 0.1f), "Aldea_Huella");
        var giro = Quaternion.LookRotation(-cara.normal);
        Pieza(PrimitiveType.Sphere, marcas, "Pelo 1", cara.point + cara.normal * 0.03f + Vector3.up * -0.05f, giro, new Vector3(0.16f, 0.07f, 0.06f), pelo);
        Pieza(PrimitiveType.Sphere, marcas, "Pelo 2", cara.point + cara.normal * 0.03f + giro * new Vector3(0.12f, 0.08f, 0f), giro * Quaternion.Euler(0f, 0f, 30f), new Vector3(0.12f, 0.05f, 0.05f), pelo);
        Pieza(PrimitiveType.Sphere, marcas, "Pelo 3", cara.point + cara.normal * 0.03f + giro * new Vector3(-0.1f, -0.12f, 0f), giro * Quaternion.Euler(0f, 0f, -20f), new Vector3(0.1f, 0.05f, 0.05f), pelo);
        for (int i = 0; i < 3; i++)
            Pieza(PrimitiveType.Cube, marcas, $"Arañazo {i + 1}", cara.point + cara.normal * 0.012f + giro * new Vector3(-0.25f + i * 0.06f, 0.05f, 0f),
                giro * Quaternion.Euler(0f, 0f, 18f), new Vector3(0.018f, 0.28f, 0.01f), aranazo);
        // Huellas: un rastro que llega a la cerca.
        for (int i = 0; i < 6; i++)
        {
            var p = AlSuelo(new Vector3(suelo.x + 0.9f - i * 0.18f + (i % 2) * 0.08f, float.NaN, suelo.z + 1.4f - i * 0.22f));
            Pieza(PrimitiveType.Cylinder, marcas, $"Huella {i + 1}", p + Vector3.up * 0.01f, Quaternion.Euler(0f, 35f, 0f), new Vector3(0.09f, 0.004f, 0.12f), huella);
        }
    }

    /// <summary>Arañazos a la altura del cuello de un perro y restos de hierro que aún brillan, en la pared de madera.</summary>
    private static void MarcasDelCobertizo(Transform punto)
    {
        var marcas = Hijo(punto, "Marcas");
        var suelo = punto.position;
        // La pared del cobertizo está al este del punto.
        var cara = Superficie(suelo + new Vector3(-0.6f, 0.5f, 0f), Vector3.right, suelo + new Vector3(0.7f, 0.5f, 0f));
        var aranazo = MaterialDeColor(new Color(0.2f, 0.12f, 0.07f), "Aldea_Aranazo");
        var hierro = MaterialQueBrilla(new Color(0.55f, 0.75f, 1f), "Aldea_HierroQueBrilla", 1.6f);
        var giro = Quaternion.LookRotation(-cara.normal);
        for (int i = 0; i < 4; i++)
            Pieza(PrimitiveType.Cube, marcas, $"Arañazo {i + 1}", cara.point + cara.normal * 0.012f + giro * new Vector3(-0.15f + i * 0.07f, 0f, 0f),
                giro * Quaternion.Euler(0f, 0f, 22f), new Vector3(0.02f, 0.34f, 0.012f), aranazo);
        Pieza(PrimitiveType.Cube, marcas, "Hierro 1", cara.point + cara.normal * 0.016f + giro * new Vector3(0.05f, 0.06f, 0f), giro * Quaternion.Euler(0f, 0f, -8f), new Vector3(0.22f, 0.012f, 0.008f), hierro);
        Pieza(PrimitiveType.Cube, marcas, "Hierro 2", cara.point + cara.normal * 0.016f + giro * new Vector3(0.02f, -0.04f, 0f), giro * Quaternion.Euler(0f, 0f, 6f), new Vector3(0.16f, 0.012f, 0.008f), hierro);
    }

    /// <summary>
    /// El punto de la madera que toca un rayo desde <paramref name="desde"/>; si no da con nada, el punto de
    /// respaldo mirando hacia el rayo.
    /// </summary>
    private static RaycastHit Superficie(Vector3 desde, Vector3 direccion, Vector3 respaldo)
    {
        var hits = Physics.RaycastAll(desde, direccion, 3f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
            if (hit.collider.GetComponentInParent<Interactable>() == null) return hit;
        return new RaycastHit { point = respaldo, normal = -direccion };
    }

    private static void Pieza(PrimitiveType tipo, Transform padre, string nombre, Vector3 pos, Quaternion giro, Vector3 escala, Material mat)
    {
        var go = Primitiva(tipo, padre, nombre, Vector3.zero, escala, mat);
        go.transform.SetPositionAndRotation(pos, giro);
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
    }

    /// <summary>Un color liso que además brilla (emisión).</summary>
    private static Material MaterialQueBrilla(Color color, string nombre, float intensidad)
    {
        var m = MaterialDeColor(color, nombre);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensidad);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }
}
