# Maqueta guía del clímax del prólogo, VERSIÓN CRESCENDO ÉPICO: mismos 10 primeros compases que componer.py, pero sin caída: sigue subiendo hasta el súper clímax.
import mido
PPQ=480; BAR=4*PPQ
BPM=4*60/3.025   # un compás = 3,025 s: el estallido (48,4 s) cae en el primer tiempo del compás 16
mid=mido.MidiFile(ticks_per_beat=PPQ)
ev={}  # canal -> lista de (tick, msg)
def add(ch,t,msg): ev.setdefault(ch,[]).append((int(t),msg))
def nota(ch,bar,beat,dur,p,v):
    t=bar*BAR+beat*PPQ
    add(ch,t,mido.Message('note_on',channel=ch,note=p,velocity=max(1,min(127,int(v)))))
    add(ch,t+dur*PPQ-5,mido.Message('note_off',channel=ch,note=p,velocity=0))
def acorde(ch,bar,beat,dur,ps,v):
    for p in ps: nota(ch,bar,beat,dur,p,v)
def prog(ch,pr): add(ch,0,mido.Message('program_change',channel=ch,program=pr))
def expr(ch,bar0,b0,bar1,b1,v0,v1,pasos=24):
    t0=bar0*BAR+b0*PPQ; t1=bar1*BAR+b1*PPQ
    for i in range(pasos+1):
        add(ch,t0+(t1-t0)*i/pasos,mido.Message('control_change',channel=ch,control=11,value=int(v0+(v1-v0)*i/pasos)))
CORO,CUERDA,BAJO,OST,METAL,TIMP,ARPA,CEL,TAIKO,PERC,TREM=0,1,2,3,4,5,6,7,8,9,10
for ch,p in [(CORO,52),(CUERDA,48),(BAJO,43),(OST,48),(METAL,61),(TIMP,47),(ARPA,46),(CEL,8),(TAIKO,116),(TREM,44)]: prog(ch,p)
for ch in range(11): add(ch,0,mido.Message('control_change',channel=ch,control=91,value=70))
D2,F2,A2,D3,F3,A3,D4,F4,A4,D5=38,41,45,50,53,57,62,65,69,74
Dm=[50,53,57]; Bb=[46,50,53]; Gm=[43,50,55]; A=[45,49,52]; Eb=[51,55,58]; C=[48,52,55]
# ── Compases 0-2: plegaria, oscuro y quieto (la voz encima) ──
for b,ch_ in enumerate([Dm,Bb,Gm]):
    acorde(CUERDA,b,0,4,ch_,45); acorde(CORO,b,0,4,[x+12 for x in ch_],38)
nota(BAJO,0,0,12,D2,50)
mel=[(0,0,2,62),(0,2,1,65),(0,3,1,64),(1,0,2,62),(1,2,1,60),(1,3,1,62),(2,0,2,58),(2,2,2,57)]
for b,bt,d,p in mel: nota(CORO,b,bt,d,p,48)
# ── 3-5: arpa y timbal suave ──
for b,ch_ in [(3,A),(4,Dm),(5,Bb)]:
    acorde(CUERDA,b,0,4,ch_,50); acorde(CORO,b,0,4,[x+12 for x in ch_],42)
    nota(TIMP,b,0,1,D2 if ch_!=A else 45-12,45); nota(BAJO,b,0,4,ch_[0]-12,55)
    for i in range(8): nota(ARPA,b,i*0.5,0.5,(ch_+[x+12 for x in ch_])[i%6]+12,45+b*3)
mel=[(3,0,1,64),(3,1,1,65),(3,2,1,67),(3,3,1,69),(4,0,2,69),(4,2,1,67),(4,3,1,65),(5,0,2,65),(5,2,1,64),(5,3,1,62)]
for b,bt,d,p in mel: nota(CORO,b,bt,d,p,55)
# ── 6-8: alza los brazos, el orbe sube: ostinato y coro que sube ──
for b,ch_ in [(6,Gm),(7,Eb),(8,A)]:
    acorde(CUERDA,b,0,4,ch_,62+3*(b-6)); acorde(CORO,b,0,4,[x+12 for x in ch_],55+4*(b-6)); nota(BAJO,b,0,4,ch_[0]-12,70)
    nota(TIMP,b,0,1,ch_[0]-12,60); nota(TIMP,b,2,1,ch_[0]-12,55)
    pat=[ch_[0]+24,ch_[1]+24,ch_[2]+24,ch_[1]+24]
    for i in range(8): nota(OST,b,i*0.5,0.5,pat[i%4],55+6*(b-6)+(i%2==0)*8)
