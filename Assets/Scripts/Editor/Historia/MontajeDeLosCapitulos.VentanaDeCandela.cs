using System;
using System.Linq;
using System.Text;
using Liyodev.NarrativeGraph;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class MontajeDeLosCapitulos
{
    private const string GuionVentanaCandela = "Cap3_02b_LaVentanaDeCandela";
    private const string TituloVentanaCandela = "3.2 · Candela abre la ventana";
    private const string GrupoVentanaCandela = "Casa del farol";
    private const string EntradaVentanaCandela = "CAP3_VENTANA_CANDELA_START";
    private const string SalidaVentanaCandela = "CAP3_VENTANA_CANDELA_DONE";
    private const float FachadaVentanaCandela = -1.8f;
    private static string RutaSecuenciaVentanaCandela => $"{CarpetaSecuencias}/SEQ_{GuionVentanaCandela}.asset";

    private static NarrativeNode CinematicaVentanaCandela()
    {
        var secuencia = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuenciaVentanaCandela);
        return secuencia == null ? null : GameStepNode.For(new PedirCinematica
        {
            cinematicName = TituloVentanaCandela,
            signalIn = EntradaVentanaCandela,
            signalDone = SalidaVentanaCandela,
            secuencia = secuencia,
            plantillaDeAjustes = "SEQ_Cap3_02_LaPosada",
        });
    }

    // Se monta también con los capítulos. Esta entrada permite ajustar la fachada y el plano sin rehacer MainWorld.
    [MenuItem("El Sendero/Historia/Montar la ventana de Candela")]
    public static void MontarVentanaDeCandela()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var escena = SceneManager.GetSceneByPath(RutaMainWorld);
        bool cerrar = !escena.isLoaded;
        if (cerrar) escena = EditorSceneManager.OpenScene(RutaMainWorld, OpenSceneMode.Additive);
        bool teniaCambios = escena.isDirty;
        try
        {
            var grafo = AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap3.asset");
            var anterior = grafo.FindNode("cap3-candela-dlg");
            if (anterior == null) throw new InvalidOperationException("Falta el diálogo de Candela en el grafo.");

            var casa = escena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .First(t => t.name == CasaDelFarol);
            var raiz = escena.GetRootGameObjects().First(r => r.name == NombreGenerado).transform;
            var candela = raiz.Find(Aldea + " · roster/SP_SPAWN_Candela");
            var informe = new StringBuilder();
            if (!MontarVentanaDeCandela(casa, candela, informe)) throw new InvalidOperationException(informe.ToString());

            var secuencia = AssetDatabase.LoadAssetAtPath<SequenceDefinition>(RutaSecuenciaVentanaCandela);
            if (secuencia == null)
            {
                secuencia = ScriptableObject.CreateInstance<SequenceDefinition>();
                secuencia.phases.Add(new SequencePhase { name = "Guion", beats = { new GuionBeat() } });
                AssetDatabase.CreateAsset(secuencia, RutaSecuenciaVentanaCandela);
            }
            secuencia.displayName = "La ventana de Candela";
            secuencia.signalIn = EntradaVentanaCandela;
            secuencia.signalOut = SalidaVentanaCandela;
            EditorUtility.SetDirty(secuencia);
            if (HorneadorDeGuion.Hornear($"{CarpetaSecuencias}/Guiones/{GuionVentanaCandela}.guion.txt", false) == null)
                throw new InvalidOperationException("No se ha podido hornear el guion de Candela.");

            var datos = new Capitulo { Nombre = anterior.Chapter };
            var nuevo = CinematicaVentanaCandela();
            Poner(grafo, datos, anterior.Guid, TituloVentanaCandela, anterior.Position, nuevo, anterior.Outputs);
            grafo.FindNode("cap3-candela-hablar").DisplayTitle = "3.2 · Llamar a la ventana de Candela";
            EditorUtility.SetDirty(grafo);
            EditorSceneManager.MarkSceneDirty(escena);
            bool guardar = !teniaCambios || OrdenesDelEditor.Confirmar(TituloVentanaCandela,
                "MainWorld tiene otros cambios pendientes. ¿Guardar la escena con todos sus cambios?", "Guardar MainWorld", "Dejar abierta");
            if (guardar && !EditorSceneManager.SaveScene(escena)) throw new InvalidOperationException("No se ha guardado MainWorld.");
            informe.AppendLine(guardar ? "MainWorld guardada." : "MainWorld queda abierta con sus cambios sin guardar.");
            AssetDatabase.SaveAssets();
            OrdenesDelEditor.Avisar(TituloVentanaCandela, informe.ToString());
        }
        catch (Exception error)
        {
            OrdenesDelEditor.Avisar(TituloVentanaCandela, error.Message);
            throw;
        }
        finally
        {
            if (cerrar) EditorSceneManager.CloseScene(escena, true);
        }
    }

    private static bool MontarVentanaDeCandela(Transform casa, Transform candela, StringBuilder informe)
    {
        var pared = casa.GetComponentInChildren<MeshCollider>();
        var madera = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/World/Zonas/Materiales/Mat_Zonas_CarpinteriaAzul.mat");
        if (candela == null || pared == null || madera == null)
        {
            informe.AppendLine("No se puede montar la ventana del farol: falta el punto de Candela, la malla o la madera.");
            return false;
        }

        // La colisión de la casa confirma el lado del vano tras la conversión de ejes del FBX.
        Physics.SyncTransforms();
        Vector3 direccion = casa.forward;
        var derecha = new Ray(casa.TransformPoint(new Vector3(1.2f, 3.12f, -2.1f)), direccion);
        var izquierda = new Ray(casa.TransformPoint(new Vector3(-1.2f, 3.12f, -2.1f)), direccion);
        bool cerradoDerecha = pared.Raycast(derecha, out _, 0.55f);
        bool cerradoIzquierda = pared.Raycast(izquierda, out _, 0.55f);
        if (cerradoDerecha == cerradoIzquierda)
        {
            informe.AppendLine("No se reconoce el vano en la malla de la casa del farol.");
            return false;
        }

        float centro = cerradoDerecha ? -1.2f : 1.2f;
        var ventana = Hijo(casa, "Contraventanas del farol");
        ventana.localPosition = new Vector3(centro, 3.03f, FachadaVentanaCandela - 0.025f);
        PonerHoja(ventana, madera, -1);
        PonerHoja(ventana, madera, 1);

        var marcas = Hijo(casa, "Marcas del plano y contraplano");
        var sitioWill = MarcaVentana(marcas, "CandelaVentana_Will", centro - 0.29f, 1.62f, -2.72f);
        var sitioCandela = MarcaVentana(marcas, "CandelaVentana_Candela", centro + 0.28f, 1.62f, -1.17f);
        // Los dos quedan ligeramente cruzados para que las cabezas en primer término dejen ver las caras.
        MarcaVentana(marcas, "CandelaVentana_CamaraExterior", centro + 0.64f, 3.15f, -4.23f);
        MarcaVentana(marcas, "CandelaVentana_CamaraInterior", centro - 0.56f, 2.93f, 0.15f);
        MarcaVentana(marcas, "CandelaVentana_MiraCandela", centro + 0.17f, 2.78f, -1.17f);
        MarcaVentana(marcas, "CandelaVentana_MiraWill", centro - 0.22f, 2.73f, -2.72f);
        candela.SetPositionAndRotation(sitioCandela.position, Quaternion.LookRotation(sitioWill.position - sitioCandela.position));

        informe.AppendLine($"Ventana del farol: vano en x local {centro:F2}, Candela en {candela.position:F2}.");
        return true;
    }

    private static Transform MarcaVentana(Transform padre, string nombre, float x, float y, float z)
    {
        var marca = Hijo(padre, nombre);
        marca.localPosition = new Vector3(x, y, z);
        return marca;
    }

    private static void PonerHoja(Transform ventana, Material madera, int lado)
    {
        var pivote = Hijo(ventana, lado < 0 ? "Bisagra izquierda" : "Bisagra derecha");
        pivote.localPosition = Vector3.right * (lado * 0.6f);
        var bisagra = pivote.GetComponent<HojaConBisagra>();
        if (bisagra == null) bisagra = pivote.gameObject.AddComponent<HojaConBisagra>();
        bisagra.grupo = GrupoVentanaCandela;
        bisagra.abiertaAlEmpezar = false;
        bisagra.anguloAbierta = lado * 103f;

        var hoja = pivote.Find("Hoja de madera");
        if (hoja == null)
        {
            hoja = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            hoja.name = "Hoja de madera";
            UnityEngine.Object.DestroyImmediate(hoja.GetComponent<BoxCollider>());
            hoja.SetParent(pivote, false);
        }
        hoja.localPosition = Vector3.left * (lado * 0.3f);
        hoja.localScale = new Vector3(0.597f, 1.47f, 0.065f);
        hoja.GetComponent<MeshRenderer>().sharedMaterial = madera;
    }
}
