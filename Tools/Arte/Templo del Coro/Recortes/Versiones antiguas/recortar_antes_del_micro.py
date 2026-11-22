# Recortes de papel del Templo del Coro para el blockout: cada pieza en su PNG transparente
# y un JSON con el punto de giro (pivote de Unity: 0,0 abajo a la izquierda; 1,1 arriba a la derecha).
# Las piezas se dibujan en coordenadas del modelo (las del lienzo, opción A); cada una se recorta
# a su caja con un margen y se exporta a ESCALA px por unidad.
# Uso: python3 recortar.py  →  carpeta salida/ con los PNG y piezas.json
import json, os, cairosvg

ESCALA = 4
MARGEN = 8
TINTA = '#2A2230'
TRAZO = f'stroke="{TINTA}" stroke-width="5" stroke-linejoin="round" stroke-linecap="round"'
os.makedirs('salida', exist_ok=True)

# (nombre, caja x0 y0 x1 y1 en unidades del modelo, pivote en unidades del modelo, contenido svg)
TENOR = [
    ('tenor_cara', (52, 22, 348, 364), (200, 290), f'''
<g {TRAZO}>
<path d="M150 262 Q200 244 250 262 L246 350 Q200 362 154 350 Z" fill="{TINTA}"/>
<path d="M70 70 Q70 30 200 30 Q330 30 330 70 L337 290 L250 290 L250 262 Q200 244 150 262 L150 290 L63 290 Z" fill="#A79FB0"/>
<circle cx="140" cy="165" r="32" fill="{TINTA}"/>
<circle cx="260" cy="165" r="32" fill="{TINTA}"/>
<path d="M188 175 Q200 160 212 175 L218 220 Q200 232 182 220 Z" fill="#8E8698"/>
<path d="M200 232 Q160 220 130 238 Q105 252 92 236 Q100 268 140 262 Q175 256 200 246 Q225 256 260 262 Q300 268 308 236 Q295 252 270 238 Q240 220 200 232 Z" fill="#5A5266"/>
</g>
<circle cx="146" cy="160" r="12" fill="#F2B544"/>
<circle cx="266" cy="160" r="12" fill="#F2B544"/>
<ellipse cx="200" cy="292" rx="24" ry="12" fill="#F2B544" opacity="0.9"/>
<g fill="none" stroke="#C9C2CF" stroke-width="3" stroke-linecap="round"><path d="M90 80 L120 84"/><path d="M84 270 L100 280"/></g>
'''),
    ('tenor_mandibula', (54, 282, 346, 386), (200, 292), f'''
<g {TRAZO}>
<path d="M61 290 L150 290 Q200 306 250 290 L339 290 L340 300 Q340 350 200 352 Q60 350 60 300 Z" fill="#A79FB0"/>
<path d="M290 312 L316 304 M84 306 L100 318" fill="none" stroke="#C9C2CF" stroke-width="3"/>
<path d="M156 352 L200 340 L244 352 L244 378 L200 366 L156 378 Z" fill="#6B3A63"/>
</g>
<circle cx="200" cy="360" r="6" fill="#F2B544"/>
'''),
    ('tenor_ceja_izq', (88, 92, 178, 138), (165, 110), f'''
<g {TRAZO}><path d="M95 110 L170 98 L172 122 L100 132 Z" fill="#6E6678"/></g>
'''),
    ('tenor_ceja_der', (222, 92, 312, 138), (235, 110), f'''
<g {TRAZO}><path d="M305 110 L230 98 L228 122 L300 132 Z" fill="#6E6678"/></g>
'''),
    ('tenor_mano_izq', (10, 182, 62, 266), (40, 196), f'''
<g {TRAZO}><path d="M18 230 Q10 200 30 190 Q46 186 50 205 L54 250 Q40 262 26 255 Z" fill="#A79FB0"/>
<path d="M30 222 L42 220 M30 236 L44 234" fill="none" stroke-width="3"/></g>
'''),
    ('tenor_mano_der', (338, 182, 390, 266), (360, 196), f'''
<g {TRAZO}><path d="M382 230 Q390 200 370 190 Q354 186 350 205 L346 250 Q360 262 374 255 Z" fill="#A79FB0"/>
<path d="M370 222 L358 220 M370 236 L356 234" fill="none" stroke-width="3"/></g>
'''),
]

