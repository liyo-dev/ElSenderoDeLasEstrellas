using System.Collections.Generic;
using System.Text;
using Kits.Puzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Liyodev.NarrativeGraph;

/// <summary>
/// Presencias que acechan (kit de puzles, <see cref="StalkingPresence"/>): mientras se lleva un
/// objeto de misión, algo cruje entre las matas y hay que quedarse quieto hasta que se aleje; si
/// se sigue andando, tira el objeto un poco más allá. La tabla dice el objeto, el tramo, el punto
/// de alivio y cada crujido (sitio, tiempos, matas, dónde cae lo tirado, de dónde llega el ruido y
/// a dónde se va, y si se ve algo un instante). Lo pone todo bajo «Cap · presencias que acechan».
/// Ver INC-1311 y «Claude outputs/caja-vuelta-con-presencia-2026-11-19.md».
/// </summary>
public static partial class MontajeDeLosCapitulos
{
    /// <summary>Un crujido. Posiciones en planta (x, z); se apoyan en el suelo al montar.</summary>
    private sealed class Crujido
    {
        public string Nombre;
        public Vector2 Pos, CaeEn, Desde, Hacia;
        public Vector2[] Matas;
        public Vector2? Ojos;
        public float Radio = 6f, Aviso = 1.2f, Quieto = 2.5f, Repetir;
        public int Orden = -1;
        public bool TiraSiempre;
    }

    private sealed class PresenciaQueAcecha
    {
        public string Cap, Nombre, Objeto;
        public TramoDeHistoria Tramo;
        public float Tope = 3f, RadioDelAlivio = 14f;
        public Vector2 Alivio;
        public List<Crujido> Crujidos = new();
        /// <summary>Lo que piensa el sujeto la primera vez: al primer aviso, al primer golpe y la primera vez que pasa (clave, ES, EN, gesto).</summary>
        public (string clave, string es, string en, string gesto) PrimerAviso, PrimerGolpe, PrimerAlivio;
    }

    private const string RutaMaterialDeOjos = "Assets/ElSendero/Data/OjosDeLaPresencia.mat";
    private const string SonidoDelAviso = "Assets/Audio/FREE SOUND PACK_TM(355)/Misc(58)/Wood_Breaking-012.wav";
    private const string SonidoDeLasHojas = "Assets/Audio/FREE SOUND PACK_TM(355)/Ambiences(43)/Leaves-002.wav";
    private const string SonidoAlTirar = "Assets/Audio/FREE SOUND PACK_TM(355)/Misc(58)/Wood_Hit-022.wav";

    private static TramoDeHistoria MientrasDuraLaMision(string mision)
    {
        var t = new TramoDeHistoria();
        t.empiezaConMisionActiva.Add(mision);
        t.acabaConMisionCompletada.Add(mision);
        return t;
    }

