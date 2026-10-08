using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Muralla del Reino: lienzos, torres y Puerta Real por la cresta de las dos terrazas de la capital.
///
/// La traza (línea media de los lienzos), las torres y la cresta de cada tramo son datos; el resto se
/// calcula contra el terreno en cada ejecución:
/// · Cada lienzo (Wall02) baja hasta 0,3 m por debajo del punto más bajo del suelo bajo su huella y sube
///   hasta la cresta de su tramo, así ninguno cuelga. La cara interior va sobre la cresta de la terraza y
///   desde dentro se ven 6,5 m de muro (más donde el suelo se hunde antes de llegar a ella).
/// · Los lienzos entran en el cuerpo de sus torres hasta que sus esquinas quedan dentro: no hay rendijas
///   entre muro y torre. Si la torre es demasiado estrecha para abrazar el lienzo, crece.
/// · Las torres intermedias del frente sur rematan a la misma cota: desde el Camino 10 el adarve se lee
///   como una línea y las torres como un ritmo. Las de esquina y las de la puerta llevan tejado y son más
///   altas; las demás crecen hasta asomar sobre los lienzos que les llegan.
/// · La Puerta Real (Wall01) se centra en el eje por el que entra el Camino 10 y se alarga hasta encajar
///   en sus dos torres: el arco deja un paso de unos 8 m.
/// · Al pie de los tramos más altos del frente sur, rocas tendidas en la ladera (espolones bajo las torres
///   que arrancan de ella y un grupo al pie de los lienzos más altos): la muralla se asienta en la roca.
/// La muralla vieja del generador, a media ladera, se retira con registro (ver VestidoDelMundo.Casas).
/// TrazaMuralla (el contorno exterior) es el recinto de la capital para la pintura y la vegetación.
public static partial class VestidoDelMundo
{
    /// Recinto de la capital: contorno exterior de la muralla (la cara de fuera de los lienzos y de la Puerta
    /// Real), cerrado y sin repetir el primer punto. Lo usan la pintura de la ciudad (no se pinta fuera) y
    /// la vegetación (dentro o fuera de la muralla).
    private static readonly Vector2[] TrazaMuralla = MurallaDelReino.ContornoExterior(MurallaDelReino.TrazaMedia());

    static partial void RetirarMurallaVieja(Scene escena, Obra o)
    {
        MurallaDelReino.RetirarLaVieja(escena, o);
    }

    static partial void PonerMuralla(Obra o)
    {
        MurallaDelReino.Levantarla(o);
    }

    /// Todo lo de la muralla, en su propia clase para que sus nombres no se crucen con los de las otras
    /// partes del vestido.
    private static class MurallaDelReino
    {
        // ── Retirar la vieja y levantar la nueva ─────────────────────────────────────────────────

        /// Retira los lienzos (Wall02) y las torres (Tower01) de la muralla vieja. Lo demás que hubiera en su
        /// grupo se queda.
        public static void RetirarLaVieja(Scene escena, Obra o)
        {
            Transform vieja = BuscarGrupoPorNombre(escena, GrupoDeLaMurallaVieja);
            if (vieja == null)
            {
                o.Informe.Add($"Muralla: no está el grupo «{GrupoDeLaMurallaVieja}» del generador; no hay muralla vieja que retirar.");
                return;
            }
            var piezas = new List<Transform>();
            int otras = 0;
            foreach (Transform t in vieja)
            {
                GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                string prefab = fuente != null ? fuente.name : "";
                if (prefab == "Wall02" || prefab == "Tower01" || t.name == "Lienzo de muralla" || t.name == "Torre de muralla") piezas.Add(t);
                else otras++;
            }
            foreach (Transform t in piezas) Retirar(escena, t, MotivoMurallaVieja, o);
            if (otras > 0) o.Informe.Add($"  · en «{GrupoDeLaMurallaVieja}» hay {otras} objetos que no son lienzos ni torres: se quedan.");
        }

        public static void Levantarla(Obra o)
        {
            PlanDeMuralla plan = PlanearMuralla(o.Suelo.Altura, (x, z) => PesoCamino(o, x, z));
            Transform muralla = Grupo(Grupo(o.Raiz, "Reino"), "Muralla");
            Transform lienzos = Grupo(muralla, "Lienzos"), torres = Grupo(muralla, "Torres");
            Transform navegacion = Grupo(muralla, "Obstáculos de navegación");
            // Los obstáculos de la clasificación general solo cubren los 2 m más bajos de cada malla, que aquí
            // quedan enterrados bajo la ladera: lienzos, torres y puerta llevan los suyos, a toda la altura.
            o.SinObstaculo.Add(lienzos);
            o.SinObstaculo.Add(torres);
            var tocados = new SortedDictionary<string, SortedSet<string>>();
            int piezas = 0, puestas = 0;
            float largo = 0f;

            foreach (TramoDeMuralla m in plan.Tramos)
            {
                largo += m.Largo;
                for (int k = 0; k < m.Piezas; k++)
                {
                    GameObject g = LevantarTramo(o, lienzos, PrefabLienzo, $"Lienzo {m.Nombre} ({k + 1}/{m.Piezas})", m, m.Desde + m.Largo * k / m.Piezas, m.Largo / m.Piezas);
                    piezas++;
                    if (g == null) continue;
                    puestas++;
                    ObstaculoDeLienzo(navegacion, g.name, m, m.Desde + m.Largo * k / m.Piezas, m.Largo / m.Piezas);
                    AnotarLoQueToca(g.name, TocaEnCaja(o, m, m.Desde + m.Largo * k / m.Piezas, m.Largo / m.Piezas), tocados);
                }
            }

            TramoDeMuralla p = plan.Puerta;
            GameObject puerta = LevantarTramo(o, muralla, PrefabPuertaReal, "Puerta Real", p, p.Desde, p.Largo);
            if (puerta != null)
            {
                // La clasificación general taparía el vano: solo las jambas llevan obstáculo.
                o.SinObstaculo.Add(puerta.transform);
                float jamba = (LargoDeLienzo - AnchoDeVano) * 0.5f * p.Largo / LargoDeLienzo;
                ObstaculoDeLienzo(navegacion, "Puerta Real, jamba oeste", p, p.Desde, jamba);
                ObstaculoDeLienzo(navegacion, "Puerta Real, jamba este", p, p.Hasta - jamba, jamba);
                AnotarLoQueToca(puerta.name, TocaEnCaja(o, p, p.Desde, p.Largo), tocados);
            }

            int conTejado = 0, almenadas = 0;
            var crecidas = new List<string>();
            for (int t = 0; t < TorresDeLaMuralla.Length; t++)
            {
                TorreDeMuralla torre = TorresDeLaMuralla[t];
                if (torre.Tipo == Torreon.DelCastillo) continue;
                float s = plan.Escala[t], sy = plan.EscalaY[t];
                var pos = new Vector3(torre.Centro.x, plan.Base[t] - BaseDeTorre * sy, torre.Centro.y);
                GameObject g = Levantar(o, torres, torre.Tipo == Torreon.ConTejado ? PrefabTorreConTejado : PrefabTorreAlmenada,
                    "Torre " + torre.Nombre, pos, Quaternion.identity, new Vector3(s, sy, s));
                if (g == null) continue;
                if (torre.Tipo == Torreon.ConTejado) conTejado++; else almenadas++;
                if (s > torre.Escala + 0.01f) crecidas.Add($"{torre.Nombre} ×{torre.Escala:0.0#}→×{s:0.0#}");
                float r = RadioDeZocalo * s;
                o.Ocupado.Add(new Huella { Centro = torre.Centro, EjeX = Vector2.right, EjeZ = Vector2.up, MedioX = r, MedioZ = r });
                Obstaculo(navegacion, g.name, new Vector3(torre.Centro.x, (plan.Base[t] + plan.Referencia(t)) * 0.5f, torre.Centro.y),
                    Quaternion.identity, new Vector3(2f * r, plan.Referencia(t) - plan.Base[t], 2f * r), cilindro: true);
                // Caja dentro del cuerpo (sus esquinas no salen del dodecágono), de un palmo sobre el suelo a lo alto.
                float abajo = Mathf.Max(o.Suelo.Altura(torre.Centro.x, torre.Centro.y), plan.Base[t]) + 0.4f;
                float arriba = plan.Referencia(t);
                var centro = new Vector3(torre.Centro.x, (abajo + arriba) * 0.5f, torre.Centro.y);
                var medio = new Vector3(0.7f * ApotemaDeTorre * s, Mathf.Max(0.2f, (arriba - abajo) * 0.5f), 0.7f * ApotemaDeTorre * s);
                AnotarLoQueToca(g.name, Toca(o, centro, medio, Quaternion.identity), tocados);
            }

            Transform escarpa = Grupo(muralla, "Escarpa");
            int rocas = 0;
            foreach (RocaDeEscarpa r in plan.Rocas)
            {
                Quaternion giro = Quaternion.LookRotation(r.EjeZ, r.EjeY);
                // Lo que asoma de la roca no debe meterse en nada de lo que sigue en la escena.
                List<string> estorba = Toca(o, r.Pivote + r.EjeY * (0.2f * r.Escala.y), new Vector3(0.6f * r.Escala.x, 0.4f * r.Escala.y, 0.6f * r.Escala.z), giro);
                if (estorba.Count > 0) { plan.Descartes.Add($"{r.Nombre}: choca con «{estorba[0]}», que sigue en la escena"); continue; }
                GameObject g = Levantar(o, escarpa, r.Prefab, r.Nombre, r.Pivote, giro, r.Escala);
                if (g == null) continue;
                rocas++;
                float h = MedioDeRoca * r.Tamano;
                o.Ocupado.Add(new Huella { Centro = r.Pos, EjeX = Vector2.right, EjeZ = Vector2.up, MedioX = h, MedioZ = h });
            }

            InformarDeLaMuralla(o.Informe, o.Suelo.Altura, plan, piezas, puestas, largo, conTejado, almenadas, crecidas, rocas, tocados);
        }

