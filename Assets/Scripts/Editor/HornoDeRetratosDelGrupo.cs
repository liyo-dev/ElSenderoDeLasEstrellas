using System.IO;
using System.Text;
using Sendero.UI;
using UnityEditor;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Rehace las caras «normal» del grupo (<c>Assets/Art/UI/Retratos/Grupo/&lt;nombre&gt;_normal.png</c>)
/// con lo que pinta el <see cref="EstudioDeRetratos"/> en Play: el modelo de ahora (pelo, ropa) en
/// vez de la foto vieja. Esas imágenes son las que salen donde no hay estudio en vivo: «Tu partida»
/// del menú principal, el informe de victoria y las tarjetas del grafo. Se sobrescribe el PNG (el
/// GUID no cambia). Solo los personajes que estén en el grupo en ese momento.
/// </summary>
public static class HornoDeRetratosDelGrupo
{
    const string Menu = "El Sendero/UI/Retratos del grupo: hornear las caras desde el estudio (en Play)";
    const string Carpeta = "Assets/Art/UI/Retratos/Grupo/";
    const int Lado = 512;

    [MenuItem(Menu)]
    public static void Hornear()
    {
        var estudio = EstudioDeRetratos.Activo;
        if (!Application.isPlaying || estudio == null)
        {
            OrdenesDelEditor.Avisar("Retratos del grupo", "Entra en Play en una escena con el grupo y el HUD (el estudio de retratos tiene que estar activo).");
            return;
        }
        var informe = new StringBuilder();
        foreach (var slot in new[] { Slot.Will, Slot.Estela, Slot.Liam })
        {
            if (!estudio.TryObtener(slot, ExpresionDeRetrato.Normal, out var img) || !img.Valida || img.TexturaSobreFondo == null)
            {
                informe.AppendLine(slot + ": no está en el estudio (no está en el grupo)");
                continue;
            }
            var color = Leer(img.Textura, img.Uv);
            var fondo = Leer(img.TexturaSobreFondo, img.Uv);
            var limpia = Recortar(color, fondo);
            string ruta = Carpeta + slot.ToString().ToLowerInvariant() + "_normal.png";
            File.WriteAllBytes(ruta, limpia.EncodeToPNG());
            Object.DestroyImmediate(color); Object.DestroyImmediate(fondo); Object.DestroyImmediate(limpia);
            AssetDatabase.ImportAsset(ruta);
            informe.AppendLine(slot + ": " + ruta);
        }
        OrdenesDelEditor.Avisar("Retratos del grupo", informe.ToString());
    }

    static Texture2D Leer(Texture textura, Rect uv)
    {
        int x = Mathf.RoundToInt(uv.x * textura.width), y = Mathf.RoundToInt(uv.y * textura.height);
        int w = Mathf.RoundToInt(uv.width * textura.width), h = Mathf.RoundToInt(uv.height * textura.height);
        var anterior = RenderTexture.active;
        RenderTexture.active = textura as RenderTexture;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(x, y, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = anterior;
        return tex;
    }

    /// Lo mismo que hace el shader del retrato: la cobertura sale de lo que cambia entre el dibujo
    /// sobre negro y sobre claro, y el color se desmultiplica. Se encaja en un cuadrado de 512,
    /// centrado y apoyado abajo.
    static Texture2D Recortar(Texture2D sobreNegro, Texture2D sobreClaro)
    {
        int w = sobreNegro.width, h = sobreNegro.height;
        var a = sobreNegro.GetPixels();
        var b = sobreClaro.GetPixels();
        var limpio = new Color[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            var d = b[i] - a[i];
            float cobertura = Mathf.Clamp01(1f - (d.r + d.g + d.b) / 3f);
            var c = a[i] / Mathf.Max(cobertura, 0.004f);
            c.a = cobertura;
            limpio[i] = c;
        }
        float k = Mathf.Min((float)Lado / w, (float)Lado / h);
        int nw = Mathf.RoundToInt(w * k), nh = Mathf.RoundToInt(h * k), x0 = (Lado - nw) / 2;
        var salida = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
        var px = new Color[Lado * Lado];
        for (int y = 0; y < nh; y++)
        for (int x = 0; x < nw; x++)
        {
            float u = (x + 0.5f) / nw * w - 0.5f, v = (y + 0.5f) / nh * h - 0.5f;
            int ix = Mathf.Clamp(Mathf.RoundToInt(u), 0, w - 1), iy = Mathf.Clamp(Mathf.RoundToInt(v), 0, h - 1);
            px[y * Lado + x0 + x] = limpio[iy * w + ix];
        }
        salida.SetPixels(px);
        salida.Apply();
        return salida;
    }
}
