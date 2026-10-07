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

    private static bool HayCopia() => File.Exists(Path.Combine(RutaCopias, ArchivoPesos));

    /// Guarda los pesos (cuantizados a bytes: el Terrain ya los guarda así, la copia es exacta) y la hierba de detalle.
    private static void GuardarCopia(TerrainData datos)
    {
        Directory.CreateDirectory(RutaCopias);
        int res = datos.alphamapResolution, capas = datos.alphamapLayers;
        float[,,] pesos = datos.GetAlphamaps(0, 0, res, res);
        using (var fs = File.Create(Path.Combine(RutaCopias, ArchivoPesos)))
        using (var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal))
        using (var w = new BinaryWriter(gz))
        {
            w.Write("VDMP1");
            w.Write(AssetDatabase.GetAssetPath(datos));
            w.Write(res);
            w.Write(capas);
            foreach (TerrainLayer t in datos.terrainLayers) w.Write(t != null ? t.name : "");
            var bytes = new byte[res * res * capas];
            int n = 0;
            for (int k = 0; k < res; k++)
                for (int i = 0; i < res; i++)
                    for (int c = 0; c < capas; c++)
                        bytes[n++] = (byte)Mathf.Clamp(Mathf.RoundToInt(pesos[k, i, c] * 255f), 0, 255);
            w.Write(bytes);
        }

        int dres = datos.detailResolution, dcapas = datos.detailPrototypes.Length;
        using (var fs = File.Create(Path.Combine(RutaCopias, ArchivoDetalle)))
        using (var gz = new GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal))
        using (var w = new BinaryWriter(gz))
        {
            w.Write("VDMD1");
            w.Write(dres);
            w.Write(dcapas);
            for (int c = 0; c < dcapas; c++)
            {
                int[,] capa = datos.GetDetailLayer(0, 0, dres, dres, c);
                for (int k = 0; k < dres; k++)
                    for (int i = 0; i < dres; i++)
                        w.Write(capa[k, i]);
            }
        }
    }

    /// Repone la copia. Devuelve un texto de error o null si todo ha ido bien.
    private static string ReponerCopia(TerrainData datos)
    {
        string rutaPesos = Path.Combine(RutaCopias, ArchivoPesos);
        if (!File.Exists(rutaPesos)) return "No hay copia del suelo en " + RutaCopias + ".";
        using (var fs = File.OpenRead(rutaPesos))
        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
        using (var r = new BinaryReader(gz))
        {
            if (r.ReadString() != "VDMP1") return "La copia del suelo tiene un formato desconocido.";
            string ruta = r.ReadString();
            int res = r.ReadInt32(), capas = r.ReadInt32();
            var nombres = new string[capas];
            for (int c = 0; c < capas; c++) nombres[c] = r.ReadString();
            if (ruta != AssetDatabase.GetAssetPath(datos)) return $"La copia es de otro terreno ({ruta}).";
            if (res != datos.alphamapResolution || capas != datos.alphamapLayers)
                return $"La copia ({res}², {capas} capas) no encaja con el terreno actual ({datos.alphamapResolution}², {datos.alphamapLayers} capas).";
            TerrainLayer[] actuales = datos.terrainLayers;
            for (int c = 0; c < capas; c++)
                if ((actuales[c] != null ? actuales[c].name : "") != nombres[c])
                    return $"El orden de capas del terreno ha cambiado desde la copia (capa {c}: «{nombres[c]}»).";
            byte[] bytes = r.ReadBytes(res * res * capas);
            var pesos = new float[res, res, capas];
            int n = 0;
            for (int k = 0; k < res; k++)
                for (int i = 0; i < res; i++)
                    for (int c = 0; c < capas; c++)
                        pesos[k, i, c] = bytes[n++] / 255f;
            datos.SetAlphamaps(0, 0, pesos);
        }

        string rutaDetalle = Path.Combine(RutaCopias, ArchivoDetalle);
        if (File.Exists(rutaDetalle))
        {
            using var fs = File.OpenRead(rutaDetalle);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var r = new BinaryReader(gz);
            if (r.ReadString() == "VDMD1")
            {
                int dres = r.ReadInt32(), dcapas = r.ReadInt32();
                if (dres == datos.detailResolution && dcapas == datos.detailPrototypes.Length)
                {
                    for (int c = 0; c < dcapas; c++)
                    {
                        var capa = new int[dres, dres];
                        for (int k = 0; k < dres; k++)
                            for (int i = 0; i < dres; i++)
                                capa[k, i] = r.ReadInt32();
                        datos.SetDetailLayer(0, 0, c, capa);
                    }
                }
                else Debug.LogWarning("[VestidoDelMundo] La copia de la hierba de detalle no encaja con el terreno actual; se deja la hierba como está.");
            }
        }
        EditorUtility.SetDirty(datos);
        return null;
    }

    /// Huella de los pesos actuales, para saber si alguien ha pintado a mano después del vestido.
    private static string HuellaPesos(TerrainData datos)
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