    /// <summary>La tabla.</summary>
    private static List<PresenciaQueAcecha> PresenciasQueAcechan() => new()
    {
        // Cap. 1, la caja de Eldran (ELDRAN_MISSION2): ida por el Camino del Reino, vuelta en bucle
        // por el soto del este y el Camino de las Granjas hasta Eldran (Eldran_Bosque, 25.8, -82.6).
        new PresenciaQueAcecha
        {
            Cap = Cap1, Nombre = "La presencia de la caja de Eldran",
            Objeto = Cap1 + " · objetos de misión/Goods_Interactable",
            Tramo = MientrasDuraLaMision("ELDRAN_MISSION2"),
            Tope = 3f, Alivio = new Vector2(25.8f, -82.6f), RadioDelAlivio = 14f,
            PrimerAviso = ("PRESENCIA_CAJA_AVISO", "¿Qué ha sido eso…?", "What was that…?", "Sobresalto01"),
            PrimerGolpe = ("PRESENCIA_CAJA_GOLPE", "¡Algo me ha tirado la caja! Si vuelve a sonar, mejor me quedo quieto hasta que se vaya.",
                "Something knocked the box out of my arms! If I hear it again, I'd better keep still until it's gone.", null),
            PrimerAlivio = ("PRESENCIA_CAJA_ALIVIO", "…Se ha ido. Vamos, que Eldran espera.", "…It's gone. Come on, Eldran's waiting.", null),
            Crujidos =
            {
                // 1. Demostración: crujido largo y la caja cae sí o sí, hacia delante (la bajada del soto).
                new Crujido { Nombre = "1 · Bajando del fresno", Orden = 0, TiraSiempre = true,
                    Pos = new Vector2(95f, -4f), Radio = 5f, Aviso = 1.8f, Quieto = 0.5f,
                    CaeEn = new Vector2(97f, -12f), Desde = new Vector2(84f, -6f), Hacia = new Vector2(80f, 6f),
                    Matas = new[] { new Vector2(91.5f, -5.5f), new Vector2(92.5f, -8f), new Vector2(90.5f, -9.5f) } },
                // 2. Primera de verdad: aviso largo y evidente; quieto, se va y se ven unos ojos.
                new Crujido { Nombre = "2 · Entre los árboles del soto", Orden = 1,
                    Pos = new Vector2(100f, -24f), Radio = 6f, Aviso = 1.5f, Quieto = 3f,
                    CaeEn = new Vector2(103.5f, -32f), Desde = new Vector2(90f, -20f), Hacia = new Vector2(86f, -34f),
                    Matas = new[] { new Vector2(96f, -21.5f), new Vector2(96.5f, -24.5f), new Vector2(97.5f, -27.5f) },
                    Ojos = new Vector2(92.5f, -27f) },
                // 3. En el Camino de las Granjas, de vuelta hacia Eldran: más corto.
                new Crujido { Nombre = "3 · Camino de las Granjas", Orden = 2,
                    Pos = new Vector2(82f, -70f), Radio = 6f, Aviso = 1f, Quieto = 2.5f,
                    CaeEn = new Vector2(75.5f, -75.5f), Desde = new Vector2(88f, -60f), Hacia = new Vector2(96f, -52f),
                    Matas = new[] { new Vector2(84.5f, -63.5f), new Vector2(82f, -63f), new Vector2(79.5f, -64.5f) },
                    Ojos = new Vector2(88f, -60.5f) },
                // Lado del camino de ida: crujido que repite y tira la caja de vuelta hacia el soto.
                new Crujido { Nombre = "Ida · entre el fresno y el Camino del Reino", Repetir = 8f,
                    Pos = new Vector2(80f, -1f), Radio = 6f, Aviso = 1f, Quieto = 2f,
                    CaeEn = new Vector2(88f, -5f), Desde = new Vector2(74f, 4f), Hacia = new Vector2(72f, 14f),
                    Matas = new[] { new Vector2(77.5f, 1.5f), new Vector2(76.5f, -1.5f), new Vector2(77f, -4.5f) } },
                new Crujido { Nombre = "Ida · Camino del Reino", Repetir = 8f,
                    Pos = new Vector2(68f, -16f), Radio = 7f, Aviso = 1f, Quieto = 2f,
                    CaeEn = new Vector2(76f, -19f), Desde = new Vector2(60f, -12f), Hacia = new Vector2(56f, -2f),
                    Matas = new[] { new Vector2(62.5f, -13f), new Vector2(61.5f, -16f), new Vector2(62f, -19f) } },
            },
        },
    };

