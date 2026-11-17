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
    }

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
    }

    /// Un elemento del plano sobre el que se puede anotar (un personaje, la cámara, una marca).
    [Serializable]
    public class CinemaElemento
    {
        public string id;
        public string etiqueta;
    }

    /// Un trazo libre sobre el fotograma, en coordenadas de la imagen (0..1).
    [Serializable]
    public class CinemaTrazo
    {
        public List<Vector2> puntos = new();
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
        /// cambio del guion) y rehornea. Devuelve el resumen y la huella de las aplicadas.
        CinemaAplicado AplicarRevisiones(string id, List<CinemaNota> notas);

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