ELEMENTOS = {'luz': '#E0A91C', 'fuego': '#E8692E', 'viento': '#3FAE86', 'mente': '#8E76D6', 'oscuro': '#6B3A63'}
CORISTA = []
for el, col in ELEMENTOS.items():
    CORISTA.append((f'corista_cuerpo_{el}', (-66, -30, 66, 112), (0, -10), f'''
<g {TRAZO}>
<path d="M-46 -6 Q-56 70 -60 104 Q0 120 60 104 Q56 70 46 -6 Z" fill="#3B3346"/>
<path d="M-14 -4 L-20 104 L-6 104 L0 2 L6 104 L20 104 L14 -4 Z" fill="{col}"/>
<path d="M-48 -10 Q-40 -22 -26 -16 Q-14 -26 0 -18 Q14 -26 26 -16 Q40 -22 48 -10 Q40 2 26 -4 Q14 6 0 -2 Q-14 6 -26 -4 Q-40 2 -48 -10 Z" fill="#FBF5E8"/>
<rect x="38" y="30" width="12" height="34" rx="3" fill="#FBF5E8"/>
</g>
<ellipse cx="44" cy="22" rx="6" ry="10" fill="#F2B544"/>
'''))
    CORISTA.append((f'corista_burbuja_{el}', (-98, -134, 98, 134), (0, 0), f'''
<ellipse cx="0" cy="0" rx="92" ry="128" fill="{col}" opacity="0.10"/>
<ellipse cx="0" cy="0" rx="92" ry="128" fill="none" stroke="{col}" stroke-width="4"/>
<path d="M-62 -60 Q-80 -20 -60 20" fill="none" stroke="{col}" stroke-width="3" opacity="0.7"/>
<path d="M58 40 Q76 70 54 96" fill="none" stroke="{col}" stroke-width="3" opacity="0.7"/>
'''))
CORISTA += [
    ('corista_cabeza', (-48, -104, 48, -12), (0, -18), f'''
<g {TRAZO}><circle cx="0" cy="-58" r="42" fill="#A79FB0"/></g>
<circle cx="-15" cy="-66" r="6" fill="{TINTA}"/><circle cx="15" cy="-66" r="6" fill="{TINTA}"/>
<path d="M-24 -82 Q-15 -90 -6 -84 M6 -84 Q15 -90 24 -82" fill="none" stroke="{TINTA}" stroke-width="4" stroke-linecap="round"/>
<circle cx="-26" cy="-50" r="6" fill="#E89A9A" opacity="0.6"/><circle cx="26" cy="-50" r="6" fill="#E89A9A" opacity="0.6"/>
'''),
    ('corista_boca', (-16, -58, 16, -22), (0, -40), f'''
<g {TRAZO}><ellipse cx="0" cy="-40" rx="11" ry="13" fill="{TINTA}" stroke-width="3"/></g>
'''),
]

PELIGROS = [
    ('nota_que_cae', (16, 4, 96, 88), (46, 66), f'''
<g {TRAZO}><ellipse cx="46" cy="66" rx="20" ry="15" fill="#A79FB0" transform="rotate(-20 46 66)"/>
<path d="M64 62 L64 10 Q86 18 90 38" fill="none"/></g>
'''),
    ('semicorchea', (16, 2, 100, 88), (46, 66), f'''
<g {TRAZO}><ellipse cx="46" cy="66" rx="20" ry="15" fill="#F2B544" transform="rotate(-20 46 66)"/>
<path d="M64 62 L64 6 Q88 12 94 32" fill="none"/><path d="M64 24 Q88 30 94 50" fill="none"/></g>
'''),
    ('sombra_de_nota', (30, 92, 90, 116), (60, 104), '<ellipse cx="60" cy="104" rx="26" ry="8" fill="#2A2230" opacity="0.35"/>'),
    ('clave_que_se_esquiva', (10, 10, 110, 110), (60, 60), f'''
<circle cx="60" cy="60" r="44" fill="none" stroke="#6B3A63" stroke-width="5" stroke-dasharray="10 8"/>
<path d="M58 100 Q52 110 44 104 Q38 96 48 92 M57 98 L63 18 Q66 8 73 15 Q81 27 59 47 Q37 66 47 82 Q58 97 75 85 Q87 72 71 63 Q55 58 52 73" fill="none" stroke="{TINTA}" stroke-width="6" stroke-linecap="round" stroke-linejoin="round"/>
'''),
    ('regalito', (14, 8, 106, 108), (60, 70), f'''
<g {TRAZO}>
<rect x="26" y="50" width="68" height="52" rx="6" fill="#F07A3C"/>
<rect x="20" y="38" width="80" height="18" rx="5" fill="#F5A066"/>
<path d="M60 38 L60 102" stroke="#F2B544" stroke-width="10"/>
<path d="M60 38 Q40 14 34 30 Q34 40 60 38 Q80 14 86 30 Q86 40 60 38 Z" fill="#F2B544"/>
</g>
'''),
]

