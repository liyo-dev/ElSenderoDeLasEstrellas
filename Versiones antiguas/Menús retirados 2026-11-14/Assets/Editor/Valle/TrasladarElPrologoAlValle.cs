using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Valle del prólogo (INC-1200): lleva el pueblo del prólogo (Prologo_Valle, alrededor de (6000, 100, 6000)) al valle
/// que el vestido de MainWorld esculpe en la costa suroeste, sumando VestidoDelMundo.DesplazamientoDelPrologo a todo.
///
/// · El suelo, las lomas y la vegetación suelta del módulo viejo se apagan (no se borran): el suelo es el de MainWorld.
/// · El NavMesh del prólogo pasa a hornear solo el volumen del valle (con MainWorld abierta, su terreno es el suelo).
/// · Los PUNTOS del guion Prologo_UltimaNoche se mueven lo mismo (x, z y altura). Después hay que rehornear el NavMesh,
///   pegar las marcas al suelo y hornear el guion.
/// Una sola vez: deja una marca en la escena y no vuelve a mover nada si la encuentra.
public static class TrasladarElPrologoAlValle
{
    const string RutaPrologo = "Assets/Scenes/Worlds/Prologo_Valle.unity";
    const string RutaMainWorld = "Assets/Scenes/Worlds/MainWorld.unity";
    const string RutaGuion = "Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt";
    public const string Marca = "Prólogo en el valle de MainWorld (INC-1200, no tocar)";

    static readonly string[] Apagar =
    {
        "ISLA_POSTGAME_MODULO/Terreno_Costa_Quibli",
        "ISLA_POSTGAME_MODULO/Montana_Prologo",
        "ISLA_POSTGAME_MODULO/Rio_Cauce_Quibli",
        "ISLA_POSTGAME_MODULO/DECORADO/00_Camino",
        "ISLA_POSTGAME_MODULO/DECORADO/06_Vegetacion",
        "ISLA_POSTGAME_MODULO/DECORADO/07_Relieve",
        "Vestido del pueblo (generado)/Campo del valle",
        "Vestido del pueblo (generado)/Suelo pintado del pueblo",
    };

    [MenuItem("El Sendero/Escenario/Valle del prólogo: llevar Prologo_Valle al valle de MainWorld (una vez)", priority = 62)]
    public static void Trasladar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Valle del prólogo", "Hazlo fuera de Play."); return; }
        var informe = new StringBuilder();
        Scene mw = Abrir(RutaMainWorld), pv = Abrir(RutaPrologo);
        if (!mw.IsValid() || !pv.IsValid()) { OrdenesDelEditor.Avisar("Valle del prólogo", "No he podido abrir MainWorld y Prologo_Valle."); return; }
        foreach (var r in pv.GetRootGameObjects())
            if (r.name == Marca) { OrdenesDelEditor.Avisar("Valle del prólogo", "El prólogo ya está en el valle de MainWorld; no se mueve nada."); return; }

        Vector3 d = VestidoDelMundo.DesplazamientoDelPrologo;
        int apagados = 0;
        foreach (string ruta in Apagar)
        {
            Transform t = Buscar(pv, ruta);
            if (t == null) { informe.Append($"no está «{ruta}»; "); continue; }
            if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); apagados++; }
        }

        int movidas = 0;
        foreach (GameObject r in pv.GetRootGameObjects())
        {
            if (r.GetComponentInChildren<NavMeshSurface>(true) != null && r.transform.position == Vector3.zero && r.transform.childCount <= 2 && r.name == "AI")
            {
                foreach (var s in r.GetComponentsInChildren<NavMeshSurface>(true))
                {
                    s.collectObjects = CollectObjects.Volume;
                    s.center = s.transform.InverseTransformPoint(new Vector3(-165f, 42f, -360f));
                    s.size = new Vector3(230f, 90f, 220f);
                    EditorUtility.SetDirty(s);
                }
                continue;
            }
            r.transform.position += d;
            movidas++;
        }
        var marca = new GameObject(Marca);
        SceneManager.MoveGameObjectToScene(marca, pv);
        marca.transform.position = new Vector3(-170f, 34f, -365f);

        EditorSceneManager.MarkSceneDirty(pv);
        EditorSceneManager.SaveScene(pv);
        int puntos = MoverPuntosDelGuion(d);
        AssetDatabase.Refresh();
        string texto = $"Prólogo llevado al valle de MainWorld: {movidas} raíces movidas {d}, {apagados} piezas del módulo viejo apagadas, " +
                       $"NavMesh del prólogo a volumen del valle, {puntos} puntos del guion movidos. {informe}" +
                       "Falta: rehornear el NavMesh, pegar las marcas al suelo y hornear el guion.";
        Debug.Log("[Valle] " + texto);
        OrdenesDelEditor.Avisar("Valle del prólogo", texto);
    }

    static int MoverPuntosDelGuion(Vector3 d)
    {
        string ruta = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", RutaGuion);
        var lineas = File.ReadAllLines(ruta);
        bool enPuntos = false;
        int n = 0;
        var ci = CultureInfo.InvariantCulture;
        for (int i = 0; i < lineas.Length; i++)
        {
            string l = lineas[i];
            string t = l.Trim();
            if (t == "PUNTOS") { enPuntos = true; continue; }
            if (t == "PISTAS" || t == "HILO" || t == "REPARTO") { enPuntos = false; continue; }
            if (!enPuntos || t.Length == 0 || t.StartsWith("#")) continue;
            int com = l.IndexOf('#');
            string cuerpo = com >= 0 ? l.Substring(0, com) : l, resto = com >= 0 ? l.Substring(com) : "";
            var tok = new List<string>(cuerpo.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (tok.Count < 3) continue;
            if (!float.TryParse(tok[1], NumberStyles.Float, ci, out float x) || !float.TryParse(tok[2], NumberStyles.Float, ci, out float z)) continue;
            if (x < 5700f || x > 6300f || z < 5700f || z > 6300f) continue;
            string nueva = tok[0].PadRight(Mathf.Max(17, tok[0].Length + 1)) + (x + d.x).ToString("0.0#", ci) + " " + (z + d.z).ToString("0.0#", ci);
            if (tok.Count >= 4 && float.TryParse(tok[3], NumberStyles.Float, ci, out float y)) nueva += " " + (y + d.y).ToString("0.0#", ci);
            for (int k = 4; k < tok.Count; k++) nueva += " " + tok[k];
            lineas[i] = nueva + (resto.Length > 0 ? "  " + resto : "");
            n++;
        }
        File.WriteAllLines(ruta, lineas, new UTF8Encoding(false));
        return n;
    }

    static Scene Abrir(string ruta)
    {
        Scene s = SceneManager.GetSceneByPath(ruta);
        if (s.IsValid() && s.isLoaded) return s;
        return EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
    }

    static Transform Buscar(Scene escena, string ruta)
    {
        string[] partes = ruta.Split('/');
        foreach (GameObject r in escena.GetRootGameObjects())
        {
            if (r.name != partes[0]) continue;
            Transform t = r.transform;
            for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
            if (t != null) return t;
        }
        return null;
    }
}
