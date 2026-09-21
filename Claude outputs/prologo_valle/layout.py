# -*- coding: utf-8 -*-
"""EL VALLE DEL PRÓLOGO — distribución del pueblo y su entorno.

La escena representa una aldea pequeña pero vivida: una calle continua conecta la ladera occidental,
la plaza y el puente oriental. Las casas y los hitos narrativos conservan espacio transitable; la
vegetación se reúne en rodales periféricos para enmarcar el valle sin tapar a la gente ni la villa.

DOS REGLAS QUE ORDENAN TODO EL DECORADO
---------------------------------------
1. ESCALA 1.0, SIEMPRE. El Fantasy Kingdom Pack está modelado a tamaño casi real (una casa mide
   6 m, la iglesia 9,9 m) y los personajes de este juego son de proporciones de dibujo (Will mide
   1,15 m). La tentación es encoger los edificios — es lo que estaba pasando: escalas de 0,43 a
   0,58 puestas a ojo, una distinta por objeto. Pero la escena que el jugador ya conoce, la
   habitación de Will (`WillHouse.unity`), usa este mismo pack A ESCALA 1: Room01_a01 son 7 x 3 m
   de techo con Will de 1,15 m dentro. Esa es la proporción del juego — personaje pequeño, mundo
   normal, como en Ni no Kuni. El valle en miniatura era lo incoherente, no el pack.

2. LA CÁMARA MIRA AL OESTE. El eje de acción queda fijado a 90° (+X) por SetActionAxisBeat, y de
   ahí sale que en un two-shot la cámara se pone al este de la pareja y mira al oeste, y que en un
   plano cerrado el fondo es la espalda del sujeto girada 35°. Se ha comprobado plano a plano con
   el port del solver (shotmath.py): de los dieciséis encuadres, catorce miran entre el suroeste y
   el norte. Así que el pueblo, la iglesia y el relieve van al OESTE y al NOROESTE, en capas de
   profundidad; el río y el puente, al este, se ven poco y son baratos a propósito.

PACKS DEL PUEBLO Y DEL TRÁNSITO
------------------------------
Casas, árboles y lomas mantienen el aspecto del Fantasy Kingdom Pack. Para tener una calle
continua en lugar de las losas redondas, la calzada y el puente usan dos piezas compatibles del
bundle RPG Tiny Fantasy World: RoadE02 y Bridge06. Se mantienen juntos como un conjunto de tránsito
acotado entre la aldea y el río.
"""

S = 100.0  # cota del suelo

# ── marcas de actuación ───────────────────────────────────────────────────────
# (nombre, x, z, grados hacia donde mira). El nombre es el que usan los beats.
MARCAS = [
    ("M_Apertura",        6006.2, 6001.4, 135),   # mirando al este (amanece) con el pueblo a su espalda: la cámara
                                                  # queda al noreste y el plano recoge la calle entera y la iglesia
    ("M_Horno",           5995.6, 6002.2,   0),   # frente al horno de la panadería
    ("M_Carreta",         6008.4, 6000.6, 135),
    ("M_Globo",           5990.8, 6001.6, 300),
    ("M_Mesa",            6010.0, 6001.5,  40),
    ("M_Mesa_Liora",      6012.5, 6003.7, 220),
    ("M_Viga",            6004.6, 5999.9,  35),
    ("M_Despedida_Mago",  6017.4, 6001.4, 155),
    ("M_Despedida_Liora", 6018.0, 5999.9, 335),
    ("M_Duelo_Mago",      6000.5, 5999.0,   0),
    ("M_Duelo_Oscuro",    6000.5, 6005.0, 180),
    ("M_Espera_Oscuro",   6000.0, 6068.0, 180),   # fuera del valle hasta la fase 5
]

# ── decorado ──────────────────────────────────────────────────────────────────
# (grupo, prefab, nombre, x, y, z, rotY, escala)
# y es ABSOLUTA. Los edificios llevan el pivot en su base (base y ≈ -0,2), así que van a 100.
D = []
def p(grupo, prefab, nombre, x, z, rot=0.0, y=S, esc=1.0):
    D.append((grupo, prefab, nombre, x, y, z, rot, esc))

# ---- LA CALLE: fachadas norte (miran al sur) ---------------------------------
# La iglesia cierra la calle por el oeste: es el fondo de los planos del horno y del duelo, y el
# campanario es donde se engancha el globo. 9,9 m de alto: la única silueta vertical del valle.
p("01_Aldea_Norte", "Church01_a01.prefab",  "Iglesia",        5986.0, 6007.6, 172)
p("01_Aldea_Norte", "Building03_a01.prefab","Panaderia",      5995.2, 6007.6, 180)
p("01_Aldea_Norte", "Building01_a01.prefab","Casa_Norte_02",  6006.8, 6008.6, 176)
p("01_Aldea_Norte", "Building12_a01.prefab","Casa_Norte_03",  6013.0, 6008.2, 190)
p("01_Aldea_Norte", "Building06_a01.prefab","Casa_Norte_04",  5978.5, 6009.0, 168)

