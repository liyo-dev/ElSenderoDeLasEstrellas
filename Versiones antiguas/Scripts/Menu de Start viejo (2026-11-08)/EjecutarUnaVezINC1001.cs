using UnityEditor;

/// <summary>Ejecuta una vez, al compilar, los dos menús de INC-1001 (rasgos de las capas y quitar el menú viejo de Start).</summary>
[InitializeOnLoad]
static class EjecutarUnaVezINC1001
{
    private const string Clave = "ElSendero.INC1001.Ejecutado";

    static EjecutarUnaVezINC1001()
    {
        if (EditorPrefs.GetBool(Clave, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(Clave, false)) return;
            EditorPrefs.SetBool(Clave, true);
            RasgosDeLasCapas.Poner();
            QuitarMenuViejoDeStart.Quitar();
        };
    }
}
