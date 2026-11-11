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
    public Color colorTexto = new Color(0.96f, 0.95f, 1f, 1f);
    public Color colorTextoSuave = new Color(0.78f, 0.76f, 0.9f, 1f);
    public Color colorResalte = new Color(1f, 0.83f, 0.35f, 1f);
}
