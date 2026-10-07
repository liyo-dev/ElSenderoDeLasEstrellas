using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Vegetación de MainWorld: arbolado por zonas con un color dominante en cada una, compuesto por reglas (nunca
/// coordenadas sueltas), el arbolado y los jardines del Reino, matas en los taludes y flores pintadas como hierba
/// de detalle del terreno.
///
/// · Campo: ladera de otoño de la subida al Reino (verde con naranja y oro), arboleda dorada de las granjas,
///   sotos del valle, del sur, del sureste y de las lomas de la costa, prados del oeste de Will y del redil;
///   orlas de los caminos, de los pueblos (lima en el vecino, palmeras y turquesa en el puerto, oro en las
///   granjas) y ribera turquesa del arroyo de la ladera este.
/// · Reino, dentro: hileras de cerezos en los jardines de palacio, jardín hundido de la taberna, paseo de ronda
///   (setos al pie de la muralla y, delante, cerezos y turquesas alternos), matas en el talud central y un
///   cerezo en la plazoleta del pozo. Fuera: pinar tras el castillo, hombros de coníferas con acentos turquesa,
///   alameda de cipreses en el Camino 10 (con dos ventanas de vista), matas al pie del talud de la muralla,
///   setos al pie de los frentes oeste, norte y este, y racimos de hiedra en la cara de los lienzos.
///
/// Reglas de composición (guía de arte de la naturaleza):
/// · Bosquetes de 3 a 7 árboles, alargados 2:1 siguiendo la curva de nivel, con una especie que pone al menos el
///   60 %, un árbol dominante 1,3 veces más alto, troncos a 0,72–1,0 diámetros de copa y como mucho un acento
///   de color; si no caben tres, no se pone. Entre bosquetes, 17–34 m de prado. Dos colores de acento
///   distintos no quedan a menos de 30 m.
/// · Caminos: tramos con vistas (30–50 m sin árboles a menos de 15 m del borde) que se alternan con tramos de
///   arboleda (un bosquete a un lado cada 20–28 m y matas enfrente, a 1,5–3 m del borde, cambiando de lado).
///   Los troncos quedan a 3 m o más del borde pintado.
/// · Escalones de altura: matas de 1–2,5 m, jardín de 5–6,5 m, campo de 9–14 m (el dominante, hasta 18 m).
/// · Árboles verdes con el follaje matizado del mapa (Follaje_0; Follaje_1 solo al borde del bosque); los de
///   color, siempre con el Tree.mat de serie (el follaje los apagaría a marrón). Cerezos: Tree_Cerezo.mat, una
///   copia de Tree.mat con la paleta recoloreada a rosa pálido que esta herramienta crea la primera vez.
///
/// El plan (dónde va cada pieza) solo depende del terreno, de lo que ya había en la escena y de los datos: es el
/// mismo al pintar las flores (antes de colocar nada) y al plantar. Al plantar, Poner hace el resto de
/// comprobaciones (apoyo en el tronco, choques, solapes) y lo que no cabe se anota en el informe.
///
/// Canon: nada en el Bosque Prohibido, en la ladera de la montaña, en el marjal del sureste (reservado) ni en la
/// ribera de la laguna (la viste VestidoDelMundo.Agua); sin tocones, troncos apilados ni luces entre árboles; el
/// carmesí (Tree04_b01, Tree05_b01) se reserva para la isla de las Ruinas. Sin Tree04_c02/c03 (su caja cubre la
/// copa), Tree02_d01 (lleva obstáculo de navegación y refugio de lluvia) ni Tree07 a más de 2,5 de escala.
public static partial class VestidoDelMundo
{
    static partial void PintarFloresDeVegetacion(Lienzo l, List<string> informe)
    {
        Vegetacion.PintarFlores(l, informe);
    }

    static partial void PonerVegetacion(Obra o)
    {
        Vegetacion.Plantar(o);
    }

    /// Todo lo de la vegetación, en su propia clase para que sus nombres no se crucen con los de las otras partes
    /// del vestido.
    private static class Vegetacion
    {
        // ── Rutas ────────────────────────────────────────────────────────────────────────────

        private const string Veg = FK + "Vegetation/";
        private const string RutaFollajeDeCampo = "Assets/Scenes/Worlds/MainWorld_data/Recursos/Follaje_0.mat";
        private const string RutaFollajeDeBosque = "Assets/Scenes/Worlds/MainWorld_data/Recursos/Follaje_1.mat";
        private const string RutaMaterialDeArbol = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials/Tree.mat";
        private const string RutaPaletaDeArbol = "Assets/Art/World/Fantasy_Kingdom_Pack/Textures/Tree_D.tga";
        private const string ArchivoPaletaDeCerezo = "Tree_Cerezo_D.png";
        private const string ArchivoMaterialDeCerezo = "Tree_Cerezo.mat";
        private const string ArchivoPaletaTemporal = "Tree_Cerezo_D (fuente temporal).tga";
        private const string PrefijoGrupo = "Vegetación — ";

        // ── Catálogo de especies ─────────────────────────────────────────────────────────────

        private enum Tono { Verde, Lima, Oro, Naranja, Turquesa, Azul, Rosa, Cerezo }

        private enum Clase { Arbol, Mata, Seto, Flor }

        private sealed class Especie
        {
            public string Nombre, Prefab;
            public Tono Tono;
            public Clase Clase;
            /// Ancho de copa entre alto, medido en el pack (sirve para separar las piezas al planear; Poner mide
            /// la pieza de verdad al colocarla).
            public float Proporcion;
            public float AltoMaximo;
            public bool Palmera;
            public float Ancho(float alto) => Proporcion * alto;
            public bool EsDeColor => Tono != Tono.Verde;
        }

        private static readonly Dictionary<string, Especie> Especies = CrearCatalogo();

        private static Dictionary<string, Especie> CrearCatalogo()
        {
            var d = new Dictionary<string, Especie>();
            void E(string nombre, Tono tono, float proporcion, float altoMaximo = 22f, Clase clase = Clase.Arbol, string prefab = null, bool palmera = false) =>
                d[nombre] = new Especie
                {
                    Nombre = nombre, Prefab = Veg + (prefab ?? nombre) + ".prefab", Tono = tono, Clase = clase,
                    Proporcion = proporcion, AltoMaximo = altoMaximo, Palmera = palmera,
                };

            // Pinos de pisos y conos (verde, lima, oro).
            E("Tree01_a01", Tono.Verde, 0.63f); E("Tree01_a02", Tono.Verde, 0.57f);
            E("Tree01_b01", Tono.Lima, 0.63f); E("Tree01_b02", Tono.Lima, 0.57f);
            E("Tree02_a02", Tono.Verde, 0.41f); E("Tree02_b01", Tono.Verde, 0.78f);
            E("Tree02_c01", Tono.Verde, 0.54f); E("Tree02_c02", Tono.Verde, 0.41f);
            E("Tree02_e01", Tono.Oro, 0.54f); E("Tree02_e02", Tono.Oro, 0.41f); E("Tree02_f01", Tono.Oro, 0.78f);
            // Frondosos (verde, naranja y amarillo de otoño).
            E("Tree03_a01", Tono.Verde, 0.70f); E("Tree03_a02", Tono.Verde, 0.68f); E("Tree03_b01", Tono.Verde, 0.99f);
            E("Tree03_c01", Tono.Verde, 0.70f); E("Tree03_c02", Tono.Verde, 0.68f); E("Tree03_d01", Tono.Verde, 0.99f);
            E("Tree03_e01", Tono.Naranja, 0.70f); E("Tree03_f01", Tono.Naranja, 0.99f); E("Tree03_e02", Tono.Oro, 0.68f);
            // Copa de seta (turquesa, lima oliva y un azul de hito); a02/a03 y d02/d03 con el tronco curvo.
            E("Tree04_a01", Tono.Turquesa, 0.68f, 12f); E("Tree04_a02", Tono.Turquesa, 0.60f, 12f); E("Tree04_a03", Tono.Turquesa, 0.68f, 12f);
            E("Tree04_d01", Tono.Lima, 0.68f, 12f); E("Tree04_d02", Tono.Lima, 0.60f, 12f); E("Tree04_d03", Tono.Lima, 0.68f, 12f);
            E("Tree04_e01", Tono.Azul, 0.62f, 12f);
            // Gran cúpula y palmeras.
            E("Tree05_a01", Tono.Verde, 0.83f);
            E("Tree06_a02", Tono.Verde, 0.99f, 14f, palmera: true); E("Tree06_a03", Tono.Verde, 0.95f, 14f, palmera: true);
            // Cerezos: las mismas mallas con la paleta rosa.
            E("Cerezo grande", Tono.Cerezo, 0.83f, 9f, prefab: "Tree05_a01");
            E("Cerezo", Tono.Cerezo, 0.70f, 9f, prefab: "Tree03_a01");
            E("Cerezo de copa alta", Tono.Cerezo, 0.68f, 9f, prefab: "Tree03_a02");
            // Matas (Tree07 con la esfera de choque que crece con la escala: como mucho 2,5 de escala).
            E("Tree07_b01", Tono.Verde, 0.98f, 4.4f, Clase.Mata); E("Tree07_a01", Tono.Turquesa, 1.13f, 3.9f, Clase.Mata);
            E("Tree07_c01", Tono.Rosa, 1.13f, 3.9f, Clase.Mata); E("Tree06_a01", Tono.Verde, 1.86f, 3.2f, Clase.Mata);
            // Setos recortados (el largo va por su Z local) y flores del pack para los jardines.
            E("Plant03_a02", Tono.Verde, 1.3f, 2f, Clase.Seto); E("Plant03_a03", Tono.Verde, 1.4f, 2f, Clase.Seto);
            E("Plant01_a04", Tono.Verde, 1.3f, 2f, Clase.Seto); E("Plant02_a04", Tono.Verde, 1.3f, 2f, Clase.Seto);
            E("Flower04_a03", Tono.Rosa, 0.5f, 1.2f, Clase.Flor); E("Flower05_a01", Tono.Rosa, 0.5f, 1.2f, Clase.Flor);
            E("Flower01_a01", Tono.Rosa, 0.5f, 1.2f, Clase.Flor); E("Flower01_c01", Tono.Rosa, 0.5f, 1.2f, Clase.Flor);
            E("Flower02_b01", Tono.Azul, 0.5f, 1.2f, Clase.Flor);
            return d;
        }

        /// Lista de especies con peso: «Tree03_a01*3 Tree05_a01*2 Tree03_c02».
        private static (string especie, float peso)[] L(string texto)
        {
            string[] partes = texto.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            var r = new (string, float)[partes.Length];
            for (int i = 0; i < partes.Length; i++)
            {
                int k = partes[i].IndexOf('*');
                r[i] = k < 0 ? (partes[i], 1f) : (partes[i].Substring(0, k), float.Parse(partes[i].Substring(k + 1), System.Globalization.CultureInfo.InvariantCulture));
            }
            return r;
        }

        // ── Paletas por zona ─────────────────────────────────────────────────────────────────

        private static readonly (string, float)[] VerdeDeCampo = L("Tree03_a01*3 Tree05_a01*3 Tree03_c02*1.5 Tree03_b01 Tree02_b01");
        private static readonly (string, float)[] FrondososVerdes = L("Tree03_a01 Tree05_a01 Tree03_c02*0.6");
        private static readonly (string, float)[] Otono = L("Tree03_e01*2 Tree03_f01 Tree02_e01*1.5 Tree03_e02*1.5");
        private static readonly (string, float)[] Cosecha = L("Tree02_f01*2 Tree03_e02*2 Tree02_e02*1.5 Tree01_b02");
        private static readonly (string, float)[] Lima = L("Tree01_b01 Tree04_d01 Tree04_d02*0.7");
        private static readonly (string, float)[] Coniferas = L("Tree01_a01*2 Tree01_a02*2 Tree02_c01*1.5 Tree02_c02*1.5");
        private static readonly (string, float)[] Turquesa = L("Tree04_a01*2 Tree04_a02 Tree04_a03");
        private static readonly (string, float)[] MatasDeCampo = L("Tree07_b01*3 Tree06_a01");
        private static readonly (string, float)[] MatasDelReino = L("Tree07_b01*2 Tree07_a01");

        /// Cómo se compone un bosquete: la especie dominante (≥60 %), las compañeras del resto y, con
        /// ProbAcento, un acento de color.
        private sealed class Receta
        {
            public float Peso = 1f;
            public (string, float)[] Dominante, Companeras, Acento;
            public float ProbAcento;
        }

        private static Receta R(float peso, (string, float)[] dominante, (string, float)[] companeras = null, (string, float)[] acento = null, float probAcento = 0f) =>
            new Receta { Peso = peso, Dominante = dominante, Companeras = companeras, Acento = acento, ProbAcento = probAcento };

        /// Paleta de flores de una zona (ver Ramos).
        private enum Flores { Campo, Otono, Cosecha, Reino, Ribera }

        /// Una zona de vegetación: arboleda dentro de un contorno, orla de un camino u orla de un pueblo.
        private sealed class Mezcla
        {
            public string Nombre;
            public int Semilla;
            public int TamMin = 3, TamMax = 6;
            public float AltoMin = 10f, AltoMax = 13f;
            public Receta[] Recetas;
            public (string, float)[] Matas = MatasDeCampo;
            public float PendienteMax = 32f;
            /// Árboles verdes con Follaje_1 (borde del bosque) en vez de Follaje_0.
            public bool FollajeDeBosque;
            public Flores Flores = Flores.Campo;
            // Arboleda
            public Vector2[] Contorno;
            public float Separacion = 30f;
            /// Ruido de 80 m por debajo del cual no se pone bosquete: prado abierto entre arboledas.
            public float Umbral;
            /// Árboles que ya había a menos de 10 m del centro para dar el sitio por arbolado.
            public int MaxExistentes = 3;
            /// Alrededor del Reino: fuera de la muralla y apartado de su cara.
            public bool FueraDeLaMuralla;
            // Orla de camino
            public Vector2[] Linea;
            public float Desde, Hasta;
            // Orla de pueblo
            public System.Func<Pueblo> Pueblo;
            public float FueraMin = -2f, FueraMax = 14f;
            /// Orla de pueblo: solo al norte de esta z (el puerto da al mar por el sur).
            public float SoloAlNorteDe = float.NegativeInfinity;
        }

        // ── Datos del mapa ───────────────────────────────────────────────────────────────────

        /// Semiancho (m) del suelo pintado de los caminos del mapa, con su fundido.
        private const float SemianchoDeCamino = 5.4f;
        /// Ribera de la laguna (centro y semiejes de una elipse 18 m más allá del agua): la viste VestidoDelMundo.Agua.
        private static readonly Vector4 RiberaDeLaLaguna = new Vector4(186f, -35f, 57f, 42f);
        /// Marjal del sureste: reservado para Risco y Vega (GDD § 10); no se planta hasta que se decida.
        private static readonly Rect MarjalReservado = Rect.MinMaxRect(196f, -264f, 269f, -157f);
        /// Por encima de esta cota empieza la ladera de la montaña (la meseta del castillo está a 112 m).
        private const float CotaDeLaMontana = 118f;

        // Caminos del mapa (ejes del generador) con orla de vegetación.
        private static readonly Vector2[] CaminoDelPuerto = P(50.8f, -183.8f, 53f, -186.1f, 57.2f, -190.6f, 63.6f, -197.4f, 72.2f, -206.4f, 83.9f, -217.3f, 98.8f, -229.9f, 116.9f, -244.4f, 138.1f, -260.6f, 156.9f, -277.8f, 173.1f, -295.9f, 186.9f, -315f, 198.1f, -335f, 210f, -354.7f, 222.5f, -374.1f, 235.6f, -393.1f, 249.4f, -411.9f, 259.7f, -425.9f, 266.6f, -435.3f, 270f, -440f);
        private static readonly Vector2[] CaminoDeLasGranjas = P(59.8f, -86.5f, 62.9f, -84.2f, 69.2f, -79.7f, 78.6f, -72.8f, 91.2f, -63.7f, 105.6f, -48.7f, 121.9f, -27.9f, 140f, -1.2f, 160f, 31.2f, 177.6f, 60f, 192.9f, 85f, 205.8f, 106.2f, 216.2f, 123.8f, 225.2f, 142.5f, 232.8f, 162.5f, 238.8f, 183.8f, 243.2f, 206.2f, 246.6f, 223.1f, 248.9f, 234.4f, 250f, 240f);
        private static readonly Vector2[] CaminoDelReino = P(37.4f, -66.4f, 41.3f, -59.7f, 49.2f, -46.4f, 60.9f, -26.5f, 76.5f, 0.1f, 84.5f, 23.2f, 84.8f, 42.7f, 77.5f, 58.8f, 62.5f, 71.2f, 43.8f, 83.1f, 21.2f, 94.4f, -5f, 105f, -35f, 115f, -60.3f, 125.3f, -80.9f, 135.9f, -96.9f, 146.9f, -108.1f, 158.1f, -115.6f, 169.7f, -119.4f, 181.6f, -119.4f, 193.8f, -115.6f, 206.2f, -112.8f, 215.6f, -110.9f, 221.9f, -110f, 225f);
        private static readonly Vector2[] CaminoDelVaradero = P(-56.5f, -177.8f, -61.1f, -181.7f, -70.2f, -189.4f, -84f, -201.1f, -102.4f, -216.7f, -126.2f, -232.4f, -155.4f, -248.3f, -190f, -264.4f, -230f, -280.6f, -265.6f, -291.6f, -296.9f, -297.2f, -323.8f, -297.5f, -346.2f, -292.5f, -363.1f, -288.8f, -374.4f, -286.2f, -380f, -285f);
        private static readonly Vector2[] CaminoDelVecino = P(190f, 80f, 194.9f, 81.4f, 204.6f, 84.1f, 219.2f, 88.2f, 238.8f, 93.8f, 258.5f, 95.8f, 278.5f, 94.2f, 298.8f, 89.2f, 319.2f, 80.8f, 331f, 70.9f, 334f, 59.6f, 328.2f, 47f, 313.8f, 33f, 306.5f, 18.8f, 306.5f, 4.2f, 313.8f, -10.5f, 328.2f, -25.5f, 337.9f, -40.9f, 342.6f, -56.8f, 342.5f, -73.1f, 337.5f, -89.9f, 333.8f, -102.4f, 331.2f, -110.8f, 330f, -115f);
        private static readonly Vector2[] CaminoDelBosque = P(-237.3f, 17.5f, -228.4f, 20.5f, -210.6f, 26.4f, -183.9f, 35.3f, -148.4f, 47.2f, -116.7f, 55.5f, -88.9f, 60.2f, -65f, 61.2f, -45f, 58.8f, -26.7f, 58.8f, -10.1f, 61.4f, 4.9f, 66.6f, 18.1f, 74.4f, 28.1f, 80.2f, 34.7f, 84.1f, 38f, 86f);
        private static readonly Vector2[] CaminoDeLaPradera = P(-85f, -100f, -83.6f, -97.6f, -80.7f, -92.7f, -76.4f, -85.4f, -70.6f, -75.6f, -66.3f, -68.3f, -63.4f, -63.4f, -62f, -61f);
        /// Camino 10 al pie de la muralla sur, hasta la Puerta Real (con el desvío de la capital).
        private static readonly Vector2[] CaminoBajoLaMuralla = P(-110f, 225f, -103.1f, 225f, -89.4f, 225f, -68.8f, 225f, -41.3f, 225f, -14.7f, 225.6f, 10.9f, 226.9f, 35.6f, 228.8f, 59.4f, 231.2f, 72f, 233.2f, 84f, 234.2f);
        /// Tramo alto del arroyo de la ladera este, bajo el hombro del Reino.
        private static readonly Vector2[] ArroyoDeLaLaderaEste = P(150f, 330f, 150.9f, 328.1f, 152.8f, 324.4f, 155.6f, 318.8f, 159.4f, 311.2f, 163.2f, 303.7f, 167.1f, 296.1f, 171f, 288.4f, 175f, 280.6f, 178.6f, 273.8f, 181.7f, 267.8f, 184.4f, 262.6f, 186.6f, 258.4f, 191.2f, 253.1f, 198.2f, 246.7f, 207.6f, 239.2f, 219.4f, 230.8f, 231.4f, 222.5f);

        private static readonly Vector2[][] CaminosVisibles =
            { CaminoDelPuerto, CaminoDeLasGranjas, CaminoDelReino, CaminoDelVaradero, CaminoDelVecino, CaminoDelBosque, CaminoDeLaPradera, CaminoBajoLaMuralla };

        /// Punto del pie de la muralla sur, bajo el castillo, al que mira el Mirador cuando se vuelve hacia el Reino.
        private static readonly Vector2 VistaDelMiradorAlCastillo = new Vector2(2f, 232f);
        /// Tramos del Camino 10 (x) sin cipreses: la vista del Mirador al castillo y a la torre suroeste.
        private static readonly Vector2[] VentanasDelCamino10 = { new Vector2(-18f, 26f), new Vector2(-66f, -46f) };

        /// Los dos jardines de palacio (x = ±87): sus parterres con Tree04 se quedan y se completan con una hilera de
        /// cerezos entre ellos y la muralla, al tresbolillo con los parterres.
        private static readonly float[] CerezosDePalacioZ = { 319.5f, 328.5f, 337.5f, 346.5f };
        private const float CerezosDePalacioX = 97f;
        /// Jardín hundido al pie de la muralla sur, junto a la taberna (la hondonada del antiguo huerto).
        private static readonly Rect JardinHundido = Rect.MinMaxRect(-47f, 243f, -17f, 254.5f);
        /// Plazoleta del pozo de la ciudad alta (VestidoDelMundo.Capital): un cerezo da sombra a su banco, entre
        /// el pozo y el castillo.
        private static readonly Vector2 CerezoDeLaPlazoleta = new Vector2(30.2f, 329.6f);

        private static Mezcla[] Arboledas() => new[]
        {
            new Mezcla
            {
                Nombre = "Ladera de otoño de la subida al Reino", Semilla = 7401, Separacion = 22f, Umbral = 0.22f, TamMin = 3, TamMax = 6, AltoMin = 9f, AltoMax = 12.5f,
                Contorno = P(-100f, 150f, 100f, 150f, 104f, 214f, 60f, 220f, -60f, 216f, -100f, 214f), Flores = Flores.Otono,
                Recetas = new[] { R(0.7f, FrondososVerdes, acento: Otono, probAcento: 0.4f), R(0.3f, Otono, Otono) },
            },
            new Mezcla
            {
                Nombre = "Lazo oeste de la subida", Semilla = 7402, Separacion = 22f, Umbral = 0.22f, TamMin = 3, TamMax = 6, AltoMin = 9f, AltoMax = 12.5f,
                Contorno = P(-135f, 120f, -62f, 120f, -62f, 212f, -100f, 220f, -135f, 232f), Flores = Flores.Otono,
                Recetas = new[] { R(0.7f, FrondososVerdes, acento: Otono, probAcento: 0.4f), R(0.3f, Otono, Otono) },
            },
            new Mezcla
            {
                Nombre = "Arboleda dorada de las granjas", Semilla = 7403, Separacion = 26f, Umbral = 0.25f, TamMin = 3, TamMax = 7, AltoMin = 9.5f, AltoMax = 12.5f,
                Contorno = P(95f, 55f, 200f, 55f, 240f, 100f, 240f, 250f, 150f, 250f, 95f, 200f), Flores = Flores.Cosecha,
                Recetas = new[] { R(0.5f, Cosecha, L("Tree02_f01 Tree03_e02 Tree02_e02 Tree03_a01 Tree05_a01")), R(0.5f, VerdeDeCampo, acento: Cosecha, probAcento: 0.6f) },
            },
            new Mezcla
            {
                Nombre = "Sotos del valle", Semilla = 7404, Separacion = 30f, Umbral = 0.25f, AltoMin = 10f, AltoMax = 13f,
                Contorno = P(-40f, -55f, 60f, -55f, 140f, 10f, 150f, 60f, 60f, 125f, -40f, 112f, -70f, 60f),
                Recetas = new[] { R(1f, VerdeDeCampo, acento: L("Tree01_b01 Tree04_d01 Tree04_d02*0.7 Tree03_e02"), probAcento: 0.6f) },
            },
            new Mezcla
            {
                Nombre = "Sotos del sureste", Semilla = 7405, Separacion = 30f, Umbral = 0.3f, AltoMin = 10.5f, AltoMax = 13.5f,
                Contorno = P(60f, -290f, 360f, -290f, 360f, -150f, 315f, -40f, 160f, -40f, 60f, -140f),
                Recetas = new[] { R(1f, L("Tree05_a01*3 Tree03_a02*2 Tree03_a01"), acento: L("Tree03_e02 Tree01_b01 Tree04_d03*0.6"), probAcento: 0.5f) },
            },
            new Mezcla
            {
                Nombre = "Sotos del sur", Semilla = 7406, Separacion = 30f, Umbral = 0.3f, AltoMin = 10.5f, AltoMax = 13.5f,
                Contorno = P(-75f, -330f, 165f, -330f, 165f, -185f, -75f, -185f),
                Recetas = new[] { R(1f, L("Tree05_a01*3 Tree03_a02*2 Tree03_a01"), acento: L("Tree03_e02 Tree01_b01 Tree04_d03*0.6"), probAcento: 0.5f) },
            },
            new Mezcla
            {
                Nombre = "Prados del oeste de Will", Semilla = 7408, Separacion = 30f, Umbral = 0.3f, AltoMin = 10f, AltoMax = 13f,
                Contorno = P(-150f, -200f, -74f, -200f, -74f, -60f, -135f, -60f, -150f, -152f),
                Recetas = new[] { R(1f, VerdeDeCampo, acento: L("Tree01_b01 Tree04_d01 Tree03_e02"), probAcento: 0.45f) },
            },
            new Mezcla
            {
                Nombre = "Lomas de la costa sur", Semilla = 7409, Separacion = 34f, Umbral = 0.3f, AltoMin = 9.5f, AltoMax = 12.5f,
                Contorno = P(-250f, -420f, 160f, -420f, 160f, -330f, -250f, -330f),
                Recetas = new[] { R(0.8f, L("Tree03_a01*2 Tree05_a01 Tree02_a02"), acento: L("Tree01_b01 Tree04_d03"), probAcento: 0.4f), R(0.2f, L("Tree06_a02 Tree06_a03")) },
            },
            new Mezcla
            {
                Nombre = "Sotos del arroyo de las granjas", Semilla = 7413, Separacion = 30f, Umbral = 0.3f, AltoMin = 9.5f, AltoMax = 12.5f,
                Contorno = P(240f, 160f, 330f, 160f, 330f, 250f, 240f, 262f),
                Recetas = new[] { R(0.7f, VerdeDeCampo, acento: Cosecha, probAcento: 0.5f), R(0.3f, L("Tree04_a02 Tree04_a03"), Turquesa) },
            },
            new Mezcla
            {
                Nombre = "Oeste: redil y varadero", Semilla = 7407, Separacion = 30f, Umbral = 0.3f, AltoMin = 10f, AltoMax = 13f,
                Contorno = P(-440f, -305f, -130f, -305f, -130f, -200f, -440f, -195f),
                Recetas = new[] { R(0.75f, L("Tree03_a01*2 Tree05_a01*2 Tree02_a02"), acento: L("Tree01_b01"), probAcento: 0.25f), R(0.25f, L("Tree06_a02 Tree06_a03")) },
            },
        };

        private static Mezcla[] OrlasDeCamino() => new[]
        {
            new Mezcla { Nombre = "Camino del puerto", Semilla = 7501, Linea = CaminoDelPuerto, Desde = 12f, Hasta = 25f, TamMin = 3, TamMax = 4, AltoMin = 10f, AltoMax = 12.5f,
                Recetas = new[] { R(0.7f, L("Tree02_c02*2 Tree02_a02")), R(0.3f, L("Tree06_a02 Tree06_a03")) } },
            new Mezcla { Nombre = "Camino de las granjas", Semilla = 7502, Linea = CaminoDeLasGranjas, Desde = 10f, Hasta = 10f, TamMin = 3, TamMax = 4, AltoMin = 10f, AltoMax = 12.5f,
                Recetas = new[] { R(1f, FrondososVerdes, acento: Lima, probAcento: 0.35f) } },
            new Mezcla { Nombre = "Camino del Reino", Semilla = 7503, Linea = CaminoDelReino, Desde = 10f, Hasta = 8f, TamMin = 3, TamMax = 4, AltoMin = 10f, AltoMax = 12.5f,
                Recetas = new[] { R(1f, FrondososVerdes, acento: Lima, probAcento: 0.35f) } },
            new Mezcla { Nombre = "Camino del varadero", Semilla = 7505, Linea = CaminoDelVaradero, Desde = 10f, Hasta = 10f, TamMin = 3, TamMax = 4, AltoMin = 10f, AltoMax = 12.5f,
                Recetas = new[] { R(1f, FrondososVerdes, acento: Lima, probAcento: 0.3f) } },
            new Mezcla { Nombre = "Entrada al pueblo vecino", Semilla = 7506, Linea = CaminoDelVecino, Desde = 10f, Hasta = 12f, TamMin = 3, TamMax = 4, AltoMin = 10f, AltoMax = 12.5f,
                Recetas = new[] { R(1f, L("Tree05_a01*2 Tree03_a02*2"), acento: Lima, probAcento: 0.4f) } },
            new Mezcla { Nombre = "Camino del borde del bosque", Semilla = 7508, Linea = CaminoDelBosque, Desde = 10f, Hasta = 10f, TamMin = 3, TamMax = 4, AltoMin = 11f, AltoMax = 14f,
                FollajeDeBosque = true, Recetas = new[] { R(1f, L("Tree02_b01*2 Tree03_d01 Tree03_c01")) } },
            new Mezcla { Nombre = "Camino de la pradera", Semilla = 7509, Linea = CaminoDeLaPradera, Desde = 4f, Hasta = 4f, TamMin = 3, TamMax = 4, AltoMin = 10f, AltoMax = 12.5f,
                Recetas = new[] { R(1f, FrondososVerdes) } },
        };

        private static Mezcla[] OrlasDePueblo() => new[]
        {
            new Mezcla
            {
                Nombre = "Orla del pueblo vecino", Semilla = 7601, Pueblo = Vecino, Separacion = 22f, TamMin = 3, TamMax = 6, AltoMin = 9.5f, AltoMax = 12.5f,
                Recetas = new[] { R(0.7f, L("Tree05_a01*2 Tree03_a02*2"), L("Tree05_a01 Tree03_a02 Tree06_a03*0.5"), L("Tree04_d02 Tree01_b01"), 0.6f), R(0.3f, L("Tree04_d02 Tree04_d01")) },
            },
            new Mezcla
            {
                Nombre = "Orla del puerto", Semilla = 7602, Pueblo = Puerto, Separacion = 20f, TamMin = 3, TamMax = 5, AltoMin = 9f, AltoMax = 12f, FueraMin = -4f, FueraMax = 12f,
                SoloAlNorteDe = -428f, Matas = L("Tree07_a01 Tree07_b01"), Flores = Flores.Ribera,
                Recetas = new[] { R(0.45f, L("Tree06_a02 Tree06_a03")), R(0.3f, Turquesa, L("Tree04_a01*2 Tree04_a02 Tree04_a03 Tree02_c01")), R(0.25f, L("Tree02_c01"), acento: Turquesa, probAcento: 0.5f) },
            },
            new Mezcla
            {
                Nombre = "Orla de las granjas", Semilla = 7603, Pueblo = Granjas, Separacion = 22f, TamMin = 3, TamMax = 6, AltoMin = 9.5f, AltoMax = 12.5f, Flores = Flores.Cosecha,
                Recetas = new[] { R(0.5f, Cosecha, L("Tree02_f01 Tree03_e02 Tree02_e02 Tree01_b02 Tree05_a01")), R(0.5f, L("Tree05_a01*2 Tree03_a01"), acento: Cosecha, probAcento: 0.6f) },
            },
        };

        /// Alrededor del Reino, fuera de la muralla: pinar tras el castillo y hombros oeste y este.
        private static Mezcla[] OrlaDelReino() => new[]
        {
            new Mezcla
            {
                Nombre = "Pinar tras el castillo", Semilla = 7410, Separacion = 18f, TamMin = 4, TamMax = 6, AltoMin = 11f, AltoMax = 14f, MaxExistentes = 9,
                FueraDeLaMuralla = true, Matas = null, Flores = Flores.Reino,
                Contorno = P(-92f, 360f, 92f, 360f, 88f, 372f, 70f, 382f, 40f, 388f, -40f, 388f, -70f, 382f, -88f, 372f),
                Recetas = new[] { R(1f, Coniferas, acento: Turquesa, probAcento: 0.3f) },
            },
            new Mezcla
            {
                Nombre = "Hombro oeste del Reino", Semilla = 7411, Separacion = 17f, TamMin = 3, TamMax = 6, AltoMin = 10f, AltoMax = 13f, MaxExistentes = 9, PendienteMax = 30f,
                FueraDeLaMuralla = true, Matas = MatasDelReino, Flores = Flores.Reino,
                Contorno = P(-132f, 236f, -100f, 236f, -106f, 262f, -106f, 350f, -132f, 350f),
                Recetas = new[] { R(0.8f, Coniferas, acento: Turquesa, probAcento: 0.45f), R(0.2f, Turquesa, Turquesa) },
            },
            new Mezcla
            {
                Nombre = "Hombro este del Reino", Semilla = 7412, Separacion = 17f, TamMin = 3, TamMax = 6, AltoMin = 10f, AltoMax = 13f, MaxExistentes = 9, PendienteMax = 30f,
                FueraDeLaMuralla = true, Matas = MatasDelReino, Flores = Flores.Reino,
                Contorno = P(110f, 256f, 130f, 256f, 130f, 322f, 118f, 338f, 110f, 338f),
                Recetas = new[] { R(0.8f, Coniferas, acento: Turquesa, probAcento: 0.45f), R(0.2f, Turquesa, Turquesa) },
            },
        };

        // ── Plan ─────────────────────────────────────────────────────────────────────────────

        /// Una pieza planeada.
        private sealed class Plantada
        {
            public Vector2 Pos;
            public Especie Especie;
            public float Alto, Rumbo, Inclinar;
            public string Zona, Grupo;
            public bool Dominante, FollajeDeBosque;
            /// Dentro de la ciudad: no tapa calles ni sendas de puerta (Obra.Corredores) al colocarla.
            public bool EnLaCiudad;
            public float Ancho => Especie.Ancho(Alto);
        }

        /// Mancha de flores pintada como hierba de detalle: elipse de semiejes Radio × Alargamiento (a lo largo del
        /// rumbo Giro, en grados) y Radio, con el borde roto por ruido.
        private sealed class Mancha
        {
            public Vector2 Centro;
            public float Radio, Alargamiento = 1f, Giro;
            public Flores Paleta;
            public int Semilla;
        }

        /// Índice espacial en una rejilla de celdas cuadradas.
        private sealed class Rejilla
        {
            private readonly float lado;
            private readonly Dictionary<long, List<int>> celdas = new();
            public Rejilla(float lado) { this.lado = lado; }
            private long Clave(int i, int k) => ((long)i << 32) ^ (uint)k;
            public void Anadir(Vector2 p, int indice)
            {
                long c = Clave(Mathf.FloorToInt(p.x / lado), Mathf.FloorToInt(p.y / lado));
                if (!celdas.TryGetValue(c, out List<int> l)) celdas[c] = l = new List<int>();
                l.Add(indice);
            }
            public void Cerca(Vector2 p, float radio, List<int> salida)
            {
                salida.Clear();
                int i0 = Mathf.FloorToInt((p.x - radio) / lado), i1 = Mathf.FloorToInt((p.x + radio) / lado);
                int k0 = Mathf.FloorToInt((p.y - radio) / lado), k1 = Mathf.FloorToInt((p.y + radio) / lado);
                for (int i = i0; i <= i1; i++)
                    for (int k = k0; k <= k1; k++)
                        if (celdas.TryGetValue(Clave(i, k), out List<int> l)) salida.AddRange(l);
            }
        }

        private sealed class Plan
        {
            public Lienzo L;
            public readonly List<Plantada> Piezas = new();
            public readonly Rejilla IndicePiezas = new Rejilla(8f);
            public readonly List<Mancha> Manchas = new();
            public readonly List<(Vector2[] linea, float radio)> Vistas = new();
            /// Franjas en las que no va ningún árbol más que los que ya se han planeado en ellas (la alameda) o
            /// ninguno (la vista del Mirador al castillo): polilínea, radio y motivo.
            public readonly List<(Vector2[] linea, float radio, string motivo)> Reservas = new();
            /// Árboles que ya estaban en la escena: posición del tronco y radio de copa.
            public readonly List<Vector3> Existentes = new();
            public readonly Rejilla IndiceExistentes = new Rejilla(10f);
            public readonly List<(Vector2 pos, Tono tono)> Acentos = new();
            public readonly Dictionary<string, int> Rechazos = new();
            public List<Zona> Libres;
            public Vector2[] MurallaCerrada;
            public (Vector2 centro, Vector2 vista)? Mirador;
            public readonly List<(Vector2 pos, float radio)> Parajes = new();
            /// Edificios, torres y casas nuevas que ya están al planear (ver IndexarObra).
            public readonly List<Huella> Obra = new();
            public Mezcla[] Arboledas;
            public Pueblo Reino;
            public readonly List<int> Tmp = new();

            public void Rechazar(string motivo)
            {
                Rechazos.TryGetValue(motivo, out int n);
                Rechazos[motivo] = n + 1;
            }
        }

        /// El plan entero, a partir del terreno pintado, de lo que ya había en la escena y de los datos.
        private static Plan Planear(Lienzo l)
        {
            var p = new Plan { L = l, Libres = new List<Zona>(ZonasLibres), Arboledas = Arboledas(), Reino = Reino() };
            ZonasDeLaCapital(p.Libres);
            ZonasDelAgua(p.Libres);
            p.MurallaCerrada = PoligonoCerrado(MurallaDelReino.TrazaMedia());
            foreach (Paraje pj in Parajes())
            {
                p.Parajes.Add((pj.Centro, 16f));
                // El mirador mira al valle: el pretil está en su lado contrario a la senda.
                if (pj.Nombre.StartsWith("Mirador"))
                {
                    p.Mirador = (pj.Centro, -FrenteDe(pj.Frente));
                    // Hacia atrás, el Mirador ve la muralla y el castillo por encima del camino: sin árboles en medio.
                    p.Reservas.Add((new[] { pj.Centro, VistaDelMiradorAlCastillo }, 7f, "vista del Mirador al castillo"));
                }
            }
            IndexarExistentes(l.Terreno.gameObject.scene, p);
            IndexarObra(l.Terreno.gameObject.scene, p);

            PlanearReino(p);
            foreach (Mezcla m in OrlasDeCamino()) PlanearOrlaDeCamino(p, m);
            foreach (Mezcla m in OrlaDelReino()) PlanearArboleda(p, m);
            foreach (Mezcla m in p.Arboledas) PlanearArboleda(p, m);
            foreach (Mezcla m in OrlasDePueblo()) PlanearOrlaDePueblo(p, m);
            PlanearRiberaDelArroyo(p);
            PlanearMatasEnTaludes(p);
            PlanearManchasDeFlores(p);
            return p;
        }

        /// Árboles que ya estaban en la escena (instancias de prefab o mallas sueltas que se llaman TreeNN_xNN),
        /// fuera de lo generado y de lo retirado.
        private static void IndexarExistentes(Scene escena, Plan p)
        {
            void Recorrer(Transform t)
            {
                if (!t.gameObject.activeInHierarchy || EsDelVestido(t)) return;
                string nombre = t.name;
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                {
                    GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (fuente != null) nombre = fuente.name;
                }
                if (EsNombreDeArbol(nombre))
                {
                    Bounds b = LimitesVisibles(t.gameObject);
                    if (b.size.sqrMagnitude > 0.01f)
                    {
                        var pos = new Vector2(t.position.x, t.position.z);
                        p.IndiceExistentes.Anadir(pos, p.Existentes.Count);
                        p.Existentes.Add(new Vector3(pos.x, pos.y, Mathf.Max(b.extents.x, b.extents.z)));
                    }
                    return;
                }
                foreach (Transform h in t) Recorrer(h);
            }
            foreach (GameObject raiz in escena.GetRootGameObjects()) Recorrer(raiz.transform);
        }

        private static readonly string[] PrefijosDeEdificio = { "BuildingAT", "Castle", "Stronghold", "WatchTower", "Tower0", "Wall0" };

        /// Huellas de lo que ya está construido al planear: edificios del mapa que siguen activos y, de lo
        /// generado, las casas nuevas y la muralla nueva (torres y Puerta Real), que se ponen antes que el suelo. Así
        /// el plan no busca sitio dentro de una casa (Poner lo comprobaría igual, pero el jardín hundido y el paseo
        /// de ronda eligen otro sitio si el primero está ocupado).
        private static void IndexarObra(Scene escena, Plan p)
        {
            void Anadir(GameObject go)
            {
                if (!go.activeInHierarchy) return;
                Huella h = HuellaOrientada(go, go.transform.eulerAngles.y);
                if (h.MedioX > 0.5f || h.MedioZ > 0.5f) p.Obra.Add(h);
            }
            void Recorrer(Transform t)
            {
                if (!t.gameObject.activeInHierarchy || EsDelVestido(t)) return;
                if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                {
                    GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    string nombre = fuente != null ? fuente.name : t.name;
                    foreach (string prefijo in PrefijosDeEdificio)
                        if (nombre.StartsWith(prefijo)) { Anadir(t.gameObject); return; }
                }
                foreach (Transform h in t) Recorrer(h);
            }
            foreach (GameObject raiz in escena.GetRootGameObjects()) Recorrer(raiz.transform);

            Transform generado = BuscarRaiz(escena);
            if (generado == null) return;
            foreach (Transform t in generado.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null) continue;
                string padre = t.parent.name;
                bool deLaMuralla = t.parent.parent != null && t.parent.parent.name == "Muralla";
                if (padre == GrupoCasasNuevas || (padre == "Torres" && deLaMuralla) || (t.name == "Puerta Real" && padre == "Muralla"))
                    Anadir(t.gameObject);
            }
        }

        /// Si el punto (con un margen) cae en algo construido, o la copa de radio «copa» se mete en ello.
        private static bool EnLaObra(Plan p, Vector2 q, float margen, float copa)
        {
            var c = new Huella { Centro = q, EjeX = Vector2.right, EjeZ = Vector2.up, MedioX = copa, MedioZ = copa };
            foreach (Huella h in p.Obra)
            {
                if (h.Contiene(q, margen)) return true;
                if (copa > 0f && h.Solapa(c)) return true;
            }
            return false;
        }

        /// «Tree05_a01», «Tree01_a02 (3)»…
        private static bool EsNombreDeArbol(string n) =>
            n.Length >= 10 && n.StartsWith("Tree0") && char.IsDigit(n[5]) && n[6] == '_' && n[7] >= 'a' && n[7] <= 'f' && char.IsDigit(n[8]) && char.IsDigit(n[9]);

        // ── Comprobaciones del plan ──────────────────────────────────────────────────────────

        /// Peso de camino o calle pintado en el punto (como PesoCamino, sin necesitar la Obra).
        private static float PesoDeCamino(Lienzo l, float x, float z) =>
            l.Peso(x, z, l.Capa("Capa4")) + l.Peso(x, z, l.Capa(CapaAdoquin)) + l.Peso(x, z, l.Capa(CapaBaldosa)) +
            l.Peso(x, z, l.Capa("SueloUrbano1")) + l.Peso(x, z, l.Capa(CapaTierraPiedras));

        /// Distancia desde el punto hasta el primer suelo de camino pintado (peso > 0,5) en ocho rumbos, hasta «maximo».
        private static float DistanciaAlCaminoPintado(Lienzo l, Vector2 p, float maximo)
        {
            float mejor = maximo;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                for (float t = 0.75f; t < mejor; t += 0.75f)
                    if (PesoDeCamino(l, p.x + c * t, p.y + s * t) > 0.5f) { mejor = t; break; }
            }
            return mejor;
        }

        /// Distancia en planta a la cara de la muralla del Reino (a su eje menos el semigrueso de los lienzos).
        private static float DistanciaALaMuralla(Plan p, Vector2 q) => DistanciaAPolilinea(q, p.MurallaCerrada) - 1.72f;

        /// Lo que no se planta nunca, además de las zonas libres. Devuelve el motivo o null.
        private static string Excluido(Plan p, Vector2 q)
        {
            Vector4 e = RiberaDeLaLaguna;
            float ex = (q.x - e.x) / e.z, ez = (q.y - e.y) / e.w;
            if (ex * ex + ez * ez < 1f) return "ribera de la laguna (la viste el agua)";
            if (MarjalReservado.Contains(q)) return "marjal del sureste (reservado)";
            if (p.L.Altura(q.x, q.y) > CotaDeLaMontana) return "ladera de la montaña";
            foreach (var (pos, radio) in p.Parajes)
                if (Vector2.Distance(pos, q) < radio) return "paraje (tiene su propia composición)";
            if (p.Mirador.HasValue)
            {
                Vector2 d = q - p.Mirador.Value.centro;
                float m = d.magnitude;
                if (m < 18f || (m < 110f && Vector2.Angle(d, p.Mirador.Value.vista) < 32f)) return "vista del Mirador al valle";
            }
            return null;
        }

        private static bool EnVista(Plan p, Vector2 q)
        {
            foreach (var (linea, radio) in p.Vistas)
                if (DistanciaAPolilinea(q, linea) < radio) return true;
            return false;
        }

        private static string EnZonaLibre(Plan p, Vector2 q, float radio)
        {
            foreach (Zona z in p.Libres)
            {
                bool dentro = z.Medio == Vector2.zero
                    ? Vector2.Distance(q, z.Centro) < z.Radio + radio
                    : Mathf.Abs(q.x - z.Centro.x) < z.Medio.x + radio && Mathf.Abs(q.y - z.Centro.y) < z.Medio.y + radio;
                if (dentro) return z.Nombre;
            }
            return null;
        }

        private enum Sitio { Campo, FueraDeLaMuralla, Ciudad }

        /// Si la pieza cabe en el plan: terreno, caminos, exclusiones, zonas libres y distancia a los árboles que
        /// ya había y a lo ya planeado. Devuelve el motivo del rechazo o null.
        private static string Libre(Plan p, Vector2 q, Especie e, float alto, float pendienteMax, float borde, Sitio sitio = Sitio.Campo)
        {
            Lienzo l = p.L;
            float radio = e.Ancho(alto) * 0.5f;
            if (l.Altura(q.x, q.y) < 1.5f) return "en el agua o en la orilla";
            if (l.Pendiente(q.x, q.y) > pendienteMax) return "pendiente";
            if (sitio != Sitio.Ciudad && PesoDeCamino(l, q.x, q.y) > 0.25f) return "sobre un camino";
            if (!e.Palmera && l.Peso(q.x, q.y, l.Capa(CapaArena)) > 0.45f) return "arena";
            if (borde > 0f && sitio != Sitio.Ciudad && DistanciaAlCaminoPintado(l, q, borde + 0.01f) < borde) return "cerca del borde del camino";
            string motivo = Excluido(p, q);
            if (motivo != null) return motivo;
            if (e.Clase == Clase.Arbol)
            {
                if (EnVista(p, q)) return "tramo de camino con vistas";
                foreach (var (linea, r, porque) in p.Reservas)
                    if (DistanciaAPolilinea(q, linea) < r) return porque;
            }
            if (sitio != Sitio.Campo)
            {
                bool dentro = DentroDePoligono(q, TrazaMuralla);
                if (sitio == Sitio.FueraDeLaMuralla && dentro) return "dentro de la muralla";
                if (sitio == Sitio.Ciudad && !dentro) return "fuera de la muralla";
                float muro = DistanciaALaMuralla(p, q);
                float minimo = e.Clase == Clase.Arbol ? (sitio == Sitio.Ciudad ? 0.85f * radio : Mathf.Max(4f, 0.85f * radio + 1f)) : 0.6f;
                if (muro < minimo) return "pegado a la muralla";
            }
            else if (DentroDePoligono(q, TrazaMuralla)) return "dentro de la muralla";
            string zona = EnZonaLibre(p, q, e.Clase == Clase.Arbol ? radio : 0.6f);
            if (zona != null) return "zona libre: " + zona;
            if (EnLaObra(p, q, e.Clase == Clase.Arbol ? Mathf.Max(0.4f, radio * 0.24f) + 0.3f : 0.4f, e.Clase == Clase.Arbol ? radio * 0.7f : 0f))
                return "en un edificio o una torre";
            p.IndiceExistentes.Cerca(q, radio + 12f, p.Tmp);
            foreach (int i in p.Tmp)
            {
                Vector3 a = p.Existentes[i];
                if (Vector2.Distance(q, new Vector2(a.x, a.y)) < 0.7f * (radio + a.z)) return "junto a un árbol que ya estaba";
            }
            p.IndicePiezas.Cerca(q, radio + 8f, p.Tmp);
            foreach (int i in p.Tmp)
            {
                Plantada o = p.Piezas[i];
                if (o.Especie.Clase == Clase.Flor) continue;
                float minimo = o.Especie.Clase == Clase.Seto || e.Clase == Clase.Seto ? 0.5f * (radio + o.Ancho * 0.5f) : 0.6f * (radio + o.Ancho * 0.5f);
                if (Vector2.Distance(q, o.Pos) < minimo) return "junto a otra pieza de la vegetación";
            }
            return null;
        }

        private static Plantada Anotar(Plan p, Vector2 q, Especie e, float alto, string zona, string grupo, float rumbo, bool dominante = false,
            bool follajeDeBosque = false, bool enLaCiudad = false, float inclinar = 0f)
        {
            var pz = new Plantada
            {
                Pos = q, Especie = e, Alto = alto, Zona = zona, Grupo = grupo, Rumbo = rumbo, Dominante = dominante,
                FollajeDeBosque = follajeDeBosque, EnLaCiudad = enLaCiudad, Inclinar = inclinar,
            };
            p.IndicePiezas.Anadir(q, p.Piezas.Count);
            p.Piezas.Add(pz);
            if (e.Clase == Clase.Arbol && e.EsDeColor) p.Acentos.Add((q, e.Tono));
            return pz;
        }

        private static Especie Elegir((string, float)[] lista, Ruido.Dado dado)
        {
            float total = 0f;
            foreach (var (_, w) in lista) total += w;
            float r = dado.Siguiente() * total;
            foreach (var (n, w) in lista)
            {
                r -= w;
                if (r <= 0f) return Especies[n];
            }
            return Especies[lista[lista.Length - 1].Item1];
        }

        private static Receta ElegirReceta(Receta[] recetas, Ruido.Dado dado)
        {
            float total = 0f;
            foreach (Receta r in recetas) total += r.Peso;
            float x = dado.Siguiente() * total;
            foreach (Receta r in recetas)
            {
                x -= r.Peso;
                if (x <= 0f) return r;
            }
            return recetas[recetas.Length - 1];
        }

        /// Familia de color para la regla de los acentos (naranja y oro son el mismo otoño).
        private static Tono Familia(Tono t) => t == Tono.Naranja ? Tono.Oro : t;

        /// Dirección de la curva de nivel en el punto (perpendicular a la cuesta); en llano, una al azar.
        private static Vector2 EjeDeCurvaDeNivel(Lienzo l, Vector2 c, int semilla)
        {
            const float e = 2.54f;
            float gx = (l.Altura(c.x + e, c.y) - l.Altura(c.x - e, c.y)) / (2f * e);
            float gz = (l.Altura(c.x, c.y + e) - l.Altura(c.x, c.y - e)) / (2f * e);
            var g = new Vector2(gx, gz);
            if (g.magnitude > 0.06f) return new Vector2(-g.y, g.x).normalized;
            float a = Ruido.Hash(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), semilla) * Mathf.PI;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        private static Vector2 CuestaAbajo(Lienzo l, Vector2 c)
        {
            const float e = 2.54f;
            var g = new Vector2(l.Altura(c.x + e, c.y) - l.Altura(c.x - e, c.y), l.Altura(c.x, c.y + e) - l.Altura(c.x, c.y - e));
            return g.sqrMagnitude > 1e-6f ? -g.normalized : Vector2.down;
        }

        private static Vector2 Girar(Vector2 v, float radianes)
        {
            float c = Mathf.Cos(radianes), s = Mathf.Sin(radianes);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ── Bosquetes ────────────────────────────────────────────────────────────────────────

        /// Un bosquete de la mezcla «m» alrededor de «c»; «lado» es hacia dónde van sus matas (el camino o, si es
        /// null, cuesta abajo). Devuelve cuántos árboles ha planeado.
        private static int Bosquete(Plan p, Mezcla m, Vector2 c, Ruido.Dado dado, string grupo, Vector2? lado, Sitio sitio)
        {
            int n = Mathf.Min(m.TamMax, m.TamMin + (int)(dado.Siguiente() * (m.TamMax - m.TamMin + 1)));
            Receta receta = ElegirReceta(m.Recetas, dado);
            Especie dominante = Elegir(receta.Dominante, dado);
            float alto = dado.Entre(m.AltoMin, m.AltoMax);
            float diametro = dominante.Ancho(alto);
            Vector2 eje = EjeDeCurvaDeNivel(p.L, c, m.Semilla), normal = new Vector2(-eje.y, eje.x);

            // Al menos el 60 % de la especie dominante; el resto, compañeras; como mucho un acento.
            var especies = new Especie[n];
            int nDominante = Mathf.CeilToInt(0.6f * n);
            for (int i = 0; i < n; i++) especies[i] = i < nDominante ? dominante : Elegir(receta.Companeras ?? receta.Dominante, dado);
            if (receta.Acento != null && dado.Siguiente() < receta.ProbAcento)
            {
                Especie acento = Elegir(receta.Acento, dado);
                bool choca = false;
                foreach (var (pos, tono) in p.Acentos)
                    if (Familia(tono) != Familia(acento.Tono) && Vector2.Distance(pos, c) < 30f) { choca = true; break; }
                if (!choca && n > 1) especies[n - 1] = acento;
            }
            int iDominante = n / 3;
            var altos = new float[n];
            for (int i = 0; i < n; i++)
                altos[i] = Mathf.Min(alto * (i == iDominante ? 1.3f : dado.Entre(0.82f, 1.12f)), especies[i].AltoMaximo);

            // Cada miembro junto a uno ya situado, a 0,75–1,0 diámetros, casi siempre a lo largo del eje, todo dentro
            // de una elipse 2:1 y solo donde cabe (Libre): el bosquete crece por donde hay sitio. El primero es el
            // dominante, cerca del centro.
            float semiLargo = Mathf.Max(diametro * 0.7f * Mathf.Sqrt(n), diametro), semiAncho = semiLargo * 0.55f;
            var validos = new List<(int i, Vector2 q)>(n);
            var orden = new List<int>(n) { iDominante };
            for (int i = 0; i < n; i++) if (i != iDominante) orden.Add(i);
            foreach (int i in orden)
            {
                float di = especies[i].Ancho(altos[i]);
                string ultimo = null;
                for (int intento = 0; intento < 24; intento++)
                {
                    Vector2 q;
                    if (validos.Count == 0)
                        q = c + eje * (dado.Entre(-0.4f, 0.4f) * semiLargo) + normal * (dado.Entre(-0.4f, 0.4f) * semiAncho);
                    else
                    {
                        var (ib, qb) = validos[dado.Indice(validos.Count)];
                        float media = (di + especies[ib].Ancho(altos[ib])) * 0.5f;
                        float angulo = dado.Entre(-0.9f, 0.9f) + (dado.Siguiente() < 0.5f ? Mathf.PI : 0f);
                        q = qb + Girar(eje, angulo) * (media * dado.Entre(0.75f, 1f));
                    }
                    float a = Vector2.Dot(q - c, eje) / semiLargo, b = Vector2.Dot(q - c, normal) / semiAncho;
                    if (a * a + b * b > 1f) continue;
                    bool cerca = false;
                    foreach (var (j2, qj) in validos)
                        if (Vector2.Distance(q, qj) < 0.72f * (di + especies[j2].Ancho(altos[j2])) * 0.5f) { cerca = true; break; }
                    if (cerca) continue;
                    ultimo = Libre(p, q, especies[i], altos[i], m.PendienteMax, 3f, sitio);
                    if (ultimo != null) continue;
                    validos.Add((i, q));
                    ultimo = null;
                    break;
                }
                if (ultimo != null) p.Rechazar(ultimo);
            }

            // Si no caben al menos tres (dos en los de TamMin 2), no se pone: se leería como árboles sueltos.
            if (validos.Count < Mathf.Min(3, m.TamMin))
            {
                p.Rechazar("bosquete en el que no caben 3 árboles");
                return 0;
            }
            int puestos = 0;
            var troncos = new List<(Vector2 q, float radio)>();
            foreach (var (i, q) in validos)
            {
                Anotar(p, q, especies[i], altos[i], m.Nombre, grupo, dado.Entre(0f, 360f), i == iDominante, m.FollajeDeBosque, inclinar: dado.Entre(0f, 2.5f));
                troncos.Add((q, especies[i].Ancho(altos[i]) * 0.5f));
                puestos++;
            }

            // 1–3 matas en el lado del camino (o cuesta abajo), a 1–1,5 radios de copa de un tronco.
            if (puestos > 0 && m.Matas != null)
            {
                Vector2 hacia = lado ?? CuestaAbajo(p.L, c);
                int k = 1 + (int)(dado.Siguiente() * 3f);
                for (int j = 0; j < k; j++)
                {
                    var (qt, rt) = troncos[dado.Indice(troncos.Count)];
                    Vector2 q = qt + Girar(hacia, dado.Entre(-0.9f, 0.9f)) * (rt * dado.Entre(1f, 1.5f));
                    Especie e = Elegir(m.Matas, dado);
                    float a = dado.Entre(1.3f, 2.2f);
                    string motivo = Libre(p, q, e, a, 40f, 1.5f, sitio);
                    if (motivo != null) { p.Rechazar("mata: " + motivo); continue; }
                    Anotar(p, q, e, a, m.Nombre, grupo, dado.Entre(0f, 360f), follajeDeBosque: m.FollajeDeBosque);
                }
            }
            return puestos;
        }

        /// Arboleda dentro de un contorno: centros de bosquete al azar (determinista) a «Separacion» como mínimo, solo
        /// donde el ruido grande supera «Umbral» (así quedan prados abiertos entre arboledas).
        private static void PlanearArboleda(Plan p, Mezcla m)
        {
            Lienzo l = p.L;
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            foreach (Vector2 v in m.Contorno)
            {
                x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x);
                z0 = Mathf.Min(z0, v.y); z1 = Mathf.Max(z1, v.y);
            }
            var dado = new Ruido.Dado(m.Semilla);
            var centros = new List<Vector2>();
            int intentos = Mathf.CeilToInt((x1 - x0) * (z1 - z0) / (m.Separacion * m.Separacion) * 10f);
            Sitio sitio = m.FueraDeLaMuralla ? Sitio.FueraDeLaMuralla : Sitio.Campo;
            for (int t = 0; t < intentos; t++)
            {
                var c = new Vector2(dado.Entre(x0, x1), dado.Entre(z0, z1));
                if (!DentroDePoligono(c, m.Contorno)) continue;
                if (Ruido.Fbm(c.x, c.y, 80f, m.Semilla + 3) < m.Umbral) continue;
                bool cerca = false;
                foreach (Vector2 o in centros) if (Vector2.Distance(o, c) < m.Separacion) { cerca = true; break; }
                if (cerca) continue;
                if (l.Pendiente(c.x, c.y) > m.PendienteMax - 4f || l.Altura(c.x, c.y) < 1.5f || PesoDeCamino(l, c.x, c.y) > 0.2f) continue;
                if (Excluido(p, c) != null || EnZonaLibre(p, c, 4f) != null) continue;
                p.IndiceExistentes.Cerca(c, 10f, p.Tmp);
                int existentes = 0;
                foreach (int i in p.Tmp)
                    if (Vector2.Distance(c, new Vector2(p.Existentes[i].x, p.Existentes[i].y)) < 10f) existentes++;
                if (existentes >= m.MaxExistentes) continue;
                centros.Add(c);
                Bosquete(p, m, c, dado, $"Bosquete {centros.Count}", null, sitio);
            }
        }

        // ── Orlas de camino ──────────────────────────────────────────────────────────────────

        /// Punto y dirección a «s» metros a lo largo de la polilínea.
        private static (Vector2 punto, Vector2 dir) Recorrido(Vector2[] linea, float s)
        {
            for (int j = 0; j < linea.Length - 1; j++)
            {
                float largo = Vector2.Distance(linea[j], linea[j + 1]);
                if (largo < 1e-3f) continue;
                Vector2 dir = (linea[j + 1] - linea[j]) / largo;
                if (s <= largo || j == linea.Length - 2) return (linea[j] + dir * Mathf.Min(s, largo), dir);
                s -= largo;
            }
            return (linea[linea.Length - 1], Vector2.up);
        }

        private static float Largo(Vector2[] linea)
        {
            float l = 0f;
            for (int j = 0; j < linea.Length - 1; j++) l += Vector2.Distance(linea[j], linea[j + 1]);
            return l;
        }

        private static Vector2[] Tramo(Vector2[] linea, float desde, float hasta)
        {
            var r = new List<Vector2>();
            for (float s = desde; s < hasta; s += 4f) r.Add(Recorrido(linea, s).punto);
            r.Add(Recorrido(linea, hasta).punto);
            return r.ToArray();
        }

        /// Orla de un camino: alterna tramos con vistas (30–50 m) y tramos de arboleda (50–80 m). En los de
        /// arboleda, cada 20–28 m un bosquete de 3–4 a un lado, retirado 6–11 m del borde, y enfrente 2–4 matas a
        /// 1,5–3 m del borde; el lado cambia en cada grupo.
        private static void PlanearOrlaDeCamino(Plan p, Mezcla m)
        {
            Vector2[] linea = m.Linea;
            float largo = Largo(linea), fin = largo - m.Hasta;
            var dado = new Ruido.Dado(m.Semilla);
            float s = m.Desde;
            bool vista = dado.Siguiente() < 0.5f;
            float lado = dado.Siguiente() < 0.5f ? 1f : -1f;
            int grupos = 0;
            while (s < fin)
            {
                if (vista)
                {
                    float l = dado.Entre(30f, 50f);
                    if (l > 20f) p.Vistas.Add((Tramo(linea, s + 6f, Mathf.Min(s + l, fin) - 6f), SemianchoDeCamino + 15f));
                    s += l;
                }
                else
                {
                    float l = dado.Entre(50f, 80f);
                    for (float sg = s + dado.Entre(6f, 10f); sg < Mathf.Min(s + l, fin) - 4f; sg += dado.Entre(20f, 28f))
                    {
                        var (q, dir) = Recorrido(linea, sg);
                        Vector2 normal = new Vector2(-dir.y, dir.x) * lado;
                        Vector2 c = q + normal * (SemianchoDeCamino + dado.Entre(6f, 11f));
                        grupos++;
                        Bosquete(p, m, c, dado, $"Tramo {grupos}", -normal, Sitio.Campo);
                        int k = 2 + (int)(dado.Siguiente() * 3f);
                        float sm = sg + dado.Entre(-4f, 4f);
                        for (int j = 0; j < k; j++)
                        {
                            float ss = Mathf.Clamp(sm + (j - (k - 1) * 0.5f) * dado.Entre(1.8f, 2.4f), 0f, largo - 0.1f);
                            var (qm, dm) = Recorrido(linea, ss);
                            Vector2 enfrente = new Vector2(dm.y, -dm.x) * lado;
                            Vector2 pm = qm + enfrente * (SemianchoDeCamino + dado.Entre(1.5f, 3f));
                            Especie e = Elegir(m.Matas, dado);
                            float a = dado.Entre(1f, 1.9f);
                            string motivo = Libre(p, pm, e, a, 40f, 1.4f);
                            if (motivo != null) { p.Rechazar("mata de camino: " + motivo); continue; }
                            Anotar(p, pm, e, a, m.Nombre, $"Matas del tramo {grupos}", dado.Entre(0f, 360f), follajeDeBosque: m.FollajeDeBosque);
                        }
                        lado = -lado;
                    }
                    s += l;
                }
                vista = !vista;
            }
        }

        // ── Orlas de pueblo ──────────────────────────────────────────────────────────────────

        /// Bosquetes a lo largo de la costura de la alfombra del pueblo con el campo (de FueraMin a FueraMax metros
        /// del radio de cada disco), alargados a lo largo de ella, sin tapar accesos, huertos ni plazas; sus matas
        /// miran al pueblo.
        private static void PlanearOrlaDePueblo(Plan p, Mezcla m)
        {
            Pueblo pueblo = m.Pueblo();
            Lienzo l = p.L;
            var dado = new Ruido.Dado(m.Semilla);
            var centros = new List<Vector2>();
            foreach (Vector3 disco in pueblo.Alfombra)
            {
                var centro = new Vector2(disco.x, disco.y);
                float r0 = disco.z + (m.FueraMin + m.FueraMax) * 0.5f;
                int n = Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * r0 / m.Separacion));
                float a0 = dado.Entre(0f, 2f * Mathf.PI);
                for (int k = 0; k < n; k++)
                {
                    float a = a0 + k * 2f * Mathf.PI / n + dado.Entre(-0.15f, 0.15f);
                    float r = disco.z + dado.Entre(m.FueraMin, m.FueraMax);
                    Vector2 c = centro + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (c.y < m.SoloAlNorteDe) continue;
                    bool descartar = false;
                    foreach (Vector3 otro in pueblo.Alfombra)
                        if (Vector2.Distance(c, new Vector2(otro.x, otro.y)) < otro.z - 10f) descartar = true;
                    foreach (Vector2[] acceso in pueblo.Accesos)
                        if (DistanciaAPolilinea(c, acceso) < 9f) descartar = true;
                    for (int i = 0; i < pueblo.Calles.Length; i++)
                        if (DistanciaAPolilinea(c, pueblo.Calles[i]) < pueblo.AnchoDeCalle(i) * 0.5f + 6f) descartar = true;
                    foreach (Rect h in pueblo.Huertos)
                        if (DistanciaAlRectangulo(h, c) < 5f) descartar = true;
                    foreach (Plaza pl in pueblo.Plazas)
                        if (pl.Distancia(c) < 6f) descartar = true;
                    foreach (Vector2 o in centros)
                        if (Vector2.Distance(o, c) < m.Separacion * 0.8f) descartar = true;
                    if (descartar) continue;
                    if (l.Altura(c.x, c.y) < 1.5f || l.Pendiente(c.x, c.y) > 28f || PesoDeCamino(l, c.x, c.y) > 0.2f) continue;
                    if (Excluido(p, c) != null || EnZonaLibre(p, c, 3f) != null) continue;
                    centros.Add(c);
                    Bosquete(p, m, c, dado, $"Bosquete {centros.Count}", (centro - c).normalized, Sitio.Campo);
                }
            }
        }

        /// Ribera del tramo alto del arroyo de la ladera este: grupos de Tree04 turquesa de tronco curvo a un lado
        /// y otro del agua, cada 14–22 m, que repiten el color de los tejados del castillo bajo su hombro este.
        private static void PlanearRiberaDelArroyo(Plan p)
        {
            var m = new Mezcla
            {
                Nombre = "Ribera del arroyo de la ladera este", Semilla = 7611, TamMin = 2, TamMax = 4, AltoMin = 6.5f, AltoMax = 9f,
                Matas = L("Tree07_a01"), Flores = Flores.Ribera,
                Recetas = new[] { R(1f, L("Tree04_a02*2 Tree04_a03*2 Tree04_a01"), acento: L("Tree04_e01"), probAcento: 0.15f) },
            };
            Vector2[] linea = ArroyoDeLaLaderaEste;
            float largo = Largo(linea);
            var dado = new Ruido.Dado(m.Semilla);
            float lado = 1f;
            int n = 0;
            for (float s = 8f; s < largo - 6f; s += dado.Entre(14f, 22f))
            {
                var (q, dir) = Recorrido(linea, s);
                Vector2 normal = new Vector2(-dir.y, dir.x) * lado;
                Bosquete(p, m, q + normal * (4.5f + dado.Entre(4f, 8f)), dado, $"Orilla {++n}", -normal, Sitio.Campo);
                lado = -lado;
            }
        }

        /// Matas (Tree07, helechos Tree06_a01) en los taludes de 28–42° que se ven desde los caminos (a menos de
        /// 45 m), a manchas de 1–3 donde el ruido de 25 m lo pide.
        private static void PlanearMatasEnTaludes(Plan p)
        {
            Lienzo l = p.L;
            var dado = new Ruido.Dado(7621);
            (string, float)[] matas = L("Tree07_b01*3 Tree06_a01*1.2 Tree07_a01*0.5");
            for (float x = -440f; x < 440f; x += 5f)
                for (float z = -470f; z < 400f; z += 5f)
                {
                    float jx = x + (Ruido.Hash(Mathf.RoundToInt(x), Mathf.RoundToInt(z), 7622) - 0.5f) * 4f;
                    float jz = z + (Ruido.Hash(Mathf.RoundToInt(x), Mathf.RoundToInt(z), 7623) - 0.5f) * 4f;
                    float pendiente = l.Pendiente(jx, jz);
                    if (pendiente < 28f || pendiente > 42f) continue;
                    if (Ruido.Fbm(jx, jz, 25f, 7624) < 0.66f) continue;
                    float y = l.Altura(jx, jz);
                    if (y < 2f || y > CotaDeLaMontana) continue;
                    var c = new Vector2(jx, jz);
                    if (DentroDePoligono(c, TrazaMuralla)) continue;
                    bool visible = false;
                    foreach (Vector2[] camino in CaminosVisibles)
                        if (DistanciaAPolilinea(c, camino) < 45f) { visible = true; break; }
                    if (!visible) continue;
                    int k = 1 + (int)(Ruido.Hash(Mathf.RoundToInt(jx * 3f), Mathf.RoundToInt(jz * 3f), 7625) * 2.99f);
                    for (int j = 0; j < k; j++)
                    {
                        float a = dado.Entre(0f, 2f * Mathf.PI), r = j == 0 ? 0f : dado.Entre(1.2f, 2.2f);
                        Vector2 q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        Especie e = Elegir(matas, dado);
                        float alto = e.Nombre == "Tree06_a01" ? dado.Entre(1.6f, 2.6f) : dado.Entre(1.4f, 2.4f);
                        string motivo = Libre(p, q, e, alto, 42f, 1.5f);
                        if (motivo != null) { p.Rechazar("mata de talud: " + motivo); continue; }
                        Anotar(p, q, e, alto, "Matas en los taludes", "Talud", dado.Entre(0f, 360f));
                    }
                }
        }

        // ── El Reino ─────────────────────────────────────────────────────────────────────────

        /// Dentro de la muralla: jardines de palacio, jardín hundido de la taberna, paseo de ronda (árboles y setos
        /// al pie del muro), matas del talud central y el cerezo de la plazoleta. Fuera: alameda de cipreses del
        /// Camino 10, matas al pie del talud de la muralla y setos al pie de los frentes oeste, norte y este. El
        /// pinar y los hombros van en OrlaDelReino; la hiedra, al plantar (necesita la muralla puesta).
        private static void PlanearReino(Plan p)
        {
            var dado = new Ruido.Dado(7420);
            const string Palacio = "Jardines de palacio", Hundido = "Jardín hundido de la taberna", Ronda = "Paseo de ronda", Talud = "Talud central";

            // Jardines de palacio: cerezos grandes en los extremos de la hilera y medianos entre ellos.
            foreach (float s in new[] { -1f, 1f })
                for (int j = 0; j < CerezosDePalacioZ.Length; j++)
                {
                    bool extremo = j == 0 || j == CerezosDePalacioZ.Length - 1;
                    PlanearEnLaCiudad(p, new Vector2(s * CerezosDePalacioX, CerezosDePalacioZ[j]), Especies[extremo ? "Cerezo grande" : "Cerezo"], extremo ? 6.2f : 5.6f,
                        Palacio, s < 0f ? "Jardín de palacio de poniente" : "Jardín de palacio de levante", 32f, dado, flores: true);
                }
            PlanearEnLaCiudad(p, CerezoDeLaPlazoleta, Especies["Cerezo grande"], 6f, Palacio, "Plazoleta del pozo", 32f, dado, flores: true);

            // Jardín hundido: los árboles en lo más hondo de la hondonada (lo primero que se ve desde la plaza es
            // su copa), cerezos y un turquesa de tronco curvo; matas rosas y turquesas por las laderas.
            var hondo = new List<(float y, Vector2 q)>();
            for (float x = JardinHundido.xMin + 1f; x < JardinHundido.xMax; x += 1f)
                for (float z = JardinHundido.yMin + 1f; z < JardinHundido.yMax; z += 1f)
                    if (p.L.Pendiente(x, z) <= 30f) hondo.Add((p.L.Altura(x, z), new Vector2(x, z)));
            hondo.Sort((a, b) => a.y.CompareTo(b.y));
            string[] delHoyo = { "Cerezo grande", "Tree04_a02", "Cerezo", "Cerezo de copa alta" };
            int puestos = 0;
            foreach (var (_, q) in hondo)
            {
                if (puestos >= delHoyo.Length) break;
                if (PlanearEnLaCiudad(p, q, Especies[delHoyo[puestos]], puestos == 0 ? 5.8f : 5f, Hundido, Hundido, 30f, dado, flores: true, anotarRechazo: false)) puestos++;
            }
            int matas = 0;
            for (float x = JardinHundido.xMin + 1f; x < JardinHundido.xMax && matas < 8; x += 2.5f)
                for (float z = JardinHundido.yMin + 1f; z < JardinHundido.yMax && matas < 8; z += 2.5f)
                {
                    float pend = p.L.Pendiente(x, z);
                    if (pend < 28f || pend > 42f || Ruido.Hash(Mathf.RoundToInt(x * 3f), Mathf.RoundToInt(z * 3f), 71) > 0.45f) continue;
                    if (PlanearEnLaCiudad(p, new Vector2(x, z), Especies[matas % 2 == 0 ? "Tree07_c01" : "Tree07_a01"], dado.Entre(1.4f, 2f), Hundido, Hundido, 42f, dado, anotarRechazo: false)) matas++;
                }

            // Paseo de ronda: al pie interior de la muralla, setos de 1,5 a 3,3 m de la cara que cubren algo más de
            // la mitad del muro, con cortes, y delante, cada 14–18 m, un árbol (cerezo y turquesa alternos).
            Vector2[] interior = Desplazar(MurallaDelReino.TrazaMedia(), 1.72f);
            Vector2[] ronda = PoligonoCerrado(interior);
            float largo = Largo(ronda);
            string[] arbolesDeRonda = { "Cerezo", "Tree04_a01", "Cerezo de copa alta", "Tree04_a03", "Cerezo grande", "Tree04_a01" };
            int k = 0;
            for (float s = 4f; s < largo; s += dado.Entre(14f, 18f))
            {
                var (q, dir) = Recorrido(ronda, s);
                if (EnJardinDePalacio(q)) continue;
                Especie e = Especies[arbolesDeRonda[k % arbolesDeRonda.Length]];
                float alto = e.Tono == Tono.Cerezo ? 5.6f : 5f;
                Vector2 dentro = new Vector2(-dir.y, dir.x);
                PlanearEnLaCiudad(p, q + dentro * (4.2f + e.Ancho(alto) * 0.3f), e, alto, Ronda, Ronda, 32f, dado);
                k++;
            }
            for (float s = 2f; s < largo; s += 6.4f + dado.Entre(1.5f, 7f))
            {
                var (q, dir) = Recorrido(ronda, s + 3.2f);
                if (dado.Siguiente() >= 0.62f) continue;
                Vector2 dentro = new Vector2(-dir.y, dir.x);
                PlanearSeto(p, q + dentro * 2.4f, dir, Especies[dado.Siguiente() < 0.3f ? "Plant03_a02" : "Plant03_a03"], "Setos al pie de la muralla", Sitio.Ciudad);
            }

            // Talud central: matas rosas, turquesas y verdes en lo que tiene 30–42°.
            for (float x = -95f; x < 105f; x += 3f)
                for (float z = 255f; z < 300f; z += 3f)
                {
                    var q = new Vector2(x, z);
                    float pend = p.L.Pendiente(x, z);
                    if (pend < 30f || pend > 42f || !DentroDePoligono(q, TrazaMuralla)) continue;
                    if (Ruido.Hash(Mathf.RoundToInt(x * 7f), Mathf.RoundToInt(z * 7f), 73) >= 0.33f) continue;
                    string nombre = new[] { "Tree07_c01", "Tree07_a01", "Tree07_b01" }[Mathf.Min(2, (int)(Ruido.Hash(Mathf.RoundToInt(x), Mathf.RoundToInt(z), 74) * 3f))];
                    PlanearEnLaCiudad(p, q, Especies[nombre], dado.Entre(1.3f, 2.2f), Talud, Talud, 42f, dado, anotarRechazo: false);
                }

            PlanearAlamedaDelCamino10(p, dado);
            PlanearSetosExteriores(p, dado);
        }

        private static bool EnJardinDePalacio(Vector2 q) => q.y > 316f && q.y < 350f && Mathf.Abs(q.x) > 76f;

        /// Pieza de la ciudad: comprueba contra el plan y contra las calles y plazas del Reino (las sendas de
        /// puerta se miran al plantar). Si «flores», le pone alrededor tres matas de flores del pack.
        private static bool PlanearEnLaCiudad(Plan p, Vector2 q, Especie e, float alto, string zona, string grupo, float pendienteMax, Ruido.Dado dado,
            bool flores = false, bool anotarRechazo = true)
        {
            float radioTronco = Mathf.Max(0.4f, e.Ancho(alto) * 0.12f);
            Pueblo reino = p.Reino;
            string motivo = null;
            for (int i = 0; i < reino.Calles.Length && motivo == null; i++)
                if (DistanciaAPolilinea(q, reino.Calles[i]) < reino.AnchoDeCalle(i) * 0.5f + 0.6f + radioTronco) motivo = "en una calle";
            foreach (Plaza pl in reino.Plazas)
                if (motivo == null && pl.Distancia(q) < radioTronco + 0.3f) motivo = "en una plaza";
            motivo ??= Libre(p, q, e, alto, pendienteMax, 0f, Sitio.Ciudad);
            if (motivo != null)
            {
                if (anotarRechazo) p.Rechazar(zona + ": " + motivo);
                return false;
            }
            Anotar(p, q, e, alto, zona, grupo, dado.Entre(0f, 360f), enLaCiudad: true, inclinar: dado.Entre(0f, 2f));
            if (flores)
            {
                string[] colores = { "Flower04_a03", "Flower05_a01", "Flower01_a01", "Flower01_c01", "Flower02_b01" };
                for (int j = 0; j < 3; j++)
                {
                    float a = dado.Entre(0f, 2f * Mathf.PI), r = dado.Entre(1.1f, 1.9f);
                    Anotar(p, q + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, Especies[colores[dado.Indice(colores.Length)]], dado.Entre(0.9f, 1.15f),
                        zona, grupo, dado.Entre(0f, 360f), enLaCiudad: true);
                }
            }
            return true;
        }

        private static void PlanearSeto(Plan p, Vector2 q, Vector2 dir, Especie e, string zona, Sitio sitio)
        {
            string motivo = Libre(p, q, e, 1.3f, 25f, sitio == Sitio.Ciudad ? 0f : 1.5f, sitio);
            if (motivo != null) { p.Rechazar(zona + ": " + motivo); return; }
            Anotar(p, q, e, 1.3f, zona, zona, Rumbo(dir), enLaCiudad: sitio == Sitio.Ciudad);
        }

        /// Cipreses (Tree02_c02 de 9–10,5 m) cada 15–17 m al sur del Camino 10, a 10 m del eje, salvo en las ventanas
        /// de vista; entre ciprés y ciprés, 2–3 matas a 7–8 m del eje. Al otro lado, entre el camino y el pie del
        /// talud de la muralla, grupos de 2–3 matas cada 10–16 m.
        private static void PlanearAlamedaDelCamino10(Plan p, Ruido.Dado dado)
        {
            const string Alameda = "Alameda de cipreses del Camino 10", Pie = "Pie del talud de la muralla";
            Vector2[] linea = CaminoBajoLaMuralla;
            float largo = Largo(linea);
            Especie cipres = Especies["Tree02_c02"], mata = Especies["Tree07_b01"];
            int n = 0;
            for (float s = 6f; s < largo - 6f; s += dado.Entre(15f, 17f))
            {
                var (q, dir) = Recorrido(linea, s);
                bool enVentana = false;
                foreach (Vector2 v in VentanasDelCamino10) if (q.x >= v.x && q.x <= v.y) enVentana = true;
                if (enVentana) continue;
                Vector2 sur = new Vector2(dir.y, -dir.x);
                float alto = dado.Entre(9f, 10.5f);
                Vector2 c = q + sur * 10f;
                string motivo = Libre(p, c, cipres, alto, 32f, 3f);
                if (motivo == null) Anotar(p, c, cipres, alto, Alameda, $"Ciprés {++n}", dado.Entre(0f, 360f));
                else p.Rechazar("ciprés: " + motivo);
                int k = 2 + (int)(dado.Siguiente() * 2f);
                for (int j = 0; j < k; j++)
                {
                    var (qm, dm) = Recorrido(linea, Mathf.Min(s + 5f + j * 2.6f, largo - 1f));
                    Vector2 pm = qm + new Vector2(dm.y, -dm.x) * dado.Entre(7f, 8.2f);
                    float a = dado.Entre(1.3f, 1.9f);
                    motivo = Libre(p, pm, mata, a, 40f, 1.4f);
                    if (motivo == null) Anotar(p, pm, mata, a, Alameda, "Matas de la alameda", dado.Entre(0f, 360f));
                    else p.Rechazar("mata de la alameda: " + motivo);
                }
            }
            // Los cipreses se leen como alameda solo si no se les mezclan los bosquetes de la ladera.
            p.Reservas.Add((linea, 17f, "franja de la alameda del Camino 10"));
            int g = 0;
            for (float s = 4f; s < largo - 4f; s += dado.Entre(10f, 16f))
            {
                var (q, dir) = Recorrido(linea, s);
                Vector2 norte = new Vector2(-dir.y, dir.x);
                int k = 2 + (int)(dado.Siguiente() * 2f);
                g++;
                for (int j = 0; j < k; j++)
                {
                    Vector2 pm = q + dir * (j * 1.8f) + norte * dado.Entre(6.6f, 8f);
                    Especie e = Especies[j % 2 == 0 ? "Tree07_b01" : "Tree07_a01"];
                    float a = dado.Entre(1.2f, 1.8f);
                    string motivo = Libre(p, pm, e, a, 40f, 1.2f);
                    if (motivo == null) Anotar(p, pm, e, a, Pie, $"Grupo {g}", dado.Entre(0f, 360f));
                    else p.Rechazar("mata al pie del talud: " + motivo);
                }
            }
        }

        /// Setos recortados a 4,2 m de la cara exterior de los frentes oeste, norte y este (el sur es la escarpa),
        /// en tramos de 6 m con cortes, donde el suelo está a menos de 2 m de la cota del pie del muro.
        private static void PlanearSetosExteriores(Plan p, Ruido.Dado dado)
        {
            Vector2[] contorno = PoligonoCerrado(TrazaMuralla);
            float largo = Largo(contorno);
            for (float s = 2f; s < largo; s += 6.4f + dado.Entre(2f, 8f))
            {
                var (q, dir) = Recorrido(contorno, s + 3.2f);
                if (q.y <= 246f && q.x > -95f) continue;
                if (dado.Siguiente() >= 0.55f) continue;
                Vector2 fuera = new Vector2(dir.y, -dir.x);
                Vector2 c = q + fuera * 4.2f;
                if (Mathf.Abs(p.L.Altura(c.x, c.y) - p.L.Altura(q.x, q.y)) > 2f) { p.Rechazar("seto exterior: desnivel con el pie del muro"); continue; }
                PlanearSeto(p, c, dir, Especies[dado.Siguiente() < 0.5f ? "Plant01_a04" : "Plant02_a04"], "Setos al pie exterior de la muralla", Sitio.FueraDeLaMuralla);
            }
        }

        /// El polígono (antihorario) desplazado «d» metros hacia dentro, por la bisectriz de cada vértice.
        private static Vector2[] Desplazar(Vector2[] poligono, float d)
        {
            int n = poligono.Length;
            var r = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 c = poligono[i];
                Vector2 d1 = (c - poligono[(i + n - 1) % n]).normalized, d2 = (poligono[(i + 1) % n] - c).normalized;
                Vector2 n1 = new Vector2(-d1.y, d1.x), n2 = new Vector2(-d2.y, d2.x), b = n1 + n2;
                if (b.sqrMagnitude < 1e-8f) { r[i] = c + n1 * d; continue; }
                b.Normalize();
                r[i] = c + b * (d / Mathf.Max(Vector2.Dot(b, n1), 0.2f));
            }
            return r;
        }

        // ── Manchas de flores ────────────────────────────────────────────────────────────────

        /// Manchas de flores del plan: en los prados que se ven desde los caminos (una cada ~30 m donde el ruido lo
        /// pide), junto a las matas de los caminos, en los jardines del Reino y al pie interior de su muralla.
        private static void PlanearManchasDeFlores(Plan p)
        {
            Lienzo l = p.L;
            var dado = new Ruido.Dado(7701);
            int semilla = 7702;
            var centros = new List<Vector2>();
            // Prados
            for (int t = 0; t < 12000; t++)
            {
                var c = new Vector2(dado.Entre(-450f, 450f), dado.Entre(-520f, 420f));
                if (Ruido.Fbm(c.x, c.y, 70f, 7703) < 0.45f) continue;
                bool cerca = false;
                foreach (Vector2 o in centros) if (Vector2.Distance(o, c) < 30f) { cerca = true; break; }
                if (cerca || !EsPrado(l, c) || l.Pendiente(c.x, c.y) > 20f) continue;
                if (FueraDeLasFlores(p, c) || MarjalReservado.Contains(c)) continue;
                bool visible = false;
                foreach (Vector2[] camino in CaminosVisibles)
                    if (DistanciaAPolilinea(c, camino) < 90f) { visible = true; break; }
                if (!visible) continue;
                centros.Add(c);
                p.Manchas.Add(new Mancha
                {
                    Centro = c, Radio = dado.Entre(6f, 15f), Alargamiento = dado.Entre(1f, 1.8f), Giro = dado.Entre(0f, 180f),
                    Paleta = PaletaDeFlores(p, c), Semilla = semilla++,
                });
            }
            // Junto a las matas de los caminos y de los bosquetes (lado del camino): una mancha pequeña por grupo.
            var vistos = new HashSet<string>();
            foreach (Plantada pz in p.Piezas)
            {
                if (pz.Especie.Clase != Clase.Mata || pz.EnLaCiudad || !vistos.Add(pz.Zona + "|" + pz.Grupo)) continue;
                if (FueraDeLasFlores(p, pz.Pos)) continue;
                p.Manchas.Add(new Mancha { Centro = pz.Pos, Radio = dado.Entre(2.5f, 4f), Alargamiento = 1.4f, Giro = dado.Entre(0f, 180f), Paleta = PaletaDeFlores(p, pz.Pos), Semilla = semilla++ });
            }
            // Reino: jardines de palacio, jardín hundido y pie interior de la muralla.
            foreach (float s in new[] { -1f, 1f })
                p.Manchas.Add(new Mancha { Centro = new Vector2(s * 92f, 333f), Radio = 13f, Alargamiento = 1.6f, Giro = 0f, Paleta = Flores.Reino, Semilla = semilla++ });
            p.Manchas.Add(new Mancha { Centro = JardinHundido.center, Radio = 10f, Alargamiento = 1.6f, Giro = 90f, Paleta = Flores.Reino, Semilla = semilla++ });
            Vector2[] ronda = PoligonoCerrado(Desplazar(MurallaDelReino.TrazaMedia(), 1.72f));
            float largo = Largo(ronda);
            for (float s = 10f; s < largo; s += dado.Entre(18f, 26f))
            {
                var (q, dir) = Recorrido(ronda, s);
                if (EnJardinDePalacio(q)) continue;
                p.Manchas.Add(new Mancha { Centro = q + new Vector2(-dir.y, dir.x) * 3f, Radio = dado.Entre(3.5f, 5.5f), Alargamiento = 2f, Giro = Rumbo(dir), Paleta = Flores.Reino, Semilla = semilla++ });
            }
        }

        private static bool EsPrado(Lienzo l, Vector2 c) =>
            l.Peso(c.x, c.y, l.Capa(CapaHierbaDelCampo)) + l.Peso(c.x, c.y, l.Capa("Capa6")) + l.Peso(c.x, c.y, l.Capa(CapaHierba)) +
            l.Peso(c.x, c.y, l.Capa(CapaFlores)) + l.Peso(c.x, c.y, l.Capa("SueloUrbano4")) >= 0.45f;

        /// Donde no se pintan flores: el pueblo de Will (referencia de estilo), el Bosque Prohibido y el agua.
        private static bool FueraDeLasFlores(Plan p, Vector2 c)
        {
            if (BosqueProhibido.Contains(c)) return true;
            if (Vector2.Distance(c, new Vector2(-0.6f, -130.3f)) < 72f) return true;
            Vector4 e = RiberaDeLaLaguna;
            float ex = (c.x - e.x) / e.z, ez = (c.y - e.y) / e.w;
            return ex * ex + ez * ez < 1f || p.L.Altura(c.x, c.y) > CotaDeLaMontana;
        }

        /// Paleta de flores del sitio: la de la arboleda o pueblo en que cae, la del Reino a menos de 60 m de su
        /// muralla y la del campo en el resto.
        private static Flores PaletaDeFlores(Plan p, Vector2 c)
        {
            if (Vector2.Distance(c, new Vector2(0f, 300f)) < 160f && (DentroDePoligono(c, TrazaMuralla) || c.y > 236f)) return Flores.Reino;
            foreach (Mezcla m in p.Arboledas)
                if (m.Contorno != null && DentroDePoligono(c, m.Contorno)) return m.Flores;
            if (Vector2.Distance(c, new Vector2(242f, 150f)) < 80f) return Flores.Cosecha;
            if (c.y < -360f && c.x > 180f) return Flores.Ribera;
            return Flores.Campo;
        }

        /// Ramos de cada paleta: texturas de flor del terreno (Flower01–14_D) con su peso.
        private static (string[] capas, float[] pesos, float peso)[] Ramos(Flores paleta)
        {
            var amarillas = (new[] { "Flower02_D", "Flower09_D", "Flower14_D", "Flower13_D" }, new[] { 2f, 1.5f, 1f, 1.5f });
            var moradas = (new[] { "Flower11_D", "Flower08_D", "Flower13_D" }, new[] { 2f, 1f, 1.5f });
            var azules = (new[] { "Flower07_D", "Flower03_D", "Flower13_D" }, new[] { 2f, 1f, 1.5f });
            var rosas = (new[] { "Flower01_D", "Flower13_D" }, new[] { 2.5f, 1.5f });
            var naranjas = (new[] { "Flower10_D", "Flower14_D", "Flower02_D" }, new[] { 2f, 1f, 1f });
            var rojas = (new[] { "Flower06_D", "Flower02_D" }, new[] { 2f, 1.5f });
            switch (paleta)
            {
                case Flores.Otono: return new[] { (naranjas.Item1, naranjas.Item2, 0.45f), (amarillas.Item1, amarillas.Item2, 0.45f), (rojas.Item1, rojas.Item2, 0.1f) };
                case Flores.Cosecha: return new[] { (amarillas.Item1, amarillas.Item2, 0.7f), (naranjas.Item1, naranjas.Item2, 0.15f), (azules.Item1, azules.Item2, 0.15f) };
                case Flores.Reino: return new[] { (moradas.Item1, moradas.Item2, 0.4f), (rosas.Item1, rosas.Item2, 0.35f), (azules.Item1, azules.Item2, 0.15f), (amarillas.Item1, amarillas.Item2, 0.1f) };
                case Flores.Ribera: return new[] { (azules.Item1, azules.Item2, 0.7f), (amarillas.Item1, amarillas.Item2, 0.3f) };
                default: return new[] { (amarillas.Item1, amarillas.Item2, 0.35f), (moradas.Item1, moradas.Item2, 0.2f), (azules.Item1, azules.Item2, 0.2f), (rosas.Item1, rosas.Item2, 0.15f), (rojas.Item1, rojas.Item2, 0.1f) };
            }
        }

        // ── Pintar las flores (hierba de detalle) ────────────────────────────────────────────

        private static readonly Collider[] BufferDeFlores = new Collider[128];

        /// Pinta las manchas de flores del plan en las capas de hierba de detalle Flower*_D del terreno, solo sobre
        /// hierba (nunca en caminos, calles, suelo duro, arena ni roca), lejos de edificios y respetando lo retocado
        /// a mano. Va en la copia del suelo: «Quitar» la deshace.
        public static void PintarFlores(Lienzo l, List<string> informe)
        {
            TerrainData datos = l.Datos;
            int dres = datos.detailResolution;
            DetailPrototype[] prototipos = datos.detailPrototypes;
            if (dres <= 0 || prototipos.Length == 0) { informe.Add("Flores: el terreno no tiene hierba de detalle; no se pintan."); return; }
            var capaDe = new Dictionary<string, int>();
            for (int i = 0; i < prototipos.Length; i++)
            {
                Texture2D t = prototipos[i].prototypeTexture;
                if (t != null && t.name.StartsWith("Flower") && !capaDe.ContainsKey(t.name)) capaDe[t.name] = i;
            }
            if (capaDe.Count == 0) { informe.Add("Flores: el terreno no tiene capas de flores (Flower*_D); no se pintan."); return; }

            Plan plan = Planear(l);
            // La densidad sigue la escala de la hierba que ya hay (cuenta de instancias o cobertura): el máximo de
            // todas las capas. Se leen de una en una para no tener todas a la vez en memoria.
            int maximo = 0;
            for (int c = 0; c < prototipos.Length; c++)
            {
                int[,] capa = datos.GetDetailLayer(0, 0, dres, dres, c);
                foreach (int v in capa) if (v > maximo) maximo = v;
            }
            if (maximo <= 0) maximo = 8;
            // Lo que se pinta, por capa: (fila, columna, valor); se aplica al final, capa a capa.
            var escrituras = new Dictionary<int, Dictionary<long, int>>();

            Vector3 origen = l.Terreno.transform.position, tamano = datos.size;
            float celda = tamano.x / dres;
            var tocadas = new HashSet<int>();
            int manchas = 0, celdas = 0, sinCapas = 0;
            var grandes = new List<Bounds>();
            foreach (Mancha m in plan.Manchas)
            {
                var ramos = Ramos(m.Paleta);
                // Los ramos sin ninguna capa en el terreno no cuentan.
                float total = 0f;
                foreach (var r in ramos) foreach (string n in r.capas) if (capaDe.ContainsKey(n)) { total += r.peso; break; }
                if (total <= 0f) { sinCapas++; continue; }
                var dado = new Ruido.Dado(m.Semilla);
                float x = dado.Siguiente() * total;
                var ramo = ramos[0];
                foreach (var r in ramos)
                {
                    bool hay = false;
                    foreach (string n in r.capas) if (capaDe.ContainsKey(n)) hay = true;
                    if (!hay) continue;
                    ramo = r;
                    x -= r.peso;
                    if (x <= 0f) break;
                }

                // Edificios, muros y piezas grandes que ya están: no se pinta debajo.
                float ext = m.Radio * m.Alargamiento + 1f;
                float y = l.Altura(m.Centro.x, m.Centro.y);
                grandes.Clear();
                int nc = Physics.OverlapBoxNonAlloc(new Vector3(m.Centro.x, y + 3f, m.Centro.y), new Vector3(ext, 5f, ext), BufferDeFlores, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < nc; i++)
                {
                    Collider co = BufferDeFlores[i];
                    if (co == null || co is TerrainCollider) continue;
                    Vector3 s = co.bounds.size;
                    if ((s.x > 4f || s.z > 4f) && s.x < 60f && s.z < 60f) grandes.Add(co.bounds);
                }

                float giro = m.Giro * Mathf.Deg2Rad, cs = Mathf.Cos(giro), sn = Mathf.Sin(giro);
                int i0 = Mathf.Max(0, Mathf.FloorToInt((m.Centro.x - ext - origen.x) / celda)), i1 = Mathf.Min(dres - 1, Mathf.CeilToInt((m.Centro.x + ext - origen.x) / celda));
                int k0 = Mathf.Max(0, Mathf.FloorToInt((m.Centro.y - ext - origen.z) / celda)), k1 = Mathf.Min(dres - 1, Mathf.CeilToInt((m.Centro.y + ext - origen.z) / celda));
                bool pintada = false;
                for (int k = k0; k <= k1; k++)
                    for (int i = i0; i <= i1; i++)
                    {
                        float wx = origen.x + (i + 0.5f) * celda, wz = origen.z + (k + 0.5f) * celda;
                        float dx = wx - m.Centro.x, dz = wz - m.Centro.y;
                        float a = (dx * sn + dz * cs) / m.Alargamiento, b = dx * cs - dz * sn;
                        float d = Mathf.Sqrt(a * a + b * b) + (Ruido.Fbm(wx, wz, 6f, m.Semilla) - 0.5f) * m.Radio * 0.6f;
                        if (d > m.Radio) continue;
                        if (l.DetalleBloqueado != null && l.DetalleBloqueado[k * dres + i]) continue;
                        if (l.Duro[l.K(wz), l.I(wx)]) continue;
                        var q = new Vector2(wx, wz);
                        if (!EsPrado(l, q) || PesoDeCamino(l, wx, wz) > 0.25f || l.Pendiente(wx, wz) > 30f || l.Altura(wx, wz) < 1.5f) continue;
                        if (l.Peso(wx, wz, l.Capa(CapaArena)) > 0.4f || l.Peso(wx, wz, l.Capa(CapaRoca)) > 0.3f) continue;
                        bool bajoEdificio = false;
                        foreach (Bounds bo in grandes)
                            if (wx > bo.min.x - 0.5f && wx < bo.max.x + 0.5f && wz > bo.min.z - 0.5f && wz < bo.max.z + 0.5f) { bajoEdificio = true; break; }
                        if (bajoEdificio) continue;
                        // Más densa en el centro; cada celda, una flor del ramo.
                        float fuerza = 1f - Ruido.Suave(m.Radio * 0.45f, m.Radio, d);
                        if (Ruido.Hash(i, k, m.Semilla) > 0.35f + 0.6f * fuerza) continue;
                        float h = Ruido.Hash(i, k, m.Semilla + 1) * Suma(ramo.pesos);
                        int capa = -1;
                        for (int j = 0; j < ramo.capas.Length; j++)
                        {
                            h -= ramo.pesos[j];
                            if (h <= 0f || j == ramo.capas.Length - 1)
                            {
                                if (capaDe.TryGetValue(ramo.capas[j], out int c)) capa = c;
                                break;
                            }
                        }
                        if (capa < 0) continue;
                        int valor = Mathf.Clamp(Mathf.RoundToInt(maximo * (0.2f + 0.3f * fuerza)), 1, maximo);
                        if (!escrituras.TryGetValue(capa, out Dictionary<long, int> enCapa)) escrituras[capa] = enCapa = new Dictionary<long, int>();
                        long clave = (long)k * dres + i;
                        if (enCapa.TryGetValue(clave, out int previo) && previo >= valor) continue;
                        enCapa[clave] = valor;
                        pintada = true;
                    }
                if (pintada) manchas++;
            }
            foreach (KeyValuePair<int, Dictionary<long, int>> e in escrituras)
            {
                int[,] arr = datos.GetDetailLayer(0, 0, dres, dres, e.Key);
                bool cambiada = false;
                foreach (KeyValuePair<long, int> w in e.Value)
                {
                    int k = (int)(w.Key / dres), i = (int)(w.Key % dres);
                    if (arr[k, i] >= w.Value) continue;
                    arr[k, i] = w.Value;
                    celdas++;
                    cambiada = true;
                }
                if (!cambiada) continue;
                datos.SetDetailLayer(0, 0, e.Key, arr);
                tocadas.Add(e.Key);
            }
            informe.Add($"Flores: {manchas} manchas pintadas como hierba de detalle ({celdas} celdas en {tocadas.Count} capas Flower*_D; " +
                        $"densidad hasta {maximo}, la de la hierba que ya había)." + (sinCapas > 0 ? $" {sinCapas} manchas sin capa de su color en el terreno." : ""));
        }

        private static float Suma(float[] v)
        {
            float s = 0f;
            foreach (float x in v) s += x;
            return s;
        }

        // ── Materiales ───────────────────────────────────────────────────────────────────────

        private sealed class Materiales
        {
            public Material Campo, Bosque, Cerezo;
        }

        private static Materiales CargarMateriales(List<string> informe)
        {
            var m = new Materiales
            {
                Campo = AssetDatabase.LoadAssetAtPath<Material>(RutaFollajeDeCampo),
                Bosque = AssetDatabase.LoadAssetAtPath<Material>(RutaFollajeDeBosque),
            };
            if (m.Campo == null) informe.Add($"  ! falta {RutaFollajeDeCampo}: los árboles verdes se quedan con Tree.mat.");
            if (m.Bosque == null) m.Bosque = m.Campo;
            m.Cerezo = MaterialDeCerezo(informe);
            return m;
        }

        /// Tree_Cerezo.mat: copia de Tree.mat con la paleta Tree_Cerezo_D.png. Se crea la primera vez y se
        /// reutiliza; null si no se ha podido crear (entonces los cerezos se plantan como turquesas).
        private static Material MaterialDeCerezo(List<string> informe)
        {
            string rutaMaterial = CarpetaRecursos + "/" + ArchivoMaterialDeCerezo;
            var material = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
            if (material != null) return material;
            Texture2D paleta = PaletaDeCerezo(informe);
            if (paleta == null) return null;
            AsegurarCarpeta(CarpetaRecursos);
            if (!AssetDatabase.CopyAsset(RutaMaterialDeArbol, rutaMaterial))
            {
                informe.Add($"  ! no se ha podido copiar {RutaMaterialDeArbol}: los cerezos se plantan como árboles turquesa.");
                return null;
            }
            material = AssetDatabase.LoadAssetAtPath<Material>(rutaMaterial);
            if (material == null) return null;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", paleta);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", paleta);
            // El tinte de serie es verdoso (0,42; 0,54; 0,40) y apagaría el rosa: gris neutro de la misma luminancia.
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.52f, 0.5f, 0.5f, 1f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            informe.Add($"Cerezo: creados {CarpetaRecursos}/{ArchivoPaletaDeCerezo} y {ArchivoMaterialDeCerezo} (se reutilizan en las próximas ejecuciones).");
            return material;
        }

        /// Tree_Cerezo_D.png: la paleta de los árboles del pack con los verdes (tono 70–170°) pasados a rosa pálido
        /// (tono 336–348°, la mitad de saturación y más claros); troncos y demás colores, iguales. Se lee de una
        /// copia temporal de Tree_D.tga que sí es legible (la textura del pack no se toca) y la copia se borra.
        private static Texture2D PaletaDeCerezo(List<string> informe)
        {
            string rutaPaleta = CarpetaRecursos + "/" + ArchivoPaletaDeCerezo;
            var existente = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaPaleta);
            if (existente != null) return existente;
            var original = AssetImporter.GetAtPath(RutaPaletaDeArbol) as TextureImporter;
            if (original == null) { informe.Add($"  ! falta {RutaPaletaDeArbol}: los cerezos se plantan como árboles turquesa."); return null; }

            AsegurarCarpeta(CarpetaRecursos);
            string rutaTemporal = CarpetaRecursos + "/" + ArchivoPaletaTemporal;
            if (!AssetDatabase.CopyAsset(RutaPaletaDeArbol, rutaTemporal)) { informe.Add("  ! no se ha podido copiar Tree_D.tga: sin cerezos."); return null; }
            try
            {
                var importador = (TextureImporter)AssetImporter.GetAtPath(rutaTemporal);
                importador.isReadable = true;
                importador.textureCompression = TextureImporterCompression.Uncompressed;
                importador.mipmapEnabled = false;
                importador.maxTextureSize = 8192;
                importador.SaveAndReimport();
                var fuente = AssetDatabase.LoadAssetAtPath<Texture2D>(rutaTemporal);
                if (fuente == null || fuente.width < 16) { informe.Add("  ! Tree_D.tga no se ha podido leer (¿falta el archivo de Git LFS?): sin cerezos."); return null; }

                Color32[] pixeles = fuente.GetPixels32();
                for (int i = 0; i < pixeles.Length; i++) pixeles[i] = ColorDeCerezo(pixeles[i]);
                var nueva = new Texture2D(fuente.width, fuente.height, TextureFormat.RGBA32, false);
                nueva.SetPixels32(pixeles);
                nueva.Apply();
                byte[] png = ImageConversion.EncodeToPNG(nueva);
                Object.DestroyImmediate(nueva);
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", rutaPaleta), png);
            }
            finally
            {
                AssetDatabase.DeleteAsset(rutaTemporal);
            }
            AssetDatabase.ImportAsset(rutaPaleta, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(rutaPaleta) is TextureImporter importadorPng)
            {
                // Mismos ajustes de importación que la paleta del pack.
                importadorPng.sRGBTexture = original.sRGBTexture;
                importadorPng.mipmapEnabled = original.mipmapEnabled;
                importadorPng.maxTextureSize = original.maxTextureSize;
                importadorPng.textureCompression = original.textureCompression;
                importadorPng.filterMode = original.filterMode;
                importadorPng.wrapMode = original.wrapMode;
                importadorPng.isReadable = false;
                importadorPng.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(rutaPaleta);
        }

        private static Color32 ColorDeCerezo(Color32 c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (h < 70f / 360f || h > 170f / 360f || s <= 0.12f) return c;
            float t = (h - 70f / 360f) / (100f / 360f);
            float h2 = ((342f + (t - 0.5f) * 12f) / 360f) % 1f;
            float s2 = Mathf.Clamp(s * 0.6f, 0.22f, 0.45f);
            float v2 = v + (1f - v) * 0.55f;
            Color r = Color.HSVToRGB(h2, s2, v2);
            return new Color32((byte)Mathf.RoundToInt(r.r * 255f), (byte)Mathf.RoundToInt(r.g * 255f), (byte)Mathf.RoundToInt(r.b * 255f), c.a);
        }

        // ── Plantar ──────────────────────────────────────────────────────────────────────────

        private sealed class Cuenta
        {
            public int Planeados, Puestos, Matas, Setos, Flores, Color, ColorConFollaje;
        }

        /// Coloca todo el plan (las flores pintadas ya están) y la hiedra de la muralla, y deja en el informe las
        /// cifras de la guía de arte.
        public static void Plantar(Obra o)
        {
            var informe = new List<string>();
            Materiales mat = CargarMateriales(informe);
            Plan plan = Planear(o.Suelo);
            int ocupadoPrevio = o.Ocupado.Count;
            var motivosAntes = new Dictionary<string, int>(o.Motivos);
            int descartadasAntes = o.Descartadas;

            var cuentas = new Dictionary<string, Cuenta>();
            var grupos = new Dictionary<string, Transform>();
            var puestosArboles = new List<(Vector2 pos, float radio, Especie especie)>();
            foreach (Plantada pz in plan.Piezas)
            {
                if (!cuentas.TryGetValue(pz.Zona, out Cuenta cuenta)) cuentas[pz.Zona] = cuenta = new Cuenta();
                if (pz.Especie.Clase == Clase.Arbol) cuenta.Planeados++;
                string claveGrupo = pz.Zona + "|" + pz.Grupo;
                if (!grupos.TryGetValue(claveGrupo, out Transform grupo))
                    grupos[claveGrupo] = grupo = Grupo(Grupo(o.Raiz, PrefijoGrupo + pz.Zona), pz.Grupo);
                GameObject go = PlantarPieza(o, grupo, pz, mat, ocupadoPrevio);
                if (go == null) continue;
                switch (pz.Especie.Clase)
                {
                    case Clase.Arbol:
                        cuenta.Puestos++;
                        puestosArboles.Add((pz.Pos, pz.Ancho * 0.5f, pz.Especie));
                        if (pz.Especie.EsDeColor)
                        {
                            cuenta.Color++;
                            if (TieneFollaje(go, mat)) cuenta.ColorConFollaje++;
                        }
                        break;
                    case Clase.Mata: cuenta.Matas++; break;
                    case Clase.Seto: cuenta.Setos++; break;
                    case Clase.Flor: cuenta.Flores++; break;
                }
            }
            int hiedras = PonerHiedra(o, mat, ocupadoPrevio);
            // Los grupos en los que no ha cabido nada se quitan (y su zona, si se queda vacía).
            foreach (Transform g in grupos.Values)
            {
                if (g == null || g.childCount > 0) continue;
                Transform zona = g.parent;
                Object.DestroyImmediate(g.gameObject);
                if (zona != null && zona != o.Raiz && zona.childCount == 0) Object.DestroyImmediate(zona.gameObject);
            }

            // Informe
            int arboles = 0, matas = 0, setos = 0, flores = 0, color = 0, colorConFollaje = 0;
            foreach (Cuenta c in cuentas.Values)
            {
                arboles += c.Puestos; matas += c.Matas; setos += c.Setos; flores += c.Flores; color += c.Color; colorConFollaje += c.ColorConFollaje;
            }
            o.Informe.Add($"Vegetación: {arboles} árboles ({color} de color), {matas} matas, {setos} setos, {hiedras} racimos de hiedra en la muralla y {flores} matas de flores en los jardines del Reino.");
            o.Informe.AddRange(informe);
            var zonas = new List<string>(cuentas.Keys);
            zonas.Sort(System.StringComparer.Ordinal);
            foreach (string z in zonas)
            {
                Cuenta c = cuentas[z];
                var partes = new List<string>();
                if (c.Planeados > 0) partes.Add($"{c.Puestos} de {c.Planeados} árboles" + (c.Puestos > 0 ? $" ({100f * c.Color / c.Puestos:0} % de color)" : ""));
                if (c.Matas > 0) partes.Add($"{c.Matas} matas");
                if (c.Setos > 0) partes.Add($"{c.Setos} setos");
                if (c.Flores > 0) partes.Add($"{c.Flores} flores");
                o.Informe.Add($"  · {z}: {string.Join(", ", partes)}.");
            }
            InformarDeLaGuia(o, plan, puestosArboles, colorConFollaje, mat);
            var rechazos = new List<KeyValuePair<string, int>>(plan.Rechazos);
            rechazos.Sort((a, b) => b.Value.CompareTo(a.Value));
            var lineas = new List<string>();
            for (int i = 0; i < rechazos.Count && i < 8; i++) lineas.Add($"{rechazos[i].Value} × {rechazos[i].Key}");
            if (lineas.Count > 0) o.Informe.Add("  Al planear se han dejado sin poner: " + string.Join("; ", lineas) + ".");
            var alPoner = new List<KeyValuePair<string, int>>();
            foreach (KeyValuePair<string, int> m in o.Motivos)
            {
                motivosAntes.TryGetValue(m.Key, out int antes);
                if (m.Value > antes) alPoner.Add(new KeyValuePair<string, int>(m.Key, m.Value - antes));
            }
            alPoner.Sort((a, b) => b.Value.CompareTo(a.Value));
            lineas.Clear();
            foreach (KeyValuePair<string, int> m in alPoner) lineas.Add($"{m.Value} × {m.Key}");
            if (lineas.Count > 0) o.Informe.Add($"  Al plantar no han cabido {o.Descartadas - descartadasAntes}: " + string.Join("; ", lineas) + ".");
            o.Informe.Add("  Hornea de nuevo la navegación (El Sendero ▸ Navegación ▸ Bakear solo la superficie caminable) y pasa el menú Noche: pone luciérnagas donde hay 6 árboles cada 24 m (revisa que no caigan en la ruta del Fuego Fatuo, Camino 6).");
        }

        /// Coloca una pieza del plan. Antes de Poner comprueba lo que Poner no mira: que una pieza de la ciudad no
        /// tape una senda de puerta y que la copa de un árbol no se meta en un edificio, una torre o algo que ya
        /// estaba.
        private static GameObject PlantarPieza(Obra o, Transform grupo, Plantada pz, Materiales mat, int ocupadoPrevio)
        {
            Especie e = pz.Especie;
            float ancho = pz.Ancho;
            var pieza = new Pieza
            {
                Prefab = e.Prefab, Nombre = NombreDe(pz), Pos = pz.Pos, Rumbo = pz.Rumbo, Tamano = pz.Alto, Medida = Medida.Alto,
                Inclinar = pz.Inclinar, PermitirCamino = true, Holgura = 0.25f,
            };
            switch (e.Clase)
            {
                case Clase.Arbol:
                    pieza.Apoyo = Apoyo.Tronco;
                    pieza.RadioTronco = Mathf.Clamp(ancho * 0.12f, 0.4f, 1.2f);
                    pieza.PendienteMax = 32f;
                    pieza.MaterialFollaje = e.Tono == Tono.Cerezo ? mat.Cerezo : e.EsDeColor ? null : pz.FollajeDeBosque ? mat.Bosque : mat.Campo;
                    if (e.Tono == Tono.Cerezo && mat.Cerezo == null) pieza.Prefab = Especies["Tree04_a01"].Prefab;
                    break;
                case Clase.Mata:
                    pieza.Apoyo = Apoyo.Tronco;
                    pieza.RadioTronco = Mathf.Clamp(ancho * 0.3f, 0.3f, 0.8f);
                    pieza.PendienteMax = 42f;
                    pieza.MaterialFollaje = e.EsDeColor ? null : pz.FollajeDeBosque ? mat.Bosque : mat.Campo;
                    pieza.Holgura = 0.1f;
                    break;
                case Clase.Seto:
                    pieza.Medida = Medida.Escala;
                    pieza.Tamano = 1f;
                    pieza.Rumbo = pz.Rumbo + GiroDelLargo(o, e);
                    pieza.DesnivelMax = 0.7f;
                    pieza.Hundir = 0.12f;
                    pieza.PendienteMax = 25f;
                    pieza.Holgura = 0.1f;
                    break;
                case Clase.Flor:
                    pieza.Medida = Medida.Escala;
                    pieza.Hundir = 0f;
                    pieza.DesnivelMax = 0.6f;
                    pieza.PermitirSolapePropio = true;
                    pieza.Holgura = 0f;
                    pieza.SinObstaculo = true;
                    break;
            }
            if (pz.EnLaCiudad && e.Clase != Clase.Flor && TapaUnPaso(o, pz, pieza))
            {
                o.Descartar("tapa una senda o una calle", pieza);
                return null;
            }
            if (e.Clase == Clase.Arbol && CopaChoca(o, pz, ocupadoPrevio, out string motivo))
            {
                o.Descartar(motivo, pieza);
                return null;
            }
            return Poner(o, grupo, pieza);
        }

        /// Si una pieza de la ciudad tapa una calle o una senda de puerta (el seto, en toda su largura).
        private static bool TapaUnPaso(Obra o, Plantada pz, Pieza pieza)
        {
            if (pz.Especie.Clase != Clase.Seto) return EnCorredor(o, pz.Pos, pieza.RadioTronco > 0f ? pieza.RadioTronco : 0.5f);
            Vector2 medio = FrenteDe(pz.Rumbo) * 2.8f;
            return EnCorredor(o, pz.Pos, 0.9f) || EnCorredor(o, pz.Pos + medio, 0.9f) || EnCorredor(o, pz.Pos - medio, 0.9f);
        }

        private static string NombreDe(Plantada pz)
        {
            Especie e = pz.Especie;
            string prefab = Path.GetFileNameWithoutExtension(e.Prefab);
            switch (e.Clase)
            {
                case Clase.Arbol: return e.Tono == Tono.Cerezo ? $"Cerezo ({prefab})" : (pz.Dominante ? "Árbol dominante (" : "Árbol (") + prefab + ")";
                case Clase.Mata: return $"Mata ({prefab})";
                case Clase.Seto: return $"Seto ({prefab})";
                default: return $"Flores ({prefab})";
            }
        }

        /// La copa (el 70 % de su radio) no entra en una casa, torre o pieza grande que se puso antes que la
        /// vegetación, ni en algo que ya estaba en la escena a la altura de la copa.
        private static bool CopaChoca(Obra o, Plantada pz, int ocupadoPrevio, out string motivo)
        {
            float r = pz.Ancho * 0.5f * 0.7f;
            var copa = new Huella { Centro = pz.Pos, EjeX = Vector2.right, EjeZ = Vector2.up, MedioX = r, MedioZ = r };
            for (int i = 0; i < ocupadoPrevio && i < o.Ocupado.Count; i++)
            {
                Huella h = o.Ocupado[i];
                if (Mathf.Max(h.MedioX, h.MedioZ) <= 2.5f) continue;
                if (h.Solapa(copa)) { motivo = "la copa se mete en un edificio o una torre"; return true; }
            }
            if (pz.Alto > 4f)
            {
                float suelo = o.Suelo.Altura(pz.Pos.x, pz.Pos.y);
                var centro = new Vector3(pz.Pos.x, suelo + pz.Alto * 0.68f, pz.Pos.y);
                var medio = new Vector3(r * 0.8f, pz.Alto * 0.22f, r * 0.8f);
                if (ChocaEnCaja(o, centro, medio)) { motivo = "la copa se mete en algo que ya estaba en la escena"; return true; }
            }
            motivo = null;
            return false;
        }

        private static readonly Dictionary<string, float> GirosDelLargo = new();

        /// Grados que hay que sumar al rumbo para que el lado largo del seto vaya a lo largo de su rumbo (mide el
        /// prefab una vez).
        private static float GiroDelLargo(Obra o, Especie e)
        {
            if (GirosDelLargo.TryGetValue(e.Prefab, out float g)) return g;
            g = 0f;
            GameObject fuente = CargarPrefab(o, e.Prefab);
            if (fuente != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(fuente, o.Raiz);
                go.transform.SetPositionAndRotation(Vector3.zero, fuente.transform.localRotation);
                Bounds b = LimitesVisibles(go);
                Object.DestroyImmediate(go);
                if (b.size.x > b.size.z) g = 90f;
            }
            GirosDelLargo[e.Prefab] = g;
            return g;
        }

        private static bool TieneFollaje(GameObject go, Materiales mat)
        {
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && (m == mat.Campo || m == mat.Bosque)) return true;
            return false;
        }

        // ── Hiedra en la muralla ─────────────────────────────────────────────────────────────

        private const string HiedraGrande = Veg + "Vine01_a01.prefab";
        private const string HiedraPequena = Veg + "Vine01_b01.prefab";

        /// Racimos de hiedra (Vine01, planos) en la cara exterior de los lienzos que se ven altos desde fuera: cada
        /// 18–26 m en el frente sur, el que se ve desde el Camino 10, y cada 30–45 m en los demás. Cada racimo trepa
        /// desde el pie hasta un 65–80 % del muro en filas de dos y una hoja, sin tapar las almenas ni las torres.
        private static int PonerHiedra(Obra o, Materiales mat, int ocupadoPrevio)
        {
            Transform lienzos = o.Raiz.Find("Reino/Muralla/Lienzos");
            if (lienzos == null || lienzos.childCount == 0) { o.Informe.Add("  · Hiedra: no está la muralla nueva; no se pone."); return 0; }
            var cajas = new List<Bounds>();
            foreach (Transform t in lienzos) cajas.Add(LimitesVisibles(t.gameObject));
            GameObject grande = CargarPrefab(o, HiedraGrande), pequena = CargarPrefab(o, HiedraPequena);
            if (grande == null || pequena == null) return 0;
            Vector3 normalGrande = NormalDeLaMalla(grande), normalPequena = NormalDeLaMalla(pequena);
            Transform grupo = null;

            Vector2[] traza = MurallaDelReino.TrazaMedia();
            Vector2[] cerrada = PoligonoCerrado(traza);
            var dado = new Ruido.Dado(7801);
            int piezas = 0, racimos = 0;
            float s = 6f, largo = Largo(cerrada);
            while (s < largo)
            {
                var (centro, dir) = Recorrido(cerrada, s);
                Vector2 fuera = new Vector2(dir.y, -dir.x);
                bool sur = fuera.y < -0.7f;
                s += sur ? dado.Entre(18f, 26f) : dado.Entre(30f, 45f);
                // Puerta Real y lo que no es lienzo (torres): sin hiedra.
                if (Vector2.Distance(centro, new Vector2(92.9f, 243f)) < 16f) continue;
                // A un palmo de la cara del lienzo (su semigrueso es 1,72 m), por los relieves de la sillería.
                Vector2 cara = centro + fuera * 1.85f;
                bool enTorre = false;
                for (int i = 0; i < ocupadoPrevio && i < o.Ocupado.Count; i++)
                {
                    Huella h = o.Ocupado[i];
                    if (Mathf.Max(h.MedioX, h.MedioZ) > 2.5f && h.Contiene(cara, 1.5f)) { enTorre = true; break; }
                }
                if (enTorre) continue;
                float arriba = float.MinValue;
                foreach (Bounds b in cajas)
                    if (centro.x > b.min.x && centro.x < b.max.x && centro.y > b.min.z && centro.y < b.max.z) arriba = Mathf.Max(arriba, b.max.y);
                if (arriba == float.MinValue) continue;
                float suelo = o.Suelo.Altura(cara.x + fuera.x * 0.3f, cara.y + fuera.y * 0.3f);
                float alto = arriba - suelo;
                if (alto < 4f) continue;

                // Racimo: filas de abajo arriba hasta el 65–80 % del muro (sin pasar de 1,5 m bajo las almenas).
                float escala = dado.Entre(2.4f, 3f);
                float altoHoja = 1.49f * escala, anchoHoja = 0.97f * escala;
                float techo = Mathf.Min(suelo + alto * dado.Entre(0.65f, 0.8f), arriba - 1.5f);
                int filas = Mathf.Clamp(Mathf.RoundToInt((techo - suelo) / (altoHoja * 0.8f)), 1, 4);
                Vector2 alLado = dir;
                racimos++;
                if (grupo == null) grupo = Grupo(o.Raiz, PrefijoGrupo + "Hiedra de la muralla");
                Transform racimo = Grupo(grupo, $"Racimo {racimos}");
                for (int f = 0; f < filas; f++)
                {
                    int enFila = f == filas - 1 ? 1 : 2;
                    float y = suelo + altoHoja * 0.42f + f * altoHoja * 0.78f;
                    for (int j = 0; j < enFila; j++)
                    {
                        float desvio = enFila == 1 ? dado.Entre(-0.25f, 0.25f) * anchoHoja : (j == 0 ? -0.42f : 0.42f) * anchoHoja + dado.Entre(-0.2f, 0.2f);
                        Vector2 q = cara + alLado * desvio;
                        bool hojaPequena = f == filas - 1 && dado.Siguiente() < 0.5f;
                        GameObject hoja = Colgar(o, racimo, hojaPequena ? pequena : grande, hojaPequena ? normalPequena : normalGrande,
                            new Vector3(q.x, y, q.y), fuera, dado.Entre(-8f, 8f), escala * dado.Entre(0.9f, 1.1f), mat.Campo);
                        if (hoja != null) piezas++;
                    }
                }
            }
            o.Informe.Add($"  · Hiedra de la muralla: {racimos} racimos ({piezas} hojas) en la cara exterior de los lienzos.");
            return racimos;
        }

        /// Normal media de la primera malla del prefab tal como viene (con el giro de serie de su raíz): la cara
        /// que se ve de la hoja (Tree.mat no pinta la cara de atrás).
        private static Vector3 NormalDeLaMalla(GameObject prefab)
        {
            MeshFilter mf = prefab.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return Vector3.forward;
            Vector3 suma = Vector3.zero;
            foreach (Vector3 n in mf.sharedMesh.normals) suma += n;
            if (suma.sqrMagnitude < 1e-6f) return Vector3.forward;
            return mf.transform.TransformDirection(suma.normalized).normalized;
        }

        /// Cuelga una hoja de hiedra: su cara mira hacia «fuera», girada «ladeo» grados en su plano, centrada en
        /// «centro». Sin obstáculo de navegación (no tiene colisor).
        private static GameObject Colgar(Obra o, Transform grupo, GameObject prefab, Vector3 normalDeSerie, Vector3 centro, Vector2 fuera, float ladeo, float escala, Material follaje)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo);
            go.name = "Hiedra";
            Transform t = go.transform;
            var haciaFuera = new Vector3(fuera.x, 0f, fuera.y);
            Vector3 normalPlana = new Vector3(normalDeSerie.x, 0f, normalDeSerie.z);
            if (normalPlana.sqrMagnitude < 1e-4f) normalPlana = Vector3.forward;
            // Solo se gira alrededor de la vertical (la hoja sigue de pie) y luego se ladea en su plano.
            Quaternion giro = Quaternion.AngleAxis(Vector3.SignedAngle(normalPlana, haciaFuera, Vector3.up), Vector3.up);
            t.rotation = Quaternion.AngleAxis(ladeo, haciaFuera) * giro * prefab.transform.localRotation;
            t.localScale = prefab.transform.localScale * escala;
            t.position = centro;
            Bounds b = LimitesVisibles(go);
            t.position += centro - b.center;
            if (follaje != null) CambiarFollaje(go, follaje);
            o.SinObstaculo.Add(t);
            o.Puestas++;
            return go;
        }

        // ── Cifras de la guía de arte ────────────────────────────────────────────────────────

        private static void InformarDeLaGuia(Obra o, Plan plan, List<(Vector2 pos, float radio, Especie especie)> nuevos, int colorConFollaje, Materiales mat)
        {
            Lienzo l = o.Suelo;
            // Copas: las que ya había y las nuevas, cada grupo en su índice (para dar el antes y el después).
            var nuevas = new List<Vector3>();
            foreach (var (pos, radio, _) in nuevos) nuevas.Add(new Vector3(pos.x, pos.y, radio));
            var indiceNuevas = new Rejilla(12f);
            for (int i = 0; i < nuevas.Count; i++) indiceNuevas.Anadir(new Vector2(nuevas[i].x, nuevas[i].y), i);
            var tmp = new List<int>();
            bool Cubre(List<Vector3> copas, Rejilla indice, Vector2 q)
            {
                indice.Cerca(q, 12f, tmp);
                foreach (int i in tmp)
                    if (Vector2.Distance(q, new Vector2(copas[i].x, copas[i].y)) < copas[i].z) return true;
                return false;
            }
            bool YaHabia(Vector2 q) => Cubre(plan.Existentes, plan.IndiceExistentes, q);
            bool BajoCopa(Vector2 q) => YaHabia(q) || Cubre(nuevas, indiceNuevas, q);

            // Copa en el anillo de 0–40 m fuera de la muralla (suelo de menos de 35°).
            int anillo = 0, cubiertoAntes = 0, cubierto = 0;
            for (float x = -160f; x <= 160f; x += 2f)
                for (float z = 195f; z <= 400f; z += 2f)
                {
                    var q = new Vector2(x, z);
                    if (DentroDePoligono(q, TrazaMuralla) || DistanciaALaMuralla(plan, q) > 40f) continue;
                    if (l.Altura(x, z) < 1.5f || l.Pendiente(x, z) > 35f) continue;
                    anillo++;
                    if (YaHabia(q)) cubiertoAntes++;
                    if (BajoCopa(q)) cubierto++;
                }

            // Celdas de 25 m del campo sin copa: la isla principal sin el Bosque Prohibido, Will, el Reino, la
            // montaña, las playas, los cortados ni lo que se deja sin plantar a propósito (laguna y marjal).
            int celdas = 0, vaciasAntes = 0, vacias = 0;
            for (float cx = -450f; cx < 450f; cx += 25f)
                for (float cz = -520f; cz < 420f; cz += 25f)
                {
                    var c = new Vector2(cx + 12.5f, cz + 12.5f);
                    float y = l.Altura(c.x, c.y);
                    if (y < 1.5f || y > CotaDeLaMontana || l.Pendiente(c.x, c.y) > 40f || l.Peso(c.x, c.y, l.Capa(CapaArena)) > 0.4f) continue;
                    if (BosqueProhibido.Contains(c) || DentroDePoligono(c, TrazaMuralla) || Vector2.Distance(c, new Vector2(-0.6f, -130.3f)) < 72f) continue;
                    if (MarjalReservado.Contains(c)) continue;
                    Vector4 e = RiberaDeLaLaguna;
                    float ex = (c.x - e.x) / e.z, ez = (c.y - e.y) / e.w;
                    if (ex * ex + ez * ez < 1f) continue;
                    celdas++;
                    bool antes = false, nueva = false;
                    for (float dx = -10f; dx <= 10f; dx += 5f)
                        for (float dz = -10f; dz <= 10f; dz += 5f)
                        {
                            Vector2 q = c + new Vector2(dx, dz);
                            antes |= YaHabia(q);
                            nueva |= Cubre(nuevas, indiceNuevas, q);
                        }
                    if (!antes) vaciasAntes++;
                    if (!antes && !nueva) vacias++;
                }

            // Árboles nuevos sueltos o en pareja (enlazando a 12 m), sin contar las hileras a propósito (paseo de
            // ronda, jardines de palacio y alameda de cipreses).
            int n = nuevos.Count;
            var padre = new int[n];
            for (int i = 0; i < n; i++) padre[i] = i;
            int Raiz(int i) { while (padre[i] != i) i = padre[i] = padre[padre[i]]; return i; }
            var indiceNuevos = new Rejilla(12f);
            for (int i = 0; i < n; i++) indiceNuevos.Anadir(nuevos[i].pos, i);
            for (int i = 0; i < n; i++)
            {
                indiceNuevos.Cerca(nuevos[i].pos, 12f, tmp);
                foreach (int j in tmp)
                    if (j > i && Vector2.Distance(nuevos[i].pos, nuevos[j].pos) < 12f) padre[Raiz(i)] = Raiz(j);
            }
            var tamano = new Dictionary<int, int>();
            for (int i = 0; i < n; i++) { int r = Raiz(i); tamano.TryGetValue(r, out int t); tamano[r] = t + 1; }
            int sueltos = 0, contados = 0;
            for (int i = 0; i < n; i++)
            {
                if (EnHilera(nuevos[i].pos)) continue;
                contados++;
                if (tamano[Raiz(i)] <= 2) sueltos++;
            }

            o.Informe.Add("  Guía de arte (contando los árboles que ya había; antes → con la vegetación nueva):");
            o.Informe.Add($"    copa en el anillo de 0–40 m fuera de la muralla del Reino: {Porcentaje(cubiertoAntes, anillo)} → {Porcentaje(cubierto, anillo)} " +
                          "(objetivo 25–35 %; solo suelo de menos de 35°: la escarpa sur y el Camino 10 no se plantan)");
            o.Informe.Add($"    celdas de 25 m del campo sin copa: {Porcentaje(vaciasAntes, celdas)} → {Porcentaje(vacias, celdas)} de {celdas} (objetivo ≤ 30 %)");
            o.Informe.Add($"    árboles nuevos sueltos o en pareja: {Porcentaje(sueltos, contados)} (objetivo ≤ 15 %; sin contar las hileras de los jardines, el paseo de ronda y la alameda)");
            o.Informe.Add($"    árboles de color con follaje matizado: {colorConFollaje} (objetivo 0)" + (mat.Cerezo == null ? "; sin material de cerezo: los cerezos se han plantado como turquesas" : ""));
        }

        private static bool EnHilera(Vector2 q) => DentroDePoligono(q, TrazaMuralla) || (q.y > 205f && q.y < 222f && q.x > -112f && q.x < 90f);

        private static string Porcentaje(int a, int b) => b <= 0 ? "—" : $"{100f * a / b:0} %";
    }
}
