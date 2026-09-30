using UnityEngine;

/// <summary>Referencias serializadas a los prefabs reales que usa el modo Party de CombatLab.</summary>
public sealed class CombatLabConfig : ScriptableObject
{
    public GameObject liamPrefab;
    public GameObject estelaPrefab;
    [Tooltip("Quién guía en la estación del jefe (F4). Vacío = nadie habla. Ver INC-489.")]
    public GuionDeCombate guionJefe;
}
