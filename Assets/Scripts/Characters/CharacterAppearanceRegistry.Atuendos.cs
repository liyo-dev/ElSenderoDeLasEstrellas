using System;
using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Atuendos de escena (<see cref="AtuendoDeEscena"/>): ropa que se pone encima de la de cada
/// personaje y se le devuelve al quitarla. La apariencia que guarda este registro (y la que va a
/// la partida) es siempre la de debajo; el cuerpo del jugador enseña la de debajo con los atuendos
/// encima (<see cref="AparienciaVisible"/>), y los NPCs del grupo se visten aparte y recuperan lo
/// suyo al quitarlo. Varios sistemas pueden poner atuendos a la vez, cada uno con su clave (como
/// <c>PlayerHUDV2.HideHUD</c>); en lo que coincidan, manda el último puesto.
/// </summary>
public partial class CharacterAppearanceRegistry
{
    private readonly struct AtuendoPuesto
    {
        public readonly string quien;
        public readonly AtuendoDeEscena atuendo;
        public AtuendoPuesto(string quien, AtuendoDeEscena atuendo) { this.quien = quien; this.atuendo = atuendo; }
    }

    /// <summary>Lo que se le cambió a un cuerpo de NPC al vestirlo, para devolvérselo.</summary>
    private sealed class CuerpoVestido
    {
        public Slot personaje;
        public Dictionary<PartCategory, string> seleccionAntes;
        public readonly List<KeyValuePair<GameObject, bool>> piezasAntes = new();
        public readonly Dictionary<Renderer, Material[]> materiales = new();
    }

    private readonly List<AtuendoPuesto> _atuendos = new();

    // Lo que enseñaba el cuerpo del jugador justo después de aplicar la apariencia de un personaje
    // con atuendo. Al capturar, lo que difiere de esto lo ha cambiado el jugador (menú de equipo) y
    // pasa a la ropa de debajo; lo demás es del atuendo y no se guarda.
    private readonly Dictionary<PartCategory, string>[] _mostradaConAtuendo =
        new Dictionary<PartCategory, string>[ControlDelJugador.Personajes];

    // Materiales originales de las piezas teñidas en el cuerpo del jugador.
    private readonly Dictionary<Renderer, Material[]> _materialesDelJugador = new();

    private readonly Dictionary<ModularAutoBuilder, CuerpoVestido> _cuerposVestidos = new();
    private readonly List<KeyValuePair<ModularAutoBuilder, CuerpoVestido>> _cuerposADesvestir = new();
    private static readonly List<Renderer> _renderers = new();

    // ─── API ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pone un atuendo con la clave de quien lo pide. Con la misma clave sustituye al anterior.
    /// Se ve al momento en el personaje al mando y en los NPCs del grupo; los demás lo llevarán
    /// al pasar a ellos.
    /// </summary>
    public void PonerAtuendo(string quien, AtuendoDeEscena atuendo)
    {
        if (string.IsNullOrEmpty(quien) || atuendo == null) return;
        int i = IndiceDeAtuendo(quien);
        if (i >= 0 && _atuendos[i].atuendo == atuendo) return;

        var activo = ControlDelJugador.Activo;
        bool tocaAlJugador = atuendo.De(activo) != null || (i >= 0 && _atuendos[i].atuendo.De(activo) != null);
        if (tocaAlJugador) GuardarRopaDelJugador(activo);

        if (i >= 0) _atuendos.RemoveAt(i);
        _atuendos.Add(new AtuendoPuesto(quien, atuendo));

        if (tocaAlJugador) ApplyAppearance(activo);
        VestirCompaneros();
    }

