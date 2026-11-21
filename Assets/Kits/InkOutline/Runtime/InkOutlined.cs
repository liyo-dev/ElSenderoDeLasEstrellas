using System.Collections.Generic;
using UnityEngine;

namespace Kits.InkOutline
{
    /// Marca un personaje para que lleve línea de tinta: pone el bit <see cref="RenderingLayer"/> en sus
    /// renderers (salvo los que excluya <see cref="Excluded"/>) mientras está activo; lo dibuja
    /// <see cref="InkOutlineFeature"/>. Para piezas que se crean después (ropa nueva), llamar a
    /// <see cref="Refresh"/>.
    [DisallowMultipleComponent]
    public sealed class InkOutlined : MonoBehaviour
    {
        /// Bit de rendering layer que filtra la línea.
        public const uint RenderingLayer = 1u << 7;

        /// Qué piezas del personaje no llevan línea (por ejemplo, las de la cara). Lo pone el juego.
        public static System.Func<Renderer, bool> Excluded;

        /// Personajes con línea en este momento.
        public static readonly List<InkOutlined> Active = new();

        private readonly List<Renderer> _renderers = new();

        /// Renderers de este personaje que llevan línea.
        public IReadOnlyList<Renderer> Renderers => _renderers;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Excluded = null; Active.Clear(); }
#endif

        /// Añade el componente si no lo tiene.
        public static InkOutlined Ensure(GameObject character)
        {
            if (character == null) return null;
            return character.TryGetComponent(out InkOutlined existing) ? existing : character.AddComponent<InkOutlined>();
        }

        private void OnEnable()
        {
            Collect();
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].renderingLayerMask &= ~RenderingLayer;
        }

        /// Vuelve a recoger los renderers (tras añadir o quitar piezas).
        public void Refresh() => Collect();

        private void Collect()
        {
            _renderers.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
                if (Excluded != null && Excluded(r)) { r.renderingLayerMask &= ~RenderingLayer; continue; }
                r.renderingLayerMask |= RenderingLayer;
                _renderers.Add(r);
            }
        }
    }
}
