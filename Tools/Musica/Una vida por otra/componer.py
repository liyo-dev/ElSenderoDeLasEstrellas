# Maqueta guía de «Una vida por otra» (título de trabajo): el conjuro de resurrección de Will en la
# cinemática final (Final_01_ElSacrificio, acto 2). Sale del mismo libro y es la misma fórmula de la
# Piedra que el conjuro del portal, así que usa la melodía de «La puerta de las estrellas», pero en
# Re menor: es un sacrificio. En el grito rompe a Re mayor y canta ancho mientras Will se apaga.
#
# Un tiempo = 0,7 s (compás de 2,8 s, como «La puerta»). La pista empieza con el acto 2 (el fundido de
# entrada, «efecto ModuleBeat routine=MusicaDelConjuro»), de modo que:
#   compás 1,7  (4,7 s)  «La fórmula de la Piedra Ancestral...»
#   compás 4    (11,2 s) ≈ los círculos se encienden y Will levita, segunda estrofa (10,9 s)
#   compás 7    (19,6 s) ≈ tercera estrofa, la sacudida (19,2 s)
#   compás 10   (28,0 s) = el grito «¡¡VUELVE CON ELLOS, LIAM!!» (28,1 s). Este compás es de 5 tiempos y
#                          más ancho (0,78 s por tiempo): se ensancha bajo el grito.
#   compás 11   (31,9 s) = EL GOLPE: la luz se lleva a Liam (ConjuroClimax, explosión y destello)
#   compás 12   (34,7 s) ≈ Will se desvanece y sonríe (34,9 s)
#   compás 14-15 (40,3 s) Re mayor quieto que se apaga con el fundido a negro (42,7 → 45,1 s)
import mido, sys
PPQ=480
T_BASE=0.7; T_GRITO=0.78
mid=mido.MidiFile(ticks_per_beat=PPQ)
ev={}
def tiempo_en_tiempos(bar,beat):
    if bar<10: return 4*bar+beat
    if bar==10: return 40+beat
    return 45+4*(bar-11)+beat
def tk(bar,beat): return int(round(tiempo_en_tiempos(bar,beat)*PPQ))
def add(ch,t,msg): ev.setdefault(ch,[]).append((int(t),msg))
def nota(ch,bar,beat,dur,p,v):
    t0=tk(bar,beat); t1=int(round((tiempo_en_tiempos(bar,beat)+dur)*PPQ))
    add(ch,t0,mido.Message('note_on',channel=ch,note=p,velocity=max(1,min(127,int(v)))))
    add(ch,t1-5,mido.Message('note_off',channel=ch,note=p,velocity=0))
def acorde(ch,bar,beat,dur,ps,v):
    for p in ps: nota(ch,bar,beat,dur,p,v)
def prog(ch,pr): add(ch,0,mido.Message('program_change',channel=ch,program=pr))
def expr(ch,bar0,b0,bar1,b1,v0,v1,pasos=24):
    t0=tk(bar0,b0); t1=tk(bar1,b1)
    for i in range(pasos+1):
        add(ch,t0+(t1-t0)*i/pasos,mido.Message('control_change',channel=ch,control=11,value=int(v0+(v1-v0)*i/pasos)))
CORO,CUERDA,BAJO,OST,METAL,TIMP,ARPA,CEL,TROMPA,PERC,FLAUTA,CHELO=0,1,2,3,4,5,6,7,8,9,10,11
for ch,p in [(CORO,52),(CUERDA,48),(BAJO,43),(OST,48),(METAL,61),(TIMP,47),(ARPA,46),(CEL,8),(TROMPA,60),(FLAUTA,73),(CHELO,42)]: prog(ch,p)
for ch in range(12): add(ch,0,mido.Message('control_change',channel=ch,control=91,value=85))
Dm=[50,53,57]; Bb=[46,50,53]; Gm=[43,46,50]; A=[45,49,52]; C=[48,52,55]
D=[50,54,57]; G=[43,47,50]; Em=[52,55,59]; Bm=[47,50,54]
def arp(ch,bar,ch_,v,oct=24,n=8,paso=0.5):
    notas=ch_+[ch_[0]+12]+[ch_[2],ch_[1]]
    for i in range(n): nota(ch,bar,i*paso,paso*1.6,notas[i%5]+oct,v+(i%4==0)*8)

# ── 0-3: lo que queda del Sendero. Will va hacia Liam y habla (4,7 s). Re menor, casi nada. ──
# La celesta dice la primera frase del tema del portal, en menor; luego solo cuerdas altas y arpa
# para dejar sitio a la voz.
for b,ch_ in enumerate([Dm,Bb,Gm,A]):
    acorde(CUERDA,b,0,4,[x+24 for x in ch_],30+b*2)
    nota(ARPA,b,0,4,ch_[0]-12,38)
    nota(BAJO,b,0,4,ch_[0]-24 if ch_[0]-24>=28 else ch_[0]-12,34)
