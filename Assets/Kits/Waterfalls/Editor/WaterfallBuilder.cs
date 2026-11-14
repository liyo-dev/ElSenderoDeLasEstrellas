using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kits.Waterfalls.Editor
{
    /// Colores de una cascada: los de la cortina y los de la espuma.
    [System.Serializable]
    public class WaterfallLook
    {
        public Color Light = new Color(0.78f, 0.95f, 1.0f);
        public Color Mid = new Color(0.36f, 0.72f, 0.88f);
        public Color Dark = new Color(0.18f, 0.46f, 0.70f);
        public Color Foam = new Color(1.0f, 0.99f, 0.95f);
        public Color Ink = new Color(0.20f, 0.15f, 0.22f);
        public Color FoamShade = new Color(0.70f, 0.86f, 0.95f);
        public Color FoamRim = new Color(0.55f, 0.75f, 0.90f);
        public float FallSpeed = 4.5f;
    }

    /// Monta cascadas completas: cortina (Waterfall), espuma, salpicaduras y vapor al pie de cada
    /// tramo que cae, y una zona atravesable (WaterCurtainZone) en cada cortina.
    public static class WaterfallBuilder
    {
        public const string CurtainShader = "Kits/Waterfalls/Curtain";
        public const string FoamShader = "Kits/Waterfalls/Foam";

        /// Crea o actualiza los dos materiales de una cascada en la carpeta dada.
        public static (Material curtain, Material foam) Materials(string folder, string prefix, WaterfallLook look)
        {
            EnsureFolder(folder);
            var curtain = Asset($"{folder}/{prefix}Curtain.mat", CurtainShader);
            curtain.SetColor("_ColorLight", look.Light);
            curtain.SetColor("_ColorMid", look.Mid);
            curtain.SetColor("_ColorDark", look.Dark);
            curtain.SetColor("_ColorFoam", look.Foam);
            curtain.SetColor("_ColorInk", look.Ink);
            curtain.SetFloat("_FallSpeed", look.FallSpeed);
            var foam = Asset($"{folder}/{prefix}Foam.mat", FoamShader);
            foam.SetColor("_ColorLit", look.Foam);
            foam.SetColor("_ColorShade", look.FoamShade);
            foam.SetColor("_ColorRim", look.FoamRim);
            foam.enableInstancing = true;
            EditorUtility.SetDirty(curtain);
            EditorUtility.SetDirty(foam);
            return (curtain, foam);
        }

        /// Crea una cascada que sigue el recorrido dado (en el mundo, de arriba abajo). Devuelve su
        /// componente; sus tramos que caen llevan espuma al pie y una cortina atravesable.
        public static Waterfall Build(Transform parent, string name, IList<Vector3> worldPath, IList<float> widths,
                                      Material curtain, Material foam, float lift = 0.25f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = curtain;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.AddComponent<MeshFilter>();
            var fall = go.AddComponent<Waterfall>();
            var so = new SerializedObject(fall);
            so.FindProperty("lift").floatValue = lift;
            so.ApplyModifiedPropertiesWithoutUndo();

            var local = new Vector3[worldPath.Count];
            for (int i = 0; i < local.Length; i++) local[i] = go.transform.InverseTransformPoint(worldPath[i]);
            var w = new float[widths.Count];
            for (int i = 0; i < w.Length; i++) w[i] = widths[i];
            fall.SetPath(local, w);

            var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            int k = 0;
            foreach (var run in fall.Runs)
            {
                k++;
                Vector3 foot = go.transform.TransformPoint(run.Foot);
                Vector3 top = go.transform.TransformPoint(run.Top);
                Vector3 flow = go.transform.TransformDirection(run.Flow);
                Quaternion facing = Quaternion.LookRotation(flow, Vector3.up);

                var pie = new GameObject($"Pie {k}").transform;
                pie.SetParent(go.transform, false);
                pie.SetPositionAndRotation(foot + flow * lift + Vector3.up * 0.1f, facing);
                FoamBlobs(pie, sphere, foam, run.Width);
                Splashes(pie, sphere, foam, run.Width);
                Mist(pie, sphere, foam, run.Width, run.Height);

                // Labio: unas gotas que saltan donde el agua empieza a caer.
                var labio = new GameObject($"Labio {k}").transform;
                labio.SetParent(go.transform, false);
                labio.SetPositionAndRotation(top + flow * lift, facing);
                LipSpray(labio, sphere, foam, run.Width);

                Zone(go.transform, k, top, foot, flow, run.Width, lift, sphere, foam);
            }
            return fall;
        }

        // ── Piezas ──────────────────────────────────────────────────────────────────────────

        private static void FoamBlobs(Transform parent, Mesh mesh, Material mat, float width)
        {
            var ps = System(parent, "Espuma", mesh, mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 12f * width;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width * 1.1f, 0.05f, 1.6f);
            Grow(ps, 0.25f);
        }

        private static void Splashes(Transform parent, Mesh mesh, Material mat, float width)
        {
            var ps = System(parent, "Salpicaduras", mesh, mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 14f * width;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = Mathf.Max(0.2f, width * 0.4f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Shrink(ps);
        }

        private static void Mist(Transform parent, Mesh mesh, Material mat, float width, float height)
        {
            var ps = System(parent, "Vapor", mesh, mat);
            var main = ps.main;
            float scale = Mathf.Clamp(height / 8f, 0.6f, 2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * scale, 1.4f * scale);
            main.startColor = new Color(0.92f, 0.97f, 1f, 0.85f);
            main.maxParticles = 120;
            var em = ps.emission;
            em.rateOverTime = 3.5f * width;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = Mathf.Max(0.3f, width * 0.45f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Grow(ps, 0.4f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        private static void LipSpray(Transform parent, Mesh mesh, Material mat, float width)
        {
            var ps = System(parent, "Gotas del labio", mesh, mat);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.gravityModifier = 1f;
            var em = ps.emission;
            em.rateOverTime = 5f * width;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width * 0.9f, 0.05f, 0.1f);
            Shrink(ps);
        }

        private static void Zone(Transform parent, int k, Vector3 top, Vector3 foot, Vector3 flow, float width, float lift,
                                 Mesh mesh, Material mat)
        {
            var z = new GameObject($"Cortina {k}");
            z.transform.SetParent(parent, false);
            Vector3 mid = (top + foot) * 0.5f + flow * lift;
            z.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(flow, Vector3.up));
            var box = z.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(width, Mathf.Max(1f, top.y - foot.y) + 0.5f, 1.4f);
            var burst = System(z.transform, "Al cruzar", mesh, mat);
            var main = burst.main;
            // Encendido y sin emitir: solo suelta gotas cuando la zona llama a Emit.
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.gravityModifier = 1.3f;
            var em = burst.emission;
            em.rateOverTime = 0f;
            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            Shrink(burst);
            var zone = z.AddComponent<WaterCurtainZone>();
            zone.Configure(burst, ~0, "");
        }

        private static ParticleSystem System(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startRotationY = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.World;
            return ps;
        }

        private static void Grow(ParticleSystem ps, float startAt)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startAt), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));
        }

        private static void Shrink(ParticleSystem ps)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        }

        private static Material Asset(string path, string shaderName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader) mat.shader = shader;
            return mat;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int cut = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, cut));
            AssetDatabase.CreateFolder(folder.Substring(0, cut), folder.Substring(cut + 1));
        }
    }
}