        private const string PrefabsDeMuralla = FK + "Main Structures/Wall/";
        private const string PrefabLienzo = PrefabsDeMuralla + "Wall02.prefab";
        private const string PrefabPuertaReal = PrefabsDeMuralla + "Wall01.prefab";
        private const string PrefabTorreConTejado = PrefabsDeMuralla + "Tower01.prefab";
        private const string PrefabTorreAlmenada = PrefabsDeMuralla + "Tower02.prefab";
        private static readonly string[] RocasDeEscarpa = { FK + "Rock/Rock01_a03.prefab", FK + "Rock/Rock02_a03.prefab" };

        private const string GrupoDeLaMurallaVieja = "Muralla del Reino";
        private const string MotivoMurallaVieja = "muralla vieja del Reino (a media ladera; la sustituye la nueva, sobre la cresta)";

        // ── Medidas de las mallas del pack, a escala 1 ───────────────────────────────────────────
        // Wall01 y Wall02: pivote en el extremo +X local, malla de x −16,086 a 0, y de −0,136 a 10,576 (almenas
        // incluidas), z de −2,458 a 2,458; las dos caras son iguales.
        private const float LargoDeLienzo = 16.086f;
        private const float AltoDeLienzo = 10.712f;
        private const float BaseDeLienzo = -0.136f;
        private const float MedioGruesoDeLienzo = 2.458f;
        // Vano de Wall01 (malla y colisionador): x de −10,32 a −5,77 (centrado en la pieza), jambas rectas hasta
        // 6,41 y clave del arco a 7,84.
        private const float AnchoDeVano = 4.55f;
        private const float AltoDeArranque = 6.41f;
        private const float AltoDeClave = 7.84f;
        // Tower01 y Tower02: centradas en su pivote, base a −0,14; zócalo de radio 3 y cuerpo (dodecágono) de
        // apotema 2,55 en su parte más estrecha.
        private const float BaseDeTorre = -0.14f;
        private const float RadioDeZocalo = 3f;
        private const float ApotemaDeTorre = 2.55f;
        private const float AlturaDeAlero = 13.39f;    // Tower01: alero del tejado cónico
        private const float AlturaDeAlmenas = 14.23f;  // Tower02: lo alto de sus almenas
        // Torres traseras del castillo (Castle01_a01 a ×2,036): cuerpo de apotema 4,5 sobre la terraza alta.
        private const float ApotemaDeTorreDelCastillo = 4.5f;
        // Rock01_a03 y Rock02_a03 (la misma malla, otro tono): ±0,83 en planta y de −0,85 a 0,77 en alto.
        private const float MedioDeRoca = 0.83f, BajoDeRoca = 0.85f, AltoDeRoca = 0.77f;

        // ── Reglas ───────────────────────────────────────────────────────────────────────────────
        private const float GruesoDeLienzos = 0.7f;         // escala Z: 3,44 m de muro
        private const float GruesoDeLaPuerta = 1.2f;        // escala Z de la Puerta Real: 5,9 m
        private const float HundirMuralla = 0.3f;           // bajo el punto más bajo del terreno bajo la huella
        private const float AsomaLaTorre = 1.5f;            // mínimo que el alero o las almenas asoman sobre lo que les llega
        private const float MargenDeEncaje = 0.1f;          // esquinas del lienzo, por dentro del cuerpo de la torre
        private const float ProporcionDeLadrillo = 0.9f;    // escala X ≈ 0,9 × escala Y (almenas sin deformar)
        private const float EstiramientoMaximo = 1.3f;      // alto/ancho de una torre de remate común
        private const float LibreBajoElArranque = 5f;       // paso libre de la puerta bajo el arranque del arco
        private const float AsomaLaPuerta = 4f;             // la puerta asoma sobre el lienzo sur
        private const float PasoDeMuestreo = 1f;            // rejilla de alturas bajo los lienzos (1 m × 5)

        // ── Traza ────────────────────────────────────────────────────────────────────────────────

        private enum Torreon { ConTejado, Almenada, DelCastillo }

