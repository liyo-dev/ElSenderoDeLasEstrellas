using System.Collections.Generic;
using UnityEngine;

/// Qué ha pasado en el combate para que la guía diga algo.
public enum MomentoDeCombate
{
    Inicio,               // tras la presentación del jefe: todas, en orden
    JefeExpuesto,         // el jefe pasa a estar expuesto (el aro se enciende)
    GolpeValido,          // el jugador le hace daño
    GolpeMalDado,         // un golpe no ha servido (no le hace nada, se cura, rebota en la coraza...)
    JugadorHerido,        // el jefe le da al jugador
    JugadorPocaVida,      // la vida del jugador baja de 'umbralPocaVida'
    OrbesSueltos,         // el jefe suelta orbes
    CambioDeFase,         // empieza la fase 'fase'
    JefeCasiVencido,      // la vida del jefe baja de 'umbralJefeCasiVencido'
    SinAcertar,           // lleva 'segundosSinAcertar' sin hacerle daño
    JefeVaAPorElHablante, // el jefe cambia de objetivo y va a por quien guía
    JefeVaAPorElJugador,  // el jefe vuelve a por el jugador
    SePideRemate,         // el jefe ya solo cae con un golpe especial
}

[System.Serializable]
public class ComentarioDeCombate
{
    public MomentoDeCombate momento;
    [Tooltip("Solo para CambioDeFase: número de la fase que empieza (1 = segunda, 2 = tercera).")]
    public int fase;
    public string key;
    [Tooltip("Gesto de quien habla (catálogo de NPCSimpleAnimator). Vacío = sin gesto.")]
    public string anim;
    [Tooltip("Veces que puede decirse en un combate. 0 = sin límite.")]
    public int vecesMax = 1;
    [Tooltip("Segundos mínimos desde la última frase de este mismo momento.")]
    public float enfriamiento;
    [Tooltip("Si en estos segundos no se ha podido decir (estaba hablando), se descarta: un " +
             "«¡ahora!» dicho tarde confunde. 0 = espera lo que haga falta.")]
    public float caducidad;
    [Tooltip("Corta lo que esté diciendo (nunca la intervención inicial).")]
    public bool interrumpe;
    [Tooltip("Segundos en pantalla. 0 = 'duracionBocadillo' del guion. Nunca menos de lo que se tarda en leer.")]
    public float duracion;

    public ComentarioDeCombate() { }

    public ComentarioDeCombate(MomentoDeCombate momento, string key, string anim, int vecesMax = 1,
                               float enfriamiento = 0f, float caducidad = 0f, bool interrumpe = false,
                               float duracion = 0f, int fase = 0)
    {
        this.momento = momento; this.key = key; this.anim = anim; this.vecesMax = vecesMax;
        this.enfriamiento = enfriamiento; this.caducidad = caducidad; this.interrumpe = interrumpe;
        this.duracion = duracion; this.fase = fase;
    }
}

/// Lo que dice un personaje para guiar al jugador en un combate contra un jefe: quién habla y
/// qué dice en cada momento. Cada encuentro tiene el suyo (BattleEncounterSO.guion): Eldran en
/// el Demonio 1, Estela en el Gólem... Lo ejecuta GuiaDeCombate. Ver INC-489.
[CreateAssetMenu(fileName = "Guion_", menuName = "El Sendero/Batalla/Guion de combate")]
public class GuionDeCombate : ScriptableObject
{
    [Header("Quién habla")]
    [Tooltip("ID del NPC que habla (el del registro de NPCs, p. ej. 'NPC_Eldran'). Si no está " +
             "registrado con ese ID, se busca en el grupo por 'nombreHablante'.")]
    public string hablanteId;
    [Tooltip("Nombre que sale sobre el bocadillo.")]
    public string nombreHablante;
    [Tooltip("Mira al jugador mientras dura el combate. Para quien mira desde fuera (Eldran); " +
             "desmarcado para quien pelea (Estela), o se le rompería el movimiento.")]
    public bool mirarAlJugador;

    [Header("Ritmo")]
    [Tooltip("Segundos de respiro entre el final de la presentación del jefe y la primera frase.")]
    public float introDelay = 0.6f;
    [Tooltip("Si la presentación no termina en estos segundos, la intervención inicial se suelta igual.")]
    public float introFallbackSeconds = 12f;
    [Tooltip("Segundos en pantalla de una frase normal.")]
    public float duracionBocadillo = 2.8f;
    [Tooltip("Silencio mínimo entre dos frases seguidas.")]
    public float pausaEntreFrases = 0.6f;

    [Header("Umbrales")]
    [Range(0f, 1f)] public float umbralPocaVida = 0.35f;
    [Range(0f, 1f)] public float umbralJefeCasiVencido = 0.2f;
    public float segundosSinAcertar = 15f;

    [Header("Qué dice y cuándo")]
    public List<ComentarioDeCombate> comentarios = new List<ComentarioDeCombate>();
}
