# Maqueta guía del clímax del prólogo, VERSIÓN CRESCENDO ÉPICO v2: mismos 10 primeros compases que componer.py, pero sin caída: sigue subiendo hasta el súper clímax.
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
for b,bt,d,p in [(9,0,2,62),(9,2,1,69),(9,3,1,67),(10,0,1,65),(10,1,1,63),(10,2,1,62),(10,3,1,62)]:
    nota(METAL,b,bt,d,p,95); nota(METAL,b,bt,d,p-12,90)
# (v2: quitado el acorde de La del compás 10, tiempo 4: chocaba con el Si b y con el Do# de la melodía; era la nota de 0:31-0:33)
for i in range(16): nota(TIMP,10,i*0.25,0.25,D2,50+i*4)
# ── VERSIÓN «CRESCENDO ÉPICO v2»: sin despedida de Liora ni respiro. Del compás 11 al 18 todo sube sin parar, y el final es un canto en mayor + cierre ──
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
# ── 15-18: «los dos chocan». v2: el ostinato ya no sube por semitonos (eso chocaba con el acorde y ensuciaba),
#    sube por notas del acorde, limpio. Y antes del golpe hay un silencio de UN tiempo entero para que el estallido se note ──
#   15 Sol m · 16 Si b · 17 Do · 18 La (dominante)
TRI={15:[55,58,62],16:[58,62,65],17:[60,64,67],18:[57,61,64]}
SUBIDA=[(15,Gm),(16,Bb),(17,C),(18,A)]
for b,ch_ in SUBIDA:
    k=b-15
    t=TRI[b]; largo=4 if b<18 else 3          # el compás 18 se corta tras 3 tiempos: silencio
    acorde(CUERDA,b,0,largo,ch_+[x+12 for x in ch_],min(127,vel(b,0)+4)); nota(BAJO,b,0,largo,ch_[0]-12,min(127,vel(b,0)+8))
    acorde(METAL,b,0,largo,[x+12 for x in ch_],min(127,vel(b,0)))
    acorde(CORO,b,0,largo,[x+24 for x in ch_],min(127,vel(b,0)-2))
    L=t+[x+12 for x in t]+[t[0]+24,t[1]+24]          # 8 notas del acorde, de grave a agudo
    pasos=16 if b<18 else 12
    for i in range(pasos):
        nota(OST,b,i*0.25,0.25,L[i%8]+(12 if k>=2 else 0)-12*(i%8==0 and False),min(127,vel(b,i*0.25)+i))
        if k>=2: nota(OST,b,i*0.25,0.25,L[i%8],min(127,vel(b,i*0.25)+i-10))
    for i in range(pasos): nota(TAIKO,b,i*0.25,0.25,45,min(127,vel(b,i*0.25)+6))
    if b in (17,18): nota(TIMP,b,0,largo,ch_[0]-12,110)
melo2=[(15,0,1,79),(15,1,1,82),(15,2,1,84),(15,3,1,86),
       (16,0,1,82),(16,1,1,86),(16,2,1,89),(16,3,1,91),
       (17,0,1,72),(17,1,1,76),(17,2,1,79),(17,3,1,84),
       (18,0,1,85),(18,1,1,86),(18,2,1,88)]
for b,bt,d,p in melo2:
    nota(CORO,b,bt,d,p,min(127,vel(b,bt)+6)); nota(METAL,b,bt,d,p-12,min(127,vel(b,bt)+4))
