# -*- coding: utf-8 -*-
"""
montaje.py -- el pase de montaje del 20 sep 2026 sobre el prologo.

Lee ConstruirPrologoUltimaNoche.cs con p3.py, reescribe las fases y lo vuelve a
escribir. Lo que NO se toca (los diez gestos de la plaza, los VFX del duelo, los
prefabs) se conserva tal cual venia: los beats son los mismos objetos.

Lo que hace este pase, en el orden en que lo pidio Raul:

 1. PLANOS. Menos cortes al principio y ningun plano que persiga al Archimago
    mientras cruza la plaza: la camara se queda quieta y el cruza el cuadro.
 2. NPCs. La gente cruza el puente de verdad, en fila y mientras dura el duelo;
    y cuando estalla la primera descarga hay caos, no diez personas quietas.
 3. El RIO. La conversacion de Liora y el Archimago sale de la plaza y se va a la
    orilla, con el agua y el puente detras.
 4. El MAGO OSCURO deja de bajar y de pelear de perfil.
 5. Las frases finales, reescritas.
 6. El FINAL nuevo: el se eleva, carga el hechizo, lo lanza; el Archimago corre,
    salta y suelta Proteccion Absoluta en el aire; negro; explosion.
"""

import io
import os
import re
import sys

import p3
from p3 import Beat, Fase
from dsl import *
from dsl import b as b_

ARCHIMAGO = "NPC_Archimago"
LIORA = "NPC_Liora"
OSCURO = "NPC_MagoOscuro"
ALDEANOS = [f"NPC_Aldeano_{i:02d}" for i in range(1, 11)]

# -- utilidades sobre la lista de beats --------------------------------------

def indice(fase, trozo_de_nota, desde=0):
    """El indice del primer beat cuya nota contenga ese trozo. Falla si no hay."""
    for i in range(desde, len(fase.beats)):
        n = fase.beats[i].get("note", "")
        if trozo_de_nota in n:
            return i
    raise KeyError(f"[{fase.nombre}] no hay ningun beat con nota que contenga {trozo_de_nota!r}")

def quitar(fase, trozo_de_nota):
    i = indice(fase, trozo_de_nota)
    return fase.beats.pop(i)

def sustituir(fase, trozo_de_nota, *nuevos):
    i = indice(fase, trozo_de_nota)
    fase.beats[i:i + 1] = list(nuevos)

def insertar_tras(fase, trozo_de_nota, *nuevos):
    i = indice(fase, trozo_de_nota)
    fase.beats[i + 1:i + 1] = list(nuevos)

def insertar_antes(fase, trozo_de_nota, *nuevos):
    i = indice(fase, trozo_de_nota)
    fase.beats[i:i] = list(nuevos)

def por_nombre(fases, trozo):
    for candidata in fases:
        if trozo in candidata.nombre:
            return candidata
    raise KeyError("no hay fase que contenga %r: %s" % (trozo, [x.nombre for x in fases]))


# ── 1. La manana: que respire ─────────────────────────────────────────────────
#
# Catorce cortes en sesenta segundos, tres de ellos planos VIVOS que persiguen al
# Archimago mientras anda. Un plano vivo se recalcula entero en cada fotograma, y
# como el personaje va girando al andar, la camara gira con el: eso es el mareo.
#
# Un pueblo se cuenta mejor con la camara quieta y la gente cruzando el cuadro.

def fase_manana(fase):
    # Los tres planos que le persiguen pasan a ser planos generales FIJOS, encuadrados
    # sobre el vecino al que va a ver. Asi el Archimago entra andando en un cuadro que
    # ya estaba ahi, que es como se rueda a alguien llegando a un sitio.
    sustituir(fase, "Le seguimos mientras cruza la plaza",
              plano("La plaza, quieta, con la carreta volcada y el vecino al lado. El Archimago "
                    "entra andando en el cuadro: la camara no le persigue, le espera.",
                    "Wide", "NPC_Aldeano_06", ARCHIMAGO, altura=1.0, distancia=1.5))

    sustituir(fase, "Le seguimos otra vez",
              plano("El corro que celebra, y el llegando por la izquierda.",
                    "Wide", "NPC_Aldeano_03", ARCHIMAGO, altura=0.6, distancia=1.35))

    sustituir(fase, "Y otra vez",
              plano("El campanario con el globo enganchado, y el vecino debajo. Camara quieta.",
                    "Wide", "NPC_Aldeano_05", ARCHIMAGO, altura=0.9, distancia=1.4))

    # Dos cortes que no cuentan nada nuevo: el plano siguiente ya dice lo mismo.
    quitar(fase, "Los dos, con la carreta ya de pie entre ellos")
    quitar(fase, "El corro.")

    # Y aire. El valle tiene que estar en pantalla antes de que pase nada.
    b = fase.beats[indice(fase, "Dejar respirar la plaza antes de que nadie hable")]
    b.set("seconds", f(4.0))

    insertar_tras(fase, "El, mirando su valle",
                  esperar(1.2, "Un segundo mirandolo antes de hablar."))

    insertar_tras(fase, "Y se queda ahi flotando un segundo",
                  esperar(0.8, "Y otro mas. La carreta flotando es el truco entero de la escena."))

    # ── La carreta ───────────────────────────────────────────────────────────
    #
    # "no se ve levitar la carreta". El plano existia y era el correcto, pero iba
    # con 'live' puesto: se recalcula cada fotograma y SIGUE a la carreta mientras
    # sube. Una camara que acompana a algo que levita cancela la levitacion --
    # queda del mismo tamano y en el mismo sitio del cuadro, y lo unico que se
    # mueve es el fondo.
    #
    # Camara quieta, mas abierta y con aire por arriba: la carreta sube DENTRO del
    # cuadro, con el suelo y el vecino abajo de referencia.
    b = fase.beats[indice(fase, "EL PLANO QUE FALTABA")]
    b.set("note", s("EL PLANO QUE FALTABA - la carreta, y solo la carreta. Camara QUIETA (nada de "
                    "'live'): si la camara la acompana mientras sube, no se ve que suba."))
    b.set("live", b_(False))
    b.set_framing("heightBias", f(1.0))
    b.set_framing("distanceScale", f(1.8))

    # ── El globo ─────────────────────────────────────────────────────────────
    #
    # "el globo simplemente desaparece". Subia 16 m en 5 s sin esperar... y dos
    # segundos despues la fase 1b lo apagaba, asi que se esfumaba a media subida.
    b = fase.beats[indice(fase, "El globo se suelta y SUBE")]
    b.set("note", s("El globo se suelta y SUBE. Sin esperar - sigue subiendo de fondo mientras la "
                    "escena continua, y ahora le da tiempo a perderse de vista antes de que nadie "
                    "lo apague."))
    b.set("deltaPosicion", v3(2.0, 22.0, 1.0))
    b.set("segundos", f(8.0))

    b = fase.beats[indice(fase, "Los dos mirandolo subir")]
    b.set("note", s("Contrapicado fuerte, camara quieta, con el vecino abajo del cuadro. El globo "
                    "se va por arriba y el plano se queda -- que es lo que hace que se vea SUBIR."))
    b.set_framing("heightBias", f(-2.4))
    b.set_framing("distanceScale", f(1.6))

    insertar_tras(fase, "El globo se suelta y SUBE",
                  esperar(1.0, "Que se le vea empezar a subir."))

    insertar_tras(fase, "Contrapicado fuerte, camara quieta",
                  esperar(2.6, "Y que se le vea IRSE. Este silencio mirando al cielo es el ultimo "
                               "momento tonto del prologo."))

    return fase


def fase_globo_ok(fase):
    """El globo no se apaga de golpe: se apaga cuando ya no se ve."""
    activo = fase.beats[indice(fase, "El globo se va")]
    fase.beats.remove(activo)
    activo.set("note", s("Y ahora si se apaga, con el ya fuera de plano. Apagarlo antes es lo que "
                         "hacia que 'desapareciera'."))
    fase.beats.append(esperar(1.2))
    fase.beats.append(activo)
    return fase


# ── 2. El rio ─────────────────────────────────────────────────────────────────
#
# La escena que Raul echaba de menos: "se supone que esa se hace al lado del rio".
#
# Y ademas resuelve un problema de encuadre que en la plaza no tiene solucion. En
# la plaza hay diez vecinos, cuatro casas y un pozo alrededor de los dos que
# hablan; el solver tiene que rodar la camara hasta donde cabe, y donde cabe es
# detras del pelo de alguien. En la orilla no hay nada detras salvo el agua.
#
# El eje se fija al OESTE a proposito: asi el rio y el puente quedan al fondo de
# los dos planos de la conversacion. Es el unico fondo del valle que no es un muro
# -- y de paso planta el puente en la cabeza del jugador veinte segundos antes de
# que la evacuacion lo necesite.

def fase_rio(fase):
    fase.nombre = '"2 - El rio"'
    fase.beats = [
        plano("Los dos bajando de la plaza al rio, de espaldas, pequenos. Esto es un respiro, y "
              "dura lo que dura a proposito.",
              "Wide", ARCHIMAGO, LIORA, altura=1.6, distancia=2.2, suave=True, duracion=3.0),

        a_la_vez("Bajan juntos.", [
            andar(ARCHIMAGO, ["M_Rio_Camino", "M_Rio_Mago"], "", velocidad=1.3),
            andar(LIORA, ["M_Rio_Camino", "M_Rio_Liora"], "", velocidad=1.3),
        ], esperarATodos=True),

        # La camara AL SUR. Ellos pasean hacia el sur, asi que vienen de frente: es el plano
        # de dos que hablan andando, y es el unico de toda la secuencia en el que no pasa nada.
        eje("La camara al sur, delante de ellos: les vemos venir mientras hablan.", 180.0),

        mirar(ARCHIMAGO, hacia=LIORA, note="Se miran un momento antes de echar a andar.",
              mutuo=True, giro=0.5),
        esperar(1.4, "Y el agua, un segundo, antes de que nadie diga nada."),

        # El paseo se LANZA sin esperar: las frases caen encima mientras andan.
        a_la_vez("Y siguen orilla abajo mientras hablan. El paseo corre de fondo -- el WaitBeat "
                 "corto de la primera posicion hace que el Parallel termine y la escena siga.",
                 [esperar(0.1, "", sinEscalar=False),
                  andar(ARCHIMAGO, ["M_Rio_Fin_Mago"], "", velocidad=0.9),
                  andar(LIORA, ["M_Rio_Fin_Liora"], "", velocidad=0.9)],
                 esperarATodos=False),

        plano("Los dos andando hacia la camara, con el rio a un lado.",
              "TwoShot", ARCHIMAGO, LIORA, distancia=1.2, vivo=True),
        emocion(LIORA, 1),
        decir(LIORA, "PROLOGO_PLAZA_LIORA_1", "Liora", 3.2),

        plano("El, andando, de frente.", "Reaction", ARCHIMAGO, LIORA, distancia=1.1, vivo=True),
        emocion(ARCHIMAGO, 1),
        decir(ARCHIMAGO, "PROLOGO_PLAZA_ARCHIMAGO", "Archimago", 3.8),

        # Para cuando cae la ultima frase ya han llegado y estan parados: la broma se remata
        # quieto, que es como se rematan las bromas.
        plano("Ella.", "Reaction", LIORA, ARCHIMAGO, distancia=1.1, vivo=True),
        decir(LIORA, "PROLOGO_PLAZA_LIORA_2", "Liora", 3.4),

        gesto(LIORA, "Laugh01", hold=0.9),
        gesto(ARCHIMAGO, "Laugh01", hold=0.9),

        # Y el plano de cierre cambia de lado a proposito, al oeste, para quedarse con la
        # postal: los dos, el agua y el puente detras. Es un salto de eje y se nota, que es
        # justo lo que hace que se lea como "fin de la escena".
        eje("Y para el ultimo plano, al oeste: el agua y el puente detras de ellos.", 270.0),
        plano("Los dos riendose con el rio detras. Es el ultimo momento tranquilo del prologo y "
              "nadie lo sabe todavia, asi que aguanta.",
              "TwoShot", ARCHIMAGO, LIORA, altura=0.4, distancia=1.5),
        esperar(2.6),
    ]
    return fase


# ── 3. El cielo: y la vuelta corriendo ────────────────────────────────────────
#
# Al mover la conversacion al rio, los dos se quedan a veinte metros de la plaza
# justo cuando empieza todo. Antes ese agujero no existia porque no se movian de
# sitio; ahora hay que cerrarlo, y cerrarlo es gratis: vuelven corriendo, que es
# ademas lo unico que haria cualquiera.
#
# Y el plano general del cielo deja de encuadrarles a ellos -- que ya no estan en
# la plaza -- y pasa a encuadrar al pueblo, que es de quien habla ese plano.

def fase_cielo(fase):
    b = fase.beats[indice(fase, "La plaza entera, con la gente y la montana al fondo")]
    b.set_framing("subjectId", s("NPC_Aldeano_01"))
    b.set_framing("secondaryId", s("NPC_Aldeano_05"))

    b = fase.beats[indice(fase, "La cara del Archimago mirando arriba")]
    b.set("note", s("La cara del Archimago, todavia en la orilla, mirando arriba. Detras de el, "
                    "el agua: es la ultima vez que ese fondo esta tranquilo."))

    fase.beats += [
        plano("Los dos echan a correr de vuelta al pueblo.",
              "Tracking", ARCHIMAGO, LIORA, altura=0.6, distancia=1.8, vivo=True, encara=False),
        a_la_vez("Vuelven corriendo. No esperan a saber que es.", [
            andar(ARCHIMAGO, ["M_Rio_Camino", "M_Apertura"], "", velocidad=3.4),
            andar(LIORA, ["M_Rio_Camino", "M_Plaza_Liora"], "", velocidad=3.2),
        ], esperarATodos=True),
        mirar(ARCHIMAGO, marca="M_Cresta", note="Y se vuelve otra vez hacia la montana.", giro=0.4),
    ]
    return fase


# ── 4. La llegada: que baje DE FRENTE ────────────────────────────────────────
#
# "el mago oscuro avanza de lado".
#
# La causa esta en ComputeThreeQuarter. Cuando un plano no declara secundario, la
# direccion "frontal" que usa para colocar la camara es el FORWARD DEL SUJETO. En
# un plano vivo de alguien que anda -- y WalkPathBeat le gira hacia donde avanza en
# cada fotograma -- eso significa que la camara va pegada a su nuca girando con el.
# Cualquier correccion de rumbo la arrastra, y lo que se ve es un tipo cruzando el
# cuadro de lado.
#
# Con el Archimago de secundario, la direccion frontal deja de ser la suya y pasa a
# ser la linea hacia el pueblo, que no se mueve. La camara se queda donde tiene que
# estar: delante de el, esperandole.

