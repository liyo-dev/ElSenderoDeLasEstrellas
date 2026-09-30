using System;

/// Marca que el golpe que se está aplicando ahora mismo es un golpe especial de remate (un dúo).
/// Quien lo da abre un ámbito alrededor de sus TakeDamage (using (GolpeDeRemate.Abrir()) { ... });
/// las reglas que lo necesitan (RemateObligatorio) lo consultan. El daño se aplica en el mismo
/// fotograma, así que no hace falta pasar nada por el golpe. Ver INC-489.
public static class GolpeDeRemate
{
    private static int s_abiertos;

    public static bool EnCurso => s_abiertos > 0;

    public static Ambito Abrir()
    {
        s_abiertos++;
        return new Ambito();
    }

    public readonly struct Ambito : IDisposable
    {
        public void Dispose() { if (s_abiertos > 0) s_abiertos--; }
    }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_abiertos = 0;
#endif
}
