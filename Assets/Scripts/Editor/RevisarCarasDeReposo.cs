#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Informa de la configuración de reposo de cada controlador sin guardar prefabs.
public static class RevisarCarasDeReposo
{
    [MenuItem("El Sendero/Diálogos/Revisar caras de reposo")]
    public static void Revisar()
    {
        var informe = new StringBuilder("# Caras de reposo\n\n");
        informe.AppendLine("| Prefab | Ojos de reposo asignados | Boca de reposo asignada | Ojos activos en el prefab | Boca activa | AVISO |");
        informe.AppendLine("|---|---|---|---|---|---|");
        int total = 0, avisos = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_NPCs", "Assets/Prefabs" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            GameObject raiz = null;
            try
            {
                raiz = PrefabUtility.LoadPrefabContents(ruta);
                foreach (var controlador in raiz.GetComponentsInChildren<NPCEmotionController>(true))
                {
                    total++;
                    var datos = new SerializedObject(controlador);
                    var perfil = datos.FindProperty("emotionProfile").objectReferenceValue as EmotionProfile;
                    var ojos = datos.FindProperty("ojosDeReposo").objectReferenceValue as GameObject;
                    var boca = datos.FindProperty("bocaDeReposo").objectReferenceValue as GameObject;
                    string prefijoOjos = datos.FindProperty("eyePrefix").stringValue;
                    string prefijoBoca = datos.FindProperty("mouthPrefix").stringValue;
                    var activosOjos = new StringBuilder();
                    var activasBocas = new StringBuilder();
                    var aviso = new StringBuilder();
                    if (perfil == null) aviso.Append("Sin perfil; ");
                    if (ojos == null) aviso.Append("Ojos de reposo sin asignar; ");
                    else if (perfil != null && !perfil.EsOjoNeutro(ojos.name)) aviso.Append("Ojos asignados no neutros; ");
                    if (boca == null) aviso.Append("Boca de reposo sin asignar; ");
                    else if (perfil != null && !perfil.EsBocaNeutra(boca.name)) aviso.Append("Boca asignada no neutra; ");
                    foreach (var pieza in controlador.GetComponentsInChildren<Transform>(true))
                    {
                        if (pieza == controlador.transform || !pieza.gameObject.activeSelf) continue;
                        if (EsPieza(pieza.name, prefijoOjos))
                        {
                            activosOjos.Append(pieza.name).Append(" ");
                            if (perfil != null && !perfil.EsOjoNeutro(pieza.name)) aviso.Append("Ojos activos no neutros; ");
                        }
                        if (EsPieza(pieza.name, prefijoBoca))
                        {
                            activasBocas.Append(pieza.name).Append(" ");
                            if (perfil != null && !perfil.EsBocaNeutra(pieza.name)) aviso.Append("Boca activa no neutra; ");
                        }
                    }
                    string entreabierta = perfil != null ? perfil.bocaHablandoEntreabierta : "Mouth08";
                    string abierta = perfil != null ? perfil.bocaHablandoAbierta : "Mouth10";
                    bool tieneEntreabierta = false, tieneAbierta = false;
                    foreach (var pieza in controlador.GetComponentsInChildren<Transform>(true))
                    {
                        if (pieza == controlador.transform) continue;
                        if (pieza.name == entreabierta) tieneEntreabierta = true;
                        if (pieza.name == abierta) tieneAbierta = true;
                    }
                    if (!tieneEntreabierta || !tieneAbierta)
                        aviso.Append("Faltan mallas de hablar: ")
                            .Append(!tieneEntreabierta ? entreabierta : "")
                            .Append(!tieneEntreabierta && !tieneAbierta ? ", " : "")
                            .Append(!tieneAbierta ? abierta : "")
                            .Append(". Usa El Sendero/Diálogos/Completar caras de todos los personajes; ");
                    if (activosOjos.Length == 0) aviso.Append("Sin ojos activos; ");
                    if (activasBocas.Length == 0) aviso.Append("Sin boca activa; ");
                    if (aviso.Length > 0) avisos++;
                    informe.AppendLine($"| {Celda(ruta)} ({Celda(controlador.name)}) | {Celda(ojos != null ? ojos.name : "—")} | {Celda(boca != null ? boca.name : "—")} | {Celda(activosOjos.ToString())} | {Celda(activasBocas.ToString())} | {Celda(aviso.ToString())} |");
                }
            }
            catch (Exception error)
            {
                avisos++;
                informe.AppendLine($"| {Celda(ruta)} | — | — | — | — | Error de lectura: {Celda(error.Message)} |");
            }
            finally
            {
                if (raiz != null) PrefabUtility.UnloadPrefabContents(raiz);
            }
        }
        string carpeta = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Claude outputs"));
        Directory.CreateDirectory(carpeta);
        string destino = Path.Combine(carpeta, "caras-de-reposo.md");
        File.WriteAllText(destino, informe.ToString(), new UTF8Encoding(false));
        Debug.Log($"Caras de reposo: {total} controladores, {avisos} avisos. Informe: {destino}");
    }
    private static bool EsPieza(string nombre, string prefijo)
        => !string.IsNullOrEmpty(prefijo) && nombre.Length > prefijo.Length
            && nombre.StartsWith(prefijo, StringComparison.Ordinal) && char.IsDigit(nombre[prefijo.Length]);
    private static string Celda(string texto) => texto.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
#endif