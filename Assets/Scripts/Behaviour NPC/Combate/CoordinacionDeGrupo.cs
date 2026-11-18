using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>Qué hace un NPC dentro de su grupo en este momento.</summary>
    public enum RolEnElGrupo
    {
        /// Pelea solo contra ese objetivo.
        Solo,
        /// Tiene (o va a por) el turno de ataque: presiona de frente.
        Presiona,
        /// Otro tiene el turno: le rodea para atacar desde otro lado cuando le toque.
        Rodea,
    }

    /// <summary>
    /// Los cerebros por puntuación que están peleando, para que un grupo se reparta: quién
    /// presiona (el que tiene el turno de <see cref="NPCAttackCoordinator"/>) y hacia qué lado
    /// rodea cada uno de los demás, sin amontonarse. No decide por nadie: cada cerebro pregunta.
    /// </summary>
    public static class CoordinacionDeGrupo
    {
        private static readonly List<CerebroDeCombatePorPuntuacion> s_activos = new List<CerebroDeCombatePorPuntuacion>();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_activos.Clear();
#endif

        public static void Entrar(CerebroDeCombatePorPuntuacion cerebro)
        {
            if (cerebro != null && !s_activos.Contains(cerebro)) s_activos.Add(cerebro);
        }

        public static void Salir(CerebroDeCombatePorPuntuacion cerebro) => s_activos.Remove(cerebro);

        /// <summary>Los otros que pelean contra el mismo objetivo (en 'salida', sin incluirle).</summary>
        public static void CompanerosContra(Transform objetivo, CerebroDeCombatePorPuntuacion yo, List<CerebroDeCombatePorPuntuacion> salida)
        {
            salida.Clear();
            for (int i = s_activos.Count - 1; i >= 0; i--)
            {
                var c = s_activos[i];
                if (c == null) { s_activos.RemoveAt(i); continue; }
                if (c != yo && c.Objetivo == objetivo) salida.Add(c);
            }
        }

        /// <summary>Todos los que pelean ahora (para el grupo que aprende de las devoluciones).</summary>
        public static void TodosLosCuerpos(List<GameObject> salida)
        {
            salida.Clear();
            for (int i = 0; i < s_activos.Count; i++)
                if (s_activos[i] != null) salida.Add(s_activos[i].gameObject);
        }

        /// <summary>
        /// Su papel contra ese objetivo: solo; presiona (tiene turno, o queda turno libre y es de
        /// los que más llevan sin atacar); o rodea (los turnos están cogidos o le toca a otro).
        /// </summary>
        public static RolEnElGrupo Rol(CerebroDeCombatePorPuntuacion yo, Transform objetivo, List<CerebroDeCombatePorPuntuacion> companeros, int atacantesALaVez)
        {
            if (companeros.Count == 0) return RolEnElGrupo.Solo;
            if (NPCAttackCoordinator.HasTurn(objetivo, yo)) return RolEnElGrupo.Presiona;
            int libres = Mathf.Max(1, atacantesALaVez) - NPCAttackCoordinator.TurnCount(objetivo);
            if (libres <= 0) return RolEnElGrupo.Rodea;
            // Quedan turnos libres: los cogen los que más tiempo llevan sin atacar.
            float miEspera = yo.SegundosDesdeMiAtaque;
            int porDelante = 0;
            for (int i = 0; i < companeros.Count; i++)
            {
                var c = companeros[i];
                if (NPCAttackCoordinator.HasTurn(objetivo, c)) continue;
                float suEspera = c.SegundosDesdeMiAtaque;
                if (suEspera > miEspera || (Mathf.Approximately(suEspera, miEspera) && c.ClaveDeOrden < yo.ClaveDeOrden)) porDelante++;
            }
            return porDelante < libres ? RolEnElGrupo.Presiona : RolEnElGrupo.Rodea;
        }

        /// <summary>
        /// Punto al que ir a rodear: a 'distancia' del objetivo, girado respecto a la línea del que
        /// presiona (o a la suya si nadie presiona). Cada rodeador toma un lado distinto
        /// (+115°, −115°, +150°, −150°...) según su orden estable en el grupo.
        /// </summary>
        public static Vector3 PuntoDeRodeo(CerebroDeCombatePorPuntuacion yo, Transform objetivo, List<CerebroDeCombatePorPuntuacion> companeros, float distancia)
        {
            Vector3 centro = objetivo.position;
            Vector3 referencia = yo.transform.position - centro;
            var quien = NPCAttackCoordinator.TurnHolder(objetivo) as Component;
            if (quien == null || quien == yo) quien = ElSiguienteEnAtacar(companeros);
            if (quien != null && quien != yo) referencia = quien.transform.position - centro;
            referencia.y = 0f;
            if (referencia.sqrMagnitude < 0.01f) referencia = Vector3.forward;
            referencia.Normalize();

            int orden = 0;
            int miClave = yo.ClaveDeOrden;
            for (int i = 0; i < companeros.Count; i++)
                if (companeros[i] != quien && companeros[i].ClaveDeOrden < miClave) orden++;

            float lado = (orden % 2 == 0) ? 1f : -1f;
            float angulo = lado * (115f + 35f * (orden / 2));
            return centro + Quaternion.Euler(0f, angulo, 0f) * referencia * distancia;
        }

        // Sin nadie con turno: el compañero que más lleva sin atacar es el que va a presionar.
        private static CerebroDeCombatePorPuntuacion ElSiguienteEnAtacar(List<CerebroDeCombatePorPuntuacion> companeros)
        {
            CerebroDeCombatePorPuntuacion mejor = null;
            float espera = float.MinValue;
            for (int i = 0; i < companeros.Count; i++)
                if (companeros[i] != null && companeros[i].SegundosDesdeMiAtaque > espera)
                {
                    espera = companeros[i].SegundosDesdeMiAtaque;
                    mejor = companeros[i];
                }
            return mejor;
        }
    }
}
