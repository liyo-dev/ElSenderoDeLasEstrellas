#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Herramienta temporal (se retira al acabar el vestido del prólogo): fotografía en fila las piezas
/// de una carpeta del pack para elegir cuáles usar. Lee «Guiones/_fotos/catalogo.txt»: cada línea,
/// «nombre|carpeta|filtro» (filtro = expresión regular del nombre del prefab).
public static class CatalogoDePiezasTemporal
{
    [MenuItem("El Sendero/Archivo/Temporal: catálogo de piezas")]
    public static void Hacer()
    {
        string lista = Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_fotos", "catalogo.txt");
        var sb = new StringBuilder();
        foreach (var viejo in Resources.FindObjectsOfTypeAll<GameObject>()) if (viejo != null && viejo.name == "Catálogo temporal" && viejo.transform.parent == null && !EditorUtility.IsPersistent(viejo)) Object.DestroyImmediate(viejo);
        foreach (var l in File.ReadAllLines(lista))
        {
            var p = l.Split(';');
            if (p.Length < 3) continue;
            var raiz = new GameObject("Catálogo temporal") { hideFlags = HideFlags.DontSave };
            raiz.transform.position = new Vector3(0f, 2000f, 0f);
            float x = 0f;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { p[1].Trim() }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(ruta);
                if (!System.Text.RegularExpressions.Regex.IsMatch(n, "^" + p[2].Trim() + "$")) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                var pz = VestidoDeFachadas.MedirPieza(pf);
                if (pz == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(raiz.transform, false);
                Quaternion giro = Quaternion.FromToRotation(pz.fuera, Vector3.back);
                if (Vector3.Dot(pz.fuera, Vector3.back) < -0.999f) giro = Quaternion.AngleAxis(180f, Vector3.up);
                go.transform.rotation = giro;
                go.transform.position = raiz.transform.position + new Vector3(x, 0f, 0f) - giro * (pz.lateral * pz.lMin + Vector3.up * pz.yMin + pz.fuera * pz.dMin);
                sb.Append($"{n}: {pz.Ancho:0.00}x{pz.Alto:0.00}x{pz.Fondo:0.00} fuera={pz.fuera} · ");
                x += pz.Ancho + 0.4f;
            }
            Vector3 c = raiz.transform.position + new Vector3(x * 0.5f, 1.2f, 0f);
            float dist = Mathf.Max(4f, x * 0.9f);
            CapturaDeEscenario.FotoDesde(p[0].Trim(), c + new Vector3(0f, 0.5f, -dist), c, 40f, 2400, 900);
            Object.DestroyImmediate(raiz);
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_fotos", "catalogo_medidas.txt"), sb.ToString());
        OrdenesDelEditor.Avisar("Catálogo", "hecho");
    }
}
#endif
