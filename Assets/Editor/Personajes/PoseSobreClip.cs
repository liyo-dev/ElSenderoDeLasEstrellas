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
/// Para personajes humanoides (los del grupo, los NPC del juego), <see cref="CrearHumanoide"/> hace lo
/// mismo con curvas de músculo, que valen para cualquier avatar humanoide.
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

    // ---------------------------------------------------------------------------------------
    // Humanoides
    // ---------------------------------------------------------------------------------------

    /// <summary>Apunte de un hueso humanoide: gira <c>hueso</c> para que la línea hasta <c>punta</c> vaya en
    /// <c>direccion</c> (espacio del personaje: X a su derecha, Y arriba, Z al frente).</summary>
    public struct ApunteHumano
    {
        public HumanBodyBones hueso, punta;
        public Vector3 direccion;

        public static ApunteHumano Hacia(HumanBodyBones hueso, HumanBodyBones punta, Vector3 direccion)
            => new ApunteHumano { hueso = hueso, punta = punta, direccion = direccion };
    }

    private const int IteracionesPrimerFotograma = 60;
    private const int IteracionesPorFotograma = 10;
    private const float LimiteDeMusculo = 1f;

    /// <summary>
    /// Crea o rehace en <paramref name="ruta"/> un clip humanoide en bucle: el movimiento de
    /// <paramref name="base_"/> (respiración, balanceo) con los huesos de los apuntes resueltos en
    /// músculos para que apunten donde se pide. Las direcciones se toman en el primer fotograma y
    /// después acompañan al hueso <paramref name="marco"/> (el pecho), así los brazos respiran con el
    /// tronco. Solo cambian las curvas de los músculos de los huesos apuntados; el resto se copia tal
    /// cual. Devuelve el mayor desvío (grados) entre la dirección pedida y la conseguida.
    /// </summary>
    public static float CrearHumanoide(GameObject prefab, AnimationClip base_, string ruta, IList<ApunteHumano> apuntes,
                                       HumanBodyBones marco = HumanBodyBones.Chest)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.hideFlags = HideFlags.HideAndDontSave;
        inst.SetActive(false);
        foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.SetActive(true);
        HumanPoseHandler handler = null;
        bool modo = !AnimationMode.InAnimationMode();
        try
        {
            var animador = inst.GetComponentsInChildren<Animator>(true).FirstOrDefault(a => a.isHuman && a.avatar != null);
            if (animador == null) throw new System.InvalidOperationException($"{prefab.name} no tiene un Animator humanoide.");
            var raiz = animador.transform;
            handler = new HumanPoseHandler(animador.avatar, raiz);
            var reposo = new HumanPose();
            handler.GetHumanPose(ref reposo);

            var huesos = apuntes.Select(a => (h: animador.GetBoneTransform(a.hueso), p: animador.GetBoneTransform(a.punta))).ToArray();
            if (huesos.Any(x => x.h == null || x.p == null)) throw new System.InvalidOperationException($"{prefab.name}: faltan huesos de los apuntes.");
            var huesoMarco = animador.GetBoneTransform(marco) ?? animador.GetBoneTransform(HumanBodyBones.Spine);

            var musculos = new List<int>();
            foreach (var a in apuntes)
                for (int dof = 0; dof < 3; dof++)
                {
                    int m = HumanTrait.MuscleFromBone((int)a.hueso, dof);
                    if (m >= 0 && !musculos.Contains(m)) musculos.Add(m);
                }

            float fps = base_.frameRate > 0f ? base_.frameRate : 30f;
            int fotogramas = Mathf.Max(1, Mathf.RoundToInt(base_.length * fps));
            var valores = new float[fotogramas + 1][];
            var objetivos = new Vector3[apuntes.Count];
            Quaternion marcoInicial = Quaternion.identity;
            float peor = 0f;
            if (modo) AnimationMode.StartAnimationMode();

            for (int f = 0; f <= fotogramas; f++)
            {
                float t = Mathf.Min(f / fps, base_.length);
                handler.SetHumanPose(ref reposo);
                AnimationMode.BeginSampling();
                try { AnimationMode.SampleAnimationClip(animador.gameObject, base_, t); }
                finally { AnimationMode.EndSampling(); }

                var pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                Quaternion giroMarco = Quaternion.Inverse(raiz.rotation) * huesoMarco.rotation;
                if (f == 0) marcoInicial = giroMarco;
                Quaternion respiro = giroMarco * Quaternion.Inverse(marcoInicial);
                for (int k = 0; k < apuntes.Count; k++) objetivos[k] = (respiro * apuntes[k].direccion).normalized;

                float[] x;
                if (f == 0)
                {
                    // Punto de partida: los huesos girados a mano hacia su dirección, leídos como músculos.
                    for (int k = 0; k < apuntes.Count; k++)
                    {
                        Vector3 ahora = huesos[k].p.position - huesos[k].h.position;
                        Vector3 quiero = raiz.TransformDirection(objetivos[k]);
                        huesos[k].h.rotation = Quaternion.FromToRotation(ahora, quiero) * huesos[k].h.rotation;
                    }
                    var guiada = new HumanPose();
                    handler.GetHumanPose(ref guiada);
                    x = musculos.Select(m => Mathf.Clamp(guiada.muscles[m], -LimiteDeMusculo, LimiteDeMusculo)).ToArray();
                }
                else x = (float[])valores[f - 1].Clone();

                x = Resolver(handler, pose, musculos, x, raiz, huesos, objetivos, f == 0 ? IteracionesPrimerFotograma : IteracionesPorFotograma);
                valores[f] = x;
                peor = Mathf.Max(peor, Desvio(handler, pose, musculos, x, raiz, huesos, objetivos));
            }

            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(ruta), frameRate = fps };
            var nombres = new HashSet<string>(musculos.Select(m => HumanTrait.MuscleName[m]));
            foreach (var b in AnimationUtility.GetCurveBindings(base_))
            {
                if (b.type == typeof(Animator) && nombres.Contains(b.propertyName)) continue;
                AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(base_, b));
            }
            for (int i = 0; i < musculos.Count; i++)
            {
                var claves = new Keyframe[fotogramas + 1];
                for (int f = 0; f <= fotogramas; f++) claves[f] = new Keyframe(Mathf.Min(f / fps, base_.length), valores[f][i]);
                var curva = new AnimationCurve(claves);
                for (int f = 0; f < claves.Length; f++) curva.SmoothTangents(f, 0f);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[musculos[i]]), curva);
            }
            var ajustes = AnimationUtility.GetAnimationClipSettings(base_);
            ajustes.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, ajustes);

            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(ruta);
            if (existente != null) { EditorUtility.CopySerialized(clip, existente); EditorUtility.SetDirty(existente); Object.DestroyImmediate(clip); }
            else AssetDatabase.CreateAsset(clip, ruta);
            AssetDatabase.SaveAssets();
            return peor;
        }
        finally
        {
            if (modo && AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            handler?.Dispose();
            Object.DestroyImmediate(inst);
        }
    }

    /// <summary>Levenberg-Marquardt sobre los músculos: acerca la dirección de cada hueso a su objetivo.</summary>
    private static float[] Resolver(HumanPoseHandler handler, HumanPose pose, List<int> musculos, float[] x, Transform raiz,
                                    (Transform h, Transform p)[] huesos, Vector3[] objetivos, int iteraciones)
    {
        int n = x.Length, r = huesos.Length * 3;
        const float paso = 1e-3f;
        float lambda = 1e-2f;
        var res = Residuo(handler, pose, musculos, x, raiz, huesos, objetivos);
        float coste = Coste(res);
        var J = new float[r, n];
        for (int it = 0; it < iteraciones && coste > 1e-7f; it++)
        {
            for (int j = 0; j < n; j++)
            {
                var xj = (float[])x.Clone();
                xj[j] += paso;
                var rj = Residuo(handler, pose, musculos, xj, raiz, huesos, objetivos);
                for (int i = 0; i < r; i++) J[i, j] = (rj[i] - res[i]) / paso;
            }
            var A = new float[n, n];
            var g = new float[n];
            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++) { float s = 0f; for (int i = 0; i < r; i++) s += J[i, a] * J[i, b]; A[a, b] = s; }
                float sg = 0f; for (int i = 0; i < r; i++) sg += J[i, a] * res[i]; g[a] = -sg;
            }
            bool mejora = false;
            for (int intento = 0; intento < 6 && !mejora; intento++)
            {
                var M = (float[,])A.Clone();
                for (int a = 0; a < n; a++) M[a, a] += lambda * (1f + A[a, a]);
                var d = Despejar(M, (float[])g.Clone());
                if (d == null) { lambda *= 10f; continue; }
                var nx = new float[n];
                for (int a = 0; a < n; a++) nx[a] = Mathf.Clamp(x[a] + d[a], -LimiteDeMusculo, LimiteDeMusculo);
                var nres = Residuo(handler, pose, musculos, nx, raiz, huesos, objetivos);
                float ncoste = Coste(nres);
                if (ncoste < coste) { x = nx; res = nres; coste = ncoste; lambda = Mathf.Max(1e-5f, lambda * 0.3f); mejora = true; }
                else lambda *= 10f;
            }
            if (!mejora) break;
        }
        return x;
    }

    private static float[] Residuo(HumanPoseHandler handler, HumanPose pose, List<int> musculos, float[] x, Transform raiz,
                                   (Transform h, Transform p)[] huesos, Vector3[] objetivos)
    {
        var m = (float[])pose.muscles.Clone();
        for (int i = 0; i < musculos.Count; i++) m[musculos[i]] = x[i];
        var p = pose;
        p.muscles = m;
        handler.SetHumanPose(ref p);
        var r = new float[huesos.Length * 3];
        for (int k = 0; k < huesos.Length; k++)
        {
            Vector3 d = raiz.InverseTransformDirection(huesos[k].p.position - huesos[k].h.position).normalized - objetivos[k];
            r[k * 3] = d.x; r[k * 3 + 1] = d.y; r[k * 3 + 2] = d.z;
        }
        return r;
    }

    private static float Desvio(HumanPoseHandler handler, HumanPose pose, List<int> musculos, float[] x, Transform raiz,
                                (Transform h, Transform p)[] huesos, Vector3[] objetivos)
    {
        Residuo(handler, pose, musculos, x, raiz, huesos, objetivos);
        float peor = 0f;
        for (int k = 0; k < huesos.Length; k++)
            peor = Mathf.Max(peor, Vector3.Angle(raiz.InverseTransformDirection(huesos[k].p.position - huesos[k].h.position), objetivos[k]));
        return peor;
    }

    private static float Coste(float[] r) { float s = 0f; foreach (var v in r) s += v * v; return s; }

    /// <summary>Resuelve M·d = g por eliminación gaussiana con pivote. Null si es singular.</summary>
    private static float[] Despejar(float[,] M, float[] g)
    {
        int n = g.Length;
        for (int c = 0; c < n; c++)
        {
            int piv = c;
            for (int f = c + 1; f < n; f++) if (Mathf.Abs(M[f, c]) > Mathf.Abs(M[piv, c])) piv = f;
            if (Mathf.Abs(M[piv, c]) < 1e-9f) return null;
            if (piv != c)
            {
                for (int k = 0; k < n; k++) (M[c, k], M[piv, k]) = (M[piv, k], M[c, k]);
                (g[c], g[piv]) = (g[piv], g[c]);
            }
            for (int f = c + 1; f < n; f++)
            {
                float factor = M[f, c] / M[c, c];
                for (int k = c; k < n; k++) M[f, k] -= factor * M[c, k];
                g[f] -= factor * g[c];
            }
        }
        var d = new float[n];
        for (int f = n - 1; f >= 0; f--)
        {
            float s = g[f];
            for (int k = f + 1; k < n; k++) s -= M[f, k] * d[k];
            d[f] = s / M[f, f];
        }
        return d;
    }

    /// <summary>
    /// Pone en la capa <paramref name="capa"/> de <paramref name="ctrl"/> (vacío: la base) un estado
    /// <paramref name="nombre"/> con <paramref name="clip"/>, sin transiciones (es una pose que se
    /// sostiene y se suelta desde código). Si ya existe, solo le cambia el clip. Devuelve true si lo
    /// ha creado; lanza si la capa no existe.
    /// </summary>
    public static bool EstadoEnBucle(AnimatorController ctrl, string nombre, Motion clip, string capa = null)
    {
        var layer = string.IsNullOrEmpty(capa) ? ctrl.layers[0] : ctrl.layers.FirstOrDefault(l => l.name == capa);
        if (layer == null) throw new System.InvalidOperationException($"{ctrl.name} no tiene la capa {capa}.");
        var maquina = layer.stateMachine;
        var estado = maquina.states.FirstOrDefault(s => s.state.name == nombre).state;
        bool nuevo = estado == null;
        if (nuevo)
        {
            float y = maquina.states.Length > 0 ? maquina.states.Max(s => s.position.y) + 60f : 0f;
            estado = maquina.AddState(nombre, new Vector3(maquina.anyStatePosition.x, y, 0f));
            if (maquina.defaultState != null) estado.writeDefaultValues = maquina.defaultState.writeDefaultValues;
        }
        estado.motion = clip;
        EditorUtility.SetDirty(estado);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return nuevo;
    }
}
