using UnityEngine;

/// La historia no arranca hasta que el mundo está montado: reparto instanciado y jugador en su
/// ancla. Sin esto, al probar desde un paso a mitad de capítulo el grafo lanza la cinemática de
/// ese paso mientras <see cref="WorldBootstrap"/> todavía está naciendo, y la cinemática se ve
/// distinta que lanzada a mano (actores que aún no existen, jugador moviéndose de sitio).
///
/// El enganche vive aquí y no en el kit: el kit solo pregunta «¿está listo el mundo?».
static class MundoListoParaLaHistoria
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Enganchar() => NarrativeGraphStarter.IsWorldReady = () => WorldBootstrap.MundoListo;
}
