using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Foto de la situación que el cerebro toma en cada decisión y con la que puntúa cada opción.
    /// Solo datos: lo rellena <see cref="CerebroDeCombatePorPuntuacion"/>.
    /// </summary>
    public struct SituacionDeCombate
    {
        // Objetivo
        public Transform objetivo;
        public float distancia;
        public bool veAlObjetivo;
        public bool lineaDeFuego;
        public float segundosSinVer;

        // Recursos propios (0–1)
        public float vida;
        public float mana;
        /// Puede pagar al menos una acción del catálogo (aunque esté enfriándose).
        public bool manaParaAlgunAtaque;

        // Lo que le viene
        public bool hechizoEntrante;
        public float segundosHastaElHechizo;
        public Vector3 deDondeViene;
        /// Ya decidió (una vez por hechizo, según su dificultad) que este lo esquiva.
        public bool decideEsquivarlo;

        // Lectura del jugador
        public bool jugadorCargando;
        public bool jugadorMeApunta;
        public float segundosDesdeQueLanzo;
        public bool jugadorDefendiendo;
        /// Segundos seguidos que lleva con el escudo arriba (0 si no lo tiene).
        public float segundosDefendiendo;
        /// Devoluciones con la B a proyectiles del grupo en los últimos segundos.
        public int devolucionesRecientes;

        // Grupo
        public RolEnElGrupo rol;
        public int companeros;
        public bool descansandoTurno;

        // Terreno
        /// De 0 a 8: cuántos lados tiene tapados a 2,5 m. 4 o más es un rincón.
        public int encierro;
        /// Personajes del grupo del jugador que le ven ahora mismo.
        public int amenazasQueMeVen;
        public bool aCubierto;

        // Memoria corta
        public float segundosDesdeMiAtaque;
        public float segundosDesdeElGolpe;
        public float danoReciente;

        public bool EnRincon => encierro >= 4;
    }
}
