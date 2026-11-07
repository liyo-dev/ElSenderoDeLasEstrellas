using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Los puntos con nombre del recorrido de la feria y de la Caja (diseño del nivel, INC-891), sobre
/// las piezas del pack que hacen de cada parada, y un rótulo de maqueta sobre cada uno. Los puntos
/// que ya existen se respetan (se pueden mover a mano); los rótulos se rehacen siempre encima de
/// los puntos.
/// </summary>
public static partial class ParqueBuilder
{
    private const string RaizDePuntos = "Puntos del parque";
    private const string RaizDeRotulos = "Rótulos de maqueta";

    /// <summary>Nombre del punto y dónde va, en coordenadas del parque (junto a la pieza del pack).</summary>
    private static readonly (string nombre, Vector3 dondeEnElParque)[] Paradas =
    {
        ("Anillas (puesto stall_007)", new Vector3(56.4f, 0f, -19.5f)),
        ("Retrato mágico (taquilla ticket_stall_002)", new Vector3(47.4f, 0f, -9.5f)),
        ("Helados (heladería stall_002)", new Vector3(83.5f, 3.6f, 26.1f)),
        ("Teatro: el público", new Vector3(73.5f, 3.6f, 28f)),
        ("Banco: Liam se calla", new Vector3(77f, 3.6f, 49.5f)),
        ("La caja (tras las carpas)", new Vector3(111f, 0f, 56f)),
    };

    private static string Recorrido(Scene escena, Transform parque)
    {
        var resumen = new StringBuilder();
        Transform puntos = RaizDeLaEscena(escena, RaizDePuntos, crear: true);
        int nuevos = 0;
        foreach (var (nombre, donde) in Paradas)
        {
            if (puntos.Find(nombre) != null) continue;
            Vector3 cerca = parque.TransformPoint(donde);
            if (!EscenaJugable.BuscarSitioLibre(cerca, 6f, out Vector3 pie)) pie = cerca;
            KitDeLaberintoDeEspejos.Punto(puntos, nombre, pie, Vector3.forward);
            nuevos++;
        }

        Transform rotulos = RaizDeLaEscena(escena, RaizDeRotulos, crear: false);
        if (rotulos != null) Object.DestroyImmediate(rotulos.gameObject);
        rotulos = RaizDeLaEscena(escena, RaizDeRotulos, crear: true);
        int total = 0;
        foreach (Transform p in puntos) { RotuloSobre(rotulos, p); total++; }
        foreach (string otro in new[] { MarcaDeEntrada, RaizDeAtracciones })
        {
            Transform t = RaizDeLaEscena(escena, otro, crear: false);
            if (t == null) continue;
            if (t.name == MarcaDeEntrada) { RotuloSobre(rotulos, t); total++; continue; }
            foreach (Transform hijo in t) { RotuloSobre(rotulos, hijo); total++; }
        }
        resumen.AppendLine($"Recorrido: {nuevos} puntos nuevos en «{RaizDePuntos}», {total} rótulos de maqueta.");
        return resumen.ToString();
    }

    /// <summary>Rótulo sobre el punto, siempre de cara a la cámara (se lee desde cualquier lado).</summary>
    private static void RotuloSobre(Transform rotulos, Transform punto)
    {
        KitDeLaberintoDeEspejos.Rotulo(rotulos, punto.name, punto.position, Vector3.forward, 4f);
        Transform rotulo = rotulos.GetChild(rotulos.childCount - 1);
        if (rotulo.GetComponent<BillboardUI>() == null) rotulo.gameObject.AddComponent<BillboardUI>();
    }

    private static Transform RaizDeLaEscena(Scene escena, string nombre, bool crear)
    {
        foreach (GameObject r in escena.GetRootGameObjects())
            if (r.name == nombre) return r.transform;
        if (!crear) return null;
        var go = new GameObject(nombre);
        SceneManager.MoveGameObjectToScene(go, escena);
        return go.transform;
    }
}
