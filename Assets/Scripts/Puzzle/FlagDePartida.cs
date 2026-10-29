using System;
using System.Collections.Generic;

/// <summary>
/// Un flag de la partida (preset activo) que marca que algo ya se hizo: un puzle resuelto, un
/// contador completo, un mecanismo activado. Reúne en un solo sitio cómo se marca, se borra y
/// cómo se avisa al cargar la partida si ya estaba puesto, para que cada pieza no repita la
/// suscripción a GameBootService / PlayerPresetService.
/// </summary>
public sealed class FlagDePartida
{
    private readonly string _sistema;
    private readonly Func<string> _clave;
    private readonly Action _alEncontrarlo;
    private bool _escuchando;

    /// <param name="sistema">Nombre para el diagnóstico de suscriptores (ProfileReadyDiagnostics).</param>
    /// <param name="clave">Clave del flag (se pide cada vez, por si el id se resuelve tarde).</param>
    /// <param name="alEncontrarlo">Se llama al cargar la partida si el flag ya estaba puesto.</param>
    public FlagDePartida(string sistema, Func<string> clave, Action alEncontrarlo)
    {
        _sistema = sistema;
        _clave = clave;
        _alEncontrarlo = alEncontrarlo;
    }

    public string Clave => _clave();

    /// <summary>Empieza a escuchar la carga de la partida (llamar en OnEnable).</summary>
    public void Escuchar()
    {
        if (_escuchando) return;
        _escuchando = true;
        GameBootService.OnProfileReady += Comprobar;
        PlayerPresetService.OnPresetApplied += Comprobar;
        ProfileReadyDiagnostics.RegisterSubscriber(_sistema);

        if (GameBootService.IsAvailable || PlayerPresetService.HasAppliedPreset)
            Comprobar();
    }

    /// <summary>Deja de escuchar (llamar en OnDisable).</summary>
    public void DejarDeEscuchar()
    {
        if (!_escuchando) return;
        _escuchando = false;
        GameBootService.OnProfileReady -= Comprobar;
        PlayerPresetService.OnPresetApplied -= Comprobar;
    }

    public bool EstaPuesto() => EstaPuesto(Clave);

    public bool Marcar() => Marcar(Clave);

    public bool Borrar() => Borrar(Clave);

    private void Comprobar()
    {
        if (EstaPuesto()) _alEncontrarlo?.Invoke();
    }

    public static bool EstaPuesto(string clave)
    {
        var flags = Partida.Preset?.flags;
        return flags != null && !string.IsNullOrEmpty(clave) && flags.Contains(clave);
    }

    public static bool Marcar(string clave)
    {
        var preset = Partida.Preset;
        if (preset == null || string.IsNullOrEmpty(clave)) return false;
        preset.flags ??= new List<string>();
        if (preset.flags.Contains(clave)) return false;
        preset.flags.Add(clave);
        return true;
    }

    public static bool Borrar(string clave)
    {
        var flags = Partida.Preset?.flags;
        return flags != null && flags.Remove(clave);
    }

    /// <summary>Borra todos los flags que empiezan por el prefijo. Devuelve cuántos.</summary>
    public static int BorrarConPrefijo(string prefijo)
    {
        var flags = Partida.Preset?.flags;
        if (flags == null || string.IsNullOrEmpty(prefijo)) return 0;
        return flags.RemoveAll(f => f != null && f.StartsWith(prefijo, StringComparison.Ordinal));
    }
}
