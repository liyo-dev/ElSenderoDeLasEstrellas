#!/usr/bin/env python3
"""Aplica en el guion del prólogo el tema del conjuro (Ultimo Incantazione)."""
import io
import os
import sys

P = r"C:/Users/luarb/dev/unity/ElSenderoDeLasEstrellas/Assets/_SEQUENCES/Guiones/Prologo_UltimaNoche.guion.txt"

REEMPLAZOS = [
    # 1) Bloque del negro: fuera el lecho ritual, entra el tema del conjuro
    ("""# El bus de música se queda a 0 dB: lo que suena ahora es el lecho ritual del cántico.
efecto MezclaBeat fuente=plegaria musicaDb=0 sfxDb=0 ambienteDb=-10 fundido=0.6
# El tema del duelo se corta aquí: el conjuro lo sostiene el lecho del cántico.
silencio 0.8
ambiente CORO eventKey=SFX_Prologo_CoroBucle volumen=0.14
ambiente CORA eventKey=Prologue_CoraCanto volumen=0.75 ritual=si
""",
     """# El bus de música se queda a 0 dB: lo que suena ahora es el tema del conjuro.
efecto MezclaBeat fuente=plegaria musicaDb=0 sfxDb=0 ambienteDb=-10 fundido=0.6
# El tema del duelo se corta aquí y entra Ultimo Incantazione: el cántico se sostiene
# con este tema, que culmina justo en el grito.
silencio 0.8
musica ULTIMO_INCANTAZIONE
"""),
    # 2) Fuera la estrofa (el lecho ritual ya no está)
    ("""# La estrofa entra con la quinta estrofa: el cántico sube.
ambiente ESTROFA eventKey=Prologue_EstrofaCanto volumen=0.75 ritual=si
""", ""),
    ("""# La despedida se dice en voz baja: la estrofa se apaga y solo queda el lecho.
ambiente ESTROFA sonar=false fundido=0.5
""", ""),
    ("""# El coro se apaga con el duelo de miradas: la despedida se queda sola.
ambiente CORO sonar=false fundido=1.5
""", ""),
    ("""# El lecho se apaga aquí: el impacto tiene que entrar en seco.
ambiente CORA sonar=false fundido=0.3
""", ""),
    # 3) En el hechizo la música no se para: el tema culmina en el grito
    ("""# La música se para en seco: empieza el conjuro en silencio.
silencio 0.3
# El Archimago dice su línea de Liora/Nina, y arranca el tema del conjuro.
musica THE_FINAL_BELL
""", ""),
    # 4) Comentarios que hablan del lecho ritual
    ("""# El paso a la música del hechizo: fundido a negro y, al descubrir, el tema del duelo se
# corta y el cántico entra con su lecho ritual; la voz se abre en el eco a tope.
""",
     """# El paso a la música del hechizo: fundido a negro y, al descubrir, el tema del duelo se
# corta y entra Ultimo Incantazione, que va a culminar en el grito; la voz se abre en el eco.
"""),
]

def main():
    with io.open(P, "r", encoding="utf-8", newline="") as f:
        texto = f.read()
    for viejo, nuevo in REEMPLAZOS:
        if viejo not in texto:
            print("NO ENCONTRADO:\n" + viejo[:120] + "\n---")
            continue
        texto = texto.replace(viejo, nuevo)
        print("OK:", viejo.splitlines()[0][:70])
    with io.open(P, "w", encoding="utf-8", newline="") as f:
        f.write(texto)
    print("\nGuion actualizado.")

if __name__ == "__main__":
    main()
