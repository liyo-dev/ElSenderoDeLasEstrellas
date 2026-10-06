# Mercancía del puesto de Tomasa (tallista): figuritas, estrella torcida, pájaros, farolillos,
# campanillas y guirnalda. Coordenadas locales del mostrador Counter01 (Blender, Z arriba, -Y = cliente).
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

PALETA = [  # sRGB
 (0xE3,0xB5,0x77),(0xC9,0x8E,0x4E),(0x8A,0x5A,0x2E),(0xD9,0x46,0x3B),
 (0xF2,0xC1,0x4E),(0x3F,0xA7,0xA0),(0x4A,0x78,0xC2),(0xD4,0xA3,0x2F),
 (0x9C,0x74,0x20),(0xEF,0xE3,0xC4),(0xF5,0xF1,0xE6),(0x6B,0xAF,0x4A),
 (0x2B,0x24,0x20),(0xE8,0x8A,0x2E),(0xE0,0x7A,0x9A),(0x8E,0x5B,0xB5)]
MAD_CLARA,MAD_MEDIA,MAD_OSC,ROJO,AMARILLO,TURQ,AZUL,LATON,LATON_OSC,CUERDA,BLANCO,VERDE,NEGRO,NARANJA,ROSA,MORADO=range(16)
CELDAS=4; TAM=64  # 4x4 celdas de 16 px