def fase_llegada(fase):
    for nota in ("la silueta recortada contra las nubes",
                 "en la ladera, en contrapicado",
                 "un plano que le SIGUE mientras baja"):
        b = fase.beats[indice(fase, nota)]
        b.set_framing("secondaryId", s(ARCHIMAGO))

    # Y la bajada, partida en dos: trece segundos de un solo plano vivo son muchos
    # para una sola cosa, por buena que sea.
    i = indice(fase, "Los ultimos 20 metros, andando de verdad")
    caminata = fase.beats[i]
    caminata.set("markNames", lst("M_Entrada_Villa"))
    caminata.set("speed", f(1.5))

    insertar_antes(fase, "Los ultimos 20 metros, andando de verdad",
                   esperar(0.8, "Un latido antes de que eche a andar."))

    return fase


# ── 5. La orden: caos primero, y la gente saliendo de verdad ──────────────────
#
# Dos cosas que no funcionaban.
#
# "cuando ocurre la explosion la gente se queda sin mas". Literalmente: estallaba
# una casa y los diez vecinos seguian de pie mirando al frente, porque no habia
# ningun beat que les dijera otra cosa. Ahora la primera descarga les dispersa
# corriendo antes de que nadie diga una palabra.
#
# Y "al final la gente debe estar saliendo por el puente... andando de verdad".
# Antes los diez salian a la vez en un Parallel que ESPERABA a que llegaran todos,
# con la camara todavia en el plano anterior: se teletransportaban en negro. Ahora
# es un Parallel que no espera (el truco es el WaitBeat corto de la primera
# posicion: el padre termina con el y los demas siguen corriendo por su cuenta),
# van en DOS OLEADAS y cruzan el puente por encima, con marcas de entrada y salida.
#
# La segunda oleada arranca en el duelo y va despacio a proposito. Son los que no
# pueden correr, y son los que hacen que "todavia estan cruzando" sea verdad
# cuando el Archimago lo dice, treinta segundos despues.

VELOCIDADES_OLEADA_1 = [1.9, 2.1, 1.7, 2.0, 1.8, 2.2]
VELOCIDADES_OLEADA_2 = [0.85, 1.0, 0.9, 1.1]

def _cruce(aldeano, indice_marca, velocidad):
    # Y un tramo mas, hasta M_Lejos: los destinos del puente estan a la vista desde la plaza, y un
    # vecino que llega y se queda de pie mirando al infinito se lee como un error. Con esto siguen
    # andando hasta que ya no importa.
    return andar(aldeano,
                 [f"M_Huida_{indice_marca:02d}", "M_Puente_Ent", "M_Puente_Sal",
                  f"M_Puente_{indice_marca:02d}", f"M_Lejos_{indice_marca:02d}"],
                 velocidad=velocidad)

def oleada(nombres, velocidades, indices, note):
    hijos = [esperar(0.1, "Con esto el Parallel termina enseguida y los demas siguen andando "
                          "de fondo el resto de la secuencia.", sinEscalar=False)]
    for nombre, v, idx in zip(nombres, velocidades, indices):
        hijos.append(_cruce(nombre, idx, v))
    return a_la_vez(note, hijos, esperarATodos=False)


def fase_orden(fase):
    fase.nombre = '"5 - La orden de evacuar"'

    hora_beat = fase.beats[indice(fase, "Cae la tarde y el valle se pone rojo")]
    incendio = fase.beats[indice(fase, "Una descarga golpea una casa")]
    escudo_gesto = fase.beats[indice(fase, "Levanta el escudo")]
    escudo_prop = fase.beats[indice(fase, "Vineta 8 - el escudo")]

    # Los dos beats de efecto del escudo (el aura y el sonido) no tienen nota, asi
    # que se localizan por tipo y por a quien van colgados.
    escudo_extras = [b for b in fase.beats if b.tipo in ("VfxBeat", "SfxBeat")
                     and b.get("atActorId") == s(ARCHIMAGO)]

    caos_gestos = []
    for i, a in enumerate(ALDEANOS):
        caos_gestos.append(emocion(a, 5 if i % 2 == 0 else 9))
        # Beg01 y no Fear01: Fear01 se retiro de todo el juego el 17 sep porque en
        # estas proporciones parte el cuello al retargetear (INC-223).
        caos_gestos.append(gesto(a, "Beg01" if i % 3 else "HeadShake01", hold=0.0))

    fase.beats = [
        hora_beat,
        incendio,

        plano("La plaza entera en el momento en que estalla. Hace falta verla LLENA para que "
              "vaciarse signifique algo.",
              "Wide", ARCHIMAGO, LIORA, altura=2.2, distancia=2.4),

        sfx("Prologue_Explosion"),
        fogonazo(duracion=0.18),
        temblor(intensidad=0.6, duracion=0.9),

        a_la_vez("El susto, los diez a la vez y sin esperar a nadie.", caos_gestos,
                 esperarATodos=False),

        # Y salen corriendo, que es lo que hace un pueblo cuando le cae una casa encima.
        a_la_vez("Se dispersan corriendo. No es todavia la evacuacion: es el panico, que es "
                 "desordenado a proposito -- cada uno hacia un sitio distinto.",
                 [esperar(0.1, "", sinEscalar=False)] +
                 [andar(a, [f"M_Huida_{i + 1:02d}"], velocidad=3.4)
                  for i, a in enumerate(ALDEANOS)],
                 esperarATodos=False),

        esperar(1.2, "Un segundo de gente corriendo antes de que nadie hable."),

        plano("Ellos dos, en medio de la plaza que se vacia.",
              "TwoShot", LIORA, ARCHIMAGO, distancia=1.2),
        emocion(LIORA, 5),
        decir(LIORA, "PROLOGO_DESPEDIDA_LIORA_1", "Liora", 2.2),

        plano("", "Reaction", ARCHIMAGO, LIORA, distancia=1.05),
        emocion(ARCHIMAGO, 10),
        decir(ARCHIMAGO, "PROLOGO_DESPEDIDA_ARCHIMAGO_1", "Archimago", 4.2),

        # La orden al pueblo, gritada, con el puente al fondo.
        plano("El, gritando hacia el este. Al fondo, el puente: por ahi es por donde se sale.",
              "Medium", ARCHIMAGO, "NPC_Aldeano_01", altura=-0.4, distancia=1.5, encara=False),
        gesto(ARCHIMAGO, "Challenging_NoWeapon", "Senala al puente.", hold=0.4),
        decir(ARCHIMAGO, "PROLOGO_HORA_ARCHIMAGO", "Archimago", 2.2),

        escudo_gesto,
        escudo_prop,
    ] + escudo_extras + [

        oleada(ALDEANOS[:6], VELOCIDADES_OLEADA_1, [1, 2, 3, 4, 5, 6],
               "Primera oleada: los seis que pueden andar solos. Se van AHORA y siguen andando "
               "por su cuenta mientras la escena continua."),

        plano("La plaza vaciandose hacia el puente, con el escudo encendido encima.",
              "Wide", ARCHIMAGO, "PROP_Escudo", altura=1.6, distancia=2.0, vivo=True),
        esperar(2.0),

        plano("", "Reaction", LIORA, ARCHIMAGO, distancia=1.05),
        decir(LIORA, "PROLOGO_DESPEDIDA_LIORA_2", "Liora", 2.0),

        plano("", "OverTheShoulder", ARCHIMAGO, LIORA, distancia=1.0),
        decir(ARCHIMAGO, "PROLOGO_DESPEDIDA_ARCHIMAGO", "Archimago", 3.0),

        plano("Ella. Es la ultima vez que se ven.",
              "CloseUp", LIORA, ARCHIMAGO, distancia=1.0),
        emocion(LIORA, 2),
        decir(LIORA, "PROLOGO_DESPEDIDA_LIORA", "Liora", 2.4),
        esperar(0.9),
        decir(LIORA, "PROLOGO_DESPEDIDA_ELEGIR", "Liora", 3.4,
              note="La broma del rio, devuelta. Es lo que convierte la despedida en una promesa "
                   "en vez de en un discurso."),
        esperar(1.4, "Que se quede en el aire."),

        # Y ella se pone al frente. Lo que pidio Raul: que LLAME a los que quedan en vez de
        # marcharse sola y dejarlos de adorno.
        plano("Ella se vuelve hacia los que quedan y les llama.",
              "Medium", LIORA, "NPC_Aldeano_07", altura=-0.4, distancia=1.5),
        gesto(LIORA, "HandWave02", "Les hace senas.", hold=0.5),
        decir(LIORA, "PROLOGO_EVACUACION_LIORA", "Liora", 2.4),

        a_la_vez("Y salen con ella: los cuatro que quedaban y Liora delante. Despacio -- son los "
                 "que no pueden correr, y son los que van a seguir cruzando el puente cuando el "
                 "Archimago diga que todavia estan cruzando, un minuto despues.",
                 [esperar(0.1, "", sinEscalar=False),
                  andar(LIORA, ["M_Huida_09", "M_Puente_Ent", "M_Puente_Sal",
                                "M_Puente_09", "M_Lejos_09"], velocidad=1.5)]
                 + [_cruce(a, i, v) for a, i, v in
                    zip(ALDEANOS[6:], [7, 8, 9, 10], VELOCIDADES_OLEADA_2)],
                 esperarATodos=False),

        plano("La plaza vaciandose hacia el puente, y el quieto en medio.",
              "Wide", ARCHIMAGO, LIORA, altura=1.8, distancia=2.2, vivo=True),
        esperar(2.4, "Que se les vea irse."),

        plano("El, solo.", "Medium", ARCHIMAGO, "", altura=-0.3, distancia=1.4),
        esperar(1.6),
    ]
    return fase


# ── 7. El duelo, movido al este ───────────────────────────────────────────────
#
# El duelo estaba en x 5989/5995, en la entrada de la villa, con el puente treinta
# metros DETRAS DE LA CAMARA. Por eso "todavia estan cruzando" no significaba nada:
# no habia nadie cruzando en ningun plano.
#
# Ahora ocurre en el camino que lleva al puente (x 6000/6006, z 5997,5), que es el
# unico pasillo limpio que queda entre la plaza y el rio. Con la camara detras del
# hombro del Mago Oscuro -- que es por donde ha venido, al oeste -- el plano mira
# al este y recoge, en este orden: el hombro de el, el Archimago, el puente y la
# gente cruzandolo. Los tres planos de la escena en uno.
#
# Y de paso se arregla "pelea de perfil": un two-shot es, por definicion,
# perpendicular al eje que une a los dos, o sea los dos de canto. Sirve para
# ESTABLECER donde esta cada uno, una vez. A partir de ahi manda el plano sobre el
# hombro, que es el que mira a alguien a la cara.

MARCAS_VIEJAS = {
    '"M_Duelo2_Mago"':   '"M_Duelo3_Mago"',
    '"M_Duelo2_Oscuro"': '"M_Duelo3_Oscuro"',
    '"M_Duelo_Choque"':  '"M_Duelo3_Choque"',
    '"M_Duelo_Caida"':   '"M_Duelo3_Caida"',
}

def renombrar_marcas(beats):
    for b in beats:
        for campo in ("markName",):
            v = b.get(campo)
            if v in MARCAS_VIEJAS:
                b.set(campo, MARCAS_VIEJAS[v])
        if b.hijos:
            renombrar_marcas(b.hijos)

def fase_duelo(fase):
    renombrar_marcas(fase.beats)

    i = indice(fase, "La camara al norte")
    fase.beats[i].set("note", s(
        "El eje al norte. La linea que une a los dos va de oeste a este, asi que la perpendicular "
        "es esta -- y los planos sobre el hombro miran al este, que es donde estan el puente y "
        "la gente cruzandolo."))

    # El Mago Oscuro no aparece de golpe en su marca: recorre andando los ultimos
    # metros hasta el sitio del duelo, con la camara esperandole de frente.
    fase.beats[i + 1:i + 1] = [
        plano("Le vemos entrar en la plaza vacia. De frente: el Archimago de secundario fija la "
              "direccion de la camara, asi que no gira con el.",
              "Tracking", OSCURO, ARCHIMAGO, altura=-0.9, distancia=1.4, vivo=True),
        andar(OSCURO, ["M_Duelo3_Oscuro"],
              "Los ultimos metros, andando. Nadie le ha visto cubrir la distancia hasta ahora y "
              "eso le hacia parecer que aparecia por corte; asi llega.",
              velocidad=1.4),

    ]

    # El establecimiento se queda en two-shot (una vez, para decir quien esta donde) y
    # los dos generales posteriores pasan a plano general de verdad, mas abierto.
    for nota, tipo, dist in (("Y se rie -- con el Archimago en cuadro", "Medium", 1.15),
                             ("Plano general otra vez", "Wide", 1.7)):
        b = fase.beats[indice(fase, nota)]
        b.set_framing("type", f"ShotType.{tipo}")
        b.set_framing("distanceScale", f(dist))

    # ── El ritmo ─────────────────────────────────────────────────────────────
    #
    # "va todo muy deprisa, no da tiempo a disfrutar de la secuencia". El duelo
    # tenia 3 segundos de espera repartidos en cuatro asaltos: los golpes se
    # encabalgaban y no se entendia quien habia ganado cada uno antes de que
    # empezara el siguiente. Las esperas de final de asalto se doblan.
    for nota, segundos in (("Fin del asalto 1", 1.3),
                           ("Fin del asalto 2", 1.3),
                           ("Un segundo de nada despues del choque", 1.8)):
        fase.beats[indice(fase, nota)].set("seconds", f(segundos))

    # Y un respiro mas donde no habia ninguno: justo despues de que le rompan la
    # guardia. Es el momento en que la escena cambia de signo.
    insertar_tras(fase, "Sale despedido dos metros hacia atras",
                  esperar(1.2, "El, en el suelo, un segundo. Sin esto el golpe no se siente."))

    # "No pero puedo mandarte a un sitio donde nunca vuelvas a hacer dano a nadie: los dos
    # pisandose uno encima de otro". Literal: el Mago Oscuro avanza hasta M_Duelo3_Mago para
    # rematarle, y cuatro beats despues el Archimago se pone en pie EN ESA MISMA MARCA. Dos
    # cuerpos en el mismo metro cuadrado. Antes de que se levante, el otro retrocede a la suya.
    insertar_antes(fase, "Se pone en pie y da el paso al frente",
                   ir_a(OSCURO, marca="M_Duelo3_Oscuro",
                        note="El retrocede a su sitio mientras el otro se levanta. Sin esto los dos "
                             "acaban de pie en la misma marca, uno dentro del otro.",
                        parar=0.3, velocidad=1.8, tope=5.0, encararse=False, asentar=0.0))

    # Y el plano de la frase. Este es el que pidio Raul: el lo dice y se VE que es verdad.
    b = fase.beats[indice(fase, "El, en el suelo.")]
    b.set("note", s("El, en el suelo, y detras de el -- al fondo del mismo plano, sin cortar a "
                    "ningun sitio -- el puente con los ultimos vecinos cruzandolo."))
    b.set_framing("type", "ShotType.OverTheShoulder")
    b.set_framing("secondaryId", s(OSCURO))
    b.set_framing("distanceScale", f(1.4))

    return fase


