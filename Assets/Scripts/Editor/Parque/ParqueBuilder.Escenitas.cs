using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Los repertorios de escenitas de las atracciones del parque (INC-896): se crean como assets en
/// <see cref="CarpetaDeRepertorios"/> la primera vez y después se respetan (se retocan en el asset).
/// Las frases van con clave PARQUE_* en dialogues_es/en.json.
/// </summary>
public static partial class ParqueBuilder
{
    private const string CarpetaDeRepertorios = "Assets/Art/World/Parque/Repertorios";

    private readonly struct G
    {
        public readonly MomentoDelViaje M; public readonly QuienDelGrupo Q; public readonly string Clave, Texto, Gesto;
        public readonly NPCEmotion E; public readonly float Espera; public readonly bool ALaVez;
        public G(MomentoDelViaje m, QuienDelGrupo q, string clave, string texto, string gesto, NPCEmotion e, float espera, bool aLaVez)
        { M = m; Q = q; Clave = clave; Texto = texto; Gesto = gesto; E = e; Espera = espera; ALaVez = aLaVez; }
    }

    private sealed class E
    {
        public string Nombre; public float Peso; public int Desde, Hasta; public QuienDelGrupo NoSube, SeMarea; public G[] Golpes;
    }

    private static readonly E[] EscenitasBarco =
    {
        new E { Nombre = "El estómago de Will", Peso = 1f, Desde = 1, Hasta = 1, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Will, Golpes = new[]
        {
            new G(MomentoDelViaje.EnLaCola, QuienDelGrupo.Will, "PARQUE_BARCO_1_1", "¡Venga, venga! ¡Este es el bueno!", "Cheer01", NPCEmotion.Happy, 0.2f, false),
            new G(MomentoDelViaje.EnLaCola, QuienDelGrupo.Liam, "PARQUE_BARCO_1_2", "Ese ángulo… ¿Seguro que lo has pensado bien?", "", NPCEmotion.Worried, 0.2f, false),
            new G(MomentoDelViaje.Arrancando, QuienDelGrupo.Estela, "PARQUE_BARCO_1_3", "Si gritas, Will, lo cuento en el pueblo.", "", NPCEmotion.Smirk, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_BARCO_1_4", "¡Aaaah! ¡Mi estómago se ha quedado arriba!", "Fear01", NPCEmotion.Scared, 0.0f, true),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "PARQUE_BARCO_1_5", "¡Más alto!", "Cheer01", NPCEmotion.Happy, 1.2f, false),
            new G(MomentoDelViaje.Bajando, QuienDelGrupo.Liam, "PARQUE_BARCO_1_6", "Cuarenta grados. Y subiendo.", "", NPCEmotion.Thinking, 0.0f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Will, "PARQUE_BARCO_1_7", "El suelo… ¿por qué se mueve el suelo?", "", NPCEmotion.Confused, 0.0f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Estela, "PARQUE_BARCO_1_8", "No me había reído tanto en años.", "Laugh01", NPCEmotion.Happy, 0.2f, false),
        } },
        new E { Nombre = "Brazos arriba", Peso = 1f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.Arrancando, QuienDelGrupo.Estela, "PARQUE_BARCO_2_1", "Esta vez, brazos arriba. Los tres.", "", NPCEmotion.Determined, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_BARCO_2_2", "¡Sin manos!", "Cheer01", NPCEmotion.Happy, 0.0f, true),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "", "", "Cheer02", NPCEmotion.Happy, 0.0f, true),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Liam, "PARQUE_BARCO_2_4", "Esto… no es tan terrible.", "Cheer01", NPCEmotion.Surprised, 0.6f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Estela, "PARQUE_BARCO_2_5", "Otra. Ahora.", "", NPCEmotion.Happy, 0.0f, false),
        } },
        new E { Nombre = "Liam se queda abajo", Peso = 0.6f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Liam, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.EnLaCola, QuienDelGrupo.Liam, "PARQUE_BARCO_3_1", "Id vosotros. Alguien tiene que guardar el sitio.", "", NPCEmotion.Neutral, 0.2f, false),
            new G(MomentoDelViaje.EnLaCola, QuienDelGrupo.Estela, "PARQUE_BARCO_3_2", "Cobarde.", "", NPCEmotion.Smirk, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_BARCO_3_3", "¡Liaaam! ¡Míranos!", "HandWave01", NPCEmotion.Happy, 0.0f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Liam, "PARQUE_BARCO_3_4", "Desde aquí abajo también se oían tus gritos.", "", NPCEmotion.Smirk, 0.0f, false),
        } },
        new E { Nombre = "El cálculo de Liam", Peso = 1f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.Arrancando, QuienDelGrupo.Liam, "PARQUE_BARCO_4_1", "Lo peor es la popa. Por eso nos he sentado aquí.", "", NPCEmotion.Determined, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Liam, "PARQUE_BARCO_4_2", "… He calculado mal.", "Fear01", NPCEmotion.Scared, 0.0f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "PARQUE_BARCO_4_3", "¡Ja! ¡El genio!", "Laugh01", NPCEmotion.Happy, 0.2f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Will, "PARQUE_BARCO_4_4", "¿Y cuánto habías calculado?", "", NPCEmotion.Smirk, 0.0f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Liam, "PARQUE_BARCO_4_5", "Lo justo para no volver a hacerlo.", "", NPCEmotion.Tired, 0.2f, false),
        } },
        new E { Nombre = "Estela no tiene miedo", Peso = 1f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "PARQUE_BARCO_5_1", "¡No me da miedo! ¡Es el viento!", "Fear01", NPCEmotion.Scared, 0.0f, false),
            new G(MomentoDelViaje.Bajando, QuienDelGrupo.Will, "PARQUE_BARCO_5_2", "Estela… me estás estrujando el brazo.", "", NPCEmotion.Smirk, 0.0f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Estela, "PARQUE_BARCO_5_3", "Ni una palabra. A nadie.", "", NPCEmotion.Angry, 0.0f, false),
        } },
    };

    private static readonly E[] EscenitasNoria =
    {
        new E { Nombre = "El parque desde arriba", Peso = 1f, Desde = 1, Hasta = 1, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.Arrancando, QuienDelGrupo.Will, "PARQUE_NORIA_1_1", "Despacito… Esta sí me gusta.", "", NPCEmotion.Happy, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "PARQUE_NORIA_1_2", "Mira. Desde aquí se ve todo el parque.", "", NPCEmotion.Neutral, 0.0f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_NORIA_1_3", "Y las luces… parecen estrellas en el suelo.", "", NPCEmotion.Happy, 0.2f, false),
            new G(MomentoDelViaje.Bajando, QuienDelGrupo.Liam, "PARQUE_NORIA_1_4", "Gracias por traerme.", "", NPCEmotion.Sad, 0.4f, false),
        } },
        new E { Nombre = "Contar las luces", Peso = 1f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_NORIA_2_1", "Una, dos, tres… ¡Cuatrocientas!", "", NPCEmotion.Happy, 0.0f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Liam, "PARQUE_NORIA_2_2", "Ciento dos. Las he contado al subir.", "", NPCEmotion.Smirk, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "PARQUE_NORIA_2_3", "Sois insoportables. Los dos.", "", NPCEmotion.Smirk, 0.2f, false),
        } },
        new E { Nombre = "La parada de arriba", Peso = 1f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_NORIA_3_1", "¿Por qué nos hemos parado?", "Question01", NPCEmotion.Surprised, 0.0f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Estela, "PARQUE_NORIA_3_2", "Si alguien se mueve, lo tiro.", "", NPCEmotion.Angry, 0.2f, false),
            new G(MomentoDelViaje.Bajando, QuienDelGrupo.Liam, "PARQUE_NORIA_3_3", "Es la parada de arriba. Para la vista.", "", NPCEmotion.Neutral, 0.0f, false),
        } },
        new E { Nombre = "Liam se calla", Peso = 0.7f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Nadie, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_NORIA_4_1", "Liam, ¿estás bien? No has dicho nada.", "", NPCEmotion.Worried, 0.0f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Liam, "PARQUE_NORIA_4_2", "Sí. Solo… estaba mirando.", "", NPCEmotion.Worried, 0.3f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Estela, "PARQUE_NORIA_4_3", "Está raro desde que entramos.", "", NPCEmotion.Thinking, 0.0f, false),
        } },
        new E { Nombre = "Estela se queda abajo", Peso = 0.5f, Desde = 2, Hasta = 0, NoSube = QuienDelGrupo.Estela, SeMarea = QuienDelGrupo.Nadie, Golpes = new[]
        {
            new G(MomentoDelViaje.EnLaCola, QuienDelGrupo.Estela, "PARQUE_NORIA_5_1", "Paso. Las norias son para parejitas.", "", NPCEmotion.Smirk, 0.2f, false),
            new G(MomentoDelViaje.EnLaCola, QuienDelGrupo.Will, "PARQUE_NORIA_5_2", "¡Pues subimos Liam y yo!", "Cheer01", NPCEmotion.Happy, 0.2f, false),
            new G(MomentoDelViaje.LoMasAlto, QuienDelGrupo.Will, "PARQUE_NORIA_5_3", "¡Estelaaa! ¡Saluda!", "HandWave01", NPCEmotion.Happy, 0.0f, false),
            new G(MomentoDelViaje.AlBajar, QuienDelGrupo.Estela, "PARQUE_NORIA_5_4", "Os he visto saludar. Qué vergüenza.", "", NPCEmotion.Smirk, 0.0f, false),
        } },
    };

    /// <summary>El repertorio de esa atracción: lo crea si no existe (y si existe, lo deja como esté).</summary>
    private static RepertorioDeAtraccion Repertorio(string nombre, string id, E[] escenitas)
    {
        string ruta = $"{CarpetaDeRepertorios}/Repertorio_{nombre}.asset";
        var existente = AssetDatabase.LoadAssetAtPath<RepertorioDeAtraccion>(ruta);
        if (existente != null) return existente;

        if (!AssetDatabase.IsValidFolder("Assets/Art/World/Parque")) AssetDatabase.CreateFolder("Assets/Art/World", "Parque");
        if (!AssetDatabase.IsValidFolder(CarpetaDeRepertorios)) AssetDatabase.CreateFolder("Assets/Art/World/Parque", "Repertorios");

        var repertorio = ScriptableObject.CreateInstance<RepertorioDeAtraccion>();
        repertorio.id = id;
        repertorio.escenitas = new List<RepertorioDeAtraccion.Escenita>();
        foreach (var e in escenitas)
        {
            var escenita = new RepertorioDeAtraccion.Escenita
            {
                nombre = e.Nombre, peso = e.Peso, desdeElViaje = e.Desde, hastaElViaje = e.Hasta,
                noSube = e.NoSube, seMarea = e.SeMarea, golpes = new List<RepertorioDeAtraccion.Golpe>(),
            };
            foreach (var g in e.Golpes)
                escenita.golpes.Add(new RepertorioDeAtraccion.Golpe
                {
                    momento = g.M, quien = g.Q, clave = g.Clave, texto = g.Texto, gesto = g.Gesto,
                    emocion = g.E, espera = g.Espera, aLaVez = g.ALaVez,
                });
            repertorio.escenitas.Add(escenita);
        }
        AssetDatabase.CreateAsset(repertorio, ruta);
        return repertorio;
    }
}
