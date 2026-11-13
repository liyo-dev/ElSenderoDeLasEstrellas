using System.Collections.Generic;
using UnityEngine;
using B = Kits.StylizedVfx.Editor.StylizedVfxBuilder;

namespace Kits.StylizedVfx.Editor
{
    /// Recetas para que cada efecto tenga firma propia (forma, recorrido y remate) y no solo color:
    /// destellos de lanzar con dirección, impactos con forma, y piezas grandes para ataques de equipo
    /// (trenzas de colores, vórtices, constelaciones, sellos de varios colores).
    public static partial class EnergyRecipes
    {
        // ── Lanzar ──────────────────────────────────────────────────────────────────────────

        /// Cómo sale un hechizo de la mano. El efecto mira hacia donde se lanza (+Z).
        public struct CastStyle
        {
            public float Size;                 // diámetro de la bola del destello
            public int Sparks;                 // piezas que salen en abanico hacia delante
            public Mesh SparkMesh;             // de qué son (null = estrellas)
            public EnergyPalette[] Colors;     // colores de las piezas (null = el del hechizo)
            public int Rings;                  // aros que salen hacia delante, uno tras otro
            public int Cones;                  // lenguas (conos) que salen disparadas
            public int Slashes;                // medias lunas que barren delante de la mano
            public bool VerticalSlashes;       // tajos de arriba abajo, en paralelo (garras)
            public float Light;
        }

