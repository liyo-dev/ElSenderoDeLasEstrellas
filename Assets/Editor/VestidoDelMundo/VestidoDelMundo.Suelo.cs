using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Pintura de suelos del Terrain de MainWorld al estilo del pueblo de Will: alfombra de hierba con
/// textura (capas SueloPueblo), núcleo de tierra pisada orgánico, sendas que serpentean y plazas
/// de adoquín con el borde gastado. Antes de tocar nada guarda una copia exacta de los pesos y de
/// la hierba de detalle en _ClaudeBackups/VestidoDelMundo, y el menú de quitar la repone.
public static partial class VestidoDelMundo
{
    private const string CarpetaCopias = "_ClaudeBackups/VestidoDelMundo";
    private const string ArchivoPesos = "pesos_originales.bin.gz";
    private const string ArchivoDetalle = "detalle_original.bin.gz";
    private const string ArchivoEstado = "estado.txt";

    // Capas por nombre de TerrainLayer: el índice se busca en el TerrainData al empezar.
    private const string CapaHierba = "SueloPueblo_0";      // FK Terrain01, hierba con textura
    private const string CapaFlores = "SueloPueblo_1";      // FK Terrain02, hierba con florecillas
    private const string CapaTierraPiedras = "SueloPueblo_2"; // FK Terrain03, tierra con piedras
    private const string CapaTierra = "SueloPueblo_3";      // FK Terrain04, tierra pisada
    private const string CapaAdoquin = "SueloUrbano0";      // FK Ground03, adoquín
    private const string CapaBaldosa = "SueloUrbano3";      // FK Ground05, baldosa clara
    private const string CapaArena = "Capa0";               // arena de playa
    private const string CapaRoca = "Capa2";                // roca de talud

    /// Pesos del Terrain en memoria; se pinta aquí y se vuelca al final con SetAlphamaps.
    private sealed class Lienzo
    {
        public readonly Terrain Terreno;
        public readonly TerrainData Datos;
        public readonly int Res;
        public readonly int NumCapas;
        public readonly float[,,] Pesos;
        /// Celdas donde ha quedado suelo duro (calle, plaza, senda): ahí se quita la hierba de detalle.
        public readonly bool[,] Duro;
        private readonly Vector3 origen;
        private readonly Vector3 tamano;
        private readonly Dictionary<string, int> indices = new();

        public Lienzo(Terrain terreno)
        {
            Terreno = terreno;
            Datos = terreno.terrainData;
            Res = Datos.alphamapResolution;
            NumCapas = Datos.alphamapLayers;
            Pesos = Datos.GetAlphamaps(0, 0, Res, Res);
            Duro = new bool[Res, Res];
            origen = terreno.transform.position;
            tamano = Datos.size;
            TerrainLayer[] capas = Datos.terrainLayers;
            for (int i = 0; i < capas.Length; i++)
                if (capas[i] != null && !indices.ContainsKey(capas[i].name)) indices[capas[i].name] = i;
        }

        public int Capa(string nombre) => indices.TryGetValue(nombre, out int i) ? i : -1;

        public bool TieneCapas(IEnumerable<string> nombres, out string falta)
        {
            foreach (string n in nombres)
                if (Capa(n) < 0) { falta = n; return false; }
            falta = null;
            return true;
        }

        public float X(int i) => origen.x + i * tamano.x / (Res - 1);
        public float Z(int k) => origen.z + k * tamano.z / (Res - 1);
        public int I(float x) => Mathf.Clamp(Mathf.RoundToInt((x - origen.x) / tamano.x * (Res - 1)), 0, Res - 1);
        public int K(float z) => Mathf.Clamp(Mathf.RoundToInt((z - origen.z) / tamano.z * (Res - 1)), 0, Res - 1);

        public float Altura(float x, float z) => Terreno.SampleHeight(new Vector3(x, 0f, z)) + origen.y;

        public float Peso(float x, float z, int capa) => capa < 0 ? 0f : Pesos[K(z), I(x), capa];

