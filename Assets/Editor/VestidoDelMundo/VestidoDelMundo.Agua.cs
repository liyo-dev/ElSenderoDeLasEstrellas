using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// Agua del vestido de MainWorld: la Laguna de la Era, una cuenca natural del terreno al sureste de la subida a las
/// granjas que se llena de agua sin excavar nada.
///
/// · Medida. Desde un punto del fondo se inunda el heightmap (GetHeights) subiendo la cota hasta que el agua se
///   saldría del recuadro de búsqueda (el desborde); la lámina queda «Margen» metros por debajo. Si la cota o el área
///   se apartan mucho de lo esperado (el terreno ha cambiado), no se pone nada y se avisa en el informe.
/// · Agua. Lámina en malla (CarpetaGenerada) con AguaLaguna.mat, una copia de Water_Lake.mat ajustada a agua de
///   ribera que se crea una vez en CarpetaRecursos; cajas trigger en la capa Water con la cara de arriba en la cota
///   (PlayerSwimmingController nada donde cubre); NavMeshModifierVolume «Not Walkable» en la capa Floor donde hay
///   0,6 m o más de fondo (la superficie de navegación solo recoge los volúmenes de su máscara de capas).
/// · Suelo. Fondo de tierra de bosque y tierra pisada, barro en la orilla con el borde roto y una llegada de arena
///   donde la senda de la Era se acerca al agua; bajo el agua se quita la hierba de detalle.
/// · Ribera compuesta por reglas a partir de la forma de la orilla, no por coordenadas sueltas: la orilla que da al
///   Camino 2 (que pasa unos 8 m más alto) queda despejada para que se vea el agua desde arriba; enfrente, bosquetes
///   de árboles de color inclinados hacia el agua hacen de telón; un árbol azul marca la punta de tierra que entra
///   en la laguna y es lo que se mira desde la llegada; juncales en los bajos que dan la espalda al camino, grupos de
///   nenúfares al abrigo de los juncos, troncos caídos medio hundidos, peñas donde la orilla es empinada, setas al
///   pie de los árboles y manchas de flores azules y blancas en la orilla despejada.
/// · Ambiente. AmbientZone con AmbientPreset_Laguna (niebla baja del clima mientras se está en la cuenca), niebla
///   nocturna sobre el agua (NieblaNocturna), ranas en el juncal (AudioSource 3D en el grupo Ambience) y ZonaSinArenas
///   para que ninguna batalla se libre en el agua ni en su orilla.
/// · Lo que se quedaría bajo el agua se retira con el registro del vestido (más de 1 m de fondo, o tapado del todo
///   si es pequeño); lo que queda con menos fondo se deja: parece arboleda inundada.
///
/// Canon: sin vado ni pasaderas que crucen el agua, sin presa ni acequias (Risco y Vega, GDD § 10), sin luces de
/// fuego fatuo (la ruta del Fuego Fatuo es el Camino 6), sin tocones ni troncos apilados.
public static partial class VestidoDelMundo
{
    static partial void ZonasDelAgua(List<Zona> libres)
    {
        Aguas.Zonas(libres);
    }

    static partial void RetirarBajoElAgua(Scene escena, Obra o)
    {
        Aguas.RetirarLoSumergido(escena, o);
    }

    static partial void PintarSueloDeAgua(Lienzo l, List<string> informe)
    {
        Aguas.PintarSuelo(l, informe);
    }

    static partial void PonerAgua(Obra o)
    {
        Aguas.PonerLagunas(o);
    }

    /// Todo lo del agua, en su propia clase para que sus nombres no se crucen con los de las otras partes del vestido.
    private static class Aguas
    {
        // ── Datos ────────────────────────────────────────────────────────────────────────────

        private sealed class Laguna
        {
            public string Nombre;
            /// Punto del fondo desde el que se inunda.
            public Vector2 Semilla;
            /// Metros entre la lámina y el desborde (lo que tendría que subir el agua para salirse).
            public float Margen = 1.2f;
            /// Semilado del recuadro de búsqueda: si el agua llega a su borde, se ha salido de la cuenca.
            public float Busqueda = 110f;
            /// Comprobación: si la cota o el área medidas se apartan mucho de estas, el terreno ha cambiado y no se pone.
            public float CotaEsperada, AreaEsperada;
            /// Punto del camino desde el que se ve la laguna: la orilla que le da la cara queda despejada.
            public Vector2 Mirador;
            /// Paraje cuya senda baja hacia la orilla: donde más se acerca al agua se abre la llegada.
            public string ParajeDeLlegada;
            public int SemillaRuido;
        }

        private static readonly Laguna[] Lagunas =
        {
            new Laguna
            {
                Nombre = "Laguna de la Era", Semilla = new Vector2(185f, -33f), Margen = 1.2f, Busqueda = 110f,
                CotaEsperada = 24.5f, AreaEsperada = 2260f,
                // Tramo del Camino 2 que pasa por encima de la orilla noroeste.
                Mirador = new Vector2(131f, -14.6f),
                ParajeDeLlegada = "Era de trilla",
                SemillaRuido = 6100,
            },
        };

        private const string Veg = FK + "Vegetation/";
        private const string Rocas = FK + "Rock/";
        private const string RutaAguaDeLago = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR/Material/Special/Water_Lake.mat";
        private const string ArchivoAguaDeLaLaguna = "AguaLaguna.mat";
        private const string ArchivoPresetDeLaLaguna = "AmbientPreset_Laguna.asset";
        private const string RutaNieblaBaja = "Assets/Settings/AmbientPresets/Mat_GroundMist.mat";
        private const string RutaRanas = "Assets/Audio/FREE SOUND PACK_TM(355)/Ambiences(43)/Frogs_Swamp-003.wav";
        private const string RutaMezclador = "Assets/AudioMixer.mixer";
        private const string GrupoDeAmbiente = "Ambience";

        private static readonly string[] JuncosAltos = { Veg + "Grass01_a02.prefab", Veg + "Grass02_a02.prefab" };
        /// Roseta de hojas: junco bajo en el borde del juncal y hoja flotante entre los nenúfares.
        private const string Roseta = Veg + "Grass03_a01.prefab";
        private const string NenufarEnFlor = Veg + "Plant06_a01.prefab";
        private static readonly string[] Troncos =
        {
            Tiny + "BuildingUtilityDeco/WoodLog01.prefab", Tiny + "BuildingUtilityDeco/WoodLog02.prefab",
        };
        private static readonly string[] PiedrasDeOrilla =
        {
            Rocas + "Rock01_a01.prefab", Rocas + "Rock01_a02.prefab", Rocas + "Rock01_a03.prefab",
            Rocas + "Rock01_a04.prefab", Rocas + "Rock01_a05.prefab", Rocas + "Rock01_a06.prefab",
        };
        /// Losas planas para sentarse en la llegada.
        private static readonly string[] Losas = { Rocas + "Rock01_a01.prefab", Rocas + "Rock01_a02.prefab" };
        private static readonly string[] SetasTurquesa = { Veg + "Mushroom02_b01.prefab", Veg + "Mushroom02_b02.prefab", Veg + "Mushroom02_b03.prefab" };
        private static readonly string[] SetasAzules = { Veg + "Mushroom01_a01.prefab", Veg + "Mushroom01_a02.prefab", Veg + "Mushroom01_a03.prefab" };
        private const string FlorAzul = Veg + "Flower02_b01.prefab";
        private const string FlorBlanca = Veg + "Flower05_a01.prefab";
        /// Árbol azul de la punta: el hito que se ve desde la llegada y desde el camino.
        private const string ArbolHito = Veg + "Tree04_e01.prefab";
        /// Bosquetes de la orilla de enfrente: árboles de tronco curvo, un color por bosquete (con el Tree.mat de serie).
        /// El primero de cada lista es el del árbol mayor.
        private static readonly string[] ArbolesOliva = { Veg + "Tree04_d03.prefab", Veg + "Tree04_d02.prefab" };
        private static readonly string[] ArbolesTurquesa = { Veg + "Tree04_a03.prefab", Veg + "Tree04_a02.prefab" };
        /// Árboles de cada bosquete (y cuántos bosquetes hay como mucho).
        private static readonly int[] ArbolesPorBosquete = { 4, 4, 2 };

        /// Fondo a partir del que el NavMesh deja de ser caminable (en la orilla se puede vadear).
        private const float FondoNoCaminable = 0.6f;
        /// Margen de la zona sin arenas alrededor del agua.
        private const float MargenSinArenas = 25f;
        /// Margen de la zona de ambiente alrededor del agua (no llega al Camino 2, que va más alto y más lejos).
        private const float MargenDeAmbiente = 8f;
        /// Radio de la llegada que se deja sin juncos ni árboles.
        private const float ClaroDeLaLlegada = 7f;
        /// Las zonas libres del agua llevan este prefijo (así las piezas de la ribera saben cuáles son suyas).
        private const string PrefijoZona = "Agua — ";

        // ── Medida de la cuenca ──────────────────────────────────────────────────────────────

        /// La laguna medida sobre el heightmap: cota, muestras inundadas y su caja.
        private sealed class Cuenca
        {
            public Laguna Def;
            public Terrain Terreno;
            public Vector3 Origen;
            public float Paso;
            public float Desborde, Cota, Fondo;
            /// Recorte del heightmap alrededor de la semilla: muestras [K0, K0 + NK) × [I0, I0 + NI).
            public int I0, K0, NI, NK;
            /// Altura de mundo de cada muestra [k, i].
            public float[,] Alto;
            /// Muestras bajo el agua [k, i] (8-conexas desde la semilla).
            public bool[,] Agua;
            public int Muestras;
            /// Caja del agua en planta (hasta el borde de las muestras inundadas).
            public Rect Caja;

            public float X(int i) => Origen.x + (I0 + i) * Paso;
            public float Z(int k) => Origen.z + (K0 + k) * Paso;
            public int I(float x) => Mathf.RoundToInt((x - Origen.x) / Paso) - I0;
            public int K(float z) => Mathf.RoundToInt((z - Origen.z) / Paso) - K0;
            public float Area => Muestras * Paso * Paso;
            public bool AguaEn(int k, int i) => k >= 0 && i >= 0 && k < NK && i < NI && Agua[k, i];
            public float Altura(float x, float z) => Terreno.SampleHeight(new Vector3(x, 0f, z)) + Origen.y;
            public float Profundidad(float x, float z) => Cota - Altura(x, z);

