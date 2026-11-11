using TMPro;
using UnityEngine;

/// <summary>
/// Sprites y fuentes con los que se monta un menú sencillo por código (la colección de Oliver),
/// los mismos que usan los menús del juego: así se ve igual que la tienda y las misiones sin
/// necesitar un prefab propio.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/UI/Estilo de menú sencillo", fileName = "Estilo_MenuSencillo")]
public sealed class EstiloDeMenuSencillo : ScriptableObject
{
    [Tooltip("Fondo que oscurece el juego detrás del menú.")]
    public Sprite fondo;
    [Tooltip("Panel principal.")]
    public Sprite panel;
    [Tooltip("Tarjeta de cada elemento.")]
    public Sprite tarjeta;
    [Tooltip("Chip pequeño (p. ej. «¡Nuevo!»).")]
    public Sprite chip;
    [Tooltip("Botón.")]
    public Sprite boton;
    [Tooltip("Fuente de los títulos.")]
    public TMP_FontAsset fuenteTitulo;
    [Tooltip("Fuente del texto.")]
    public TMP_FontAsset fuenteTexto;
    [Tooltip("Texto escrito sobre el papel de los paneles y las tarjetas.")]
    public Color colorTexto = new Color(0.14f, 0.12f, 0.10f, 1f);
    [Tooltip("Texto secundario sobre papel.")]
    public Color colorTextoSuave = new Color(0.38f, 0.30f, 0.23f, 1f);
    [Tooltip("Texto destacado sobre papel.")]
    public Color colorResalte = new Color(0.54f, 0.35f, 0f, 1f);
}