    private static void PonerPresenciasQueAcechan(Transform raiz, StringBuilder informe)
    {
        var sfx = GrupoDelMezclador("SFX");
        var clipAviso = AssetDatabase.LoadAssetAtPath<AudioClip>(SonidoDelAviso);
        var clipHojas = AssetDatabase.LoadAssetAtPath<AudioClip>(SonidoDeLasHojas);
        var clipTirar = AssetDatabase.LoadAssetAtPath<AudioClip>(SonidoAlTirar);
        if (clipAviso == null || clipHojas == null || clipTirar == null)
            informe.AppendLine("AVISO: presencias que acechan: falta algún sonido (aviso, hojas o golpe).");

        foreach (var pr in PresenciasQueAcechan())
        {
            var objeto = raiz.Find(pr.Objeto);
            if (objeto == null) informe.AppendLine($"AVISO: presencia «{pr.Nombre}»: no encuentro «{pr.Objeto}»; actuará con cualquier cosa que se lleve.");

            var padre = Hijo(raiz, pr.Cap + " · presencias que acechan");
            var soporte = Hijo(padre, pr.Nombre + " (tramo)");
            var go = new GameObject(pr.Nombre);
            go.transform.SetParent(soporte, false);
            var presencia = go.AddComponent<StalkingPresence>();

            var alivio = new GameObject("Alivio").transform;
            alivio.SetParent(go.transform, false);
            alivio.position = AlSuelo(new Vector3(pr.Alivio.x, float.NaN, pr.Alivio.y));

            var sp = new SerializedObject(presencia);
            sp.FindProperty("objeto").objectReferenceValue = objeto != null ? objeto.gameObject : null;
            sp.FindProperty("topeDeVelocidad").floatValue = pr.Tope;
            sp.FindProperty("alivio").objectReferenceValue = alivio;
            sp.FindProperty("radioDelAlivio").floatValue = pr.RadioDelAlivio;
            Pensamiento(sp.FindProperty("alPrimerAviso"), pr.PrimerAviso);
            Pensamiento(sp.FindProperty("alPrimerGolpe"), pr.PrimerGolpe);
            Pensamiento(sp.FindProperty("alPrimerAlivio"), pr.PrimerAlivio);
            sp.ApplyModifiedPropertiesWithoutUndo();

            foreach (var c in pr.Crujidos)
                PonerCrujido(go.transform, c, sfx, clipAviso, clipHojas, clipTirar, informe);

            EnTramo(soporte, pr.Tramo, go);
            informe.AppendLine($"Presencia que acecha {pr.Cap}: «{pr.Nombre}» con {pr.Crujidos.Count} crujidos.");
        }
    }

