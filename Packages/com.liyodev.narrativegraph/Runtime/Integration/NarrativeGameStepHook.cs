using UnityEngine;
using UnityEngine.Events;

namespace Liyodev.NarrativeGraph
{
    /// <summary>
    /// Lo que atiende un «Pedir al juego» montado en la escena, sin código: cuando la historia lo pide por su id,
    /// lanza sus eventos (abrir una puerta, llamar a un sistema que ya existe, empezar una
    /// animación…). Para avisar de que ha terminado, desde un evento, una animación o una señal de
    /// Timeline se llama a <see cref="Finish"/> (o <see cref="FinishThrough(string)"/> para elegir salida);
    /// con <see cref="finishRightAway"/>, termina en cuanto lanza los eventos.
    /// </summary>
    [AddComponentMenu("Narrative Graph/Game Step")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Liyodev.NarrativeGraph")]
    public sealed class NarrativeGameStepHook : MonoBehaviour, INarrativeGameStep
    {
        /// <summary>Nombre del campo del id (lo lee el índice del editor en escenas y prefabs).</summary>
        public const string IdFieldName = nameof(gameStepId);

        [Tooltip("Id con el que lo pide el paso «Pedir al juego» del grafo.")]
        public string gameStepId;

        [Tooltip("Termina en cuanto lanza los eventos (acciones de un golpe). Si no, espera a que algo llame a Finish.")]
        public bool finishRightAway;

        [Tooltip("Lo que hace al empezar. Los datos del grafo se leen en CurrentText, CurrentNumber y CurrentAsset.")]
        public UnityEvent onBegin = new();

        [Tooltip("Lo que hace si la historia deja de esperar antes de que termine (tiempo agotado, se para el capítulo).")]
        public UnityEvent onCancel = new();

        NarrativeGameStepCall _current;

        /// <summary>Texto que puso el grafo en la llamada en curso.</summary>
        public string CurrentText => _current?.Text ?? string.Empty;

        /// <summary>Número que puso el grafo en la llamada en curso.</summary>
        public float CurrentNumber => _current?.Number ?? 0f;

        /// <summary>Asset que puso el grafo en la llamada en curso.</summary>
        public Object CurrentAsset => _current?.Asset;

        /// <summary>true mientras la historia espera a que termine.</summary>
        public bool IsRunning => _current != null && !_current.IsOver;

        void OnEnable() => NarrativeGameSteps.Register(gameStepId, this);

        void OnDisable()
        {
            NarrativeGameSteps.Unregister(gameStepId, this);
            // Si se apaga a medias, la historia no se queda esperando a un objeto que ya no está.
            if (IsRunning) _current.Abandon();
            _current = null;
        }

        void INarrativeGameStep.Begin(NarrativeGameStepCall call)
        {
            _current = call;
            onBegin.Invoke();
            if (finishRightAway) call.Finish(0);
        }

        void INarrativeGameStep.Cancel(NarrativeGameStepCall call)
        {
            if (call != _current) return;
            _current = null;
            onCancel.Invoke();
        }

        /// <summary>Termina por la primera salida. Para eventos, animaciones y señales de Timeline.</summary>
        public void Finish() => _current?.Finish(0);

        /// <summary>Termina por la salida con ese número (0 = la primera).</summary>
        public void FinishThrough(int output) => _current?.Finish(output);

        /// <summary>Termina por la salida con ese nombre.</summary>
        public void FinishThrough(string output) => _current?.Finish(output);
    }
}
