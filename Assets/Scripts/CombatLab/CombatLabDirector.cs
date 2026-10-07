using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Escenarios de combate del área central del LAB (pestaña «Combate» del panel). Las teclas
/// F1–F4 eligen escenario; R recarga el LAB para recuperar vida, maná, IA y objetivos.
/// </summary>
public sealed class CombatLabDirector : MonoBehaviour, ISeccionDelLab
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
        DarLaSalida();
        ActualizarGuia();
    }

    /// Los enemigos de la estación abierta pelean ya, como tras la presentación en una arena.
    private void DarLaSalida()
    {
        var station = stations[_activeStation];
        if (station == null) return;
        foreach (var inicio in station.GetComponentsInChildren<IInicioDeCombate>())
            inicio.EmpezarCombate();
    }

    /// En la estación del jefe arranca la guía del combate (quien habla y qué dice sale de
    /// CombatLabConfig.guionJefe), como haría la arena en el juego. En las demás, se calla.
    private void ActualizarGuia()
    {
        var guiaActual = GetComponent<GuiaDeCombate>();
        if (guiaActual != null) guiaActual.Parar();

        var station = _activeStation < stations.Length ? stations[_activeStation] : null;
        var jefe = station != null ? station.GetComponentInChildren<IJefeConFases>(true) as Component : null;
        if (jefe == null) return;

        var config = Resources.Load<CombatLabConfig>("CombatLab/CombatLabConfig");
        var guia = GuiaDeCombate.Empezar(gameObject, config != null ? config.guionJefe : null, jefe.gameObject);
        if (guia != null) guia.LanzarIntervencion();
    }

    public string Titulo => "Combate";
    public int Orden => 10;

    public void Dibujar()
    {
        GUILayout.Label("Escenarios del área central. Al elegir uno aparecen sus enemigos y empiezan a pelear.", EstiloDelLab.Etiqueta);
        GUILayout.Space(6f);
        for (int i = 0; i < stations.Length; i++)
        {
            string nombre = i < stationNames.Length ? stationNames[i] : $"Escenario {i + 1}";
            if (EstiloDelLab.Opcion($"F{i + 1} · {nombre}", i == _activeStation)) SelectStation(i);
        }
        GUILayout.Space(10f);
        if (GUILayout.Button("Reiniciar el LAB (R): vida, maná y enemigos como al entrar", EstiloDelLab.Boton)) ReloadScene();
        GUILayout.Label("Los jefes de verdad (Demonios, Gólem y Mago Oscuro) están en la pestaña Zonas ▸ Zona de jefes.", EstiloDelLab.Nota);
    }

    private static void ReloadScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }
}
