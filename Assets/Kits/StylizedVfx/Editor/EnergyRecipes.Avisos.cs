using System.Collections.Generic;
using UnityEngine;
using B = Kits.StylizedVfx.Editor.StylizedVfxBuilder;

namespace Kits.StylizedVfx.Editor
{
    public static partial class EnergyRecipes
    {
        /// Aviso en el suelo de dónde va a caer algo: un solo aro plano, de trazo grueso, que late
        /// despacio. Sin relleno, iconos ni capas: se lee de un vistazo en mitad del combate. Se apoya
        /// en el suelo con GroundMark. Quien lo usa puede hacerlo crecer (escala) para marcar el tiempo.
        public static void GroundWarning(GameObject root, EnergyPalette p, EnergyMeshes m, float radius)
        {
            root.AddComponent<GroundMark>().FootprintRadius = radius;
            var rs = new List<Renderer>();
            var g = B.Group(root.transform, "Aro", new Vector3(0f, 0.02f, 0f));
            B.Animate(g, B.PopIn(), 0.2f);
            // Aro de órbita (tubo grueso) aplastado: una banda en el suelo, no un tubo que asoma.
            rs.Add(B.Piece(g, "Borde", m.OrbitRing ?? m.Ring, p.Piece, Vector3.zero, Quaternion.identity,
                           new Vector3(radius, radius * 0.25f, radius)));
            Pulse(root, rs.ToArray(), null, null, 0f, 0f, 0.45f, 0.35f, 0.15f);
        }
    }
}
