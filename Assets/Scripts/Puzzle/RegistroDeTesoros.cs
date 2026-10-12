using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro de tesoros: cuántos hay y cuántos se han encontrado en cada región. Junta el
/// catálogo (todos los del juego) con los que hay cargados ahora (por si alguno aún no está en
/// el catálogo, como los del LAB). Encontrado = flag TESORO_&lt;id&gt; en la partida.
/// </summary>
public static class RegistroDeTesoros
{
    public readonly struct Region
    {
        public readonly string Clave;
        public readonly int Encontrados;
        public readonly int Total;

        public Region(string clave, int encontrados, int total)
        {
            Clave = clave;
            Encontrados = encontrados;
            Total = total;
        }

        /// <summary>Nombre localizado (TESORO_REGION_&lt;clave&gt;).</summary>
        public string Nombre
        {
            get
            {
                string clave = "TESORO_REGION_" + Clave;
                return LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, Clave) : Clave;
            }
        }
    }

    /// <summary>Cambia lo encontrado (se abre un tesoro).</summary>
    public static event Action Cambiado;

    // id → región de todos los tesoros conocidos (catálogo + cargados).
    private static readonly Dictionary<string, string> s_regionDe = new Dictionary<string, string>();
    private static bool s_catalogoLeido;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_regionDe.Clear();
        s_catalogoLeido = false;
        Cambiado = null;
        Tesoro.Abierto -= AlAbrirse;
    }
#endif

    /// <summary>Anota un tesoro cargado (lo llama cada Tesoro al activarse).</summary>
    public static void Conocer(string id, string region)
    {
        LeerCatalogo();
        if (!string.IsNullOrEmpty(id)) s_regionDe[id] = region;
    }

    /// <summary>Regiones con al menos un tesoro, en orden alfabético de clave.</summary>
    public static List<Region> Regiones()
    {
        LeerCatalogo();
        var cuentas = new SortedDictionary<string, (int encontrados, int total)>(StringComparer.Ordinal);
        foreach (var par in s_regionDe)
        {
            cuentas.TryGetValue(par.Value, out var c);
            c.total++;
            if (FlagDePartida.EstaPuesto(Tesoro.ClaveDe(par.Key))) c.encontrados++;
            cuentas[par.Value] = c;
        }
        var lista = new List<Region>(cuentas.Count);
        foreach (var par in cuentas) lista.Add(new Region(par.Key, par.Value.encontrados, par.Value.total));
        return lista;
    }

    private static void LeerCatalogo()
    {
        if (s_catalogoLeido) return;
        s_catalogoLeido = true;
        Tesoro.Abierto += AlAbrirse;
        var catalogo = Resources.Load<CatalogoDeTesoros>(CatalogoDeTesoros.RutaEnResources);
        if (catalogo == null) return;
        foreach (var entrada in catalogo.tesoros)
            if (!string.IsNullOrEmpty(entrada.id))
                s_regionDe[entrada.id] = string.IsNullOrWhiteSpace(entrada.region) ? Tesoro.RegionPorDefecto : entrada.region;
    }

    private static void AlAbrirse(Tesoro _) => Cambiado?.Invoke();
}
