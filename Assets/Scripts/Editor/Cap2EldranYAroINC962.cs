#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// Cap. 2 y casa de Will (INC-962). Idempotente.
/// - Eldran sale de casa en «La mañana después» y espera a un lado de la puerta de la casa de Will
///   (marca Eldran_JuntoACasaDeWill en MainWorld; nodo 12 del grafo). Se retira el nodo 25c, que lo
///   colocaba en la puerta al volver de los encargos.
/// - La casa de Will se puede abrir desde fuera: la puerta de MainWorld pasa de PortalTrigger (cambio
///   de escena entera) a InteriorPortalTrigger (carga aditiva de WillHouse) hasta WillHouse_Entrada,
///   una marca dentro de la casa a distancia de la puerta de salida.
/// - Las piezas del aro del Demonio 1 se retiran al empezar el Cap. 2 (Eldran las recoge).
public static class Cap2EldranYAroINC962
{
    const string Grafo = "Assets/NarrativeGraph/Cap2.asset";
    const string Demonio = "Assets/Prefabs/Enemy/Demon.prefab";
    const string Mundo = "Assets/Scenes/Worlds/MainWorld.unity";
    const string Casa = "Assets/Scenes/Interior/WillHouse.unity";
    const string Espera = "cap2-prepara-viaje-espera";
    const string Volver = "cap2-prepara-viaje-volver";
    const string NodoSinUso = "cap2-prepara-viaje-eldran-puerta";
    const string NodoColocar = "cap2-manana-despues-12";

    public const string MarcaEldran = "Eldran_JuntoACasaDeWill";
    const string MarcaEntrada = "WillHouse_Entrada";
    const string AnclaPuerta = "House_FrontDoor";
    const string Bedroom = "Bedroom";
    const string PuertaDeFuera = "House_FrontDoor_Teleport_To_Bedroom";
    const string RaizMarcas = "Marcas de la historia (Cap. 2)";

    /// Marca de entrada, en local del padre de Bedroom: pasado el hueco de la puerta, hacia el sur.
    /// Marca de Eldran respecto a House_FrontDoor: metros hacia fuera y hacia el lado (negativo = a la
    /// izquierda de quien sale).
    const float MarcaEldranFuera = 0.8f;
    const float MarcaEldranLado = -1.6f;
    /// La zona de la puerta llega hasta aquí por fuera (metros desde House_FrontDoor hacia la casa):
    /// lo bastante fuera para tocarla al chocar con la puerta cerrada, sin alcanzar al que sale.
    const float ZonaHastaFuera = 0.45f;
    const float ZonaFondo = 1.4f;

    static readonly Vector3 EntradaLocal = new Vector3(116.2f, 0f, 4.15f);

