using System;
using UnityEngine;

namespace Game.NPC
{
    /// Lo que hace un vecino que no toma parte en una charla y queda cerca de ella.
    public enum ReaccionAnteCharla
    {
        /// Sigue a lo suyo sin darse por enterado (trabajar, pasear, comer…).
        SeguirConLoSuyo,
        /// Se gira a mirar un momento, con un gesto, y sigue a lo suyo.
        Curiosear,
        /// Se da la vuelta y se queda haciendo algo de espaldas a la charla hasta que acaba.
        DarLaEspalda,
        /// Se aparta unos pasos, fuera del plano, y espera allí haciendo algo hasta que acaba.
        Apartarse,
        /// Mira un momento con curiosidad y después se aparta.
        CuriosearYApartarse,
    }

    /// Qué clase de vecino es, según lo que hace cuando nadie le pide nada.
    public enum TipoDeVecino
    {
        Cualquiera,
        /// Tiene un oficio con ruta de trabajo (NPCAmbientConfig.permitirTrabajo).
        ConOficio,
        /// Pasea por su zona (NPCAmbientConfig.enableWander).
        Paseante,
        /// Ni pasea ni tiene oficio: está en su sitio.
        Quieto,
    }

    /// Una regla de reacción: a quién se aplica (tipo y personalidad) y qué hace si estorba en la
    /// charla o si solo queda cerca. Se usa la primera regla de la lista que encaja con el vecino.
    [Serializable]
    public class ReglaDeReaccion
    {
        [Tooltip("Nombre para reconocer la regla en el Inspector.")]
        public string nombre = "Regla";

        [Tooltip("Clase de vecino a la que se aplica.")]
        public TipoDeVecino tipo = TipoDeVecino.Cualquiera;

        [Tooltip("Sociabilidad de la personalidad (NPCSocialConfig) en la que se aplica (x = mínimo, y = máximo). Sin config social cuenta 0,5.")]
        public Vector2 sociabilidad = new Vector2(0f, 1f);

        [Tooltip("Energía de la personalidad en la que se aplica (x = mínimo, y = máximo). Sin config social cuenta 0,5.")]
        public Vector2 energia = new Vector2(0f, 1f);

        [Tooltip("Qué hace si queda dentro del escenario de la charla (estorba en el plano).")]
        public ReaccionAnteCharla siEstorba = ReaccionAnteCharla.Apartarse;

        [Header("Si queda cerca pero fuera del escenario (se sortea con estos pesos)")]
        [Min(0f)] public float pesoSeguirConLoSuyo = 1f;
        [Min(0f)] public float pesoCuriosear = 0.5f;
        [Min(0f)] public float pesoDarLaEspalda = 0.3f;

        [Header("Actuación")]
        [Tooltip("Segundos que se queda mirando al curiosear (x = mínimo, y = máximo).")]
        public Vector2 segundosMirando = new Vector2(1.5f, 3f);

        [Tooltip("Gestos al girarse a mirar (se elige uno). Vacío = solo se gira.")]
        public string[] gestosAlMirar = { "Question01", "HeadNod01" };

        [Tooltip("Gestos mientras espera apartado o de espaldas a que acabe la charla (se elige uno cada vez).")]
        public string[] gestosEsperando = { "Idle02", "Idle03", "Suspiro01" };

        [Tooltip("Cada cuántos segundos hace un gesto mientras espera (x = mínimo, y = máximo).")]
        public Vector2 cadaCuantoUnGesto = new Vector2(4f, 8f);

        [Tooltip("Probabilidad de echar un vistazo a la charla en vez de un gesto mientras espera.")]
        [Range(0f, 1f)] public float vistazoMientrasEspera = 0.3f;

        public bool Encaja(TipoDeVecino tipoDelVecino, NPCPersonality personalidad)
        {
            if (tipo != TipoDeVecino.Cualquiera && tipo != tipoDelVecino) return false;
            if (personalidad.sociability < sociabilidad.x || personalidad.sociability > sociabilidad.y) return false;
            if (personalidad.energy < energia.x || personalidad.energy > energia.y) return false;
            return true;
        }

        /// Sortea la reacción de un vecino que está cerca pero no estorba.
        public ReaccionAnteCharla SortearCerca()
        {
            float total = pesoSeguirConLoSuyo + pesoCuriosear + pesoDarLaEspalda;
            if (total <= 0f) return ReaccionAnteCharla.SeguirConLoSuyo;
            float r = UnityEngine.Random.value * total;
            if (r < pesoCuriosear) return ReaccionAnteCharla.Curiosear;
            r -= pesoCuriosear;
            if (r < pesoDarLaEspalda) return ReaccionAnteCharla.DarLaEspalda;
            return ReaccionAnteCharla.SeguirConLoSuyo;
        }

        public static string Elegir(string[] gestos)
            => gestos == null || gestos.Length == 0 ? null : gestos[UnityEngine.Random.Range(0, gestos.Length)];

        public static float Entre(Vector2 rango)
            => UnityEngine.Random.Range(Mathf.Min(rango.x, rango.y), Mathf.Max(rango.x, rango.y));
    }