# ---- fachadas sur (miran al norte) ------------------------------------------
p("02_Aldea_Sur",   "Building06_a01.prefab","Casa_Sur_01",    5991.5, 5992.0,   6)
p("02_Aldea_Sur",   "Building01_a02.prefab","Casa_Sur_02",    5999.8, 5991.6,   0)
p("02_Aldea_Sur",   "Building03_a02.prefab","Casa_Sur_03",    6007.6, 5992.2, 352)
p("02_Aldea_Sur",   "Building12_a02.prefab","Casa_Sur_04",    5982.0, 5993.5,  14)

# ---- la plaza ---------------------------------------------------------------
p("03_Plaza", "Well01_a01.prefab",    "Pozo",            5999.2, 6001.4,  20)
p("03_Plaza", "Light03_a01.prefab",   "Farol_01",        5997.0, 6003.0,   0)
p("03_Plaza", "Light03_a01.prefab",   "Farol_02",        6006.5, 6003.2,   0)
p("03_Plaza", "Light03_a01.prefab",   "Farol_03",        6001.0, 5996.6,   0)
p("03_Plaza", "Signpost02_a01.prefab","Poste_Indicador", 6013.2, 5998.4,  65)
p("03_Plaza", "Cart01.prefab",        "Carro_Plaza",     5993.0, 5998.6, 110)
p("03_Plaza", "Barrel03_a01.prefab",  "Barril_01",       5992.2, 6004.4,  25)
p("03_Plaza", "Barrel03_a02.prefab",  "Barril_02",       5991.6, 6003.8, 200)
p("03_Plaza", "Wood02_a01.prefab",    "Lena_Plaza",      6012.6, 5996.4,  40)
p("03_Plaza", "Trough01_a01.prefab",  "Abrevadero",      6004.2, 6004.2,  90)

# ---- estación 1: el horno de la panadería -----------------------------------
# Delante de la panadería, a 1,6 m de la marca del Archimago: en un two-shot de los dos, la pareja
# ocupa el centro y detrás cae la iglesia (la cámara mira a 288°, y la iglesia está a 288°).
p("04_Estaciones", "Counter01_a01.prefab", "Horno_Mostrador", 5995.6, 6003.9, 180)
p("04_Estaciones", "Fire01_a01.prefab",    "Horno_Fuego",     5995.6, 6004.3, 180)
p("04_Estaciones", "Bread01_a01.prefab",   "Horno_Pan_01",    5995.0, 6003.8, 15)
p("04_Estaciones", "Bread02_a01.prefab",   "Horno_Pan_02",    5996.2, 6003.9, 160)
p("04_Estaciones", "Wood02_a02.prefab",    "Horno_Lena",      5993.9, 6004.2, 70)
p("04_Estaciones", "Barrel01_a01.prefab",  "Horno_Barril",    5997.4, 6004.0, 0)
p("04_Estaciones", "Shop_sign01_a01.prefab","Panaderia_Rotulo",5995.2, 6004.6, 180)

# ---- estación 2: la carreta atascada ----------------------------------------
p("04_Estaciones", "Cart06.prefab",       "Carreta",          6009.8, 5999.2, 28)
p("04_Estaciones", "Wheel01_a01.prefab",  "Carreta_Rueda",    6008.9, 5998.3, 75)
p("04_Estaciones", "Hay01_a01.prefab",    "Carreta_Heno",     6011.2, 5998.4, 10)
p("04_Estaciones", "Vegetables02_a01.prefab","Carreta_Verdura",6009.2, 5998.6, 0)

# ---- estación 3: el globo del campanario ------------------------------------
p("04_Estaciones", "Balloon01_a05.prefab","Globo",            5987.4, 6004.2, 0, y=S+5.9)
p("04_Estaciones", "Rope01_a01.prefab",   "Globo_Cuerda",     5987.4, 6004.2, 0, y=S+4.8, esc=(0.8,3.0,0.8))

