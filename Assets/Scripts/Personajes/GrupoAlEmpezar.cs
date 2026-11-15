using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Deja el grupo entero al empezar la escena, sin pasar por la historia: los compañeros que ya
/// están en la escena entran en el equipo. Es para escenas que se juegan fuera del grafo (una
/// maqueta, un nivel nuevo que se prueba suelto); en la historia el grupo lo decide el grafo con
/// «Pedir al juego» → <c>PedirGrupo</c>.
///
/// Reintenta unos segundos porque los compañeros se registran un poco después de cargar, y se rinde
/// con un aviso: nunca deja el nivel esperando.
/// </summary>
public sealed class GrupoAlEmpezar : MonoBehaviour
{
    [Tooltip("Compañeros que entran en el grupo al empezar (Will siempre va al mando).")]
    [SerializeField] private List<Slot> companeros = new List<Slot> { Slot.Estela, Slot.Liam };
    [Tooltip("Segundos que se reintenta antes de rendirse (los compañeros terminan de registrarse después de cargar).")]
    [SerializeField, Min(0f)] private float segundosDeEspera = 8f;

    private float _limite;
    private bool _hecho;

    private void OnEnable()
    {
        _limite = Time.unscaledTime + segundosDeEspera;
        _hecho = false;
    }

    private void Update()
    {
        if (_hecho) return;
        if (PlayerService.Player == null && Time.unscaledTime < _limite) return;

        bool todos = true;
        for (int i = 0; i < companeros.Count; i++)
        {
            var quien = companeros[i];
            if (quien == Slot.Will || ControlDelJugador.EnElGrupo(quien)) continue;
            todos &= SalidasDelGrupo.Unir(quien);
        }

        if (todos)
        {
            _hecho = true;
            return;
        }

        if (Time.unscaledTime < _limite) return;
        _hecho = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        for (int i = 0; i < companeros.Count; i++)
            if (!ControlDelJugador.EnElGrupo(companeros[i]))
                Debug.LogWarning($"[GrupoAlEmpezar] {companeros[i]} no ha entrado en el grupo: no está en la escena o no se puede unir.");
#endif
    }
}
