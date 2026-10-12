"""Icono del grimorio de Amanecer (INC-724): sol que sale sobre el horizonte, rayos en abanico y
los hilos naranja y crema de Estela. Mismo estilo que los demás iconos de
Assets/Art/UI/Attacks/Grimorio/ (contorno tinta violeta, dorados y halo suave).
Uso: python3 icono_amanecer.py (escribe Assets/Art/UI/Attacks/Grimorio/amanecer.png).
"""
import math, os
from PIL import Image, ImageDraw, ImageFilter
S=2048; k=S/512
OUT=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','..','Assets','Art','UI','Attacks','Grimorio','amanecer.png'))
TINTA=(42,17,60,255); ORO=(255,212,110,255); ORO2=(255,236,170,255); CREMA=(255,249,231,255)
NAR=(255,140,50,255); CREMAH=(255,240,205,255)
def P(x,y): return (x*k,y*k)
cx,cy=256,300   # centro del sol (sobre el horizonte)
img=Image.new('RGBA',(S,S),(0,0,0,0))
# halo suave
glow=Image.new('RGBA',(S,S),(0,0,0,0)); g=ImageDraw.Draw(glow)
g.ellipse([P(cx-190,cy-190),P(cx+190,cy+190)],fill=(255,225,150,150))
glow=glow.filter(ImageFilter.GaussianBlur(60*k/4))
img.alpha_composite(glow)
d=ImageDraw.Draw(img)
# rayos en abanico (solo por encima del horizonte)
rayos=[]
n=11
for i in range(n):
    a=math.radians(180+ (i+0.5)*180/n)
    largo=230 if i%2==0 else 165
    ancho=math.radians(5.5 if i%2==0 else 4)
    p0=(cx+95*math.cos(a-ancho*2), cy+95*math.sin(a-ancho*2))
    p1=(cx+largo*math.cos(a), cy+largo*math.sin(a))
    p2=(cx+95*math.cos(a+ancho*2), cy+95*math.sin(a+ancho*2))
    rayos.append(([P(*p0),P(*p1),P(*p2)], ORO if i%2==0 else ORO2))
for pts,c in rayos:
    d.polygon(pts,fill=TINTA)
# engrosar contorno: dibujar de nuevo el relleno algo más pequeño
def encoger(pts,f):
    mx=sum(p[0] for p in pts)/3; my=sum(p[1] for p in pts)/3
    return [(mx+(x-mx)*f,my+(y-my)*f) for x,y in pts]
for pts,c in rayos:
    d.line(pts+[pts[0]],fill=TINTA,width=30,joint='curve')
    d.polygon(encoger(pts,0.80),fill=c)
# sol (semicírculo + algo) con contorno
r=105
d.ellipse([P(cx-r-8,cy-r-8),P(cx+r+8,cy+r+8)],fill=TINTA)
d.ellipse([P(cx-r,cy-r),P(cx+r,cy+r)],fill=ORO)
d.ellipse([P(cx-r*0.78,cy-r*0.86),P(cx+r*0.62,cy+r*0.5)],fill=ORO2)
d.ellipse([P(cx-r*0.45,cy-r*0.6),P(cx+r*0.15,cy-r*0.05)],fill=CREMA)
# tapar lo de debajo del horizonte
hy=cy+8
d.rectangle([P(0,hy),P(512,512)],fill=(0,0,0,0))
# horizonte: banda con contorno
d.rounded_rectangle([P(36,hy-4),P(476,hy+22)],radius=int(13*k),fill=TINTA)
d.rounded_rectangle([P(46,hy+3),P(466,hy+15)],radius=int(6*k),fill=CREMA)
# hilos de Estela (naranja y crema) que suben hacia el sol
def hilo(x0,col,curva):
    pts=[]
    for j in range(600):
        tt=j/599
        y=490-tt*(490-(hy+30))
        x=x0+(cx-x0)*(tt**0.8)*0.82+curva*math.sin(tt*math.pi)
        pts.append(P(x,y))
    for rad,c in ((46,TINTA),(24,col)):
        for (x,y) in pts:
            d.ellipse([x-rad,y-rad,x+rad,y+rad],fill=c)
hilo(110,NAR,-30); hilo(402,CREMAH,30)
# brillo bajo el horizonte (reflejo)
ref=Image.new('RGBA',(S,S),(0,0,0,0)); rd=ImageDraw.Draw(ref)
rd.ellipse([P(120,hy+10),P(392,hy+60)],fill=(255,200,120,90))
ref=ref.filter(ImageFilter.GaussianBlur(30))
out=Image.alpha_composite(ref,img)
out=out.resize((512,512),Image.LANCZOS)
out.save(OUT)
print('ok')