# C · El Órgano (personaje de la nave, «¡os presento a mi órgano!»): tubos por pelo, teclas por dientes,
# cejas de enfado y dos patitas para ir dando saltos. Pivote en los pies.
ORGANO = [
    ('organo', (60, 14, 340, 318), (200, 312), f'''
<g {TRAZO}>
<rect x="128" y="40" width="26" height="110" rx="13" fill="#B9B2C2"/>
<rect x="158" y="24" width="26" height="126" rx="13" fill="#B9B2C2"/>
<rect x="188" y="16" width="26" height="134" rx="13" fill="#B9B2C2"/>
<rect x="218" y="24" width="26" height="126" rx="13" fill="#B9B2C2"/>
<rect x="248" y="40" width="26" height="110" rx="13" fill="#B9B2C2"/>
<rect x="98" y="62" width="26" height="88" rx="13" fill="#B9B2C2"/>
<rect x="278" y="62" width="26" height="88" rx="13" fill="#B9B2C2"/>
<path d="M170 286 L168 312 L188 312 M230 286 L232 312 L212 312" fill="none" stroke-width="7"/>
<circle cx="96" cy="216" r="28" fill="#A79FB0"/>
<circle cx="304" cy="216" r="28" fill="#A79FB0"/>
<rect x="100" y="120" width="200" height="172" rx="22" fill="#A79FB0"/>
<path d="M132 164 L182 178 M268 164 L218 178" fill="none" stroke-width="9"/>
<circle cx="164" cy="204" r="24" fill="{TINTA}"/>
<circle cx="236" cy="204" r="24" fill="{TINTA}"/>
<rect x="146" y="234" width="108" height="44" rx="10" fill="{TINTA}"/>
<path d="M156 240 h16 v14 h-16 Z M176 240 h16 v14 h-16 Z M196 240 h16 v14 h-16 Z M216 240 h16 v14 h-16 Z M236 240 h12 v14 h-12 Z" fill="#F4EFE6" stroke-width="3"/>
</g>
<circle cx="168" cy="198" r="9" fill="#F2B544"/>
<circle cx="240" cy="198" r="9" fill="#F2B544"/>
<g fill="none" stroke="#C9C2CF" stroke-width="3" stroke-linecap="round"><path d="M114 140 L140 136"/><path d="M118 276 L130 282"/></g>
'''),
]

