using System;
using Kits.StylizedVfx.Editor;
using UnityEditor;
using UnityEngine;
using R = Kits.StylizedVfx.Editor.EnergyRecipes;

/// Dúos y trío con la receta «dibujo animado con volumen»: cada ataque de equipo enseña las magias de
/// los que lo hacen juntándose (una hebra de color por personaje) y acaba con un golpe de forma propia.
/// Se rehacen en su sitio los prefabs de Assets/Art/VFX/Equipo (mismo GUID: los SpecialAttackSO y
/// DuoSpecialAttackSystem siguen enganchados). Remates: Estela = sísmico, Liam = supernova,
/// Trío = cataclismo (SpecialAttackSO.CoreografiaDeEquipo). Ver INC-1154
/// Ojo: VfxDelJuegoADibujo también rehace estos prefabs; si se pasa después, los deja como antes.
public static class EquipoConVolumen
{
    private const string Carpeta = "Assets/Art/VFX/Equipo";

    [MenuItem("El Sendero/Magia/Dúos y trío con volumen (INC-1154)")]
    public static void Menu()
    {
        var log = new System.Text.StringBuilder();
        Construir(log);
        Debug.Log("[VFX] Dúos y trío con volumen: " + log);
    }

    public static void Construir(System.Text.StringBuilder log)
    {
        if (!HechizosConVolumen.PrepararPaletas()) return;
        var m = HechizosConVolumen.Mallas;
        EnergyPalette P(string n) => HechizosConVolumen.Paleta(n);
        var will = P("Luz"); var fuego = P("Fuego"); var viento = P("Viento"); var liam = P("Mente");
        var blanco = P("Blanco"); var polvo = P("Polvo"); var oscuro = P("Oscuro");

        // Will y Estela (suspendidos en el aire): la luz de Will marca el cielo, el viento de Estela
        // aviva las estrellas y caen en llamas.
        Rehacer("SpecialAttack_Estela", r =>
        {
            R.MagicCircle(r, will, m, 5.5f);
            Hijo(r, "Juntas", g => R.Braid(g, new[] { will, fuego, viento }, m, 0.9f, 4f, 14f));
            Hijo(r, "Meteoros en llamas", g => R.SkyRain(g, fuego, m, 5f, 10f, 4f, 0.7f, m.Sphere));
            Hijo(r, "Estrellas", g => R.SkyRain(g, will, m, 5f, 11f, 3f, 0.6f, m.Star));
        }, log);

        // Will y Liam (cara a cara, el agujero sobre sus cabezas): las runas de Liam y la luz de Will
        // dibujan una constelación en el suelo y se cierran en un vórtice de estrellas que atrae.
        // El hijo «VFX_AgujeroNegro» lo sube DuoSpecialAttackSystem sobre sus cabezas.
        Rehacer("SpecialAttack_Liam", r =>
        {
            Hijo(r, "Constelación", g => R.Constellation(g, will, m, 5f, 7, 491));
            Hijo(r, "Juntas", g => R.Braid(g, new[] { will, liam }, m, 0.8f, 3.5f, 14f));
            Hijo(r, "VFX_AgujeroNegro", g => R.Vortex(g, liam, oscuro, m, 1.6f));
        }, log);

        // Estela y Liam (cara a cara): el fuego y el viento de Estela giran y las cadenas de Liam los aprietan.
        Rehacer("SpecialAttack_EstelaLiam", r =>
        {
            R.MagicCircle(r, liam, m, 5f);
            Hijo(r, "Remolino de fuego", g => R.Funnel(g, fuego, m, 3.4f, 3.2f, 4, 260f));
            Hijo(r, "Llamas", g => R.GroundFlames(g, fuego, m, 3f, 1.1f, 36f));
            Hijo(r, "Eslabones", g => R.RisingMotes(g, liam, m, 3.6f, m.OrbitRing, 16f, 1.4f, 0.4f));
            Hijo(r, "Sello del pacto", g => R.Constellation(g, liam, m, 3.8f, 6, 494));
            Hijo(r, "Juntas", g => R.Braid(g, new[] { fuego, viento, liam }, m, 0.8f, 3.5f, 12f));
        }, log);

        // Trío: un sello de tres puntas (una por personaje), las tres magias que suben y la columna de luz que baja.
        Rehacer("SpecialAttack_Trio", r =>
        {
            Hijo(r, "Sello", g => R.Sigil(g, will, new[] { will, fuego, liam }, m, 8f));
            Hijo(r, "Juntas", g => R.Braid(g, new[] { will, fuego, liam }, m, 1.2f, 6f, 16f));
            Hijo(r, "Columna", g => R.Pillar(g, blanco, will, m, 1.4f, 4f, 16f, true));
        }, log);

        // Remates. Sísmico: el suelo se abre en pinchos de luz y polvo.
        Rehacer("ImpactoRemate_Estela", r =>
        {
            R.Impact(r, fuego, m, new R.ImpactStyle { Scale = 3f, Waves = 3, Debris = m.Spike, Puffs = polvo });
            Hijo(r, "Grietas", g => R.Spikes(g, will, polvo, m, 12, 4.5f, 1.3f, 492));
        }, log);
        // Supernova: estalla el vórtice.
        Rehacer("ImpactoRemate_Liam", r =>
        {
            R.Nova(r, liam, m, 6f);
            Hijo(r, "Destello", g => R.Impact(g, will, m, new R.ImpactStyle { Scale = 2.5f, Cross = true, RisingStars = 14 }));
        }, log);
        // Cataclismo: onda de luz enorme con esquirlas de los tres colores.
        Rehacer("ImpactoRemate_Trio", r =>
        {
            R.Nova(r, will, m, 9f);
            Hijo(r, "Golpe", g => R.Impact(g, blanco, m, new R.ImpactStyle { Scale = 3.5f, Waves = 3, Shards = new[] { will, fuego, liam }, RisingStars = 16 }));
            Hijo(r, "Grietas", g => R.Spikes(g, will, polvo, m, 16, 6f, 1.5f, 493));
        }, log);

        AssetDatabase.SaveAssets();
    }

    private static void Rehacer(string nombre, Action<GameObject> montar, System.Text.StringBuilder log)
    {
        R.RebuildInPlace($"{Carpeta}/{nombre}.prefab", nombre, montar);
        log.Append(nombre).Append(' ');
    }

    private static void Hijo(GameObject r, string nombre, Action<GameObject> montar)
    {
        var g = new GameObject(nombre);
        g.transform.SetParent(r.transform, false);
        montar(g);
    }
}