mel=[(6,0,1,67),(6,1,1,70),(6,2,1,69),(6,3,1,67),(7,0,2,67),(7,2,1,65),(7,3,1,63),(8,0,4,64)]
for b,bt,d,p in mel: nota(CORO,b,bt,d,p,68)
# ── 9-10: clímax del cántico: metales, taiko, coro entero ──
for b,ch_ in [(9,Dm),(10,Bb)]:
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],85); acorde(CORO,b,0,4,[x+12 for x in ch_],80); nota(BAJO,b,0,4,ch_[0]-12,90)
    for i in range(4): nota(TAIKO,b,i,1,45,85+(i==0)*20)
    pat=[ch_[0]+24,ch_[2]+24,ch_[1]+24,ch_[2]+24]
    for i in range(8): nota(OST,b,i*0.5,0.5,pat[i%4],80+(i%2==0)*10)
for b,bt,d,p in [(9,0,2,62),(9,2,1,69),(9,3,1,67),(10,0,1,65),(10,1,1,64),(10,2,1,62),(10,3,1,61)]:
    nota(METAL,b,bt,d,p,95); nota(METAL,b,bt,d,p-12,90)
acorde(CUERDA,10,3,1,A,90)
for i in range(16): nota(TIMP,10,i*0.25,0.25,D2,50+i*4)
# ── VERSIÓN «CRESCENDO ÉPICO»: sin despedida de Liora ni respiro. Del compás 11 al 18 todo sube sin parar, y el final es un canto en mayor + cierre ──
# (la maqueta original, componer.py, baja a casi nada en el compás 11 porque la cinemática lo pide;
#  aquí ese hueco se sustituye por una escalada continua hasta el súper clímax del compás 17)
Gm=[43,50,55]; Bb=[46,50,53]; C=[48,52,55]; Eb=[51,55,58]; A=[45,49,52]
INI,FIN=11,19            # compases de la escalada (el estallido cae en el primer tiempo del compás 19)
def vel(bar,beat,v0=102,v1=126):
    x=((bar-INI)*4+beat)/((FIN-INI)*4)   # 0 → 1 a lo largo de toda la escalada
    return v0+(v1-v0)*max(0,min(1,x))
# Raíces que suben casi por grados: Sol, Si b, Do, Mi b, Sol, La  → y explota en Re mayor
prog_sube=[(11,Gm),(12,Bb),(13,C),(14,Eb)]
for b,ch_ in prog_sube:
    k=b-INI
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],vel(b,0)); acorde(CORO,b,0,4,[x+12 for x in ch_],vel(b,0)-6)
    nota(BAJO,b,0,4,ch_[0]-12,vel(b,0)+4)
    acorde(METAL,b,0,4,[ch_[0]+12,ch_[1]+12,ch_[2]+12],vel(b,0)-4)                # los metales no se sueltan desde el 11: nada de hueco
    pat=[ch_[0]+24,ch_[2]+24,ch_[1]+24,ch_[2]+24]
    for i in range(8): nota(OST,b,i*0.5,0.5,pat[i%4],vel(b,i*0.5)+(i%2==0)*8)
    # taiko: corcheas desde el compás 11 (el 10 ya llevaba negras: no baja la densidad)
    paso=0.5
    for i in range(int(4/paso)): nota(TAIKO,b,i*paso,paso,45,vel(b,i*paso)+(i==0)*14)
    nota(TIMP,b,0,1,ch_[0]-12,vel(b,0)+6); nota(TIMP,b,2,1,ch_[0]-12,vel(b,2))
    if b in (13,): nota(PERC,b,0,4,49,95)                                       # platillo que abre el segundo tramo
# melodía del coro (y metales una octava abajo desde el 13): cada compás empieza más arriba que el anterior
melo=[(11,0,1,67),(11,1,1,70),(11,2,2,74),
      (12,0,1,70),(12,1,1,74),(12,2,2,77),
      (13,0,1,72),(13,1,1,76),(13,2,2,79),
      (14,0,1,75),(14,1,1,79),(14,2,2,82)]
for b,bt,d,p in melo:
    nota(CORO,b,bt,d,p,vel(b,bt)+6)
    if b>=13: nota(METAL,b,bt,d,p-12,vel(b,bt)+4)