    /// <summary>Quita el atuendo que puso <paramref name="quien"/> y devuelve la ropa de debajo.</summary>
    public void QuitarAtuendo(string quien)
    {
        int i = IndiceDeAtuendo(quien);
        if (i < 0) return;

        var activo = ControlDelJugador.Activo;
        bool tocaAlJugador = _atuendos[i].atuendo != null && _atuendos[i].atuendo.De(activo) != null;
        if (tocaAlJugador) GuardarRopaDelJugador(activo);

        _atuendos.RemoveAt(i);

        if (tocaAlJugador) ApplyAppearance(activo);
        VestirCompaneros();
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
        if (_currentBuilderSlot == personaje) CaptureCurrentAppearance(personaje);
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

    // ─── Cuerpo del jugador ───────────────────────────────────────────────────

    private int IndiceDeAtuendo(string quien)
    {
        for (int i = 0; i < _atuendos.Count; i++)
            if (_atuendos[i].quien == quien) return i;
        return -1;
    }

    // Antes de cambiar los atuendos del personaje al mando, lo que el jugador se haya cambiado
    // pasa a la ropa de debajo. Sin una apariencia aplicada aún por este registro (arranque) no
    // hay nada propio que guardar: la del preset llega después.
    private void GuardarRopaDelJugador(Slot activo)
    {
        if (_currentBuilderSlot == activo) CaptureCurrentAppearance(activo);
    }

    /// <summary>Lo que el cuerpo del jugador enseña tras aplicar la apariencia (solo con atuendo).</summary>
    private void RecordarLoMostrado(Slot personaje, ModularAutoBuilder builder)
    {
        _mostradaConAtuendo[(int)personaje] = LlevaAtuendo(personaje) ? builder.GetSelection() : null;
    }

    /// <summary>
    /// De lo que enseña el cuerpo del jugador, separa lo del atuendo: devuelve la ropa de debajo con
    /// los cambios que haya hecho el jugador desde que se aplicó. Sin atuendo, la selección tal cual.
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

    /// <summary>Devuelve los materiales del cuerpo del jugador y tiñe lo que pidan los atuendos del personaje.</summary>
    private void TenirCuerpoDelJugador(ModularAutoBuilder builder, Slot personaje)
    {
        Tenir(builder, personaje, _materialesDelJugador);
        PonerPrendasSueltas(builder, personaje);
    }

    // ─── NPCs del grupo ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        PartyControlManager.OnActiveCharacterChanged += AlCambiarDePersonaje;
        PlayerParty.OnPartyChanged += AlCambiarElGrupo;
    }

    private void OnDisable()
    {
        PartyControlManager.OnActiveCharacterChanged -= AlCambiarDePersonaje;
        PlayerParty.OnPartyChanged -= AlCambiarElGrupo;
        // Un atuendo no sobrevive a su registro: los NPCs recuperan lo suyo.
        DesvestirCompaneros();
    }

    private void AlCambiarDePersonaje(int _) => VestirCompaneros();
    private void AlCambiarElGrupo(IReadOnlyList<NPCPartyMember> _) => VestirCompaneros();

    /// <summary>
    /// Deja a cada NPC del grupo con los atuendos de su personaje: devuelve la ropa a los que la
    /// tenían prestada y viste de nuevo a los que lleven atuendo (cubre relevos, altas y bajas).
    /// </summary>
    private void VestirCompaneros()
    {
        if (Instance != this) return;
        if (_atuendos.Count == 0 && _cuerposVestidos.Count == 0) return;

        DesvestirCompaneros();
        for (int i = 0; i < ControlDelJugador.Personajes; i++)
        {
            var personaje = (Slot)i;
            if (!LlevaAtuendo(personaje)) continue;
            var cuerpo = CuerpoNpcDe(personaje);
            if (cuerpo != null) VestirCuerpo(cuerpo, personaje);
        }
    }

    private void DesvestirCompaneros()
    {
        if (_cuerposVestidos.Count == 0) return;
        _cuerposADesvestir.Clear();
        _cuerposADesvestir.AddRange(_cuerposVestidos);
        _cuerposVestidos.Clear();
        foreach (var kv in _cuerposADesvestir)
            if (kv.Key != null) DesvestirCuerpo(kv.Key, kv.Value);
        _cuerposADesvestir.Clear();
    }

