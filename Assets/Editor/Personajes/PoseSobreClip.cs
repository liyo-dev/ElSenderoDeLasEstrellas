using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Animaciones nuevas para personajes de pack sin tocar sus archivos: copia un clip suyo (el reposo,
/// el correr…) y fija la pose de algunos huesos encima, apuntándolos a una dirección o a un punto en el
/// espacio del personaje (X a su derecha, Y arriba, Z al frente). Sirve para «brazos arriba», «la mano a
/// la boca», etc. El resto del cuerpo conserva el movimiento del clip de partida.
/// También copia el controlador del pack (una sola vez) y le añade estados con esos clips.
/// </summary>
public static class PoseSobreClip
{
    public struct Apunte
    {
        /// <summary>Hueso que gira y el hueso hijo que marca su dirección (su punta).</summary>
        public string hueso, punta;
        /// <summary>Dirección (o punto, si <see cref="esPunto"/>) en el espacio del personaje.</summary>
        public Vector3 objetivo;
        public bool esPunto;

        public static Apunte Hacia(string hueso, string punta, Vector3 direccion) => new Apunte { hueso = hueso, punta = punta, objetivo = direccion };
        public static Apunte A(string hueso, string punta, Vector3 punto) => new Apunte { hueso = hueso, punta = punta, objetivo = punto, esPunto = true };
    }

    /// <summary>Lo que hace falta del personaje en la pose para apuntar: posiciones y giros respecto a su raíz.</summary>
    public sealed class Medidas
    {
        public readonly Dictionary<string, Vector3> posicion = new();
        public readonly Dictionary<string, Quaternion> giro = new();
        public Bounds caja;
    }

    /// <summary>Medidas del personaje en el primer fotograma del clip (para calcular objetivos: la boca, los hombros…).</summary>
    public static Medidas Medir(GameObject prefab, AnimationClip clip) => Posar(prefab, clip, null, null, null);

    /// <summary>
    /// Crea o rehace en <paramref name="ruta"/> la copia de <paramref name="base_"/> con los apuntes fijados.
    /// Devuelve las medidas del personaje ya en la pose nueva (para colocar lo que lleve en la mano).
    /// </summary>
    public static Medidas Crear(GameObject prefab, AnimationClip base_, string ruta, IList<Apunte> apuntes, bool bucle)
    {
        var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(ruta), frameRate = base_.frameRate };
        var medidas = Posar(prefab, base_, apuntes, clip, ruta);
        var ajustes = AnimationUtility.GetAnimationClipSettings(clip);
        ajustes.loopTime = bucle;
        AnimationUtility.SetAnimationClipSettings(clip, ajustes);

        Directory.CreateDirectory(Path.GetDirectoryName(ruta));
        var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
        if (existente != null) { EditorUtility.CopySerialized(clip, existente); EditorUtility.SetDirty(existente); Object.DestroyImmediate(clip); }
        else AssetDatabase.CreateAsset(clip, ruta);
        return medidas;
    }

    private static Medidas Posar(GameObject prefab, AnimationClip base_, IList<Apunte> apuntes, AnimationClip destino, string ruta)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var raiz = inst.transform;
            raiz.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // Los caminos del clip van desde el objeto del Animator (si el modelo no trae, la raíz: ahí lo pone el reparto).
            var animador = inst.GetComponentInChildren<Animator>(true);
            var animado = animador != null ? animador.transform : raiz;
            base_.SampleAnimation(animado.gameObject, 0f);
            var huesos = raiz.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());

            var tocados = new List<Transform>();
            if (apuntes != null)
                foreach (var a in apuntes)
                {
                    if (!huesos.TryGetValue(a.hueso, out var h) || !huesos.TryGetValue(a.punta, out var p))
                    { Debug.LogWarning($"[PoseSobreClip] {prefab.name}: no encuentro {a.hueso} o {a.punta}."); continue; }
                    Vector3 ahora = p.position - h.position;
                    Vector3 quiero = a.esPunto ? raiz.TransformPoint(a.objetivo) - h.position : raiz.TransformDirection(a.objetivo);
                    if (ahora.sqrMagnitude < 1e-8f || quiero.sqrMagnitude < 1e-8f) continue;
                    h.rotation = Quaternion.FromToRotation(ahora, quiero) * h.rotation;
                    if (!tocados.Contains(h)) tocados.Add(h);
                }

            if (destino != null)
            {
                // Todo el clip de partida, menos el giro de los huesos tocados, que queda fijo en la pose nueva.
                var rutas = new HashSet<string>(tocados.Select(t => AnimationUtility.CalculateTransformPath(t, animado)));
                foreach (var b in AnimationUtility.GetCurveBindings(base_))
                {
                    bool giro = b.propertyName.StartsWith("m_LocalRotation") || b.propertyName.StartsWith("localEulerAngles");
                    if (giro && rutas.Contains(b.path)) continue;
                    AnimationUtility.SetEditorCurve(destino, b, AnimationUtility.GetEditorCurve(base_, b));
                }
                float largo = Mathf.Max(base_.length, 1f / Mathf.Max(1f, base_.frameRate));
                foreach (var t in tocados)
                {
                    string camino = AnimationUtility.CalculateTransformPath(t, animado);
                    var q = t.localRotation;
                    float[] v = { q.x, q.y, q.z, q.w };
                    string[] c = { "x", "y", "z", "w" };
                    for (int i = 0; i < 4; i++)
                        AnimationUtility.SetEditorCurve(destino, EditorCurveBinding.FloatCurve(camino, typeof(Transform), "m_LocalRotation." + c[i]),
                                                        AnimationCurve.Constant(0f, largo, v[i]));
                }
                destino.EnsureQuaternionContinuity();
            }

            var m = new Medidas();
            foreach (var kv in huesos)
            {
                m.posicion[kv.Key] = raiz.InverseTransformPoint(kv.Value.position);
                m.giro[kv.Key] = Quaternion.Inverse(raiz.rotation) * kv.Value.rotation;
            }
            var rs = inst.GetComponentsInChildren<Renderer>(true);
            if (rs.Length > 0) { m.caja = rs[0].bounds; foreach (var r in rs) m.caja.Encapsulate(r.bounds); }
            return m;
        }
        finally { Object.DestroyImmediate(inst); }
    }

    /// <summary>
    /// Copia del controlador <paramref name="origen"/> en <paramref name="destino"/> (se copia una vez y
    /// después se conserva) con estos estados añadidos o puestos al día. Los que no van en bucle vuelven
    /// solos al estado por defecto.
    /// </summary>
    public static AnimatorController ControladorConEstados(string origen, string destino, params (string estado, AnimationClip clip, bool bucle)[] estados)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(destino);
        if (ctrl == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            if (!AssetDatabase.CopyAsset(origen, destino)) { Debug.LogWarning($"[PoseSobreClip] No puedo copiar {origen}."); return null; }
            ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(destino);
        }
        var maquina = ctrl.layers[0].stateMachine;
        foreach (var (nombre, clip, bucle) in estados)
        {
            if (clip == null) continue;
            var viejo = maquina.states.FirstOrDefault(s => s.state.name == nombre).state;
            if (viejo != null) maquina.RemoveState(viejo);
            var estado = maquina.AddState(nombre);
            estado.motion = clip;
            if (bucle || maquina.defaultState == null) continue;
            var vuelta = estado.AddTransition(maquina.defaultState);
            vuelta.hasExitTime = true;
            vuelta.exitTime = 0.95f;
            vuelta.duration = 0.2f;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }
}