            public float Pendiente(float x, float z)
            {
                Vector3 t = Terreno.terrainData.size;
                return Terreno.terrainData.GetSteepness(Mathf.Clamp01((x - Origen.x) / t.x), Mathf.Clamp01((z - Origen.z) / t.z));
            }
        }

        /// Terrain de MainWorld cargado (el que usa RutaTerreno).
        private static Terrain TerrenoDelMundo()
        {
            foreach (Terrain t in Terrain.activeTerrains)
                if (t != null && t.terrainData != null && AssetDatabase.GetAssetPath(t.terrainData) == RutaTerreno) return t;
            return null;
        }

        private static readonly int[] DK = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DI = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// Inunda el heightmap desde la semilla y mide la laguna. Devuelve null (con el motivo) si no hay cuenca o si
        /// no se parece a lo esperado.
        private static Cuenca Medir(Terrain terreno, Laguna lg, out string error)
        {
            error = null;
            if (terreno == null) { error = "no encuentro el Terrain de MainWorld"; return null; }
            TerrainData datos = terreno.terrainData;
            int res = datos.heightmapResolution;
            var c = new Cuenca { Def = lg, Terreno = terreno, Origen = terreno.transform.position, Paso = datos.size.x / (res - 1) };
            int ic = Mathf.RoundToInt((lg.Semilla.x - c.Origen.x) / c.Paso), kc = Mathf.RoundToInt((lg.Semilla.y - c.Origen.z) / c.Paso);
            int r = Mathf.CeilToInt(lg.Busqueda / c.Paso);
            c.I0 = Mathf.Max(0, ic - r);
            c.K0 = Mathf.Max(0, kc - r);
            c.NI = Mathf.Min(res, ic + r + 1) - c.I0;
            c.NK = Mathf.Min(res, kc + r + 1) - c.K0;
            if (c.NI < 8 || c.NK < 8) { error = "la semilla cae fuera del terreno"; return null; }

            float[,] h = datos.GetHeights(c.I0, c.K0, c.NI, c.NK);
            c.Alto = new float[c.NK, c.NI];
            float techo = float.MinValue;
            for (int k = 0; k < c.NK; k++)
                for (int i = 0; i < c.NI; i++)
                {
                    c.Alto[k, i] = c.Origen.y + h[k, i] * datos.size.y;
                    techo = Mathf.Max(techo, c.Alto[k, i]);
                }

            int si = ic - c.I0, sk = kc - c.K0;
            float bajo = c.Alto[sk, si], alto = techo + 1f;
            var visto = new bool[c.NK, c.NI];
            var pila = new Stack<int>();
            // El desborde es la cota más baja a la que el agua, subiendo desde la semilla, llega al borde del recuadro.
            for (int n = 0; n < 40 && alto - bajo > 0.002f; n++)
            {
                float medio = (bajo + alto) * 0.5f;
                if (Inundar(c, si, sk, medio, visto, pila, out _)) alto = medio;
                else bajo = medio;
            }
            c.Desborde = alto;
            c.Cota = c.Desborde - lg.Margen;
            if (c.Alto[sk, si] >= c.Cota) { error = $"la semilla ({lg.Semilla.x:0}, {lg.Semilla.y:0}) no queda bajo el agua (desborde a {c.Desborde:0.00} m)"; return null; }
            if (Inundar(c, si, sk, c.Cota, visto, pila, out int muestras)) { error = "el agua se sale del recuadro de búsqueda"; return null; }

            c.Agua = visto;
            c.Muestras = muestras;
            c.Fondo = float.MaxValue;
            int i0 = int.MaxValue, i1 = int.MinValue, k0 = int.MaxValue, k1 = int.MinValue;
            for (int k = 0; k < c.NK; k++)
                for (int i = 0; i < c.NI; i++)
                {
                    if (!c.Agua[k, i]) continue;
                    c.Fondo = Mathf.Min(c.Fondo, c.Alto[k, i]);
                    i0 = Mathf.Min(i0, i); i1 = Mathf.Max(i1, i);
                    k0 = Mathf.Min(k0, k); k1 = Mathf.Max(k1, k);
                }
            c.Caja = Rect.MinMaxRect(c.X(i0) - c.Paso * 0.5f, c.Z(k0) - c.Paso * 0.5f, c.X(i1) + c.Paso * 0.5f, c.Z(k1) + c.Paso * 0.5f);

            if (Mathf.Abs(c.Cota - lg.CotaEsperada) > 0.8f || c.Area < lg.AreaEsperada * 0.6f || c.Area > lg.AreaEsperada * 1.6f)
            {
                error = $"el terreno ha cambiado: cota {c.Cota:0.00} m y {c.Area:0} m² (se esperaban unos {lg.CotaEsperada:0.0} m y {lg.AreaEsperada:0} m²). " +
                        "Revisa la cuenca y actualiza la tabla de lagunas en VestidoDelMundo.Agua.";
                return null;
            }
            return c;
        }

        /// Marca en «visto» las muestras conexas a la semilla por debajo de «cota». Devuelve true si llegan al borde
        /// del recuadro (el agua se saldría).
        private static bool Inundar(Cuenca c, int si, int sk, float cota, bool[,] visto, Stack<int> pila, out int cuantas)
        {
            System.Array.Clear(visto, 0, visto.Length);
            pila.Clear();
            cuantas = 0;
            if (c.Alto[sk, si] >= cota) return false;
            visto[sk, si] = true;
            pila.Push(sk * c.NI + si);
            bool sale = false;
            while (pila.Count > 0)
            {
                int n = pila.Pop(), k = n / c.NI, i = n % c.NI;
                cuantas++;
                if (k == 0 || i == 0 || k == c.NK - 1 || i == c.NI - 1) sale = true;
                for (int d = 0; d < 8; d++)
                {
                    int kk = k + DK[d], ii = i + DI[d];
                    if (kk < 0 || ii < 0 || kk >= c.NK || ii >= c.NI || visto[kk, ii] || c.Alto[kk, ii] >= cota) continue;
                    visto[kk, ii] = true;
                    pila.Push(kk * c.NI + ii);
                }
            }
            return sale;
        }

        /// Muestras inundadas ampliadas «n» muestras alrededor.
        private static bool[,] Ampliada(Cuenca c, int n)
        {
            bool[,] a = c.Agua;
            for (int paso = 0; paso < n; paso++)
            {
                var b = (bool[,])a.Clone();
                for (int k = 0; k < c.NK; k++)
                    for (int i = 0; i < c.NI; i++)
                    {
                        if (!a[k, i]) continue;
                        for (int d = 0; d < 8; d++)
                        {
                            int kk = k + DK[d], ii = i + DI[d];
                            if (kk >= 0 && ii >= 0 && kk < c.NK && ii < c.NI) b[kk, ii] = true;
                        }
                    }
                a = b;
            }
            return a;
        }

        /// Descompone una máscara de muestras en rectángulos que no se pisan (los más grandes primero por filas).
        private static List<RectInt> Rectangulos(bool[,] m)
        {
            int nk = m.GetLength(0), ni = m.GetLength(1);
            var usado = new bool[nk, ni];
            var r = new List<RectInt>();
            for (int k = 0; k < nk; k++)
                for (int i = 0; i < ni; i++)
                {
                    if (!m[k, i] || usado[k, i]) continue;
                    int ancho = 1;
                    while (i + ancho < ni && m[k, i + ancho] && !usado[k, i + ancho]) ancho++;
                    int alto = 1;
                    for (bool sigue = true; sigue && k + alto < nk; )
                    {
                        for (int j = 0; j < ancho; j++)
                            if (!m[k + alto, i + j] || usado[k + alto, i + j]) { sigue = false; break; }
                        if (sigue) alto++;
                    }
                    for (int a = 0; a < alto; a++)
                        for (int j = 0; j < ancho; j++) usado[k + a, i + j] = true;
                    r.Add(new RectInt(i, k, ancho, alto));
                }
            return r;
        }

        /// Rectángulo de mundo (en planta) que cubren las muestras de un RectInt.
        private static Rect EnMundo(Cuenca c, RectInt q) =>
            Rect.MinMaxRect(c.X(q.xMin) - c.Paso * 0.5f, c.Z(q.yMin) - c.Paso * 0.5f, c.X(q.xMax - 1) + c.Paso * 0.5f, c.Z(q.yMax - 1) + c.Paso * 0.5f);

        // ── Orilla: rejilla de 1 m con la distancia al agua ──────────────────────────────────

        /// La orilla en una rejilla de 1 m (más fina que el heightmap): qué está mojado, la distancia con signo a la
        /// línea del agua (+ en tierra, − en el agua) y los puntos que ordenan la composición.
        private sealed class Ribera
        {
            public readonly Cuenca C;
            public readonly float X0, Z0;
            public readonly int NX, NZ;
            public readonly float[,] Suelo;
            public readonly bool[,] Mojado;
            public readonly float[,] Dist;
            /// Punto de la senda del paraje más cercano al agua y punto de la orilla frente a él.
            public Vector2 PuntoDeSenda, Llegada;
            public bool HayLlegada;
            /// Punta de tierra más rodeada de agua: el hito.
            public Vector2 Hito;
            public bool HayHito;

            private const float Borde = 26f;

            public Ribera(Cuenca c)
            {
                C = c;
                X0 = Mathf.Floor(c.Caja.xMin - Borde);
                Z0 = Mathf.Floor(c.Caja.yMin - Borde);
                NX = Mathf.CeilToInt(c.Caja.width + 2f * Borde) + 1;
                NZ = Mathf.CeilToInt(c.Caja.height + 2f * Borde) + 1;
                Suelo = new float[NZ, NX];
                Mojado = new bool[NZ, NX];
                bool[,] cerca = Ampliada(c, 1);
                for (int a = 0; a < NZ; a++)
                    for (int b = 0; b < NX; b++)
                    {
                        float x = X0 + b, z = Z0 + a;
                        float y = c.Altura(x, z);
                        Suelo[a, b] = y;
                        int k = c.K(z), i = c.I(x);
                        Mojado[a, b] = y < c.Cota && k >= 0 && i >= 0 && k < c.NK && i < c.NI && cerca[k, i];
                    }
                float[,] enTierra = Chaflan(Mojado, true), enAgua = Chaflan(Mojado, false);
                Dist = new float[NZ, NX];
                for (int a = 0; a < NZ; a++)
                    for (int b = 0; b < NX; b++)
                        Dist[a, b] = Mojado[a, b] ? 0.5f - enAgua[a, b] : enTierra[a, b] - 0.5f;
                BuscarLlegada();
                BuscarHito();
            }

