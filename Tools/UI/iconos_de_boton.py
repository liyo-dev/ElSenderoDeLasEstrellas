# Redibuja los iconos de botón (mando y teclado) al estilo papel de la interfaz.
import os, sys, glob, math
from PIL import Image, ImageDraw, ImageFont
import numpy as np
ROOT=os.path.expanduser("~/mnt/ElSenderoDeLasEstrellas")
FONT=f"{ROOT}/Assets/Plugins/Fonts/Nunito-ExtraBold.ttf"
PAPEL=(246,233,209); CLARO=(255,248,232); APAGADO=(234,217,187); TINTA=(94,74,56); TXT=(36,31,26); ORO=(255,212,94); CREMA=(255,246,224)
SOMBRA=(52,36,22,140)
VERDE=(63,166,75); ROJO=(216,73,59); AZUL=(59,120,216); AMARILLO=(231,179,37); ROSA=(208,106,168)

def slot_of(fn):
    n=os.path.splitext(os.path.basename(fn))[0]
    m={"interactable_A":"south","interactable_b":"east","interactable_x":"west","interactable_y":"north",
       "interactable_lb":"shoulderLeft","interactable_rb":"shoulderRight","interactable_lt":"triggerLeft","interactable_rt":"triggerRight",
       "interactable_dpad":"dpad","interactable_dpad_up":"dpadUp","interactable_dpad_down":"dpadDown","interactable_dpad_left":"dpadLeft",
       "interactable_dpad_right":"dpadRight","interactable_Joystick":"stick","interactable_confirm":"confirm","interactable_confirm_spacebar":"spacebar",
       "interactable_teleport":"teleport","left":"dpadLeft"}
    return m.get(n,n)

# contenido por familia: (forma, contenido, relleno, color de contenido)
def spec(fam,slot):
    if fam=="KeyboardMouse":
        k={"south":"E","north":"Q","teleport":"T","start":"Esc","select":"M","triggerLeft":"Ctrl","triggerRight":"Ctrl",
           "dpadUp":"J","dpadDown":"G","confirm":"Espacio","stick":"WASD","dpad":"J G , ."}
        if slot in k: return ("key",("text",k[slot]),CLARO,TXT)
        if slot=="spacebar": return ("key",("none",None),CLARO,TXT)
        if slot=="dpadLeft": return ("key",("keyarrow",(",","left")),CLARO,TXT)
        if slot=="dpadRight": return ("key",("keyarrow",(".","right")),CLARO,TXT)
        if slot=="west": return ("key",("mouse","left"),CLARO,TXT)
        if slot=="east": return ("key",("mouse","right"),CLARO,TXT)
        if slot=="shoulderLeft": return ("key",("mouse","down"),CLARO,TXT)
        if slot=="shoulderRight": return ("key",("mouse","up"),CLARO,TXT)
    common={"dpad":("circle",("dpad",None),CLARO,TINTA),"dpadUp":("circle",("dpad","up"),CLARO,TINTA),"dpadDown":("circle",("dpad","down"),CLARO,TINTA),
            "dpadLeft":("circle",("dpad","left"),CLARO,TINTA),"dpadRight":("circle",("dpad","right"),CLARO,TINTA),
            "stick":("circle",("stick",None),CLARO,TINTA),"start":("circle",("lines",None),CLARO,TINTA),"select":("circle",("view",None),CLARO,TINTA)}
    if fam=="Xbox":
        d={"south":("circle",("text","A"),VERDE,CREMA),"east":("circle",("text","B"),ROJO,CREMA),"west":("circle",("text","X"),AZUL,CREMA),
           "north":("circle",("text","Y"),AMARILLO,TXT),"shoulderLeft":("pill",("text","LB"),CLARO,TXT),"shoulderRight":("pill",("text","RB"),CLARO,TXT),
           "triggerLeft":("trigger",("text","LT"),CLARO,TXT),"triggerRight":("trigger",("text","RT"),CLARO,TXT)}
        d["teleport"]=d["north"]; d["confirm"]=d["south"]
    elif fam=="PlayStation":
        d={"south":("circle",("cross",None),CLARO,AZUL),"east":("circle",("ring",None),CLARO,ROJO),"west":("circle",("square",None),CLARO,ROSA),
           "north":("circle",("tri",None),CLARO,VERDE),"shoulderLeft":("pill",("text","L1"),CLARO,TXT),"shoulderRight":("pill",("text","R1"),CLARO,TXT),
           "triggerLeft":("trigger",("text","L2"),CLARO,TXT),"triggerRight":("trigger",("text","R2"),CLARO,TXT)}
        d["teleport"]=d["north"]; d["confirm"]=d["south"]
        common["select"]=("circle",("pad",None),CLARO,TINTA)
    else: # Switch
        d={"south":("circle",("text","B"),CLARO,TXT),"east":("circle",("text","A"),CLARO,TXT),"west":("circle",("text","Y"),CLARO,TXT),
           "north":("circle",("text","X"),CLARO,TXT),"shoulderLeft":("pill",("text","L"),CLARO,TXT),"shoulderRight":("pill",("text","R"),CLARO,TXT),
           "triggerLeft":("trigger",("text","ZL"),CLARO,TXT),"triggerRight":("trigger",("text","ZR"),CLARO,TXT)}
        d["teleport"]=d["north"]; d["confirm"]=d["south"]
        common["start"]=("circle",("text","+"),CLARO,TXT); common["select"]=("circle",("text","−"),CLARO,TXT)
    d.update({k:v for k,v in common.items() if k not in d})
    return d.get(slot)

