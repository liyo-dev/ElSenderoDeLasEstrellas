#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Pelo de dibujo animado (INC-1352): al importar los pelos de los personajes, sus normales se sustituyen
/// por las de un elipsoide que envuelve la malla. La forma no cambia (los picos siguen ahí), pero la luz de
/// dos tonos ve una sola masa redondeada: el pelo se pinta con una zona de luz y una de sombra grandes, en
/// vez de una sombra por cada pico. Afecta a los FBX cuyo nombre empieza por «Hair» dentro de
/// Assets/Art/Characters. El menú los reimporta; para quitarlo, desactivar con el menú de vuelta.
public sealed class NormalesDeForma : AssetPostprocessor
{
    private const string Carpeta = "Assets/Art/Characters/";
    private const string Clave = "ElSendero.NormalesDeForma.Activo";

    public override int GetPostprocessOrder() => -10;   // antes de las normales de la línea de tinta
    public override uint GetVersion() => 2;

    private static bool Activo => EditorPrefs.GetBool(Clave, true);

    private static bool EsPelo(string ruta) =>
        ruta.StartsWith(Carpeta) && ruta.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)
        && System.IO.Path.GetFileName(ruta).StartsWith("Hair");

    private void OnPostprocessModel(GameObject raiz)
    {
        if (!Activo || !EsPelo(assetPath)) return;
        foreach (var piel in raiz.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Aplicar(piel.sharedMesh);
        foreach (var filtro in raiz.GetComponentsInChildren<MeshFilter>(true)) Aplicar(filtro.sharedMesh);
    }

    private static void Aplicar(Mesh malla)
    {
        if (malla == null) return;
        var v = malla.vertices;
        if (v.Length == 0) return;
        var caja = new Bounds(v[0], Vector3.zero);
        for (int i = 1; i < v.Length; i++) caja.Encapsulate(v[i]);
        // Centro algo bajo (el pelo envuelve la cabeza por arriba) y radios de la caja.
        Vector3 centro = caja.center - new Vector3(0f, caja.extents.y * 0.15f, 0f);
        Vector3 radios = Vector3.Max(caja.extents, Vector3.one * 1e-4f);
        var originales = malla.normals;
        var n = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 d = v[i] - centro;
            Vector3 normal = new Vector3(d.x / (radios.x * radios.x), d.y / (radios.y * radios.y), d.z / (radios.z * radios.z));
            // Un poco de la normal original para que los mechones no queden del todo planos.
            if (originales != null && originales.Length == v.Length) normal = normal.normalized * 0.8f + originales[i] * 0.2f;
            n[i] = normal.sqrMagnitude > 1e-10f ? normal.normalized : Vector3.up;
        }
        malla.normals = n;
    }

    [MenuItem("El Sendero/Aspecto de dibujo/Pelo de dibujo: aplicar a los pelos")]
    public static void AplicarATodos() { EditorPrefs.SetBool(Clave, true); Reimportar(); }

    [MenuItem("El Sendero/Aspecto de dibujo/Pelo de dibujo: volver a las normales del modelo")]
    public static void Quitar() { EditorPrefs.SetBool(Clave, false); Reimportar(); }

    private static void Reimportar()
    {
        int n = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Characters" }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (!EsPelo(ruta)) continue;
                AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
                n++;
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        OrdenesDelEditor.Avisar("Pelo", $"{n} pelos reimportados (normales de forma: {Activo})");
    }
}
#endif
