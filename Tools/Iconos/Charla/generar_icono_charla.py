# Genera los fotogramas del icono animado de charla (Canvas Charla, INC-700).
# Uso: python3 generar_icono_charla.py  (necesita numpy, opencv-python y Pillow)
# Escribe charla_00..11.png (512x512) y charla_preview.gif en la carpeta actual; los PNG van a Assets/Art/UI/NpcStatus/Charla/.
import numpy as np, cv2
from PIL import Image, ImageDraw, ImageFilter
S=2048; K=S/1024
yy,xx=np.mgrid[0:S,0:S].astype(np.float32); r=np.hypot(xx-S/2,yy-S/2)/K
def lerp(a,b,t): return a+(b-a)*t
def orbe():
    img=np.zeros((S,S,4),np.float32)
    # resplandor exterior
    g=np.clip(1-(r-485)/40,0,1)*(r>=485)
    glow=np.array([110,70,20],np.float32)
    img[...,:3]+=glow*g[...,None]; img[...,3]=np.maximum(img[...,3],70*g)
    # aro con degradado vertical
    ring=(r>=455)&(r<487)
    tv=((yy/K)-25)/975; tv=np.clip(tv,0,1)[...,None]
    top=np.array([218,176,108],np.float32); bot=np.array([106,62,14],np.float32)
    rc=lerp(top,bot,tv)
    img[ring,:3]=rc[ring]; img[ring,3]=255
    # interior
    inn=r<455
    base=np.array([27,22,50],np.float32); cen=np.array([20,16,34],np.float32)
    rr=np.clip(r/455,0,1)[...,None]
    ic=lerp(cen,base,rr**1.5)
    # brillo superior (elipse difusa)
    hl=((xx/K-512)/345)**2+((yy/K-285)/170)**2
    hm=np.clip(1-hl,0,1)
    hm=cv2.GaussianBlur((hl<1).astype(np.float32),(0,0),30*K)
    hcol=np.array([108,104,124],np.float32)
    ic=lerp(ic,hcol,(hm*0.92)[...,None])
    # borde interior oscuro
    e=np.clip(1-(455-r)/14,0,1)[...,None]
    ic=lerp(ic,np.array([40,30,56],np.float32),e*0.6)
    img[inn,:3]=ic[inn]; img[inn,3]=255
    # antialias del borde exterior del aro
    return img
def bocadillo_mask(cx,cy,w,h,lado,esc):
    m=Image.new('L',(S,S),0); d=ImageDraw.Draw(m)
    w*=esc; h*=esc
    x0,y0,x1,y1=[v*K for v in (cx-w/2,cy-h/2,cx+w/2,cy+h/2)]
    d.rounded_rectangle((x0,y0,x1,y1),radius=h*0.42*K,fill=255)
    # rabito
    if lado<0: pts=[(cx-w*0.22,cy+h*0.30),(cx-w*0.02,cy+h*0.40),(cx-w*0.42,cy+h*0.78)]
    else:      pts=[(cx+w*0.22,cy+h*0.30),(cx+w*0.02,cy+h*0.40),(cx+w*0.42,cy+h*0.78)]
    d.polygon([(x*K,y*K) for x,y in pts],fill=255)
    return np.array(m,np.float32)/255
def pegar(img,mask,col_top,col_bot,brillo):
    # sombra
    sh=cv2.GaussianBlur(np.roll(np.roll(mask,int(10*K),0),int(4*K),1),(0,0),14*K)*0.7
    a=sh*(r<455)
    img[...,:3]=img[...,:3]*(1-a[...,None])+np.array([8,6,16],np.float32)*a[...,None]
    # resplandor
    gl=cv2.GaussianBlur(mask,(0,0),22*K)*0.35*brillo
    img[...,:3]=img[...,:3]+np.array([240,170,60],np.float32)*gl[...,None]*(r<455)[...,None]
    # relleno con degradado vertical dentro de la mascara
    ys=np.where(mask.max(1)>0.5)[0]; y0,y1=(ys.min(),ys.max()) if len(ys) else (0,1)
    t=np.clip((yy-y0)/max(1,(y1-y0)),0,1)[...,None]
    col=lerp(np.array(col_top,np.float32),np.array(col_bot,np.float32),t)*brillo
    # brillo superior del bocadillo
    sheen=np.clip(1-t*2.2,0,1)*0.25
    col=col+(255-col)*sheen
    img[...,:3]=img[...,:3]*(1-mask[...,None])+col*mask[...,None]
def puntos(img,cx,cy,esc,activo,f):
    for k,dx in enumerate((-74,0,74)):
        sub=0
        if activo and f==k+1: sub=-22
        elif activo and f==k+2: sub=-8
        m=Image.new('L',(S,S),0); d=ImageDraw.Draw(m)
        rr=(25 if activo else 20)*esc
        x=(cx+dx*esc)*K; y=(cy+sub*esc)*K
        d.ellipse((x-rr*K,y-rr*K,x+rr*K,y+rr*K),fill=255)
        mm=np.array(m,np.float32)/255
        c=np.array([46,32,60],np.float32) if activo else np.array([70,52,62],np.float32)
        img[...,:3]=img[...,:3]*(1-mm[...,None])+c*mm[...,None]
def frame(i):
    img=orbe()
    fase=i//6; t=i%6
    pop={0:0.96,1:1.08,2:1.02,3:1.0,4:1.0,5:1.0}[t]
    L=dict(cx=385,cy=590,lado=-1); R=dict(cx=640,cy=405,lado=1)
    act,ina=(L,R) if fase==0 else (R,L)
    # primero el inactivo (detras), luego el activo
    for b,activo in ((ina,False),(act,True)):
        esc=pop if activo else 0.84
        m=bocadillo_mask(b['cx'],b['cy'],350,245,b['lado'],esc)
        if activo: pegar(img,m,(255,244,214),(244,176,64),1.0)
        else:      pegar(img,m,(214,196,160),(176,128,58),0.78)
        puntos(img,b['cx'],b['cy']-4,esc,activo,t)
    out=Image.fromarray(np.clip(img,0,255).astype(np.uint8),'RGBA').resize((512,512),Image.LANCZOS)
    return out
fr=[frame(i) for i in range(12)]
for i,f in enumerate(fr): f.save(f'charla_{i:02d}.png')
fr[0].save('preview.gif',save_all=True,append_images=fr[1:],duration=150,loop=0,disposal=2)
# hoja de contacto
sheet=Image.new('RGBA',(512*6,512*2),(255,255,255,255))
for i,f in enumerate(fr): sheet.alpha_composite(f,((i%6)*512,(i//6)*512))
sheet.convert('RGB').resize((1536,512)).save('hoja.png')
