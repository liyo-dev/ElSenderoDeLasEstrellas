using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Kits.Waterfalls.Editor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Nivel de la montaña de Risco (diseño en GDD § 7.4): el camino que sube en zetas por la cara sur de la sierra,
/// sobre la ciudad, con el derrumbe que dejó la explosión del capítulo 5, la senda alta de repisas y la del pinar,
/// el refugio, el puente sobre el tajo, la galería que cruza el collado, el pueblo de Risco detrás de la sierra (calle
/// alta, plaza y terrazas que bajan hacia el mar: VestidoDelMundo.Risco.cs), el montacargas de cuerda de los
/// pescadores que baja por la garganta hasta la cala, la cueva del río que vuelve de la cala al nacimiento del río del bosque y la aldea de Vega, río
/// abajo.
///
/// · El relieve se rehace en cada vestido desde una copia exacta de las alturas originales
///   (_ClaudeBackups/VestidoDelMundo/alturas_originales.bin.gz); «quitar el vestido» la repone.
/// · Todo sale de las tablas de este archivo: para retocar el nivel se cambian las tablas y se vuelve a vestir.
public static partial class VestidoDelMundo
{
    // ── Tablas del nivel ─────────────────────────────────────────────────────────────────────

    /// Camino de Risco: arranca en la explanada al norte de la ciudad y sube en zetas por la cara de la sierra.
    private static readonly Vector2 ArranqueDelCamino = new Vector2(-30f, 388f);
    private const float RumboDeLasPiernas = 80f, LargoDePierna = 36f, SeparacionDePiernas = 9f, PendienteDelCamino = 0.2f;
    private const int PiernasDelCamino = 3;
    private const float AnchoDelCamino = 6f, TaludDelCamino = 5f;
    /// El derrumbe corta la segunda pierna en su mitad: hueco de este largo, con dos peñas para cruzarlo saltando.
    private const int PiernaDelDerrumbe = 1;
    private const float HuecoDelDerrumbe = 9f;

    /// Pueblo de Risco: plaza y calle alta; debajo de la plaza, las terrazas (VestidoDelMundo.Risco.cs).
    /// La calle alta queda a la cota de la salida de la galería (un tramo de escalera por debajo del puente) y la
    /// plaza, tres tramos de escalones (6 m) más abajo.
    private static readonly Vector2 CentroDeLaPlaza = new Vector2(-10f, 456f);
    private const float MedioLargoDeLaPlaza = 24f, MedioFondoDeLaPlaza = 5f, DeLaCalleALaPlaza = 6f;
    private static readonly Vector2 CentroDeLaCalleAlta = new Vector2(-8f, 447f);
    private const float MedioLargoDeLaCalleAlta = 22f, MedioFondoDeLaCalleAlta = 3.5f;

    /// Garganta que baja de la plaza a la cala, con el montacargas en su cabecera.
    private static readonly Vector3[] Garganta =
    {
        new Vector3(-42f, 2.6f, 458f), new Vector3(-50f, 2.3f, 467f), new Vector3(-60f, 2.0f, 476f),
        new Vector3(-70f, 1.7f, 485f), new Vector3(-80f, 1.4f, 494f),
    };
    private const float AnchoDeLaGarganta = 14f;
    private static readonly Vector2 CentroDeLaCala = new Vector2(-82f, 491f);
    private const float CotaDeLaCala = 1.2f;

    /// Cueva del río: de la cabecera del río del bosque a la garganta de la cala.
    private static readonly Vector2 BocaDeLaCuevaDelRio = new Vector2(-165f, 236f), HaciaLaCala = new Vector2(-63f, 469f);
    private const float SueloDeLaCuevaAlta = 43f, SueloDeLaCuevaBaja = 3f;
    /// Cabecera del río del bosque (primer punto de su cinta): el agua de la cueva sale hasta aquí.
    private static readonly Vector2 NacimientoDelRio = new Vector2(-165f, 211f);

    // ── Relieve ──────────────────────────────────────────────────────────────────────────────

    private const string ArchivoAlturas = "alturas_originales.bin.gz";
    private const string ArchivoHuellaDelRelieve = "alturas_vestido.txt";

    /// Cambio de altura (m) que ha dejado el relieve en cada muestra del heightmap; null si no se ha esculpido.
    private static float[,] s_cambioDelRelieve;

    private sealed class Relieve
    {
        public float[,] H;          // alturas del mundo en metros, [k (z), i (x)]
        public int Res;
        public Vector3 Origen, Medida;
        public float Paso => Medida.x / (Res - 1);
        public float X(int i) => Origen.x + i * Paso;
        public float Z(int k) => Origen.z + k * Medida.z / (Res - 1);

        public float Altura(float x, float z)
        {
            float fi = Mathf.Clamp((x - Origen.x) / Paso, 0f, Res - 1.001f), fk = Mathf.Clamp((z - Origen.z) / (Medida.z / (Res - 1)), 0f, Res - 1.001f);
            int i = (int)fi, k = (int)fk;
            float a = fi - i, b = fk - k;
            return Mathf.Lerp(Mathf.Lerp(H[k, i], H[k, i + 1], a), Mathf.Lerp(H[k + 1, i], H[k + 1, i + 1], a), b);
        }

        public void Recorrer(float x0, float z0, float x1, float z1, Action<int, int, float, float> accion)
        {
            int i0 = Mathf.Max(0, Mathf.FloorToInt((x0 - Origen.x) / Paso)), i1 = Mathf.Min(Res - 1, Mathf.CeilToInt((x1 - Origen.x) / Paso));
            float pasoZ = Medida.z / (Res - 1);
            int k0 = Mathf.Max(0, Mathf.FloorToInt((z0 - Origen.z) / pasoZ)), k1 = Mathf.Min(Res - 1, Mathf.CeilToInt((z1 - Origen.z) / pasoZ));
            for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                    accion(i, k, X(i), Z(k));
        }
    }

    /// Muralla de la capital (con margen): el relieve no la toca.
    private static bool BajoLaMuralla(float x, float z) => x > -110f && x < 119f && z > 230f && z < 357f;

