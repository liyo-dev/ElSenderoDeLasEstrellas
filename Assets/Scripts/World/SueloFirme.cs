using UnityEngine;

/// <summary>
/// Dónde poner de pie a un personaje cerca de un punto: suelo (las capas dadas) plano, firme a su
/// alrededor —no el borde de una barandilla ni el canto de un peldaño, que también son suelo cuando
/// la escalera es una sola malla— y con sitio libre para el cuerpo. Prueba en espiral desde el punto.
/// La usan quien deja al jugador al bajar de algo (<see cref="PasajeroJugador"/>) y los constructores
/// de escenas para colocar marcas.
/// </summary>
public static class SueloFirme
{
    /// <summary>Capas que no estorban al cuerpo (zonas, agua, disparadores).</summary>
    public static int CapasQueNoEstorban => LayerMask.GetMask("Ignore Raycast", "Water", "AmbientZone", "Weather", "Interactable");

    private const float Apoyo = 0.3f;          // distancia a la que se mira que siga habiendo suelo alrededor
    private const float Desnivel = 0.15f;      // diferencia de altura admitida alrededor del pie
    private const float RadioDelCuerpo = 0.4f;
    private const float AltoDelCuerpo = 1.6f;

    /// <summary>
    /// Busca el pie más cercano a <paramref name="cerca"/> (hasta <paramref name="radio"/> m en planta)
    /// sobre <paramref name="suelo"/>, mirando desde <paramref name="desdeArriba"/> m por encima del
    /// punto hacia abajo. El cuerpo no puede tocar nada sólido salvo lo que excluye
    /// <paramref name="noEstorba"/>. Devuelve false (y el propio punto) si no hay ninguno.
    /// </summary>
    public static bool Buscar(Vector3 cerca, float radio, float desdeArriba, int suelo, int noEstorba, out Vector3 pie)
    {
        int estorba = Physics.DefaultRaycastLayers & ~noEstorba;
        float alcance = desdeArriba + 4f;
        for (float r = 0f; r <= radio; r += 0.5f)
        {
            int pasos = r < 0.25f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 0.5f);
            for (int i = 0; i < pasos; i++)
            {
                float a = i * Mathf.PI * 2f / pasos;
                Vector3 p = cerca + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!Physics.Raycast(new Vector3(p.x, cerca.y + desdeArriba, p.z), Vector3.down, out RaycastHit hit, alcance, suelo,
                        QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.normal.y < 0.8f || !Firme(hit.point, suelo)) continue;
                Vector3 candidato = hit.point + Vector3.up * 0.02f;
                if (Physics.CheckCapsule(candidato + Vector3.up * (0.45f + RadioDelCuerpo), candidato + Vector3.up * (AltoDelCuerpo - RadioDelCuerpo),
                        RadioDelCuerpo, estorba, QueryTriggerInteraction.Ignore))
                    continue;
                pie = candidato;
                return true;
            }
        }
        pie = cerca;
        return false;
    }

    /// <summary>True si alrededor del punto, en cuatro direcciones, sigue habiendo suelo casi a su altura.</summary>
    static bool Firme(Vector3 punto, int suelo)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 lado = Quaternion.Euler(0f, 90f * i, 0f) * Vector3.forward * Apoyo;
            Vector3 desde = punto + lado + Vector3.up * Desnivel;
            if (!Physics.Raycast(desde, Vector3.down, out RaycastHit hit, Desnivel * 2f, suelo, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.normal.y < 0.8f) return false;
        }
        return true;
    }
}
