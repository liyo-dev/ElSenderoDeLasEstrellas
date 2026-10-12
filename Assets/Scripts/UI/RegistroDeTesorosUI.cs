using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Recuadro «Tesoros» del menú de Start (en la pestaña de objetos): encontrados / total por
/// región, del <see cref="RegistroDeTesoros"/>. Se rellena al mostrarse y cuando se abre un
/// tesoro. Lo monta «Menú de Start · registro de tesoros».
/// </summary>
public sealed class RegistroDeTesorosUI : MonoBehaviour
{
    [SerializeField] private Text titulo;
    [SerializeField] private Text lista;

    private readonly StringBuilder _texto = new StringBuilder();

    private void OnEnable()
    {
        RegistroDeTesoros.Cambiado += Refrescar;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLocaleChanged += Refrescar;
        Refrescar();
    }

    private void OnDisable()
    {
        RegistroDeTesoros.Cambiado -= Refrescar;
        if (LocalizationManager.Instance != null) LocalizationManager.Instance.OnLocaleChanged -= Refrescar;
    }

    public void Refrescar()
    {
        if (titulo != null) titulo.text = Texto("TESORO_REGISTRO_TITULO", "Tesoros");
        if (lista == null) return;

        var regiones = RegistroDeTesoros.Regiones();
        _texto.Clear();
        if (regiones.Count == 0) _texto.Append(Texto("TESORO_REGISTRO_VACIO", "Aún no sabes de ningún tesoro."));
        string fila = Texto("TESORO_REGISTRO_FILA", "{0}: {1} / {2}");
        for (int i = 0; i < regiones.Count; i++)
        {
            if (i > 0) _texto.Append('\n');
            _texto.AppendFormat(fila, regiones[i].Nombre, regiones[i].Encontrados, regiones[i].Total);
        }
        lista.text = _texto.ToString();
    }

    private static string Texto(string clave, string porDefecto)
        => LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, porDefecto) : porDefecto;
}
