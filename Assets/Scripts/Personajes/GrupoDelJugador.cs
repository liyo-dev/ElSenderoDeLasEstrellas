using UnityEngine;

/// Lo que es del grupo y no de un personaje: el inventario, la ropa que se tiene, las cargas del
/// ataque doble de Estela y Liam, quien pone la partida en los cuerpos (PlayerPresetService) y la
/// celebración de victoria (CelebracionDeVictoria).
/// Uno por escena, al lado de los cuerpos
/// (`Assets/Prefabs/GrupoDelJugador.prefab`). PlayerService busca aquí lo que no encuentra en el
/// cuerpo del jugador (`PlayerService.TryGetComponent`). Ver INC-484.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class GrupoDelJugador : MonoBehaviour
{
    private void Awake()
    {
        PlayerService.RegistrarGrupo(gameObject);
        // La entrada de ataques de equipo pertenece al grupo y permanece activa;
        // sus requisitos de carga, compañeros y estado se comprueban al pulsar.
        var ataquesDeEquipo = GetComponent<DuoSpecialAttackSystem>();
        if (ataquesDeEquipo != null && !ataquesDeEquipo.enabled) ataquesDeEquipo.enabled = true;
        // Vida, maná y caídas de cada personaje (INC-717): si el prefab aún no lo trae, se pone aquí.
        if (GetComponent<SaludDelGrupo>() == null) gameObject.AddComponent<SaludDelGrupo>();
        if (GetComponent<ReanimarManteniendoA>() == null) gameObject.AddComponent<ReanimarManteniendoA>();
        // La celebración de victoria (cámara, música, foto del grupo) es de la escena, no de un cuerpo.
        if (GetComponent<Game.Player.CelebracionDeVictoria>() == null) gameObject.AddComponent<Game.Player.CelebracionDeVictoria>();
    }

    private void OnDestroy() => PlayerService.QuitarGrupo(gameObject);
}
