# -*- coding: utf-8 -*-
"""
ritmo.py -- busca los tramos en los que la camara se queda mirando a la nada.

Lo escribo despues de la decima grabacion, donde hay CUARENTA Y CINCO SEGUNDOS
seguidos de casas: del 1:12 al 1:57 la camara esta parada detras de una fila de
tejados y no se ve a nadie, con los bocadillos saliendo del alero.

La causa no era el solver de camara. Eran dos errores de montaje, y los dos son
el mismo error:

  1. Una FASE ENTERA sin un solo ShotBeat. Hereda el ultimo plano de la fase
     anterior -- que en ese caso encuadraba un globo que ya se habia ido
     veintidos metros hacia arriba.

  2. Un plano QUIETO puesto ANTES de una caminata. Se resuelve donde estan los
     actores en ese momento, ellos andan dieciocho metros, y la camara se queda
     mirando el sitio del que se han ido.

Ninguno de los dos da error, ninguno de los dos lo ve validar.py (los nombres
estan todos bien) y los dos solo se ven grabando. De ahi este archivo.

Las tres reglas:

  A. Toda fase con dialogo tiene al menos un plano propio.
  B. Toda caminata esta cubierta: o el plano vigente es 'live', o hay un plano
     nuevo despues de ella y antes de que nadie hable.
  C. Ningun plano aguanta mas de N segundos de dialogo y esperas seguidas.

No es un validador de correccion: es un validador de ABURRIMIENTO. Por eso avisa
en vez de fallar.
"""

import sys
import p3

MOVIMIENTO = ("WalkPathBeat", "MoveToBeat")
SEGUNDOS_MAXIMOS_POR_PLANO = 14.0


def _seg(beat):
    """Cuanto dura un beat, a ojo, para medir tramos."""
    if beat.tipo == "WaitBeat":
        return float((beat.get("seconds") or "0f")[:-1])
    if beat.tipo == "SayBeat":
        return float((beat.get("pageDuration") or "0f")[:-1])
    if beat.tipo == "GestureBeat":
        return float((beat.get("holdSeconds") or "0f")[:-1]) * int(beat.get("repeats") or 1)
    return 0.0


def revisa(fases):
    avisos = []

    for fase in fases:
        tipos = [b.tipo for b in fase.beats]

        # ── Regla A ──
        if "SayBeat" in tipos and "ShotBeat" not in tipos:
            avisos.append(
                f"{fase.nombre}: la fase TIENE DIALOGO Y NI UN PLANO. Hereda el ultimo de la "
                f"fase anterior, que puede estar encuadrando cualquier cosa. Es el fallo de "
                f"las fases del globo en la decima grabacion.")

        vivo = None          # el ultimo ShotBeat, y si era 'live'
        desde_el_plano = 0.0
        caminata_pendiente = None

        for i, b in enumerate(fase.beats):
            if b.tipo == "ShotBeat":
                vivo = b.get("live") == "true"
                desde_el_plano = 0.0
                caminata_pendiente = None
                continue

            hijos = [b] + list(b.hijos or [])

            for h in hijos:
                if h.tipo in MOVIMIENTO and not vivo:
                    caminata_pendiente = (i, h.tipo, h.get("actorId"))

            # ── Regla B ──
            if b.tipo == "SayBeat" and caminata_pendiente is not None:
                j, tipo, quien = caminata_pendiente
                avisos.append(
                    f"{fase.nombre} #{j}: {tipo} de {quien} sin plano que lo cubra, y en #{i} "
                    f"ya habla alguien. El plano vigente es de ANTES de andar: encuadra el sitio "
                    f"del que se han ido.")
                caminata_pendiente = None

            desde_el_plano += _seg(b)

            # ── Regla C ──
            if desde_el_plano > SEGUNDOS_MAXIMOS_POR_PLANO:
                avisos.append(
                    f"{fase.nombre} #{i}: {desde_el_plano:.0f} s con el mismo plano. Por encima "
                    f"de {SEGUNDOS_MAXIMOS_POR_PLANO:.0f} s sin cortar, una escena se para.")
                desde_el_plano = 0.0

        # Una caminata al final de la fase tampoco esta cubierta.
        if caminata_pendiente is not None:
            j, tipo, quien = caminata_pendiente
            avisos.append(
                f"{fase.nombre} #{j}: {tipo} de {quien} al final de la fase, sin plano detras. "
                f"Lo que se vea depende de por donde ande la fase siguiente.")

    return avisos


def main():
    _, fases, _ = p3.parsea()
    avisos = revisa(fases)

    if not avisos:
        print("OK - ninguna camara se queda mirando a la nada.")
        return 0

    print(f"{len(avisos)} aviso(s) de ritmo:")
    for a in avisos:
        print("  -", a)
    return 0   # avisa, no falla: son decisiones de montaje, no errores


if __name__ == "__main__":
    sys.exit(main())
