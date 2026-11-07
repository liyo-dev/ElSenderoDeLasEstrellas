# Genera Assets/Art/Items/Trompetilla/Trompetilla.obj: python generar_trompetilla.py 1 0 Trompetilla.obj
# (1 0 = orden de las caras que deja las normales hacia fuera; comprobado con trimesh).
import math,sys
FLIPX=sys.argv[1]=="1"; FLIPY=sys.argv[2]=="1"
V=[];grupos={}
def g(n): grupos.setdefault(n,[]); return grupos[n]
N=16
def lathe(perfil,o,eje='x',ci=False,cf=False,grupo="Dorado"):
    base=len(V);ox,oy,oz=o
    for (t,r) in perfil:
        for k in range(N):
            a=2*math.pi*k/N;c,s=math.cos(a)*r*1.3,math.sin(a)*r*1.3
            V.append((ox+t,oy+c,oz+s) if eje=='x' else (ox+c,oy+t,oz+s))
    flip=FLIPX if eje=='x' else FLIPY
    def add(f): g(grupo).append(tuple(reversed(f)) if flip else f)
    for i in range(len(perfil)-1):
        for k in range(N):
            a=base+i*N+k;b=base+i*N+(k+1)%N;c=a+N;d=b+N
            add((a+1,c+1,d+1,b+1))
    def tapa(i,ini):
        cx=len(V);t=perfil[i][0]
        V.append((ox+t,oy,oz) if eje=='x' else (ox,oy+t,oz))
        for k in range(N):
            a=base+i*N+k;b=base+i*N+(k+1)%N
            add((cx+1,a+1,b+1) if ini else (cx+1,b+1,a+1))
    if ci: tapa(0,True)
    if cf: tapa(len(perfil)-1,False)
lathe([(0.0,0.045),(0.02,0.05),(0.05,0.03),(0.12,0.022)],(0,0,0),ci=True)
lathe([(0.12,0.024),(0.80,0.024)],(0,0,0))
bell=[(0.80+0.34*(i/8),0.026+0.15*((i/8)**2.6)) for i in range(9)]
bell+=[(1.16,0.19),(1.17,0.185),(1.15,0.16)]
bell+=[(1.15-0.30*(i/5),0.15*(1-i/5)**2.2+0.018) for i in range(6)]
lathe(bell,(0,0,0),cf=True)
lathe([(0.30,0.02),(0.70,0.02)],(0,-0.13,0),ci=True,cf=True)
lathe([(-0.13,0.02),(0.0,0.02)],(0.30,0,0),eje='y')
lathe([(-0.13,0.02),(0.0,0.02)],(0.70,0,0),eje='y')
for x in (0.40,0.50,0.60):
    lathe([(-0.10,0.034),(-0.11,0.03),(0.06,0.03),(0.07,0.034)],(x,0,0),eje='y',ci=True,cf=True)
    lathe([(0.07,0.011),(0.11,0.011)],(x,0,0),eje='y')
    lathe([(0.11,0.024),(0.125,0.03),(0.14,0.026)],(x,0,0),eje='y',ci=True,cf=True,grupo="Boton")
out=sys.argv[3]
with open(out,"w") as f:
    f.write("# Trompetilla de juguete (Chuchelandia). Generada por script; 1 unidad = 1 m, a lo largo de X.\nmtllib Trompetilla.mtl\no Trompetilla\n")
    for v in V: f.write("v %.5f %.5f %.5f\n"%v)
    for n,cs in grupos.items():
        f.write("g %s\nusemtl %s\n"%(n,n))
        for c in cs: f.write("f "+" ".join(map(str,c))+"\n")
