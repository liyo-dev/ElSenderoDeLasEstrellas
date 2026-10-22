using UnityEngine;

namespace Sendero.UI
{
    /// Cara que pone un personaje en su retrato del HUD.
    public enum ExpresionDeRetrato { Normal = 0, Dolorido = 1, Caido = 2, Contento = 3 }

    /// Lo que hace falta para pintar un retrato: la textura, la parte de ella que se usa y cuánto
    /// del dibujo queda por debajo del borde del panel (lo que el recorte esconde).
    public readonly struct ImagenDeRetrato
    {
        public readonly Texture Textura;
        /// Parte de la textura en coordenadas UV (RawImage.uvRect).
        public readonly Rect Uv;
        /// Ancho entre alto de la parte usada, en píxeles.
        public readonly float Aspecto;
        /// Fracción del alto del dibujo que queda por debajo del borde del panel: 0 en un busto ya
        /// recortado; ~0,4 en una imagen de cuerpo entero (el corte cae a la altura del pecho).
        public readonly float FraccionBajoElBorde;
        /// El mismo dibujo sobre un fondo opaco claro, para recuperar la transparencia que una
        /// RenderTexture no conserva: el shader compara las dos imágenes y lo que cambia es el
        /// fondo. Nulo en las fuentes que ya traen alfa (los retratos horneados).
        public readonly Texture TexturaSobreFondo;

        public ImagenDeRetrato(Texture textura, Rect uv, float aspecto, float fraccionBajoElBorde, Texture texturaSobreFondo = null)
        {
            Textura = textura;
            Uv = uv;
            Aspecto = aspecto > 0f ? aspecto : 1f;
            FraccionBajoElBorde = Mathf.Clamp(fraccionBajoElBorde, 0f, 0.9f);
            TexturaSobreFondo = texturaSobreFondo;
        }

        public bool Valida => Textura != null;

        /// La imagen de un sprite (su rectángulo dentro de la textura, por si está en un atlas).
        public static ImagenDeRetrato DesdeSprite(Sprite sprite, float fraccionBajoElBorde)
        {
            if (sprite == null || sprite.texture == null) return default;
            var tex = sprite.texture;
            var r = sprite.textureRect;
            var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
            return new ImagenDeRetrato(tex, uv, r.width / Mathf.Max(1f, r.height), fraccionBajoElBorde);
        }
    }

    /// De dónde sale la imagen de un retrato del grupo. El retrato no sabe si es un sprite fijo
    /// (<see cref="JuegoDeRetratos"/>), una foto horneada del modelo o una RenderTexture en vivo: le
    /// pide la cara que toca y la pinta. Una fuente en vivo puede usar la petición como aviso de
    /// que el personaje ha cambiado de cara.
    public interface IFuenteDeRetrato
    {
        /// Imagen del personaje con esa cara. Si la fuente no tiene esa cara, da la Normal; si no
        /// tiene al personaje (o no está lista), devuelve false.
        bool TryObtener(PartyControlManager.CharacterSlot personaje, ExpresionDeRetrato expresion, out ImagenDeRetrato imagen);
    }

    /// Algo puntual que el retrato quiere que el personaje haga (además de cambiar de cara).
    public enum ReaccionDeRetrato { Golpe = 0, Saludo = 1 }

    /// Fuente de imagen que se mueve (un retrato vivo): necesita saber cuándo se ve, para no
    /// gastar en lo que no está en pantalla, y qué reacciones hacer.
    public interface IFuenteDeRetratoVivo : IFuenteDeRetrato
    {
        void Mostrar(PartyControlManager.CharacterSlot personaje, bool visible);
        void Reaccionar(PartyControlManager.CharacterSlot personaje, ReaccionDeRetrato reaccion);
    }
}
