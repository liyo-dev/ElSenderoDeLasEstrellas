# -*- coding: utf-8 -*-
"""Constructores cortos para escribir beats sin pelearse con el formato del C#."""
from p3 import Beat

def f(x):  return f"{float(x)}f"
def s(x):  return '"' + str(x).replace('"', '\\"') + '"'
def b(x):  return "true" if x else "false"
def v3(x, y, z): return f"new Vector3({float(x)}f, {float(y)}f, {float(z)}f)"
def lst(*nombres): return "new List<string> { " + ", ".join(s(n) for n in nombres) + " }"

def _beat(tipo, note, campos, hijos=None):
    return Beat(tipo, [("note", s(note))] + campos, hijos)

# -- camara ------------------------------------------------------------------

def framing(tipo, sujeto, secundario="", altura=0.0, distancia=1.0, fov=0.0,
            cruzar=False, aire=True, encara=True, ind=12):
    """
    El ShotFraming, con la sangria que espera el emisor para este nivel.

    `encara` (INC-293): girar al sujeto hacia el secundario antes de resolver el
    plano. Por defecto SI, porque declarar un plano de A hablando con B es
    declarar que A mira a B, y el encuadre de tres cuartos esta construido sobre
    esa suposicion. Se pone a False en los planos de ACCION: alguien que huye,
    esquiva, vuela o cae no debe girarse hacia quien le habla.
    """
    p = " " * (ind + 4)
    q = " " * (ind + 8)
    return ("new ShotFraming\n"
            f"{p}{{\n"
            f"{q}type = ShotType.{tipo},\n"
            f"{q}subjectId = {s(sujeto)},\n"
            f"{q}secondaryId = {s(secundario)},\n"
            f"{q}heightBias = {f(altura)},\n"
            f"{q}distanceScale = {f(distancia)},\n"
            f"{q}fovOverride = {f(fov)},\n"
            f"{q}crossTheLine = {b(cruzar)},\n"
            f"{q}headroom = {b(aire)},\n"
            f"{q}encara = {b(encara)},\n"
            f"{p}}}")

def plano(note, tipo, sujeto, secundario="", altura=0.0, distancia=1.0, fov=0.0,
          cruzar=False, aire=True, suave=False, duracion=1.4, esperar=True,
          vivo=False, encara=True, ind=12):
    return _beat("ShotBeat", note, [
        ("shotName", s("")),
        ("smooth", b(suave)),
        ("duration", f(duracion)),
        ("waitForArrival", b(esperar)),
        ("live", b(vivo)),
        ("framing", framing(tipo, sujeto, secundario, altura, distancia, fov, cruzar,
                            aire, encara, ind)),
    ])

def eje(note, grados):
    return _beat("SetActionAxisBeat", note, [("sideDegrees", f(grados))])

# -- actores -----------------------------------------------------------------

def gesto(actor, g, note="", repeticiones=1, hold=0.0, volver=False):
    return _beat("GestureBeat", note, [
        ("actorId", s(actor)), ("gesture", s(g)),
        ("repeats", str(int(repeticiones))), ("holdSeconds", f(hold)),
        ("returnToNormalAfter", b(volver)),
    ])

def emocion(actor, n, note="", revertir=0.0):
    return _beat("EmotionBeat", note, [
        ("actorId", s(actor)), ("emotion", f"(NPCEmotion){n}"), ("revertAfter", f(revertir)),
    ])

def mirar(actor, hacia="", marca="", note="", apartar=False, mutuo=False, giro=0.0):
    return _beat("FaceBeat", note, [
        ("actorId", s(actor)), ("targetActorId", s(hacia)), ("markName", s(marca)),
        ("lookAway", b(apartar)), ("mutual", b(mutuo)), ("turnDuration", f(giro)),
    ])

def colocar(actor, marca, note="", haciaMarca="", haciaActor=""):
    return _beat("PlaceAtMarkBeat", note, [
        ("actorId", s(actor)), ("markName", s(marca)),
        ("faceTowardsMark", s(haciaMarca)), ("faceTowardsActor", s(haciaActor)),
    ])

