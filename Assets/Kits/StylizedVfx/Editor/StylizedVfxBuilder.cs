using UnityEngine;

namespace Kits.StylizedVfx.Editor
{
    /// Piezas para montar efectos de dibujo con volumen desde un constructor de editor: esferas y
    /// mallas con el shader de energía, luces, piezas que saltan o se abren (ScaleOverLife) y
    /// partículas de mallas 3D. El juego decide formas, tamaños y colores.
    public static class StylizedVfxBuilder
    {
        /// Ajustes de un sistema de partículas de mallas.
        public struct MeshParticles
        {
            public float LifetimeMin, LifetimeMax;
            public float SpeedMin, SpeedMax;
            /// Tamaño en metros (se corrige con el tamaño de la malla).
            public float SizeMin, SizeMax;
            public float Rate;
            public int Burst;
            public bool Loop;
            public float Duration;
            public ParticleSystemShapeType Shape;
            public float Radius;
            /// Para formas de cono: ángulo de apertura.
            public float Angle;
            /// Partículas que se quedan en el mundo (estelas) en vez de seguir al efecto.
            public bool World;
            public float Gravity;
            /// Velocidad añadida hacia arriba (humo, estrellas que suben).
            public float Rise;
            /// Giro alrededor del eje vertical del efecto (remolinos).
            public float Orbital;
            /// Frenado (0 = nada).
            public float Drag;
            /// Curva de tamaño con la vida (null = salta, se mantiene y se encoge).
            public AnimationCurve SizeCurve;
            /// Giro libre en 3D de cada partícula.
            public bool Tumble;
            /// Emisión por metro recorrido (estelas), además de la de por segundo.
            public float RatePerMeter;
            public int MaxParticles;
            /// Nacen por todo el volumen de la forma en vez de solo en su borde.
            public bool Volume;
        }

        public static MeshRenderer Sphere(Transform parent, string name, float diameter, Material mat, Vector3 position = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * diameter;
            return NoShadows(go.GetComponent<MeshRenderer>(), mat);
        }

        public static MeshRenderer Cylinder(Transform parent, string name, float diameter, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            return NoShadows(go.GetComponent<MeshRenderer>(), mat);
        }

        public static MeshRenderer Box(Transform parent, string name, Vector3 size, Material mat, Vector3 position, Quaternion rotation)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            return NoShadows(go.GetComponent<MeshRenderer>(), mat);
        }

        public static MeshRenderer Piece(Transform parent, string name, Mesh mesh, Material mat, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            return NoShadows(go.AddComponent<MeshRenderer>(), mat);
        }

        public static Transform Group(Transform parent, string name, Vector3 position = default, Quaternion? rotation = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation ?? Quaternion.identity;
            return t;
        }

        public static Light PointLight(Transform parent, Color color, Vector3 position = default)
        {
            var luz = new GameObject("Luz").AddComponent<Light>();
            luz.transform.SetParent(parent, false);
            luz.transform.localPosition = position;
            luz.type = LightType.Point;
            luz.color = color;
            luz.shadows = LightShadows.None;
            return luz;
        }

        /// La pieza sigue una curva de escala desde que se enciende (va en un hijo, no en la raíz).
        public static ScaleOverLife Animate(Transform piece, AnimationCurve curve, float seconds, float delay = 0f, Vector3? axes = null)
        {
            var s = piece.gameObject.AddComponent<ScaleOverLife>();
            s.Configure(curve, seconds, delay, axes ?? Vector3.one);
            return s;
        }

        /// Salta de 0 a algo más que su tamaño y se asienta.
        public static AnimationCurve PopIn() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 6f), new Keyframe(0.6f, 1.12f), new Keyframe(1f, 1f));

        /// Salta, se mantiene un instante y se encoge a nada (destellos).
        public static AnimationCurve Flash() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 8f), new Keyframe(0.3f, 1f), new Keyframe(0.55f, 0.9f), new Keyframe(1f, 0f));

        /// Se abre rápido y frena al final (ondas).
        public static AnimationCurve Expand(float from = 0.15f) => new AnimationCurve(
            new Keyframe(0f, from, 0f, 4f), new Keyframe(1f, 1f, 0f, 0f));

        /// Luz de golpe: sube en seco y se apaga en «seconds».
        public static AnimationCurve LightFlash(float seconds) => new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.04f, 1f), new Keyframe(seconds, 0f));

        public static ParticleSystem Particles(Transform parent, string name, Mesh mesh, Material mat, MeshParticles s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float tamDeLaMalla = Mathf.Max(0.001f, mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
            var main = ps.main;
            main.loop = s.Loop;
            main.duration = s.Duration > 0f ? s.Duration : 1f;
            main.playOnAwake = true;
            main.useUnscaledTime = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = s.World ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.LifetimeMin, Mathf.Max(s.LifetimeMin, s.LifetimeMax));
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.SpeedMin, s.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin / tamDeLaMalla, Mathf.Max(s.SizeMin, s.SizeMax) / tamDeLaMalla);
            main.gravityModifier = s.Gravity;
            main.maxParticles = s.MaxParticles > 0 ? s.MaxParticles : 60;
            if (s.Tumble)
            {
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var giro = ps.rotationOverLifetime;
                giro.enabled = true;
                giro.separateAxes = true;
                giro.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
                giro.y = new ParticleSystem.MinMaxCurve(-3f, 3f);
                giro.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            }

            var emision = ps.emission;
            emision.rateOverTime = s.Rate;
            emision.rateOverDistance = s.RatePerMeter;
            if (s.Burst > 0) emision.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.Burst) });

            var forma = ps.shape;
            forma.enabled = true;
            forma.shapeType = s.Shape;
            forma.radius = Mathf.Max(0.0001f, s.Radius);
            forma.angle = s.Angle;
            forma.radiusThickness = s.Volume ? 1f : 0f;
            if (s.Shape == ParticleSystemShapeType.Circle) forma.rotation = new Vector3(-90f, 0f, 0f);

            if (s.Rise != 0f || s.Orbital != 0f)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(0f);
                vel.y = new ParticleSystem.MinMaxCurve(s.Rise);
                vel.z = new ParticleSystem.MinMaxCurve(0f);
                vel.orbitalY = new ParticleSystem.MinMaxCurve(s.Orbital);
            }

            if (s.Drag > 0f)
            {
                var limite = ps.limitVelocityOverLifetime;
                limite.enabled = true;
                limite.drag = s.Drag;
            }

            var tam = ps.sizeOverLifetime;
            tam.enabled = true;
            tam.size = new ParticleSystem.MinMaxCurve(1f, s.SizeCurve ?? new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f)));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = mesh;
            r.alignment = ParticleSystemRenderSpace.Local;
            NoShadows(r, mat);
            return ps;
        }

        public static T NoShadows<T>(T r, Material mat) where T : Renderer
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
    }
}
