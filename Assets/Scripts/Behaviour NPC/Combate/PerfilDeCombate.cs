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

        [Tooltip("Es magia de un elemento: rompe el escudo del jugador de su mismo color y deja al personaje aturdido 2 s. Apagado = lo que diga el proyectil (sin elemento, solo empuja).")]
        public bool conElemento;

        [Tooltip("Elemento de la magia (también tiñe la bola de su color).")]
        public MagicElement elemento = MagicElement.Fire;
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

        [Tooltip("El escudo es elemental: solo lo rompe el elemento que dicta RuptoresDeEscudo (el de su mismo elemento); lo demás rebota. Apagado = escudo común, que para todo.")]
        public bool escudoElemental;

        [Tooltip("Elemento del escudo elemental.")]
        public MagicElement elementoDelEscudo = MagicElement.Storm;

        [Tooltip("Con poco maná busca cobertura de verdad (comprobada con rayos) para recargar.")]
        public bool cubrirseParaRecargar = true;

        [Min(4f), Tooltip("Radio en el que busca cobertura (m).")]
        public float radioDeCobertura = 14f;

        [Min(1f), Tooltip("Multiplica la recarga de maná mientras está a cubierto.")]
        public float recargaACubierto = 1.5f;

        [Range(0f, 1f), Tooltip("Cuánto aprende del hechizo que el jugador repite: si lanza el mismo varias veces seguidas, lo esquiva y se protege de él más a menudo (0 = no lo nota).")]
        public float leerLaRepeticion = 0f;

        [Min(2), Tooltip("Lanzamientos del mismo hechizo, dentro de la ventana, a partir de los que empieza a leerlo.")]
        public int repeticionesParaLeerla = 3;

        [Min(3f), Tooltip("Segundos que mira hacia atrás para contar lanzamientos del mismo hechizo.")]
        public float segundosDeRepeticion = 12f;
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
        [Range(0f, 1f), Tooltip("Contra un escudo mantenido: con movilidad o dificultad desde este valor da vueltas continuas alrededor del objetivo; por debajo, flanquea una vez y espera a que lo baje.")]
        public float umbralDeVueltasContinuas = 0.8f;

        [Tooltip("Adapta sus ataques a la defensa del jugador: con el escudo mantenido deja de lanzar lo devolvible y rodea, y usa menos lo que le devuelven. Apagado = lanza igual (lecciones, entrenamientos).")]
        public bool leeLaDefensaDelJugador = true;
    }

    /// <summary>
    /// Una fase del combate a partir de la segunda: cuando la vida baja de <see cref="desdeVida"/>,
    /// el NPC hace una transición que se lee (se para, es invulnerable, dice su frase, hace su gesto
    /// y, si se pide, sacude la pantalla) y sigue peleando con otro catálogo, carácter o defensas.
    /// El golpe que cruza el umbral se queda en él: ninguna fase se salta de un solo golpe.
    /// </summary>
    [Serializable]
    public sealed class FaseDeCombate
    {
        [Tooltip("Nombre para depurar (sale en el Inspector del cerebro).")]
        public string nombre = "Fase";

        [Range(0.05f, 0.95f), Tooltip("Fracción de vida a la que empieza esta fase.")]
        public float desdeVida = 0.5f;

        [Header("Qué cambia")]
        [Tooltip("Ataques desde esta fase. Vacío = sigue con los de antes.")]
        public List<AccionDeCombate> acciones = new List<AccionDeCombate>();

        [Tooltip("Cambia el carácter por el de abajo.")]
        public bool cambiaCaracter;
        public CaracterDeCombate caracter = new CaracterDeCombate();

        [Tooltip("Cambia las defensas por las de abajo.")]
        public bool cambiaDefensas;
        public DefensasDeCombate defensas = new DefensasDeCombate();

        [Range(-1f, 1f), Tooltip("Dificultad desde esta fase (negativa = la de antes).")]
        public float dificultad = -1f;

        [Header("Transición")]
        [Min(0f), Tooltip("Segundos de la transición: quieto, invulnerable y sin atacar.")]
        public float segundosDeTransicion = 1.5f;

        [Tooltip("Estado del Animator (capa de medio cuerpo) que hace al cambiar. Vacío = ninguno.")]
        public string animacion = "";

        [Tooltip("Clave de localización de la frase que dice al cambiar (bocadillo). Vacía = usa el texto.")]
        public string claveDeFrase = "";

        [TextArea(1, 3), Tooltip("Frase que dice al cambiar, si la clave no está traducida. Vacía = no dice nada.")]
        public string frase = "";

        [Tooltip("La frase sale en la caja de diálogo y la transición espera a que el jugador la cierre (explicaciones de un combate de entrenamiento). Apagado = bocadillo.")]
        public bool fraseEnCaja;

        [Tooltip("Sacudida de cámara y destello al acabar la transición.")]
        public bool sacudida = true;

        [Range(0f, 1f), Tooltip("Recupera vida hasta esta fracción al acabar la transición (0 = nada).")]
        public float recuperaVidaHasta = 0f;
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

        [Tooltip("Fases a partir de la segunda, de más vida a menos. La primera es lo de arriba.")]
        public List<FaseDeCombate> fases = new List<FaseDeCombate>();

        [Header("Sin efecto todavía")]
        [Tooltip("Afinidades de elemento (etapa posterior).")]
        public List<AfinidadDeCombate> afinidades = new List<AfinidadDeCombate>();
    }
}
