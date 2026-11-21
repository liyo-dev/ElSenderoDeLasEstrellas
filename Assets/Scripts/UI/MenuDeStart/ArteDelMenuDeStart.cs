using System;
using UnityEngine;

namespace Sendero.UI
{
    /// <summary>
    /// Letras y dibujos del menú de Start (los de la maqueta): estilo de los menús de papel,
    /// iconos de las pestañas, de la moneda, de los huecos de ropa, de los rasgos y de vida y
    /// maná. Vive en Resources porque el menú se monta por código dentro del lienzo de Start.
    /// </summary>
    [CreateAssetMenu(menuName = "El Sendero/UI/Arte del menú de Start", fileName = "ArteDelMenuDeStart")]
    public sealed class ArteDelMenuDeStart : ScriptableObject
    {
        public const string RutaEnResources = "UI/ArteDelMenuDeStart";

        [Serializable]
        public struct IconoDeRasgo
        {
            public RasgoDePrenda rasgo;
            public Sprite icono;
        }

        [Tooltip("Fuentes de los menús de papel.")]
        public EstiloDeMenuSencillo estilo;
        [Tooltip("Iconos de las pestañas, en su orden: Inventario, Magia, Equipo, Sintonía.")]
        public Sprite[] pestanas = new Sprite[4];
        public Sprite moneda;
        public Sprite corazon;
        public Sprite gota;
        [Tooltip("Icono de cada hueco de ropa cuando no lleva nada con icono, en el orden de la pestaña Equipo.")]
        public Sprite[] huecos = new Sprite[4];
        public IconoDeRasgo[] rasgos = Array.Empty<IconoDeRasgo>();
        [Tooltip("Icono de un rasgo que no está en la lista.")]
        public Sprite rasgoPorDefecto;

        public Sprite IconoDe(RasgoDePrenda rasgo)
        {
            foreach (var r in rasgos) if (r.rasgo == rasgo && r.icono != null) return r.icono;
            return rasgoPorDefecto;
        }

        private static ArteDelMenuDeStart s_cargado;
        public static ArteDelMenuDeStart Cargado => s_cargado != null ? s_cargado : (s_cargado = Resources.Load<ArteDelMenuDeStart>(RutaEnResources));

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_cargado = null; }
#endif
    }
}
