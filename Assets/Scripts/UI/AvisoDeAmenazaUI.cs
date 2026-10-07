using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Core.InputGlyphs;

namespace Sendero.UI
{
    /// <summary>
    /// Icono del aviso de combate sobre la cabeza del jugador (<see cref="AvisoDeAmenazas"/>):
    /// anillo con el glifo del botón del mando que se use (B para la defensa, A para recuperarse) o
    /// un «!» rojo para lo que no se para. Tenue cuando viene; opaco, dorado y con golpe de escala
    /// en el momento justo. Prefab en Resources/UI/AvisoDeAmenaza (lo crea el menú de INC-666).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvisoDeAmenazaUI : MonoBehaviour
    {
        [Header("Piezas")]
        [SerializeField] private RectTransform icono;
        [SerializeField] private CanvasGroup grupo;
        [SerializeField] private Image anillo;
        [SerializeField] private Image glifo;
        [SerializeField] private Graphic exclamacion;

        [Header("Aspecto")]
        [SerializeField] private Color colorAviso = Color.white;
        [SerializeField] private Color colorAhora = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color colorNoBloqueable = new Color(0.95f, 0.25f, 0.2f);
        [Tooltip("Opacidad del icono mientras el momento justo aún no ha llegado.")]
        [SerializeField, Range(0f, 1f)] private float alfaAviso = 0.55f;
        [Tooltip("Escala del icono mientras el momento justo aún no ha llegado.")]
        [SerializeField, Min(0.1f)] private float escalaAviso = 0.85f;
        [Tooltip("Metros sobre la cabeza del personaje.")]
        [SerializeField] private float alturaSobreLaCabeza = 0.55f;
        [Tooltip("Velocidad del fundido de entrada y salida (opacidad por segundo).")]
        [SerializeField, Min(0.1f)] private float velocidadDeFundido = 10f;
        [SerializeField, Min(0f)] private float golpeDeEscala = 0.3f;

        private AvisoDeAmenazas _fuente;
        private bool _teniaFuente;
        private Camera _camara;
        private float _alfaObjetivo;
        private TipoDeAviso _tipo;
        private EstadoDeAviso _estado;

        /// <summary>Empieza a mostrar los avisos de este jugador.</summary>
        public void Seguir(AvisoDeAmenazas fuente)
        {
            if (_fuente != null) _fuente.OnAviso -= AlAviso;
            _fuente = fuente;
            _teniaFuente = fuente != null;
            if (fuente == null) return;
            fuente.OnAviso += AlAviso;
            AlAviso(fuente.Tipo, fuente.Estado);
        }

        private void Awake()
        {
            if (grupo != null) grupo.alpha = 0f;
        }

        private void OnEnable() => InputGlyphService.FamilyChanged += AlCambiarDeMando;
        private void OnDisable() => InputGlyphService.FamilyChanged -= AlCambiarDeMando;

        private void OnDestroy()
        {
            if (_fuente != null) _fuente.OnAviso -= AlAviso;
            if (icono != null) icono.DOKill();
        }

        private void AlCambiarDeMando(InputGlyphDeviceFamily _) => PonerGlifo();

        private void AlAviso(TipoDeAviso tipo, EstadoDeAviso estado)
        {
            bool yaEraAhora = _tipo == tipo && _estado == EstadoDeAviso.Ahora;
            _tipo = tipo;
            _estado = estado;

            if (tipo == TipoDeAviso.Ninguno)
            {
                _alfaObjetivo = 0f;
                return;
            }

            PonerGlifo();
            bool ahora = estado == EstadoDeAviso.Ahora;
            bool noBloqueable = tipo == TipoDeAviso.NoBloqueable;
            Color color = noBloqueable ? colorNoBloqueable : ahora ? colorAhora : colorAviso;
            if (anillo != null) anillo.color = color;
            if (glifo != null) glifo.enabled = !noBloqueable;
            if (exclamacion != null)
            {
                exclamacion.enabled = noBloqueable;
                exclamacion.color = color;
            }
            _alfaObjetivo = ahora || noBloqueable ? 1f : alfaAviso;

            if (icono == null) return;
            icono.DOKill();
            icono.localScale = Vector3.one * (ahora ? 1f : escalaAviso);
            if (ahora && !yaEraAhora && golpeDeEscala > 0f)
                icono.DOPunchScale(Vector3.one * golpeDeEscala, 0.25f, 6, 0.6f).SetUpdate(true);
        }

        private void PonerGlifo()
        {
            if (glifo == null) return;
            string nombre = _tipo == TipoDeAviso.Recuperacion ? InputGlyphNames.South : InputGlyphNames.East;
            var sprite = InputGlyphService.GetSprite(nombre);
            if (sprite == null) return;
            glifo.sprite = sprite;
            glifo.preserveAspect = true;
        }

        private void LateUpdate()
        {
            if (_teniaFuente && _fuente == null) { Destroy(gameObject); return; }   // el jugador ya no está
            if (grupo == null) return;

            grupo.alpha = Mathf.MoveTowards(grupo.alpha, _alfaObjetivo, velocidadDeFundido * Time.unscaledDeltaTime);
            if (grupo.alpha <= 0.001f || _fuente == null || icono == null) return;

            if (_camara == null || !_camara.isActiveAndEnabled) _camara = Camera.main;
            if (_camara == null) return;

            Vector3 punto = _fuente.Cabeza.position + Vector3.up * alturaSobreLaCabeza;
            Vector3 pantalla = _camara.WorldToScreenPoint(punto);
            if (pantalla.z <= 0f) { grupo.alpha = 0f; return; }   // detrás de la cámara
            icono.position = pantalla;
        }
    }
}
