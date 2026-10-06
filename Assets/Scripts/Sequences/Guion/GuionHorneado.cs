using System;
using System.Collections.Generic;
using UnityEngine;

/// Un guion ya horneado: todo lo que pasa en la secuencia, con su segundo exacto.
///
/// Lo escribe el horneado del Editor (HorneadorDeGuion) a partir del guion en texto, y lo
/// reproduce GuionBeat sin decidir nada en directo: los caminos, los tiempos de cada frase y las
/// cámaras ya están calculados y comprobados. Por eso no hay atascos, ni pausas sueltas, ni planos
/// que tiemblen: en Play solo se lee esta tabla.
///
/// No se edita a mano. Se cambia el guion de texto y se vuelve a hornear.
[CreateAssetMenu(fileName = "GuionHorneado", menuName = "Sendero/Secuencias/Guion horneado")]
public class GuionHorneado : ScriptableObject
{
    /// Pose de cámara del plano que toca en el segundo t, con los actores donde dice el guion
    /// (aunque todavía no se hayan colocado). La usa el SequencePlayer en el corte de entrada para
    /// que, al abrirse el telón, ya se vea el primer plano y no otro encuadre durante un fotograma.
    public bool PoseDeCamara(float t, out Vector3 posicion, out Quaternion rotacion, out float fov)
    {
        posicion = default; rotacion = Quaternion.identity; fov = 35f;
        if (planos == null || planos.Count == 0) return false;
        PlanoHorneado p = planos[0];
        foreach (var q in planos) if (q.t0 <= t) p = q;
        if (p.posiciones == null || p.posiciones.Count == 0) return false;
        posicion = p.PosicionEn(t);
        Vector3 foco = Vector3.zero; float peso = 0f;
        for (int i = 0; i < p.sujetos.Count; i++)
        {
            var a = actores.Find(x => x.id == p.sujetos[i]);
            if (a == null || a.posiciones.Count == 0) continue;
            float w = i < p.pesos.Count ? p.pesos[i] : 1f;
            foco += Interpolar.Posicion(a.posiciones, t) * w; peso += w;
        }
        if (peso > 0f && p.extras != null)
            foreach (var x in p.extras) { foco += x - Vector3.up * p.alturaDelFoco; peso += 1f; }
        foco = peso > 0f ? foco / peso + Vector3.up * p.alturaDelFoco : p.focoFijo;
        Vector3 d = foco - posicion;
        if (d.sqrMagnitude < 1e-4f) return false;
        fov = p.lente;
        float tanV = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), tanH = tanV * 16f / 9f;
        rotacion = Quaternion.LookRotation(d, Vector3.up)
                 * Quaternion.Euler(-Mathf.Atan(2f * p.encuadre.y * tanV) * Mathf.Rad2Deg,
                                    -Mathf.Atan(2f * p.encuadre.x * tanH) * Mathf.Rad2Deg, 0f);
        return true;
    }

    [Tooltip("Nombre del guion (el de la cabecera GUION del texto).")]
    public string nombre;

    [Tooltip("Duración total en segundos.")]
    public float duracion;

    [Tooltip("Cómo salen las frases en pantalla.")]
    public PresentacionDeTexto presentacion = PresentacionDeTexto.Subtitulo;

    [Tooltip("Fecha del horneado y huella del texto, para saber si está al día.")]
    public string huella;

    public List<ActorHorneado> actores = new();
    public List<LineaHorneada> lineas = new();
    public List<PlanoHorneado> planos = new();
    public List<EfectoHorneado> efectos = new();
    public List<EtiquetaHorneada> etiquetas = new();

    [Tooltip("Los PUNTOS del guion, que durante la reproducción valen como marcas del escenario.")]
    public List<PuntoHorneado> puntos = new();

    [Tooltip("Perfil de ruido de cámara en mano (de los presets de Cinemachine).")]
    public Unity.Cinemachine.NoiseSettings ruidoDeMano;

    public ActorHorneado Actor(string id) => actores.Find(a => a.id == id);
}

