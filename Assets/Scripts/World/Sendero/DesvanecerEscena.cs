using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Hace desaparecer una escena entera pieza a pieza, de lejos a cerca de un punto: primero se apaga
/// cada luz y después cada objeto se encoge y se deshace en polvo de luz. Es el parque que se esfuma
/// en la ruptura del Sendero («las luces del parque empezaron a apagarse una a una»), pero vale para
/// cualquier escena. Los personajes (NPCSimpleAnimator) no se tocan. Al acabar puede descargar la
/// escena. Se lanza con Empezar(); lo usa DesvanecerEscenaBeat.
/// </summary>
public class DesvanecerEscena : MonoBehaviour
{
    private struct Pieza
    {
        public Transform transform;
        public Vector3 escala;
        public Vector3 centro;
        public float momento;
    }

    private struct Farol
    {
        public Light luz;
        public float intensidad;
        public float momento;
    }

    private const float DuracionDePieza = 0.7f;
    private const float AdelantoDeLaLuz = 0.8f;

    public bool Terminado { get; private set; }

    public static DesvanecerEscena Empezar(Scene escena, Vector3 centro, float duracion, GameObject vfx, bool descargar)
    {
        var go = new GameObject("Desvanecer " + escena.name);
        DontDestroyOnLoad(go); // la escena que desaparece puede ser la activa
        var d = go.AddComponent<DesvanecerEscena>();
        d.StartCoroutine(d.Correr(escena, centro, duracion, vfx, descargar));
        return d;
    }

    private IEnumerator Correr(Scene escena, Vector3 centro, float duracion, GameObject vfx, bool descargar)
    {
        var piezas = new List<Pieza>();
        var faroles = new List<Farol>();
        var vistas = new HashSet<Transform>();
        float lejos = 1f;

        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || r.GetComponentInParent<NPCSimpleAnimator>() != null) continue;
                if (!vistas.Add(r.transform)) continue;
                var c = r.bounds.center;
                float d = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(centro.x, centro.z));
                lejos = Mathf.Max(lejos, d);
                piezas.Add(new Pieza { transform = r.transform, escala = r.transform.localScale, centro = c, momento = d });
            }
            foreach (var l in raiz.GetComponentsInChildren<Light>(false))
            {
                if (!l.enabled) continue;
                float d = Vector3.Distance(l.transform.position, centro);
                lejos = Mathf.Max(lejos, d);
                faroles.Add(new Farol { luz = l, intensidad = l.intensity, momento = d });
            }
        }

        // De lejos a cerca: lo más lejano se va primero.
        for (int i = 0; i < piezas.Count; i++) { var p = piezas[i]; p.momento = (1f - p.momento / lejos) * duracion; piezas[i] = p; }
        for (int i = 0; i < faroles.Count; i++) { var f = faroles[i]; f.momento = Mathf.Max(0f, (1f - f.momento / lejos) * duracion - AdelantoDeLaLuz); faroles[i] = f; }
        piezas.Sort((a, b) => a.momento.CompareTo(b.momento));

        float t = 0f;
        int siguiente = 0;
        var enCurso = new List<(Pieza pieza, float desde)>();
        while (siguiente < piezas.Count || enCurso.Count > 0 || t < duracion)
        {
            t += Time.deltaTime;
            foreach (var f in faroles)
                if (f.luz != null) f.luz.intensity = f.intensidad * Mathf.Clamp01(1f - (t - f.momento) / AdelantoDeLaLuz);

            while (siguiente < piezas.Count && piezas[siguiente].momento <= t)
            {
                var p = piezas[siguiente++];
                if (p.transform == null) continue;
                foreach (var col in p.transform.GetComponents<Collider>()) col.enabled = false;
                if (vfx != null && VfxPoolService.Instance != null)
                    VfxPoolService.Instance.Play(vfx, p.centro, Quaternion.identity, 2.5f);
                enCurso.Add((p, t));
            }

            for (int i = enCurso.Count - 1; i >= 0; i--)
            {
                var (p, desde) = enCurso[i];
                if (p.transform == null) { enCurso.RemoveAt(i); continue; }
                float k = Mathf.Clamp01((t - desde) / DuracionDePieza);
                p.transform.localScale = p.escala * (1f - k * k);
                if (k >= 1f)
                {
                    p.transform.gameObject.SetActive(false);
                    enCurso.RemoveAt(i);
                }
            }
            yield return null;
        }

        if (descargar && escena.isLoaded) yield return SceneManager.UnloadSceneAsync(escena);
        Terminado = true;
        Destroy(gameObject);
    }
}
