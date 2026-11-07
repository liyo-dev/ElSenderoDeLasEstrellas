using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Añade a un personaje modular (RPG Tiny Hero) una pieza hecha aparte con su mismo esqueleto (un pelo,
/// un cuerpo): la malla del FBX pasa a ser una pieza más del modelo, junto a las de su categoría, con
/// los huesos del propio modelo (por nombre). Queda apagada: la enciende el vestidor
/// (<see cref="ModularAutoBuilder"/>) como cualquier otra, porque la reconoce por el nombre (Hair…, Body…).
/// Si ya estaba, se rehace.
/// </summary>
public static class PiezaNuevaDelModelo
{
    /// <param name="raiz">Raíz del prefab abierto (PrefabUtility.LoadPrefabContents).</param>
    /// <param name="modelo">El FBX importado.</param>
    /// <param name="pieza">Nombre de la malla en el FBX y de la pieza en el modelo (p. ej. Hair14_Coleta).</param>
    /// <param name="vecina">Una pieza de la misma categoría que ya tenga el modelo (p. ej. Hair08): la nueva va a su lado.
    /// Se pueden dar varias separadas por «|» (la primera que exista).</param>
    public static bool Anadir(GameObject raiz, GameObject modelo, string pieza, string vecina, System.Text.StringBuilder informe)
    {
        var origen = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == pieza);
        if (origen == null) { informe.AppendLine($"{raiz.name}: el modelo {modelo.name} no trae la malla {pieza}."); return false; }
        var renderers = raiz.GetComponentsInChildren<Renderer>(true);
        Renderer referencia = null;
        foreach (var nombre in vecina.Split('|'))
            if ((referencia = renderers.FirstOrDefault(r => r.name == nombre)) != null) { vecina = nombre; break; }
        if (referencia == null) { informe.AppendLine($"{raiz.name}: no tiene la pieza {vecina}; no se añade {pieza}."); return false; }

        var huesos = new Dictionary<string, Transform>();
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (!huesos.ContainsKey(t.name) && t.GetComponent<Renderer>() == null) huesos[t.name] = t;

        var padre = referencia.transform.parent;
        var vieja = padre.Find(pieza);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var go = new GameObject(pieza) { layer = referencia.gameObject.layer };
        go.transform.SetParent(padre, false);
        var smr = go.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = origen.sharedMesh;
        smr.sharedMaterials = origen.sharedMaterials;
        int sinPareja = 0;
        smr.bones = origen.bones.Select(b =>
        {
            if (b != null && huesos.TryGetValue(b.name, out var h)) return h;
            sinPareja++; return null;
        }).ToArray();
        smr.rootBone = origen.rootBone != null && huesos.TryGetValue(origen.rootBone.name, out var raizHueso) ? raizHueso
                     : (referencia as SkinnedMeshRenderer)?.rootBone;
        smr.localBounds = origen.localBounds;
        smr.shadowCastingMode = referencia.shadowCastingMode;
        smr.receiveShadows = referencia.receiveShadows;
        go.SetActive(false);
        informe.AppendLine($"{raiz.name}: pieza {pieza} añadida junto a {vecina}{(sinPareja > 0 ? $" ({sinPareja} huesos sin pareja)" : "")}.");
        return true;
    }

    /// <summary>Enciende unas piezas y apaga otras en el modelo (su aspecto de partida).</summary>
    public static void Encender(GameObject raiz, IEnumerable<string> encender, IEnumerable<string> apagar)
    {
        var renderers = raiz.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (apagar.Contains(r.name)) r.gameObject.SetActive(false);
            if (encender.Contains(r.name)) r.gameObject.SetActive(true);
        }
    }
}
