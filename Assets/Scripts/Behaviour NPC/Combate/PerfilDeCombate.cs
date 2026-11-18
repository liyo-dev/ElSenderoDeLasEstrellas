using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC.Combate
{
    /// <summary>Para qué sirve una acción de ataque cuando el cerebro la puntúa.</summary>
    public enum FuncionDeAccion
    {
        [Tooltip("Ataque normal.")]
        Golpe = 0,

        [Tooltip("Barato y rápido: para no dejar respirar al objetivo (lo prefiere quien presiona).")]
        Presion = 1,

        [Tooltip("Fuerte: se reserva para cuando el objetivo acaba de lanzar o no puede defenderse.")]
        Remate = 2,
    }

    /// <summary>Un ataque del catálogo de un perfil de combate.</summary>
    [Serializable]
    public sealed class AccionDeCombate
    {
        [Tooltip("Nombre para depurar (sale en el Inspector del cerebro).")]
        public string nombre = "Ataque";

        [Tooltip("Proyectil que se lanza (con EnemyProjectile para que avise, se pueda devolver y se le asigne el lanzador).")]
        public GameObject proyectil;

        [Tooltip("Estado del Animator (capa de medio cuerpo) que se reproduce al lanzar.")]
        public string animacion = "MagicRight";

        [Min(0f), Tooltip("Distancia mínima al objetivo para usarla (m).")]
        public float distanciaMinima = 0f;

        [Min(0.5f), Tooltip("Distancia máxima al objetivo para usarla (m).")]
        public float distanciaMaxima = 10f;

        [Min(0f), Tooltip("Segundos de aviso: la bola crece en la mano y el jugador ve el aviso antes de que salga (0 = sale sin aviso).")]
        public float segundosDeAviso = 0.6f;

        [Min(0f), Tooltip("Segundos que se queda expuesto tras lanzar: la ventana de castigo del jugador.")]
        public float segundosDeRecuperacion = 0.5f;

        [Min(0f), Tooltip("Segundos hasta poder repetir esta acción.")]
        public float enfriamiento = 2f;

        [Min(0f), Tooltip("Maná que cuesta.")]
        public float costeDeMana = 20f;

        [Min(0f), Tooltip("Daño del proyectil.")]
        public float dano = 10f;

        [Min(0f), Tooltip("Velocidad del proyectil (m/s). 0 = la del prefab.")]
        public float velocidad = 0f;

        [Min(0f), Tooltip("Preferencia relativa frente a las demás acciones (1 = normal).")]
        public float peso = 1f;

        [Tooltip("Para qué la usa el cerebro.")]
        public FuncionDeAccion funcion = FuncionDeAccion.Golpe;

        [Tooltip("El jugador puede devolverla con la B. Si el jugador abusa de la B, el cerebro prefiere las que no.")]
        public bool sePuedeDevolver = true;
    }

    /// <summary>Cómo se defiende.</summary>
    [Serializable]
    public sealed class DefensasDeCombate
    {
        [Tooltip("Se aparta de los hechizos que le llegan.")]
        public bool esquivar = true;

        [Min(0.5f), Tooltip("Metros del salto lateral.")]
        public float distanciaDeEsquiva = 3f;

        [Min(0.2f), Tooltip("Segundos entre esquivas.")]
        public float recuperacionDeEsquiva = 1.2f;

        [Tooltip("Levanta el escudo (necesita NPCShieldController: se pone si la Combat Config lleva escudo).")]
        public bool escudo = true;

        [Min(0.5f), Tooltip("Segundos que lo mantiene como mucho.")]
        public float duracionDelEscudo = 2.5f;

        [Min(0f), Tooltip("Segundos hasta poder volver a levantarlo.")]
        public float enfriamientoDelEscudo = 8f;

        [Tooltip("Con poco maná busca cobertura de verdad (comprobada con rayos) para recargar.")]
        public bool cubrirseParaRecargar = true;

        [Min(4f), Tooltip("Radio en el que busca cobertura (m).")]
        public float radioDeCobertura = 14f;

        [Min(1f), Tooltip("Multiplica la recarga de maná mientras está a cubierto.")]
        public float recargaACubierto = 1.5f;
    }

    /// <summary>Carácter: cuánto arriesga, cuánto se mueve, cuánto espera.</summary>
    [Serializable]
    public sealed class CaracterDeCombate
    {
        [Range(0f, 1f), Tooltip("Ganas de atacar y de acortar las pausas entre ataques.")]
        public float agresividad = 0.6f;

        [Range(0f, 1f), Tooltip("Cuánto le importa no recibir: esquivar, escudo, cubrirse antes.")]
        public float cautela = 0.5f;

        [Range(0f, 1f), Tooltip("Cuánto se recoloca y rodea en vez de quedarse plantado.")]
        public float movilidad = 0.6f;

        [Range(0f, 1f), Tooltip("Cuánto espera su turno y recarga antes de volver a la carga.")]
        public float paciencia = 0.5f;

        [Min(1f), Tooltip("Distancia a la que le gusta pelear (m).")]
        public float distanciaPreferida = 7f;

        [Min(0.5f), Tooltip("Si el objetivo se acerca más, se aparta (m).")]
        public float distanciaMinima = 3f;

        [Min(1f), Tooltip("Si el objetivo se aleja más, se acerca (m).")]
        public float distanciaMaxima = 12f;

        [Min(0.5f), Tooltip("Velocidad andando (m/s).")]
        public float velocidadAndando = 2.5f;

        [Min(1f), Tooltip("Velocidad corriendo (m/s).")]
        public float velocidadCorriendo = 6f;

        [Range(1, 4), Tooltip("Cuántos del grupo pueden atacar al mismo objetivo a la vez (NPCAttackCoordinator). Los demás rodean.")]
        public int atacantesALaVez = 1;
    }

    /// <summary>Hueco para las fases de un combate (etapa E3). Todavía no tiene efecto.</summary>
    [Serializable]
    public sealed class FaseDeCombate
    {
        public string nombre;
        [Range(0f, 1f)] public float desdeVida = 0.5f;
    }

    /// <summary>Hueco para las afinidades de elemento (etapa posterior). Todavía no tiene efecto.</summary>
    [Serializable]
    public sealed class AfinidadDeCombate
    {
        public MagicElement elemento;
        public string nota;
    }

    /// <summary>
    /// Perfil de combate de un NPC para el cerebro por puntuación: qué ataques tiene, cómo se
    /// defiende, su carácter y su dificultad. Si el catálogo está vacío, los ataques salen de los
    /// tres huecos de su NPCCombatConfig (<see cref="PerfilEnUso"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "PerfilDeCombate", menuName = "El Sendero/NPCs/Módulos/Perfil de combate", order = 3)]
    public sealed class PerfilDeCombate : ScriptableObject
    {
        [Tooltip("Ataques. Vacío = los tres hechizos de la Combat Config del NPC.")]
        public List<AccionDeCombate> acciones = new List<AccionDeCombate>();

        public DefensasDeCombate defensas = new DefensasDeCombate();

        public CaracterDeCombate caracter = new CaracterDeCombate();

        [Range(0f, 1f), Tooltip("Dificultad: reflejos, rapidez al decidir, puntería en la decisión y pausas entre ataques.")]
        public float dificultad = 0.6f;

        [Header("Sin efecto todavía")]
        [Tooltip("Fases del combate (etapa E3).")]
        public List<FaseDeCombate> fases = new List<FaseDeCombate>();

        [Tooltip("Afinidades de elemento (etapa posterior).")]
        public List<AfinidadDeCombate> afinidades = new List<AfinidadDeCombate>();
    }
}
