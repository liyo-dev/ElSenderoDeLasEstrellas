using System.Collections;
using UnityEngine;

namespace Game.Player
{
    /// Paso del cierre de batalla: la celebración de Will (cámara, salto, pose, música). La hace
    /// PlayerBattleModeController, que registra este paso mientras está activo. Ver INC-470.
    public sealed class PasoCelebracionDeVictoria : IPasoDeCierre
    {
        private readonly PlayerBattleModeController _jugador;
        private readonly float _esperaTrasDerrota;

        public PasoCelebracionDeVictoria(PlayerBattleModeController jugador, float esperaTrasDerrota)
        {
            _jugador = jugador;
            _esperaTrasDerrota = esperaTrasDerrota;
        }

        public int Orden => 100;

        public IEnumerator Ejecutar(ResultadoDeBatalla resultado)
        {
            if (_jugador == null || !_jugador.isActiveAndEnabled) yield break;
            // Que se vea caer al enemigo antes de celebrarlo.
            if (_esperaTrasDerrota > 0f) yield return new WaitForSeconds(_esperaTrasDerrota);
            // Si otro sistema ya lo está celebrando (un jefe NPC con su propio cierre), no se
            // celebra dos veces: se espera a que acabe.
            if (_jugador.IsPlayingVictory)
            {
                while (_jugador.IsPlayingVictory) yield return null;
                yield break;
            }
            yield return _jugador.CelebrarVictoria(resultado.BattleId);
        }

        // Al final de todo el cierre (después del informe): cámara, input y control de vuelta.
        public void Terminar(ResultadoDeBatalla resultado)
        {
            if (_jugador != null) _jugador.TerminarVictoria();
            // El jingle de victoria suena una vez y se para: se vuelve a la música de la zona.
            AudioService.Instance?.RestoreAfterBattle();
        }
    }
}
