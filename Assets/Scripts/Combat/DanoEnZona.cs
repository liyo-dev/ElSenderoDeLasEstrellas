using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// Daño de área a los personajes del grupo (GDD § 6.1: cada uno tiene su vida, igual al mando que
/// con IA). Es el camino único para los ataques de zona de los enemigos (ZonaDanina,
/// PozoGravitatorio, BombaOscura, SueloImpisable, la lluvia de rocas de EnemyProjectile): cada pieza
/// decide su forma (círculo en el suelo, esfera, cerca del suelo…) y aquí se resuelve a quién
/// alcanza y cómo se le hace el daño.
///
/// Recorre los tres personajes con <see cref="ControlDelJugador"/> en lugar de preguntar a la física:
/// son tres cuerpos, no hace falta reservar nada, y los compañeros cuentan aunque su colisionador
/// esté en otra capa. El que va al mando recibe el golpe por PlayerHealthSystem (pasa por sus reglas
/// de daño: la defensa de la B, el modo cinemática, la invulnerabilidad tras un golpe); los que lleva
/// la IA, por su <see cref="CuerpoDelGrupo"/> (su Damageable y sus reglas). Los caídos no cuentan.
/// Ver INC-717.
public static class DanoEnZona
{
    /// Altura de un cuerpo desde los pies, para medir la distancia a una explosión en el aire.
    public const float AlturaDelCuerpo = 1.8f;

    /// Un personaje del grupo que puede recibir daño ahora mismo.
    public readonly struct Cuerpo
    {
        public readonly Slot Personaje;
        public readonly Transform Transform;
        /// La vida del jugador, si este personaje es el que va al mando.
        public readonly PlayerHealthSystem SaludDelJugador;
        /// El cuerpo con IA, si lo lleva la IA.
        public readonly CuerpoDelGrupo Companero;

        public bool EsElJugador => SaludDelJugador != null;
        public Vector3 Posicion => Transform.position;

        public Cuerpo(Slot personaje, Transform transform, PlayerHealthSystem saludDelJugador, CuerpoDelGrupo companero)
        {
            Personaje = personaje;
            Transform = transform;
            SaludDelJugador = saludDelJugador;
            Companero = companero;
        }
    }

    private static readonly Cuerpo[] s_cuerpos = new Cuerpo[ControlDelJugador.Personajes];

    // Caché de componentes por cuerpo: el NPC de cada personaje y el cuerpo del jugador cambian
    // poco, y algunas piezas preguntan en cada frame.
    private static readonly GameObject[] s_npcVisto = new GameObject[ControlDelJugador.Personajes];
    private static readonly CuerpoDelGrupo[] s_cuerpoVisto = new CuerpoDelGrupo[ControlDelJugador.Personajes];
    private static GameObject s_jugadorVisto;
    private static PlayerHealthSystem s_saludVista;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        System.Array.Clear(s_cuerpos, 0, s_cuerpos.Length);
        System.Array.Clear(s_npcVisto, 0, s_npcVisto.Length);
        System.Array.Clear(s_cuerpoVisto, 0, s_cuerpoVisto.Length);
        s_jugadorVisto = null;
        s_saludVista = null;
    }
