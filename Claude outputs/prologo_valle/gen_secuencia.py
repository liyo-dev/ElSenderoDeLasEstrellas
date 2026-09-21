# -*- coding: utf-8 -*-
"""Reescribe Assets/_SEQUENCES/SEQ_Prologo_UltimaNoche.asset.

Conserva LITERALMENTE los beats que ya estaban bien — las catorce frases con sus claves de
localización, los ocho InputPrompt con sus flags, los gestos, las emociones, la cámara lenta y los
fundidos — y cambia lo que no estaba: dónde ocurre cada cosa y qué se ve.

El diagnóstico era que los 43 beats pasaban con los tres actores clavados en (6000,100,6000): el
horno estaba a seis metros, la carreta a cuatro y el campanario a doce, siempre fuera de cuadro,
porque ShotComposer calcula el plano DESDE el actor y el actor no estaba donde pasaban las cosas.
Aquí cada fase tiene su marca (PlaceAtMarkBeat, detrás de un corte, sin caminar) y los planos
encuadran también los objetos, que ahora son actores de la escena (SequenceStage.Props).
"""
import os, re, sys, shutil, datetime
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unitylib as u

ASSET = os.path.join(u.RAIZ, "Assets/_SEQUENCES/SEQ_Prologo_UltimaNoche.asset")
# Los beats que se conservan se leen SIEMPRE de esta copia del asset tal como estaba antes de
# tocarlo, nunca del asset vivo: si se leyeran del vivo, la segunda ejecución reutilizaría los
# beats ya renumerados de la primera y el montaje saldría barajado.
ORIGEN = os.path.join(os.path.dirname(os.path.abspath(__file__)), "secuencia_original.asset")

# ── lo que ya existe, indexado por los tres últimos dígitos de su rid ─────────
def leer_beats_existentes():
    t = open(ORIGEN, encoding="utf-8").read()
    partes = re.split(r"    - rid: (\d+)\n      type: \{class: (\w+), ns: , asm: ([\w-]+)\}\n      data:\n", t)
    d = {}
    for i in range(1, len(partes), 4):
        rid, cls, asm, cuerpo = partes[i], partes[i+1], partes[i+2], partes[i+3]
        cuerpo = cuerpo.split("    - rid:")[0].rstrip("\n")
        # quitar la cola del fichero en el último beat
        cuerpo = cuerpo.split("\n  version:")[0].rstrip("\n")
        d[rid[-3:]] = (cls, asm, cuerpo)
    return d

VIEJOS = leer_beats_existentes()

def viejo(n, **cambios):
    """Reutiliza un beat existente, opcionalmente cambiando alguno de sus campos."""
    cls, asm, cuerpo = VIEJOS["%03d" % n]
    for campo, valor in cambios.items():
        cuerpo, k = re.subn(r"(?m)^(        %s: ).*$" % re.escape(campo), lambda m: m.group(1) + str(valor), cuerpo)
        assert k == 1, "no encuentro el campo %s en el beat %03d" % (campo, n)
    return (cls, asm, cuerpo)

def nuevo(cls, **campos):
    cuerpo = "\n".join("        %s: %s" % (k, v) for k, v in campos.items())
    return (cls, "Assembly-CSharp", cuerpo)

def plano(tipo, sujeto, secundario="", duracion=1.6, dist=1, alto=0, fov=0, cruza=0, nota="", suave=0, vivo=0):
    TIPOS = {"Wide":0, "TwoShot":1, "OTS":2, "Medium":3, "CloseUp":4, "Reaction":5, "Tracking":6}
    return nuevo("ShotBeat",
        note=nota, shotName="",
        framing="\n          type: %d\n          subjectId: %s\n          secondaryId: %s"
                "\n          heightBias: %s\n          distanceScale: %s\n          fovOverride: %s"
                "\n          crossTheLine: %d\n          headroom: 1" % (
                    TIPOS[tipo], sujeto, secundario, alto, dist, fov, cruza),
        smooth=suave, duration=duracion, waitForArrival=1, live=vivo)

def colocar(actor, marca, mirando="", nota=""):
    return nuevo("PlaceAtMarkBeat", note=nota, actorId=actor, markName=marca,
                 faceTowardsMark="", faceTowardsActor=mirando)

MORNING, NIGHT, SUNSET = 6, 7, 8

