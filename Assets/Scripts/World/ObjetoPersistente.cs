using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Guarda en la partida si un objeto de la escena se ha movido o se ha retirado del mundo
/// (entregado, consumido…) y lo deja igual al cargar. Ver INC-540.
///
/// - <b>Movido:</b> al guardar, <see cref="CapturarEstado"/> anota posición y giro de los objetos
///   que ya no están donde los puso la escena. Los que han vuelto a su sitio se borran de la lista.
/// - <b>Retirado:</b> quien lo quita del mundo llama a <see cref="Retirar"/>, que lo anota en el
///   acto (el objeto ya no existirá cuando se guarde) y lo destruye.
///
/// El id sale de la escena, el nombre y la posición de PARTIDA (<see cref="IdDePersistencia"/>),
/// así que no cambia aunque el objeto se mueva. <see cref="PickupObject"/> lo añade solo a todo
/// objeto que se puede llevar en brazos; en cualquier otro objeto se pone a mano.
/// </summary>
[DisallowMultipleComponent]
public sealed class ObjetoPersistente : MonoBehaviour
{
    /// <summary>Estado guardado de un objeto. Sin entrada = sigue como lo dejó la escena.</summary>
    [Serializable]
    public struct Estado
    {
        public string id;
        public bool retirado;
        public Vector3 posicion;
        public Quaternion rotacion;
    }

    [Tooltip("Id fijo. Vacío = automático (escena + nombre + posición de partida). Solo hace falta si el objeto " +
             "se va a renombrar o recolocar en la escena y hay partidas guardadas que conservar.")]
    [SerializeField] private string idManual;

    private const float ToleranciaDePosicion = 0.05f;
    private const float ToleranciaDeGiro = 1f;

    private static readonly List<ObjetoPersistente> _vivos = new();

    private string _id;
    private Vector3 _posicionInicial;
    private Quaternion _rotacionInicial;
    private Rigidbody _rigidbody;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _vivos.Clear();
#endif

    public string Id => _id;

    private void Awake()
    {
        _posicionInicial = transform.position;
        _rotacionInicial = transform.rotation;
        _id = string.IsNullOrWhiteSpace(idManual) ? IdDePersistencia.DeObjeto(gameObject, _posicionInicial) : idManual;
        _rigidbody = GetComponent<Rigidbody>();

        // Registro en Awake/OnDestroy (no en OnEnable/OnDisable): un objeto apagado también se guarda.
        _vivos.Add(this);
        GameBootService.OnProfileReady += Aplicar;
        if (GameBootService.IsAvailable) Aplicar();
    }

    private void OnDestroy()
    {
        GameBootService.OnProfileReady -= Aplicar;
        _vivos.Remove(this);
    }

    /// <summary>
    /// Quita el objeto del mundo para el resto de la partida: lo anota como retirado y lo destruye.
    /// Un objeto sin <see cref="ObjetoPersistente"/> solo se destruye.
    /// </summary>
    public static void Retirar(GameObject objeto)
    {
        if (objeto == null) return;

        if (objeto.TryGetComponent(out ObjetoPersistente persistente))
        {
            var lista = ListaDelPreset();
            if (lista != null) Escribir(lista, new Estado { id = persistente._id, retirado = true });
        }

        Destroy(objeto);
    }

    /// <summary>
    /// Anota en el preset dónde están los objetos que se han movido de su sitio de partida.
    /// Lo llama <see cref="GameBootProfile.UpdateRuntimePresetFromCurrentState"/> al guardar.
    /// Los objetos de escenas no cargadas conservan lo que ya tenían. Devuelve cuántas entradas hay.
    /// </summary>
    public static int CapturarEstado(PlayerPresetSO preset)
    {
        if (preset == null) return 0;
        preset.objetosDelMundo ??= new List<Estado>();
        var lista = preset.objetosDelMundo;

        for (int i = 0; i < _vivos.Count; i++)
        {
            var o = _vivos[i];
            if (o == null) continue;

            var t = o.transform;
            bool movido = (t.position - o._posicionInicial).sqrMagnitude > ToleranciaDePosicion * ToleranciaDePosicion
                          || Quaternion.Angle(t.rotation, o._rotacionInicial) > ToleranciaDeGiro;

            if (movido) Escribir(lista, new Estado { id = o._id, posicion = t.position, rotacion = t.rotation });
            else Borrar(lista, o._id);
        }

        return lista.Count;
    }

    private void Aplicar()
    {
        var lista = ListaDelPreset();
        if (lista == null) return;

        int i = Buscar(lista, _id);
        if (i < 0) return;

        var estado = lista[i];
        if (estado.retirado)
        {
            Destroy(gameObject);
            return;
        }

        transform.SetPositionAndRotation(estado.posicion, estado.rotacion);
        if (_rigidbody != null)
        {
            _rigidbody.position = estado.posicion;
            _rigidbody.rotation = estado.rotacion;
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    private static List<Estado> ListaDelPreset()
    {
        var preset = GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;
        if (preset == null) return null;
        preset.objetosDelMundo ??= new List<Estado>();
        return preset.objetosDelMundo;
    }

    private static int Buscar(List<Estado> lista, string id)
    {
        for (int i = 0; i < lista.Count; i++)
            if (lista[i].id == id) return i;
        return -1;
    }

    private static void Escribir(List<Estado> lista, Estado estado)
    {
        int i = Buscar(lista, estado.id);
        if (i >= 0) lista[i] = estado;
        else lista.Add(estado);
    }

    private static void Borrar(List<Estado> lista, string id)
    {
        int i = Buscar(lista, id);
        if (i >= 0) lista.RemoveAt(i);
    }
}