    /// El NPC que pone cuerpo al personaje, aunque esté oculto porque el jugador lo lleva (así,
    /// al soltarlo, ya aparece vestido). El de Will solo existe mientras otro va al mando.
    private static GameObject CuerpoNpcDe(Slot personaje)
    {
        NPCPartyMember npc = personaje == Slot.Will
            ? ControlDelJugador.NpcDe(Slot.Will)
            : ControlDelJugador.MiembroDe(personaje);
        return npc != null ? npc.gameObject : null;
    }

    private void VestirCuerpo(GameObject cuerpo, Slot personaje)
    {
        var ropa = RopaDelCuerpo(cuerpo);
        if (ropa == null || _cuerposVestidos.ContainsKey(ropa)) return;

        var estado = new CuerpoVestido { personaje = personaje, seleccionAntes = ropa.GetSelection() };
        // Se apunta también qué piezas estaban encendidas: el vestidor solo registra una por
        // categoría y el modelo puede traer más.
        ropa.GetComponentsInChildren(true, _renderers);
        foreach (var r in _renderers)
            estado.piezasAntes.Add(new KeyValuePair<GameObject, bool>(r.gameObject, r.gameObject.activeSelf));
        _renderers.Clear();

        var objetivo = new Dictionary<PartCategory, string>(estado.seleccionAntes);
        for (int i = 0; i < _atuendos.Count; i++)
            if (_atuendos[i].atuendo != null) _atuendos[i].atuendo.Vestir(personaje, objetivo);
        CambiarLoQueDifiere(ropa, objetivo);
        Tenir(ropa, personaje, estado.materiales);
        PonerPrendasSueltas(ropa, personaje);
        ropa.RefreshRendererBoundsAfterAppearanceChange();
        _cuerposVestidos[ropa] = estado;
    }

    private static void DesvestirCuerpo(ModularAutoBuilder ropa, CuerpoVestido estado)
    {
        RestaurarMateriales(estado.materiales);
        var sueltas = ropa.GetComponent<PrendasSueltas>();
        if (sueltas != null) sueltas.QuitarTodas();
        CambiarLoQueDifiere(ropa, estado.seleccionAntes);
        foreach (var pieza in estado.piezasAntes)
            if (pieza.Key != null && pieza.Key.activeSelf != pieza.Value) pieza.Key.SetActive(pieza.Value);
        ropa.RefreshRendererBoundsAfterAppearanceChange();
    }

    /// Cambia en el vestidor solo las categorías que no coinciden con el objetivo, con sus reglas
    /// (sombrero y pelo no van juntos, armas por mano…).
    private static void CambiarLoQueDifiere(ModularAutoBuilder ropa, Dictionary<PartCategory, string> objetivo)
    {
        var actual = ropa.GetSelection();
        foreach (var kv in objetivo)
            if (!actual.TryGetValue(kv.Key, out var ahora) || !string.Equals(ahora, kv.Value, StringComparison.OrdinalIgnoreCase))
                ropa.SetByName(kv.Key, kv.Value);
        foreach (var kv in actual)
            if (!objetivo.ContainsKey(kv.Key)) ropa.SetByName(kv.Key, null);
    }

    /// El vestidor del cuerpo. Los NPCs de Estela y Liam traen todas las piezas en su modelo pero
    /// no lo llevan: se les pone uno para cambiarlas con las mismas reglas que al jugador (y así
    /// sus retratos copian la ropa como los del resto).
    private static ModularAutoBuilder RopaDelCuerpo(GameObject cuerpo)
    {
        var ropa = cuerpo.GetComponentInChildren<ModularAutoBuilder>(true);
        if (ropa != null) return ropa;
        var animador = cuerpo.GetComponentInChildren<Animator>(true);
        return (animador != null ? animador.gameObject : cuerpo).AddComponent<ModularAutoBuilder>();
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
