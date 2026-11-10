using UnityEngine;

namespace Sendero.UI
{
    /// <summary>
    /// Cómo se ven las barras de vida y maná apiladas: cuántos puntos caben en cada capa y el
    /// color de cada capa. Cuando una capa se llena, encima empieza la siguiente con su color y
    /// al lado sale «×N». Lo usan el HUD (<see cref="PlayerHUDV2"/>), los retratos del grupo, el
    /// panel del menú de Start y la pestaña de Sintonía.
    ///
    /// Si existe un asset en <c>Resources/EstiloDeBarrasApiladas</c> se usa ese; si no, los
    /// valores de aquí.
    /// </summary>
    [CreateAssetMenu(menuName = "El Sendero/UI/Estilo de barras apiladas", fileName = "EstiloDeBarrasApiladas")]
    public sealed class EstiloDeBarrasApiladas : ScriptableObject
    {
        public const string RutaEnResources = "EstiloDeBarrasApiladas";

        [Tooltip("Puntos de vida o maná que caben en cada capa de la barra.")]
        [Min(1f)] public float puntosPorCapa = 100f;

        [Tooltip("Color de cada capa de vida, de abajo arriba. Las capas de más repiten el último.")]
        public Color[] coloresDeVida =
        {
            new Color32(0x4C, 0xD0, 0x7D, 0xFF),
            new Color32(0x2F, 0xC4, 0xB2, 0xFF),
            new Color32(0xF2, 0xC9, 0x4C, 0xFF),
        };

        [Tooltip("Color de cada capa de maná, de abajo arriba. Las capas de más repiten el último.")]
        public Color[] coloresDeMana =
        {
            new Color32(0x4F, 0x8C, 0xFF, 0xFF),
            new Color32(0x9B, 0x6B, 0xFF, 0xFF),
            new Color32(0xE8, 0xEE, 0xFF, 0xFF),
        };

        [Tooltip("Color del «×N» que dice cuántas capas hay.")]
        public Color colorDelNumero = Color.white;

        private static EstiloDeBarrasApiladas s_actual;

        /// El estilo en uso (el asset de Resources o, si no hay, uno con los valores por defecto).
        public static EstiloDeBarrasApiladas Actual
        {
            get
            {
                if (s_actual != null) return s_actual;
                s_actual = Resources.Load<EstiloDeBarrasApiladas>(RutaEnResources);
                if (s_actual == null)
                {
                    s_actual = CreateInstance<EstiloDeBarrasApiladas>();
                    s_actual.hideFlags = HideFlags.DontSave;
                }
                return s_actual;
            }
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_actual = null; }
#endif

        /// Cuántas capas ocupa ese valor (al menos una).
        public int Capas(float valor) => Mathf.Max(1, Mathf.CeilToInt(valor / puntosPorCapa - 0.0001f));

        /// Qué parte de la capa de arriba está llena (0 a 1).
        public float ParteDeArriba(float valor)
        {
            if (valor <= 0f) return 0f;
            int capas = Capas(valor);
            return Mathf.Clamp01((valor - (capas - 1) * puntosPorCapa) / puntosPorCapa);
        }

        /// Color de la capa <paramref name="indice"/> (0 = la de abajo).
        public static Color ColorDeCapa(Color[] colores, int indice)
        {
            if (colores == null || colores.Length == 0) return Color.white;
            return colores[Mathf.Clamp(indice, 0, colores.Length - 1)];
        }
    }
}
