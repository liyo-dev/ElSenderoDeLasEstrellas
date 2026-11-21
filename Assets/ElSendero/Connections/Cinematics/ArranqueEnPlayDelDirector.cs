using Liyodev.NarrativeGraph;
#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

/// Cuando el Director pide «Ver en Unity», deja la señal de entrada de la secuencia en SessionState
/// y entra en Play. Esto la arranca dentro de Play igual que haría el grafo: espera a que el mundo
/// esté montado (reparto colocado) y emite la señal que el SequencePlayer de la escena escucha.
///
/// La escena se prueba como escena, no como partida: la historia no arranca por su cuenta (si no,
/// MainWorld empieza Cap1 y sale el prólogo encima de la secuencia que se quiere ver).
///
/// Solo existe en el Editor: no toca la build. Ver TDD § 35.
public static class ArranqueEnPlayDelDirector
{
    private const string ClaveSenal = "Director.Play.Senal";
    private const string ClaveNombre = "Director.Play.Nombre";
    private const string ClaveDesde = "Director.Play.Desde";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Preparar()
    {
        string senal = SessionState.GetString(ClaveSenal, string.Empty);
        if (string.IsNullOrEmpty(senal)) return;

        SessionState.EraseString(ClaveSenal);
        string nombre = SessionState.GetString(ClaveNombre, "cinemática");
        SessionState.EraseString(ClaveNombre);
        float desde = SessionState.GetFloat(ClaveDesde, 0f);
        SessionState.EraseFloat(ClaveDesde);

        NarrativeQuickStart.SuppressAutoStart();

        var go = new GameObject("Director · arranque en Play");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Arranque>().Empezar(senal, nombre, desde);
    }

    private class Arranque : MonoBehaviour
    {
        private string _senal;
        private string _nombre;
        private float _desde;

        public void Empezar(string senal, string nombre, float desde)
        {
            _senal = senal;
            _nombre = nombre;
            _desde = Mathf.Max(0f, desde);
            StartCoroutine(Rodar());
        }

        private IEnumerator Rodar()
        {
            // El mundo tiene que estar montado (jugador colocado y reparto creado) antes de
            // levantar la señal: si no, la secuencia arranca con la escena a medio montar.
            float limite = Time.realtimeSinceStartup + 30f;
            while (!WorldBootstrap.MundoListo && Time.realtimeSinceStartup < limite)
                yield return null;

            if (!WorldBootstrap.MundoListo)
            {
                Debug.LogError($"[Director] «{_nombre}»: el mundo no ha terminado de montarse en " +
                               "30 s; no se rueda la secuencia.");
                Destroy(gameObject);
                yield break;
            }

            // Un frame para que los Start de la escena terminen de registrarse.
            yield return null;

            if (CinematicSequencerBase.AnySequenceActive)
            {
                Debug.LogWarning($"[Director] «{_nombre}»: ya hay una cinemática en marcha; no se " +
                                 "lanza otra encima.");
                Destroy(gameObject);
                yield break;
            }

            var reproductor = BuscarReproductor(_senal);
            if (reproductor == null)
            {
                Debug.LogError($"[Director] «{_nombre}»: no hay ningún SequencePlayer que escuche " +
                               $"'{_senal}' en la escena; la señal no la escucha nadie.");
                Destroy(gameObject);
                yield break;
            }

            if (_desde > 0f)
            {
                reproductor.EmpezarEnOverride = _desde;
                reproductor.EmpezarEnGuion = _nombre;
            }

            Debug.Log($"[Director] Rodando «{_nombre}» en Play (señal {_senal}" +
                      (_desde > 0f ? $", desde {_desde:0.0} s" : string.Empty) + ").");
            DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_senal, $"[Director] {_nombre}");
            Destroy(gameObject, 3f);
        }

        /// El reproductor de esta escena que escucha esa señal de entrada (la del asset o, si no,
        /// la del Inspector; lo dice el propio SequencePlayer).
        private static SequencePlayer BuscarReproductor(string senal)
        {
            foreach (var reproductor in Object.FindObjectsByType<SequencePlayer>())
                if (reproductor != null && reproductor.SenalDeEntrada == senal) return reproductor;
            return null;
        }
    }
}
#endif
