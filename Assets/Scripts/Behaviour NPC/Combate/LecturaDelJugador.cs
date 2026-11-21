using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Lo que los cerebros de combate leen del jugador: si está cargando un hechizo, cuándo lanzó el
    /// último, si tiene el escudo levantado, a quién apunta y cuántas veces ha devuelto con la B los
    /// proyectiles de un grupo hace poco. Escucha a <see cref="MagicCaster"/> y a
    /// <see cref="PlayerShieldController"/> del cuerpo que lleva el jugador y se vuelve a enganchar
    /// cuando cambia (<see cref="PlayerService.OnPlayerRegistered"/>). Sin coste por fotograma.
    /// </summary>
    public static class LecturaDelJugador
    {
        private const int Capacidad = 16;

        private struct Devolucion
        {
            public GameObject lanzador;
            public float cuando;
        }

        private static readonly Devolucion[] s_devoluciones = new Devolucion[Capacidad];
        private static int s_siguiente;
        private static bool s_enganchado;
        private static MagicCaster s_caster;
        private static PlayerShieldController s_escudo;
        private static GameObject s_cuerpo;
        private static float s_ultimoLanzamiento = -999f;

        private const int Lanzamientos = 8;
        private static readonly MagicSpellSO[] s_lanzadoQue = new MagicSpellSO[Lanzamientos];
        private static readonly float[] s_lanzadoCuando = new float[Lanzamientos];
        private static int s_lanzadoSiguiente;
        private static MagicSpellSO s_ultimoHechizo;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_enganchado = false;
            s_caster = null;
            s_escudo = null;
            s_cuerpo = null;
            s_ultimoLanzamiento = -999f;
            s_siguiente = 0;
            for (int i = 0; i < Capacidad; i++) s_devoluciones[i] = default;
            s_lanzadoSiguiente = 0;
            s_ultimoHechizo = null;
            for (int i = 0; i < Lanzamientos; i++) { s_lanzadoQue[i] = null; s_lanzadoCuando[i] = -999f; }
        }
#endif

        /// <summary>Se engancha la primera vez que un cerebro lo necesita.</summary>
        public static void Asegurar()
        {
            if (!s_enganchado)
            {
                s_enganchado = true;
                PlayerService.OnPlayerRegistered += AlRegistrarseElJugador;
                PlayerShieldController.AlDevolverAtaque += AlDevolver;
            }
            var actual = PlayerService.Player;
            if (actual != s_cuerpo) AlRegistrarseElJugador(actual);
        }

        /// <summary>Está en pleno gesto de lanzar (la carga antes de que salga el hechizo).</summary>
        public static bool Cargando => s_caster != null && s_caster.isActiveAndEnabled && s_caster.IsCasting;

        /// <summary>Segundos desde el último hechizo que lanzó.</summary>
        public static float SegundosDesdeQueLanzo => Time.time - s_ultimoLanzamiento;

        /// <summary>Tiene el escudo levantado.</summary>
        public static bool Defendiendo => s_escudo != null && s_escudo.isActiveAndEnabled && s_escudo.IsDefending;

        /// <summary>El jugador tiene fijado como objetivo a ese personaje.</summary>
        public static bool ApuntaA(Transform quien)
        {
            if (s_caster == null || quien == null) return false;
            return s_caster.TryGetTarget(out var objetivo) && objetivo != null &&
                   (objetivo == quien || objetivo.IsChildOf(quien));
        }

        /// <summary>
        /// Veces que el jugador ha devuelto en los últimos 'segundos' un proyectil lanzado por
        /// alguno de 'lanzadores' (el grupo aprende junto: si se lo devuelven a uno, el otro también lo sabe).
        /// </summary>
        public static int DevolucionesRecientes(System.Collections.Generic.IReadOnlyList<GameObject> lanzadores, float segundos)
        {
            int n = 0;
            float desde = Time.time - segundos;
            for (int i = 0; i < Capacidad; i++)
            {
                var d = s_devoluciones[i];
                if (d.lanzador == null || d.cuando < desde) continue;
                for (int j = 0; j < lanzadores.Count; j++)
                    if (lanzadores[j] == d.lanzador) { n++; break; }
            }
            return n;
        }

        private static void AlRegistrarseElJugador(GameObject cuerpo)
        {
            if (s_caster != null) s_caster.OnSpellCast -= AlLanzar;
            s_cuerpo = cuerpo;
            s_caster = cuerpo != null ? cuerpo.GetComponentInChildren<MagicCaster>(true) : null;
            s_escudo = cuerpo != null ? cuerpo.GetComponentInChildren<PlayerShieldController>(true) : null;
            if (s_caster != null) s_caster.OnSpellCast += AlLanzar;
        }

        /// <summary>
        /// Veces que el jugador ha lanzado en los últimos 'segundos' el mismo hechizo que lanzó el
        /// último (0 si hace más de 'segundos' que no lanza nada).
        /// </summary>
        public static int RepeticionesDelUltimo(float segundos)
        {
            if (s_ultimoHechizo == null) return 0;
            float desde = Time.time - segundos;
            int n = 0;
            for (int i = 0; i < Lanzamientos; i++)
                if (s_lanzadoQue[i] == s_ultimoHechizo && s_lanzadoCuando[i] >= desde) n++;
            return n;
        }

        private static void AlLanzar(MagicSpellSO hechizo, CastHand mano)
        {
            s_ultimoLanzamiento = Time.time;
            if (hechizo == null) return;
            s_ultimoHechizo = hechizo;
            s_lanzadoQue[s_lanzadoSiguiente] = hechizo;
            s_lanzadoCuando[s_lanzadoSiguiente] = Time.time;
            s_lanzadoSiguiente = (s_lanzadoSiguiente + 1) % Lanzamientos;
        }

        private static void AlDevolver(GameObject lanzador)
        {
            if (lanzador == null) return;
            s_devoluciones[s_siguiente] = new Devolucion { lanzador = lanzador, cuando = Time.time };
            s_siguiente = (s_siguiente + 1) % Capacidad;
        }
    }
}
