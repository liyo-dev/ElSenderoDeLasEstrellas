using System;
using System.Collections.Generic;
using UnityEngine;

/// La versión de un gesto que le va a cada personaje: no todos ríen igual. Los guiones y las reacciones
/// automáticas piden el gesto de siempre («Laugh01») y aquí se cambia por el del personaje, si tiene uno
/// apuntado y su Animator tiene ese estado. Quien no esté en la tabla hace el gesto tal cual.
///
/// La tabla es un texto en Resources/Actuacion/gestos_por_personaje.txt, una línea por personaje:
///   NPC_Eldran  Laugh01=Laugh_Risita
/// (el id del actor y, separados por espacios, tantos gesto=variante como haga falta; # para comentar).
/// Un guion que quiera una variante concreta la pide por su nombre («gesto=Laugh_Carcajada») y no pasa por aquí.
public static class GestosPorPersonaje
{
    private const string Recurso = "Actuacion/gestos_por_personaje";
    private static Dictionary<string, Dictionary<string, string>> _tabla;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => _tabla = null;
#endif

    public static string Variante(SequenceActor actor, string gesto)
    {
        if (actor == null || string.IsNullOrEmpty(gesto) || string.IsNullOrEmpty(actor.Id)) return gesto;
        _tabla ??= Cargar();
        if (_tabla.TryGetValue(actor.Id, out var suyos) && suyos.TryGetValue(gesto, out string variante) &&
            actor.TieneEstado(variante)) return variante;
        return gesto;
    }

    private static Dictionary<string, Dictionary<string, string>> Cargar()
    {
        var tabla = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var texto = Resources.Load<TextAsset>(Recurso);
        if (texto == null) return tabla;
        foreach (string cruda in texto.text.Split('\n'))
        {
            string linea = cruda.Trim();
            if (linea.Length == 0 || linea[0] == '#') continue;
            string[] partes = linea.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2) continue;
            if (!tabla.TryGetValue(partes[0], out var suyos)) tabla[partes[0]] = suyos = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 1; i < partes.Length; i++)
            {
                int igual = partes[i].IndexOf('=');
                if (igual > 0 && igual < partes[i].Length - 1) suyos[partes[i].Substring(0, igual)] = partes[i].Substring(igual + 1);
            }
        }
        return tabla;
    }
}