def ir_a(actor, marca="", hacia="", note="", parar=1.8, angulo=0.0, velocidad=0.0,
         tope=8.0, encararse=True, asentar=0.3):
    return _beat("MoveToBeat", note, [
        ("actorId", s(actor)), ("towardsActorId", s(hacia)), ("markName", s(marca)),
        ("stopDistance", f(parar)), ("approachAngle", f(angulo)),
        ("speedOverride", f(velocidad)), ("timeout", f(tope)),
        ("faceEachOtherOnArrival", b(encararse)), ("settleOnArrival", f(asentar)),
    ])

def andar(actor, marcas, note="", velocidad=1.4, alSuelo=True, alturaExtra=0.0,
          mirarAlAvance=True, animarAndando=True, encadenaCon="", retraso=0.0):
    return _beat("WalkPathBeat", note, [
        ("actorId", s(actor)),
        ("speed", f(velocidad)),
        ("stickToGround", b(alSuelo)),
        ("groundOffset", f(alturaExtra)),
        ("faceTravelDirection", b(mirarAlAvance)),
        ("animarAndando", b(animarAndando)),
        ("encadenarCon", s(encadenaCon)),
        ("retraso", f(retraso)),
        ("markNames", lst(*marcas)),
    ])

# -- dialogo -----------------------------------------------------------------

def decir(actor, clave, nombre, duracion=2.6, note="", gest="", repeticiones=1,
          conGestos=True, offset=None):
    return _beat("SayBeat", note, [
        ("actorId", s(actor)), ("markName", s("")), ("textKey", s(clave)),
        ("pageDuration", f(duracion)), ("gesture", s(gest)),
        ("gestureRepeats", str(int(repeticiones))), ("speakerNameKey", s(nombre)),
        ("playGestures", b(conGestos)),
        ("overrideBubbleOffset", b(offset is not None)),
        ("bubbleOffset", v3(*(offset or (0, 0, 0)))),
    ])

# -- escenario ---------------------------------------------------------------

def esperar(segundos, note="", sinEscalar=True):
    return _beat("WaitBeat", note, [("seconds", f(segundos)), ("unscaled", b(sinEscalar))])

def a_la_vez(note, hijos, esperarATodos=True):
    return Beat("ParallelBeat", [("note", s(note)), ("waitForAll", b(esperarATodos))], hijos)

def mantener(actor, pose, note=""):
    """
    Mantiene una pose hasta que algo la pise. Lo contrario de un gesto.

    Un gesto es un DISPARO: reproduce el clip y el animador manda al personaje a
    idle al acabar. Para saludar esta bien; para volar, sostener un hechizo o
    quedarse en el aire es justo lo contrario, y es de donde salian las dos formas
    del mismo fallo: reinicio del clip desde el fotograma 0 (el tiron) o un idle
    colado entre repeticiones.
    """
    return _beat("PoseBeat", note, [
        ("actorId", s(actor)), ("pose", s(pose)),
        ("soltar", b(False)), ("volverAIdle", b(True)),
    ])


def soltar(actor, note="", aIdle=True):
    """Suelta la pose. `aIdle=False` la deja puesta: para encadenar con otro gesto sin
    que se vea un idle de un fotograma entre los dos."""
    return _beat("PoseBeat", note, [
        ("actorId", s(actor)), ("pose", s("")),
        ("soltar", b(True)), ("volverAIdle", b(aIdle)),
    ])


