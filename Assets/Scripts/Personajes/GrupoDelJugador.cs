using UnityEngine;

/// Lo que es del grupo y no de un personaje: el inventario, la ropa que se tiene, las cargas del
/// ataque doble de Estela y Liam. Uno por escena, al lado de los cuerpos
/// (`Assets/Prefabs/GrupoDelJugador.prefab`). PlayerService busca aquí lo que no encuentra en el
/// cuerpo del jugador (`PlayerService.TryGetComponent`). Ver INC-484.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-900)]
public sealed class GrupoDelJugador : MonoBehaviour
{
    private void Awake() => PlayerService.RegistrarGrupo(gameObject);

    private void OnDestroy() => PlayerService.QuitarGrupo(gameObject);
}