# Atrezo de teatro: los peligros como decorados de cartón pintado (planos, de cara al público). Pivote
# abajo en el centro (salen del suelo), menos el rayo, que va tumbado en el suelo (pivote en el centro).
CARTON = '#7A5230'
ATREZO = [
    ('atrezo_columna', (-44, 0, 44, 240), (0, 240), f'''
<g {TRAZO}>
<rect x="-30" y="34" width="60" height="176" fill="#B9B2C2"/>
<rect x="-42" y="8" width="84" height="28" rx="6" fill="#A79FB0"/>
<rect x="-42" y="208" width="84" height="28" rx="6" fill="#A79FB0"/>
<path d="M-12 44 L-12 200 M0 44 L0 200 M12 44 L12 200" fill="none" stroke-width="3"/>
</g>
<g fill="none" stroke="#D6D0DC" stroke-width="3" stroke-linecap="round"><path d="M-24 60 L-24 110"/></g>
'''),
    ('atrezo_llama', (-124, 0, 124, 124), (0, 124), f'''
<g {TRAZO}>
<path d="M-118 116 Q-110 60 -86 36 Q-90 70 -66 76 Q-74 30 -40 6 Q-44 56 -16 60 Q-20 20 8 0 Q10 44 34 50 Q30 18 58 10 Q54 52 80 58 Q86 34 104 28 Q100 70 118 116 Z" fill="#E8692E"/>
<path d="M-80 116 Q-76 86 -58 74 Q-58 96 -38 98 Q-42 70 -16 56 Q-14 86 6 90 Q6 66 30 58 Q30 90 52 94 Q58 78 72 74 Q70 100 82 116 Z" fill="#F2B544" stroke-width="3"/>
<rect x="-122" y="112" width="244" height="10" rx="3" fill="{CARTON}"/>
</g>
'''),
    ('atrezo_latigo', (-144, 0, 144, 44), (0, 44), f'''
<g {TRAZO}>
<path d="M-140 40 L-140 22 Q-120 2 -100 18 Q-90 26 -96 34 Q-80 8 -56 16 Q-40 24 -48 34 Q-30 6 -6 16 Q8 24 0 34 Q18 6 42 16 Q56 24 50 34 Q66 6 90 16 Q104 24 98 34 Q114 8 140 20 L140 40 Z" fill="#5A3A5E"/>
</g>
<g fill="none" stroke="#9A7BA0" stroke-width="3" stroke-linecap="round"><path d="M-128 30 Q-118 20 -108 26"/><path d="M-30 28 Q-20 18 -10 24"/><path d="M68 28 Q78 18 88 24"/></g>
'''),
    # Lo que cae del telar en cada «¡corred, corred!»: bajan a plomo, colgados de su cuerda.
    ('telar_saco', (-58, -385, 58, 62), (0, 60), f'''
<path d="M0 -385 L0 -34" stroke="{TINTA}" stroke-width="5"/>
<g {TRAZO}>
<path d="M-14 -28 Q-52 -8 -54 30 Q-56 60 0 60 Q56 60 54 30 Q52 -8 14 -28 Z" fill="#C8A46A"/>
<path d="M-18 -34 L18 -34 L14 -22 L-14 -22 Z" fill="#8C6A3E"/>
<circle cx="0" cy="-40" r="7" fill="none"/>
</g>
<g fill="none" stroke="#8C6A3E" stroke-width="3" stroke-linecap="round"><path d="M-36 10 L-30 40"/><path d="M-6 0 L-2 44"/><path d="M26 6 L22 42"/></g>
'''),
    ('telar_luna', (-64, -385, 64, 62), (0, 60), f'''
<g stroke="{TINTA}" stroke-width="5"><path d="M-34 -385 L-34 -46"/><path d="M20 -385 L20 -56"/></g>
<g {TRAZO}>
<path d="M20 -56 A60 60 0 1 0 20 56 Q-10 40 -12 0 Q-10 -40 20 -56 Z" fill="#F2D16B"/>
</g>
<circle cx="-38" cy="-8" r="5" fill="{TINTA}"/>
<path d="M-46 14 Q-38 22 -30 14" fill="none" stroke="{TINTA}" stroke-width="4" stroke-linecap="round"/>
'''),
    ('telar_estrella', (-64, -385, 64, 52), (0, 50), f'''
<path d="M0 -385 L0 -62" stroke="{TINTA}" stroke-width="5"/>
<g {TRAZO}><path d="M0.0 -62.0 L15.3 -21.0 L59.0 -19.2 L24.7 8.0 L36.4 50.2 L0.0 26.0 L-36.4 50.2 L-24.7 8.0 L-59.0 -19.2 L-15.3 -21.0 Z" fill="#F2B544"/></g>
<g fill="#FFF3C4"><circle cx="-14" cy="-6" r="4"/><circle cx="12" cy="8" r="3"/><circle cx="2" cy="-22" r="3"/></g>
'''),
    ('atrezo_rayo', (-16, 0, 16, 480), (0, 240), f'''
<g stroke="{TINTA}" stroke-width="3" stroke-linejoin="round"><path d="M1 0 L-15 40 L1 80 L-15 120 L1 160 L-15 200 L1 240 L-15 280 L1 320 L-15 360 L1 400 L-15 440 L1 480 L15 480 L-1 440 L15 400 L-1 360 L15 320 L-1 280 L15 240 L-1 200 L15 160 L-1 120 L15 80 L-1 40 L15 0 Z" fill="#E2D8FF"/></g>
'''),
]

