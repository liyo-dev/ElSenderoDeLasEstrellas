using System.IO;
using UnityEditor;
using UnityEngine;

/// Crea (o completa) el asset de los efectos de la devolución con la tabla de partida y el VFX
/// de aturdido de dibujo en los estados que incapacitan. Ver INC-1165.
public static class EfectosDeLaDevolucionMenu
{
    private const string Carpeta = "Assets/Resources/Combate";
    private const string Ruta = Carpeta + "/EfectosDeLaDevolucion.asset";
    private const string Aturdido = "Assets/_VFX/Hechizos/VFX_Hechizo_Mente_Estado_Aturdido.prefab";

    [MenuItem("El Sendero/Combate/Efectos de la devolución: crear o completar (INC-1165)")]
    public static void Crear()
    {
        Directory.CreateDirectory(Carpeta);
        var tabla = AssetDatabase.LoadAssetAtPath<EfectosDeLaDevolucion>(Ruta);
        if (tabla == null)
        {
            tabla = ScriptableObject.CreateInstance<EfectosDeLaDevolucion>();
            tabla.efectos = EfectosDeLaDevolucion.PorDefecto();
            AssetDatabase.CreateAsset(tabla, Ruta);
        }
        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(Aturdido);
        for (int i = 0; i < tabla.efectos.Length; i++)
        {
            var e = tabla.efectos[i];
            if (e.vfx == null && (e.estado == EstadoDeCombate.Aturdir || e.estado == EstadoDeCombate.Dormir)) e.vfx = vfx;
            tabla.efectos[i] = e;
        }
        EditorUtility.SetDirty(tabla);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Combate] Efectos de la devolución en {Ruta} ({tabla.efectos.Length} elementos).");
    }
}
