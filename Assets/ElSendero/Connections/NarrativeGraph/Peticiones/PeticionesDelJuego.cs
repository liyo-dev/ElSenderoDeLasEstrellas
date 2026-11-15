using UnityEngine;

/// Quién atiende en El Sendero cada petición del paso «Pedir al juego». El grafo no conoce a
/// ninguno: solo emite la petición y espera a que el manejador apuntado aquí termine.
public static class PeticionesDelJuego
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Registrar()
    {
        NarrativeRequests.Register<PedirCinematica, CinematicaEnCurso>();
        NarrativeRequests.Register<PedirHoraDelDia, HoraDelDiaEnCurso>();
        NarrativeRequests.Register<PedirSegunHoraDelDia, SegunHoraDelDiaEnCurso>();
        NarrativeRequests.Register<PedirEsperarHoraDelDia, EsperarHoraDelDiaEnCurso>();
        NarrativeRequests.Register<PedirDesbloqueo, DesbloqueoEnCurso>();
        NarrativeRequests.Register<PedirVestuario, VestuarioEnCurso>();
        NarrativeRequests.Register<PedirGrupo, GrupoEnCurso>();
        NarrativeRequests.Register<PedirTextoDramatico, TextoDramaticoEnCurso>();
        NarrativeRequests.Register<PedirPopupDeLore, PopupDeLoreEnCurso>();
        NarrativeRequests.Register<PedirFinDeLaDemo, FinDeLaDemoEnCurso>();
        NarrativeRequests.Register<PedirAvisoDeTutorial, AvisoDeTutorialEnCurso>();
        NarrativeRequests.Register<PedirEntregaDeObjeto, EntregaDeObjetoEnCurso>();
        NarrativeRequests.Register<PedirBatalla, BatallaEnCurso>();
        NarrativeRequests.Register<PedirCombateContraNpc, CombateContraNpcEnCurso>();
        NarrativeRequests.Register<PedirGuiarAlJugador, GuiarAlJugadorEnCurso>();
        NarrativeRequests.Register<PedirSeguirSinSerVisto, SeguirSinSerVistoEnCurso>();
    }
}
