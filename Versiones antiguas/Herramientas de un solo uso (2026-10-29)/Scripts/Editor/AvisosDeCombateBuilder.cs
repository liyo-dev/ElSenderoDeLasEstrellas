using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Invector.vCharacterController;

/// <summary>
/// Montaje del aviso de combate en el momento justo (INC-666) y de la presentación del
/// contraataque (INC-670): añade <see cref="AvisoDeAmenazas"/> y
/// <see cref="PresentacionDelContraataque"/> a _WILL.prefab, crea el icono en
/// Resources/UI/AvisoDeAmenaza y añade la fila «Avisos de combate» (Sí/No) a los Ajustes de los
/// menús principales, copiando la de Vibración. Idempotente.
/// </summary>
public static class AvisosDeCombateBuilder
{
    private const string RutaJugador = "Assets/Prefabs/_WILL.prefab";
    private const string RutaIcono = "Assets/Resources/UI/AvisoDeAmenaza.prefab";
    private const string SpriteAnillo = "Assets/Art/UI/HUD/ability_socket_ring.png";
    private const string ClaveDeLaFila = "SETTINGS_COMBAT_HINTS";
    private const string OndaDelContraataque = "Assets/VFX/GabrielAguiarProductions 1/FreeQuickEffectsVol1/Prefabs/vfx_Shockwave_01.prefab";
    private static readonly string[] EscenasConAjustes =
    {
        "Assets/Scenes/Systems/MainMenu.unity",
        "Assets/Scenes/Systems/MainMenu_Cinematica_Preview.unity",
    };

    [MenuItem("El Sendero/Combate/Montar avisos de combate (INC-666)")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Avisos de combate", "Sal de Play para montarlos.", "Aceptar");
            return;
        }

        var hecho = new List<string>();
        var avisos = new List<string>();
        AnadirAlJugador(hecho, avisos);
        CrearIcono(hecho, avisos);
        foreach (var escena in EscenasConAjustes) AnadirFilaDeAjustes(escena, hecho, avisos);
        AssetDatabase.SaveAssets();