        public static void Cast(GameObject root, EnergyPalette p, EnergyMeshes m, CastStyle s)
        {
            float d = s.Size > 0f ? s.Size : 0.3f;
            var rs = new List<Renderer>();
            var t = B.Group(root.transform, "Destello");
            B.Animate(t, B.Flash(), 0.3f);
            rs.Add(B.Sphere(t, "Bola", d, p.Energy));

            for (int i = 0; i < s.Rings; i++)
            {
                var g = B.Group(root.transform, "Aro " + (i + 1), new Vector3(0f, 0f, 0.1f + 0.2f * i), Quaternion.Euler(90f, 0f, 0f));
                B.Animate(g, B.Flash(), 0.32f, i * 0.07f);
                rs.Add(B.Piece(g, "Aro", m.Ring, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * d * (1.3f + 0.45f * i)));
            }

            if (s.Cones > 0)
            {
                var c = Burst(s.Cones, 2.5f, 4.5f, d * 0.5f, d * 0.8f, 0.18f, 0.28f);
                c.Shape = ParticleSystemShapeType.Cone; c.Angle = 22f; c.Radius = 0.02f; c.Drag = 3f;
                c.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                B.Particles(root.transform, "Lenguas", m.Spike, p.Energy, c);
            }

            if (s.Sparks > 0)
            {
                var mesh = s.SparkMesh ?? m.Star;
                var colores = s.Colors != null && s.Colors.Length > 0 ? s.Colors : new[] { p };
                int cada = Mathf.Max(1, s.Sparks / colores.Length);
                foreach (var col in colores)
                {
                    var e = Burst(cada, 1.8f, 3.2f, d * 0.22f, d * 0.36f, 0.3f, 0.45f);
                    e.Shape = ParticleSystemShapeType.Cone; e.Angle = 38f; e.Radius = 0.02f; e.Drag = 2f;
                    B.Particles(root.transform, "Piezas " + col.Name, mesh, mesh == m.Star ? col.Spark : col.Energy, e);
                }
            }

            float mitad = (s.Slashes - 1) * 0.5f;
            for (int i = 0; i < s.Slashes; i++)
            {
                float k = i - mitad;
                var pos = new Vector3(s.VerticalSlashes ? k * d * 0.9f : 0f, 0f, 0.25f);
                var rot = Quaternion.Euler(0f, 0f, s.VerticalSlashes ? 90f + k * 8f : k * 30f);
                var g = B.Group(root.transform, "Tajo " + (i + 1), pos, rot);
                B.Animate(g, B.Flash(), 0.28f, i * 0.05f);
                rs.Add(B.Piece(g, "Media luna", m.Arc ?? m.Ring, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * d * 2.2f));
            }

            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light), null, s.Light > 0f ? s.Light : 3f, 2.5f, 0, 0, 0,
                  0.3f + 0.07f * Mathf.Max(s.Rings, s.Slashes), 0.25f, B.LightFlash(0.35f));
        }

        // ── Impactos con forma ──────────────────────────────────────────────────────────────

        /// Qué deja un golpe.
        public struct ImpactStyle
        {
            public float Scale;
            public Mesh Debris;                // lo que sale despedido (null = estrellas)
            public EnergyPalette Puffs;        // bocanadas (humo de dibujo, polvo)
            public int Waves;                  // ondas, una tras otra (0 = las dos de siempre a la vez)
            public bool Cross;                 // destello en cruz (tres rayos en los tres ejes)
            public int Marks;                  // tajos que se quedan un momento (garras, cortes)
            public EnergyPalette[] Shards;     // esquirlas de cristal, una tanda por color
            public int RisingStars;            // estrellitas que suben al final
        }

        public static void Impact(GameObject root, EnergyPalette p, EnergyMeshes m, ImpactStyle s)
        {
            float sc = s.Scale > 0f ? s.Scale : 1f;
            var rs = new List<Renderer>();
            var d = B.Group(root.transform, "Destello");
            B.Animate(d, B.Flash(), 0.3f);
            rs.Add(B.Sphere(d, "Bola", (s.Cross ? 0.45f : 0.7f) * sc, p.Energy));

            bool seguidas = s.Waves > 0;
            int ondas = seguidas ? s.Waves : 2;
            for (int i = 0; i < ondas; i++)
            {
                float radio = i == 0 ? 0.9f : (i == 1 ? 0.75f : 0.9f + 0.3f * (i - 1));
                var rot = i == 0 ? Quaternion.identity : (i == 1 ? Quaternion.Euler(60f, 30f, 0f) : Quaternion.Euler(-30f * i, 70f * i, 0f));
                rs.Add(Wave(root.transform, m, "Onda " + (i + 1), radio * sc * (seguidas ? 1f + 0.25f * i : 1f), rot, p.Piece, 0.45f, 0f,
                            seguidas ? i * 0.13f : 0f));
            }

            if (s.Shards == null || s.Debris != null)
            {
                var est = Burst(8, 3f * sc, 4.5f * sc, 0.08f * sc, 0.13f * sc, 0.35f, 0.55f);
                est.Drag = 3f; est.Gravity = 0.3f;
                var mesh = s.Debris ?? m.Star;
                B.Particles(root.transform, "Piezas", mesh, mesh == m.Star ? p.Spark : p.Energy, est);
            }
            if (s.Shards != null)
            {
                foreach (var col in s.Shards)
                {
                    var e = Burst(4, 3f * sc, 5f * sc, 0.12f * sc, 0.2f * sc, 0.4f, 0.6f);
                    e.Drag = 2.5f; e.Gravity = 0.6f;
                    B.Particles(root.transform, "Esquirlas " + col.Name, m.Crystal ?? m.Spike, col.Energy, e);
                }
            }

            if (s.Cross)
            {
                var c = B.Group(root.transform, "Destello en cruz");
                B.Animate(c, B.Flash(), 0.35f);
                float largo = 1.7f * sc, fino = 0.07f * sc;
                rs.Add(B.Box(c, "Rayo X", new Vector3(largo, fino, fino), p.Spark, Vector3.zero, Quaternion.identity));
                rs.Add(B.Box(c, "Rayo Y", new Vector3(fino, largo, fino), p.Spark, Vector3.zero, Quaternion.identity));
                rs.Add(B.Box(c, "Rayo Z", new Vector3(fino, fino, largo), p.Spark, Vector3.zero, Quaternion.identity));
            }

            for (int i = 0; i < s.Marks; i++)
            {
                var g = B.Group(root.transform, "Marca " + (i + 1), Vector3.zero, Quaternion.Euler(0f, i * 180f / s.Marks, 0f));
                var h = B.Group(g, "Tajo", Vector3.zero, Quaternion.Euler(0f, 0f, 90f));
                B.Animate(h, B.PopIn(), 0.12f, i * 0.05f);
                rs.Add(B.Piece(h, "Media luna", m.Arc ?? m.Ring, p.Energy, Vector3.zero, Quaternion.identity, Vector3.one * 0.7f * sc));
            }

            if (s.RisingStars > 0)
            {
                var sube = Burst(s.RisingStars, 0.3f, 0.8f, 0.07f * sc, 0.12f * sc, 0.7f, 1f);
                sube.Rise = 1.6f; sube.Shape = ParticleSystemShapeType.Sphere; sube.Radius = 0.4f * sc;
                B.Particles(root.transform, "Estrellitas que suben", m.Star, p.Spark, sube);
            }

            if (s.Puffs != null)
            {
                var bolas = Burst(6, 1.5f * sc, 2.2f * sc, 0.2f * sc, 0.3f * sc, 0.45f, 0.6f);
                bolas.Drag = 3f;
                B.Particles(root.transform, "Bolas", m.Puff, s.Puffs.Energy, bolas);
            }

            float fin = 0.35f + (seguidas ? 0.13f * (ondas - 1) : 0f) + (s.Marks > 0 ? 0.3f : 0f);
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light), null, 4f, 3.5f * sc, 0, 0, 0, fin, 0.25f, B.LightFlash(0.5f));
        }

        // ── Piezas grandes (ataques de equipo) ─────────────────────────────────────────────

        /// Hebras de varios colores que suben girando entre sí: las magias de cada uno que se juntan.
        public static void Braid(GameObject root, EnergyPalette[] colors, EnergyMeshes m, float radius, float height, float rate)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                var col = colors[i];
                var g = B.Group(root.transform, "Hebra " + col.Name, Vector3.zero, Quaternion.Euler(0f, i * 360f / colors.Length, 0f));
                var s = Settings(height / 3.2f, height / 3f, 0f, 0f, 0.1f, 0.16f);
                s.Loop = true; s.Rate = rate; s.Rise = 3.2f; s.Orbital = 3.5f; s.Tumble = true; s.MaxParticles = 120;
                s.Shape = ParticleSystemShapeType.Circle; s.Radius = radius;
                s.SizeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 6f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.2f));
                B.Particles(g, "Estrellas", m.Star, col.Spark, s);
                var b = s; b.Rate = rate * 0.6f; b.SizeMin = 0.14f; b.SizeMax = 0.22f;
                B.Particles(g, "Bolitas", m.Sphere, col.Energy, b);
            }
        }

        /// Vórtice que se traga lo de alrededor: núcleo oscuro dentro de una burbuja, anillos que vuelcan
        /// y estrellas que caen hacia dentro. Se queda mientras dure el efecto.
        public static void Vortex(GameObject root, EnergyPalette p, EnergyPalette core, EnergyMeshes m, float diameter)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            var t = B.Group(root.transform, "Vórtice");
            B.Animate(t, B.PopIn(), 0.45f);
            var n = B.Sphere(t, "Núcleo", diameter * 0.55f, core.Energy);
            spins.Add(new Spin(n.transform, Vector3.up, 120f));
            rs.Add(n);
            rs.Add(B.Sphere(t, "Burbuja", diameter, p.Shell));
            OrbitingRing(t, p, m, "A", diameter * 0.7f, 75f, 0f, 220f, 420f, diameter * 0.06f, spins, rs);
            OrbitingRing(t, p, m, "B", diameter * 0.8f, 40f, 90f, -180f, -380f, diameter * 0.06f, spins, rs);
            OrbitingRing(t, p, m, "C", diameter * 0.9f, 10f, 200f, 150f, 300f, diameter * 0.06f, spins, rs, m.Ring);
            var entran = Settings(0.5f, 0.6f, -diameter * 3f, -diameter * 3f, 0.08f, 0.14f);
            entran.Loop = true; entran.Rate = 40f; entran.Tumble = true; entran.Shape = ParticleSystemShapeType.Sphere; entran.Radius = diameter * 2.2f;
            entran.Orbital = 2f; entran.MaxParticles = 80;
            B.Particles(t, "Estrellas que caen dentro", m.Star, p.Spark, entran);
            Pulse(root, rs.ToArray(), B.PointLight(t, p.Light), spins.ToArray(), 3f, diameter * 4f + 2f, 0.5f, 0.35f, 0.15f);
        }

        /// Constelación en el suelo: estrellas que aparecen una a una y se unen con trazos.
        public static void Constellation(GameObject root, EnergyPalette p, EnergyMeshes m, float radius, int points, int seed)
        {
            var rs = new List<Renderer>();
            var spins = new List<Spin>();
            var azar = new System.Random(seed);
            var c = B.Group(root.transform, "Constelación", new Vector3(0f, 0.05f, 0f));
            var puntos = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float a = i * 360f / points + (float)azar.NextDouble() * 30f;
                float r = radius * (0.45f + (float)azar.NextDouble() * 0.55f);
                puntos[i] = Quaternion.Euler(0f, a, 0f) * new Vector3(r, 0f, 0f);
                var g = B.Group(c, "Estrella " + (i + 1), puntos[i] + new Vector3(0f, 0.25f, 0f));
                B.Animate(g, B.PopIn(), 0.2f, i * 0.08f);
                var e = B.Piece(g, "Estrella", m.Star, p.Spark, Vector3.zero, Quaternion.identity, Vector3.one * (0.45f / Size(m.Star)));
                spins.Add(new Spin(e.transform, Vector3.up, 160f));
                rs.Add(e);
            }
            for (int i = 0; i < points; i++)
            {
                Vector3 a = puntos[i], b = puntos[(i + 1) % points];
                var dir = b - a;
                var g = B.Group(c, "Trazo " + (i + 1), (a + b) * 0.5f, Quaternion.LookRotation(dir.normalized, Vector3.up));
                B.Animate(g, B.PopIn(), 0.2f, 0.08f * points + i * 0.05f, new Vector3(0f, 0f, 1f));
                rs.Add(B.Box(g, "Línea", new Vector3(0.06f, 0.03f, dir.magnitude), p.Piece, Vector3.zero, Quaternion.identity));
            }
            Pulse(root, rs.ToArray(), B.PointLight(c, p.Light, new Vector3(0f, 0.6f, 0f)), spins.ToArray(), 2f, radius * 2f + 2f, 0.9f, 0.3f, 0.2f);
        }

        /// Sello de varias puntas en el suelo, una de cada color, dentro de un aro: varios que se unen en uno.
        public static void Sigil(GameObject root, EnergyPalette ring, EnergyPalette[] blades, EnergyMeshes m, float radius)
        {
            var rs = new List<Renderer>();
            var spins = new List<Spin>();
            var c = B.Group(root.transform, "Sello", new Vector3(0f, 0.04f, 0f));
            B.Animate(c, B.PopIn(), 0.4f);
            var gira = B.Group(c, "Puntas");
            spins.Add(new Spin(gira, Vector3.up, 30f));
            rs.Add(B.Piece(c, "Aro", m.Ring, ring.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius));
            rs.Add(B.Piece(c, "Aro interior", m.Ring, ring.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius * 0.35f));
            for (int i = 0; i < blades.Length; i++)
            {
                float a = i * 360f / blades.Length;
                var g = B.Group(gira, "Punta " + blades[i].Name, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, radius * 0.5f), Quaternion.Euler(0f, a, 0f));
                B.Animate(g, B.PopIn(), 0.25f, 0.15f + i * 0.12f, new Vector3(0f, 0f, 1f));
                // Rombo alargado: dos cajas en aspa muy estrechas, tumbadas en el suelo.
                rs.Add(B.Box(g, "Hoja", new Vector3(radius * 0.22f, 0.04f, radius * 0.95f), blades[i].Energy, Vector3.zero, Quaternion.identity));
                rs.Add(B.Box(g, "Filo", new Vector3(radius * 0.06f, 0.05f, radius * 0.9f), blades[i].Spark, new Vector3(0f, 0.02f, 0f), Quaternion.identity));
            }
            Pulse(root, rs.ToArray(), B.PointLight(c, ring.Light, new Vector3(0f, 0.8f, 0f)), spins.ToArray(), 2.5f, radius * 2f + 2f, 0.7f, 0.3f, 0.2f);
        }
    }
}