            /// Distancia de chaflán (1 y √2) a la celda más cercana con Mojado == fuente.
            private float[,] Chaflan(bool[,] mojado, bool fuente)
            {
                const float Inf = 1e9f, D2 = 1.41421356f;
                var d = new float[NZ, NX];
                for (int a = 0; a < NZ; a++)
                    for (int b = 0; b < NX; b++) d[a, b] = mojado[a, b] == fuente ? 0f : Inf;
                for (int a = 0; a < NZ; a++)
                    for (int b = 0; b < NX; b++)
                    {
                        float v = d[a, b];
                        if (a > 0)
                        {
                            v = Mathf.Min(v, d[a - 1, b] + 1f);
                            if (b > 0) v = Mathf.Min(v, d[a - 1, b - 1] + D2);
                            if (b < NX - 1) v = Mathf.Min(v, d[a - 1, b + 1] + D2);
                        }
                        if (b > 0) v = Mathf.Min(v, d[a, b - 1] + 1f);
                        d[a, b] = v;
                    }
                for (int a = NZ - 1; a >= 0; a--)
                    for (int b = NX - 1; b >= 0; b--)
                    {
                        float v = d[a, b];
                        if (a < NZ - 1)
                        {
                            v = Mathf.Min(v, d[a + 1, b] + 1f);
                            if (b > 0) v = Mathf.Min(v, d[a + 1, b - 1] + D2);
                            if (b < NX - 1) v = Mathf.Min(v, d[a + 1, b + 1] + D2);
                        }
                        if (b < NX - 1) v = Mathf.Min(v, d[a, b + 1] + 1f);
                        d[a, b] = v;
                    }
                return d;
            }

            private int Fila(float z) => Mathf.Clamp(Mathf.RoundToInt(z - Z0), 0, NZ - 1);
            private int Col(float x) => Mathf.Clamp(Mathf.RoundToInt(x - X0), 0, NX - 1);
            public bool Dentro(float x, float z) => x >= X0 && z >= Z0 && x <= X0 + NX - 1 && z <= Z0 + NZ - 1;

            /// Distancia con signo a la línea del agua: + en tierra, − en el agua. Fuera de la rejilla, lejos.
            public float Distancia(float x, float z) => Dentro(x, z) ? Dist[Fila(z), Col(x)] : 999f;
            public float Distancia(Vector2 p) => Distancia(p.x, p.y);
            public bool EnElAgua(Vector2 p) => Distancia(p) < 0f;

            /// Dirección del agua hacia la tierra (el gradiente de la distancia).
            public Vector2 Normal(Vector2 p)
            {
                int a = Fila(p.y), b = Col(p.x);
                float gx = Dist[a, Mathf.Min(b + 1, NX - 1)] - Dist[a, Mathf.Max(b - 1, 0)];
                float gz = Dist[Mathf.Min(a + 1, NZ - 1), b] - Dist[Mathf.Max(a - 1, 0), b];
                var n = new Vector2(gx, gz);
                return n.sqrMagnitude > 1e-8f ? n.normalized : Vector2.zero;
            }

            /// Cuánto da la cara la orilla al mirador: > 0,3 orilla del camino (despejada), &lt; −0,2 orilla de enfrente.
            public float Lado(Vector2 p)
            {
                Vector2 v = C.Def.Mirador - p;
                return v.sqrMagnitude < 1e-4f ? 0f : Vector2.Dot(Normal(p), v.normalized);
            }

            /// Parte del círculo que es agua.
            public float FraccionDeAgua(Vector2 p, float radio)
            {
                int n = 0, agua = 0, r = Mathf.CeilToInt(radio);
                for (int a = -r; a <= r; a++)
                    for (int b = -r; b <= r; b++)
                    {
                        if (a * a + b * b > radio * radio) continue;
                        n++;
                        if (Distancia(p.x + b, p.y + a) < 0f) agua++;
                    }
                return n == 0 ? 0f : agua / (float)n;
            }

            /// La llegada: donde la senda del paraje más se acerca al agua, se baja por la pendiente hasta la orilla.
            private void BuscarLlegada()
            {
                Vector2[] senda = null;
                foreach (Paraje pj in Parajes())
                    if (pj.Nombre == C.Def.ParajeDeLlegada && pj.Senda != null)
                    {
                        senda = new Vector2[pj.Senda.Length + 1];
                        System.Array.Copy(pj.Senda, senda, pj.Senda.Length);
                        senda[senda.Length - 1] = pj.Centro;
                    }
                if (senda == null) return;
                float mejor = float.MaxValue;
                for (int j = 0; j < senda.Length - 1; j++)
                {
                    float largo = Vector2.Distance(senda[j], senda[j + 1]);
                    for (float t = 0f; t <= largo; t += 0.5f)
                    {
                        Vector2 q = Vector2.Lerp(senda[j], senda[j + 1], largo < 1e-4f ? 0f : t / largo);
                        float d = Distancia(q);
                        if (d < mejor) { mejor = d; PuntoDeSenda = q; }
                    }
                }
                if (mejor > 20f) return;
                Vector2 p = PuntoDeSenda;
                for (int n = 0; n < 80 && Distancia(p) > 1.6f; n++) p -= Normal(p) * 0.5f;
                Llegada = p;
                HayLlegada = true;
            }

            /// El hito: la tierra seca (a 1,4–4 m del agua y 0,3 m o más por encima) más rodeada de agua.
            private void BuscarHito()
            {
                float mejor = 0.5f;
                for (int a = 0; a < NZ; a++)
                    for (int b = 0; b < NX; b++)
                    {
                        float d = Dist[a, b];
                        if (d < 1.4f || d > 4f || Suelo[a, b] - C.Cota < 0.3f) continue;
                        var p = new Vector2(X0 + b, Z0 + a);
                        if (HayLlegada && Vector2.Distance(p, Llegada) < ClaroDeLaLlegada + 6f) continue;
                        if (C.Pendiente(p.x, p.y) > 20f) continue;
                        float f = FraccionDeAgua(p, 9f) + 0.4f * FraccionDeAgua(p, 15f);
                        if (f > mejor) { mejor = f; Hito = p; HayHito = true; }
                    }
            }
        }

        /// Cada laguna medida sobre el terreno dado; las que no se pueden medir, con su motivo.
        private static List<(Laguna lg, Cuenca c, string error)> Medidas(Terrain terreno)
        {
            var r = new List<(Laguna, Cuenca, string)>();
            foreach (Laguna lg in Lagunas)
            {
                Cuenca c = Medir(terreno, lg, out string error);
                r.Add((lg, c, error));
            }
            return r;
        }

        // ── Zonas libres ─────────────────────────────────────────────────────────────────────

        /// El agua y su orilla (una muestra más, ~2,5 m) en rectángulos: no se coloca nada del resto del vestido.
        public static void Zonas(List<Zona> libres)
        {
            foreach (var (lg, c, _) in Medidas(TerrenoDelMundo()))
            {
                if (c == null) continue;
                foreach (RectInt q in Rectangulos(Ampliada(c, 1)))
                {
                    Rect m = EnMundo(c, q);
                    libres.Add(Zona.Rectangulo(PrefijoZona + lg.Nombre, m.xMin, m.yMin, m.xMax, m.yMax));
                }
            }
        }

        /// Si el punto (con su radio) cae en una zona libre que no es del agua.
        private static string OtraZonaLibre(Obra o, Vector2 p, float radio)
        {
            foreach (Zona z in o.Libres)
            {
                if (z.Nombre != null && z.Nombre.StartsWith(PrefijoZona)) continue;
                bool dentro = z.Medio == Vector2.zero
                    ? Vector2.Distance(p, z.Centro) < z.Radio + radio
                    : Mathf.Abs(p.x - z.Centro.x) < z.Medio.x + radio && Mathf.Abs(p.y - z.Centro.y) < z.Medio.y + radio;
                if (dentro) return z.Nombre;
            }
            return null;
        }

        // ── Retirada de lo que queda bajo el agua ────────────────────────────────────────────

        private const string MotivoHondo = "bajo el agua de la laguna (más de 1 m de fondo)";
        private const string MotivoTapado = "tapado por el agua de la laguna (planta pequeña, o mata cuya esfera cerraría el paso a nado)";

        /// Retira lo que quedaría bajo el agua: más de 1 m de fondo, o tapado del todo si es pequeño (6 de cada 10
        /// partes de su alto); las matas Tree07, con 0,2 m, porque su esfera de colisión cerraría el paso a nado. No
        /// retira nada con lógica de juego (MonoBehaviour): lo anota para revisarlo.
        public static void RetirarLoSumergido(Scene escena, Obra o)
        {
            foreach (var (lg, c, _) in Medidas(o.Terreno))
            {
                if (c == null) continue;
                Rect caja = c.Caja;
                int hondos = 0, tapados = 0;
                var pila = new Stack<Transform>();
                foreach (GameObject raiz in escena.GetRootGameObjects()) pila.Push(raiz.transform);
                while (pila.Count > 0)
                {
                    Transform t = pila.Pop();
                    if (EsDelVestido(t) || !t.gameObject.activeInHierarchy) continue;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    {
                        foreach (Transform h in t) pila.Push(h);
                        continue;
                    }
                    Vector3 p = t.position;
                    if (!caja.Contains(new Vector2(p.x, p.z))) continue;
                    int k = c.K(p.z), i = c.I(p.x);
                    if (!c.AguaEn(k, i) && !c.AguaEn(k + 1, i) && !c.AguaEn(k - 1, i) && !c.AguaEn(k, i + 1) && !c.AguaEn(k, i - 1)) continue;
                    float fondo = c.Profundidad(p.x, p.z);
                    if (fondo <= 0.2f) continue;
                    Bounds b = LimitesVisibles(t.gameObject);
                    if (b.size.x > 40f || b.size.z > 40f) continue;
                    GameObject fuente = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    bool mata = fuente != null && fuente.name.StartsWith("Tree07");
                    float umbral = mata ? 0.2f : Mathf.Min(1f, 0.6f * b.size.y);
                    if (fondo <= umbral) continue;
                    if (t.GetComponentInChildren<MonoBehaviour>(true) != null)
                    {
                        o.Informe.Add($"  ! «{t.name}» queda bajo {fondo:0.0} m de agua en {lg.Nombre} pero tiene lógica de juego: no se retira, revísalo a mano.");
                        continue;
                    }
                    bool hondo = fondo > 1f;
                    Retirar(escena, t, hondo ? MotivoHondo : MotivoTapado, o);
                    if (hondo) hondos++; else tapados++;
                }
                if (hondos + tapados > 0)
                    o.Informe.Add($"{lg.Nombre}: {hondos + tapados} objetos de la escena quedarían bajo el agua y se retiran ({hondos} con más de 1 m de fondo, {tapados} pequeños o matas); los de menos fondo se quedan como arboleda inundada.");
            }
        }