# ── el montaje ────────────────────────────────────────────────────────────────
FASES = [
("1 - La mañana", "", "", [
    nuevo("SetActionAxisBeat",
          note="La camara entra por el este. Todo el decorado esta compuesto para ese lado: la iglesia, el pueblo y las lomas quedan al oeste, que es adonde miran catorce de los dieciseis encuadres.",
          sideDegrees=90),
    nuevo("TimeOfDayBeat", note="Amanece. Se lo pide al DayNightCycle y se restaura solo al terminar.",
          timeOfDay=MORNING, immediate=1, waitForTransition=0, transitionSeconds=2),
    colocar("NPC_Archimago", "M_Apertura", nota="Mirando su valle desde la plaza."),
    plano("Wide", "NPC_Archimago", duracion=3.0, dist=3.4, alto=2.2,
          nota="Establecer: el valle entero por la mañana, con el Archimago pequeno en cuadro. Es lo primero que ve el jugador del juego."),
    viejo(2),
    colocar("NPC_Archimago", "M_Horno", "PROP_Horno", nota="Corte a la panaderia."),
    plano("TwoShot", "NPC_Archimago", "PROP_Horno", duracion=1.8,
          nota="El mago y el horno, con la iglesia al fondo (la camara mira a 288 grados y la iglesia esta a 288)."),
    viejo(4), viejo(5),
    plano("OTS", "PROP_Horno", "NPC_Archimago", duracion=1.4,
          nota="Sobre el hombro del mago: se ve prender el horno. Sin esto el favor ocurre fuera de cuadro."),
    viejo(6),
    colocar("NPC_Archimago", "M_Carreta", "PROP_Carreta", nota="Corte a la carreta atascada."),
    plano("Wide", "NPC_Archimago", "PROP_Carreta", duracion=1.7,
          nota="Los dos en cuadro: se ve que la carreta esta volcada y por donde hay que empujarla."),
    viejo(8), viejo(9),
    plano("CloseUp", "NPC_Archimago", "PROP_Carreta", duracion=1.4, nota="El esfuerzo."),
    # El beat 010 (la frase de la carreta) estaba REFERENCIADO por la fase pero no existia en la
    # lista de RefIds del asset: la clave PROLOGO_MANANA_CARRETA no llegaba a decirse nunca y el
    # SequencePlayer se lo saltaba en silencio. Se reconstruye igual que sus hermanos.
    nuevo("SayBeat", note="Reconstruido: faltaba en el asset (referenciado sin RefId).",
          actorId="NPC_Archimago", markName="", textKey="PROLOGO_MANANA_CARRETA",
          pageDuration=2.4, gesture="", gestureRepeats=1, speakerNameKey="Archimago",
          playGestures=1, overrideBubbleOffset=0, bubbleOffset="{x: 0, y: 0, z: 0}"),
    colocar("NPC_Archimago", "M_Globo", "PROP_Globo", nota="Corte al campanario."),
    plano("Wide", "PROP_Globo", "NPC_Archimago", duracion=1.9, dist=1.7, alto=0.8,
          nota="El globo enganchado arriba del campanario. El campanario es la unica silueta vertical del valle."),
    plano("Medium", "NPC_Archimago", "PROP_Globo", duracion=1.4, alto=0.35,
          nota="Contraplano: el mago mirando hacia arriba."),
    viejo(12), viejo(13),
]),
("1b - El globo se libera bien", "manana_globo_ok", "", [
    nuevo("SetPropActiveBeat", note="El globo se va.", propId="PROP_Globo", active=0),
    viejo(14),
]),
("1c - El globo revienta", "", "manana_globo_ok", [
    nuevo("SetPropActiveBeat", note="El globo revienta.", propId="PROP_Globo", active=0),
    viejo(15),
]),
("2 - La carta", "", "", [
    colocar("NPC_Archimago", "M_Mesa", "NPC_Liora", nota="La mesa del desayuno, junto al rio."),
    colocar("NPC_Liora", "M_Mesa_Liora", "NPC_Archimago"),
    viejo(16), viejo(17), viejo(18), viejo(19), viejo(20),
    nuevo("TimeOfDayBeat",
          note="La luz empieza a caer MIENTRAS sostiene la carta que no va a contestar. No se espera a la transicion: cambia por debajo de la escena.",
          timeOfDay=SUNSET, immediate=0, waitForTransition=0, transitionSeconds=2),
]),
("3 - La hora concedida", "", "", [
    nuevo("SetPropActiveBeat", note="El valle empieza a arder: fuegos, humo y sus luces.",
          propId="PROP_Incendio", active=1),
    colocar("NPC_Archimago", "M_Viga", "PROP_Viga", nota="La viga caida sobre la calle."),
    plano("Wide", "NPC_Archimago", "PROP_Viga", duracion=2.2,
          nota="La calle con la viga cruzada y el pueblo ardiendo al fondo."),
    viejo(22), viejo(23),
    plano("CloseUp", "NPC_Archimago", "PROP_Viga", duracion=1.7, alto=-0.3,
          nota="Contrapicado: la camara por debajo de los ojos hace que aguantar parezca costar."),
    viejo(24),
]),
("4 - La despedida", "", "", [
    nuevo("TimeOfDayBeat", note="Ya es de noche. El incendio es la unica luz.",
          timeOfDay=NIGHT, immediate=0, waitForTransition=0, transitionSeconds=2),
    colocar("NPC_Archimago", "M_Despedida_Mago", "NPC_Liora", nota="En el puente, a la salida del valle."),
    colocar("NPC_Liora", "M_Despedida_Liora", "NPC_Archimago"),
    viejo(25), viejo(26), viejo(27),
    plano("Reaction", "NPC_Archimago", "NPC_Liora", duracion=1.7,
          nota="El que escucha. Detras de el, el pueblo ardiendo."),
    viejo(28), viejo(29),
]),
("5 - El duelo", "", "", [
    colocar("NPC_Archimago", "M_Duelo_Mago", "NPC_MagoOscuro", nota="La plaza."),
    colocar("NPC_MagoOscuro", "M_Duelo_Oscuro", "NPC_Archimago"),
    viejo(30), viejo(31), viejo(32), viejo(33),
    viejo(34),
    viejo(35), viejo(36),
    plano("Medium", "NPC_MagoOscuro", "NPC_Archimago", duracion=1.2,
          nota="El Mago Oscuro entiende que el hechizo ha cambiado, un segundo antes de que pase."),
    viejo(37), viejo(38), viejo(39), viejo(40),
]),
("6 - Deja que elija", "", "", [
    plano("CloseUp", "NPC_MagoOscuro", "NPC_Archimago", duracion=1.6, alto=-0.2,
          nota="La ultima cara que se ve antes del negro."),
    viejo(41), viejo(42), viejo(43),
]),
]