    /// <summary>
    /// Cómo ceden el sitio los vecinos cuando empieza una conversación cerca de ellos (diálogo de
    /// caja, con o sin cámara de diálogo): quién estorba, cuánto se apartan y qué hace cada clase de
    /// vecino y de personalidad. Lo usa <see cref="TestigosDeCharla"/>. Vive en Resources
    /// (<see cref="RutaEnResources"/>); si falta, se usan los valores por defecto de esta clase.
    /// </summary>
    [CreateAssetMenu(menuName = "El Sendero/NPCs/Reacciones ante charlas", fileName = "ReaccionesAnteCharlas")]
    public sealed class ReaccionesAnteCharlas : ScriptableObject
    {
        public const string RutaEnResources = "NPCs/ReaccionesAnteCharlas";

        [Header("Escenario de la charla")]
        [Tooltip("Metros alrededor de quien toma parte en la charla en los que un vecino estorba en el plano.")]
        [Min(0.5f)] public float radioDelEscenario = 3f;

        [Tooltip("Metros de más, fuera del escenario, hasta los que se aparta quien estorba.")]
        [Min(0.5f)] public float separacionAlApartarse = 2.5f;

        [Tooltip("Metros alrededor de la charla en los que los vecinos se dan por enterados (curiosean, dan la espalda o siguen a lo suyo).")]
        [Min(1f)] public float radioDeInteres = 10f;

        [Tooltip("Cada cuántos segundos se mira quién anda cerca mientras dura la charla (por si alguien entra en el escenario).")]
        [Min(0.1f)] public float comprobarCada = 0.5f;

        [Tooltip("Si está activo, también reaccionan en los diálogos que van dentro de una cinemática. Normalmente no: la cinemática decide su escena.")]
        public bool tambienDentroDeCinematicas = false;

        [Header("Al acabar")]
        [Tooltip("Segundos que tardan en retomar lo suyo cuando acaba la charla (x = mínimo, y = máximo); así no se mueven todos a la vez.")]
        public Vector2 esperaAlAcabar = new Vector2(0.3f, 1.2f);

        [Tooltip("Quien no pasea ni tiene oficio vuelve a su sitio (y a mirar hacia donde miraba) después de apartarse.")]
        public bool volverASuSitio = true;

        [Tooltip("Segundos máximos andando hacia un sitio antes de darse por llegado (por si algo le corta el paso).")]
        [Min(1f)] public float tiempoMaximoAndando = 8f;

        [Header("Reglas (se usa la primera que encaja)")]
        public ReglaDeReaccion[] reglas = ReglasPorDefecto();

        [Tooltip("Regla para quien no encaja en ninguna de las de arriba.")]
        public ReglaDeReaccion reglaPorDefecto = new ReglaDeReaccion
        {
            nombre = "Por defecto",
            siEstorba = ReaccionAnteCharla.Apartarse,
            pesoSeguirConLoSuyo = 0.5f,
            pesoCuriosear = 0.6f,
            pesoDarLaEspalda = 0.4f,
        };

        public ReglaDeReaccion ReglaPara(TipoDeVecino tipo, NPCPersonality personalidad)
        {
            if (reglas != null)
            {
                for (int i = 0; i < reglas.Length; i++)
                {
                    var r = reglas[i];
                    if (r != null && r.Encaja(tipo, personalidad)) return r;
                }
            }
            return reglaPorDefecto;
        }

        static ReglaDeReaccion[] ReglasPorDefecto() => new[]
        {
            new ReglaDeReaccion
            {
                nombre = "Con oficio: sigue trabajando",
                tipo = TipoDeVecino.ConOficio,
                siEstorba = ReaccionAnteCharla.Apartarse,
                pesoSeguirConLoSuyo = 1f, pesoCuriosear = 0.3f, pesoDarLaEspalda = 0f,
            },
            new ReglaDeReaccion
            {
                nombre = "Muy sociable: curiosea",
                sociabilidad = new Vector2(0.65f, 1f),
                siEstorba = ReaccionAnteCharla.CuriosearYApartarse,
                pesoSeguirConLoSuyo = 0.3f, pesoCuriosear = 1f, pesoDarLaEspalda = 0.2f,
                vistazoMientrasEspera = 0.5f,
            },
            new ReglaDeReaccion
            {
                nombre = "Reservado: discreto",
                sociabilidad = new Vector2(0f, 0.35f),
                siEstorba = ReaccionAnteCharla.Apartarse,
                pesoSeguirConLoSuyo = 0.6f, pesoCuriosear = 0.15f, pesoDarLaEspalda = 1f,
                vistazoMientrasEspera = 0.1f,
            },
        };

        static ReaccionesAnteCharlas s_cargadas;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_cargadas = null;
#endif

        /// Las del proyecto (Resources) o, si no hay asset, unas con los valores por defecto.
        public static ReaccionesAnteCharlas Cargadas
        {
            get
            {
                if (s_cargadas != null) return s_cargadas;
                s_cargadas = Resources.Load<ReaccionesAnteCharlas>(RutaEnResources);
                if (s_cargadas == null)
                {
                    s_cargadas = CreateInstance<ReaccionesAnteCharlas>();
                    s_cargadas.hideFlags = HideFlags.DontSave;
                }
                return s_cargadas;
            }
        }
    }
}