# ---- estación 4: la mesa junto al río ---------------------------------------
p("04_Estaciones", "Table01_a01.prefab",  "Mesa",             6011.4, 6002.6, 35)
p("04_Estaciones", "Chair01_a01.prefab",  "Mesa_Silla_01",    6010.7, 6003.7, 305)
p("04_Estaciones", "Chair01_a02.prefab",  "Mesa_Silla_02",    6012.2, 6001.5, 125)
p("04_Estaciones", "Bowl01_a01.prefab",   "Mesa_Cuenco",      6011.1, 6002.5, 0, y=S+0.93)
p("04_Estaciones", "Bread01_a01.prefab",  "Mesa_Pan",         6011.7, 6002.7, 40, y=S+0.93)
p("04_Estaciones", "Milk01_a01.prefab",   "Mesa_Leche",       6011.5, 6002.3, 0, y=S+0.93)
p("04_Estaciones", "Leaflet01_a01.prefab","Mesa_Carta",       6011.2, 6002.9, 120, y=S+0.93)

# ---- estación 5: la viga que hay que sostener -------------------------------
# Cruza la calle en diagonal, apoyada en la casa del norte. Es la única cosa del decorado que se
# lee sola en silueta: por eso va justo en el eje de la calle, con la iglesia al fondo.
# La viga cae DESDE el alero de la casa del norte y baja cruzando la calle hasta donde está el
# Archimago: su extremo bajo queda justo encima de la marca M_Viga, a metro y medio del suelo. Es
# lo que hace que "sostener" se entienda sin explicarlo — el ángulo se ha calculado para que apunte
# exactamente ahí (ver el cálculo en la bitácora de la sesión), no a ojo.
p("08_Incendio",   "Beam01_a01.prefab",   "Viga",             6006.8, 6004.8, (0,294.2,103.6), y=S+2.6, esc=(1.5,5.6,1.5))
p("08_Incendio",   "Wood01_a01.prefab",   "Viga_Escombro_01", 6005.6, 6001.4, 20)
p("08_Incendio",   "Wood01_a02.prefab",   "Viga_Escombro_02", 6007.4, 6002.6, 200)

# ---- calle principal --------------------------------------------------------
# RoadE02 mide 13,4 x 2,7 m. Unity importa la malla extendiéndola hacia +X desde el pivote;
# los pivotes quedan al oeste para que el último tramo acabe en la orilla (x=6018,3), sin
# prolongar la calzada sobre el agua. Un ancho visual de 3,7 m encaja mejor con la plaza.
for i, extremo_oeste in enumerate((5978.1, 5991.5, 6004.9)):
    p("00_Camino", "RoadE02.prefab", "Camino_Tramo_%02d" % (i + 1), extremo_oeste, 5998.64,
      0, y=S + 0.03, esc=(1.0, 1.0, 1.35))

# ---- el río y el puente (este; se ve poco a propósito) ----------------------
# El río de verdad es una lámina de agua a ras de suelo con las orillas cubiertas de piedra y
# juncos: el suelo del valle es un plano liso y no se puede excavar un cauce, así que el truco es
# esconder la junta. Se ve en dos planos de los diecisiete, así que no merece más.
# Bridge06 mide 14,3 m de largo y su pivote está en el centro. A escala 0,7 salva los 9,5 m del
# cauce con un pequeño apoyo en ambas orillas; el tablero queda alineado con la calle en z=5998,64.
p("05_Rio", "Bridge06.prefab", "Puente", 6023.0, 5998.64, 90, y=S + 0.50, esc=(0.7, 0.35, 0.7))
_orillas = [(6018.6, 5988), (6018.2, 5992), (6018.8, 5996), (6018.4, 6004), (6018.9, 6008),
            (6018.3, 6012), (6027.4, 5990), (6027.8, 5994), (6027.2, 5998), (6027.6, 6006),
            (6027.3, 6010), (6027.7, 6014)]
for i, (x, z) in enumerate(_orillas):
    p("05_Rio", "Rock01_a0%d.prefab" % (1 + i % 6), "Ribera_Roca_%02d" % (i + 1), x, z,
      (i * 47) % 360, esc=round(0.8 + 0.1 * (i % 4), 2))
for i, (x, z) in enumerate(_orillas[::2]):
    p("05_Rio", ["Plant02_a01.prefab", "Plant03_a01.prefab", "Grass03_a01.prefab"][i % 3],
      "Ribera_Junco_%02d" % (i + 1), x + (0.5 if x < 6023 else -0.5), z + 1.5, (i * 83) % 360)

# ---- vegetación: tres rodales abiertos, fuera de la plaza y del descenso ----
# Solo verdes de la misma familia (ver VegetacionPaletaInforme.md): Tree02/Tree03/Tree06 son verde
# #0C..#12; Tree04 es turquesa y Tree05_b rojo — fuera, son los que rompían la paleta.
ARBOLES = ["Tree02_a01.prefab","Tree03_a01.prefab","Tree06_a01.prefab","Tree02_b01.prefab",
           "Tree03_c01.prefab","Tree06_a02.prefab","Tree02_d01.prefab","Tree03_d01.prefab"]
