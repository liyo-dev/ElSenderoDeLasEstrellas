using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Añade a un personaje modular (RPG Tiny Hero) una pieza hecha aparte con su mismo esqueleto (un pelo,
/// un cuerpo): la malla del FBX pasa a ser una pieza más del modelo, junto a las de su categoría, con
/// los huesos del propio modelo (por nombre). Queda apagada: la enciende el vestidor
/// (<see cref="ModularAutoBuilder"/>) como cualquier otra, porque la reconoce por el nombre (Hair…, Body…).
/// Los huesos propios de la pieza que el modelo no tenga (una coleta) se le crean. Si ya estaba, se rehace.
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

        int creados = CrearHuesosQueFaltan(origen, huesos, informe, raiz.name);

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
        informe.AppendLine($"{raiz.name}: pieza {pieza} añadida junto a {vecina}{(creados > 0 ? $" ({creados} huesos nuevos)" : "")}{(sinPareja > 0 ? $" ({sinPareja} huesos sin pareja)" : "")}.");
        return true;
    }

    /// <summary>
    /// Huesos propios de la pieza que el esqueleto del modelo no tiene (p. ej. ColetaBone01 bajo «head»):
    /// se crean bajo su padre, en la pose de atado del FBX (sacada de las bindposes, en la escala del modelo),
    /// de modo que en reposo la malla queda igual. Si ya están, se dejan.
    /// </summary>
    static int CrearHuesosQueFaltan(SkinnedMeshRenderer origen, Dictionary<string, Transform> huesos, System.Text.StringBuilder informe, string modelo)
    {
        var propios = origen.bones;
        var bindposes = origen.sharedMesh.bindposes;
        int creados = 0;
        bool otraVuelta = true;
        while (otraVuelta)
        {
            otraVuelta = false;
            for (int i = 0; i < propios.Length; i++)
            {
                var b = propios[i];
                if (b == null || huesos.ContainsKey(b.name) || b.parent == null) continue;
                if (!huesos.TryGetValue(b.parent.name, out var padre)) continue;   // su padre aún no está: otra vuelta
                var nuevo = new GameObject(b.name).transform;
                nuevo.SetParent(padre, false);
                int j = System.Array.FindIndex(propios, x => x == b.parent);
                if (j >= 0 && j < bindposes.Length && i < bindposes.Length)
                {
                    var local = bindposes[j] * bindposes[i].inverse;   // el hueso respecto a su padre, en la pose de atado
                    nuevo.localPosition = local.GetPosition();
                    nuevo.localRotation = local.rotation;
                    nuevo.localScale = local.lossyScale;
                }
                else { nuevo.localPosition = b.localPosition; nuevo.localRotation = b.localRotation; nuevo.localScale = b.localScale; }
                huesos[b.name] = nuevo;
                creados++;
                otraVuelta = true;
                informe.AppendLine($"{modelo}: hueso {b.name} creado bajo {padre.name}.");
            }
        }
        return creados;
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
