using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static VfxProcedural;

/// Pasa al aspecto de dibujo los efectos que el juego usa fuera de los hechizos: los de los paquetes
/// (destellos, brillos, confeti, humo, golpes, círculos, portales…) y los propios de las cinemáticas
/// y el ambiente. Cada prefab se rehace EN SU SITIO: se le quitan las partículas, mallas y luces (lo
/// que tenga de juego, como colisiones o componentes del proyecto, se queda) y se le mete dentro un
/// hijo «Dibujo» del tipo que toca, al tamaño que tenía el efecto. Así cambian a la vez escenas,
/// guiones, configuraciones y código que lo nombran, sin tocar ninguna referencia. Las copias de
/// antes están en «Versiones antiguas/VFX antes del aspecto de dibujo (2026-11-12)». Idempotente:
/// el tamaño sale de la medida guardada del efecto original, con un tope por tipo. Ver INC-1127
public static class VfxDelJuegoADibujo
{
    private const string NombreDelDibujo = "Dibujo";

    private enum Tipo
    {
        Destello, Brillo, Chispitas, Confeti, Humo, Polvo, Chispas, Golpe, GolpeDePiedra, Oscuro, Circulo,
        Tajo, Columna, Llama, Onda, Implosion, Portal, Remolino, Curacion, Velocidad, Meteoro, Juicio,
        Veneno, Estrellas, Orbe, Chimenea,
    }

    private sealed class Entrada
    {
        public string Ruta; public Tipo Tipo; public Color Color;
        /// Radio en el mundo, en metros, cuando se sabe mejor que la medida (velas, antorchas…).
        public float Radio;
        public Entrada(string ruta, Tipo tipo, Color color, float radio = 0f) { Ruta = ruta; Tipo = tipo; Color = color; Radio = radio; }
    }

    private static readonly Color Crema = new Color(1f, 0.97f, 0.90f);
    private static readonly Color Oro = new Color(1f, 0.74f, 0.30f);
    private static readonly Color Fuego = new Color(1f, 0.55f, 0.28f);
    private static readonly Color Rojo = new Color(0.90f, 0.28f, 0.22f);
    private static readonly Color Rosa = new Color(1f, 0.62f, 0.70f);
    private static readonly Color Lila = new Color(0.70f, 0.58f, 1f);
    private static readonly Color Cielo = new Color(0.62f, 0.82f, 1f);
    private static readonly Color Menta = new Color(0.62f, 0.92f, 0.80f);
    private static readonly Color Verde = new Color(0.60f, 0.85f, 0.35f);
    private static readonly Color Sombra = new Color(0.30f, 0.20f, 0.42f);
    private static readonly Color Tierra = new Color(0.82f, 0.74f, 0.64f);

    private const string Best = "Assets/VFX/100BestEffectPack/Effects/";
    private const string Free = "Assets/VFX/Free Game VFX/Prefab/";
    private const string Gabriel = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/";
    private const string Hovl = "Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/";
    private const string Lana = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/";
    private const string Equipo = "Assets/Art/VFX/Equipo/";
    private const string Reino = "Assets/Art/World/Fantasy_Kingdom_Pack/Particle/";
    private const string Dulce = "Assets/Art/World/ithappy/Sweet_Land/VFX/FX_Prefabs/";
    private const string Orbes = "Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/";

