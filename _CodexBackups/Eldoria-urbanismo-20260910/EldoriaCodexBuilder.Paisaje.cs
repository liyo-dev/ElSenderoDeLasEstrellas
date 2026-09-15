using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static partial class EldoriaCodexBuilder
{
    const string SolicitudPaisaje="Temp/EldoriaPaisaje-pendiente.txt";

    [InitializeOnLoadMethod]
    static void RevisarSolicitudPaisaje()
    {
        // Ejecución única solicitada durante la edición. Sin archivo de solicitud no hace nada.
        // Nunca entra en Play, guarda escenas del usuario ni modifica una maqueta existente.
        if(File.Exists(SolicitudPaisaje))EditorApplication.update+=EjecutarSolicitudPaisaje;
    }

    static void EjecutarSolicitudPaisaje()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        EditorApplication.update-=EjecutarSolicitudPaisaje;
        if(!File.Exists(SolicitudPaisaje))return;
        File.Delete(SolicitudPaisaje);
        try { Crear(); }
        catch(Exception e)
        {
            File.WriteAllText("Temp/EldoriaPaisaje-error.txt",e.ToString());
            Debug.LogException(e);
        }
    }

    // Revisión paisajística: todos los materiales y mallas se guardan en la variante nueva.
    // No se modifican los prefabs, materiales ni escenas originales.
    static float PesoBosque(float x,float z)
    {
        float dx=(x+310)/175, dz=(z-55)/215;
        float borde=Mathf.Sqrt(dx*dx+dz*dz);
        borde+=(Mathf.PerlinNoise(x*.014f+13,z*.014f+29)-.5f)*.38f;
        float bosque=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.12f,borde));
        // Revisión 16 (Claude): segundo bosque — el del Fuego Fatuo, entre el Reino (montaña) y el pueblo vecino
        // (playa este), en la franja oriental. Más pequeño y con borde más nervioso.
        float ex=(x-330)/95, ez=(z-40)/120;
        float borde2=Mathf.Sqrt(ex*ex+ez*ez)+(Mathf.PerlinNoise(x*.02f+71,z*.02f+5)-.5f)*.5f;
        return Mathf.Max(bosque,(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1.1f,borde2)))*.9f);
    }

    static float RelieveLomas(float x,float z)
    {
        // Contrafuertes al oeste y en el sur; los corredores y plataformas se resuelven después.
        float oeste=26*Mathf.Exp(-((x+345)*(x+345)/17000+(z-170)*(z-170)/27000));
        float sur=17*Mathf.Exp(-((x+95)*(x+95)/36000+(z+375)*(z+375)/6500));
        float este=20*Mathf.Exp(-((x-365)*(x-365)/9000+(z-10)*(z-10)/19000));
        float masa=oeste+sur+este;
        float escalon=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,18,masa))*7;
        return masa+escalon;
    }

    static float RelieveIslote(float x,float z,Vector4 islote)
    {
        // Dos cabezas desiguales, plataforma superior y faldón rocoso: evita el cono repetido.
        float angulo=islote.x*.017f+islote.y*.009f;
        float dx=x-islote.x, dz=z-islote.y;
        float u=(dx*Mathf.Cos(angulo)+dz*Mathf.Sin(angulo))/islote.z;
        float v=(-dx*Mathf.Sin(angulo)+dz*Mathf.Cos(angulo))/(islote.z*.7f);
        float r=Mathf.Sqrt(u*u+v*v);
        r+=(Mathf.PerlinNoise(x*.065f+33,z*.065f+14)-.5f)*.12f;
        float baseRoca=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,1,r));
        float cabeza=Mathf.Max(Pico(x,z,islote.x-islote.z*.15f,islote.y,islote.z*.65f,islote.w*.25f),
            Pico(x,z,islote.x+islote.z*.25f,islote.y+islote.z*.13f,islote.z*.4f,islote.w*.33f));
        return -12+baseRoca*islote.w*.62f+cabeza;
    }

    static void PrepararArroyo()
    {
        // Se mide la ladera antes de instalar el cauce, sin recursión en Altura.
        arroyoSuave=null;
        var puntos=Suavizar(new[]{new Vector3(150,0,330),new Vector3(165,0,300),CascadaCentro,
            new Vector3(190,0,252),new Vector3(237,0,218),new Vector3(289,0,188),new Vector3(400,0,180),new Vector3(470,0,160)});
        float anterior=float.PositiveInfinity;
        for(int i=0;i<puntos.Length;i++)
        {
            // Una corriente nunca sube aguas abajo. El salto tallado determina la cascada.
            puntos[i].y=Mathf.Max(0,Mathf.Min(anterior,Altura(puntos[i].x,puntos[i].z)+.25f));
            anterior=puntos[i].y;
        }
        puntos[puntos.Length-1].y=0;
        arroyoSuave=puntos;
    }

    static Material AguaPaisaje(Material origen,bool fluvial)
    {
        // Revisión 11 (Claude): Raúl pidió el agua de MainWorld (material `Water`, ithappy/WaterURP). Si `origen` es
        // ese material se copia y se ajusta; el shader propio de Codex ("Agua de maqueta") queda solo como reserva
        // para cuando no se encuentre el de MainWorld.
        bool deMainWorld=origen!=null&&origen.shader!=null&&origen.shader.name.Contains("WaterURP");
        var shader=deMainWorld?null:Shader.Find("El Sendero/Eldoria/Agua de maqueta");
        var copia=shader!=null?new Material(shader):new Material(origen);
        copia.name=fluvial?"Agua de río — espuma contenida":"Mar — costa turquesa";
        if(shader!=null)
        {
            copia.SetTexture("_Fondo",AssetDatabase.LoadAssetAtPath<Texture2D>(carpeta+"/Batimetria.asset"));
            copia.SetColor("_Claro",new Color(.13f,.49f,.43f));
            copia.SetColor("_Profundo",new Color(.055f,.24f,.32f));
            copia.SetFloat("_Espuma",fluvial?0:.23f);
        }
        void Numero(string nombre,float valor) { if(copia.HasProperty(nombre))copia.SetFloat(nombre,valor); }
        void ColorAgua(string nombre,Color valor) { if(copia.HasProperty(nombre))copia.SetColor(nombre,valor); }
        Numero("_FoamAmount",fluvial?.035f:.18f);
        Numero("_FoamCutoff",.32f);
        Numero("_IsFoam",fluvial?0:1);
        Numero("_SurfaceOpacity",fluvial?.06f:.16f);
        Numero("_NormalStrength",fluvial?.12f:.22f);
        Numero("_Depth",fluvial?2.5f:12);
        Numero("_AmbientFresnel",1.2f);
        ColorAgua("_ColorShallow",new Color(.18f,.52f,.49f));
        ColorAgua("_ColorDeep",fluvial?new Color(.08f,.32f,.35f):new Color(.065f,.26f,.36f));
        ColorAgua("_ColorAmbient",new Color(.31f,.54f,.59f));
        AssetDatabase.CreateAsset(copia,carpeta+(fluvial?"/AguaRio.mat":"/AguaMar.mat"));
        return copia;
    }

    static readonly Dictionary<(Material,bool),Material> materialesVegetacion=new Dictionary<(Material,bool),Material>();
    static void MatizarArbol(GameObject objeto)
    {
        foreach(var renderer in objeto.GetComponentsInChildren<MeshRenderer>())
        {
            var materiales=renderer.sharedMaterials;
            for(int i=0;i<materiales.Length;i++)
            {
                var original=materiales[i]; if(original==null)continue;
                bool umbrio=PesoBosque(objeto.transform.position.x,objeto.transform.position.z)>.4f;
                var clave=(original,umbrio);
                Material copia;
                if(!materialesVegetacion.TryGetValue(clave,out copia))
                {
                    copia=new Material(original){name=original.name+" — Eldoria"};
                    string propiedad=copia.HasProperty("_BaseColor")?"_BaseColor":copia.HasProperty("_Color")?"_Color":null;
                    if(propiedad!=null)copia.SetColor(propiedad,copia.GetColor(propiedad)*new Color(.72f,.82f,.72f,1));
                    if(copia.HasProperty("_DetailMapImpact"))
                    {
                        var tinte=new Texture2D(1,1){name="Tinte de follaje"};
                        tinte.SetPixel(0,0,umbrio?new Color(.13f,.23f,.16f,1):new Color(.24f,.36f,.20f,1));tinte.Apply();
                        AssetDatabase.CreateAsset(tinte,carpeta+"/TinteFollaje_"+materialesVegetacion.Count+".asset");
                        copia.SetTexture("_DetailMap",tinte);
                        copia.SetColor("_DetailMapColor",Color.white);
                        copia.SetFloat("_DetailMapImpact",umbrio?.62f:.38f);
                        copia.SetFloat("_DetailMapBlendingMode",2);
                        copia.DisableKeyword("_DETAILMAPBLENDINGMODE_MULTIPLY");
                        copia.DisableKeyword("_DETAILMAPBLENDINGMODE_ADD");
                        copia.EnableKeyword("_DETAILMAPBLENDINGMODE_INTERPOLATE");
                        copia.SetFloat("_LightContribution",.65f);
                        copia.SetFloat("_ReceiveShadows",1);
                        copia.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                    }
                    AssetDatabase.CreateAsset(copia,carpeta+"/Follaje_"+materialesVegetacion.Count+".mat");
                    materialesVegetacion.Add(clave,copia);
                }
                materiales[i]=copia;
            }
            renderer.sharedMaterials=materiales;
        }
    }

    static void IntegrarPueblos()
    {
        materialesVegetacion.Clear();
        int retirados=0;
        var azar=new System.Random(731);
        foreach(var zona in Zonas)
        {
            // En las demos costeras se rompe la valla de árboles sin alterar edificios ni accesorios.
            // Se conservan los jardines/cultivos del pueblo inicial y las terrazas de montaña.
            if(zona.demo!="05"&&zona.demo!="06")continue;
            var candidatos=new List<GameObject>();
            foreach(var t in zona.grupo.GetComponentsInChildren<Transform>())
            {
                if(!t.name.StartsWith("Tree",StringComparison.OrdinalIgnoreCase))continue;
                if(t.parent!=null&&t.parent.name.StartsWith("Tree",StringComparison.OrdinalIgnoreCase))continue;
                var fuente=PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                string ruta=fuente!=null?AssetDatabase.GetAssetPath(fuente):"";
                // Las copias de escenas pueden perder su conexión; el nombre exacto y un MeshRenderer
                // distinguen los árboles del pack de grupos de decoración.
                if(!ruta.Contains("Vegetation/")&&t.GetComponent<MeshRenderer>()==null)continue;
                candidatos.Add(t.gameObject);
            }
            foreach(var arbol in candidatos)
            {
                if(azar.NextDouble()<.65){Object.DestroyImmediate(arbol);retirados++;}
                else MatizarArbol(arbol);
            }
        }
        informe.AppendLine($"Transición de pueblos: {retirados} árboles de las hileras de las demos 05/06 retirados de la copia. Jardines de la demo 09 conservados.");
    }

    static void Arboledas(Transform grupo,GameObject conifera,GameObject frondosa,System.Random azar)
    {
        // Revisión 11 (Claude): variedad de frondosas (varias familias del pack), no una sola.
        var frondosas=new List<GameObject>();
        foreach(var n in new[]{"Tree02_a01","Tree03_a01","Tree04_a01","Tree06_a01","Tree07_a01"})
        { var v=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Vegetation/"+n+".prefab"); if(v!=null)frondosas.Add(v); }
        if(frondosas.Count==0)frondosas.Add(frondosa);
        // Rejilla de ocupación solo del generador: evita apilar troncos sin búsquedas cuadráticas.
        var ocupadas=new HashSet<Vector2Int>();
        int total=0;
        for(int i=0;i<18000&&total<2900;i++)
        {
            float x=(float)azar.NextDouble()*1000-500,z=(float)azar.NextDouble()*900-420;
            float bosque=PesoBosque(x,z);
            float mancha=Mathf.PerlinNoise(x*.019f+17,z*.019f+8);
            float densidad=Mathf.Lerp(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.47f,.69f,mancha))*.5f,.94f,bosque);
            if(azar.NextDouble()>densidad)continue;
            float h=terreno.SampleHeight(new Vector3(x,0,z))+terreno.transform.position.y;
            if(CostaDist(new Vector2(x,z))<34||h>115||!Libre(x,z,h))continue;
            if(terreno.terrainData.GetSteepness((x+650)/1300,(z+650)/1300)>33)continue;
            var celda=new Vector2Int(Mathf.FloorToInt(x/6),Mathf.FloorToInt(z/6));
            if(!ocupadas.Add(celda))continue;
            var modelo=bosque>.4f?(azar.NextDouble()<.28?conifera:frondosa):(h>68?frondosa:frondosas[azar.Next(frondosas.Count)]);
            var arbol=(GameObject)PrefabUtility.InstantiatePrefab(modelo,grupo);
            arbol.transform.position=new Vector3(x,h-.15f,z);
            arbol.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
            float alto=bosque>.4f?Mathf.Lerp(15,23,(float)azar.NextDouble()):Mathf.Lerp(10,17,(float)azar.NextDouble());
            arbol.transform.localScale*=FactorAltura(modelo,alto);
            MatizarArbol(arbol);total++;
        }
        informe.AppendLine($"Arboledas: {total} árboles en masas irregulares, claros y praderas; bosque de 15–23 m, exterior de 10–17 m. Sin dispersión de palmeras tierra adentro.");
    }

    static void ApoyarFaro(GameObject faro)
    {
        if(faro==null)return;
        // Plataforma de roca bajo TODA la huella: no apoyar solo el centro en una cumbre.
        var rs=faro.GetComponentsInChildren<MeshRenderer>();if(rs.Length==0)return;
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
        float radio=Mathf.Max(b.extents.x,b.extents.z)+2;
        float cota=b.min.y;
        var centro=new Vector3(CaboFaro.x,0,CaboFaro.y);
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;
            var q=centro+new Vector3(Mathf.Cos(a)*radio,0,Mathf.Sin(a)*radio);
            cota=Mathf.Max(cota,terreno.SampleHeight(q)+terreno.transform.position.y);
        }
        cota+=.3f;faro.transform.position+=Vector3.up*(cota-b.min.y);
        var vertices=new List<Vector3>{new Vector3(centro.x,cota,centro.z)};
        var triangulos=new List<int>();
        for(int i=0;i<12;i++)
        {
            float a=i*Mathf.PI/6;
            var q=centro+new Vector3(Mathf.Cos(a)*radio,0,Mathf.Sin(a)*radio);
            vertices.Add(new Vector3(q.x,cota,q.z));
            q=centro+(q-centro)*1.65f;
            vertices.Add(new Vector3(q.x,terreno.SampleHeight(q)+terreno.transform.position.y-1,q.z));
        }
        for(int i=0;i<12;i++)
        {
            int a=1+i*2,borde=1+(i+1)%12*2;
            triangulos.AddRange(new[]{0,borde,a,a,borde,borde+1,a,borde+1,a+1});
        }
        var m=new Mesh{name="Roca de apoyo del faro"};m.SetVertices(vertices);m.SetTriangles(triangulos,0);m.RecalculateNormals();m.RecalculateBounds();
        AssetDatabase.CreateAsset(m,carpeta+"/CaboFaro.asset");
        var go=new GameObject("Cabo del faro — basamento rocoso",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
        go.transform.SetParent(raiz);go.GetComponent<MeshFilter>().sharedMesh=m;go.GetComponent<MeshCollider>().sharedMesh=m;
        go.GetComponent<MeshRenderer>().sharedMaterial=Material("Roca del cabo",new Color(.37f,.38f,.34f));
        { int capa=LayerMask.NameToLayer("Floor"); if(capa>=0) go.layer=capa; }
        informe.AppendLine($"Faro apoyado sobre basamento de radio {radio:0.0} m, cota {cota:0.0} m.");
    }

    static void VerificarAgua()
    {
        foreach(var linea in new[]{rioSuave,arroyoSuave})
        {
            float minima=float.PositiveInfinity;int secos=0;Vector3 peor=Vector3.zero;
            for(int i=1;i<linea.Length;i++)
            {
                int pasos=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(linea[i-1],linea[i])/2));
                for(int j=0;j<=pasos;j++)
                {
                    var p=Vector3.Lerp(linea[i-1],linea[i],j/(float)pasos);
                    float profundidad=p.y-terreno.SampleHeight(p)-terreno.transform.position.y;
                    if(profundidad<minima){minima=profundidad;peor=p;}
                    if(profundidad<.1f)secos++;
                }
            }
            informe.AppendLine($"Cauce {(linea==rioSuave?"principal":"oriental")}: profundidad mínima central {minima:0.00} m en {peor}; {secos} muestras con menos de 0,1 m de agua.");
            if(secos>0)informe.AppendLine("REVISAR: el cauce necesita más profundidad en esos puntos; no se declara continuidad validada.");
        }
    }

    static void CapturarPaisaje(Camera cam)
    {
        // La máscara aísla la variante de otras escenas abiertas en la sesión.
        var posicion=cam.transform.position;var rotacion=cam.transform.rotation;
        bool ortografica=cam.orthographic;float tamano=cam.orthographicSize;
        ulong mascara=cam.overrideSceneCullingMask;var destino=cam.targetTexture;var activo=RenderTexture.active;
        ulong mascaraEscena=EditorSceneManager.GetSceneCullingMask(cam.gameObject.scene);
        var calidad=QualitySettings.renderPipeline;
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        UniversalRenderPipelineAsset pipelineCaptura=null;
        var rt=new RenderTexture(1600,1200,24);
        var lectura=new Texture2D(1600,1200,TextureFormat.RGB24,false);
        try
        {
            rt.Create();
            if(pipeline!=null)
            {
                pipelineCaptura=Object.Instantiate(pipeline);
                pipelineCaptura.shadowDistance=2200;
                pipelineCaptura.mainLightShadowmapResolution=4096;
                QualitySettings.renderPipeline=pipelineCaptura;
            }
            ulong exclusiva=EditorSceneManager.CalculateAvailableSceneCullingMask();
            EditorSceneManager.SetSceneCullingMask(cam.gameObject.scene,exclusiva);
            cam.overrideSceneCullingMask=exclusiva;
            void Foto(string nombre,Vector3 desde,Vector3 hacia,bool orto,float escala)
            {
                cam.transform.position=desde;cam.transform.LookAt(hacia);cam.orthographic=orto;cam.orthographicSize=escala;
                cam.targetTexture=rt;
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
                RenderPipeline.SubmitRenderRequest(cam,request);
                RenderTexture.active=rt;lectura.ReadPixels(new Rect(0,0,1600,1200),0,0);lectura.Apply();
                File.WriteAllBytes(carpeta+"/"+nombre+".png",lectura.EncodeToPNG());
            }
            Foto("Vista_mapa",new Vector3(0,1100,-1000),new Vector3(0,35,0),true,630);
            Foto("Vista_puerto",new Vector3(455,115,-660),new Vector3(245,18,-402),false,0);
            Foto("Vista_valle",new Vector3(-185,150,-350),new Vector3(25,65,130),false,0);
            informe.AppendLine("Capturas guardadas: Vista_mapa.png, Vista_puerto.png y Vista_valle.png. Revisarlas antes de aceptar la composición.");
        }
        finally
        {
            cam.transform.SetPositionAndRotation(posicion,rotacion);cam.orthographic=ortografica;cam.orthographicSize=tamano;
            cam.overrideSceneCullingMask=mascara;cam.targetTexture=destino;RenderTexture.active=activo;
            EditorSceneManager.SetSceneCullingMask(cam.gameObject.scene,mascaraEscena);
            QualitySettings.renderPipeline=calidad;
            if(pipelineCaptura!=null)Object.DestroyImmediate(pipelineCaptura);
            rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(lectura);
        }
    }
}