for b,bt,d,p in [(0,0,2,77),(0,2,1,76),(0,3,1,74),(1,0,3,69),(1,3,1,70)]:
    nota(CEL,b,bt,d,p,58); nota(FLAUTA,b,bt,d,p,34)
for b,ch_ in [(2,Gm),(3,A)]: arp(CEL,b,ch_,30,oct=24,n=4,paso=1.0)
for b,bt,d,p in [(2,0,2,62),(2,2,2,65),(3,0,4,64)]: nota(CHELO,b,bt,d,p,48)   # el chelo termina la frase, abajo

# ── 4-6: los círculos y la levitación; segunda estrofa. Entra el coro lejano y la trompa canta la
# segunda frase del tema (en menor) debajo de la voz. ──
nota(TIMP,4,0,2,38,58); nota(PERC,3,3,1,52,30)
for b,ch_ in [(4,Gm),(5,Dm),(6,Bb)]:
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],48+3*(b-4)); acorde(CORO,b,0,4,[x+12 for x in ch_],38+4*(b-4))
    nota(BAJO,b,0,4,ch_[0]-12,56); arp(ARPA,b,ch_,44+3*(b-4),oct=12)
acorde(CUERDA,6,2,2,A+[x+12 for x in A],56)
for b,bt,d,p in [(4,0,2,70),(4,2,1,74),(4,3,1,70),(5,0,2,69),(5,2,1,65),(5,3,1,69),(6,0,1,67),(6,1,1,69),(6,2,1,69),(6,3,1,73)]:
    nota(TROMPA,b,bt,d,p,64); nota(CHELO,b,bt,d,p-12,44)

# ── 7-9: tercera estrofa; todo empuja hacia arriba. Ostinato, timbales, el coro crece. ──
for b,ch_ in [(7,Dm),(8,Bb)]:
    k=b-7
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],62+6*k); acorde(CORO,b,0,4,[x+12 for x in ch_],54+8*k)
    nota(BAJO,b,0,4,ch_[0]-12,70+5*k); nota(TIMP,b,0,1,ch_[0]-12,60+8*k); nota(TIMP,b,2,1,ch_[0]-12,52+8*k)
    pat=[ch_[0]+24,ch_[1]+24,ch_[2]+24,ch_[1]+24]
    for i in range(8): nota(OST,b,i*0.5,0.5,pat[i%4],56+8*k+(i%2==0)*8)
    arp(ARPA,b,ch_,52+5*k,oct=24)
for bt,ch_ in [(0,Gm),(2,A)]:
    acorde(CUERDA,9,bt,2,ch_+[x+12 for x in ch_],76); acorde(CORO,9,bt,2,[x+12 for x in ch_],70)
    nota(BAJO,9,bt,2,ch_[0]-12,80)
    pat=[ch_[0]+24,ch_[1]+24,ch_[2]+24,ch_[1]+24]
    for i in range(4): nota(OST,9,bt+i*0.5,0.5,pat[i%4],74+(i%2==0)*8)
for b,bt,d,p in [(7,0,2,74),(7,2,1,77),(7,3,1,76),(8,0,2,74),(8,2,1,70),(8,3,1,74),(9,0,1.5,76),(9,1.5,0.5,77),(9,2,1,79),(9,3,0.5,81)]:
    nota(TROMPA,b,bt,d,p-12,80); nota(CUERDA,b,bt,d,p,78); nota(CORO,b,bt,d,p,66)
for i in range(14): nota(TIMP,9,i*0.25,0.25,45,40+i*6)        # redoble que crece
for i in range(6): nota(PERC,9,2+i*0.25,0.25,52,30+i*14)       # platillo que crece; medio tiempo de aire antes del grito
expr(CUERDA,7,0,9,3.5,85,127); expr(OST,7,0,9,3.5,80,127); expr(CORO,7,0,9,3.5,85,127)

# ── 10: EL GRITO (28,0 s, 5 tiempos anchos). Si bemol y Do sostenidos bajo la voz (dos peldaños hacia
# el Re), redoble que crece y medio tiempo de silencio antes del golpe. Nada de melodía: canta Will. ──
for bt,d,ch_ in [(0,2.5,Bb),(2.5,2,C)]:
    acorde(CUERDA,10,bt,d,[ch_[0]-12]+ch_+[x+12 for x in ch_],100+6*(bt>0)); acorde(CORO,10,bt,d,[x+12 for x in ch_],96+6*(bt>0))
    nota(BAJO,10,bt,d,ch_[0]-12,104); nota(TIMP,10,bt,1,ch_[0]-12,108); nota(PERC,10,bt,1,36,100)
    acorde(METAL,10,bt,d,[ch_[0]]+[ch_[1]],92+8*(bt>0))
