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
    // Revisión 18 (Claude): los dos bosques se separan en dos funciones para poder darles paleta propia
    // (`ParajeEn`); `PesoBosque` sigue devolviendo el máximo de ambos para todo lo que ya lo usaba.
    static float PesoBosqueProhibido(float x,float z)
    {
        float dx=(x+310)/175, dz=(z-55)/215;
        float borde=Mathf.Sqrt(dx*dx+dz*dz);
        borde+=(Mathf.PerlinNoise(x*.014f+13,z*.014f+29)-.5f)*.38f;
        return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.12f,borde));
    }
    static float PesoFuegoFatuo(float x,float z)
    {
        // Revisión 16 (Claude): segundo bosque — el del Fuego Fatuo, entre el Reino (montaña) y el pueblo vecino
        // (playa este), en la franja oriental. Más pequeño y con borde más nervioso.
        float ex=(x-330)/95, ez=(z-40)/120;
        float borde2=Mathf.Sqrt(ex*ex+ez*ez)+(Mathf.PerlinNoise(x*.02f+71,z*.02f+5)-.5f)*.5f;
        return (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1.1f,borde2)))*.9f;
    }
    static float PesoBosque(float x,float z)=>Mathf.Max(PesoBosqueProhibido(x,z),PesoFuegoFatuo(x,z));
    // Peso 1 en el corazón de un islote habitable (jungla del hechicero, Ruinas de la Piedra Ancestral) y 0 en el
    // agua. El radio de tierra real del islote ronda el 55-60 % del radio base de `RelieveIslote` (medido).
    static float PesoIslote(float x,float z,Vector4 islote)
    {
        // Revisión 19: cubre hasta la playa (r≈0.9); en el mar da igual, los árboles exigen h≥1.5 m.
        float r=Vector2.Distance(new Vector2(x,z),new Vector2(islote.x,islote.y))/islote.z;
        return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1f,r));
    }
    // Revisión 19 (Claude): relieve de un islote HABITABLE. Sector de playa hacia `haciaPlaya` (arena que baja suave de
    // la meseta al mar — por ahí se desembarca/se sale del agua), acantilado en el resto (roca por pendiente), lomas
    // suaves en el interior y un claro llano (r≈0.2·radio) en el centro para el recinto/cabaña. Sustituye a
    // `RelieveIslote` + aplanado de zona para estos dos islotes: aquello daba una meseta pelada con un solo anillo de
    // 6 m plantable, de ahí las "islas pobres y sin chicha" de la revisión 18.
    static float RelieveIsloteHabitable(float x,float z,Vector4 islote,Vector2 haciaPlaya,float cota)
    {
        float dx=x-islote.x,dz=z-islote.y;float dist=Mathf.Sqrt(dx*dx+dz*dz);
        float r=dist/islote.z+(Mathf.PerlinNoise(x*.02f+51,z*.02f+9)-.5f)*.10f;
        if(r>1.2f)return -12;
        float coseno=dist>.001f?(dx*haciaPlaya.x+dz*haciaPlaya.y)/dist:1;
        float sector=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.8f,coseno)); // 1 = de cara a la playa
        float lomas=(Mathf.PerlinNoise(x*.03f+3,z*.03f+77)-.5f)*7+(Mathf.PerlinNoise(x*.08f+19,z*.08f+2)-.5f)*2;
        float claro=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.16f,.28f,r));
        float meseta=cota+lomas*(1-claro);
        float acantilado=Mathf.Lerp(meseta,-12,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.68f,r)));
        float playa=r<.92f?Mathf.Lerp(meseta,1.5f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,.92f,r))):Mathf.Lerp(1.5f,-12,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.92f,1.06f,r))); // rampa larga (<35 %, caminable); revisión 21: la arena acaba antes (1.15→1.06) para no tragarse los escollos
        return Mathf.Lerp(acantilado,playa,sector);
    }
    static float PesoJungla(float x,float z)=>PesoIslote(x,z,IsloteJungla);
    static float PesoRuinas(float x,float z)=>PesoIslote(x,z,IsloteRuinas);
    static float PesoSecreta(float x,float z)=>PesoIslote(x,z,IsloteSecreta); // revisión 20
    static float PesoHermanas(float x,float z){float w=0;foreach(var h in Hermanas)w=Mathf.Max(w,PesoIslote(x,z,h.islote));return w;} // revisión 22

    // Revisión 18 (Claude): paleta de vegetación POR ZONA, con los colores reales de cada prefab del pack
    // (catálogo `VegetacionPaletaInforme.md`; decisión en propuesta-separacion-zonas-lore-vegetacion-isla-
    // piedra-ancestral-2026-09-11.md). Cada zona lee como un sitio distinto sin mapa: verde → turquesa → rojo.
    // La letra del nombre NO es un código de color consistente entre familias, de ahí las listas explícitas.
    enum Paraje { Campo, BosqueProhibido, FuegoFatuo, Jungla, Ruinas, Secreta, Hermanas }
    static readonly Dictionary<Paraje,string[]> ArbolesPorParaje=new Dictionary<Paraje,string[]>
    {
        // Verde denso: lo que ya era (Tree02) más siluetas de Tree03 y los verdes más oscuros (Tree06) para el fondo.
        {Paraje.BosqueProhibido,new[]{"Tree02_a01","Tree02_a02","Tree02_b01","Tree02_c01","Tree02_d01","Tree03_a01","Tree03_b01","Tree03_c01","Tree03_d01","Tree06_a01","Tree06_a02","Tree06_a03"}},
        // Fuego Fatuo: verdes oscuros de Tree06 y Tree03 (un bosque más cerrado y sombrío que el Prohibido, sin ser el mismo).
        {Paraje.FuegoFatuo,new[]{"Tree06_a01","Tree06_a02","Tree06_a03","Tree03_a02","Tree03_c02","Tree02_c02"}},
        // Jungla del hechicero: dosel turquesa (Tree04_a/c, Tree07_a) con una segunda capa lima (Tree04_d, Tree01_b).
        {Paraje.Jungla,new[]{"Tree04_a01","Tree04_a02","Tree04_a03","Tree04_c01","Tree04_c02","Tree04_c03","Tree07_a01","Tree04_d01","Tree04_d02","Tree04_d03","Tree01_b01","Tree01_b02"}},
        // Ruinas / Piedra Ancestral: TODOS los rojos y otoñales del pack — no se usan en ningún otro sitio.
        {Paraje.Ruinas,new[]{"Tree02_e01","Tree02_e02","Tree02_f01","Tree03_e01","Tree03_e02","Tree03_f01","Tree05_b01","Tree07_c01","Tree04_b01"}},
        // Campo y pueblos: los verdes más claros y "amables".
        {Paraje.Campo,new[]{"Tree01_a01","Tree01_a02","Tree05_a01","Tree07_b01","Tree02_a01","Tree03_a01"}},
        // Isla secreta (revisión 20): el tercer color que faltaba — lima (Tree01_b, Tree04_d) con el verde claro de Tree07_b01.
        {Paraje.Secreta,new[]{"Tree01_b01","Tree01_b02","Tree04_d01","Tree04_d02","Tree04_d03","Tree07_b01"}},
        // Las Hermanas (revisión 22): verde claro alegre con flores — islas "bonitas" de recompensa.
        {Paraje.Hermanas,new[]{"Tree01_a01","Tree01_a02","Tree07_b01","Tree05_a01","Tree04_d01"}}
    };
    static readonly Dictionary<Paraje,string[]> SueloPorParaje=new Dictionary<Paraje,string[]>
    {
        {Paraje.BosqueProhibido,new[]{"Grass01_a01","Grass01_a02","Grass02_a01","Grass02_a02","Grass03_a01","Mushroom02_a01","Mushroom02_a02","Mushroom02_a03","Mushroom01_c01","Mushroom01_c02","Mushroom01_c03"}},
        {Paraje.FuegoFatuo,new[]{"Grass03_a01","Grass01_a01","Mushroom02_a01","Mushroom01_a01","Mushroom01_a02","Mushroom01_a03"}}, // setas azules = "fuegos fatuos" a ras de suelo
        {Paraje.Jungla,new[]{"Grass01_a01","Grass01_a02","Grass02_a01","Grass02_a02","Grass03_a01","Mushroom02_b01","Mushroom02_b02","Mushroom02_b03","Mushroom01_a04","Mushroom01_a05"}},
        {Paraje.Ruinas,new[]{"Mushroom03_c01","Mushroom02_c01","Mushroom02_c02","Mushroom02_c03","Mushroom04_c01"}}, // solo junto a la Piedra: acento mágico azul-gris/violeta
        {Paraje.Campo,new[]{"Grass01_a01","Grass02_a01","Mushroom01_b01","Mushroom01_b02"}},
        {Paraje.Secreta,new[]{"Mushroom03_a01","Mushroom04_a01","Mushroom04_b01","Flower01_a01","Flower01_b01","Flower03_a01","Flower05_a01","Grass02_a02"}}, // las setas magenta/roja/mostaza que estaban en reserva, más flores
        {Paraje.Hermanas,new[]{"Flower01_a01","Flower01_b01","Flower01_c01","Flower02_a01","Flower02_b01","Flower03_a01","Flower04_a01","Flower05_a01","Flower05_a02","Grass01_a01","Grass02_a01","Mushroom01_b01"}}
    };
    static Paraje ParajeEn(float x,float z)
    {
        if(PesoRuinas(x,z)>.3f)return Paraje.Ruinas;
        if(PesoJungla(x,z)>.3f)return Paraje.Jungla;
        if(PesoSecreta(x,z)>.3f)return Paraje.Secreta;
        if(PesoHermanas(x,z)>.3f)return Paraje.Hermanas;
        float prohibido=PesoBosqueProhibido(x,z),fatuo=PesoFuegoFatuo(x,z);
        if(prohibido>.4f&&prohibido>=fatuo)return Paraje.BosqueProhibido;
        if(fatuo>.4f)return Paraje.FuegoFatuo;
        return Paraje.Campo;
    }
    static readonly Dictionary<string,GameObject> prefabsVegetacion=new Dictionary<string,GameObject>();
    static readonly HashSet<string> prefabsAusentes=new HashSet<string>();
    static GameObject PrefabVegetal(string nombre)
    {
        GameObject go;
        if(prefabsVegetacion.TryGetValue(nombre,out go))return go;
        go=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Vegetation/"+nombre+".prefab");
        if(go==null&&prefabsAusentes.Add(nombre))informe.AppendLine("Vegetación: prefab no encontrado, se omite: "+nombre);
        prefabsVegetacion[nombre]=go;return go;
    }
    static GameObject ModeloVegetal(Dictionary<Paraje,string[]> tabla,Paraje paraje,System.Random azar)
    {
        var nombres=tabla[paraje];
        for(int intento=0;intento<nombres.Length;intento++)
        {
            var go=PrefabVegetal(nombres[azar.Next(nombres.Length)]);
            if(go!=null)return go;
        }
        return null;
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
        var shader=deMainWorld?Shader.Find("El Sendero/Eldoria/Agua MainWorld corregida"):Shader.Find("El Sendero/Eldoria/Agua de maqueta");
        var copia=deMainWorld?new Material(origen):shader!=null?new Material(shader):new Material(origen);
        if(shader!=null)copia.shader=shader;
        copia.name=fluvial?"Agua de río — espuma contenida":"Mar — costa turquesa";
        if(shader!=null&&!deMainWorld)
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

    // Revisión 18: `estilo` — 2 = umbrío (bosques), 1 = exterior verde (campo, como hasta ahora), 0 = SIN tinte (jungla
    // turquesa y ruinas rojas: el tinte verde de la copia les robaba el color que justamente las distingue),
    // -1 = decidir por `PesoBosque` como antes (árboles de las demos y pinar de la cascada).
    static readonly Dictionary<(Material,int),Material> materialesVegetacion=new Dictionary<(Material,int),Material>();
    static void MatizarArbol(GameObject objeto,int estilo=-1)
    {
        if(estilo<0)estilo=PesoBosque(objeto.transform.position.x,objeto.transform.position.z)>.4f?2:1;
        if(estilo==0)return;
        bool umbrio=estilo==2;
        foreach(var renderer in objeto.GetComponentsInChildren<MeshRenderer>())
        {
            var materiales=renderer.sharedMaterials;
            for(int i=0;i<materiales.Length;i++)
            {
                var original=materiales[i]; if(original==null)continue;
                var clave=(original,estilo);
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
        materialesVegetacion.Clear();prefabsVegetacion.Clear();prefabsAusentes.Clear();
        int retirados=0;
        var azar=new System.Random(731);
        foreach(var zona in Zonas)
        {
            // En las demos costeras se rompe la valla de árboles sin alterar edificios ni accesorios.
            // Se conservan los jardines/cultivos del pueblo inicial y las terrazas de montaña.
            if(zona.demo!="05"&&zona.demo!="06"&&zona.demo!="09")continue;
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
                var delta=arbol.transform.position-zona.centro; float radio=new Vector2(delta.x/zona.mitad.x,delta.z/zona.mitad.y).magnitude;
                if(azar.NextDouble()<.65&&(zona.demo!="09"||radio>.68f)){Object.DestroyImmediate(arbol);retirados++;}
                else MatizarArbol(arbol);
            }
        }
        informe.AppendLine($"Transición de pueblos: {retirados} árboles de las hileras de las demos 05/06 retirados de la copia. Jardines de la demo 09 conservados.");
    }

    static void Arboledas(Transform grupo,GameObject conifera,GameObject frondosa,System.Random azar)
    {
        // Revisión 18 (Claude): el modelo de cada árbol lo decide el paraje (`ParajeEn`) y su lista de prefabs
        // (`ArbolesPorParaje`), nunca una lista global — así el Bosque Prohibido se queda verde, la isla del
        // hechicero sale turquesa y la isla de las Ruinas roja/otoñal. Se muestrea todo el terreno (antes solo el
        // continente) para que los dos islotes habitables también se pueblen.
        var ocupadas=new HashSet<Vector2Int>();
        var cuenta=new Dictionary<Paraje,int>();foreach(Paraje p in Enum.GetValues(typeof(Paraje)))cuenta[p]=0;
        int total=0;
        for(int i=0;i<26000&&total<3600;i++)
        {
            float x=(float)azar.NextDouble()*1300-650,z=(float)azar.NextDouble()*1300-650;
            float costa=CostaDist(new Vector2(x,z));
            float jungla=PesoJungla(x,z),ruinas=PesoRuinas(x,z),secreta=PesoSecreta(x,z),hermanas=PesoHermanas(x,z);
            bool enIslote=costa<0&&(jungla>.2f||ruinas>.2f||secreta>.2f||hermanas>.2f);
            if(!enIslote&&costa<34)continue; // playas bajas del continente y mar abierto
            var paraje=ParajeEn(x,z);
            float bosque=PesoBosque(x,z);
            float mancha=Mathf.PerlinNoise(x*.019f+17,z*.019f+8);
            float campo=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.47f,.69f,mancha))*.5f;
            float densidad;
            switch(paraje)
            {
                case Paraje.Jungla: densidad=Mathf.Lerp(.15f,.96f,jungla);break;
                case Paraje.Ruinas: densidad=Mathf.Lerp(.1f,.5f,ruinas);break; // bosque seco alrededor del templo abandonado (revisión 19: algo más denso, la 18 salió pelada)
                case Paraje.Secreta: densidad=Mathf.Lerp(.15f,.95f,secreta);break; // revisión 21: la isla salía rala (20 árboles)
                case Paraje.Hermanas: densidad=Mathf.Lerp(.1f,.55f,hermanas);break; // abiertas, con claros de flores
                case Paraje.FuegoFatuo: densidad=Mathf.Lerp(campo,.86f,bosque);break;
                case Paraje.BosqueProhibido: densidad=Mathf.Lerp(campo,.94f,bosque);break;
                default: densidad=campo;break;
            }
            if(azar.NextDouble()>densidad)continue;
            float h=terreno.SampleHeight(new Vector3(x,0,z))+terreno.transform.position.y;
            if(h>115||!Libre(x,z,h))continue;
            if(enIslote&&h<7)continue; // revisión 19: la playa del islote queda limpia
            if(terreno.terrainData.GetSteepness((x+650)/1300,(z+650)/1300)>33)continue;
            float celdaMetros=paraje==Paraje.Jungla||paraje==Paraje.Secreta?5:6;
            var celda=new Vector2Int(Mathf.FloorToInt(x/celdaMetros),Mathf.FloorToInt(z/celdaMetros));
            if(!ocupadas.Add(celda))continue;
            GameObject modelo;
            if(paraje==Paraje.Campo&&h>68)modelo=frondosa; // laderas altas: el "pino" de siempre
            else modelo=ModeloVegetal(ArbolesPorParaje,paraje,azar)??frondosa;
            var arbol=(GameObject)PrefabUtility.InstantiatePrefab(modelo,grupo);
            arbol.transform.position=new Vector3(x,h-.15f,z);
            arbol.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
            float alto;
            switch(paraje)
            {
                case Paraje.BosqueProhibido: alto=Mathf.Lerp(15,23,(float)azar.NextDouble());break;
                case Paraje.FuegoFatuo: alto=Mathf.Lerp(13,20,(float)azar.NextDouble());break;
                case Paraje.Jungla: alto=Mathf.Lerp(12,19,(float)azar.NextDouble());break;
                case Paraje.Ruinas: alto=Mathf.Lerp(9,15,(float)azar.NextDouble());break;
                case Paraje.Secreta: alto=Mathf.Lerp(10,16,(float)azar.NextDouble());break;
                case Paraje.Hermanas: alto=Mathf.Lerp(9,15,(float)azar.NextDouble());break;
                default: alto=Mathf.Lerp(10,17,(float)azar.NextDouble());break;
            }
            arbol.transform.localScale*=FactorAltura(modelo,alto);
            int estilo=paraje==Paraje.BosqueProhibido||paraje==Paraje.FuegoFatuo?2:paraje==Paraje.Campo?1:0;
            MatizarArbol(arbol,estilo);total++;cuenta[paraje]++;
        }
        informe.AppendLine($"Arboledas por paraje: {total} árboles — Bosque Prohibido {cuenta[Paraje.BosqueProhibido]} (verde denso, 15–23 m), Fuego Fatuo {cuenta[Paraje.FuegoFatuo]} (verde oscuro), isla del hechicero {cuenta[Paraje.Jungla]} (turquesa/lima, 12–19 m), isla de las Ruinas {cuenta[Paraje.Ruinas]} (rojo/otoñal, 9–15 m), isla secreta {cuenta[Paraje.Secreta]} (lima), Las Hermanas {cuenta[Paraje.Hermanas]} (verde claro y flores), campo {cuenta[Paraje.Campo]} (verde claro, 10–17 m).");
        SueloVegetal(grupo,azar);
    }

    // Revisión 18 (Claude): detalle de suelo con prefabs del pack (hierba y setas) según la paleta de cada paraje.
    // Las setas azules/violetas quedan reservadas al Fuego Fatuo y a las Ruinas; las rojas ("venenosas") al Bosque.
    static void SueloVegetal(Transform padre,System.Random azar)
    {
        var grupo=new GameObject("Suelo vegetal — hierba y setas por paraje").transform;grupo.SetParent(padre);
        var tope=new Dictionary<Paraje,int>{{Paraje.BosqueProhibido,700},{Paraje.FuegoFatuo,200},{Paraje.Jungla,320},{Paraje.Ruinas,70},{Paraje.Campo,250},{Paraje.Secreta,160},{Paraje.Hermanas,360}};
        var cuenta=new Dictionary<Paraje,int>();foreach(Paraje p in Enum.GetValues(typeof(Paraje)))cuenta[p]=0;
        int total=0;
        for(int i=0;i<50000&&total<2060;i++)
        {
            float x=(float)azar.NextDouble()*1300-650,z=(float)azar.NextDouble()*1300-650;
            float costa=CostaDist(new Vector2(x,z));
            float jungla=PesoJungla(x,z),ruinas=PesoRuinas(x,z),secreta=PesoSecreta(x,z),hermanas=PesoHermanas(x,z);
            bool enIslote=costa<0&&(jungla>.2f||ruinas>.2f||secreta>.2f||hermanas>.2f);
            if(!enIslote&&costa<34)continue;
            var paraje=ParajeEn(x,z);
            if(cuenta[paraje]>=tope[paraje])continue;
            float densidad;
            switch(paraje)
            {
                case Paraje.BosqueProhibido: densidad=.55f*PesoBosqueProhibido(x,z);break;
                case Paraje.FuegoFatuo: densidad=.4f*PesoFuegoFatuo(x,z);break;
                case Paraje.Jungla: densidad=.6f*jungla;break;
                // Solo en el anillo alrededor del recinto de las Ruinas (el propio recinto está despejado por `Hitos`).
                case Paraje.Ruinas: densidad=Vector2.Distance(new Vector2(x,z),new Vector2(IsloteRuinas.x,IsloteRuinas.y))<46?.5f:0;break;
                case Paraje.Secreta: densidad=.7f*secreta;break; // flores y setas raras por toda la isla
                case Paraje.Hermanas: densidad=.8f*hermanas;break; // praderas de flores
                default: densidad=.06f;break;
            }
            if(azar.NextDouble()>densidad)continue;
            float h=terreno.SampleHeight(new Vector3(x,0,z))+terreno.transform.position.y;
            if(h>115||!Libre(x,z,h))continue;
            if(enIslote&&h<7)continue;
            if(terreno.terrainData.GetSteepness((x+650)/1300,(z+650)/1300)>35)continue;
            var modelo=ModeloVegetal(SueloPorParaje,paraje,azar);if(modelo==null)continue;
            var pieza=(GameObject)PrefabUtility.InstantiatePrefab(modelo,grupo);
            pieza.transform.rotation=Quaternion.Euler(0,(float)azar.NextDouble()*360,0);
            bool hierba=modelo.name.StartsWith("Grass",StringComparison.OrdinalIgnoreCase);
            pieza.transform.localScale*=FactorAltura(modelo,hierba?Mathf.Lerp(.7f,1.3f,(float)azar.NextDouble()):Mathf.Lerp(.5f,1.1f,(float)azar.NextDouble()));
            var b=LimitesVisibles(pieza);
            pieza.transform.position=new Vector3(x,h-(b.min.y-pieza.transform.position.y)-.03f,z);
            total++;cuenta[paraje]++;
        }
        informe.AppendLine($"Suelo vegetal: {total} piezas — Bosque Prohibido {cuenta[Paraje.BosqueProhibido]}, Fuego Fatuo {cuenta[Paraje.FuegoFatuo]}, jungla {cuenta[Paraje.Jungla]}, Ruinas {cuenta[Paraje.Ruinas]} (setas azul-gris/violeta junto a la Piedra), campo {cuenta[Paraje.Campo]}.");
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
        VerificarTerrenoSobreElNivelDelMar();
    }

    // INC-197 (12 sept 2026): lo de arriba solo comprueba el río y el arroyo — nunca hubo ninguna
    // comprobación de que el RESTO del terreno (campos, praderas, zonas sin `relieve` propio) se quede
    // por encima del nivel del mar (el plano "Mar" está siempre a Y=0, cubriendo los 2400x2400 m del
    // mapa). Un hueco así no salía en ningún informe hasta que Raúl lo veía "de pronto" tapando el
    // minimapa en el juego. Barrido en rejilla de todo el mapa (mismo rango -650..650 que usa
    // `PraderasGlobales`), fuera de la franja costera de 15 m donde SÍ es normal que la rampa siga
    // bajando hacia el agua (ver el suelo de seguridad añadido en `Altura` para esa franja) y fuera de
    // los islotes (que se apoyan en el mar a propósito). No corrige nada — solo avisa, igual que el
    // resto de `VerificarAgua`, para que quede constancia en el `Informe.txt` de la próxima maqueta.
    static void VerificarTerrenoSobreElNivelDelMar()
    {
        int huecos=0;float peorAltura=float.PositiveInfinity;Vector2 peorPunto=Vector2.zero;
        for(float z=-650;z<650;z+=10)for(float x=-650;x<650;x+=10)
        {
            float costa=CostaDist(new Vector2(x,z));
            if(costa<=15f)continue; // franja costera: bajar hacia el agua ahí es el diseño, no un bug
            bool enIslote=false;
            foreach(var zona in Zonas){if(!zona.esIslote)continue;float d=new Vector2((x-zona.centro.x)/Mathf.Max(1,zona.mitad.x),(z-zona.centro.z)/Mathf.Max(1,zona.mitad.y)).magnitude;if(d<1.3f){enIslote=true;break;}}
            if(enIslote)continue;
            float h=terreno.SampleHeight(new Vector3(x,0,z))+terreno.transform.position.y;
            if(h>=.3f)continue;
            huecos++;
            if(h<peorAltura){peorAltura=h;peorPunto=new Vector2(x,z);}
        }
        if(huecos>0)
            informe.AppendLine($"REVISAR: {huecos} puntos del terreno (fuera de costa e islotes) quedan a menos de 0,3 m sobre "+
                $"el nivel del mar — el peor, a {peorAltura:0.00} m, en ({peorPunto.x:0},{peorPunto.y:0}). El plano \"Mar\" (Y=0, "+
                "2400x2400 m) asoma ahí por encima del terreno; en el minimapa se ve como una mancha plana tapando esa zona "+
                "(INC-197). Revisar el relieve de esa zona en concreto antes de dar la maqueta por buena.");
        else
            informe.AppendLine("Terreno fuera de costa/islotes: ningún punto muestreado queda bajo 0,3 m sobre el nivel del mar.");
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
            Foto("Vista_puerto",new Vector3(345,55,-530),new Vector3(270,5,-449),false,0);
            Foto("Vista_castillo",new Vector3(110,155,185),new Vector3(0,109,300),false,0);
            Foto("Vista_vecino",new Vector3(420,75,-210),new Vector3(330,23,-115),false,0);
            Foto("Vista_pueblo",new Vector3(100,80,-240),new Vector3(0,24,-130),false,0);
            Foto("Vista_ruinas",new Vector3(IsloteRuinas.x+RuinasPlaya.x*240,70,IsloteRuinas.y+RuinasPlaya.y*240),new Vector3(IsloteRuinas.x,RuinasCota,IsloteRuinas.y),false,0); // revisión 19: desde el mar, por la playa
            Foto("Vista_jungla",new Vector3(IsloteJungla.x+JunglaPlaya.x*235,65,IsloteJungla.y+JunglaPlaya.y*235),new Vector3(IsloteJungla.x,JunglaCota,IsloteJungla.y),false,0);
            Foto("Vista_bosque",new Vector3(-250,70,-40),new Vector3(-330,40,90),false,0);
            Foto("Vista_ruta",new Vector3(400,75,-640),new Vector3(455,5,-535),false,0); // revisión 20: Rompiente, pasarelas y arrecife
            Foto("Vista_hermanas",new Vector3(-330,120,330),new Vector3(-520,14,460),false,0); // revisión 22
            Foto("Vista_granjas",new Vector3(330,70,60),new Vector3(250,38,150),false,0);
            Foto("Vista_secreta",new Vector3(IsloteSecreta.x+SecretaPlaya.x*200,55,IsloteSecreta.y+SecretaPlaya.y*200),new Vector3(IsloteSecreta.x,SecretaCota,IsloteSecreta.y),false,0);
            Foto("Vista_puente",new Vector3(-150,34,-81),new Vector3(-170,26,-46),false,0);
            Foto("Vista_valle",new Vector3(-185,150,-350),new Vector3(25,65,130),false,0);
            // Revisión 27: mirador junto a la cascada — cámara desde mar adentro (aguas abajo) mirando hacia atrás,
            // al velo de agua y a la repisa del mirador a media altura del acantilado.
            {
                var dirM=new Vector3(CascadaDireccion.x,0,CascadaDireccion.y).normalized;
                var lateralM=Vector3.Cross(Vector3.up,dirM);
                var miradorPos=CascadaCentro+lateralM*15;miradorPos.y=cascadaNivelBajo+CascadaSalto*0.55f;
                Foto("Vista_mirador",miradorPos+dirM*70+Vector3.up*15-lateralM*25,miradorPos,false,0);
            }
            informe.AppendLine("Capturas guardadas: Vista_mapa, Vista_puerto, Vista_castillo, Vista_vecino, Vista_pueblo, Vista_ruinas, Vista_jungla, Vista_bosque, Vista_ruta, Vista_hermanas, Vista_granjas, Vista_secreta, Vista_puente, Vista_valle y Vista_mirador (.png). Revisarlas antes de aceptar la composición.");
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
