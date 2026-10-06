#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// Escritor de JSON mínimo y sin dependencias, para los ficheros que leen las herramientas de
/// guion (y Claude, fuera de Unity). JsonUtility no sabe escribir diccionarios ni listas sueltas.
public sealed class GuionJson
{
    private readonly StringBuilder _sb = new();
    private readonly Stack<bool> _primero = new();
    private int _nivel;

    public static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    public GuionJson Obj(string clave = null) { Clave(clave); _sb.Append('{'); _primero.Push(true); _nivel++; return this; }
    public GuionJson FinObj() { _nivel--; _primero.Pop(); Salto(); _sb.Append('}'); return this; }
    public GuionJson Arr(string clave = null) { Clave(clave); _sb.Append('['); _primero.Push(true); _nivel++; return this; }
    public GuionJson FinArr() { _nivel--; _primero.Pop(); _sb.Append(']'); return this; }

    public GuionJson Val(string clave, string v) { Clave(clave); Texto(v); return this; }
    public GuionJson Val(string clave, float v) { Clave(clave); _sb.Append(F(v)); return this; }
    public GuionJson Val(string clave, int v) { Clave(clave); _sb.Append(v); return this; }
    public GuionJson Val(string clave, bool v) { Clave(clave); _sb.Append(v ? "true" : "false"); return this; }
    public GuionJson Vec(string clave, Vector3 v)
    {
        Clave(clave);
        _sb.Append('[').Append(F(v.x)).Append(',').Append(F(v.y)).Append(',').Append(F(v.z)).Append(']');
        return this;
    }
    public GuionJson Num(float v) { Clave(null); _sb.Append(F(v)); return this; }
    public GuionJson Ent(int v) { Clave(null); _sb.Append(v); return this; }
    public GuionJson Txt(string v) { Clave(null); Texto(v); return this; }

    private void Clave(string clave)
    {
        if (_primero.Count > 0)
        {
            if (!_primero.Peek()) _sb.Append(',');
            _primero.Pop(); _primero.Push(false);
            if (clave != null || _nivel <= 2) Salto();
        }
        if (clave != null) { Texto(clave); _sb.Append(':'); }
    }

    private void Salto() { _sb.Append('\n').Append(' ', _nivel * 1); }

    private void Texto(string v)
    {
        if (v == null) { _sb.Append("null"); return; }
        _sb.Append('"');
        foreach (char c in v)
        {
            switch (c)
            {
                case '"': _sb.Append("\\\""); break;
                case '\\': _sb.Append("\\\\"); break;
                case '\n': _sb.Append("\\n"); break;
                case '\r': break;
                case '\t': _sb.Append("\\t"); break;
                default:
                    if (c < 32) _sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else _sb.Append(c);
                    break;
            }
        }
        _sb.Append('"');
    }

    public override string ToString() => _sb.ToString();
}

/// Un SVG sencillo encima de la planta del escenario: rejilla, marcas, caminos y rótulos. El SVG
/// se abre en cualquier navegador y los textos se leen bien a cualquier tamaño.
public sealed class LienzoSvg
{
    private readonly StringBuilder _sb = new();
    public readonly Rect Area;       // en metros: x = X del mundo, y = Z del mundo
    public readonly int Ancho, Alto; // en píxeles

    public LienzoSvg(Rect area, int ancho, int alto, string fondo)
    {
        Area = area; Ancho = ancho; Alto = alto;
        _sb.Append($"<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='{ancho}' height='{alto}' viewBox='0 0 {ancho} {alto}' font-family='Segoe UI, Arial, sans-serif'>\n");
        _sb.Append("<rect width='100%' height='100%' fill='#202020'/>\n");
        if (!string.IsNullOrEmpty(fondo))
            _sb.Append($"<image href='{fondo}' xlink:href='{fondo}' x='0' y='0' width='{ancho}' height='{alto}' opacity='0.85'/>\n");
    }

    public Vector2 P(Vector3 mundo)
        => new((mundo.x - Area.xMin) / Area.width * Ancho, (Area.yMax - mundo.z) / Area.height * Alto);

    private static string N(float v) => GuionJson.F(v);

    public void Crudo(string svg) => _sb.Append(svg).Append('\n');

    public void Rejilla(float paso, string color = "#ffffff", float opacidad = 0.18f)
    {
        float x0 = Mathf.Ceil(Area.xMin / paso) * paso;
        for (float x = x0; x <= Area.xMax; x += paso)
        {
            var a = P(new Vector3(x, 0, Area.yMin)); var b = P(new Vector3(x, 0, Area.yMax));
            bool rotulo = Mathf.Abs(Mathf.Repeat(x, paso * 2f)) < 0.01f;
            _sb.Append($"<line x1='{N(a.x)}' y1='{N(a.y)}' x2='{N(b.x)}' y2='{N(b.y)}' stroke='{color}' stroke-opacity='{N(rotulo ? opacidad * 1.8f : opacidad)}' stroke-width='1'/>\n");
            if (rotulo) Texto(new Vector2(a.x + 2, 12), N(x), 10, color, 0.8f);
        }
        float z0 = Mathf.Ceil(Area.yMin / paso) * paso;
        for (float z = z0; z <= Area.yMax; z += paso)
        {
            var a = P(new Vector3(Area.xMin, 0, z)); var b = P(new Vector3(Area.xMax, 0, z));
            bool rotulo = Mathf.Abs(Mathf.Repeat(z, paso * 2f)) < 0.01f;
            _sb.Append($"<line x1='{N(a.x)}' y1='{N(a.y)}' x2='{N(b.x)}' y2='{N(b.y)}' stroke='{color}' stroke-opacity='{N(rotulo ? opacidad * 1.8f : opacidad)}' stroke-width='1'/>\n");
            if (rotulo) Texto(new Vector2(2, a.y - 2), N(z), 10, color, 0.8f);
        }
    }

