using UnityEngine;

/// Ruido determinista para el vestido del mundo: mismo resultado en cada ejecución y en cada máquina
/// (no usa UnityEngine.Random ni la hora), para que repetir el menú dé exactamente el mismo mundo.
public static partial class VestidoDelMundo
{
    private static class Ruido
    {
        /// Valor pseudoaleatorio en [0,1] para una celda entera y una semilla.
        public static float Hash(int x, int z, int semilla)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)z * 668265263u + (uint)semilla * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffu) / 65535f;
            }
        }

        /// Ruido de valor suavizado (interpolación cúbica entre celdas de lado «escala» metros).
        public static float Valor(float x, float z, float escala, int semilla)
        {
            float gx = x / escala, gz = z / escala;
            int ix = Mathf.FloorToInt(gx), iz = Mathf.FloorToInt(gz);
            float fx = gx - ix, fz = gz - iz;
            float sx = fx * fx * (3f - 2f * fx), sz = fz * fz * (3f - 2f * fz);
            float a = Hash(ix, iz, semilla), b = Hash(ix + 1, iz, semilla);
            float c = Hash(ix, iz + 1, semilla), d = Hash(ix + 1, iz + 1, semilla);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sz);
        }

        /// Suma de octavas de ruido de valor, normalizada a [0,1].
        public static float Fbm(float x, float z, float escala, int semilla, int octavas = 3)
        {
            float total = 0f, amplitud = 1f, norma = 0f;
            for (int o = 0; o < octavas; o++)
            {
                total += Valor(x, z, escala, semilla + o * 31) * amplitud;
                norma += amplitud;
                amplitud *= 0.5f;
                escala *= 0.5f;
            }
            return total / norma;
        }

        public static float Suave(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// Generador lineal congruente para repartos (árboles, cajas…) sin depender de UnityEngine.Random.
        public sealed class Dado
        {
            private uint estado;
            public Dado(int semilla) { estado = (uint)semilla * 2654435761u + 1013904223u; }
            public float Siguiente()
            {
                unchecked { estado = estado * 1664525u + 1013904223u; }
                return (estado >> 8) / 16777216f;
            }
            public float Entre(float a, float b) => a + (b - a) * Siguiente();
            public int Indice(int n) => Mathf.Min(n - 1, (int)(Siguiente() * n));
        }
    }

    /// Distancia en planta de un punto a una polilínea.
    private static float DistanciaAPolilinea(Vector2 p, Vector2[] puntos)
    {
        float mejor = float.MaxValue;
        for (int j = 0; j < puntos.Length - 1; j++)
            mejor = Mathf.Min(mejor, DistanciaASegmento(p, puntos[j], puntos[j + 1]));
        return mejor;
    }

    private static float DistanciaASegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l = ab.sqrMagnitude;
        float t = l < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
        return Vector2.Distance(p, a + ab * t);
    }

    /// Curva cuadrática de a a b con el punto de control desplazado a un lado, para que las sendas no salgan rectas.
    private static Vector2[] Curva(Vector2 a, Vector2 b, float flexion, int semilla, int muestras = 10)
    {
        Vector2 medio = (a + b) * 0.5f;
        Vector2 d = b - a;
        float l = Mathf.Max(d.magnitude, 1e-4f);
        Vector2 normal = new Vector2(-d.y, d.x) / l;
        float desvio = (Ruido.Hash(Mathf.RoundToInt(a.x * 7f), Mathf.RoundToInt(b.y * 13f), semilla) - 0.5f) * 2f * flexion * l;
        Vector2 c = medio + normal * desvio;
        var r = new Vector2[muestras];
        for (int i = 0; i < muestras; i++)
        {
            float t = i / (muestras - 1f);
            r[i] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
        }
        return r;
    }
}