def saltar(actor, note="", altura=3.5, desplazamiento=0.0, lado=False,
           subida=0.45, sostener=0.0, caida=0.5,
           poseSubida="JumpStart_InPlace_NoWeapon",
           poseAire="JumpAir_InPlace_NoWeapon",
           poseCaida="JumpEnd_InPlace_NoWeapon"):
    """
    Un salto con arco propio: sube desde DONDE ESTA y baja al suelo que tenga debajo.

    Sustituye al montaje de "JumpStart + andar hasta una marca aerea", que saltaba
    hacia la coordenada en vez de hacia arriba y aterrizaba donde dijera el numero
    -- en la sexta grabacion, encima de un tejado.

    `caida=0` lo deja arriba: lo que venga despues decide como baja.
    """
    return _beat("SaltoBeat", note, [
        ("actorId", s(actor)), ("altura", f(altura)),
        ("desplazamiento", f(desplazamiento)), ("haciaElLado", b(lado)),
        ("subida", f(subida)), ("sostener", f(sostener)), ("caida", f(caida)),
        ("poseSubida", s(poseSubida)), ("poseAire", s(poseAire)), ("poseCaida", s(poseCaida)),
    ])


def caer(actor, note="", segundos=0.7, desplazamiento=0.0, lado=False,
         poseAire="JumpAir_InPlace_NoWeapon", poseCaida="JumpEnd_InPlace_NoWeapon"):
    """
    Caerse desde donde se este hasta el suelo. Es `saltar` con altura 0.

    Para alguien al que alcanzan en el aire: no se baja volando, se cae. El suelo
    se busca con un raycast, asi que aterriza donde haya suelo y no donde diga un
    numero.
    """
    return saltar(actor, note, altura=0.0, desplazamiento=desplazamiento, lado=lado,
                  subida=0.0, sostener=0.0, caida=segundos,
                  poseSubida="", poseAire=poseAire, poseCaida=poseCaida)


def vfx(guid, note="", actor="", marca="", offset=(0, 0, 0), vida=0.0, pronto=0.0,
        sigue=False):
    """
    `sigue`: que el efecto ACOMPANE al actor en vez de quedarse donde el estaba.

    Se enciende en lo que va pegado a alguien (un escudo, un aura, el hechizo entre
    las manos) y se deja apagado en lo que ocurre en un SITIO aunque se situe con un
    actor: el polvo del despegue tiene que quedarse en el suelo, no subir con el.
    """
    return _beat("VfxBeat", note, [
        ("vfxPrefab", f'Prefab("{guid}")'), ("atActorId", s(actor)), ("markName", s(marca)),
        ("offset", v3(*offset)), ("lifetime", f(vida)), ("earlyDespawn", f(pronto)),
        ("seguirAlActor", b(sigue)),
    ])

def sfx(evento, note="", actor="", marca="", volumen=1.0):
    return _beat("SfxBeat", note, [
        ("eventKey", s(evento)), ("clip", "null"), ("atActorId", s(actor)),
        ("markName", s(marca)), ("volume", f(volumen)),
    ])

def temblor(note="", intensidad=0.3, duracion=0.4, esperarFin=False):
    return _beat("ShakeBeat", note, [
        ("intensity", f(intensidad)), ("duration", f(duracion)), ("waitForEnd", b(esperarFin)),
    ])

def fogonazo(note="", color="new Color(1f, 0.6f, 0.2f, 1f)", duracion=0.25):
    return _beat("ScreenFlashBeat", note, [("color", color), ("duration", f(duracion))])

def fundido(note="", entrando=True, color="Color.black", duracion=0.25, esperarFin=True):
    return _beat("ScreenFadeBeat", note, [
        ("fadeIn", b(entrando)), ("color", color),
        ("duration", f(duracion)), ("waitForEnd", b(esperarFin)),
    ])

def prop(propId, activo=True, note=""):
    return _beat("SetPropActiveBeat", note, [("propId", s(propId)), ("active", b(activo))])

