using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Prendas que no son piezas del modelo modular (un vestido, una armadura hechos aparte, con el mismo
/// esqueleto): se ponen encima del personaje cambiando los huesos de su malla por los del personaje
/// (por nombre), así se mueven con cualquier animación. Va en el objeto del vestidor
/// (<see cref="ModularAutoBuilder"/>) y recuerda lo que ha puesto para quitarlo.
/// </summary>
public sealed class PrendasSueltas : MonoBehaviour
{
    private readonly List<GameObject> _puestas = new();
    private readonly List<GameObject> _origen = new();

    /// <summary>Deja puestas exactamente estas prendas (quita las que sobran, pone las que faltan).</summary>
    public void Dejar(IReadOnlyList<GameObject> prendas)
    {
        for (int i = _puestas.Count - 1; i >= 0; i--)
        {
            bool sigue = false;
            for (int k = 0; k < prendas.Count; k++) if (prendas[k] == _origen[i]) { sigue = true; break; }
            if (sigue && _puestas[i] != null) continue;
            if (_puestas[i] != null) Destroy(_puestas[i]);
            _puestas.RemoveAt(i); _origen.RemoveAt(i);
        }
        foreach (var prenda in prendas)
        {
            if (prenda == null || _origen.Contains(prenda)) continue;
            var puesta = Poner(prenda);
            if (puesta == null) continue;
            _puestas.Add(puesta); _origen.Add(prenda);
        }
    }

    public void QuitarTodas() => Dejar(System.Array.Empty<GameObject>());

    private GameObject Poner(GameObject prefab)
    {
        var huesos = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (!huesos.ContainsKey(t.name) && t.GetComponent<Renderer>() == null) huesos[t.name] = t;

        var copia = Instantiate(prefab);
        var mallas = copia.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (mallas.Length == 0) { Destroy(copia); Debug.LogWarning($"[PrendasSueltas] {prefab.name} no trae mallas con esqueleto.", this); return null; }

        var grupo = new GameObject("Prenda: " + prefab.name);
        grupo.transform.SetParent(transform, false);
        int sinHueso = 0;
        foreach (var smr in mallas)
        {
            var propios = smr.bones;
            var nuevos = new Transform[propios.Length];
            for (int i = 0; i < propios.Length; i++)
            {
                if (propios[i] != null && huesos.TryGetValue(propios[i].name, out var h)) nuevos[i] = h;
                else sinHueso++;
            }
            smr.bones = nuevos;
            if (smr.rootBone != null && huesos.TryGetValue(smr.rootBone.name, out var raiz)) smr.rootBone = raiz;
            smr.updateWhenOffscreen = true;
            smr.transform.SetParent(grupo.transform, false);
        }
        if (sinHueso > 0) Debug.LogWarning($"[PrendasSueltas] {prefab.name} en {name}: {sinHueso} huesos sin pareja en el personaje.", this);
        // Lo que queda de la copia es su esqueleto: las mallas ya usan el del personaje.
        Destroy(copia);
        return grupo;
    }
}
