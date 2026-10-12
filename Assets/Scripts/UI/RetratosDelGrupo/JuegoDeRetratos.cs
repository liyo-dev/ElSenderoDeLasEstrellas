using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sendero.UI
{
    /// Retratos fijos del grupo: un sprite por personaje y cara. Es la fuente de imagen de los
    /// retratos del HUD cuando no hay retrato en vivo, y la de respaldo cuando lo hay.
    [CreateAssetMenu(menuName = "El Sendero/UI/Juego de retratos del grupo", fileName = "Retratos_")]
    public sealed class JuegoDeRetratos : ScriptableObject, IFuenteDeRetrato
    {
        [Serializable]
        public sealed class Entrada
        {
            public PartyControlManager.CharacterSlot personaje = PartyControlManager.CharacterSlot.Will;
            [Tooltip("Cara de siempre. Las demás, si faltan, usan esta.")]
            public Sprite normal;
            [Tooltip("Al recibir un golpe y con poca vida.")]
            public Sprite dolorido;
            [Tooltip("Fuera de combate.")]
            public Sprite caido;
            [Tooltip("Con la carga del trío llena.")]
            public Sprite contento;
            [Tooltip("Fracción del alto del dibujo que queda por debajo del borde del panel: 0 si el sprite ya es un busto; ~0,4 si es de cuerpo entero.")]
            [Range(0f, 0.9f)] public float fraccionBajoElBorde;
        }

        [SerializeField] private List<Entrada> retratos = new List<Entrada>();

        public IReadOnlyList<Entrada> Retratos => retratos;

        public bool TryObtener(PartyControlManager.CharacterSlot personaje, ExpresionDeRetrato expresion, out ImagenDeRetrato imagen)
        {
            imagen = default;
            for (int i = 0; i < retratos.Count; i++)
            {
                var e = retratos[i];
                if (e == null || e.personaje != personaje) continue;
                var sprite = expresion switch
                {
                    ExpresionDeRetrato.Dolorido => e.dolorido,
                    ExpresionDeRetrato.Caido => e.caido,
                    ExpresionDeRetrato.Contento => e.contento,
                    _ => null
                };
                if (sprite == null) sprite = e.normal;
                imagen = ImagenDeRetrato.DesdeSprite(sprite, e.fraccionBajoElBorde);
                return imagen.Valida;
            }
            return false;
        }
    }
}