# El público del Tenor: muñecos de cartón que se ha hecho él (lleva siglos solo), clavados en un palo.
PUBLICO = []
for letra, vestido, extra in (
        ('a', '#B5524A', '<path d="M-20 4 Q0 -14 20 4 Z" fill="#5A3A2E"/>'),
        ('b', '#4A6FA5', '<rect x="-18" y="-14" width="36" height="12" rx="3" fill="#3B3346"/><rect x="-26" y="-4" width="52" height="6" rx="3" fill="#3B3346"/>'),
        ('c', '#5E8C61', '<path d="M8 2 L22 -8 L22 10 Z M8 2 L-6 -8 L-6 10 Z" fill="#E8692E"/>')):
    PUBLICO.append((f'publico_muneco_{letra}', (-40, -18, 40, 170), (0, 170), f'''
<g {TRAZO}>
<rect x="-4" y="96" width="8" height="74" fill="{CARTON}"/>
<path d="M-24 44 L24 44 L32 104 L-32 104 Z" fill="{vestido}"/>
<path d="M-24 50 L-36 80 M24 50 L36 80" fill="none" stroke-width="7"/>
<circle cx="0" cy="20" r="24" fill="#D9B98C"/>
{extra}
</g>
<circle cx="-8" cy="18" r="3.5" fill="{TINTA}"/>
<circle cx="8" cy="18" r="3.5" fill="{TINTA}"/>
<path d="M-7 29 Q0 35 7 29" fill="none" stroke="{TINTA}" stroke-width="3" stroke-linecap="round"/>
<circle cx="-14" cy="27" r="4" fill="#E5A08A" opacity="0.8"/>
<circle cx="14" cy="27" r="4" fill="#E5A08A" opacity="0.8"/>
'''))

piezas = {}
for nombre, (x0, y0, x1, y1), (px, py), cuerpo in TENOR + CORISTA + PELIGROS + ORGANO + ATREZO + PUBLICO:
    x0 -= MARGEN; y0 -= MARGEN; x1 += MARGEN; y1 += MARGEN
    w, h = x1 - x0, y1 - y0
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{x0} {y0} {w} {h}" width="{w * ESCALA}" height="{h * ESCALA}">{cuerpo}</svg>'
    cairosvg.svg2png(bytestring=svg.encode(), write_to=f'salida/{nombre}.png')
    with open(f'salida/{nombre}.svg', 'w', encoding='utf-8') as f: f.write(svg)
    piezas[nombre] = {'png': f'{nombre}.png', 'ancho_px': w * ESCALA, 'alto_px': h * ESCALA,
                      'pivote_unity': [round((px - x0) / w, 4), round(1 - (py - y0) / h, 4)],
                      'posicion_en_el_modelo': [px, py]}

# Montaje: dónde va cada pieza respecto a la cara (pivote de la cara = origen), en unidades del modelo
# (1 unidad = 1 px a escala 1; en Unity, con 100 px por unidad y ESCALA 4: 1 unidad del modelo = 0,04 m).
base = piezas['tenor_cara']['posicion_en_el_modelo']
for n in piezas:
    if n.startswith('tenor_'):
        p = piezas[n]['posicion_en_el_modelo']
        piezas[n]['desde_la_cara'] = [p[0] - base[0], -(p[1] - base[1])]
with open('salida/piezas.json', 'w', encoding='utf-8') as f:
    json.dump({'escala_px_por_unidad_del_modelo': ESCALA,
               'nota': 'pivote_unity en coordenadas normalizadas del sprite (0,0 abajo izquierda). desde_la_cara: desplazamiento del pivote de cada pieza respecto al de la cara, en unidades del modelo, eje Y hacia arriba.',
               'movimientos': {'tenor_cara': 'cabecea en cada tiempo (±4°)', 'tenor_ceja_izq/der': 'sube medio tiempo antes de un peligro',
                               'tenor_mandibula': 'baja en el PUM y vuelve con rebote', 'tenor_mano_izq/der': 'arriba en el 1, abajo en el 3',
                               'corista_cabeza': 'cabecea en cada tiempo', 'corista_boca': 'se abre (escala Y) en el ¡Ah! del 2 y el 4',
                               'corista_burbuja_*': 'escudo del elemento; se rompe con esa magia',
                               'nota_que_cae + sombra_de_nota': 'la sombra crece hasta el PUM', 'semicorchea': 'cae delante en cada «pi» del final y rompe la losa donde cae', 'clave_que_se_esquiva': 'gira; no se devuelve',
                               'regalito': 'se devuelve con B', 'organo': 'salta hacia el jugador en cada tiempo y cae encima (se aplasta al caer)', 'atrezo_*': 'los peligros como decorado de cartón: columna, llamarada, ola (latigazo) y rayo (tumbado)', 'telar_*': 'lo que cae del telar colgado de su cuerda (saco de arena, luna y estrella de cartón): baja a plomo', 'publico_muneco_*': 'el público de cartón del Tenor, en su palo: bota en el platillo y en ¡Bravo!'},
               'piezas': piezas}, f, ensure_ascii=False, indent=1)
print(len(piezas), 'piezas')
