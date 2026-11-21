using UnityEditor;
using UnityEngine;

/// <summary>
/// Pone la lección del escudo de Erika (Cap. 2) con la regla de los escudos elementales: se rompen
/// con la magia de su mismo color. El escudo pasa a ser de Fuego (Will lo rompe con su llama) y las
/// tres frases de Erika lo cuentan así. Menú de un solo uso: tras ejecutarlo, su archivo va a
/// «Versiones antiguas». Ver INC-1357.
/// </summary>
public static class EscudosDelMismoColor
{
    private const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    private const string Inicio = "Mi escudo es de fuego, ¿ves el color? Solo lo rompe otro fuego. Usa la Llama Astral.";
    private const string Fin = "¡Roto! Cada escudo cede ante la magia de su mismo color.";
    private const string Rebote = "¡Eso no lo rompe! Un escudo de fuego solo cede al fuego.";

    private static readonly System.Collections.Generic.Dictionary<string, string> FrasesOriginales = new()
    {
        { "ERIKA_LECCION_PROTEGERSE_INICIO", "Cuando te lance algo, mantén la Protección y aguanta." },
        { "ERIKA_LECCION_PROTEGERSE_FIN", "¡Bien aguantado!" },
        { "ERIKA_LECCION_DEVOLVER_INICIO", "Ahora alza la Protección justo cuando te llegue: se la devuelves." },
        { "ERIKA_LECCION_DEVOLVER_FIN", "¡Uf! Me ha dejado mareada. Eso es devolver." },
        { "ERIKA_LECCION_GOLPEAR_INICIO", "Y ahora pégame tú. Sin miedo, que no me haces nada." },
        { "ERIKA_LECCION_GOLPEAR_FIN", "¡Vale, vale! Ya veo que sabes." },
    };

    [MenuItem("El Sendero/Combate/Lección del escudo de Erika: mismo color (INC-1357)")]
    public static void Aplicar()
    {
        var grafo = AssetDatabase.LoadMainAssetAtPath(Grafo);
        if (grafo == null) { OrdenesDelEditor.Avisar("Escudos", "No encuentro " + Grafo); return; }

        var so = new SerializedObject(grafo);
        var it = so.GetIterator();
        int cambiados = 0;
        while (it.Next(true))
        {
            if (it.name != "elementoDelEscudo" || it.propertyType != SerializedPropertyType.Enum) continue;
            string padre = it.propertyPath.Substring(0, it.propertyPath.Length - "elementoDelEscudo".Length);
            var npc = so.FindProperty(padre + "npcId");
            var objetivo = so.FindProperty(padre + "objetivo");
            if (npc == null || npc.stringValue != "NPC_Erika" || objetivo == null) continue;

            if (objetivo.intValue == (int)PedirLeccionDeCombate.Objetivo.RomperEscudo)
            {
                it.intValue = (int)MagicElement.Fire;
                Poner(so, padre + "fraseAlEmpezar", Inicio);
                Poner(so, padre + "fraseAlAcabar", Fin);
                Poner(so, padre + "fraseAlRebotar", Rebote);
                cambiados++;
                continue;
            }

            // Las otras lecciones no llevan escudo: se dejan como estaban (una primera versión de
            // este menú les puso las frases del escudo; se devuelven las suyas).
            it.intValue = (int)MagicElement.Storm;
            Poner(so, padre + "fraseAlRebotar", "");
            var clave = so.FindProperty(padre + "claveAlEmpezar");
            if (clave != null && FrasesOriginales.TryGetValue(clave.stringValue, out var inicio)) Poner(so, padre + "fraseAlEmpezar", inicio);
            var claveFin = so.FindProperty(padre + "claveAlAcabar");
            if (claveFin != null && FrasesOriginales.TryGetValue(claveFin.stringValue, out var fin)) Poner(so, padre + "fraseAlAcabar", fin);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(grafo);
        AssetDatabase.SaveAssets();
        Debug.Log(cambiados > 0
            ? $"[Escudos] Lección del escudo de Erika: escudo de Fuego y frases de «mismo color» ({cambiados})."
            : "[Escudos] No he encontrado la lección del escudo de Erika en el Cap. 2.");
    }

    private static void Poner(SerializedObject so, string ruta, string texto)
    {
        var p = so.FindProperty(ruta);
        if (p != null && p.propertyType == SerializedPropertyType.String) p.stringValue = texto;
    }
}