        private readonly struct TorreDeMuralla
        {
            public readonly string Nombre;
            public readonly Torreon Tipo;
            public readonly Vector2 Centro;
            public readonly float Escala;
            /// Cota común de las almenas de las torres de un mismo frente (0: la que dé su escala). La torre se
            /// estira en vertical hasta ella.
            public readonly float Remate;

            public TorreDeMuralla(string nombre, Torreon tipo, float x, float z, float escala, float remate = 0f)
            { Nombre = nombre; Tipo = tipo; Centro = new Vector2(x, z); Escala = escala; Remate = remate; }
        }

        /// Torres, de la esquina suroeste en el sentido de la traza. Las del frente sur se meten 1 m hacia la
        /// ciudad para no bajar más por la ladera; las del quiebro J1–J2 cubren sus dos esquinas.
        private static readonly TorreDeMuralla[] TorresDeLaMuralla =
        {
            new("SO", Torreon.ConTejado, -87.79f, 247.09f, 1.4f),
            new("S1", Torreon.Almenada, -60.93f, 242.7f, 1.5f, 115.5f),
            new("S2", Torreon.Almenada, -31f, 242.7f, 1.3f, 115.5f),
            new("S3", Torreon.Almenada, -7f, 242.7f, 1.3f, 115.5f),
            new("J1", Torreon.Almenada, 30f, 240.25f, 1.3f, 115.5f),
            new("J2", Torreon.Almenada, 48.5f, 240.25f, 1.3f, 115.5f),
            new("PO", Torreon.ConTejado, 78f, 242.2f, 1.5f),
            new("PE", Torreon.ConTejado, 108.3f, 242.2f, 1.5f),
            new("E1", Torreon.Almenada, 107f, 262f, 1.2f),
            new("E2", Torreon.Almenada, 108f, 282f, 1.1f),
            new("E3", Torreon.Almenada, 108f, 317f, 1f),
            new("NE", Torreon.ConTejado, 108f, 352f, 1.3f),
            new("N1", Torreon.Almenada, 61f, 352f, 1f),
            new("CE", Torreon.DelCastillo, 13.74f, 351.74f, 1f),
            new("CO", Torreon.DelCastillo, -13.74f, 351.74f, 1f),
            new("N2", Torreon.Almenada, -61f, 352f, 1f),
            new("NO", Torreon.ConTejado, -104f, 352f, 1.3f),
            new("O1", Torreon.Almenada, -104f, 317f, 1f),
            new("O2", Torreon.Almenada, -102f, 284f, 1.1f),
            new("O3", Torreon.Almenada, -96.53f, 264.16f, 1.2f),
        };

        /// Línea media de los lienzos (x, z), cerrada y en sentido antihorario visto desde arriba: la ciudad queda
        /// a la izquierda de cada tramo. El frente sur va por la cresta de la terraza baja con un quiebro entre J1
        /// y J2 que deja libre la «Casa del mercado 17»; el norte remata en las torres traseras del castillo.
        public static Vector2[] TrazaMedia() => P(
            -89f, 245.5f, -61f, 241.7f, -31f, 241.7f, -7f, 241.7f, 30f, 241.7f, 30f, 238.8f, 48.5f, 238.8f, 48.5f, 241.7f,
            78f, 241.7f, 108.3f, 241.7f, 107.5f, 262f, 108f, 282f, 108f, 317f, 108f, 352f, 61f, 352f, 14f, 352f,
            -14f, 352f, -61f, 352f, -104f, 352f, -104f, 317f, -102f, 284f, -97f, 264f);

        /// Torre que cubre cada vértice de la traza (dos vértices seguidos con la misma torre no llevan lienzo).
        private static readonly string[] TorreDeCadaVertice =
            { "SO", "S1", "S2", "S3", "J1", "J1", "J2", "J2", "PO", "PE", "E1", "E2", "E3", "NE", "N1", "CE", "CO", "N2", "NO", "O1", "O2", "O3" };

        /// Cresta (lo alto de las almenas) de cada tramo, del vértice i al i + 1: 6,5 m sobre la terraza que
        /// cierra (104 abajo, 112 arriba) y escalonada en los lados este y oeste, que suben de una a otra. Los
        /// tramos dentro de una torre, el del castillo y el de la puerta no la usan.
        private static readonly float[] CrestaDeCadaTramo =
        {
            110.5f, 110.5f, 110.5f, 110.5f, 0f, 110.5f, 0f, 110.5f, 0f, 113f, 117f, 118.5f, 118.5f, 118.5f, 118.5f, 0f,
            118.5f, 118.5f, 118.5f, 118.5f, 117.5f, 111.5f,
        };

        private const int TramoDeLaPuerta = 8;
        /// Eje de la Puerta Real: por aquí entra el Camino 10 en la ciudad.
        private static readonly Vector2 EjeDeLaPuertaReal = new(92.85f, 241.7f);

        private static float MedioGruesoDelTramo(int i) => MedioGruesoDeLienzo * (i == TramoDeLaPuerta ? GruesoDeLaPuerta : GruesoDeLienzos);

        /// La traza desplazada hacia fuera (a la derecha del sentido de la traza) el semigrosor de cada tramo:
        /// inglete donde dos tramos seguidos se desplazan lo mismo y un punto por cada tramo donde no.
        public static Vector2[] ContornoExterior(Vector2[] traza)
        {
            var r = new List<Vector2>();
            int n = traza.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 c = traza[i];
                Vector2 d1 = (c - traza[(i + n - 1) % n]).normalized, d2 = (traza[(i + 1) % n] - c).normalized;
                Vector2 n1 = new Vector2(d1.y, -d1.x), n2 = new Vector2(d2.y, -d2.x);
                float m1 = MedioGruesoDelTramo((i + n - 1) % n), m2 = MedioGruesoDelTramo(i);
                if (Mathf.Abs(m1 - m2) > 1e-4f) { r.Add(c + n1 * m1); r.Add(c + n2 * m2); continue; }
                Vector2 bis = n1 + n2;
                if (bis.sqrMagnitude < 1e-8f) { r.Add(c + n1 * m1); continue; }
                bis.Normalize();
                r.Add(c + bis * (m1 / Mathf.Max(Vector2.Dot(bis, n1), 0.2f)));
            }
            return r.ToArray();
        }

        // ── Plan: lo que se calcula contra el terreno ────────────────────────────────────────────

        /// Un tramo de muro recto entre dos torres: «Piezas» lienzos iguales de «Desde» a «Hasta» (metros a lo
        /// largo de Dir desde Inicio).
        private sealed class TramoDeMuralla
        {
            public string Nombre;
            public int Indice;
            public Vector2 Inicio, Dir;
            public float Desde, Hasta;
            public int Piezas;
            public float Sx, Sy, Sz, Base, Cresta;
            public float Largo => Hasta - Desde;
            public float MedioGrueso => MedioGruesoDeLienzo * Sz;
            /// Hacia la ciudad.
            public Vector2 Dentro => new Vector2(-Dir.y, Dir.x);
            public Vector2 Punto(float s, float o) => Inicio + Dir * s + Dentro * o;
            public float Giro => Mathf.Atan2(Dir.y, -Dir.x) * Mathf.Rad2Deg;   // el +X local mira hacia atrás: la malla avanza por Dir
        }

