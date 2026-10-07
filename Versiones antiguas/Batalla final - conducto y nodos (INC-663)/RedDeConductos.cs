using System;
using UnityEngine;

/// Los conductos de sombra del altar (fase 3): varios nodos que hay que cortar casi a la vez.
/// Cuando se rompe el primero empieza la cuenta; si antes de 'ventana' segundos están todos
/// rotos, el Mago se queda sin conducto (AlCortarseTodos); si no, los rotos se reconectan
/// (AlReconectarse) y vuelta a empezar. Ver INC-509.
public sealed class RedDeConductos : MonoBehaviour
{
    [SerializeField] private NodoDeConducto[] nodos;
    [Tooltip("Segundos desde el primer nodo roto para romper los demás.")]
    [SerializeField] private float ventana = 8f;

    private Transform _altar;
    private float _primeraRotura = -1f;

    public bool Activa { get; private set; }
    public NodoDeConducto[] Nodos => nodos;
    /// Segundos que quedan para romper el resto (0 si no hay cuenta en marcha).
    public float TiempoRestante => _primeraRotura < 0f ? 0f : Mathf.Max(0f, ventana - (Time.time - _primeraRotura));

    public event Action AlCortarseTodos;
    public event Action AlReconectarse;
    public event Action AlEmpezarCuenta;

    void Awake()
    {
        foreach (var n in nodos) if (n) n.AlRomperse += AlRomperseNodo;
    }

    void OnDestroy()
    {
        foreach (var n in nodos) if (n) n.AlRomperse -= AlRomperseNodo;
    }

    public void Activar(Transform altar)
    {
        _altar = altar;
        _primeraRotura = -1f;
        Activa = true;
        foreach (var n in nodos) if (n) n.Activar(altar);
    }

    public void Apagar()
    {
        Activa = false;
        _primeraRotura = -1f;
        foreach (var n in nodos) if (n) n.Apagar();
    }

    private void AlRomperseNodo(NodoDeConducto _)
    {
        if (!Activa) return;
        if (_primeraRotura < 0f)
        {
            _primeraRotura = Time.time;
            AlEmpezarCuenta?.Invoke();
        }

        foreach (var n in nodos) if (n && !n.Roto) return;

        _primeraRotura = -1f;
        Activa = false;
        AlCortarseTodos?.Invoke();
    }

    void Update()
    {
        if (!Activa || _primeraRotura < 0f || Time.time - _primeraRotura < ventana) return;

        _primeraRotura = -1f;
        foreach (var n in nodos) if (n && n.Roto) n.Activar(_altar);
        AlReconectarse?.Invoke();
    }
}
