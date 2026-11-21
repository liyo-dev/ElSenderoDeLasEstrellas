"""SFX del combate contra el Demonio (19 nov 2026, INC-DEMONIO): estilo del conjuro del prólogo
(disenar_hechizos_conjuro.py): lo que carga SUBE, chasquido + golpe en 170–300 Hz, rugido corto que se
aclara. Síntesis propia; no se usa audio de fuera.
Salen (mono, suenan en 3D):
  - SFX_Demonio_Golpe: le entra un golpe (chasquido, golpe medio y gruñido corto que se corta).
  - SFX_Demonio_Rechazo: golpe con el aro apagado (tintineo de hierro y chispa que se ahoga).
  - SFX_Demonio_Invocacion: invoca la lluvia (carga oscura que sube 1,3 s y salida hacia el cielo).
  - SFX_Demonio_TeleSalida: se hunde en su sombra (aspiración que sube y «plop» grave).
  - SFX_Demonio_TeleLlegada: brota del suelo (retumbo que crece 0,6 s y estallido).
  - SFX_Demonio_Esfumarse: se deshace en ceniza (siseo que sube y se aclara, chispas que se apagan).
"""
import numpy as np, os, sys
sys.path.insert(0, '/work/sfx')
src = open('/work/sfx/disenar_hechizos_conjuro.py').read()
exec(src[:src.rindex("if __name__ == '__main__':")])
OUT = '/work/sfx/demonio'; os.makedirs(OUT, exist_ok=True)
S = {}

# Golpe recibido: «DOSH» seco con un gruñido que se corta (el demonio acusa).
d = 0.7; t = T(d); n = len(t)
grun = zumbido(lambda tt: 150*np.exp(-tt*2.0)+70, d, 5, 0.03, 120, 2500, 4.0)*np.exp(-t/0.12)*np.clip(t/0.01, 0, 1)
y = (pon(n, chasquido(0.35, 1101, 0.035), 0)*1.3 + pon(n, golpe(0.5, 300, 150, 0.1), 0)*1.3 +
     grun*0.55 + tex_oscuro(d, 1102)*np.exp(-t/0.1)*0.4)
S['Demonio_Golpe'] = forma(acabar(y, graves=0.65, corte=150, sala=0.12, dsala=0.7, seed=1101), 0.0, 0.25, 0.12)

# Rechazo: el hierro del aro suena y la chispa se ahoga.
d = 0.8; t = T(d); n = len(t)
hierro = (fm_bell(1760, d, 1.41, 3.0, 0.18)*0.6 + fm_bell(2489, d, 1.73, 2.0, 0.12)*0.4)
fz = chisporroteo(d, 1103, 1500, 7000, (24, 40))*np.exp(-t/0.12)
y = hierro + fz*0.6 + pon(n, chasquido(0.25, 1104, 0.02), 0)*0.7
S['Demonio_Rechazo'] = acabar(y, graves=0.3, sala=0.15, dsala=0.8, seed=1103)

# Invocación de la lluvia: carga oscura 1,3 s que sube y salida hacia el cielo (canto que sube).
c = carga('Oscuro', 1.3, 1105, 1.6) + carga('Fuego', 1.3, 1106, 1.2)*0.5
s = salida('Fuego', 1107, 1.2, 1.8, 1.3)
n = len(c)+len(s); y = pon(n, c, 0) + pon(n, s, 1.3)
sube = zumbido(lambda tt: 600*(1+1.5*np.clip(tt/1.2, 0, 1)), 1.2, 3, 0.01, 400, 9000, 2.5)*expdec(int(1.2*SR), 0.5, 0.01)
y = y + pon(n, sube*0.4, 1.3)
S['Demonio_Invocacion'] = forma(acabar(y, graves=0.7, corte=150, sala=0.25, dsala=1.8, seed=1105), 1.3, 2.2, 0.3)

# Salida del teletransporte: aspiración que sube y «plop» grave al hundirse.
d = 0.9; t = T(d); n = len(t)
asp = hp(sweep_filter(noise(d, 1108), 300, 7000, 'low', curve=0.6), 250)*np.clip(t/0.45, 0, 1)**2*(t < 0.47)
tono = zumbido(lambda tt: 200*(2.2**np.clip(tt/0.45, 0, 1)), d, 5, 0.015, 200, 6000, 3.0)*np.clip(t/0.45, 0, 1)**2*(t < 0.47)
plop = pon(n, golpe(0.45, 220, 70, 0.12), 0.45)*1.4 + pon(n, chasquido(0.2, 1109, 0.02), 0.45)*0.5
y = asp*0.7 + tono*0.5 + plop + pon(n, tex_oscuro(0.4, 1110)*np.exp(-T(0.4)/0.1), 0.46)*0.6
S['Demonio_TeleSalida'] = acabar(y, graves=0.8, corte=120, sala=0.18, dsala=1.0, seed=1108)

# Llegada: retumbo del suelo que crece 0,6 s y estallido (el demonio brota).
d = 1.4; t = T(d); n = len(t)
ret = rugido(0.6, 40, 400, (10, 26), 1111, 1.2)*np.clip(T(0.6)/0.6, 0, 1)**1.5
est = impacto('Oscuro', 1112, False)
y = pon(n, ret*0.8, 0) + pon(n, est, 0.6) + pon(n, golpe(0.6, 260, 110, 0.2), 0.6)*1.0
S['Demonio_TeleLlegada'] = forma(acabar(y, graves=0.75, corte=130, sala=0.15, dsala=1.0, seed=1111), 0.6, 1.0, 0.18)

# Esfumarse: siseo de ceniza que sube y se aclara, canto que sube y chispas que se apagan.
d = 2.6; t = T(d); n = len(t)
siseo = hp(sweep_filter(noise(d, 1113), 1200, 9000, 'low', curve=0.7), 600)*np.clip(t/0.25, 0, 1)*np.exp(-t/1.1)
canto = zumbido(lambda tt: 500*(1+0.9*np.clip(tt/2.0, 0, 1)), d, 3, 0.006, 400, 8000, 2.0)*np.clip(t/0.4, 0, 1)*np.exp(-t/0.9)
chis = chisporroteo(d, 1114, 2500, 10000, (10, 30))*np.exp(-t/0.8)
camp = sum(pon(n, fm_bell(f, 1.6, 2.0, 1.2, 0.6), 0.25+k*0.22) for k, f in enumerate((587, 698, 880, 1175)))
y = pon(n, golpe(0.6, 200, 80, 0.25), 0)*0.8 + siseo*0.8 + canto*0.35 + chis*0.6 + camp*0.22
S['Demonio_Esfumarse'] = acabar(y, graves=0.5, corte=150, sala=0.3, dsala=2.2, seed=1113)

for k, y in S.items():
    write(f'{OUT}/SFX_{k}.wav', y, -2.0)
    print(k, round(len(y)/SR, 2), 's')
