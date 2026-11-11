# Maqueta guía de «La puerta de las estrellas»: el conjuro de Will ante la Piedra Ancestral
# (Sendero_01_ElConjuro). No es un conjuro de combate: es un ruego que se llena de luz. Re mayor.
# Un compás = 2,8 s. La pista empieza 0,4 s después del principio de la cinemática, de modo que:
#   compás 4 (11,6 s)  ≈ segunda estrofa, la Piedra despierta (11,9 s)
#   compás 7 (20,0 s)  ≈ tercera estrofa, nace la luz y Will abre los brazos (20,6 s)
#   compás 10 (28,4 s) = el grito
#   compás 11 (31,2 s) = se abre el portal
#   compás 12 (34,0 s) ≈ el Sendero al otro lado (33,8 s)
import mido, sys
PPQ=480; BAR=4*PPQ
BPM=4*60/2.8
mid=mido.MidiFile(ticks_per_beat=PPQ)
ev={}
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
CORO,CUERDA,BAJO,OST,METAL,TIMP,ARPA,CEL,TROMPA,PERC,FLAUTA=0,1,2,3,4,5,6,7,8,9,10
for ch,p in [(CORO,52),(CUERDA,48),(BAJO,43),(OST,48),(METAL,61),(TIMP,47),(ARPA,46),(CEL,8),(TROMPA,60),(FLAUTA,73)]: prog(ch,p)
for ch in range(11): add(ch,0,mido.Message('control_change',channel=ch,control=91,value=80))
D=[50,54,57]; Bm=[47,50,54]; G=[43,47,50]; A=[45,49,52]; Em=[52,55,59]; Bb=[46,50,53]; C=[48,52,55]
def arp(ch,bar,ch_,v,oct=24,n=8,paso=0.5):
    notas=ch_+[ch_[0]+12]+[ch_[2],ch_[1]]
    for i in range(n): nota(ch,bar,i*paso,paso*1.6,notas[i%5]+oct,v+(i%4==0)*8)

# ── 0-3: la mano en el corazón. Celesta y arpa, cuerdas muy arriba y muy suaves (la voz encima). ──
for b,ch_ in enumerate([D,Bm,G,A]):
    acorde(CUERDA,b,0,4,[x+24 for x in ch_],34+b*2)
    arp(CEL,b,ch_,44+b*2,oct=24)
    nota(ARPA,b,0,4,ch_[0]-12,40)
nota(CUERDA,0,0,8,64+12,30)            # la novena, que le da el aire de pregunta
for b,bt,d,p in [(0,0,2,78),(0,2,1,76),(0,3,1,74),(1,0,3,69),(1,3,1,71),(2,0,2,74),(2,2,1,76),(2,3,1,78),(3,0,4,76)]:
    nota(FLAUTA,b,bt,d,p,50)

# ── 4-6: las manos ofrecidas, la Piedra despierta. Entran el bajo, la trompa y un coro lejano. ──
nota(TIMP,4,0,2,43-12+12,60); nota(PERC,3,3,1,52,35)
for b,ch_ in [(4,G),(5,D),(6,Em)]:
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],52+3*(b-4)); acorde(CORO,b,0,4,[x+12 for x in ch_],40+4*(b-4))
    nota(BAJO,b,0,4,ch_[0]-12,58); arp(ARPA,b,ch_,48+3*(b-4),oct=12)
acorde(CUERDA,6,2,2,A+[x+12 for x in A],60)
for b,bt,d,p in [(4,0,2,71),(4,2,1,74),(4,3,1,71),(5,0,2,69),(5,2,1,66),(5,3,1,69),(6,0,1,67),(6,1,1,69),(6,2,1,71),(6,3,1,73)]:
    nota(TROMPA,b,bt,d,p,70); nota(FLAUTA,b,bt,d,p+12,45)

# ── 7-9: los brazos abiertos, la luz crece. Ostinato de cuerdas, el coro sube, todo va hacia arriba. ──
for b,ch_ in [(7,Bm),(8,G),(9,A)]:
    k=b-7
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],66+6*k); acorde(CORO,b,0,4,[x+12 for x in ch_],58+8*k)
    nota(BAJO,b,0,4,ch_[0]-12,72+5*k); nota(TIMP,b,0,1,ch_[0]-12,62+8*k); nota(TIMP,b,2,1,ch_[0]-12,55+8*k)
    pat=[ch_[0]+24,ch_[1]+24,ch_[2]+24,ch_[1]+24]
    for i in range(8): nota(OST,b,i*0.5,0.5,pat[i%4],58+8*k+(i%2==0)*8)
    arp(ARPA,b,ch_,55+5*k,oct=24)
for b,bt,d,p in [(7,0,2,74),(7,2,1,78),(7,3,1,76),(8,0,2,74),(8,2,1,71),(8,3,1,74),(9,0,1.5,76),(9,1.5,0.5,78),(9,2,1,79),(9,3,0.5,81)]:
    nota(TROMPA,b,bt,d,p-12,84); nota(CUERDA,b,bt,d,p,82); nota(CORO,b,bt,d,p,70)
