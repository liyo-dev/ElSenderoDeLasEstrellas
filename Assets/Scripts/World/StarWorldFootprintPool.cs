using System.Collections.Generic;
using Game.Player;
using UnityEngine;

/// <summary>
/// Gestiona un pool de FootprintEffect para el mundo estelar.
/// Colocar en la escena de la batalla final. Requiere FootstepHandler en cada personaje.
/// </summary>
public class StarWorldFootprintPool : MonoBehaviour
{
    [SerializeField] private FootprintEffect _prefab;
    [SerializeField] private int _poolSize = 24;
    [SerializeField] private Color _footprintColor = new Color(0.5f, 0.8f, 1f, 1f);

    [Tooltip("Diámetro de cada huella en unidades de mundo. Ajustar según la escala de los personajes.")]
    [SerializeField] private float _footprintScale = 0.35f;

    [SerializeField] private float _yOffset = 0.02f;

    private readonly Queue<FootprintEffect> _pool = new();

    private void Awake()
    {
        if (_prefab == null)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            Debug.LogWarning("[StarWorldFootprintPool] _prefab no está asignado en el Inspector.");
#endif
            return;
        }

        for (int i = 0; i < _poolSize; i++)
        {
            FootprintEffect effect = Instantiate(_prefab, transform);
            effect.gameObject.SetActive(false);
            _pool.Enqueue(effect);
        }
    }

    private void OnEnable()  => FootstepHandler.OnFootstep += HandleFootstep;
    private void OnDisable() => FootstepHandler.OnFootstep -= HandleFootstep;

    private void HandleFootstep(Vector3 position)
    {
        if (_pool.Count == 0) return;
        FootprintEffect effect = _pool.Dequeue();
        effect.Activate(position + Vector3.up * _yOffset, _footprintScale, _footprintColor, ReturnToPool);
    }

    private void ReturnToPool(FootprintEffect effect) => _pool.Enqueue(effect);
}
