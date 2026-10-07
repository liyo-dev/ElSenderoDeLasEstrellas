using Game.NPC;
using UnityEngine;

/// Pestaña «Trucos» del LAB: invencible, maná infinito, cámara lenta y curar al grupo. Solo
/// existen en el LAB (los añade su arranque) y se apagan al salir de la escena.
public sealed class TrucosDelLab : MonoBehaviour, ISeccionDelLab
{
    private const float VelocidadLenta = 0.4f;

    private PlayerHealthSystem _salud;
    private ManaPool _mana;
    private bool _invencible;
    private bool _manaInfinito;
    private bool _lento;

    public string Titulo => "Trucos";
    public int Orden => 60;

    private void Update()
    {
        if (!_manaInfinito) return;
        if (_mana == null) Buscar();
        if (_mana != null && _mana.Current < _mana.Max) _mana.Refill(_mana.Max);
    }

    private void OnDisable()
    {
        if (_lento) TimeScaleArbiterService.Release(this);
        if (_invencible && _salud != null) _salud.SetGodMode(false);
    }

    private void Buscar()
    {
        var jugador = PlayerService.Player;
        if (jugador == null) return;
        _salud = jugador.GetComponentInChildren<PlayerHealthSystem>(true);
        _mana = jugador.GetComponentInChildren<ManaPool>(true);
    }

    public void Dibujar()
    {
        if (_salud == null || _mana == null) Buscar();
        GUILayout.Label("Ayudas para probar con calma. Solo funcionan en el LAB.", EstiloDelLab.Etiqueta);
        GUILayout.BeginHorizontal();
        if (EstiloDelLab.Opcion($"Invencible: {(_invencible ? "Sí" : "No")}", _invencible))
        {
            _invencible = !_invencible;
            if (_salud != null) _salud.SetGodMode(_invencible);
        }
        if (EstiloDelLab.Opcion($"Maná infinito: {(_manaInfinito ? "Sí" : "No")}", _manaInfinito)) _manaInfinito = !_manaInfinito;
        if (EstiloDelLab.Opcion($"Cámara lenta: {(_lento ? "Sí" : "No")}", _lento))
        {
            _lento = !_lento;
            if (_lento) TimeScaleArbiterService.Request(this, VelocidadLenta);
            else TimeScaleArbiterService.Release(this);
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Curar a todo el grupo", EstiloDelLab.Boton, GUILayout.Width(260f))) CurarGrupo();
        GUILayout.Label("La cámara lenta va a menos de la mitad; se nota al cerrar el panel.", EstiloDelLab.Nota);
    }

    private void CurarGrupo()
    {
        if (_salud != null) _salud.Revive(1f);
        if (PlayerParty.HasInstance)
            foreach (var m in PlayerParty.Instance.Members)
            {
                var vida = m != null ? m.GetComponent<Damageable>() : null;
                if (vida != null) vida.Heal(vida.Max);
            }
        PanelDelLab.Aviso("Grupo curado");
    }
}
