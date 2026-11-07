using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Los momentos de un viaje en una atracción, en orden.</summary>
public enum MomentoDelViaje
{
    [InspectorName("En la cola (antes de subir)")] EnLaCola,
    [InspectorName("Arrancando")] Arrancando,
    [InspectorName("Lo más alto / lo más fuerte")] LoMasAlto,
    [InspectorName("Bajando")] Bajando,
    [InspectorName("Al bajar (ya en el suelo)")] AlBajar,
}

/// <summary>Uno del grupo, o nadie.</summary>
public enum QuienDelGrupo { Nadie, Will, Estela, Liam }

/// <summary>
/// Las escenitas de una atracción montable (<see cref="AtraccionMontable"/>): lo que dicen y hacen los
/// del grupo en cada momento del viaje, quién no sube, quién se marea. Cada viaje elige una al azar
/// según su peso, sin repetir hasta haber visto todas las que valen para ese viaje. Son datos: se
/// retocan en el asset. Ver INC-896.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Atracciones/Repertorio de atracción", fileName = "Repertorio")]
public class RepertorioDeAtraccion : ScriptableObject
{
    [Tooltip("Identificador para contar los viajes (lo comparten las atracciones con el mismo repertorio).")]
    public string id = "atraccion";
    [Tooltip("Escenitas posibles.")]
    public List<Escenita> escenitas = new();

    /// <summary>Una escenita de un viaje.</summary>
    [Serializable]
    public sealed class Escenita
    {
        [Tooltip("Nombre para reconocerla (no se enseña).")]
        public string nombre = "Escenita";
        [Tooltip("Probabilidad relativa frente a las demás que valen para este viaje.")]
        [Min(0f)] public float peso = 1f;
        [Tooltip("Primer viaje en el que puede salir (1 = el primero).")]
        [Min(1)] public int desdeElViaje = 1;
        [Tooltip("Último viaje en el que puede salir (0 = sin límite).")]
        [Min(0)] public int hastaElViaje = 0;
        [Tooltip("Quien se queda abajo esperando (no sube).")]
        public QuienDelGrupo noSube = QuienDelGrupo.Nadie;
        [Tooltip("Quien se marea al bajar.")]
        public QuienDelGrupo seMarea = QuienDelGrupo.Nadie;
        [Tooltip("Segundos que dura el mareo.")]
        [Min(0f)] public float segundosDeMareo = 5f;
        [Tooltip("Lo que pasa, en orden dentro de cada momento.")]
        public List<Golpe> golpes = new();

        /// <summary>
        /// Si vale para el viaje número <paramref name="viaje"/> con este grupo. Quien lleva el mando
        /// (<paramref name="alMando"/>) siempre sube.
        /// </summary>
        public bool ValePara(int viaje, Func<QuienDelGrupo, bool> estaEnElGrupo, QuienDelGrupo alMando)
        {
            if (peso <= 0f || viaje < desdeElViaje || (hastaElViaje > 0 && viaje > hastaElViaje)) return false;
            if (noSube != QuienDelGrupo.Nadie && (noSube == alMando || !estaEnElGrupo(noSube))) return false;
            if (seMarea != QuienDelGrupo.Nadie && !estaEnElGrupo(seMarea)) return false;
            foreach (var g in golpes)
                if (g.quien != QuienDelGrupo.Nadie && !estaEnElGrupo(g.quien)) return false;
            return true;
        }
    }

    /// <summary>Una frase y/o un gesto de uno del grupo.</summary>
    [Serializable]
    public sealed class Golpe
    {
        public MomentoDelViaje momento = MomentoDelViaje.LoMasAlto;
        [Tooltip("Quién lo hace.")]
        public QuienDelGrupo quien = QuienDelGrupo.Will;
        [Tooltip("Clave de la frase (vacía = sin frase, solo gesto).")]
        public string clave;
        [Tooltip("Texto si la clave no existe.")]
        [TextArea] public string texto;
        [Tooltip("Estado del Animator que hace mientras (Cheer01, Laugh01, Fear01...). Vacío = ninguno.")]
        public string gesto;
        [Tooltip("Cara que pone.")]
        public NPCEmotion emocion = NPCEmotion.None;
        [Tooltip("Segundos de espera antes de este golpe.")]
        [Min(0f)] public float espera = 0.3f;
        [Tooltip("No espera a que acabe su frase para el siguiente golpe (hablan a la vez).")]
        public bool aLaVez;
    }
}

/// <summary>
/// Cuántos viajes lleva el grupo en cada repertorio y qué escenitas ya han salido, durante la sesión
/// (en el Sendero no se guarda nada de esto). Ver INC-896.
/// </summary>
public static class RegistroDeViajes
{
    private static readonly Dictionary<string, int> Viajes = new();
    private static readonly Dictionary<string, HashSet<int>> Vistas = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Viajes.Clear(); Vistas.Clear(); }
#endif

    /// <summary>Viajes ya hechos con ese repertorio.</summary>
    public static int Hechos(string id) => id != null && Viajes.TryGetValue(id, out int n) ? n : 0;

    /// <summary>
    /// Elige la escenita del próximo viaje (el número <see cref="Hechos"/> + 1) y lo apunta. Null si
    /// no hay ninguna que valga.
    /// </summary>
    public static RepertorioDeAtraccion.Escenita Elegir(RepertorioDeAtraccion repertorio, Func<QuienDelGrupo, bool> estaEnElGrupo, QuienDelGrupo alMando)
    {
        if (repertorio == null || repertorio.escenitas == null || repertorio.escenitas.Count == 0) return null;
        string id = string.IsNullOrEmpty(repertorio.id) ? repertorio.name : repertorio.id;
        int viaje = Hechos(id) + 1;
        Viajes[id] = viaje;
        if (!Vistas.TryGetValue(id, out var vistas)) Vistas[id] = vistas = new HashSet<int>();

        int elegida = Sortear(repertorio, viaje, estaEnElGrupo, alMando, vistas);
        if (elegida < 0 && vistas.Count > 0)
        {
            vistas.Clear();
            elegida = Sortear(repertorio, viaje, estaEnElGrupo, alMando, vistas);
        }
        if (elegida < 0) return null;
        vistas.Add(elegida);
        return repertorio.escenitas[elegida];
    }

    private static int Sortear(RepertorioDeAtraccion r, int viaje, Func<QuienDelGrupo, bool> estaEnElGrupo, QuienDelGrupo alMando, HashSet<int> vistas)
    {
        float total = 0f;
        for (int i = 0; i < r.escenitas.Count; i++)
            if (!vistas.Contains(i) && r.escenitas[i] != null && r.escenitas[i].ValePara(viaje, estaEnElGrupo, alMando)) total += r.escenitas[i].peso;
        if (total <= 0f) return -1;
        float tirada = UnityEngine.Random.value * total;
        for (int i = 0; i < r.escenitas.Count; i++)
        {
            if (vistas.Contains(i) || r.escenitas[i] == null || !r.escenitas[i].ValePara(viaje, estaEnElGrupo, alMando)) continue;
            tirada -= r.escenitas[i].peso;
            if (tirada <= 0f) return i;
        }
        return -1;
    }
}
