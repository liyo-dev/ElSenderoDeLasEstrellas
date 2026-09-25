# -*- coding: utf-8 -*-
"""Saca el guion montado a texto, para mirarlo sin abrir Unity. Uso: python ver.py "8 -" [desde] [hasta]"""
import sys, io, p3, montaje as m

def construido():
    cab, fases, pie = p3.parsea(m.BASE)
    m.fase_manana(m.por_nombre(fases, "1 - Un dia cualquiera"))
    m.fase_globo_ok(m.por_nombre(fases, "1b - El globo se libera bien"))
    m.fase_rio(m.por_nombre(fases, "2 - La plaza"))
    m.fase_cielo(m.por_nombre(fases, "3 - Algo cambia en el cielo"))
    m.fase_llegada(m.por_nombre(fases, "4 - La llegada"))
    f5 = m.por_nombre(fases, "5 - La orden de evacuar")
    f6 = m.por_nombre(fases, "6 - Solo quedan ellos dos")
    m.fase_orden(f5); fases.remove(f6)
    m.fase_duelo(m.por_nombre(fases, "7 - El duelo"))
    f8 = m.por_nombre(fases, "8 - Proteccion Absoluta")
    prompt = next(b for b in f8.beats if b.tipo == "InputPromptBeat")
    m.fase_final(f8, prompt)
    m.fase_explosion(m.por_nombre(fases, "9 - El Sendero"))
    for p in (m.pase_pulido, m.pase_decima, m.pase_undecima, m.pase_prologo11,
              m.pase_vida_de_la_plaza, m.pase_silencios, m.pase_duelo_fluido,
              m.pase_prologo12, m.pase_prologo13, m.pase_cielo, m.pase_prologo14, m.pase_prologo15, m.pase_prologo16, m.pase_prologo17, m.pase_prologo18, m.pase_prologo19, m.pase_prologo20, m.pase_prologo21, m.pase_prologo22, m.pase_prologo23, m.pase_prologo24):
        p(fases)
    return fases

def linea(b, i, nivel=0):
    pad = "   " * nivel
    campos = []
    for k in ("actorId", "shotType", "targetId", "clave", "gesto", "animacion", "segundos",
              "duracion", "live", "hora", "fenomeno"):
        v = b.get(k)
        if v is not None: campos.append(f"{k}={v}")
    nota = m._nota(b)
    print(f"{pad}{i:>4} {b.tipo:<22} {' '.join(campos)[:90]}")
    if nota: print(f"{pad}      · {nota[:150]}")
    for j, h in enumerate(getattr(b, 'hijos', []) or []):
        linea(h, j, nivel + 1)

if __name__ == "__main__":
    pref = sys.argv[1] if len(sys.argv) > 1 else "8 -"
    a = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    z = int(sys.argv[3]) if len(sys.argv) > 3 else 10**6
    for f in construido():
        if f.nombre.startswith(pref):
            print(f"### {f.nombre}  ({len(f.beats)} beats)")
            for i, b in enumerate(f.beats):
                if a <= i <= z: linea(b, i)
