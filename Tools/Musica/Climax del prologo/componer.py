# Maqueta guía del clímax del prólogo (estilo conjuro de anime de los 90): misma duración y golpes que la cinemática.
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
# ── 11: despedida de Liora: casi nada, violín agudo y coro pp ──
nota(CUERDA,11,0,4,A4+12,38); acorde(CORO,11,0,4,[62,65,69],32)
for bt,p in [(0,81),(1,77),(2,74),(3,69)]: nota(CEL,11,bt,1,p,40)
# ── 12: el respiro, abre los ojos: dron y latido que crece ──
nota(BAJO,12,0,4,D2-12,45); nota(TIMP,12,0,1,D2,70); nota(TIMP,12,2,1,D2,80)
for i in range(8): nota(PERC,12,2+i*0.25,0.25,52 if i<7 else 49,30+i*12)   # platillo que crece
# ── 13: EL GRITO: tutti en Si bemol, fanfarria ──
acorde(CUERDA,13,0,4,[34,46,50,53,58,62,65],118); acorde(CORO,13,0,4,[58,62,65,70],112)
for bt,d,p in [(0,1,70),(1,0.5,65),(1.5,0.5,70),(2,2,74)]: nota(METAL,13,bt,d,p,120); nota(METAL,13,bt,d,p-12,115)
nota(TIMP,13,0,1,34+12,120); nota(PERC,13,0,2,49,120); nota(PERC,13,0,1,36,120); nota(BAJO,13,0,4,34,110)
for i in range(4): nota(TAIKO,13,i,1,45,110)
# ── 14-15: los dos sostienen y chocan: ostinato en semicorcheas, sube sin parar ──
for b,ch_ in [(14,Gm),(15,A)]:
    acorde(CUERDA,b,0,4,ch_+[x+12 for x in ch_],100+6*(b-14)); nota(BAJO,b,0,4,ch_[0]-12,105)
    base=[ch_[0]+24,ch_[0]+26,ch_[0]+27,ch_[0]+26]
    for i in range(16):
        if b==15 and i>=14: break           # medio tiempo de silencio antes del golpe
        nota(OST,b,i*0.25,0.25,base[i%4]+(i//4)*(1 if b==15 else 0),90+i*2)
    for i in range(16 if b==14 else 14): nota(TAIKO,b,i*0.25,0.25,45,70+i*3+(b-14)*15)
coro=[(14,0,2,67),(14,2,2,69),(15,0,1,70),(15,1,1,71),(15,2,1,72),(15,3,0.5,73)]
for b,bt,d,p in coro: nota(CORO,b,bt,d,p,100+(b-14)*15); nota(METAL,b,bt,d,p-12,100+(b-14)*15)
for i in range(14): nota(PERC,15,i*0.25,0.25,38,60+i*4)   # redoble que crece
expr(OST,14,0,15,3.5,90,127); expr(CUERDA,14,0,15,3.5,95,127)
# ── 16: ESTALLIDO (48,4 s): Re mayor de golpe, y el acorde que brilla y se apaga ──
Dmay=[26,38,45,50,54,57,62,66,69,74,78]
for ch in (CUERDA,TREM): acorde(ch,16,0,10,Dmay[2:],127)
acorde(CORO,16,0,10,[62,66,69,74],120); acorde(METAL,16,0,3,[50,54,57,62],127); nota(BAJO,16,0,10,26,127)
nota(TIMP,16,0,2,38,127); nota(PERC,16,0,4,49,127); nota(PERC,16,0,4,57,127); nota(PERC,16,0,2,35,127); nota(PERC,16,0,2,36,127)
nota(TAIKO,16,0,2,45,127)
for i in range(12): nota(ARPA,16,i*0.125,0.5,[62,66,69,74,78,81][i%6]+(12 if i>=6 else 0),100)
for i in range(16): nota(CEL,16,1+i*0.5,0.5,[93,90,86,81,78,74][i%6],70-i*3)
for ch in (CUERDA,TREM,CORO,BAJO,CEL,ARPA): expr(ch,16,0.5,18,2,127,0)
mid.tracks.append(mido.MidiTrack([mido.MetaMessage('set_tempo',tempo=int(60e6/BPM),time=0),mido.MetaMessage('end_of_track',time=18*BAR+3*PPQ)]))
for ch,l in ev.items():
    tr=mido.MidiTrack(); l.sort(key=lambda x:(x[0],x[1].type=='note_on')); last=0
    for t,m in l: tr.append(m.copy(time=t-last)); last=t
    mid.tracks.append(tr)
mid.save('/tmp/maqueta/climax_prologo_guia.mid'); print('ok', mid.length)
