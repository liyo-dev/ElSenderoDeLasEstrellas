using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Slot = PartyControlManager.CharacterSlot;

namespace Sendero.UI
{
    /// Dónde va un retrato en el carrusel del HUD.
    public enum PosicionDeRetrato { Oculto, Centro, Izquierda, Derecha }

    /// <summary>
    /// Retrato de busto de un personaje del grupo en el HUD. Lo coloca <see cref="PartyHUDPanel"/>
    /// (centro el que va al mando, a los lados los demás) y reacciona a lo que le pasa al personaje:
    /// sacudida y destello rojo al recibir un golpe, respiración cansada con poca vida, gris y
    /// hundido si ha caído, halo dorado con el trío listo. La imagen la pide a una
    /// <see cref="IFuenteDeRetrato"/>; el corte a la altura del pecho lo hace el RectMask2D del
    /// contenedor, cuyo borde inferior es el borde superior del panel.
    ///
    /// Jerarquía: raíz (posición y escala del carrusel) → Halo, Cuerpo (hundido de caído, saltito)
    /// → Vaivén (respiración, sacudida) → Imagen (RawImage con el shader UI/RetratoDelGrupo), y
    /// la etiqueta de K.O. (texto con LocalizedText). Todas las animaciones van en tiempo no escalado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RetratoDelGrupo : MonoBehaviour
    {
        private static readonly int IdSaturacion = Shader.PropertyToID("_Saturacion");
        private static readonly int IdBrillo = Shader.PropertyToID("_Brillo");
        private static readonly int IdDestello = Shader.PropertyToID("_Destello");

        [Header("Piezas")]
        [SerializeField] private RawImage imagen;
        [SerializeField] private Image halo;
        [SerializeField] private RectTransform cuerpo;
        [SerializeField] private RectTransform vaiven;
        [SerializeField] private TMP_Text etiquetaCaido;

        [Header("Tamaño")]
        [Tooltip("Alto que se ve por encima del borde del panel con escala 1 (unidades del canvas).")]
        [SerializeField] private float altoVisible = 300f;
        [Tooltip("Desplazamiento vertical de la imagen para separar la cara de la etiqueta del nombre (unidades del canvas).")]
        [SerializeField] private float elevacionDeLaImagen = 45f;
        [Tooltip("Escala de los retratos de los lados.")]
        [SerializeField] private float escalaLateral = 0.68f;

        [Header("Aspecto")]
        [SerializeField, Range(0f, 1f)] private float saturacionLateral = 0.45f;
        [SerializeField, Range(0f, 1f)] private float brilloLateral = 0.62f;
        [SerializeField, Range(0f, 1f)] private float brilloCaido = 0.45f;
        [SerializeField, Range(0f, 1f)] private float haloDelActivo = 0.75f;
        [SerializeField] private Color colorDelTrio = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color colorDelGolpe = new Color(1f, 0.18f, 0.18f, 1f);

        [Header("Barras de vida y maná")]
        [Tooltip("Muestra la vida y el maná del personaje al pie de su retrato (cada uno tiene los suyos, INC-717). Crecen con el retrato: las del que va al mando se ven más grandes.")]
        [SerializeField] private bool mostrarBarras = true;
        [Tooltip("Ancho y alto de cada barra con escala 1 (unidades del canvas).")]
        [SerializeField] private Vector2 tamanoDeBarra = new Vector2(190f, 12f);
        [Tooltip("Altura de las barras sobre el borde del panel.")]
        [SerializeField] private float alturaDeLasBarras = 12f;
        [SerializeField] private Color colorDeVida = new Color(0.36f, 0.85f, 0.42f, 1f);
        [SerializeField] private Color colorDeVidaBaja = new Color(0.95f, 0.32f, 0.25f, 1f);
        [SerializeField] private Color colorDeMana = new Color(0.36f, 0.62f, 1f, 1f);
        [SerializeField] private Color colorDeFondoDeBarra = new Color(0f, 0f, 0f, 0.6f);

        [Header("Tiempos")]
        [SerializeField] private float duracionDelCambio = 0.45f;
        [SerializeField] private float alturaDelSalto = 14f;
        [SerializeField] private float duracionDelGolpe = 0.4f;
        [Tooltip("Segundos que dura la cara de dolor tras un golpe.")]
        [SerializeField] private float caraDeGolpe = 0.6f;
        [SerializeField] private float periodoDeRespiracion = 1.4f;
        [SerializeField] private float hundidoDeCaido = 14f;
        [SerializeField] private float inclinacionDeCaido = -6f;

        private Slot _personaje;
        private Color _color = Color.white;
        private IFuenteDeRetrato _fuente;
        private IFuenteDeRetrato _respaldo;
        private IFuenteDeRetratoVivo _vivo;
        private Material _material;
        private RectTransform _raiz;

        private PosicionDeRetrato _posicion = PosicionDeRetrato.Oculto;
        private bool _cansado, _caido, _trio;
        private float _finDelGolpe;
        private ExpresionDeRetrato _expresion = (ExpresionDeRetrato)(-1);
        private float _saturacion = 1f, _brillo = 1f;

        private RectTransform _barras, _rellenoDeVida, _rellenoDeMana;
        private Image _imagenDeVida;

        private Tween _tCarrusel, _tEscala, _tAspecto, _tHalo, _tCaido, _tGiro, _tSalto, _tSacudida, _tDestello, _tRespiracion, _tCara;

        public Slot Personaje => _personaje;
        public PosicionDeRetrato Posicion => _posicion;

        // El panel puede configurar y colocar el retrato antes de su Awake (al activarse el HUD,
        // el padre recibe OnEnable antes que los hijos), así que la raíz y el material se
        // preparan la primera vez que hacen falta.
        private RectTransform Raiz => _raiz != null ? _raiz : (_raiz = (RectTransform)transform);

        private void Awake()
        {
            AsegurarMaterial();
            if (etiquetaCaido != null && !_caido) etiquetaCaido.gameObject.SetActive(false);
        }

        private void AsegurarMaterial()
        {
            if (_material != null || imagen == null || imagen.material == null) return;
            _material = new Material(imagen.material) { name = imagen.material.name + " (" + name + ")" };
            imagen.material = _material;
            AplicarAspecto();
        }

        private void OnEnable()
        {
            if (_cansado && !_caido) Respirar(true);
            if (_vivo != null && _posicion != PosicionDeRetrato.Oculto) _vivo.Mostrar(_personaje, true);
        }

        private void OnDisable()
        {
            // Lo pasajero (sacudida, destello, saltito, respiración) se corta y vuelve a reposo.
            Matar(ref _tSalto, true);
            Matar(ref _tSacudida, true);
            Matar(ref _tDestello, false);
            Matar(ref _tCara, false);
            Respirar(false);
            _finDelGolpe = 0f;
            if (_vivo != null) _vivo.Mostrar(_personaje, false);
            if (_material != null) _material.SetColor(IdDestello, new Color(colorDelGolpe.r, colorDelGolpe.g, colorDelGolpe.b, 0f));
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        /// Quién es y de dónde saca la imagen (respaldo: si la fuente principal no la tiene).
        public void Configurar(Slot personaje, Color color, IFuenteDeRetrato fuente, IFuenteDeRetrato respaldo)
        {
            _personaje = personaje;
            _color = color.a > 0f ? color : Color.white;
            _fuente = fuente;
            _vivo = fuente as IFuenteDeRetratoVivo;
            _respaldo = respaldo;
            _expresion = (ExpresionDeRetrato)(-1);
            AsegurarMaterial();
            RefrescarImagen();
            RefrescarAspecto(false);
        }

        /// Coloca el retrato en el carrusel. <paramref name="x"/> es la posición horizontal en su contenedor.
        public void Colocar(PosicionDeRetrato posicion, float x, bool animar)
        {
            bool eraCentro = _posicion == PosicionDeRetrato.Centro;
            _posicion = posicion;
            bool visible = posicion != PosicionDeRetrato.Oculto;
            if (visible && !gameObject.activeSelf) { gameObject.SetActive(true); animar = false; }
            if (_vivo != null) _vivo.Mostrar(_personaje, visible);
            if (!visible)
            {
                Matar(ref _tCarrusel, false);
                Matar(ref _tEscala, false);
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }

            float escala = posicion == PosicionDeRetrato.Centro ? 1f : escalaLateral;
            var destino = new Vector2(x, 0f);
            Matar(ref _tCarrusel, false);
            Matar(ref _tEscala, false);
            if (animar)
            {
                _tCarrusel = Raiz.DOAnchorPos(destino, duracionDelCambio).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);
                _tEscala = Raiz.DOScale(escala, duracionDelCambio).SetEase(Ease.OutBack, 1.2f).SetUpdate(true);
            }
            else
            {
                Raiz.anchoredPosition = destino;
                Raiz.localScale = Vector3.one * escala;
            }

            if (posicion == PosicionDeRetrato.Centro && !eraCentro && animar && !_caido && cuerpo != null)
            {
                Matar(ref _tSalto, true);
                _tSalto = cuerpo.DOPunchAnchorPos(new Vector2(0f, alturaDelSalto), duracionDelCambio, 1, 0f).SetUpdate(true);
                if (_vivo != null) _vivo.Reaccionar(_personaje, ReaccionDeRetrato.Saludo);
            }
            RefrescarAspecto(animar);
        }

        /// Vida baja y caído, tal como los da <see cref="EstadoDelGrupoParaRetratos"/>.
        public void PonerSalud(bool cansado, bool caido)
        {
            bool cambiaCaido = caido != _caido;
            _caido = caido;
            _cansado = cansado && !caido;
            Respirar(_cansado && isActiveAndEnabled);
            if (cambiaCaido) PonerPoseDeCaido(caido);
            RefrescarImagen();
            RefrescarAspecto(true);
        }

        /// Vida y maná (0..1) en las barras del pie del retrato; vida baja en rojo.
        public void PonerBarras(float vida, float mana, bool vidaBaja)
        {
            if (!mostrarBarras) return;
            CrearBarras();
            Llenar(_rellenoDeVida, vida);
            Llenar(_rellenoDeMana, mana);
            if (_imagenDeVida != null) _imagenDeVida.color = vidaBaja ? colorDeVidaBaja : colorDeVida;
        }

        private static void Llenar(RectTransform relleno, float parte)
        {
            if (relleno == null) return;
            relleno.localScale = new Vector3(Mathf.Clamp01(parte), 1f, 1f);
        }

        private void CrearBarras()
        {
            if (_barras != null) return;
            var go = new GameObject("Barras", typeof(RectTransform));
            _barras = (RectTransform)go.transform;
            _barras.SetParent(Raiz, false);
            _barras.anchorMin = _barras.anchorMax = new Vector2(0.5f, 0f);
            _barras.pivot = new Vector2(0.5f, 0f);
            _barras.sizeDelta = new Vector2(tamanoDeBarra.x, tamanoDeBarra.y * 2f + 4f);
            _barras.anchoredPosition = new Vector2(0f, alturaDeLasBarras);
            _barras.SetAsLastSibling();
            _rellenoDeMana = Barra("Mana", 0f, colorDeMana, out _);
            _rellenoDeVida = Barra("Vida", tamanoDeBarra.y + 4f, colorDeVida, out _imagenDeVida);
        }

        private RectTransform Barra(string nombre, float y, Color color, out Image relleno)
        {
            var fondo = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            var rtFondo = (RectTransform)fondo.transform;
            rtFondo.SetParent(_barras, false);
            rtFondo.anchorMin = new Vector2(0f, 0f);
            rtFondo.anchorMax = new Vector2(1f, 0f);
            rtFondo.pivot = new Vector2(0.5f, 0f);
            rtFondo.sizeDelta = new Vector2(0f, tamanoDeBarra.y);
            rtFondo.anchoredPosition = new Vector2(0f, y);
            var imgFondo = fondo.GetComponent<Image>();
            imgFondo.color = colorDeFondoDeBarra;
            imgFondo.raycastTarget = false;

            var lleno = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)lleno.transform;
            rt.SetParent(rtFondo, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(1.5f, 1.5f);
            rt.offsetMax = new Vector2(-1.5f, -1.5f);
            relleno = lleno.GetComponent<Image>();
            relleno.color = color;
            relleno.raycastTarget = false;
            return rt;
        }

        /// Le acaban de dar: sacudida, destello rojo y cara de dolor un momento.
        public void Golpe()
        {
            if (!isActiveAndEnabled || _caido) return;
            _finDelGolpe = Time.unscaledTime + caraDeGolpe;
            if (_vivo != null) _vivo.Reaccionar(_personaje, ReaccionDeRetrato.Golpe);
            if (vaiven != null)
            {
                Matar(ref _tSacudida, true);
                _tSacudida = vaiven.DOShakeAnchorPos(duracionDelGolpe * 0.95f, new Vector2(8f, 0f), 30, 0f, false, true).SetUpdate(true);
            }
            if (_material != null)
            {
                Matar(ref _tDestello, false);
                var desde = new Color(colorDelGolpe.r, colorDelGolpe.g, colorDelGolpe.b, 0.75f);
                var hasta = new Color(colorDelGolpe.r, colorDelGolpe.g, colorDelGolpe.b, 0f);
                _material.SetColor(IdDestello, desde);
                _tDestello = DOTween.To(() => _material.GetColor(IdDestello), c => _material.SetColor(IdDestello, c), hasta, duracionDelGolpe)
                    .SetEase(Ease.OutQuad).SetUpdate(true);
            }
            RefrescarImagen();
            Matar(ref _tCara, false);
            _tCara = DOVirtual.DelayedCall(caraDeGolpe, RefrescarImagen, true);
        }

        /// Trío listo: color completo y halo dorado (si no ha caído).
        public void PonerTrioListo(bool listo)
        {
            if (_trio == listo) return;
            _trio = listo;
            RefrescarImagen();
            RefrescarAspecto(true);
        }

        // ─── Imagen ────────────────────────────────────────────────────────────

        private ExpresionDeRetrato ExpresionActual()
        {
            if (_caido) return ExpresionDeRetrato.Caido;
            if (Time.unscaledTime < _finDelGolpe || _cansado) return ExpresionDeRetrato.Dolorido;
            if (_trio) return ExpresionDeRetrato.Contento;
            return ExpresionDeRetrato.Normal;
        }

        private void RefrescarImagen()
        {
            if (imagen == null) return;
            var expresion = ExpresionActual();
            if (expresion == _expresion && imagen.texture != null) return;
            if (!Obtener(expresion, out var img)) return;
            _expresion = expresion;
            imagen.texture = img.Textura;
            imagen.uvRect = img.Uv;
            var rt = imagen.rectTransform;
            float alto = altoVisible / Mathf.Max(0.1f, 1f - img.FraccionBajoElBorde);
            rt.sizeDelta = new Vector2(alto * img.Aspecto, alto);
            rt.anchoredPosition = new Vector2(0f, elevacionDeLaImagen - alto * img.FraccionBajoElBorde);
        }

        private bool Obtener(ExpresionDeRetrato expresion, out ImagenDeRetrato img)
        {
            img = default;
            if (_fuente != null && _fuente.TryObtener(_personaje, expresion, out img) && img.Valida) return true;
            return _respaldo != null && _respaldo.TryObtener(_personaje, expresion, out img) && img.Valida;
        }

        // ─── Aspecto (saturación, brillo, halo) ────────────────────────────────

        private void RefrescarAspecto(bool animar)
        {
            bool centro = _posicion == PosicionDeRetrato.Centro;
            float sat, bri, haloA;
            Color haloC;
            if (_caido) { sat = 0f; bri = brilloCaido; haloA = 0f; haloC = _color; }
            else if (_trio) { sat = 1f; bri = 1.05f; haloA = 1f; haloC = colorDelTrio; }
            else if (centro) { sat = 1f; bri = 1f; haloA = haloDelActivo; haloC = _color; }
            else { sat = saturacionLateral; bri = brilloLateral; haloA = 0f; haloC = _color; }

            float d = animar ? duracionDelCambio : 0f;
            if (_material != null)
            {
                Matar(ref _tAspecto, false);
                if (d <= 0f) { _saturacion = sat; _brillo = bri; AplicarAspecto(); }
                else
                {
                    float s0 = _saturacion, b0 = _brillo;
                    _tAspecto = DOVirtual.Float(0f, 1f, d, t =>
                    {
                        _saturacion = Mathf.Lerp(s0, sat, t);
                        _brillo = Mathf.Lerp(b0, bri, t);
                        AplicarAspecto();
                    }).SetUpdate(true);
                }
            }
            if (halo != null)
            {
                Matar(ref _tHalo, false);
                var c = new Color(haloC.r, haloC.g, haloC.b, haloA);
                if (d <= 0f) halo.color = c;
                else _tHalo = halo.DOColor(c, d).SetUpdate(true);
            }
        }

        private void AplicarAspecto()
        {
            if (_material == null) return;
            _material.SetFloat(IdSaturacion, _saturacion);
            _material.SetFloat(IdBrillo, _brillo);
        }

        // ─── Poses ─────────────────────────────────────────────────────────────

        private void PonerPoseDeCaido(bool caido)
        {
            if (etiquetaCaido != null) etiquetaCaido.gameObject.SetActive(caido);
            if (cuerpo == null) return;
            Matar(ref _tSalto, true);
            Matar(ref _tCaido, false);
            Matar(ref _tGiro, false);
            var pos = new Vector2(0f, caido ? -hundidoDeCaido : 0f);
            var giro = new Vector3(0f, 0f, caido ? inclinacionDeCaido : 0f);
            if (!isActiveAndEnabled) { cuerpo.anchoredPosition = pos; cuerpo.localEulerAngles = giro; return; }
            _tCaido = cuerpo.DOAnchorPos(pos, duracionDelCambio).SetEase(Ease.OutCubic).SetUpdate(true);
            _tGiro = cuerpo.DOLocalRotate(giro, duracionDelCambio).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        private void Respirar(bool si)
        {
            if (vaiven == null) return;
            if (si)
            {
                if (_tRespiracion != null && _tRespiracion.IsActive()) return;
                vaiven.localScale = Vector3.one;
                _tRespiracion = vaiven.DOScaleY(0.97f, periodoDeRespiracion * 0.5f)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
            else
            {
                Matar(ref _tRespiracion, false);
                vaiven.localScale = Vector3.one;
            }
        }

        private static void Matar(ref Tween tween, bool completar)
        {
            if (tween != null && tween.IsActive()) tween.Kill(completar);
            tween = null;
        }
    }
}
