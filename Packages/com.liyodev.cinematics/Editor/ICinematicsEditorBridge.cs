using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// Una cinemática tal como la enseña la lista del Director: lo justo para elegirla sin abrirla.
    [Serializable]
    public class CinemaEntry
    {
        public string id;
        public string nombre;
        public string escena;
        [Tooltip("Asset del guion horneado (vacío si el guion aún no se ha horneado).")]
        public string rutaHorneado;
        [Tooltip("El .guion.txt del que sale (vacío si no se encuentra).")]
        public string rutaGuion;
        public float duracion;
        public int planos;
        public int frases;
        public int errores;
        public int avisos;
        [Tooltip("«Al día», «Texto cambiado» o «Sin hornear».")]
        public string estado;
        [Tooltip("Correcciones que no se pueden aplicar solas y esperan a una sesión de IA.")]
        public int encargos;
    }

    /// Un plano del horneado, con su foto si la hay.
    [Serializable]
    public class CinemaShot
    {
        public float t0;
        public float t1;
        public string descripcion;
        [Tooltip("Ruta absoluta del fotograma, o vacío.")]
        public string imagen;
        [Tooltip("Fotogramas del animatic, en orden (el primero es el póster del plano).")]
        public List<string> fotogramas = new();
        [Tooltip("Lo que se puede anotar en este plano: cada personaje que sale o habla, y la cámara.")]
        public List<CinemaElemento> elementos = new();
        [Tooltip("«storyboard» (con postproceso) o «previz».")]
        public string origen;
    }

    /// Una frase del guion, con su segundo y quién la dice.
    [Serializable]
    public class CinemaLine
    {
        public float t0;
        public float t1;
        public string quien;
        public string texto;
        public string clave;
        [Tooltip("El elemento al que se puede anotar esta frase (el actor o la marca de la voz).")]
        public string elemento;
        [Tooltip("Línea del .guion.txt que la pide (0 = horneado viejo).")]
        public int linea;
        [Tooltip("Voz que llega sin que se vea a quien habla (se enseña con su nombre).")]
        public bool deFueraDeEscena;
    }

    /// De qué es una pista del timeline.
    public enum CinemaTrackKind
    {
        Camara = 0,
        Voz = 1,
        Movimiento = 2,
        Gesto = 3,
        Musica = 4,
        Sonido = 5,
        Vfx = 6,
        Prop = 7,
        Marca = 8,
        Escena = 9,
    }

    /// Un trozo puesto en una pista: un plano, una frase, un camino, un efecto.
    [Serializable]
    public class CinemaClip
    {
        public float t0;
        public float t1;
        public string etiqueta;
        [Tooltip("Línea del .guion.txt que lo pide (0 = horneado viejo).")]
        public int linea;
        [Tooltip("El elemento del plano al que se puede anotar, si lo hay (un actor, una marca).")]
        public string elemento;
        [Tooltip("Sonido que toca en ese instante (música o efecto), para el animatic.")]
        public AudioClip clip;
        [Range(0f, 1f)] public float volumen = 1f;
        [Tooltip("Qué es este trozo en la vista de pistas: acción, reacción, reacción automática o vida de fondo.")]
        public CinemaClipClase clase;
    }

    /// Para la vista «Pistas por personaje»: lo escrito (acción o reacción) y lo que pone solo el horno.
    public enum CinemaClipClase { Accion = 0, Reaccion = 1, Automatica = 2, Vida = 3 }

    /// Una fila del timeline: la cámara, cada personaje, la música, los efectos.
    [Serializable]
    public class CinemaTrack
    {
        public string nombre;
        public CinemaTrackKind tipo;
        public List<CinemaClip> clips = new();
    }

    /// La cinemática abierta: planos, frases y lo que dijo el último horneado.
    [Serializable]
    public class CinemaDetail
    {
        public CinemaEntry entrada;
        public List<CinemaShot> planos = new();
        public List<CinemaLine> lineas = new();
        [Tooltip("Filas del timeline: cámara, cada personaje, música, efectos, marcas.")]
        public List<CinemaTrack> pistas = new();
        public List<string> errores = new();
        public List<string> avisos = new();
        [Tooltip("Si las frases salen en subtítulo (bandas de cine) y no en bocadillo.")]
        public bool subtitulos;
        public string carpetaDelStoryboard;
    }

    /// Una anotación de la página del storyboard tal como llega a Unity.
    [Serializable]
    public class CinemaNota
    {
        public string id;
        public string panel;
        public string elemento;
        public string tipo;
        public string texto;
        [Tooltip("Segundo clavado en el timeline (-1 = nota de todo el plano).")]
        public float t = -1f;
        public float[] p1;
        public float[] p2;
        public CinemaCaja caja;
        public List<CinemaPunto> puntos;

        /// El texto que se lee en el encargo: el escrito o, si es un dibujo, lo que dibuja.
        public string TextoEfectivo()
        {
            if (!string.IsNullOrEmpty(texto)) return texto;

            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (tipo == "flecha" && p1 != null && p1.Length >= 2 && p2 != null && p2.Length >= 2)
                return string.Format(ci, "flecha de ({0:0.###}, {1:0.###}) a ({2:0.###}, {3:0.###})",
                    p1[0], p1[1], p2[0], p2[1]);
            if (tipo == "caja" && caja != null && (caja.w > 0f || caja.h > 0f))
                return string.Format(ci, "caja en ({0:0.###}, {1:0.###}) de {2:0.###} x {3:0.###}",
                    caja.x, caja.y, caja.w, caja.h);
            if (tipo == "trazo" && puntos != null && puntos.Count > 0)
            {
                CinemaPunto a = puntos[0];
                CinemaPunto b = puntos[puntos.Count - 1];
                return string.Format(ci, "trazo de {0} puntos de ({1:0.###}, {2:0.###}) a ({3:0.###}, {4:0.###})",
                    puntos.Count, a.x, a.y, b.x, b.y);
            }
            return string.IsNullOrEmpty(tipo) ? string.Empty : $"({tipo})";
        }

        /// La huella de una nota: plano + elemento + lo que dice. Es la misma que guarda el libro de
        /// aplicadas, para que una nota no se aplique ni se encargue dos veces.
        public string Clave() => CalcularClave(panel, elemento, string.IsNullOrEmpty(texto) ? TextoEfectivo() : texto);

        public static string CalcularClave(string panel, string elemento, string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            return Slug(panel + "-" + elemento + "-" + texto);
        }

        public static string Slug(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return string.Empty;
            var limpio = new System.Text.StringBuilder(texto.Length);
            foreach (char c in texto.ToLowerInvariant())
                limpio.Append(char.IsLetterOrDigit(c) ? c : '-');
            return System.Text.RegularExpressions.Regex.Replace(limpio.ToString(), "-+", "-").Trim('-');
        }
    }

    /// Un punto de un trazo sobre el fotograma, en coordenadas de la imagen (0..1).
    [Serializable]
    public class CinemaPunto
    {
        public float x;
        public float y;
    }

    /// Caja dibujada sobre un fotograma, en coordenadas de la imagen (0..1).
    [Serializable]
    public class CinemaCaja
    {
        public float x;
        public float y;
        public float w;
        public float h;
    }

    /// Una corrección que no se puede aplicar sola: espera a una sesión de IA, que la hace sobre el
    /// guion (o sobre lo que pida) y la cierra. Lleva todo el contexto del plano para no tener que
    /// volver a buscarlo.
    [Serializable]
    public class CinemaEncargo
    {
        public string clave;
        public string panel;
        public string elemento;
        public string etiquetaElemento;
        public string tipo;
        public string texto;
        [Tooltip("Segundo clavado en el timeline (-1 = todo el plano).")]
        public float t = -1f;
        public float t0;
        public float t1;
        public int linea;
        public string ordenGuion;
        public string rutaGuion;
        public string imagen;
        public string fecha;
    }

    /// La cola de encargos de una cinemática, tal como se guarda en el proyecto.
    [Serializable]
    public class CinemaColaDeEncargos
    {
        public string guion;
        public string rutaGuion;
        public string escena;
        public string actualizado;
        public List<CinemaEncargo> encargos = new();
    }

    /// Un elemento del plano sobre el que se puede anotar (un personaje, la cámara, una marca).
    [Serializable]
    public class CinemaElemento
    {
        public string id;
        public string etiqueta;
    }

    /// Una marca sobre la imagen del plano, en coordenadas de la imagen (0..1, el (0, 0) arriba a la
    /// izquierda): un punto señalado, una dirección (de dónde a dónde) o un dibujo a mano.
    [Serializable]
    public class CinemaTrazo
    {
        public const string Trazo = "trazo";
        public const string Punto = "punto";
        public const string Flecha = "flecha";

        [Tooltip("«trazo» (dibujo a mano), «punto» (señalado) o «flecha» (de dónde a dónde).")]
        public string tipo = Trazo;
        [Tooltip("Segundo en el que se hizo la marca (-1 = todo el plano).")]
        public float t = -1f;
        public List<Vector2> puntos = new();

        /// La marca en una frase para la IA.
        public string Describir()
        {
            if (puntos == null || puntos.Count == 0) return string.Empty;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            Vector2 a = puntos[0];
            Vector2 b = puntos[puntos.Count - 1];
            string cuando = t >= 0f ? string.Format(ci, " en t={0:0.0}", t) : string.Empty;
            switch (tipo)
            {
                case Punto:
                    return string.Format(ci, "señala ({0:0.###}, {1:0.###}){2}", a.x, a.y, cuando);
                case Flecha:
                    return string.Format(ci, "dirección de ({0:0.###}, {1:0.###}) a ({2:0.###}, {3:0.###}){4}",
                        a.x, a.y, b.x, b.y, cuando);
                default:
                    return string.Format(ci, "dibujo de {0} puntos de ({1:0.###}, {2:0.###}) a ({3:0.###}, {4:0.###}){5}",
                        puntos.Count, a.x, a.y, b.x, b.y, cuando);
            }
        }
    }

    /// Una nota de la pizarra: un plano, un elemento, lo que se dice y lo que se dibuja encima.
    /// Si `t` es mayor o igual que 0, la nota va clavada a ese segundo (una nota del timeline).
    [Serializable]
    public class CinemaNotaPizarra
    {
        public string panel;
        public string elemento;
        public string texto;
        public float t = -1f;
        public List<CinemaTrazo> trazos = new();

        /// La nota entera para la IA: el texto y cada marca (señalado, dirección, dibujo) con su segundo.
        public string Describir()
        {
            var partes = new List<string>();
            if (!string.IsNullOrEmpty(texto)) partes.Add(texto);
            if (trazos != null)
                foreach (var trazo in trazos)
                {
                    string marca = trazo?.Describir();
                    if (!string.IsNullOrEmpty(marca)) partes.Add(marca);
                }
            return string.Join(" · ", partes);
        }

        /// Lo que dice la nota: el texto o, si es solo un dibujo, lo que dibuja. Así una nota de la
        /// pizarra se puede encargar y cerrar con la misma huella que las de la página.
        public string TextoEfectivo()
        {
            if (!string.IsNullOrEmpty(texto)) return texto;
            if (trazos == null || trazos.Count == 0) return string.Empty;

            var puntos = trazos[0]?.puntos;
            if (puntos == null || puntos.Count == 0) return $"({trazos.Count} trazo(s))";

            Vector2 a = puntos[0];
            Vector2 b = puntos[puntos.Count - 1];
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            return string.Format(ci, "trazo de {0} puntos de ({1:0.###}, {2:0.###}) a ({3:0.###}, {4:0.###})",
                puntos.Count, a.x, a.y, b.x, b.y);
        }
    }

    /// La pizarra de un guion, guardada en el proyecto (no en el navegador).
    [Serializable]
    public class CinemaPizarra
    {
        public string guion;
        public List<CinemaNotaPizarra> notas = new();
    }

    /// Lo que quedó de un lote de correcciones: el resumen para enseñar y la huella de las notas
    /// que de verdad se aplicaron (para borrarlas de la pizarra: que desaparezcan es el acuse).
    [Serializable]
    public class CinemaAplicado
    {
        public string resumen;
        public List<string> claves = new();
        [Tooltip("Correcciones que no se aplicaron solas y quedaron en la cola de la IA.")]
        public int pendientes;
        [Tooltip("La vista legible de esa cola, si quedó algo pendiente.")]
        public string rutaEncargos;
    }

    /// Lo que quedó de aplicar un lote de operaciones al guion desde la IA.
    [Serializable]
    public class CinemaEdicionResultado
    {
        public bool aplicado;
        public string resumen;
        public List<string> hechos = new();
        public List<string> diff = new();
        public List<string> avisos = new();
        public List<string> errores = new();
    }

    /// Lo que el Director necesita saber del proyecto sin depender de ningún ensamblado suyo:
    /// quién es cada cinemática, sus fotos, sus textos, sus voces y qué hacer con ella.
    public interface ICinematicsEditorBridge
    {
        /// Todas las cinemáticas del proyecto: las horneadas y, si se encuentran, las que solo son texto.
        List<CinemaEntry> Listar();

        /// La cinemática abierta, con planos, frases, fotos y el parte del último horneado.
        CinemaDetail Leer(string id);

        /// La voz de una frase (null si no la tiene).
        AudioClip VozDe(string clave);

        void Hornear(string id, bool storyboard);
        void AbrirTaller(string id);
        void AbrirGuion(string id);
        void GenerarPaginaAnotable(string id);
        void AbrirStoryboard(string id);
        void Avisar(string titulo, string mensaje);

        /// Aplica un lote de anotaciones de la página del storyboard (las que sabe traducir a un
        /// cambio del guion) y rehornea. Devuelve el resumen y la huella de las aplicadas. Lo que no
        /// sabe aplicar solo queda en la cola de encargos de la IA (ver <see cref="EncargosPendientes"/>).
        CinemaAplicado AplicarRevisiones(string id, List<CinemaNota> notas);

        /// El manual del lenguaje de guiones, para que la IA sepa qué puede escribir.
        string ManualDeGuion();

        /// El estado de una cinemática para el asistente: ficha, encargos, informe y guion numerado.
        /// `plano` es el índice que tiene abierto el Director (-1 = ninguno).
        string EstadoParaAsistente(string id, int plano = -1, float segundo = -1f);

        /// Aplica un lote de operaciones al .guion.txt: valida, hornea en memoria, calcula el diff y
        /// solo entonces escribe y rehornea. Cierra los encargos que pida el lote.
        CinemaEdicionResultado AplicarEdicionDeGuion(string id, string jsonOps);

        /// Cuántas correcciones de esa cinemática esperan a que una sesión de IA las haga.
        int EncargosPendientes(string id);

        /// Abre (o enseña en el explorador) la vista legible de esa cola de encargos.
        void AbrirEncargos(string id);

        /// El texto de la cola de encargos, para leerla desde fuera (p. ej. el buzón de órdenes).
        string TextoDeEncargos(string id);

        /// Abre la escena de la cinemática y la rueda en PlayMode de verdad, desde `desde` segundos.
        /// Devuelve false (y avisa) si no hay secuencia montada para ese guion.
        bool RodarEnPlay(string id, float desde);

        /// La pizarra del guion (notas y trazos), guardada en el proyecto.
        CinemaPizarra LeerPizarra(string id);
        void GuardarPizarra(string id, CinemaPizarra pizarra);

        /// Las escenas del proyecto, para elegir dónde pasa una cinemática nueva.
        string[] Escenas();

        /// Escribe el esqueleto de un guion nuevo. Devuelve null si fue bien, o el motivo.
        string CrearCinematica(string nombre, string escena);
    }

    /// Registro del puente, igual que NarrativeServices: el proyecto se apunta solo y el Director no
    /// sabe quién está al otro lado. Sin puente, la ventana lo dice y no hace nada.
    public static class CinematicsEditorServices
    {
        private static ICinematicsEditorBridge _bridge;

        public static bool HayPuente => _bridge != null;

        public static ICinematicsEditorBridge Bridge
        {
            get
            {
                if (_bridge == null) Descubrir();
                return _bridge;
            }
        }

        public static void Register(ICinematicsEditorBridge puente) => _bridge = puente;

        /// <summary>Algo cambió fuera de la mesa (se aplicó una corrección, se horneó): que se repinte.</summary>
        public static event Action Cambiaron;

        public static void AvisarCambio() => Cambiaron?.Invoke();

        public static void Unregister(ICinematicsEditorBridge puente)
        {
            if (_bridge == puente) _bridge = null;
        }

        /// En un proyecto sin enchufe no hay que configurar nada: se busca la implementación sola.
        private static void Descubrir()
        {
            foreach (var tipo in TypeCache.GetTypesDerivedFrom<ICinematicsEditorBridge>())
            {
                if (tipo.IsAbstract || tipo.IsInterface) continue;
                if (Activator.CreateInstance(tipo) is ICinematicsEditorBridge instancia)
                {
                    _bridge = instancia;
                    return;
                }
            }
        }
    }
}
