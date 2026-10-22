#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Entrada del manifiesto: de qué clip sale cada copia corregida y cómo ha quedado.
/// </summary>
[Serializable]
public class EntradaDeCorreccion
{
    public AnimationClip original;
    public AnimationClip corregida;
    public string rutaOriginal;
    public string rutaCorregida;
    public bool usado;
    public string usos;
    public string rig;
    public float cabezaAntes;
    public float cabezaDespues;
    public float torsoAntes;
    public float torsoDespues;
    public bool aplicada;
    public bool sinResolver;
}

/// <summary>
/// Lista de copias corregidas generadas. Es la fuente de verdad para aplicar y revertir.
/// </summary>
public class ManifestDeCorrecciones : ScriptableObject
{
    public string fecha;
    public List<EntradaDeCorreccion> entradas = new List<EntradaDeCorreccion>();
}
#endif
