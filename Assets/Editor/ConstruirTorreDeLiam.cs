#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// Construye el decorado y sus datos con los sistemas de escena y secuencia oficiales.
public static class ConstruirTorreDeLiam
{
    const string Escena = "Assets/Scenes/Interior/TorreDeLiam.unity";
    const string Secuencia = "Assets/_SEQUENCES/SEQ_LosPlanesDeLiam.asset";
    const string Pack = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/";
    static readonly Vector3 Origen = new(-6000, 100, 6000);
    const float AlturaMesa = 0.4f;
    const string MusicaDeLaTorre = "Assets/Audio/Music/Pesadilla en Suspenso.mp3";
    static Transform raiz;

    [MenuItem("El Sendero/Mundo/Construir torre de Liam")]
    public static void Construir()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("La construcción requiere salir de Play.");
        var anterior = SceneManager.GetActiveScene();
        var escena = SceneManager.GetSceneByPath(Escena);
        if (!escena.IsValid()) escena = File.Exists(Escena)
            ? EditorSceneManager.OpenScene(Escena, OpenSceneMode.Additive)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        if (escena.isDirty) throw new InvalidOperationException("La torre tiene cambios sin guardar.");
        SceneManager.SetActiveScene(escena);
        try
        {
            foreach (var go in escena.GetRootGameObjects())
                if (go.name == "TorreDeLiam_Decorado") UnityEngine.Object.DestroyImmediate(go);
            raiz = new GameObject("TorreDeLiam_Decorado").transform;
            raiz.position = Origen;
            var madera = Material("Assets/Art/World/LWRP Floating Islands/Materials/Brown Wood.mat");
            RenderSettings.skybox=null; RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(0.12f,0.16f,0.24f);
            RenderSettings.ambientIntensity=0.4f;
            Prop("Cuarto de piedra y madera", Pack+"Interior/Room/Room01_a01.prefab", Vector3.zero, new(7.4f,3.4f,6.4f));
            // El collider de apoyo no añade obstáculos a los encuadres.
            var suelo=Nuevo("Apoyo del suelo");suelo.transform.localPosition=new(0,-0.1f,0);
            suelo.AddComponent<BoxCollider>().size=new(7.2f,0.2f,6.2f);
            for (int i=-1;i<=1;i++) Caja("Viga", new(i*3.2f,3.25f,0), new(0.18f,0.24f,6.4f), madera);
            Prop("Cama", Pack+"Props/Furniture/Bed/Bed01_a01.prefab", new(-2.5f,0,1.3f), new(1.3f,0.8f,2.1f));
            // Muebles a escala de los personajes (chibi): con la mesa a 0,75 m Liam sentado no asomaba por encima.
            var mesa = Prop("Mesa", Pack+"Props/Furniture/Table/Table01_a01.prefab", new(0,0,-0.1f), new(2.2f,AlturaMesa,1.1f));
            Prop("Silla", Pack+"Props/Furniture/Chair/Chair01_a01.prefab", new(0,0,0.95f), new(0.5f,0.6f,0.5f),new(0,180,0));
            // El módulo de habitación no tiene pared del fondo: se cierra detrás del mapa y del espejo.
            Caja("Pared del fondo", new(0,1.7f,3.08f), new(7.4f,3.4f,0.1f), madera);
            var bola = Prop("PROP_Bola", Pack+"Props/Goods/Crystalball02_b01.prefab", new(0.4f,AlturaMesa+0.01f,-0.15f), new(0.42f,0.4f,0.42f));
            var esfera=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera.name="Cristal emisivo";esfera.transform.SetParent(raiz,false);
            esfera.transform.localPosition=new(0.4f,AlturaMesa+0.27f,-0.15f);esfera.transform.localScale=Vector3.one*0.3f;
            esfera.GetComponent<Renderer>().sharedMaterial=Material("Assets/VFX/G-spot_Lab/Magic Energy Seamless Textures Free Pack/MagicEnergy_Purple_01/MagicEnergy_Purple_01.mat");
            UnityEngine.Object.DestroyImmediate(esfera.GetComponent<Collider>());
            var llave = Prop("PROP_Llave", "Assets/Art/Items/BTM_Items_Gems/Prefabs/Key.prefab", new(-0.5f,AlturaMesa+0.01f,-0.35f), new(0.32f,0.04f,0.13f));
            foreach(var animacion in llave.GetComponentsInChildren<Benjathemaker.SimpleGemsAnim>())UnityEngine.Object.DestroyImmediate(animacion);
            Prop("Vela", "Assets/Art/World/Modular Castle/Assets/prefabs/candle1.prefab", new(-0.95f,AlturaMesa+0.01f,0.25f), new(0.12f,0.26f,0.12f));
            var fria = Luz("Pulso del cristal", new(0.4f,AlturaMesa+0.21f,0.05f), new(0.48f,0.42f,0.8f), 0.5f, 2.3f);
            var flicker = fria.gameObject.AddComponent<WarmLightFlicker>();
            var f = new SerializedObject(flicker);
            f.FindProperty("preset").enumValueIndex=0;
            f.FindProperty("warmColor").colorValue=fria.color;
            f.FindProperty("baseIntensity").floatValue=0.5f;
            f.FindProperty("lightRange").floatValue=2.3f;
            f.FindProperty("intensityVariation").floatValue=0.12f;
            f.FindProperty("flickerSpeed").floatValue=0.5f;
            f.FindProperty("enableColorVariation").boolValue=false;
            f.ApplyModifiedPropertiesWithoutUndo();
            var calida = Luz("Luz de vela", new(-0.95f,AlturaMesa+0.29f,0.25f), new(1,0.84f,0.65f), 0.12f, 0.85f);
            var brillo = Luz("PROP_BrilloLlave", new(-0.5f,AlturaMesa+0.08f,-0.35f), new(0.6f,0.65f,0.85f), 0.06f, 0.38f);
            var luna = Luz("Relleno de noche",new(0,3,0),new(0.55f,0.65f,0.85f),0.32f,10);
            luna.type=LightType.Directional;luna.transform.localRotation=Quaternion.Euler(35,155,0);
            // La textura cartográfica existente se monta como lámina para conservar su lectura con luz nocturna.
            var mapa = Nuevo("PROP_Mapa");mapa.transform.localPosition=new(-0.6f,2,2.86f);
            var lienzo=mapa.AddComponent<Canvas>();lienzo.renderMode=RenderMode.WorldSpace;
            var rect=mapa.GetComponent<RectTransform>();rect.sizeDelta=new(240,170);rect.localScale=Vector3.one*0.01f;
            var papel=Lamina(rect,"Papel del mapa",Vector2.zero,new(250,180),new Color(0.55f,0.48f,0.36f));
            var imagen=new GameObject("Cartografía existente",typeof(RectTransform),typeof(RawImage));imagen.transform.SetParent(rect,false);
            imagen.GetComponent<RectTransform>().sizeDelta=new(240,170);
            imagen.GetComponent<RawImage>().texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Scenes/Worlds/MainWorld_data/Vista_mapa.png");
            imagen.GetComponent<RawImage>().color=new Color(0.58f,0.54f,0.43f);imagen.GetComponent<RawImage>().raycastTarget=false;
            Vector3[] puntos = {new(-1.55f,1.55f,2.85f),new(-0.95f,2.5f,2.85f),new(0.3f,1.7f,2.85f),new(-1.6f,2.35f,2.85f),new(0.15f,2.5f,2.85f),new(-0.4f,1.3f,2.85f)};
            for (int i=0;i<puntos.Length;i++)
            {
                var a=new Vector2((puntos[i].x+0.6f)*100,(puntos[i].y-2)*100);
                int destino=i==puntos.Length-1?2:(i+1)%5;
                var b=new Vector2((puntos[destino].x+0.6f)*100,(puntos[destino].y-2)*100);
                var hilo=Lamina(rect,"Hilo rojo",(a+b)*0.5f,new(Vector2.Distance(a,b),2.2f),new Color(0.65f,0.035f,0.045f));
                hilo.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
                Lamina(rect,i==puntos.Length-1?"Chincheta nueva - pueblo de Will":"Chincheta",a,Vector2.one*(i==puntos.Length-1?8:5.5f),new Color(0.8f,0.065f,0.05f));
            }
            var metal=Material("Assets/Material & Textures/Black.mat");
            foreach(var r in llave.GetComponentsInChildren<Renderer>())r.sharedMaterial=metal;
            var espejo = Caja("PROP_Espejo",new(2,1.75f,2.95f),new(0.8f,1.3f,0.04f),metal);
            Caja("Marco del espejo",new(2,1.75f,3),new(0.98f,1.48f,0.06f),madera);
            var spawn = Nuevo("SpawnPoint_Liam_Torre"); spawn.transform.localPosition=new(0,0,0.9f);spawn.transform.localRotation=Quaternion.Euler(0,180,0);
            spawn.AddComponent<NpcSpawnPoint>().spawnId="SPAWN_LIAM_TORRE";
            var roster = Asset<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_TorreDeLiam.asset");
            roster.rosterId="TorreDeLiam";
            roster.entries=new(){new NpcRosterSO.Entry{spawnId="SPAWN_LIAM_TORRE",prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("de7b566c44b3b6949964c333227535b2")),gameObjectName="Liam_Torre",persistenceId="LIAM_TORRE",startActive=true,requireNavMesh=false}};
            EditorUtility.SetDirty(roster);
            var def = Asset<SequenceDefinition>(Secuencia);
            def.displayName="Los planes de Liam"; def.summary="Liam observa el combate, ve romperse el aro y decide conducir a Will hasta el Sendero.";
            def.signalIn="LIAM_CRYSTAL_START"; def.signalOut="LIAM_CRYSTAL_DONE"; def.musicId="LIAM_CRYSTAL";
            def.presentacionDeTexto=PresentacionDeTexto.Subtitulo;
            EditorUtility.SetDirty(def);
            var escenario=Nuevo("Secuencia Los planes de Liam");
            var driver=escenario.AddComponent<CinematicCameraDriver>();
            var stage=escenario.AddComponent<SequenceStage>();
            var so=new SerializedObject(stage);
            so.FindProperty("_cameraDriver").objectReferenceValue=driver;
            var props=so.FindProperty("_props"); props.arraySize=6;
            Transform[] objetivos={bola.transform,llave.transform,brillo.transform,mapa.transform,espejo.transform,mesa.transform};
            string[] ids={"PROP_Bola","PROP_Llave","PROP_BrilloLlave","PROP_Mapa","PROP_Espejo","PROP_Mesa"};
            for(int i=0;i<ids.Length;i++){var p=props.GetArrayElementAtIndex(i);p.FindPropertyRelative("id").stringValue=ids[i];p.FindPropertyRelative("target").objectReferenceValue=objetivos[i];p.FindPropertyRelative("eyeHeight").floatValue=i==0?0.3f:0;}
            so.ApplyModifiedPropertiesWithoutUndo();
            var env=escenario.AddComponent<AnchorEnvironment>(); env.zoneRoot=raiz.gameObject; env.hideExteriorWorld=true;
            foreach(var l in new[]{fria,calida,brillo,luna})l.transform.SetParent(escenario.transform,true);
            env.lightsEnableOnEnter=new[]{fria,calida,brillo,luna}; env.adjustCameraClipping=true; env.interiorFarClipPlane=25;
            var volumen=escenario.AddComponent<Volume>();volumen.isGlobal=true;volumen.priority=100;
            var postproceso=escenario.AddComponent<PostprocesoDeEscena>();postproceso.entrada=0;postproceso.exclusivo=true;
            var player=escenario.AddComponent<SequencePlayer>(); var sp=new SerializedObject(player);
            sp.FindProperty("_definition").objectReferenceValue=def; sp.FindProperty("_stage").objectReferenceValue=stage;
            sp.FindProperty("_interiorAnchor").objectReferenceValue=env;
            var transicion=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath("013263c83b6a84c4d95beaa8bd163ac8"));
            sp.FindProperty("_entryTransition").objectReferenceValue=transicion;
            sp.FindProperty("_exitTransition").objectReferenceValue=transicion;
            sp.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(escena); EditorSceneManager.SaveScene(escena,Escena);
            var builds=EditorBuildSettings.scenes.ToList(); if(!builds.Any(b=>b.path==Escena)) builds.Add(new EditorBuildSettingsScene(Escena,true));
            EditorBuildSettings.scenes=builds.ToArray(); AssetDatabase.SaveAssets();
        }
        finally {SceneManager.SetActiveScene(anterior);}
    }

    [MenuItem("El Sendero/Archivo/Liam/Enlazar torre y condicionar roster")]
    public static void Enlazar()
    {
        // Cap. 1 termina al ganar al Demonio: se completa su misión y se pasa el testigo al Cap. 2.
        var cap1=AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap1.asset");
        var batalla=cap1.FindNodeByTitle("32.- ARENA DE BATALLA: DEMONIO")??throw new InvalidOperationException("Falta la batalla del Demonio en Cap1.");
        var completar=cap1.nodes.OfType<CompleteQuestStepsNode>().Single(n=>n.questId=="ELDRAN_MISSION3");
        var finCap1=cap1.nodes.OfType<RaiseCustomEventNode>().Single(n=>n.eventKey=="CH_Cap1_BACK_1");
        var modelo=cap1.FindNodeByTitle("4.- Se destapa la habitacion de Will (Easy Transitions)") as ScreenFadeNode
            ??throw new InvalidOperationException("Falta el destape de la habitación en Cap1.");
        QuitarTramoDeLiam(cap1);
        completar.displayTitle="33.- Demonio derrotado: completa ELDRAN_MISSION3";
        batalla.outputs=new(){completar.guid};
        completar.outputs=new(){finCap1.guid};
        completar.position=batalla.position+new Vector2(380,0);
        finCap1.position=completar.position+new Vector2(380,0);
        EditorUtility.SetDirty(cap1);

        // Cap. 2 abre con Liam en su torre, después del checkpoint de inicio.
        var cap2=AssetDatabase.LoadAssetAtPath<NarrativeGraph>("Assets/NarrativeGraph/Cap2.asset");
        var checkpoint=cap2.nodes.OfType<CheckpointNode>().Single();
        QuitarTramoDeLiam(cap2);
        var p=checkpoint.position;
        var carga=Anadir(cap2,new AdditiveSceneNode{displayTitle="3.- Carga la torre de Liam (TorreDeLiam)",sceneName="TorreDeLiam",operacion=AdditiveSceneNode.Operacion.Cargar,waitForCompletion=true,blockSaving=true},p+new Vector2(380,0));
        var lanza=Anadir(cap2,new RaiseCustomEventNode{displayTitle="4.- Los planes de Liam (LIAM_CRYSTAL_START)",eventKey="LIAM_CRYSTAL_START",blockSaving=true},p+new Vector2(760,0));
        var espera=Anadir(cap2,new WaitCustomEventNode{displayTitle="5.- Espera fin de la escena de Liam (LIAM_CRYSTAL_DONE)",eventKey="LIAM_CRYSTAL_DONE",blockSaving=true},p+new Vector2(1140,0));
        var descarga=Anadir(cap2,new AdditiveSceneNode{displayTitle="6.- Descarga la torre de Liam",sceneName="TorreDeLiam",operacion=AdditiveSceneNode.Operacion.Descargar,waitForCompletion=true,blockSaving=true},p+new Vector2(1520,0));
        // La secuencia acaba en negro: se destapa el mundo con el mismo telón que tras el prólogo y vuelve la música del lugar.
        var destape=Anadir(cap2,new ScreenFadeNode{displayTitle="7.- Se destapa el mundo (vuelve la musica del lugar)",color=modelo.color,duration=modelo.duration,fadeIn=modelo.fadeIn,transicion=modelo.transicion,restaurarMusicaDeEscena=true,fundidoDeMusica=modelo.fundidoDeMusica},p+new Vector2(1900,0));
        checkpoint.outputs=new(){carga.guid};
        carga.outputs=new(){lanza.guid}; lanza.outputs=new(){espera.guid}; espera.outputs=new(){descarga.guid}; descarga.outputs=new(){destape.guid}; destape.outputs=new();
        EditorUtility.SetDirty(cap2);

        // Música de la secuencia (regla LIAM_CRYSTAL del perfil de audio).
        var perfil=AssetDatabase.LoadAssetAtPath<AudioGraphProfile>("Assets/_AUDIOPROFILE/AudioGraphProfile.asset");
        var regla=perfil.GetSequenceRule("LIAM_CRYSTAL")??throw new InvalidOperationException("Falta la regla LIAM_CRYSTAL en el perfil de audio.");
        regla.music=AssetDatabase.LoadAssetAtPath<AudioClip>(MusicaDeLaTorre)??throw new InvalidOperationException("Falta la música: "+MusicaDeLaTorre);
        EditorUtility.SetDirty(perfil);
        var roster=AssetDatabase.LoadAssetAtPath<NpcRosterSO>("Assets/Resources/NpcRosters/NpcRoster_MainWorld.asset");
        foreach(var e in roster.entries)
        {
            if(e.spawnId=="SPAWN__LIAM"){e.startActive=false;e.requiredFlag="LIAM_EN_GRUPO";}
            if(e.gameObjectName=="_ESTELA"){e.startActive=false;e.requiredFlag="ESTELA_EN_GRUPO";}
        }
        EditorUtility.SetDirty(roster); AssetDatabase.SaveAssets();
    }
    [MenuItem("El Sendero/Archivo/Liam/Actualizar flags de presets posteriores")]
    public static void ActualizarPresets()
    {
        string[] nombres={"Arrestados_Antes_Demonio2","Arrestados_Antes_Rey","Arrestados_Despues_Demonio2","Final_Primera_Parte"};
        foreach(var nombre in nombres)
        {
            var preset=AssetDatabase.LoadAssetAtPath<PlayerPresetSO>($"Assets/Scripts/Player/SO/PlayerPreset_{nombre}.asset");
            if(preset==null)throw new InvalidOperationException("Falta preset: "+nombre);
            if(!preset.partyMemberIds.Contains("_LIAM"))throw new InvalidOperationException("El preset no incluye a Liam: "+nombre);
            if(!preset.flags.Contains("LIAM_EN_GRUPO")){preset.flags.Add("LIAM_EN_GRUPO");EditorUtility.SetDirty(preset);}
        }
        AssetDatabase.SaveAssets();
    }
    /// Quita del grafo los nodos de la escena de Liam (carga/descarga de la torre, sus señales y el destape posterior).
    static void QuitarTramoDeLiam(NarrativeGraph g)
    {
        var fuera=g.nodes.Where(n=>
            (n is AdditiveSceneNode a&&a.sceneName=="TorreDeLiam")||
            (n is RaiseCustomEventNode r&&r.eventKey=="LIAM_CRYSTAL_START")||
            (n is WaitCustomEventNode w&&w.eventKey=="LIAM_CRYSTAL_DONE")||
            (n is ScreenFadeNode f&&f.displayTitle!=null&&f.displayTitle.Contains("Se destapa el mundo"))).ToList();
        var guids=new HashSet<string>(fuera.Select(n=>n.guid));
        foreach(var n in fuera) g.nodes.Remove(n);
        foreach(var n in g.nodes) n.outputs.RemoveAll(guids.Contains);
    }
    static T Anadir<T>(NarrativeGraph g,T nodo,Vector2 posicion) where T:NarrativeNode
    {nodo.chapter="Cap. 2";nodo.position=posicion;g.nodes.Add(nodo);return nodo;}
    static T Asset<T>(string path) where T:ScriptableObject
    {var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a==null){a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);}return a;}
    static Material Material(string path)=>AssetDatabase.LoadAssetAtPath<Material>(path)??throw new InvalidOperationException("Falta material: "+path);
    static RectTransform Lamina(Transform padre,string nombre,Vector2 pos,Vector2 tamano,Color color)
    {
        var go=new GameObject(nombre,typeof(RectTransform),typeof(Image));go.transform.SetParent(padre,false);
        var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=pos;rect.sizeDelta=tamano;
        var imagen=go.GetComponent<Image>();imagen.color=color;imagen.raycastTarget=false;return rect;
    }
    static GameObject Nuevo(string nombre){var go=new GameObject(nombre);go.transform.SetParent(raiz,false);return go;}
    static GameObject Caja(string nombre,Vector3 pos,Vector3 escala,Material mat,bool col=false)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=nombre;go.transform.SetParent(raiz,false);go.transform.localPosition=pos;go.transform.localScale=escala;go.GetComponent<Renderer>().sharedMaterial=mat;if(!col)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
    static GameObject Prop(string nombre,string path,Vector3 pos,Vector3 tamano,Vector3 giro=default)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path)??throw new InvalidOperationException("Falta prop: "+path);
        var go=Nuevo(nombre);
        var modelo=(GameObject)PrefabUtility.InstantiatePrefab(prefab,go.transform);modelo.transform.localPosition=Vector3.zero;modelo.transform.localRotation=Quaternion.Euler(giro);
        var rr=go.GetComponentsInChildren<Renderer>();var b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);
        var s=b.size;go.transform.localScale=new(tamano.x/Mathf.Max(s.x,0.001f),tamano.y/Mathf.Max(s.y,0.001f),tamano.z/Mathf.Max(s.z,0.001f));
        b=rr[0].bounds;foreach(var r in rr)b.Encapsulate(r.bounds);
        go.transform.position+=Origen+pos-new Vector3(b.center.x,b.min.y,b.center.z);
        foreach(var c in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);
        return go;
    }
    static Light Luz(string nombre,Vector3 pos,Color color,float intensidad,float alcance)
    {var go=Nuevo(nombre);go.transform.localPosition=pos;var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=intensidad;l.range=alcance;return l;}
}
#endif