        /// Mezcla convexa: lleva la celda hacia la capa indicada con fuerza s (0..1).
        public void Mezclar(int i, int k, int capa, float s, bool duro = false)
        {
            if (capa < 0 || s <= 0f) return;
            s = Mathf.Min(s, 1f);
            for (int c = 0; c < NumCapas; c++) Pesos[k, i, c] *= 1f - s;
            Pesos[k, i, capa] += s;
            if (duro && s >= 0.45f) Duro[k, i] = true;
        }

        /// Mezcla convexa hacia una combinación de dos capas: (1 - t) de «a» y t de «b».
        public void MezclarPar(int i, int k, int a, int b, float s, float t)
        {
            if (a < 0 || b < 0 || s <= 0f) return;
            s = Mathf.Min(s, 1f);
            for (int c = 0; c < NumCapas; c++) Pesos[k, i, c] *= 1f - s;
            Pesos[k, i, a] += s * (1f - t);
            Pesos[k, i, b] += s * t;
        }

        /// Recorre las celdas dentro de un rectángulo de mundo.
        public void Recorrer(float x0, float z0, float x1, float z1, Action<int, int, float, float> accion)
        {
            int i0 = I(Mathf.Min(x0, x1)) - 1, i1 = I(Mathf.Max(x0, x1)) + 1;
            int k0 = K(Mathf.Min(z0, z1)) - 1, k1 = K(Mathf.Max(z0, z1)) + 1;
            for (int k = Mathf.Max(0, k0); k <= Mathf.Min(Res - 1, k1); k++)
                for (int i = Mathf.Max(0, i0); i <= Mathf.Min(Res - 1, i1); i++)
                    accion(i, k, X(i), Z(k));
        }

        public void Normalizar()
        {
            for (int k = 0; k < Res; k++)
                for (int i = 0; i < Res; i++)
                {
                    float suma = 0f;
                    for (int c = 0; c < NumCapas; c++) suma += Pesos[k, i, c];
                    if (suma <= 1e-5f) continue;
                    for (int c = 0; c < NumCapas; c++) Pesos[k, i, c] /= suma;
                }
        }
    }

    // ── Pinceles ─────────────────────────────────────────────────────────────────────────────

    /// Máscara de celdas que un pincel no debe tocar (huertos, agua…).
    private delegate bool Mascara(float x, float z);