# ── 8. El final ───────────────────────────────────────────────────────────────
#
# Lo que pidio Raul, tal cual: "el mago oscuro saltando y volando arriba, un
# planazo hacia el con las manos levantadas y sobre sus manos el tipico hechizo que
# se va haciendo grande, lo lanza y enfocamos al archimago... corre, salta y en el
# aire dice: proteccion absoluta... nos vamos a negro, EXPLOSION y fin".
#
# Los dos vuelos son WalkPathBeat con 'stickToGround' apagado. Y las dos marcas de
# aire estan MAS LEJOS EN HORIZONTAL QUE EN VERTICAL a proposito: WalkPathBeat da un
# tramo por terminado cuando llega en horizontal, asi que un vuelo recto hacia
# arriba acabaria en el primer fotograma sin haber subido nada.
#
# Los tres estados de salto los da de alta PrologoAnimatorSaltoWiring; los clips ya
# estaban en el proyecto.

VFX_BOLA        = "5ee28f65ca127db42a9a46da980189d4"
VFX_CARGA       = "a2a060732547fe64581bb0cb3c2bdf1d"
VFX_APARICION   = "e824247f4f364400b0475f062217a915"
VFX_ESCUDO      = "b99922c2f59b1a542bdd06ae8bee47ae"
VFX_COLUMNA     = "6555300494081614fa6b6a823cca9f64"
VFX_ESTALLIDO   = "41494896fc96c9748b81d3356632794e"
VFX_IMPACTO     = "df9374346b76e444dbbb2b019de4da25"
VFX_CHOQUE      = "867c572a5be680d42a042d2349f10143"
VFX_GRIETA      = "d8087ea6f3f1a934e8f05e0bcded1494"


def volar(actor, marcas, pose, velocidad, segundos, note=""):
    """
    Un tramo de vuelo: desplazarse MANTENIENDO una pose.

    Antes esto repartia la pose en repeticiones (`segundos / 0.6`) porque no habia
    otra forma: un gesto es un disparo que acaba en idle. Salian las dos caras del
    mismo fallo -- si la repeticion llegaba antes de acabar el clip la animacion se
    reiniciaba desde el fotograma 0 (el tiron, "parece que se ha quedado pillado"),
    y si llegaba despues se colaba un idle. Y la ultima repeticion terminaba su
    corrutina en pleno vuelo y mandaba a idle a alguien que estaba a veinte metros
    de altura.

    Ahora la pose se MANTIENE (PoseBeat -> NPCSimpleAnimator.HoldPose): se cruza una
    vez y se queda, y solo se vuelve a cruzar si el Animator se ha salido de ella.
    El clip no se reinicia nunca en el caso normal.

    `animarAndando=False` sigue haciendo falta: sin eso, WalkPathBeat mete al
    personaje en el blend tree de locomocion y cruza el cielo a pasitos.
    """
    return a_la_vez(note, [
        mantener(actor, pose, "La pose se sostiene todo el tramo."),
        andar(actor, marcas, velocidad=velocidad, alSuelo=False, mirarAlAvance=False,
              animarAndando=False),
    ], esperarATodos=True)


# ── 8. El final ───────────────────────────────────────────────────────────────
#
# Segunda pasada (20 sep, tarde). Raul, sobre la octava grabacion:
#
#   "el final de la batalla debe ser epico y ahora mismo se ven el archimago y el
#   mago oscuro en la misma posicion, quiero el final con mas disparos que queda
#   chulisimo pero con saltos, con mas intensidad, mas variedad de ataques, y que
#   se vea claramente sin prisas -- va todo muy deprisa, no da tiempo a disfrutar
#   de la secuencia con lo bonita que es"
#
# Lo de "en la misma posicion" no era el montaje: era que las marcas del duelo
# nuevo no existian todavia en la escena, asi que colocarles no hacia nada y los
# dos se quedaban donde estuvieran. Eso lo arregla ejecutar el wiring.
#
# Lo demas si es montaje, y es esto: cuatro asaltos EN EL AIRE con cuatro formas
# distintas de atacar -- disparo desde arriba, esquiva saltando, contraataque
# desde el suelo, y picado -- antes del hechizo final. Ahora que el controller
# tiene `fly_idle`, `fly_dive`, `Landing` y los saltos con giro, se puede.
#
# Y el ritmo. Cada asalto acaba en una espera de mas de un segundo. Parece mucho
# escrito, y en pantalla es lo que hace que se entienda quien ha ganado ese
# asalto antes de que empiece el siguiente.

def fase_final(fase, prompt_beat):
    fase.nombre = '"8 - El ultimo hechizo"'
    fase.beats = [
        # ── Asalto 1: despega ─────────────────────────────────────────────────
        plano("El, desde abajo, antes de despegar.",
              "Medium", OSCURO, ARCHIMAGO, altura=-1.4, distancia=1.2),
        emocion(OSCURO, 3),
        gesto(OSCURO, "Challenging_NoWeapon", "Se planta.", hold=0.9),
        esperar(0.8),

        gesto(OSCURO, "JumpStart_InPlace_NoWeapon", "Flexiona y salta.", hold=0.3),
        sfx("Prologue_SpellRelease", actor=OSCURO),
        vfx(VFX_APARICION, "El aire revienta a sus pies al despegar.",
            actor=OSCURO, offset=(0.0, 0.2, 0.0), vida=1.6),

        plano("Y la camara sube con el, desde muy abajo.",
              "Tracking", OSCURO, ARCHIMAGO, altura=-2.8, distancia=1.6, vivo=True, encara=False),
        volar(OSCURO, ["M_Aire_Oscuro"], "fly_idle", 3.6, 2.4,
              "Sube echandose atras: siete metros al oeste por seis y medio de alto."),
        mirar(OSCURO, hacia=ARCHIMAGO, note="Arriba, se vuelve a mirarle.", giro=0.4),

        plano("EL PLANAZO. Desde el suelo, hacia arriba, con el recortado contra el cielo.",
              "Wide", OSCURO, "", altura=-5.0, distancia=1.6, fov=58.0, encara=False),
        mantener(OSCURO, "fly_idle", "Flotando. Una pose sostenida, no tres disparos del mismo "
                 "clip: eso es lo que se leia como que se habia quedado pillado."),
        esperar(1.4, "Que se le vea ahi arriba antes de que haga nada."),

        # ── Asalto 2: dispara desde arriba, el Archimago esquiva saltando ─────
        gesto(OSCURO, "MagicRight", "Dispara desde arriba.", hold=0.4),

        # Va a la MARCA y no al Archimago porque este tiene que FALLAR: el se quita de en
        # medio y la bola revienta donde estaba. Sale sin esperar, para que el plano pueda
        # cortar a su cara mientras el hechizo todavia esta bajando.
        a_la_vez("El disparo sale de su mano y baja.",
                 [hechizo(OSCURO, marca="M_Duelo3_Mago", proyectil=VFX_BOLA_OSC,
                          sfxLanza="Prologue_SpellInstantiate", sfxImpacto="Impact1",
                          velocidad=13.0, carga=0.3, sacudida=0.55,
                          note="Nace en su mano, viaja, y revienta en el suelo."),
                  esperar(0.35, "", sinEscalar=False)],
                 esperarATodos=False),

        plano("El Archimago lo ve venir.",
              "Reaction", ARCHIMAGO, OSCURO, altura=-0.8, distancia=1.15),
        emocion(ARCHIMAGO, 4),
        plano("La esquiva, siguiendole.",
              "Tracking", ARCHIMAGO, OSCURO, altura=-0.6, distancia=1.4, vivo=True, encara=False),
        saltar(ARCHIMAGO,
               "SE QUITA DE EN MEDIO. Salta hacia ARRIBA y de lado, desde donde este, y baja al "
               "suelo que tenga debajo. Antes esto eran dos viajes a marcas aereas puestas a "
               "mano: iba hacia la coordenada y no hacia arriba -- \"parece que salta para otro "
               "lado\" -- y aterrizaba donde dijera el numero, que en la sexta grabacion fue "
               "encima de un tejado.",
               altura=2.8, desplazamiento=2.6, lado=True,
               subida=0.4, sostener=0.35, caida=0.45,
               poseAire="JumpAirSpin_InPlace_NoWeapon"),
        esperar(1.2, "Un respiro. Aqui es donde se entiende que ha esquivado."),

        # ── Asalto 3: contesta desde el suelo y el otro se aparta volando ─────
        plano("El, desde abajo, contestando.",
              "Medium", ARCHIMAGO, OSCURO, altura=-1.0, distancia=1.2),
        emocion(ARCHIMAGO, 10),
        gesto(ARCHIMAGO, "MagicLeft", "Responde.", hold=0.5),

        # Este SI va a por el, y de verdad: SpellBeat relee el objetivo cada fotograma, asi
        # que mientras el Mago Oscuro se aparta volando, el hechizo le persigue.
        a_la_vez("El contraataque, siguiendole por el aire.",
                 [hechizo(ARCHIMAGO, objetivo=OSCURO, proyectil=VFX_BOLA_LUZ,
                          sfxLanza="Star_SpellCast", sfxImpacto="ProjectileClash",
                          velocidad=15.0, carga=0.3, sacudida=0.4,
                          registrar="HECHIZO_ARCHIMAGO",
                          note="Nace en su mano y persigue al Mago Oscuro."),
                  esperar(0.3, "", sinEscalar=False)],
                 esperarATodos=False),

        # El unico golpe que le llega en todo el prologo, y antes duraba medio segundo:
        # el hechizo le alcanzaba, el seguia volando y se reia en el mismo aliento.
        # Raul: "cuando el mago oscuro recibe el golpe del archimago esta volando
        # mirando hacia abajo y se rie, eso se pierde, deberiamos esperar". Y a
        # continuacion: "cuando baja yo no lo bajaria volando, dejaria que cayera".
        # Las dos cosas son la misma nota y se arreglan juntas: el golpe le TIRA DEL
        # CIELO, cae, toma tierra, y se rie desde el suelo -- que es peor.
        plano("Le da en el aire, y se le ve encajarlo.",
              "Tracking", OSCURO, ARCHIMAGO, altura=-2.2, distancia=1.3, vivo=True, encara=False),
        gesto(OSCURO, "TakeDamage", "Le alcanza de lleno.", hold=0.5),
        esperar(0.6, "Un instante suspendido antes de venirse abajo."),

        plano("Y CAE.", "Tracking", OSCURO, "", altura=-1.0, distancia=1.5, vivo=True, encara=False),
        caer(OSCURO, "No baja volando: se cae. Es lo que hace el jugador cuando le alcanzan en "
                     "el aire, y es lo que hace que el golpe cuente.", segundos=0.75),
        gesto(OSCURO, "Landing", "Toma tierra de mala manera.", hold=0.5),
        esperar(1.1, "Y AQUI se espera. Es el unico momento del prologo en que el Mago Oscuro "
                     "no manda."),

        plano("Su cara, desde abajo.", "CloseUp", OSCURO, ARCHIMAGO, altura=-0.7, distancia=1.1),
        emocion(OSCURO, 13),
        gesto(OSCURO, "Laugh01", "Y se rie. Desde el suelo, que es peor.", hold=1.3),
        esperar(1.0),

        # ── Asalto 4: el picado ───────────────────────────────────────────────
        plano("Los dos: el arriba, el otro abajo. Lo que viene hay que verlo entero.",
              "Wide", OSCURO, ARCHIMAGO, altura=-2.6, distancia=1.9),
        gesto(OSCURO, "Challenging_NoWeapon", "Toma impulso.", hold=0.8),
        saltar(OSCURO, "Y vuelve a subir, ahora que esta en el suelo.",
               altura=7.0, desplazamiento=0.0, subida=0.6, sostener=0.2, caida=0.0),
        esperar(0.7),

        sfx("MagoOscuroGrieta", actor=OSCURO),
        plano("EL PICADO, con la camara pegada a el.",
              "Tracking", OSCURO, ARCHIMAGO, altura=-0.8, distancia=1.3, vivo=True, encara=False),
        # La guardia se levanta MIENTRAS baja, no despues.
        #
        # "el archimago se protege pero el mago oscuro no ha lanzado nada". El picado si
        # era el ataque, pero los beats iban en fila: primero bajaba entero, y solo cuando
        # habia terminado se cubria el Archimago. O sea que se protegia de algo que ya
        # habia pasado. En paralelo se lee lo que es: lo ve venir y se cubre.
        a_la_vez("Baja a por el, y el se cubre mientras baja.",
                 [volar(OSCURO, ["M_Picado_Oscuro"], "fly_dive", 9.0, 1.3,
                        "Se tira en picado a por el: diez metros en poco mas de un segundo."),
                  gesto(ARCHIMAGO, "Defend_NoWeapon", "Lo ve venir y se cubre.", hold=0.0),
                  vfx(VFX_ESCUDO, "El escudo, encendido antes del choque.",
                      actor=ARCHIMAGO, offset=(0.0, 0.0, 0.0), vida=1.8, sigue=True)],
                 esperarATodos=True),

        # Y AHORA el choque. Antes no habia impacto ninguno: solo un fogonazo y un temblor,
        # que sin nada que reviente se leen como que no ha pasado nada.
        vfx(VFX_IMPACTO_J, "El choque, sobre el escudo.",
            actor=ARCHIMAGO, offset=(0.0, 0.7, 0.0), vida=1.2),
        sfx("MagoOscuroGolpe", actor=ARCHIMAGO),
        fogonazo(duracion=0.16),
        temblor(intensidad=0.8, duracion=0.9),
        gesto(ARCHIMAGO, "DefendHit_NoWeapon", "Aguanta, pero le tira para atras.", hold=0.6),

        # Y toca suelo de verdad antes de volver a subir. Es lo que pedia Raul con su propia
        # regla: "si el player esta volando y pulsa el boton de dejar de volar, que animacion
        # mostramos, pues aqui la misma".
        gesto(OSCURO, "Landing", "Toma tierra al final del picado.", hold=0.5),

        plano("Su cara, aguantando.",
              "Reaction", ARCHIMAGO, OSCURO, altura=0.0, distancia=1.05),
        esperar(1.1),

        gesto(OSCURO, "JumpStart_InPlace_NoWeapon", "Y vuelve a despegar.", hold=0.25),

        volar(OSCURO, ["M_Aire_Oscuro_3"], "fly_idle", 5.5, 1.8,
              "Y vuelve a subir, por el otro lado."),
        esperar(0.9),

        # ── El hechizo ────────────────────────────────────────────────────────
        plano("EL PLANAZO otra vez, mas cerrado. Las manos levantadas y el cielo detras.",
              "Wide", OSCURO, "", altura=-5.5, distancia=1.5, fov=56.0, encara=False),
        mantener(OSCURO, "FoundSomething_NoWeapon",
                 "SOSTIENE el hechizo. Es una pose mantenida, que es lo que significa sostener: "
                 "repetirla tres veces era lo que se veia como un tic."),
        sfx("Prologue_SpellChargeLoop", actor=OSCURO),
        vfx(VFX_BOLA, "El hechizo nace entre sus manos. Y se queda EN sus manos: esta flotando, "
            "asi que un VFX clavado en el mundo se le escapa del cuerpo.",
            actor=OSCURO, offset=(0.0, 1.9, 0.0), vida=4.5, sigue=True),
        esperar(1.3),

        vfx(VFX_CARGA, "Y crece. Un segundo VFX encima del primero, mas grande, es lo que hace "
                       "que se lea como que se esta HACIENDO GRANDE y no como que ya estaba ahi.",
            actor=OSCURO, offset=(0.0, 2.1, 0.0), vida=3.5, sigue=True),
        temblor(intensidad=0.35, duracion=1.6),
        esperar(1.6, "Que dure. Es la unica amenaza de todo el prologo que se ve venir."),

        plano("Su cara, un segundo antes de soltarlo.",
              "CloseUp", OSCURO, ARCHIMAGO, altura=-1.2, distancia=1.1),
        esperar(1.0),

        plano("Y lo suelta.",
              "Medium", OSCURO, ARCHIMAGO, altura=-3.4, distancia=1.45),
        gesto(OSCURO, "MagicRight", "Lo lanza.", hold=0.3),
        sfx("Prologue_WarClashStinger_A", actor=OSCURO),

        # El grande. Sale de sus manos y baja DESPACIO -- ocho metros por segundo -- para que
        # se le vea venir. Se registra como actor para que la camara pueda seguirlo.
        a_la_vez("Y cae. El plano corta al Archimago mientras el hechizo sigue en el aire.",
                 [hechizo(OSCURO, objetivo=ARCHIMAGO, proyectil=VFX_BOLA_OSC,
                          mano=VFX_ESTALLIDO, sfxImpacto="Prologue_Explosion",
                          alturaMano=1.9, velocidad=8.0, carga=0.5, sacudida=0.9,
                          registrar="HECHIZO_FINAL", note="El hechizo grande, cayendo."),
                  esperar(0.4, "", sinEscalar=False)],
                 esperarATodos=False),
        esperar(0.6),

        # ── El Archimago corre y salta ────────────────────────────────────────
        plano("CORTE al Archimago. Le sigue mientras corre, con el Mago Oscuro de secundario para "
              "que la camara no gire con el.",
              "Tracking", ARCHIMAGO, OSCURO, altura=-0.5, distancia=1.4, vivo=True, encara=False),
        emocion(ARCHIMAGO, 10),
        andar(ARCHIMAGO, ["M_Carrera_Mago"],
              "Corre HACIA lo que le viene encima, no en contra. Cuatro metros y medio por segundo "
              "es carrera: WalkPathBeat pasa esa velocidad al animator y sale Run.",
              velocidad=4.5),

        sfx("Star_SpellCast", actor=ARCHIMAGO),
        escala_tiempo(0.45, "Camara lenta SOLO para esto.", rampa=0.2),

        plano("El, en el aire, desde abajo, con el hechizo cayendole encima. VIVO: sube tres "
          "metros, asi que un plano quieto le pierde justo cuando dice la frase.",
              "Medium", ARCHIMAGO, OSCURO, altura=-2.4, distancia=1.5, vivo=True, encara=False),
        saltar(ARCHIMAGO,
               "EL SALTO. Sube tres metros desde donde este y SE QUEDA ARRIBA (caida=0): la "
               "frase se dice en el aire y lo que viene despues es el fundido a negro, asi que "
               "no hay que bajarle. Antes iba a la marca M_Salto_Mago_Aire y terminaba de pie "
               "encima de un tejado.",
               altura=3.0, desplazamiento=1.2, lado=False,
               subida=0.7, sostener=0.0, caida=0.0),

        decir(ARCHIMAGO, "PROLOGO_HECHIZO", "Archimago", 2.4,
              note="Dicha en el aire. Es la ultima frase del prologo."),

        prompt_beat,

        vfx(VFX_ESCUDO, "La luz nace en sus manos y cubre el valle. SIGUE al Archimago: lo "
            "lanza en el aire y despues baja, y sin esto el escudo se queda arriba.",
            actor=ARCHIMAGO, offset=(0.0, 0.0, 0.0), vida=3.0, sigue=True),
        vfx(VFX_COLUMNA, "", actor=ARCHIMAGO, offset=(0.0, 0.0, 0.0), vida=3.0, sigue=True),
        fogonazo("El escudo se enciende.", color="new Color(1f, 1f, 1f, 1f)", duracion=0.25),
        esperar(0.5),

        # ── Negro. Y entonces. ────────────────────────────────────────────────
        fundido("A NEGRO, y la explosion se oye DESPUES. Ver lo que pasa seria menos que "
                "imaginarlo: lo que hay al otro lado del negro es el valle entero.",
                entrando=True, color="Color.black", duracion=0.7, esperarFin=True),
        escala_tiempo(1.0),
        esperar(0.6, "Medio segundo de negro y de silencio."),
    ]
    return fase


