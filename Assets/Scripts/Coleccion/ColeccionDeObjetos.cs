using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una colección de objetos raros (el álbum de Oliver): páginas de piezas, cada una con la frase
/// que dice el coleccionista al recibirla y un premio al completar la página. Lo que ya se ha
/// entregado se guarda como flags de la partida (<see cref="FlagDePartida"/>).
/// Solo deben entrar objetos que ninguna misión pida: al entregarlos salen del inventario.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Colección de objetos", fileName = "Coleccion_")]
public sealed class ColeccionDeObjetos : ScriptableObject
{
    [Tooltip("Id corto y único de la colección; forma parte de las claves de la partida.")]
    public string coleccionId;
    public string tituloId;
    public string titulo = "Colección";
    public List<PaginaDeColeccion> paginas = new();

    public string ClaveDePieza(ItemData objeto) => $"COLECCION:{coleccionId}:{objeto.itemId}";
    public string ClaveDePremio(int pagina) => $"COLECCION_PREMIO:{coleccionId}:{pagina}";

    public bool Tiene(PiezaDeColeccion pieza) =>
        pieza != null && pieza.objeto != null && FlagDePartida.EstaPuesto(ClaveDePieza(pieza.objeto));

    public bool PaginaCompleta(int i)
    {
        var p = paginas[i];
        if (p.piezas == null || p.piezas.Count == 0) return false;
        foreach (var pieza in p.piezas)
            if (!Tiene(pieza)) return false;
        return true;
    }

    public string Titulo => Texto(tituloId, titulo);

    /// <summary>Texto traducido, o el de respaldo si la clave no existe.</summary>
    public static string Texto(string clave, string respaldo) =>
        LocalizationManager.Instance != null && !string.IsNullOrEmpty(clave)
            ? LocalizationManager.Instance.Get(clave, respaldo)
            : respaldo;
}

[Serializable]
public sealed class PaginaDeColeccion
{
    public string tituloId;
    public string titulo;
    public List<PiezaDeColeccion> piezas = new();
    [Tooltip("Se da una vez, al completar la página.")]
    public ItemData premio;
    [Min(1)] public int cantidadDelPremio = 1;

    public string Titulo => ColeccionDeObjetos.Texto(tituloId, titulo);
}

[Serializable]
public sealed class PiezaDeColeccion
{
    public ItemData objeto;
    [Tooltip("Lo que dice el coleccionista al recibirla (y al mirarla en el álbum).")]
    public string fraseId;
    public string frase;

    public string Frase => ColeccionDeObjetos.Texto(fraseId, frase);
}
