using System;
using UnityEngine;

/// <summary>
/// Lo que hace falta para pasar o usar algo: habilidades desbloqueadas, objetos en la bolsa del
/// grupo, hechizos aprendidos (Amanecer, por ejemplo) y/o flags de la partida (una misión hecha…).
/// Lo usan las barreras, los tesoros y los puzles para saber si el jugador puede, y qué decirle
/// si no.
/// </summary>
[Serializable]
public sealed class RequisitoDeAcceso
{
    /// <summary>Prefijo de las claves de texto automáticas: REQUISITO_FALTA_FLY, REQUISITO_FALTA_SWIM…</summary>
    public const string PrefijoDeAviso = "REQUISITO_FALTA_";
    public const string AvisoGenerico = "REQUISITO_FALTA_ALGO";
    /// <summary>Aviso de objeto que falta; el texto lleva {0} para su nombre.</summary>
    public const string AvisoDeObjeto = "REQUISITO_FALTA_OBJETO";
    /// <summary>Aviso de hechizo que falta (sin nombrarlo, para no adelantar nada).</summary>
    public const string AvisoDeHechizo = "REQUISITO_FALTA_HECHIZO";

    [Tooltip("Habilidades que tienen que estar desbloqueadas.")]
    public AbilityKey[] habilidades = Array.Empty<AbilityKey>();

    [Tooltip("Objetos que tiene que llevar el grupo (no se gastan).")]
    public ItemData[] objetos = Array.Empty<ItemData>();

    [Tooltip("Hechizos que tiene que haber aprendido el grupo.")]
    public SpellId[] hechizos = Array.Empty<SpellId>();

    [Tooltip("Flags de la partida que tienen que estar puestos (misión hecha, mecanismo activado…).")]
    public string[] flags = Array.Empty<string>();

    [Tooltip("Clave de texto del aviso. Vacía = automática según lo primero que falte.")]
    public string claveDeAviso;

    public bool Vacio => Longitud(habilidades) == 0 && Longitud(objetos) == 0 && Longitud(hechizos) == 0 && Longitud(flags) == 0;

    public bool Cumple()
    {
        if (habilidades != null)
            for (int i = 0; i < habilidades.Length; i++)
                if (!UnlockService.HasAbility(habilidades[i])) return false;
        if (PrimerObjetoQueFalta() != null) return false;
        if (FaltaAlgunHechizo()) return false;
        if (flags != null)
            for (int i = 0; i < flags.Length; i++)
                if (!string.IsNullOrEmpty(flags[i]) && !UnlockService.HasFlag(flags[i])) return false;
        return true;
    }

    /// <summary>Clave de texto que explica lo que falta (vacía si se cumple).</summary>
    public string ClaveDeLoQueFalta()
    {
        if (Cumple()) return string.Empty;
        if (!string.IsNullOrEmpty(claveDeAviso)) return claveDeAviso;
        if (habilidades != null)
            for (int i = 0; i < habilidades.Length; i++)
                if (!UnlockService.HasAbility(habilidades[i]))
                    return PrefijoDeAviso + habilidades[i].ToString().ToUpperInvariant();
        if (PrimerObjetoQueFalta() != null) return AvisoDeObjeto;
        if (FaltaAlgunHechizo()) return AvisoDeHechizo;
        return AvisoGenerico;
    }

    /// <summary>Texto localizado de lo que falta, con el nombre del objeto si es un objeto (vacío si se cumple).</summary>
    public string TextoDeLoQueFalta()
    {
        string clave = ClaveDeLoQueFalta();
        if (string.IsNullOrEmpty(clave)) return string.Empty;
        string texto = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, clave) : clave;
        if (clave != AvisoDeObjeto) return texto;
        var falta = PrimerObjetoQueFalta();
        string nombre = falta != null ? falta.GetLocalizedName() : string.Empty;
        return texto.Contains("{0}") ? string.Format(texto, nombre) : texto;
    }

    /// <summary>Muestra en el HUD lo que falta (si falta algo).</summary>
    public void AvisarDeLoQueFalta(float segundos)
    {
        string texto = TextoDeLoQueFalta();
        if (!string.IsNullOrEmpty(texto)) HudToastService.Instance?.ShowRaw(texto, segundos);
    }

    private ItemData PrimerObjetoQueFalta()
    {
        if (Longitud(objetos) == 0) return null;
        PlayerService.TryGetComponent(out Inventory bolsa, allowSceneLookup: false);
        for (int i = 0; i < objetos.Length; i++)
        {
            var objeto = objetos[i];
            if (objeto == null) continue;
            if (bolsa == null || !bolsa.HasItem(objeto)) return objeto;
        }
        return null;
    }

    private bool FaltaAlgunHechizo()
    {
        if (Longitud(hechizos) == 0) return false;
        var aprendidos = UnlockService.GetActivePreset()?.unlockedSpells;
        for (int i = 0; i < hechizos.Length; i++)
        {
            if (hechizos[i] == SpellId.None) continue;
            if (aprendidos == null || !aprendidos.Contains(hechizos[i])) return true;
        }
        return false;
    }

    private static int Longitud(Array lista) => lista == null ? 0 : lista.Length;
}
