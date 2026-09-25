using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Los objetos encuadrables del SequenceStage que apuntan a un decorado APAGADO (INC-403).
///
/// ── Qué pasó ──────────────────────────────────────────────────────────────────────────────────
/// En la grabación prologo16 (24 sep) «la carreta y el globo no se mueven», «el globo no se rompe,
/// se atranca» y «tapa la ventana de la iglesia». En Prologo_Valle hay ahora DOS decorados: el
/// original (`DECORADO`, desactivado) y el que se ve, dentro de `ISLA_POSTGAME_MODULO`. Las
/// entradas del SequenceStage (PROP_Carreta, PROP_Globo, PROP_Horno, PROP_Campanario) seguían
/// apuntando a los objetos del decorado apagado: la secuencia levantaba una carreta invisible y
/// soltaba un globo invisible, y en pantalla se quedaban quietos los de la copia — el globo, en
/// el campanario, tapando la vidriera.
///
/// ── Regla ─────────────────────────────────────────────────────────────────────────────────────
/// Si el objeto de un prop está encendido él mismo pero algún padre está apagado, es una
/// referencia huérfana: se busca en la misma escena un objeto ENCENDIDO con el mismo nombre (y,
/// si hay varios, el que comparte más nombres de padres) y se apunta a él. Los props apagados a
/// propósito (el incendio, el escudo, la puerta del Sendero, que se encienden con un beat) están
/// apagados ELLOS MISMOS, así que no se tocan.
public static class RepararPropsDelEscenario
{
    [MenuItem("El Sendero/Prólogo: apuntar los objetos del escenario al decorado que se ve", priority = 33)]
    public static void Menu()
    {
        int n = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var escena = SceneManager.GetSceneAt(i);
            if (escena.isLoaded && escena.name.StartsWith("Prologo")) n += Ejecutar(escena);
        }
        EditorUtility.DisplayDialog("Objetos del escenario",
            n > 0 ? $"{n} objeto(s) apuntaban al decorado apagado y ya apuntan al que se ve. Guarda con Ctrl+S."
                  : "Todos los objetos del escenario apuntan ya a algo que se ve.", "Vale");
    }

    public static int Ejecutar(Scene escena)
    {
        if (!escena.IsValid() || !escena.isLoaded) return 0;

        int arreglados = 0;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var stage in raiz.GetComponentsInChildren<SequenceStage>(true))
            {
                var so = new SerializedObject(stage);
                var props = so.FindProperty("_props");
                if (props == null) continue;

                for (int k = 0; k < props.arraySize; k++)
                {
                    var entrada = props.GetArrayElementAtIndex(k);
                    var target = entrada.FindPropertyRelative("target");
                    var t = target.objectReferenceValue as Transform;
                    string id = entrada.FindPropertyRelative("id").stringValue;
                    if (t == null)
                    {
                        // Pasa si se borra o se sustituye el decorado al que apuntaba (p. ej. al pegar
                        // el definitivo encima de ISLA_POSTGAME_MODULO): ya no hay nombre que buscar.
                        Debug.LogWarning($"[Escenario] '{id}' no apunta a nada (¿se borró o se sustituyó el " +
                                         "decorado?). Hay que apuntarlo a mano en el SequenceStage.", stage);
                        continue;
                    }
                    if (!t.gameObject.activeSelf || t.gameObject.activeInHierarchy) continue;

                    var bueno = BuscarGemeloEncendido(escena, t);
                    if (bueno == null)
                    {
                        Debug.LogWarning($"[Escenario] '{id}' apunta a '{Ruta(t)}', que está dentro de algo " +
                                         "apagado, y no encuentro otro objeto encendido que se llame igual. " +
                                         "No lo toco: hay que apuntarlo a mano en el SequenceStage.", stage);
                        continue;
                    }

                    target.objectReferenceValue = bueno;
                    arreglados++;
                    Debug.Log($"[Escenario] '{id}': '{Ruta(t)}' (apagado) → '{Ruta(bueno)}'.", bueno);
                }

                if (so.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(stage);
                    EditorSceneManager.MarkSceneDirty(escena);
                }
            }
        }
        return arreglados;
    }

    private static Transform BuscarGemeloEncendido(Scene escena, Transform viejo)
    {
        var padresViejos = new List<string>();
        for (var p = viejo.parent; p != null; p = p.parent) padresViejos.Add(p.name);

        Transform mejor = null;
        int mejorPuntos = -1;
        foreach (var raiz in escena.GetRootGameObjects())
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(false))
            {
                if (t == viejo || t.name != viejo.name || !t.gameObject.activeInHierarchy) continue;

                int puntos = 0;
                var p = t.parent;
                for (int i = 0; i < padresViejos.Count && p != null; i++, p = p.parent)
                {
                    if (p.name != padresViejos[i]) break;
                    puntos++;
                }

                if (puntos > mejorPuntos) { mejor = t; mejorPuntos = puntos; }
            }
        }
        return mejor;
    }

    private static string Ruta(Transform t)
    {
        string r = t.name;
        for (var p = t.parent; p != null; p = p.parent) r = p.name + "/" + r;
        return r;
    }
}