_bosque = [
    # Rodal norte
    (5988,6032),(6002,6042),(6017,6037),(6032,6046),(5995,6055),(6020,6054),
    # Rodal sur
    (5988,5968),(6003,5963),(6018,5969),(6035,5960),(5995,5950),(6020,5948),
    # Ribera oriental
    (6045,6028),(6060,6038),(6075,6023),(6049,6008),(6073,6002),(6057,5976),
]
for i,(x,z) in enumerate(_bosque):
    p("06_Vegetacion", ARBOLES[i % len(ARBOLES)], "Arbol_%02d" % (i+1), x, z, (i*73) % 360,
      esc=round(0.9 + 0.05*((i*7) % 6), 2))

# hierba y flores sueltas SOLO donde la cámara pasa cerca (a 2 m del suelo se nota, a 20 no)
_matas = [(5994.6,6002.9),(5997.2,6001.6),(6002.4,6000.6),(6006.4,5999.8),(6009.6,6001.2),
          (6013.4,6003.6),(6016.2,6000.9),(5992.4,6000.4),(5999.6,6003.6),(6004.6,6002.8),
          (5988.9,6002.4),(6001.6,5997.8),(6008.8,6003.4),(5996.4,5997.6),(6012.6,5999.4)]
for i,(x,z) in enumerate(_matas):
    p("06_Vegetacion", ["Grass01_a01.prefab","Grass02_a01.prefab","Grass03_a01.prefab"][i%3],
      "Hierba_%02d" % (i+1), x, z, (i*111)%360, esc=round(0.85+0.1*(i%4),2))
for i,(x,z) in enumerate(_matas[::2]):
    p("06_Vegetacion", ["Flower04_a01.prefab","Flower01_a01.prefab","Flower02_a01.prefab"][i%3],
      "Flores_%02d" % (i+1), x+0.6, z-0.5, (i*57)%360, esc=0.9)

# ---- relieve: cresta abierta al norte y al este -----------------------------
# La montaña propia ocupa el oeste. Estas siete lomas lejanas prolongan el horizonte al norte, al
# este y al sur; se dejan huecos para que la villa no parezca encerrada en un anillo de obstáculos.
_lomas = [
    ("Hill02_c01.prefab", 5972, 6070, 4.6, -2.6),
    ("Hill02_a01.prefab", 6000, 6083, 4.4, -2.5),
    ("Hill02_c01.prefab", 6032, 6074, 4.3, -2.4),
    ("Hill02_a01.prefab", 6062, 6052, 4.5, -2.5),
    ("Hill02_c01.prefab", 6082, 6012, 4.2, -2.3),
    ("Hill02_a01.prefab", 6068, 5960, 4.4, -2.5),
    ("Hill02_c01.prefab", 6028, 5930, 4.3, -2.4),
]
for i,(pref,x,z,esc,dy) in enumerate(_lomas):
    p("07_Relieve", pref, "Loma_%02d" % (i+1), x, z, (i*61)%360, y=S+dy, esc=esc)

# ---- el incendio de la noche (apagado al empezar) ---------------------------
# Son la única luz de las fases 5 y 6, así que van donde la cámara ya está mirando: la fachada
# norte al fondo de la calle y la casa del sur que se ve tras el duelo.
FUEGOS = [
    ("Fuego_Casa_Norte_02", 6002.6, 6005.2, 1.6), ("Fuego_Casa_Norte_03", 6010.2, 6005.0, 1.3),
    ("Fuego_Panaderia",     5995.2, 6005.0, 1.1), ("Fuego_Casa_Sur_02",   5999.8, 5994.0, 1.4),
    ("Fuego_Casa_Sur_03",   6007.6, 5994.4, 1.2), ("Fuego_Viga",          6006.2, 6000.8, 1.0),
    ("Fuego_Plaza",         5993.4, 5999.2, 0.9), ("Fuego_Casa_Norte_04", 5978.5, 6006.4, 1.5),
]
MIRA_VIGA = (6004.9, S + 1.5, 6000.4)
HUMOS = [("Humo_Norte", 6002.6, 6006.0, 5.0), ("Humo_Sur", 5999.8, 5993.2, 5.0),
         ("Humo_Oeste", 5986.0, 6005.0, 6.5), ("Humo_Este", 6010.2, 6005.8, 4.6)]


# ── la lámina de agua ────────────────────────────────────────────────────────
# (x, z, escala en X, escala en Z, cota). Plano de Unity con Mat_Valle_Agua, ligeramente por
# encima del prado para que no desaparezca dentro de él.
AGUA = (6023.0, 6000.0, 0.95, 7.0, S + 0.04)
