using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// La zona que marca un mapa del tesoro. Mientras el grupo lleva el mapa (un objeto de la bolsa)
/// y el tesoro sigue sin abrir, la zona sale en el minimapa (y se ve su señal en el mundo, si
/// tiene). <see cref="Activas"/> es la lista que puede usar una brújula para apuntar a la más
/// cercana. Va en un objeto en el centro de la zona, con un MinimapMarker.
/// </summary>
[RequireComponent(typeof(MinimapMarker))]
public sealed class MarcaDeMapaDelTesoro : MonoBehaviour
{
    [Tooltip("Objeto que hay que llevar para ver la marca.")]
    [SerializeField] private ItemData mapa;
    [Tooltip("Tesoro que marca (opcional): al abrirlo, la marca desaparece.")]
    [SerializeField] private Tesoro tesoro;
    [Tooltip("Radio de la zona marcada, en metros (para quien la dibuje o apunte a ella).")]
    [SerializeField, Min(0f)] private float radio = 8f;
    [Tooltip("Señal en el mundo que se ve con la marca (opcional): un haz de luz, un brillo…")]
    [SerializeField] private GameObject senalEnElMundo;

    private static readonly List<MarcaDeMapaDelTesoro> s_activas = new List<MarcaDeMapaDelTesoro>();

    /// <summary>Marcas visibles ahora mismo (el jugador lleva su mapa y el tesoro sigue cerrado).</summary>
    public static IReadOnlyList<MarcaDeMapaDelTesoro> Activas => s_activas;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => s_activas.Clear();
#endif

    private MinimapMarker _marcador;
    private Inventory _bolsa;
    private bool _visible;

    public float Radio => radio;
    public Vector3 Centro => transform.position;
    public ItemData Mapa => mapa;

    private void Awake()
    {
        _marcador = GetComponent<MinimapMarker>();
        Aplicar(false);
    }

    private void OnEnable()
    {
        Tesoro.Abierto += AlAbrirseUnTesoro;
        GameBootService.OnProfileReady += Actualizar;
        PlayerPresetService.OnPresetApplied += Actualizar;
        Actualizar();
    }

    private void OnDisable()
    {
        Tesoro.Abierto -= AlAbrirseUnTesoro;
        GameBootService.OnProfileReady -= Actualizar;
        PlayerPresetService.OnPresetApplied -= Actualizar;
        if (_bolsa != null) _bolsa.OnInventoryChanged -= AlCambiarLaBolsa;
        _bolsa = null;
        Aplicar(false);
    }

    private void AlAbrirseUnTesoro(Tesoro abierto)
    {
        if (abierto == tesoro) Actualizar();
    }

    private void AlCambiarLaBolsa(ItemData objeto, int _)
    {
        if (objeto == mapa) Actualizar();
    }

    /// <summary>Vuelve a mirar si se lleva el mapa y si el tesoro sigue cerrado.</summary>
    public void Actualizar()
    {
        EngancharBolsa();
        bool llevaElMapa = mapa != null && _bolsa != null && _bolsa.HasItem(mapa);
        bool pendiente = tesoro == null || !(tesoro.EstaAbierto || FlagDePartida.EstaPuesto(tesoro.Clave));
        Aplicar(llevaElMapa && pendiente);
    }

    private void EngancharBolsa()
    {
        if (_bolsa != null) return;
        if (!PlayerService.TryGetComponent(out _bolsa, allowSceneLookup: false) || _bolsa == null) return;
        _bolsa.OnInventoryChanged += AlCambiarLaBolsa;
    }

    private void Aplicar(bool visible)
    {
        if (_marcador != null) _marcador.SetVisible(visible);
        if (senalEnElMundo != null && senalEnElMundo.activeSelf != visible) senalEnElMundo.SetActive(visible);
        if (visible == _visible) return;
        _visible = visible;
        if (visible) s_activas.Add(this);
        else s_activas.Remove(this);
    }
}
