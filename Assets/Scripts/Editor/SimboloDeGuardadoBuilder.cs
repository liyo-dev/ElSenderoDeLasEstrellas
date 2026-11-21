using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta el símbolo de guardado (idempotente): crea o pone al día el
/// <see cref="EstiloDelGuardado"/> de Resources con las letras de los menús de papel. El
/// indicador de la esquina y el aviso del arranque se montan por código y lo leen de ahí; no hay
/// nada que poner en escenas ni prefabs. Ver INC-1314.
/// </summary>
public static class SimboloDeGuardadoBuilder
{
    const string Menu = "El Sendero/Interfaz/Símbolo de guardado (indicador y aviso al arrancar)";
    const string Ruta = "Assets/Resources/" + EstiloDelGuardado.RutaEnResources + ".asset";
    // El de los menús de papel (Baloo 2 y Nunito); lo crea «Menús de papel como la maqueta».
    const string RutaEstilo = "Assets/_UI/Estilo_MenusDePapel.asset";

    [MenuItem(Menu)]
    public static void Montar()
    {
        if (Application.isPlaying) { OrdenesDelEditor.Avisar("Símbolo de guardado", "Sal de Play antes de montarlo."); return; }

        bool creado = false;
        var ajustes = AssetDatabase.LoadAssetAtPath<EstiloDelGuardado>(Ruta);
        if (ajustes == null)
        {
            ajustes = ScriptableObject.CreateInstance<EstiloDelGuardado>();
            AssetDatabase.CreateAsset(ajustes, Ruta);
            creado = true;
        }

        var estilo = AssetDatabase.LoadAssetAtPath<EstiloDeMenuSencillo>(RutaEstilo);
        bool cambio = creado;
        if (ajustes.estilo != estilo)
        {
            ajustes.estilo = estilo;
            cambio = true;
        }
        if (cambio)
        {
            EditorUtility.SetDirty(ajustes);
            AssetDatabase.SaveAssets();
        }

        OrdenesDelEditor.Avisar("Símbolo de guardado",
            Ruta + (creado ? " creado" : cambio ? " puesto al día" : " ya estaba") +
            " · letras: " + (estilo != null ? AssetDatabase.GetAssetPath(estilo) : "¡faltan!"));
    }
}
