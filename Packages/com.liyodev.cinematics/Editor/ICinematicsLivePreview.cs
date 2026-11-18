using System;
using UnityEngine;

namespace Liyodev.Cinematics
{
    /// Una cinemática abierta en vivo en el Editor, sin entrar en PlayMode: coloca la escena en un
    /// segundo (actores, poses, cámara) y la rinde en una textura. Es lo que permite a la mesa
    /// reproducir y arrastrar la cinemática como un vídeo.
    ///
    /// Mientras está abierta la escena está tocada; <see cref="Soltar"/> la deja como estaba sin
    /// cerrar la vista (la mesa lo pide antes de guardar escenas o de entrar en Play) y el siguiente
    /// <see cref="Rendir"/> la vuelve a colocar. <see cref="IDisposable.Dispose"/> la suelta y libera
    /// todo lo que creó.
    public interface ICinematicLiveView : IDisposable
    {
        /// Coloca la escena en `segundo` y la rinde en `destino`. Devuelve false si no pudo (el motivo,
        /// en <see cref="Error"/>); la textura queda como estaba.
        bool Rendir(float segundo, RenderTexture destino);

        /// Por qué falló el último <see cref="Rendir"/> (vacío si fue bien).
        string Error { get; }

        /// Devuelve la escena a como estaba, sin cerrar la vista.
        void Soltar();
    }

    /// Lo implementa el puente que sabe abrir una cinemática en vivo. Es aparte de
    /// <see cref="ICinematicsEditorBridge"/> para que un puente que no lo sepa hacer siga valiendo:
    /// la mesa enseña entonces solo los fotogramas del storyboard.
    public interface ICinematicsLivePreview
    {
        /// Abre la vista en vivo de una cinemática. Devuelve null (y el motivo en `error`) si no se puede.
        ICinematicLiveView AbrirVistaEnVivo(string id, out string error);
    }

    /// Lo implementa el puente que sabe mandar sobre la cinemática que está rodando en PlayMode. Con
    /// él, la mesa sigue el segundo de la de verdad y sus mandos (play, pausa, barra) la mueven.
    public interface ICinematicsPlayControl
    {
        /// La cinemática que rueda ahora en Play: su id, el segundo y si está en pausa. False si no hay ninguna.
        bool EnMarcha(out string id, out float segundo, out float duracion, out bool pausada);

        /// Salta a ese segundo de la cinemática que rueda.
        void Saltar(float segundo);

        /// Pausa o reanuda la cinemática que rueda.
        void Pausar(bool pausar);
    }
}
