using System.Collections.Generic;
using UnityEngine;
using Slot = PartyControlManager.CharacterSlot;

/// <summary>
/// Lo que una prenda le da a quien la lleva: no números de ataque o defensa, sino rasgos de estilo
/// de juego (GDD § 6.1). Cada prenda lista los suyos (<see cref="WardrobeItemSO.Rasgos"/>); los
/// sistemas que los aplican preguntan aquí si el personaje al mando lleva uno.
/// Los valores guardados en los assets son los números del enum: no reordenar.
/// </summary>
public enum RasgoDePrenda
{
    /// Los hechizos de Viento cuestan menos maná.
    VientoMasBarato = 0,
    /// La ventana para devolver un ataque con B es más larga.
    ContraataqueMasAmplio = 1,
    /// Se puede planear (mantener salto en el aire).
    Planear = 2,
    /// Los lanzadores y trampolines impulsan más alto.
    ReboteExtra = 3,
}

public static class RasgosDePrenda
{
    /// Parte del coste de maná que se paga con <see cref="RasgoDePrenda.VientoMasBarato"/>.
    public const float CosteDeVientoConRasgo = 0.75f;
    /// Segundos que se alarga la ventana de contraataque con <see cref="RasgoDePrenda.ContraataqueMasAmplio"/>.
    public const float VentanaExtraDeContraataque = 0.1f;
    /// Rebote extra (fracción) con <see cref="RasgoDePrenda.ReboteExtra"/>.
    public const float ReboteExtraConRasgo = 0.5f;

    /// El personaje al mando lleva puesta una prenda con ese rasgo.
    public static bool AlMandoLleva(RasgoDePrenda rasgo) => Lleva(ControlDelJugador.Activo, rasgo);

    /// Ese personaje lleva puesta una prenda con ese rasgo (en su cuerpo; sin ropa modular, ninguno).
    public static bool Lleva(Slot personaje, RasgoDePrenda rasgo)
    {
        var ropa = Ropa(personaje);
        return ropa != null && ropa.LlevaRasgo(rasgo);
    }

    /// Los rasgos de todo lo que lleva puesto ese personaje.
    public static void De(Slot personaje, List<RasgoDePrenda> destino)
    {
        destino.Clear();
        var ropa = Ropa(personaje);
        if (ropa != null) destino.AddRange(ropa.RasgosPuestos);
    }

    /// Coste de maná de un hechizo para el personaje al mando, con sus rasgos.
    public static float CosteDeMana(MagicSpellSO hechizo)
    {
        if (hechizo == null) return 0f;
        float coste = hechizo.manaCost;
        if (hechizo.element == MagicElement.Storm && AlMandoLleva(RasgoDePrenda.VientoMasBarato))
            coste *= CosteDeVientoConRasgo;
        return coste;
    }

    public static string Texto(RasgoDePrenda rasgo)
    {
        string clave = "RASGO_" + rasgo.ToString().ToUpperInvariant();
        string respaldo = rasgo switch
        {
            RasgoDePrenda.VientoMasBarato => "Los hechizos de Viento cuestan menos maná",
            RasgoDePrenda.ContraataqueMasAmplio => "Más margen para devolver ataques",
            RasgoDePrenda.Planear => "Permite planear",
            RasgoDePrenda.ReboteExtra => "Rebota más alto en trampolines",
            _ => rasgo.ToString()
        };
        return LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(clave, respaldo) : respaldo;
    }

    // La ropa de cada cuerpo se busca una vez y se guarda mientras el cuerpo sea el mismo.
    private static readonly GameObject[] s_cuerpos = new GameObject[3];
    private static readonly ModularAutoBuilder[] s_ropas = new ModularAutoBuilder[3];

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        System.Array.Clear(s_cuerpos, 0, s_cuerpos.Length);
        System.Array.Clear(s_ropas, 0, s_ropas.Length);
    }
#endif

    private static ModularAutoBuilder Ropa(Slot personaje)
    {
        int i = (int)personaje;
        if (i < 0 || i >= s_cuerpos.Length) return null;
        var cuerpo = ControlDelJugador.CuerpoDe(personaje);
        if (cuerpo == null) return null;
        if (s_cuerpos[i] != cuerpo)
        {
            s_cuerpos[i] = cuerpo;
            s_ropas[i] = cuerpo.GetComponentInChildren<ModularAutoBuilder>(true);
        }
        return s_ropas[i];
    }
}
