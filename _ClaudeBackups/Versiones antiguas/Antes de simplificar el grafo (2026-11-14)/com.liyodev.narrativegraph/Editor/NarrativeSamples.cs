using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace NarrativeGraphEditor
{
    /// Importa los ejemplos del paquete (lo mismo que «Import» en el Package Manager). Para usarlo
    /// desde la línea de órdenes: -executeMethod NarrativeGraphEditor.NarrativeSamples.ImportAll
    public static class NarrativeSamples
    {
        public const string PackageName = "com.liyodev.narrativegraph";

        public static void ImportAll()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NarrativeSamples).Assembly);
            if (info == null) { Debug.LogWarning("[Narrative Graph] No está instalado como paquete: no hay ejemplos que importar."); return; }
            foreach (var sample in Sample.FindByPackage(info.name, info.version))
            {
                bool ok = sample.Import(Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow);
                Debug.Log($"[Narrative Graph] Ejemplo «{sample.displayName}»: {(ok ? "importado" : "no se pudo importar")}.");
            }
            AssetDatabase.Refresh();
        }
    }
}
