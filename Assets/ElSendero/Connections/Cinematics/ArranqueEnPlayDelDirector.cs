#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;

/// Cuando el Director pide «Ver en Unity», deja la señal de entrada de la secuencia en SessionState
/// y entra en Play. Esto la arranca dentro de Play igual que haría el grafo: espera a que el mundo
/// esté montado (reparto colocado) y emite la señal que el SequencePlayer de la escena escucha.
///
/// Solo existe en el Editor: no toca la build. Ver TDD § 35.
public static class ArranqueEnPlayDelDirector
{
    private const string ClaveSenal = "Director.Play.Senal";
    private const string ClaveNombre = "Director.Play.Nombre";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Preparar()
    {
        string senal = SessionState.GetString(ClaveSenal, string.Empty);
        if (string.IsNullOrEmpty(senal)) return;

        SessionState.EraseString(ClaveSenal);
        string nombre = SessionState.GetString(ClaveNombre, "cinemática");
        SessionState.EraseString(ClaveNombre);

        var go = new GameObject("Director · arranque en Play");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Arranque>().Empezar(senal, nombre);
    }

    private class Arranque : MonoBehaviour
    {
        private string _senal;
        private string _nombre;

        public void Empezar(string senal, string nombre)
        {
            _senal = senal;
            _nombre = nombre;
            StartCoroutine(Rodar());
        }

        private IEnumerator Rodar()
        {
            // Se deja montar el mundo: manejadores, reparto y reproductores de la escena.
            float limite = Time.realtimeSinceStartup + 12f;
            while (Time.realtimeSinceStartup < limite)
            {
                if (Object.FindAnyObjectByType<SequencePlayer>() != null) break;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);

            if (Object.FindAnyObjectByType<SequencePlayer>() == null)
            {
                Debug.LogError($"[Director] «{_nombre}»: no hay ningún SequencePlayer en la escena; " +
                               "la señal no la escucha nadie.");
                Destroy(gameObject);
                yield break;
            }

            Debug.Log($"[Director] Rodando «{_nombre}» en Play (señal {_senal}).");
            DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_senal, $"[Director] {_nombre}");
            Destroy(gameObject, 3f);
        }
    }
}
#endif