def fit_text(dr,text,w,h):
    s=int(h)
    while s>4:
        f=ImageFont.truetype(FONT,s); b=dr.textbbox((0,0),text,font=f)
        if b[2]-b[0]<=w and b[3]-b[1]<=h: return f,b
        s=int(s*0.92)
    f=ImageFont.truetype(FONT,4); return f,dr.textbbox((0,0),text,font=f)

def draw_glyph(W,H,forma,cont,fill,ink):
    SS=4; W2,H2=W*SS,H*SS
    im=Image.new("RGBA",(W2,H2),(0,0,0,0)); dr=ImageDraw.Draw(im)
    m=min(W2,H2); bw=max(2,int(m*0.075)); sh=int(m*0.07)
    # caja del cuerpo (deja hueco abajo para la sombra)
    if forma=="circle":
        dmt=min(W2,H2-sh); x0=(W2-dmt)//2; y0=(H2-sh-dmt)//2; box=(x0,y0,x0+dmt,y0+dmt); r=dmt//2
    else:
        hh=H2-sh; ww=W2
        if forma=="key" and W2>=H2*1.6: ww=W2
        box=(0,0,ww,hh); r={"key":int(min(ww,hh)*0.24),"pill":hh//2,"trigger":int(hh*0.32)}[forma]
        if forma=="trigger": r=int(hh*0.30)
    x0,y0,x1,y1=box
    def shape(d,b,col,outline=None,width=0):
        if forma=="circle": d.ellipse(b,fill=col,outline=outline,width=width)
        else: d.rounded_rectangle(b,radius=r,fill=col,outline=outline,width=width)
    # sombra dura
    sim=Image.new("RGBA",(W2,H2),(0,0,0,0)); shape(ImageDraw.Draw(sim),(x0,y0+sh,x1,y1+sh),SOMBRA); im.alpha_composite(sim)
    shape(dr,box,TINTA)
    inner=(x0+bw,y0+bw,x1-bw,y1-bw)
    rr=r
    if forma=="circle": dr.ellipse(inner,fill=fill)
    else:
        dr.rounded_rectangle(inner,radius=max(1,r-bw),fill=fill)
        if forma=="key":  # labio inferior de la tecla
            lip=int((y1-y0)*0.16)
            lipim=Image.new("RGBA",(W2,H2),(0,0,0,0)); ld=ImageDraw.Draw(lipim)
            ld.rounded_rectangle(inner,radius=max(1,r-bw),fill=APAGADO)
            mask=Image.new("L",(W2,H2),0); ImageDraw.Draw(mask).rectangle((0,inner[3]-lip,W2,inner[3]),fill=255)
            la=np.array(lipim); la[...,3]=(la[...,3].astype(int)*np.array(mask).astype(int)//255).astype(np.uint8)
            im.alpha_composite(Image.fromarray(la))
            dr=ImageDraw.Draw(im)
    # zona de contenido
    cx0,cy0,cx1,cy1=inner
    if forma=="key": cy1-= int((y1-y0)*0.16)
    cw,ch=cx1-cx0,cy1-cy0; cx,cy=(cx0+cx1)/2,(cy0+cy1)/2
    kind,val=cont
    lw=max(2,int(min(cw,ch)*0.11))
    if kind=="text":
        t=val; big = t in (",",".")
        tw=cw*(0.72 if len(t)>2 else 0.62); th=ch*(1.6 if big else 0.58)
        if forma=="pill" or forma=="trigger": th=ch*0.62; tw=cw*0.7
        f,b=fit_text(dr,t,tw,th)
        tx=cx-(b[0]+b[2])/2; ty=cy-(b[1]+b[3])/2
        dr.text((tx,ty),t,font=f,fill=ink)
    elif kind=="keyarrow":
        # La tecla y, debajo, hacia dónde cambia de personaje: a tamaño de HUD una coma sola parece otra cosa.
        car,lado=val
        f=ImageFont.truetype(FONT,int(ch*1.15)); b=dr.textbbox((0,0),car,font=f)
        tx=cx-(b[0]+b[2])/2; ty=cy-ch*0.18-(b[1]+b[3])/2
        dr.text((tx,ty),car,font=f,fill=ink)
        s=min(cw,ch)*0.19; ay=cy+ch*0.25
        if lado=="left": dr.polygon([(cx-s*1.3,ay),(cx+s,ay-s),(cx+s,ay+s)],fill=ORO,outline=TINTA,width=max(2,int(lw*0.45)))
        else: dr.polygon([(cx+s*1.3,ay),(cx-s,ay-s),(cx-s,ay+s)],fill=ORO,outline=TINTA,width=max(2,int(lw*0.45)))
    elif kind=="dpad":
        a=min(cw,ch)*0.80; t=a*0.36
        arms={"up":(cx-t/2,cy-a/2,cx+t/2,cy-t/2),"down":(cx-t/2,cy+t/2,cx+t/2,cy+a/2),"left":(cx-a/2,cy-t/2,cx-t/2,cy+t/2),"right":(cx+t/2,cy-t/2,cx+a/2,cy+t/2)}
        pts=[(cx-t/2,cy-a/2),(cx+t/2,cy-a/2),(cx+t/2,cy-t/2),(cx+a/2,cy-t/2),(cx+a/2,cy+t/2),(cx+t/2,cy+t/2),(cx+t/2,cy+a/2),(cx-t/2,cy+a/2),(cx-t/2,cy+t/2),(cx-a/2,cy+t/2),(cx-a/2,cy-t/2),(cx-t/2,cy-t/2)]
        dr.polygon(pts,fill=TINTA)
        if val: dr.rectangle(arms[val],fill=ORO)
        if val:
            ins=t*0.16; x0a,y0a,x1a,y1a=arms[val]
            dr.rectangle(arms[val],fill=TINTA)
            g={"up":(x0a+ins,y0a+ins,x1a-ins,y1a),"down":(x0a+ins,y0a,x1a-ins,y1a-ins),"left":(x0a+ins,y0a+ins,x1a,y1a-ins),"right":(x0a,y0a+ins,x1a-ins,y1a-ins)}[val]
            dr.rectangle(g,fill=ORO)
    elif kind=="stick":
        R=min(cw,ch)*0.30; dr.ellipse((cx-R,cy-R,cx+R,cy+R),outline=ink,width=lw)
        R2=R*0.42; dr.ellipse((cx-R2,cy-R2,cx+R2,cy+R2),fill=ink)
    elif kind=="lines":
        L=min(cw,ch)*0.42
        for k in (-1,0,1):
            yy=cy+k*L*0.36; dr.line((cx-L/2,yy,cx+L/2,yy),fill=ink,width=lw)
    elif kind=="view":
        s=min(cw,ch)*0.26; o=s*0.35
        dr.rounded_rectangle((cx-s-o*0.4,cy-s*0.75-o*0.5,cx+s-o*0.4,cy+s*0.45-o*0.5),radius=s*0.2,outline=ink,width=int(lw*0.8))
        dr.rounded_rectangle((cx-s+o*0.6,cy-s*0.45+o*0.5,cx+s+o*0.6,cy+s*0.75+o*0.5),radius=s*0.2,fill=ink)
    elif kind=="pad":
        s=min(cw,ch)*0.3; dr.rounded_rectangle((cx-s,cy-s*0.62,cx+s,cy+s*0.62),radius=s*0.2,outline=ink,width=lw)
    elif kind=="cross":
        s=min(cw,ch)*0.24; dr.line((cx-s,cy-s,cx+s,cy+s),fill=ink,width=int(lw*1.2)); dr.line((cx-s,cy+s,cx+s,cy-s),fill=ink,width=int(lw*1.2))
    elif kind=="ring":
        s=min(cw,ch)*0.25; dr.ellipse((cx-s,cy-s,cx+s,cy+s),outline=ink,width=int(lw*1.1))
    elif kind=="square":
        s=min(cw,ch)*0.22; dr.rectangle((cx-s,cy-s,cx+s,cy+s),outline=ink,width=int(lw*1.1))
    elif kind=="tri":
        s=min(cw,ch)*0.28; dr.polygon([(cx,cy-s),(cx+s*1.05,cy+s*0.75),(cx-s*1.05,cy+s*0.75)],outline=ink,width=int(lw*1.1))
    elif kind=="mouse":
        scroll = val in ("up","down")
        mw=min(cw,ch)*(0.46 if scroll else 0.56); mh=mw*1.38; lw=max(2,int(lw*0.75))
        mcx = cx - (mw*0.42 if scroll else 0)
        b=(mcx-mw/2,cy-mh/2,mcx+mw/2,cy+mh/2); rad=mw/2
        mid=cy+mh*0.02
        dr.rounded_rectangle(b,radius=rad,fill=CLARO)
        if val in("left","right"):
            hl=Image.new("RGBA",(W2,H2),(0,0,0,0)); ImageDraw.Draw(hl).rounded_rectangle(b,radius=rad,fill=ORO)
            mk=Image.new("L",(W2,H2),0)
            if val=="left": ImageDraw.Draw(mk).rectangle((b[0],b[1],mcx,mid),fill=255)
            else: ImageDraw.Draw(mk).rectangle((mcx,b[1],b[2],mid),fill=255)
            ha=np.array(hl); ha[...,3]=(ha[...,3].astype(int)*np.array(mk).astype(int)//255).astype(np.uint8)
            im.alpha_composite(Image.fromarray(ha)); dr=ImageDraw.Draw(im)
        dr.rounded_rectangle(b,radius=rad,outline=ink,width=lw)
        dr.line((b[0]+lw*0.3,mid,b[2]-lw*0.3,mid),fill=ink,width=int(lw*0.8))
        if scroll:
            ww2=mw*0.14; wy=b[1]+(mid-b[1])*0.55
            dr.rounded_rectangle((mcx-ww2,wy-mh*0.13,mcx+ww2,wy+mh*0.13),radius=ww2,fill=ORO,outline=ink,width=int(lw*0.7))
            ax=mcx+mw*0.95; s2=mw*0.32
            if val=="up": dr.polygon([(ax,cy-s2),(ax+s2*0.9,cy+s2*0.55),(ax-s2*0.9,cy+s2*0.55)],fill=ink)
            else: dr.polygon([(ax,cy+s2),(ax+s2*0.9,cy-s2*0.55),(ax-s2*0.9,cy-s2*0.55)],fill=ink)
        else:
            dr.line((mcx,b[1]+lw*0.3,mcx,mid),fill=ink,width=int(lw*0.8))
    elif kind=="forbid":
        R=min(cw,ch)*0.32; dr.ellipse((cx-R,cy-R,cx+R,cy+R),outline=ROJO,width=int(lw*1.2))
        dd=R*0.7; dr.line((cx-dd,cy+dd,cx+dd,cy-dd),fill=ROJO,width=int(lw*1.2))
    return im.resize((W,H),Image.LANCZOS)

def render_file(src,fam,dst):
    orig=Image.open(src).convert("RGBA"); W,H=orig.size
    slot=slot_of(src)
    if os.path.basename(src)=="prohibido.png": sp=("circle",("forbid",None),CLARO,ROJO)
    else: sp=spec(fam,slot)
    if sp is None: return None
    a=np.array(orig)[...,3]; ys,xs=np.where(a>20)
    bx0,by0,bx1,by1=xs.min(),ys.min(),xs.max()+1,ys.max()+1
    bw,bh=bx1-bx0,by1-by0
    g=draw_glyph(bw,bh,*sp)
    out=Image.new("RGBA",(W,H),(0,0,0,0)); out.paste(g,(bx0,by0),g)
    os.makedirs(os.path.dirname(dst),exist_ok=True); out.save(dst)
    return slot

if __name__=="__main__":
    outroot=sys.argv[1]
    jobs=[]
    for base in ["Assets/Art/UI/InputGlyphs","Assets/Resources/InputGlyphs"]:
        for fam in ["KeyboardMouse","Xbox","PlayStation","Switch"]:
            for f in sorted(glob.glob(f"{ROOT}/{base}/{fam}/*.png")): jobs.append((f,fam))
    for f in sorted(glob.glob(f"{ROOT}/Assets/Art/UI/Buttons/*.png")):
        if os.path.basename(f) in ("standard_btn.png",): continue
        jobs.append((f,"Xbox"))
    done=[]
    for f,fam in jobs:
        rel=os.path.relpath(f,ROOT); dst=os.path.join(outroot,rel)
        s=render_file(f,fam,dst); done.append((rel,s))
        if s is None: print("SIN ESPEC:",rel)
    print(len(done),"hechos")
