using UnityEngine;

public enum SaveRequestContext
{
    Manual,
    Auto
}

/// <summary>
/// Configuración de arranque: con qué preset empieza una partida nueva, si se arranca desde un
/// preset de pruebas en vez del guardado y si se permiten auto-guardados. Solo datos: la partida
/// en curso vive en <see cref="Partida"/>.
/// </summary>
[CreateAssetMenu(fileName = "GameBootProfile", menuName = "El Sendero/Core/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
    [Header("Arranque")]
    public string sceneToLoad = "MainWorld";
    [Tooltip("Preset con el que empieza una partida nueva.")]
    public PlayerPresetSO defaultPlayerPreset;

    [Header("Pruebas")]
    [Tooltip("Ignora el guardado y arranca desde el preset de pruebas.")]
    public bool usePresetInsteadOfSave;
    public PlayerPresetSO bootPreset;

    [Header("Guardado")]
    [Tooltip("Permite auto-guardados fuera de los puntos de guardado.")]
    public bool allowAutoSaves;

    public bool ShouldBootFromPreset() => usePresetInsteadOfSave && bootPreset != null;
}
