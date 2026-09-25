using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// El escenario de una secuencia: lo único que NO se puede escribir como texto.
///
/// Se pone en un GameObject de la escena, junto al SequencePlayer. Contiene las posiciones del
/// mundo — planos de cámara y marcas de suelo — cada una con un NOMBRE, y los beats de la
/// SequenceDefinition las referencian por ese nombre.
///
/// Esa es la frontera exacta del reparto de trabajo: colocar y nombrar estas cosas se hace en el
/// Editor, arrastrando; todo lo demás (qué pasa, en qué orden, quién reacciona) se describe por
/// escrito en el asset y no requiere abrir Unity.
[DisallowMultipleComponent]
[AddComponentMenu("Cinematics/Sequence Stage")]
public class SequenceStage : MonoBehaviour
{
    [Serializable]
    public struct NamedTransform
    {
        [Tooltip("Nombre con el que los beats se refieren a este punto. Sin espacios sobrantes; " +
                 "no distingue mayúsculas.")]
        public string name;

        public Transform target;
    }

    [Header("Planos de cámara")]
    [Tooltip("Los puntos de cámara de esta secuencia. Lo normal es que cada uno sea un GameObject " +
             "vacío con el componente CinematicShot (así se ve el encuadre en la Scene View). " +
             "Si se deja el campo 'name' vacío, se usa el 'label' del CinematicShot, y si tampoco " +
             "lo tiene, el nombre del GameObject.")]
    [SerializeField] private List<NamedTransform> _shots = new();

    [Serializable]
    public struct PropActor
    {
        [Tooltip("Nombre con el que los beats se refieren a este objeto, igual que al ID de un " +
                 "personaje (p. ej. 'PROP_Horno'). Conviene el prefijo PROP_ para que al leer el " +
                 "asset se vea de un vistazo que no es un personaje.")]
        public string id;

        [Tooltip("El objeto de la escena que la cámara va a encuadrar.")]
        public Transform target;

        [Tooltip("Altura, sobre el pivot del objeto, del punto al que apuntan los planos. Para un " +
                 "horno o una carreta, la altura a la que mirarías tú: medio metro, un metro. 0 = " +
                 "al pivot.")]
        public float eyeHeight;

        [Tooltip("Los que andan por la escena lo RODEAN en vez de atravesarlo. Para cosas que están " +
                 "en el suelo y en medio del paso — la carreta —, no para lo que vuela o lo que es " +
                 "un efecto. Lo usa WalkPathBeat.")]
        public bool esObstaculo;
    }

    [Header("Objetos que la cámara puede encuadrar")]
    [Tooltip("Objetos del decorado dados de alta como actores de la secuencia (el horno, la " +
             "carreta, el portón). A partir de ahí un ShotBeat puede usarlos como 'subjectId' o " +
             "'secondaryId' igual que a un personaje, y un FaceBeat puede hacer que alguien los " +
             "mire.\n\n" +
             "Existe porque sin esto la única forma de encuadrar algo del escenario era que un " +
             "SequenceModule lo registrara por código (ctx.RegisterActor), y un horno que ya está " +
             "colocado en la escena no necesita código ninguno. Lo registra el SequencePlayer al " +
             "empezar la secuencia y se va con ella.")]
    [SerializeField] private List<PropActor> _props = new();

    /// Los objetos del decorado dados de alta como actores. Los registra el SequencePlayer.
    public IReadOnlyList<PropActor> Props => _props;

    [Header("Marcas de posición")]
    [Tooltip("Puntos del suelo a los que un beat de movimiento puede mandar a un actor " +
             "(p. ej. 'punto de encuentro'). Solo hacen falta cuando el destino no es otro actor.")]
    [SerializeField] private List<NamedTransform> _marks = new();

    [Header("Cámara")]
    [Tooltip("El CinematicCameraDriver que ejecuta los cortes. Si se deja vacío, el SequencePlayer " +
             "usa el que tenga asignado en su propio Inspector (campo heredado de CinematicSequencerBase).")]
    [SerializeField] private CinematicCameraDriver _cameraDriver;

    public CinematicCameraDriver CameraDriver => _cameraDriver;

    [Header("Ajuste de los planos calculados")]
    [Tooltip("Aleja (>1) o acerca (<1) TODOS los planos calculados de esta secuencia, sin tocar su " +
             "composición: el encuadre es el mismo, solo que desde más lejos o más cerca.\n\n" +
             "Está aquí, y no en el código, porque cuánto de cerca se ve un personaje es una " +
             "decisión de gusto, no de geometría — y depende del arte del juego. Se puede mover " +
             "DURANTE el Play, con la secuencia corriendo, y el cambio se nota al instante en los " +
             "planos que siguen a alguien. Cuando encuentres el valor que te gusta, dilo y se pasa " +
             "a ser el de por defecto.\n\n" +
             "Para un plano suelto que necesite otra cosa, cada beat tiene su propio " +
             "'distanceScale', que se multiplica con este.")]
    [Range(0.4f, 3f)]
    [SerializeField] private float _distanceMultiplier = 1f;