def mover_prop(propId, note="", dPos=(0, 0, 0), dRot=(0, 0, 0), segundos=1.0,
               suavizar=True, esperarFin=True, desdeSuSitio=False):
    """
    `desdeSuSitio`: los deltas se cuentan desde donde el objeto estaba al EMPEZAR la
    secuencia, no desde donde este ahora. Con los dos deltas a cero, vuelve exacto.

    Se usa para todo lo que tiene que acabar en su sitio. Volcar una carreta sumando
    62 grados y enderezarla restando otros 62 parece lo mismo y no lo es: el estado
    final sale de la suma, y el dia que un beat intermedio no corre la carreta se
    queda tumbada el resto del prologo sin un solo error en consola. Es exactamente
    lo que pasaba en la grabacion 10.
    """
    return _beat("PropMoveBeat", note, [
        ("propId", s(propId)), ("deltaPosicion", v3(*dPos)), ("deltaRotacion", v3(*dRot)),
        ("segundos", f(segundos)), ("suavizar", b(suavizar)), ("esperar", b(esperarFin)),
        ("desdeDondeEstaba", b(desdeSuSitio)),
    ])

def musica(idMusica, note="", fundidoSalida=0.5):
    return _beat("MusicBeat", note, [("musicId", s(idMusica)), ("fadeOut", f(fundidoSalida))])

def hora(cual, note="", inmediato=False, esperarTransicion=False, segundos=2.0):
    return _beat("TimeOfDayBeat", note, [
        ("timeOfDay", f"DayNightCycle.TimeOfDay.{cual}"), ("immediate", b(inmediato)),
        ("waitForTransition", b(esperarTransicion)), ("transitionSeconds", f(segundos)),
    ])

def clima(fenomeno, note="", encender=True, inmediato=False):
    return _beat("WeatherBeat", note, [
        ("fenomeno", f"Fenomeno.{fenomeno}"), ("encender", b(encender)), ("immediate", b(inmediato)),
    ])

def bandera(nombre, valor=True, note=""):
    return _beat("SetFlagBeat", note, [("flag", s(nombre)), ("value", b(valor))])

def escala_tiempo(valor, note="", rampa=0.0):
    return _beat("TimeScaleBeat", note, [("timeScale", f(valor)), ("rampDuration", f(rampa))])


# -- hechizos ----------------------------------------------------------------
#
# Los tres VFX salen del sistema de hechizos DEL JUEGO (Assets/_SPELLS/*.asset), no de
# una eleccion propia: es lo que contesta a "no entiendo por que en las secuencias no
# se ven nunca". Se ven igual porque son los mismos.

VFX_MANO      = "895c6d094b6b213418cddcfb520298e9"   # spawnVFX de todos los hechizos
VFX_BOLA_LUZ  = "eccbc655050af0b4f81d8db39f84a58e"   # el proyectil de Bola de Fuego
VFX_BOLA_OSC  = "232bdd92f4fb5f642bb0d7a40d53380f"   # el de Bola Prisma, en oscuro
VFX_IMPACTO_J = "67a684e320da6e7439421a07e3fa265c"   # impactVFX de todos los hechizos


def hechizo(lanza, note="", objetivo="", marca="", mano=VFX_MANO, proyectil=VFX_BOLA_LUZ,
            impacto=VFX_IMPACTO_J, sfxLanza="", sfxImpacto="", alturaMano=1.15,
            separacion=0.45, velocidad=16.0, carga=0.35, alturaImpacto=1.1,
            sacudida=0.4, esperar=True, registrar=""):
    return _beat("SpellBeat", note, [
        ("lanzaId", s(lanza)),
        ("objetivoId", s(objetivo)),
        ("objetivoMarca", s(marca)),
        ("vfxEnLaMano", f'Prefab("{mano}")' if mano else "null"),
        ("vfxProyectil", f'Prefab("{proyectil}")' if proyectil else "null"),
        ("vfxImpacto", f'Prefab("{impacto}")' if impacto else "null"),
        ("sfxLanzamiento", s(sfxLanza)),
        ("sfxImpacto", s(sfxImpacto)),
        ("alturaDeLaMano", f(alturaMano)),
        ("separacionDelCuerpo", f(separacion)),
        ("velocidad", f(velocidad)),
        ("tiempoDeCarga", f(carga)),
        ("alturaDelImpacto", f(alturaImpacto)),
        ("sacudidaAlImpacto", f(sacudida)),
        ("esperarAlImpacto", b(esperar)),
        ("registrarComo", s(registrar)),
    ])