        private sealed class RocaDeEscarpa
        {
            public string Nombre, Prefab;
            public Vector2 Pos;
            public float Tamano;
            public Vector3 Pivote, EjeY, EjeZ, Escala;
        }

        private sealed class PlanDeMuralla
        {
            public readonly List<TramoDeMuralla> Tramos = new();
            public TramoDeMuralla Puerta;
            public float SueloDelVanoMin, SueloDelVanoMax;
            public float[] Escala, EscalaY, Base, Asoma;
            public readonly List<RocaDeEscarpa> Rocas = new();
            public readonly List<string> Descartes = new();
            public readonly List<string> Avisos = new();

            public float Referencia(int t) =>
                Base[t] + (TorresDeLaMuralla[t].Tipo == Torreon.ConTejado ? AlturaDeAlero : AlturaDeAlmenas) * EscalaY[t];
        }

        private static int IndiceDeTorre(string nombre)
        {
            for (int i = 0; i < TorresDeLaMuralla.Length; i++)
                if (TorresDeLaMuralla[i].Nombre == nombre) return i;
            return -1;
        }

        /// Hasta dónde (a lo largo de d, desde la proyección del centro de la torre) puede llegar el extremo de
        /// un muro de semigrosor «medio» con sus esquinas dentro del cuerpo de la torre. Negativo si no cabe.
        private static float Encaje(int torre, float escala, Vector2 a, Vector2 d, float medio)
        {
            TorreDeMuralla t = TorresDeLaMuralla[torre];
            float perp = Mathf.Abs(Vector2.Dot(t.Centro - a, new Vector2(-d.y, d.x)));
            float apotema = (t.Tipo == Torreon.DelCastillo ? ApotemaDeTorreDelCastillo : ApotemaDeTorre * escala) - MargenDeEncaje;
            float q = apotema * apotema - (perp + medio) * (perp + medio);
            return q > 0f ? Mathf.Sqrt(q) : -1f;
        }

        private static float MinimoBajoTramo(System.Func<float, float, float> altura, TramoDeMuralla m, float desde, float hasta)
        {
            int nl = Mathf.Max(3, Mathf.CeilToInt((hasta - desde) / PasoDeMuestreo) + 1);
            float min = float.MaxValue;
            for (int i = 0; i < nl; i++)
            {
                float s = desde + (hasta - desde) * i / (nl - 1);
                for (int j = 0; j < 5; j++)
                {
                    Vector2 p = m.Punto(s, -m.MedioGrueso + 2f * m.MedioGrueso * j / 4f);
                    min = Mathf.Min(min, altura(p.x, p.y));
                }
            }
            return min;
        }

        /// Altura mínima y máxima del terreno en un círculo: centro y «anillos» anillos de «puntos» puntos.
        private static void ExtremosEnCirculo(System.Func<float, float, float> altura, Vector2 c, float radio, out float min, out float max,
            int anillos = 2, int puntos = 24)
        {
            min = max = altura(c.x, c.y);
            for (int anillo = 1; anillo <= anillos; anillo++)
                for (int k = 0; k < puntos; k++)
                {
                    float a = k * Mathf.PI * 2f / puntos, r = radio * anillo / anillos;
                    float y = altura(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r);
                    min = Mathf.Min(min, y);
                    max = Mathf.Max(max, y);
                }
        }

        private static float MinimoEnCirculo(System.Func<float, float, float> altura, Vector2 c, float radio)
        {
            ExtremosEnCirculo(altura, c, radio, out float min, out _);
            return min;
        }

        private static float Redondear(float v) => Mathf.Round(v * 100f) / 100f;

