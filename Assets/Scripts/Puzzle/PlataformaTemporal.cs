using System.Collections;
using UnityEngine;

/// <summary>
/// Plataforma que aparece y desaparece por eventos (por ejemplo, mientras un cristal está
/// encendido). Al desaparecer quita su collider: si el jugador está encima, cae.
/// Parpadea durante el último tramo antes de irse para avisar.
/// </summary>
public sealed class PlataformaTemporal : MonoBehaviour
{
    [SerializeField] private bool visibleAlEmpezar;
    [Tooltip("Colliders que se activan con la plataforma (si se deja vacío, los del objeto y sus hijos).")]
    [SerializeField] private Collider[] colliders;
    [Tooltip("Renderers que se ven con la plataforma (si se deja vacío, los del objeto y sus hijos).")]
    [SerializeField] private Renderer[] renderers;
    [SerializeField, Min(0f)] private float segundosDeAviso = 1f;
    [SerializeField, Min(0.02f)] private float ritmoDelParpadeo = 0.1f;
    [SerializeField] private string sfxAlAparecer = "RuneActivate";

    private Coroutine _ocultar;
    private bool _visible;

    public bool Visible => _visible;

    private void Awake()
    {
        if (colliders == null || colliders.Length == 0) colliders = GetComponentsInChildren<Collider>(true);
        if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
        Aplicar(visibleAlEmpezar, true);
        _visible = visibleAlEmpezar;
    }

    /// <summary>Aparece y se queda hasta que se llame a Ocultar().</summary>
    public void Mostrar()
    {
        Cancelar();
        Aparecer();
    }

    /// <summary>Aparece durante unos segundos (con parpadeo al final).</summary>
    public void MostrarDurante(float segundos)
    {
        Cancelar();
        Aparecer();
        _ocultar = StartCoroutine(Co_OcultarTras(segundos));
    }

    /// <summary>Desaparece con el aviso de parpadeo.</summary>
    public void Ocultar()
    {
        if (!_visible) return;
        Cancelar();
        _ocultar = StartCoroutine(Co_OcultarTras(0f));
    }

    /// <summary>Desaparece al instante, sin aviso.</summary>
    public void OcultarYa()
    {
        Cancelar();
        _visible = false;
        Aplicar(false, true);
    }

    private void Aparecer()
    {
        if (!_visible && !string.IsNullOrEmpty(sfxAlAparecer))
            AudioService.Instance?.PlaySFX(sfxAlAparecer, 0.8f, transform.position);
        _visible = true;
        Aplicar(true, true);
    }

    private IEnumerator Co_OcultarTras(float segundos)
    {
        float espera = Mathf.Max(0f, segundos - segundosDeAviso);
        if (espera > 0f) yield return new WaitForSeconds(espera);

        float fin = Time.time + segundosDeAviso;
        bool ver = true;
        while (Time.time < fin)
        {
            ver = !ver;
            Aplicar(ver, false);
            yield return new WaitForSeconds(ritmoDelParpadeo);
        }
        _ocultar = null;
        _visible = false;
        Aplicar(false, true);
    }

    private void Cancelar()
    {
        if (_ocultar == null) return;
        StopCoroutine(_ocultar);
        _ocultar = null;
    }

    private void Aplicar(bool ver, bool tambienColliders)
    {
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = ver;
        if (!tambienColliders) return;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = ver;
    }
}
