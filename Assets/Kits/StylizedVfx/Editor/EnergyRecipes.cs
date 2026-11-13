using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using B = Kits.StylizedVfx.Editor.StylizedVfxBuilder;

namespace Kits.StylizedVfx.Editor
{
    /// Juego de colores de un efecto: los cuatro materiales del kit (energía opaca, piezas finas,
    /// chispas y burbuja) y el color de su luz. Lo monta el juego con StylizedVfxAssets.
    public sealed class EnergyPalette
    {
        public string Name;
        public Material Energy, Piece, Spark, Shell;
        public Color Light = Color.white;
    }

    /// Mallas que usan las recetas. El juego las carga o las crea (StylizedVfxAssets.Torus/Cone).
    public sealed class EnergyMeshes
    {
        public Mesh Star, Puff, Sphere, Ring, OrbitRing, Spike, Petal;
        /// Opcionales (StylizedVfxMeshes): cristal facetado y media luna. Si faltan, las recetas usan Spike y OrbitRing.
        public Mesh Crystal, Arc;
    }

    /// Recetas de efectos de dibujo con volumen: cada una monta, dentro de una raíz, un efecto completo
    /// (piezas, partículas de mallas, luz y su EnergyPulse) a partir de una paleta y unos tamaños.
    /// Reglas de la receta: piezas 3D sombreadas, que se cruzan y giran en varios ejes, luz que late,
    /// lo del suelo apoyado en el suelo, aparecer y desaparecer a mordiscos.
    public static partial class EnergyRecipes
    {
        // ── Reconstruir en su sitio ─────────────────────────────────────────────────────────

