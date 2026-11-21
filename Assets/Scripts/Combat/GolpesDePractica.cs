using System;
using UnityEngine;

/// Regla de daño de práctica (IFiltroDeDano): cuenta los golpes que recibe su dueño y, si se pide,
/// los deja sin daño. Para lecciones de combate y entrenamientos: el personaje «recibe» el golpe
/// (se cuenta y se avisa) pero no pierde vida. Se pone y se quita en juego; quien lo pone llama a
/// Damageable.RefrescarFiltros.
[DisallowMultipleComponent]
public sealed class GolpesDePractica : MonoBehaviour, IFiltroDeDano
{
    [Tooltip("Los golpes no quitan vida (solo se cuentan).")]
    [SerializeField] private bool sinDano = true;

    public int Golpes { get; private set; }

    /// Cada golpe recibido, con quién lo dio (puede ser null).
    public event Action<GameObject> AlRecibirGolpe;

    public bool SinDano
    {
        get => sinDano;
        set => sinDano = value;
    }

    public float Filtrar(float cantidad, GameObject instigador)
    {
        if (!isActiveAndEnabled || cantidad <= 0f) return cantidad;
        Golpes++;
        AlRecibirGolpe?.Invoke(instigador);
        return sinDano ? 0f : cantidad;
    }
}