[Serializable]
public class PuntoHorneado
{
    public string nombre;
    public Vector3 posicion;
}

/// Una etiqueta del hilo principal (@nombre) con su segundo. Solo informa.
[Serializable]
public class EtiquetaHorneada
{
    public string nombre;
    public float t;
}

/// Todo lo que hace un personaje durante la secuencia entera.
[Serializable]
public class ActorHorneado
{
    [Tooltip("Persistence ID del NPC (NPC_Archimago...) o 'Player'.")]
    public string id;

    [Tooltip("Alias del guion, solo para leer avisos.")]
    public string alias;

    [Tooltip("Color del nombre en los subtítulos. Alpha 0 = el de siempre.")]
    public Color colorDelNombre;

    [Tooltip("Clave de localización del nombre en los subtítulos.")]
    public string claveDelNombre;

    [Tooltip("Dónde está en cada momento. Entre dos claves se interpola en línea recta; un " +
             "tramo con la misma posición en las dos claves es estar quieto.")]
    public List<ClaveDePosicion> posiciones = new();

    [Tooltip("Hacia dónde mira cuando está quieto. Andando mira hacia donde anda.")]
    public List<ClaveDeMirada> miradas = new();

    [Tooltip("Órdenes de animación, en orden de tiempo.")]
    public List<OrdenDeAnimacion> animacion = new();

    [Tooltip("Cambios de cara, en orden de tiempo.")]
    public List<OrdenDeCara> caras = new();

    [Tooltip("Cuándo está visible: tramos [desde, hasta). Vacío = siempre.")]
    public List<TramoVisible> visible = new();

    [Tooltip("Tramos en que se desliza sin dar pasos (empujones, levitar): se mueve, pero sin " +
             "animación de andar y sin girarse hacia donde va.")]
    public List<TramoVisible> deslizando = new();
}

[Serializable]
public struct ClaveDePosicion
{
    public float t;
    public Vector3 p;
    public ClaveDePosicion(float t, Vector3 p) { this.t = t; this.p = p; }
}

public enum TipoDeMirada { Rumbo = 0, Punto = 1, Actor = 2 }

[Serializable]
public struct ClaveDeMirada
{
    public float t;
    public TipoDeMirada tipo;
    [Tooltip("Rumbo en grados (0 = +Z) si tipo es Rumbo.")]
    public float rumbo;
    public Vector3 punto;
    public string actor;
}

public enum TipoDeAnimacion
{
    /// Vuelve a su reposo (idle normal), soltando cualquier bucle.
    Reposo = 0,
    /// Entra en un estado y se queda en él (baile, rezar, trabajar...).
    Bucle = 1,
    /// Un gesto que se reproduce una vez y vuelve a lo que estuviera haciendo.
    Gesto = 2,
    /// Empieza a hablar (pose de conversación + boca), si está quieto.
    HablaEmpieza = 3,
    HablaTermina = 4,
}

[Serializable]
public struct OrdenDeAnimacion
{
    public float t;
    public TipoDeAnimacion tipo;
    public string estado;
    [Tooltip("Si es un bucle de pose que debe quedarse congelado en su último fotograma.")]
    public bool congelar;
}

[Serializable]
public struct OrdenDeCara
{
    public float t;
    public NPCEmotion emocion;
    [Tooltip("Segundos; 0 = hasta la siguiente orden.")]
    public float dura;
}

[Serializable]
public struct TramoVisible
{
    public float desde;
    public float hasta;
}

/// Una frase con voz. Empieza y acaba en segundos exactos: los de su audio.
[Serializable]
public class LineaHorneada
{
    public float t0;
    public float t1;
    public string actor;
    public string clave;
    [Tooltip("Gesto concreto para esta frase (opcional).")]
    public string gesto;
    [Tooltip("Hablar sin pose de conversación (andando, corriendo, peleando).")]
    public bool sinPose;
    [Tooltip("Presentación de esta frase: -1 hereda la del guion.")]
    public int presentacion = -1;
}

