using System.Collections;
using UnityEngine;

namespace Game.Player
{
    /// Paso del cierre de batalla: la celebración de la victoria (cámara, salto, pose, música). La
    /// hace CelebracionDeVictoria, que registra este paso mientras está activa. Ver INC-470.
    public sealed class PasoCelebracionDeVictoria : IPasoDeCierre
    {
        private readonly CelebracionDeVictoria _jugador;
        private readonly float _esperaTrasDerrota;

        public PasoCelebracionDeVictoria(CelebracionDeVictoria jugador, float esperaTrasDerrota)
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
            // Y que se vea cómo se deshace si se está esfumando (con un tope, por si algo falla).
            float tope = Time.time + 5f;
            while (EsfumarseAlCaer.HayAlgunoEnCurso && Time.time < tope) yield return null;
            // Si otro sistema ya lo está celebrando (un jefe NPC con su propio cierre), no se
            // celebra dos veces: se espera a que acabe.
            if (_jugador.IsPlayingVictory)
            {
                while (_jugador.IsPlayingVictory) yield return null;
                yield break;
            }
            yield return _jugador.CelebrarVictoria(resultado.BattleId);
            resultado.AnotarEnLaFoto(_jugador.PersonajesEnLaFoto);
        }

        // Al final de todo el cierre (después del informe): cámara, input y control de vuelta.
        // La música no se toca: el jingle de victoria devuelve la del lugar al acabar (INC-500).
        public void Terminar(ResultadoDeBatalla resultado)
        {
            if (_jugador != null) _jugador.TerminarVictoria();
        }
    }
}