        string texto = string.Join("\n", hecho);
        if (avisos.Count > 0) texto += "\n\nAvisos:\n- " + string.Join("\n- ", avisos);
        EditorUtility.DisplayDialog("Avisos de combate", texto, "Aceptar");
    }

    private static void AnadirAlJugador(List<string> hecho, List<string> avisos)
    {
        var raiz = PrefabUtility.LoadPrefabContents(RutaJugador);
        if (raiz == null) { avisos.Add($"No encuentro {RutaJugador}."); return; }
        try
        {
            var controller = raiz.GetComponentInChildren<vThirdPersonController>(true);
            if (controller == null) { avisos.Add("_WILL no tiene vThirdPersonController."); return; }
            bool cambiado = false;
            if (controller.GetComponent<AvisoDeAmenazas>() == null)
            {
                controller.gameObject.AddComponent<AvisoDeAmenazas>();
                hecho.Add("Añadido AvisoDeAmenazas a _WILL.");
                cambiado = true;
            }
            else hecho.Add("_WILL ya tenía AvisoDeAmenazas.");

            if (controller.GetComponent<PresentacionDelContraataque>() == null)
            {
                var presentacion = controller.gameObject.AddComponent<PresentacionDelContraataque>();
                var onda = AssetDatabase.LoadAssetAtPath<GameObject>(OndaDelContraataque);
                if (onda != null)
                {
                    var so = new SerializedObject(presentacion);
                    so.FindProperty("ondaEnLosPies").objectReferenceValue = onda;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else avisos.Add($"No encuentro {OndaDelContraataque}: el contraataque queda sin onda en los pies.");
                hecho.Add("Añadida PresentacionDelContraataque a _WILL (congelado, cámara lenta, destello y vibración).");
                cambiado = true;
            }
            else hecho.Add("_WILL ya tenía PresentacionDelContraataque.");

            if (cambiado) PrefabUtility.SaveAsPrefabAsset(raiz, RutaJugador);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    /// Canvas propio por encima del HUD: anillo, glifo del botón y «!» para lo que no se para.
    private static void CrearIcono(List<string> hecho, List<string> avisos)
    {
        string carpeta = Path.GetDirectoryName(RutaIcono).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(carpeta)) AssetDatabase.CreateFolder("Assets/Resources", "UI");

        var raiz = new GameObject("AvisoDeAmenaza", typeof(RectTransform));
        try
        {
            var canvas = raiz.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var escalador = raiz.AddComponent<CanvasScaler>();
            escalador.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escalador.referenceResolution = new Vector2(1920f, 1080f);
            escalador.matchWidthOrHeight = 0.5f;
            var grupo = raiz.AddComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.interactable = false;
            grupo.blocksRaycasts = false;

            var icono = Hijo("Icono", raiz.transform, new Vector2(84f, 84f));

            var anillo = Hijo("Anillo", icono, Vector2.zero).gameObject.AddComponent<Image>();
            Estirar(anillo.rectTransform);
            anillo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteAnillo);
            anillo.raycastTarget = false;
            if (anillo.sprite == null) avisos.Add($"No encuentro {SpriteAnillo}: el anillo queda sin sprite.");

            var glifo = Hijo("Glifo", icono, new Vector2(52f, 52f)).gameObject.AddComponent<Image>();
            glifo.preserveAspect = true;
            glifo.raycastTarget = false;

            var exclamacion = Hijo("Exclamacion", icono, new Vector2(60f, 70f)).gameObject.AddComponent<Text>();
            exclamacion.text = "!";
            exclamacion.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            exclamacion.fontSize = 56;
            exclamacion.fontStyle = FontStyle.Bold;
            exclamacion.alignment = TextAnchor.MiddleCenter;
            exclamacion.raycastTarget = false;
            exclamacion.enabled = false;

            var ui = raiz.AddComponent<Sendero.UI.AvisoDeAmenazaUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("icono").objectReferenceValue = icono;
            so.FindProperty("grupo").objectReferenceValue = grupo;
            so.FindProperty("anillo").objectReferenceValue = anillo;
            so.FindProperty("glifo").objectReferenceValue = glifo;
            so.FindProperty("exclamacion").objectReferenceValue = exclamacion;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, RutaIcono);
            hecho.Add($"Icono del aviso: {RutaIcono}.");
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }

    /// Copia la fila de Vibración de los Ajustes, la rotula «Avisos de combate» y la cablea.
    private static void AnadirFilaDeAjustes(string ruta, List<string> hecho, List<string> avisos)
    {
        if (!File.Exists(ruta)) { avisos.Add($"No existe {ruta}."); return; }

        var escena = SceneManager.GetSceneByPath(ruta);
        bool abiertaAqui = !escena.isLoaded;
        if (abiertaAqui) escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Additive);
        try
        {
            SettingsMenuController ajustes = null;
            foreach (var raiz in escena.GetRootGameObjects())
            {
                ajustes = raiz.GetComponentInChildren<SettingsMenuController>(true);
                if (ajustes != null) break;
            }
            if (ajustes == null) { avisos.Add($"{Path.GetFileName(ruta)}: no tiene SettingsMenuController."); return; }

            var so = new SerializedObject(ajustes);
            var si = so.FindProperty("combatHintsYesButton");
            var no = so.FindProperty("combatHintsNoButton");
            if (si.objectReferenceValue != null && no.objectReferenceValue != null)
            {
                hecho.Add($"{Path.GetFileName(ruta)}: ya tenía la fila de avisos de combate.");
                return;
            }

            var vibracion = so.FindProperty("vibrationYesButton").objectReferenceValue as Button;
            if (vibracion == null || vibracion.transform.parent == null)
            {
                avisos.Add($"{Path.GetFileName(ruta)}: no encuentro la fila de Vibración; no se toca.");
                return;
            }

            var filaOriginal = vibracion.transform.parent;
            var fila = Object.Instantiate(filaOriginal.gameObject, filaOriginal.parent);
            fila.name = "Row (Avisos de combate)";
            fila.transform.SetSiblingIndex(filaOriginal.GetSiblingIndex() + 1);

            Button botonSi = null, botonNo = null;
            foreach (var boton in fila.GetComponentsInChildren<Button>(true))
            {
                for (int i = boton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(boton.onClick, i);
                if (boton.name == "ButtonYes") botonSi = boton;
                else if (boton.name == "ButtonNo") botonNo = boton;
            }
            if (botonSi == null || botonNo == null)
            {
                Object.DestroyImmediate(fila);
                avisos.Add($"{Path.GetFileName(ruta)}: la fila de Vibración no tiene ButtonYes/ButtonNo; no se toca.");
                return;
            }

            var rotulo = fila.GetComponentInChildren<LocalizedText>(true);
            if (rotulo != null)
            {
                rotulo.gameObject.name = "AvisosDeCombateLabel";
                var soRotulo = new SerializedObject(rotulo);
                var clave = soRotulo.FindProperty("_key");
                if (clave != null) { clave.stringValue = ClaveDeLaFila; soRotulo.ApplyModifiedPropertiesWithoutUndo(); }
            }
            var texto = fila.GetComponentInChildren<Text>(true);
            if (texto != null) texto.text = "Avisos de combate";

            si.objectReferenceValue = botonSi;
            no.objectReferenceValue = botonNo;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
            hecho.Add($"{Path.GetFileName(ruta)}: fila «Avisos de combate» añadida en Ajustes.");
        }
        finally
        {
            if (abiertaAqui) EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static RectTransform Hijo(string nombre, Transform padre, Vector2 tamano)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.sizeDelta = tamano;
        return rt;
    }

    private static void Estirar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
