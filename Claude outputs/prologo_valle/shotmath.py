# -*- coding: utf-8 -*-
"""Port fiel de ShotComposer.cs a Python, para poder diseñar el decorado sabiendo dónde cae la
cámara en cada plano ANTES de tener que abrir Unity. La verdad la sigue dando SequenceShotCapture,
que renderiza con el solver de verdad; esto es para no diseñar a ciegas entre captura y captura.

Si algún día cambia ShotComposer.cs, esto hay que nivelarlo (o tirarlo)."""
import math

FOV = {"Wide":62, "TwoShot":52, "OTS":48, "Medium":48, "CloseUp":40, "Reaction":40, "Tracking":55}
ALTO = {"CloseUp":1.35, "Reaction":1.35, "Medium":2.10, "OTS":2.30, "Tracking":4.20, "Wide":3.50, "TwoShot":3.50}
TRES_CUARTOS = 35.0
ESCORZO_TWOSHOT = 18.0
DIST_MIN = 1.10
MAX_TWOSHOT = 7.0

def v(x,y,z): return (x,y,z)
def sub(a,b): return (a[0]-b[0], a[1]-b[1], a[2]-b[2])
def add(a,b): return (a[0]+b[0], a[1]+b[1], a[2]+b[2])
def mul(a,k): return (a[0]*k, a[1]*k, a[2]*k)
def largo(a): return math.sqrt(a[0]**2+a[1]**2+a[2]**2)
def norm(a):
    l=largo(a)
    return (0,0,0) if l<1e-6 else (a[0]/l,a[1]/l,a[2]/l)
def plano(a): return (a[0],0.0,a[2])
def rot_y(a,g):
    r=math.radians(g); c,s=math.cos(r), math.sin(r)
    return (a[0]*c + a[2]*s, a[1], -a[0]*s + a[2]*c)
def cross_up(a):  # Vector3.Cross(up, a)
    return (a[2], 0.0, -a[0])
def dot(a,b): return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]

def dist_para_alto(h, fov): return (h*0.5)/math.tan(math.radians(fov)*0.5)
def dist_para_ancho(w, fov, aspect):
    hv=math.radians(fov)*0.5
    hh=math.atan(math.tan(hv)*aspect)
    return (w*0.5)/math.tan(hh)

class Actor:
    def __init__(self, id, pos, mirando=None, eye=0.95, radio=0.45):
        self.id=id; self.pos=pos; self.eye=eye; self.radio=radio
        self.fwd = norm(plano(sub(mirando, pos))) if mirando else (0,0,1)
    @property
    def ojos(self): return (self.pos[0], self.pos[1]+self.eye, self.pos[2])
    @property
    def pecho(self): return (self.pos[0], self.pos[1]+self.eye*0.72, self.pos[2])

def lado_accion(sujeto, secundario, lado_fijado):
    eje = plano(sub(secundario.pos, sujeto.pos)) if secundario else plano(sujeto.fwd)
    if largo(eje) < 1e-4: eje = plano(sujeto.fwd)
    eje = norm(eje)
    perp = norm(cross_up(eje))
    return perp if dot(perp, lado_fijado) >= 0 else mul(perp,-1)

def resolver(tipo, sujeto, secundario=None, lado_fijado=(1,0,0), aspect=16/9,
             escala_dist=1.0, alto_camara=0.0, fov_override=0.0, suelo=100.0, headroom=True):
    fov = fov_override if fov_override>0 else FOV[tipo]
    alto = ALTO[tipo]
    lado = lado_accion(sujeto, secundario, lado_fijado)
    ojos = sujeto.ojos
    mirar = ojos

    if tipo=="TwoShot" and secundario:
        sep = largo(plano(sub(sujeto.ojos, secundario.ojos)))
        if sep > MAX_TWOSHOT:
            return resolver("Medium", sujeto, secundario, lado_fijado, aspect, escala_dist, alto_camara, 0, suelo, headroom) + ("(two-shot descartado: %.1f m)"%sep,)
        medio = mul(add(sujeto.ojos, secundario.ojos), 0.5)
        mirar = medio
        ancho = sep + 1.6
        alto = max(2.0, ancho/aspect)
        d = max(dist_para_ancho(ancho,fov,aspect), dist_para_alto(alto,fov))*escala_dist
        pos = add(medio, mul(rot_y(lado, ESCORZO_TWOSHOT), max(d, DIST_MIN)))
    elif tipo=="Wide":
        centro = sujeto.pecho; radio=1.2
        if secundario:
            centro = mul(add(sujeto.pecho, secundario.pecho),0.5)
            radio = largo(plano(sub(sujeto.pecho, secundario.pecho)))*0.5 + 1.5
        mirar = centro; alto = radio*2
        hv=math.radians(fov)*0.5; hh=math.atan(math.tan(hv)*aspect)
        d = radio/math.sin(min(hv,hh))*escala_dist
        pos = add(add(centro, mul(rot_y(lado,25.0), d)), (0, radio*0.35, 0))
    elif tipo=="OTS" and secundario:
        mirar = sujeto.ojos
        eje = norm(plano(sub(sujeto.ojos, secundario.ojos)))
        pos = add(add(add(secundario.ojos, mul(eje,-0.78*escala_dist)), mul(lado,0.42*escala_dist)), (0,0.10,0))
        fov = 2*math.degrees(math.atan((ALTO["OTS"]*0.5)/max(0.01,largo(sub(pos,mirar)))))
    else:
        frontal = plano(sub(secundario.ojos, ojos)) if secundario else plano(sujeto.fwd)
        if largo(frontal)<1e-4: frontal = plano(sujeto.fwd)
        frontal = norm(frontal)
        signo = 1 if dot(cross_up(frontal), lado) >= 0 else -1
        d = max(dist_para_alto(alto,fov)*escala_dist, DIST_MIN)
        pos = add(ojos, mul(rot_y(frontal, TRES_CUARTOS*signo), d))

    pos = add(pos, (0, alto_camara, 0))
    # pase de seguridad (distancia mínima y suelo; las paredes las comprueba el juego)
    dmin = DIST_MIN + sujeto.radio
    delta = sub(pos, mirar); dd = largo(delta)
    if dd < dmin: pos = add(mirar, mul(norm(delta) if dd>0.01 else (0,0,-1), dmin))
    if pos[1] < suelo+0.35: pos = (pos[0], suelo+0.35, pos[2])

    direccion = norm(sub(mirar,pos))
    yaw = math.degrees(math.atan2(direccion[0], direccion[2]))
    pitch = math.degrees(math.asin(max(-1,min(1,-direccion[1]))))
    if headroom and tipo!="Wide":
        hv=math.radians(fov)*0.5
        pitch += math.degrees(math.atan(math.tan(hv)/3.0))
    return pos, yaw, pitch, fov, mirar, ""

def describe(nombre, tipo, sujeto, secundario=None, **kw):
    r = resolver(tipo, sujeto, secundario, **kw)
    pos, yaw, pitch, fov, mirar = r[0], r[1], r[2], r[3], r[4]
    aviso = r[5] if len(r)>5 else ""
    d = largo(sub(pos,mirar))
    # hacia dónde mira la cámara en el plano del suelo: eso es el FONDO del plano
    print("%-34s %-8s cám=(%7.2f,%6.2f,%7.2f) d=%4.1fm fov=%2.0f  mira hacia %3.0f°  %s"
          % (nombre, tipo, pos[0], pos[1], pos[2], d, fov, yaw%360, aviso))
    return pos, yaw, fov