    /// Franja llana a la cota de un recorrido 3D (x, y, z): en el centro, la cota del recorrido; en el talud se funde
    /// con lo que haya. «hueco» deja sin tocar el tramo del recorrido (en metros desde su inicio) que devuelve true.
    private static void Franja(Relieve r, IList<Vector3> puntos, float ancho, float talud, Func<float, bool> hueco = null)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector3 p in puntos) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
        float m = ancho * 0.5f + talud + r.Paso;
        var acumulado = new float[puntos.Count];
        for (int j = 1; j < puntos.Count; j++)
            acumulado[j] = acumulado[j - 1] + Vector2.Distance(new Vector2(puntos[j - 1].x, puntos[j - 1].z), new Vector2(puntos[j].x, puntos[j].z));
        r.Recorrer(minX - m, minZ - m, maxX + m, maxZ + m, (i, k, x, z) =>
        {
            if (BajoLaMuralla(x, z)) return;
            float mejor = float.MaxValue, cota = 0f, s = 0f;
            var p = new Vector2(x, z);
            for (int j = 0; j + 1 < puntos.Count; j++)
            {
                var a = new Vector2(puntos[j].x, puntos[j].z);
                var b = new Vector2(puntos[j + 1].x, puntos[j + 1].z);
                Vector2 ab = b - a;
                float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d = Vector2.Distance(p, a + ab * t);
                if (d < mejor) { mejor = d; cota = Mathf.Lerp(puntos[j].y, puntos[j + 1].y, t); s = acumulado[j] + t * Mathf.Sqrt(l2); }
            }
            if (hueco != null && hueco(s)) return;
            float w = mejor <= ancho * 0.5f ? 1f : Mathf.Clamp01(1f - (mejor - ancho * 0.5f) / talud);
            w = w * w * (3f - 2f * w);
            if (w > 0f) r.H[k, i] = Mathf.Lerp(r.H[k, i], cota, w);
        });
    }

    /// Rellano llano a la cota «y», fundido con lo que haya en «talud» metros. «forma» 2 es una elipse; cuanto más
    /// alta, más se parece a un rectángulo de esquinas redondeadas.
    private static void Rellano(Relieve r, Vector2 c, float rx, float rz, float y, float talud, float forma = 2f)
    {
        float m = Mathf.Max(rx, rz) + talud + r.Paso;
        r.Recorrer(c.x - m, c.y - m, c.x + m, c.y + m, (i, k, x, z) =>
        {
            if (BajoLaMuralla(x, z)) return;
            float dx = Mathf.Abs(x - c.x) / rx, dz = Mathf.Abs(z - c.y) / rz;
            float q = Mathf.Pow(Mathf.Pow(dx, forma) + Mathf.Pow(dz, forma), 1f / forma);
            // Distancia al borde: en la elipse, aproximada por el radio menor; en las formas casi rectangulares, la del
            // rectángulo (si no, el talud se alargaría por los lados largos).
            float d = forma > 2f ? Mathf.Max(Mathf.Abs(x - c.x) - rx, Mathf.Abs(z - c.y) - rz) : (q - 1f) * Mathf.Min(rx, rz);
            if (q <= 1f) d = Mathf.Min(d, 0f);
            float w = d <= 0f ? 1f : Mathf.Clamp01(1f - d / talud);
            w = w * w * (3f - 2f * w);
            if (w > 0f) r.H[k, i] = Mathf.Lerp(r.H[k, i], y, w);
        });
    }

    /// Recorrido del camino en zetas: piernas de LargoDePierna hacia RumboDeLasPiernas (alternando el sentido), apiladas
    /// hacia el norte cada SeparacionDePiernas, con giros de media vuelta. La cota del arranque es la del terreno
    /// original. También devuelve, para cada pierna, la distancia desde el inicio donde empieza y donde acaba.
    private static List<Vector3> RecorridoDelCamino(Func<float, float, float> alturaOriginal, out List<(float desde, float hasta)> piernas)
    {
        piernas = new List<(float, float)>();
        float a = RumboDeLasPiernas * Mathf.Deg2Rad;
        var u = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        var v = new Vector2(u.y, -u.x);
        if (v.y < 0f) v = -v;
        Vector2 p = ArranqueDelCamino;
        float y = alturaOriginal(p.x, p.y), s = 0f, sentido = 1f;
        var puntos = new List<Vector3> { new Vector3(p.x, y, p.y) };
        for (int n = 0; n < PiernasDelCamino; n++)
        {
            float desde = s;
            Vector2 q = p + u * (sentido * LargoDePierna);
            y += PendienteDelCamino * LargoDePierna;
            s += LargoDePierna;
            puntos.Add(new Vector3(q.x, y, q.y));
            piernas.Add((desde, s));
            p = q;
            if (n == PiernasDelCamino - 1) break;
            Vector2 centro = p + v * (SeparacionDePiernas * 0.5f);
            const int Pasos = 5;
            float arco = Mathf.PI * SeparacionDePiernas * 0.5f / Pasos;
            for (int k = 1; k <= Pasos; k++)
            {
                float ang = Mathf.PI * k / Pasos;
                Vector2 g = centro - v * (SeparacionDePiernas * 0.5f * Mathf.Cos(ang)) + u * (sentido * SeparacionDePiernas * 0.5f * Mathf.Sin(ang));
                y += PendienteDelCamino * 0.6f * arco;
                s += arco;
                puntos.Add(new Vector3(g.x, y, g.y));
            }
            p = new Vector2(puntos[puntos.Count - 1].x, puntos[puntos.Count - 1].z);
            sentido = -sentido;
        }
        return puntos;
    }

    /// Punto del recorrido a «s» metros del inicio.
    private static Vector3 PuntoDelRecorrido(IList<Vector3> puntos, float s)
    {
        for (int j = 0; j + 1 < puntos.Count; j++)
        {
            float l = Vector2.Distance(new Vector2(puntos[j].x, puntos[j].z), new Vector2(puntos[j + 1].x, puntos[j + 1].z));
            if (s <= l || j == puntos.Count - 2) return Vector3.Lerp(puntos[j], puntos[j + 1], Mathf.Clamp01(s / Mathf.Max(l, 1e-4f)));
            s -= l;
        }
        return puntos[puntos.Count - 1];
    }

    private static (float desde, float hasta) TramoDelDerrumbe(List<(float desde, float hasta)> piernas)
    {
        var (a, b) = piernas[PiernaDelDerrumbe];
        float medio = (a + b) * 0.5f;
        return (medio - HuecoDelDerrumbe * 0.5f, medio + HuecoDelDerrumbe * 0.5f);
    }

    /// Lado del recorrido hacia donde baja la ladera (el sur): ahí cae quien falla el salto del derrumbe.
    private static readonly Vector2 LaderaAbajo = new Vector2(0f, -1f);

    /// Fin del camino en zetas: rellano del refugio, al pie del collado.
    private static Vector3 FinDelCamino(IList<Vector3> camino) => camino[camino.Count - 1];

    /// Del refugio sale el puente hacia el este, sobre el tajo, hasta la boca de la galería del collado.
    private const float LuzDelPuente = 16f;
    private static Vector3 CabezaDelPuente(Vector3 fin) => new Vector3(fin.x + 6.5f, fin.y, fin.z + 0.5f);
    private static Vector2 BocaDeLaGaleria(Vector3 fin) => new Vector2(fin.x + 6.5f + LuzDelPuente + 1.6f, fin.z + 0.5f);
    /// Galería: boca, túnel, un tramo de escalera que baja, túnel y salida en el extremo este de la calle alta.
    private static readonly char[] PiezasDeLaGaleria = { 'B', 'T', 'E', 'T', 'B' };
    private static float CotaDeLaCalleAlta(Vector3 fin) => fin.y - SubeLaEscalera;
    private static float CotaDeLaPlaza(Vector3 fin) => CotaDeLaCalleAlta(fin) - DeLaCalleALaPlaza;
    private static Vector2 SalidaDeLaGaleria => new Vector2(CentroDeLaCalleAlta.x + MedioLargoDeLaCalleAlta - 2f, CentroDeLaCalleAlta.y - 1f);

    /// Senda alta: desde el giro de la segunda a la tercera pierna, dos repisas suben a una cornisa que va por encima
    /// de la tercera pierna (subiendo un poco más que ella) y otras dos bajan al refugio. La del pinar es la tercera
    /// pierna, con pinos y un claro en medio.
    private const float CornisaAlEmpezar = 3.5f, CornisaAlAcabar = 4f, DetrasDeLaPierna = 7.5f;

    private static float LargoDeLaGaleria() => LargoDeLasPiezas(PiezasDeLaGaleria);

    static partial void DarFormaAlRelieve(Terrain terreno, List<string> informe)
    {
        TerrainData datos = terreno.terrainData;
        int res = datos.heightmapResolution;
        float[,] actual = datos.GetHeights(0, 0, res, res);
        string rutaOriginal = Path.Combine(RutaCopias, ArchivoAlturas), rutaHuella = Path.Combine(RutaCopias, ArchivoHuellaDelRelieve);
        float[,] baseNormalizada;
        if (!File.Exists(rutaOriginal))
        {
            Directory.CreateDirectory(RutaCopias);
            EscribirAlturas(rutaOriginal, actual);
            baseNormalizada = actual;
            informe.Add("Relieve: copia de las alturas originales guardada en " + CarpetaCopias + ".");
        }
        else
        {
            baseNormalizada = LeerAlturas(rutaOriginal, res);
            if (baseNormalizada == null)
            {
                informe.Add("Relieve: la copia de las alturas no encaja con el terreno; no se esculpe la montaña.");
                return;
            }
            string huellaActual = HuellaDeAlturas(actual);
            string huellaVestido = File.Exists(rutaHuella) ? File.ReadAllText(rutaHuella).Trim() : "";
            if (huellaActual != huellaVestido && huellaActual != HuellaDeAlturas(baseNormalizada))
                informe.Add("Relieve: AVISO, las alturas han cambiado a mano desde el último vestido; se rehace desde la copia original y esos retoques se pierden.");
        }

        var r = new Relieve { Res = res, Origen = terreno.transform.position, Medida = datos.size, H = new float[res, res] };
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
                r.H[k, i] = r.Origen.y + baseNormalizada[k, i] * r.Medida.y;
        var original = (float[,])r.H.Clone();
        float AlturaOriginal(float x, float z) => r.Altura(x, z);

        EsculpirLaMontana(r, AlturaOriginal);

        var nueva = new float[res, res];
        s_cambioDelRelieve = new float[res, res];
        int tocadas = 0;
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
            {
                nueva[k, i] = Mathf.Clamp01((r.H[k, i] - r.Origen.y) / r.Medida.y);
                s_cambioDelRelieve[k, i] = r.H[k, i] - original[k, i];
                if (Mathf.Abs(s_cambioDelRelieve[k, i]) > 0.05f) tocadas++;
            }
        datos.SetHeights(0, 0, nueva);
        terreno.Flush();
        EditorUtility.SetDirty(datos);
        File.WriteAllText(rutaHuella, HuellaDeAlturas(datos.GetHeights(0, 0, res, res)));
        informe.Add($"Relieve: montaña de Risco esculpida ({tocadas} muestras del heightmap cambiadas).");
    }

    static partial void ReponerElRelieve(TerrainData datos, List<string> informe)
    {
        string rutaOriginal = Path.Combine(RutaCopias, ArchivoAlturas);
        if (!File.Exists(rutaOriginal)) { informe.Add("Relieve: no había copia de las alturas, no se toca."); return; }
        float[,] h = LeerAlturas(rutaOriginal, datos.heightmapResolution);
        if (h == null) { informe.Add("Relieve: la copia de las alturas no encaja con el terreno; no se repone."); return; }
        datos.SetHeights(0, 0, h);
        EditorUtility.SetDirty(datos);
        File.Delete(rutaOriginal);
        string rutaHuella = Path.Combine(RutaCopias, ArchivoHuellaDelRelieve);
        if (File.Exists(rutaHuella)) File.Delete(rutaHuella);
        s_cambioDelRelieve = null;
        informe.Add("Relieve: repuestas las alturas originales.");
    }

    /// Todo el relieve del nivel, en orden: lo que va después manda sobre lo de antes.
    private static void EsculpirLaMontana(Relieve r, Func<float, float, float> alturaOriginal)
    {
        // El macizo del norte primero: el camino, la garganta y la cala lo cortan después.
        LevantarElMacizo(r);
        TallarLaSierra(r);
        // Camino en zetas, sin el hueco del derrumbe.
        List<Vector3> camino = RecorridoDelCamino(alturaOriginal, out var piernas);
        var (h0, h1) = TramoDelDerrumbe(piernas);
        Franja(r, camino, AnchoDelCamino, TaludDelCamino, s => s > h0 && s < h1);
        // Muesca del derrumbe: la ladera se ha llevado el camino hasta la pierna de abajo.
        Vector3 medio = PuntoDelRecorrido(camino, (h0 + h1) * 0.5f);
        float abajo = medio.y - PendienteDelCamino * LargoDePierna;
        Franja(r, new[]
        {
            new Vector3(medio.x, medio.y - 2.5f, medio.z),
            new Vector3(medio.x + LaderaAbajo.x * SeparacionDePiernas * 0.7f, abajo + 0.5f, medio.z + LaderaAbajo.y * SeparacionDePiernas * 0.7f),
        }, HuecoDelDerrumbe - 2f, 2.5f);
        // La pierna de abajo, otra vez llana bajo la muesca (los cascotes caen sobre ella).
        Franja(r, camino.GetRange(0, 2), AnchoDelCamino, 2f);

        Vector3 fin = FinDelCamino(camino);
        Rellano(r, new Vector2(fin.x, fin.z), 7f, 6f, fin.y, 4f);
        // Claro del pinar, en medio de la tercera pierna.
        var (p0, p1) = piernas[piernas.Count - 1];
        Vector3 claro = PuntoDelRecorrido(camino, (p0 + p1) * 0.5f);
        Rellano(r, new Vector2(claro.x, claro.z), 6.5f, 5.5f, claro.y, 3f);
        // Cornisa de la senda alta, por detrás (al norte) de la tercera pierna.
        Franja(r, Cornisa(camino, piernas), 3.5f, 3f);
        // Tajo bajo el puente: una grieta honda de norte a sur.
        Vector3 cabeza = CabezaDelPuente(fin);
        float xTajo = cabeza.x + LuzDelPuente * 0.5f;
        Franja(r, new[] { new Vector3(xTajo, cabeza.y - 20f, cabeza.z - 9f), new Vector3(xTajo, cabeza.y - 30f, cabeza.z + 16f) }, 4f, 3.5f);
        Rellano(r, new Vector2(cabeza.x - 0.5f, cabeza.z), 3f, 3.5f, cabeza.y, 2.5f);
        Rellano(r, BocaDeLaGaleria(fin), 3f, 3.5f, cabeza.y, 2.5f);

        // Risco: calle alta, garganta, plaza (la plaza al final: su borde manda sobre la garganta) y la cala.
        float cotaCalle = CotaDeLaCalleAlta(fin), cotaPlaza = CotaDeLaPlaza(fin);
        Rellano(r, CentroDeLaCalleAlta, MedioLargoDeLaCalleAlta, MedioFondoDeLaCalleAlta, cotaCalle, 3f, FormaDeCalle);
        Rellano(r, PlazaDelEmbalse, 7f, 5f, cotaCalle, 2.5f);
        Franja(r, RampaDeLaCalleAlta(cotaPlaza), 3.2f, 1f);
        Franja(r, Garganta, AnchoDeLaGarganta, 10f);
        Rellano(r, CentroDeLaPlaza, MedioLargoDeLaPlaza, MedioFondoDeLaPlaza, cotaPlaza, 3f, FormaDeCalle);
        Franja(r, RecorridoDeLaSendaDelCollado(cotaPlaza), 4f, 4f);
        Franja(r, RecorridoDeLaSendaDeLaCumbre(), 3f, 3f);
        EsculpirElPuebloEnTerrazas(r, cotaPlaza);
        Rellano(r, CentroDeLaCala, 11f, 7f, CotaDeLaCala, 10f);
        // Vega: un rellano para cada casa y cada huerta, a la altura que ya tenía el terreno en su centro.
        foreach (var (prefab, pos, rumbo) in CasasDeVega) Rellano(r, pos, 5.5f, 5.5f, alturaOriginal(pos.x, pos.y), 5f);
        foreach (Vector2 campo in CamposDeVega) Rellano(r, campo, 7f, 6f, alturaOriginal(campo.x, campo.y), 5f);
        // Nacimiento de la cascada: una loma tapa la trasera de la boca y un rellano sostiene el mirador.
        Vector2 adentroDelArroyo = -HaciaAbajoDelArroyo.normalized;
        Rellano(r, NacimientoDelArroyo + adentroDelArroyo * 9f, 5f, 5f, CotaDelNacimiento + 7f, 6f);
        Vector2 ladoDelArroyo = new Vector2(adentroDelArroyo.y, -adentroDelArroyo.x);
        Vector2 sitioDelMirador = NacimientoDelArroyo + ladoDelArroyo * 9f + adentroDelArroyo * 2f;
        Rellano(r, sitioDelMirador, 4.5f, 4.5f, alturaOriginal(sitioDelMirador.x, sitioDelMirador.y), 3f);
        // Delante de las dos bocas de la cueva del río, el suelo a la cota de la cueva.
        Vector2 dirCueva = (HaciaLaCala - BocaDeLaCuevaDelRio).normalized;
        Rellano(r, BocaDeLaCuevaDelRio - dirCueva * 3f, 4f, 4f, SueloDeLaCuevaAlta, 4f);
        Rellano(r, BocaDeLaCuevaDelRio + dirCueva * (LargoDeLaCuevaDelRio + 2.5f), 3f, 3f, SueloDeLaCuevaBaja - 0.2f, 3f);
        EsculpirElRio(r, alturaOriginal);
    }

    /// Plaza y calle alta casi rectangulares (esquinas redondeadas).
    private const float FormaDeCalle = 6f;

    /// Rampa de los escalones que suben de la plaza (oeste) a la calle alta (este), pegada al borde de la calle.
    private static Vector3[] RampaDeLaCalleAlta(float cotaPlaza)
    {
        float z = CentroDeLaCalleAlta.y + MedioFondoDeLaCalleAlta - 0.3f;
        return new[] { new Vector3(InicioDeLaRampa - 1f, cotaPlaza, z), new Vector3(InicioDeLaRampa + 4.5f * TramosDeLaRampa + 1f, cotaPlaza + DeLaCalleALaPlaza, z) };
    }
    private const float InicioDeLaRampa = 0.5f;
    private const int TramosDeLaRampa = 3;

    /// Recorrido de la cornisa de la senda alta: arranca encima del giro y acaba encima del refugio.
    private static Vector3[] Cornisa(List<Vector3> camino, List<(float desde, float hasta)> piernas)
    {
        var (p0, p1) = piernas[piernas.Count - 1];
        Vector3 a = PuntoDelRecorrido(camino, p0 + 3f), b = PuntoDelRecorrido(camino, p1 - 6f);
        return new[] { new Vector3(a.x + 1f, a.y + CornisaAlEmpezar, a.z + DetrasDeLaPierna), new Vector3(b.x, b.y + CornisaAlAcabar, b.z + DetrasDeLaPierna) };
    }

    /// Presa y embalse, al oeste de la calle alta.
    private static readonly Vector2 PlazaDelEmbalse = new Vector2(-34f, 440.5f);

    private static void EscribirAlturas(string ruta, float[,] h)
    {
        int res = h.GetLength(0);
        using var f = File.Create(ruta);
        using var gz = new GZipStream(f, System.IO.Compression.CompressionLevel.Optimal);
        using var w = new BinaryWriter(gz);
        w.Write("VDMA1");
        w.Write(res);
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
                w.Write(h[k, i]);
    }

    private static float[,] LeerAlturas(string ruta, int resEsperada)
    {
        using var f = File.OpenRead(ruta);
        using var gz = new GZipStream(f, CompressionMode.Decompress);
        using var rd = new BinaryReader(gz);
        if (rd.ReadString() != "VDMA1") return null;
        int res = rd.ReadInt32();
        if (res != resEsperada) return null;
        var h = new float[res, res];
        for (int k = 0; k < res; k++)
            for (int i = 0; i < res; i++)
                h[k, i] = rd.ReadSingle();
        return h;
    }

    private static string HuellaDeAlturas(float[,] h)
    {
        unchecked
        {
            ulong x = 1469598103934665603UL;
            int res = h.GetLength(0);
            for (int k = 0; k < res; k++)
                for (int i = 0; i < res; i++)
                {
                    x ^= (ulong)Mathf.RoundToInt(h[k, i] * 65535f);
                    x *= 1099511628211UL;
                }
            return x.ToString("x16");
        }
    }

    // ── Suelo ────────────────────────────────────────────────────────────────────────────────

    static partial void PintarSueloDeLaMontana(Lienzo l, List<string> informe)
    {
        var camino = RecorridoDelCamino((x, z) => l.Altura(x, z), out var piernas);
        Vector3 fin = FinDelCamino(camino);
        PintarElMacizo(l, CotaDeLaPlaza(fin), informe);
        PintarElRio(l, informe);
        PintarSenda(l, Planta(camino), 4.5f, 801, null, serpenteo: 0.4f);
        PintarSenda(l, Planta(Cornisa(camino, piernas)), 3f, 802, null, serpenteo: 0.5f);
        PintarMancha(l, new Vector2(fin.x, fin.z), 6f, CapaTierra, 0.9f, 803);
        PintarElipse(l, CentroDeLaPlaza, MedioLargoDeLaPlaza, MedioFondoDeLaPlaza, CapaAdoquin, FormaDeCalle);
        PintarElipse(l, CentroDeLaCalleAlta, MedioLargoDeLaCalleAlta, MedioFondoDeLaCalleAlta, CapaAdoquin, FormaDeCalle);
        PintarSenda(l, Planta(Garganta), 7f, 804, null, CapaArena, CapaTierraPiedras, 0.3f);
        PintarElipse(l, CentroDeLaCala, 13f, 9f, CapaArena);
        PintarElipse(l, PlazaDelEmbalse, 7f, 5f, CapaRoca);
        foreach (Vector2 campo in CamposDeVega)
            PintarMancha(l, campo, 6.5f, CapaTierra, 0.85f, 805 + (int)campo.x);
        PintarElPuebloDeRisco(l, CotaDeLaPlaza(fin));
        informe.Add("Suelo: camino de Risco, cornisa, Risco y sus terrazas, garganta, cala y huertas de Vega pintadas.");
        RocaEnLasParedes(l, informe);
    }

    private static Vector2[] Planta(IList<Vector3> p)
    {
        var r = new Vector2[p.Count];
        for (int i = 0; i < p.Count; i++) r[i] = new Vector2(p[i].x, p[i].z);
        return r;
    }

    private static void PintarElipse(Lienzo l, Vector2 c, float rx, float rz, string capa, float forma = 2f)
    {
        int n = l.Capa(capa);
        if (n < 0) return;
        l.Recorrer(c.x - rx - 1f, c.y - rz - 1f, c.x + rx + 1f, c.y + rz + 1f, (i, k, x, z) =>
        {
            float dx = Mathf.Abs(x - c.x) / rx, dz = Mathf.Abs(z - c.y) / rz;
            float q = Mathf.Pow(Mathf.Pow(dx, forma) + Mathf.Pow(dz, forma), 1f / forma);
            if (q < 1.05f) l.Mezclar(i, k, n, Mathf.Clamp01((1.05f - q) / 0.15f), duro: true);
        });
    }

    static partial void ZonasDeLaMontana(List<Zona> libres)
    {
        libres.Add(new Zona("Risco (plaza y calle alta)", CentroDeLaPlaza.x, CentroDeLaPlaza.y - 4f, 30f));
        foreach (Terraza t in TerrazasDeRisco) libres.Add(Zona.Rectangulo("Risco — " + t.Nombre, t.XMin - 1.5f, t.ZMin - 1.5f, t.XMax + 1.5f, t.ZMax + 1.5f));
        libres.Add(new Zona("Cala de Risco", CentroDeLaCala.x, CentroDeLaCala.y, 16f));
        libres.Add(new Zona("Boca de la cueva del río", BocaDeLaCuevaDelRio.x, BocaDeLaCuevaDelRio.y, 9f));
        foreach (var (prefab, pos, rumbo) in CasasDeVega) libres.Add(new Zona("Casa de Vega", pos.x, pos.y, 7f));
        foreach (var c in CauceDelRio) libres.Add(new Zona("Río de las cascaditas", c.x, c.z, c.medio + c.llano + 3f));
        foreach (var (nombre, centro, rx, rz, _, _) in PozasDelRio) libres.Add(new Zona(nombre, centro.x, centro.y, Mathf.Max(rx, rz) + 3f));
    }

    // ── Piezas ───────────────────────────────────────────────────────────────────────────────

    private const string GrupoDeRocasDeCosta = "Rocas de costa e islotes";
    private const string MotivoRelieveDeLaMontana = "peña o árbol del mundo donde el relieve de la montaña de Risco ha cambiado la altura";

    private static void MontanaDeRisco(Obra o)
    {
        Transform grupo = Grupo(o.Raiz, "Montaña de Risco");
        RetirarLoQueQuedaFuera(o);
        var camino = RecorridoDelCamino((x, z) => o.Suelo.Altura(x, z), out var piernas);
        Vector3 fin = FinDelCamino(camino);
        CaminoDeRisco(o, Grupo(grupo, "Camino de Risco"), camino, piernas);
        SendasYPuente(o, Grupo(grupo, "Senda alta, pinar, refugio y puente"), camino, piernas);
        GaleriaDelCollado(o, Grupo(grupo, "Galería del collado"), fin);
        PuebloDeRisco(o, Grupo(grupo, "Risco"), fin);
        Montacargas(o, Grupo(grupo, "Montacargas de los pescadores"), CotaDeLaPlaza(fin));
        CalaDeRisco(o, Grupo(grupo, "Cala"));
        CuevaDelRio(o, Grupo(grupo, "Cueva del río"));
        AldeaDeVega(o, Grupo(grupo, "Vega"));
        NacimientoDeLaCascada(o, Grupo(grupo, "Nacimiento de la cascada"));
        MacizoDelNorte(o, Grupo(grupo, "Macizo del norte"), CotaDeLaPlaza(fin));
        RioDeLasCascaditas(o, Grupo(o.Raiz, "Río de las cascaditas"));
        LugaresDeLaMontana(o, grupo, CotaDeLaPlaza(fin));
    }

    /// Retira (con el registro) las peñas y árboles del mundo que el relieve deja enterrados o en el aire.
    private static void RetirarLoQueQuedaFuera(Obra o)
    {
        if (s_cambioDelRelieve == null) return;
        Scene escena = o.Raiz.gameObject.scene;
        TerrainData datos = o.Terreno.terrainData;
        Vector3 origen = o.Terreno.transform.position;
        int res = datos.heightmapResolution;
        var fuera = new List<Transform>();
        foreach (string nombre in new[] { GrupoDeRocasDelMundo, GrupoDeRocasDeCosta })
        {
            Transform g = BuscarGrupo(escena, nombre);
            if (g == null) continue;
            // Las piezas sueltas y las de los grupos de suelo vegetal (hierba y setas por paraje).
            var piezas = new List<Transform>();
            foreach (Transform t in g)
            {
                if (t.GetComponent<Renderer>() == null && t.childCount > 0) foreach (Transform h in t) piezas.Add(h);
                else piezas.Add(t);
            }
            foreach (Transform t in piezas)
            {
                if (!t.gameObject.activeSelf) continue;
                int i = Mathf.RoundToInt((t.position.x - origen.x) / datos.size.x * (res - 1));
                int k = Mathf.RoundToInt((t.position.z - origen.z) / datos.size.z * (res - 1));
                if (i < 0 || k < 0 || i >= res || k >= res) continue;
                PuntoDelCauce c = EnElCauce(t.position.x, t.position.z);
                var q = new Vector2(t.position.x, t.position.z);
                // Árboles sueltos en las paredes de la montaña: sin sentido ahí.
                bool enLaPared = t.name.StartsWith("Tree") && t.position.z > 365f &&
                                 (t.position.y > 118f || DentroDelMacizo(q.x, q.y) > 0f) && PendienteEn(o, q) > 34f;
                if (Mathf.Abs(s_cambioDelRelieve[k, i]) > 1.2f || c.Distancia < c.Medio + c.Llano + 1f || enLaPared) fuera.Add(t);
            }
        }
        foreach (Transform t in fuera) Retirar(escena, t, MotivoRelieveDeLaMontana, o);
        if (fuera.Count > 0) Physics.SyncTransforms();
    }

    private static GameObject PonZona(Obra o, Transform grupo, string pieza, string nombre, Vector3 pos, float rumbo, float escala = 1f, bool sinObstaculo = false)
    {
        GameObject go = PonExacto(o, grupo, PrefabsDeZonas + pieza + ".prefab", nombre, pos, rumbo, sinObstaculo);
        if (go != null && !Mathf.Approximately(escala, 1f)) go.transform.localScale *= escala;
        return go;
    }

    private static float AlturaEn(Obra o, Vector2 p) => o.Suelo.Altura(p.x, p.y);

    private static void CaminoDeRisco(Obra o, Transform grupo, List<Vector3> camino, List<(float desde, float hasta)> piernas)
    {
        Vector3 inicio = camino[0];
        PonZona(o, grupo, "HitoDelCamino", "Hito del camino de Risco", new Vector3(inicio.x + 2.5f, AlturaEn(o, new Vector2(inicio.x + 2.5f, inicio.z - 3.5f)), inicio.z - 3.5f), 200f);
        // Mojones en el borde de fuera de cada giro.
        for (int n = 0; n < piernas.Count - 1; n++)
        {
            Vector3 g = PuntoDelRecorrido(camino, piernas[n].hasta + 1.2f);
            Vector2 fuera = new Vector2(g.x, g.z) + new Vector2(Mathf.Sin(RumboDeLasPiernas * Mathf.Deg2Rad), Mathf.Cos(RumboDeLasPiernas * Mathf.Deg2Rad)) * (n % 2 == 0 ? 4.5f : -4.5f);
            PonZona(o, grupo, "MojonDePiedras", "Mojón del camino", new Vector3(fuera.x, AlturaEn(o, fuera) - 0.1f, fuera.y), n * 50f);
        }
        // El derrumbe: dos peñas para cruzar saltando y los cascotes sobre la pierna de abajo.
        var (h0, h1) = TramoDelDerrumbe(piernas);
        Transform derrumbe = Grupo(grupo, "Derrumbe");
        for (int j = 0; j < 2; j++)
        {
            Vector3 p = PuntoDelRecorrido(camino, h0 + HuecoDelDerrumbe * (j + 1) / 3f);
            Vector2 lado = LaderaAbajo * (j == 0 ? 0.7f : -0.5f);
            PonZona(o, derrumbe, "RepisaDeRoca_Pequena", "Peña del derrumbe", new Vector3(p.x + lado.x, p.y + 0.15f * j, p.z + lado.y), 40f + j * 70f, 0.6f);
        }
        Vector3 medio = PuntoDelRecorrido(camino, (h0 + h1) * 0.5f);
        Vector3 bajo = PuntoDelRecorrido(camino, piernas[PiernaDelDerrumbe - 1].hasta - (piernas[PiernaDelDerrumbe].hasta - (h0 + h1) * 0.5f));
        PonZona(o, derrumbe, "PiedrasDelDerrumbe", "Cascotes del derrumbe", new Vector3(bajo.x, AlturaEn(o, new Vector2(bajo.x, bajo.z)), bajo.z), 15f);
        Vector2 muesca = new Vector2(medio.x, medio.z) + LaderaAbajo * (SeparacionDePiernas * 0.45f);
        PonZona(o, derrumbe, "PiedrasDelDerrumbe", "Cascotes de la muesca", new Vector3(muesca.x, AlturaEn(o, muesca), muesca.y), 100f, 0.8f);
        o.Informe.Add($"  Camino de Risco: {camino.Count} puntos, de {camino[0].y:0.0} m a {camino[camino.Count - 1].y:0.0} m; derrumbe en ({medio.x:0}, {medio.z:0}) a {medio.y:0.0} m.");
    }

    /// Senda alta (repisas y cornisa), senda del pinar (la tercera pierna, con pinos y un claro), refugio y puente.
    private static void SendasYPuente(Obra o, Transform grupo, List<Vector3> camino, List<(float desde, float hasta)> piernas)
    {
        Vector3 fin = FinDelCamino(camino);
        PonZona(o, grupo, "RefugioDelPastor", "Refugio del pastor", new Vector3(fin.x + 1f, fin.y, fin.z + 5f), 10f);
        // Senda alta: del giro suben dos repisas a la cornisa; al final de la cornisa, dos repisas bajan al refugio.
        Vector3[] cornisa = Cornisa(camino, piernas);
        var (p0, p1) = piernas[piernas.Count - 1];
        Vector3 giro = PuntoDelRecorrido(camino, p0 + 1f);
        Transform alta = Grupo(grupo, "Senda alta");
        int repisas = 0;
        for (int j = 1; j <= 2; j++)
        {
            float t = j / 3f;
            var p = new Vector3(Mathf.Lerp(giro.x - 3f, cornisa[0].x - 1f, t), Mathf.Lerp(giro.y, cornisa[0].y, t), Mathf.Lerp(giro.z + 2.5f, cornisa[0].z, t));
            PonZona(o, alta, j % 2 == 0 ? "RepisaDeRoca_Mediana" : "RepisaDeRoca_Pequena", "Repisa de la senda alta", p + new Vector3(j % 2 == 0 ? -1.2f : 1.2f, 0f, 0f), j * 53f, j % 2 == 0 ? 0.5f : 0.65f);
            repisas++;
        }
        for (int j = 1; j <= 2; j++)
        {
            float t = j / 3f;
            var p = Vector3.Lerp(cornisa[1], new Vector3(fin.x - 3f, fin.y, fin.z + 1.5f), t);
            PonZona(o, alta, "RepisaDeRoca_Pequena", "Repisa de la senda alta", p, j * 71f, 0.6f);
            repisas++;
        }
        // Pinar: pinos a los dos lados de la tercera pierna, sin tapar el claro de en medio.
        Transform pinar = Grupo(grupo, "Pinar");
        Vector3 claro = PuntoDelRecorrido(camino, (p0 + p1) * 0.5f);
        string[] pinos = { "Tree01_a01", "Tree01_a02", "Tree02_c01" };
        var az = new System.Random(731);
        int puestos = 0;
        Vector2[] planta = Planta(camino);
        for (int j = 0; j < 60 && puestos < 20; j++)
        {
            float s = p0 + (float)az.NextDouble() * (p1 - p0);
            Vector3 p = PuntoDelRecorrido(camino, s);
            Vector3 q3 = PuntoDelRecorrido(camino, s + 1f);
            var dir = new Vector2(q3.x - p.x, q3.z - p.z).normalized;
            float lado = (az.NextDouble() < 0.5 ? -1f : 1f) * (4.2f + (float)az.NextDouble() * 3.5f);
            Vector2 q = new Vector2(p.x, p.z) + new Vector2(-dir.y, dir.x) * lado;
            if (Vector2.Distance(q, new Vector2(claro.x, claro.z)) < 7f || DistanciaAPolilinea(q, planta) < 4f ||
                DistanciaAPolilinea(q, Planta(cornisa)) < 3f || PendienteEn(o, q) > 30f) continue;
            PonZona0(o, pinar, FK + "Vegetation/" + pinos[az.Next(pinos.Length)] + ".prefab", "Pino del pinar",
                     new Vector3(q.x, AlturaEn(o, q) - 0.3f, q.y), (float)az.NextDouble() * 360f, 0.85f + 0.3f * (float)az.NextDouble());
            puestos++;
        }
        // Puente colgante sobre el tajo, hacia el este.
        Vector3 cabeza = CabezaDelPuente(fin);
        PonZona(o, grupo, "PuenteColgante_Largo", "Puente del tajo", new Vector3(cabeza.x + LuzDelPuente * 0.5f, cabeza.y, cabeza.z), 90f);
        o.Informe.Add($"  Refugio en ({fin.x:0}, {fin.z:0}) a {fin.y:0.0} m; {repisas} repisas en la senda alta (cornisa a {cornisa[0].y:0.0} m); {puestos} pinos; puente a {cabeza.y:0.0} m.");
    }

    /// Como PonZona, para prefabs que no son de las zonas (ruta completa).
    private static GameObject PonZona0(Obra o, Transform grupo, string ruta, string nombre, Vector3 pos, float rumbo, float escala = 1f)
    {
        GameObject go = PonExacto(o, grupo, ruta, nombre, pos, rumbo);
        if (go != null && !Mathf.Approximately(escala, 1f)) go.transform.localScale *= escala;
        return go;
    }

    /// Galería que cruza el collado: boca al otro lado del tajo, un tramo de escalera que baja y salida en la calle alta.
    private static void GaleriaDelCollado(Obra o, Transform grupo, Vector3 fin)
    {
        Vector2 a = BocaDeLaGaleria(fin) + new Vector2(-1.2f, 0f);
        Vector2 dir = (SalidaDeLaGaleria - a).normalized;
        float largo = LargoDeLaGaleria();
        float arriba = fin.y, abajo = CotaDeLaCalleAlta(fin);
        float inicio = 0f, final = 0f, s = 0f;
        foreach (char c in PiezasDeLaGaleria)
        {
            if (c == 'E' && inicio == 0f) inicio = s;
            s += c == 'B' ? LargoDeBoca : c == 'S' ? LargoDeSala : LargoDeTunel;
            if (c == 'E') final = s;
        }
        float SueloDeLaGaleria(float d) => d <= inicio ? arriba : d >= final ? abajo : Mathf.Lerp(arriba, abajo, (d - inicio) / (final - inicio));
        Vector2 b = a + dir * largo;
        DespejarLaTraza(o, a, b, 5.5f);
        int abiertas = AbrirElTerreno(o, a, b, SueloDeLaGaleria, out float aire);
        PonerPiezasDeTunel(o, grupo, "Galería del collado", a, dir, PiezasDeLaGaleria, SueloDeLaGaleria);
        o.Informe.Add($"  Galería del collado: de ({a.x:0}, {a.y:0}) a {arriba:0.0} m a ({b.x:0}, {b.y:0}) a {abajo:0.0} m; {abiertas} celdas abiertas" +
                      (aire > 0f ? $"; OJO: {aire:0} m al aire" : "") + ".");
    }

    /// Pone las piezas de un túnel a lo largo de «dir» desde «a»: B boca, T túnel, S sala, E escalera que baja en
    /// el sentido de la marcha. Devuelve la distancia al centro de cada sala.
    private static List<float> PonerPiezasDeTunel(Obra o, Transform grupo, string nombre, Vector2 a, Vector2 dir, IList<char> piezas, Func<float, float> suelo)
    {
        float rumbo = Rumbo(dir), s = 0f;
        var salas = new List<float>();
        Vector3 En(float d, float y) { Vector2 p = a + dir * d; return new Vector3(p.x, y, p.y); }
        for (int j = 0; j < piezas.Count; j++)
        {
            char c = piezas[j];
            float l = c == 'B' ? LargoDeBoca : c == 'S' ? LargoDeSala : LargoDeTunel;
            float medio = s + l * 0.5f;
            switch (c)
            {
                case 'B':
                    PonExacto(o, grupo, BocaDeLaCueva, nombre + (j == 0 ? " — boca" : " — salida"), En(medio, suelo(medio)), j == 0 ? rumbo : rumbo + 180f);
                    break;
                case 'S':
                    PonExacto(o, grupo, SalaDeLaCueva, nombre + " — sala", En(medio, suelo(medio)), rumbo);
                    salas.Add(medio);
                    break;
                case 'E':
                    // La escalera sube hacia +Z local: se pone del revés, con la base en el extremo de abajo.
                    PonExacto(o, grupo, TunelConEscalera, nombre + " — escalera", En(medio, suelo(s + l)), rumbo + 180f);
                    break;
                default:
                    PonExacto(o, grupo, TunelRecto, nombre + " — túnel", En(medio, suelo(medio)), rumbo);
                    break;
            }
            s += l;
        }
        return salas;
    }

    /// Montacargas de cuerda: en la punta de la pasarela que sale de la plaza a la cabecera de la garganta, un solo
    /// tramo de castillete con la cabria encima; la jaula cuelga de la cuerda y sube del fondo de la garganta a la plaza.
    /// Postes de llamada arriba y abajo.
    private static void Montacargas(Obra o, Transform grupo, float alto)
    {
        // Un poco dentro de la cabecera: la pared del pueblo baja hasta el mismo eje.
        Vector3 pie = Garganta[0] + new Vector3(-4f, 0f, 0f);
        float bajo = AlturaEn(o, new Vector2(pie.x, pie.z));
        PonZona(o, grupo, "TramoDeCastillete", "Castillete del montacargas", new Vector3(pie.x, alto, pie.z), -90f, 1f, sinObstaculo: true);
        PonZona(o, grupo, "CabriaDelMontacargas", "Cabria del montacargas", new Vector3(pie.x, alto + 10f, pie.z), -90f, 1f, sinObstaculo: true);
        // La jaula sale abajo; sube hasta dejar su suelo a la cota de la plaza.
        GameObject jaula = PonZona(o, grupo, "JaulaDelMontacargas", "Jaula del montacargas", new Vector3(pie.x, bajo + 0.3f, pie.z), -90f, 1f, sinObstaculo: true);
        if (jaula != null)
        {
            var elevador = jaula.AddComponent<PlatformElevator>();
            var so = new SerializedObject(elevador);
            so.FindProperty("raiseHeight").floatValue = alto - (bajo + 0.3f);
            so.FindProperty("moveSpeed").floatValue = 7f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Usable(jaula.transform, "Palanca del montacargas", new Vector3(1.15f, 0.9f, 1.15f), elevador.Toggle);
        }
        // Pasarela de la plaza a la jaula y postes para llamarla arriba y abajo.
        Vector2 borde = new Vector2(CentroDeLaPlaza.x - MedioLargoDeLaPlaza + 0.5f, pie.z);
        Vector2 aLaJaula = (new Vector2(pie.x, pie.z) - borde);
        for (float d = 1.5f; d < aLaJaula.magnitude - 1f; d += 3f)
        {
            Vector2 p = borde + aLaJaula.normalized * d;
            PonZona(o, grupo, "PasarelaDeLaAldea", "Pasarela del montacargas", new Vector3(p.x, alto - 1.6f, p.y), Rumbo(new Vector2(-aLaJaula.y, aLaJaula.x)), 1f, sinObstaculo: true);
        }
        // Dos tablas a los lados del hueco de la jaula, bajo los pies de la punta del castillete.
        Vector2 dirJaula = aLaJaula.normalized, ladoJaula = new Vector2(-dirJaula.y, dirJaula.x);
        foreach (float s in new[] { -2.1f, 2.1f })
        {
            Vector2 p = new Vector2(pie.x, pie.z) + dirJaula * 0.6f + ladoJaula * s;
            PonZona(o, grupo, "PasarelaDeLaAldea", "Pasarela del montacargas", new Vector3(p.x, alto - 1.6f, p.y), Rumbo(ladoJaula), 1f, sinObstaculo: true);
        }
        if (jaula != null)
        {
            var elevador = jaula.GetComponent<PlatformElevator>();
            GameObject arriba = PonZona(o, grupo, "PosteDeLlamada", "Poste de llamada (arriba)", new Vector3(borde.x + 1.5f, alto, borde.y - 2.4f), 90f);
            if (arriba != null) Usable(arriba.transform, "Llamar al montacargas", new Vector3(1.3f, 1.8f, 1.3f), elevador.MoveUp);
            Vector2 p = new Vector2(pie.x, pie.z) + new Vector2(2.6f, 3.4f);
            GameObject abajo = PonZona(o, grupo, "PosteDeLlamada", "Poste de llamada (abajo)", new Vector3(p.x, AlturaEn(o, p), p.y), 40f);
            if (abajo != null) Usable(abajo.transform, "Llamar al montacargas", new Vector3(1.3f, 1.8f, 1.3f), elevador.MoveDown);
        }
        o.Informe.Add($"  Montacargas de cuerda: la jaula sube de {bajo:0.0} m a {alto:0.0} m.");
    }

    /// Interactuable que, al usarlo, llama a «accion» (evento persistente).
    private static void Usable(Transform padre, string nombre, Vector3 caja, UnityEngine.Events.UnityAction accion)
    {
        var uso = new GameObject(nombre);
        int capa = LayerMask.NameToLayer("Interactable");
        uso.layer = capa >= 0 ? capa : 8;
        uso.transform.SetParent(padre, false);
        var zona = uso.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.center = new Vector3(0f, caja.y * 0.5f, 0f);
        zona.size = caja * 2f;
        var interactuable = uso.AddComponent<Interactable>();
        ModelosDeLasZonas.IconoDeInteractuar(interactuable, uso.transform, InteractableMode.Custom, new System.Text.StringBuilder());
        UnityEventTools.AddVoidPersistentListener(interactuable.OnInteract, accion);
    }

    private static void CalaDeRisco(Obra o, Transform grupo)
    {
        Vector2 c = CentroDeLaCala;
        PonZona0(o, grupo, Barca, "Barca varada", new Vector3(c.x - 3f, AlturaEn(o, c + new Vector2(-3f, 2f)), c.y + 2f), 20f);
        PonZona0(o, grupo, BoteAncho, "Bote varado", new Vector3(c.x + 4f, AlturaEn(o, c + new Vector2(4f, 3.5f)), c.y + 3.5f), -35f);
        for (int j = 0; j < 3; j++)
        {
            Vector2 p = c + new Vector2(-1f, 8f + j * 3f);
            PonZona(o, grupo, "PasarelaDeLaAldea", "Embarcadero de la cala", new Vector3(p.x, 0f, p.y), 0f, 1f, sinObstaculo: true);
        }
        PonZona0(o, grupo, Nasa, "Nasa", new Vector3(c.x + 2f, AlturaEn(o, c + new Vector2(2f, -3f)), c.y - 3f), 70f);
        PonZona0(o, grupo, Red, "Red tendida", new Vector3(c.x - 6f, AlturaEn(o, c + new Vector2(-6f, -2f)), c.y - 2f), 110f);
    }

    // ── Cueva del río ────────────────────────────────────────────────────────────────────────

    /// Piezas de la cueva del río: boca, 4 túneles, sala del manantial, 3 túneles, 8 escaleras que bajan, 14 túneles
    /// y boca (254,6 m: sale en la pared suroeste de la garganta).
    private const string PiezasDeLaCuevaDelRio = "BTTTTSTTTEEEEEEEETTTTTTTTTTTTTTB";
    private static float LargoDeLasPiezas(IEnumerable<char> piezas)
    {
        float l = 0f;
        foreach (char c in piezas) l += c == 'B' ? LargoDeBoca : c == 'S' ? LargoDeSala : LargoDeTunel;
        return l;
    }
    private static float LargoDeLaCuevaDelRio => LargoDeLasPiezas(PiezasDeLaCuevaDelRio);

    private static void CuevaDelRio(Obra o, Transform grupo)
    {
        var piezas = new List<char>(PiezasDeLaCuevaDelRio);
        float largo = LargoDeLaCuevaDelRio;

        Vector2 a = BocaDeLaCuevaDelRio, dir = (HaciaLaCala - a).normalized, b = a + dir * largo;
        float inicioEscalera = 0f, finEscalera = 0f, s = 0f;
        foreach (char c in piezas)
        {
            if (c == 'E' && inicioEscalera == 0f) inicioEscalera = s;
            s += c == 'B' ? LargoDeBoca : c == 'S' ? LargoDeSala : LargoDeTunel;
            if (c == 'E') finEscalera = s;
        }
        float SueloDeLaCueva(float d) => d <= inicioEscalera ? SueloDeLaCuevaAlta
            : d >= finEscalera ? SueloDeLaCuevaBaja
            : Mathf.Lerp(SueloDeLaCuevaAlta, SueloDeLaCuevaBaja, (d - inicioEscalera) / (finEscalera - inicioEscalera));
        DespejarLaTraza(o, a, b, 5.5f);
        int abiertas = AbrirElTerreno(o, a, b, SueloDeLaCueva, out float aire);
        float rumbo = Rumbo(dir);
        Vector3 En(float d, float y) { Vector2 p = a + dir * d; return new Vector3(p.x, y, p.y); }
        var lado = new Vector2(-dir.y, dir.x);
        List<float> salas = PonerPiezasDeTunel(o, grupo, "Cueva del río", a, dir, piezas, SueloDeLaCueva);
        float salaDelManantial = salas[0];

        // El agua: entra por la grieta de la sala del manantial y sale por la boca hasta el nacimiento del río.
        Vector2 enLaSala = a + dir * salaDelManantial + lado * 4.6f;
        PonZona(o, grupo, "GrietaDelManantial", "Grieta del manantial", new Vector3(enLaSala.x, SueloDeLaCuevaAlta, enLaSala.y), Rumbo(lado));
        var cinta = new List<Vector3>();
        for (float d = salaDelManantial + 3.5f; d >= 0f; d -= 2f) cinta.Add(En(d, SueloDeLaCuevaAlta + 0.08f));
        Vector2 fuera = a, hastaElRio = NacimientoDelRio - a;
        for (float d = 2f; d <= hastaElRio.magnitude; d += 2f)
        {
            Vector2 p = fuera + hastaElRio.normalized * d;
            cinta.Add(new Vector3(p.x, Mathf.Min(SueloDeLaCuevaAlta, AlturaEn(o, p)) + 0.1f, p.y));
        }
        LaminaDeAgua(o, grupo, "Agua de la cueva del río", Cinta(cinta, 2.9f), 0f);
        // Cortina de agua que cae del techo justo después de la boca: se atraviesa.
        var (cortina, espuma) = CascadasDelJuego.Materiales();
        Vector3 caida = En(LargoDeBoca + LargoDeTunel * 0.5f, SueloDeLaCuevaAlta);
        WaterfallBuilder.Build(grupo, "Cortina de la cueva del río",
            new List<Vector3> { caida + Vector3.up * 3.6f - (Vector3)(new Vector3(dir.x, 0f, dir.y) * 0.2f), caida + Vector3.up * 0.05f },
            new List<float> { 3.0f, 3.0f }, cortina, espuma, 0.05f);
        // Charcos donde el agua se va quedando, y la arena que mete el mar al final.
        foreach (float d in new[] { salaDelManantial + 12f, salaDelManantial + 19f, salaDelManantial + 25f, finEscalera + 6f, finEscalera + 15f })
            PonZona(o, grupo, "CharcoDeCueva", "Charco de la cueva", En(d, SueloDeLaCueva(d)) + (Vector3)(new Vector3(lado.x, 0f, lado.y) * 0.4f), d * 13f, 0.85f, sinObstaculo: true);
        for (float d = largo - 46f; d < largo - 3f; d += 7.5f)
            PonZona(o, grupo, "ArenaDeCueva", "Arena de la cueva", En(d, SueloDeLaCuevaBaja), rumbo + (d % 15f < 7.5f ? 0f : 180f), 1f, sinObstaculo: true);
        o.Informe.Add($"  Cueva del río: {largo:0.0} m de ({a.x:0}, {a.y:0}) a ({b.x:0}, {b.y:0}); suelo de {SueloDeLaCuevaAlta:0} a {SueloDeLaCuevaBaja:0} m; {abiertas} celdas abiertas" +
                      (aire > 0f ? $"; OJO: {aire:0} m al aire" : "") + ".");
    }

    // ── Nacimiento de la cascada de la ladera este ───────────────────────────────────────────

    /// El arroyo de la ladera este nace en el rellano alto de la peña del este y cae por la cascada grande. Ahí el
    /// agua sale de una boca de cueva, con un mirador al lado: destino de vuelo desde el que se ve toda la isla (y,
    /// si se salta, se cae a la poza de abajo).
    private static readonly Vector2 NacimientoDelArroyo = new Vector2(150f, 330f), HaciaAbajoDelArroyo = new Vector2(0.45f, -0.89f);
    private const float CotaDelNacimiento = 156.7f;

    private static void NacimientoDeLaCascada(Obra o, Transform grupo)
    {
        Vector2 adentro = -HaciaAbajoDelArroyo.normalized;
        Vector2 boca = NacimientoDelArroyo + adentro * (LargoDeBoca * 0.5f + 0.5f);
        PonExacto(o, grupo, BocaDeLaCueva, "Boca del nacimiento", new Vector3(boca.x, CotaDelNacimiento - 0.3f, boca.y), Rumbo(adentro));
        var lado = new Vector2(adentro.y, -adentro.x);
        Vector2 mirador = NacimientoDelArroyo + lado * 9f + adentro * 2f;
        PonExacto(o, grupo, MiradorDeLaCumbre, "Mirador de la cascada", new Vector3(mirador.x, AlturaEn(o, mirador), mirador.y), Rumbo(-HaciaAbajoDelArroyo) + 180f);
        o.Informe.Add($"  Nacimiento de la cascada: boca en ({boca.x:0}, {boca.y:0}) y mirador en ({mirador.x:0}, {mirador.y:0}) a {AlturaEn(o, mirador):0.0} m.");
    }

    // ── Vega ─────────────────────────────────────────────────────────────────────────────────

    private static readonly (string prefab, Vector2 pos, float rumbo)[] CasasDeVega =
    {
        ("CasaDelPueblo_A", new Vector2(-194f, 210f), -100f), ("CasaDelPueblo_D", new Vector2(-191f, 189f), -80f),
        ("CasaDelPueblo_B", new Vector2(-140f, 205f), 95f), ("CasaDelPueblo_E", new Vector2(-143f, 186f), 80f),
    };
    private static readonly Vector2[] CamposDeVega = { new Vector2(-188f, 170f), new Vector2(-150f, 168f), new Vector2(-203f, 196f) };

    private static void AldeaDeVega(Obra o, Transform grupo)
    {
        // Casas y huertas van sobre sus rellanos (EsculpirLaMontana): se ponen a la cota del suelo.
        int puestas = 0;
        foreach (var (prefab, pos, rumbo) in CasasDeVega)
            if (PonZona(o, grupo, prefab, "Casa de Vega", new Vector3(pos.x, AlturaEn(o, pos), pos.y), rumbo) != null) puestas++;
        string[] huertas = { FK + "Props/Farm/Farm01_a01.prefab", FK + "Props/Farm/Farm01_b01.prefab", FK + "Props/Farm/Farm02_a01.prefab" };
        for (int j = 0; j < CamposDeVega.Length; j++)
            if (PonZona0(o, grupo, huertas[j % huertas.Length], "Huerta de Vega", new Vector3(CamposDeVega[j].x, AlturaEn(o, CamposDeVega[j]), CamposDeVega[j].y), j * 35f) != null) puestas++;
        // Noria en el nacimiento del río: la rueda moja en la lámina (32 m).
        Vector2 noria = new Vector2(-168f, 202f);
        PonZona(o, grupo, "NoriaDeVega", "Noria de Vega", new Vector3(noria.x, 31.9f, noria.y), 0f);
        o.Informe.Add($"  Vega: {puestas} piezas (casas y huertas) y la noria.");
    }

    // ── Lugares (presentación al entrar) ─────────────────────────────────────────────────────

    private static void LugaresDeLaMontana(Obra o, Transform grupo, float cotaPlaza)
    {
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, "LUGAR_RISCO", "Risco", "Risco");
        TextosDeLaHistoria.Poner(TextosDeLaHistoria.Tabla.Interfaz, "LUGAR_VEGA", "Vega", "Vega");
        TextosDeLaHistoria.Escribir();
        Lugar(o, grupo, "AmbientPreset_Risco", "LUGAR_RISCO", "Risco (vestido de MainWorld): solo presenta el lugar.",
              new Vector3(CentroDeLaPlaza.x - 4f, cotaPlaza - 6f, 468f), new Vector3(72f, 48f, 64f));
        Lugar(o, grupo, "AmbientPreset_Vega", "LUGAR_VEGA", "Vega (vestido de MainWorld): solo presenta el lugar.",
              new Vector3(-168f, 40f, 195f), new Vector3(80f, 40f, 70f));
    }

    private static void Lugar(Obra o, Transform grupo, string archivo, string clave, string descripcion, Vector3 centro, Vector3 medida)
    {
        AmbientPreset preset = RecursoPersistente(archivo + ".asset", () =>
        {
            var p = ScriptableObject.CreateInstance<AmbientPreset>();
            p.name = archivo;
            p.changeMusic = false;
            p.controlAmbientLight = false;
            p.transitionDuration = 2f;
            p.description = descripcion;
            return p;
        });
        if (preset != null && preset.nombreDelLugar != clave)
        {
            preset.nombreDelLugar = clave;
            EditorUtility.SetDirty(preset);
        }
        var zona = new GameObject("Lugar — " + clave);
        int capa = LayerMask.NameToLayer("AmbientZone");
        if (capa >= 0) zona.layer = capa;
        zona.transform.SetParent(grupo, false);
        zona.transform.position = centro;
        var caja = zona.AddComponent<BoxCollider>();
        caja.isTrigger = true;
        caja.size = medida;
        var ambiente = zona.AddComponent<AmbientZone>();
        var so = new SerializedObject(ambiente);
        so.FindProperty("ambientPreset").objectReferenceValue = preset;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Láminas de agua ──────────────────────────────────────────────────────────────────────

    /// Agua lisa de las piezas de las zonas: el agua del río del mundo hace espuma por profundidad y, en láminas tan
    /// someras, se vería toda blanca.
    private const string MaterialDelAguaDulce = "Assets/Art/World/Zonas/Materiales/Mat_Zonas_Agua.mat";

    /// Polígono convexo en planta (x, z) de un disco elíptico.
    private static Vector3[] Disco(Vector2 c, float rx, float rz, int lados)
    {
        var p = new Vector3[lados];
        for (int i = 0; i < lados; i++)
        {
            float a = 2f * Mathf.PI * i / lados;
            p[i] = new Vector3(c.x + rx * Mathf.Cos(a), 0f, c.y + rz * Mathf.Sin(a));
        }
        return p;
    }

    /// Cinta de agua de «ancho» a lo largo de un recorrido 3D: pares de vértices izquierda/derecha.
    private static Vector3[] Cinta(IList<Vector3> recorrido, float ancho)
    {
        var v = new Vector3[recorrido.Count * 2];
        for (int i = 0; i < recorrido.Count; i++)
        {
            Vector3 d = recorrido[Mathf.Min(i + 1, recorrido.Count - 1)] - recorrido[Mathf.Max(i - 1, 0)];
            var n = new Vector3(-d.z, 0f, d.x).normalized * (ancho * 0.5f);
            v[2 * i] = recorrido[i] - n;
            v[2 * i + 1] = recorrido[i] + n;
        }
        return v;
    }

    /// Lámina de agua sin colisión. Con «cota» distinta de cero, «puntos» es un polígono convexo en planta a esa
    /// cota; con cero, «puntos» es una cinta (pares izquierda/derecha) con su propia altura. Sin «material», agua lisa.
    private static void LaminaDeAgua(Obra o, Transform grupo, string nombre, Vector3[] puntos, float cota, Material material = null)
    {
        var malla = new Mesh { name = nombre };
        var tris = new List<int>();
        Vector3[] v;
        if (cota != 0f)
        {
            v = new Vector3[puntos.Length + 1];
            Vector3 c = Vector3.zero;
            for (int i = 0; i < puntos.Length; i++) { v[i + 1] = new Vector3(puntos[i].x, cota, puntos[i].z); c += v[i + 1]; }
            v[0] = c / puntos.Length;
            for (int i = 0; i < puntos.Length; i++) { tris.Add(0); tris.Add(1 + (i + 1) % puntos.Length); tris.Add(1 + i); }
        }
        else
        {
            v = puntos;
            for (int i = 0; i + 3 < v.Length; i += 2) { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2); }
        }
        var go = new GameObject(nombre);
        go.transform.SetParent(grupo, false);
        Vector3 origen = v[0];
        for (int i = 0; i < v.Length; i++) v[i] -= origen;
        go.transform.position = origen;
        malla.vertices = v;
        malla.triangles = tris.ToArray();
        // Las caras se ven por arriba: si el orden las deja mirando abajo, se les da la vuelta.
        malla.RecalculateNormals();
        if (malla.normals.Length > 0 && malla.normals[0].y < 0f)
        {
            int[] t = malla.triangles;
            for (int i = 0; i < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            malla.triangles = t;
            malla.RecalculateNormals();
        }
        malla.RecalculateBounds();
        AssetDatabase.CreateAsset(malla, CarpetaGenerada + "/" + nombre.Replace(' ', '_') + ".asset");
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material != null ? material : AssetDatabase.LoadAssetAtPath<Material>(MaterialDelAguaDulce);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        o.SinObstaculo.Add(go.transform);
    }
}