        /// Calcula la muralla entera contra el terreno («altura» en mundo); «pesoCamino» es el peso de camino
        /// pintado en un punto (para no echar rocas sobre el Camino 10). No toca la escena.
        private static PlanDeMuralla PlanearMuralla(System.Func<float, float, float> altura, System.Func<float, float, float> pesoCamino)
        {
            var plan = new PlanDeMuralla();
            Vector2[] traza = TrazaMedia();
            int n = traza.Length, nt = TorresDeLaMuralla.Length;
            var torreDe = new int[n];
            for (int i = 0; i < n; i++) torreDe[i] = IndiceDeTorre(TorreDeCadaVertice[i]);
            plan.Escala = new float[nt];
            plan.EscalaY = new float[nt];
            plan.Base = new float[nt];
            plan.Asoma = new float[nt];
            for (int t = 0; t < nt; t++) plan.Escala[t] = TorresDeLaMuralla[t].Escala;

            bool ConMuro(int i)
            {
                int a = torreDe[i], b = torreDe[(i + 1) % n];
                return a != b && !(TorresDeLaMuralla[a].Tipo == Torreon.DelCastillo && TorresDeLaMuralla[b].Tipo == Torreon.DelCastillo);
            }
            Vector2 DirDe(int i) => (traza[(i + 1) % n] - traza[i]).normalized;

            // 1. Las torres crecen hasta abrazar el grueso de los muros que les llegan (la puerta es más gruesa).
            for (int vuelta = 0; vuelta < 20; vuelta++)
            {
                bool cambio = false;
                for (int i = 0; i < n; i++)
                {
                    if (!ConMuro(i)) continue;
                    foreach (int t in new[] { torreDe[i], torreDe[(i + 1) % n] })
                    {
                        if (TorresDeLaMuralla[t].Tipo == Torreon.DelCastillo || Encaje(t, plan.Escala[t], traza[i], DirDe(i), MedioGruesoDelTramo(i)) >= 0f) continue;
                        plan.Escala[t] = Redondear(plan.Escala[t] + 0.1f);
                        cambio = true;
                    }
                }
                if (!cambio) break;
            }

            // 2. Lienzos: de torre a torre, desde la base del punto más bajo hasta la cresta del tramo, en piezas
            //    iguales cuyo ladrillo quede lo más cerca posible de la proporción del pack.
            for (int i = 0; i < n; i++)
            {
                if (!ConMuro(i) || i == TramoDeLaPuerta) continue;
                int ta = torreDe[i], tb = torreDe[(i + 1) % n];
                Vector2 a = traza[i], d = DirDe(i);
                float ea = Encaje(ta, plan.Escala[ta], a, d, MedioGruesoDelTramo(i)), eb = Encaje(tb, plan.Escala[tb], a, d, MedioGruesoDelTramo(i));
                if (ea < 0f || eb < 0f) plan.Avisos.Add($"el tramo {TorresDeLaMuralla[ta].Nombre}–{TorresDeLaMuralla[tb].Nombre} no cabe entero en el cuerpo de sus torres: queda una rendija");
                var m = new TramoDeMuralla
                {
                    Nombre = TorresDeLaMuralla[ta].Nombre + "–" + TorresDeLaMuralla[tb].Nombre,
                    Indice = i, Inicio = a, Dir = d, Sz = GruesoDeLienzos, Cresta = CrestaDeCadaTramo[i],
                    Desde = Vector2.Dot(TorresDeLaMuralla[ta].Centro - a, d) + Mathf.Max(ea, 0f),
                    Hasta = Vector2.Dot(TorresDeLaMuralla[tb].Centro - a, d) - Mathf.Max(eb, 0f),
                };
                m.Base = MinimoBajoTramo(altura, m, m.Desde, m.Hasta) - HundirMuralla;
                m.Sy = (m.Cresta - m.Base) / AltoDeLienzo;
                float mejor = float.MaxValue;
                for (int k = 1; k <= 12; k++)
                {
                    float error = Mathf.Abs(Mathf.Log(m.Largo / k / LargoDeLienzo / (ProporcionDeLadrillo * m.Sy)));
                    if (error < mejor) { mejor = error; m.Piezas = k; }
                }
                m.Sx = m.Largo / m.Piezas / LargoDeLienzo;
                plan.Tramos.Add(m);
            }

            // 3. Puerta Real: centrada en el eje del Camino 10 y tan larga como haga falta para encajar en sus
            //    dos torres; tan alta como pidan el paso libre bajo el arco y lo que asoma sobre el lienzo sur.
            {
                int ta = torreDe[TramoDeLaPuerta], tb = torreDe[TramoDeLaPuerta + 1];
                Vector2 a = traza[TramoDeLaPuerta], d = DirDe(TramoDeLaPuerta);
                float medio = MedioGruesoDelTramo(TramoDeLaPuerta);
                float centro = Vector2.Dot(EjeDeLaPuertaReal - a, d);
                float desde = Vector2.Dot(TorresDeLaMuralla[ta].Centro - a, d) + Mathf.Max(Encaje(ta, plan.Escala[ta], a, d, medio), 0f);
                float hasta = Vector2.Dot(TorresDeLaMuralla[tb].Centro - a, d) - Mathf.Max(Encaje(tb, plan.Escala[tb], a, d, medio), 0f);
                float mitad = Mathf.Max(centro - desde, hasta - centro);
                var p = new TramoDeMuralla
                {
                    Nombre = "Puerta Real", Indice = TramoDeLaPuerta, Inicio = a, Dir = d, Sz = GruesoDeLaPuerta,
                    Desde = centro - mitad, Hasta = centro + mitad, Piezas = 1,
                };
                p.Sx = p.Largo / LargoDeLienzo;
                p.Base = MinimoBajoTramo(altura, p, p.Desde, p.Hasta) - HundirMuralla;
                float mitadDelVano = AnchoDeVano * p.Sx * 0.5f;
                plan.SueloDelVanoMin = float.MaxValue;
                plan.SueloDelVanoMax = float.MinValue;
                for (float s = centro - mitadDelVano; s <= centro + mitadDelVano + 1e-3f; s += 0.5f)
                    for (int j = 0; j < 5; j++)
                    {
                        Vector2 q = p.Punto(s, -medio + 2f * medio * j / 4f);
                        float y = altura(q.x, q.y);
                        plan.SueloDelVanoMin = Mathf.Min(plan.SueloDelVanoMin, y);
                        plan.SueloDelVanoMax = Mathf.Max(plan.SueloDelVanoMax, y);
                    }
                float crestaSur = CrestaDeCadaTramo[TramoDeLaPuerta - 1];
                p.Sy = Mathf.Max((plan.SueloDelVanoMax + LibreBajoElArranque - p.Base) / AltoDeArranque, (crestaSur + AsomaLaPuerta - p.Base) / AltoDeLienzo);
                p.Cresta = p.Base + AltoDeLienzo * p.Sy;
                plan.Puerta = p;
            }

            // 4. Torres: base bajo su zócalo; las de remate común se estiran hasta él (si se estiran de más, se
            //    ensanchan) y las demás crecen hasta asomar sobre lo que les llega.
            for (int t = 0; t < nt; t++)
            {
                TorreDeMuralla torre = TorresDeLaMuralla[t];
                if (torre.Tipo == Torreon.DelCastillo) continue;
                float cresta = float.MinValue;
                foreach (TramoDeMuralla m in plan.Tramos)
                    if (torreDe[m.Indice] == t || torreDe[(m.Indice + 1) % n] == t) cresta = Mathf.Max(cresta, m.Cresta);
                if (torreDe[TramoDeLaPuerta] == t || torreDe[TramoDeLaPuerta + 1] == t) cresta = Mathf.Max(cresta, plan.Puerta.Cresta);
                float alto = torre.Tipo == Torreon.ConTejado ? AlturaDeAlero : AlturaDeAlmenas;
                bool comun = torre.Remate > 0f && torre.Tipo == Torreon.Almenada && torre.Remate >= cresta + AsomaLaTorre;
                if (torre.Remate > 0f && !comun) plan.Avisos.Add($"la torre {torre.Nombre} no puede rematar a {torre.Remate:0.0}: no asomaría sobre sus lienzos; toma la altura de su escala");
                for (int vuelta = 0; vuelta < 30; vuelta++)
                {
                    float s = plan.Escala[t];
                    plan.Base[t] = MinimoEnCirculo(altura, torre.Centro, RadioDeZocalo * s) - HundirMuralla;
                    if (comun)
                    {
                        plan.EscalaY[t] = (torre.Remate - plan.Base[t]) / alto;
                        if (plan.EscalaY[t] > EstiramientoMaximo * s) { plan.Escala[t] = Redondear(s + 0.1f); continue; }
                    }
                    else
                    {
                        plan.EscalaY[t] = s;
                        if (plan.Base[t] + alto * s < cresta + AsomaLaTorre) { plan.Escala[t] = Redondear(s + 0.1f); continue; }
                    }
                    break;
                }
                plan.Asoma[t] = plan.Referencia(t) - cresta;
            }

            PlanearEscarpa(plan, altura, pesoCamino, torreDe);
            return plan;
        }

        // ── Escarpa ──────────────────────────────────────────────────────────────────────────────

        private const int SemillaDeEscarpa = 7301;
        private const int UltimoTramoDelFrenteSur = 7;         // del vértice SO al PO
        private const float DesnivelDeTorreEnLadera = 5f;      // bajo su zócalo: la torre arranca de la ladera
        private const float DesnivelDeEspolonLargo = 8f;       // con más, el espolón lleva una tercera roca
        private const float LienzoAltoDesdeFuera = 12.5f;      // lienzos que llevan rocas al pie
        private const float HolguraAlCamino = 3f;
        private const float InclinacionDeRoca = 0.8f;          // fracción de la pendiente que sigue la roca
        private const float HundimientoDeRoca = 0.45f;         // fracción de su alto que entra en la ladera
        private const float AplastadoDeRoca = 0.8f;