    private static readonly Entrada[] Tabla =
    {
        // 100 Best Effect Pack
        new Entrada(Best + "DarkEffect/DarkEffect1.prefab", Tipo.Oscuro, Rojo),
        new Entrada(Best + "DarkEffect/DarkEffect3.prefab", Tipo.Oscuro, Rojo),
        new Entrada(Best + "DarkEffect/DarkEffect4.prefab", Tipo.Implosion, Sombra),
        new Entrada(Best + "DarkEffect/DarkEffect5.prefab", Tipo.Oscuro, Lila),
        new Entrada(Best + "ExplosionEffect/ExplosionEffect.prefab", Tipo.Golpe, Fuego),
        new Entrada(Best + "ExplosionEffect/ExplosionEffect3.prefab", Tipo.Golpe, Crema),
        new Entrada(Best + "ExplosionEffect/ExplosionEffect5.prefab", Tipo.Golpe, Oro),
        new Entrada(Best + "FireEffect/FireEffect1.prefab", Tipo.Llama, Color.white, 0.35f),
        new Entrada(Best + "FlameEmissionEffect/FlameEmissionEffect.prefab", Tipo.Llama, Color.white),
        new Entrada(Best + "OtherMagicEffect/MagicEffect20.prefab", Tipo.Circulo, Lila),
        new Entrada(Best + "SwampEffect/SwampEffect1.prefab", Tipo.Veneno, Verde),
        // Combate
        new Entrada("Assets/VFX/Combate/GiroRapido.prefab", Tipo.Remolino, Crema),
        // Free Game VFX
        new Entrada(Free + "FX_Fireball.prefab", Tipo.Llama, Color.white),
        new Entrada(Free + "FX_Greenlight_shrink.prefab", Tipo.Implosion, Menta),
        new Entrada(Free + "FX_LightPillar.prefab", Tipo.Columna, Oro),
        // Gabriel Aguiar
        new Entrada(Gabriel + "vfx_Explosion_01.prefab", Tipo.Golpe, Fuego),
        new Entrada(Gabriel + "vfx_Explosion_02.prefab", Tipo.Golpe, Fuego),
        new Entrada(Gabriel + "vfx_Flames_01.prefab", Tipo.Llama, Color.white),
        new Entrada(Gabriel + "vfx_Heal_02.prefab", Tipo.Curacion, Menta),
        new Entrada(Gabriel + "vfx_Hyperdrive_01.prefab", Tipo.Velocidad, Crema),
        new Entrada(Gabriel + "vfx_Implosion_01.prefab", Tipo.Implosion, Lila),
        new Entrada(Gabriel + "vfx_Portal_01.prefab", Tipo.Portal, Lila),
        new Entrada(Gabriel + "vfx_Shockwave_01.prefab", Tipo.Onda, Crema),
        new Entrada(Gabriel + "vfx_Smoke_01.prefab", Tipo.Humo, Crema),
        new Entrada(Gabriel + "vfx_Tornado_01.prefab", Tipo.Remolino, Menta),
        // Hovl: magic effects pack
        new Entrada(Hovl + "AoE effects/Ground AOE explosion.prefab", Tipo.Golpe, Crema),
        new Entrada(Hovl + "AoE effects/Laser AOE.prefab", Tipo.Juicio, Lila),
        new Entrada(Hovl + "AoE effects/Meteors AOE.prefab", Tipo.Meteoro, Oro),
        new Entrada(Hovl + "AoE effects/Red energy explosion.prefab", Tipo.Golpe, Rojo),
        new Entrada(Hovl + "AoE effects/Smoke AOE explosion.prefab", Tipo.Humo, Crema),
        new Entrada(Hovl + "Character auras/Plexus.prefab", Tipo.Brillo, Lila),
        new Entrada(Hovl + "Character auras/Star aura.prefab", Tipo.Brillo, Oro),
        new Entrada(Hovl + "Environment/Crystal effect blue.prefab", Tipo.Brillo, Cielo),
        new Entrada(Hovl + "Environment/Teleport.prefab", Tipo.Portal, Cielo),
        new Entrada(Hovl + "Hits and explosions/Explosion.prefab", Tipo.Golpe, Fuego),
        new Entrada(Hovl + "Hits and explosions/Holy hit.prefab", Tipo.Golpe, Oro),
        new Entrada(Hovl + "Hits and explosions/Star hit.prefab", Tipo.Golpe, Oro),
        new Entrada(Hovl + "Hits and explosions/Stones hit.prefab", Tipo.GolpeDePiedra, Tierra),
        new Entrada(Hovl + "Magic circles/Magic circle.prefab", Tipo.Circulo, Oro),
        new Entrada(Hovl + "Magic circles/Magic circle 2.prefab", Tipo.Circulo, Lila),
        new Entrada(Hovl + "Magic circles/Healing circle.prefab", Tipo.Circulo, Menta),
        new Entrada(Hovl + "Portals/Portal yellow.prefab", Tipo.Portal, Oro),
        new Entrada(Hovl + "Portals/Portal red.prefab", Tipo.Portal, Rojo),
        new Entrada(Hovl + "Portals/Portal blue.prefab", Tipo.Portal, Cielo),
        new Entrada(Hovl + "Slash effects/Charge slash blue.prefab", Tipo.Tajo, Cielo),
        new Entrada(Hovl + "Slash effects/Charge slash purple.prefab", Tipo.Tajo, Lila),
        new Entrada(Hovl + "Slash effects/Electro slash.prefab", Tipo.Tajo, Cielo),
        new Entrada(Hovl + "Smoke effects/Dust ground mountain.prefab", Tipo.Polvo, Tierra),
        new Entrada(Hovl + "Smoke effects/Dust ground.prefab", Tipo.Polvo, Tierra),
        new Entrada(Hovl + "Smoke effects/Dust hemisphere loop.prefab", Tipo.Polvo, Tierra),
        new Entrada(Hovl + "Smoke effects/Dust puff.prefab", Tipo.Humo, Tierra),
        new Entrada(Hovl + "Smoke effects/Smoke ground.prefab", Tipo.Polvo, Crema),
        new Entrada(Hovl + "Smoke effects/Smoke puff.prefab", Tipo.Humo, Crema),
        new Entrada(Hovl + "Smoke effects/Smoke vortex.prefab", Tipo.Oscuro, Sombra),
        new Entrada(Hovl + "Sparks/Sparks explode red.prefab", Tipo.Chispas, Rojo),
        new Entrada(Hovl + "Sparks/Sparks explode white.prefab", Tipo.Chispas, Crema),
        new Entrada(Hovl + "Sparks/Sparks explode yellow.prefab", Tipo.Chispas, Oro),
        new Entrada(Hovl + "Sparks/Sparks flashing white.prefab", Tipo.Brillo, Crema),
        new Entrada(Hovl + "Sparks/Sparks flashing yellow.prefab", Tipo.Brillo, Oro),
        // Hovl: procedural fire
        new Entrada("Assets/VFX/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab", Tipo.Golpe, Fuego),
        new Entrada("Assets/VFX/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab", Tipo.Llama, Color.white),
        // Lana Studio
        new Entrada(Lana + "Confetti/Confetti_blast_multicolor.prefab", Tipo.Confeti, Color.white),
        new Entrada(Lana + "Confetti/Confetti_directional_multicolor.prefab", Tipo.Confeti, Color.white),
        new Entrada(Lana + "Flash/1 Flash_magic_ellow_blue.prefab", Tipo.Destello, Oro),
        new Entrada(Lana + "Flash/2 Flash_round_ellow.prefab", Tipo.Destello, Oro),
        new Entrada(Lana + "Flash/Flash_ellow_pink.prefab", Tipo.Destello, Rosa),
        new Entrada(Lana + "Flash/Flash_blue_purple.prefab", Tipo.Destello, Lila),
        new Entrada(Lana + "Flash/Flash_ellow.prefab", Tipo.Destello, Oro),
        new Entrada(Lana + "Shine/Shine_blue.prefab", Tipo.Brillo, Cielo),
        new Entrada(Lana + "Shine/Shine_ellow.prefab", Tipo.Brillo, Oro),
        new Entrada(Lana + "Shine/Shine_pink.prefab", Tipo.Brillo, Rosa),
        new Entrada(Lana + "Sparkle/Sparkle_ellow.prefab", Tipo.Brillo, Oro),
        new Entrada(Lana + "Sparkle/Sparkle_ellow_flying.prefab", Tipo.Chispitas, Oro),
        // Otros paquetes
        new Entrada("Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/AoE Poison.prefab", Tipo.Veneno, Verde),
        new Entrada("Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/AoE Stars.prefab", Tipo.Estrellas, Oro),
        new Entrada("Assets/VFX/Travis Game Assets/Hit Impact Effects/Prefabs/Hits/Hit_02.prefab", Tipo.Golpe, Crema),
        new Entrada("Assets/VFX/Univeral FX Shader/Prefab/Vortex.prefab", Tipo.Remolino, Lila),
        new Entrada("Assets/VFX/Univeral FX Shader/Prefab/FireSmall.prefab", Tipo.Llama, Color.white, 0.3f),
        // Propios: prólogo y cinemáticas
        // El orbe del Archimago (VFX_OrbeProtector) no está: lo construye OrbeDelArchimago; el resto de
        // efectos del prólogo son los del piloto (VfxDelPrologo). Ver INC-1136
        new Entrada("Assets/_VFX/Prologo/VFX_AgujeroNegro.prefab", Tipo.Implosion, Sombra),
        new Entrada("Assets/_VFX/Prologo/VFX_BolaDeConjuro.prefab", Tipo.Orbe, Oro),
        new Entrada("Assets/_VFX/Prologo/VFX_CirculoDelArchimago.prefab", Tipo.Circulo, Oro),
        new Entrada("Assets/_VFX/Prologo/VFX_CirculoDelMagoOscuro.prefab", Tipo.Circulo, Rojo),
        new Entrada("Assets/_VFX/Prologo/VFX_LuzProtectora.prefab", Tipo.Brillo, Oro),
        new Entrada("Assets/_VFX/Prologo/VFX_MagoOscuro_HechizoSostenido.prefab", Tipo.Oscuro, Rojo),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_ColapsoAmbiental.prefab", Tipo.Polvo, Sombra),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_GolpeTraicion.prefab", Tipo.Golpe, Rojo),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_MagoOscuro_CataclismoBarrido.prefab", Tipo.Oscuro, Rojo),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_MagoOscuro_Derrota.prefab", Tipo.Implosion, Sombra),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_PortalSendero_Colapso.prefab", Tipo.Portal, Lila),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_Rebobinado.prefab", Tipo.Remolino, Lila),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_SacrificioLiam.prefab", Tipo.Brillo, Lila),
        new Entrada("Assets/_VFX/BatallaFinal/VFX_SenderoDestruccion.prefab", Tipo.Golpe, Crema),
        new Entrada("Assets/_VFX/MananaDespues/VFX_VelaGrande.prefab", Tipo.Llama, Color.white, 0.12f),
        new Entrada("Assets/_VFX/MananaDespues/VFX_VelaPequena.prefab", Tipo.Llama, Color.white, 0.08f),
        new Entrada("Assets/_VFX/MananaDespues/VFX_Manga.prefab", Tipo.Llama, Color.white, 0.1f),
        // Propios: ambiente
        new Entrada("Assets/_VFX/Sendero/VFX_PolvoDeLuz.prefab", Tipo.Chispitas, Oro),
        new Entrada("Assets/_VFX/Mundo/VFX_Pagina_Brillo.prefab", Tipo.Brillo, Oro),
        // Propios: ataques de equipo
        new Entrada(Equipo + "AuraConcentracion_Liam.prefab", Tipo.Brillo, Lila),
        new Entrada(Equipo + "AuraConcentracion_Will.prefab", Tipo.Brillo, Oro),
        new Entrada(Equipo + "AuraFormacion_Trio.prefab", Tipo.Circulo, Oro),
        new Entrada(Equipo + "AuraLevitacion_Estela.prefab", Tipo.Brillo, Fuego),
        new Entrada(Equipo + "FocoMano_Estela.prefab", Tipo.Llama, Color.white),
        new Entrada(Equipo + "ImpactoRemate_Estela.prefab", Tipo.Golpe, Fuego),
        new Entrada(Equipo + "ImpactoRemate_Liam.prefab", Tipo.Golpe, Lila),
        new Entrada(Equipo + "ImpactoRemate_Trio.prefab", Tipo.Golpe, Oro),
        new Entrada(Equipo + "SpecialAttack_Estela.prefab", Tipo.Meteoro, Fuego),
        new Entrada(Equipo + "SpecialAttack_EstelaLiam.prefab", Tipo.Golpe, Fuego),
        new Entrada(Equipo + "SpecialAttack_Liam.prefab", Tipo.Implosion, Lila),
        new Entrada(Equipo + "SpecialAttack_Trio.prefab", Tipo.Golpe, Oro),
        // Ambiente de los escenarios (solo los efectos sueltos; los edificios que llevan copias
        // propias de estas partículas no se tocan)
        new Entrada(Reino + "Fire01_P.prefab", Tipo.Llama, Color.white, 0.3f),
        new Entrada(Reino + "Fire02_P.prefab", Tipo.Llama, Color.white, 0.3f),
        new Entrada(Reino + "Smoke01_P.prefab", Tipo.Chimenea, Crema),
        new Entrada(Reino + "Smoke02_P.prefab", Tipo.Chimenea, Crema),
        new Entrada(Reino + "Smoke03_P.prefab", Tipo.Chimenea, Crema),
        new Entrada(Reino + "Light01_P.prefab", Tipo.Brillo, Oro),
        new Entrada(Reino + "Light02_P.prefab", Tipo.Brillo, Oro),
        new Entrada(Reino + "Firefly01_P.prefab", Tipo.Chispitas, Oro),
        new Entrada(Dulce + "FX_Dust.prefab", Tipo.Polvo, Rosa),
        new Entrada(Dulce + "FX_Smoke_Big.prefab", Tipo.Chimenea, Crema),
        new Entrada(Dulce + "FX_Smoke_Medium.prefab", Tipo.Chimenea, Crema),
        new Entrada(Dulce + "FX_Smoke_Small.prefab", Tipo.Chimenea, Crema),
        new Entrada(Dulce + "FX_Water_Vapor.prefab", Tipo.Chimenea, Cielo),
        new Entrada("Assets/Art/World/Modular Castle/Assets/particles/fire1.prefab", Tipo.Llama, Color.white, 0.3f),
        new Entrada("Assets/Art/World/Modular Castle/Assets/particles/fire2.prefab", Tipo.Llama, Color.white, 0.3f),
        new Entrada("Assets/Prefabs/VFX/FootDust_Toon.prefab", Tipo.Humo, Tierra, 0.35f),
        new Entrada("Assets/Art/Characters/Enemies/Imp Demon Cute Series/Prefabs/FX Spark.prefab", Tipo.Chispas, Rojo),
        // Orbes que se recogen en combate (conservan su BattleOrb y su colisión)
        new Entrada("Assets/Prefabs/Orbes/Orbe_Esencia.prefab", Tipo.Orbe, Oro),
        new Entrada("Assets/Prefabs/Orbes/Orbe_EsenciaBrillante.prefab", Tipo.Orbe, Lila),
        new Entrada(Orbes + "Basic Orb.prefab", Tipo.Orbe, Cielo),
        new Entrada(Orbes + "Basic Orb 2.prefab", Tipo.Orbe, Lila),
        new Entrada(Orbes + "Bubble Orb .prefab", Tipo.Orbe, Cielo),
        new Entrada(Orbes + "Light Orb 2.prefab", Tipo.Orbe, Oro),
        new Entrada(Orbes + "Multiple Earth Orb.prefab", Tipo.Orbe, Verde),
    };

    /// Radio en el mundo que medían los efectos originales (antes de pasarlos a dibujo), para que
    /// volver a pasar el menú dé siempre el mismo tamaño.
    private static readonly Dictionary<string, float> Medidas = new Dictionary<string, float>
    {
        ["Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect1.prefab"] = 6.49f,

        ["Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect3.prefab"] = 8.0f,

        ["Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect4.prefab"] = 5.61f,

        ["Assets/VFX/100BestEffectPack/Effects/DarkEffect/DarkEffect5.prefab"] = 7.64f,

        ["Assets/VFX/100BestEffectPack/Effects/ExplosionEffect/ExplosionEffect.prefab"] = 8.0f,

        ["Assets/VFX/100BestEffectPack/Effects/ExplosionEffect/ExplosionEffect3.prefab"] = 8.0f,

        ["Assets/VFX/100BestEffectPack/Effects/ExplosionEffect/ExplosionEffect5.prefab"] = 7.76f,

        ["Assets/VFX/100BestEffectPack/Effects/FireEffect/FireEffect1.prefab"] = 4.82f,

        ["Assets/VFX/100BestEffectPack/Effects/FlameEmissionEffect/FlameEmissionEffect.prefab"] = 8.0f,

        ["Assets/VFX/100BestEffectPack/Effects/OtherMagicEffect/MagicEffect20.prefab"] = 5.68f,

        ["Assets/VFX/100BestEffectPack/Effects/SwampEffect/SwampEffect1.prefab"] = 4.96f,

        ["Assets/VFX/Combate/GiroRapido.prefab"] = 0.3f,

        ["Assets/VFX/Free Game VFX/Prefab/FX_Fireball.prefab"] = 6.79f,

        ["Assets/VFX/Free Game VFX/Prefab/FX_Greenlight_shrink.prefab"] = 2.78f,

        ["Assets/VFX/Free Game VFX/Prefab/FX_LightPillar.prefab"] = 8.0f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_01.prefab"] = 2.75f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Explosion_02.prefab"] = 7.49f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Flames_01.prefab"] = 3.31f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Heal_02.prefab"] = 2.84f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Hyperdrive_01.prefab"] = 8.0f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Implosion_01.prefab"] = 7.57f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Portal_01.prefab"] = 2.16f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Shockwave_01.prefab"] = 5.68f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Smoke_01.prefab"] = 3.09f,

        ["Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Tornado_01.prefab"] = 2.32f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Ground AOE explosion.prefab"] = 6.19f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Laser AOE.prefab"] = 5.77f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Meteors AOE.prefab"] = 5.77f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Red energy explosion.prefab"] = 3.98f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Smoke AOE explosion.prefab"] = 6.19f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Character auras/Plexus.prefab"] = 1.17f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Character auras/Star aura.prefab"] = 1.94f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Environment/Crystal effect blue.prefab"] = 2.65f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Environment/Teleport.prefab"] = 1.72f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Explosion.prefab"] = 6.82f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab"] = 2.17f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Star hit.prefab"] = 2.17f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Stones hit.prefab"] = 2.35f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab"] = 5.77f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle 2.prefab"] = 5.77f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Healing circle.prefab"] = 5.77f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield blue.prefab"] = 1.0f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield pink.prefab"] = 1.0f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Magic shields/Magic shield yellow.prefab"] = 1.0f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal yellow.prefab"] = 1.14f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal red.prefab"] = 1.14f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Portals/Portal blue.prefab"] = 1.14f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash blue.prefab"] = 3.94f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Charge slash purple.prefab"] = 3.94f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Slash effects/Electro slash.prefab"] = 2.59f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust ground mountain.prefab"] = 8.0f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust ground.prefab"] = 8.0f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust hemisphere loop.prefab"] = 4.85f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Dust puff.prefab"] = 3.77f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke ground.prefab"] = 8.0f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke puff.prefab"] = 5.66f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke vortex.prefab"] = 7.31f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode red.prefab"] = 2.99f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode white.prefab"] = 2.89f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode yellow.prefab"] = 3.21f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing white.prefab"] = 1.46f,

        ["Assets/VFX/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks flashing yellow.prefab"] = 1.56f,

        ["Assets/VFX/Hovl Studio/Procedural fire/Prefabs/Explosion.prefab"] = 2.27f,

        ["Assets/VFX/Hovl Studio/Procedural fire/Prefabs/Magic fire 0.prefab"] = 1.76f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Confetti_blast_multicolor.prefab"] = 6.95f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Confetti/Confetti_directional_multicolor.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Flash/1 Flash_magic_ellow_blue.prefab"] = 3.75f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Flash/2 Flash_round_ellow.prefab"] = 7.04f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_ellow_pink.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_blue_purple.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Flash/Flash_ellow.prefab"] = 1.79f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Shine/Shine_blue.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Shine/Shine_ellow.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Shine/Shine_pink.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Sparkle/Sparkle_ellow.prefab"] = 8.0f,

        ["Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Sparkle/Sparkle_ellow_flying.prefab"] = 1.92f,

        ["Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/AoE Poison.prefab"] = 5.68f,

        ["Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/AoE Stars.prefab"] = 5.62f,

        ["Assets/VFX/Travis Game Assets/Hit Impact Effects/Prefabs/Hits/Hit_02.prefab"] = 1.57f,

        ["Assets/VFX/Univeral FX Shader/Prefab/Vortex.prefab"] = 8.0f,

        ["Assets/VFX/Univeral FX Shader/Prefab/FireSmall.prefab"] = 8.0f,

        ["Assets/_VFX/Prologo/VFX_AgujeroNegro.prefab"] = 2.39f,

        ["Assets/_VFX/Prologo/VFX_BolaDeConjuro.prefab"] = 0.3f,

        ["Assets/_VFX/Prologo/VFX_CirculoDelArchimago.prefab"] = 5.77f,

        ["Assets/_VFX/Prologo/VFX_CirculoDelMagoOscuro.prefab"] = 5.77f,

        ["Assets/_VFX/Prologo/VFX_LuzProtectora.prefab"] = 0.84f,



        ["Assets/_VFX/MananaDespues/VFX_VelaGrande.prefab"] = 2.68f,

        ["Assets/_VFX/MananaDespues/VFX_VelaPequena.prefab"] = 1.07f,

        ["Assets/_VFX/MananaDespues/VFX_Manga.prefab"] = 1.5f,

        ["Assets/_VFX/Sendero/VFX_PolvoDeLuz.prefab"] = 1.03f,

        ["Assets/_VFX/Mundo/VFX_Pagina_Brillo.prefab"] = 0.62f,

        ["Assets/Art/VFX/Equipo/AuraConcentracion_Liam.prefab"] = 0.99f,

        ["Assets/Art/VFX/Equipo/AuraConcentracion_Will.prefab"] = 1.64f,

        ["Assets/Art/VFX/Equipo/AuraFormacion_Trio.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/AuraLevitacion_Estela.prefab"] = 0.86f,

        ["Assets/Art/VFX/Equipo/FocoMano_Estela.prefab"] = 0.4f,

        ["Assets/Art/VFX/Equipo/ImpactoRemate_Estela.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/ImpactoRemate_Liam.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/ImpactoRemate_Trio.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/SpecialAttack_Estela.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/SpecialAttack_EstelaLiam.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/SpecialAttack_Liam.prefab"] = 8.0f,

        ["Assets/Art/VFX/Equipo/SpecialAttack_Trio.prefab"] = 8.0f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Fire01_P.prefab"] = 0.64f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Fire02_P.prefab"] = 0.6f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Smoke01_P.prefab"] = 2.03f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Smoke02_P.prefab"] = 8.0f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Smoke03_P.prefab"] = 3.02f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Light01_P.prefab"] = 0.8f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Light02_P.prefab"] = 2.84f,

        ["Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Firefly01_P.prefab"] = 0.72f,

        ["Assets/Art/World/ithappy/Sweet_Land/VFX/FX_Prefabs/FX_Dust.prefab"] = 8.0f,

        ["Assets/Art/World/ithappy/Sweet_Land/VFX/FX_Prefabs/FX_Smoke_Big.prefab"] = 8.0f,

        ["Assets/Art/World/ithappy/Sweet_Land/VFX/FX_Prefabs/FX_Smoke_Medium.prefab"] = 8.0f,

        ["Assets/Art/World/ithappy/Sweet_Land/VFX/FX_Prefabs/FX_Smoke_Small.prefab"] = 8.0f,

        ["Assets/Art/World/ithappy/Sweet_Land/VFX/FX_Prefabs/FX_Water_Vapor.prefab"] = 8.0f,

        ["Assets/Art/World/Modular Castle/Assets/particles/fire1.prefab"] = 0.75f,

        ["Assets/Art/World/Modular Castle/Assets/particles/fire2.prefab"] = 0.3f,

        ["Assets/Prefabs/VFX/FootDust_Toon.prefab"] = 0.3f,

        ["Assets/Art/Characters/Enemies/Imp Demon Cute Series/Prefabs/FX Spark.prefab"] = 0.62f,

        ["Assets/Prefabs/Orbes/Orbe_Esencia.prefab"] = 0.3f,

        ["Assets/Prefabs/Orbes/Orbe_EsenciaBrillante.prefab"] = 1.07f,

        ["Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/Basic Orb.prefab"] = 0.3f,

        ["Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/Basic Orb 2.prefab"] = 0.32f,

        ["Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/Bubble Orb .prefab"] = 1.32f,

        ["Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/Light Orb 2.prefab"] = 0.82f,

        ["Assets/Art/Matthew Guz/Orbs Effects FREE/Prefab/Multiple Earth Orb.prefab"] = 2.23f,
    };

    [MenuItem("El Sendero/Magia/Todo el juego a dibujo (INC-1127)")]
    public static void PasarTodo()
    {
        var log = new StringBuilder("=== Efectos del juego a dibujo (INC-1127) ===\n");
        var avisos = new List<string>();
        var t = VfxPropiosDeHechizosBuilder.TexturasDeDibujo();
        foreach (var e in Tabla)
        {
            try { Pasar(e, t, log, avisos); }
            catch (System.Exception ex) { avisos.Add($"{e.Ruta}: {ex.Message}"); }
        }
        AssetDatabase.SaveAssets();
        log.AppendLine($"{Tabla.Length} entradas.");
        if (avisos.Count == 0) Debug.Log(log.Append("Sin avisos.").ToString());
        else Debug.LogWarning(log.Append($"{avisos.Count} aviso(s):\n  • ").Append(string.Join("\n  • ", avisos)).ToString());
    }

    private static void Pasar(Entrada e, Texturas t, StringBuilder log, List<string> avisos)
    {
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(e.Ruta);
        if (original == null) { avisos.Add($"No encuentro {e.Ruta}."); return; }
        if (PrefabUtility.GetPrefabAssetType(original) == PrefabAssetType.Variant) { avisos.Add($"{e.Ruta} es una variante; sigue a su base."); return; }

        bool bucle;
        float medido = Medir(original, out bucle);
        if (Medidas.TryGetValue(e.Ruta, out float guardado)) medido = guardado;
        float radio = e.Radio > 0f ? e.Radio : Mathf.Min(medido * 0.7f, Tope(e.Tipo));

        var raiz = PrefabUtility.LoadPrefabContents(e.Ruta);
        try
        {
            var previo = raiz.transform.Find(NombreDelDibujo);
            if (previo != null) { bucle = EraBucle(previo); Object.DestroyImmediate(previo.gameObject); }
            Desnudar(raiz);

            // El dibujo cuelga de la raíz, que puede venir escalada: se compensa para que mida
            // 'radio' metros en el mundo.
            float escalaDeLaRaiz = Mathf.Max(0.001f, Mathf.Abs(raiz.transform.localScale.x));
            var dibujo = new GameObject(NombreDelDibujo);
            dibujo.transform.SetParent(raiz.transform, false);
            float escala = radio / escalaDeLaRaiz;
            dibujo.transform.localScale = Vector3.one * escala;
            Construir(e.Tipo, dibujo, t, e.Color);
            // Las luces se montan para tamaño 1: su alcance crece con el efecto.
            foreach (var luz in dibujo.GetComponentsInChildren<Light>(true)) luz.range *= radio;
            foreach (var ps in dibujo.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.loop = bucle;
                m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, e.Ruta);
            log.AppendLine($"   {System.IO.Path.GetFileNameWithoutExtension(e.Ruta)}: {e.Tipo}, tamaño {escala:0.##}{(bucle ? ", en bucle" : "")}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    /// Radio máximo en el mundo por tipo: la medida de los efectos originales sale grande (cuenta el
    /// humo, las estelas y lo que se dispersa), y un efecto de dibujo lee bien más recogido.
    private static float Tope(Tipo tipo)
    {
        switch (tipo)
        {
            case Tipo.Llama: return 0.5f;
            case Tipo.Orbe: return 0.6f;
            case Tipo.Chispas: case Tipo.Remolino: case Tipo.Brillo: case Tipo.Chispitas:
            case Tipo.Destello: case Tipo.Chimenea: return 1.2f;
            case Tipo.Humo: case Tipo.Tajo: case Tipo.Velocidad: case Tipo.Curacion: case Tipo.GolpeDePiedra: return 1.5f;
            case Tipo.Golpe: case Tipo.Oscuro: case Tipo.Implosion: case Tipo.Confeti: case Tipo.Columna: return 2.2f;
            case Tipo.Portal: case Tipo.Polvo: return 2.5f;
            case Tipo.Veneno: case Tipo.Estrellas: return 4f;
            default: return 6f;   // círculos, ondas y zonas: su tamaño es el área de juego
        }
    }

    private static bool EraBucle(Transform dibujo)
        => dibujo.GetComponentsInChildren<ParticleSystem>(true).Any(p => p.main.loop);

    /// Tamaño (radio aproximado) y si va en bucle, viendo el efecto original medio segundo.
    private static float Medir(GameObject original, out bool bucle)
    {
        var sistemas = original.GetComponentsInChildren<ParticleSystem>(true);
        bucle = sistemas.Any(p => p.main.loop);
        var escena = EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(original, escena);
            inst.transform.position = Vector3.zero;
            foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(0.6f, false, true, true);
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool alguno = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                if (r.bounds.size == Vector3.zero) continue;
                if (!alguno) { b = r.bounds; alguno = true; } else b.Encapsulate(r.bounds);
            }
            float radio = alguno ? Mathf.Max(b.extents.x, b.extents.z, b.extents.y * 0.6f) : 1f;
            return Mathf.Clamp(radio * 0.8f, 0.3f, 8f);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(escena);
        }
    }

    /// Quita lo que se ve (partículas, mallas, luces, animación, scripts de paquetes) y deja lo que es
    /// de juego: colisiones, cuerpos físicos y componentes del proyecto.
    private static void Desnudar(GameObject raiz)
    {
        foreach (var hijo in raiz.GetComponentsInChildren<Transform>(true).Reverse())
        {
            if (hijo == null) continue;
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(hijo.gameObject) && hijo.gameObject != raiz)
                PrefabUtility.UnpackPrefabInstance(hijo.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        Limpiar(raiz.transform, true);
    }

    private static bool Limpiar(Transform tr, bool esRaiz)
    {
        bool tieneJuego = false;
        foreach (Transform hijo in tr.Cast<Transform>().ToList())
            if (!Limpiar(hijo, false)) Object.DestroyImmediate(hijo.gameObject);
            else tieneJuego = true;

        for (int pasada = 0; pasada < 4; pasada++)
            foreach (var c in tr.GetComponents<Component>())
                if (c != null && EsVisual(c)) Object.DestroyImmediate(c);

        foreach (var c in tr.GetComponents<Component>())
            if (c != null && !(c is Transform)) tieneJuego = true;
        return esRaiz || tieneJuego;
    }

    private static bool EsVisual(Component c)
    {
        if (c is ParticleSystem || c is ParticleSystemRenderer || c is Renderer || c is MeshFilter || c is Light ||
            c is Animator || c is Animation || c is ParticleSystemForceField) return true;
        if (c.GetType().Name == "VisualEffect") return true;
        if (c is MonoBehaviour mb)
        {
            var script = MonoScript.FromMonoBehaviour(mb);
            string ruta = script != null ? AssetDatabase.GetAssetPath(script) : "";
            return string.IsNullOrEmpty(ruta) || ruta.StartsWith("Assets/VFX/");
        }
        return false;
    }

    private static void Construir(Tipo tipo, GameObject d, Texturas t, Color c)
        => VfxPropiosDeHechizosBuilder.DibujoGenerico(tipo.ToString(), d, t, c);
}
