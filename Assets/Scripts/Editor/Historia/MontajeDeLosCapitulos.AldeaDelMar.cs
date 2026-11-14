using System.Collections.Generic;
using System.Linq;
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
    private const string PuestoDelPosadero = "posada_posadero";
    private static readonly string[] VecinosDeLaAldea = { "Posadero", "Remedios", "Anselmo", "Engracia", "Eusebio", "Fermin", "Benito", "Rosario" };
    /// <summary>Vecinos que se quedan sentados: su punto de salida se ajusta al asiento que tienen debajo.</summary>
    private static readonly string[] SentadosDeLaAldea = { "Eusebio", "Benito", "Rosario" };
    /// <summary>Atrezo que va encima de una mesa (se apoya en lo que tenga debajo, no en el suelo).</summary>
    private static readonly HashSet<string> s_sobreLaMesa = new();

    private static string RutaDeVecino(string nombre) => $"{CarpetaAldea}/{nombre}.prefab";

    /// <summary>Prefabs, textos, roster y agendas de la aldea. Se llama desde el montaje del Cap. 3.</summary>
    private static void MontarAldeaDelMar(StringBuilder informe)
    {
        // El nombre del pueblo se anuncia al entrar en su zona de ambiente (PresentacionDeLugares).
        const string nombre = "LUGAR_ALDEA_DEL_MAR";
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, nombre, "Aldea del Mar", "Seaside Village");
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
        // Lucía es una niña: su cuerpo y sus caras salen de NinosHechosEnBlender, no de las caras de los NPCs.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RutaDeVecino("Lucia")) == null)
        {
            PersonajesConAspectoFijo.Asegurar("Lucia");
            NinosHechosEnBlender.Aplicar(informe);
        }

        foreach (var (clave, es, en) in new[]
        {
            ("CHAR_REMEDIOS", "Remedios", "Remedios"), ("CHAR_ANSELMO", "Anselmo", "Anselmo"),
            ("CHAR_ENGRACIA", "Engracia", "Engracia"), ("CHAR_EUSEBIO", "Eusebio", "Eusebio"),
            ("CHAR_FERMIN", "Fermín", "Fermín"), ("CHAR_BENITO", "Benito", "Benito"), ("CHAR_ROSARIO", "Rosario", "Rosario"),
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
            ("DLG_BENITO_01", "Un día de pesca y un plato caliente. No pido más.", "A day's fishing and a hot meal. I don't ask for more."),
            ("DLG_ROSARIO_01", "Aquí se come la mejor sopa de la costa. Y la única, también.", "Best soup on the coast, this. Only soup on the coast, too."),
            ("DLG_LUCIA_01", "¡Hola! Mi madre dice que no hable con forasteros… pero tú no pareces forastero.", "Hi! My mum says not to talk to strangers… but you don't look like a stranger."),
        }) TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, clave, es, en);

        // ── Dónde están (siempre; el posadero y Lucía, desde el Cap. 3, en la tabla del capítulo) ──
        Vecino("Remedios", 268.5f, -446.0f, 180f, navMesh: true, voz: "Vecina");
        Vecino("Anselmo", 270.0f, -459.5f, 180f, y: 3.76f);   // al final del muelle: la altura de los tablones, no la del fondo
        Vecino("Engracia", 256.0f, -440.8f, 90f, voz: "Vecina");
        Vecino("Eusebio", 255.3f, -425.8f, 90f);              // en el banco de la plaza
        Vecino("Fermin", 270.0f, -432.0f, 0f, navMesh: true);
        Vecino("Benito", 282.6f, -430.8f, 90f);                // comiendo en la terraza de la posada, en su banco
        Vecino("Rosario", 285.4f, -430.8f, 270f, voz: "Vecina"); // enfrente, en el banco junto a la puerta

        // ── Lo que cuentan: durante el Cap. 3 (animales con aro, carros de noche) y el resto del juego ──
        var enCap3 = Tramo(S3.Aldea, FinCap3);
        Charla("Remedios", enCap3,
            Frase("REMEDIOS_C3_1", "CHAR_REMEDIOS", "¿Lo de los animales con aro? Pregunta en la posada. Yo no tengo tiempo ni de rascarme.", "The collared animals? Ask at the inn. I don't even have time to scratch myself."),
            Frase("REMEDIOS_C3_2", "CHAR_REMEDIOS", "Anoche un perro con un collar raro se pasó horas rascándose contra la casa de las nasas, la del tejado de paja. No me dejó pegar ojo.", "Last night a dog with a strange collar spent hours scratching against the house with the lobster pots, the one with the thatched roof. I didn't sleep a wink."));
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
            Frase("EUSEBIO_C3_1", "CHAR_EUSEBIO", "Lucía, la cría de la posada, dice que vio un zorro con collar en la cerca del huerto. Esa niña ve muchas cosas… pero esta vez la creo.", "Lucía, the little girl from the inn, says she saw a fox with a collar at the garden fence. That girl sees all sorts… but this time I believe her."),
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

        Charla("Benito", enCap3,
            Frase("BENITO_C3_1", "CHAR_BENITO", "¿Animales con aro? Mientras no le pongan uno a mi gato…", "Animals with collars? As long as nobody puts one on my cat…"),
            Frase("BENITO_C3_2", "CHAR_BENITO", "Si quieres saber algo de este pueblo, pregúntale a Remedios. No duerme nunca.", "If you want to know anything about this village, ask Remedios. She never sleeps.", NPCEmotion.Happy));
        Charla("Benito", null,
            Frase("BENITO_1", "CHAR_BENITO", "Un día de pesca y un plato caliente. No pido más.", "A day's fishing and a hot meal. I don't ask for more."),
            Frase("BENITO_2", "CHAR_BENITO", "La sopa de hoy lleva de todo. No preguntes qué.", "Today's soup has a bit of everything. Don't ask what.", NPCEmotion.Happy));
        Charla("Rosario", enCap3,
            Frase("ROSARIO_C3_1", "CHAR_ROSARIO", "Lucía no habla de otra cosa que del zorro. Y de la cuchara, claro.", "Lucía talks about nothing but that fox. And the spoon, of course.", NPCEmotion.Happy),
            Frase("ROSARIO_C3_2", "CHAR_ROSARIO", "Desde que se ven esos animales, cierro el gallinero con dos vueltas de llave.", "Ever since those animals turned up, I lock the henhouse twice over.", NPCEmotion.Worried));
        Charla("Rosario", null,
            Frase("ROSARIO_1", "CHAR_ROSARIO", "Aquí se come la mejor sopa de la costa. Y la única, también.", "Best soup on the coast, this. Only soup on the coast, too."),
            Frase("ROSARIO_2", "CHAR_ROSARIO", "Siéntate, hombre, que de pie se come peor.", "Sit down, love, food goes down better sitting.", NPCEmotion.Happy));

        // La terraza de la posada: la mesa de encuentro (Lucía, su cuenco y la cuchara; la sopa de Will sale
        // en la cinemática) y otra mesa con Benito y Rosario comiendo.
        const string Muebles = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Furniture/";
        const string Comida = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Food/";
        // La mesa va entre el banco de Benito (el mismo de la plaza) y el banco junto a la puerta (Rosario).
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Mesa de la terraza", Prefab = Muebles + "Table/Table01_a03.prefab", Pos = new Vector3(284.0f, float.NaN, -430.8f), Rumbo = 90f, Colision = true });
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Banco de la terraza (Benito)", Prefab = Muebles + "Chair/Chair01_a01.prefab", Pos = new Vector3(282.6f, float.NaN, -430.8f), Rumbo = 90f, Colision = true });
        SobreLaMesa(Aldea, "Cuenco de Benito", Comida + "Bowl01_c01.prefab", 283.65f, -430.8f, 0f, 0.24f);
        SobreLaMesa(Aldea, "Cuenco de Rosario", Comida + "Bowl01_c02.prefab", 284.35f, -430.8f, 0f, 0.24f);
        SobreLaMesa(Aldea, "Pan de la terraza", Comida + "Bread01_a01.prefab", 284.0f, -431.25f, 30f, 0.28f);
        SobreLaMesa(Aldea, "Vaso de la terraza", Comida + "Glass01_a01.prefab", 284.0f, -430.3f, 0f, 0.12f);
        // Lucía es más baja que las mesas de la terraza: come en una mesita a su altura, al sur de la mesa de encuentro.
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Mesita de Lucía", Prefab = Muebles + "Table/Table03_a01.prefab", Pos = new Vector3(277.3f, float.NaN, -429.7f), Rumbo = 0f, Tamano = 0.75f, Colision = true });
        SobreLaMesa(Aldea, "Cuenco de Lucía", Comida + "Bowl01_a01.prefab", 277.5f, -429.8f, 0f, 0.2f);
        // Lo que mueve la cinemática de la posada (PROP_…, en su escenario): la cuchara, su brillo y la sopa de Will.
        SobreLaMesa(Cap3, "PROP_Cuchara Cap3", Muebles + "Kitchen/Spoon02_a01.prefab", 277.15f, -429.62f, 90f, 0.38f);
        SobreLaMesa(Cap3, "PROP_SopaWill Cap3", Comida + "Bowl01_c01.prefab", 279.2f, -427.35f, 0f, 0.24f);
        s_atrezo.Add(new Atrezo { Cap = Cap3, Nombre = "PROP_BrilloCuchara Cap3", Prefab = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Sparkle/Sparkle_ellow.prefab",
            Pos = new Vector3(277.15f, float.NaN, -429.62f), Tamano = 0f });

        // Las nasas junto a la casa de las nasas (siempre).
        const string Nasa = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Goods/Fishingcage01_a01.prefab";
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Nasa del cobertizo 1", Prefab = Nasa, Pos = new Vector3(296.3f, float.NaN, -415.4f), Rumbo = 90f, Tamano = 0.9f, Colision = true });
        s_atrezo.Add(new Atrezo { Cap = Aldea, Nombre = "Nasa del cobertizo 2", Prefab = Nasa, Pos = new Vector3(296.4f, float.NaN, -414.3f), Rumbo = 75f, Tamano = 0.8f, Colision = true });

        informe.AppendLine($"Aldea del mar: {VecinosDeLaAldea.Length - 1} vecinos ({nuevos} prefabs nuevos), agendas y nasas.");
    }

    /// <summary>Una pieza de atrezo que va encima de una mesa: se apoya después en lo que tenga debajo.</summary>
    private static void SobreLaMesa(string cap, string nombre, string prefab, float x, float z, float rumbo, float tamano)
    {
        s_atrezo.Add(new Atrezo { Cap = cap, Nombre = nombre, Prefab = prefab, Pos = new Vector3(x, float.NaN, z), Rumbo = rumbo, Tamano = tamano });
        s_sobreLaMesa.Add(nombre);
    }

    /// <summary>Apoya en el mueble que tiene debajo cada pieza de <see cref="s_sobreLaMesa"/>; el brillo de la cuchara va con ella.</summary>
    private static void ApoyarLoQueVaEnLaMesa(Transform raiz, StringBuilder informe)
    {
        Physics.SyncTransforms();   // el atrezo recién puesto tiene que contar para los rayos
        int n = 0;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (!s_sobreLaMesa.Contains(t.name)) continue;
            var b = Limites(t.gameObject);
            float mejor = float.NegativeInfinity;
            foreach (var hit in Physics.RaycastAll(new Vector3(t.position.x, b.max.y + 2f, t.position.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(t) && hit.point.y > mejor) mejor = hit.point.y;
            if (float.IsNegativeInfinity(mejor)) continue;
            t.position += Vector3.up * (mejor + 0.005f - b.min.y);
            n++;
        }
        var cuchara = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PROP_Cuchara Cap3");
        var brillo = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PROP_BrilloCuchara Cap3");
        if (cuchara != null && brillo != null)
        {
            brillo.SetParent(cuchara, true);
            brillo.position = Limites(cuchara.gameObject).center;
            brillo.localScale = Vector3.one * 0.35f / Mathf.Max(0.001f, cuchara.lossyScale.x);
            brillo.gameObject.SetActive(false);
        }
        // La sopa de Will no está en la mesa hasta que se la sirven en la cinemática.
        var sopa = raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "PROP_SopaWill Cap3");
        if (sopa != null) sopa.gameObject.SetActive(false);
        informe.AppendLine($"Aldea del mar: {n} piezas apoyadas en las mesas de la terraza.");
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

        // El posadero ya no sirve con bandeja: los personajes no llevan objetos en las manos (Raúl,
        // 13 nov). Atiende en la puerta de la posada con sus gestos de espera. Ver INC-1155.
        AsegurarOficiosDeLaAldea(informe);

        // Pistas de la madera: lo que se ve, dentro del punto de inspección (sale y se va con su tramo).
        var inspeccion = raiz.Find(Cap3 + " · inspección");
        var huerto = inspeccion != null ? inspeccion.Find("Pista · la cerca del huerto/Punto") : null;
        var cobertizo = inspeccion != null ? inspeccion.Find("Pista · el cobertizo de las redes/Punto") : null;
        if (huerto != null) MarcasDelHuerto(huerto); else informe.AppendLine("AVISO: falta el punto de la cerca del huerto.");
        if (cobertizo != null) MarcasDelCobertizo(cobertizo); else informe.AppendLine("AVISO: falta el punto del cobertizo de las redes.");

        // Los que se quedan sentados (Eusebio en el banco, Benito y Rosario en la terraza), sentados de verdad.
        foreach (var n in SentadosDeLaAldea)
        {
            var spawn = raiz.Find(Aldea + " · roster/SP_SPAWN_" + n);
            if (spawn != null) SentarEnSuAsiento(n, spawn, informe); else informe.AppendLine($"AVISO: falta el punto de salida de {n}.");
        }
        ApoyarLoQueVaEnLaMesa(raiz, informe);
        informe.AppendLine("Aldea del mar: ruta de cajas de Remedios y marcas de las pistas de la madera.");
    }

    /// <summary>
    /// Coloca el punto de salida de un vecino que se queda sentado (actividad fija) para que, con la pose
    /// de sentado de su prefab, la cadera quede sobre el asiento que tiene debajo y los pies en el suelo.
    /// El asiento es el mueble con colisión más cercano bajo el punto; el borde delantero es el lado al que
    /// mira el punto. Se mide la pose de verdad (como el horno de guiones) y se elige, entre las actividades
    /// de sentarse, la que deja los pies más cerca del suelo. La actividad elegida se escribe en su
    /// configuración de ambiente.
    /// </summary>
    private static void SentarEnSuAsiento(string nombre, Transform punto, StringBuilder informe)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaDeVecino(nombre));
        var ambiente = AssetDatabase.LoadAssetAtPath<Game.NPC.Modules.NPCAmbientConfig>($"Assets/_NPCs/Ambient/NPC_Ambient_{nombre}.asset");
        if (prefab == null || ambiente == null) { informe.AppendLine($"AVISO: no puedo sentar a {nombre} (falta su prefab o su ambiente)."); return; }

        // El asiento: la colisión con la cara de arriba más alta a menos de 0,7 m del suelo, justo debajo.
        Physics.SyncTransforms();
        float rumbo = punto.eulerAngles.y;
        var frente = Quaternion.Euler(0f, rumbo, 0f) * Vector3.forward;
        Vector3 suelo = AlSuelo(new Vector3(punto.position.x, float.NaN, punto.position.z));
        Collider asiento = null;
        foreach (var hit in Physics.RaycastAll(suelo + Vector3.up * 1.2f, Vector3.down, 1.4f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.point.y - suelo.y > 0.15f && hit.point.y - suelo.y < 0.7f && (asiento == null || hit.point.y > asiento.bounds.max.y))
                asiento = hit.collider;
        if (asiento == null) { informe.AppendLine($"AVISO: no hay asiento bajo {nombre}."); return; }
        var b = asiento.bounds;
        // Borde delantero del asiento en la dirección a la que mira.
        float fondo = Mathf.Abs(frente.x) * b.extents.x + Mathf.Abs(frente.z) * b.extents.z;
        Vector3 borde = new Vector3(b.center.x, b.max.y, b.center.z) + frente * fondo;
        borde = new Vector3(Mathf.Abs(frente.x) > 0.5f ? borde.x : punto.position.x, borde.y, Mathf.Abs(frente.z) > 0.5f ? borde.z : punto.position.z);
        // La cadera, un poco hacia dentro del borde y por encima de la tabla.
        Vector3 cadera = borde - frente * 0.16f + Vector3.up * 0.09f;

        var catalogo = CapturaDeEscenario.Catalogo(prefab);
        var pose = Object.Instantiate(prefab);
        pose.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animador = pose.GetComponentInChildren<Animator>(true);
            var mejor = (act: NPCAmbientActivity.None, raiz: Vector3.zero, error: float.MaxValue);
            foreach (var act in new[] { NPCAmbientActivity.SitLow, NPCAmbientActivity.SitMedium, NPCAmbientActivity.SitHigh })
            {
                string estado = NPCSimpleAnimator.GetActivityLoopState(act);
                var entrada = catalogo.Find(e => e.estado == estado);
                var clip = entrada.clip == null ? null : System.Array.Find(animador.runtimeAnimatorController.animationClips, c => c.name == entrada.clip);
                if (clip == null) continue;
                animador.Rebind();
                pose.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, rumbo, 0f));
                Horno.MuestrearEstado(pose, clip, estado, 0f);
                animador.Update(0f);
                Vector3 h = animador.GetBoneTransform(HumanBodyBones.Hips).position;
                float pie = Mathf.Min(animador.GetBoneTransform(HumanBodyBones.LeftFoot).position.y, animador.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                Vector3 raiz = cadera - h;
                // Error: lo que los pies quedan por encima o por debajo del suelo (el tobillo va a ~8 cm).
                float error = Mathf.Abs(raiz.y + pie - (suelo.y + 0.08f));
                if (error < mejor.error) mejor = (act, raiz, error);
            }
            if (mejor.act == NPCAmbientActivity.None) { informe.AppendLine($"AVISO: {nombre} no tiene poses de sentado."); return; }
            punto.position = mejor.raiz;
            if (ambiente.ambientActivity != mejor.act) { ambiente.ambientActivity = mejor.act; EditorUtility.SetDirty(ambiente); }
            informe.AppendLine($"Aldea del mar: {nombre} sentado en «{asiento.name}» ({mejor.act}, raíz {mejor.raiz:F2}, pies a {mejor.error:F2} m del suelo).");
        }
        finally { Object.DestroyImmediate(pose); }
    }

    /// Quita la ruta de servir del posadero de las escenas abiertas y le pone su oficio sin bandeja,
    /// sin tener que montar el capítulo entero. Ver INC-1155.
    [MenuItem("El Sendero/Historia/Aldea del mar: posadero sin bandeja (INC-1155)")]
    private static void PosaderoSinBandeja()
    {
        var informe = new StringBuilder();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var escena = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;
            foreach (var raiz in escena.GetRootGameObjects())
                foreach (var ruta in raiz.GetComponentsInChildren<NPCWorkRoute>(true))
                {
                    if (ruta == null || ruta.puestoId != PuestoDelPosadero) continue;
                    informe.AppendLine($"Quitada «{ruta.name}» de {escena.name}.");
                    UnityEngine.Object.DestroyImmediate(ruta.gameObject);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
                }
        }
        AsegurarOficiosDeLaAldea(informe);
        Debug.Log("[Montaje] " + informe);
    }

    private static Transform PuntoDeRuta(Transform padre, string nombre, Vector3 pos, float rumbo)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(AlSuelo(pos), Quaternion.Euler(0f, rumbo, 0f));
        return go.transform;
    }

    /// <summary>
    /// Que cada vecino haga algo de su oficio (INC-1033): el posadero atiende en la puerta de la posada (sin bandeja, INC-1155),
    /// Anselmo se sienta en el borde del muelle mirando al mar y Engracia atiende su puesto entre ratos quieta.
    /// </summary>
    private static void AsegurarOficiosDeLaAldea(StringBuilder informe)
    {
        var posadero = AmbienteDelVecino("Posadero", "Remedios");
        if (posadero != null)
        {
            posadero.enableWander = false;
            posadero.permitirTrabajo = false;
            posadero.puestoId = "";
            posadero.ambientActivity = NPCAmbientActivity.None;
            EditorUtility.SetDirty(posadero);
        }
        GestosDeEsperaDelVecino("Posadero", "Idle01", "InteractWithPeople_NoWeapon", "Idle02", "Idle03");
        var anselmo = AmbienteDelVecino("Anselmo", "Eusebio");
        if (anselmo != null)
        {
            anselmo.enableWander = false;
            anselmo.permitirTrabajo = false;
            anselmo.puestoId = "";
            anselmo.ambientActivity = NPCAmbientActivity.SitGround;
            EditorUtility.SetDirty(anselmo);
        }
        GestosDeEsperaDelVecino("Engracia", "Idle01", "InteractWithPeople_NoWeapon", "Idle02", "Idle03");
        AssetDatabase.SaveAssets();
        informe.AppendLine("Aldea del mar: el posadero atiende en la posada, Anselmo se sienta en el muelle y Engracia atiende su puesto.");
    }

    /// El ambiente de un vecino (se crea copiando el de otro si no tiene) y puesto en su prefab.
    private static Game.NPC.Modules.NPCAmbientConfig AmbienteDelVecino(string nombre, string plantilla)
    {
        string ruta = $"Assets/_NPCs/Ambient/NPC_Ambient_{nombre}.asset";
        var ambiente = AssetDatabase.LoadAssetAtPath<Game.NPC.Modules.NPCAmbientConfig>(ruta);
        if (ambiente == null)
        {
            if (!AssetDatabase.CopyAsset($"Assets/_NPCs/Ambient/NPC_Ambient_{plantilla}.asset", ruta)) return null;
            ambiente = AssetDatabase.LoadAssetAtPath<Game.NPC.Modules.NPCAmbientConfig>(ruta);
            if (ambiente == null) return null;
        }
        string rutaPrefab = RutaDeVecino(nombre);
        var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var cerebro = raiz.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
            if (cerebro != null)
            {
                var so = new SerializedObject(cerebro);
                var prop = so.FindProperty("configuration.ambientConfig");
                if (prop != null && prop.objectReferenceValue != ambiente)
                {
                    prop.objectReferenceValue = ambiente;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
                }
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        return ambiente;
    }

    /// Los gestos que un vecino hace de vez en cuando mientras está quieto.
    private static void GestosDeEsperaDelVecino(string nombre, params string[] estados)
    {
        string rutaPrefab = RutaDeVecino(nombre);
        var raiz = PrefabUtility.LoadPrefabContents(rutaPrefab);
        try
        {
            var animador = raiz.GetComponentInChildren<NPCSimpleAnimator>(true);
            if (animador == null) return;
            var so = new SerializedObject(animador);
            var lista = so.FindProperty("idleVariationStates");
            if (lista == null) return;
            lista.arraySize = estados.Length;
            for (int i = 0; i < estados.Length; i++) lista.GetArrayElementAtIndex(i).stringValue = estados[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
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

    /// <summary>
    /// En la pared norte de la casa de las nasas, sobre el zócalo de piedra (el yeso claro, donde se ven):
    /// cuatro surcos oscuros a la altura del cuello de un perro, restos de hierro que brillan dentro de ellos,
    /// un mechón de pelo y las huellas que llegan hasta la pared. Tamaños pensados para leerse desde la
    /// cámara del juego, a 3-4 m.
    /// </summary>
    private static void MarcasDelCobertizo(Transform punto)
    {
        var marcas = Hijo(punto, "Marcas");
        var suelo = punto.position;
        // La pared está al sur del punto.
        var cara = Superficie(suelo + new Vector3(0f, 0.7f, 0.3f), Vector3.back, suelo + new Vector3(0f, 0.7f, -0.9f));
        var aranazo = MaterialDeColor(new Color(0.24f, 0.15f, 0.09f), "Aldea_Aranazo");
        var hierro = MaterialQueBrilla(new Color(0.5f, 0.78f, 1f), "Aldea_HierroQueBrilla", 3.2f);
        var pelo = MaterialDeColor(new Color(0.32f, 0.26f, 0.22f), "Aldea_PeloDePerro");
        var huella = MaterialDeColor(new Color(0.24f, 0.17f, 0.1f), "Aldea_Huella");
        var giro = Quaternion.LookRotation(-cara.normal);
        for (int i = 0; i < 4; i++)
            Pieza(PrimitiveType.Cube, marcas, $"Arañazo {i + 1}", cara.point + cara.normal * 0.012f + giro * new Vector3(-0.18f + i * 0.11f, 0.02f * (i % 2), 0f),
                giro * Quaternion.Euler(0f, 0f, 20f), new Vector3(0.045f, 0.5f, 0.012f), aranazo);
        for (int i = 0; i < 3; i++)
            Pieza(PrimitiveType.Cube, marcas, $"Hierro {i + 1}", cara.point + cara.normal * 0.02f + giro * new Vector3(-0.13f + i * 0.11f, -0.04f + i * 0.05f, 0f),
                giro * Quaternion.Euler(0f, 0f, 20f), new Vector3(0.02f, 0.2f, 0.012f), hierro);
        Pieza(PrimitiveType.Sphere, marcas, "Pelo", cara.point + cara.normal * 0.03f + giro * new Vector3(0.24f, -0.12f, 0f), giro * Quaternion.Euler(0f, 0f, -25f), new Vector3(0.14f, 0.06f, 0.05f), pelo);
        // Huellas de perro: vienen de las nasas y acaban en la pared.
        for (int i = 0; i < 6; i++)
        {
            var p = AlSuelo(new Vector3(cara.point.x - 1.1f + i * 0.2f + (i % 2) * 0.1f, float.NaN, cara.point.z + 1.5f - i * 0.24f));
            Pieza(PrimitiveType.Cylinder, marcas, $"Huella {i + 1}", p + Vector3.up * 0.01f, Quaternion.Euler(0f, 150f, 0f), new Vector3(0.12f, 0.004f, 0.15f), huella);
        }
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