        /// Rocas tendidas en la ladera del frente sur: un espolón al pie de cada torre que arranca de ella (una
        /// roca grande y una o dos menores, del tamaño de la torre) y un grupo al pie de los lienzos que se ven
        /// más altos desde fuera. Si la roca principal de un grupo no cabe, el grupo entero se omite.
        private static void PlanearEscarpa(PlanDeMuralla plan, System.Func<float, float, float> altura, System.Func<float, float, float> pesoCamino, int[] torreDe)
        {
            var dado = new Ruido.Dado(SemillaDeEscarpa);
            var torresDelSur = new HashSet<int>();
            for (int i = 0; i <= UltimoTramoDelFrenteSur + 1; i++) torresDelSur.Add(torreDe[i]);
            torresDelSur.Add(torreDe[TramoDeLaPuerta + 1]);

            for (int t = 0; t < TorresDeLaMuralla.Length; t++)
            {
                if (!torresDelSur.Contains(t)) continue;
                TorreDeMuralla torre = TorresDeLaMuralla[t];
                float rz = RadioDeZocalo * plan.Escala[t];
                ExtremosEnCirculo(altura, torre.Centro, rz, out float bajo, out float alto);
                float desnivel = alto - bajo;
                if (desnivel < DesnivelDeTorreEnLadera) continue;
                Vector2 c = torre.Centro, fuera = CuestaAbajo(altura, c), lado = new Vector2(fuera.y, -fuera.x);
                string nombre = "Espolón de la torre " + torre.Nombre;
                float k = 0.6f * rz;
                float desvio = dado.Entre(-0.6f, 0.6f) * k;
                string motivo = PonerRoca(plan, altura, pesoCamino, nombre, c + fuera * (rz + 0.3f * k) + lado * desvio, k, dado);
                if (motivo != null) { plan.Descartes.Add($"{nombre}: {motivo}"); continue; }
                float signo = desvio > 0f ? -1f : 1f;
                float k2 = k * dado.Entre(0.5f, 0.65f);
                motivo = PonerRoca(plan, altura, pesoCamino, nombre, c + fuera * (rz + 0.1f * k) + lado * signo * (0.95f * k + 0.6f), k2, dado);
                if (motivo != null) plan.Descartes.Add($"{nombre} (segunda roca): {motivo}");
                if (desnivel <= DesnivelDeEspolonLargo) continue;
                float k3 = k * dado.Entre(0.4f, 0.5f);
                motivo = PonerRoca(plan, altura, pesoCamino, nombre, c + fuera * (rz + 1.05f * k) - lado * desvio * 0.5f, k3, dado);
                if (motivo != null) plan.Descartes.Add($"{nombre} (tercera roca): {motivo}");
            }

            foreach (TramoDeMuralla m in plan.Tramos)
            {
                if (m.Indice > UltimoTramoDelFrenteSur) continue;
                float fueraMin = float.MaxValue;
                for (float s = m.Desde; s < m.Hasta; s += 0.5f)
                {
                    Vector2 q = m.Punto(s, -m.MedioGrueso);
                    fueraMin = Mathf.Min(fueraMin, altura(q.x, q.y));
                }
                if (m.Cresta - fueraMin < LienzoAltoDesdeFuera || m.Largo < 14f) continue;
                string nombre = "Roca al pie del lienzo " + m.Nombre;
                float sm = (m.Desde + m.Hasta) * 0.5f + dado.Entre(-0.15f, 0.15f) * m.Largo;
                float k = dado.Entre(2.4f, 3f);
                string motivo = PonerRoca(plan, altura, pesoCamino, nombre, m.Punto(sm, -(m.MedioGrueso + 0.45f * k)), k, dado);
                if (motivo != null) { plan.Descartes.Add($"{nombre}: {motivo}"); continue; }
                float k2 = k * dado.Entre(0.5f, 0.65f);
                float signo = dado.Siguiente() < 0.5f ? 1f : -1f;
                motivo = PonerRoca(plan, altura, pesoCamino, nombre, m.Punto(sm + signo * (0.9f * (k + k2) + 0.3f), -(m.MedioGrueso + 0.3f * k2)), k2, dado);
                if (motivo != null) plan.Descartes.Add($"{nombre} (segunda roca): {motivo}");
            }
        }

        /// Dirección en planta en la que baja el terreno.
        private static Vector2 CuestaAbajo(System.Func<float, float, float> altura, Vector2 c)
        {
            const float e = 1.5f;
            var g = new Vector2(altura(c.x + e, c.y) - altura(c.x - e, c.y), altura(c.x, c.y + e) - altura(c.x, c.y - e));
            return g.sqrMagnitude > 1e-8f ? -g.normalized : new Vector2(0f, -1f);
        }

        private static Vector3 NormalDelSuelo(System.Func<float, float, float> altura, Vector2 p)
        {
            const float e = 1.2f;
            float gx = (altura(p.x + e, p.y) - altura(p.x - e, p.y)) / (2f * e), gz = (altura(p.x, p.y + e) - altura(p.x, p.y - e)) / (2f * e);
            return new Vector3(-gx, 1f, -gz).normalized;
        }

        /// Gira «v» con la rotación más corta que lleva el eje Y a «arriba» (Rodrigues; arriba unitario).
        private static Vector3 GirarDesdeVertical(Vector3 v, Vector3 arriba)
        {
            Vector3 eje = Vector3.Cross(Vector3.up, arriba);
            float c = arriba.y;
            if (eje.sqrMagnitude < 1e-10f) return c >= 0f ? v : -v;
            return v * c + Vector3.Cross(eje, v) + eje * (Vector3.Dot(eje, v) / (1f + c));
        }

        /// Una roca de tamaño «k» tendida en la ladera en «pos»: sigue parte de la pendiente y se mete en ella hasta
        /// que toda su cara de abajo queda bajo el terreno. Devuelve el motivo si no se pone.
        private static string PonerRoca(PlanDeMuralla plan, System.Func<float, float, float> altura, System.Func<float, float, float> pesoCamino,
            string nombre, Vector2 pos, float k, Ruido.Dado dado)
        {
            float alcance = k + HolguraAlCamino;
            if (pesoCamino(pos.x, pos.y) > 0.3f) return "junto al Camino 10";
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Vector2 q = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * alcance;
                // Dentro del recinto están las calles de la ciudad: no cuentan para la ladera.
                if (!DentroDePoligono(q, TrazaMuralla) && pesoCamino(q.x, q.y) > 0.3f) return "junto al Camino 10";
            }
            foreach (RocaDeEscarpa r in plan.Rocas)
                if (Vector2.Distance(pos, r.Pos) < 0.75f * (k + r.Tamano)) return "se pisa con otra roca";

            float giro = dado.Entre(0f, 360f) * Mathf.Deg2Rad;
            Vector3 arriba = Vector3.Lerp(Vector3.up, NormalDelSuelo(altura, pos), InclinacionDeRoca).normalized;
            Vector3 ex = GirarDesdeVertical(new Vector3(Mathf.Cos(giro), 0f, -Mathf.Sin(giro)), arriba);
            Vector3 ez = GirarDesdeVertical(new Vector3(Mathf.Sin(giro), 0f, Mathf.Cos(giro)), arriba);
            var escala = new Vector3(k, k * AplastadoDeRoca, k);
            float ky = escala.y;
            var suelo = new Vector3(pos.x, altura(pos.x, pos.y), pos.y);
            float hundido = HundimientoDeRoca * (BajoDeRoca + AltoDeRoca) * ky;
            Vector3 pivote = suelo;
            for (int vuelta = 0; vuelta < 12; vuelta++)
            {
                pivote = suelo + arriba * (BajoDeRoca * ky - hundido);
                float peor = float.MinValue;
                for (int ix = -1; ix <= 1; ix++)
                    for (int iz = -1; iz <= 1; iz++)
                    {
                        Vector3 q = pivote + ex * (0.75f * ix * escala.x) - arriba * (BajoDeRoca * ky) + ez * (0.75f * iz * escala.z);
                        peor = Mathf.Max(peor, q.y - altura(q.x, q.z));
                    }
                if (peor <= -0.1f) break;
                hundido += peor + 0.1f;
            }
            if (hundido > 0.85f * (BajoDeRoca + AltoDeRoca) * ky) return "la ladera no la asienta";