        /// Rehace un prefab conservando su raíz (y con ella su GUID y lo que otros prefabs, como
        /// variantes, tengan enganchado a ella): quita hijos y componentes visuales y vuelve a montar.
        /// «keep» decide qué componentes de la raíz se quedan (por defecto, colisiones y física).
        public static void RebuildInPlace(string path, string rootName, Action<GameObject> build, Func<Component, bool> keep = null)
        {
            keep ??= c => c is Collider || c is Rigidbody;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                StylizedVfxAssets.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
                var nuevo = new GameObject(rootName);
                try { build(nuevo); PrefabUtility.SaveAsPrefabAsset(nuevo, path); }
                finally { UnityEngine.Object.DestroyImmediate(nuevo); }
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                // Primero lo que depende de otros (sistemas de partículas antes que su renderer).
                for (int vuelta = 0; vuelta < 3; vuelta++)
                {
                    foreach (var c in root.GetComponents<Component>())
                    {
                        if (c == null || c is Transform || keep(c)) continue;
                        if (c is ParticleSystemRenderer && root.GetComponent<ParticleSystem>() != null) continue;
                        UnityEngine.Object.DestroyImmediate(c);
                    }
                }
                root.transform.localScale = Vector3.one;
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ── Recetas ─────────────────────────────────────────────────────────────────────────

        /// Destello de lanzar o de desaparecer: bola que salta y se va, estrellitas y luz de golpe.
        public static void Flash(GameObject root, EnergyPalette p, EnergyMeshes m, float diameter, int sparks, float light = 3f)
        {
            var t = B.Group(root.transform, "Destello");
            B.Animate(t, B.Flash(), 0.35f);
            var bola = B.Sphere(t, "Bola", diameter, p.Energy);
            if (sparks > 0)
                B.Particles(root.transform, "Estrellitas", m.Star, p.Spark,
                            Burst(sparks, 1.5f, 2.5f, diameter * 0.2f, diameter * 0.33f, 0.3f, 0.45f));
            Pulse(root, new Renderer[] { bola }, B.PointLight(root.transform, p.Light), null, light, 2.5f, 0, 0, 0, 0, 0.3f, B.LightFlash(0.35f));
        }

        /// Forma de lo que viaja.
        public enum ProjectileShape
        {
            Orb = 0,        // bola (estirada si Elongated)
            Crystal = 1,    // cristal facetado que gira
            Crescent = 2,   // media luna plana que corta el aire
            Heart = 3,      // corazón (dos bolas y una punta)
            Needle = 4,     // aguja fina con punta
            Claws = 5,      // tres tajos en paralelo
        }

        /// Cómo es un proyectil.
        public struct ProjectileStyle
        {
            public float Core;              // diámetro del núcleo (o ancho de la forma)
            public ProjectileShape Shape;
            public int Rings;               // anillos que vuelcan alrededor
            public int Beads;               // cuentas que orbitan (mente)
            public bool Flames;             // lenguas de fuego que se quedan atrás (conos)
            public bool StarCore;           // el núcleo lleva una estrella grande girando
            public bool Elongated;          // dardo: núcleo estirado en el sentido de la marcha
            public bool SpiralRings;        // los anillos giran alrededor del eje de la marcha (viento)
            public int Echoes;              // aros en fila, cada uno más débil, que se repiten detrás
            public float CometTail;         // metros de cola (cono) detrás del núcleo; 0 = sin cola
            public Mesh TrailMesh;          // de qué es la estela (null = esferas)
            public float TrailRate;         // piezas por metro
            public EnergyPalette[] TrailColors; // estelas de más, una por color (prismas)
            public WobbleMode Wobble;       // recorrido que se ve (el proyectil va recto)
            public float WobbleAmplitude;
            public float WobbleFrequency;
            public float Light;
        }

        /// Lo que viaja: una forma sombreada, anillos o cuentas que la rodean y estela que se queda atrás.
        /// Con vaivén, todo lo que se ve va dentro de un hijo que se mueve y la estela dibuja el recorrido.
        public static void Projectile(GameObject root, EnergyPalette p, EnergyMeshes m, ProjectileStyle s)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            float c = s.Core;
            Transform body = root.transform;
            if (s.Wobble != WobbleMode.None)
            {
                body = B.Group(root.transform, "Vaivén");
                body.gameObject.AddComponent<PathWobble>().Configure(s.Wobble, s.WobbleAmplitude, s.WobbleFrequency > 0f ? s.WobbleFrequency : 3f, true);
            }

            switch (s.Shape)
            {
                case ProjectileShape.Crystal:
                {
                    var cristal = B.Piece(body, "Cristal", m.Crystal ?? m.Spike, p.Energy, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one * c);
                    spins.Add(new Spin(cristal.transform, Vector3.up, 320f));
                    rs.Add(cristal);
                    rs.Add(B.Sphere(body, "Brillo", c * 0.35f, p.Spark));
                    break;
                }
                case ProjectileShape.Crescent:
                {
                    rs.Add(B.Piece(body, "Media luna", m.Arc ?? m.OrbitRing, p.Energy, Vector3.zero, Quaternion.identity, Vector3.one * c));
                    rs.Add(B.Piece(body, "Filo", m.Arc ?? m.OrbitRing, p.Spark, new Vector3(0f, 0f, c * 0.04f), Quaternion.identity, new Vector3(c * 0.8f, c * 0.5f, c * 0.8f)));
                    break;
                }
                case ProjectileShape.Heart:
                {
                    var g = B.Group(body, "Corazón");
                    spins.Add(new Spin(g, Vector3.up, 90f));
                    rs.Add(B.Sphere(g, "Lóbulo izquierdo", c * 0.62f, p.Energy, new Vector3(-c * 0.26f, c * 0.14f, 0f)));
                    rs.Add(B.Sphere(g, "Lóbulo derecho", c * 0.62f, p.Energy, new Vector3(c * 0.26f, c * 0.14f, 0f)));
                    float r = c * 0.56f / 0.18f;
                    rs.Add(B.Piece(g, "Punta", m.Spike, p.Energy, new Vector3(0f, c * 0.06f, 0f), Quaternion.Euler(180f, 0f, 0f), new Vector3(r, c * 0.62f, r * 0.55f)));
                    rs.Add(B.Sphere(g, "Brillo", c * 0.25f, p.Spark, new Vector3(-c * 0.3f, c * 0.28f, -c * 0.2f)));
                    break;
                }
                case ProjectileShape.Needle:
                {
                    var aguja = B.Sphere(body, "Aguja", 1f, p.Energy);
                    aguja.transform.localScale = new Vector3(c * 0.3f, c * 0.3f, c * 2.6f);
                    rs.Add(aguja);
                    float r = c * 0.2f / 0.18f;
                    rs.Add(B.Piece(body, "Punta", m.Spike, p.Spark, new Vector3(0f, 0f, c * 1.1f), Quaternion.Euler(90f, 0f, 0f), new Vector3(r, c * 1.1f, r)));
                    break;
                }
                case ProjectileShape.Claws:
                {
                    for (int i = 0; i < 3; i++)
                        rs.Add(B.Piece(body, "Garra " + (i + 1), m.Arc ?? m.OrbitRing, p.Energy, new Vector3((i - 1) * c * 0.32f, 0f, i == 1 ? c * 0.12f : 0f),
                                       Quaternion.Euler(0f, 0f, 90f + (i - 1) * 10f), Vector3.one * c * 0.9f));
                    break;
                }
                default:
                {
                    var nucleo = B.Sphere(body, "Núcleo", c, p.Energy);
                    if (s.Elongated) nucleo.transform.localScale = new Vector3(c * 0.6f, c * 0.6f, c * 1.8f);
                    spins.Add(new Spin(nucleo.transform, new Vector3(0.2f, 1f, 0.4f).normalized, 220f));
                    rs.Add(nucleo);
                    break;
                }
            }
            if (s.StarCore)
            {
                var est = B.Piece(body, "Estrella", m.Star, p.Spark, Vector3.zero, Quaternion.identity, Vector3.one * (c * 1.5f / Size(m.Star)));
                spins.Add(new Spin(est.transform, Vector3.forward, 360f));
                rs.Add(est);
            }
            if (s.CometTail > 0f)
            {
                float r = c * 0.42f / 0.18f;
                rs.Add(B.Piece(body, "Cola", m.Spike, p.Energy, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), new Vector3(r, s.CometTail, r)));
                rs.Add(B.Piece(body, "Cola brillante", m.Spike, p.Spark, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), new Vector3(r * 0.45f, s.CometTail * 0.7f, r * 0.45f)));
            }
            for (int i = 0; i < s.Rings; i++)
            {
                if (s.SpiralRings)
                {
                    // Aros que giran alrededor del eje de la marcha, uno detrás de otro: un remolino que avanza.
                    var g = B.Group(body, "Espiral " + (i + 1), new Vector3(0f, 0f, -i * c * 0.55f), Quaternion.Euler(90f, 0f, 0f));
                    var aro = B.Piece(g, "Aro", m.OrbitRing, p.Piece, Vector3.zero, Quaternion.Euler(0f, 0f, 12f), Vector3.one * c * (0.8f + 0.25f * i));
                    spins.Add(new Spin(g, Vector3.up, 540f * (i % 2 == 0 ? 1 : -1)));
                    rs.Add(aro);
                }
                else
                {
                    OrbitingRing(body, p, m, ((char)('A' + i)).ToString(), c * (0.75f + 0.15f * i), 70f - i * 25f, i * 90f,
                                 260f * (i % 2 == 0 ? 1 : -1), 420f, c * 0.12f, spins, rs);
                }
            }
            for (int i = 0; i < s.Beads; i++)
            {
                var g = B.Group(body, "Órbita " + (i + 1), Vector3.zero, Quaternion.Euler(30f * i, 120f * i, 0f));
                spins.Add(new Spin(g, Vector3.up, 400f));
                rs.Add(B.Sphere(g, "Cuenta", c * 0.28f, p.Spark, new Vector3(c * 0.95f, 0f, 0f)));
            }
            for (int i = 0; i < s.Echoes; i++)
            {
                // Cada copia, más atrás, más pequeña y más apagada: el mismo aro que se repite.
                var g = B.Group(body, "Eco " + (i + 1), new Vector3(0f, 0f, -i * c * 2.4f), Quaternion.Euler(90f, 0f, 0f));
                spins.Add(new Spin(g, Vector3.up, 300f * (i % 2 == 0 ? 1 : -1)));
                float k = Mathf.Pow(0.72f, i);
                rs.Add(B.Piece(g, "Aro", m.OrbitRing, i == 0 ? p.Energy : p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * c * 1.7f * k));
            }
            if (s.Flames)
            {
                var lenguas = Settings(0.22f, 0.3f, 0f, 0f, c * 0.8f, c * 1.2f);
                lenguas.World = true; lenguas.Loop = true; lenguas.RatePerMeter = 10f; lenguas.Shape = ParticleSystemShapeType.Sphere; lenguas.Radius = c * 0.2f;
                lenguas.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                var ps = B.Particles(body, "Lenguas", m.Spike, p.Energy, lenguas);
                // Los conos miran hacia atrás (la punta, contra la marcha).
                ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }
            var estela = Settings(0.25f, 0.35f, 0f, 0f, c * 0.45f, c * 0.7f);
            estela.World = true; estela.Loop = true; estela.RatePerMeter = s.TrailRate > 0 ? s.TrailRate : 12f; estela.MaxParticles = 90;
            estela.Shape = ParticleSystemShapeType.Sphere; estela.Radius = c * 0.1f; estela.Tumble = s.TrailMesh != null && s.TrailMesh != m.Sphere;
            estela.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var malla = s.TrailMesh ?? m.Sphere;
            B.Particles(body, "Estela", malla, malla == m.Star ? p.Spark : p.Energy, estela);
            if (s.TrailColors != null)
            {
                foreach (var col in s.TrailColors)
                {
                    var e = estela; e.Radius = c * 0.35f; e.RatePerMeter = estela.RatePerMeter * 0.6f;
                    B.Particles(body, "Estela " + col.Name, malla, malla == m.Star ? col.Spark : col.Energy, e);
                }
            }
            Pulse(root, rs.ToArray(), B.PointLight(body, p.Light), spins.ToArray(), s.Light > 0 ? s.Light : 2.2f, 2.5f, 0.5f, 0.3f, 0.08f);
        }

        /// Donde da: destello, dos ondas que se abren y piezas que salen despedidas.
        public static void Impact(GameObject root, EnergyPalette p, EnergyMeshes m, float scale, Mesh debris = null, EnergyPalette puffs = null)
        {
            var rs = new List<Renderer>();
            var d = B.Group(root.transform, "Destello");
            B.Animate(d, B.Flash(), 0.3f);
            rs.Add(B.Sphere(d, "Bola", 0.7f * scale, p.Energy));
            rs.Add(Wave(root.transform, m, "Onda", 0.9f * scale, Quaternion.identity, p.Piece, 0.45f));
            rs.Add(Wave(root.transform, m, "Onda inclinada", 0.75f * scale, Quaternion.Euler(60f, 30f, 0f), p.Piece, 0.45f));
            var est = Burst(8, 3f * scale, 4.5f * scale, 0.08f * scale, 0.13f * scale, 0.35f, 0.55f);
            est.Drag = 3f; est.Gravity = 0.3f;
            var mesh = debris ?? m.Star;
            B.Particles(root.transform, "Piezas", mesh, mesh == m.Star ? p.Spark : p.Energy, est);
            if (puffs != null)
            {
                var bolas = Burst(6, 1.5f * scale, 2.2f * scale, 0.2f * scale, 0.3f * scale, 0.45f, 0.6f);
                bolas.Drag = 3f;
                B.Particles(root.transform, "Bolas", m.Puff, puffs.Energy, bolas);
            }
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light), null, 4f, 3.5f * scale, 0, 0, 0, 0.35f, 0.25f, B.LightFlash(0.5f));
        }

        /// Burbuja alrededor de alguien (la raíz a sus pies). «persistent»: no se va sola (escudos que se sostienen).
        public static void Shield(GameObject root, EnergyPalette p, EnergyMeshes m, float center, float diameter, bool persistent)
        {
            var t = B.Group(root.transform, "Burbuja", new Vector3(0f, center, 0f));
            B.Animate(t, B.PopIn(), 0.22f);
            var burbuja = B.Sphere(t, "Esfera", diameter, p.Shell);
            var spins = new List<Spin>();
            var rs = new List<Renderer> { burbuja };
            // Dos aros finos que la recorren: se ve que es una esfera, no un disco.
            OrbitingRing(t, p, m, "A", diameter * 0.52f, 70f, 0f, 60f, 140f, diameter * 0.04f, spins, rs);
            OrbitingRing(t, p, m, "B", diameter * 0.53f, 55f, 120f, -50f, -120f, diameter * 0.04f, spins, rs);
            var est = Burst(6, 1.2f, 2f, 0.06f, 0.1f, 0.3f, 0.45f);
            est.Shape = ParticleSystemShapeType.Sphere; est.Radius = diameter * 0.47f;
            B.Particles(t, "Estrellitas", m.Star, p.Spark, est);
            Pulse(root, rs.ToArray(), B.PointLight(t, p.Light), spins.ToArray(), 1.4f, diameter * 1.5f, 0.6f, 0.3f, 0.12f,
                  persistent ? 0f : 0.55f, 0.3f);
        }

        /// Círculo mágico apoyado en el suelo: aro con rombos y estrella de seis puntas que giran al revés.
        public static void MagicCircle(GameObject root, EnergyPalette p, EnergyMeshes m, float radius, bool risingSparks = true, float light = 0.9f)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            var c = B.Group(root.transform, "Círculo", new Vector3(0f, 0.03f, 0f));
            B.Animate(c, B.PopIn(), 0.4f);
            var fuera = B.Group(c, "Aro y rombos");
            spins.Add(new Spin(fuera, Vector3.up, 25f));
            rs.Add(B.Piece(fuera, "Aro exterior", m.Ring, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius));
            rs.Add(B.Piece(fuera, "Aro interior", m.Ring, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius * 0.87f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                rs.Add(B.Box(fuera, "Rombo " + (i + 1), new Vector3(0.06f, 0.02f, 0.06f) * Mathf.Max(1f, radius), p.Piece,
                             Quaternion.Euler(0f, a, 0f) * new Vector3(radius * 1.09f, 0f, 0f), Quaternion.Euler(0f, a + 45f, 0f)));
            }
            var estrella = B.Group(c, "Estrella de seis puntas");
            spins.Add(new Spin(estrella, Vector3.up, -35f));
            float rv = radius * 0.82f;
            for (int tri = 0; tri < 2; tri++)
                for (int k = 0; k < 3; k++)
                {
                    Vector3 v0 = Vertex(tri * 60f + k * 120f, rv), v1 = Vertex(tri * 60f + (k + 1) * 120f, rv);
                    var dir = v1 - v0;
                    rs.Add(B.Box(estrella, $"Trazo {tri * 3 + k + 1}", new Vector3(0.03f * Mathf.Max(1f, radius), 0.02f, dir.magnitude), p.Piece,
                                 (v0 + v1) * 0.5f, Quaternion.LookRotation(dir.normalized, Vector3.up)));
                }
            if (risingSparks)
            {
                var suben = Settings(0.9f, 1.3f, 0f, 0f, 0.05f, 0.08f);
                suben.Loop = true; suben.Rate = 6f * Mathf.Max(1f, radius); suben.Rise = 0.7f; suben.Tumble = true;
                suben.Shape = ParticleSystemShapeType.Circle; suben.Radius = radius * 0.95f;
                B.Particles(c, "Estrellitas que suben", m.Star, p.Spark, suben);
            }
            Pulse(root, rs.ToArray(), B.PointLight(c, p.Light, new Vector3(0f, 0.35f, 0f)), spins.ToArray(), light, 2.2f * Mathf.Max(1f, radius * 0.6f), 1.6f, 0.3f, 0.3f);
        }

        /// Lenguas de fuego (conos) que brotan del suelo por todo el círculo y se encogen.
        public static void GroundFlames(GameObject root, EnergyPalette p, EnergyMeshes m, float radius, float height, float rate)
        {
            var g = B.Group(root.transform, "Llamas");
            var s = Settings(0.45f, 0.7f, 0f, 0f, height * 0.7f, height);
            s.Loop = true; s.Rate = rate; s.Shape = ParticleSystemShapeType.Circle; s.Radius = radius; s.Volume = true; s.World = false;
            s.SizeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 9f), new Keyframe(0.2f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f));
            B.Particles(g, "Lenguas", m.Spike, p.Energy, s);
            var chis = Settings(0.6f, 0.9f, 0.5f, 1f, 0.05f, 0.09f);
            chis.Loop = true; chis.Rate = rate * 0.6f; chis.Rise = 1.6f; chis.Tumble = true; chis.Shape = ParticleSystemShapeType.Circle; chis.Radius = radius; chis.Volume = true;
            B.Particles(g, "Chispas", m.Star, p.Spark, chis);
            Pulse(root, new Renderer[0], B.PointLight(root.transform, p.Light, new Vector3(0f, height * 0.6f, 0f)), null, 2.2f, radius * 2.2f + 1.5f, 0.25f, 0.45f, 0f);
        }

        /// Embudo de aros que giran (remolinos, tornados), con estrellitas que suben en espiral.
        public static void Funnel(GameObject root, EnergyPalette p, EnergyMeshes m, float radius, float height, int levels, float spinSpeed = 220f)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            for (int i = 0; i < levels; i++)
            {
                float k = levels > 1 ? i / (float)(levels - 1) : 0f;
                var nivel = B.Group(root.transform, "Nivel " + (i + 1), new Vector3(0f, 0.1f + height * k, 0f));
                float sentido = (i & 1) == 0 ? 1f : -1f;
                OrbitingRing(nivel, p, m, ((char)('A' + i)).ToString(), Mathf.Lerp(radius * 0.4f, radius, k), 10f, i * 70f,
                             sentido * (spinSpeed + i * 30f), sentido * 260f, 0.05f + radius * 0.02f, spins, rs, m.Ring);
            }
            var est = Settings(1f, 1.1f, 0f, 0f, 0.06f, 0.1f);
            est.Loop = true; est.Rate = 18f * Mathf.Max(1f, radius); est.Rise = height * 1.2f; est.Orbital = 4f; est.Tumble = true;
            est.Shape = ParticleSystemShapeType.Circle; est.Radius = radius * 0.5f;
            B.Particles(root.transform, "Estrellitas en espiral", m.Star, p.Spark, est);
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light, new Vector3(0f, height * 0.5f, 0f)), spins.ToArray(), 1.5f, radius * 2f + 2f, 0.8f, 0.3f, 0.3f);
        }

        /// Cosas que caen del cielo en el círculo y revientan al tocar el suelo (lluvias, meteoros).
        public static void SkyRain(GameObject root, EnergyPalette p, EnergyMeshes m, float radius, float height, float rate, float size, Mesh falling)
        {
            var cielo = B.Group(root.transform, "Cielo", new Vector3(0f, height, 0f));
            var caen = Settings(height / 14f, height / 14f, 0f, 0f, size * 0.8f, size);
            caen.Loop = true; caen.Rate = rate; caen.Rise = -14f; caen.Tumble = true; caen.World = false;
            caen.Shape = ParticleSystemShapeType.Circle; caen.Radius = radius * 0.85f; caen.Volume = true;
            caen.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1f));
            B.Particles(cielo, "Caen", falling ?? m.Sphere, falling == m.Star ? p.Spark : p.Energy, caen);
            var cola = Settings(0.25f, 0.35f, 0f, 0f, size * 0.4f, size * 0.6f);
            cola.Loop = true; cola.Rate = rate * 6f; cola.Rise = -14f; cola.World = false;
            cola.Shape = ParticleSystemShapeType.Circle; cola.Radius = radius * 0.85f; cola.Volume = true;
            cola.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            B.Particles(cielo, "Colas", m.Sphere, p.Energy, cola);
            var suelo = B.Group(root.transform, "Golpes en el suelo", new Vector3(0f, 0.05f, 0f));
            var golpes = Settings(0.3f, 0.3f, 0f, 0f, size * 2f, size * 2.6f);
            golpes.Loop = true; golpes.Rate = rate; golpes.Shape = ParticleSystemShapeType.Circle; golpes.Radius = radius * 0.85f; golpes.Volume = true;
            golpes.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 4f), new Keyframe(1f, 1f));
            B.Particles(suelo, "Ondas", m.Ring, p.Piece, golpes);
            Pulse(root, new Renderer[0], B.PointLight(root.transform, p.Light, new Vector3(0f, 1f, 0f)), null, 2.5f, radius * 2f + 2f, 0.3f, 0.5f, 0f);
        }

        /// Onda de luz que se abre una vez: burbuja que crece, aro por el suelo y rayos de estrellas.
        public static void Nova(GameObject root, EnergyPalette p, EnergyMeshes m, float radius)
        {
            var rs = new List<Renderer>();
            var b = B.Group(root.transform, "Burbuja", new Vector3(0f, 0.3f, 0f));
            B.Animate(b, B.Expand(0.05f), 0.6f);
            rs.Add(B.Sphere(b, "Esfera", radius * 2f, p.Shell));
            var d = B.Group(root.transform, "Destello", new Vector3(0f, 0.8f, 0f));
            B.Animate(d, B.Flash(), 0.45f);
            rs.Add(B.Sphere(d, "Bola", Mathf.Min(1.6f, radius * 0.4f), p.Energy));
            rs.Add(Wave(root.transform, m, "Onda", radius, Quaternion.identity, p.Piece, 0.55f, 0.05f));
            var est = Burst(18, radius * 1.4f, radius * 2f, 0.12f, 0.2f, 0.5f, 0.8f);
            est.Drag = 2f; est.Duration = 30f;
            B.Particles(d, "Rayos de estrellas", m.Star, p.Spark, est);
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light, new Vector3(0f, 1f, 0f)), null, 5f, radius * 2.5f, 0, 0, 0, 0.6f, 0.3f, B.LightFlash(0.9f));
        }

        /// Cúpula que se queda sobre el grupo (no se va sola).
        public static void Dome(GameObject root, EnergyPalette p, EnergyMeshes m, float radius)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            var c = B.Group(root.transform, "Cúpula");
            B.Animate(c, B.PopIn(), 0.4f);
            rs.Add(B.Sphere(c, "Esfera", radius * 2f, p.Shell));
            OrbitingRing(c, p, m, "A", radius * 1.01f, 90f, 0f, 20f, 60f, 0.12f, spins, rs, m.Ring);
            OrbitingRing(c, p, m, "B", radius * 1.02f, 30f, 60f, -15f, -60f, 0.12f, spins, rs, m.Ring);
            MagicCircleInto(c, p, m, radius, spins, rs);
            var suben = Settings(0.9f, 1.3f, 0f, 0f, 0.08f, 0.14f);
            suben.Loop = true; suben.Rate = 30f; suben.Rise = 2.5f; suben.Tumble = true; suben.Shape = ParticleSystemShapeType.Circle; suben.Radius = radius * 0.9f; suben.Volume = true;
            B.Particles(c, "Estrellitas", m.Star, p.Spark, suben);
            Pulse(root, rs.ToArray(), B.PointLight(c, p.Light, new Vector3(0f, radius * 0.4f, 0f)), spins.ToArray(), 2f, radius * 2.5f, 1.4f, 0.3f, 0.3f);
        }

        /// Columna de luz que baja o sube (juicios, pilares), con aros que recorren la columna.
        public static void Pillar(GameObject root, EnergyPalette core, EnergyPalette halo, EnergyMeshes m, float coreDiameter, float haloDiameter, float height, bool down)
        {
            var rs = new List<Renderer>();
            var col = B.Group(root.transform, "Columna");
            B.Animate(col, B.PopIn(), 0.25f, 0f, new Vector3(0f, 1f, 0f));
            rs.Add(B.Cylinder(col, "Halo", haloDiameter, height, halo.Shell));
            rs.Add(B.Cylinder(col, "Centro", coreDiameter, height, core.Energy));
            var aros = Settings(height / 8f, height / 8f, 0f, 0f, haloDiameter * 1.3f, haloDiameter * 1.5f);
            aros.Loop = true; aros.Rate = 5f; aros.Rise = down ? -8f : 8f; aros.Shape = ParticleSystemShapeType.Sphere; aros.Radius = 0.001f;
            aros.SizeCurve = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.6f));
            var g = B.Group(root.transform, "Aros", new Vector3(0f, down ? height : 0f, 0f));
            B.Particles(g, "Aros que recorren", m.Ring, halo.Piece, aros);
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, halo.Light, new Vector3(0f, 1.5f, 0f)), null, 4f, haloDiameter * 3f + 3f, 0.5f, 0.25f, 0.1f);
        }

        /// Pinchos de cristal que salen del suelo en corona y se quedan (grietas).
        public static void Spikes(GameObject root, EnergyPalette p, EnergyPalette dust, EnergyMeshes m, int count, float radius, float height, int seed)
        {
            var rs = new List<Renderer>();
            var azar = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                float a = i * (360f / count) + (float)azar.NextDouble() * 25f;
                float r = radius * (0.35f + (float)azar.NextDouble() * 0.65f);
                var pos = Quaternion.Euler(0f, a, 0f) * new Vector3(r, 0f, 0f);
                var g = B.Group(root.transform, "Pincho " + (i + 1), pos, Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(0f, 0f, -(15f + (float)azar.NextDouble() * 20f)));
                g.localScale = new Vector3(1f, height * (0.6f + (float)azar.NextDouble() * 0.6f), 1f);
                B.Animate(g, B.PopIn(), 0.18f, i * 0.04f, new Vector3(0f, 1f, 0f));
                rs.Add(B.Piece(g, "Cristal", m.Spike, p.Energy, Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Max(1f, radius * 0.4f)));
            }
            if (dust != null)
            {
                var bolas = Burst(10, 1f, 2f, 0.2f, 0.34f, 0.5f, 0.7f);
                bolas.Drag = 3f; bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = radius * 0.6f; bolas.Duration = 30f;
                B.Particles(root.transform, "Polvo", m.Puff, dust.Energy, bolas);
            }
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light, new Vector3(0f, 0.5f, 0f)), null, 2.5f, radius * 2f + 2f, 0.4f, 0.4f, 0.1f);
        }

        /// Piezas que suben despacio desde el círculo (curaciones: pétalos, luces).
        public static void RisingMotes(GameObject root, EnergyPalette p, EnergyMeshes m, float radius, Mesh mesh, float rate, float rise, float size)
        {
            var s = Settings(1.4f, 2f, 0f, 0f, size * 0.7f, size);
            s.Loop = true; s.Rate = rate; s.Rise = rise; s.Orbital = 0.8f; s.Tumble = true;
            s.Shape = ParticleSystemShapeType.Circle; s.Radius = radius; s.Volume = true;
            B.Particles(root.transform, "Suben", mesh ?? m.Star, mesh == m.Star || mesh == null ? p.Spark : p.Energy, s);
        }

        /// Estrellitas que dan vueltas sobre la cabeza (aturdido, confuso). La raíz a los pies.
        public static void OrbitHalo(GameObject root, EnergyPalette p, EnergyMeshes m, float headHeight, float radius, int count)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            var g = B.Group(root.transform, "Corona", new Vector3(0f, headHeight, 0f));
            B.Animate(g, B.PopIn(), 0.3f);
            spins.Add(new Spin(g, Vector3.up, 200f));
            for (int i = 0; i < count; i++)
            {
                float a = i * 360f / count;
                var e = B.Piece(g, "Estrella " + (i + 1), m.Star, p.Spark, Quaternion.Euler(0f, a, 0f) * new Vector3(radius, 0f, 0f),
                                Quaternion.Euler(0f, a, 0f), Vector3.one * (0.16f / Size(m.Star)));
                spins.Add(new Spin(e.transform, Vector3.forward, 300f));
                rs.Add(e);
            }
            rs.Add(B.Piece(g, "Aro", m.Ring, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius));
            Pulse(root, rs.ToArray(), B.PointLight(g, p.Light), spins.ToArray(), 0.8f, 1.5f, 0.6f, 0.4f, 0.15f);
        }

        /// Cadenas: eslabones (aros pequeños) en corona que se cierran alrededor de alguien y giran.
        public static void Chains(GameObject root, EnergyPalette p, EnergyMeshes m, float height, float radius, int links)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            for (int vuelta = 0; vuelta < 2; vuelta++)
            {
                var g = B.Group(root.transform, "Cadena " + (vuelta + 1), new Vector3(0f, height * (0.35f + 0.35f * vuelta), 0f), Quaternion.Euler(vuelta == 0 ? 8f : -8f, 0f, 0f));
                B.Animate(g, new AnimationCurve(new Keyframe(0f, 2.2f), new Keyframe(0.7f, 0.95f), new Keyframe(1f, 1f)), 0.35f);
                spins.Add(new Spin(g, Vector3.up, vuelta == 0 ? 60f : -60f));
                for (int i = 0; i < links; i++)
                {
                    float a = i * 360f / links;
                    var rot = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(0f, 0f, (i % 2 == 0) ? 90f : 0f);
                    rs.Add(B.Piece(g, "Eslabón " + (i + 1), m.Ring, p.Piece, Quaternion.Euler(0f, a, 0f) * new Vector3(radius, 0f, 0f),
                                   rot * Quaternion.Euler(90f, 0f, 0f), new Vector3(0.09f, 0.09f, 0.14f) * 1.4f));
                }
            }
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light, new Vector3(0f, height * 0.5f, 0f)), spins.ToArray(), 1.2f, 2f, 0.8f, 0.35f, 0.1f);
        }

        /// Llegada o desaparición: aros que se cierran sobre un punto y bocanada.
        public static void Implosion(GameObject root, EnergyPalette p, EnergyMeshes m, float size, EnergyPalette puffs)
        {
            var aros = Burst(4, 0f, 0f, size * 1.6f, size * 2f, 0.35f, 0.45f);
            aros.Shape = ParticleSystemShapeType.Sphere; aros.Radius = 0.001f; aros.Tumble = true;
            aros.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var g = B.Group(root.transform, "Centro", new Vector3(0f, size * 0.8f, 0f));
            B.Particles(g, "Aros que se cierran", m.Ring, p.Piece, aros);
            var d = B.Group(g, "Destello");
            B.Animate(d, B.Flash(), 0.4f, 0.3f);
            var bola = B.Sphere(d, "Bola", size * 0.8f, p.Energy);
            if (puffs != null)
            {
                var bolas = Burst(8, 1.2f, 2f, 0.2f, 0.3f, 0.5f, 0.7f);
                bolas.Drag = 3f; bolas.Shape = ParticleSystemShapeType.Circle; bolas.Radius = 0.3f;
                B.Particles(root.transform, "Bocanada", m.Puff, puffs.Energy, bolas);
            }
            Pulse(root, new Renderer[] { bola }, B.PointLight(g, p.Light), null, 3f, 3f, 0, 0, 0, 0, 0.3f, B.LightFlash(0.7f));
        }

        /// Carga entre las manos (la mueve quien lanza): bolita que crece, aros que se cierran y estrellitas que entran.
        public static void HandCharge(GameObject root, EnergyPalette p, EnergyMeshes m, float size)
        {
            var aros = Settings(0.45f, 0.45f, 0f, 0f, size * 3.5f, size * 4.2f);
            aros.Loop = true; aros.Rate = 5f; aros.Tumble = true; aros.Shape = ParticleSystemShapeType.Sphere; aros.Radius = 0.001f;
            aros.SizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f, -0.5f, 0f));
            B.Particles(root.transform, "Aros que se cierran", m.OrbitRing, p.Piece, aros);
            var entran = Settings(0.35f, 0.35f, -1f, -1f, size * 0.3f, size * 0.45f);
            entran.Loop = true; entran.Rate = 18f; entran.Tumble = true; entran.Shape = ParticleSystemShapeType.Sphere; entran.Radius = size * 2.7f;
            B.Particles(root.transform, "Estrellitas que entran", m.Star, p.Spark, entran);
            var n = B.Group(root.transform, "Núcleo");
            B.Animate(n, B.PopIn(), 0.3f);
            var nucleo = B.Sphere(n, "Bola", size, p.Energy);
            Pulse(root, new Renderer[] { nucleo }, B.PointLight(root.transform, p.Light), null, 1.8f, 1.8f, 0.3f, 0.5f, 0.12f);
        }

        /// Sol que viaja: núcleo grande con una corona de rayos (conos) que gira y anillos que vuelcan.
        public static void Sun(GameObject root, EnergyPalette p, EnergyMeshes m, float diameter, int rays, float light)
        {
            var spins = new List<Spin>();
            var rs = new List<Renderer>();
            var nucleo = B.Sphere(root.transform, "Núcleo", diameter, p.Energy);
            spins.Add(new Spin(nucleo.transform, Vector3.up, 60f));
            rs.Add(nucleo);
            var corona = B.Group(root.transform, "Corona");
            spins.Add(new Spin(corona, Vector3.forward, 90f));
            for (int i = 0; i < rays; i++)
            {
                float a = i * 360f / rays;
                var g = B.Group(corona, "Rayo " + (i + 1), Quaternion.Euler(0f, 0f, a) * new Vector3(0f, diameter * 0.45f, 0f), Quaternion.Euler(0f, 0f, a));
                rs.Add(B.Piece(g, "Cono", m.Spike, p.Piece, Vector3.zero, Quaternion.identity, new Vector3(diameter * 0.9f, diameter * 0.45f, diameter * 0.9f)));
            }
            OrbitingRing(root.transform, p, m, "A", diameter * 0.75f, 70f, 0f, 120f, 300f, diameter * 0.08f, spins, rs);
            OrbitingRing(root.transform, p, m, "B", diameter * 0.8f, 40f, 90f, -100f, -260f, diameter * 0.08f, spins, rs);
            Pulse(root, rs.ToArray(), B.PointLight(root.transform, p.Light), spins.ToArray(), light, diameter * 4f + 2f, 0.6f, 0.3f, 0.15f);
        }

        // ── Piezas sueltas ──────────────────────────────────────────────────────────────────

        /// Anillo fino que vuelca alrededor del centro, con una cuenta de luz que corre por él.
        public static void OrbitingRing(Transform parent, EnergyPalette p, EnergyMeshes m, string letter, float radius, float tilt, float heading,
                                        float tumble, float bead, float beadSize, List<Spin> spins, List<Renderer> rs, Mesh mesh = null)
        {
            var v = B.Group(parent, "Vuelco " + letter, Vector3.zero, Quaternion.Euler(0f, heading, 0f));
            spins.Add(new Spin(v, Vector3.up, tumble));
            var inc = B.Group(v, "Inclinación " + letter, Vector3.zero, Quaternion.Euler(tilt, 0f, 0f));
            var aro = B.Piece(inc, "Anillo " + letter, mesh ?? m.OrbitRing, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius);
            spins.Add(new Spin(aro.transform, Vector3.up, bead));
            rs.Add(aro);
            rs.Add(B.Sphere(aro.transform, "Cuenta " + letter, beadSize / radius, p.Spark, new Vector3(1f, 0f, 0f)));
        }

        public static Renderer Wave(Transform parent, EnergyMeshes m, string name, float radius, Quaternion rotation, Material mat, float seconds,
                                    float height = 0f, float delay = 0f)
        {
            var g = B.Group(parent, name, new Vector3(0f, height, 0f), rotation);
            B.Animate(g, B.Expand(), seconds, delay);
            return B.Piece(g, "Aro", m.Ring, mat, Vector3.zero, Quaternion.identity, Vector3.one * radius);
        }

        public static StylizedVfxBuilder.MeshParticles Settings(float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax)
            => new StylizedVfxBuilder.MeshParticles
            {
                LifetimeMin = lifeMin, LifetimeMax = lifeMax, SpeedMin = speedMin, SpeedMax = speedMax,
                SizeMin = sizeMin, SizeMax = sizeMax, Shape = ParticleSystemShapeType.Sphere, Radius = 0.05f, Duration = 1f,
            };

        /// Salen todas a la vez, una sola vez.
        public static StylizedVfxBuilder.MeshParticles Burst(int count, float speedMin, float speedMax, float sizeMin, float sizeMax, float lifeMin, float lifeMax)
        {
            var s = Settings(lifeMin, lifeMax, speedMin, speedMax, sizeMin, sizeMax);
            s.Burst = count; s.Loop = false; s.Tumble = true; s.MaxParticles = count + 4;
            return s;
        }

        public static EnergyPulse Pulse(GameObject root, Renderer[] rs, Light light, Spin[] spins, float intensity, float range,
                                        float period, float depth, float appear, float disappearAfter = 0f, float disappearTime = 0.3f, AnimationCurve lightCurve = null)
        {
            var p = root.AddComponent<EnergyPulse>();
            p.Configure(rs, light, spins ?? new Spin[0]);
            p.ConfigureTiming(intensity, range, period, depth, appear);
            p.ConfigureLifetime(disappearAfter, disappearTime, lightCurve);
            return p;
        }

        private static void MagicCircleInto(Transform c, EnergyPalette p, EnergyMeshes m, float radius, List<Spin> spins, List<Renderer> rs)
        {
            var g = B.Group(c, "Aro del suelo", new Vector3(0f, 0.03f, 0f));
            spins.Add(new Spin(g, Vector3.up, 20f));
            rs.Add(B.Piece(g, "Aro", m.Ring, p.Piece, Vector3.zero, Quaternion.identity, Vector3.one * radius));
        }

        private static Vector3 Vertex(float degrees, float r) => Quaternion.Euler(0f, degrees + 90f, 0f) * new Vector3(r, 0f, 0f);

        public static float Size(Mesh mesh) => mesh == null ? 1f : Mathf.Max(0.001f, mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
    }
}
