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
/// Las notas se conservan después del build para permitir recompilar y probar sin reescribirlas.
/// Se actualizan al preparar cambios nuevos; generar un ejecutable no equivale a publicarlo.
/// El panel muestra únicamente la entrada actual, sin archivar un histórico. Ver INC-737.
///
/// El menú "El Sendero → Build → Saltar autoincremento de versión (solo el próximo build)" permite
/// generar un build de pruebas sin validar las notas. El preprocess consulta
/// BuildVersionIncrementer.SeSaltaraElProximoBuild porque el incrementador aún no consume el aviso.
/// </summary>
public class PatchNotesBuildGuard : IPreprocessBuildWithReport
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

        ValidarNotas();

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string datePath = Path.Combine(projectRoot, BuildDateFile);
        File.WriteAllText(datePath, PatchNotesDateFormatter.FormatSpanishDate(System.DateTime.Now));
        AssetDatabase.ImportAsset(BuildDateFile);
    }

    /// Valida sin modificar archivos ni consumir el permiso de build de pruebas.
    [MenuItem("El Sendero/Build/Validar notas del parche")]
    public static void ValidarNotas()
    {
        if (BuildVersionIncrementer.SeSaltaraElProximoBuild) return;
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

    }

}