# ── 9. La explosion, y Will ───────────────────────────────────────────────────

def fase_explosion(fase):
    fase.nombre = '"9 - La explosion"'
    fase.beats = [
        sfx("Prologue_Explosion", "LA EXPLOSION, sobre negro."),
        sfx("Prologue_WarClashStinger_B"),
        temblor("El mando tiembla aunque no se vea nada. Es lo que hace que el negro sea la "
                "explosion y no un corte.",
                intensidad=0.9, duracion=1.6, esperarFin=False),
        esperar(1.8),

        fundido("Y de ese negro se sale a BLANCO -- el blanco se convierte en la luz de la manana "
                "entrando por la ventana de Will.",
                entrando=True, color="new Color(1.0f, 1.0f, 1.0f, 1.0f)", duracion=1.6,
                esperarFin=True),
        esperar(0.8),
    ]
    return fase


# ── Pase de pulido (novena grabacion) ────────────────────────────────────────
#
# Los apuntes de Raul que no son causa de raiz sino montaje, uno por uno.

def pase_pulido(fases):
    manana = por_nombre(fases, "1 - Un dia cualquiera")
    globo_ok = por_nombre(fases, "1b - El globo")
    rio = por_nombre(fases, "2 - El rio")
    llegada = por_nombre(fases, "4 - La llegada")
    orden = por_nombre(fases, "5 - La orden")
    duelo = por_nombre(fases, "7 - El duelo")
    final = por_nombre(fases, "8 - El ultimo hechizo")
    explosion = por_nombre(fases, "9 - La explosion")

    # 1. La apertura: vista de pajaro.
    #
    # "el primer plano lo veo demasiado lejos; yo haria un plano cenital, el tipico
    # vista de pajaro en el que se abren las nubes y se ve el pueblecito, y ya
    # enganchamos con el clip del archimago diciendo lo del sol".
    #
    # Un cenital es el mismo plano general pero con la camara MUY arriba: heightBias
    # +14 la pone a catorce metros sobre donde le tocaria. Y baja hasta el plano medio
    # con 'smooth', que es el enganche que pide -- no un corte.
    b = manana.beats[indice(manana, "La plaza con gente y la montana al fondo")]
    b.set("note", s("VISTA DE PAJARO. La camara catorce metros por encima del pueblo, mirandolo "
                    "desde arriba, y desde ahi baja sola hasta el. El valle primero, el hombre "
                    "despues."))
    b.set_framing("heightBias", f(14.0))
    b.set_framing("distanceScale", f(1.6))
    b.set("duration", f(4.5))

    b = manana.beats[indice(manana, "El, mirando su valle")]
    b.set("note", s("Y baja hasta el, sin cortar: el descenso ES el enganche con su frase."))
    b.set("smooth", b_(True))
    b.set("duration", f(3.2))

    # 5. "Seguid asi, que se os oiga desde el puente": el primero muy lejos y el
    #    segundo de espaldas. Se cierra el general y el contraplano se hace reaccion.
    b = manana.beats[indice(manana, "El corro que celebra")]
    b.set_framing("distanceScale", f(1.0))
    b.set_framing("heightBias", f(0.3))

    i = indice(manana, "PROLOGO_BAILE") if False else None
    for j, bb in enumerate(manana.beats):
        if bb.tipo == "ShotBeat" and bb.framing("subjectId") == s(ARCHIMAGO) \
                and bb.framing("secondaryId") == s("NPC_Aldeano_03"):
            bb.set("note", s("El, en el corro. Plano medio de verdad, no un general."))
            bb.set_framing("type", "ShotType.Medium")
            bb.set_framing("distanceScale", f(0.95))

    # 6. El globo: el corro de bocadillos.
    #
    # "con cuidado ahi va que lo disfruten... yo diria: con cuidado. Y bocadillos de
    # la gente diciendo 'ala, por fin vuela' o cosas asi, en plan muchos a la vez".
    #
    # Tres vecinos distintos, encabalgados: el Parallel no espera a nadie, asi que
    # las tres frases se solapan como se solapan las voces de un corro.
    i = indice(globo_ok, "")
    globo_ok.beats.append(
        a_la_vez("El corro entero a la vez. Que se pisen: es lo que hace que suene a gente y no "
                 "a turnos de palabra.",
                 [esperar(0.1, "", sinEscalar=False),
                  decir("NPC_Aldeano_05", "PROLOGO_GLOBO_VECINO_1", "Vecina", 2.4),
                  decir("NPC_Aldeano_03", "PROLOGO_GLOBO_VECINO_2", "Vecino", 2.4),
                  decir("NPC_Aldeano_08", "PROLOGO_GLOBO_VECINO_3", "Vecino", 2.4)],
                 esperarATodos=False))
    globo_ok.beats.append(esperar(2.4, "Que se oiga el corro."))

    # 7. El rio: quitarle el paseo.
    #
    # "la conversacion en el rio con Liora no puede ser mas ortopedica, esta super
    # mal. Mejor planos sencillos: pon los dos mirandose quietos y manteniendo la
    # conversacion. Cuando tengamos todo correcto ya podemos complicarlo."
    #
    # Tiene razon y es la decision correcta: el walk-and-talk necesita que andar
    # funcione bien, y andar no funciona bien todavia. Se vuelve a plano/contraplano
    # con los dos parados, que es lo que nunca falla.
    rio.beats = [
        plano("Bajan de la plaza al rio. Encuadrado sobre ELLOS y bastante cerrado: antes esto "
              "era un general tan abierto que durante tres segundos solo se veian casas.",
              "Wide", ARCHIMAGO, LIORA, altura=1.0, distancia=1.5, suave=True, duracion=3.0),

        a_la_vez("Bajan juntos y se plantan.", [
            andar(ARCHIMAGO, ["M_Rio_Camino", "M_Rio_Fin_Mago"], "", velocidad=1.3),
            andar(LIORA, ["M_Rio_Camino", "M_Rio_Fin_Liora"], "", velocidad=1.3),
        ], esperarATodos=True),

        eje("La camara al oeste: el agua y el puente detras de ellos.", 270.0),
        mirar(ARCHIMAGO, hacia=LIORA, note="Se encaran.", mutuo=True, giro=0.6),
        esperar(1.6, "Y el agua, un momento, antes de que nadie hable."),

        plano("Los dos, quietos, encarados, con el rio detras.",
              "TwoShot", ARCHIMAGO, LIORA, distancia=1.15),
        emocion(LIORA, 1),
        decir(LIORA, "PROLOGO_PLAZA_LIORA_1", "Liora", 3.2),

        plano("", "Reaction", ARCHIMAGO, LIORA, distancia=1.1),
        emocion(ARCHIMAGO, 1),
        decir(ARCHIMAGO, "PROLOGO_PLAZA_ARCHIMAGO", "Archimago", 3.8),

        plano("", "Reaction", LIORA, ARCHIMAGO, distancia=1.1),
        decir(LIORA, "PROLOGO_PLAZA_LIORA_2", "Liora", 3.4),

        gesto(LIORA, "Laugh01", hold=0.9),
        gesto(ARCHIMAGO, "Laugh01", hold=0.9),

        plano("Los dos riendose con el rio detras. Es el ultimo momento tranquilo del prologo.",
              "TwoShot", ARCHIMAGO, LIORA, altura=0.4, distancia=1.45),
        esperar(2.6),
    ]

    # 8. La musica del Mago Oscuro entra CUANDO APARECE, no veinte segundos despues.
    musica_reveal = llegada.beats[indice(llegada, "Y entonces entra su musica")]
    llegada.beats.remove(musica_reveal)
    musica_reveal.set("note", s("Su musica entra AQUI, con la figura, no cuando termina de bajar. "
                                "Lo que da miedo es el momento en que aparece."))
    llegada.beats.insert(indice(llegada, "Vineta 5 - la silueta recortada"), musica_reveal)

    # 9. Y no se gira para hablar.
    #
    # "iba bien bajando la colina y se gira para decir lo de arrodillaos; no tiene
    # sentido". Un FaceBeat explicito justo antes de la frase le fija mirando al
    # pueblo, que es hacia donde venia.
    insertar_antes(llegada, "Su cara, de cerca y desde abajo",
                   mirar(OSCURO, hacia=ARCHIMAGO,
                         note="Se queda mirando al pueblo, que es hacia donde venia. Sin esto "
                              "acaba de perfil.", giro=0.5))

    # 10. El escudo NO se levanta en la orden de evacuar.
    #
    # "al puente todos ahora y lanza un hechizo de proteccion, lo quitamos: solo lo va
    # a usar para protegerse el durante el combate, por eso estan saliendo del valle".
    #
    # Y de paso arregla el otro apunte -- la media pantalla azul encima del bocadillo
    # de 'Proteccion Absoluta' era esa misma cupula, encendida desde hacia dos minutos.
    for nota in ("Levanta el escudo", "Vineta 8 - el escudo"):
        quitar(orden, nota)
    for bb in list(orden.beats):
        if bb.tipo in ("VfxBeat", "SfxBeat") and bb.get("atActorId") == s(ARCHIMAGO):
            orden.beats.remove(bb)
    for nota in ("La plaza vaciandose hacia el puente, con el escudo encendido encima.",):
        bb = orden.beats[indice(orden, nota)]
        bb.set("note", s("La plaza vaciandose hacia el puente."))
        bb.set_framing("secondaryId", s(LIORA))

    # 11. La musica no se corta a mitad de la pelea.
    #
    # "de pronto la musica deja de sonar". Era a proposito -- un silencio para que la
    # frase del destierro cayera sola -- pero con la batalla aerea nueva por delante,
    # cortarla ahi mata justo lo que hay que agrandar. El silencio se va al final, al
    # ultimo hechizo, que es donde de verdad significa algo.
    # (20 sep, novena) Y al final tampoco. Raul: "cuando el mago oscuro lanza el conjuro
    # se para la musica, por que?". No hay por que: el silencio era una idea mia y en
    # el sitio nuevo tampoco funciona -- el momento mas grande de la secuencia se queda
    # sin musica justo cuando mas la necesita. Fuera del todo.
    duelo.beats.remove(duelo.beats[indice(duelo, "SILENCIO. Se corta la musica entera")])

    # 12. El escudo, aqui.
    insertar_antes(final, "La luz nace en sus manos",
                   prop("PROP_Escudo", True,
                        "La cupula se levanta AHORA, con la frase, y no dos minutos antes. Era lo "
                        "que llenaba media pantalla de azul encima del bocadillo."))

    # 13. El boton del prompt.
    #
    # "sale que pulsemos el boton A, que es el mismo de pasar la secuencia; por mas
    # que lo pulse no pasa nada. Ya hemos dicho varias veces que hay que cambiarlo".
    #
    # A es Interact, y Interact es tambien saltar la cinematica: la pulsacion se la
    # come el saltador antes de llegar al prompt. Se cambia a AttackMagicNorth -- la
    # Y del mando, el boton de magia ESPECIAL del juego, que es exactamente lo que es
    # Proteccion Absoluta -- y de machaqueo con direccion a un simple mantener.
    prompt = next(bb for bb in final.beats if bb.tipo == "InputPromptBeat")
    prompt.set("note", s("Mantener Y. No es A a proposito: A es Interact, que es tambien el boton "
                         "de saltarse la secuencia, y por eso pulsarlo no hacia nada."))
    prompt.set("actionRef", 'Accion("GamePlay", "AttackMagicNorth")')
    prompt.set("directionActionRef", "null")
    prompt.set("iconGlyphName", s("interactable_y"))
    prompt.set("holdSeconds", f(1.2))
    prompt.set("maxHoldSeconds", f(4.0))
    prompt.envoltura = (prompt.envoltura[0], ", PanicInputMode.Hold")

    # 14. El final: un solo negro.
    #
    # "la explosion ocurre en negro, lo cual es correcto, pero de pronto vemos otra vez
    # el valle y fundido a blanco, otra vez el valle y fundido a negro. El primer
    # fundido a negro debe enganchar con lo siguiente".
    #
    # Eran dos fundidos peleandose: el negro de la fase 8 y el blanco de la 9, y entre
    # los dos el valle asomando. Se queda UNO. La secuencia ya tiene endStayBlack, asi
    # que el negro se mantiene y Will despierta encima de el.
    explosion.beats = [
        sfx("Prologue_Explosion", "LA EXPLOSION, sobre negro. Y el negro se queda: de aqui se sale "
                                  "ya en la habitacion de Will, sin volver a ensenar el valle."),
        sfx("Prologue_WarClashStinger_B"),
        temblor("El mando tiembla aunque no se vea nada. Es lo que hace que el negro sea la "
                "explosion y no un corte.",
                intensidad=0.9, duracion=1.6, esperarFin=False),
        esperar(2.2),
    ]

    return fases