# ── 15-18: «los dos chocan», ahora cuatro compases: la tensión sube por capas hasta que no cabe más ──
#   15 Sol m · 16 Si b · 17 Do · 18 La (dominante, con silencio de medio tiempo antes del golpe)
SUBIDA=[(15,Gm),(16,Bb),(17,C),(18,A)]
for b,ch_ in SUBIDA:
    k=b-15
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],min(127,vel(b,0)+6)); nota(BAJO,b,0,4,ch_[0]-12,min(127,vel(b,0)+10))
    acorde(METAL,b,0,4,[x+12 for x in ch_],min(127,vel(b,0)+2)); acorde(CORO,b,0,4,[x+24 for x in ch_],min(127,vel(b,0)))
    base=[ch_[0]+24,ch_[0]+26,ch_[0]+27,ch_[0]+26]
    for i in range(16):
        if b==18 and i>=14: break                   # medio tiempo de silencio antes del golpe
        # cada compás el ostinato sube un semitono por tiempo y empieza medio tono más arriba que el anterior
        nota(OST,b,i*0.25,0.25,base[i%4]+(i//4)+k,min(127,vel(b,i*0.25)+i))
        if k>=2: nota(OST,b,i*0.25,0.25,base[i%4]+(i//4)+k+12,min(127,vel(b,i*0.25)+i-8))   # a partir del 17, a dos octavas
    for i in range(16 if b<18 else 14): nota(TAIKO,b,i*0.25,0.25,45,min(127,vel(b,i*0.25)+8))
    if b in (17,18): nota(TIMP,b,0,4,ch_[0]-12,110)
# melodía: cada compás un peldaño por encima del anterior, hasta lo más agudo
melo2=[(15,0,1,79),(15,1,1,82),(15,2,1,84),(15,3,1,86),
       (16,0,1,82),(16,1,1,86),(16,2,1,89),(16,3,1,91),
       (17,0,1,72),(17,1,1,76),(17,2,1,79),(17,3,1,84),
       (18,0,1,85),(18,1,1,86),(18,2,1,88),(18,3,0.5,91)]
for b,bt,d,p in melo2:
    nota(CORO,b,bt,d,p,min(127,vel(b,bt)+8)); nota(METAL,b,bt,d,p-12,min(127,vel(b,bt)+6))
# redoble de caja que va de corcheas a fusas, y platillo que se abre en el último compás
for i in range(48): nota(PERC,15+i//16,(i%16)*0.25,0.25,38,min(127,66+i*1.3))
for i in range(28): nota(PERC,18,i*0.125,0.125,38,min(127,110+i//2))
for i in range(14): nota(PERC,18,i*0.25,0.25,52,min(127,30+i*8))
expr(OST,INI,0,18,3.5,100,127); expr(CUERDA,INI,0,18,3.5,100,127); expr(CORO,INI,0,18,3.5,100,127); expr(METAL,INI,0,18,3.5,100,127)

# ── 19: ESTALLIDO (57,5 s): Re mayor de golpe, tutti, y el motivo del cántico por fin en mayor ──
E=19
Dmay=[26,38,45,50,54,57,62,66,69,74,78]
for ch in (CUERDA,TREM): acorde(ch,E,0,10,Dmay[2:],127)
acorde(CORO,E,0,10,[62,66,69,74,78],122); acorde(METAL,E,0,3,[50,54,57,62],127); nota(BAJO,E,0,10,26,127)
nota(TIMP,E,0,2,38,127); nota(PERC,E,0,4,49,127); nota(PERC,E,0,4,57,127); nota(PERC,E,0,2,35,127); nota(PERC,E,0,2,36,127)
for i in range(8): nota(TAIKO,E,i,1,45,127-i*3)
for bt,d,p in [(0,2,74),(2,1,78),(3,1,81)]: nota(METAL,E,bt,d,p,127); nota(METAL,E,bt,d,p-12,120)
for bt,d,p in [(0,1,86),(1,1,85),(2,2,81)]: nota(CORO,E+1,bt,d,p,118); nota(METAL,E+1,bt,d,p-12,112)
nota(CORO,E+1,3,1,78,112)
for i in range(12): nota(ARPA,E,i*0.125,0.5,[62,66,69,74,78,81][i%6]+(12 if i>=6 else 0),105)
for i in range(16): nota(CEL,E,1+i*0.5,0.5,[93,90,86,81,78,74][i%6],72-i)
for ch in (CUERDA,TREM): expr(ch,E,1,E+2,0,127,105)

# ── 21-26: EL CANTO GLORIOSO: el tema ancho y cantado, en mayor. Re · La · Si m · Sol · Sol · La ──
# (sigue lleno de energía: la caída del original aquí se convierte en la parte más hermosa)
Dmaj=[50,54,57]; Amaj=[45,49,52]; Bmin=[47,50,54]; Gmaj=[43,47,50]
CANTO=[(21,Dmaj,102),(22,Amaj,104),(23,Bmin,106),(24,Gmaj,108),(25,Gmaj,112),(26,Amaj,116)]
for b,ch_,v in CANTO:
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],v); acorde(CORO,b,0,4,[x+12 for x in ch_],v-8)
    nota(BAJO,b,0,4,ch_[0]-12,v+6)
    acorde(METAL,b,0,4,[x+12 for x in ch_],v-14)
    nota(TIMP,b,0,1,ch_[0]-12,v); nota(TAIKO,b,0,1,45,v-6); nota(TAIKO,b,2,1,45,v-14)
    arp=ch_+[x+12 for x in ch_]+[ch_[0]+24]
    for i in range(8): nota(ARPA,b,i*0.5,0.5,arp[[0,1,2,3,4,5,4,3][i]]+12,70+(i%2==0)*10+(b-21)*2)
    for i in range(4): nota(CEL,b,i,1,arp[[2,4,5,4][i]]+24,50+(b-21)*3)
canto=[(21,0,2,81),(21,2,1,78),(21,3,1,81),
       (22,0,2,85),(22,2,1,81),(22,3,1,78),
       (23,0,2,83),(23,2,1,81),(23,3,1,78),
       (24,0,1,79),(24,1,1,83),(24,2,2,86),
       (25,0,2,86),(25,2,1,83),(25,3,1,86),
       (26,0,1,85),(26,1,1,88),(26,2,1,90),(26,3,0.5,91)]
for b,bt,d,p in canto:
    v=106+(b-21)*3
    nota(CORO,b,bt,d,p,v); nota(METAL,b,bt,d,p-12,v-4); nota(CUERDA,b,bt,d,p+0,v-8)
# último impulso: bar 26 acelera con semicorcheas y platillo, y un silencio de medio tiempo antes del cierre
for i in range(14): nota(OST,26,i*0.25,0.25,[69,73,76,81][i%4]+(i//4)*2,100+i*2)
for i in range(14): nota(PERC,26,i*0.25,0.25,52,40+i*6)
for i in range(14): nota(TAIKO,26,i*0.25,0.25,45,90+i*2)
for ch in (CUERDA,CORO,METAL): expr(ch,21,0,26,3.5,105,127)

# ── 27-30: EL CIERRE: Re mayor a lo grande, el tema en lo más alto, y el brillo que se apaga despacio ──
Z=27
for ch in (CUERDA,TREM): acorde(ch,Z,0,16,Dmay[2:],127)
acorde(CORO,Z,0,16,[62,66,69,74,78,81],122); acorde(METAL,Z,0,6,[50,54,57,62,66],127); nota(BAJO,Z,0,16,26,127); nota(BAJO,Z,0,16,38,110)
nota(TIMP,Z,0,3,38,127); nota(TIMP,Z,0,3,26,120)
for p,v in [(49,127),(57,127),(35,127),(36,127)]: nota(PERC,Z,0,6,p,v)
for i in range(4): nota(TAIKO,Z,i,1,45,127-i*5)
for bt,d,p in [(0,3,86),(3,1,85),(4,1,81),(5,3,86)]:           # el tema, a toda orquesta y en lo más alto
    nota(METAL,Z,bt,d,p,127); nota(METAL,Z,bt,d,p-12,120); nota(CORO,Z,bt,d,p-12,115)
for i in range(16): nota(ARPA,Z,i*0.125,0.5,[62,66,69,74,78,81,86,90][i%8]+(12 if i>=8 else 0),110-i)
for i in range(24): nota(CEL,Z,1+i*0.5,0.5,[93,90,86,81,78,74][i%6],70-i*2)
for ch in (CUERDA,TREM,CORO,BAJO): expr(ch,Z,5,Z+3,3,127,0)
for ch in (METAL,CEL,ARPA): expr(ch,Z,3,Z+2,0,127,0)
nota(CEL,Z+3,0,3,98,44); nota(CEL,Z+3,0,3,93,38)               # última chispa
FIN_TICKS=(Z+3)*BAR+3*PPQ
# el final se ensancha: el tempo baja en dos escalones para que el cierre respire
tr0=mido.MidiTrack([mido.MetaMessage('set_tempo',tempo=int(60e6/BPM),time=0),
                    mido.MetaMessage('set_tempo',tempo=int(60e6/70),time=25*BAR),
                    mido.MetaMessage('set_tempo',tempo=int(60e6/58),time=2*BAR),
                    mido.MetaMessage('end_of_track',time=FIN_TICKS-27*BAR)])
mid.tracks.append(tr0)
for ch,l in ev.items():
    tr=mido.MidiTrack(); l.sort(key=lambda x:(x[0],x[1].type=='note_on')); last=0
    for t,m in l: tr.append(m.copy(time=t-last)); last=t
    mid.tracks.append(tr)
import os
SALIDA=os.path.join(os.path.dirname(os.path.abspath(__file__)),'crescendo_epico_guia.mid')
mid.save(SALIDA); print('ok', round(mid.length,1),'s ->',SALIDA)
