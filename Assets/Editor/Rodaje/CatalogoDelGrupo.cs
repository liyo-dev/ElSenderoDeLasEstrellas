#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Lista los estados de animación de Will, Estela y Liam (los que acepta «bucle» en un guion)
/// en «Guiones/_ordenes/catalogo_grupo.txt», para elegir poses de escaparate.
public static class CatalogoDelGrupo
{
    [MenuItem("El Sendero/Rodaje/Listar animaciones del grupo")]
    public static void Listar()
    {
        var sb = new StringBuilder();
        foreach (var ruta in new[] { "Assets/Prefabs/_WILL.prefab", "Assets/Prefabs/_ESTELA.prefab", "Assets/Prefabs/_LIAM.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            sb.AppendLine("## " + ruta);
            if (prefab == null) { sb.AppendLine("  (no existe)"); continue; }
            foreach (var e in CapturaDeEscenario.Catalogo(prefab))
                sb.AppendLine($"  capa={e.nombreCapa} estado={e.estado} clip={e.clip} largo={e.largo:0.00} bucle={e.bucle}");
        }
        File.WriteAllText(Path.Combine(CapturaDeEscenario.CarpetaDeGuiones, "_ordenes", "catalogo_grupo.txt"), sb.ToString());
    }
}
#endif
