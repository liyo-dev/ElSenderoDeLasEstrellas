using System;
using UnityEngine;
using Game.NPC.Modules;

namespace Game.NPC
{
    /// <summary>Qué cerebro lleva un NPC en combate.</summary>
    public enum TipoDeCerebroDeCombate
    {
        [Tooltip("La máquina de estados de siempre (NPCCombatBrain).")]
        Clasico = 0,

        [Tooltip("Decide por puntuación con un perfil de combate (CerebroDeCombatePorPuntuacion).")]
        PorPuntuacion = 1,
    }

    /// <summary>
    /// Lo que el resto del juego necesita de un cerebro de combate de NPC: arrancarlo y pararlo
    /// (CombatState), avisarle de un golpe (NPCCombatLifecycleHandler) y leer su maná (NPCManaBarUI).
    /// Lo cumplen el cerebro clásico y el de puntuación; el que esté peleando queda en
    /// <see cref="Common.NPCStateContext.CerebroDeCombate"/>.
    /// </summary>
    public interface ICerebroDeCombate
    {
        void Inicializar(NPCBehaviourManagerV2 manager);
        void Comenzar(NPCCombatConfig config);
        void Detener();

        /// <summary>Le han cortado un hechizo a medias: vuelve a decidir desde cero.</summary>
        void InterrumpirLanzamiento();

        /// <summary>Le han golpeado desde esa posición.</summary>
        void AlRecibirDano(Vector3 origen);

        float GetCurrentMana();
        float GetMaxMana();
        float GetManaPercent();
        event Action<float> OnManaChanged;
    }
}
