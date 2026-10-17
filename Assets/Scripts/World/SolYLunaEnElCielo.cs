using UnityEngine;

/// Publica las direcciones, colores HDR y tamaños de los astros que dibuja El Sendero/Cielo.
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class SolYLunaEnElCielo : MonoBehaviour
{
    [Header("Tamaño angular")]
    [Tooltip("Diámetro del sol en grados de cielo.")]
    [Range(1f, 30f)] [SerializeField] private float tamanoDelSol = 9f;
    [Tooltip("Diámetro de la luna en grados de cielo.")]
    [Range(1f, 30f)] [SerializeField] private float tamanoDeLaLuna = 7f;

    [Header("Desvanecido")]
    [Tooltip("Grados bajo el horizonte a los que el astro se desvanece por completo.")]
    [SerializeField, Min(0.01f)] private float margenBajoElHorizonte = 8f;
    [Tooltip("Opacidad máxima de la luna.")]
    [Range(0f, 1f)] [SerializeField] private float opacidadDeLaLuna = 0.9f;

    private static readonly Color SolAlto = new Color(4f, 3.7f, 3.2f);
    private static readonly Color SolEnElHorizonte = new Color(4f, 1.6f, 0.6f);
    private static readonly Color ColorDeLaLuna = new Color(1.6f, 1.55f, 1.4f);
    private static readonly int DireccionSolId = Shader.PropertyToID("_CieloDireccionDelSol");
    private static readonly int DireccionLunaId = Shader.PropertyToID("_CieloDireccionDeLaLuna");
    private static readonly int ColorSolId = Shader.PropertyToID("_CieloColorDelSol");
    private static readonly int ColorLunaId = Shader.PropertyToID("_CieloColorDeLaLuna");
    private static readonly int TamanoSolId = Shader.PropertyToID("_CieloTamanoDelSol");
    private static readonly int TamanoLunaId = Shader.PropertyToID("_CieloTamanoDeLaLuna");
    private static readonly Camera[] Camaras = new Camera[128];
    private static Camera s_camara;
    private static SolYLunaEnElCielo s_publicador;
    private Transform _luzDeEscena;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_camara = null;
        s_publicador = null;
        System.Array.Clear(Camaras, 0, Camaras.Length);
        ApagarAstros();
    }
#endif

    private void OnEnable()
    {
        CachearLuzDeEscena();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.hierarchyChanged += ActualizarCacheDeEditor;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.hierarchyChanged -= ActualizarCacheDeEditor;
#endif
        if (s_publicador != this) return;
        s_publicador = null;
        ApagarAstros();
    }

    private static void ApagarAstros()
    {
        Shader.SetGlobalColor(ColorSolId, Color.clear);
        Shader.SetGlobalColor(ColorLunaId, Color.clear);
    }

    private void CachearLuzDeEscena()
    {
        _luzDeEscena = RenderSettings.sun != null ? RenderSettings.sun.transform : null;
        if (_luzDeEscena != null) return;
        var luces = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        for (int i = 0; i < luces.Length; i++)
        {
            Light luz = luces[i];
            if (luz.type != LightType.Directional || !luz.isActiveAndEnabled) continue;
            _luzDeEscena = luz.transform;
            break;
        }
    }

#if UNITY_EDITOR
    private void ActualizarCacheDeEditor()
    {
        if (!Application.isPlaying) CachearLuzDeEscena();
    }
#endif

    private void LateUpdate()
    {
        DayNightCycle ciclo = Application.isPlaying ? DayNightCycle.Instance : null;
        Transform luz = DayNightCycle.Sun != null ? DayNightCycle.Sun : _luzDeEscena;
        if (ciclo == null && luz == null)
        {
            if (s_publicador == this) ApagarAstros();
            return;
        }
        Vector3 sol = ciclo != null ? ciclo.DireccionDelSol : -luz.forward;
        Vector3 luna = ciclo != null ? ciclo.DireccionDeLaLuna : -sol;
        if (SolForzado.DireccionDelDisco.HasValue) sol = SolForzado.DireccionDelDisco.Value;
        if (sol.sqrMagnitude > 0.000001f) sol.Normalize();
        if (luna.sqrMagnitude > 0.000001f) luna.Normalize();
        Color colorSol = Color.Lerp(SolEnElHorizonte, SolAlto, Mathf.Clamp01(sol.y * 2.2f));
        colorSol.a = Visibilidad(sol) * SolForzado.OpacidadDelSol;
        Color colorLuna = ColorDeLaLuna;
        colorLuna.a = Visibilidad(luna) * opacidadDeLaLuna;
        Shader.SetGlobalVector(DireccionSolId, sol);
        Shader.SetGlobalVector(DireccionLunaId, luna);
        Shader.SetGlobalColor(ColorSolId, colorSol);
        Shader.SetGlobalColor(ColorLunaId, colorLuna);
        // El shader recibe radios en radianes; el Inspector muestra diámetros en grados.
        Shader.SetGlobalFloat(TamanoSolId, tamanoDelSol * 0.5f * Mathf.Deg2Rad);
        Shader.SetGlobalFloat(TamanoLunaId, tamanoDeLaLuna * 0.5f * Mathf.Deg2Rad);
        s_publicador = this;
    }

    private float Visibilidad(Vector3 direccion)
    {
        float altura = Mathf.Asin(Mathf.Clamp(direccion.y, -1f, 1f)) * Mathf.Rad2Deg;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-Mathf.Max(0.01f, margenBajoElHorizonte), 0f, altura));
    }

    /// Plano lejano de la cámara actual para las herramientas que sitúan efectos en el cielo.
    public static float LejosActual
    {
        get
        {
            Camera camara = CamaraActual();
            return camara != null ? camara.farClipPlane : 300f;
        }
    }

    /// Selecciona la cámara activa sin crear listas ni consultar Camera.main por frame.
    public static Camera CamaraActual()
    {
        int cantidad = Camera.allCamerasCount;
        if (cantidad <= Camaras.Length)
        {
            int total = Camera.GetAllCameras(Camaras);
            Camera principal = null, mejor = null;
            for (int i = 0; i < total; i++)
            {
                Camera camara = Camaras[i];
                if (camara == null || !camara.isActiveAndEnabled || camara.cameraType != CameraType.Game || camara.orthographic) continue;
                if (mejor == null || camara.depth > mejor.depth) mejor = camara;
                if (camara.CompareTag("MainCamera") && (principal == null || camara.depth > principal.depth)) principal = camara;
            }
            s_camara = principal != null ? principal : mejor;
        }
        if (s_camara != null && s_camara.isActiveAndEnabled) return s_camara;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var vista = UnityEditor.SceneView.lastActiveSceneView;
            if (vista != null) return vista.camera;
        }
#endif
        return null;
    }
}