# redoble de caja limpio (solo los dos últimos compases) y platillo que se abre
for i in range(28): nota(PERC,17+i//16,(i%16)*0.25,0.25,38,min(120,60+i*2))
for i in range(12): nota(PERC,18,i*0.25,0.25,52,min(110,40+i*6))
expr(OST,INI,0,18,3,100,127); expr(CUERDA,INI,0,18,3,100,127); expr(CORO,INI,0,18,3,100,127); expr(METAL,INI,0,18,3,100,127)

# ── 19: ESTALLIDO, limpio: un acorde abierto y largo, el tema en unísono de metales y coro, UN platillo y bombo.
#    (antes eran 11 notas de trémolo + arpa + celesta + 4 golpes a la vez: una masa de ruido) ──
E=19
acorde(CUERDA,E,0,8,[38,45,50,54,57,62,66,69],127); acorde(TREM,E,0,8,[62,66,69,74],96)
acorde(CORO,E,0,8,[62,66,69,74],122); acorde(METAL,E,0,2,[50,54,57,62],127)
nota(BAJO,E,0,8,26,127); nota(BAJO,E,0,8,38,112); nota(TIMP,E,0,2,38,127)
nota(PERC,E,0,4,49,127); nota(PERC,E,0,2,36,120); nota(TAIKO,E,0,1,45,127); nota(TAIKO,E,2,1,45,112)
for bt,d,p in [(0,2,74),(2,1,78),(3,1,81)]: nota(METAL,E,bt,d,p,127); nota(METAL,E,bt,d,p-12,118); nota(CORO,E,bt,d,p,118)
for bt,d,p in [(0,1,86),(1,1,85),(2,2,81)]: nota(CORO,E+1,bt,d,p,118); nota(METAL,E+1,bt,d,p-12,112)
nota(CORO,E+1,3,1,78,108)
for i in range(8): nota(ARPA,E,2+i*0.25,0.5,[62,66,69,74,78,81,86,90][i],78)       # un solo barrido de arpa, suave
for ch in (CUERDA,TREM,CORO,BAJO): expr(ch,E+1,0,E+2,0,127,82)                    # y baja a un brillo cálido para el canto

# ── 21-32: EL CANTO, DESARROLLADO. Es la melodía bonita: antes duraba 6 compases y se cortaba.
#    A (21-24): primera vez · A' (25-28): segunda vez, más grande y con ostinato · B (29-32): la melodía sube y se abre ──
Dmaj=[50,54,57]; Amaj=[45,49,52]; Bmin=[47,50,54]; Gmaj=[43,47,50]
CANTO=[(21,Dmaj,102),(22,Amaj,104),(23,Bmin,106),(24,Gmaj,108),
       (25,Dmaj,108),(26,Amaj,110),(27,Bmin,112),(28,Gmaj,114),
       (29,Gmaj,114),(30,Amaj,116),(31,Bmin,118),(32,Amaj,122)]
for b,ch_,v in CANTO:
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],v); acorde(CORO,b,0,4,[x+12 for x in ch_],v-8)
    nota(BAJO,b,0,4,ch_[0]-12,v+6)
    acorde(METAL,b,0,4,[x+12 for x in ch_],v-14+(6 if b>=25 else 0))
    nota(TIMP,b,0,1,ch_[0]-12,v)
    arp=ch_+[x+12 for x in ch_]+[ch_[0]+24]
    for i in range(8): nota(ARPA,b,i*0.5,0.5,arp[[0,1,2,3,4,5,4,3][i]]+12,70+(i%2==0)*10+(b-21)*1)
    for i in range(4): nota(CEL,b,i,1,arp[[2,4,5,4][i]]+24,50+(b-21)*2)
    if b>=25:                                   # a partir de la segunda vez: ostinato de cuerdas y taiko, más empuje
        tri=[ch_[0]+24,ch_[1]+24,ch_[2]+24,ch_[1]+24]
        for i in range(8): nota(OST,b,i*0.5,0.5,tri[i%4],72+(b-25)*3+(i%2==0)*6)
        nota(TAIKO,b,0,1,45,v-6); nota(TAIKO,b,2,1,45,v-12)
melA=[(0,0,2,81),(0,2,1,78),(0,3,1,81),
      (1,0,2,85),(1,2,1,81),(1,3,1,78),
      (2,0,2,83),(2,2,1,81),(2,3,1,78),
      (3,0,1,79),(3,1,1,83),(3,2,2,86)]
melB=[(29,0,2,86),(29,2,1,83),(29,3,1,86),
      (30,0,2,88),(30,2,1,85),(30,3,1,88),
      (31,0,2,90),(31,2,1,86),(31,3,1,83),
      (32,0,1,85),(32,1,1,88),(32,2,1,90),(32,3,0.5,91)]