# ── Pase de la decima grabacion ───────────────────────────────────────────────
#
# El hallazgo gordo: CUARENTA Y CINCO SEGUNDOS seguidos de casas.
#
# Del 1:12 al 1:57 la camara esta parada detras de una fila de casas y no se ve a
# nadie. Los bocadillos salen encima de un tejado. Y la causa no es el solver: es
# que las fases 1b y 1c NO TIENEN NI UN PLANO. Heredan el ultimo de la fase 1 --
# el contrapicado del globo -- y el globo, para cuando hablan, se ha ido veintidos
# metros hacia arriba. La camara sigue mirando donde estaba.
#
# Lo mismo en el rio: el plano general de "bajan al rio" se resuelve ANTES de que
# anden, asi que encuadra la plaza, y los dos se van dieciocho metros al este y
# salen de cuadro. La camara se queda mirando unas casas.
#
# La regla que faltaba, y que ahora comprueba validar.py: DESPUES DE ANDAR, UN
# PLANO. Siempre. O el plano que cubre la caminata es 'live'.

def pase_decima(fases):
    manana = por_nombre(fases, "1 - Un dia cualquiera")
    globo_ok = por_nombre(fases, "1b - El globo")
    globo_mal = por_nombre(fases, "1c - El globo")
    rio = por_nombre(fases, "2 - El rio")
    duelo = por_nombre(fases, "7 - El duelo")
    final = por_nombre(fases, "8 - El ultimo hechizo")

    # 1. "Con cuidado" se dice MIENTRAS lanza el hechizo, no despues.
    #
    # Y el plano del vecino que avisa, para que la frase no salga de un tejado.
    insertar_antes(manana, "El globo enganchado en el campanario",
                   plano("El vecino senalando hacia arriba. Es QUIEN HABLA: sin este plano la "
                         "frase sale de un tejado, que es lo que se ve en la decima grabacion.",
                         "Medium", "NPC_Aldeano_05", "PROP_Globo", altura=-0.5, distancia=1.2))

    insertar_antes(manana, "El aura prende tambien en el globo",
                   decir(ARCHIMAGO, "PROLOGO_MANANA_GLOBO_OK", "Archimago", 2.0,
                         note="'Con cuidado...' se dice AL HACER el hechizo, no despues de que "
                              "el globo ya se haya ido. Es una advertencia, no un comentario."))

    # 2. Las fases del globo necesitan sus propios planos. Las dos.
    globo_ok.beats = [
        plano("La cara del vecino mirando subir el globo.",
              "Reaction", "NPC_Aldeano_05", "PROP_Globo", altura=-0.6, distancia=1.1),
        emocion("NPC_Aldeano_05", 13),
        esperar(0.8),

        plano("Y el corro entero, celebrandolo.",
              "Wide", "NPC_Aldeano_03", "NPC_Aldeano_05", altura=0.5, distancia=1.2),
        a_la_vez("El corro a la vez. Que se pisen: es lo que hace que suene a gente y no a "
                 "turnos de palabra.",
                 [esperar(0.1, "", sinEscalar=False),
                  decir("NPC_Aldeano_05", "PROLOGO_GLOBO_VECINO_1", "Vecina", 2.4),
                  decir("NPC_Aldeano_03", "PROLOGO_GLOBO_VECINO_2", "Vecino", 2.4),
                  decir("NPC_Aldeano_08", "PROLOGO_GLOBO_VECINO_3", "Vecino", 2.4)],
                 esperarATodos=False),
        a_la_vez("Y lo celebran con el cuerpo, no solo con la boca.",
                 [gesto("NPC_Aldeano_05", "Cheer01", hold=0.0),
                  gesto("NPC_Aldeano_03", "HandClap01", hold=0.0),
                  gesto("NPC_Aldeano_08", "Cheer02", hold=0.0),
                  gesto("NPC_Aldeano_01", "Laugh01", hold=0.0)],
                 esperarATodos=False),
        esperar(2.6, "Que se oiga el corro."),
        prop("PROP_Globo", False, "Y ahora si se apaga, con el ya fuera de plano."),
    ]

    globo_mal.beats = [
        plano("La cara del vecino cuando revienta.",
              "Reaction", "NPC_Aldeano_05", ARCHIMAGO, altura=-0.4, distancia=1.1),
        prop("PROP_Globo", False, "El globo revienta."),
        emocion("NPC_Aldeano_05", 2),
        plano("Y el, disculpandose.", "Medium", ARCHIMAGO, "NPC_Aldeano_05", distancia=1.1),
        decir(ARCHIMAGO, "PROLOGO_MANANA_GLOBO_ROTO", "Archimago", 2.6),
        esperar(1.2),
    ]

    # 3. El rio: el plano general va DESPUES de andar, no antes.
    i = indice(rio, "Bajan de la plaza al rio")
    general = rio.beats.pop(i)
    general.set("note", s("Y ya en la orilla, el general: los dos, el agua y el puente. Este plano "
                          "iba ANTES de que anduvieran, asi que encuadraba la plaza y ellos se iban "
                          "de cuadro -- cuarenta segundos de casas."))
    general.set("smooth", b_(False))
    general.set_framing("distanceScale", f(1.35))

    # La caminata la cubre un plano VIVO, que es el unico que puede seguir a alguien.
    insertar_antes(rio, "Bajan juntos y se plantan",
                   plano("Bajando hacia el rio. VIVO: un plano quieto no puede cubrir dieciocho "
                         "metros de caminata.",
                         "Wide", ARCHIMAGO, LIORA, altura=1.2, distancia=1.5, vivo=True))
    insertar_tras(rio, "Se encaran", general)

    # 4. Fuera el prompt. "Yo quitaria esto, que todo sea cinematica".
    for bb in list(final.beats):
        if bb.tipo == "InputPromptBeat":
            final.beats.remove(bb)

    # 5. El escudo, a la altura del pecho y no por encima de la cabeza.
    #
    # Estaba con un offset de +1,0 m sobre los pies, y estos personajes miden 1,15:
    # el escudo salia flotando sobre el sombrero.
    for fase in (duelo, final):
        for bb in fase.beats:
            if bb.tipo == "VfxBeat" and "b99922c2" in (bb.get("vfxPrefab") or ""):
                bb.set("offset", v3(0.0, 0.45, 0.0))

    # 6. Que baile todo el corro, no tres.
    i = indice(manana, "El corro, a la vez - dos bailando y uno llevando el compas")
    corro = manana.beats[i]
    ya = {h.get("actorId") for h in corro.hijos}
    for actor, paso in (("NPC_Aldeano_01", "Dance_NoWeapon"),
                        ("NPC_Aldeano_03", "Dance_NoWeapon"),
                        ("NPC_Aldeano_08", "HandClap01"),
                        ("NPC_Aldeano_02", "Dance_NoWeapon"),
                        ("NPC_Aldeano_07", "Cheer02"),
                        ("NPC_Aldeano_06", "HandClap01")):
        if s(actor) in ya:
            continue
        corro.hijos.append(gesto(actor, paso, repeticiones=3, hold=1.4))
    corro.set("note", s("El corro ENTERO. Antes bailaban tres y el resto se quedaba de pie en "
                        "medio del corro mirando al frente, que es lo que canta."))

    # 7. Vida ambiental.
    #
    # "me falta que los npcs anden cuando no hacen nada, hablen entre ellos". Cuatro
    # rondas de gestos de conversacion repartidos por los tramos en los que la camara
    # esta en otra cosa: sin esto, en cuanto un vecino termina su gesto se queda de
    # pie mirando al frente hasta el final del prologo.
    insertar_tras(manana, "Dejar respirar la plaza antes de que nadie hable",
                  vida(ALDEANOS, "La plaza habla sola desde el primer segundo."))
    insertar_tras(manana, "Y se queda ahi flotando un segundo",
                  vida(ALDEANOS[2:], "Y los de mas alla siguen a lo suyo."))
    insertar_tras(manana, "El campanario con el globo enganchado",
                  vida(ALDEANOS[:6], "Los de la plaza, mientras tanto."))

    rio_i = indice(rio, "Se encaran")
    rio.beats.insert(rio_i, vida(ALDEANOS, "El pueblo sigue vivo detras, aunque no salga."))

    pase_sexta(fases)
    return fases