        // ── Suelo ────────────────────────────────────────────────────────────────────────────

        /// Fondo de tierra de bosque con algo de tierra pisada, barro en la orilla con el borde roto, la llegada de
        /// arena con su senda y, bajo el agua, sin hierba de detalle.
        public static void PintarSuelo(Lienzo l, List<string> informe)
        {
            foreach (var (lg, c, error) in Medidas(l.Terreno))
            {
                if (c == null) { informe.Add($"Agua — {lg.Nombre}: no se pinta su suelo ({error})."); continue; }
                var rb = new Ribera(c);
                int bosque = l.Capa("Capa5"), tierra = l.Capa(CapaTierra);
                if (bosque < 0 || tierra < 0) { informe.Add($"Agua — {lg.Nombre}: al terreno le falta la capa Capa5 o {CapaTierra}; no se pinta su suelo."); continue; }
                int semilla = lg.SemillaRuido;
                int fondo = 0, barro = 0;
                Rect caja = c.Caja;
                l.Recorrer(caja.xMin - 8f, caja.yMin - 8f, caja.xMax + 8f, caja.yMax + 8f, (i, k, x, z) =>
                {
                    float d = rb.Distancia(x, z);
                    if (d > 6f) return;
                    float sobre = l.Altura(x, z) - c.Cota;
                    if (d < 0f && sobre < 0f)
                    {
                        // Fondo: 70 % tierra de bosque y 30 % tierra pisada, con manchas.
                        float t = 0.2f + 0.25f * Ruido.Fbm(x, z, 7f, semilla + 11);
                        l.MezclarPar(i, k, bosque, tierra, 1f, t);
                        l.Duro[k, i] = true;
                        fondo++;
                        return;
                    }
                    // Barro de la orilla: banda de 2 a 4,5 m que no sube más de 0,6 m sobre el agua, con el borde roto.
                    float ancho = 2f + 2.5f * Ruido.Fbm(x, z, 9f, semilla + 12);
                    float s = (1f - Ruido.Suave(ancho * 0.35f, ancho, d)) * (1f - Ruido.Suave(0.35f, 0.6f, sobre)) * 0.85f;
                    if (Ruido.Fbm(x, z, 4f, semilla + 13) < 0.35f) s *= 0.45f;
                    if (s <= 0.02f) return;
                    l.MezclarPar(i, k, tierra, bosque, s, 0.35f + 0.3f * Ruido.Fbm(x, z, 6f, semilla + 14));
                    if (d < 1f && s > 0.45f) l.Duro[k, i] = true;
                    barro++;
                });

                string llegada = "";
                if (rb.HayLlegada)
                {
                    // Senda corta desde la de la Era hasta el agua y una playa de arena y barro donde termina.
                    PintarSenda(l, Curva(rb.PuntoDeSenda, rb.Llegada, 0.15f, semilla + 15, 6), 2f, semilla + 16, null, CapaTierra, CapaTierra, 0.8f);
                    PintarMancha(l, rb.Llegada, 3.6f, CapaArena, 0.7f, semilla + 17);
                    PintarMancha(l, rb.Llegada + rb.Normal(rb.Llegada) * 1.5f, 2.6f, CapaTierra, 0.45f, semilla + 18, duro: false);
                    llegada = $"; llegada de arena en ({rb.Llegada.x:0}, {rb.Llegada.y:0}) con su senda desde la de la {lg.ParajeDeLlegada}";
                }
                informe.Add($"Agua — {lg.Nombre}: suelo de fondo en {fondo} celdas y barro de orilla en {barro}{llegada}; sin hierba de detalle bajo el agua.");
            }
        }

        // ── Puesta ───────────────────────────────────────────────────────────────────────────

        public static void PonerLagunas(Obra o)
        {
            foreach (var (lg, c, error) in Medidas(o.Terreno))
            {
                if (c == null) { o.Informe.Add($"Agua — {lg.Nombre}: no se pone ({error})."); continue; }
                Transform grupo = Grupo(o.Raiz, "Agua — " + lg.Nombre);
                var rb = new Ribera(c);
                var cuenta = new SortedDictionary<string, int>();

                string lamina = PonerLamina(o, grupo, c);
                int cajas = PonerNado(o, grupo, c);
                int volumenes = PonerFondoNoCaminable(grupo, c, out string avisoNav);
                string ambiente = PonerAmbiente(o, grupo, c);

                var plan = new Composicion(o, rb, cuenta);
                plan.Componer(grupo);
                string ranas = PonerRanas(grupo, c, plan.Juncos);

                float maxFondo = c.Cota - c.Fondo;
                o.Informe.Add($"Agua — {lg.Nombre}: lámina a {c.Cota:0.00} m ({lg.Margen:0.0} m bajo el desborde, a {c.Desborde:0.00} m), " +
                              $"{c.Area:0} m² de agua y hasta {maxFondo:0.0} m de fondo. {lamina}");
                o.Informe.Add($"  Nado: {cajas} cajas en la capa Water con la cara de arriba en la cota. Navegación: {volumenes} volúmenes «Not Walkable» donde hay {FondoNoCaminable:0.0} m o más de fondo{avisoNav}.");
                var piezas = new List<string>();
                foreach (KeyValuePair<string, int> e in cuenta) piezas.Add($"{e.Value} {e.Key}");
                o.Informe.Add("  Ribera: " + (piezas.Count > 0 ? string.Join(", ", piezas) : "nada") + ".");
                if (rb.HayHito) o.Informe.Add($"  Hito: árbol azul en la punta de tierra de ({rb.Hito.x:0}, {rb.Hito.y:0}); la llegada desde la {lg.ParajeDeLlegada} mira hacia él.");
                o.Informe.Add($"  Ambiente: {ambiente}{ranas}");
                o.Informe.Add($"  Pendiente: vuelve a hornear el NavMesh (El Sendero ▸ Navegación ▸ Bakear solo la superficie caminable); hasta entonces el fondo de {lg.Nombre} sigue siendo caminable.");
            }
        }

        /// Lámina de agua: malla en rejilla a la cota sobre las muestras inundadas ampliadas 2 (la orilla de la malla
        /// queda bajo el terreno), con UV0 = xz / 60 en coordenadas de mundo para que el oleaje no tenga costuras.
        private static string PonerLamina(Obra o, Transform grupo, Cuenca c)
        {
            bool[,] m = Ampliada(c, 2);
            var centro = new Vector3(c.Caja.center.x, c.Cota, c.Caja.center.y);
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var normales = new List<Vector3>();
            var triangulos = new List<int>();
            var indice = new Dictionary<int, int>();
            int Vertice(int k, int i)
            {
                int clave = k * (c.NI + 1) + i;
                if (indice.TryGetValue(clave, out int v)) return v;
                float x = c.X(i), z = c.Z(k);
                v = vertices.Count;
                vertices.Add(new Vector3(x - centro.x, 0f, z - centro.z));
                uv.Add(new Vector2(x / 60f, z / 60f));
                normales.Add(Vector3.up);
                indice[clave] = v;
                return v;
            }
            for (int k = 0; k < c.NK - 1; k++)
                for (int i = 0; i < c.NI - 1; i++)
                {
                    if (!m[k, i] && !m[k + 1, i] && !m[k, i + 1] && !m[k + 1, i + 1]) continue;
                    int a = Vertice(k, i), b = Vertice(k, i + 1), d = Vertice(k + 1, i), e = Vertice(k + 1, i + 1);
                    triangulos.Add(a); triangulos.Add(d); triangulos.Add(e);
                    triangulos.Add(a); triangulos.Add(e); triangulos.Add(b);
                }
            var malla = new Mesh { name = "Lámina — " + c.Def.Nombre };
            malla.SetVertices(vertices);
            malla.SetNormals(normales);
            malla.SetUVs(0, uv);
            malla.SetTriangles(triangulos, 0);
            malla.RecalculateBounds();
            malla.RecalculateTangents();
            GuardarMallaGenerada(malla, "Lámina de la " + c.Def.Nombre);

            var go = new GameObject("Lámina de agua");
            go.transform.SetParent(grupo, false);
            go.transform.position = centro;
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var render = go.AddComponent<MeshRenderer>();
            render.shadowCastingMode = ShadowCastingMode.Off;
            render.receiveShadows = false;
            render.lightProbeUsage = LightProbeUsage.Off;
            Material agua = MaterialDelAgua();
            render.sharedMaterial = agua;
            o.SinObstaculo.Add(go.transform);
            return agua != null
                ? $"Lámina de {triangulos.Count / 3} triángulos con {ArchivoAguaDeLaLaguna}."
                : $"  ! falta {RutaAguaDeLago}: la lámina queda sin material.";
        }

