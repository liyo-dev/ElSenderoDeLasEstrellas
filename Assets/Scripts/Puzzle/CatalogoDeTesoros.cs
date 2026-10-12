using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de todos los tesoros del juego (id y región), para que el registro sepa el total de
/// cada región aunque su escena no esté cargada. Vive en Resources y lo rellena el Editor al
/// guardar una escena con tesoros (o con «Actualizar el catálogo de tesoros»).
/// </summary>
public sealed class CatalogoDeTesoros : ScriptableObject
{
    public const string RutaEnResources = "CatalogoDeTesoros";

    [Serializable]
    public struct Entrada
    {
        public string id;
        public string region;
        [Tooltip("Escena donde está (para rehacer su parte al guardarla).")]
        public string escena;
    }

    public List<Entrada> tesoros = new List<Entrada>();
}
