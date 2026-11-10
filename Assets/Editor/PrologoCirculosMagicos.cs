#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Los círculos mágicos del prólogo (el azul del Archimago y el morado del Mago Oscuro) son los de Hovl
/// sin las paredes de luz que suben del borde («Sides») ni las chispas que suben («Sparks»): tapaban
/// al personaje que estaba encima, y con ellas no se veía al Mago levitando ni el orbe del Archimago.
/// Ver INC-978. Se puede ejecutar varias veces: rehace las dos copias.
public static class PrologoCirculosMagicos
{
    const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/";
    const string Carpeta = "Assets/_VFX/Prologo/";
    static readonly string[] Fuera = { "Sides", "Sparks" };

    [MenuItem("El Sendero/Archivo/Prólogo/Círculos mágicos sin paredes de luz (INC-978)")]
    public static void Hacer()
    {
        string informe = Copia(Hovl + "Magic circle.prefab", Carpeta + "VFX_CirculoDelArchimago.prefab") + " "
                       + Copia(Hovl + "Magic circle 2.prefab", Carpeta + "VFX_CirculoDelMagoOscuro.prefab");
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Círculos del prólogo", informe);
    }

    static string Copia(string origen, string destino)
    {
        var raiz = PrefabUtility.LoadPrefabContents(origen);
        try
        {
            int quitados = 0;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == raiz.transform) continue;
                if (System.Array.IndexOf(Fuera, t.name) < 0) continue;
                Object.DestroyImmediate(t.gameObject);
                quitados++;
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, destino);
            return $"{System.IO.Path.GetFileName(destino)}: {quitados} piezas fuera.";
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
#endif