        /// AguaLaguna.mat: copia de Water_Lake.mat (shader Water_Final, el que el minimapa reconoce como agua) con
        /// colores de agua de ribera: verde oliva medio transparente en la orilla (se ve el fondo de barro) y verde
        /// azulado hondo en el centro. Se crea una vez; lo que se retoque después en el Editor se conserva.
        private static Material MaterialDelAgua() => RecursoPersistente(ArchivoAguaDeLaLaguna, () =>
        {
            var origen = AssetDatabase.LoadAssetAtPath<Material>(RutaAguaDeLago);
            if (origen == null) return null;
            var m = new Material(origen) { name = "AguaLaguna" };
            if (m.HasProperty("_ShallowWaterColor")) m.SetColor("_ShallowWaterColor", new Color(0.22f, 0.30f, 0.20f, 0.45f));
            if (m.HasProperty("_DeepWaterColor")) m.SetColor("_DeepWaterColor", new Color(0.05f, 0.27f, 0.32f, 0.92f));
            if (m.HasProperty("_Depth")) m.SetFloat("_Depth", 2.2f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
            return m;
        });

        /// Cajas trigger en la capa Water (lo que busca PlayerSwimmingController) con la cara de arriba en la cota,
        /// sobre las muestras inundadas ampliadas 1 (cubren la orilla; en seco el jugador queda por encima y no nada).
        private static int PonerNado(Obra o, Transform grupo, Cuenca c)
        {
            var go = new GameObject("Agua para nadar");
            int capa = LayerMask.NameToLayer("Water");
            if (capa >= 0) go.layer = capa;
            go.transform.SetParent(grupo, false);
            float suelo = c.Fondo - 1f, alto = c.Cota - suelo;
            int n = 0;
            foreach (RectInt q in Rectangulos(Ampliada(c, 1)))
            {
                Rect m = EnMundo(c, q);
                var caja = go.AddComponent<BoxCollider>();
                caja.isTrigger = true;
                caja.center = new Vector3(m.center.x, suelo + alto * 0.5f, m.center.y);
                caja.size = new Vector3(m.width, alto, m.height);
                n++;
            }
            o.SinObstaculo.Add(go.transform);
            return n;
        }

        /// Volúmenes «Not Walkable» (área 1) donde el agua cubre FondoNoCaminable o más, en la capa Floor: la
        /// superficie de navegación (capa Floor) solo recoge los volúmenes de su máscara de capas.
        private static int PonerFondoNoCaminable(Transform grupo, Cuenca c, out string aviso)
        {
            aviso = "";
            var hondo = new bool[c.NK, c.NI];
            for (int k = 0; k < c.NK; k++)
                for (int i = 0; i < c.NI; i++)
                    hondo[k, i] = c.Agua[k, i] && c.Cota - c.Alto[k, i] >= FondoNoCaminable;
            var padre = new GameObject("Fondo no caminable");
            padre.transform.SetParent(grupo, false);
            int capa = LayerMask.NameToLayer("Floor");
            if (capa < 0) aviso = " (¡no hay capa Floor: la navegación no los verá!)";
            float suelo = c.Fondo - 1f, techo = c.Cota + 0.5f;
            int n = 0;
            foreach (RectInt q in Rectangulos(hondo))
            {
                Rect m = EnMundo(c, q);
                var go = new GameObject($"No caminable {++n}");
                if (capa >= 0) go.layer = capa;
                go.transform.SetParent(padre.transform, false);
                go.transform.position = new Vector3(m.center.x, (suelo + techo) * 0.5f, m.center.y);
                var v = go.AddComponent<NavMeshModifierVolume>();
                v.center = Vector3.zero;
                v.size = new Vector3(m.width, techo - suelo, m.height);
                v.area = 1;
            }
            return n;
        }

        /// Zona de ambiente con niebla baja (caja del agua + MargenDeAmbiente, sin llegar al Camino 2), niebla
        /// nocturna sobre el agua y zona sin arenas (caja del agua + MargenSinArenas).
        private static string PonerAmbiente(Obra o, Transform grupo, Cuenca c)
        {
            var partes = new List<string>();
            int capaZona = LayerMask.NameToLayer("AmbientZone");
            Rect caja = c.Caja;

            AmbientPreset preset = RecursoPersistente(ArchivoPresetDeLaLaguna, () =>
            {
                var p = ScriptableObject.CreateInstance<AmbientPreset>();
                p.name = "AmbientPreset_Laguna";
                p.forcesMist = true;
                p.changeMusic = false;
                p.controlAmbientLight = false;
                p.transitionDuration = 2.5f;
                p.description = "Laguna de la Era (vestido de MainWorld): niebla baja del clima mientras se está en la cuenca; no cambia la música ni la luz.";
                return p;
            });
            var zona = new GameObject("Ambiente de la laguna");
            if (capaZona >= 0) zona.layer = capaZona;
            zona.transform.SetParent(grupo, false);
            zona.transform.position = new Vector3(caja.center.x, c.Cota + 1f, caja.center.y);
            var cajaZona = zona.AddComponent<BoxCollider>();
            cajaZona.isTrigger = true;
            cajaZona.size = new Vector3(caja.width + 2f * MargenDeAmbiente, 10f, caja.height + 2f * MargenDeAmbiente);
            var ambiente = zona.AddComponent<AmbientZone>();
            var so = new SerializedObject(ambiente);
            SerializedProperty propiedad = so.FindProperty("ambientPreset");
            if (propiedad != null && preset != null)
            {
                propiedad.objectReferenceValue = preset;
                so.ApplyModifiedPropertiesWithoutUndo();
                partes.Add($"AmbientZone con {ArchivoPresetDeLaLaguna} (niebla baja del clima en la cuenca)");
            }
            else partes.Add("AmbientZone sin preset (¡revisa el campo ambientPreset!)");

            var niebla = new GameObject("Niebla nocturna sobre la laguna");
            niebla.transform.SetParent(grupo, false);
            niebla.transform.position = new Vector3(caja.center.x, c.Cota - 0.3f, caja.center.y);
            var nieblaNocturna = niebla.AddComponent<NieblaNocturna>();
            nieblaNocturna.tamano = new Vector3(caja.width + 6f, 1.2f, caja.height + 6f);
            nieblaNocturna.cantidad = Mathf.Clamp(Mathf.RoundToInt(c.Area / 90f), 12, 40);
            nieblaNocturna.opacidad = 0.26f;
            nieblaNocturna.material = AssetDatabase.LoadAssetAtPath<Material>(RutaNieblaBaja);
            partes.Add("niebla nocturna sobre el agua");

            var sinArenas = new GameObject("Sin arenas: laguna");
            if (capaZona >= 0) sinArenas.layer = capaZona;
            sinArenas.transform.SetParent(grupo, false);
            sinArenas.transform.position = new Vector3(caja.center.x, c.Cota + 5f, caja.center.y);
            var cajaSinArenas = sinArenas.AddComponent<BoxCollider>();
            cajaSinArenas.isTrigger = true;
            cajaSinArenas.size = new Vector3(caja.width + 2f * MargenSinArenas, 20f, caja.height + 2f * MargenSinArenas);
            sinArenas.AddComponent<ZonaSinArenas>();
            partes.Add($"ninguna arena de batalla a menos de {MargenSinArenas:0} m del agua");

            o.SinObstaculo.Add(zona.transform);
            o.SinObstaculo.Add(sinArenas.transform);
            return string.Join(", ", partes);
        }

        /// Ranas en el juncal: AudioSource 3D en bucle en el centro del juncal más tupido, por el grupo Ambience del
        /// mezclador. El clip se pasa una vez a mono y a Streaming (ningún otro sitio lo usa).
        private static string PonerRanas(Transform grupo, Cuenca c, List<Vector2> juncos)
        {
            // Primero el ajuste de importación (reimporta el clip) y después se carga.
            string importado = PrepararClipDeAmbiente(RutaRanas) ? " (clip pasado a mono y a Streaming)" : "";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RutaRanas);
            if (clip == null) return $", ! falta {RutaRanas}: sin ranas.";

            Vector2 sitio = new Vector2(c.Caja.center.x, c.Caja.center.y);
            int mejor = -1;
            foreach (Vector2 j in juncos)
            {
                int vecinos = 0;
                foreach (Vector2 q in juncos) if ((q - j).sqrMagnitude < 64f) vecinos++;
                if (vecinos > mejor) { mejor = vecinos; sitio = j; }
            }
            var go = new GameObject("Ranas del juncal");
            go.transform.SetParent(grupo, false);
            go.transform.position = new Vector3(sitio.x, c.Cota + 0.3f, sitio.y);
            var fuente = go.AddComponent<AudioSource>();
            fuente.clip = clip;
            fuente.loop = true;
            fuente.playOnAwake = true;
            fuente.spatialBlend = 1f;
            // Lineal: a 50 m ya no se oye (el logarítmico se queda sonando bajito en todo el mapa).
            fuente.rolloffMode = AudioRolloffMode.Linear;
            fuente.minDistance = 8f;
            fuente.maxDistance = 50f;
            fuente.volume = 0.6f;
            fuente.dopplerLevel = 0f;
            fuente.spread = 60f;
            fuente.priority = 200;
            AudioMixerGroup ambiente = GrupoDelMezclador();
            fuente.outputAudioMixerGroup = ambiente;
            return $", ranas en el juncal de ({sitio.x:0}, {sitio.y:0}){(ambiente == null ? " (¡sin grupo Ambience en el mezclador!)" : "")}{importado}.";
        }

        private static AudioMixerGroup GrupoDelMezclador()
        {
            var mezclador = AssetDatabase.LoadAssetAtPath<AudioMixer>(RutaMezclador);
            if (mezclador == null) return null;
            foreach (AudioMixerGroup g in mezclador.FindMatchingGroups(GrupoDeAmbiente))
                if (g.name == GrupoDeAmbiente) return g;
            return null;
        }

        /// Un bucle de ambiente 3D: mono (el 3D lo posiciona igual y ocupa la mitad) y en Streaming (no se
        /// descomprime entero en memoria). Devuelve true si ha cambiado algo.
        private static bool PrepararClipDeAmbiente(string ruta)
        {
            if (!(AssetImporter.GetAtPath(ruta) is AudioImporter importador)) return false;
            AudioImporterSampleSettings ajustes = importador.defaultSampleSettings;
            if (importador.forceToMono && ajustes.loadType == AudioClipLoadType.Streaming) return false;
            importador.forceToMono = true;
            ajustes.loadType = AudioClipLoadType.Streaming;
            importador.defaultSampleSettings = ajustes;
            importador.SaveAndReimport();
            return true;
        }

        // ── Composición de la ribera ─────────────────────────────────────────────────────────

        /// La ribera compuesta: primero lo que ordena la vista (hito, bosquetes de enfrente, peñas, troncos), luego
        /// los juncales y los nenúfares a su abrigo, y al final los detalles (setas al pie de los árboles, flores en
        /// la orilla despejada). Todo depende de la forma de la orilla y de la semilla de la laguna.
        private sealed class Composicion
        {
            private readonly Obra o;
            private readonly Ribera rb;
            private readonly Cuenca c;
            private readonly SortedDictionary<string, int> cuenta;
            private readonly Ruido.Dado dado;
            private readonly int semilla;
            /// Lo ya decidido que ocupa sitio (centro y radio): los juncos, las setas y las flores no se le meten.
            private readonly List<Vector3> ocupado = new();
            /// Troncos de los árboles de la ribera puestos (para las setas).
            private readonly List<Vector2> arboles = new();
            public readonly List<Vector2> Juncos = new();

