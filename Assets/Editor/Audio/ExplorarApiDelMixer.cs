#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Herramienta de diagnostico: Unity se introspecciona a si mismo y cuenta en el log que
/// API interna tiene para tocar el AudioMixer (crear efectos, parametros, ids nativos).
/// Es temporal: se borra cuando la automatizacion del mixer este hecha.
public static class ExplorarApiDelMixer
{
    const string RUTA_MIXER = "Assets/AudioMixer.mixer";

    [InitializeOnLoadMethod]
    static void ExplorarAlCargar()
    {
        EditorApplication.delayCall += Ejecutar;
    }

    static void Ejecutar()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== API INTERNA DEL MIXER ===");

        foreach (var nombre in new[] { "UnityEditor.Audio.AudioMixerController",
                                       "UnityEditor.Audio.AudioMixerGroupController",
                                       "UnityEditor.Audio.AudioMixerEffectController" })
        {
            var tipo = Type.GetType(nombre + ", UnityEditor");
            if (tipo == null) { sb.AppendLine("NO ENCONTRADO: " + nombre); continue; }
            sb.AppendLine($"-- {tipo.FullName} (public={tipo.IsPublic})");
            foreach (var m in tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.Name.Contains("Effect") || m.Name.Contains("Group") || m.Name.Contains("Add")
                    || m.Name.Contains("Create") || m.Name.Contains("Remove") || m.Name.Contains("Parameter"))
                    sb.AppendLine($"     {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            }
            foreach (var f in tipo.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                sb.AppendLine($"     [campo] {f.FieldType.Name} {f.Name}");
        }

        // Como esta serializado ahora mismo un grupo con sus efectos.
        var objetos = AssetDatabase.LoadAllAssetsAtPath(RUTA_MIXER);
        sb.AppendLine($"-- sub-asset del mixer: {objetos.Length}");
        foreach (var o in objetos)
        {
            if (o == null) continue;
            sb.AppendLine($"   {o.GetType().FullName} '{o.name}'");
            var so = new SerializedObject(o);
            var it = so.GetIterator();
            bool entrar = true;
            while (it.NextVisible(entrar))
            {
                entrar = false;
                if (it.name.Contains("Effect") || it.name.Contains("Group") || it.name.Contains("Volume")
                    || it.name.Contains("Send") || it.name.Contains("Exposed") || it.name.Contains("Name"))
                {
                    string valor = it.propertyType.Name;
                    if (it.propertyType == SerializedPropertyType.String) valor = it.stringValue;
                    else if (it.propertyType == SerializedPropertyType.Float) valor = it.floatValue.ToString();
                    else if (it.propertyType == SerializedPropertyType.Boolean) valor = it.boolValue.ToString();
                    else if (it.propertyType == SerializedPropertyType.Integer) valor = it.intValue.ToString();
                    sb.AppendLine($"      {it.propertyPath} ({it.propertyType}) = {valor}");
                }
            }
        }

        var ruta = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "ExploracionDelMixer.txt");
        System.IO.File.WriteAllText(ruta, sb.ToString(), Encoding.UTF8);
        Debug.Log("[Audio] Exploración de la API del mixer escrita en Logs/ExploracionDelMixer.txt");
    }
}
#endif
