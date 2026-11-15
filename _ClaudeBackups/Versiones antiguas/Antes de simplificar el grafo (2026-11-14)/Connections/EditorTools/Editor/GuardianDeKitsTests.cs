using System;
using System.Collections.Generic;
using System.IO;
using Kits.EditorTools;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// Vigila la regla 8.9: nada de <c>Assets/Kits</c> puede conocer El Sendero. Aquí solo están los
/// datos del juego (sus nombres); las comprobaciones las hace <see cref="KitGuard"/>. Ver TDD § 31.
/// </summary>
public class GuardianDeKitsTests
{
    // Personajes, lugares y capítulos. Con «=», solo la palabra suelta: «Will» y «Victoria»
    // también son palabras corrientes dentro de otros nombres (OnWillRenderObject).
    static readonly string[] NombresDelJuego =
    {
        "=Will", "Estela", "Liam", "Eldran", "Erika", "=Victoria", "Oliver", "Archimago",
        "Sendero", "Chuchelandia", "Eldoria", "Prologo", "Prólogo", "Cap1", "Cap2", "Cap3", "Cap4"
    };

    string _carpetaDePrueba;

    [TearDown]
    public void TearDown()
    {
        if (_carpetaDePrueba != null && Directory.Exists(_carpetaDePrueba))
            Directory.Delete(_carpetaDePrueba, true);
        _carpetaDePrueba = null;
    }

    [Test]
    public void LosKitsNoConocenElJuego()
    {
        List<KitViolation> fallos = KitGuard.Check(Reglas("Assets/Kits"), AssetDatabase.GUIDToAssetPath);

        Assert.IsEmpty(fallos, "Assets/Kits incumple la regla 8.9:\n" + string.Join("\n", fallos));
    }

    [Test]
    public void ElGuardianDetectaCadaIncumplimiento()
    {
        _carpetaDePrueba = Path.Combine(Path.GetTempPath(), "GuardianDeKits_" + Guid.NewGuid().ToString("N"));
        string kit = Path.Combine(_carpetaDePrueba, "Core");
        Directory.CreateDirectory(Path.Combine(kit, "Editor"));

        File.WriteAllText(Path.Combine(kit, "Kits.Core.asmdef"),
            "{ \"name\": \"Kits.Core\", \"references\": [ \"Assembly-CSharp\", \"Unity.TextMeshPro\" ] }");
        // Correcto: no debe dar ningún fallo, ni por el comentario.
        File.WriteAllText(Path.Combine(kit, "Pool.cs"),
            "namespace Kits.Core { // Eldran\n public class Pool { void OnWillRenderObject() {} } }");
        File.WriteAllText(Path.Combine(kit, "Bad.cs"),
            "public class Bad { string id = \"Cap1_Eldran\"; }");
        File.WriteAllText(Path.Combine(kit, "Editor", "Menu.cs"),
            "namespace Kits.Core { class Menu { [UnityEditor.MenuItem(\"El Juego/Abrir\")] static void Abrir() {} } }");
        File.WriteAllText(Path.Combine(kit, "LiamCamera.cs"), "namespace Kits.Core { class C {} }");

        string texto = string.Join("\n", KitGuard.Check(Reglas(_carpetaDePrueba), null));

        StringAssert.Contains("Bad.cs:1  «Cap1»", texto);
        StringAssert.Contains("Bad.cs:1  «Eldran»", texto);
        StringAssert.Contains("Bad.cs  falta el namespace Kits.Core", texto);
        StringAssert.Contains("LiamCamera.cs  «Liam» en la ruta", texto);
        StringAssert.Contains("menú «El Juego/…»", texto);
        StringAssert.Contains("carpeta Editor dentro de Kits.Core", texto);
        StringAssert.Contains("no es un kit: Assembly-CSharp", texto);
        StringAssert.DoesNotContain("Pool.cs", texto);
        StringAssert.DoesNotContain("TextMeshPro", texto);
    }

    static KitRuleSet Reglas(string carpetaDeKits)
    {
        return new KitRuleSet { KitsRoot = carpetaDeKits, ForbiddenTerms = NombresDelJuego };
    }
}
