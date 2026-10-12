using Core.InputGlyphs;
using UnityEngine;
using UnityEngine.UI;

namespace Sendero.UI
{
    /// <summary>
    /// Aviso sobre un compañero caído al que se puede reanimar: glifo del botón A dentro de un
    /// anillo que se llena mientras se mantiene (ReanimarManteniendoA). Se crea por código con su
    /// propio Canvas por encima del HUD; el anillo usa el sprite del aviso de combate
    /// (Resources/UI/AvisoDeAmenaza) si no se le da otro. Ver INC-717.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvisoDeReanimarUI : MonoBehaviour
    {
        private const float Tamano = 84f;
        private const float AlturaSobreElCuerpo = 0.9f;
        private static readonly Color ColorFondo = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color ColorRelleno = new Color(0.45f, 1f, 0.55f, 1f);

        private CanvasGroup _grupo;
        private RectTransform _icono;
        private Image _relleno;
        private Image _glifo;
        private Camera _camara;
        private Transform _objetivo;

        public static AvisoDeReanimarUI Crear(Transform padre, Sprite anillo)
        {
            if (anillo == null) anillo = AnilloDelAvisoDeCombate();

            var raiz = new GameObject("AvisoDeReanimar", typeof(RectTransform));
            raiz.transform.SetParent(padre, false);
            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 61;
            var escalador = raiz.AddComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;

            var ui = raiz.AddComponent<AvisoDeReanimarUI>();
            ui._grupo = raiz.AddComponent<CanvasGroup>();
            ui._grupo.alpha = 0f;
            ui._grupo.interactable = false;
            ui._grupo.blocksRaycasts = false;

            ui._icono = Hijo("Icono", raiz.transform, new Vector2(Tamano, Tamano));
            var fondo = Hijo("Fondo", ui._icono, new Vector2(Tamano, Tamano)).gameObject.AddComponent<Image>();
            fondo.sprite = anillo;
            fondo.color = ColorFondo;
            fondo.raycastTarget = false;

            ui._relleno = Hijo("Relleno", ui._icono, new Vector2(Tamano, Tamano)).gameObject.AddComponent<Image>();
            ui._relleno.sprite = anillo;
            ui._relleno.color = ColorRelleno;
            ui._relleno.type = Image.Type.Filled;
            ui._relleno.fillMethod = Image.FillMethod.Radial360;
            ui._relleno.fillOrigin = (int)Image.Origin360.Top;
            ui._relleno.fillClockwise = true;
            ui._relleno.fillAmount = 0f;
            ui._relleno.raycastTarget = false;

            ui._glifo = Hijo("Glifo", ui._icono, new Vector2(52f, 52f)).gameObject.AddComponent<Image>();
            ui._glifo.preserveAspect = true;
            ui._glifo.raycastTarget = false;
            ui.PonerGlifo();
            return ui;
        }

        private void OnEnable() => InputGlyphService.FamilyChanged += AlCambiarDeMando;
        private void OnDisable() => InputGlyphService.FamilyChanged -= AlCambiarDeMando;
        private void AlCambiarDeMando(InputGlyphDeviceFamily _) => PonerGlifo();

        /// Muestra el aviso sobre ese cuerpo con el anillo lleno en esa parte (0..1).
        public void Mostrar(Transform cuerpo, float progreso)
        {
            _objetivo = cuerpo;
            if (_relleno != null) _relleno.fillAmount = Mathf.Clamp01(progreso);
            if (_grupo != null) _grupo.alpha = 1f;
        }

        public void Ocultar()
        {
            _objetivo = null;
            if (_grupo != null && _grupo.alpha > 0f) _grupo.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (_objetivo == null || _icono == null || _grupo == null || _grupo.alpha <= 0f) return;
            if (_camara == null || !_camara.isActiveAndEnabled) _camara = Camera.main;
            if (_camara == null) return;
            Vector3 pantalla = _camara.WorldToScreenPoint(_objetivo.position + Vector3.up * AlturaSobreElCuerpo);
            if (pantalla.z <= 0f) { _grupo.alpha = 0f; return; }
            _icono.position = pantalla;
        }

        private void PonerGlifo()
        {
            if (_glifo == null) return;
            var sprite = InputGlyphService.GetSprite(InputGlyphNames.South);
            if (sprite != null) _glifo.sprite = sprite;
        }

        private static Sprite AnilloDelAvisoDeCombate()
        {
            var prefab = Resources.Load<GameObject>("UI/AvisoDeAmenaza");
            var anillo = prefab != null ? prefab.transform.Find("Icono/Anillo") : null;
            var imagen = anillo != null ? anillo.GetComponent<Image>() : null;
            return imagen != null ? imagen.sprite : null;
        }

        private static RectTransform Hijo(string nombre, Transform padre, Vector2 tamano)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(padre, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = tamano;
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }
    }
}
