using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Informe temporal (INC-969): color medio de cada pieza del pack modular, leído del atlas por sus UV.</summary>
public static class InformeColoresDePiezas
{
    [MenuItem("El Sendero/Archivo/Informe temporal: colores de las piezas del pack")]
    public static void Informe()
    {
        var raiz = PrefabUtility.LoadPrefabContents("Assets/_NPCs/Sofia.prefab");
        var sb = new StringBuilder();
        try
        {
            Texture2D leible = null;
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(r.name, @"^(Hair|Hat|Body|Cloak|AC1[01])")) continue;
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                var tex = r.sharedMaterial != null ? r.sharedMaterial.mainTexture as Texture2D : null;
                if (m == null || tex == null) continue;
                if (leible == null)
                {
                    var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(tex, rt);
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    leible = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    leible.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0); leible.Apply();
                    RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                }
                var uvs = m.uv;
                if (uvs == null || uvs.Length == 0) continue;
                // Colores por frecuencia (redondeados), los tres más comunes.
                var cuenta = uvs.Select(uv => leible.GetPixelBilinear(uv.x, uv.y))
                    .GroupBy(c => $"{Mathf.RoundToInt(c.r * 8)}{Mathf.RoundToInt(c.g * 8)}{Mathf.RoundToInt(c.b * 8)}")
                    .OrderByDescending(g => g.Count()).Take(3)
                    .Select(g => { var c = g.First(); return $"#{ColorUtility.ToHtmlStringRGB(c)}({g.Count() * 100 / uvs.Length}%)"; });
                sb.AppendLine($"{r.name}: {string.Join(" ", cuenta)}");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
        File.WriteAllText(Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes", "colores_piezas.txt"), sb.ToString());
    }
}
