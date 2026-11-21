using System;
using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Atuendos de escena (<see cref="AtuendoDeEscena"/>): ropa que se pone encima de la de cada
/// personaje y se le devuelve al quitarla. La apariencia que guarda este registro (y la que va a
/// la partida) es siempre la de debajo; el cuerpo de cada personaje enseña la de debajo con los
/// atuendos encima (<see cref="AparienciaVisible"/>), lo lleve el jugador o la IA. Varios sistemas
/// pueden poner atuendos a la vez, cada uno con su clave (como <c>PlayerHUDV2.HideHUD</c>); en lo
/// que coincidan, manda el último puesto.
/// </summary>
public partial class CharacterAppearanceRegistry
{
    private readonly struct AtuendoPuesto
    {
        public readonly string quien;
        public readonly AtuendoDeEscena atuendo;
        public AtuendoPuesto(string quien, AtuendoDeEscena atuendo) { this.quien = quien; this.atuendo = atuendo; }
    }

    private readonly List<AtuendoPuesto> _atuendos = new();

    // Lo que enseña cada cuerpo tras vestirlo con atuendo, para separar después lo que haya
    // cambiado el jugador (menú de equipo) de lo que pone el atuendo.
    private readonly Dictionary<PartCategory, string>[] _mostradaConAtuendo =
        new Dictionary<PartCategory, string>[ControlDelJugador.Personajes];

    // Cuerpos que acaban de aparecer en escena y se visten en cuanto su vestidor esté listo.
    private readonly bool[] _porVestir = new bool[ControlDelJugador.Personajes];

    private static readonly List<Renderer> _renderers = new();

    /// <summary>
    /// Pone un atuendo con la clave de quien lo pide. Con la misma clave sustituye al anterior.
    /// Se ve al momento en el cuerpo de cada personaje al que toca.
    /// </summary>
    public void PonerAtuendo(string quien, AtuendoDeEscena atuendo)
    {
        if (string.IsNullOrEmpty(quien) || atuendo == null) return;
        int i = IndiceDeAtuendo(quien);
        if (i >= 0 && _atuendos[i].atuendo == atuendo) return;

        var anterior = i >= 0 ? _atuendos[i].atuendo : null;
        GuardarRopaDeLosTocados(atuendo, anterior);
        if (i >= 0) _atuendos.RemoveAt(i);
        _atuendos.Add(new AtuendoPuesto(quien, atuendo));
        VestirLosTocados(atuendo, anterior);
    }

    /// <summary>Quita el atuendo que puso <paramref name="quien"/> y devuelve la ropa de debajo.</summary>
    public void QuitarAtuendo(string quien)
    {
        int i = IndiceDeAtuendo(quien);
        if (i < 0) return;

        var quitado = _atuendos[i].atuendo;
        GuardarRopaDeLosTocados(quitado, null);
        _atuendos.RemoveAt(i);
        VestirLosTocados(quitado, null);
    }

    /// <summary>El personaje lleva algún atuendo de escena.</summary>
    public bool LlevaAtuendo(Slot personaje)
    {
        for (int i = 0; i < _atuendos.Count; i++)
            if (_atuendos[i].atuendo != null && _atuendos[i].atuendo.De(personaje) != null) return true;
        return false;
    }

    /// <summary>Lo que le dan a su forma de moverse los atuendos que lleva el personaje.</summary>
    public RasgosDeMovimiento RasgosDe(Slot personaje)
    {
        var rasgos = default(RasgosDeMovimiento);
        for (int i = 0; i < _atuendos.Count; i++)
        {
            var parte = _atuendos[i].atuendo != null ? _atuendos[i].atuendo.De(personaje) : null;
            if (parte != null) rasgos = RasgosDeMovimiento.Sumar(rasgos, parte.rasgos);
        }
        return rasgos;
    }

    /// <summary>
    /// La ropa de debajo del personaje (sin atuendos), al día con lo que el jugador haya cambiado
    /// en el menú de equipo. Es lo que se guarda en la partida.
    /// </summary>
    public Dictionary<PartCategory, string> RopaDeDebajo(Slot personaje)
    {
        if (_aplicada[(int)personaje]) CaptureCurrentAppearance(personaje);
        return GetAppearance(personaje);
    }

    /// <summary>Lo que se ve del personaje: la ropa de debajo con los atuendos encima, por orden.</summary>
    public Dictionary<PartCategory, string> AparienciaVisible(Slot personaje)
    {
        var debajo = _appearances[(int)personaje];
        if (!LlevaAtuendo(personaje)) return debajo;
        var visible = debajo != null ? new Dictionary<PartCategory, string>(debajo) : new Dictionary<PartCategory, string>();
        for (int i = 0; i < _atuendos.Count; i++)
            if (_atuendos[i].atuendo != null) _atuendos[i].atuendo.Vestir(personaje, visible);
        return visible;
    }

    private int IndiceDeAtuendo(string quien)
    {
        for (int i = 0; i < _atuendos.Count; i++)
            if (_atuendos[i].quien == quien) return i;
        return -1;
    }

    private static bool Toca(AtuendoDeEscena atuendo, Slot personaje) => atuendo != null && atuendo.De(personaje) != null;