    public void Punto(Vector3 mundo, float radio, string color, string borde = "#000000")
    {
        var p = P(mundo);
        _sb.Append($"<circle cx='{N(p.x)}' cy='{N(p.y)}' r='{N(radio)}' fill='{color}' stroke='{borde}' stroke-width='1'/>\n");
    }

    public void Flecha(Vector3 mundo, float rumbo, float largo, string color)
    {
        var p = P(mundo);
        float rad = rumbo * Mathf.Deg2Rad;
        var q = new Vector2(p.x + Mathf.Sin(rad) * largo, p.y - Mathf.Cos(rad) * largo);
        _sb.Append($"<line x1='{N(p.x)}' y1='{N(p.y)}' x2='{N(q.x)}' y2='{N(q.y)}' stroke='{color}' stroke-width='2'/>\n");
    }

    public void Texto(Vector2 px, string texto, float tam, string color, float opacidad = 1f, string ancla = "start")
    {
        texto = System.Security.SecurityElement.Escape(texto);
        _sb.Append($"<text x='{N(px.x)}' y='{N(px.y)}' font-size='{N(tam)}' fill='{color}' fill-opacity='{N(opacidad)}' text-anchor='{ancla}' stroke='#000' stroke-width='2.5' paint-order='stroke' stroke-opacity='0.7'>{texto}</text>\n");
    }

    public void TextoEn(Vector3 mundo, string texto, float tam, string color, float dx = 6, float dy = -6)
    {
        var p = P(mundo);
        Texto(new Vector2(p.x + dx, p.y + dy), texto, tam, color);
    }

    public void Linea(IList<Vector3> puntos, string color, float grosor, float opacidad = 1f, string guiones = null)
    {
        if (puntos == null || puntos.Count < 2) return;
        var sb = new StringBuilder();
        foreach (var m in puntos) { var p = P(m); sb.Append(N(p.x)).Append(',').Append(N(p.y)).Append(' '); }
        string dash = string.IsNullOrEmpty(guiones) ? "" : $" stroke-dasharray='{guiones}'";
        _sb.Append($"<polyline points='{sb}' fill='none' stroke='{color}' stroke-width='{N(grosor)}' stroke-opacity='{N(opacidad)}' stroke-linejoin='round' stroke-linecap='round'{dash}/>\n");
    }

    public void Triangulo(Vector3 a, Vector3 b, Vector3 c, string color, float opacidad)
    {
        var pa = P(a); var pb = P(b); var pc = P(c);
        _sb.Append($"<polygon points='{N(pa.x)},{N(pa.y)} {N(pb.x)},{N(pb.y)} {N(pc.x)},{N(pc.y)}' fill='{color}' fill-opacity='{N(opacidad)}' stroke='none'/>\n");
    }

    public void Caja(Bounds b, string color, float opacidad)
    {
        var p0 = P(new Vector3(b.min.x, 0, b.max.z)); var p1 = P(new Vector3(b.max.x, 0, b.min.z));
        _sb.Append($"<rect x='{N(p0.x)}' y='{N(p0.y)}' width='{N(p1.x - p0.x)}' height='{N(p1.y - p0.y)}' fill='{color}' fill-opacity='{N(opacidad * 0.35f)}' stroke='{color}' stroke-opacity='{N(opacidad)}' stroke-width='1'/>\n");
    }

    public void Cono(Vector3 origen, Vector3 objetivo, float fov, string color, float opacidad)
    {
        var p = P(origen);
        Vector3 dir = objetivo - origen; dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return;
        float rumbo = Mathf.Atan2(dir.x, dir.z);
        float largo = Mathf.Min(dir.magnitude, 14f) / Area.width * Ancho;
        float media = fov * 0.5f * Mathf.Deg2Rad * 1.5f;
        var a = new Vector2(p.x + Mathf.Sin(rumbo - media) * largo, p.y - Mathf.Cos(rumbo - media) * largo);
        var b = new Vector2(p.x + Mathf.Sin(rumbo + media) * largo, p.y - Mathf.Cos(rumbo + media) * largo);
        _sb.Append($"<polygon points='{N(p.x)},{N(p.y)} {N(a.x)},{N(a.y)} {N(b.x)},{N(b.y)}' fill='{color}' fill-opacity='{N(opacidad)}' stroke='{color}' stroke-opacity='{N(opacidad * 2f)}'/>\n");
    }

    public string Cerrar() { _sb.Append("</svg>\n"); return _sb.ToString(); }
}
#endif