def uv_celda(c):
    return ((c%CELDAS)+0.5)/CELDAS, 1-((c//CELDAS)+0.5)/CELDAS

def crear_paleta(ruta):
    img=bpy.data.images.new("PaletaTomasa",TAM,TAM,alpha=False)
    px=[0.0]*(TAM*TAM*4); cs=TAM//CELDAS
    for y in range(TAM):
        for x in range(TAM):
            c=(CELDAS-1-y//cs)*CELDAS + x//cs
            r,g,b=PALETA[c]; i=(y*TAM+x)*4
            px[i:i+4]=[r/255,g/255,b/255,1]
    img.pixels=px; img.filepath_raw=ruta; img.file_format='PNG'; img.save()
    return img

# ---------- primitivas (bmesh) con color ----------
def _obj(nombre,bm,color,suave=False):
    me=bpy.data.meshes.new(nombre); bm.to_mesh(me); bm.free()
    uv=me.uv_layers.new(name="UVMap"); u=uv_celda(color)
    for d in uv.data: d.uv=u
    for p in me.polygons: p.use_smooth=suave
    o=bpy.data.objects.new(nombre,me); bpy.context.scene.collection.objects.link(o); return o

def caja(sx,sy,sz,color,loc=(0,0,0),rot=(0,0,0)):
    bm=bmesh.new(); bmesh.ops.create_cube(bm,size=1)
    bmesh.ops.scale(bm,vec=(sx,sy,sz),verts=bm.verts)
    o=_obj("caja",bm,color); o.location=loc; o.rotation_euler=rot; return o

def cilindro(r1,r2,h,color,seg=8,loc=(0,0,0),rot=(0,0,0),suave=False):
    bm=bmesh.new()
    bmesh.ops.create_cone(bm,cap_ends=True,segments=seg,radius1=r1,radius2=r2,depth=h)
    o=_obj("cil",bm,color,suave); o.location=loc; o.rotation_euler=rot; return o

def esfera(r,color,loc=(0,0,0),esc=(1,1,1),rot=(0,0,0),seg=8,anillos=6):
    bm=bmesh.new(); bmesh.ops.create_uvsphere(bm,u_segments=seg,v_segments=anillos,radius=r)
    o=_obj("esf",bm,color,True); o.location=loc; o.scale=esc; o.rotation_euler=rot; return o

def torno(perfil,color,seg=10,loc=(0,0,0),rot=(0,0,0)):
    """perfil: [(radio, z)] de arriba abajo; revoluciona alrededor de Z."""
    bm=bmesh.new(); anillos=[]
    for r,z in perfil:
        anillos.append([bm.verts.new((r*math.cos(2*math.pi*i/seg),r*math.sin(2*math.pi*i/seg),z)) for i in range(seg)])
    for a,b in zip(anillos,anillos[1:]):
        for i in range(seg):
            j=(i+1)%seg
            try: bm.faces.new((a[i],a[j],b[j],b[i]))
            except ValueError: pass
    bm.faces.new(list(reversed(anillos[0]))); bm.faces.new(anillos[-1])
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-5)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    o=_obj("torno",bm,color,True); o.location=loc; o.rotation_euler=rot; return o

def prisma(puntos2d,grosor,color,loc=(0,0,0),rot=(0,0,0)):
    """Polígono en el plano XZ extruido en Y (cara hacia -Y)."""
    bm=bmesh.new()
    f=bm.faces.new([bm.verts.new((x,-grosor/2,z)) for x,z in puntos2d])
    r=bmesh.ops.extrude_face_region(bm,geom=[f])
    vs=[e for e in r['geom'] if isinstance(e,bmesh.types.BMVert)]
    bmesh.ops.translate(bm,vec=(0,grosor,0),verts=vs)
    bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
    o=_obj("prisma",bm,color); o.location=loc; o.rotation_euler=rot; return o

def unir(nombre,objs,loc=None,rot=None):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.object.join(); o=bpy.context.active_object; o.name=nombre; o.data.name=nombre
    # pivote abajo en el centro
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    if loc is not None or rot is not None:
        if rot: o.rotation_euler=rot
        if loc: o.location=loc
    return o

# ---------- piezas ----------
def estrella_pts(radios,internos,giro=0.0,desvios=None):
    n=len(radios); pts=[]
    for i in range(n):
        a=giro+math.pi/2+2*math.pi*i/n+(desvios[i] if desvios else 0)
        pts.append((radios[i]*math.cos(a),radios[i]*math.sin(a)))
        b=giro+math.pi/2+2*math.pi*(i+0.5)/n
        pts.append((internos[i]*math.cos(b),internos[i]*math.sin(b)))
    return pts

def estrella_torcida():
    # Puntas desiguales y una doblada: «está mal tallada».
    pts=estrella_pts([0.088,0.066,0.082,0.052,0.076],[0.037,0.029,0.040,0.027,0.033],
                     giro=0.10,desvios=[0.0,0.13,-0.09,0.32,-0.05])
    o=prisma(pts,0.022,MAD_CLARA)
    bpy.context.view_layer.objects.active=o
    # ligero bombeo irregular de la cara delantera
    for v in o.data.vertices:
        if v.co.y<0 and abs(v.co.x)<0.03 and abs(v.co.z)<0.03: v.co.y-=0.004
    o.name="EstrellaTorcida"; o.data.name="EstrellaTorcida"
    return o

def figurita_estrella(c,tam):
    return prisma(estrella_pts([tam]*5,[tam*0.45]*5),tam*0.3,c)

def figurita_arbol():
    return [cilindro(0.008,0.008,0.025,MAD_OSC,6,loc=(0,0,0.0125)),
            cilindro(0.032,0.0,0.07,VERDE,7,loc=(0,0,0.06))]

def figurita_casa():
    return [caja(0.05,0.045,0.04,MAD_CLARA,loc=(0,0,0.02)),
            prisma([(-0.031,0.04),(0.031,0.04),(0,0.07)],0.05,ROJO)]

def figurita_caballito():
    p=[caja(0.07,0.025,0.03,MAD_MEDIA,loc=(0,0,0.045)),           # cuerpo
       caja(0.022,0.022,0.04,MAD_MEDIA,loc=(0.035,0,0.07),rot=(0,-0.4,0)),  # cuello
       caja(0.035,0.02,0.018,MAD_MEDIA,loc=(0.052,0,0.088)),        # cabeza
       caja(0.006,0.024,0.03,MAD_OSC,loc=(0.03,0,0.08),rot=(0,-0.4,0))]     # crin
    for x in (-0.028,0.028):
        for y in (-0.008,0.008): p.append(caja(0.01,0.01,0.03,MAD_MEDIA,loc=(x,y,0.015)))
    return p

def figurita_seta():
    return [cilindro(0.01,0.012,0.025,BLANCO,6,loc=(0,0,0.0125)),
            torno([(0.0,0.05),(0.018,0.048),(0.028,0.036),(0.03,0.028),(0.0,0.028)],ROJO,8)]

def pajaro(color_pecho):
    p=[esfera(0.04,color_pecho,loc=(0,0,0.045),esc=(1.35,0.85,0.8)),     # cuerpo pintado
       esfera(0.026,color_pecho,loc=(0.045,0,0.075)),                      # cabeza
       cilindro(0.009,0.0,0.025,NARANJA,6,loc=(0.077,0,0.075),rot=(0,math.pi/2,0)),  # pico
       prisma([(-0.04,0.0),(-0.09,0.03),(-0.085,-0.005)],0.03,MAD_MEDIA,loc=(0,0,0.05)), # cola
       esfera(0.006,NEGRO,loc=(0.058,-0.019,0.083)),esfera(0.006,NEGRO,loc=(0.058,0.019,0.083)),
       esfera(0.03,BLANCO,loc=(0.022,0,0.036),esc=(0.9,0.72,0.55)),       # pecho
       esfera(0.03,MAD_MEDIA,loc=(-0.005,-0.026,0.05),esc=(1.0,0.35,0.6),rot=(0,0.2,0)),  # alas
       esfera(0.03,MAD_MEDIA,loc=(-0.005,0.026,0.05),esc=(1.0,0.35,0.6),rot=(0,0.2,0)),
       cilindro(0.035,0.035,0.012,MAD_OSC,10,loc=(0,0,0.006))]            # peana
    return p

def farolillo(color):
    perfil=[]; h=0.17; n=16
    for i in range(n+1):
        t=i/n; z=h-t*h
        r=0.03+0.052*math.sin(math.pi*t)**0.8
        if i%2 and 0<i<n: r*=0.93      # costillas del papel plegado
        perfil.append((r,z))
    p=[torno(perfil,color,14,loc=(0,0,0.022)),
       cilindro(0.034,0.034,0.022,NEGRO,10,loc=(0,0,0.011)),
       cilindro(0.034,0.034,0.022,NEGRO,10,loc=(0,0,0.203)),
       cilindro(0.004,0.004,0.05,AMARILLO,4,loc=(0.0,0,0.235)),
       esfera(0.01,AMARILLO,loc=(0,0,0.262))]
    return p

def campanilla():
    perfil=[(0.0,0.07),(0.012,0.068),(0.02,0.06),(0.024,0.04),(0.03,0.018),(0.04,0.004),(0.042,0.0),(0.0,0.0)]
    return [torno(perfil,LATON,12),
            cilindro(0.008,0.008,0.05,MAD_OSC,6,loc=(0,0,0.095)),
            esfera(0.009,LATON_OSC,loc=(0,0,0.122))]

def campanilla_de_viento():
    """Soporte de madera en T con un móvil de tubos colgando."""
    p=[caja(0.12,0.08,0.02,MAD_OSC,loc=(0,0,0.01)),
       cilindro(0.01,0.01,0.42,MAD_MEDIA,6,loc=(0,0,0.22)),
       caja(0.2,0.018,0.018,MAD_MEDIA,loc=(0.09,0,0.42))]
    cx=0.16
    p.append(cilindro(0.0015,0.0015,0.05,CUERDA,4,loc=(cx,0,0.385)))
    p.append(cilindro(0.045,0.045,0.012,MAD_CLARA,10,loc=(cx,0,0.355)))
    largos=[0.15,0.13,0.11,0.12,0.14]
    for i,l in enumerate(largos):
        a=2*math.pi*i/5; x=cx+0.032*math.cos(a); y=0.032*math.sin(a)
        p.append(cilindro(0.001,0.001,0.02,CUERDA,4,loc=(x,y,0.34)))
        p.append(cilindro(0.009,0.009,l,LATON,6,loc=(x,y,0.33-l/2)))
    p.append(cilindro(0.0012,0.0012,0.13,CUERDA,4,loc=(cx,0,0.285)))
    p.append(cilindro(0.016,0.016,0.008,MAD_OSC,8,loc=(cx,0,0.24)))
    p.append(prisma([(-0.02,0.0),(0.02,0.0),(0.0,-0.045)],0.004,ROJO,loc=(cx,0,0.215)))
    return p

def guirnalda(a,b,caida,n_banderas=11,colores=(ROJO,AMARILLO,TURQ,AZUL,BLANCO,ROSA)):
    """Cordel en catenaria de a a b con banderines triangulares."""
    a=Vector(a); b=Vector(b); partes=[]
    def punto(t):
        p=a.lerp(b,t); p.z-=caida*4*t*(1-t); return p
    seg=24
    for i in range(seg):
        p0=punto(i/seg); p1=punto((i+1)/seg); m=(p0+p1)/2; d=p1-p0
        c=cilindro(0.004,0.004,d.length,CUERDA,4,loc=m)
        c.rotation_euler=d.to_track_quat('Z','Y').to_euler(); partes.append(c)
    for k in range(n_banderas):
        t=(k+0.5)/n_banderas; p0=punto(t-0.035); p1=punto(t+0.035); m=(p0+p1)/2
        dx=(p1-p0).normalized(); ang=math.atan2(dx.z,dx.x)
        f=prisma([(-0.06,0.0),(0.06,0.0),(0.0,-0.13)],0.006,colores[k%len(colores)],loc=m)
        f.rotation_euler=(0,-ang,0); partes.append(f)
    return partes

# ---------- montaje en el mostrador ----------
PEND=math.atan(0.311)           # inclinación del fondo de los cajones
def suelo(y): return 0.638+(y+0.129)*0.311
def sobre_suelo(o,x,y,giro=0.0,extra=(0,0,0)):
    o.location=(x,y,suelo(y)); o.rotation_euler=Euler((PEND+extra[0],extra[1],giro+extra[2]),'ZXY') if False else Euler((PEND+extra[0],extra[1],0),'XYZ')
    # giro alrededor de la normal del suelo
    o.rotation_euler=(Matrix.Rotation(PEND+extra[0],4,'X')@Matrix.Rotation(giro,4,'Z')@Matrix.Rotation(extra[1],4,'Y')).to_euler()
    return o

ESC=1.45   # escala de lectura: que se vea desde la cámara del juego
def colocar(o,x,y,giro=0.0,extra=(0,0,0),esc=ESC,alza=0.0):
    sobre_suelo(o,x,y,giro,extra); o.scale=(esc,esc,esc); o.location.z+=alza; return o

def montar(sufijo_raiz="MercanciaTomasa"):
    random.seed(7); R=random.uniform
    raiz=bpy.data.objects.new(sufijo_raiz,None); bpy.context.scene.collection.objects.link(raiz)
    hechos=[]
    # Cajón A (x -1.136..-0.593): figuritas de madera; la estrella torcida delante, de pie.
    fab=[figurita_arbol,figurita_casa,figurita_caballito,figurita_seta,figurita_casa,figurita_arbol,
         figurita_caballito,figurita_seta,figurita_arbol,figurita_casa]
    sitios=[(-1.06,0.27),(-0.93,0.28),(-0.78,0.27),(-0.66,0.25),(-1.05,0.10),(-0.90,0.12),
            (-0.72,0.10),(-1.06,-0.06),(-0.66,-0.05),(-0.70,-0.27)]
    for i,(f,(x,y)) in enumerate(zip(fab,sitios)):
        hechos.append(colocar(unir("Figurita_%02d"%(i+1),f()),x,y,R(-1.2,1.2)))
    for i,(x,y,c) in enumerate([(-0.80,-0.04,MAD_MEDIA),(-1.07,-0.27,MAD_CLARA),(-0.97,0.19,MAD_MEDIA),(-0.62,0.17,MAD_CLARA)]):
        o=figurita_estrella(c,0.035); o.name="FiguritaEstrella_%02d"%(i+1)
        hechos.append(colocar(o,x,y,0,extra=(-math.pi/2,R(-0.5,0.5),0),alza=0.012))
    est=estrella_torcida()
    est.location=(-0.87,-0.20,suelo(-0.20)+0.115); est.rotation_euler=(-0.30,0.14,0.06)
    est.scale=(1.35,1.35,1.35); hechos.append(est)
    # Cajón B (x -0.551..-0.017): pájaros pintados (el poste del cartel está en (-0.485,-0.12)).
    for i,(x,y,g,c) in enumerate([(-0.40,-0.27,-0.6,ROJO),(-0.16,-0.27,-2.5,TURQ),(-0.28,-0.05,-1.6,AMARILLO),
                                   (-0.10,0.02,-2.9,AZUL),(-0.44,0.20,-1.0,VERDE),(-0.17,0.24,-2.2,ROSA)]):
        hechos.append(colocar(unir("Pajaro_%02d"%(i+1),pajaro(c)),x,y,g))
    # Cajón C (x 0.017..0.551): farolillos de papel.
    for i,(x,y,c) in enumerate([(0.12,-0.25,ROJO),(0.29,-0.23,AMARILLO),(0.45,-0.25,TURQ),
                                (0.17,0.12,MORADO),(0.40,0.10,ROJO),(0.28,0.30,AMARILLO)]):
        hechos.append(colocar(unir("Farolillo_%02d"%(i+1),farolillo(c)),x,y,R(0,1),esc=1.25))
    # Cajón D (x 0.585..1.136): campanillas tumbadas + campanilla de viento en su soporte.
    for i,(x,y,g) in enumerate([(0.68,-0.27,0.3),(0.84,-0.28,-0.4),(1.01,-0.25,0.9),(0.70,-0.06,-0.9),(0.90,-0.05,0.5),(1.06,0.02,-0.2)]):
        hechos.append(colocar(unir("Campanilla_%02d"%(i+1),campanilla()),x,y,0,extra=(-math.pi/2+0.25,g,0),alza=0.05))
    o=unir("CampanillaDeViento",campanilla_de_viento())
    o.location=(0.70,0.22,suelo(0.22)); o.scale=(1.35,1.35,1.35); hechos.append(o)
    # Guirnalda bajo el toldo, de poste a poste (puesto -> mostrador: y + 0.479)
    hechos.append(unir("Guirnalda",guirnalda((-1.74,-0.53+0.479,2.25),(1.74,-0.53+0.479,2.25),0.22)))
    # Una malla por tipo de producto (menos objetos en escena); la estrella torcida va sola.
    grupos={}
    for o in hechos:
        clave=o.name.split("_")[0]
        if clave=="FiguritaEstrella": clave="Figurita"
        grupos.setdefault(clave,[]).append(o)
    final=[]
    for clave,objs in grupos.items():
        if len(objs)==1: o=objs[0]
        else:
            bpy.ops.object.select_all(action='DESELECT')
            for x in objs: x.select_set(True)
            bpy.context.view_layer.objects.active=objs[0]; bpy.ops.object.join(); o=bpy.context.active_object
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        nombres={"Figurita":"Figuritas","Pajaro":"Pajaros","Farolillo":"Farolillos","Campanilla":"Campanillas"}
        o.name=nombres.get(clave,clave); o.data.name=o.name
        o.parent=raiz; final.append(o)
    return raiz,final
