using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EldoriaCodexBuilder
{
    sealed class Solar
    {
        public Bounds limites;
        public float cota;
        public GameObject edificio;
        public bool plaza;
        public bool natural;
    }
    static readonly List<Solar> solares=new List<Solar>();
    static readonly string[] Viviendas={"BuildingAT01","BuildingAT03","BuildingAT07","BuildingAT12","BuildingAT17","BuildingAT18","BuildingAT23","BuildingAT53"};
    static Material maderaPuerto;

    static void PrepararUrbanismo()
    {
        solares.Clear();maderaPuerto=null;despejesMuralla.Clear();trampCerca=-1;
    }

    static Bounds LimitesVisibles(GameObject go)
    {
        bool primero=true;var b=new Bounds(go.transform.position,Vector3.zero);
        foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
        {
            if(!r.enabled||!r.gameObject.activeInHierarchy)continue;
            if(primero){b=r.bounds;primero=false;}else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static GameObject PiezaUrbana(Transform grupo,string ruta,string nombre,Vector3 posicion,Vector3 frente,bool cimentar=true,float ancho=0)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        if(prefab==null)throw new InvalidOperationException("No se encuentra la pieza urbana: "+ruta);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,grupo);go.name=nombre;
        go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.LookRotation(frente);
        var b=LimitesVisibles(go);
        if(ancho>0){go.transform.localScale*=ancho/Mathf.Max(b.size.x,b.size.z);b=LimitesVisibles(go);}
        go.transform.position+=new Vector3(posicion.x-b.center.x,posicion.y-b.min.y,posicion.z-b.center.z);
        b=LimitesVisibles(go);
        if(cimentar)solares.Add(new Solar{limites=b,cota=posicion.y,edificio=go});
        return go;
    }

    static void Plaza(Vector3 centro,Vector2 tamano,string nombre)
    {
        solares.Add(new Solar{limites=new Bounds(centro,new Vector3(tamano.x,1,tamano.y)),cota=centro.y,plaza=true,natural=nombre.StartsWith("Claro")||nombre.StartsWith("Descanso")||nombre.StartsWith("Pradera")||nombre.StartsWith("Huerto")||nombre.StartsWith("Huerta")}); // revisión 22: los huertos son "naturales" (prado, no empedrado)
        var reserva=new GameObject(nombre);reserva.transform.SetParent(raiz);reserva.transform.position=centro;
        informe.AppendLine($"Espacio reservado: {nombre}, {tamano.x:0} × {tamano.y:0} m en {centro}. Geometría, sin disparadores narrativos.");
    }

    static float SueloBarrio(float z)
    {
        // Barrio abierto con ladera continua. La rasante de sus calles se impone después.
        return Mathf.Lerp(90,112,Mathf.SmoothStep(0,1,Mathf.InverseLerp(210,285,z)));
    }

    // Revisión 23: trazado de la muralla del Reino (x,z), en sentido horario desde la esquina suroeste. La puerta va en el
    // hueco entre los puntos 1 y 2 (lado sureste), por donde la calle sube hacia el norte por x≈95.
    static readonly Vector2[] MurallaTrazado={new Vector2(-92,238),new Vector2(80,238),new Vector2(106,252),new Vector2(108,300),new Vector2(66,350),new Vector2(-66,350),new Vector2(-104,300)};
    static readonly List<Vector3> despejesMuralla=new List<Vector3>();
    static void MurallaDelReino(Transform grupo)
    {
        var lienzo=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Main Structures/Wall/Wall02.prefab");
        var torre=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Main Structures/Wall/Tower01.prefab");
        if(lienzo==null||torre==null){informe.AppendLine("Muralla del Reino omitida: Wall02/Tower01 no encontrados en Main Structures/Wall.");return;}
        var raizMuralla=new GameObject("Muralla del Reino").transform;raizMuralla.SetParent(grupo);
        float alturaMuro=6, alturaTorre=9.5f; // revisión 24: a 9/14 m las casas parecían de juguete al lado de la muralla
        int tramos=0;
        float DistanciaVia(Vector3 p)
        {
            float minima=float.MaxValue;
            foreach(var ruta in rutasSuaves){float y;minima=Mathf.Min(minima,Cercania(new Vector2(p.x,p.z),ruta,out y));}
            return minima;
        }
        Vector3 Suelo(Vector2 q){var p=new Vector3(q.x,0,q.y);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;return p;}
        var tmp=(GameObject)PrefabUtility.InstantiatePrefab(lienzo);var bt=LimitesVisibles(tmp);float largoNativo=Mathf.Max(bt.size.x,bt.size.z);Object.DestroyImmediate(tmp);
        if(largoNativo<.5f)largoNativo=6;
        for(int i=0;i<MurallaTrazado.Length;i++)
        {
            int j=(i+1)%MurallaTrazado.Length;
            // La abertura se decide por intersección con la vía; el hueco antiguo no coincidía con su curva.
            var a=Suelo(MurallaTrazado[i]);var b=Suelo(MurallaTrazado[j]);
            var d=b-a;d.y=0;float L=d.magnitude;d/=L;
            int n=Mathf.Max(1,Mathf.CeilToInt(L/6f)); // Tramos cortos siguen el relieve y acotan la abertura de acceso.
            for(int k=0;k<n;k++)
            {
                var p=Vector3.Lerp(a,b,(k+.5f)/n);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;
                bool cruza=false;
                for(int muestra=0;muestra<=12;muestra++)
                    if(DistanciaVia(p+d*((muestra/12f-.5f)*L/n))<4){cruza=true;break;}
                if(cruza)continue; // Muestrear el lienzo evita cortar muros paralelos al camino.
                var go=(GameObject)PrefabUtility.InstantiatePrefab(lienzo,raizMuralla);go.name="Lienzo de muralla";
                go.transform.rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,d));
                var bb=LimitesVisibles(go);float lado=Mathf.Max(bb.size.x,bb.size.z);
                float sx=(L/n+.15f)/largoNativo, sy=bb.size.y>.1f?alturaMuro/bb.size.y:1;
                bool largoEnX=bt.size.x>=bt.size.z; // Escala en el eje local del prefab, no su AABB ya girado.
                go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(largoEnX?sx:1,sy,largoEnX?1:sx));
                bb=LimitesVisibles(go);go.transform.position+=new Vector3(p.x-bb.center.x,p.y-.4f-bb.min.y,p.z-bb.center.z);
                despejes.Add(new Vector3(p.x,p.z,5));tramos++;
            }
        }
        foreach(var q in MurallaTrazado)
        {
            var p=Suelo(q);
            if(DistanciaVia(p)<6)continue;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(torre,raizMuralla);go.name="Torre de muralla";
            var bb=LimitesVisibles(go);if(bb.size.y>.1f)go.transform.localScale*=alturaTorre/bb.size.y;
            bb=LimitesVisibles(go);go.transform.position+=new Vector3(p.x-bb.center.x,p.y-.4f-bb.min.y,p.z-bb.center.z);
            despejes.Add(new Vector3(p.x,p.z,6));
        }
        // Revisión 24: `Castle01_b01` resultó ser una verja dorada de 30 m — fuera. El hueco queda flanqueado por dos torres
        // hasta elegir puerta con el catálogo (`EldoriaCatalogoAssets`).
        GameObject puerta=null;
        if(puerta!=null)
        {
            var a=Suelo(MurallaTrazado[1]);var b=Suelo(MurallaTrazado[2]);var c=(a+b)*.5f;c.y=terreno.SampleHeight(c)+terreno.transform.position.y;
            var go=(GameObject)PrefabUtility.InstantiatePrefab(puerta,raizMuralla);go.name="Puerta del Reino";
            var d=b-a;d.y=0;go.transform.rotation=Quaternion.LookRotation(-Vector3.Cross(Vector3.up,d.normalized));
            var bb=LimitesVisibles(go);float ancho=Mathf.Max(bb.size.x,bb.size.z);if(ancho>.1f)go.transform.localScale*=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/ancho;
            bb=LimitesVisibles(go);go.transform.position+=new Vector3(c.x-bb.center.x,c.y-.3f-bb.min.y,c.z-bb.center.z);
        }
        informe.AppendLine($"Muralla del Reino: {tramos} lienzos macizos Wall02 de {alturaMuro} m de altura, en tramos de hasta 6 m; torres Tower01 de {alturaTorre} m. Abertura sobre la vía principal, sin verja gigante.");
    }

    static void ConstruirBarrioMontana(Zona zona)
    {
        zona.centro=new Vector3(0,112,295);zona.mitad=new Vector2(127,100);
        zona.relieve=(x,z,h)=>Mathf.Lerp(SueloBarrio(z),z<276?104:112,
            (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(78,100,Mathf.Abs(x))))*
            Mathf.SmoothStep(0,1,Mathf.InverseLerp(230,244,z))*
            (z<276?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(268,276,z)):Mathf.SmoothStep(0,1,Mathf.InverseLerp(276,283,z))));
        zona.grupo=new GameObject("Reino — barrio de montaña y explanada real").transform;zona.grupo.SetParent(raiz);
        PiezaUrbana(zona.grupo,Pack+"Main Structures/Wall/Castle01_a01.prefab","Castillo — entrada despejada",new Vector3(0,112,338),Vector3.back,true,44);
        // Revisión 23: muralla con torres y puerta (Raúl: "sigue sin gustarme el Reino" — un llano con casitas sueltas no
        // lee como reino). La muralla se levanta tras el terreno en `VestirEspacios` (`MurallaDelReino`); aquí solo se
        // reserva el trazado para que las casas complementarias no la pisen.
        foreach(var q in MurallaTrazado){despejesMuralla.Add(new Vector3(q.x,SueloBarrio(q.y),q.y));}
        Plaza(new Vector3(0,112,299),new Vector2(40,28),"Plaza real — audiencia exterior y Demonio 2 (GDD 10–13)");
        Plaza(new Vector3(0,104,259),new Vector2(48,24),"Plaza de la taberna — encuentro y persecución (GDD 9)");
        // Manzanas trazadas: fachadas a calles paralelas y plaza central libre.
        string[] casasReino={"BuildingAT01","BuildingAT07","BuildingAT12","BuildingAT17","BuildingAT23"};
        int vivienda=0;
        foreach(float x in new[]{-72f,-36f,36f,72f})foreach(float z in new[]{286f,316f,337f})
        {
            float sentido=Mathf.Abs(x)>50?-Mathf.Sign(x):Mathf.Sign(x);
            PiezaUrbana(zona.grupo,Pack+"Building Combination/"+casasReino[vivienda%casasReino.Length]+".prefab",
                "Casa de la calle "+(x<0?"occidental ":"oriental ")+(++vivienda),new Vector3(x,112,z),Vector3.right*sentido);
        }
        foreach(var p in new[]{new Vector2(-70,249),new Vector2(-70,272),new Vector2(70,249),new Vector2(70,273),new Vector2(39,247),new Vector2(39,274)})
            PiezaUrbana(zona.grupo,Pack+"Building Combination/"+casasReino[vivienda++%casasReino.Length]+".prefab",
                "Casa del mercado "+vivienda,new Vector3(p.x,104,p.y),p.y<260?Vector3.forward:Vector3.back);
        PiezaUrbana(zona.grupo,Pack+"Building Combination/BuildingAT10.prefab","Taberna del mercado",new Vector3(-39,104,260),Vector3.right);
        PiezaUrbana(zona.grupo,Tiny+"BuildingUtilityDeco/Well01.prefab","Pozo de la plaza baja",new Vector3(15,104,263),Vector3.back);
        foreach(float x in new[]{-14f,0f,14f})
            PiezaUrbana(zona.grupo,Pack+"Main Structures/Tent/Tent02_a01.prefab","Puesto del mercado",new Vector3(x,104,247),Vector3.forward,true,6);
        // Se reservan los accesos interiores, sin simular que el castillo ya contiene esas escenas.
        var interior=new GameObject("PENDIENTE — sala del trono y calabozo (GDD 10–11)");interior.transform.SetParent(zona.grupo);interior.transform.position=new Vector3(0,112,355);
        informe.AppendLine("Montaña: viviendas reales a escala nativa, explanada de 40×28 m libre frente al castillo y ascenso mediante calles en curva; retirados los anillos concéntricos.");
    }

    static void ConstruirPuebloVecino(Zona zona)
    {
        // La ubicación exacta es una propuesta de nivel: el GDD pide un pueblo, no tres props sobre arena.
        zona.centro=new Vector3(330,23,-115);zona.mitad=new Vector2(52,43);
        zona.relieve=(x,z,h)=>23;
        zona.grupo=new GameObject("Pueblo vecino — terraza sobre la playa").transform;zona.grupo.SetParent(raiz);
        var puntos=new[]{new Vector2(-22,12),new Vector2(0,22),new Vector2(24,12),new Vector2(27,-12),new Vector2(-24,-13),new Vector2(4,-26)};
        for(int i=0;i<puntos.Length;i++)
        {
            var p=zona.centro+new Vector3(puntos[i].x,0,puntos[i].y);
            var dir=zona.centro-p;dir.y=0;
            PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[i]+".prefab","Vivienda del pueblo vecino "+(i+1),p,dir.normalized);
        }
        Plaza(zona.centro,new Vector2(30,24),"Plaza del pueblo vecino — GDD 15");
        PiezaUrbana(zona.grupo,Tiny+"BuildingUtilityDeco/Well01.prefab","Pozo del pueblo vecino",zona.centro+new Vector3(9,0,0),Vector3.back);
        informe.AppendLine("Pueblo vecino: seis viviendas identificadas, plaza y acceso desde la arboleda oriental. La playa queda libre de cercados y accesorios usados como falsas casas. Revisión 18: la casa del hechicero ya no está aquí — vive exiliado en la isla-jungla de enfrente.");
    }

    static void ConstruirPuerto(Zona zona)
    {
        zona.centro=new Vector3(270,5,-440);zona.mitad=new Vector2(68,57);
        zona.relieve=(x,z,h)=>Mathf.Lerp(5,-5,Mathf.SmoothStep(0,1,Mathf.InverseLerp(-447,-480,z)));
        zona.grupo=new GameObject("Pueblo pesquero — calles y muelles").transform;zona.grupo.SetParent(raiz);
        var puntos=new[]{new Vector2(-32,7),new Vector2(-60,50),new Vector2(15,22),new Vector2(35,8),new Vector2(-35,-12),new Vector2(35,-12),new Vector2(-50,22),new Vector2(50,24)};
        for(int i=0;i<puntos.Length;i++)
        {
            var p=zona.centro+new Vector3(puntos[i].x,0,puntos[i].y);
            PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[i]+".prefab","Casa de pescadores "+(i+1),p,Vector3.back);
        }
        Plaza(new Vector3(270,5,-433),new Vector2(24,31),"Plaza del puerto — acceso al embarcadero");
        maderaPuerto=Material("Madera de muelles",new Color(.34f,.20f,.095f));
        // Pasarela central y dos brazos. Los postes llegan al fondo; el tablero queda accesible desde tierra.
        for(int i=0;i<43;i++)BloqueUrbano(zona.grupo,"Tablón de embarcadero",new Vector3(270,3.55f,-452-i),new Vector3(6,.3f,.92f),maderaPuerto);
        for(int i=0;i<42;i++)BloqueUrbano(zona.grupo,"Tablón de muelle transversal",new Vector3(249+i,3.55f,-484),new Vector3(.92f,.3f,6),maderaPuerto);
        for(int i=0;i<8;i++)foreach(float lado in new[]{-3.5f,3.5f})BloqueUrbano(zona.grupo,"Pilote",new Vector3(270+lado,-.25f,-450-i*6),new Vector3(.55f,9,.55f),maderaPuerto);
        foreach(float x in new[]{249f,259f,279f,290f})foreach(float z in new[]{-480.5f,-487.5f})BloqueUrbano(zona.grupo,"Pilote transversal",new Vector3(x,-.25f,z),new Vector3(.55f,9,.55f),maderaPuerto);
        var rampa=BloqueUrbano(zona.grupo,"Rampa de acceso al muelle",new Vector3(270,4.30f,-455.5f),new Vector3(6,.35f,17),maderaPuerto);
        rampa.transform.rotation=Quaternion.Euler(-5,0,0);
        var barco=PiezaUrbana(zona.grupo,Pack+"Props/Ship/Ship01_a01.prefab","Velero del puerto",new Vector3(255,-.7f,-498),Vector3.back,false);
        barcos.Add(LimitesVisibles(barco));
        var bote=PiezaUrbana(zona.grupo,Pack+"Props/Ship/Boat01_a01.prefab","Barca de pesca",new Vector3(286,-.08f,-495),Vector3.back,false);
        barcos.Add(LimitesVisibles(bote));
        informe.AppendLine("Puerto reconstruido: ocho viviendas a escala nativa, calle a tierra, pasarela central con muelle transversal, pilotes hasta el fondo y dos embarcaciones separadas.");
    }

    static GameObject BloqueUrbano(Transform grupo,string nombre,Vector3 posicion,Vector3 tamano,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=nombre;go.transform.SetParent(grupo);
        go.transform.position=posicion;go.transform.localScale=tamano;go.GetComponent<MeshRenderer>().sharedMaterial=material;
        int capa=LayerMask.NameToLayer("Floor");if(capa>=0)go.layer=capa;
        go.isStatic=true;return go;
    }

    static float PesoSolar(Solar solar,float x,float z,float margen)
    {
        if(solar.natural){float r=new Vector2((x-solar.limites.center.x)/solar.limites.extents.x,(z-solar.limites.center.z)/solar.limites.extents.z).magnitude;return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01((r-1)*Mathf.Min(solar.limites.extents.x,solar.limites.extents.z)/margen));}
        float dx=Mathf.Max(0,Mathf.Abs(x-solar.limites.center.x)-solar.limites.extents.x-.7f);
        float dz=Mathf.Max(0,Mathf.Abs(z-solar.limites.center.z)-solar.limites.extents.z-.7f);
        return 1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Sqrt(dx*dx+dz*dz)/margen));
    }

    static void CorregirRasantesYApoyos(float[,] alturas)
    {
        int res=alturas.GetLength(0);
        for(int z=0;z<res;z++)for(int x=0;x<res;x++)
        {
            float wx=x/(float)(res-1)*1300-650,wz=z/(float)(res-1)*1300-650;
            float h=alturas[z,x]*300-25;var p=new Vector2(wx,wz);float nivel;
            float rio=Cercania(p,rioSuave,out nivel),arroyo=arroyoSuave!=null?Cercania(p,arroyoSuave,out nivel):999;
            if(rio>9&&arroyo>6)
            {
                float mejor=999,cota=0;
                foreach(var ruta in rutasSuaves){float y;float d=Cercania(p,ruta,out y);if(d<mejor){mejor=d;cota=y;}}
                // El ancho útil y su transición se respetan sobre el heightmap FINAL, después de copiar las demos.
                float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,20,mejor));h=Mathf.Lerp(h,cota,w);
                foreach(var solar in solares)h=Mathf.Lerp(h,solar.cota,PesoSolar(solar,wx,wz,solar.plaza?12:7));
                // Una transición de plaza o huerto no debe levantar el suelo bajo otra vivienda.
                foreach(var solar in solares)if(solar.edificio!=null)h=Mathf.Lerp(h,solar.cota,PesoSolar(solar,wx,wz,3));
                bool juntoCasa=false;foreach(var solar in solares)if(solar.edificio!=null&&PesoSolar(solar,wx,wz,5)>.01f){juntoCasa=true;break;}
                if(!juntoCasa)h=Mathf.Lerp(h,cota,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,9,mejor)));
            }
            alturas[z,x]=(h+25)/300;
        }
    }

    // Mezcla convexa: cada aplicación conserva pesos positivos cuya suma es uno.
    static void MezclarSuelo(float[,,] pesos,int z,int x,int capa,float peso)
    {
        peso=Mathf.Clamp01(peso);
        for(int c=0;c<pesos.GetLength(2);c++)pesos[z,x,c]*=1-peso;
        pesos[z,x,capa]+=peso;
    }

    static void PintarPlazas(float[,,] pesos)
    {
        int res=pesos.GetLength(0);
        for(int z=0;z<res;z++)for(int x=0;x<res;x++)
        {
            float wx=x/(float)(res-1)*1300-650,wz=z/(float)(res-1)*1300-650;
            foreach(var solar in solares)
            {
                if(!solar.plaza)continue;
                float peso=PesoSolar(solar,wx,wz,3);
                if(peso<=0)continue;
                bool reino=Mathf.Abs(solar.limites.center.x)<120&&solar.limites.center.z>235;
                int capa=solar.natural?10:8;
                // Marco pétreo y paño claro en las plazas del castillo.
                if(reino&&!solar.natural)
                {
                    var b=solar.limites;
                    float borde=Mathf.Min(b.extents.x-Mathf.Abs(wx-b.center.x),b.extents.z-Mathf.Abs(wz-b.center.z));
                    MezclarSuelo(pesos,z,x,8,peso*.98f);
                    MezclarSuelo(pesos,z,x,11,Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,4,borde))*.9f);
                }
                else MezclarSuelo(pesos,z,x,capa,peso*(solar.natural?.65f:.92f));
            }
        }
    }

    static void PintarPueblos(float[,,] pesos)
    {
        int res=pesos.GetLength(0),pintadas=0;
        for(int z=0;z<res;z++)for(int x=0;x<res;x++)
        {
            float wx=x/(float)(res-1)*1300-650,wz=z/(float)(res-1)*1300-650,w=0;
            foreach(var zona in Zonas)
            {
                if(zona.demo!=null||zona.esPoza||zona.mitad.x<1)continue;
                float r=new Vector2((wx-zona.centro.x)/zona.mitad.x,(wz-zona.centro.z)/zona.mitad.y).magnitude;
                w=Mathf.Max(w,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,1.12f,r)));
            }
            if(w<=.01f)continue;
            bool reino=Mathf.Abs(wx)<125&&wz>235&&wz<370;
            float junto=0;
            foreach(var solar in solares)if(solar.edificio!=null)junto=Mathf.Max(junto,PesoSolar(solar,wx,wz,4));
            float distancia=DistanciaCaminos(wx,wz);
            float calle=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,5.3f,distancia));
            // Patios de tierra y accesos usados; los espacios entre barrios conservan vegetación.
            MezclarSuelo(pesos,z,x,10,junto*w*.85f);
            MezclarSuelo(pesos,z,x,reino?8:9,calle*w*.96f);
            // Umbrales pétreos cosen las viviendas al espacio público sin pavimentar todo el recinto.
            MezclarSuelo(pesos,z,x,reino?8:9,junto*w*(reino?.65f:.38f));
            pintadas++;
        }
        informe.AppendLine($"Suelo urbano: {pintadas} celdas; piedra en calles del Reino, plazas enmarcadas, tierra y adoquín en pueblos. Capas reales del pack; mezclas convexas.");
    }

    // Revisión 22: huerto vallado — cultivos en filas (mismo prefab que las huertas del pueblo inicial) y cerca de madera
    // de RPG Tiny alrededor. `ancho`/`largo` en metros; la cerca se mide una vez por bounds para encadenar tramos.
    static float trampCerca=-1;
    static void Huerto(Transform grupo,string nombre,Vector3 centro,float ancho,float largo,float giroGrados=0)
    {
        var raizHuerto=new GameObject(nombre).transform;raizHuerto.SetParent(grupo);raizHuerto.position=centro;
        var giro=Quaternion.Euler(0,giroGrados,0);
        Vector3 Local(float dx,float dz){var p=centro+giro*new Vector3(dx,0,dz);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;return p;}
        int filas=Mathf.Max(1,Mathf.FloorToInt((ancho-2)/2f)),plantas=Mathf.Max(1,Mathf.FloorToInt((largo-2)/2.2f));
        for(int f=0;f<filas;f++)for(int q=0;q<plantas;q++)
            PiezaUrbana(raizHuerto,Pack+"Vegetation/Flower02_a01.prefab","Cultivo",Local(-ancho/2+1.5f+f*2f,-largo/2+1.5f+q*2.2f),giro*Vector3.back,false,1.7f); // revisión 23: cultivos a 1,7 m (antes se veían como puntos)
        var cerca=AssetDatabase.LoadAssetAtPath<GameObject>(Tiny+"BuildingUtilityDeco/WoodFence01.prefab");
        if(cerca==null){informe.AppendLine("Huerto sin cerca: WoodFence01 no encontrado.");return;}
        if(trampCerca<0){var tmp=(GameObject)PrefabUtility.InstantiatePrefab(cerca);var b=LimitesVisibles(tmp);trampCerca=Mathf.Max(b.size.x,b.size.z);Object.DestroyImmediate(tmp);if(trampCerca<.5f)trampCerca=2;}
        void Lado(Vector3 a,Vector3 b)
        {
            var d=b-a;d.y=0;float L=d.magnitude;if(L<.5f)return;d/=L;int n=Mathf.Max(1,Mathf.RoundToInt(L/trampCerca));
            for(int i=0;i<n;i++)
            {
                var p=a+d*((i+.5f)*L/n);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;
                if(DistanciaCaminos(p.x,p.z)<5+L/n*.5f)continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(cerca,raizHuerto);go.name="Cerca";
                go.transform.rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,d));
                var bb=LimitesVisibles(go);float lado=Mathf.Max(bb.size.x,bb.size.z);if(lado>.1f)go.transform.localScale*=(L/n)/lado;
                bb=LimitesVisibles(go);go.transform.position+=new Vector3(p.x-bb.center.x,p.y-bb.min.y,p.z-bb.center.z);
            }
        }
        var c1=Local(-ancho/2,-largo/2);var c2=Local(ancho/2,-largo/2);var c3=Local(ancho/2,largo/2);var c4=Local(-ancho/2,largo/2);
        Lado(c1,c2);Lado(c2,c3);Lado(c3,c4);Lado(c4,c1);
        solares.Add(new Solar{limites=new Bounds(centro,new Vector3(ancho,1,largo)),cota=centro.y,plaza=true,natural=true});
    }

    // Revisión 22: granjas al pie de la cascada. Dos casas de labranza, pozo, tres huertos vallados y un almiar de barriles.
    static void ConstruirGranjaCascada(Zona zona)
    {
        zona.mitad=new Vector2(40,30);
        zona.relieve=(x,z,h)=>zona.centro.y;
        zona.grupo=new GameObject(zona.nombre).transform;zona.grupo.SetParent(raiz);zona.grupo.position=zona.centro;
        var c=zona.centro;
        PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[3]+".prefab","Casa de labranza 1 — campo de Erika",c+new Vector3(-40,0,10),Vector3.right);
        PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[6]+".prefab","Casa de labranza 2",c+new Vector3(22,0,-12),Vector3.left);
        PiezaUrbana(zona.grupo,Tiny+"BuildingUtilityDeco/Well01.prefab","Pozo de las granjas",c+new Vector3(-2,0,-2),Vector3.back);
        for(int i=0;i<4;i++)PiezaUrbana(zona.grupo,Pack+"Props/Goods/Barrel01_a01.prefab","Barril de la granja",c+new Vector3(-14+i*1.3f,0,-10),Vector3.back,false);
        // Reservas de los huertos (se plantan en `VestirEspacios`, tras el terreno): llanas y sin árboles.
        Plaza(c+new Vector3(4,0,16),new Vector2(22,12),"Huerto grande — campo de Erika");
        Plaza(c+new Vector3(-42,0,-8),new Vector2(12,10),"Huerto de la casa 1");
        Plaza(c+new Vector3(24,0,4),new Vector2(14,10),"Huerto junto al arroyo"); // revisión 28: alejado de "Huerto grande" (se montaban)
        informe.AppendLine($"Granjas de la cascada en {c}: dos casas, pozo y huertos vallados (los huertos se plantan tras el terreno, en `VestirEspacios`).");
    }

    static bool EnSolar(float x,float z)
    {
        foreach(var solar in solares)if(PesoSolar(solar,x,z,3)>.01f)return true;
        return false;
    }

    static void AsentarArbolado()
    {
        var lista=new List<Transform>();
        foreach(var t in raiz.GetComponentsInChildren<Transform>())
        {
            if(!t.name.StartsWith("Tree",StringComparison.OrdinalIgnoreCase))continue;
            bool integrado=false;
            for(var p=t.parent;p!=null&&p!=raiz;p=p.parent)
                if(p.name.StartsWith("Tree",StringComparison.OrdinalIgnoreCase)||p.name.StartsWith("Building",StringComparison.OrdinalIgnoreCase)||p.name.StartsWith("Vivienda")||p.name.StartsWith("Casa ")){integrado=true;break;}
            if(!integrado)lista.Add(t);
        }
        int asentados=0,retirados=0;
        foreach(var t in lista)
        {
            if(t==null)continue;var b=LimitesVisibles(t.gameObject);if(b.size.y<.1f)continue;
            float x=b.center.x,z=b.center.z;
            if(terreno.terrainData.GetSteepness((x+650)/1300,(z+650)/1300)>32||EnSolar(x,z))
            {Object.DestroyImmediate(t.gameObject);retirados++;continue;}
            float y=terreno.SampleHeight(b.center)+terreno.transform.position.y;
            t.position+=Vector3.up*(y-b.min.y-.12f);asentados++;
        }
        informe.AppendLine($"Apoyo de vegetación: {asentados} árboles asentados por base visible; {retirados} retirados de taludes o parcelas.");
    }

    static void VerificarUrbanismo()
    {
        Physics.SyncTransforms();int fallos=0;
        foreach(var solar in solares)
        {
            if(solar.edificio==null)continue;
            var b=LimitesVisibles(solar.edificio);
            float peor=0;
            foreach(float u in new[]{-.8f,0,.8f})foreach(float v in new[]{-.8f,0,.8f})
            {
                var p=b.center+new Vector3(u*b.extents.x,0,v*b.extents.z);
                float h=terreno.SampleHeight(p)+terreno.transform.position.y;
                peor=Mathf.Max(peor,Mathf.Abs(h-b.min.y));
            }
            if(peor>.45f){fallos++;informe.AppendLine($"REVISAR apoyo: {solar.edificio.name}, diferencia máxima {peor:0.00} m.");}
        }
        // Rasante real a intervalos de dos metros: las cotas de diseño solas no bastan.
        // Revisión 17: antes solo se comprobaban los caminos 3 y 7 (de 7) — se amplía a todos, porque una
        // subida por encima del objetivo puede aparecer en cualquiera al mezclarse con el relieve de una zona.
        for(int r=0;r<rutasSuaves.Length;r++)
        {
            float maxima=0;Vector3 peor=Vector3.zero;Vector3 anterior=Vector3.zero;bool primero=true;
            foreach(var p in MuestrearRecorrido(rutasSuaves[r],2))
            {
                var q=p;q.y=AlturaTransitable(p);
                if(!primero){float horizontal=Vector2.Distance(new Vector2(q.x,q.z),new Vector2(anterior.x,anterior.z));if(horizontal>.2f){float pendiente=Mathf.Abs(q.y-anterior.y)/horizontal;if(pendiente>maxima){maxima=pendiente;peor=q;}}}
                anterior=q;primero=false;
            }
            informe.AppendLine($"Camino {r+1}, pendiente REAL sobre terreno/tablero: {maxima:P1} en {peor}.");
            if(maxima>.25f){fallos++;informe.AppendLine("REVISAR: ascenso real por encima del objetivo del 25 %.");}
        }
        informe.AppendLine($"Urbanismo: {solares.Count} parcelas/explanadas, {fallos} comprobaciones pendientes. Las comprobaciones geométricas no sustituyen el recorrido del jugador.");
        VerificarPasosLibres();
    }

    static void VerificarPasosLibres()
    {
        // Comprobación geométrica de gálibo; no ejecuta ni sustituye al controlador Invector.
        var contactos=new Collider[256];var obstaculos=new HashSet<Collider>();int muestras=0,saturaciones=0;
        for(int r=0;r<rutasSuaves.Length;r++)
        {
            foreach(var p in MuestrearRecorrido(rutasSuaves[r],.5f))
            {
                float suelo=AlturaTransitable(p);muestras++;
                var pie=new Vector3(p.x,suelo+.12f,p.z);
                int n=Physics.OverlapCapsuleNonAlloc(pie+Vector3.up*.35f,pie+Vector3.up*1.45f,.35f,contactos,~0,QueryTriggerInteraction.Ignore);
                if(n==contactos.Length)saturaciones++;
                for(int i=0;i<n;i++)
                {
                    var c=contactos[i];
                    if(c==null||c.gameObject.scene!=raiz.gameObject.scene||c is TerrainCollider||c.bounds.max.y<=suelo+.15f)continue;
                    if(obstaculos.Add(c))informe.AppendLine($"REVISAR paso camino {r+1}: {c.name}, en {p}. La cápsula de comprobación mide 0,70 × 1,80 m.");
                }
            }
        }
        informe.AppendLine($"Gálibo de caminos: {muestras} muestras cada 0,5 m, {obstaculos.Count} colisionadores a revisar, {saturaciones} consultas saturadas. No equivale a una prueba en Play.");
    }

    static IEnumerable<Vector3> MuestrearRecorrido(Vector3[] ruta,float paso)
    {
        for(int i=1;i<ruta.Length;i++)
        {
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(ruta[i-1],ruta[i])/paso));
            for(int j=i==1?0:1;j<=n;j++)yield return Vector3.Lerp(ruta[i-1],ruta[i],j/(float)n);
        }
    }

    static float PesoPinturaIntegrada(Zona zona,Terrain t,float x,float z)
    {
        float basePeso=PesoTransferencia(zona,t,x,z);
        float r=new Vector2((x-zona.centro.x)/zona.mitad.x,(z-zona.centro.z)/zona.mitad.y).magnitude;
        float borde=r+(Mathf.PerlinNoise(x*.047f+8,z*.047f+19)-.5f)*.15f;
        return basePeso*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,1.18f,borde)));
    }

    static readonly Dictionary<TerrainLayer,TerrainLayer> capasIntegradas=new Dictionary<TerrainLayer,TerrainLayer>();
    static TerrainLayer CapaIntegrada(TerrainLayer original)
    {
        TerrainLayer capa;if(capasIntegradas.TryGetValue(original,out capa))return capa;
        capa=Object.Instantiate(original);capa.name=original.name+" — integrada";
        // La textura, sus dibujos y el tamaño de mosaico se conservan. Se modula solo el albedo de la copia.
        capa.diffuseRemapMin=Vector4.zero;capa.diffuseRemapMax=new Vector4(.74f,.77f,.70f,1);
        AssetDatabase.CreateAsset(capa,carpeta+"/SueloPueblo_"+capasIntegradas.Count+".terrainlayer");
        capasIntegradas.Add(original,capa);return capa;
    }
}