    // Un cuerpo que este registro aún no ha vestido no tiene nada propio que guardar: su
    // apariencia llega después (preset o valores de partida).
    private void GuardarRopaDeLosTocados(AtuendoDeEscena uno, AtuendoDeEscena otro)
    {
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var p = (Slot)i;
            if ((Toca(uno, p) || Toca(otro, p)) && _aplicada[i]) CaptureCurrentAppearance(p);
        }
    }

    private void VestirLosTocados(AtuendoDeEscena uno, AtuendoDeEscena otro)
    {
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var p = (Slot)i;
            if (Toca(uno, p) || Toca(otro, p)) ApplyAppearance(p);
        }
    }

    /// <summary>Lo que el cuerpo enseña tras vestirlo (solo con atuendo).</summary>
    private void RecordarLoMostrado(Slot personaje, ModularAutoBuilder builder)
    {
        _mostradaConAtuendo[(int)personaje] = LlevaAtuendo(personaje) ? builder.GetSelection() : null;
    }

    /// <summary>
    /// De lo que enseña el cuerpo, separa lo del atuendo: devuelve la ropa de debajo con los
    /// cambios que haya hecho el jugador desde que se vistió. Sin atuendo, la selección tal cual.
    /// </summary>
    private Dictionary<PartCategory, string> QuitarLoDelAtuendo(Slot personaje, Dictionary<PartCategory, string> seleccion)
    {
        var mostrada = _mostradaConAtuendo[(int)personaje];
        if (mostrada == null) return seleccion;

        var debajo = _appearances[(int)personaje] != null
            ? new Dictionary<PartCategory, string>(_appearances[(int)personaje])
            : new Dictionary<PartCategory, string>();
        foreach (var kv in seleccion)
            if (!mostrada.TryGetValue(kv.Key, out var antes) || antes != kv.Value) debajo[kv.Key] = kv.Value;
        foreach (var kv in mostrada)
            if (!seleccion.ContainsKey(kv.Key)) debajo.Remove(kv.Key);

        _mostradaConAtuendo[(int)personaje] = new Dictionary<PartCategory, string>(seleccion);
        return debajo;
    }

    // ─── Cuerpos que aparecen ─────────────────────────────────────────────────

    private void OnEnable() => PilotoDelCuerpo.Aparece += AlAparecerCuerpo;

    private void OnDisable() => PilotoDelCuerpo.Aparece -= AlAparecerCuerpo;

    // Un cuerpo nuevo en escena (carga de escena) se viste con lo de su personaje. Se espera a que
    // su vestidor haya arrancado: el piloto avisa antes de que despierten los demás componentes.
    private void AlAparecerCuerpo(PilotoDelCuerpo cuerpo)
    {
        if (cuerpo == null || !ControlDelJugador.PersonajeDe(cuerpo.gameObject, out var p)) return;
        _aplicada[(int)p] = false;
        _materiales[(int)p].Clear();
        _porVestir[(int)p] = true;
    }

    private void Update()
    {
        for (int i = 0; i < _porVestir.Length; i++)
        {
            if (!_porVestir[i]) continue;
            _porVestir[i] = false;
            ApplyAppearance((Slot)i);
        }
    }

    // ─── Tintes ───────────────────────────────────────────────────────────────

    /// Devuelve los materiales que se cambiaron en ese cuerpo y pone los tintes de los atuendos
    /// del personaje (cada pieza con su material duplicado, en todas sus ranuras).
    private void Tenir(ModularAutoBuilder ropa, Slot personaje, Dictionary<Renderer, Material[]> originales)
    {
        RestaurarMateriales(originales);
        if (ropa == null || !HayTintes(personaje)) return;

        ropa.GetComponentsInChildren(true, _renderers);
        for (int i = 0; i < _atuendos.Count; i++)
        {
            var parte = _atuendos[i].atuendo != null ? _atuendos[i].atuendo.De(personaje) : null;
            if (parte == null) continue;
            foreach (var tinte in parte.tintes)
            {
                if (tinte == null || tinte.material == null || string.IsNullOrEmpty(tinte.pieza)) continue;
                foreach (var r in _renderers)
                {
                    if (!string.Equals(r.name, tinte.pieza, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!originales.ContainsKey(r)) originales[r] = r.sharedMaterials;
                    var materiales = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int m = 0; m < materiales.Length; m++) materiales[m] = tinte.material;
                    r.sharedMaterials = materiales;
                }
            }
        }
        _renderers.Clear();
    }

    /// Pone en ese cuerpo las prendas sueltas de los atuendos del personaje (y quita las que ya no tocan).
    private void PonerPrendasSueltas(ModularAutoBuilder ropa, Slot personaje)
    {
        if (ropa == null) return;
        _prendas.Clear();
        for (int i = 0; i < _atuendos.Count; i++)
        {
            var parte = _atuendos[i].atuendo != null ? _atuendos[i].atuendo.De(personaje) : null;
            if (parte == null) continue;
            foreach (var p in parte.prendasSueltas) if (p != null) _prendas.Add(p);
        }
        var sueltas = ropa.GetComponent<PrendasSueltas>();
        if (sueltas == null)
        {
            if (_prendas.Count == 0) return;
            sueltas = ropa.gameObject.AddComponent<PrendasSueltas>();
        }
        sueltas.Dejar(_prendas);
        _prendas.Clear();
    }

    private readonly List<GameObject> _prendas = new();

    private bool HayTintes(Slot personaje)
    {
        for (int i = 0; i < _atuendos.Count; i++)
        {
            var parte = _atuendos[i].atuendo != null ? _atuendos[i].atuendo.De(personaje) : null;
            if (parte != null && parte.tintes.Count > 0) return true;
        }
        return false;
    }

    private static void RestaurarMateriales(Dictionary<Renderer, Material[]> originales)
    {
        foreach (var kv in originales)
            if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
        originales.Clear();
    }
}