            plan.Rocas.Add(new RocaDeEscarpa
            {
                Nombre = nombre, Prefab = RocasDeEscarpa[plan.Rocas.Count % RocasDeEscarpa.Length], Pos = pos, Tamano = k,
                Pivote = pivote, EjeY = arriba, EjeZ = ez, Escala = escala,
            });
            return null;
        }

        // ── Colocación ───────────────────────────────────────────────────────────────────────────

        /// Instancia un prefab con pose y escala exactas (sin las comprobaciones de Poner: la muralla se calcula
        /// entera contra el terreno y no se recorta).
        private static GameObject Levantar(Obra o, Transform grupo, string ruta, string nombre, Vector3 pos, Quaternion giro, Vector3 escala)
        {
            GameObject prefab = CargarPrefab(o, ruta);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
            go.name = nombre;
            go.transform.SetPositionAndRotation(pos, giro * prefab.transform.localRotation);
            go.transform.localScale = Vector3.Scale(prefab.transform.localScale, escala);
            o.Puestas++;
            return go;
        }

        /// Obstáculo de navegación (Carve) de la base a la cresta de un trozo de tramo, de «largo» metros desde
        /// «desde»: corta el NavMesh del terreno a cualquier altura de la ladera por la que pase el muro.
        private static void ObstaculoDeLienzo(Transform grupo, string nombre, TramoDeMuralla m, float desde, float largo)
        {
            Vector2 c = m.Punto(desde + largo * 0.5f, 0f);
            Obstaculo(grupo, nombre, new Vector3(c.x, (m.Base + m.Cresta) * 0.5f, c.y),
                Quaternion.LookRotation(new Vector3(m.Dentro.x, 0f, m.Dentro.y)), new Vector3(largo, m.Cresta - m.Base, 2f * m.MedioGrueso));
        }

        /// Un objeto sin collider con un NavMeshObstacle de caja (o de cilindro, de diámetro tamano.x) centrado en
        /// él. Sin collider, los menús de navegación que reajustan obstáculos no lo tocan.
        private static void Obstaculo(Transform grupo, string nombre, Vector3 centro, Quaternion giro, Vector3 tamano, bool cilindro = false)
        {
            var go = new GameObject("Obstáculo: " + nombre);
            go.transform.SetParent(grupo, false);
            go.transform.SetPositionAndRotation(centro, giro);
            var obs = go.AddComponent<NavMeshObstacle>();
            obs.carving = true;
            obs.center = Vector3.zero;
            if (cilindro)
            {
                obs.shape = NavMeshObstacleShape.Capsule;
                obs.radius = tamano.x * 0.5f;
                obs.height = tamano.y;
            }
            else
            {
                obs.shape = NavMeshObstacleShape.Box;
                obs.size = tamano;
            }
        }

        /// Un lienzo (o la puerta) de «largo» metros que empieza en «desde» a lo largo del tramo.
        private static GameObject LevantarTramo(Obra o, Transform grupo, string ruta, string nombre, TramoDeMuralla m, float desde, float largo)
        {
            Vector2 a = m.Punto(desde, 0f);
            var pos = new Vector3(a.x, m.Base - BaseDeLienzo * m.Sy, a.y);
            GameObject g = Levantar(o, grupo, ruta, nombre, pos, Quaternion.Euler(0f, m.Giro, 0f), new Vector3(largo / LargoDeLienzo, m.Sy, m.Sz));
            if (g != null)
                o.Ocupado.Add(new Huella { Centro = m.Punto(desde + largo * 0.5f, 0f), EjeX = m.Dir, EjeZ = m.Dentro, MedioX = largo * 0.5f, MedioZ = m.MedioGrueso });
            return g;
        }

        /// Lo que ya estaba en la escena (salvo el castillo, en el que rematan los lienzos del norte) dentro de la
        /// parte de un lienzo que queda sobre el terreno.
        private static List<string> TocaEnCaja(Obra o, TramoDeMuralla m, float desde, float largo)
        {
            Vector2 c = m.Punto(desde + largo * 0.5f, 0f);
            float suelo = Mathf.Max(m.Base, o.Suelo.Altura(c.x, c.y));
            var centro = new Vector3(c.x, (suelo + m.Cresta) * 0.5f + 0.2f, c.y);
            var medio = new Vector3(Mathf.Max(0.1f, largo * 0.5f - 0.3f), Mathf.Max(0.2f, (m.Cresta - suelo) * 0.5f - 0.4f), Mathf.Max(0.1f, m.MedioGrueso - 0.2f));
            return Toca(o, centro, medio, Quaternion.Euler(0f, m.Giro, 0f));
        }

        private static List<string> Toca(Obra o, Vector3 centro, Vector3 medio, Quaternion giro)
        {
            var r = new List<string>();
            Physics.SyncTransforms();
            int n = Physics.OverlapBoxNonAlloc(centro, medio, o.Buffer, giro, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = o.Buffer[i];
                if (Ignorable(c) || c.transform.IsChildOf(o.Raiz) || c.gameObject.scene != o.Raiz.gameObject.scene || EsDelCastillo(c)) continue;
                GameObject raiz = PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject);
                r.Add((raiz != null ? raiz : c.gameObject).name);
            }
            return r;
        }

        private static bool EsDelCastillo(Collider c)
        {
            GameObject raiz = PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject);
            GameObject fuente = raiz != null ? PrefabUtility.GetCorrespondingObjectFromSource(raiz) : null;
            return fuente != null && fuente.name.StartsWith("Castle01");
        }

        private static void AnotarLoQueToca(string pieza, List<string> tocados, SortedDictionary<string, SortedSet<string>> anotados)
        {
            foreach (string t in tocados)
            {
                if (!anotados.TryGetValue(t, out SortedSet<string> piezas)) anotados[t] = piezas = new SortedSet<string>();
                piezas.Add(pieza);
            }
        }

        // ── Informe ──────────────────────────────────────────────────────────────────────────────

        private static void InformarDeLaMuralla(List<string> informe, System.Func<float, float, float> altura, PlanDeMuralla plan, int piezas, int puestas,
            float largo, int conTejado, int almenadas, List<string> crecidas, int rocas, SortedDictionary<string, SortedSet<string>> tocados)
        {
            TramoDeMuralla p = plan.Puerta;
            informe.Add($"Muralla del Reino: {puestas} de {piezas} lienzos (Wall02, {largo:0} m en {plan.Tramos.Count} tramos), {conTejado + almenadas} torres " +
                          $"({conTejado} con tejado, {almenadas} almenadas) y la Puerta Real (Wall01), por la cresta de las dos terrazas.");
            informe.Add($"  · Puerta Real: {p.Largo:0.0} × {p.Cresta - p.Base:0.0} m, centrada en x = {EjeDeLaPuertaReal.x:0.00}; vano de {AnchoDeVano * p.Sx:0.0} m " +
                          $"con {p.Base + AltoDeArranque * p.Sy - plan.SueloDelVanoMax:0.0} m libres bajo el arranque del arco y " +
                          $"{p.Base + AltoDeClave * p.Sy - plan.SueloDelVanoMax:0.0} m bajo la clave (suelo del vano {plan.SueloDelVanoMin:0.0}–{plan.SueloDelVanoMax:0.0}). Sin obstáculo de navegación.");

            // Comprobación con una rejilla más fina que la del cálculo: ninguna pieza debe quedar sobre el vacío.
            float peorHueco = float.MinValue, vistoFuera = 0f, vistoDentroMin = float.MaxValue;
            string peor = "";
            void Comprobar(TramoDeMuralla m, float s)
            {
                for (int j = 0; j <= 8; j++)
                {
                    Vector2 q = m.Punto(s, -m.MedioGrueso + m.MedioGrueso * j / 4f);
                    float hueco = m.Base - altura(q.x, q.y);
                    if (hueco > peorHueco) { peorHueco = hueco; peor = m == plan.Puerta ? m.Nombre : "lienzo " + m.Nombre; }
                }
            }
            for (float s = p.Desde; s <= p.Hasta + 1e-3f; s += 0.5f) Comprobar(p, s);
            var hondonadas = new List<string>();
            foreach (TramoDeMuralla m in plan.Tramos)
            {
                float hondo = 0f, tramosHondos = 0f, muestras = 0f, vistoDentroMax = 0f;
                for (float s = m.Desde; s <= m.Hasta + 1e-3f; s += 0.5f)
                {
                    Comprobar(m, s);
                    Vector2 qi = m.Punto(s, m.MedioGrueso), qf = m.Punto(s, -m.MedioGrueso);
                    float dentro = altura(qi.x, qi.y);
                    vistoFuera = Mathf.Max(vistoFuera, m.Cresta - altura(qf.x, qf.y));
                    vistoDentroMin = Mathf.Min(vistoDentroMin, m.Cresta - dentro);
                    vistoDentroMax = Mathf.Max(vistoDentroMax, m.Cresta - dentro);
                    float terraza = float.MinValue;
                    for (int k = 1; k <= 3; k++)
                    {
                        Vector2 qt = m.Punto(s, m.MedioGrueso + 2f * k);
                        terraza = Mathf.Max(terraza, altura(qt.x, qt.y));
                    }
                    hondo = Mathf.Max(hondo, terraza - dentro);
                    if (terraza - dentro > 1f) tramosHondos++;
                    muestras++;
                }
                if (tramosHondos / muestras > 0.15f)
                    hondonadas.Add($"{m.Nombre}, hasta {hondo:0.0} m en el {100f * tramosHondos / muestras:0} % del tramo (desde el fondo se ven {vistoDentroMax:0.0} m de muro)");
            }
            for (int t = 0; t < TorresDeLaMuralla.Length; t++)
            {
                if (TorresDeLaMuralla[t].Tipo == Torreon.DelCastillo) continue;
                ExtremosEnCirculo(altura, TorresDeLaMuralla[t].Centro, RadioDeZocalo * plan.Escala[t], out float suelo, out _, 6, 72);
                if (plan.Base[t] - suelo > peorHueco) { peorHueco = plan.Base[t] - suelo; peor = "torre " + TorresDeLaMuralla[t].Nombre; }
            }
            informe.Add(peorHueco <= 0f
                ? $"  · Ninguna pieza cuelga: la base más alta respecto a su terreno queda {-peorHueco:0.00} m enterrada ({peor})."
                : $"  ! La pieza «{peor}» queda {peorHueco:0.00} m sobre el terreno en algún punto: revisar.");
            float crestaMin = float.MaxValue, crestaMax = float.MinValue;
            var tramos = new List<string>();
            foreach (TramoDeMuralla m in plan.Tramos)
            {
                crestaMin = Mathf.Min(crestaMin, m.Cresta);
                crestaMax = Mathf.Max(crestaMax, m.Cresta);
                tramos.Add($"{m.Nombre} {m.Piezas}×{m.Largo / m.Piezas:0.0} m (base {m.Base:0.0}, ×{m.Sx:0.00}/{m.Sy:0.00})");
            }
            informe.Add($"  · Cresta del adarve de {crestaMin:0.0} (terraza baja) a {crestaMax:0.0} (terraza alta): desde dentro se ven al menos {vistoDentroMin:0.0} m de muro; desde fuera, hasta {vistoFuera:0.0} m.");
            informe.Add("  · Tramos: " + string.Join("; ", tramos) + ".");
            if (hondonadas.Count > 0)
                informe.Add("  · Hondonadas del terreno al pie interior (la terraza no llega a la muralla; no se toca el terreno): " + string.Join("; ", hondonadas) + ".");

            var remates = new List<string>();
            float remate = 0f;
            foreach (TorreDeMuralla t in TorresDeLaMuralla)
                if (t.Remate > 0f) { remates.Add(t.Nombre); remate = t.Remate; }
            informe.Add((remates.Count > 0 ? $"  · Torres: las del frente sur ({string.Join(", ", remates)}) rematan sus almenas a {remate:0.0}; las demás" : "  · Torres: todas") +
                          $" asoman entre {MinimoAsoma(plan):0.0} y {MaximoAsoma(plan):0.0} m sobre lo que les llega." +
                          (crecidas.Count > 0 ? " Crecen para abrazar sus muros o asomar: " + string.Join(", ", crecidas) + "." : ""));
            informe.Add($"  · Escarpa: {rocas} rocas tendidas en la ladera del frente sur." + (plan.Descartes.Count > 0 ? " Omitidas: " + string.Join("; ", plan.Descartes) + "." : ""));
            foreach (string aviso in plan.Avisos) informe.Add("  ! " + aviso + ".");
            foreach (KeyValuePair<string, SortedSet<string>> t in tocados)
                informe.Add($"  ! «{t.Key}», que sigue en la escena, se mete en {string.Join(", ", t.Value)}.");
        }

        private static float MinimoAsoma(PlanDeMuralla plan)
        {
            float r = float.MaxValue;
            for (int t = 0; t < TorresDeLaMuralla.Length; t++)
                if (TorresDeLaMuralla[t].Tipo != Torreon.DelCastillo && TorresDeLaMuralla[t].Remate <= 0f) r = Mathf.Min(r, plan.Asoma[t]);
            return r;
        }

        private static float MaximoAsoma(PlanDeMuralla plan)
        {
            float r = float.MinValue;
            for (int t = 0; t < TorresDeLaMuralla.Length; t++)
                if (TorresDeLaMuralla[t].Tipo != Torreon.DelCastillo && TorresDeLaMuralla[t].Remate <= 0f) r = Mathf.Max(r, plan.Asoma[t]);
            return r;
        }
    }
}