#endif

    /// Los personajes del grupo que pueden recibir daño ahora (en el grupo, en escena y en pie).
    /// Devuelve cuántos hay; la lista es compartida y vale hasta la siguiente llamada.
    public static int CuerposDelGrupo(out Cuerpo[] cuerpos)
    {
        cuerpos = s_cuerpos;
        int n = 0;
        Slot activo = ControlDelJugador.Activo;
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var p = (Slot)i;
            if (!ControlDelJugador.EnElGrupo(p)) continue;
            var go = ControlDelJugador.CuerpoDe(p);
            if (go == null || !go.activeInHierarchy) continue;

            if (p == activo)
            {
                var salud = SaludDe(go);
                if (salud == null || !salud.IsAlive) continue;
                s_cuerpos[n++] = new Cuerpo(p, go.transform, salud, null);
            }
            else
            {
                var cuerpo = CuerpoDelGrupoDe(i, go);
                if (cuerpo == null || !cuerpo.PuedeRecibirDano) continue;
                s_cuerpos[n++] = new Cuerpo(p, go.transform, null, cuerpo);
            }
        }
        return n;
    }

    /// Hace daño a un personaje del grupo. True si el golpe ha entrado (sus reglas no lo han anulado).
    public static bool Danar(in Cuerpo cuerpo, float dano, GameObject instigador = null, bool ignorarInvulnerabilidad = false)
    {
        if (dano <= 0f) return false;
        if (cuerpo.SaludDelJugador != null)
            return cuerpo.SaludDelJugador.TakeDamage(dano, ignorarInvulnerabilidad);

        var c = cuerpo.Companero;
        if (c == null || c.Vida == null) return false;
        float antes = c.Vida.Current;
        c.RecibirGolpe(dano, instigador);
        return c.Vida.Current < antes;
    }

    /// Aparta a un personaje del grupo. Solo el que va al mando tiene empuje
    /// (PlayerHealthSystem.Empujar); a los que lleva la IA los mueve su navegación.
    public static void Empujar(in Cuerpo cuerpo, Vector3 direccion, float fuerza)
    {
        if (fuerza <= 0f || cuerpo.SaludDelJugador == null) return;
        cuerpo.SaludDelJugador.Empujar(direccion, fuerza);
    }

    /// El cuerpo está en un círculo del suelo: dentro del radio en horizontal y entre 'bajo' metros
    /// por debajo y 'alto' metros por encima del centro.
    public static bool EnCilindro(Vector3 punto, Vector3 centro, float radio, float alto, float bajo)
    {
        Vector3 d = punto - centro;
        if (d.y > alto || d.y < -bajo) return false;
        d.y = 0f;
        return d.sqrMagnitude <= radio * radio;
    }

    /// El cuerpo (de los pies a la cabeza) toca una esfera.
    public static bool EnEsfera(Vector3 pies, Vector3 centro, float radio)
    {
        Vector3 cercano = pies;
        cercano.y = Mathf.Clamp(centro.y, pies.y, pies.y + AlturaDelCuerpo);
        return (cercano - centro).sqrMagnitude <= radio * radio;
    }

    /// Daña a todos los del grupo que estén en un círculo del suelo y aparta al que va al mando
    /// hacia fuera. Devuelve a cuántos ha alcanzado el golpe.
    public static int GolpearEnCilindro(Vector3 centro, float radio, float alto, float bajo, float dano,
        float empuje = 0f, GameObject instigador = null, bool ignorarInvulnerabilidad = false)
    {
        int alcanzados = 0;
        int n = CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < n; i++)
        {
            var c = cuerpos[i];
            if (!EnCilindro(c.Posicion, centro, radio, alto, bajo)) continue;
            if (!Danar(c, dano, instigador, ignorarInvulnerabilidad)) continue;
            alcanzados++;
            if (empuje > 0f) Empujar(c, HaciaFuera(c, centro, 0f), empuje);
        }
        return alcanzados;
    }

    /// Daña a todos los del grupo que toquen una esfera y aparta al que va al mando hacia fuera,
    /// con algo de elevación (0 = horizontal). Devuelve a cuántos ha alcanzado el golpe.
    public static int GolpearEnEsfera(Vector3 centro, float radio, float dano, float empuje = 0f,
        float elevacionDelEmpuje = 0f, GameObject instigador = null, bool ignorarInvulnerabilidad = false)
    {
        int alcanzados = 0;
        int n = CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < n; i++)
        {
            var c = cuerpos[i];
            if (!EnEsfera(c.Posicion, centro, radio)) continue;
            if (!Danar(c, dano, instigador, ignorarInvulnerabilidad)) continue;
            alcanzados++;
            if (empuje > 0f) Empujar(c, HaciaFuera(c, centro, elevacionDelEmpuje), empuje);
        }
        return alcanzados;
    }

    /// Hay alguien del grupo dentro de una esfera (para lo que reacciona a que se acerquen).
    public static bool AlguienEnEsfera(Vector3 centro, float radio)
    {
        int n = CuerposDelGrupo(out var cuerpos);
        for (int i = 0; i < n; i++)
            if (EnEsfera(cuerpos[i].Posicion, centro, radio)) return true;
        return false;
    }

    private static Vector3 HaciaFuera(in Cuerpo c, Vector3 centro, float elevacion)
    {
        Vector3 fuera = c.Posicion - centro;
        fuera.y = 0f;
        fuera = fuera.sqrMagnitude > 0.01f ? fuera.normalized : -c.Transform.forward;
        fuera.y = elevacion;
        return fuera.normalized;
    }

    private static PlayerHealthSystem SaludDe(GameObject jugador)
    {
        if (jugador != s_jugadorVisto || s_saludVista == null)
        {
            s_jugadorVisto = jugador;
            s_saludVista = jugador.GetComponent<PlayerHealthSystem>();
        }
        return s_saludVista;
    }

    private static CuerpoDelGrupo CuerpoDelGrupoDe(int i, GameObject npc)
    {
        if (npc != s_npcVisto[i] || s_cuerpoVisto[i] == null)
        {
            s_npcVisto[i] = npc;
            s_cuerpoVisto[i] = npc.GetComponent<CuerpoDelGrupo>();
        }
        return s_cuerpoVisto[i];
    }
}
