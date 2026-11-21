using Kits.InkOutline;
using UnityEngine;

/// Enchufe de la línea de tinta con El Sendero: las piezas de la cara (ojos, bocas, cejas y símbolos,
/// que van pegadas delante de la cabeza) no llevan línea; si la llevaran saldría un anillo alrededor.
public static class LineaDeTintaDelJuego
{
    private static readonly string[] PiezasDeLaCara = { "Eye", "Mouth", "Fx", "Caras", "Eyebrow" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Enchufar() => InkOutlined.Excluded = EsPiezaDeLaCara;

    public static bool EsPiezaDeLaCara(Renderer r)
    {
        string nombre = r.name;
        foreach (var prefijo in PiezasDeLaCara)
            if (nombre.StartsWith(prefijo, System.StringComparison.Ordinal)) return true;
        return false;
    }
}
