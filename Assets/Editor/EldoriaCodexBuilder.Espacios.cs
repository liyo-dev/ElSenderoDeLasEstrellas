using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EldoriaCodexBuilder
{
    static float[,] distanciasSenderos;
    static Material piedraRuinas;
    static readonly List<MeshCollider> tablerosPuentes=new List<MeshCollider>();
    static readonly List<Vector3[]> callesUrbanas=new List<Vector3[]>();

    static void PrepararEspacios()
    {
        piedraRuinas=null;tablerosPuentes.Clear();callesUrbanas.Clear();
        // Chaikin mueve los nodos interiores: los ramales se unen a la línea suavizada real.
        Vector3 Empalme(Vector3[] ruta,Vector3 p)
        {
            float mejor=float.MaxValue;Vector3 resultado=p;
            for(int i=1;i<ruta.Length;i++){float t;float d=DistanciaSegmento(new Vector2(p.x,p.z),new Vector2(ruta[i-1].x,ruta[i-1].z),new Vector2(ruta[i].x,ruta[i].z),out t);if(d<mejor){mejor=d;resultado=Vector3.Lerp(ruta[i-1],ruta[i],t);}}
            return resultado;
        }
        var regreso=(Vector3[])Caminos[7].Clone();regreso[0]=Empalme(rutasSuaves[3],regreso[0]);regreso[regreso.Length-1]=Empalme(rutasSuaves[2],regreso[regreso.Length-1]);rutasSuaves[7]=Suavizar(regreso);
        Plaza(new Vector3(-330,36,65),new Vector2(34,30),"Claro de Estela — escenario GDD 7");
        Plaza(new Vector3(-95,32,65),new Vector2(36,28),"Descanso del camino — arena Golem GDD 8");
        Plaza(new Vector3(-62,25,-61),new Vector2(32,25),"Pradera del despertar — arena Demonio 1 GDD 4–5");
        for(int i=0;i<3;i++)Plaza(new Vector3(84+i*18,24,-177),new Vector2(12,24),"Huerta del pueblo "+(i+1));
        // Revisión 22: reservas de los huertos vallados de puerto, pueblo vecino y Reino (se plantan en `VestirEspacios`).
        Plaza(new Vector3(232,5,-405),new Vector2(12,9),"Huerto del puerto");
        Plaza(new Vector3(300,23,-140),new Vector2(12,9),"Huerto del pueblo vecino");
        Plaza(new Vector3(360,23,-92),new Vector2(10,9),"Huerto del pueblo vecino 2");
        Plaza(new Vector3(-30,SueloBarrio(240),240),new Vector2(12,9),"Huerto del Reino — terraza baja");
        CompletarCasas(Zonas[5],new Vector2(-88,246),new Vector2(96,342),26,811); // revisión 23: más casas, dentro de la muralla
        CompletarCasas(Zonas[1],new Vector2(220,-437),new Vector2(320,-395),8,812);
        CompletarCasas(Zonas[4],new Vector2(289,-150),new Vector2(371,-78),6,813);
        foreach(var solar in solares)
        {
            if(solar.edificio==null)continue;
            var inicio=solar.limites.center;float distancia=60;var fin=inicio;
            foreach(var ruta in rutasSuaves)for(int i=1;i<ruta.Length;i++)
            {
                float t;float d=DistanciaSegmento(new Vector2(inicio.x,inicio.z),new Vector2(ruta[i-1].x,ruta[i-1].z),new Vector2(ruta[i].x,ruta[i].z),out t);
                if(d<distancia){distancia=d;fin=Vector3.Lerp(ruta[i-1],ruta[i],t);}
            }
            if(distancia>=60||distancia<1||Mathf.Abs(solar.cota-fin.y)/distancia>.20f)continue;
            bool libre=true;
            for(float t=0;t<=1&&libre;t+=.05f)
            {
                var p=Vector3.Lerp(inicio,fin,t);
                foreach(var otro in solares)
                    if(otro!=solar&&otro.edificio!=null&&Mathf.Abs(p.x-otro.limites.center.x)<otro.limites.extents.x+1.2f&&Mathf.Abs(p.z-otro.limites.center.z)<otro.limites.extents.z+1.2f){libre=false;break;}
            }
            if(libre)callesUrbanas.Add(new[]{inicio,fin});
        }
        informe.AppendLine("Calles secundarias pintadas sin atravesar otras viviendas: "+callesUrbanas.Count+".");
    }

    static void CompletarCasas(Zona zona,Vector2 min,Vector2 max,int cantidad,int semilla)
    {
        var azar=new System.Random(semilla);int puestas=0;
        for(int intento=0;intento<300&&puestas<cantidad;intento++)
        {
            float x=Mathf.Lerp(min.x,max.x,(float)azar.NextDouble()),z=Mathf.Lerp(min.y,max.y,(float)azar.NextDouble());
            if(DistanciaCaminos(x,z)<10)continue;
            if(zona==Zonas[5]){bool cerca=false;foreach(var m in despejesMuralla)if(Vector2.Distance(new Vector2(x,z),new Vector2(m.x,m.z))<9){cerca=true;break;}if(cerca)continue;} // revisión 23: no pisar la muralla
            float y=zona==Zonas[5]?SueloBarrio(z):zona.centro.y;
            var go=PiezaUrbana(zona.grupo,Pack+"Building Combination/"+Viviendas[azar.Next(Viviendas.Length)]+".prefab","Vivienda complementaria "+(puestas+1),new Vector3(x,y,z),x<zona.centro.x?Vector3.right:Vector3.left,false);
            var b=LimitesVisibles(go);var holgura=b;holgura.Expand(new Vector3(8,0,8));bool libre=true;
            if(DistanciaCaminos(b.center.x,b.center.z)<Mathf.Max(b.extents.x,b.extents.z)+10)libre=false;
            foreach(var solar in solares)
                if(Mathf.Abs(holgura.center.x-solar.limites.center.x)<holgura.extents.x+solar.limites.extents.x&&Mathf.Abs(holgura.center.z-solar.limites.center.z)<holgura.extents.z+solar.limites.extents.z){libre=false;break;}
            if(!libre){Object.DestroyImmediate(go);continue;}
            solares.Add(new Solar{limites=b,cota=y,edificio=go});puestas++;
        }
        informe.AppendLine(zona.nombre+": "+puestas+" viviendas adicionales, con separación de parcelas y calles.");
    }

    static void PuenteTransitable(Transform padre,string nombre,Vector3 centro,Vector3 direccion,Vector3[] cauce)
    {
        direccion.y=0;direccion.Normalize();Vector3 lateral=Vector3.Cross(Vector3.up,direccion);float agua;
        Cercania(new Vector2(centro.x,centro.z),cauce,out agua);
        float BuscarOrilla(float signo)
        {
            for(float d=8;d<=45;d+=.5f)
            {
                var q=centro+direccion*d*signo;float nivel;
                // La orilla se determina por el ancho del cauce, no por la cota de agua del centro:
                // en un arroyo inclinado aquella cota alargaba artificialmente el puente aguas abajo.
                if(Cercania(new Vector2(q.x,q.z),cauce,out nivel)>(cauce==rioSuave?11:8))return d+3;
            }
            return 45;
        }
        float antes=BuscarOrilla(-1),despues=BuscarOrilla(1),largo=antes+despues;
        var a=centro-direccion*antes;var b=centro+direccion*despues;
        a.y=terreno.SampleHeight(a)+terreno.transform.position.y+.025f;b.y=terreno.SampleHeight(b)+terreno.transform.position.y+.025f;
        if(maderaPuerto==null)maderaPuerto=Material("Madera de pasos",new Color(.34f,.20f,.095f));
        var grupo=new GameObject(nombre+" — tablero de 4 m y accesos a ras").transform;grupo.SetParent(padre);
        var vertices=new List<Vector3>();var indices=new List<int>();int n=Mathf.CeilToInt(largo/.7f);
        Vector3 Punto(float t)=>Vector3.Lerp(a,b,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.14f);
        for(int i=0;i<=n;i++)
        {
            var p=Punto(i/(float)n);vertices.Add(p-lateral*2);vertices.Add(p+lateral*2);
            if(i>0){int k=i*2;indices.AddRange(new[]{k-2,k,k-1,k-1,k,k+1});}
            if(i==n)continue;
            var q=Punto((i+1)/(float)n);var tablón=BloqueUrbano(grupo,"Tablón",(p+q)*.5f-Vector3.up*.12f,new Vector3(4,.24f,Vector3.Distance(p,q)+.015f),maderaPuerto);
            tablón.transform.rotation=Quaternion.LookRotation(q-p);Object.DestroyImmediate(tablón.GetComponent<Collider>());
            if(i%5==0&&i>1&&i<n-2)foreach(float lado in new[]{-1f,1f})
            {
                var pie=p+lateral*(lado*2.15f);
                BloqueUrbano(grupo,"Poste de baranda",pie+Vector3.up*.52f,new Vector3(.16f,1.15f,.16f),maderaPuerto);
            }
        }
        foreach(float lado in new[]{-1f,1f})
        {
            var inicio=Punto(.04f)+lateral*lado*2.15f+Vector3.up*.98f;var fin=Punto(.96f)+lateral*lado*2.15f+Vector3.up*.98f;
            var pasamanos=BloqueUrbano(grupo,"Pasamanos",(inicio+fin)*.5f,new Vector3(.12f,.12f,Vector3.Distance(inicio,fin)),maderaPuerto);pasamanos.transform.rotation=Quaternion.LookRotation(fin-inicio);
        }
        var mesh=new Mesh{name=nombre+" colisión continua"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh,carpeta+"/"+nombre+"_paso.asset");var colision=grupo.gameObject.AddComponent<MeshCollider>();colision.sharedMesh=mesh;tablerosPuentes.Add(colision);
        int capa=LayerMask.NameToLayer("Floor");if(capa>=0)grupo.gameObject.layer=capa;
        for(float d=-antes;d<=despues;d+=3){var p=centro+direccion*d;despejes.Add(new Vector3(p.x,p.z,4));}
        informe.AppendLine($"{nombre}: ancho útil 4 m, longitud {largo:0.0} m, pendiente {Mathf.Abs(b.y-a.y)/largo:P1}; unión al terreno 0,025 m en ambos extremos. Colisión continua sin escalones entre tablones.");
    }

    static float AlturaTransitable(Vector3 p)
    {
        float h=terreno.SampleHeight(p)+terreno.transform.position.y;
        foreach(var tablero in tablerosPuentes)
        {
            RaycastHit impacto;
            if(tablero.Raycast(new Ray(new Vector3(p.x,400,p.z),Vector3.down),out impacto,450))h=Mathf.Max(h,impacto.point.y);
        }
        return h;
    }

    static void RuinasAncestrales(Transform padre,Zona zona)
    {
        piedraRuinas=Material("Piedra antigua",new Color(.36f,.40f,.38f));
        var grupo=new GameObject("Santuario ancestral — recinto de piedra y ruinas").transform;grupo.SetParent(padre);
        var c=zona.centro;
        var monolito=Colocar(grupo,Tiny+"Rock/Rock06.prefab","Piedra ancestral — monolito",c,Vector3.back,4.2f,true,c.y,0);
        if(monolito!=null)foreach(var renderer in monolito.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=piedraRuinas;
        for(int i=0;i<20;i++)
        {
            float a=i*Mathf.PI*2/20;if(i>11&&i<18)continue;
            var p=c+new Vector3(Mathf.Cos(a)*13,0,Mathf.Sin(a)*13);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;
            float alto=i%4==0?2.2f:i%3==0?.65f:1.25f;
            var bloque=BloqueUrbano(grupo,"Muro derruido",p+Vector3.up*alto*.5f,new Vector3(3,alto,1.2f),piedraRuinas);bloque.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg+90,i%5==0?7:0);
        }
        BloqueUrbano(grupo,"Dintel conservado del santuario",c+new Vector3(0,4.1f,-11),new Vector3(16.2f,.7f,1.3f),piedraRuinas);
        foreach(float lado in new[]{-1f,1f})
        {
            var p=c+new Vector3(lado*7.5f,0,-11);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;
            BloqueUrbano(grupo,"Jamba de acceso al santuario",p+Vector3.up*2,new Vector3(1.2f,4,1.2f),piedraRuinas);
        }
        despejes.Add(new Vector3(c.x,c.z,20));
        informe.AppendLine("Santuario ancestral: monolito de piedra, muro derruido con entrada y espacio central; retirado el cristal rosa provisional. Guardián y libro pendientes de conexión.");
    }

    static void VestirEspacios()
    {
        var grupo=new GameObject("Vida de los pueblos — huertas, enseres y claros").transform;grupo.SetParent(raiz);
        void Prop(string ruta,string nombre,Vector3 p,Vector3 frente)
        {
            p.y=terreno.SampleHeight(p)+terreno.transform.position.y;
            PiezaUrbana(grupo,Pack+ruta,nombre,p,frente,false);
        }
        foreach(var solar in solares)
        {
            if(solar.edificio==null||solar.edificio.name.StartsWith("Castillo"))continue;
            var p=solar.limites.center+new Vector3(solar.limites.extents.x+1.4f,0,0);
            Prop("Vegetation/Flowerpot/Flowerpot01_b03.prefab","Maceta junto a vivienda",p,Vector3.back);
        }
        for(int i=0;i<8;i++)Prop("Props/Goods/Barrel01_a01.prefab","Barriles del puerto",new Vector3(258+i%4*1.2f,0,-448+i/4*1.3f),Vector3.back);
        // Mobiliario en bordes de plaza; los ejes centrales permanecen despejados.
        foreach(var p in new[]{new Vector3(-16,0,290),new Vector3(16,0,290),new Vector3(-16,0,308),new Vector3(16,0,308),new Vector3(260,0,-443),new Vector3(280,0,-443),new Vector3(318,0,-123),new Vector3(342,0,-123)})
            Prop("Props/Lighting/Light01_a01.prefab","Farol de plaza",p,Vector3.back);
        foreach(var p in new[]{new Vector3(-12,0,256),new Vector3(-12,0,264),new Vector3(279,0,-427),new Vector3(339,0,-110)})
        {
            Prop("Props/Furniture/Table/Table01_a01.prefab","Mesa de encuentro",p,Vector3.back);
            Prop("Props/Furniture/Chair/Chair02_a01.prefab","Asiento de plaza",p+Vector3.left*1.8f,Vector3.right);
            Prop("Props/Furniture/Chair/Chair02_a01.prefab","Asiento de plaza",p+Vector3.right*1.8f,Vector3.left);
        }
        for(int huerta=0;huerta<3;huerta++)for(int fila=0;fila<5;fila++)for(int planta=0;planta<9;planta++)
            Prop("Vegetation/Flower02_a01.prefab","Cultivo",new Vector3(80+huerta*18+fila*2,0,-187+planta*2.3f),Vector3.back);
        // Revisión 22: huertos vallados en los pueblos hechos a mano (tenían hierba lisa y leían como placeholder).
        foreach(var zona in Zonas)if(zona.esGranja)
        {
            var c=zona.centro;
            Huerto(grupo,"Huerto grande — campo de Erika",c+new Vector3(4,0,16),22,12,0);
            Huerto(grupo,"Huerto de la casa 1",c+new Vector3(-42,0,-8),12,10,0);
            Huerto(grupo,"Huerto junto al arroyo",c+new Vector3(24,0,4),14,10,12); // revisión 28: alejado de "Huerto grande" (se montaban)
        }
        Huerto(grupo,"Huerto del puerto",new Vector3(232,0,-405),12,9,0);
        Huerto(grupo,"Huerto del pueblo vecino",new Vector3(300,0,-140),12,9,0);
        Huerto(grupo,"Huerto del pueblo vecino 2",new Vector3(360,0,-92),10,9,20);
        Huerto(grupo,"Huerto del Reino — terraza baja",new Vector3(-30,0,240),12,9,0);
        MurallaDelReino(grupo); // revisión 23
        var roca=Material("Roca chamuscada del claro",new Color(.20f,.23f,.22f));
        for(int i=0;i<8;i++)
        {
            float a=i*Mathf.PI*2/8;var p=new Vector3(-330+Mathf.Cos(a)*19,0,65+Mathf.Sin(a)*17);p.y=terreno.SampleHeight(p)+terreno.transform.position.y;
            var tronco=BloqueUrbano(grupo,"Tronco quemado — huella del paso de Estela",p+Vector3.up*1.7f,new Vector3(.7f,3.4f,.65f),roca);tronco.transform.rotation=Quaternion.Euler(5,i*43,8);
        }
    }
}
