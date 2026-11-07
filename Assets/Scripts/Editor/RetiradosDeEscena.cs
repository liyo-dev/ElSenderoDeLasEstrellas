using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Retirar de una escena, desde un menú de montaje, lo que ya estaba y estorba a lo nuevo, sin
/// borrarlo: queda inactivo y con la etiqueta EditorOnly (no llega a la build), y su estado de antes
/// se apunta en un registro de la propia escena (un objeto raíz con una marca por retirado:
/// «ruta|GlobalObjectId» y su estado «estado|activo|etiqueta»). <see cref="Reponer"/> lo devuelve
/// todo como estaba; un montaje idempotente repone al empezar y vuelve a retirar lo que toque.
/// </summary>
public static class RetiradosDeEscena
{
    private const string PrefijoEstado = "estado|";

    /// <summary>Retira el objeto y lo apunta en el registro. False si ya estaba retirado.</summary>
    public static bool Retirar(Scene escena, GameObject go, string nombreDelRegistro)
    {
        if (go == null) return false;
        string id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
        var registro = Registro(escena, nombreDelRegistro, crear: true);
        foreach (Transform marca in registro)
            if (marca.name.EndsWith("|" + id)) return false;

        var nueva = new GameObject(Ruta(go.transform) + "|" + id) { tag = "EditorOnly" };
        nueva.transform.SetParent(registro, false);
        var estado = new GameObject(PrefijoEstado + go.activeSelf + "|" + go.tag) { tag = "EditorOnly" };
        estado.transform.SetParent(nueva.transform, false);
        go.SetActive(false);
        go.tag = "EditorOnly";
        GuardarOverrides(go);
        return true;
    }

    /// <summary>Devuelve todo lo retirado a su estado de antes y borra el registro. Devuelve cuántos.</summary>
    public static int Reponer(Scene escena, string nombreDelRegistro, StringBuilder informe = null)
    {
        var registro = Registro(escena, nombreDelRegistro, crear: false);
        if (registro == null) return 0;
        var marcas = new List<Transform>();
        foreach (Transform marca in registro) marcas.Add(marca);

        int repuestos = 0, perdidos = 0;
        foreach (var marca in marcas)
        {
            int corte = marca.name.LastIndexOf('|');
            GameObject go = null;
            if (corte >= 0 && GlobalObjectId.TryParse(marca.name.Substring(corte + 1), out var gid))
                go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
            if (go == null || go.scene != escena) { perdidos++; continue; }
            ReponerEstado(go, marca);
            Object.DestroyImmediate(marca.gameObject);
            repuestos++;
        }
        if (registro.childCount == 0) Object.DestroyImmediate(registro.gameObject);
        if (perdidos > 0) informe?.AppendLine($"«{nombreDelRegistro}»: {perdidos} retirados ya no están en la escena; su marca se conserva.");
        return repuestos;
    }

    private static void ReponerEstado(GameObject go, Transform marca)
    {
        bool activo = true;
        string etiqueta = "Untagged";
        foreach (Transform hijo in marca)
        {
            if (!hijo.name.StartsWith(PrefijoEstado)) continue;
            string[] partes = hijo.name.Split(new[] { '|' }, 3);
            if (partes.Length == 3 && bool.TryParse(partes[1], out bool a)) { activo = a; etiqueta = partes[2]; }
            break;
        }
        try { go.tag = etiqueta; }
        catch (UnityException) { go.tag = "Untagged"; }
        go.SetActive(activo);
        GuardarOverrides(go);

        // En una instancia de prefab, si queda como el prefab, se quita el override que dejó la retirada.
        var fuente = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (fuente == null) return;
        var so = new SerializedObject(go);
        var activa = so.FindProperty("m_IsActive");
        var tag = so.FindProperty("m_TagString");
        if (activa != null && activa.prefabOverride && fuente.activeSelf == go.activeSelf)
            PrefabUtility.RevertPropertyOverride(activa, InteractionMode.AutomatedAction);
        if (tag != null && tag.prefabOverride && fuente.tag == go.tag)
            PrefabUtility.RevertPropertyOverride(tag, InteractionMode.AutomatedAction);
    }

    private static Transform Registro(Scene escena, string nombre, bool crear)
    {
        foreach (var raiz in escena.GetRootGameObjects())
            if (raiz.name == nombre) return raiz.transform;
        if (!crear) return null;
        var go = new GameObject(nombre) { tag = "EditorOnly" };
        SceneManager.MoveGameObjectToScene(go, escena);
        return go.transform;
    }

    private static string Ruta(Transform t)
    {
        var sb = new StringBuilder(t.name);
        for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
        return sb.ToString();
    }

    private static void GuardarOverrides(Object objeto)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(objeto)) PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
    }
}