    [MenuItem("El Sendero/Archivo/Cap2: Eldran espera junto a casa de Will, se puede entrar en casa y el aro se recoge (INC-962)")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            OrdenesDelEditor.Avisar("INC-962", "Sal del modo Play antes de ejecutarlo.");
            return;
        }
        var informe = new List<string>();
        informe.Add(MarcarPiezasDelAro());
        informe.Add(MontarCasa());
        informe.Add(MontarMundo());
        informe.Add(AjustarGrafo());
        string texto = string.Join(" ", informe);
        Debug.Log("[INC-962] " + texto);
        OrdenesDelEditor.Avisar("INC-962", texto);
    }

    static string AjustarGrafo()
    {
        var g = AssetDatabase.LoadAssetAtPath<NarrativeGraph>(Grafo);
        if (g == null) return "No encuentro Cap2.asset.";
        var espera = g.FindNode(Espera);
        if (espera != null)
        {
            int i = espera.outputs.IndexOf(NodoSinUso);
            if (i >= 0) espera.outputs[i] = Volver;
            if (!espera.outputs.Contains(Volver)) espera.outputs.Add(Volver);
        }
        g.nodes.RemoveAll(n => n != null && n.guid == NodoSinUso);
        foreach (var n in g.nodes) n?.outputs?.RemoveAll(x => x == NodoSinUso);

        if (g.FindNode(NodoColocar) is PlaceActorNode colocar)
        {
            colocar.markId = MarcaEldran;
            colocar.showActor = true;
            colocar.displayTitle = "12.- Eldran espera junto a la casa de Will (sale en la cinemática)";
        }
        EditorUtility.SetDirty(g);
        AssetDatabase.SaveAssets();
        return "Grafo: nodo 12 → " + MarcaEldran + "; 25c retirado (25b → 25d).";
    }

    static string MontarCasa()
    {
        var casa = SceneManager.GetSceneByPath(Casa);
        bool abierta = casa.isLoaded;
        if (abierta && casa.isDirty) return "WillHouse tiene cambios sin guardar: no la toco.";
        if (!abierta) casa = EditorSceneManager.OpenScene(Casa, OpenSceneMode.Additive);
        try
        {
            SpawnAnchor bedroom = null, entrada = null;
            foreach (var r in casa.GetRootGameObjects())
                foreach (var a in r.GetComponentsInChildren<SpawnAnchor>(true))
                {
                    if (a.anchorId == Bedroom) bedroom = a;
                    if (a.anchorId == MarcaEntrada) entrada = a;
                }
            if (bedroom == null) return "No encuentro Bedroom en WillHouse.";
            if (entrada == null)
            {
                var go = new GameObject(MarcaEntrada);
                SceneManager.MoveGameObjectToScene(go, casa);
                go.transform.SetParent(bedroom.transform.parent, false);
                entrada = go.AddComponent<SpawnAnchor>();
                entrada.anchorId = MarcaEntrada;
            }
            entrada.transform.localPosition = EntradaLocal;
            entrada.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            EditorSceneManager.MarkSceneDirty(casa);
            EditorSceneManager.SaveScene(casa);
            return $"WillHouse: {MarcaEntrada} en {entrada.transform.position}.";
        }
        finally
        {
            if (!abierta) EditorSceneManager.CloseScene(casa, true);
        }
    }

    static string MontarMundo()
    {
        var mundo = SceneManager.GetSceneByPath(Mundo);
        if (!mundo.isLoaded) return "MainWorld no está abierta: no toco la puerta ni la marca de Eldran.";
        bool estabaSucia = mundo.isDirty;

        SpawnAnchor puerta = null, marca = null;
        Transform portal = null, raiz = null;
        foreach (var r in mundo.GetRootGameObjects())
        {
            if (r.name == RaizMarcas) raiz = r.transform;
            foreach (var a in r.GetComponentsInChildren<SpawnAnchor>(true))
            {
                if (a.anchorId == AnclaPuerta) puerta = a;
                if (a.anchorId == MarcaEldran) marca = a;
            }
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == PuertaDeFuera) portal = t;
        }
        if (puerta == null || portal == null) return "No encuentro House_FrontDoor o la puerta de fuera en MainWorld.";

        // La puerta: carga aditiva del interior.
        var viejo = portal.GetComponent<PortalTrigger>();
        if (viejo) Object.DestroyImmediate(viejo, true);
        var entrar = portal.GetComponent<InteriorPortalTrigger>() ?? portal.gameObject.AddComponent<InteriorPortalTrigger>();
        entrar.sceneToLoad = "WillHouse";
        entrar.sceneToUnload = "";
        entrar.targetAnchorId = MarcaEntrada;
        entrar.singleUse = false;
        AjustarZonaDeLaPuerta(portal, puerta.transform.position);

        // La marca de Eldran: a un lado de la puerta, en el NavMesh, mirando a por donde sale Will.
        if (raiz == null)
        {
            raiz = new GameObject(RaizMarcas).transform;
            SceneManager.MoveGameObjectToScene(raiz.gameObject, mundo);
        }
        if (marca == null)
        {
            var go = new GameObject(MarcaEldran);
            go.transform.SetParent(raiz, false);
            marca = go.AddComponent<SpawnAnchor>();
            marca.anchorId = MarcaEldran;
        }
        Vector3 p = puerta.transform.position;
        Vector3 fuera = Vector3.ProjectOnPlane(puerta.transform.forward, Vector3.up).normalized;
        Vector3 lado = Vector3.Cross(Vector3.up, fuera);
        // A la izquierda de quien sale (entre la puerta y la farola), donde no estorba el camino.
        Vector3 deseado = p + fuera * MarcaEldranFuera + lado * MarcaEldranLado;
        bool hay = NavMesh.SamplePosition(deseado, out var hit, 1.0f, NavMesh.AllAreas);
        Vector3 elegido = hay ? hit.position : new Vector3(deseado.x, p.y, deseado.z);
        marca.transform.position = elegido;
        Vector3 mira = Vector3.ProjectOnPlane(p + fuera * 1.5f - elegido, Vector3.up);
        if (mira.sqrMagnitude > 1e-4f) marca.transform.rotation = Quaternion.LookRotation(mira.normalized, Vector3.up);

        EditorSceneManager.MarkSceneDirty(mundo);
        string guardado = estabaSucia ? "MainWorld ya tenía cambios de otros hilos: queda sin guardar (guárdala a mano)." : (EditorSceneManager.SaveScene(mundo) ? "MainWorld guardada." : "No se pudo guardar MainWorld.");
        return $"Puerta de la casa: entra en WillHouse ({MarcaEntrada}). {MarcaEldran} en {elegido}{(hay ? "" : " (fuera del NavMesh)")}. Zona de la puerta: {portal.GetComponent<Collider>()?.bounds}. {guardado}";
    }

    /// La puerta cerrada (Door04) tiene colisión: la zona tiene que asomar por delante de ella para
    /// que el jugador la toque, pero sin llegar a House_FrontDoor, donde aparece al salir.
    static void AjustarZonaDeLaPuerta(Transform portal, Vector3 ancla)
    {
        var caja = portal.GetComponent<BoxCollider>();
        if (caja == null) return;
        caja.isTrigger = true;
        var b = caja.bounds;
        Vector3 fuera = (ancla - b.center); fuera.y = 0f;
        // Eje horizontal dominante hacia fuera (la puerta está alineada con los ejes del mundo).
        bool ejeX = Mathf.Abs(fuera.x) >= Mathf.Abs(fuera.z);
        float signo = ejeX ? Mathf.Sign(fuera.x) : Mathf.Sign(fuera.z);
        float borde = (ejeX ? ancla.x : ancla.z) - signo * ZonaHastaFuera;
        float fondo = borde - signo * ZonaFondo;
        Vector3 centro = b.center, tam = b.size;
        if (ejeX) { centro.x = (borde + fondo) * 0.5f; tam.x = ZonaFondo; }
        else { centro.z = (borde + fondo) * 0.5f; tam.z = ZonaFondo; }
        caja.center = portal.InverseTransformPoint(centro);
        Vector3 local = portal.InverseTransformVector(tam);
        caja.size = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
    }

    static string MarcarPiezasDelAro()
    {
        var raiz = PrefabUtility.LoadPrefabContents(Demonio);
        try
        {
            var piezas = raiz.GetComponentsInChildren<PiezaQueCae>(true);
            foreach (var p in piezas) p.RetirarConSenales(AroDeRunasEnElCuello.SenalesQueRecogenElAro);
            if (piezas.Length > 0) PrefabUtility.SaveAsPrefabAsset(raiz, Demonio);
            return $"Aro del demonio: {piezas.Length} piezas se retiran al empezar el Cap. 2.";
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }
}
#endif