def pase_sexta(fases):
    """
    La sexta grabacion. Lo que Raul vio, arreglado donde estaba la causa.
    """
    manana = fases[0]
    rio = fases[3]

    # 1. EL MAGO OSCURO ESTABA BAILANDO EN LA PLAZA.
    #
    # "el mago oscuro esta con la gente que baila al principio y hay que quitarlo".
    # Su SpawnPoint esta en (6000.5, 100, 6005) -- dentro de la plaza, a cinco metros
    # del corro. La fase 4 lo coloca en M_Cresta con un "Y AHORA aparece", pero hasta
    # entonces lleva dos minutos de pie entre los vecinos.
    #
    # M_Espera_Oscuro existe justo para esto: (6000, 100, 6068), a 68 m al norte.
    manana.beats.insert(0, colocar(
        OSCURO, "M_Espera_Oscuro",
        note="FUERA DE ESCENA. Su SpawnPoint esta en mitad de la plaza, asi que sin esto "
             "se pasa los dos primeros minutos de pie entre los vecinos que bailan. "
             "Aparece en la fase 4, no antes."))

    # 2. El campanario, encuadrado sobre EL CAMPANARIO.
    #
    # "se ha enganchado arriba del todo lo sigue diciendo la casa". El plano era
    # Wide sobre el vecino con el Archimago de secundario, o sea que la camara se iba
    # hacia el Archimago y encuadraba tejados. Lo que hay que ver es el globo colgado
    # del campanario con el vecino debajo, asi que el sujeto es el globo.
    b = manana.beats[indice(manana, "El campanario con el globo enganchado")]
    b.set_framing("subjectId", s("PROP_Globo"))
    b.set_framing("secondaryId", s("NPC_Aldeano_05"))
    b.set_framing("heightBias", f(-1.8))
    b.set_framing("distanceScale", f(1.15))
    b.set("note", s("El campanario con el globo enganchado, en contrapicado, y el vecino "
                    "debajo. Encuadrado sobre EL GLOBO: antes iba sobre el vecino con el "
                    "Archimago de secundario y la camara se iba hacia el, a encuadrar tejados."))

    # 3. La bajada al rio, desde arriba.
    #
    # "Volvemos a tener el mismo plano que no quiero que es el de las casas. Ese quitalo
    # pon vista cenital si no te sale otra cosa." Son veinticinco segundos de caminata y
    # a ras de suelo no hay forma: el pueblo entero se mete en medio. Desde doce metros
    # se les ve a los dos, se ve el pueblo y se ve a donde van.
    b = rio.beats[indice(rio, "Bajando hacia el rio")]
    b.set_framing("heightBias", f(12.0))
    b.set_framing("distanceScale", f(1.0))
    b.set("note", s("Bajando hacia el rio, EN PICADO desde doce metros. A ras de suelo este "
                    "tramo eran veinticinco segundos de fachadas: el pueblo se mete en medio "
                    "haga lo que haga la camara. Desde arriba se les ve a ellos, el pueblo y "
                    "a donde van."))

    # 4. El mago no se gira para decir "con cuidado".
    #
    # El globo ya esta muy por encima, asi que encarar hacia el no significa nada: al
    # aplanar la direccion queda un vector diminuto y acaba mirando a cualquier sitio.
    # El beat lo protege igualmente (ver ShotBeat.Co_Encarar), pero aqui se dice tambien
    # en el encuadre, que es donde se lee.
    b = manana.beats[indice(manana, "Contraplano - el mago mirando hacia arriba")]
    b.set_framing("encara", b_(False))

    # 4b. Los VFX que van PEGADOS a algo, que lo acompanen.
    #
    # "el prefab de la proteccion se queda muy arriba". No era la altura: VfxBeat
    # planta el efecto en la posicion que el actor ocupaba en ESE instante y ahi se
    # queda. Con alguien quieto no se nota; con el Archimago lanzando el escudo en el
    # aire y bajando despues, el escudo se queda arriba.
    #
    # Y lo mismo, que no habia visto, con las dos auras de la manana: la carreta
    # levita y el globo sube, y sus auras se quedaban donde estaban las cosas antes
    # de moverse.
    #
    # Lo que ocurre en un SITIO no se toca: el polvo que levanta al despegar tiene
    # que quedarse en el suelo, y un impacto pasa donde pasa.
    acompanan = (
        "La magia nace en su mano",
        "El aura prende en la carreta",
        "El aura prende tambien en el globo",
        "La bola se forma en su mano",
        "El escudo se enciende justo a tiempo",
    )

    def pegar_al_actor(beats):
        tocados = 0
        for b in beats:
            if b.tipo == "VfxBeat":
                nota = b.get("note", "") or ""
                if any(("\"" + k) in nota or k in nota for k in acompanan):
                    b.set("seguirAlActor", "true")
                    tocados += 1
            if b.hijos:
                tocados += pegar_al_actor(b.hijos)
        return tocados

    n = 0
    for fase in fases:
        n += pegar_al_actor(fase.beats)
    if n != len(acompanan):
        raise SystemExit(f"esperaba {len(acompanan)} VFX que acompanan y he tocado {n}")

    # 4c. La llegada del Mago Oscuro: que se le vea.
    #
    # "de pronto aparece el mago oscuro al principio". Con el ya fuera de la plaza
    # (arriba), la aparicion vuelve a tener sentido dramatico. Lo que faltaba es que
    # se le VEA: sus tres planos de la cresta iban a 1,8x, 1,6x y 1,4x de distancia
    # sobre un personaje de 1,15 m a cincuenta metros del pueblo, o sea una mota.
    # Y se le veia de espaldas porque nada le habia girado hacia el valle.
    # OJO: Fase.nombre viene con las comillas puestas ('"4 - La llegada"'), porque es el
    # literal C# tal cual. Buscar por prefijo sin tenerlo en cuenta no encuentra nada.
    def fase_que_empieza(prefijo):
        for fa in fases:
            if fa.nombre.strip('"').startswith(prefijo):
                return fa
        raise KeyError(f"no hay ninguna fase que empiece por '{prefijo}'")

    llegada = fase_que_empieza("4")

    for nota, dist in (("la silueta recortada contra las nubes", 1.25),
                       ("en la ladera, en contrapicado", 1.1),
                       ("un plano que le SIGUE mientras baja", 1.05)):
        b = llegada.beats[indice(llegada, nota)]
        b.set_framing("distanceScale", f(dist))

    # Y que mire al pueblo desde el primer fotograma en que existe. El PlaceAtMarkBeat
    # ya le orienta hacia M_Apertura, pero encararle al Archimago es lo que fija el eje
    # de todos los planos siguientes -- y es a quien ha venido a ver.
    llegada.beats.insert(indice(llegada, "la silueta recortada contra las nubes"),
                         mirar(OSCURO, hacia=ARCHIMAGO,
                               note="Mira al valle desde el primer fotograma: si no, su entrada "
                                    "es la espalda de alguien parado en una loma.", giro=0.0))

    # 4d. Los dos planazos del cielo: que llene el cuadro.
    #
    # Iban a 1,6x y 1,5x con el campo de vision abierto a 58 y 56 grados, sobre un
    # personaje de 1,15 m contra un cielo vacio: en la sexta grabacion es un punto
    # negro. Un contrapicado funciona por lo que OCUPA el que esta arriba.
    duelo = fase_que_empieza("7")
    final = fase_que_empieza("8")
    for fa, nota, dist, fov_ in ((duelo, "EL PLANAZO. Desde el suelo", 1.0, 38.0),
                                 (final, "EL PLANAZO otra vez", 0.95, 36.0)):
        try:
            b = fa.beats[indice(fa, nota)]
        except Exception:
            continue
        b.set_framing("distanceScale", f(dist))
        b.set_framing("fovOverride", f(fov_))

    # 4e. Fuera "Todavia estan cruzando": ya han cruzado.
    #
    # Raul, sobre la octava grabacion. La frase se escribio cuando la evacuacion
    # todavia estaba en marcha en ese punto del montaje; despues de reordenar, para
    # cuando el la dice el puente ya esta vacio, asi que suena a que no se ha
    # enterado de lo que acaba de pasar.
    #
    # Se va tambien el FaceBeat de delante: existia SOLO para prepararla ("y no mira
    # al Mago Oscuro - mira al puente"). Sin la frase, girarse a mirar el puente en
    # mitad del duelo no significa nada.
    fuera = None
    for i, bt in enumerate(duelo.beats):
        if bt.tipo == "SayBeat" and "PROLOGO_DUELO_ARCHIMAGO_2" in (bt.get("textKey") or ""):
            fuera = i
            break
    if fuera is None:
        raise SystemExit("no encuentro PROLOGO_DUELO_ARCHIMAGO_2 en el duelo")

    quitar = [fuera]
    if fuera > 0 and duelo.beats[fuera - 1].tipo == "FaceBeat" \
            and "M_Puente_01" in (duelo.beats[fuera - 1].get("markName") or ""):
        quitar.append(fuera - 1)
    for i in sorted(quitar, reverse=True):
        del duelo.beats[i]

    # 4f. En el duelo no se gesticula: se aguanta la guardia.
    #
    # "el archimago hace la animacion 3 veces". SayBeat relanza un gesto cada 1,6 s
    # mientras el bocadillo esta en pantalla (TalkRetriggerInterval), para que el
    # personaje no se quede parado a mitad de la frase. En una charla esta bien; en
    # un duelo, una frase de 3,6 s son TRES gestos de hablar seguidos, y ademas de
    # repetirse quedan fuera de tono: esta amenazando a alguien, no charlando.
    #
    # Se apagan los gestos en las cuatro frases del duelo y del final, y a cambio los
    # dos sostienen su guardia de combate: quietos pero en pose, que es lo que pide
    # la escena. La pose se mantiene sola (PoseBeat) hasta que otro gesto la pise.
    for fa, clave, quien, pose in (
            (duelo, "PROLOGO_DUELO_MAGO",      OSCURO,    "Challenging_NoWeapon"),
            (duelo, "PROLOGO_DUELO_MAGO_2",    OSCURO,    "Challenging_NoWeapon"),
            (duelo, "PROLOGO_DUELO_ARCHIMAGO", ARCHIMAGO, "Idle_Battle_NoWeapon"),
            (final, "PROLOGO_HECHIZO",         ARCHIMAGO, None)):
        i = None
        for k, bt in enumerate(fa.beats):
            if bt.tipo == "SayBeat" and clave in (bt.get("textKey") or ""):
                i = k
                break
        if i is None:
            raise SystemExit(f"no encuentro la frase {clave}")

        fa.beats[i].set("playGestures", b_(False))

        # La del final NO lleva pose nueva: se dice en el aire, con la pose de salto
        # ya sostenida por el SaltoBeat. Meterle otra aqui la pisaria.
        if pose:
            fa.beats.insert(i, mantener(
                quien, pose,
                note="Aguanta la guardia mientras habla. Sin esto, SayBeat le mete un gesto de "
                     "charla cada 1,6 s -- tres en una frase de 3,6 s, que es lo que se veia "
                     "como 'hace la animacion tres veces'."))

    # 4g. Liora no se va paseando: se va corriendo.
    #
    # "liora se va tranquila cuando debe irse super corriendo". Iba a 1,5 m/s -- un
    # paseo -- mientras el pueblo entero huye. Y los cuatro vecinos de la segunda
    # tanda iban aun mas despacio (0,85 a 1,1), que con el valle ardiendo detras no
    # se lee como miedo sino como distraccion.
    orden = fase_que_empieza("5")

    def correr(beats):
        # Recursivo A PROPOSITO: las caminatas de la huida viven dentro de un a_la_vez,
        # asi que recorrer solo el primer nivel no encuentra ninguna.
        tocados = 0
        for bt in beats:
            if bt.hijos:
                tocados += correr(bt.hijos)
            if bt.tipo != "WalkPathBeat":
                continue
            if "M_Puente_Ent" not in (bt.get("markNames") or ""):
                continue
            v = float((bt.get("speed") or "0").rstrip("f"))
            actor = (bt.get("actorId") or "").strip('"')
            if actor == "NPC_Liora":
                bt.set("speed", f(4.2))
                bt.set("note", s("Se va CORRIENDO. Es la ultima vez que se ven y lo sabe."))
            elif v < 2.4:
                # Los rezagados siguen siendo mas lentos que los primeros, pero corriendo.
                bt.set("speed", f(round(2.6 + (v - 0.85) * 1.1, 2)))
            tocados += 1
        return tocados

    if correr(orden.beats) == 0:
        raise SystemExit("no he encontrado ninguna caminata de huida hacia el puente")

    # 4h. La apertura no se queda parada.
    #
    # "al principio el efecto de camara esta chulo pero nada mas que enfoquemos al
    # archimago que empiece a hablar, que se queda ahi unos segundos parada la cosa".
    # Eran 12,9 s hasta la primera frase: 4,5 de vista de pajaro + 4,0 parada + 3,2 de
    # descenso + 1,2 parada. El movimiento se queda tal cual -- es lo que le gusta --
    # y se recortan las dos esperas.
    i = indice(manana, "Dejar respirar la plaza antes de que nadie hable")
    manana.beats[i].set("seconds", f(1.0))
    i = indice(manana, "Un segundo mirandolo antes de hablar")
    manana.beats[i].set("seconds", f(0.15))
    manana.beats[i].set("note", s("Lo justo para que la camara asiente. Habla YA."))

    # 4i. Fuera el plano de la montana gris.
    #
    # "hay un momento donde enfocamos a la montana y se ve gris, eso lo quitamos". El
    # plano decia "la plaza entera con la gente y la montana al fondo", pero encuadrado
    # sobre un aldeano a esa distancia la montana se come el cuadro y la gente
    # desaparece. Lo que importa aqui es la REACCION, asi que se encuadra a la gente.
    cielo = fase_que_empieza("3")
    b = cielo.beats[indice(cielo, "La plaza entera, con la gente y la montana al fondo")]
    b.set_framing("secondaryId", s("NPC_Aldeano_05"))
    b.set_framing("heightBias", f(1.2))
    b.set_framing("distanceScale", f(0.85))
    b.set("note", s("La gente de la plaza, no la montana. Antes esto se iba tan atras que el "
                    "cuadro era una pared de roca gris y los vecinos no se veian."))

    # 4j. El rayo: que suene y parpadee, nada mas.
    #
    # "el rayo lo quitamos tambien, que suene nada mas y parpadee la luz". El fogonazo
    # era blanco puro a pantalla completa durante un cuarto de segundo, que se lee como
    # EL rayo. Dos parpadeos cortos y flojos se leen como la luz de una tormenta lejana.
    i = indice(cielo, "EL RAYO golpea la cresta")
    b = cielo.beats[i]
    b.set("color", "new Color(1f, 1f, 1f, 0.5f)")
    b.set("duration", f(0.1))
    b.set("note", s("Solo el parpadeo de la luz. El rayo no se ve: se oye."))
    cielo.beats.insert(i + 1, fogonazo("Y el segundo parpadeo, mas flojo.",
                                       color="new Color(1f, 1f, 1f, 0.28f)", duracion=0.08))

    # 4k. Del trueno directo a su bajada.
    #
    # "despues de la montana gris hay un plano del mago oscuro que quitamos y el rayo
    # fuera directamente a su bajada". La silueta recortada contra las nubes era un
    # plano de el quieto en la cresta, pequeno: el susto se diluye. Que baje ya.
    llegada2 = fase_que_empieza("4")
    j = indice(llegada2, "la silueta recortada contra las nubes")
    del llegada2.beats[j]

    # 4l. El vecino de la carreta da las gracias con la boca, no con la cabeza.
    #
    # "el npc que le pide con la cabeza asiente pero mejor que diga muchas gracias o
    # algo asi". Tres HeadNod01 seguidos no son un agradecimiento, son un tic.
    # Va justo despues de posar la carreta, que es cuando se agradece algo.
    i = indice(manana, "Y la posa")
    manana.beats.insert(i + 1, decir("NPC_Aldeano_06", "PROLOGO_CARRETA_GRACIAS", "Vecino", 2.2,
                                     note="Lo DICE, no lo asiente. Tres HeadNod01 seguidos no son "
                                          "un agradecimiento, son un tic.",
                                     gest="HeadNod01"))

    # 4m. La camara no se queda mirando la carreta cuando al Archimago le tiran.
    #
    # "hay un momento donde el archimago se sale de camara tras recibir un ataque y
    # la camara se queda enfocando a la carreta". El plano no era VIVO, asi que se
    # resolvia una vez y ahi se quedaba -- y justo despues un MoveToBeat manda al
    # Archimago dos metros hacia atras. Con el plano vivo, la camara le acompana.
    b = duelo.beats[indice(duelo, "Y el suelo se abre BAJO EL")]
    b.set("live", b_(True))
    b.set("note", s("Y el suelo se abre BAJO EL. VIVO: justo despues sale despedido dos metros, y "
                    "con el plano quieto la camara se quedaba encuadrando la carreta."))

    # 4n. El plano de los dos lanzando a la vez, sin casa delante.
    #
    # "la casa tapa parte de la escena a pesar de que parece que esta centrada". Es un
    # general a 1,9x de distancia en una calle estrecha: desde ahi no hay angulo sin
    # fachada. Subiendo la camara se sale por encima de los tejados.
    b = duelo.beats[indice(duelo, "Plano general otra vez")]
    b.set_framing("heightBias", f(4.5))
    b.set_framing("distanceScale", f(1.35))
    b.set("note", s("Plano general del choque, desde ARRIBA. A ras de suelo la calle es estrecha y "
                    "siempre entra media fachada; cuatro metros y medio mas alto se sale por "
                    "encima de los tejados y se ve el choque entero."))

    # 4o. El ultimo hechizo TIENE que pasar algo.
    #
    # "ni siquiera puedes salvarte tu y dispara un hechizo pero el archimago
    # simplemente esta con la cara de sorprendido, no pasa nada, esta raro ese
    # momento". Y es literal: levantaba la mano para el ultimo golpe y acto seguido
    # retrocedia, sin que nada explicara por que no lo tiraba.
    #
    # Lo que faltaba no es un efecto, es un motivo: el Archimago se pone en pie DENTRO
    # de esa luz, y es eso lo que le hace bajar la mano.
    i = indice(duelo, "El retrocede a su sitio mientras el otro se levanta")
    for k, bt in enumerate([
            plano("El Archimago se pone en pie DENTRO de esa luz.",
                  "Medium", ARCHIMAGO, OSCURO, altura=-0.6, distancia=1.15),
            emocion(ARCHIMAGO, 10),
            gesto(ARCHIMAGO, "Idle_Battle_NoWeapon",
                  "Se levanta. No esquiva y no se cubre: se levanta.", hold=0.7),
            esperar(0.5),
            plano("Y el baja la mano.", "Reaction", OSCURO, ARCHIMAGO,
                  altura=-0.5, distancia=1.05),
            emocion(OSCURO, 4),
            gesto(OSCURO, "Question01", "Baja la mano. Esto no se lo esperaba.", hold=0.8),
            esperar(0.6, "Y ese es el momento en que el duelo cambia de manos."),
    ]):
        duelo.beats.insert(i + k, bt)

    # 4p. El parpadeo de camara al avanzar sobre el caido.
    #
    # "cuando el mago oscuro camina hacia el archimago la camara hace otro parpadeo,
    # como un cambio de camara rapido". Eran dos cortes pegados: un plano para el
    # avance, el MoveToBeat, y otro plano inmediatamente despues. Con el primero VIVO
    # la camara le acompana mientras anda y el segundo corte deja de hacer falta tan
    # pronto, asi que se le da aire.
    b = duelo.beats[indice(duelo, "El avanza sobre el caido")]
    b.set("live", b_(True))
    b.set("note", s("El avanza sobre el caido. VIVO: la camara le acompana mientras anda, en vez "
                    "de cortar, moverle y volver a cortar -- que es el parpadeo que se veia."))
    j = indice(duelo, "Los dos - el de pie, el otro en el suelo")
    duelo.beats.insert(j, esperar(0.5, "Aire entre los dos cortes."))

    # 5. Sin pausa antes de la tormenta.
    #
    # "hay una pausa antes de la tormenta y deberia ser nada mas acaben de reirse".
    i = len(rio.beats) - 1
    while i >= 0 and rio.beats[i].tipo != "WaitBeat":
        i -= 1
    if i >= 0:
        rio.beats[i].set("seconds", f(0.35))
        rio.beats[i].set("note", s("Justo lo que dura la risa. El trueno entra encima, no despues: "
                                   "la pausa mataba el corte."))

    return fases