def generar():
    cabecera_original = open(ORIGEN, encoding="utf-8").read()
    cabecera = cabecera_original.split("  phases:")[0]
    # el plano de apertura del asset: que coincida con el primer plano del montaje
    cabecera = re.sub(r"(?s)  openingShot:\n.*?\n  endStayBlack:",
"""  openingShot:
    type: 0
    subjectId: NPC_Archimago
    secondaryId: 
    heightBias: 2.2
    distanceScale: 3.4
    fovOverride: 0
    crossTheLine: 0
    headroom: 1
  endStayBlack:""", cabecera)

    rid = 9100000000000000000
    fases_txt, refs_txt = [], []
    for nombre, solo_si, salvo_si, beats in FASES:
        rids = []
        for cls, asm, cuerpo in beats:
            rid += 1
            rids.append(rid)
            refs_txt.append("    - rid: %d\n      type: {class: %s, ns: , asm: %s}\n      data:\n%s\n"
                            % (rid, cls, asm, cuerpo))
        fases_txt.append("  - name: %s\n    onlyIfFlag: %s\n    skipIfFlag: %s\n    beats:\n%s"
                         % (nombre, solo_si, salvo_si,
                            "".join("    - rid: %d\n" % r for r in rids)))
    return cabecera + "  phases:\n" + "".join(fases_txt) + "  references:\n    version: 2\n    RefIds:\n" + "".join(refs_txt)

if __name__ == "__main__":
    texto = generar()
    copia = os.path.join(u.RAIZ, "_ClaudeBackups",
                         "SEQ_Prologo_UltimaNoche_%s.asset" % datetime.datetime.now().strftime("%Y%m%d_%H%M%S"))
    os.makedirs(os.path.dirname(copia), exist_ok=True)
    shutil.copy2(ASSET, copia)
    open(ASSET, "w", encoding="utf-8", newline="\n").write(texto)
    n = texto.count("- rid: ") // 2
    print("Secuencia reescrita: %d beats en %d fases" % (n, texto.count("  - name: ")))