nota(PERC,10,0,2,49,96)
for i in range(16): nota(TIMP,10,0.5+i*0.25,0.25,48,50+i*4)   # redoble largo hacia el golpe
for i in range(8): nota(PERC,10,2.5+i*0.25,0.25,38,60+i*7)    # caja que empuja
for i in range(8): nota(OST,10,2.5+i*0.25,0.25,[60,64,67,72,76,79,84,88][i],92+i*4)
expr(CUERDA,10,0,10,4.4,100,127); expr(CORO,10,0,10,4.4,100,127)

# ── 11: EL GOLPE (31,9 s). Re MAYOR de golpe: la luz cruza y se lleva a Liam. Platillo, arpa,
# metales en la nota larga de arriba. ──
Dmay=[38,45,50,54,57,62,66,69,74,78]
acorde(CUERDA,11,0,4,Dmay,124); acorde(CORO,11,0,4,[62,66,69,74,78],116); acorde(METAL,11,0,3,[50,57,62,66],120)
nota(BAJO,11,0,4,26,120); nota(TIMP,11,0,2,38,125); nota(PERC,11,0,4,49,125); nota(PERC,11,0,4,57,118); nota(PERC,11,0,2,36,120)
for i in range(16): nota(ARPA,11,i*0.125,0.6,[50,54,57,62,66,69,74,78,81,86,90,93,98,93,90,86][i],96)
for i in range(12): nota(CEL,11,1+i*0.25,0.5,[93,90,86,81,90,86,81,78,86,81,78,74][i],76)
nota(TROMPA,11,0,4,69,112); nota(FLAUTA,11,0,4,93,78)

# ── 12-13: Will se apaga y sonríe. El canto del portal, pero tierno: cuerdas, coro y trompa, sin
# metales ni percusión, cada vez más arriba y más suave. ──
for b,chs in [(12,[(0,4,G)]),(13,[(0,2,Em),(2,2,A)])]:
    for bt,d,ch_ in chs:
        acorde(CUERDA,b,bt,d,[ch_[0]-12]+ch_+[x+12 for x in ch_],100-8*(b-12)); acorde(CORO,b,bt,d,[x+12 for x in ch_],92-8*(b-12))
        nota(BAJO,b,bt,d,ch_[0]-12,92-8*(b-12))
    arp(ARPA,b,chs[0][2],70-8*(b-12),oct=24)
for b,bt,d,p in [(12,0,2,83),(12,2,1,81),(12,3,1,79),(13,0,2,78),(13,2,1.5,76),(13,3.5,0.5,73)]:
    nota(TROMPA,b,bt,d,p-12,96-6*(b-12)); nota(CUERDA,b,bt,d,p,100-8*(b-12)); nota(CORO,b,bt,d,p,90-8*(b-12))
expr(METAL,11,3,12,0,127,0,pasos=8)

# ── 14-15: Re mayor quieto que se apaga con el negro; la celesta recuerda la primera frase, ya en
# mayor (lo que empezó en menor acaba en luz). ──
acorde(CUERDA,14,0,8,[38,50,57,62,66,69,74],86); acorde(CORO,14,0,7,[62,66,69,74],78); nota(BAJO,14,0,8,26,82)
nota(TROMPA,14,0,4,62,80)
for b,bt,d,p in [(14,0,2,78),(14,2,1,76),(14,3,1,74),(15,0,3,69),(15,3,1,74)]:
    nota(CEL,b,bt,d,p+12,60-6*(b-14)); nota(FLAUTA,b,bt,d,p,40-6*(b-14))
for ch in (CUERDA,CORO,BAJO,TROMPA,CEL,FLAUTA): expr(ch,14,0,15,3.6,127,0,pasos=40)

meta=mido.MidiTrack()
meta.append(mido.MetaMessage('set_tempo',tempo=int(T_BASE*1e6),time=0))
meta.append(mido.MetaMessage('set_tempo',tempo=int(T_GRITO*1e6),time=tk(10,0)))
meta.append(mido.MetaMessage('set_tempo',tempo=int(T_BASE*1e6),time=tk(11,0)-tk(10,0)))
meta.append(mido.MetaMessage('end_of_track',time=tk(16,2)-tk(11,0)))
mid.tracks.append(meta)
for ch,l in ev.items():
    tr=mido.MidiTrack(); l.sort(key=lambda x:(x[0],x[1].type=='note_on')); last=0
    for t,m in l: tr.append(m.copy(time=t-last)); last=t
    mid.tracks.append(tr)
sal=sys.argv[1] if len(sys.argv)>1 else 'una_vida_por_otra_guia.mid'
mid.save(sal); print('ok', round(mid.length,1))
