#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Los dos estados del Mago Oscuro (Tools/Modelos/MagoOscuro/): en el prólogo es una figura de sombra con la capucha
/// puesta y solo se ven sus ojos de luz; al final se ve su cara (piel gris lavanda, boca, ojeras y pelo con el mechón gris),
/// con la misma capucha y la misma silueta. Cada estado es un prefab: <c>_MAGO_OSCURO</c> (sombra, el de siempre) y
/// <c>_MAGO_OSCURO_REVELADO</c> (copia suya con la cabeza al descubierto y el pelo). Esto enciende la cabeza del pack en el
/// primero y rehace el segundo a partir de él; después, <see cref="CarasDeLosNPCs"/> les pone cada juego de caras
/// (ojos de luz / cara del final) y <see cref="TonosDePiel"/> el tono (sombra / lavanda).
/// Orden: «Vestuario propio de los NPCs», este menú, «Caras de los NPCs». Se puede repetir.
/// </summary>
public static class MagoOscuroPorEstados
{
    const string Sombra = "Assets/Prefabs/_MAGO_OSCURO.prefab";
    const string Revelado = "Assets/Prefabs/_MAGO_OSCURO_REVELADO.prefab";
    const string Cabeza = "Head01_Male";
    const string Pelo = "Hair19_MagoOscuro";

    [MenuItem("El Sendero/Personajes/Mago Oscuro: sombra y revelado", priority = 61)]
    public static void Preparar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Mago Oscuro", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Preparar(informe);
        AssetDatabase.SaveAssets();
        OrdenesDelEditor.Avisar("Mago Oscuro: sombra y revelado", informe.ToString());
    }

    public static void Preparar(StringBuilder informe)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Sombra) == null) { informe.AppendLine($"No encuentro {Sombra}."); return; }
        var raiz = PrefabUtility.LoadPrefabContents(Sombra);
        try
        {
            if (!Tiene(raiz, Pelo)) { informe.AppendLine($"Falta {Pelo} en el Mago Oscuro: pasa antes «Vestuario propio de los NPCs»."); return; }
            if (!Tiene(raiz, Cabeza)) { informe.AppendLine($"El Mago Oscuro no tiene {Cabeza}."); return; }

            // Sombra: la cabeza del pack bajo la capucha (con tono de sombra), sin pelo
            PiezaNuevaDelModelo.Encender(raiz, new[] { Cabeza }, new[] { Pelo });
            // Los ojos de luz brillan más mientras habla (los materiales los pone «Caras de los NPCs»)
            if (raiz.GetComponent<BrilloAlHablar>() == null) raiz.AddComponent<BrilloAlHablar>();
            PrefabUtility.SaveAsPrefabAsset(raiz, Sombra);

            // Revelado: lo mismo, con el pelo, en un prefab aparte (se rehace desde el de sombra)
            PiezaNuevaDelModelo.Encender(raiz, new[] { Cabeza, Pelo }, new string[0]);
            var brillo = raiz.GetComponent<BrilloAlHablar>();
            if (brillo != null) Object.DestroyImmediate(brillo);   // la cara del final no tiene ojos de luz
            raiz.name = System.IO.Path.GetFileNameWithoutExtension(Revelado);
            PrefabUtility.SaveAsPrefabAsset(raiz, Revelado);
            informe.AppendLine("Mago Oscuro: sombra (cabeza bajo la capucha, ojos que brillan más al hablar) y revelado (cabeza y pelo) preparados.");
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    static bool Tiene(GameObject raiz, string pieza)
    {
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true)) if (r.name == pieza) return true;
        return false;
    }
}
#endif
