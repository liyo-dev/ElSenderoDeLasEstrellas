using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Navegación rápida entre escenarios aislados del laboratorio. Las teclas F1–F4 eligen
/// configuración; R recarga la escena para recuperar vida, maná, IA y objetivos.
/// </summary>
public sealed class CombatLabDirector : MonoBehaviour
{
    [SerializeField] private GameObject[] stations = new GameObject[4];
    [SerializeField] private string[] stationNames =
    {
        "Base y blanco de práctica",
        "Duelo: un enemigo",
        "Grupo: varios enemigos",
        "Jefe: Gólem"
    };

    private int _activeStation;

    private void Awake()
    {
        SelectStation(0);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame) SelectStation(0);
        else if (keyboard.f2Key.wasPressedThisFrame) SelectStation(1);
        else if (keyboard.f3Key.wasPressedThisFrame) SelectStation(2);
        else if (keyboard.f4Key.wasPressedThisFrame) SelectStation(3);

        if (keyboard.rKey.wasPressedThisFrame)
            ReloadScene();
    }

    private void SelectStation(int index)
    {
        _activeStation = Mathf.Clamp(index, 0, stations.Length - 1);
        for (int i = 0; i < stations.Length; i++)
        {
            var station = stations[i];
            if (station == null) continue;
            bool shouldBeActive = i == _activeStation;
            if (station.activeSelf != shouldBeActive)
                station.SetActive(shouldBeActive);
        }
    }

    private void OnGUI()
    {
        string station = _activeStation < stationNames.Length ? stationNames[_activeStation] : "Escenario";
        GUI.Box(new Rect(18f, 18f, 436f, 112f), "COMBAT LAB");
        GUI.Label(new Rect(34f, 46f, 385f, 22f), $"Prueba activa: {station}");
        if (GUI.Button(new Rect(34f, 72f, 76f, 26f), "F1 Base")) SelectStation(0);
        if (GUI.Button(new Rect(113f, 72f, 76f, 26f), "F2 Duelo")) SelectStation(1);
        if (GUI.Button(new Rect(192f, 72f, 76f, 26f), "F3 Grupo")) SelectStation(2);
        if (GUI.Button(new Rect(271f, 72f, 76f, 26f), "F4 Jefe")) SelectStation(3);
        if (GUI.Button(new Rect(350f, 72f, 92f, 26f), "Reiniciar")) ReloadScene();
        GUI.Label(new Rect(34f, 98f, 385f, 18f), "Atajos: F1–F4 seleccionan; R reinicia.");
    }

    private static void ReloadScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }
}
