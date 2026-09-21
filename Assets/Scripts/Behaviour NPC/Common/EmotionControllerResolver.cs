using UnityEngine;

namespace Game.NPC.Common
{
    /// Elige el NPCEmotionController "bueno" de un personaje.
    ///
    /// Hace falta porque algunos prefabs llevan MÁS DE UNO en el mismo GameObject, uno por variante
    /// de malla/atuendo: `_WILL.prefab` tiene dos, los dos con m_Enabled a 1, pero solo uno tiene
    /// asignados sus meshes de ojos y boca (el otro los tiene a 0 — es un duplicado que quedó ahí).
    /// Un `GetComponent&lt;NPCEmotionController&gt;()` devuelve el primero que encuentre, que puede
    /// perfectamente ser el vacío — y entonces la cara del personaje no cambia nunca, en silencio,
    /// sin ningún error. Eso es justo lo que pasaba con Will.
    ///
    /// Ya estaba documentado como problema conocido en MagoOscuroFinalBattleSequencer ("_WILL.prefab
    /// puede llevar más de un NPCEmotionController... confirma en el Inspector cuál es el activo"),
    /// pero la solución de entonces fue arrastrarlo a mano en el Inspector de esa cinemática. Esto
    /// lo resuelve por código, una vez, para todo el que lo necesite.
    ///
    /// IMPORTANTE: resolver TARDE, no en Awake(). El criterio se apoya en EyeMeshCount/MouthMeshCount,
    /// que cada controlador rellena en su propio Awake() a partir de sus meshes originales — y el
    /// orden de Awake() entre componentes del mismo GameObject no está garantizado. Llamar a esto
    /// desde Awake() puede devolver el controlador equivocado. Por eso todos sus usuarios lo
    /// resuelven de forma perezosa, la primera vez que hace falta cambiar una cara.
    public static class EmotionControllerResolver
    {
        public static NPCEmotionController Resolve(GameObject root)
        {
            if (root == null) return null;

            var all = root.GetComponentsInChildren<NPCEmotionController>(true);
            if (all == null || all.Length == 0) return null;
            if (all.Length == 1) return all[0];

            // 1) El que de verdad tiene caras que poner.
            foreach (var c in all)
                if (c != null && (c.EyeMeshCount > 0 || c.MouthMeshCount > 0))
                    return c;

            // 2) Si ninguno las tiene todavía, al menos uno activo y habilitado.
            foreach (var c in all)
                if (c != null && c.isActiveAndEnabled)
                    return c;

            // 3) Lo que haya.
            return all[0];
        }
    }
}
