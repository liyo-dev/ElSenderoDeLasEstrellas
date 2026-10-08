using System.IO;
using Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// NOTA: igual que BuildVersionIncrementer.cs, este archivo cualifica siempre "UnityEditor.PlayerSettings"
// (nunca "PlayerSettings" a secas) porque el proyecto tiene su propia clase global "PlayerSettings"
// en Assets/Scripts/Core/PlayerSettings.cs que si no se cualifica gana la resolución y rompe la
// compilación (CS0117).

/// <summary>
/// Mantiene las Notas del Parche in-game (<see cref="PatchNotesFlyoutPanel"/>) sincronizadas con
/// cada build real para mostrar las notas de la versión actual sin texto interno de pendiente.
///
/// Trabaja sobre dos archivos en Assets/Resources/PatchNotes/, cargados en runtime por
/// PatchNotesFlyoutPanel mediante Resources.Load&lt;TextAsset&gt;:
/// - CurrentEntryBullets.txt: los cambios de la build en curso, sin cabecera ni número de versión.
/// - BuildDate.txt: la fecha del build más reciente, en el formato de la cabecera.
///
/// OnPreprocessBuild (callbackOrder -1100) valida las notas antes de que BuildVersionIncrementer
/// (-1000) suba y guarde la versión, para que un build cancelado por notas no gaste un número.
/// Si CurrentEntryBullets.txt falta, está vacío o contiene el marcador de pendiente, cancela el
/// build mediante BuildFailedException. Si las notas son válidas, escribe la fecha en BuildDate.txt.
///
/// OnPostprocessBuild resetea CurrentEntryBullets.txt al marcador de pendiente solo si el build
/// termina en éxito y BuildVersionIncrementer.WasLastBuildVersionSkipped indica que no se salta
/// el autoincremento. El panel muestra únicamente la entrada actual, sin archivar un histórico.
///
/// El menú "El Sendero → Build → Saltar autoincremento de versión (solo el próximo build)" permite
/// generar un build de pruebas sin validar ni resetear las notas. El preprocess consulta
/// BuildVersionIncrementer.SeSaltaraElProximoBuild porque el incrementador aún no consume el aviso.
/// </summary>
public class PatchNotesBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -1100;

    const string PendingPlaceholder =
        "(Pendiente: añade aquí los cambios de esta build antes de compilar.)";

    const string ResourcesRoot = "Assets/Resources/PatchNotes";
    const string CurrentEntryFile = ResourcesRoot + "/CurrentEntryBullets.txt";
    const string BuildDateFile = ResourcesRoot + "/BuildDate.txt";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (BuildVersionIncrementer.SeSaltaraElProximoBuild)
        {
            Debug.Log("[PatchNotesBuildGuard] Autoincremento de versión saltado para este build — " +
                      "no se validan ni tocan las Notas del Parche.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string currentEntryPath = Path.Combine(projectRoot, CurrentEntryFile);

        if (!File.Exists(currentEntryPath))
        {
            throw new BuildFailedException(
                $"[PatchNotesBuildGuard] Build cancelado: no existe '{CurrentEntryFile}'. Crea el " +
                "archivo con los cambios de esta build (solo los bullets, sin cabecera de versión) " +
                "antes de compilar.");
        }

        string bullets = File.ReadAllText(currentEntryPath).Trim();

        if (string.IsNullOrEmpty(bullets) || bullets == PendingPlaceholder)
        {
            throw new BuildFailedException(
                $"[PatchNotesBuildGuard] Build cancelado: '{CurrentEntryFile}' está vacío o sigue con " +
                "el marcador de pendiente. Añade los cambios reales de esta build antes de compilar " +
                "(o usa 'El Sendero → Build → Saltar autoincremento de versión' si es un build de " +
                "pruebas que no vas a publicar).");
        }

        string datePath = Path.Combine(projectRoot, BuildDateFile);
        File.WriteAllText(datePath, PatchNotesDateFormatter.FormatSpanishDate(System.DateTime.Now));
        AssetDatabase.ImportAsset(BuildDateFile);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (BuildVersionIncrementer.WasLastBuildVersionSkipped)
            return;

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogWarning("[PatchNotesBuildGuard] El build no terminó en éxito — no se resetean " +
                              "las Notas del Parche (se dejan tal cual para reintentar).");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string currentEntryPath = Path.Combine(projectRoot, CurrentEntryFile);

        File.WriteAllText(currentEntryPath, PendingPlaceholder);
        AssetDatabase.ImportAsset(CurrentEntryFile);

        Debug.Log("[PatchNotesBuildGuard] CurrentEntryBullets.txt reseteado para la próxima build " +
                  $"(v{UnityEditor.PlayerSettings.bundleVersion} ya publicada).");
    }
}