public enum TipoDePlano { General = 0, Conjunto = 1, Medio = 2, Primer = 3, Detalle = 4, Dos = 5, Hombro = 6, Sigue = 7, Fijo = 8 }

/// Un plano ya resuelto: dónde está la cámara en cada momento y a quién encuadra.
[Serializable]
public class PlanoHorneado
{
    public float t0;
    public float t1;
    public TipoDePlano tipo;
    [Tooltip("Descripción legible, para el parte de rodaje.")]
    public string descripcion;

    [Tooltip("Posición de la cámara en el tiempo. Una sola clave = cámara quieta.")]
    public List<ClaveDePosicion> posiciones = new();

    [Tooltip("Campo de visión vertical, en grados.")]
    public float lente = 35f;
    [Tooltip("Lente al final del plano (zoom lento). 0 = igual que al principio.")]
    public float lenteFinal;

    [Tooltip("Actores a los que se encuadra: el foco es su centro.")]
    public List<string> sujetos = new();
    [Tooltip("Peso de cada sujeto en el foco (mismo orden que sujetos).")]
    public List<float> pesos = new();
    [Tooltip("Puntos del escenario que también entran en el encuadre junto a los sujetos (el agujero negro sobre el Mago).")]
    public List<Vector3> extras = new();
    [Tooltip("Foco fijo cuando no hay sujetos (un punto del escenario).")]
    public Vector3 focoFijo;
    [Tooltip("Altura del foco sobre los pies de los sujetos.")]
    public float alturaDelFoco = 1.4f;

    [Tooltip("Dónde queda el foco en pantalla: (0,0) centro, ±0,5 borde.")]
    public Vector2 encuadre;
    [Tooltip("Suavidad de la panorámica que sigue al foco (segundos). 0 = clavada.")]
    public float suavidad = 0.6f;
    [Tooltip("Segundos de transición desde el plano anterior. 0 = corte.")]
    public float mezcla;
    [Tooltip("Cámara en mano: 0 = trípode, 1 = mano marcada.")]
    public float mano;

    public Vector3 PosicionEn(float t) => Interpolar.Posicion(posiciones, t);
}

/// Un beat de los de siempre (VFX, sonido, cut-in, hora del día...) lanzado en su segundo, sin
/// esperar a que termine.
[Serializable]
public class EfectoHorneado
{
    public float t;
    [SerializeReference] public SequenceBeat beat;
    [Tooltip("Línea del guion de texto, para los avisos.")]
    public int linea;
}

public static class Interpolar
{
    /// Índice de la última clave con t <= tiempo (o 0).
    public static int Indice(List<ClaveDePosicion> claves, float t)
    {
        int lo = 0, hi = claves.Count - 1;
        if (hi < 0) return -1;
        if (t <= claves[0].t) return 0;
        if (t >= claves[hi].t) return hi;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (claves[mid].t <= t) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    public static Vector3 Posicion(List<ClaveDePosicion> claves, float t)
    {
        if (claves == null || claves.Count == 0) return Vector3.zero;
        int i = Indice(claves, t);
        if (i >= claves.Count - 1) return claves[claves.Count - 1].p;
        var a = claves[i]; var b = claves[i + 1];
        float dt = b.t - a.t;
        if (dt <= 1e-5f) return b.p;
        return Vector3.Lerp(a.p, b.p, Mathf.Clamp01((t - a.t) / dt));
    }

    /// Velocidad (m/s, en horizontal) en el instante t.
    public static Vector3 Velocidad(List<ClaveDePosicion> claves, float t)
    {
        if (claves == null || claves.Count < 2) return Vector3.zero;
        int i = Indice(claves, t);
        if (i >= claves.Count - 1) return Vector3.zero;
        var a = claves[i]; var b = claves[i + 1];
        float dt = b.t - a.t;
        // Un salto de sitio en un instante (aparecer en otra marca) no es andar.
        if (dt <= 0.02f) return Vector3.zero;
        Vector3 v = (b.p - a.p) / dt;
        v.y = 0f;
        return v;
    }
}
