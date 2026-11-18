using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>
    /// Lo que una opción de combate puede pedirle al cuerpo que la ejecuta: moverse, mirar, gastar
    /// maná, pedir turno, lanzar una acción y consultar su perfil, su memoria y los rayos. Las
    /// opciones no conocen al MonoBehaviour: solo esto.
    /// </summary>
    public interface ICombatiente
    {
        Transform Transform { get; }
        Transform Objetivo { get; }
        PerfilEnUso Perfil { get; }
        MemoriaDeCombate Memoria { get; }
        SondeoDelTerreno Sondeo { get; }

        /// <summary>Personajes del grupo del jugador en pie (los que pueden atacarle).</summary>
        IReadOnlyList<Transform> Amenazas { get; }

        /// <summary>Compañeros de su grupo que pelean contra el mismo objetivo.</summary>
        IReadOnlyList<Transform> Companeros { get; }

        // Movimiento
        bool MoverA(Vector3 destino, bool corriendo);
        void Parar();
        bool HaLlegado(float margen);
        bool PuntoAlcanzable(Vector3 punto, out Vector3 enMalla);
        void Mirar(Vector3 punto);

        // Ataque
        bool AccionLista(int indice);
        bool PuedePagar(float coste);
        bool PedirTurno();
        void SoltarTurno();
        LanzadorDeAcciones Lanzador { get; }

        // Defensa
        bool EscudoListo { get; }
        void LevantarEscudo(float segundos);
        void BajarEscudo();
        bool EsquivaLista { get; }
        void EsquivaHecha();

        /// <summary>El mejor escondite cercano que le tapa de todos los que le atacan.</summary>
        bool BuscarCobertura(out Vector3 sitio);

        /// <summary>Punto al que ir para rodear al objetivo mientras otro del grupo presiona.</summary>
        Vector3 PuntoDeRodeo();

        /// <summary>Está a cubierto (recarga más deprisa) o deja de estarlo.</summary>
        bool ACubierto { get; set; }

        /// <summary>Lleva demasiado sin ver al objetivo: deja el combate.</summary>
        void AbandonarCombate();
    }
}