            public Composicion(Obra obra, Ribera ribera, SortedDictionary<string, int> cuentas)
            {
                o = obra;
                rb = ribera;
                c = ribera.C;
                cuenta = cuentas;
                semilla = c.Def.SemillaRuido;
                dado = new Ruido.Dado(semilla);
            }

            public void Componer(Transform grupo)
            {
                PonerHito(Grupo(grupo, "Árboles de la orilla"));
                PonerBosquetes(Grupo(grupo, "Árboles de la orilla"));
                PonerRocas(Grupo(grupo, "Peñas y piedras"));
                PonerTroncos(Grupo(grupo, "Troncos caídos"));
                PlanearJuncos();
                PonerNenufares(Grupo(grupo, "Nenúfares"));
                PonerJuncos(Grupo(grupo, "Juncal"));
                PonerSetas(Grupo(grupo, "Setas"));
                PonerFlores(Grupo(grupo, "Flores de la orilla"));
            }

            private void Contar(string que, GameObject puesta)
            {
                if (puesta == null) return;
                cuenta.TryGetValue(que, out int n);
                cuenta[que] = n + 1;
            }

            private bool Libre(Vector2 p, float radio)
            {
                foreach (Vector3 q in ocupado)
                    if ((new Vector2(q.x, q.y) - p).sqrMagnitude < (radio + q.z) * (radio + q.z)) return false;
                return true;
            }

            private bool CercaDeLaLlegada(Vector2 p, float extra = 0f) => rb.HayLlegada && Vector2.Distance(p, rb.Llegada) < ClaroDeLaLlegada + extra;

            /// Coloca con Poner tras mirar las zonas libres que no son del agua (las del agua las pisa a propósito).
            private GameObject Colocar(Transform g, Pieza p, float radio)
            {
                string zona = OtraZonaLibre(o, p.Pos, radio);
                if (zona != null) { o.Descartar("zona que debe quedar libre: " + zona, p); return null; }
                p.IgnorarZonas = true;
                return Poner(o, g, p);
            }

            // Árboles

