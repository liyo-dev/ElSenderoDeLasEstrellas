using Core.InputGlyphs;
using TMPro;
using UnityEngine;

/// <summary>
/// Repinta un texto con iconos de botones (<see cref="InputGlyphService.SpriteTag"/>) en cuanto
/// cambia el dispositivo activo (teclado, Xbox, PlayStation…), para que el icono sea siempre el
/// del mando o la tecla con que se está jugando. Con <see cref="Crecer"/>, el hueco se ensancha a lo
/// que mide el icono (las teclas con nombre largo, como «Espacio», son más anchas que un botón) hacia
/// el lado que no choca con lo de alrededor, y arrastra con él lo que va a su lado.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class IconoDeBotonVivo : MonoBehaviour
{
    /// <summary>Hacia dónde se ensancha el hueco cuando el icono no cabe.</summary>
    public enum HaciaDonde { NoCrece, Izquierda, Derecha }

    private TMP_Text _texto;
    private RectTransform _rt;
    private HaciaDonde _crecer = HaciaDonde.NoCrece;
    private RectTransform _arrastrado;
    private bool _pendiente;
    private float _extra;          // lo que se ha ensanchado ahora mismo
    private float _anchoAplicado = -1f;

    private void Awake()
    {
        _texto = GetComponent<TMP_Text>();
        _rt = transform as RectTransform;
    }

    /// <summary>Hace que el hueco se ensanche al ancho del icono hacia <paramref name="hacia"/>;
    /// <paramref name="arrastrar"/> (opcional) se desplaza lo mismo, para que no lo tape.</summary>
    public void Crecer(HaciaDonde hacia, RectTransform arrastrar = null)
    {
        _crecer = hacia;
        _arrastrado = arrastrar;
        if (_texto != null) _texto.alignment = TextAlignmentOptions.Center;
        _pendiente = true;
    }

    private void OnEnable()
    {
        InputGlyphService.FamilyChanged += AlCambiar;
        AlCambiar(default);
    }

    private void OnDisable() => InputGlyphService.FamilyChanged -= AlCambiar;

    private void AlCambiar(InputGlyphDeviceFamily _)
    {
        if (_texto == null) return;
        InputGlyphService.UsarIconos(_texto);
        _texto.SetAllDirty();
        _pendiente = true;
    }

    private void LateUpdate()
    {
        if (!_pendiente || _crecer == HaciaDonde.NoCrece || _rt == null) return;
        _pendiente = false;
        // Si alguien ha vuelto a colocar el hueco, lo ensanchado ya no cuenta.
        if (!Mathf.Approximately(_rt.sizeDelta.x, _anchoAplicado)) _extra = 0f;
        float base_ = _rt.sizeDelta.x - _extra;
        _texto.ForceMeshUpdate();
        float extra = Mathf.Max(0f, _texto.preferredWidth - base_);
        float cambio = extra - _extra;
        if (Mathf.Abs(cambio) < 0.5f) { _anchoAplicado = _rt.sizeDelta.x; return; }
        _rt.sizeDelta = new Vector2(base_ + extra, _rt.sizeDelta.y);
        float mover = _crecer == HaciaDonde.Izquierda ? -cambio * (1f - _rt.pivot.x) : cambio * _rt.pivot.x;
        _rt.anchoredPosition += new Vector2(mover, 0f);
        if (_crecer == HaciaDonde.Derecha && _arrastrado != null) _arrastrado.anchoredPosition += new Vector2(cambio, 0f);
        if (_crecer == HaciaDonde.Izquierda && _arrastrado != null) _arrastrado.anchoredPosition -= new Vector2(cambio, 0f);
        _extra = extra;
        _anchoAplicado = _rt.sizeDelta.x;
    }
}
