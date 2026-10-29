using UnityEngine;

/// <summary>
/// Conecta los sistemas de El Sendero con el grafo narrativo: registra en
/// <see cref="NarrativeServices"/> un adaptador por cada puerto del grafo antes de que se cargue
/// la primera escena. Es el único sitio que hay que mirar para saber qué sistema del juego atiende
/// cada cosa que pide un nodo.
///
/// Los adaptadores no guardan referencias a los sistemas: los buscan en cada llamada (sus
/// singletons), así que da igual el orden en que despierten los managers de Start.unity.
/// El presentador de rótulos (<see cref="INarrativeTitleCardPresenter"/>) lo registra el propio
/// DramaticTextOverlayUI al despertar.
/// </summary>
public static class NarrativeGameServices
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var partida = new PartidaNarrativeData();
        partida.Install();
        NarrativeServices.Register<INarrativeFlagStore>(partida);
        NarrativeServices.Register<INarrativeSaveData>(partida);

        var audio = new AudioServiceNarrativeAdapter();
        NarrativeServices.Register<INarrativeAudio>(audio);
        NarrativeServices.Register<INarrativeAudioMix>(audio);

        NarrativeServices.Register<INarrativeScreen>(new ScreenNarrativeAdapter());
        NarrativeServices.Register<INarrativeTextLocalizer>(new LocalizationNarrativeAdapter());

        var player = new PlayerNarrativeAdapter();
        NarrativeServices.Register<INarrativePlayerControl>(player);
        NarrativeServices.Register<INarrativeInventory>(player);

        var npcs = new NpcNarrativeAdapter();
        NarrativeServices.Register<INarrativeActorDirectory>(npcs);
        NarrativeServices.Register<INarrativeAttentionMarkers>(npcs);
        NarrativeServices.Register<INarrativeActorPlacement>(npcs);

        var dialogues = new DialogueNarrativeAdapter();
        NarrativeServices.Register<INarrativeDialoguePlayer>(dialogues);
        NarrativeServices.Register<INarrativeChoicePresenter>(dialogues);

        NarrativeServices.Register<INarrativeCameraFocus>(new CameraFocusNarrativeAdapter());
        NarrativeServices.Register<INarrativeSpeechBubbles>(new SpeechBubbleNarrativeAdapter());
        NarrativeServices.Register<INarrativeSceneHooks>(new WorldNarrativeSceneHooks());

        QuestServiceAdapter.Install();
    }
}
