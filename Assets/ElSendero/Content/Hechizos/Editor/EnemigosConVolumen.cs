using System;
using Kits.StylizedVfx;
using UnityEditor;
using UnityEngine;
using R = Kits.StylizedVfx.Editor.EnergyRecipes;

/// Lo que lanzan los enemigos y los magos rivales con la receta «dibujo animado con volumen» (la de
/// los hechizos del grupo, INC-1140): la bola de plasma (demonio, Erika, Lety, Vicky, magos), su
/// impacto, la lluvia del demonio y los avisos en el suelo. Se rehacen EN SU SITIO los prefabs
/// visuales de Assets/_VFX/Hechizos, así que las bolas de Assets/_ENEMY_SPELLS (con su
/// EnemyProjectile) y los enemigos que los usan siguen enganchados.
/// También quita la flor de luz de la protección de los NPC: sus fichas de combate pasan al escudo
/// de burbuja (Escudo_Npc). Ver INC-1187
public static class EnemigosConVolumen
{
    private const string Carpeta = "Assets/_VFX/Hechizos";
    private const string CarpetaDeFichas = "Assets/_NPCs/Combat";

    [MenuItem("El Sendero/Magia/Enemigos con volumen (INC-1187)")]
    public static void Menu()
    {
        var log = new System.Text.StringBuilder();
        Construir(log);
        Debug.Log("[VFX] Enemigos con volumen: " + log);
    }

    public static void Construir(System.Text.StringBuilder log)
    {
        if (!HechizosConVolumen.PrepararPaletas()) return;
        var m = HechizosConVolumen.Mallas;
        var rojo = HechizosConVolumen.Paleta("Demonio");
        var oscuro = HechizosConVolumen.Paleta("Oscuro");
        var humo = HechizosConVolumen.Paleta("PolvoOscuro");

        // Bola de plasma: núcleo rojo que tiembla, un aro que vuelca alrededor y humo granate y
        // negro que se queda atrás. El envoltorio de la bola la escala ×2: se monta a la mitad.
        Rehacer(VfxPropiosDeHechizosBuilder.PlasmaDibujo, r =>
        {
            R.Projectile(r, rojo, m, new R.ProjectileStyle
            {
                Core = 0.5f, Rings = 1, TrailMesh = m.Puff, TrailRate = 10f, TrailColors = new[] { humo }, Light = 3f,
                Wobble = WobbleMode.Corkscrew, WobbleAmplitude = 0.04f, WobbleFrequency = 8f,
            });
            r.transform.localScale = Vector3.one * 0.5f;
        }, log);
        Rehacer(VfxPropiosDeHechizosBuilder.PlasmaDibujoImpacto,
                r => R.Impact(r, rojo, m, new R.ImpactStyle { Scale = 1.5f, Waves = 2, Puffs = humo }), log);
        Rehacer(VfxPropiosDeHechizosBuilder.DemonioLanzamiento,
                r => R.Impact(r, rojo, m, new R.ImpactStyle { Scale = 0.8f, Puffs = humo }), log);

        // Lluvia: gota de fuego que cae con su estela de humo, golpe al dar y el aviso del suelo.
        Rehacer(VfxPropiosDeHechizosBuilder.DemonioLluviaGota,
                r => R.Projectile(r, rojo, m, new R.ProjectileStyle { Core = 0.45f, TrailMesh = m.Puff, TrailRate = 12f, TrailColors = new[] { humo } }), log);
        Rehacer(VfxPropiosDeHechizosBuilder.DemonioLluviaImpacto,
                r => R.Impact(r, rojo, m, new R.ImpactStyle { Scale = 1.3f, Waves = 1, Puffs = humo }), log);
        Rehacer(VfxPropiosDeHechizosBuilder.DemonioLluviaAviso, r => R.GroundWarning(r, rojo, m, 1.8f), log);
        Rehacer(VfxPropiosDeHechizosBuilder.GolemAviso, r => R.GroundWarning(r, rojo, m, 1.6f), log);

        // Protección del Mago Oscuro: burbuja como la del resto, en su violeta.
        Rehacer(VfxPropiosDeHechizosBuilder.EscudoMagoOscuro, r => R.Shield(r, oscuro, m, 0.95f, 2.6f, true), log);

        PonerEscudoDeBurbuja(log);
        AssetDatabase.SaveAssets();
    }

    /// Las fichas de combate de los NPC (Erika, Lety, Vicky, magos…) usan el escudo de burbuja.
    private static void PonerEscudoDeBurbuja(System.Text.StringBuilder log)
    {
        var escudo = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPropiosDeHechizosBuilder.Ruta(VfxPropiosDeHechizosBuilder.EscudoNpc));
        if (escudo == null) { Debug.LogError("[VFX] Falta el escudo de los NPC: pasa antes «Hechizos con volumen»."); return; }
        foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { CarpetaDeFichas }))
        {
            var ficha = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (ficha == null) continue;
            var so = new SerializedObject(ficha);
            var campo = so.FindProperty("shieldPrefab");
            if (campo == null || campo.objectReferenceValue == null || campo.objectReferenceValue == escudo) continue;
            campo.objectReferenceValue = escudo;
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Append('(').Append(ficha.name).Append(": escudo de burbuja) ");
        }
    }

    private static void Rehacer(string nombre, Action<GameObject> montar, System.Text.StringBuilder log)
    {
        R.RebuildInPlace($"{Carpeta}/VFX_Hechizo_{nombre}.prefab", "VFX_Hechizo_" + nombre, montar);
        log.Append(nombre).Append(' ');
    }
}
