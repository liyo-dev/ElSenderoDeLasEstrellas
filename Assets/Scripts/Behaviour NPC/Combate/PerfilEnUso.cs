using System.Collections.Generic;
using UnityEngine;
using Game.NPC.Modules;

namespace Game.NPC.Combate
{
    /// <summary>
    /// El perfil con el que pelea un NPC en este combate: el <see cref="PerfilDeCombate"/> que tenga
    /// asignado y, para lo que falte, lo que dice su <see cref="NPCCombatConfig"/>. Es también el
    /// adaptador de los ataques de siempre: los tres huecos de hechizo de la config entran en el
    /// catálogo sin rehacer ningún asset. Se monta una vez al empezar el combate.
    /// </summary>
    public sealed class PerfilEnUso
    {
        private static readonly string[] AnimacionDelHueco = { "MagicLeft", "MagicRight", "MagicSpecial" };
        private static readonly FuncionDeAccion[] FuncionDelHueco = { FuncionDeAccion.Presion, FuncionDeAccion.Golpe, FuncionDeAccion.Remate };

        public readonly List<AccionDeCombate> Acciones = new List<AccionDeCombate>();
        public DefensasDeCombate Defensas { get; private set; }
        public CaracterDeCombate Caracter { get; private set; }
        public float Dificultad { get; private set; }

        public float MaxMana { get; private set; }
        public float RecargaDeMana { get; private set; }
        public float EsperaParaRecargar { get; private set; }
        public float DuracionDeBusqueda { get; private set; }

        /// <summary>Fases a partir de la segunda, de más vida a menos (vacío = una sola fase).</summary>
        public readonly List<FaseDeCombate> Fases = new List<FaseDeCombate>();

        /// <summary>
        /// Pasa a pelear como dice la fase: su catálogo (si trae alguno con proyectil), su carácter,
        /// sus defensas y su dificultad, cada cosa solo si la fase la cambia.
        /// </summary>
        public void Aplicar(FaseDeCombate fase)
        {
            if (fase == null) return;
            bool hayAcciones = false;
            for (int i = 0; i < fase.acciones.Count; i++)
                if (fase.acciones[i] != null && fase.acciones[i].proyectil != null) { hayAcciones = true; break; }
            if (hayAcciones)
            {
                Acciones.Clear();
                for (int i = 0; i < fase.acciones.Count; i++)
                    if (fase.acciones[i] != null && fase.acciones[i].proyectil != null) Acciones.Add(fase.acciones[i]);
            }
            if (fase.cambiaCaracter && fase.caracter != null) Caracter = fase.caracter;
            if (fase.cambiaDefensas && fase.defensas != null) Defensas = fase.defensas;
            if (fase.dificultad >= 0f) Dificultad = fase.dificultad;
        }

        public static PerfilEnUso Montar(PerfilDeCombate perfil, NPCCombatConfig config)
        {
            var uso = new PerfilEnUso();
            uso.MaxMana = config != null ? Mathf.Max(1f, config.maxMana) : 100f;
            uso.RecargaDeMana = config != null ? Mathf.Max(0f, config.manaRegenPerSecond) : 15f;
            uso.EsperaParaRecargar = config != null ? Mathf.Max(0f, config.manaRegenDelayAfterSpend) : 1f;
            uso.DuracionDeBusqueda = config != null
                ? (config.activelySearchForPlayer ? config.searchDuration : config.passiveSearchDuration)
                : 12f;

            if (perfil != null)
            {
                uso.Defensas = perfil.defensas;
                uso.Caracter = perfil.caracter;
                uso.Dificultad = perfil.dificultad;
                for (int i = 0; i < perfil.acciones.Count; i++)
                    if (perfil.acciones[i] != null && perfil.acciones[i].proyectil != null) uso.Acciones.Add(perfil.acciones[i]);
                for (int i = 0; i < perfil.fases.Count; i++)
                    if (perfil.fases[i] != null) uso.Fases.Add(perfil.fases[i]);
                // De más vida a menos: es el orden en que se entra en ellas.
                uso.Fases.Sort((a, b) => b.desdeVida.CompareTo(a.desdeVida));
            }
            else
            {
                uso.Defensas = DefensasDesde(config);
                uso.Caracter = CaracterDesde(config);
                uso.Dificultad = config != null ? config.difficultyLevel : 0.6f;
            }

            if (uso.Acciones.Count == 0) AnadirHuecosDeLaConfig(uso.Acciones, config, uso.Caracter);
            return uso;
        }

        /// <summary>Los tres hechizos de la config como acciones del catálogo.</summary>
        public static void AnadirHuecosDeLaConfig(List<AccionDeCombate> acciones, NPCCombatConfig config, CaracterDeCombate caracter)
        {
            if (config == null) return;
            float alcance = Mathf.Max(caracter.distanciaMaxima, caracter.distanciaPreferida + 3f);
            for (int hueco = 0; hueco < 3; hueco++)
            {
                var prefab = config.GetSpellPrefab(hueco);
                if (prefab == null) continue;
                acciones.Add(new AccionDeCombate
                {
                    nombre = $"Hechizo {hueco + 1}",
                    proyectil = prefab,
                    animacion = AnimacionDelHueco[hueco],
                    distanciaMinima = 0f,
                    distanciaMaxima = alcance,
                    segundosDeAviso = hueco == 2 ? 0.8f : 0.5f,
                    segundosDeRecuperacion = hueco == 2 ? 0.8f : 0.45f,
                    enfriamiento = config.GetSpellCooldown(hueco),
                    costeDeMana = config.GetSpellManaCost(hueco),
                    dano = config.GetSpellDamage(hueco),
                    velocidad = 0f,
                    peso = Mathf.Max(0.05f, config.GetSpellChance(hueco)),
                    funcion = FuncionDelHueco[hueco],
                    sePuedeDevolver = true,
                });
            }
        }

        private static DefensasDeCombate DefensasDesde(NPCCombatConfig c)
        {
            var d = new DefensasDeCombate();
            if (c == null) return d;
            d.escudo = c.useShield;
            d.duracionDelEscudo = Mathf.Clamp(c.shieldMaxDuration, 0.5f, 3f);
            d.enfriamientoDelEscudo = c.shieldCooldown;
            d.radioDeCobertura = Mathf.Max(4f, c.coverSearchRadius);
            return d;
        }

        private static CaracterDeCombate CaracterDesde(NPCCombatConfig c)
        {
            var k = new CaracterDeCombate();
            if (c == null) return k;
            k.agresividad = c.isAggressive ? 0.7f : 0.45f;
            k.cautela = Mathf.Clamp01(0.3f + c.lowManaRetreatThreshold);
            k.movilidad = 0.6f;
            k.paciencia = 0.5f;
            // Las configs de siempre dan una franja de ataque muy estrecha (p. ej. 3–4 m): se abre
            // para que el cerebro tenga sitio donde recolocarse sin salirse del alcance.
            k.distanciaMinima = Mathf.Max(1.5f, c.minAttackDistance);
            k.distanciaMaxima = Mathf.Max(k.distanciaMinima + 4f, c.maxAttackDistance);
            k.distanciaPreferida = Mathf.Clamp((c.minAttackDistance + c.maxAttackDistance) * 0.5f + 2f, k.distanciaMinima + 1f, k.distanciaMaxima - 1f);
            k.atacantesALaVez = c.maxConcurrentAttackersOnTarget > 0 ? c.maxConcurrentAttackersOnTarget : 1;
            return k;
        }
    }
}