def vida(actores, note, gestos=("Talk01", "Talk02", "Talk03", "HeadNod01", "Laugh01",
                                "Question01", "HandClap01", "InteractWithPeople_NoWeapon",
                                "HeadShake01", "Cheer02")):
    """
    Una ronda de vida ambiental: cada vecino hace algo distinto, a la vez y sin
    bloquear.

    "me falta que los npcs anden cuando no hacen nada, hablen entre ellos". Hablar
    entre ellos es esto -- gestos de conversacion repartidos, no todos lo mismo.
    Andar es mas caro (necesita destinos y NavMesh por vecino) y se queda para
    cuando el resto este bien.
    """
    hijos = [esperar(0.1, "", sinEscalar=False)]
    for i, a in enumerate(actores):
        hijos.append(gesto(a, gestos[i % len(gestos)], repeticiones=2, hold=2.2))
    return a_la_vez(note, hijos, esperarATodos=False)


# ── Y se aplica ───────────────────────────────────────────────────────────────

# El punto de partida es una COPIA del archivo tal y como estaba antes de este pase,
# no el archivo de Assets. Asi montaje.py se puede volver a ejecutar las veces que
# haga falta -- para cambiar una frase, un numero o un plano -- sin que los cambios
# se apliquen dos veces encima de si mismos.
BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "base_construir.cs")


def main():
    cab, fases, pie = p3.parsea(BASE)

    fase_manana(por_nombre(fases, "1 - Un dia cualquiera"))
    fase_globo_ok(por_nombre(fases, "1b - El globo se libera bien"))
    fase_rio(por_nombre(fases, "2 - La plaza"))
    fase_cielo(por_nombre(fases, "3 - Algo cambia en el cielo"))
    fase_llegada(por_nombre(fases, "4 - La llegada"))

    f5 = por_nombre(fases, "5 - La orden de evacuar")
    f6 = por_nombre(fases, "6 - Solo quedan ellos dos")
    fase_orden(f5)
    fases.remove(f6)   # su contenido se ha fundido con el de la 5

    fase_duelo(por_nombre(fases, "7 - El duelo"))

    f8 = por_nombre(fases, "8 - Proteccion Absoluta")
    prompt = next(b for b in f8.beats if b.tipo == "InputPromptBeat")
    fase_final(f8, prompt)

    fase_explosion(por_nombre(fases, "9 - El Sendero"))

    pase_pulido(fases)
    pase_decima(fases)
    pase_undecima(fases)
    pase_prologo11(fases)
    pase_vida_de_la_plaza(fases)
    pase_silencios(fases)

    salida = p3.emite(cab, fases, pie)
    io.open(p3.RUTA_CS, "w", encoding="utf-8").write(salida)

    total = sum(len(fs.beats) for fs in fases)
    print(f"Escrito: {len(fases)} fases, {total} beats.")
    for fs in fases:
        print(f"   {fs.nombre:34s} {len(fs.beats):3d}")




# ── Pase de la decima grabacion, segunda tanda ──────────────────────────────

def _beats_recursivos(fase):
    """Todos los beats de la fase, incluidos los que viven dentro de Parallels."""
    def baja(lista):
        for b in lista:
            yield b
            if b.hijos:
                yield from baja(b.hijos)
    return list(baja(fase.beats))


def _busca(fase, tipo, **campos):
    """Todos los beats de ese tipo (a cualquier profundidad) que casen con los campos."""
    fuera = []
    for b in _beats_recursivos(fase):
        if b.tipo != tipo:
            continue
        if all((b.get(k) or "").find(v) >= 0 for k, v in campos.items()):
            fuera.append(b)
    return fuera


def pase_undecima(fases):
    """
    La decima grabacion, segunda tanda. Un arreglo por sintoma, en la causa.
    """
    manana = fases[0]
    rio = por_nombre(fases, "2 - El rio")
    cielo = por_nombre(fases, "3 - Algo cambia en el cielo")
    llegada = por_nombre(fases, "4 - La llegada")
    orden = por_nombre(fases, "5 - La orden de evacuar")
    duelo = por_nombre(fases, "7 - El duelo")
    final = por_nombre(fases, "8 - El ultimo hechizo")

    # 1. LA CARRETA. «Ya no esta metida en el suelo, pero cuando hace la magia la
    #    pone mal en lugar de bien.»
    #
    #    Los tres movimientos eran deltas: -0,35 al volcarla, +1,5 al levantarla,
    #    -1,5 al posarla. Suman -0,35, asi que aunque el giro cuadrase sobre el papel
    #    la carreta acababa treinta y cinco centimetros por debajo de donde PREPARAR
    #    TODO la habia apoyado. Y el giro NO cuadra en cuanto un beat intermedio no
    #    corre: en la grabacion 10 se la ve derecha a los 39,4 s y volcada a los 41,9,
    #    o sea que el hechizo la tumba en vez de enderezarla.
    #
    #    Con 'desdeDondeEstaba' los tres se cuentan desde la pose que tenia al
    #    empezar la secuencia, no desde la anterior. La posada, con los dos deltas a
    #    cero, la deja EXACTAMENTE donde estaba: derecha y apoyada. Ver PropMoveBeat.
    for b in _busca(manana, "PropMoveBeat", propId="PROP_Carreta"):
        b.set("desdeDondeEstaba", "true")
    sube = [b for b in _busca(manana, "PropMoveBeat", propId="PROP_Carreta")
            if "SE LEVANTA" in b.get("note", "")][0]
    sube.set("deltaRotacion", v3(0, 0, 0))
    sube.set("note", s("SE LEVANTA. Sube metro y medio y se endereza a la vez, en 1,4 s -- "
                       "despacio, que pese. El giro es ABSOLUTO: acaba derecha, no 62 grados "
                       "menos de como estuviera."))
    posa = [b for b in _busca(manana, "PropMoveBeat", propId="PROP_Carreta")
            if "posa" in b.get("note", "")][0]
    posa.set("deltaPosicion", v3(0, 0, 0))
    posa.set("note", s("Y la posa: vuelve EXACTA a como estaba, derecha y apoyada en el "
                       "suelo. No se resta lo que se sumo -- se recuerda donde estaba."))

    # 2. «Se ha enganchado arriba del todo sigue estando mal, enfoca la casa.»
    #
    #    La frase se decia sobre un plano general del globo con el Archimago de
    #    secundario: para meter a los dos en cuadro la camara se iba atras y lo que
    #    llenaba la pantalla eran los tejados de en medio. Ahora la frase se dice
    #    sobre QUIEN HABLA -- el plano del vecino ya existia, solo iba detras -- y el
    #    globo se ve despues, en contrapicado cerrado y sin secundario: desde abajo,
    #    con el campanario y el cielo de fondo, no hay casa que quepa.
    i_vecino = indice(manana, "El vecino senalando hacia arriba")
    i_globo = indice(manana, "El globo enganchado en el campanario")
    frase = next(b for b in manana.beats
                 if b.tipo == "SayBeat" and b.get("textKey") == s("PROLOGO_FAVOR_GLOBO"))
    plano_globo = manana.beats[i_globo]
    manana.beats.remove(frase)
    manana.beats.remove(plano_globo)

    plano_globo.set("note", s("El globo, arriba del campanario, en contrapicado cerrado. Antes era un plano "
                              "GENERAL con el Archimago dentro: para meter a los dos la camara "
                              "se iba atras y lo que llenaba el cuadro eran los tejados de en "
                              "medio. Ahora es un primer plano DEL GLOBO: la camara sube con el, a la altura del campanario, y lo que queda detras es cielo. El secundario no sale -- solo da el angulo."))
    plano_globo.set_framing("type", "ShotType.CloseUp")
    plano_globo.set_framing("subjectId", s("PROP_Globo"))
    plano_globo.set_framing("heightBias", f(-1.4))
    plano_globo.set_framing("distanceScale", f(1.3))
    plano_globo.set("duration", f(1.8))

    i = indice(manana, "El vecino senalando hacia arriba") + 1
    manana.beats[i:i] = [frase, plano_globo]

    # 3. «Cuando el Archimago y Liora caminan al rio van muy lento.»
    for b in _busca(rio, "WalkPathBeat"):
        if b.get("speed") == f(1.3):
            b.set("speed", f(1.9))
            b.set("note", s("Paso normal. A 1,3 m/s bajaban al rio como en una procesion."))

    # 4. LA MUSICA. «En el oscuro quiero una gran explosion de sonido que sea lo que
    #    interrumpe la musica.»
    #
    #    Antes la musica se iba sola con un fundido de segundo y medio en la tormenta,
    #    asi que cuando aparecia la figura ya no habia nada que interrumpir. Ahora la
    #    manana sigue sonando por encima del trueno y lo que la corta en seco es el
    #    golpe de su aparicion.
    corte = next(b for b in cielo.beats if b.tipo == "MusicBeat")
    cielo.beats.remove(corte)

    i = indice(llegada, "Y AHORA aparece")
    llegada.beats[i].set("note", s("Y AHORA aparece, en el sitio al que ya esta mirando todo el "
                                   "pueblo. El orden importa: primero el golpe y el susto, "
                                   "despues la figura."))
    llegada.beats[i + 1:i + 1] = [
        sfx("Prologue_Explosion", "EL GOLPE. Es esto lo que corta la musica -- no un "
            "fundido, un impacto.", actor=OSCURO, volumen=1.0),
        temblor("Y se nota en el mando.", intensidad=0.55, duracion=0.6),
    ]
    reveal = next(b for b in llegada.beats if b.tipo == "MusicBeat")
    reveal.set("fadeOut", f(0.0))
    reveal.set("note", s("Su musica entra encima del golpe, sin fundido: la de la manana no "
                         "se apaga, se la llevan por delante."))

    # 5. «Cuando aparece el mago oscuro hay un plano donde se ve el rayo, ese plano
    #    fuera y el rayo fuera.»  Fuera el VFX del rayo de la cresta.
    rayo = next(b for b in llegada.beats
                if b.tipo == "VfxBeat" and b.get("atActorId") == s("NPC_MagoOscuro")
                and "e824247f4f364400b0475f062217a915" in b.get("vfxPrefab", ""))
    llegada.beats.remove(rayo)

    # 6. «Mientras baja el mago oscuro hay algo raro con las camaras.»
    #
    #    Eran dieciseis segundos de una figura diminuta en la misma ladera marron,
    #    con tres saltos de posicion que no se leen como cortes porque el fondo no
    #    cambia. Se quita la parada intermedia entera (M_Ladera_01 y su plano) y se
    #    acelera el tramo andando: del corte de la cresta al pueblo en la mitad.
    i = indice(llegada, "Corte - ya esta mas abajo")
    del llegada.beats[i:i + 3]          # PlaceAtMark + plano de la ladera + espera
    andando = next(b for b in llegada.beats
                   if b.tipo == "WalkPathBeat" and b.get("actorId") == s(OSCURO))
    andando.set("speed", f(2.4))
    andando.set("note", s("Baja los ultimos metros andando, de frente a la camara. A 1,5 m/s "
                          "eran trece segundos de figura pequena en una ladera marron."))

    # 7. Los dos planos con media cabeza en el cuadro. «'No tengo ni idea, Liora,
    #    llevatelos': aqui se ve la cabeza de Liora ocupando media pantalla.»  «'Vuelve
    #    pronto': se ve media cabeza del Archimago ocupando toda la pantalla.»
    #
    #    La causa esta en ShotComposer (el tres cuartos deja la camara al lado del
    #    secundario, y aqui el pelo es media cabeza); ahi se ha puesto la penalizacion.
    #    Aqui se abren los tres planos de la despedida, que eran los mas cerrados de
    #    todo el prologo, y se quita el escorzo: un plano sobre el hombro con este pelo
    #    es una pantalla de pelo.
    for clave, tipo, dist in (("PROLOGO_DESPEDIDA_ARCHIMAGO_1", "Medium", 1.25),
                              ("PROLOGO_DESPEDIDA_ARCHIMAGO", "Medium", 1.2),
                              ("PROLOGO_DESPEDIDA_LIORA", "Medium", 1.15)):
        j = next(k for k, b in enumerate(orden.beats)
                 if b.tipo == "SayBeat" and b.get("textKey") == s(clave))
        p = next(orden.beats[k] for k in range(j - 1, -1, -1) if orden.beats[k].tipo == "ShotBeat")
        p.set_framing("type", f"ShotType.{tipo}")
        p.set_framing("distanceScale", f(dist))
        p.set("note", s("Plano abierto y sin escorzo: cerrado, lo que llenaba la pantalla "
                        "era el pelo del otro."))

    # 8. «Siguen caminando por el agua.»
    #
    #    Las rutas ya pasaban por el puente, pero despues del puente iban a M_Puente_XX
    #    -- que estan entre x=6024 y x=6034, o sea la mitad de ellas ENCIMA del rio, y
    #    varias hacia atras. Del final del puente se sale directo a M_Lejos_XX, que
    #    estan todas al este de la orilla.
    for fase in (orden,):
        for b in _busca(fase, "WalkPathBeat"):
            marcas = b.get("markNames") or ""
            if "M_Puente_Sal" not in marcas:
                continue
            nuevas = [m for m in re.findall(r'"([^"]+)"', marcas)
                      if not (m.startswith("M_Puente_") and m[9:].isdigit())]
            b.set("markNames", lst(*nuevas))
            b.set("note", s("Del puente, al este y fuera. Los puntos M_Puente_01..10 estan "
                            "sobre el agua: salir a ellos era volver a meterse en el rio."))

    # 9. LA PROTECCION. «Sigue haciendo la animacion 3 veces» y «se queda pillado en
    #    una animacion, creo que es la de proteccion».
    #
    #    Defend_NoWeapon dura 20 fotogramas y viene del pack con loopTime=1: es un
    #    clip CICLICO. Disparado como gesto y sin nada que lo saque, se repite solo
    #    -- dos o tres veces mientras dura el picado, que es exactamente lo que se ve.
    #    Y DefendHit_NoWeapon, con returnToNormalAfter en false, se quedaba dando
    #    vueltas para siempre: eso es lo de "se queda pillado".
    #
    #    El bucle se quita en el propio clip (El Sendero/Animaciones/Arreglar bucles),
    #    que es donde esta la causa; aqui se pone la guardia como POSE sostenida en vez
    #    de como disparo, y el golpe encajado vuelve a la normalidad al acabar.
    for b in [x for f in fases for x in _busca(f, "GestureBeat", gesture="Defend_NoWeapon")]:
        if b.get("gesture") != s("Defend_NoWeapon"):
            continue
        b.tipo = "PoseBeat"
        b.campos = [("note", s("La guardia se SOSTIENE, no se dispara: el clip es ciclico y "
                               "disparado se repetia solo dos o tres veces.")),
                    ("actorId", b.get("actorId")),
                    ("pose", s("Defend_NoWeapon")),
                    ("soltar", "false"),
                    ("volverAIdle", "false")]
    for b in [x for f in fases for x in _busca(f, "GestureBeat", gesture="DefendHit_NoWeapon")]:
        if b.get("gesture") != s("DefendHit_NoWeapon"):
            continue
        b.set("holdSeconds", f(0.5))
        b.set("returnToNormalAfter", "true")
        b.set("note", s("Y al acabar vuelve a la normalidad: sin esto se quedaba encajando "
                        "el golpe en bucle el resto del duelo."))

    # 10. «Ni siquiera puedes salvarte tu: el disparo no me gusta porque es unas
    #     piedras cayendo.»  Era 'Ground AOE explosion'. Su esfera de plasma, que es
    #     la que ya usa en el primer asalto, dice mucho mejor "esto es lo ultimo".
    #     (el mismo prefab se usa mas adelante para la grieta del suelo, y ahi SI pega: son
    #     piedras porque el suelo se rompe. El que se cambia es solo el de la frase.)
    todos = [x for f in fases for x in _beats_recursivos(f)]
    k = next(i for i, x in enumerate(todos)
             if x.tipo == "SayBeat" and x.get("textKey") == s("PROLOGO_DUELO_MAGO_2"))
    b = next(x for x in todos[k:]
             if x.tipo == "VfxBeat" and "3dd50886582244645be87adb42aa8528" in (x.get("vfxPrefab") or ""))
    b.set("vfxPrefab", 'Prefab("5ee28f65ca127db42a9a46da980189d4")')
    b.set("offset", v3(0, 1.5, 0))
    b.set("seguirAlActor", "true")
    b.set("note", s("Su esfera, cargandose en la mano. Antes eran unas piedras cayendo."))

    # 11. «Cuando el archimago salta para volar de pronto aparece un rayo, hay que
    #     quitarlo.»  Era el mismo prefab de rayo, puesto a los pies del Mago Oscuro al
    #     despegar. Polvo, que es lo que levanta alguien que despega.
    b = next(x for f in fases for x in _beats_recursivos(f)
             if x.tipo == "VfxBeat" and "e824247f4f364400b0475f062217a915" in (x.get("vfxPrefab") or ""))
    b.set("vfxPrefab", 'Prefab("41494896fc96c9748b81d3356632794e")')
    b.set("note", s("Polvo a sus pies al despegar. Antes salia un rayo, que no venia de "
                    "ningun sitio."))

    # 12. «Cuando el Archimago corre y se pone de espaldas hay un momento donde se
    #     para; debe correr y hacer la animacion sin pasar por idle.»
    corre = next(b for b in final.beats
                 if b.tipo == "WalkPathBeat" and b.get("actorId") == s(ARCHIMAGO))
    corre.set("encadenarCon", s("JumpStart_InPlace_NoWeapon"))

    # 13. «Lo de Proteccion Absoluta mejor hacerlo en dos partes: mientras corre que
    #     diga PROTECCION, y en el aire ABSOLUTA levantando los brazos.»
    i = _idx(final, corre)
    final.beats.insert(i, a_la_vez("Lo grita corriendo, no parado.", [
        corre,
        decir(ARCHIMAGO, "PROLOGO_HECHIZO_1", "Archimago", 1.2, conGestos=False,
              note="La primera mitad, en carrera."),
    ]))
    final.beats.remove(corre)

    dice = next(b for b in final.beats
                if b.tipo == "SayBeat" and b.get("textKey") == s("PROLOGO_HECHIZO"))
    dice.set("note", s("Y la segunda mitad arriba, con los brazos levantados. Es la ultima "
                       "frase del prologo."))
    j = _idx(final, dice)
    final.beats.insert(j, mantener(ARCHIMAGO, "FoundSomething_NoWeapon",
                                   "Los brazos levantados, y SOSTENIDOS ahi: un gesto suelto "
                                   "acabaria en idle en mitad del aire, y el clip sin bucle se "
                                   "queda en su ultimo fotograma -- con los brazos arriba."))

    # 14. EL PUENTE. «Por lo general esta bastante bien, aunque sobre todo la parte de
    #     cruzar el puente se puede mejorar.»
    #
    #     El puente se nombra tres veces y no se ve ni una. Los dos planos que hay son el
    #     mismo —la plaza vaciandose, encuadrada sobre el Archimago— y el propio solver
    #     avisa de que el segundo «se parece demasiado al anterior». Entre los dos cabe lo
    #     que la escena esta contando: la fila de gente cruzando, con el rio debajo y la
    #     cupula detras. Se rueda desde arriba y encuadrando a LIORA, que va la primera:
    #     asi la camara esta en el puente y no en la plaza.
    i = indice(orden, "La plaza vaciandose hacia el puente, y el quieto en medio") + 2
    orden.beats[i:i] = [
        plano("LA FILA EN EL PUENTE. El puente se nombra tres veces en la fase y no se veia "
              "ni una. Encuadrado sobre Liora, que va la primera, y desde cuatro metros y "
              "medio de alto: asi entran el tablero, el agua y los que todavia estan "
              "cruzando. VIVO, porque van andando.",
              "Wide", LIORA, ALDEANOS[6], altura=4.5, distancia=1.5, encara=False,
              vivo=True, duracion=2.0),
        esperar(2.0, "Que se les vea cruzar de verdad, no salir de cuadro."),
    ]

    return fases