    public float DistanceMultiplier => Mathf.Max(0.1f, _distanceMultiplier);

    [Tooltip("HERRAMIENTA DE AJUSTE. Con esto marcado, TODOS los planos calculados se recalculan " +
             "cada frame en vez de una sola vez al cortar. Es lo que hace que mover el deslizador " +
             "de arriba se vea al instante en cualquier plano, y no solo en los que siguen a " +
             "alguien.\n\n" +
             "Sin esto, un plano de corte seco ya está resuelto cuando mueves el deslizador, así " +
             "que no se entera del cambio — que es justo lo que pasaba el 16 sep ('he movido el " +
             "slider y no hace nada').\n\n" +
             "Quitarlo cuando termines de ajustar: recalcular cada frame gasta de más, y hace que " +
             "la cámara siga a los personajes en planos que deberían estar quietos.")]
    [FormerlySerializedAs("_previsualizarPlanosEnVivo")]
    [SerializeField] private bool _livePreviewShots = false;

    public bool LivePreview => _livePreviewShots;

    [Header("Mecánica propia de esta escena")]
    [Tooltip("Módulos con la mecánica de juego que solo existe en esta secuencia (un panic input, " +
             "un proyectil con reglas propias). Los beats de tipo 'Mecánica' los invocan por el " +
             "nombre de su rutina. La mayoría de secuencias no necesita ninguno: si una escena " +
             "solo habla, gesticula y corta de plano, esta lista se queda vacía.")]
    [SerializeField] private List<SequenceModule> _modules = new();

    public IReadOnlyList<SequenceModule> Modules => _modules;

    /// Añade módulos al escenario (los del prefab 'modulos' de una secuencia montada en vivo).
    public void AgregarModulos(IEnumerable<SequenceModule> modulos)
    {
        if (modulos == null) return;
        _modules ??= new List<SequenceModule>();
        foreach (var m in modulos)
            if (m != null && !_modules.Contains(m)) _modules.Add(m);
    }

    /// El módulo que sabe ejecutar esa rutina, o null (con aviso) si no hay ninguno.
    public SequenceModule GetModuleFor(string routine)
    {
        if (string.IsNullOrWhiteSpace(routine) || _modules == null) return null;

        foreach (var module in _modules)
            if (module != null && module.Handles(routine))
                return module;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        var disponibles = new List<string>();
        foreach (var module in _modules)
            if (module != null && module.Routines != null)
                disponibles.AddRange(module.Routines);

        Debug.LogWarning($"[SequenceStage:{name}] Ningún módulo sabe ejecutar la rutina '{routine}'. " +
            $"Rutinas disponibles: {(disponibles.Count > 0 ? string.Join(", ", disponibles) : "(ninguna: la lista de módulos está vacía)")}.");
#endif
        return null;
    }

    /// Avisa a todos los módulos de que la secuencia se cierra. Lo llama el SequencePlayer, tanto
    /// en el final normal como al saltar la secuencia o si algo falla.
    public void CleanupModules()
    {
        if (_modules == null) return;
        foreach (var module in _modules)
            if (module != null) module.OnSequenceCleanup();
    }


    /// Plano de cámara por nombre, o null (con aviso) si no está en la lista.
    public Transform GetShot(string shotName) => Resolve(_shots, shotName, "plano de cámara");

    /// Marca de posición por nombre, o null (con aviso) si no está en la lista.
    public Transform GetMark(string markName) => Resolve(_marks, markName, "marca de posición");

    private Transform Resolve(List<NamedTransform> list, string wanted, string what)
    {
        if (string.IsNullOrWhiteSpace(wanted) || list == null) return null;
        string key = wanted.Trim();

        foreach (var entry in list)
        {
            if (entry.target == null) continue;
            if (string.Equals(NameOf(entry), key, StringComparison.OrdinalIgnoreCase))
                return entry.target;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[SequenceStage:{name}] No hay ningún {what} llamado '{wanted}'. " +
            $"Disponibles: {string.Join(", ", NamesOf(list))}. " +
            "Revisa el nombre en el asset de la secuencia, o añade el punto en este componente.");
#endif
        return null;
    }

    /// El nombre efectivo de una entrada: el campo 'name' si lo tiene, si no el 'label' del
    /// CinematicShot, si no el nombre del GameObject. Así basta con arrastrar los planos ya
    /// colocados y etiquetados, sin volver a teclear su nombre aquí.
    private static string NameOf(NamedTransform entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.name)) return entry.name.Trim();
        if (entry.target != null && entry.target.TryGetComponent(out CinematicShot shot)
            && !string.IsNullOrWhiteSpace(shot.label))
            return shot.label.Trim();
        return entry.target != null ? entry.target.name : string.Empty;
    }

    private static List<string> NamesOf(List<NamedTransform> list)
    {
        var names = new List<string>();
        if (list != null)
            foreach (var entry in list)
                if (entry.target != null) names.Add(NameOf(entry));
        return names;
    }
}