            /// Caja de las mallas del prefab en el espacio de su raíz (sin la posición de la raíz): su centro en planta
            /// es lo que la copa se aparta del pie (el pivote de los árboles FK está en la base del tronco).
            private static Bounds CajaDelPrefab(GameObject prefab)
            {
                bool hay = false;
                var caja = new Bounds();
                Vector3 raiz = prefab.transform.position;
                foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Bounds b = mf.sharedMesh.bounds;
                    Matrix4x4 m = mf.transform.localToWorldMatrix;
                    for (int e = 0; e < 8; e++)
                    {
                        Vector3 p = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, Esquina(e))) - raiz;
                        if (!hay) { caja = new Bounds(p, Vector3.zero); hay = true; }
                        else caja.Encapsulate(p);
                    }
                }
                return caja;
            }

            /// Árbol con el pie en «pie» y la copa inclinada hacia «haciaElAgua» (si el prefab tiene el tronco curvo).
            private GameObject Arbol(Transform g, string prefab, string nombre, Vector2 pie, Vector2 haciaElAgua, float alto)
            {
                GameObject asset = CargarPrefab(o, prefab);
                if (asset == null) return null;
                Bounds caja = CajaDelPrefab(asset);
                var desvio = new Vector2(caja.center.x, caja.center.z);
                float factor = caja.size.y > 0.01f ? alto / caja.size.y : 1f;
                float rumbo;
                Vector2 copa = pie;
                if (desvio.magnitude * factor > 0.15f && haciaElAgua.sqrMagnitude > 1e-4f)
                {
                    rumbo = Rumbo(haciaElAgua) - Rumbo(desvio);
                    copa = pie + haciaElAgua.normalized * desvio.magnitude * factor;
                }
                else rumbo = dado.Entre(0f, 360f);
                GameObject a = Colocar(g, new Pieza
                {
                    Prefab = prefab, Nombre = nombre, Pos = copa, Rumbo = rumbo, Tamano = alto, Medida = Medida.Alto,
                    Apoyo = Apoyo.Tronco, RadioTronco = 0.35f, DesnivelMax = 1.2f, PendienteMax = 30f, Holgura = 0.2f,
                }, 1.5f);
                if (a != null) arboles.Add(pie);
                return a;
            }

            /// El hito: árbol azul en la punta de tierra más rodeada de agua, con dos piedras a sus pies en la orilla.
            private void PonerHito(Transform g)
            {
                if (!rb.HayHito) return;
                Vector2 n = rb.Normal(rb.Hito);
                GameObject hito = Arbol(g, ArbolHito, "Árbol hito de la punta", rb.Hito, -n, 6.6f);
                Contar("árbol hito (azul) en la punta", hito);
                if (hito != null) ocupado.Add(new Vector3(rb.Hito.x, rb.Hito.y, 1f));
            }

            /// Bosquetes en la orilla de enfrente del camino (la que no le da la cara), separados 26 m o más y lejos de
            /// los árboles que ya había: 4, 4 y 2 árboles alargados 2:1 a lo largo de la orilla, en zigzag (uno junto
            /// al agua, el siguiente un par de metros más atrás) a 0,7–1 copa de distancia, el mayor (7,6 m) delante y
            /// los demás de 5,2–6,2 m; todos con la copa hacia el agua. Un color por bosquete: oliva el que queda
            /// detrás del hito, y turquesa y oliva alternos los demás.
            private void PonerBosquetes(Transform g)
            {
                var existentes = ArbolesQueHabia();
                var candidatos = new List<(float puntos, Vector2 p)>();
                for (int a = 0; a < rb.NZ; a++)
                    for (int b = 0; b < rb.NX; b++)
                    {
                        float d = rb.Dist[a, b];
                        if (d < 2.5f || d > 5f) continue;
                        var p = new Vector2(rb.X0 + b, rb.Z0 + a);
                        float sobre = rb.Suelo[a, b] - c.Cota;
                        if (sobre < 0.3f || sobre > 5f || c.Pendiente(p.x, p.y) > 22f) continue;
                        float lado = rb.Lado(p);
                        if (lado > -0.15f) continue;
                        if (CercaDeLaLlegada(p, 8f) || (rb.HayHito && Vector2.Distance(p, rb.Hito) < 12f)) continue;
                        bool apretado = false;
                        foreach (Vector2 e in existentes) if ((e - p).sqrMagnitude < 49f) { apretado = true; break; }
                        if (apretado) continue;
                        candidatos.Add((-lado + 0.6f * Ruido.Fbm(p.x, p.y, 20f, semilla + 1), p));
                    }
                candidatos.Sort((x, y) => y.puntos.CompareTo(x.puntos));
                var centros = new List<Vector2>();
                foreach (var (_, p) in candidatos)
                {
                    bool lejos = true;
                    foreach (Vector2 q in centros) if ((q - p).sqrMagnitude < 26f * 26f) { lejos = false; break; }
                    if (!lejos) continue;
                    centros.Add(p);
                    if (centros.Count >= ArbolesPorBosquete.Length) break;
                }
                int lejanos = 0;
                for (int gi = 0; gi < centros.Count; gi++)
                {
                    // Detrás del hito azul, oliva (contrasta con él); los demás alternan turquesa y oliva, así dos
                    // colores distintos no quedan juntos.
                    bool trasElHito = rb.HayHito && Vector2.Distance(centros[gi], rb.Hito) < 30f;
                    bool oliva = trasElHito || lejanos++ % 2 == 1;
                    string color = oliva ? "oliva" : "turquesa";
                    string[] prefabs = oliva ? ArbolesOliva : ArbolesTurquesa;
                    int n = ArbolesPorBosquete[gi], mayor = n / 2;
                    Vector2 centro = centros[gi], normal = rb.Normal(centro), tangente = new Vector2(-normal.y, normal.x);
                    Transform bosquete = Grupo(g, $"Bosquete {gi + 1} ({color})");
                    for (int j = 0; j < n; j++)
                    {
                        float aLoLargo = (j - (n - 1) * 0.5f) * dado.Entre(2.6f, 3.4f);
                        float retiro = j == mayor ? -0.4f : j % 2 == 0 ? dado.Entre(-0.3f, 0.6f) : dado.Entre(1.6f, 2.8f);
                        Vector2 pie = centro + tangente * aLoLargo + normal * retiro;
                        for (int t = 0; t < 12; t++)
                        {
                            float d = rb.Distancia(pie);
                            if (d < 2.2f) pie += rb.Normal(pie) * 0.5f;
                            else if (d > 6.5f) pie -= rb.Normal(pie) * 0.5f;
                            else break;
                        }
                        if (!Libre(pie, 2f) || c.Profundidad(pie.x, pie.y) > -0.25f) continue;
                        float alto = j == mayor ? 7.6f : dado.Entre(5.2f, 6.2f);
                        string prefab = j == mayor ? prefabs[0] : prefabs[dado.Indice(prefabs.Length)];
                        GameObject a = Arbol(bosquete, prefab, $"Árbol de la orilla ({color})", pie, -rb.Normal(pie), alto);
                        Contar($"árboles de orilla ({color})", a);
                        if (a != null) ocupado.Add(new Vector3(pie.x, pie.y, 1f));
                    }
                }
            }

            /// Pies de los árboles que ya había en la escena junto a la laguna (activos, fuera de lo generado; de
            /// cualquier pack: su prefab se llama Tree…).
            private List<Vector2> ArbolesQueHabia()
            {
                var r = new List<Vector2>();
                Rect zona = new Rect(c.Caja.xMin - 30f, c.Caja.yMin - 30f, c.Caja.width + 60f, c.Caja.height + 60f);
                var pila = new Stack<Transform>();
                foreach (GameObject raiz in o.Raiz.gameObject.scene.GetRootGameObjects()) pila.Push(raiz.transform);
                while (pila.Count > 0)
                {
                    Transform t = pila.Pop();
                    if (EsDelVestido(t) || !t.gameObject.activeInHierarchy) continue;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) { foreach (Transform h in t) pila.Push(h); continue; }
                    var p = new Vector2(t.position.x, t.position.z);
                    if (!zona.Contains(p)) continue;
                    string ruta = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                    if (ruta != null && System.IO.Path.GetFileNameWithoutExtension(ruta).IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) >= 0) r.Add(p);
                }
                return r;
            }

            // Piedras y troncos

            /// Peñas donde la orilla es empinada (más de 16° a 2,5 m tierra adentro), en grupos de 2 o 4 piedras de
            /// tamaños distintos a lo largo del agua, separados 14 m o más; dos piedras al pie del hito y dos losas
            /// para sentarse en la llegada, mirando al hito.
            private void PonerRocas(Transform g)
            {
                var candidatos = new List<(float puntos, Vector2 p)>();
                for (int a = 0; a < rb.NZ; a++)
                    for (int b = 0; b < rb.NX; b++)
                    {
                        float d = rb.Dist[a, b];
                        if (d < 0f || d > 1f) continue;
                        var p = new Vector2(rb.X0 + b, rb.Z0 + a);
                        Vector2 n = rb.Normal(p);
                        float pendiente = c.Pendiente(p.x + n.x * 2.5f, p.y + n.y * 2.5f);
                        if (pendiente < 16f || CercaDeLaLlegada(p)) continue;
                        candidatos.Add((pendiente + 8f * Ruido.Fbm(p.x, p.y, 12f, semilla + 9), p));
                    }
                candidatos.Sort((x, y) => y.puntos.CompareTo(x.puntos));
                var rocas = new List<Vector2>();
                foreach (var (_, p) in candidatos)
                {
                    bool lejos = true;
                    foreach (Vector2 q in rocas) if ((q - p).sqrMagnitude < 14f * 14f) { lejos = false; break; }
                    if (!lejos || !Libre(p, 1.5f)) continue;
                    rocas.Add(p);
                    if (rocas.Count >= 3) break;
                }
                foreach (Vector2 centro in rocas)
                {
                    Vector2 n = rb.Normal(centro), t = new Vector2(-n.y, n.x);
                    // Nunca tres piedras juntas (se leerían como un montón hecho a mano): dos o cuatro, una mayor, en
                    // fila a lo largo del agua con un palmo entre ellas, unas más metidas en el agua que otras.
                    int cuantas = dado.Siguiente() < 0.5f ? 2 : 4, mayor = (cuantas - 1) / 2;
                    var lados = new float[cuantas];
                    var huecos = new float[cuantas];
                    float largo = 0f;
                    for (int j = 0; j < cuantas; j++)
                    {
                        lados[j] = j == mayor ? dado.Entre(2f, 2.5f) : dado.Entre(1.1f, 1.6f);
                        huecos[j] = j < cuantas - 1 ? dado.Entre(0.25f, 0.6f) : 0f;
                        largo += lados[j] + huecos[j];
                    }
                    float s = -largo * 0.5f;
                    for (int j = 0; j < cuantas; j++)
                    {
                        Vector2 p = centro + t * (s + lados[j] * 0.5f) + n * dado.Entre(-0.8f, 0.6f);
                        s += lados[j] + huecos[j];
                        if (!Libre(p, lados[j] * 0.4f)) continue;
                        Contar("peñas de la orilla", Piedra(g, PiedrasDeOrilla[dado.Indice(PiedrasDeOrilla.Length)], "Peña de la orilla", p, lados[j]));
                    }
                }
                if (rb.HayHito)
                {
                    Vector2 n = rb.Normal(rb.Hito), t = new Vector2(-n.y, n.x);
                    foreach (float lado in new[] { -1f, 1f })
                    {
                        Vector2 p = rb.Hito - n * 1.6f + t * (lado * 1.8f);
                        if (Libre(p, 0.7f)) Contar("piedras al pie del hito", Piedra(g, PiedrasDeOrilla[dado.Indice(PiedrasDeOrilla.Length)], "Piedra al pie del hito", p, lado > 0f ? 1.8f : 1.2f));
                    }
                }
                if (rb.HayLlegada && rb.HayHito)
                {
                    Vector2 v = (rb.Hito - rb.Llegada).normalized, t = new Vector2(-v.y, v.x);
                    foreach (float lado in new[] { -1f, 1f })
                    {
                        Vector2 p = rb.Llegada - v * 1f + t * (lado * 1.7f);
                        if (rb.Distancia(p) < 0.6f) p -= v * 0.8f;
                        GameObject losa = Colocar(g, new Pieza
                        {
                            Prefab = Losas[lado < 0f ? 0 : 1], Nombre = "Losa para sentarse en la llegada", Pos = p, Rumbo = Rumbo(v) + dado.Entre(-15f, 15f),
                            Tamano = 1.5f, Medida = Medida.Lado, Hundir = 0.12f, DesnivelMax = 0.8f, Holgura = 0.1f,
                        }, 0.8f);
                        Contar("losas para sentarse en la llegada", losa);
                        if (losa != null) ocupado.Add(new Vector3(p.x, p.y, 0.9f));
                    }
                }
            }

            private GameObject Piedra(Transform g, string prefab, string nombre, Vector2 p, float lado)
            {
                GameObject piedra = Colocar(g, new Pieza
                {
                    Prefab = prefab, Nombre = nombre, Pos = p, Rumbo = dado.Entre(0f, 360f), Tamano = lado, Medida = Medida.Lado,
                    Apoyo = Apoyo.Fondo, Hundir = dado.Entre(0.2f, 0.35f) * lado * 0.5f, DesnivelMax = 1.6f, Holgura = 0.1f,
                }, lado * 0.5f);
                if (piedra != null) ocupado.Add(new Vector3(p.x, p.y, lado * 0.5f + 0.2f));
                return piedra;
            }

            /// Troncos caídos medio hundidos en el agua somera (0,1–0,6 m), donde hay juncal o un bosquete cerca, en la
            /// orilla de enfrente o en los flancos, separados 15 m o más, hasta seis: apuntan al agua desde la orilla,
            /// con la punta del agua más baja.
            private void PonerTroncos(Transform g)
            {
                var candidatos = new List<(float puntos, Vector2 p)>();
                for (int a = 0; a < rb.NZ; a++)
                    for (int b = 0; b < rb.NX; b++)
                    {
                        float d = rb.Dist[a, b];
                        if (d < -2.4f || d > -0.8f) continue;
                        var p = new Vector2(rb.X0 + b, rb.Z0 + a);
                        float fondo = c.Profundidad(p.x, p.y);
                        if (fondo < 0.1f || fondo > 0.6f || rb.Lado(p) > 0.3f) continue;
                        if (CercaDeLaLlegada(p, 3f) || (rb.HayHito && Vector2.Distance(p, rb.Hito) < 6f)) continue;
                        int cerca = 0;
                        foreach (Vector2 q in arboles) if ((q - p).sqrMagnitude < 81f) cerca += 3;
                        float juncal = Ruido.Suave(0.40f, 0.60f, Ruido.Fbm(p.x, p.y, 11f, semilla + 5));
                        float puntos = cerca + 4f * juncal;
                        if (puntos < 2f) continue;
                        candidatos.Add((puntos + 2f * Ruido.Fbm(p.x, p.y, 9f, semilla + 8), p));
                    }
                candidatos.Sort((x, y) => y.puntos.CompareTo(x.puntos));
                var puestos = new List<Vector2>();
                foreach (var (_, p) in candidatos)
                {
                    if (puestos.Count >= 6) break;
                    bool lejos = true;
                    foreach (Vector2 q in puestos) if ((q - p).sqrMagnitude < 15f * 15f) { lejos = false; break; }
                    if (!lejos || !Libre(p, 1.5f)) continue;
                    string prefab = Troncos[puestos.Count % Troncos.Length];
                    GameObject asset = CargarPrefab(o, prefab);
                    if (asset == null) break;
                    Bounds caja = CajaDelPrefab(asset);
                    // El largo del tronco va por su eje más largo en planta: ese eje apunta al agua.
                    float giroPropio = caja.size.x > caja.size.z ? 90f : 0f;
                    GameObject tronco = Colocar(g, new Pieza
                    {
                        Prefab = prefab, Nombre = "Tronco caído en el agua", Pos = p,
                        Rumbo = Rumbo(-rb.Normal(p)) + giroPropio + dado.Entre(-25f, 25f), Tamano = dado.Entre(2.3f, 2.8f), Medida = Medida.Lado,
                        Apoyo = Apoyo.Fondo, Hundir = 0.5f, Inclinar = giroPropio > 0f ? 0f : 7f, DesnivelMax = 1.5f, Holgura = 0.1f,
                    }, 1.4f);
                    Contar("troncos caídos medio hundidos", tronco);
                    if (tronco == null) continue;
                    puestos.Add(p);
                    ocupado.Add(new Vector3(p.x, p.y, 1.5f));
                }
            }

            // Juncal y nenúfares

            /// Juncos en el agua somera y en la orilla mojada (de 0,2 m por encima a 0,45 m de fondo), en manchas que
            /// marca el ruido: densos en la orilla de enfrente, menos en los flancos y casi nada en la orilla del
            /// camino; ni en la llegada ni al pie del hito. Se eligen en orden aleatorio (fijo por la semilla) y
            /// separados 1,15 m o más, hasta 175: así se reparten sin dibujar la rejilla.
            private void PlanearJuncos()
            {
                var candidatos = new List<(float orden, Vector2 p)>();
                for (int a = 0; a < rb.NZ; a++)
                    for (int b = 0; b < rb.NX; b++)
                    {
                        var p = new Vector2(rb.X0 + b + (Ruido.Hash(b, a, semilla + 3) - 0.5f) * 0.8f, rb.Z0 + a + (Ruido.Hash(a, b, semilla + 4) - 0.5f) * 0.8f);
                        if (rb.Distancia(p) > 2.5f) continue;
                        float fondo = c.Profundidad(p.x, p.y);
                        if (fondo < -0.2f || fondo > 0.45f) continue;
                        if (CercaDeLaLlegada(p) || (rb.HayHito && Vector2.Distance(p, rb.Hito) < 2.2f)) continue;
                        float lado = rb.Lado(p);
                        float base_ = lado < -0.2f ? 1f : lado <= 0.3f ? 0.7f : 0.12f;
                        float densidad = base_ * Ruido.Suave(0.40f, 0.60f, Ruido.Fbm(p.x, p.y, 11f, semilla + 5));
                        if (Ruido.Hash(b, a, semilla + 6) > densidad || !Libre(p, 0.5f)) continue;
                        candidatos.Add((Ruido.Hash(a, b, semilla + 2), p));
                    }
                candidatos.Sort((x, y) => x.orden.CompareTo(y.orden));
                foreach (var (_, p) in candidatos)
                {
                    bool junto = false;
                    foreach (Vector2 q in Juncos) if ((q - p).sqrMagnitude < 1.15f * 1.15f) { junto = true; break; }
                    if (junto) continue;
                    Juncos.Add(p);
                    if (Juncos.Count >= 175) return;
                }
            }

            private void PonerJuncos(Transform g)
            {
                foreach (Vector2 p in Juncos)
                {
                    float fondo = c.Profundidad(p.x, p.y);
                    bool bajo = fondo < -0.05f && dado.Siguiente() < 0.4f;
                    // En el agua, más altos; en la orilla de enfrente, los más altos de todos.
                    float alto = (fondo > 0.1f ? 1.7f : 1.35f) + (rb.Lado(p) < -0.2f ? 0.3f : 0f) + dado.Entre(-0.2f, 0.35f);
                    Pieza pieza = bajo
                        ? new Pieza { Prefab = Roseta, Nombre = "Juncia baja", Tamano = dado.Entre(1.2f, 1.7f) }
                        : new Pieza { Prefab = JuncosAltos[dado.Indice(JuncosAltos.Length)], Nombre = "Junco", Tamano = alto, Medida = Medida.Alto };
                    pieza.Pos = p;
                    pieza.Rumbo = dado.Entre(0f, 360f);
                    pieza.Apoyo = Apoyo.Fondo;
                    pieza.Hundir = 0.12f;
                    pieza.DesnivelMax = 1f;
                    pieza.Holgura = 0.05f;
                    pieza.PermitirSolapePropio = true;
                    pieza.SinObstaculo = true;
                    Contar("juncos", Colocar(g, pieza, 0.5f));
                }
            }

            /// Nenúfares en grupos de 4 a 6 en agua de 0,5–1,3 m con al menos cuatro juncos a 7 m (al abrigo del
            /// juncal), fuera de la orilla del camino, separados 13 m o más, hasta cinco grupos; uno de cada dos grupos
            /// lleva un nenúfar en flor. Flotan a ras de la lámina y sin colisión.
            private void PonerNenufares(Transform g)
            {
                var candidatos = new List<(float puntos, Vector2 p)>();
                for (int a = 0; a < rb.NZ; a++)
                    for (int b = 0; b < rb.NX; b++)
                    {
                        if (rb.Dist[a, b] >= -1f) continue;
                        var p = new Vector2(rb.X0 + b, rb.Z0 + a);
                        float fondo = c.Profundidad(p.x, p.y);
                        if (fondo < 0.5f || fondo > 1.3f || rb.Lado(p) > 0.3f || CercaDeLaLlegada(p)) continue;
                        int junto = 0;
                        foreach (Vector2 q in Juncos) if ((q - p).sqrMagnitude < 49f) junto++;
                        if (junto < 4) continue;
                        candidatos.Add((junto + 3f * Ruido.Fbm(p.x, p.y, 15f, semilla + 7), p));
                    }
                candidatos.Sort((x, y) => y.puntos.CompareTo(x.puntos));
                var grupos = new List<Vector2>();
                foreach (var (_, p) in candidatos)
                {
                    bool lejos = true;
                    foreach (Vector2 q in grupos) if ((q - p).sqrMagnitude < 13f * 13f) { lejos = false; break; }
                    if (!lejos || !Libre(p, 1f)) continue;
                    grupos.Add(p);
                    if (grupos.Count >= 5) break;
                }
                for (int gi = 0; gi < grupos.Count; gi++)
                {
                    int quiero = 4 + dado.Indice(3), puestos = 0;
                    var aqui = new List<Vector2>();
                    for (int t = 0; t < 40 && puestos < quiero; t++)
                    {
                        float ang = dado.Entre(0f, Mathf.PI * 2f), r = dado.Entre(0f, 2.6f);
                        Vector2 p = grupos[gi] + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                        float fondo = c.Profundidad(p.x, p.y);
                        if (fondo < 0.35f || fondo > 1.6f || !Libre(p, 0.4f)) continue;
                        bool junto = false;
                        foreach (Vector2 q in aqui) if ((q - p).sqrMagnitude < 0.81f) { junto = true; break; }
                        if (junto) continue;
                        bool flor = puestos == 0 && gi % 2 == 0;
                        Pieza pieza = flor
                            ? new Pieza { Prefab = NenufarEnFlor, Nombre = "Nenúfar en flor", Tamano = dado.Entre(0.3f, 0.36f), Hundir = 0.03f }
                            : new Pieza { Prefab = Roseta, Nombre = "Nenúfar", Tamano = dado.Entre(1.3f, 1.8f), Hundir = 0.1f };
                        pieza.Pos = p;
                        pieza.Rumbo = dado.Entre(0f, 360f);
                        pieza.Apoyo = Apoyo.Flotar;
                        pieza.CotaAgua = c.Cota;
                        pieza.Holgura = 0.02f;
                        pieza.PermitirSolapePropio = true;
                        pieza.SinObstaculo = true;
                        GameObject puesto = Colocar(g, pieza, 0.5f);
                        if (puesto == null) continue;
                        // Flotan: sin colisión (el parterre FK del que sale el nenúfar en flor trae MeshCollider).
                        foreach (Collider col in puesto.GetComponentsInChildren<Collider>(true))
                        {
                            col.enabled = false;
                            GuardarOverrides(col);
                        }
                        Contar(flor ? "nenúfares en flor" : "nenúfares", puesto);
                        aqui.Add(p);
                        puestos++;
                    }
                    foreach (Vector2 p in aqui) ocupado.Add(new Vector3(p.x, p.y, 0.45f));
                }
            }

            // Detalles

            /// Setas turquesa y azules en corros de 2 o 3 al pie de los árboles de la ribera, del lado de tierra y en
            /// seco (0,15 m o más sobre el agua).
            private void PonerSetas(Transform g)
            {
                var puestas = new List<Vector2>();
                for (int ai = 0; ai < arboles.Count; ai++)
                {
                    Vector2 pie = arboles[ai], n = rb.Normal(pie);
                    string[] familia = ai % 2 == 0 ? SetasTurquesa : SetasAzules;
                    int cuantas = 2 + dado.Indice(2);
                    for (int j = 0; j < cuantas; j++)
                    {
                        float ang = Mathf.Atan2(n.y, n.x) + dado.Entre(-1.2f, 1.2f), r = dado.Entre(1.25f, 2.4f);
                        Vector2 p = pie + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                        if (c.Profundidad(p.x, p.y) > -0.15f || !Libre(p, 0.2f)) continue;
                        bool junto = false;
                        foreach (Vector2 q in puestas) if ((q - p).sqrMagnitude < 0.25f) { junto = true; break; }
                        if (junto) continue;
                        GameObject seta = Colocar(g, new Pieza
                        {
                            Prefab = familia[dado.Indice(familia.Length)], Nombre = "Seta", Pos = p, Rumbo = dado.Entre(0f, 360f),
                            Tamano = dado.Entre(0.3f, 0.6f), Medida = Medida.Alto, Hundir = 0.03f, DesnivelMax = 0.6f, Holgura = 0.02f,
                            PermitirSolapePropio = true, SinObstaculo = true,
                        }, 0.2f);
                        Contar("setas", seta);
                        if (seta != null) puestas.Add(p);
                    }
                }
            }

            /// Manchas de flores azules y blancas en la orilla despejada (la del camino) y junto a la llegada: hasta
            /// cinco manchas alargadas a lo largo del agua, a 2–9 m de ella, separadas 13 m o más, cada una de 8 a 12
            /// flores con un color que manda.
            private void PonerFlores(Transform g)
            {
                var candidatos = new List<(float puntos, Vector2 p)>();
                for (int a = 0; a < rb.NZ; a++)
                    for (int b = 0; b < rb.NX; b++)
                    {
                        float d = rb.Dist[a, b];
                        if (d < 2f || d > 9f) continue;
                        var p = new Vector2(rb.X0 + b, rb.Z0 + a);
                        if (c.Pendiente(p.x, p.y) > 22f) continue;
                        float lado = rb.Lado(p);
                        bool llegada = rb.HayLlegada && Vector2.Distance(p, rb.Llegada) < 10f;
                        if (lado < 0.1f && !llegada) continue;
                        if (rb.HayLlegada && Vector2.Distance(p, rb.Llegada) < 3.5f) continue;
                        candidatos.Add((lado + Ruido.Fbm(p.x, p.y, 14f, semilla + 10), p));
                    }
                candidatos.Sort((x, y) => y.puntos.CompareTo(x.puntos));
                var manchas = new List<Vector2>();
                foreach (var (_, p) in candidatos)
                {
                    bool lejos = true;
                    foreach (Vector2 q in manchas) if ((q - p).sqrMagnitude < 13f * 13f) { lejos = false; break; }
                    if (!lejos) continue;
                    manchas.Add(p);
                    if (manchas.Count >= 5) break;
                }
                var puestas = new List<Vector2>();
                for (int mi = 0; mi < manchas.Count; mi++)
                {
                    Vector2 centro = manchas[mi], n = rb.Normal(centro), t = new Vector2(-n.y, n.x);
                    float azules = mi % 2 == 0 ? 0.7f : 0.35f;
                    int quedan = 8 + dado.Indice(5);
                    for (int intento = 0; intento < 60 && quedan > 0; intento++)
                    {
                        float u = dado.Entre(-1f, 1f), v = dado.Entre(-1f, 1f);
                        if (u * u + v * v > 1f) continue;
                        Vector2 p = centro + t * (u * 4.5f) + n * (v * 2f);
                        if (rb.Distancia(p) < 1f || !Libre(p, 0.2f)) continue;
                        bool junto = false;
                        foreach (Vector2 q in puestas) if ((q - p).sqrMagnitude < 0.3f) { junto = true; break; }
                        if (junto) continue;
                        bool azul = dado.Siguiente() < azules;
                        GameObject flor = Colocar(g, new Pieza
                        {
                            Prefab = azul ? FlorAzul : FlorBlanca, Nombre = azul ? "Flor azul" : "Flor blanca", Pos = p, Rumbo = dado.Entre(0f, 360f),
                            Tamano = dado.Entre(0.9f, 1.3f), Hundir = 0.02f, DesnivelMax = 0.6f, PendienteMax = 25f, Holgura = 0.02f,
                            PermitirSolapePropio = true, SinObstaculo = true,
                        }, 0.2f);
                        Contar("flores (azules y blancas)", flor);
                        if (flor == null) continue;
                        puestas.Add(p);
                        quedan--;
                    }
                }
            }
        }
    }
}