melo_canto=[(21+o,bt,d,p) for o,bt,d,p in melA]+[(25+o,bt,d,p) for o,bt,d,p in melA]+melB
for b,bt,d,p in melo_canto:
    v=106+(b-21)*1.5
    nota(CORO,b,bt,d,p,min(127,v)); nota(METAL,b,bt,d,p-12,min(127,v-4)); nota(CUERDA,b,bt,d,p,min(127,v-8))
    if b>=25: nota(CEL,b,bt,d,p+12,60)                 # a partir de la segunda vez la melodía brilla además una octava arriba
# último impulso del compás 32: semicorcheas, platillo y taiko; medio tiempo de silencio antes del cierre
for i in range(14): nota(OST,32,i*0.25,0.25,[69,73,76,81][i%4]+(i//4)*2,100+i*2)
for i in range(14): nota(PERC,32,i*0.25,0.25,52,40+i*6)
for i in range(14): nota(TAIKO,32,i*0.25,0.25,45,90+i*2)
for ch in (CUERDA,CORO,METAL): expr(ch,21,0,32,3.5,100,127)

# ── 33-36: EL CIERRE: Re mayor a lo grande, el tema en lo más alto, y el brillo que se apaga despacio ──
Z=33
Dmay=[26,38,45,50,54,57,62,66,69,74,78]
acorde(CUERDA,Z,0,16,[38,45,50,54,57,62,66,69],127); acorde(TREM,Z,0,16,[62,66,69,74],100)
acorde(CORO,Z,0,16,[62,66,69,74,78,81],122); acorde(METAL,Z,0,6,[50,54,57,62,66],127); nota(BAJO,Z,0,16,26,127); nota(BAJO,Z,0,16,38,110)
nota(TIMP,Z,0,3,38,127); nota(TIMP,Z,0,3,26,120)
for p,v in [(49,127),(36,125)]: nota(PERC,Z,0,6,p,v)
for i in range(4): nota(TAIKO,Z,i,1,45,127-i*5)
for bt,d,p in [(0,3,86),(3,1,85),(4,1,81),(5,3,86)]:           # el tema, a toda orquesta y en lo más alto
    nota(METAL,Z,bt,d,p,127); nota(METAL,Z,bt,d,p-12,120); nota(CORO,Z,bt,d,p-12,115)
for i in range(16): nota(ARPA,Z,i*0.125,0.5,[62,66,69,74,78,81,86,90][i%8]+(12 if i>=8 else 0),100-i)
for i in range(24): nota(CEL,Z,1+i*0.5,0.5,[93,90,86,81,78,74][i%6],66-i*2)
for ch in (CUERDA,TREM,CORO,BAJO): expr(ch,Z,5,Z+3,3,127,0)
for ch in (METAL,CEL,ARPA): expr(ch,Z,3,Z+2,0,127,0)
nota(CEL,Z+3,0,3,98,44); nota(CEL,Z+3,0,3,93,38)               # última chispa
FIN_TICKS=(Z+3)*BAR+3*PPQ
# el final se ensancha: el tempo baja en dos escalones para que la melodía y el cierre respiren
tr0=mido.MidiTrack([mido.MetaMessage('set_tempo',tempo=int(60e6/BPM),time=0),
                    mido.MetaMessage('set_tempo',tempo=int(60e6/70),time=31*BAR),
                    mido.MetaMessage('set_tempo',tempo=int(60e6/58),time=2*BAR),
                    mido.MetaMessage('end_of_track',time=FIN_TICKS-33*BAR)])
mid.tracks.append(tr0)
for ch,l in ev.items():
    tr=mido.MidiTrack(); l.sort(key=lambda x:(x[0],x[1].type=='note_on')); last=0
    for t,m in l: tr.append(m.copy(time=t-last)); last=t
    mid.tracks.append(tr)
import os
SALIDA=os.path.join(os.path.dirname(os.path.abspath(__file__)),'crescendo_epico_v2_guia.mid')
mid.save(SALIDA); print('ok', round(mid.length,1),'s ->',SALIDA)
