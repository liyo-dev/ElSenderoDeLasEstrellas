"""Dobla las frases de Chuchelandia (CHUCHELANDIA_*, la prueba de Estela, INC-889).

Voces (docs/doblaje-sendero/casting y registro_doblaje.csv):
    Will, Estela, Liam ... las de siempre (redoblar_final.py).
    Heraldo ............. voz de juguete de cuerda, aguda y solemne (casting «Heraldo»: Hopsi).
    Heraldo suplente .... la misma voz que el heraldo, más nerviosa (otros ajustes): son idénticos.
    Condesa Merengue .... pija, remilgada y dramática (casting «Condesa Merengue»: voz diseñada D01, muy pija).
    Gominolas ........... agudas y alegres (casting «Gominolas»: Miffy Mouse).
    Su Alteza Pompón .... no habla: sus chapoteos son efectos de sonido.
    Jueces .............. no hablan: sus cartelitos son texto (DUELO_CARTEL_*), igual que la pancarta
                          (TEQUIERO_PANCARTA*, escrita en el mantel de la cinemática).
    Sin voz (solo texto): pistas y avisos (PUERTA_PISTA, TEQUIERO_PISTA_*, TEQUIERO_SOLO_WILL) y los
    títulos de las rondas (TITULO_*).

Uso (desde la raíz del proyecto):

    python Tools/Audio/doblar_chuchelandia.py                 # genera, normaliza y coloca todas
    python Tools/Audio/doblar_chuchelandia.py --tomas 3       # tres tomas de cada una
    python Tools/Audio/doblar_chuchelandia.py --solo CHUCHELANDIA_POMPON_18
    python Tools/Audio/doblar_chuchelandia.py --faltan        # solo las que aún no están colocadas

La toma 1 se coloca en Assets/Resources/Voices/es; todas las tomas quedan en
«Versiones antiguas/Voces Chuchelandia raw (2026-10-31)/» para escucharlas y cambiarlas a mano.
Al terminar, añadir las tomas elegidas a docs/doblaje-cap1/registro_doblaje.csv y volver a hornear
las cinemáticas (El Sendero ▸ Escenario ▸ Chuchelandia: hornear las cinemáticas): el horneado toma la
duración real de cada voz.
"""
import argparse
import os
import shutil
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from redoblar_final import (AJUSTES, AJUSTES_LIAM, AJUSTES_WILL, NORMALIZADOR, RAIZ, VOCES,  # noqa: E402
                            VOZ_ESTELA, VOZ_LIAM, VOZ_WILL, clave_de_api, generar)

TOMAS = os.path.join(RAIZ, "Versiones antiguas", "Voces Chuchelandia raw (2026-10-31)")

# Elegidas de oído por Raúl en el casting (docs/doblaje-sendero/casting/<rol>/).
VOZ_HERALDO = "ygoBNrnmTEdu5NtDTmAY"   # Hopsi - Crazy & Funny Bunny (elegida por Raúl)
VOZ_CONDESA = "0YarjsPinmwESyWzj8SZ"   # Condesa Merengue - pija D01 (voz diseñada, elegida por Raúl)
VOZ_GOMINOLA = "M5t0724ORuAGCh3p3DUR"  # Miffy Mouse - Squeaky, Upbeat Cartoon (elegida por Raúl)

AJUSTES_HERALDO = {"stability": 0.45, "similarity_boost": 0.80, "style": 0.55, "use_speaker_boost": True}
AJUSTES_SUPLENTE = {"stability": 0.30, "similarity_boost": 0.80, "style": 0.65, "use_speaker_boost": True}
AJUSTES_CONDESA = {"stability": 0.55, "similarity_boost": 0.85, "style": 0.45, "use_speaker_boost": True}
AJUSTES_GOMINOLA = {"stability": 0.35, "similarity_boost": 0.75, "style": 0.60, "use_speaker_boost": True}