    /// <summary>Rellena un pensamiento de la presencia y apunta su texto en la tabla de diálogos (ES/EN).</summary>
    private static void Pensamiento(SerializedProperty prop, (string clave, string es, string en, string gesto) p)
    {
        if (prop == null) return;
        prop.FindPropertyRelative("key").stringValue = p.clave ?? "";
        prop.FindPropertyRelative("fallback").stringValue = p.es ?? "";
        prop.FindPropertyRelative("gesture").stringValue = p.gesto ?? "";
        if (!string.IsNullOrEmpty(p.clave))
            TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Dialogos, p.clave, p.es, p.en);
    }

    private static void PonerCrujido(Transform presencia, Crujido c, AudioMixerGroup sfx,
        AudioClip clipAviso, AudioClip clipHojas, AudioClip clipTirar, StringBuilder informe)
    {
        var punto = new GameObject("Crujido · " + c.Nombre);
        punto.transform.SetParent(presencia, false);
        punto.transform.position = AlSuelo(new Vector3(c.Pos.x, float.NaN, c.Pos.y)) + Vector3.up;

        var ruido = new GameObject("Ruido");
        ruido.transform.SetParent(punto.transform, false);
        var fuente = ruido.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = true;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 3f;
        fuente.maxDistance = 32f;
        fuente.outputAudioMixerGroup = sfx;

        Transform MarcaDelCrujido(string nombre, Vector2 p, float alto)
        {
            var t = new GameObject(nombre).transform;
            t.SetParent(punto.transform, true);
            t.position = AlSuelo(new Vector3(p.x, float.NaN, p.y)) + Vector3.up * alto;
            return t;
        }

        var caeEn = MarcaDelCrujido("Cae aquí", c.CaeEn, 0f);
        var desde = MarcaDelCrujido("Llega desde", c.Desde, 1f);
        var hacia = MarcaDelCrujido("Se va hacia", c.Hacia, 1f);

        var matas = new List<RustleShaker>();
        Vector3 centro = Vector3.zero;
        foreach (var m in c.Matas)
        {
            var mata = PonerAtrezo(punto.transform, new Atrezo
            {
                Cap = Cap1, Nombre = "Mata que cruje", Prefab = Matorral, Pos = new Vector3(m.x, float.NaN, m.y),
                Rumbo = (m.x * 37f + m.y * 11f) % 360f, Tamano = 1.9f, Colision = false,
            }, informe);
            if (mata == null) continue;
            matas.Add(mata.AddComponent<RustleShaker>());
            centro += mata.transform.position;
        }
        if (matas.Count > 0) centro /= matas.Count; else centro = punto.transform.position;
        var mirar = new GameObject("Mirar aquí").transform;
        mirar.SetParent(punto.transform, true);
        mirar.position = centro + Vector3.up * 0.3f;

        GameObject ojos = c.Ojos.HasValue ? PonerOjos(punto.transform, c.Ojos.Value, punto.transform.position) : null;

        var rp = punto.AddComponent<RustlePoint>();
        var so = new SerializedObject(rp);
        so.FindProperty("radio").floatValue = c.Radio;
        so.FindProperty("orden").intValue = c.Orden;
        so.FindProperty("repetirCada").floatValue = c.Repetir;
        so.FindProperty("segundosDeAviso").floatValue = c.Aviso;
        so.FindProperty("segundosQuieto").floatValue = c.Quieto;
        so.FindProperty("tiraSiempre").boolValue = c.TiraSiempre;
        so.FindProperty("mirarA").objectReferenceValue = mirar;
        so.FindProperty("caeEn").objectReferenceValue = caeEn;
        var lista = so.FindProperty("matas");
        lista.arraySize = matas.Count;
        for (int i = 0; i < matas.Count; i++) lista.GetArrayElementAtIndex(i).objectReferenceValue = matas[i];
        so.FindProperty("fuente").objectReferenceValue = fuente;
        so.FindProperty("llegaDesde").objectReferenceValue = desde;
        so.FindProperty("seVaHacia").objectReferenceValue = hacia;
        so.FindProperty("alAvisar").objectReferenceValue = clipAviso;
        so.FindProperty("mientrasDura").objectReferenceValue = clipHojas;
        so.FindProperty("alTirar").objectReferenceValue = clipTirar;
        so.FindProperty("vislumbre").objectReferenceValue = ojos;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Dos puntos violeta a la altura de algo agachado entre las matas, mirando al camino. Apagados.</summary>
    private static GameObject PonerOjos(Transform padre, Vector2 sitio, Vector3 caminoA)
    {
        var ojos = new GameObject("Ojos");
        ojos.transform.SetParent(padre, true);
        ojos.transform.position = AlSuelo(new Vector3(sitio.x, float.NaN, sitio.y)) + Vector3.up * 1.05f;
        Vector3 hacia = caminoA - ojos.transform.position; hacia.y = 0f;
        if (hacia.sqrMagnitude > 0.01f) ojos.transform.rotation = Quaternion.LookRotation(hacia);
        var material = MaterialDeOjos();
        for (int i = 0; i < 2; i++)
        {
            var ojo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ojo.name = i == 0 ? "Ojo izquierdo" : "Ojo derecho";
            Object.DestroyImmediate(ojo.GetComponent<Collider>());
            ojo.transform.SetParent(ojos.transform, false);
            ojo.transform.localPosition = new Vector3(i == 0 ? -0.09f : 0.09f, 0f, 0f);
            ojo.transform.localScale = new Vector3(0.075f, 0.045f, 0.03f);
            var r = ojo.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        GameObjectUtility.SetStaticEditorFlags(ojos, 0);
        ojos.SetActive(false);
        return ojos;
    }

    private static Material MaterialDeOjos()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialDeOjos);
        if (m != null) return m;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        m = new Material(shader) { name = "OjosDeLaPresencia" };
        // Por encima de 1 para que el bloom los haga brillar de día.
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(2.2f, 0.8f, 3.2f, 1f));
        AssetDatabase.CreateAsset(m, RutaMaterialDeOjos);
        return m;
    }

    private static AudioMixerGroup GrupoDelMezclador(string grupo)
    {
        var mezclador = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/AudioMixer.mixer");
        if (mezclador == null) return null;
        var grupos = mezclador.FindMatchingGroups(grupo);
        return grupos != null && grupos.Length > 0 ? grupos[0] : null;
    }
}