for i in range(14): nota(TIMP,9,i*0.25,0.25,45,40+i*6)        # redoble que crece
for i in range(6): nota(PERC,9,2+i*0.25,0.25,52,30+i*14)       # platillo que crece; medio tiempo de aire antes del grito
expr(CUERDA,7,0,9,3.5,85,127); expr(OST,7,0,9,3.5,80,127); expr(CORO,7,0,9,3.5,85,127)

# ── 10: EL GRITO (28,4 s). Si bemol y Do: dos peldaños hacia el Re; fanfarria que sube. ──
for bt,ch_ in [(0,Bb),(2,C)]:
    acorde(CUERDA,10,bt,2,[ch_[0]-12]+ch_+[x+12 for x in ch_],112); acorde(CORO,10,bt,2,[x+12 for x in ch_],108)
    nota(BAJO,10,bt,2,ch_[0]-12,110); nota(TIMP,10,bt,1,ch_[0]-12,112); nota(PERC,10,bt,1,36,110)
for bt,d,p in [(0,1,74),(1,1,77),(2,1,76),(3,0.5,79)]:
    nota(METAL,10,bt,d,p,116); nota(METAL,10,bt,d,p-12,110); nota(TROMPA,10,bt,d,p-12,110)
nota(PERC,10,0,2,49,105)
for i in range(6): nota(PERC,10,2+i*0.25,0.25,38,70+i*9)      # caja que empuja; medio tiempo de silencio antes del portal
for i in range(6): nota(OST,10,2+i*0.25,0.25,[72,76,79,84,88,91][i],100+i*4)

# ── 11: SE ABRE EL PORTAL (31,2 s). Re mayor de golpe: platillo, arpa y la nota larga arriba. ──
Dmay=[38,45,50,54,57,62,66,69,74,78]
acorde(CUERDA,11,0,4,Dmay,124); acorde(CORO,11,0,4,[62,66,69,74,78],116); acorde(METAL,11,0,3,[50,57,62,66],120)
nota(BAJO,11,0,4,26,120); nota(TIMP,11,0,2,38,125); nota(PERC,11,0,4,49,125); nota(PERC,11,0,4,57,118); nota(PERC,11,0,2,36,120)
for i in range(16): nota(ARPA,11,i*0.125,0.6,[50,54,57,62,66,69,74,78,81,86,90,93,98,93,90,86][i],96)
for i in range(12): nota(CEL,11,1+i*0.25,0.5,[93,90,86,81,90,86,81,78,86,81,78,74][i],78)
nota(TROMPA,11,0,4,69,112); nota(FLAUTA,11,0,4,93,80)

# ── 12-13: el Sendero al otro lado. El canto: ancho, luminoso, de trompas, coro y cuerdas. ──
for b,chs in [(12,[(0,4,G)]),(13,[(0,2,Em),(2,2,A)])]:
    for bt,d,ch_ in chs:
        acorde(CUERDA,b,bt,d,[ch_[0]-12]+ch_+[x+12 for x in ch_],108); acorde(CORO,b,bt,d,[x+12 for x in ch_],100)
        nota(BAJO,b,bt,d,ch_[0]-12,104); nota(TIMP,b,bt,1,ch_[0]-12,96)
    arp(ARPA,b,chs[0][2],80,oct=24)
    for i in range(8): nota(OST,b,i*0.5,0.5,[chs[0][2][0]+24,chs[0][2][2]+24][i%2],84)
for b,bt,d,p in [(12,0,2,83),(12,2,1,81),(12,3,1,79),(13,0,2,78),(13,2,1.5,76),(13,3.5,0.5,73)]:
    nota(TROMPA,b,bt,d,p-12,112); nota(METAL,b,bt,d,p-12,96); nota(CUERDA,b,bt,d,p,108); nota(CORO,b,bt,d,p,100)
nota(PERC,12,0,3,49,96)

# ── 14-16: llega. Re mayor que se queda quieto; la celesta recuerda el principio y se apaga. ──
acorde(CUERDA,14,0,12,[38,50,57,62,66,69,74],100); acorde(CORO,14,0,8,[62,66,69,74],92); nota(BAJO,14,0,12,26,96)
nota(TROMPA,14,0,6,62,100); nota(TIMP,14,0,2,38,90); nota(PERC,14,0,4,49,70)
for b in (14,15,16): arp(CEL,b,D if b!=15 else G,62-6*(b-14),oct=24)
for b,bt,d,p in [(15,0,2,78),(15,2,1,76),(15,3,1,74),(16,0,4,81)]: nota(FLAUTA,b,bt,d,p,52)
nota(CUERDA,15,0,8,76,40)
for ch in (CUERDA,CORO,BAJO,TROMPA): expr(ch,14,1,16,3.5,127,0,pasos=40)

mid.tracks.append(mido.MidiTrack([mido.MetaMessage('set_tempo',tempo=int(60e6/BPM),time=0),mido.MetaMessage('end_of_track',time=17*BAR+2*PPQ)]))
for ch,l in ev.items():
    tr=mido.MidiTrack(); l.sort(key=lambda x:(x[0],x[1].type=='note_on')); last=0
    for t,m in l: tr.append(m.copy(time=t-last)); last=t
    mid.tracks.append(tr)
sal=sys.argv[1] if len(sys.argv)>1 else 'puerta_de_las_estrellas_guia.mid'
mid.save(sal); print('ok', round(mid.length,1))