    /// Alfombra de hierba con textura que se funde con el prado de alrededor por un borde irregular.
    private static void PintarAlfombra(Lienzo l, Vector2 centro, float radio, int semilla, Mascara mascara, float borde = 16f, float floresUmbral = 0.64f)
    {
        int hierba = l.Capa(CapaHierba), flores = l.Capa(CapaFlores);
        float ext = radio * 1.25f + borde;
        l.Recorrer(centro.x - ext, centro.y - ext, centro.x + ext, centro.y + ext, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float d = Vector2.Distance(new Vector2(x, z), centro);
            float r = radio + (Ruido.Fbm(x, z, 45f, semilla) - 0.5f) * radio * 0.45f;
            float s = 1f - Ruido.Suave(r - borde, r, d);
            if (s <= 0f) return;
            l.Mezclar(i, k, Ruido.Fbm(x, z, 16f, semilla + 5) > floresUmbral ? flores : hierba, s);
        });
    }

    /// Núcleo de tierra pisada: unión de discos con borde roto, islas de hierba y moteado.
    private static void PintarNucleo(Lienzo l, IList<Vector3> discos, int semilla, Mascara mascara, float intensidad = 1f)
    {
        if (discos.Count == 0) return;
        int tierra = l.Capa(CapaTierra), piedras = l.Capa(CapaTierraPiedras);
        float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
        foreach (Vector3 d in discos)
        {
            x0 = Mathf.Min(x0, d.x - d.z); x1 = Mathf.Max(x1, d.x + d.z);
            z0 = Mathf.Min(z0, d.y - d.z); z1 = Mathf.Max(z1, d.y + d.z);
        }
        l.Recorrer(x0 - 8f, z0 - 8f, x1 + 8f, z1 + 8f, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float campo = -1f;
            foreach (Vector3 d in discos)
                campo = Mathf.Max(campo, 1f - Vector2.Distance(new Vector2(x, z), new Vector2(d.x, d.y)) / d.z);
            campo += (Ruido.Fbm(x, z, 11f, semilla) - 0.5f) * 0.55f;
            if (campo <= 0f) return;
            float s = Ruido.Suave(0f, 0.28f, campo) * intensidad;
            if (campo < 0.22f && Ruido.Hash(i, k, semilla + 77) < 0.45f) s *= 0.35f;
            if (Ruido.Fbm(x, z, 7f, semilla + 3) < 0.3f && campo < 0.6f) s *= 0.4f;
            l.Mezclar(i, k, Ruido.Fbm(x, z, 6f, semilla + 9) > 0.5f ? piedras : tierra, s, duro: true);
        });
    }

    /// Senda que serpentea: anchura variable, borde moteado y mezcla de tierra con y sin piedras.
    private static void PintarSenda(Lienzo l, Vector2[] puntos, float ancho, int semilla, Mascara mascara, string capaA = CapaTierraPiedras, string capaB = CapaTierra, float serpenteo = 1.6f)
    {
        if (puntos == null || puntos.Length < 2) return;
        int a = l.Capa(capaA), b = l.Capa(capaB);
        var p = new Vector2[puntos.Length];
        for (int j = 0; j < puntos.Length; j++)
        {
            Vector2 q = puntos[j];
            if (j > 0 && j < puntos.Length - 1)
            {
                Vector2 d = puntos[j + 1] - puntos[j - 1];
                float l2 = Mathf.Max(d.magnitude, 1e-4f);
                float desvio = (Ruido.Fbm(q.x, q.y, 23f, semilla) - 0.5f) * ancho * serpenteo;
                q += new Vector2(-d.y, d.x) / l2 * desvio;
            }
            p[j] = q;
        }
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector2 q in p)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y);
        }
        float m = ancho * 2f;
        l.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float d = DistanciaAPolilinea(new Vector2(x, z), p);
            float r = ancho * 0.5f * (0.75f + 0.6f * Ruido.Fbm(x, z, 8f, semilla + 1));
            if (d > r + 2.5f) return;
            float s = 1f - Ruido.Suave(r * 0.55f, r + 2.5f, d);
            if (d > r && Ruido.Hash(i, k, semilla + 5) < 0.5f) s *= 0.4f;
            l.Mezclar(i, k, Ruido.Fbm(x, z, 5f, semilla + 2) > 0.45f ? a : b, s, duro: true);
        });
    }

    /// Plaza rectangular (girada «grados» alrededor de su centro) con marco gastado de adoquín y tierra.
    private static void PintarPlaza(Lienzo l, Vector2 centro, Vector2 tamano, float grados, int semilla, string capaInterior, float marco = 3f, Mascara mascara = null, string capaMarco = CapaAdoquin)
    {
        int interior = l.Capa(capaInterior), adoquin = l.Capa(capaMarco), piedras = l.Capa(CapaTierraPiedras);
        float rad = grados * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
        Vector2 media = tamano * 0.5f;
        float ext = media.magnitude + marco + 4f;
        l.Recorrer(centro.x - ext, centro.y - ext, centro.x + ext, centro.y + ext, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float dx = x - centro.x, dz = z - centro.y;
            float lx = dx * cs - dz * sn, lz = dx * sn + dz * cs;
            float fx = Mathf.Max(Mathf.Abs(lx) - media.x, 0f), fz = Mathf.Max(Mathf.Abs(lz) - media.y, 0f);
            float fuera = Mathf.Sqrt(fx * fx + fz * fz);
            float dentro = Mathf.Min(media.x - Mathf.Abs(lx), media.y - Mathf.Abs(lz));
            float n = (Ruido.Fbm(x, z, 5f, semilla) - 0.5f) * 3.5f;
            if (fuera <= 0f && dentro > 1.2f + n) l.Mezclar(i, k, interior, 1f, duro: true);
            else if (fuera < marco + n)
            {
                float s = Mathf.Max(0.35f, 1f - Ruido.Suave(0f, marco + n, fuera));
                l.Mezclar(i, k, Ruido.Hash(i, k, semilla) < 0.45f ? adoquin : piedras, s, duro: true);
            }
        });
    }

    /// Calle empedrada: adoquín en el centro, tierra con piedras en los bordes y alguna calva de tierra.
    private static void PintarCalle(Lienzo l, Vector2[] puntos, float ancho, int semilla, Mascara mascara)
    {
        int adoquin = l.Capa(CapaAdoquin), piedras = l.Capa(CapaTierraPiedras), tierra = l.Capa(CapaTierra);
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector2 q in puntos)
        {
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x);
            minZ = Mathf.Min(minZ, q.y); maxZ = Mathf.Max(maxZ, q.y);
        }
        float m = ancho + 4f;
        l.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float d = DistanciaAPolilinea(new Vector2(x, z), puntos);
            float r = ancho * 0.5f * (0.85f + 0.3f * Ruido.Fbm(x, z, 9f, semilla));
            if (d > r + 2.2f) return;
            if (d <= r * 0.85f)
            {
                bool calva = Ruido.Fbm(x, z, 6f, semilla + 4) < 0.22f;
                l.Mezclar(i, k, calva ? tierra : adoquin, calva ? 0.7f : 1f, duro: true);
            }
            else
            {
                float s = 1f - Ruido.Suave(r * 0.85f, r + 2.2f, d);
                if (Ruido.Hash(i, k, semilla + 2) < 0.4f) s *= 0.5f;
                l.Mezclar(i, k, Ruido.Hash(i, k, semilla + 8) < 0.5f ? adoquin : piedras, s, duro: true);
            }
        });
    }

    /// Mancha suelta (patio, era, suelo de ruina): disco irregular de la capa dada.
    private static void PintarMancha(Lienzo l, Vector2 centro, float radio, string capa, float fuerza, int semilla, Mascara mascara = null, bool duro = true)
    {
        int c = l.Capa(capa);
        l.Recorrer(centro.x - radio * 1.6f, centro.y - radio * 1.6f, centro.x + radio * 1.6f, centro.y + radio * 1.6f, (i, k, x, z) =>
        {
            if (mascara != null && mascara(x, z)) return;
            float d = Vector2.Distance(new Vector2(x, z), centro);
            float r = radio * (0.7f + 0.6f * Ruido.Fbm(x, z, radio * 0.8f + 2f, semilla));
            if (d > r) return;
            float s = fuerza * (1f - Ruido.Suave(r * 0.45f, r, d));
            if (Ruido.Hash(i, k, semilla + 3) < 0.25f) s *= 0.5f;
            l.Mezclar(i, k, c, s, duro);
        });
    }

    // ── Copia de seguridad del suelo ─────────────────────────────────────────────────────────

    private static string RutaCopias => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", CarpetaCopias);

    /// Original: el suelo de antes del primer vestido (no se reescribe; «Quitar» vuelve a él).
    /// Base: el suelo sobre el que se repinta cuando alguien eligió conservar lo pintado a mano.
    /// Vestido: el suelo tal como lo dejó la última pintura (para saber qué se ha retocado a mano).
    private enum Copia { Original, Base, Vestido }

    private static string ArchivoPesosDe(Copia c) =>
        c == Copia.Original ? ArchivoPesos : c == Copia.Base ? "pesos_base.bin.gz" : "pesos_vestido.bin.gz";

    private static string ArchivoDetalleDe(Copia c) =>
        c == Copia.Original ? ArchivoDetalle : c == Copia.Base ? "detalle_base.bin.gz" : "detalle_vestido.bin.gz";

    private static bool HayCopia(Copia c = Copia.Original) => File.Exists(Path.Combine(RutaCopias, ArchivoPesosDe(c)));

    private static void BorrarCopia(Copia c)
    {
        foreach (string a in new[] { ArchivoPesosDe(c), ArchivoDetalleDe(c) })
        {
            string r = Path.Combine(RutaCopias, a);
            if (File.Exists(r)) File.Delete(r);
        }
    }

    /// El suelo en bytes: pesos cuantizados (el Terrain ya los guarda así, la copia es exacta) y hierba de detalle.
    private sealed class Instantanea
    {
        public string Ruta;
        public int Res, Capas;
        public string[] Nombres;
        /// Pesos [k, i, c] aplanados.
        public byte[] Pesos;
        public int DRes;
        /// Hierba de detalle por capa, [k * DRes + i]. Null si no hay copia de la hierba.
        public int[][] Detalle;

        public bool Encaja(Instantanea o) =>
            o.Ruta == Ruta && o.Res == Res && o.Capas == Capas && string.Join("|", o.Nombres) == string.Join("|", Nombres);
    }

    private static Instantanea Tomar(TerrainData datos)
    {
        int res = datos.alphamapResolution, capas = datos.alphamapLayers;
        float[,,] pesos = datos.GetAlphamaps(0, 0, res, res);
        var s = new Instantanea
        {
            Ruta = AssetDatabase.GetAssetPath(datos), Res = res, Capas = capas,
            Nombres = new string[capas], Pesos = new byte[res * res * capas], DRes = datos.detailResolution,
        };
        TerrainLayer[] capasTerreno = datos.terrainLayers;
        for (int c = 0; c < capas; c++) s.Nombres[c] = capasTerreno[c] != null ? capasTerreno[c].name : "";
        int n = 0;
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
                for (int c = 0; c < capas; c++)
                    s.Pesos[n++] = (byte)Mathf.Clamp(Mathf.RoundToInt(pesos[k, i, c] * 255f), 0, 255);
        s.Detalle = new int[datos.detailPrototypes.Length][];
        for (int c = 0; c < s.Detalle.Length; c++)
        {
            int[,] capa = datos.GetDetailLayer(0, 0, s.DRes, s.DRes, c);
            var plano = new int[s.DRes * s.DRes];
            for (int k = 0; k < s.DRes; k++)
                for (int i = 0; i < s.DRes; i++)
                    plano[k * s.DRes + i] = capa[k, i];
            s.Detalle[c] = plano;
        }
        return s;
    }

    private static void Escribir(Instantanea s, Copia copia)
    {
        Directory.CreateDirectory(RutaCopias);
        EscribirComprimido(Path.Combine(RutaCopias, ArchivoPesosDe(copia)), w =>
        {
            w.Write("VDMP1");
            w.Write(s.Ruta);
            w.Write(s.Res);
            w.Write(s.Capas);
            foreach (string nombre in s.Nombres) w.Write(nombre);
            w.Write(s.Pesos);
        });
        if (s.Detalle == null) return;
        EscribirComprimido(Path.Combine(RutaCopias, ArchivoDetalleDe(copia)), w =>
        {
            w.Write("VDMD1");
            w.Write(s.DRes);
            w.Write(s.Detalle.Length);
            foreach (int[] capa in s.Detalle)
                foreach (int v in capa) w.Write(v);
        });
    }

    /// Escribe primero a un temporal y lo pone en su sitio al terminar: si algo falla a mitad, la copia
    /// anterior sigue entera.
    private static void EscribirComprimido(string ruta, Action<BinaryWriter> escribir)
    {
        string temporal = ruta + ".tmp";
        using (var fs = File.Create(temporal))
        using (var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal))
        using (var w = new BinaryWriter(gz))
            escribir(w);
        if (File.Exists(ruta)) File.Delete(ruta);
        File.Move(temporal, ruta);
    }

    private static Instantanea Leer(Copia copia, out string error)
    {
        error = null;
        string rutaPesos = Path.Combine(RutaCopias, ArchivoPesosDe(copia));
        if (!File.Exists(rutaPesos)) { error = "No hay copia del suelo en " + RutaCopias + "."; return null; }
        var s = new Instantanea();
        using (var fs = File.OpenRead(rutaPesos))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var r = new BinaryReader(gz))
        {
            if (r.ReadString() != "VDMP1") { error = "La copia del suelo tiene un formato desconocido."; return null; }
            s.Ruta = r.ReadString();
            s.Res = r.ReadInt32();
            s.Capas = r.ReadInt32();
            s.Nombres = new string[s.Capas];
            for (int c = 0; c < s.Capas; c++) s.Nombres[c] = r.ReadString();
            s.Pesos = r.ReadBytes(s.Res * s.Res * s.Capas);
            if (s.Pesos.Length != s.Res * s.Res * s.Capas) { error = "La copia del suelo está incompleta."; return null; }
        }

        string rutaDetalle = Path.Combine(RutaCopias, ArchivoDetalleDe(copia));
        if (!File.Exists(rutaDetalle)) return s;
        using (var fs = File.OpenRead(rutaDetalle))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var r = new BinaryReader(gz))
        {
            if (r.ReadString() != "VDMD1") return s;
            s.DRes = r.ReadInt32();
            s.Detalle = new int[r.ReadInt32()][];
            for (int c = 0; c < s.Detalle.Length; c++)
            {
                var capa = new int[s.DRes * s.DRes];
                for (int t = 0; t < capa.Length; t++) capa[t] = r.ReadInt32();
                s.Detalle[c] = capa;
            }
        }
        return s;
    }

    /// Pone en el terreno una copia leída. Devuelve un texto de error o null si todo ha ido bien.
    private static string Aplicar(TerrainData datos, Instantanea s)
    {
        if (s.Ruta != AssetDatabase.GetAssetPath(datos)) return $"La copia es de otro terreno ({s.Ruta}).";
        if (s.Res != datos.alphamapResolution || s.Capas != datos.alphamapLayers)
            return $"La copia ({s.Res}², {s.Capas} capas) no encaja con el terreno actual ({datos.alphamapResolution}², {datos.alphamapLayers} capas).";
        TerrainLayer[] actuales = datos.terrainLayers;
        for (int c = 0; c < s.Capas; c++)
            if ((actuales[c] != null ? actuales[c].name : "") != s.Nombres[c])
                return $"El orden de capas del terreno ha cambiado desde la copia (capa {c}: «{s.Nombres[c]}»).";

        var pesos = new float[s.Res, s.Res, s.Capas];
        int n = 0;
        for (int k = 0; k < s.Res; k++)
            for (int i = 0; i < s.Res; i++)
                for (int c = 0; c < s.Capas; c++)
                    pesos[k, i, c] = s.Pesos[n++] / 255f;
        datos.SetAlphamaps(0, 0, pesos);

        if (s.Detalle != null)
        {
            if (s.DRes == datos.detailResolution && s.Detalle.Length == datos.detailPrototypes.Length)
                for (int c = 0; c < s.Detalle.Length; c++)
                {
                    var capa = new int[s.DRes, s.DRes];
                    for (int k = 0; k < s.DRes; k++)
                        for (int i = 0; i < s.DRes; i++)
                            capa[k, i] = s.Detalle[c][k * s.DRes + i];
                    datos.SetDetailLayer(0, 0, c, capa);
                }
            else Debug.LogWarning("[VestidoDelMundo] La copia de la hierba de detalle no encaja con el terreno actual; se deja la hierba como está.");
        }
        EditorUtility.SetDirty(datos);
        return null;
    }

    private static void GuardarCopia(TerrainData datos, Copia copia = Copia.Original) => Escribir(Tomar(datos), copia);

    /// Repone una copia. Devuelve un texto de error o null si todo ha ido bien.
    private static string ReponerCopia(TerrainData datos, Copia copia = Copia.Original)
    {
        Instantanea s = Leer(copia, out string error);
        return s == null ? error : Aplicar(datos, s);
    }

    /// Guarda la base para repintar conservando lo retocado a mano: en las celdas donde el suelo actual ya no
    /// es el que dejó el vestido se queda lo actual; en las demás, el suelo sobre el que se pintó. Así el
    /// vestido no se aplica dos veces. Devuelve un texto de error o null.
    private static string GuardarBaseConRetoques(TerrainData datos, out int retocadas)
    {
        retocadas = 0;
        Instantanea actual = Tomar(datos);
        Instantanea debajo = Leer(HayCopia(Copia.Base) ? Copia.Base : Copia.Original, out string error);
        if (debajo == null) return error;
        Instantanea vestido = Leer(Copia.Vestido, out _);
        if (vestido == null || !vestido.Encaja(actual) || !debajo.Encaja(actual))
        {
            Escribir(actual, Copia.Base);
            retocadas = -1;
            return null;
        }

        int capas = actual.Capas;
        for (int o = 0; o < actual.Pesos.Length; o += capas)
        {
            bool tocada = false;
            for (int c = 0; c < capas && !tocada; c++) tocada = actual.Pesos[o + c] != vestido.Pesos[o + c];
            if (tocada) retocadas++;
            else Array.Copy(debajo.Pesos, o, actual.Pesos, o, capas);
        }
        if (vestido.Detalle != null && debajo.Detalle != null && actual.Detalle != null &&
            vestido.DRes == actual.DRes && debajo.DRes == actual.DRes &&
            vestido.Detalle.Length == actual.Detalle.Length && debajo.Detalle.Length == actual.Detalle.Length)
            for (int c = 0; c < actual.Detalle.Length; c++)
                for (int t = 0; t < actual.Detalle[c].Length; t++)
                    if (actual.Detalle[c][t] == vestido.Detalle[c][t]) actual.Detalle[c][t] = debajo.Detalle[c][t];
        Escribir(actual, Copia.Base);
        return null;
    }

    /// Huella del suelo: pesos de las capas y hierba de detalle. Si cambia, alguien ha pintado a mano.
    private static string HuellaSuelo(TerrainData datos)
    {
        int res = datos.alphamapResolution, capas = datos.alphamapLayers;
        float[,,] p = datos.GetAlphamaps(0, 0, res, res);
        uint h = 2166136261u;
        unchecked
        {
            for (int k = 0; k < res; k++)
                for (int i = 0; i < res; i++)
                    for (int c = 0; c < capas; c++)
                    {
                        h ^= (byte)Mathf.Clamp(Mathf.RoundToInt(p[k, i, c] * 255f), 0, 255);
                        h *= 16777619u;
                    }
            int dres = datos.detailResolution;
            for (int c = 0; c < datos.detailPrototypes.Length; c++)
            {
                int[,] capa = datos.GetDetailLayer(0, 0, dres, dres, c);
                for (int k = 0; k < dres; k++)
                    for (int i = 0; i < dres; i++)
                    {
                        h ^= (uint)capa[k, i];
                        h *= 16777619u;
                    }
            }
        }
        return h.ToString("x8");
    }

    private static string LeerEstado()
    {
        string r = Path.Combine(RutaCopias, ArchivoEstado);
        return File.Exists(r) ? File.ReadAllText(r, Encoding.UTF8).Trim() : "";
    }

    private static void EscribirEstado(string huella)
    {
        Directory.CreateDirectory(RutaCopias);
        File.WriteAllText(Path.Combine(RutaCopias, ArchivoEstado), huella, Encoding.UTF8);
    }

    /// Quita la hierba de detalle donde el lienzo ha dejado suelo duro (calles, plazas, sendas).
    private static int QuitarHierbaDeSueloDuro(Lienzo l)
    {
        TerrainData datos = l.Datos;
        int dres = datos.detailResolution, capas = datos.detailPrototypes.Length;
        if (dres <= 0 || capas == 0) return 0;
        int quitadas = 0;
        for (int c = 0; c < capas; c++)
        {
            int[,] capa = datos.GetDetailLayer(0, 0, dres, dres, c);
            bool cambiado = false;
            for (int k = 0; k < dres; k++)
            {
                int ak = Mathf.Clamp(Mathf.RoundToInt(k / (float)(dres - 1) * (l.Res - 1)), 0, l.Res - 1);
                for (int i = 0; i < dres; i++)
                {
                    if (capa[k, i] == 0) continue;
                    int ai = Mathf.Clamp(Mathf.RoundToInt(i / (float)(dres - 1) * (l.Res - 1)), 0, l.Res - 1);
                    if (!l.Duro[ak, ai]) continue;
                    capa[k, i] = 0;
                    cambiado = true;
                    quitadas++;
                }
            }
            if (cambiado) datos.SetDetailLayer(0, 0, c, capa);
        }
        return quitadas;
    }
}
