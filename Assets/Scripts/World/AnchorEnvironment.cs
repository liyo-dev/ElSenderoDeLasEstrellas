using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AnchorEnvironment : MonoBehaviour
{
    [Header("Tipo de entorno")]
    public bool isInterior = true;

    [Header("Cámara en interior")]
    public bool useSolidColorBackground = true;
    public Color interiorBgColor = new Color(0.05f, 0.05f, 0.06f);
    public Material interiorSkyboxOverride; // opcional

    [Header("Luces al entrar (activar)")]
    public Light[] lightsEnableOnEnter;
    [Header("Luces al entrar (desactivar)")]
    public Light[] lightsDisableOnEnter;
    
    [Header("Gestión de Zonas Visibles")]
    [Tooltip("GameObject raíz de esta zona/interior. Se activará al entrar aquí.")]
    public GameObject zoneRoot;
    
    [Tooltip("Otras zonas que deben OCULTARSE al entrar aquí. Útil para interiores en la misma escena.")]
    public GameObject[] zonesToHideOnEnter;
    
    [Tooltip("Si true, oculta automáticamente el mundo exterior (busca 'WorldRoot' o similar)")]
    public bool hideExteriorWorld = true;

    [Tooltip("OBSOLETO (14 sep 2026): ya no se lee. ApplyZoneVisibility() ahora oculta TODOS los " +
             "ExteriorWorldRoot registrados en la escena automáticamente — basta con poner ese " +
             "componente en el GameObject, no hace falta además listarlo aquí por nombre (ver " +
             "EnvironmentController.cs). Se mantiene el campo, sin usarlo, para no perder el valor " +
             "ya serializado en anchors existentes; puedes vaciarlo con tranquilidad.")]
    public string exteriorWorldRootName = "ExteriorWorld";

    [Tooltip("OBSOLETO (14 sep 2026): ya no se lee, mismo motivo que exteriorWorldRootName de arriba.")]
    public string[] exteriorWorldRootNames;
    
    [Header("Culling de Cámara")]
    [Tooltip("Si true, ajusta el far clip plane de la cámara para limitar qué se ve")]
    public bool adjustCameraClipping = false;
    [Tooltip("Far clip plane cuando está en este interior (más pequeño = menos visible)")]
    public float interiorFarClipPlane = 50f;

    // Registro de interiores cargados, para saber en cuál cae un punto (ver InteriorQueContiene).
    private static readonly List<AnchorEnvironment> s_cargados = new();
    private Bounds _limites;
    private bool _limitesHechos;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_cargados.Clear();
#endif

    private void OnEnable() { if (!s_cargados.Contains(this)) s_cargados.Add(this); }
    private void OnDisable() => s_cargados.Remove(this);

    /// El interior cargado cuya zona contiene el punto, o null si el punto está al aire libre.
    /// La zona es la caja de los renderers de zoneRoot (o de este objeto si no tiene).
    public static AnchorEnvironment InteriorQueContiene(Vector3 punto)
    {
        for (int i = 0; i < s_cargados.Count; i++)
        {
            var a = s_cargados[i];
            if (a == null || !a.isInterior) continue;
            if (a.Limites().Contains(punto)) return a;
        }
        return null;
    }

    private Bounds Limites()
    {
        if (_limitesHechos) return _limites;
        _limitesHechos = true;
        var raiz = zoneRoot != null ? zoneRoot.transform : transform;
        bool hay = false;
        // Con la caja local y la matriz del renderer: la zona puede estar apagada (interior al que
        // aún no se ha entrado) y entonces Renderer.bounds viene vacía.
        foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds local = r.localBounds;
            Matrix4x4 m = r.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 esquina = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 w = m.MultiplyPoint3x4(esquina);
                if (!hay) { _limites = new Bounds(w, Vector3.zero); hay = true; }
                else _limites.Encapsulate(w);
            }
        }
        if (!hay) _limites = new Bounds(raiz.position, Vector3.zero);
        _limites.Expand(1f);
        return _limites;
    }
}