LINEAS = [
    # ── 1 · Llegada ──
    {"clave": "CHUCHELANDIA_LLEGADA_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[dismayed] [flat] Oh, no."},
    {"clave": "CHUCHELANDIA_LLEGADA_02", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[horrified] [firm] No. De verdad que no."},
    {"clave": "CHUCHELANDIA_LLEGADA_03", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[delighted] [dreamy] A mí me gusta."},
    {"clave": "CHUCHELANDIA_LLEGADA_04", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[deadpan] [annoyed] A ti te gusta todo lo que se pueda comer."},
    # ── 2 · El pergamino ──
    {"clave": "CHUCHELANDIA_PERGAMINO_01", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[wind-up toy voice] [solemn] [squeaky] ¡Bienvenidos a Chuchelandia!"},
    {"clave": "CHUCHELANDIA_PERGAMINO_02", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[solemn] [reading a scroll] [squeaky] Por orden real, nadie abandonará el reino hasta que se abra la Puerta del Corazón."},
    {"clave": "CHUCHELANDIA_PERGAMINO_03", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[solemn] [proud] [squeaky] Y la Puerta del Corazón solo se abre ante la princesa más adorable del reino."},
    {"clave": "CHUCHELANDIA_PERGAMINO_04", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[threatening] [flat] Ni lo penséis."},
    {"clave": "CHUCHELANDIA_PERGAMINO_05", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[innocent] [too quickly] Yo no he dicho nada."},
    {"clave": "CHUCHELANDIA_PERGAMINO_06", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[dry] [annoyed] Lo has pensado muy alto."},
    # ── 2 · Los intentos con el candado (jugable) ──
    {"clave": "CHUCHELANDIA_PUERTA_LIAM_INTERESANTE", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[intrigued] [dry] Interesante."},
    {"clave": "CHUCHELANDIA_PUERTA_ESTELA_NO_INTERESANTE", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[annoyed] [through gritted teeth] No es interesante."},
    {"clave": "CHUCHELANDIA_PUERTA_ESTELA_TOCAR", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[disgusted] [arms crossed] Ni pienso tocarlo."},
    {"clave": "CHUCHELANDIA_PUERTA_LIAM_PIRULETA_01", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[puzzled] [dry] Una piruleta."},
    {"clave": "CHUCHELANDIA_PUERTA_LIAM_PIRULETA_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[pleased] [deadpan] De fresa. Me la quedo."},
    {"clave": "CHUCHELANDIA_PUERTA_WILL_RISITA", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[offended] [confused] ¿Se está riendo de mí?"},
    {"clave": "CHUCHELANDIA_PUERTA_LIAM_RISITA", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[dry] [deadpan] Con motivo."},
    # ── 3 · ¡Una candidata! ──
    {"clave": "CHUCHELANDIA_CANDIDATA_01", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[sympathetic] [squeaky] Ni los caballeros ni los pajes pueden abrir la Puerta del Corazón."},
    {"clave": "CHUCHELANDIA_CANDIDATA_02", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[sympathetic] [softly] [squeaky] Solo una princesa."},
    {"clave": "CHUCHELANDIA_CANDIDATA_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[firm] [crossing arms] Aquí no hay ninguna princesa."},
    {"clave": "CHUCHELANDIA_CANDIDATA_04", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[excited crowd] [cheering] [high-pitched] ¡Una candidata! ¡Tenemos una candidata!"},
    {"clave": "CHUCHELANDIA_CANDIDATA_09", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[screaming] [kicking] [being lifted and carried away] ¡Noooooo! ¡Dejadmeeee!"},
    {"clave": "CHUCHELANDIA_CANDIDATA_05", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[shouts] [furious] [being carried away] ¡Idiota! ¡Haz algo!"},
    {"clave": "CHUCHELANDIA_CANDIDATA_06", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[warning] [mortified] [low] No digas nada."},
    {"clave": "CHUCHELANDIA_CANDIDATA_07", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[holding back a laugh] [sincere] El corazoncito te favorece."},
    {"clave": "CHUCHELANDIA_CANDIDATA_08", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[cold] [deadpan] He dicho que no digas nada, orejitas."},
    # ── 3 · El vestidor ──
    {"clave": "CHUCHELANDIA_VESTIDOR_01", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [bossy] [muffled, behind a door] ¡Levante los brazos, alteza!"},
    {"clave": "CHUCHELANDIA_VESTIDOR_02", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[shouts] [muffled, behind a door] ¡No soy alteza!"},
    {"clave": "CHUCHELANDIA_VESTIDOR_03", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [excited] [muffled] ¡Más volantes!"},
    {"clave": "CHUCHELANDIA_VESTIDOR_04", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[outraged] [muffled] ¿¡Más!?"},
    {"clave": "CHUCHELANDIA_VESTIDOR_05", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [triumphant] [muffled] ¡El lazo! ¡Traed el lazo grande!"},
    {"clave": "CHUCHELANDIA_VESTIDOR_06", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[threatening] [muffled] [trailing off] Como me pongáis ese lazo…"},
    {"clave": "CHUCHELANDIA_VESTIDOR_07", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[deadpan] [quiet] Le han puesto el lazo."},
    {"clave": "CHUCHELANDIA_VESTIDOR_08", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[completely serious] [deadpan] Estás muy rosa."},
    {"clave": "CHUCHELANDIA_VESTIDOR_09", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[seething] [quiet threat] Una palabra más y os prendo fuego."},
    {"clave": "CHUCHELANDIA_VESTIDOR_10", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[deflated] [grumbling] Bueno. Os lleno de purpurina."},
    {"clave": "CHUCHELANDIA_VESTIDOR_11", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[pause] [with great rage] Pero con mucha rabia."},
    # ── 7 · Su Alteza Pompón ──
    {"clave": "CHUCHELANDIA_POMPON_01", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[proud] [booming] [squeaky] ¡Su Alteza Pompón! ¡Princesa de Chuchelandia y campeona de ternura durante cuatrocientos años seguidos!"},
    {"clave": "CHUCHELANDIA_POMPON_02", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[confused] [flat] ¿Eso es un oso?"},
    {"clave": "CHUCHELANDIA_POMPON_03", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[offended] [squeaky] Es la princesa."},
    {"clave": "CHUCHELANDIA_POMPON_04", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[flat] [certain] Es un oso."},
    {"clave": "CHUCHELANDIA_POMPON_05", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[tense] [squeaky] Es la princesa oso."},
    {"clave": "CHUCHELANDIA_POMPON_06", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[flat] [unimpressed] Es un oso con una tiara."},
    {"clave": "CHUCHELANDIA_POMPON_07", "voz": VOZ_HERALDO, "ajustes": AJUSTES_HERALDO,
     "texto": "[through gritted teeth] [slowly] [squeaky] Es. La. Princesa."},
    {"clave": "CHUCHELANDIA_POMPON_08", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[shrugs] [flat] Pues tiene cara de oso."},
    {"clave": "CHUCHELANDIA_POMPON_09", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[curious] [innocent] ¿Le pasa algo?"},
    {"clave": "CHUCHELANDIA_POMPON_10", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [polite] [tight smile] No le pasa nada, alteza."},
    {"clave": "CHUCHELANDIA_POMPON_11", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [pointed] [pause] Es solo que usted es una candidata… complicada."},
    {"clave": "CHUCHELANDIA_POMPON_12", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[stubborn] [flat] Yo solo digo que es un oso."},
    {"clave": "CHUCHELANDIA_POMPON_13", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[matter-of-fact] [afterthought] Y bastante pegajoso."},
    {"clave": "CHUCHELANDIA_POMPON_14", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[defensive] [surprised] ¿Qué? Yo no he hecho nada."},
    {"clave": "CHUCHELANDIA_POMPON_15", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [reproachful] [brushing off confetti] Le ha llamado oso a Su Alteza. Cinco veces."},
    {"clave": "CHUCHELANDIA_POMPON_16", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[checking notes] [dry] Cinco."},
    {"clave": "CHUCHELANDIA_POMPON_17", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[exasperated] [shouts] ¡Es que es un oso!"},
    {"clave": "CHUCHELANDIA_POMPON_18", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[deadpan] [instantly] Seis."},
    {"clave": "CHUCHELANDIA_POMPON_19", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [weary] [calling out] ¡Que pase el suplente!"},
    {"clave": "CHUCHELANDIA_POMPON_20", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[resigned] [sweeping] [high-pitched] Ya van tres esta semana."},
    {"clave": "CHUCHELANDIA_POMPON_21", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[whispers] [nervous] [squeaky] ¿Es ella?"},
    {"clave": "CHUCHELANDIA_POMPON_22", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[whispers] [grave] Es ella."},
    {"clave": "CHUCHELANDIA_POMPON_23", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[nervous] [proud] [squeaky] ¡Su Alteza Pompón! ¡Princesa de Chuchelandia!"},
    {"clave": "CHUCHELANDIA_POMPON_24", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[muffled] [mouth covered] ¡Mmmf!"},
    {"clave": "CHUCHELANDIA_POMPON_25", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [ceremonial] Para abrir la Puerta del Corazón, la candidata debe vencer a la princesa reinante en el Duelo de Ternura."},
    {"clave": "CHUCHELANDIA_POMPON_26", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[suspicious] ¿Y si pierdo?"},
    {"clave": "CHUCHELANDIA_POMPON_27", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [serene] Nadie ha vencido nunca a Su Alteza."},
    {"clave": "CHUCHELANDIA_POMPON_28", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[annoyed] Eso no responde a mi pregunta."},
    {"clave": "CHUCHELANDIA_POMPON_29", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[quietly] [dry] Sí que la responde."},
    # ── 5 · Clases de la Condesa ──
    {"clave": "CHUCHELANDIA_CLASES_REVERENCIA_01", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [precise] Una princesa hace la reverencia así."},
    {"clave": "CHUCHELANDIA_CLASES_REVERENCIA_02", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [patient] Otra vez. Con menos ganas de pelea."},
    {"clave": "CHUCHELANDIA_CLASES_REVERENCIA_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[defensive] [through gritted teeth] No tengo ganas de pelea."},
    {"clave": "CHUCHELANDIA_CLASES_REVERENCIA_04", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [gently pointed] Alteza, está apretando los puños."},
    {"clave": "CHUCHELANDIA_CLASES_SALUDO_01", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [dreamy] El saludo real. La muñeca gira con suavidad, como quien acaricia el aire."},
    {"clave": "CHUCHELANDIA_CLASES_SALUDO_02", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [dry] Eso es espantar moscas, alteza."},
    {"clave": "CHUCHELANDIA_CLASES_RISITA_01", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [tiny crystal giggle] La risita. Jijí."},
    {"clave": "CHUCHELANDIA_CLASES_RISITA_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[writing notes] [dry] [quiet] Risita: peligrosa."},
    {"clave": "CHUCHELANDIA_CLASES_CARINO_01", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [ceremonial] Por último, la princesa dedica unas palabras de cariño a sus súbditos."},
    {"clave": "CHUCHELANDIA_CLASES_CARINO_02", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[hesitant] [long pause] Sois…"},
    {"clave": "CHUCHELANDIA_CLASES_CARINO_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[awkward] [forced sweetness] Blanditos."},
    {"clave": "CHUCHELANDIA_CLASES_BIEN_01", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [reluctant approval] Aceptable."},
    {"clave": "CHUCHELANDIA_CLASES_BIEN_02", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [faint praise] Casi parecía una princesa. Casi."},
    {"clave": "CHUCHELANDIA_CLASES_FIN", "voz": VOZ_CONDESA, "ajustes": AJUSTES_CONDESA,
     "texto": "[prim] [sighs] Queda mucho trabajo por delante."},
    # ── 8 · El Duelo de Ternura ──
    {"clave": "CHUCHELANDIA_DUELO_RONDA1", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[announcing] [squeaky] [facing away] ¡Primera prueba! ¡La reverencia!"},
    {"clave": "CHUCHELANDIA_DUELO_RONDA2", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[announcing] [squeaky] ¡Segunda prueba! ¡La canción de cuna!"},
    {"clave": "CHUCHELANDIA_DUELO_RONDA3", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[announcing] [dramatic] [squeaky] ¡Tercera prueba! ¡La prueba final! ¡Los ojitos!"},
    {"clave": "CHUCHELANDIA_DUELO_RONDA3_CIEN", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[announcing] [very excited] [squeaky] ¡Y en la prueba final, cada corazón vale por cien!"},
    {"clave": "CHUCHELANDIA_DUELO_TURNO_POMPON", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[announcing] [squeaky] ¡Su Alteza Pompón!"},
    {"clave": "CHUCHELANDIA_DUELO_TURNO_CANDIDATA", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[announcing] [nervous] [squeaky] ¡La candidata!"},
    {"clave": "CHUCHELANDIA_DUELO_LIAM_SIN_SENTIDO", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[flat] [unimpressed] Eso no tiene ningún sentido."},
    {"clave": "CHUCHELANDIA_DUELO_FRESA_TRADICION", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[cheerful] [matter-of-fact] [high-pitched] Es la tradición."},
    {"clave": "CHUCHELANDIA_DUELO_WILL_COMO_VAMOS", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[whispers] [hopeful] ¿Cómo vamos?"},
    {"clave": "CHUCHELANDIA_DUELO_LIAM_SESENTA_A_NUEVE", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[whispers] [checking notes] [dry] Sesenta a nueve."},
    {"clave": "CHUCHELANDIA_DUELO_WILL_A_FAVOR", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[whispers] [hopeful] ¿A favor de quién?"},
    {"clave": "CHUCHELANDIA_DUELO_LIAM_PERDEMOS", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[flat] [resigned] Perdemos."},
    # ── 9 · «¡TE QUIERO!» jugado, como Will (bocadillos) ──
    {"clave": "CHUCHELANDIA_TEQUIERO_IDEA_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[determined] [whispers] Aguanta, Estela. Tengo una idea."},
    {"clave": "CHUCHELANDIA_TEQUIERO_IDEA_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[worried] [deadpan] Eso me preocupa."},
    {"clave": "CHUCHELANDIA_TEQUIERO_MANTEL", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[satisfied] [quietly] Esto servirá."},
    {"clave": "CHUCHELANDIA_TEQUIERO_SIROPE", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[concentrated] [proud] Con letras bien grandes."},
    {"clave": "CHUCHELANDIA_TEQUIERO_SILLA", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[resigned] [dry] Sujeto la otra punta."},
    # ── 9 · «¡TE QUIERO!» ──
    {"clave": "CHUCHELANDIA_TEQUIERO_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[shouts] [enthusiastic] [waving] ¡Vamos, Estela!"},
    {"clave": "CHUCHELANDIA_TEQUIERO_07", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[wobbling] [nervous] [bouncing] ¡Uy, uy, uy…!"},
    {"clave": "CHUCHELANDIA_TEQUIERO_08", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[alarmed] [grabbing his arm] ¡Will!"},
    {"clave": "CHUCHELANDIA_TEQUIERO_02", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[shouts] [overjoyed] [high-pitched] ¡Vivan los novios!"},
    {"clave": "CHUCHELANDIA_TEQUIERO_03", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[muffled, under a tablecloth] [deadpan] Esto no es lo que habíamos planeado."},
    {"clave": "CHUCHELANDIA_TEQUIERO_04", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[sheepish] [dripping] No habíamos planeado nada."},
    {"clave": "CHUCHELANDIA_TEQUIERO_05", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[deadpan] Pues ya ves."},
    {"clave": "CHUCHELANDIA_TEQUIERO_06", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[quietly] [counting] [surprised] Ha ganado."},
    # ── 10 · Victoria y salida ──
    {"clave": "CHUCHELANDIA_VICTORIA_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[amused] [warm] Creo que le caes bien."},
    {"clave": "CHUCHELANDIA_VICTORIA_02", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[quick] [embarrassed] Uno."},
    {"clave": "CHUCHELANDIA_VICTORIA_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[stern] [embarrassed] Y no se lo contéis a nadie."},
    {"clave": "CHUCHELANDIA_VICTORIA_04", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[softly] [gruff tenderness] Quédatela. A ti te queda mejor."},
    {"clave": "CHUCHELANDIA_VICTORIA_05", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[solemn] [teasing] [peeling caramel from hair] La hechicera más poderosa y orgullosa del grupo…"},
    {"clave": "CHUCHELANDIA_VICTORIA_06", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[solemn] [teasing] …coronada oficialmente como la princesa más adorable de Chuchelandia."},
    {"clave": "CHUCHELANDIA_VICTORIA_07", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[teasing] [proud] Con récord de corazones."},
    # ── 4 · Carrera (bocadillos) ──
    {"clave": "CHUCHELANDIA_CARRERA_INICIO_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[grumbling] [determined] Corro con un vestido de siete volantes. Que nadie diga nada."},
    {"clave": "CHUCHELANDIA_CARRERA_INICIO_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[dignified] [dry] Un paje no corre. Avanza con dignidad."},
    {"clave": "CHUCHELANDIA_CARRERA_INICIO_03", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[excited] [bouncing] ¡Esta armadura rebota sola!"},
    {"clave": "CHUCHELANDIA_CARRERA_REINTENTO_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[teasing] [laughing] ¡Te ha ganado una gominola!"},
    {"clave": "CHUCHELANDIA_CARRERA_REINTENTO_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[helpful] [dry] Ese lazo pesa, pero planea. Úsalo en los saltos largos."},
    {"clave": "CHUCHELANDIA_CARRERA_REINTENTO_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[determined] [annoyed] Otra vez. Y esta vez, sin piedad."},
    {"clave": "CHUCHELANDIA_CARRERA_GANADA_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[triumphant] [proud] ¡Primera! Con volantes y todo."},
    {"clave": "CHUCHELANDIA_CARRERA_GANADA_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[dry] [amused] La gominola pide la revancha. Yo no se la daría."},
    {"clave": "CHUCHELANDIA_CARRERA_WILL_REBOTA_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[yelling] [flying through the air] ¡Aaah! ¡No era por ahí!"},
    {"clave": "CHUCHELANDIA_CARRERA_WILL_REBOTA_02", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[dazed] [cheerful] ¡Estoy bien! ¡Creo que estoy en otra plataforma!"},
    {"clave": "CHUCHELANDIA_CARRERA_LIAM_DIGNO_01", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[dignified] [slow] Correr con un corazón en la cabeza no es digno. Caminar, sí."},
    # ── 4 · Escalada (bocadillos) ──
    {"clave": "CHUCHELANDIA_TORRE_INICIO_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[skeptical] [dry] ¿Trepar en vestido? Esto no lo enseña la Condesa."},
    {"clave": "CHUCHELANDIA_TORRE_REINTENTO_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[indignant] [shouting up] ¡Ha subido por la escalera! ¡Eso es trampa!"},
    {"clave": "CHUCHELANDIA_TORRE_REINTENTO_02", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[advising] [dry] Menos rabia en las manos y más en las piernas."},
    {"clave": "CHUCHELANDIA_TORRE_GANADA_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[smug] [satisfied] Desde aquí arriba sois todos muy pequeñitos. Me gusta."},
    # ── 4 · Puntería (bocadillos) ──
    {"clave": "CHUCHELANDIA_TIRO_INICIO_01", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[resigned] [determined] Mi fuego sale en corazoncitos. Pues serán corazoncitos con puntería."},
    {"clave": "CHUCHELANDIA_TIRO_REINTENTO_01", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[intrigued] [dry] Interesante."},
    {"clave": "CHUCHELANDIA_TIRO_REINTENTO_02", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[annoyed] [warning] No es interesante. Otra vez."},
    {"clave": "CHUCHELANDIA_TIRO_GANADA_01", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[cheering] [then correcting himself] ¡Todos reventados! Bueno… todos llenos de corazones."},
    # ── 6 · El reino libre ──
    {"clave": "CHUCHELANDIA_REINO_01", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[starstruck] [high-pitched] ¡La candidata! ¿Me firma el lazo?"},
    {"clave": "CHUCHELANDIA_REINO_02", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[admiring] [high-pitched] Su Alteza lleva cuatrocientos años ganando. Y sin despeinarse."},
    {"clave": "CHUCHELANDIA_REINO_03", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[friendly] [warning] ¿Ha probado el río de sirope? No, no se bañe. Se queda pegada."},
    {"clave": "CHUCHELANDIA_REINO_04", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[curious] [honest] Aquí nadie se enfada. Bueno, usted. Usted es la primera."},
    {"clave": "CHUCHELANDIA_REINO_05", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[gossiping] [whispers] Dicen que el heraldo se pone rosa cuando se emociona. Y luego… pop."},
    {"clave": "CHUCHELANDIA_REINO_06", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[admiring] [dreamy] La Condesa Merengue nunca hace ruido al sentarse. Es un don."},
    {"clave": "CHUCHELANDIA_REINO_07", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[serious] [helpful] Cuando suenen todas las trompetillas a la vez, apártese. Viene Su Alteza."},
    {"clave": "CHUCHELANDIA_REINO_08", "voz": VOZ_GOMINOLA, "ajustes": AJUSTES_GOMINOLA,
     "texto": "[giggling] [shy] El caballero de gominola verde rebota muy bien. ¿Está soltero?"},
    # ── 6 · Las trompetillas perdidas (opcional) ──
    {"clave": "CHUCHELANDIA_TROMPETILLAS_ENCARGO_01", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[nervous] [squeaky] Soy el suplente del heraldo. Por si acaso."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_ENCARGO_02", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[worried] [squeaky] El ejército ha perdido sus trompetillas por todo el reino. ¿Me ayuda?"},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_ENCARGO_03", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[dramatic] [squeaky] Sin trompetillas no se puede anunciar a Su Alteza. Y eso sería… un drama."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_EN_CURSO", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[hopeful] [squeaky] ¿Alguna trompetilla? Son doradas, brillan y hacen «tururú»."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_RECOGIDA_WILL", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[pleased] ¡Una trompetilla!"},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_RECOGIDA_LIAM", "voz": VOZ_LIAM, "ajustes": AJUSTES_LIAM,
     "texto": "[disgusted] [dry] Otra. Y está pegajosa."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_RECOGIDA_ESTELA", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[curious] [suspicious] Si la soplo, ¿sale purpurina?"},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_TODAS", "voz": VOZ_WILL, "ajustes": AJUSTES_WILL,
     "texto": "[cheerful] ¡Ya están todas! Vamos a devolverlas."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_ENTREGA_01", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[overjoyed] [squeaky] ¡Todas! ¡Hasta la que tenía chicle!"},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_ENTREGA_02", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[grateful] [careful] [squeaky] Tome, alteza. Para la candidata más… decidida."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_ENTREGA_03", "voz": VOZ_ESTELA, "ajustes": AJUSTES,
     "texto": "[shrugs] [satisfied] ¿Decidida? Me vale."},
    {"clave": "CHUCHELANDIA_TROMPETILLAS_DESPUES", "voz": VOZ_HERALDO, "ajustes": AJUSTES_SUPLENTE,
     "texto": "[happy] [proud] [squeaky] Tururú. ¡Ya suenan todas! Gracias, alteza."},
]


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--tomas", type=int, default=1, help="tomas por frase (la 1 se coloca)")
    p.add_argument("--solo", nargs="*", default=None, help="claves a doblar (por defecto, todas)")
    p.add_argument("--faltan", action="store_true", help="salta las frases que ya están en Voices/es y reutiliza las tomas ya generadas")
    args = p.parse_args()

    os.makedirs(TOMAS, exist_ok=True)
    os.makedirs(VOCES, exist_ok=True)
    clave_api = clave_de_api()

    colocadas = []
    for linea in LINEAS:
        clave = linea["clave"]
        if args.solo and clave not in args.solo:
            continue
        if args.faltan and os.path.exists(os.path.join(VOCES, clave + ".mp3")):
            continue
        print(f"\n{clave}")
        for n in range(1, max(1, args.tomas) + 1):
            toma = os.path.join(TOMAS, f"{clave}_toma{n}.mp3")
            if args.faltan and os.path.exists(toma):
                # Generada en una tanda que se cortó antes de colocar: se reutiliza sin gastar créditos.
                print(f"  toma {n}: ya estaba")
                if n == 1:
                    colocadas.append(clave)
                continue
            audio = generar(clave_api, linea["voz"], linea["texto"], linea["ajustes"])
            if audio is None:
                print(f"  toma {n}: FALLÓ")
                continue
            with open(toma, "wb") as f:
                f.write(audio)
            print(f"  toma {n}: {len(audio) // 1024} KB")
            if n == 1:
                colocadas.append(clave)
            time.sleep(1.0)

    if not colocadas:
        print("\nNo se ha colocado ninguna toma.")
        return

    # Mismo estándar que el resto de voces: -18 LUFS, techo -2 dBTP.
    print("\nNormalizando las tomas (-18 LUFS, -2 dBTP)...")
    entorno = dict(os.environ)
    if "FFMPEG" not in entorno and shutil.which("ffmpeg") and os.name != "nt":
        entorno["FFMPEG"] = shutil.which("ffmpeg")  # fuera de Windows, el ffmpeg del sistema
    subprocess.call([sys.executable, NORMALIZADOR, "--aplicar", "--carpeta",
                     os.path.relpath(TOMAS, RAIZ), "--objetivo", "-18", "--techo", "-2"], cwd=RAIZ, env=entorno)
    for clave in colocadas:
        shutil.copy2(os.path.join(TOMAS, f"{clave}_toma1.mp3"), os.path.join(VOCES, clave + ".mp3"))
    print(f"\nListo: {len(colocadas)} voces colocadas. Vuelve a Unity para que las reimporte.")


if __name__ == "__main__":
    main()