def pase_prologo11(fases):
    """
    La grabacion 11. Cuatro cosas, y solo esas.
    """
    manana = fases[0]
    orden = por_nombre(fases, "5 - La orden de evacuar")

    # 1. «La carreta metida en el suelo de nuevo.» Al volcarla gira 62 grados sobre su
    #    pivot, que esta en la base: media carreta se mete en la tierra (se ve a 0:36). Ningun
    #    numero fijo lo arregla porque depende de como la haya colocado Raul, asi que se MIDE:
    #    `apoyarEnElSuelo` la baja o la sube hasta que su parte mas baja toca el suelo.
    vuelca = [b for b in _busca(manana, "PropMoveBeat", propId="PROP_Carreta")
              if "VOLCADA" in b.get("note", "")][0]
    vuelca.set("deltaPosicion", v3(0, 0, 0))
    vuelca.set("apoyarEnElSuelo", "true")
    vuelca.set("note", s("La carreta empieza VOLCADA, al instante y antes del primer plano. Y "
                         "APOYADA: al girarla sobre su base media carreta se metia en la tierra, "
                         "asi que la altura no se pone a ojo -- se mide contra el suelo."))

    # 2. «El Archimago se le queda de espaldas.» Dos causas:
    #    - En el TwoShot con el vecino no se giraba hacia el: la guarda de ShotBeat saltaba el
    #      giro por debajo de 1,5 m, que es justo la distancia de una conversacion (arreglado en
    #      CameraBeats).
    #    - Lanza el hechizo sin mirar a la carreta: nadie se lo pedia.
    i = next(k for k, b in enumerate(manana.beats)
             if b.tipo == "GestureBeat" and b.get("actorId") == s(ARCHIMAGO)
             and b.get("gesture") == s("MagicLeft"))
    manana.beats.insert(i, mirar(ARCHIMAGO, hacia="PROP_Carreta", giro=0.3,
                                 note="Mira a la carreta ANTES de levantar la mano. Sin esto lanzaba "
                                      "el hechizo de espaldas a la camara, mirando a donde hubiera "
                                      "acabado al llegar."))

    #    - «¡Al puente! ¡Todos, ahora!»: el plano estaba pensado con el puente al fondo, o sea
    #      con la camara DETRAS de el. Se le ve de espaldas gritando. Ahora se encara a los que
    #      se van y la camara le coge de frente.
    grito = next(b for b in orden.beats
                 if b.tipo == "ShotBeat" and "gritando hacia el este" in (b.get("note") or ""))
    grito.set_framing("encara", "true")
    grito.set("note", s("El, gritando a los que se van, DE FRENTE. Antes el plano ponia el puente "
                        "al fondo, que es lo mismo que poner la camara a su espalda."))

    return fases


def pase_vida_de_la_plaza(fases):
    """
    «Los NPCs estan siempre quietos en sus sitios al principio del prologo, esperando algo,
    aunque hagan animaciones. Lo suyo es que haya movimiento.»

    Todo lo que hacia la plaza eran gestos EN EL SITIO. Aqui cuatro vecinos que no tienen papel
    en la manana (04, 09, 10) o que lo tienen luego en el corro (02) echan a andar: uno se une al
    corro, otro va a la mesa, otro al horno, otro cruza la plaza. Paso de paseo, salidas
    escalonadas con `retraso` para que no arranquen a la vez como en un desfile, y todo en un
    Parallel que NO espera: la escena sigue mientras pasean.

    Las rutas van por el lado oeste y norte de la plaza, lejos del camino del Archimago
    (M_Apertura -> M_Carreta), y la carreta se rodea sola (esObstaculo).
    """
    manana = fases[0]

    i = indice(manana, "La plaza se mueve desde el primer segundo") + 1
    manana.beats.insert(i, a_la_vez("La plaza PASEA, no solo gesticula.", [
        andar("NPC_Aldeano_02", ["M_Duelo_Oscuro"], velocidad=1.2, retraso=0.4,
              note="Se une al corro."),
        andar("NPC_Aldeano_09", ["M_Mesa"], velocidad=1.1, retraso=1.3,
              note="A la mesa del desayuno."),
        andar("NPC_Aldeano_04", ["M_Duelo_Mago", "M_Duelo_Choque"], velocidad=1.25, retraso=2.2,
              note="Cruza la plaza hacia el oeste."),
        andar("NPC_Aldeano_10", ["M_Horno"], velocidad=1.1, retraso=3.4,
              note="Al horno."),
    ], esperarATodos=False))

    # Y a media manana, los dos que se habian ido vuelven: mientras el Archimago sube al
    # campanario la plaza sigue moviendose en vez de quedarse congelada donde la dejamos.
    i = indice(manana, "Los de la plaza, mientras tanto") + 1
    manana.beats.insert(i, a_la_vez("Y los que se fueron, vuelven.", [
        andar("NPC_Aldeano_04", ["M_Aldeano_04"], velocidad=1.2, retraso=0.5),
        andar("NPC_Aldeano_10", ["M_Aldeano_10"], velocidad=1.1, retraso=1.8),
    ], esperarATodos=False))

    return fases


def pase_silencios(fases):
    """
    «Cuando suena la tormenta prefiero que la musica haga fade y se quede en silencio: asi
    generamos tension, y cuando aparece el Mago Oscuro metemos el tema. Lo mismo antes del duelo:
    fade, y la frase de "¿Vas a salvarlos a todos, mago?" sin musica.»

    Dos silencios, y cada tema entra DESPUES de su silencio. El golpe de la aparicion
    (Prologue_Explosion) se queda: ahora no interrumpe la musica, rompe el silencio.
    """
    cielo = por_nombre(fases, "3 - Algo cambia en el cielo")
    llegada = por_nombre(fases, "4 - La llegada")
    duelo = por_nombre(fases, "7 - El duelo")

    # 1. Con el trueno, la manana se apaga.
    i = next(k for k, b in enumerate(cielo.beats)
             if b.tipo == "SfxBeat" and "Trueno" in (b.get("note") or ""))
    cielo.beats.insert(i, musica("", "La musica de la manana se apaga con el trueno. Desde "
                                 "aqui hasta que aparece el, silencio: es lo que da tension.",
                                 fundidoSalida=2.0))

    reveal = next(b for b in llegada.beats if b.tipo == "MusicBeat")
    reveal.set("note", s("Su tema entra con el, encima del golpe. Viene de silencio: la manana "
                         "se apago con el trueno."))

    # 2. Antes del duelo, silencio otra vez. Su tema se apaga mientras entra en la plaza vacia,
    #    la frase se dice sin musica, y el tema del climax entra DESPUES de la frase.
    climax = next(b for b in duelo.beats if b.tipo == "MusicBeat")
    duelo.beats.remove(climax)
    duelo.beats.insert(0, musica("", "Silencio antes del duelo: su tema se apaga mientras entra "
                                 "en la plaza vacia.", fundidoSalida=2.0))
    j = next(k for k, b in enumerate(duelo.beats)
             if b.tipo == "SayBeat" and b.get("textKey") == s("PROLOGO_DUELO_MAGO")) + 1
    climax.set("note", s("El climax entra DESPUES de «¿Vas a salvarlos a todos, mago?», que se "
                         "dice en silencio."))
    duelo.beats.insert(j, climax)

    # 3. «Cuando Liora y el Archimago, tras el rayo, caminan por el camino para ver que ha
    #    pasado, la camara se vuelve loca.» Plano vivo a 0,6 m por encima de ellos, por un camino
    #    entre casas: el buscador cambiaba de angulo en cuanto una fachada se metia en medio
    #    (nueve veces en la consola de la grabacion 11) y cada cambio era un salto. El plano vivo
    #    ya se suaviza en SequencePlayer; ademas este se sube a cinco metros, como el picado de
    #    la bajada al rio, donde las casas no se interponen.
    vuelta = next(b for b in cielo.beats
                  if b.tipo == "ShotBeat" and "echan a correr de vuelta" in (b.get("note") or ""))
    vuelta.set_framing("heightBias", f(5.0))
    vuelta.set_framing("distanceScale", f(1.4))
    vuelta.set("note", s("Los dos vuelven corriendo al pueblo, EN PICADO desde cinco metros, como "
                         "la bajada al rio. A ras de suelo, entre casas, la camara saltaba de "
                         "angulo cada vez que una fachada se metia en medio."))
    return fases


def _idx(fase, beat):
    return fase.beats.index(beat)


if __name__ == "__main__":
    main()
