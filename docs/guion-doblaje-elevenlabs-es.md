---
title: Guion de diálogos para doblaje con ElevenLabs (ES) — El Sendero de las Estrellas
date: 2026-08-30
---

# Guion de diálogos para voz — El Sendero de las Estrellas

Todo el texto hablado del juego (`Assets/Resources/Localization/dialogues_es.json`, `cinematics_es.json`, `prologue_es.json` y los monólogos de `other_es.json`), organizado por escena, con el personaje asignado a cada línea y etiquetas de interpretación para que la IA lo lea con el tono correcto. **No se ha tocado ningún archivo del proyecto** — esto es solo un documento de producción para grabar voces.

## Cómo usarlo con ElevenLabs

Un aviso importante primero: la **Dubbing Studio** de ElevenLabs (`elevenlabs.io/app/dubbing`) está pensada para redoblar un audio/vídeo ya existente a otro idioma — necesita un archivo fuente. Para generar voces nuevas a partir de texto puro (que es lo que necesitas aquí, porque el juego no tiene audio original) tienes dos caminos dentro de la misma app:

1. **Dentro de un proyecto de Dubbing**, se puede añadir una pista de voz manual ("Voiceover") y escribir el texto directamente en la tarjeta del hablante, sin partir de audio. Es una opción algo escondida pensada para añadir líneas nuevas a un doblaje existente, pero funciona para texto puro.
2. **Más directo para este caso**: usar **Text to Speech / Studio** (mismo panel lateral de la app, no la sección Dubbing) creando un proyecto con varios personajes (voces) y pegando el guion de cada uno en su pista. Es el flujo pensado para justo esto: diálogo multi-personaje sin audio fuente.

Elige la que te vaya mejor en la interfaz — el guion de abajo sirve para cualquiera de las dos, porque ya viene separado por personaje y limpio de marcado de Unity.

### Etiquetas de interpretación

El modelo Eleven v3 lee etiquetas entre corchetes **en inglés** dentro del propio texto (van en inglés porque así es como el modelo las reconoce, aunque el diálogo esté en español). Las que se usan en este guion:

- Emoción: `[sad]` `[angry]` `[happily]` `[sorrowful]` `[awe]` `[booming]`
- Ritmo/entrega: `[whispers]` `[shouts]` `[softly]` `[pause]` `[rushed]` `[slowly]` `[drawn out]`
- Reacciones no verbales: `[laughs]` `[sighs]` `[surprised]` `[annoyed]` `[interrupting]`
- Puntuación con efecto real: los puntos suspensivos `...` fuerzan una pausa natural, las comas ayudan al ritmo de respiración, y ESCRIBIR EN MAYÚSCULA una palabra le da énfasis.

En el guion de abajo la etiqueta va al principio de la línea, entre corchetes, antes del texto — cópiala tal cual en la tarjeta de ElevenLabs junto con el texto.

### Limpieza aplicada

Se ha quitado de las líneas todo lo que no es voz: marcado `<sprite name="...">` (iconos de botón/objeto de la UI), tags `<kbonly>`/`<gpadonly>` (se ha dejado la versión de mando, más natural al oído), y las instrucciones puramente de UI (contadores, prompts de botón) que no las dice ningún personaje — esas se han excluido del guion porque no son diálogo.

### Aviso sobre el reparto de personaje

La mayoría de líneas tienen el hablante claro por el nombre de la clave (`DLG_ESTELA_...`, `DLG_ELDRAN_...`, etc.) o porque el propio texto lo indica (`ESTELA: ...`, `Liam: ...`). Pero bastantes escenas de grupo (la celda, la taberna, el trono del Rey, la huida del bosque de libros) mezclan a varios personajes bajo una clave genérica sin marcar quién dice qué línea — ahí el reparto de abajo es mi lectura del contexto y la personalidad de cada uno (ver ficha de personajes), no un dato del archivo. Los puntos donde dudé más de verdad los marco con **(revisar)**. Antes de grabar en serio, una pasada tuya por esas líneas evita voces mal asignadas.

---

# Ficha de casting de voces

Perfiles de personalidad y voces oficiales aprobadas en ElevenLabs (diseñadas con estilo *seiyuu* de anime en castellano de España, octubre de 2026):

- **Will** (Will - Anime España 1.2 — JwXx2SOucScqglxgpL6T) — Protagonista, chico joven de 18 años. Estilo shōnen clásico (Edward Elric / Tanjiro / Sora): timbre tenor heroico, cálido y carismático en momentos cotidianos, y con garra rotunda al gritar en combate.
- **Estela** (Estela - Lina candidata 1 — 9MKgqtaIyv28bwBeybox) — Maga prodigio de 18 años. Estilo Lina Inverse (*Slayers*, Megumi Hayashibara): brillante, explosiva, descarada, cómica y rápida, pasando de la altanería al enfado histriónico en un instante.
- **Liam** (Liam - Anime candidata 1 — hwazPFcispEO8ANIs7LE) — Rival/compañero del trío, 18-20 años. Estilo anime frío y calculador (Sasuke / Kurapika): sereno, seco, irónico, con voz aterciopelada y trasfondo melancólico por salvar a su hermano Tobías.
- **Eldran** (Eldran - Anime candidata 2 — RL0oxYTUxBdVxuByUCtW) — Mentor y tutor de Will, ~60 años. Estilo sabio anime veterano (Jiraiya / Silvers Rayleigh): voz rasgada y entrañable en sus despistes cotidianos, e imponente y fiera al mandar órdenes en combate.
- **Mago Oscuro** (Mago Oscuro - Anime candidata 3 — uE9bcSSRFJvj5BdWjLpf) — Villano ancestral. Estilo archienemigo teatral (Aizen / Dio Brando / Jafar): voz grave, sedosa, pausada, condescendiente y aterradoramente fría.
- **Archimago** (Archimago - Anime candidata 2 — JOlPriKZ6hldygMIxHSm) — Noble héroe del prólogo y antepasado de Will, 35-45 años. Estilo noble protector (Minato Namikaze / Hohenheim): voz cálida y paternal en calma, con desgarro heroico y devoción en el cántico del sacrificio.
- **Oliver** (Oliver - Anime candidata 1 — GyyGyS0WACM14Hkzduei) — Mejor amigo de Will, 18 años. Estilo compañero cómico anime (Usopp / Zenitsu): hablador rápido, alegre, nervioso y jadeante tras correr, con gran expresividad y lealtad.
- **Victoria** (Victoria - Anime candidata 3 — YdH8EmiKzVcrI4UltjiQ) — Vecina de 30-40 años. Estilo mujer despierta y práctica: irónica, risueña y con retranca constante en sus piques con Eldran.
- **Nina** (Nina - Anime candidata 2 — U6p8vPqRYiiCAw7v94oR) — Niña del globo del campanario. Registro dulce, agudo, adorable y tierno estilo Ghibli.
- **Liora** (Sara Martin - Gentle and Layered — KHCvMklQZZo0O30ERnVn) — Pareja del Archimago. Voz femenina madura, suave, cariñosa y protectora (conservada por Raúl).
- **Tomasa** (Tomasa - Anime candidata 1 — 7F6LSLBol0zaPBiXCvwe) — Tallista del mercado (55-65 años, entrañable, humilde y risueña).
- **Heraldo / heraldo suplente, Condesa Merengue y gominolas de Chuchelandia** — Ver la escena de Chuchelandia (casting en `docs/doblaje-sendero/casting/`).
- **Resto de NPCs** — Voces de reparto peninsulares de resguardo (Julio para hombres maduros, Cristina para mujeres).

---

# PRÓLOGO

**Narrador** — [slowly] [awe]
"Hace eones, un mago desafió a los dioses recorriendo el Sendero de las Estrellas." `PRLG_01`

**Narrador** — [drawn out]
"Pero su anhelo de obtener un poder infinito desató un cataclismo que fragmentó nuestro mundo." `PRLG_02`

**Narrador**
"Para proteger lo que quedaba, los dioses sellaron el Sendero y ocultaron sus oscuros secretos en un libro prohibido." `PRLG_03`

**Narrador** — [softly]
"Siglos después, el mundo había olvidado la leyenda…" `PRLG_04`

**Will** — [surprised] [interrupting]
"Otra vez esa pesadilla." `NIGHTMARE_AGAIN` (other_es.json — línea al despertar, justo antes de la siguiente)

**Eldran** — [shouts]
"Will, ¡DESPIERTA!" `PRLG_WILL_WAKE_UP`

---

# ESCENA: Introducción — despertar y primer combate (cinematics CH1 / EVT)

**Narrador**
"La historia comienza en un pequeño reino." `CH1_01`

**Narrador**
"Aquí vive Will, nuestro héroe." `CH1_02`

**Narrador**
"Su vecino se llama Eldran y lo cuida desde que era pequeño." `CH1_03`

**Narrador** — [pause]
"Sobre la mesa hay una carta... ¿Qué contendrá?" `CH1_04`

**Voz Misteriosa** (antagonista aún sin revelar) — [softly] [drawn out]
"Así que… la estrella finalmente ha despertado." `CIV_01`

**Voz Misteriosa** — [softly]
"El primer paso está completo…" `CIV_02`

**Narrador**
"Will llega con la caja de frutas." `EVT_01`

**Narrador**
"De pronto presiente algo." `EVT_02`

**Narrador** — [rushed]
"Una bola de fuego se dirige hacia ellos." `EVT_03`

**Narrador**
"Will cierra los ojos y desata sin saberlo una poderosa magia." `EVT_04`

**Narrador**
"Y una bola de fuego sale de sus manos, desviando el proyectil por completo." `EVT_05`

**Narrador**
"Will está confundido con lo que acaba de pasar cuando de pronto..." `EVT_06`

**Narrador** — [booming]
"El suelo tiembla y una figura oscura emerge entre humo y fuego: un demonio." `EVT_07`

**Eldran** — [shouts]
"¡Usa tu magia, presiona el botón de ataque y derrota al demonio!" `EVT_08`

**Voz Misteriosa** — [awe]
"¡Qué interesante!" `EVT_09`

**Narrador**
"El ataque del demonio ha despertado tu magia." `EVT_10`

**Eldran** — [shouts]
"¡Will! Úsala contra el demonio, presiona el botón de ataque y derrótale." `EVT_11`

**Eldran** — [shouts] [surprised]
"¡Will, cuidado!" `EVT_AWAKEN_01`

**Will** — [surprised]
"¡...!" `EVT_AWAKEN_02`

**Eldran** — [shouts]
"¡NOO!" `EVT_AWAKEN_03`

**Eldran** — [shocked]
"¡No... no puede ser!" `EVT_AWAKEN_04`

**Will** — [confused]
"¿Qué... qué acabo de hacer?" `EVT_AWAKEN_05`

**Eldran** — [shouts]
"¡Will apartate!" `EVT_AWAKEN_06`

**Eldran** — [happily] [shouts]
"¡Así se hace, Will! ¡Lo tienes!" `EVT_ELDRAN_CHEER_01`

**Eldran** — [shouts]
"¡Vamos, muchacho! ¡Sé que puedes!" `EVT_ELDRAN_CHEER_02`

**Eldran** — [shouts]
"¡No te rindas! ¡Acábalo de una vez!" `EVT_ELDRAN_CHEER_03`

---

# ESCENA: Antipático (NPC secundario)

Voz: seco, desganado, cero paciencia — se ablanda un poco solo al final.

**Antipático**
"¿Qué miras?" `DLG_ANTIPATICO_01`

**Antipático** — [annoyed]
"Estoy ocupado ignorando a la gente." `DLG_ANTIPATICO_02`

**Antipático**
"Si no traes algo útil, no hables." `DLG_ANTIPATICO_03`

**Antipático** — [annoyed]
"...¿Sigues aquí? Impresionante." `DLG_ANTIPATICO_04`

**Antipático**
"Mira hacemos una cosa: si encuentras mis Botas te las puedes quedar." `DLG_ANTIPATICO_05`

**Antipático**
"Estuve nadando y olvidé ponérmelas." `DLG_ANTIPATICO_06`

**Antipático**
"Ven a verme si las encuentras y te contaré un secreto." `DLG_ANTIPATICO_07`

**Antipático**
"Busca las Botas cerca del mar. Estuve nadando por ahí." `DLG_ANTIPATICO_IN_PROGRESS`

**Antipático**
"Ah pues mira, las has encontrado." `DLG_ANTIPATICO_TURNIN_01`

**Antipático**
"¿Sabes qué? Pensaba que no lo conseguirías. Ahí va el secreto:" `DLG_ANTIPATICO_TURNIN_02`

**Antipático**
"Con ellas podrás saltar. De nada muchacho... Ahora fuera de mi vista." `DLG_ANTIPATICO_TURNIN_03`

---

# ESCENA: Amiga del Niño Pez / Niño Pez (misión de las algas)

**Amiga del Niño Pez** — voz amable, un poco tímida
"¿Buscas a mi amigo? Siempre se esconde cerca del mar." `DLG_AMIGAPEZ_01`

**Amiga del Niño Pez**
"Si encuentras su caracola azul, díselo rápido." `DLG_AMIGAPEZ_02`

**Amiga del Niño Pez**
"A veces se camufla con Algas." `DLG_AMIGAPEZ_03`

**Amiga del Niño Pez**
"Si te las pide puedes preguntar en las tiendas del reino." `DLG_AMIGAPEZ_04`

**Amiga del Niño Pez**
"A veces les sobra y hasta te la pueden regalar." `DLG_AMIGAPEZ_05`

**Niño Pez** — voz infantil, torpe, entrañable
"¡Glub! Perdón, a veces olvido hablar aire." `DLG_NIÑOPEZ_01`

**Niño Pez**
"¿Has visto mi caracola azul? La uso para llamar a mis amigos marinos." `DLG_NIÑOPEZ_02`

**Niño Pez**
"Pero no puedo meterme en el agua sin mis Algas de camuflaje," `DLG_NIÑOPEZ_03`

**Niño Pez**
"Seguro que mi amiga me las ha quitado." `DLG_NIÑOPEZ_04`

**Niño Pez**
"Si la encuentras te diré como puedes ayudarme a buscar mi caracola." `DLG_NIÑOPEZ_05`

**Niño Pez**
"Seguro que alguien del Reino tiene Algas..." `DLG_NIÑOPEZ_ALGAS_PROGRRESS`

**Niño Pez**
"Busca mi caracola porfa." `DLG_NIÑOPEZ_CARACOLA_01`

**Niño Pez** — [happily]
"Gracias, eres mi héroe." `DLG_NIÑOPEZ_CARACOLA_TURNIN`

**Niño Pez**
"Te doy a cambio este complemento que yo no lo uso." `DLG_NIÑOPEZ_CARACOLA_TURNIN_02`

**Niño Pez** — [happily]
"Oh Muchas gracias, ahora que tengo mis Algas puedo entrar al mar." `DLG_NIÑOPEZ_TURNIN_01`

**Niño Pez**
"Es muy fácil, solo tienes que mover los brazos para nadar." `DLG_NIÑOPEZ_TURNIN_02`

**Niño Pez**
"Pruebalo y ayúdame a buscar mi caracola." `DLG_NIÑOPEZ_TURNIN_03`

---

# ESCENA: Eldran — misión 1 y 2 (la carta y la caja de frutas)

**Eldran**
"Veo que leíste mi carta..." `DLG_ELDRAN_MISSION1_01`

**Eldran**
"¿Has leido la carta que te dejé en la mesita?" `DLG_ELDRAN_MISSION1_BEFORE_01`

**Eldran**
"La caja debe estar cerca de un árbol." `DLG_ELDRAN_MISSION2_INPROGRESS_01`

**Eldran**
"Búscala por esta zona no debe andar muy lejos." `DLG_ELDRAN_MISSION2_INPROGRESS_02`

**Eldran** — [happily]
"Gracias por venir." `DLG_ELDRAN_MISSION2_OFFER_01`

**Eldran**
"He estado recogiendo frutas en el bosque..." `DLG_ELDRAN_MISSION2_OFFER_02`

**Eldran**
"Y ahora la caja pesa demasiado para que yo la traiga solo." `DLG_ELDRAN_MISSION2_OFFER_03`

**Eldran**
"¿Podrías ir al bosque a buscarla?" `DLG_ELDRAN_MISSION2_OFFER_04`

**Eldran**
"Y luego traérmela aquí, por favor." `DLG_ELDRAN_MISSION2_OFFER_05`

**Eldran** — [happily]
"¡Excelente! Conseguiste traer la caja." `DLG_ELDRAN_MISSION2_TURNIN_01`

**Eldran**
"Sabía que podía contar contigo." `DLG_ELDRAN_MISSION2_TURNIN_02`

**Eldran**
"Toma, unas monedas por tu ayuda." `DLG_ELDRAN_MISSION2_TURNIN_03`

**Eldran** — [happily]
"¡Bien hecho! Te espero en la puerta de tu casa, ven cuando estes preparado." `DLG_ELDRAN_MISSION3_COMPLETE`

**Eldran**
"¡Muy bien, Will! Por cierto... ven conmigo, quiero enseñarte algo antes de irme." `DLG_ELDRAN_MISSION3_COMEWITHME_01`

**Eldran**
"Mira, esto es un Punto de Guardado." `DLG_ELDRAN_SAVEPOINT_01`

**Eldran**
"Aquí podrás guardar tu partida y descansar para recuperar tu salud y tu maná, así que no dudes en usarlo cuando lo necesites." `DLG_ELDRAN_SAVEPOINT_02`

**Eldran**
"Bueno, con esto ya sabes lo importante. Te dejo seguir explorando." `DLG_ELDRAN_SAVEPOINT_03`

**Eldran** — [softly]
"Ve con cuidado, Will. Nos vemos pronto." `DLG_ELDRAN_SAVEPOINT_TURNIN_01`

---

# ESCENA: Eldran — misiones 5 y 6 (preparativos y bosque prohibido)

**Eldran**
"Will, la amenaza del demonio ha despertado la magia de tu interior." `DLG_ELDRAN_MISSION5_OFFER_01`

**Eldran** — [sighs]
"Pero hay algo que no me gusta..." `DLG_ELDRAN_MISSION5_OFFER_02`

**Eldran**
"Hacía eones que no vivíamos una amenaza así en el Reino." `DLG_ELDRAN_MISSION5_OFFER_03`

**Eldran**
"No entiendo qué ha cambiado para que ese demonio haya aparecido ahora." `DLG_ELDRAN_MISSION5_OFFER_04`

**Eldran**
"De momento no digamos nada para no alterar al pueblo." `DLG_ELDRAN_MISSION5_OFFER_05`

**Eldran**
"Pero necesito que vayas al bosque prohibido y busques a Estela, una maga de la que he oído hablar." `DLG_ELDRAN_MISSION5_OFFER_06`

**Eldran**
"Aunque antes necesitas alguna cosilla para que el guardia te deje pasar." `DLG_ELDRAN_MISSION5_OFFER_07`

**Eldran**
"Lo primero es una capa de mago. Busca a Victoria en la tienda de ropa y dile que vas de mi parte. Ella te la dará." `DLG_ELDRAN_MISSION5_OFFER_08`

**Eldran**
"Luego compra en cualquier tienda una Poción de Vida." `DLG_ELDRAN_MISSION5_OFFER_09`

**Eldran**
"Por último habla con Erika y dile que te entrene. Necesitas entrenamiento antes de salir a enfrentarte al mundo." `DLG_ELDRAN_MISSION5_OFFER_10`

**Eldran**
"Te recuerdo que la capa puedes conseguirla preguntando a Victoria en la tienda de ropa." `DLG_ELDRAN_MISSION5_PROGRESS_01`

**Eldran**
"La Poción de Vida en cualquier tienda del reino la puedes encontrar." `DLG_ELDRAN_MISSION5_PROGRESS_02`

**Eldran**
"Habla con Erika suele ir de verde y debe andar por el Reino." `DLG_ELDRAN_MISSION5_PROGRESS_03`

**Eldran** — [happily]
"Has conseguido todo y muy rápido." `DLG_ELDRAN_MISSION5_TURNIN`

**Eldran**
"Bien hecho Will, ahora puedes ponerte la capa de mago desde el menú." `DLG_ELDRAN_MISSION6_OFFER_01`

**Eldran**
"Pulsa el botón de menú y desplázate hasta Equipo." `DLG_ELDRAN_MISSION6_OFFER_02`

**Eldran**
"Baja hasta la sección capas y equipala." `DLG_ELDRAN_MISSION6_OFFER_03`

**Eldran**
"Ahora siempre que consigas algo de vestimenta nueva podras cambiar tu apariencia desde este menú." `DLG_ELDRAN_MISSION6_OFFER_04`

**Eldran**
"Pero bueno, a lo importante..." `DLG_ELDRAN_MISSION6_OFFER_05`

**Eldran**
"Sal del Reino y ve hacia el bosque prohibido y busca a Estela." `DLG_ELDRAN_MISSION6_OFFER_06`

**Eldran**
"No sé mucho de ella pero en el Reino es famosa por ser la más poderosa de la región." `DLG_ELDRAN_MISSION6_OFFER_07`

**Eldran**
"Yo no duraría ni un minuto en el bosque pero tu con tu magia puedes lograrlo." `DLG_ELDRAN_MISSION6_OFFER_08`

**Eldran**
"Ten mucho cuidado ahí dentro y equipate bien antes de salir." `DLG_ELDRAN_MISSION6_OFFER_09`

**Eldran**
"Busca a Estela en el bosque prohibido." `DLG_ELDRAN_MISSION6_PROGRESS_01`

**Eldran**
"Venid a verme cuanto antes." `DLG_ELDRAN_MISSION6_PROGRESS_02`

**Eldran** — [happily]
"Que bien que ya estáis por aquí..." `DLG_ELDRAN_MISSION6_TURNIN_01`

**Estela** — [happily]
"Ha sido pan comido..." `DLG_ELDRAN_MISSION6_TURNIN_02`

**Will** — [annoyed]
"Si lo tenías todo calculado ¿no?, sobre todo la parte del Golem." `DLG_ELDRAN_MISSION6_TURNIN_03`

**Eldran**
"Bueno mejor será no hablar aquí, nos vemos en la taberna." `DLG_ELDRAN_MISSION6_TURNIN_04`

**Estela**
"¡Por fin! que con el estómago vacío no puedo pensar." `DLG_ELDRAN_MISSION6_TURNIN_05`

---

# ESCENA: Estela — bosque prohibido (primer encuentro)

**Will**
"Tu debes ser Estela... me manda Eldran. Ha aparecido un demonio y necesitamos tu ayuda." `DLG_ESTELA_BOSQUE_00`

**Estela** — [happily] [rushed]
"¡Wau! Parece una misión ultra secreta y super peligrosa." `DLG_ESTELA_BOSQUE_01`

**Estela** — [happily]
"¡Me apunto!" `DLG_ESTELA_BOSQUE_02`

**Estela**
"Por cierto no pensarás ir andando de vuelta ¿no?" `DLG_ESTELA_BOSQUE_03`

**Estela**
"En ese punto de guardado ahora puedes usar teletransporte pulsando el botón indicado." `DLG_ESTELA_BOSQUE_04`

**Estela**
"Y ahora gracias a mi también puedes cambiar de personaje. Pulsa a la derecha y haz el cambio." `DLG_ESTELA_BOSQUE_05`

**Estela** — voz obsesionada con la comida, robando puestos, cómica (monólogo interno del minijuego de "provisiones")
"Sin dinero… Sin dinero… ¡Sin un maldito cobre!" `FORAGE_MONOLOGUE_01`

**Estela** — [annoyed]
"Bueno… tampoco es robar del todo. Es más bien… redistribuir los recursos." `FORAGE_MONOLOGUE_02`

**Estela** — [happily]
"Solo tengo que pillar algo de cada puesto sin que me vean. Fácil. Soy invisible cuando quiero." `FORAGE_MONOLOGUE_03`

---

# ESCENA: Guardia Real detiene al trío por la montaña destruida

**Guardia**
"Alto ahí... ¿os creéis que podéis ir destruyendo montañas como si nada?" `DLG_GUARD_01`

**Guardia**
"Por órdenes de su majestad, vosotros tres debéis acompañarme." `DLG_GUARD_02`

**Guardia**
"¿Quién ha sido?" `DLG_GUARD_03`

**Will** — [rushed]
"¡Ha sido ella!" `DLG_WILL_ARRESTO_ACUSA`

**Liam**
"¡Sí, ha sido ella!" `DLG_LIAM_ARRESTO_ACUSA`

**Estela** — [angry]
"¡¿Qué?! ¡Eso no es justo, todos estábamos ahí!" `DLG_ESTELA_ARRESTO_INDIGNADA`

**Guardia**
"A su majestad no le gusta que le hagan esperar." `DLG_GUARD_04`

**Eldran**
"Os espero aquí fuera. Intentaré hablar con alguien para que os dejen salir... aunque, sabiendo que vais con Estela, dudo que aguantéis mucho ahí dentro." `DLG_ELDRAN_ARRESTO_DESPEDIDA`

**Guardia**
"Entrad, su majestad os espera." `DLG_GUARD_05`

**Guardia**
"Seguidme y no os quedéis atrás." `DLG_GUARD_06`

---

# ESCENA: Cómo se destruyó la montaña (flashback cómico)

**Liam** — [angry] [surprised]
"¡Estela! ¿¡Qué acabas de hacer!?" `DG_LIAM_ARRESTO_01`

**Estela** — [annoyed]
"El tonto este se interponía entre mi estómago y mi paz mental." `DG_LIAM_ARRESTO_02`

**Liam**
"La Guardia Real no tardará en investigar." `DG_LIAM_ARRESTO_03`

**Liam** — [annoyed]
"A ver yo solo quería ayudar.... estupendo, bonita, mira quién viene por ahí...." `DG_LIAM_ARRESTO_04`

**Estela** — [angry] [booming]
"¡Nadie me interrumpe mientras como!" `EVT_MOUNTAIN_01`

**Estela** — [shouts]
"¡Así que cómete esta!" `EVT_MOUNTAIN_02`

**Eldran** — [shouts] [rushed]
"¡CORREEEEDDDDD!" `EVT_MOUNTAIN_ELDRAN_RUN`

---

# ESCENA: Ante el Rey (trono)

**Rey** — voz autoritaria, se asusta con facilidad
"Así que vosotros sois los que habéis destruido la montaña." `DLG_KING_01`

**Will** — [rushed]
"Bueno en realidad ha sido ella..." `DLG_KING_02`

**Estela** — [whispers] [angry]
"Ya verás cuando te pille." `DLG_KING_03`

**Rey**
"Me temo que eso no será aquí. Por la seguridad de nuestros ciudadanos me veo obligado a deteneros." `DLG_KING_04`

**Eldran**
"Disculpe majestad... hay un asunto más importante que debería conocer." `DLG_KING_05`

**Rey**
"Me temo que tendrá que esperar a mañana." `DLG_KING_06`

**Rey** — [shouts]
"¡Guardias! arrestadles." `DLG_KING_07`

**Guardia** — [surprised]
"Alto ahí, ¿dónde creeis que vais?" `DLG_KING_08`

**Estela**
"Mierda" `DLG_KING_09`

**Rey** — [surprised] (revisar: puede ser un aparte del Rey durante un ataque que interrumpe el arresto)
"Como no haga algo ahora estoy perdido." `DLG_KING_10`

**Rey** — [awe]
"No me puedo creer lo que acaba de pasar..." `DLG_KING_11`

**Eldran**
"Es lo que tratabamos de decirte..." `DLG_KING_12`

**Rey** — [sighs]
"Disculpad que no os escuché..." `DLG_KING_13`

**Rey**
"Creo que a cambio de lo que habéis hecho por mi podemos olvidar el tema de la montaña...." `DLG_KING_14`

**Rey**
"Lo que sí os voy a pedir es que no comentéis nada por el Reino de este tema..." `DLG_KING_15`

**Will**
"Tranquilo no diremos nada..." `DLG_KING_16`

**Eldran**
"No se preocupe su majestad. Nos encargaremos de averiguar qué está pasando." `DLG_KING_17`

**Rey**
"Aceptad este hechizo como muestra de agradecimiento." `DLG_KING_18`

**Eldran**
"Chicos os espero en las afueras del Reino. Reunios conmigo cuando estéis preparados." `DLG_KING_19`

---

# ESCENA: Celda / prisión (dos versiones — ver nota técnica al final)

**Will** — [sarcastic]
"Estupendo... y ahora ¿qué hacemos?" `DLG_ESTELA_PRISION_00`

**Estela**
"Pues está claro ¿no?" `DLG_ESTELA_PRISION_01`

**Estela** — [happily]
"Dejadmelo a mi y vuelo esto por los aires." `DLG_ESTELA_PRISION_02`

**Liam**
"Calma chicos. Lo mejor es que intentemos razonar con el Rey." `DLG_ESTELA_PRISION_03`

**Will**
"La verdad que con demonios y golems amenazando el reino.... no me quedo muy tranquilo aquí dentro." `DLG_ESTELA_PRISION_04`

**Liam**
"Tienes razón..." `DLG_ESTELA_PRISION_05`

**Estela** — [happily]
"Entonces iniciamos la operación explosiva..." `DLG_ESTELA_PRISION_06`

**Liam** — [interrupting]
"¡No! Escuchad..." `DLG_ESTELA_PRISION_07`

**Liam**
"Will vamos a buscar una forma de salir de aquí..." `DLG_ESTELA_PRISION_08`

**Liam**
"Así al salir Eldran puede hablar con el Rey y contarle lo que ha pasado mientras nosotros intentamos averiguar qué ocurre." `DLG_ESTELA_PRISION_09`

**Estela** — [happily]
"Vale, vale... Entonces iniciamos la operación ¡Avestruz! Yo me encargo de lo técnico, vosotros cubridme." `DLG_ESTELA_PRISION_10`

**Will** — [confused]
"¿Por qué avestruz?" `DLG_ESTELA_PRISION_11`

**Liam** — [annoyed]
"Esta niña es tonta" `DLG_ESTELA_PRISION_12`

**Estela** — [angry]
"¡Tú! ¡Todo esto es por tu culpa! ¡Estaba a punto de comer y tuviste que venir a molestar! Si no hubieras interrumpido, no estaríamos en esta pocilga." `LORE_CELDA_1`

**Liam** — [annoyed]
"Deja de llorar por un plato de comida." `LORE_CELDA_3`

**Will**
"Bueno calma... ¿Liam, por qué no terminas de contarnos eso del Sendero?" `LORE_CELDA_4`

**Liam**
"El Sendero no es más que un portal que conduce a un mundo ubicado en algún lugar en las estrellas." `LORE_CELDA_5`

**Liam**
"Dentro de ese mundo habrá que superar unas pruebas. Una por cada persona que entre." `LORE_CELDA_6`

**Liam**
"Si se superan todas las pruebas el Sendero puede conceder un deseo a cada uno al final." `LORE_CELDA_7`

**Estela** — [awe]
"A mi me parece súper interesante." `LORE_CELDA_8`

**Will**
"Bueno lo primero es salir de aquí. No podemos perder tiempo con demonios rondando el Reino." `LORE_CELDA_9`

**Liam** — (revisar: también podría ser Estela)
"Will ¿por qué no miras si hay algo que pueda abrir esa puerta?" `LORE_CELDA_10`

**Liam**
"Creo que si nos separamos podemos avanzar más rápido, cada uno puede encargarse de algo distinto." `LORE_CELDA_11`

**Liam**
"Pulsa abajo para unir o disolver al equipo." `LORE_CELDA_12`

---

# ESCENA: Escolta tras salir del castillo — la leyenda del Sendero

**Estela** — [sarcastic]
"De verdad, ¿por una montaña? Qué exagerados." `LORE_ESCOLTA_GUARDIA_1`

**Will**
"Bueno ahora lo importante es salir de este embrollo." `LORE_ESCOLTA_GUARDIA_2`

**Will**
"Oye Liam, ¿de qué nos ibas a hablar en la taberna?" `LORE_ESCOLTA_GUARDIA_3`

**Guardia**
"Pues no pude evitar escuchar vuestra conversación." `LORE_ESCOLTA_GUARDIA_4`

**Guardia**
"¿Os parece casualidad lo del Golem y el Demonio?" `LORE_ESCOLTA_GUARDIA_5`

**Guardia**
"Desde luego que no chico." `LORE_ESCOLTA_GUARDIA_6`

**Guardia**
"¿Conocéis la leyenda del Sendero de las Estrellas?" `LORE_ESCOLTA_GUARDIA_7`

**Liam** — [softly] (sabe más de lo que aparenta)
"Algo he escuchado alguna vez." `LORE_ESCOLTA_GUARDIA_8`

**Estela** — [sarcastic]
"¿Un cuento de niños?" `LORE_ESCOLTA_GUARDIA_9`

**Guardia** — [drawn out]
"Os lo creáis o no puede que ahí estén vuestras respuestas..." `LORE_ESCOLTA_GUARDIA_10`

**Guardia**
"Aunque no creo que sea el momento para hablarlo." `LORE_ESCOLTA_GUARDIA_11`

---

# ESCENA: Will y Liam — conversación personal (el pasado de Will)

**Liam**
"Oye Will y ¿siempre has estado con Eldran?" `LORE_WILL_LIAM_01`

**Will** — [sorrowful]
"No... la verdad es que no recuerdo nada de antes." `LORE_WILL_LIAM_02`

**Will** — [softly]
"Solo sé que desperté una noche de lluvia... y Eldran ya estaba ahí." `LORE_WILL_LIAM_03`

**Liam** — [surprised]
"¿Nada de nada? ¿Ni un nombre, ni una cara?" `LORE_WILL_LIAM_04`

**Will** — [sorrowful] [slowly]
"Nada. A veces pienso que ese fue el día en que empecé a existir." `LORE_WILL_LIAM_05`

---

# ESCENA: Taberna — el hambre de Estela y la llegada de Liam

**Narrador**
"Eldran, Will y Estela discuten de lo ocurrido y tratan de buscar una solución..." `EVT_TABERNA_01`

**Narrador** — [laughs]
"Cuando de repente el estómago de Estela rugió de forma atroz." `EVT_TABERNA_02`

**Eldran**
"Hay que averiguar qué hay detrás de ese demonio... y ahora, de un golem.\nY por qué han ido a por vosotros." `EVT_TAB_ELDRAN_01`

**Eldran** — [sighs]
"Creo que he perdido el apetito." `EVT_TAB_ELDRAN_02`

**Estela**
"Sí, sí, muy revelador. ¿Y cuándo traen la comida?" `EVT_TAB_ESTELA_01`

**Estela**
"Eso... no he sido yo." `EVT_TAB_ESTELA_02`

**Narrador**
"Por fin llega la comida y nuestros héroes se disponen a comer." `EVT_TABERNA_03`

**Estela** — [happily]
"¡AL FIN!" `EVT_TAB_ESTELA_03`

**Narrador** — [laughs]
"Estela lo hace de una forma un poco... especial..." `EVT_TABERNA_04`

**Will** — [surprised]
"Estela... ¿de verdad comes así?" `EVT_TAB_WILL_01`

**Narrador**
"Eldran y Will, sorprendidos por la forma de comer de Estela, pierden el apetito." `EVT_TABERNA_05`

**Narrador**
"En ese momento se acerca Liam, que estaba escuchando la conversación desde lejos." `EVT_TABERNA_06`

**Liam** — [softly]
"Disculpad que os interrumpa...\nMe llamo Liam y no he podido evitar escuchar vuestra conversación.\nCreo que tengo información que podría interesaros." `EVT_TAB_LIAM_01`

**Narrador**
"Intenta contar al grupo algo pero llama la atención de Estela que en ese momento solo podía pensar en la comida." `EVT_TABERNA_07`

**Will** — [interrupting]
"Y también qué tiene que ver con mi magia..." `EVT_TAB_WILL_02`

**Narrador**
"Estela enfadada por la interrupción conjura una bola de fuego." `EVT_TABERNA_08`

**Estela** — [angry] [shouts]
"¡NADIE me interrumpe cuando estoy comiendo!" `EVT_TAB_ESTELA_RAGE`

**Narrador**
"Los tres se sorprenden..." `EVT_TABERNA_09`

**Will** — [shouts] [surprised]
"¡¿QUÉ?!" `EVT_TAB_WILL_03`

**Narrador** — [rushed]
"¡Corred! Huid de Estela hasta que se le pase el enfado." `EVT_TABERNA_10`

**Estela** — [angry]
"¡Se va a enterar este!" `EVT_TAB_ESTELA_SEVAENTERAR`

---

# ESCENA: El Libro de los Hechizos Prohibidos (amigo de Eldran)

**Amigo de Eldran** (anciano, guarda el secreto del Libro) — voz mayor, cautelosa
"Hola chicos, se ve que tenéis magia a distancia" `DG_AMIGOELDRAN_BEFORE`

**Amigo de Eldran**
"Disculpad, ¿puedo ayudaros?" `DG_AMIGOELDRAN_TURNIN`

**Will**
"Perdone, ¿es usted el amigo de Eldran? Nos envía él, necesitamos su ayuda." `DG_LIBROHECHIZOS_01`

**Amigo de Eldran** — [softly]
"¿Eldran? en qué estará metido ahora mi viejo amigo..." `DG_LIBROHECHIZOS_02`

**Will**
"Sabemos que usted tiene información sobre el Libro de los Hechizos Prohibidos." `DG_LIBROHECHIZOS_03`

**Amigo de Eldran** — [whispers] [surprised]
"¡Silencio, muchacho! No digas eso en alto..." `DG_LIBROHECHIZOS_04`

**Amigo de Eldran**
"¿Sabéis lo peligroso que es lo que me estáis pidiendo?" `DG_LIBROHECHIZOS_05`

**Will**
"Sí, lo sabemos pero más peligroso es no hacer nada y dejar que sigan atacando el Reino..." `DG_LIBROHECHIZOS_06`

**Amigo de Eldran** — [surprised]
"¿Han atacado el Reino?" `DG_LIBROHECHIZOS_07`

**Will**
"Sí... y si no hacemos algo quien sabe lo que pueda pasar..." `DG_LIBROHECHIZOS_08`

**Amigo de Eldran**
"A ver lo único que sé es que es custodiado por un guardián. Al final del camino Rocoso hay unas Ruinas..." `DG_LIBROHECHIZOS_09`

**Amigo de Eldran**
"Se dice que allí un guardián custodia un importante libro de hechizos pero nadie ha sido capaz de conseguirlo y poco a poco la gente ha dejado de visitar el templo." `DG_LIBROHECHIZOS_10`

**Estela** — [confused]
"Perdonad. ¿De qué habláis? Es que estaba pensando en ir a esa taberna de ahí y perdí el hilo..." `DG_LIBROHECHIZOS_11`

**Liam** — [sighs]
"Discúlpala.... la pobre no da para más...." `DG_LIBROHECHIZOS_12`

**Estela** — [angry]
"¿Qué dices Liam? ¿Que te apetece comerte una bola de fuego?" `DG_LIBROHECHIZOS_13`

**Will** — [sighs]
"No empecéis otra vez...." `DG_LIBROHECHIZOS_14`

**Estela**
"Ha empezado él." `DG_LIBROHECHIZOS_15`

**Amigo de Eldran** — [laughs]
"Mi mi mi mi..." `DG_LIBROHECHIZOS_16`

---

# ESCENA: Despedida antes del Sendero

**Eldran** — [drawn out]
"Bueno, ya podemos respirar tranquilos... Aunque hay que admitir que ha sido una coincidencia muy conveniente" `DLG_ELDRAN_MISSION13_TURNIN_01`

**Liam** — [softly] (empieza a mostrar que sabe más de lo que dice)
"Demasiado conveniente, si me preguntas a mí. Ese demonio atacó justo a tiempo para salvarnos el cuello. Casi parecía amaestrado." `DLG_ELDRAN_MISSION13_TURNIN_01_02`

**Will** — [rushed]
"E-el Reino está bajo ataque, era cuestión de tiempo que llegaran al castillo. Lo importante es que ahora somos libres y el Rey confía en nosotros." `DLG_ELDRAN_MISSION13_TURNIN_01_03`

**Eldran**
"Liam tiene razón, hemos tenido suerte, aprovechémosla. Entonces Liam, ¿qué es lo que hay que hacer para ir al Sendero?" `DLG_ELDRAN_MISSION13_TURNIN_02`

**Liam**
"Pues para abrir el portal necesitamos un antiguo libro de hechizos..." `DLG_ELDRAN_MISSION13_TURNIN_03`

**Liam**
"Pero desconozco su ubicación." `DLG_ELDRAN_MISSION13_TURNIN_04`

**Liam**
"Quizás un amigo mio pueda echarnos una mano." `DLG_ELDRAN_MISSION13_TURNIN_05`

**Liam**
"Ademas vive cerca. Justo en el pueblo de al lado." `DLG_ELDRAN_MISSION13_TURNIN_06`

**Estela** — [happily]
"¿Entonces buscamos al vegestorio, encontramos el libro y abrimos el portal?" `DLG_ELDRAN_MISSION13_TURNIN_07`

**Liam**
"Si algo así Estela...." `DLG_ELDRAN_MISSION13_TURNIN_08`

**Eldran** — [softly]
"Tened cuidado. Yo estaré por el Reino como siempre..." `DLG_ELDRAN_MISSION13_TURNIN_09`

**Eldran** — [sorrowful]
"Aquí se separan nuestros caminos. Will espero que puedas encontrar en el Sendero respuestas." `DLG_ELDRAN_MISSION13_TURNIN_10`

**Estela** — [happily] [shouts]
"¿Vamos o que? ¡Que comience la operación Avestruz 2.0!" `DLG_ELDRAN_MISSION13_TURNIN_11`

**Liam** — [annoyed]
"Lo confirmo. Es tonta." `DLG_ELDRAN_MISSION13_TURNIN_12`

**Estela** — [sorrowful] [slowly]
"La primera vez que lancé un hechizo, casi me quedo sin cejas.\nY aquí sigo... hecha una auténtica prodigio.\nEl miedo no es el problema, pequeñín.\nEl problema sería que dejaras de intentarlo. Y eso no te lo permito." `EVT_REINOEXIT_ESTELA_01`

**Liam** — [softly]
"Will... ¿qué ocurre?" `EVT_REINOEXIT_LIAM_01`

**Liam** — [softly]
"No vas a estar solo en esto.\nPase lo que pase." `EVT_REINOEXIT_LIAM_02`

**Will** — [sorrowful] [slowly]
"Es la primera vez que salgo de aquí.\nToda mi vida ha estado ahí detrás.\nTengo miedo, Liam. ¿Y si no soy capaz de controlar esto?\n¿Y si allá fuera no basta con lo que soy... y todo sale mal?" `EVT_REINOEXIT_WILL_01`

**Will** — [softly]
"...Gracias chicos. ¿Vamos?" `EVT_REINOEXIT_WILL_02`

---

# ESCENA: El Golem (ataque y revelación de Liam)

**Liam** — [angry] [whispers]
"Esa estúpida va a arruinar mis planes..." `EVT_GOLEM_01`

**Liam**
"Veremos si es tan fuerte como dicen..." `EVT_GOLEM_02`

**Narrador**
"Las arañas creían haber acorralado a una presa indefensa. No sabían que acababan de encerrarse con Estela." `EVT_ESTELA_01`

**Narrador**
"Con una sonrisa de desafío, Estela liberó su verdadero poder." `EVT_ESTELA_02`

**Narrador**
"Con un guiño pícaro y una sonrisa de satisfacción, Estela contempló su obra. No quedó ni una sola araña en pie." `EVT_ESTELA_03`

**Estela** — [angry] [shouts]
"¡Fuera de mi camino!" `EVT_ESTELA_APP_01`

**Estela** — [sarcastic]
"¿Arañas? Qué decepción." `EVT_ESTELA_APP_02`

**Estela** — [shouts]
"¡Esta es para vosotras!" `EVT_ESTELA_APP_03`

**Estela** — [sarcastic]
"No sabéis con quién os habéis metido." `EVT_ESTELA_APP_04`

**Estela** — [shouts]
"¡Ahí va la última!" `EVT_ESTELA_APP_05`

**Estela** — [sarcastic]
"Esta por fea." `EVT_ESTELA_ARAÑA_1`

**Estela** — [laughs]
"¿Oh no te lo esperabas?" `EVT_ESTELA_ARAÑA_2`

**Estela** — [sarcastic]
"Pobrecita." `EVT_ESTELA_ARAÑA_3`

**Estela** — [annoyed]
"Sois taaaan aburridas..." `EVT_ESTELA_ARAÑA_4`

**Estela** — [dramatic] (interpretación teatral, fingida)
"Oh nooo, por favor... ¡que alguien me ayude!\n...estos dos pedazos de hombres tan fuertes y musculosos...\n...van a robar a una pequeña princesita como yo..." `EVT_ESTELA_DRAMATIC`

**Estela** — [sarcastic]
"...¿Eso era todo?" `EVT_ESTELA_ESO_ERA`

**Estela** — [happily]
"Que es ¡Pan comido!" `EVT_ESTELA_PANCOMI`

**Lety y Vicky** (arañas, dúo burlón) — voces gemelas, entre sí muy sincronizadas
"¡Arañas, atacad!" `EVT_W1_ARANAS_ATACAD`

**Lety y Vicky** — [sarcastic]
"¿Pan comido? A ver si te resulta tan fácil nuestra técnica..." `EVT_W1_PANCOMI`

**Lety y Vicky** — [sarcastic]
"¿Princesita? Dirás princefea, ¿no?" `EVT_W1_PRINCEFEA`

**Estela** — [angry] [awe]
"Hmph. Tal y como predije. La trayectoria de la explosión fue perfecta." `EVT_GOLEM_END_01`

**Estela**
"Lo tenía todo calculado." `EVT_GOLEM_END_02`

**Will** — [sarcastic]
"¿Calculado? ¡Si te has tropezado antes de disparar! Admítelo..." `EVT_GOLEM_END_03`

**Will** — [sighs]
"Ha sido un milagro que no acabáramos hechos puré." `EVT_GOLEM_END_04`

**Liam** — [softly] (desde las sombras, revelando su secreto)
"Esa hechicera es un problema imprevisto en mi ecuación." `EVT_GOLEM_END_05`

**Liam**
"El Golem ha fallado. Si quiero que el chico llegue al final, tendré que guiarlo yo mismo." `EVT_GOLEM_END_06`

**Liam** — [drawn out] [awe]
"Y ese es el chico de corazón puro que abrirá el Sendero." `EVT_LIAM_CRYSTAL_01`

**Liam**
"Me alegra que ese demonio haya despertado su magia." `EVT_LIAM_CRYSTAL_02`

**Liam** — [laughs]
"Ja… ja, ja…" `EVT_LIAM_CRYSTAL_03`

**Liam**
"Veremos si este gólem es suficiente..." `EVT_LIAM_GOLEM_01`

**Liam**
"Si no... tendré que intervenir yo mismo." `EVT_LIAM_GOLEM_02`

---

# ESCENA: Combates varios / enemigos genéricos (líneas cortas de encuentro)

**Erika (Guerrera China)** — voz firme, marcial, sin rodeos
"Así que tú eres Will… Eldran me habló de ti." `DLG_GUERRERACHINA_01`

**Erika**
"Si te ha enviado aquí, es que cree que aún puedes mejorar." `DLG_GUERRERACHINA_02`

**Erika**
"Voy a enseñarte un nuevo ataque y una técnica de protección." `DLG_GUERRERACHINA_03`

**Erika**
"No será fácil, tendrás que esforzarte más que nunca." `DLG_GUERRERACHINA_04`

**Erika**
"Podrás usar PROTECCIÓN si presionas los botones de bloqueo a la vez." `DLG_GUERRERACHINA_05`

**Erika**
"Voy a darte un hechizo nuevo: BOLA PRISMA. Usa el botón de hechizo y atacame con él." `DLG_GUERRERACHINA_06`

**Erika**
"Por último espero que traigas al menos una POCIÓN DE VIDA. Desde el menú inventario puedes usarla y recuperar vida." `DLG_GUERRERACHINA_07`

**Erika**
"Prepárate, Will. El entrenamiento empieza ahora." `DLG_GUERRERACHINA_08`

**Erika**
"Así se hace." `DLG_GUERRERACHINA_AFTER`

**Erika**
"Bienvenido a la zona de entrenamiento. Solo lo hacemos con magos lo siento." `DLG_GUERRERACHINA_BEFORE`

**Erika**
"Sigue entrenando para ser un gran mago." `DLG_GUERRERACHINA_DEFEAT`

**Lety** — mitad de un dúo burlón, algo temeraria
"Mira Vicky, una presa fácil." `DLG_LETY_ALERT_01`

**Lety**
"Vamos a divertirnos un rato contigo." `DLG_LETY_ALERT_02`

**Vicky** — la otra mitad, más calculadora
"Ya veo Lety... atento muchacho a nuestro ataque dual." `DLG_VICKY_ALERT_01`

**Vicky**
"Por cierto chico, cuando quieras cambiar el foco de tu ataque..." `DLG_VICKY_ALERT_02`

**Vicky**
"Puedes pulsar los gatillos laterales." `DLG_VICKY_ALERT_03`

**Lety** — [rushed]
"Corre Vicky, corre..." `DLG_LETY_DIZZY`

**Vicky** — [surprised]
"No... Lety, ten cuidado, ahora estás sola." `DLG_VICKY_DIZZY`

> Nota técnica: `dialogues_es.json` tiene también las claves antiguas `DLG_LETY_VICKY_ALERT_01-04` y `DLG_LETY_VICKY_DEFEATED`, con el mismo texto que las de arriba pero sin separar por personaje. Parecen una versión anterior no limpiada — no las dupliques al grabar, usa solo las de Lety/Vicky por separado.

**Mago enemigo #1** — voz genérica, tono amenazante leve
"No deberías estar aquí." `DLG_MAGO#1_ALERT`

**Mago enemigo #1** — [sighs]
"¡Auch! Eso ha dolido." `DLG_MAGO#1_DEFEATED`

**Mago enemigo #2**
"¡Alto ahí! Aqui se cobra peaje muchacho." `DLG_MAGO#2_ALERT`

**Mago enemigo #2**
"Seguiré entrenando para estar a tu altura." `DLG_MAGO#2_DEFEATED`

**Mago enemigo #3**
"Demuestrame tu poder." `DLG_MAGO#3_ALERT`

**Mago enemigo #3** — [surprised]
"Pues si que eres fuerte." `DLG_MAGO#3_DEFEATED`

**Pirata** — voz ronca, fanfarrón
"¿Ves este ojo?" `DLG_PIRATE_01`

**Pirata**
"Me lo quitó un lobo..." `DLG_PIRATE_02`

**Pirata** — [surprised]
"Te he subestimado grumete." `DLG_PIRATE_03`

**Pirata**
"Continúa así y serás un gran mago." `DLG_PIRATE_04`

**Guardia del Bosque** — voz cansada, harta de las arañas
"Este bosque era tranquilo... hasta que las arañas tomaron los senderos." `DLG_GUARDIABOSQUE_01`

**Guardia del Bosque**
"Si acabas con ellas, tendrás mi agradecimiento. Y algo más." `DLG_GUARDIABOSQUE_02`

**Guardia del Bosque**
"Todavía se oyen arañas entre los árboles. No bajes la guardia." `DLG_GUARDIABOSQUE_03`

**Guardia del Bosque** — [happily]
"El bosque respira de nuevo. Toma esto, te lo has ganado." `DLG_GUARDIABOSQUE_04`

**Guardia del Bosque**
"Solo los magos pueden pasar al bosque prohibido." `DLG_WOODS_GUARD`

**Enemigo genérico** (amenaza en el camino)
"Venga, danos todo lo que llevas y no te haremos daño." `EVT_W2_AMENAZA`

**Will** — [surprised]
"Madre mía..." `EVT_WILL_MADREMIA`

**Will** — [rushed]
"¿Has visto, Estela? ¡Ahora un golem! Esto nunca había pasado... ¡Corre, volvamos con Eldran!" `EVT_WILL_POST_GOLEM`

---

# ESCENA: El fuego fatuo (misión nocturna del pueblo)

**Aldeano/a atrapado/a** (sin nombre propio) — voz asustada, agradecida
"¡Por favor, ayudadme!" `DLG_FUEGO_FATUO_01`

**Will**
"¿Qué ocurre? Tranquilícese." `DLG_FUEGO_FATUO_02`

**Aldeano/a**
"Resulta que es imposible cruzar al otro lado para llegar al pueblo de noche..." `DLG_FUEGO_FATUO_03`

**Aldeano/a**
"Aparece un ser extraño, y cuando crees que ya vas a llegar de pronto vuelves a estar en la entrada." `DLG_FUEGO_FATUO_04`

**Aldeano/a**
"Hay mucha gente que trabaja de día y tiene que volver a sus casas y están atrapados." `DLG_FUEGO_FATUO_05`

**Will** — (revisar: también podría ser Estela o Liam)
"Nosotros justo íbamos hacia allí." `DLG_FUEGO_FATUO_06`

**Will**
"No se preocupe le ayudaremos con lo del ser extraño." `DLG_FUEGO_FATUO_07`

**Aldeano/a** — [happily]
"¡Muchas gracias! Venid a verme si lo conseguís, os daré algo a cambio..." `DLG_FUEGO_FATUO_08`

**Estela**
"Bueno, como hay que esperar a la noche, ¿que os parece si voy a por provisiones?" `DLG_FUEGO_FATUO_09`

**Liam**
"Will que te parece si tu y yo inspeccionamos el camino para que cuando sea de noche sepamos como es" `DLG_FUEGO_FATUO_10`

**Will**
"Perfecto. Estela nos vemos en un rato aqui en la entrada." `DLG_FUEGO_FATUO_11`

**Liam**
"Will cuando se disuelva el equipo ahora podras acercarte a Estela o a mi y pedirnos que te sigamos." `DLG_FUEGO_FATUO_12`

---

# ESCENA: NPCs del Reino (tienda, misiones secundarias, ambiente)

**Comerciante genérico / Mercader** — usar según corresponda al puesto
(sin líneas propias de diálogo hablado en el archivo — solo la etiqueta de nombre)

**Bárbara (Tendera)** — voz amable de tienda
"Tengo la tienda cerrada de momento." `DLG_TENDERA_01`

**Bárbara**
"Pero toma un poco de Algas que me sobra un montón." `DLG_TENDERA_02`

**Bárbara**
"Necesito un par de pociones de magia para mis remedios, y se me han agotado en la tienda." `DLG_TENDERA_ALGAS_01`

**Bárbara**
"Si me traes dos pociones de magia, te daré algas frescas a cambio — tengo de sobra." `DLG_TENDERA_ALGAS_02`

**Bárbara**
"¿Ya tienes las pociones de magia? Las necesito para mis remedios." `DLG_TENDERA_ALGAS_03`

**Bárbara** — [happily]
"Justo lo que necesitaba. Toma, las algas son tuyas — buen trato." `DLG_TENDERA_ALGAS_04`

**Rosa (Tabernera)** — voz cansada, resacosa, entrañable
"Ay, qué cabeza... Anoche la fiesta en la taberna se alargó más de la cuenta. Necesito preparar mi remedio contra la resaca, pero se me han acabado las algas." `DLG_TABERNERA_ALGAS_01`

**Rosa**
"¿Podrías traerme 2 algas? Te pagaré bien por ellas, te lo prometo." `DLG_TABERNERA_ALGAS_02`

**Rosa**
"¿Ya tienes las algas? Las necesito para el remedio, cuanto antes mejor." `DLG_TABERNERA_ALGAS_03`

**Rosa** — [happily]
"¡Perfecto, justo lo que necesitaba! Toma, esto es para ti. Ahora sí podré curarme esta resaca." `DLG_TABERNERA_ALGAS_04`

**Manuel** — voz de vecino preocupado por su abuela
"Lo que ves entre esas columnas es un punto de guardado. Acércate y podrás guardar la partida." `DLG_MANUEL_01`

**Manuel**
"Menos mal que pasas por aquí. Mi abuela lleva unos días muy débil y no consigo pociones de vida en ningún sitio." `DLG_MANUEL_POCIONES_01`

**Manuel**
"¿Podrías conseguirme 2 pociones de vida? Te daré algo especial a cambio, te lo prometo." `DLG_MANUEL_POCIONES_02`

**Manuel**
"¿Tienes ya las pociones? Mi abuela las necesita cuanto antes." `DLG_MANUEL_POCIONES_03`

**Manuel** — [happily]
"¡Muchísimas gracias! Esto le hará mucho bien. Toma, quería dártelo a ti, seguro que te queda genial." `DLG_MANUEL_POCIONES_04`

**Nora** — voz dulce, entusiasta
"Me encantan los abrazos calentitos." `DLG_NORA_01`

**Nora** — [happily]
"¡Qué bien que apareces! Llevo días queriendo probar un hechizo nuevo, pero necesito más energía mágica de la que tengo." `DLG_NORA_POCIONES_01`

**Nora**
"Si me consigues 3 pociones de magia, te compensaré bien, ¡lo prometo!" `DLG_NORA_POCIONES_02`

**Nora**
"¿Ya tienes las 3 pociones de magia? Estoy deseando probar el hechizo." `DLG_NORA_POCIONES_03`

**Nora** — [happily]
"¡Genial, ya puedo intentarlo! Toma, esto es para ti, por las molestias." `DLG_NORA_POCIONES_04`

**Oliver** — voz de guía cansado de repetirse
"Por fin apareces..." `DLG_OLIVER_MENUS_01`

**Oliver**
"Estoy recordando a todo el mundo como podemos abrir los menús del juego." `DLG_OLIVER_MENUS_02`

**Oliver**
"Cuando quieras abrir el MENÚ PRINCIPAL pulsa el botón de inicio" `DLG_OLIVER_MENUS_03`

**Oliver**
"Ahí podrás pausar el juego, volver al menú..." `DLG_OLIVER_MENUS_04`

**Oliver**
"ver tu inventario, y más cosillas que no te cuento para no enrollarme." `DLG_OLIVER_MENUS_05`

**Oliver**
"Por último cuando quieras ver las misiones que tienes pendiente puedes usar arriba en la cruceta." `DLG_OLIVER_MENUS_06`

**Oliver**
"Con el menú de misiones abierto podrás editarlas si pulsas de nuevo arriba." `DLG_OLIVER_MENUS_07`

**Oliver** — [sighs]
"Y ahora un descansito que menuda mañana..." `DLG_OLIVER_MENUS_08`

**Leonardo** — voz de tutorial, amable
"¿Quieres que te cuente cómo funcionan los menús?" `DLG_LEONARDO_01`

**Leonardo**
"Para abrir el menú de Pausa hazlo con el botón de inicio." `DLG_LEONARDO_02`

**Leonardo**
"Para abrir el menú de misiones pulsa dos veces arriba en la cruceta." `DLG_LEONARDO_03`

**Leonardo**
"Para abrir el panel de inventario pulsa arriba en la cruceta." `DLG_LEONARDO_04`

**Roberto** — voz mareada, agradecida
"Ay que mareo tengo..." `DLG_ROBERTO_01`

**Roberto**
"Si me traes una Poción de Vida te puedo dar algo que te va a gustar..." `DLG_ROBERTO_02`

**Roberto**
"Ojalá puedas conseguir una Poción de Vida para mi mareo." `DLG_ROBERTO_03`

**Roberto** — [happily]
"Oh Muchas gracias, ya me encuentro mucho mejor." `DLG_ROBERTO_04`

**Rudolfo** — voz brusca, cortante
"¿Vienes a molestar?" `DLG_RUDOLFO_COMPLETE_01`

**Rudolfo**
"No pienso darte nada más..." `DLG_RUDOLFO_COMPLETE_02`

**Rudolfo**
"Espero que te quedaran bien mis Botas. Chao." `DLG_RUDOLFO_COMPLETE_03`

**Sara** — línea única, tierna
"Te quiero." `DLG_SARA_01`

**Verónica** — voz misteriosa, casi de cuento de fantasmas contado en broma
"Hay una leyenda que dice..." `DLG_VERONICA_01`

**Verónica** — [drawn out]
"que si pronuncias mi nombre tres veces" `DLG_VERONICA_02`

**Verónica** — [whispers]
"delante de un espejo...." `DLG_VERONICA_03`

**Verónica** — [shouts] [surprised]
"¡Aparece un fantasma!" `DLG_VERONICA_04`

**Verónica** — [laughs]
"Mejor no lo pruebes..." `DLG_VERONICA_05`

**Victoria** — voz cariñosa de tendera, un poco pícara
"Así que te manda Eldran... en qué estará metido ahora..." `DLG_VICTORIA_01`

**Victoria** — [happily]
"Bueno cariño tu no te preocupes, que Victoria está aquí para ayudarte." `DLG_VICTORIA_02`

**Victoria** — [happily]
"¿Una capa de mago? Claro que sí, ahora mismo te la traigo." `DLG_VICTORIA_03`

**Victoria** — [softly]
"Aunque me da a mi que esta capa es para una futura promesa de mago, ¿no?" `DLG_VICTORIA_04`

**Victoria**
"Dale saludos a Eldran de mi parte." `DLG_VICTORIA_05`

**Victoria**
"Tengo la tienda cerrada. Vuelve más tarde." `DLG_VICTORIA_BEFORE`

---

# ESCENA: Batalla Final — el Mago Oscuro (el clímax del juego)

Esta es la escena más importante para doblar bien: el monólogo revelador del villano, el giro de Liam, el sacrificio y el epílogo. Tómate tu tiempo aquí.

**Mago Oscuro** — [booming] [slowly] (revelación larga, grave, casi solemne — es el villano diciéndole a Will quién es de verdad)
"¿Por qué crees que estás vivo, muchacho?
¿No te has preguntado nunca por qué no recuerdas nada de ti o tu pasado?
Te lo diré yo, no recuerdas nada de ti, ni de tu familia ni de tu pueblo
porque tú hace eones moriste luchando contra mí.
Tú moriste ese día. ¡Eres la reencarnación del único mago que logró detenerme!
Me obligaste a usar el super hechizo de destrucción, pero entonces no fui capaz de controlarlo.
Cuando todo escapó de mi alcance pude ligar mi alma al sendero
para cuando otro idiota superase las pruebas volver a pedir un deseo:
mi regreso a la vida.
Para que el hechizo no afectara a tu familia y estúpido pueblo
utilizaste toda la magia de tu interior para crear un hechizo protector
que pudo proteger a tu pueblo pero te destruyó a ti.
Lo que no entiendo es cómo después de haber muerto sigues aquí…" `MAGOOSCURO_MONOLOGUE`

**Will** — [surprised] [rushed] (el recuerdo golpea de golpe)
"Ahora... ahora lo recuerdo todo.
El hechizo de protección. Mis padres. Mi pueblo…
¿Pero qué hago aquí? ¿Cómo es posible? Y en este cuerpo…" `WILL_FLASHBACK_REVELATION`

**Narrador / Sistema** (aviso de mecánica de juego, no lo dice ningún personaje) — [booming]
"¡Hechizo Prohibido: Regresión Temporal!
Retrocede el tiempo... y esta vez, esquívalo." `TIME_SPELL_TUTORIAL`

**Liam** — [sorrowful] [softly] (últimas palabras)
"Cuida de mi hermano." `LIAM_LAST_WORDS`

**Will** — [sorrowful]
"Cura a su hermano. Es lo único que pido." `WILL_FINAL_WISH`

**Estela** — [shouts] [surprised]
"¡Will, ¿qué haces? Sal de ahí!" `ESTELA_PROTEST_PORTAL`

**Will** — [softly] [slowly]
"Tranquila, Estela. No te preocupes... Lo tengo todo calculado." `WILL_CALM_REPLY`

**Estela** — [sorrowful]
"Antes de destruir el Sendero, usó el hechizo prohibido de resurrección.
Por eso estás vivo. Por eso... ya no está." `ESTELA_EXPLAINS_EPILOGUE`

**Will** — [softly] [sorrowful]
"Cuidaos el uno al otro. Ya he vuelto a casa." `WILL_FAREWELL`

---

# ESCENA: La visión de Will (entre la vida y la muerte)

**Will** — [confused] [softly]
"¿Dónde estoy?" `WILL_VISION_WHERE_AM_I`

**Will** — [sorrowful]
"¿He recuperado la memoria… y estoy muerto?" `WILL_VISION_AM_I_DEAD`

**La Voz** (entidad de la visión — nota: un documento de trabajo reciente la describe como "el mago bueno del prólogo"; confirma el nombre/identidad exacta antes de grabar) — [softly] [drawn out]
"No, Will. Estás aquí, y allí.
Estás donde quieras estar.
Esto es solo tu poder, tu magia,
fruto del bien que has hecho a lo largo de tu vida." `VOICE_VISION_REASSURANCE`

**La Voz** — [softly]
"Tu magia, en el momento de tu muerte,
buscó por el tiempo y el espacio
un recipiente donde guardar tu alma.
Tu deseo de proteger a tu pueblo, a tu familia,
de acabar con la tiranía de aquel hombre,
te llevó hasta un chico enfermo
que, justo cuando tu alma llegó,
había dejado de respirar por un fallo en su corazón." `VOICE_VISION_EXPLANATION`

**Will** — [surprised] [softly]
"Entonces yo…" `WILL_VISION_THEN_I`

**La Voz** — [drawn out]
"Debes acabar con el Sendero.
No hay otra forma de evitar que magos como él
vuelvan a usar este lugar para su propio beneficio." `VOICE_VISION_MUST_END_SENDERO`

**Will** — [sorrowful]
"Destruir el Sendero… ¿Y Estela? ¿Y Liam?" `WILL_VISION_WHAT_ABOUT_FRIENDS`

**La Voz** — [softly]
"Debes protegerlos.
A ellos, y a toda la humanidad que vendrá después.
Solo así demostrarás, de una vez por todas, quién eres de verdad." `VOICE_VISION_PROTECT_THEM`

---

# ESCENA: Chuchelandia — la prueba de Estela (novela XXI)

Comedia. Fuente: novela, cap. XXI, recortada al ritmo del juego (los chistes con remate son cinemática). Textos en `cinematics_es.json` (`CHUCHELANDIA_*`), guiones en `Assets/_SEQUENCES/Guiones/Chuche_*.guion.txt`, lista para grabar en `docs/doblaje-sendero/chuchelandia_lineas.csv` y script `Tools/Audio/doblar_chuchelandia.py`. Sustituye a la escena 29 antigua (Duque de Regaliz), que ya no existe.

**Reparto nuevo** (casting en `docs/doblaje-sendero/casting/`):
- **Heraldo** — voz de juguete de cuerda: aguda, solemne, un poco metálica, muy seria con lo suyo. Casting «Heraldo».
- **Heraldo suplente** — la misma voz que el heraldo (son idénticos), más nerviosa y con prisa.
- **Condesa Merengue** — remilgada, precisa, muy educada incluso cuando riñe; vocaliza cada sílaba. Casting «Condesa Merengue».
- **Gominolas** — voces agudas y alegres; para «a coro», varias tomas superpuestas. Casting «Gominolas».
- **Su Alteza Pompón** — no habla: chapoteos felices (efecto de sonido).
- **Jueces** — no hablan: levantan cartelitos.

Sin voz (solo texto en pantalla): los cartelitos de los jueces, la pancarta, los títulos de las rondas (`TITULO_*`) y las pistas y avisos de lo jugable (`PUERTA_PISTA`, `TEQUIERO_PISTA_*`, `TEQUIERO_SOLO_WILL`).

## 1 · Llegada (cinemática con bocadillos)

La puerta rosa los suelta en el reino con una ráfaga de purpurina y una fanfarria que desafina en la última nota.

**Estela** — [dismayed] [flat]
"Oh, no." `CHUCHELANDIA_LLEGADA_01`

**Estela** — [horrified] [firm]
"No. De verdad que no." `CHUCHELANDIA_LLEGADA_02`

**Will** — [delighted] [dreamy]
"A mí me gusta." `CHUCHELANDIA_LLEGADA_03`

**Estela** — [deadpan] [annoyed]
"A ti te gusta todo lo que se pueda comer." `CHUCHELANDIA_LLEGADA_04`

## 2 · El pergamino del heraldo (cinemática con bocadillos)

Ante la Puerta del Corazón, el heraldo desenrolla un pergamino rosa que le llega a los pies. Lee con toda la solemnidad que le permite su voz de juguete de cuerda.

**Heraldo** — [wind-up toy voice] [solemn] [squeaky]
"¡Bienvenidos a Chuchelandia!" `CHUCHELANDIA_PERGAMINO_01`

**Heraldo** — [solemn] [reading a scroll] [squeaky]
"Por orden real, nadie abandonará el reino hasta que se abra la Puerta del Corazón." `CHUCHELANDIA_PERGAMINO_02`

**Heraldo** — [solemn] [proud] [squeaky]
"Y la Puerta del Corazón solo se abre ante la princesa más adorable del reino." `CHUCHELANDIA_PERGAMINO_03`

**Estela** — [threatening] [flat]
"Ni lo penséis." `CHUCHELANDIA_PERGAMINO_04`

**Will** — [innocent] [too quickly]
"Yo no he dicho nada." `CHUCHELANDIA_PERGAMINO_05`

**Estela** — [dry] [annoyed]
"Lo has pensado muy alto." `CHUCHELANDIA_PERGAMINO_06`

## 2 · Los intentos con el candado (jugable)

El fuego de Estela sale en purpurina y corazoncitos; suena un «¡tachán!» que no ha hecho nadie.

**Liam** — [intrigued] [dry]
"Interesante." `CHUCHELANDIA_PUERTA_LIAM_INTERESANTE`

**Estela** — [annoyed] [through gritted teeth]
"No es interesante." `CHUCHELANDIA_PUERTA_ESTELA_NO_INTERESANTE`

Si Estela intenta abrirlo con las manos:

**Estela** — [disgusted] [arms crossed]
"Ni pienso tocarlo." `CHUCHELANDIA_PUERTA_ESTELA_TOCAR`

La ganzúa de Liam: el candado escupe una piruleta.

**Liam** — [puzzled] [dry]
"Una piruleta." `CHUCHELANDIA_PUERTA_LIAM_PIRULETA_01`

**Liam** — [pleased] [deadpan]
"De fresa. Me la quedo." `CHUCHELANDIA_PUERTA_LIAM_PIRULETA_02`

Will empuja la puerta: el candado se sacude y suelta una risita.

**Will** — [offended] [confused]
"¿Se está riendo de mí?" `CHUCHELANDIA_PUERTA_WILL_RISITA`

**Liam** — [dry] [deadpan]
"Con motivo." `CHUCHELANDIA_PUERTA_LIAM_RISITA`

La pista para cambiar de personaje (`CHUCHELANDIA_PUERTA_PISTA`) es un aviso de texto: no se graba.

## 3 · ¡Una candidata! (cinemática con bocadillos)

El heraldo lo explica con cierta lástima. El ejército de gominolas sube a Estela a pulso y se la lleva en volandas, boca arriba y pataleando; a Will lo visten de caballero con orejitas de conejo y a Liam de paje con un corazón en la cabeza.

**Heraldo** — [sympathetic] [squeaky]
"Ni los caballeros ni los pajes pueden abrir la Puerta del Corazón." `CHUCHELANDIA_CANDIDATA_01`

**Heraldo** — [sympathetic] [softly] [squeaky]
"Solo una princesa." `CHUCHELANDIA_CANDIDATA_02`

**Estela** — [firm] [crossing arms]
"Aquí no hay ninguna princesa." `CHUCHELANDIA_CANDIDATA_03`

**Gominolas (a coro)** — [excited crowd] [cheering] [high-pitched]
"¡Una candidata! ¡Tenemos una candidata!" `CHUCHELANDIA_CANDIDATA_04`

**Estela** — [screaming] [kicking] [being lifted and carried away]
"¡Noooooo! ¡Dejadmeeee!" `CHUCHELANDIA_CANDIDATA_09`

**Estela** — [shouts] [furious] [being carried away]
"¡Idiota! ¡Haz algo!" `CHUCHELANDIA_CANDIDATA_05`

**Liam** — [warning] [mortified] [low]
"No digas nada." `CHUCHELANDIA_CANDIDATA_06`

**Will** — [holding back a laugh] [sincere]
"El corazoncito te favorece." `CHUCHELANDIA_CANDIDATA_07`

**Liam** — [cold] [deadpan]
"He dicho que no digas nada, orejitas." `CHUCHELANDIA_CANDIDATA_08`

## 3 · El vestidor real (cinemática con bocadillos)

Las primeras seis frases suenan desde dentro del vestidor, a través de la puerta (amortiguadas). La Condesa dirige el vestido.

**Condesa Merengue** — [prim] [bossy] [muffled, behind a door]
"¡Levante los brazos, alteza!" `CHUCHELANDIA_VESTIDOR_01`

**Estela** — [shouts] [muffled, behind a door]
"¡No soy alteza!" `CHUCHELANDIA_VESTIDOR_02`

**Condesa Merengue** — [prim] [excited] [muffled]
"¡Más volantes!" `CHUCHELANDIA_VESTIDOR_03`

**Estela** — [outraged] [muffled]
"¿¡Más!?" `CHUCHELANDIA_VESTIDOR_04`

**Condesa Merengue** — [prim] [triumphant] [muffled]
"¡El lazo! ¡Traed el lazo grande!" `CHUCHELANDIA_VESTIDOR_05`

**Estela** — [threatening] [muffled] [trailing off]
"Como me pongáis ese lazo…" `CHUCHELANDIA_VESTIDOR_06`

**Liam** — [deadpan] [quiet]
"Le han puesto el lazo." `CHUCHELANDIA_VESTIDOR_07`

**Liam** — [completely serious] [deadpan]
"Estás muy rosa." `CHUCHELANDIA_VESTIDOR_08`

**Estela** — [seething] [quiet threat]
"Una palabra más y os prendo fuego." `CHUCHELANDIA_VESTIDOR_09`

**Estela** — [deflated] [grumbling]
"Bueno. Os lleno de purpurina." `CHUCHELANDIA_VESTIDOR_10`

**Estela** — [pause] [with great rage]
"Pero con mucha rabia." `CHUCHELANDIA_VESTIDOR_11`

## 4 · Pruebas de candidata: carrera (bocadillos de juego)

Frases cortas mientras se juega. Estela corre con el vestido; Will rebota con la armadura; Liam camina muy digno con el cuerno.

**Estela** — [grumbling] [determined]
"Corro con un vestido de siete volantes. Que nadie diga nada." `CHUCHELANDIA_CARRERA_INICIO_01`

**Liam** — [dignified] [dry]
"Un paje no corre. Avanza con dignidad." `CHUCHELANDIA_CARRERA_INICIO_02`

**Will** — [excited] [bouncing]
"¡Esta armadura rebota sola!" `CHUCHELANDIA_CARRERA_INICIO_03`

**Will** — [teasing] [laughing]
"¡Te ha ganado una gominola!" `CHUCHELANDIA_CARRERA_REINTENTO_01`

**Liam** — [helpful] [dry]
"Ese lazo pesa, pero planea. Úsalo en los saltos largos." `CHUCHELANDIA_CARRERA_REINTENTO_02`

**Estela** — [determined] [annoyed]
"Otra vez. Y esta vez, sin piedad." `CHUCHELANDIA_CARRERA_REINTENTO_03`

**Estela** — [triumphant] [proud]
"¡Primera! Con volantes y todo." `CHUCHELANDIA_CARRERA_GANADA_01`

**Liam** — [dry] [amused]
"La gominola pide la revancha. Yo no se la daría." `CHUCHELANDIA_CARRERA_GANADA_02`

**Will** — [yelling] [flying through the air]
"¡Aaah! ¡No era por ahí!" `CHUCHELANDIA_CARRERA_WILL_REBOTA_01`

**Will** — [dazed] [cheerful]
"¡Estoy bien! ¡Creo que estoy en otra plataforma!" `CHUCHELANDIA_CARRERA_WILL_REBOTA_02`

**Liam** — [dignified] [slow]
"Correr con un corazón en la cabeza no es digno. Caminar, sí." `CHUCHELANDIA_CARRERA_LIAM_DIGNO_01`

## 4 · Pruebas de candidata: escalada (bocadillos de juego)

**Estela** — [skeptical] [dry]
"¿Trepar en vestido? Esto no lo enseña la Condesa." `CHUCHELANDIA_TORRE_INICIO_01`

**Will** — [indignant] [shouting up]
"¡Ha subido por la escalera! ¡Eso es trampa!" `CHUCHELANDIA_TORRE_REINTENTO_01`

**Liam** — [advising] [dry]
"Menos rabia en las manos y más en las piernas." `CHUCHELANDIA_TORRE_REINTENTO_02`

**Estela** — [smug] [satisfied]
"Desde aquí arriba sois todos muy pequeñitos. Me gusta." `CHUCHELANDIA_TORRE_GANADA_01`

## 4 · Pruebas de candidata: puntería (bocadillos de juego)

**Estela** — [resigned] [determined]
"Mi fuego sale en corazoncitos. Pues serán corazoncitos con puntería." `CHUCHELANDIA_TIRO_INICIO_01`

**Liam** — [intrigued] [dry]
"Interesante." `CHUCHELANDIA_TIRO_REINTENTO_01`

**Estela** — [annoyed] [warning]
"No es interesante. Otra vez." `CHUCHELANDIA_TIRO_REINTENTO_02`

**Will** — [cheering] [then correcting himself]
"¡Todos reventados! Bueno… todos llenos de corazones." `CHUCHELANDIA_TIRO_GANADA_01`

## 5 · Clases de la Condesa Merengue (tutorial del duelo)

La Condesa pronuncia cada palabra con el cuidado de quien coloca una guinda.

**Condesa Merengue** — [prim] [precise]
"Una princesa hace la reverencia así." `CHUCHELANDIA_CLASES_REVERENCIA_01`

**Condesa Merengue** — [prim] [patient]
"Otra vez. Con menos ganas de pelea." `CHUCHELANDIA_CLASES_REVERENCIA_02`

**Estela** — [defensive] [through gritted teeth]
"No tengo ganas de pelea." `CHUCHELANDIA_CLASES_REVERENCIA_03`

**Condesa Merengue** — [prim] [gently pointed]
"Alteza, está apretando los puños." `CHUCHELANDIA_CLASES_REVERENCIA_04`

**Condesa Merengue** — [prim] [dreamy]
"El saludo real. La muñeca gira con suavidad, como quien acaricia el aire." `CHUCHELANDIA_CLASES_SALUDO_01`

**Condesa Merengue** — [prim] [dry]
"Eso es espantar moscas, alteza." `CHUCHELANDIA_CLASES_SALUDO_02`

**Condesa Merengue** — [prim] [tiny crystal giggle]
"La risita. Jijí." `CHUCHELANDIA_CLASES_RISITA_01`

**Liam** — [writing notes] [dry] [quiet]
"Risita: peligrosa." `CHUCHELANDIA_CLASES_RISITA_02`

**Condesa Merengue** — [prim] [ceremonial]
"Por último, la princesa dedica unas palabras de cariño a sus súbditos." `CHUCHELANDIA_CLASES_CARINO_01`

**Estela** — [hesitant] [long pause]
"Sois…" `CHUCHELANDIA_CLASES_CARINO_02`

**Estela** — [awkward] [forced sweetness]
"Blanditos." `CHUCHELANDIA_CLASES_CARINO_03`

**Condesa Merengue** — [prim] [reluctant approval]
"Aceptable." `CHUCHELANDIA_CLASES_BIEN_01`

**Condesa Merengue** — [prim] [faint praise]
"Casi parecía una princesa. Casi." `CHUCHELANDIA_CLASES_BIEN_02`

**Condesa Merengue** — [prim] [sighs]
"Queda mucho trabajo por delante." `CHUCHELANDIA_CLASES_FIN`

## 6 · El reino libre (habitantes, al hablarles)

Gominolas del reino: voces agudas, alegres, un poco cotillas.

**Gominola** — [starstruck] [high-pitched]
"¡La candidata! ¿Me firma el lazo?" `CHUCHELANDIA_REINO_01`

**Gominola** — [admiring] [high-pitched]
"Su Alteza lleva cuatrocientos años ganando. Y sin despeinarse." `CHUCHELANDIA_REINO_02`

**Gominola** — [friendly] [warning]
"¿Ha probado el río de sirope? No, no se bañe. Se queda pegada." `CHUCHELANDIA_REINO_03`

**Gominola** — [curious] [honest]
"Aquí nadie se enfada. Bueno, usted. Usted es la primera." `CHUCHELANDIA_REINO_04`

**Gominola** — [gossiping] [whispers]
"Dicen que el heraldo se pone rosa cuando se emociona. Y luego… pop." `CHUCHELANDIA_REINO_05`

**Gominola** — [admiring] [dreamy]
"La Condesa Merengue nunca hace ruido al sentarse. Es un don." `CHUCHELANDIA_REINO_06`

**Gominola** — [serious] [helpful]
"Cuando suenen todas las trompetillas a la vez, apártese. Viene Su Alteza." `CHUCHELANDIA_REINO_07`

**Gominola** — [giggling] [shy]
"El caballero de gominola verde rebota muy bien. ¿Está soltero?" `CHUCHELANDIA_REINO_08`

## 6 · Misión opcional: las trompetillas perdidas

El heraldo suplente ya ronda por el reino, practicando anuncios por si acaso. Encarga la misión al hablarle; repite «¿Alguna trompetilla?» mientras sigue en curso. Las frases de recogida las dice quien vaya al mando al coger cada trompetilla.

**Heraldo suplente** — [nervous] [squeaky]
"Soy el suplente del heraldo. Por si acaso." `CHUCHELANDIA_TROMPETILLAS_ENCARGO_01`

**Heraldo suplente** — [worried] [squeaky]
"El ejército ha perdido sus trompetillas por todo el reino. ¿Me ayuda?" `CHUCHELANDIA_TROMPETILLAS_ENCARGO_02`

**Heraldo suplente** — [dramatic] [squeaky]
"Sin trompetillas no se puede anunciar a Su Alteza. Y eso sería… un drama." `CHUCHELANDIA_TROMPETILLAS_ENCARGO_03`

**Heraldo suplente** — [hopeful] [squeaky]
"¿Alguna trompetilla? Son doradas, brillan y hacen «tururú»." `CHUCHELANDIA_TROMPETILLAS_EN_CURSO`

**Will** — [pleased]
"¡Una trompetilla!" `CHUCHELANDIA_TROMPETILLAS_RECOGIDA_WILL`

**Liam** — [disgusted] [dry]
"Otra. Y está pegajosa." `CHUCHELANDIA_TROMPETILLAS_RECOGIDA_LIAM`

**Estela** — [curious] [suspicious]
"Si la soplo, ¿sale purpurina?" `CHUCHELANDIA_TROMPETILLAS_RECOGIDA_ESTELA`

**Will** — [cheerful]
"¡Ya están todas! Vamos a devolverlas." `CHUCHELANDIA_TROMPETILLAS_TODAS`

**Heraldo suplente** — [overjoyed] [squeaky]
"¡Todas! ¡Hasta la que tenía chicle!" `CHUCHELANDIA_TROMPETILLAS_ENTREGA_01`

**Heraldo suplente** — [grateful] [careful] [squeaky]
"Tome, alteza. Para la candidata más… decidida." `CHUCHELANDIA_TROMPETILLAS_ENTREGA_02`

**Estela** — [shrugs] [satisfied]
"¿Decidida? Me vale." `CHUCHELANDIA_TROMPETILLAS_ENTREGA_03`

Si se le vuelve a hablar después:

**Heraldo suplente** — [happy] [proud] [squeaky]
"Tururú. ¡Ya suenan todas! Gracias, alteza." `CHUCHELANDIA_TROMPETILLAS_DESPUES`

## 7 · Su Alteza Pompón (cinemática con bocadillos)

El heraldo se va hinchando y cambiando de color con cada «es un oso» hasta que explota en confeti. El suplente anuncia de espaldas a Estela, por si acaso. Pompón no habla: chapotea (efecto de sonido).

**Heraldo** — [proud] [booming] [squeaky]
"¡Su Alteza Pompón! ¡Princesa de Chuchelandia y campeona de ternura durante cuatrocientos años seguidos!" `CHUCHELANDIA_POMPON_01`

**Estela** — [confused] [flat]
"¿Eso es un oso?" `CHUCHELANDIA_POMPON_02`

**Heraldo** — [offended] [squeaky]
"Es la princesa." `CHUCHELANDIA_POMPON_03`

**Estela** — [flat] [certain]
"Es un oso." `CHUCHELANDIA_POMPON_04`

**Heraldo** — [tense] [squeaky]
"Es la princesa oso." `CHUCHELANDIA_POMPON_05`

**Estela** — [flat] [unimpressed]
"Es un oso con una tiara." `CHUCHELANDIA_POMPON_06`

**Heraldo** — [through gritted teeth] [slowly] [squeaky]
"Es. La. Princesa." `CHUCHELANDIA_POMPON_07`

**Estela** — [shrugs] [flat]
"Pues tiene cara de oso." `CHUCHELANDIA_POMPON_08`

**Estela** — [curious] [innocent]
"¿Le pasa algo?" `CHUCHELANDIA_POMPON_09`

**Condesa Merengue** — [prim] [polite] [tight smile]
"No le pasa nada, alteza." `CHUCHELANDIA_POMPON_10`

**Condesa Merengue** — [prim] [pointed] [pause]
"Es solo que usted es una candidata… complicada." `CHUCHELANDIA_POMPON_11`

**Estela** — [stubborn] [flat]
"Yo solo digo que es un oso." `CHUCHELANDIA_POMPON_12`

**Estela** — [matter-of-fact] [afterthought]
"Y bastante pegajoso." `CHUCHELANDIA_POMPON_13`

**Estela** — [defensive] [surprised]
"¿Qué? Yo no he hecho nada." `CHUCHELANDIA_POMPON_14`

**Condesa Merengue** — [prim] [reproachful] [brushing off confetti]
"Le ha llamado oso a Su Alteza. Cinco veces." `CHUCHELANDIA_POMPON_15`

**Liam** — [checking notes] [dry]
"Cinco." `CHUCHELANDIA_POMPON_16`

**Estela** — [exasperated] [shouts]
"¡Es que es un oso!" `CHUCHELANDIA_POMPON_17`

**Liam** — [deadpan] [instantly]
"Seis." `CHUCHELANDIA_POMPON_18`

**Condesa Merengue** — [prim] [weary] [calling out]
"¡Que pase el suplente!" `CHUCHELANDIA_POMPON_19`

**Gominola** — [resigned] [sweeping] [high-pitched]
"Ya van tres esta semana." `CHUCHELANDIA_POMPON_20`

**Heraldo suplente** — [whispers] [nervous] [squeaky]
"¿Es ella?" `CHUCHELANDIA_POMPON_21`

**Condesa Merengue** — [whispers] [grave]
"Es ella." `CHUCHELANDIA_POMPON_22`

**Heraldo suplente** — [nervous] [proud] [squeaky]
"¡Su Alteza Pompón! ¡Princesa de Chuchelandia!" `CHUCHELANDIA_POMPON_23`

**Estela** — [muffled] [mouth covered]
"¡Mmmf!" `CHUCHELANDIA_POMPON_24`

**Condesa Merengue** — [prim] [ceremonial]
"Para abrir la Puerta del Corazón, la candidata debe vencer a la princesa reinante en el Duelo de Ternura." `CHUCHELANDIA_POMPON_25`

**Estela** — [suspicious]
"¿Y si pierdo?" `CHUCHELANDIA_POMPON_26`

**Condesa Merengue** — [prim] [serene]
"Nadie ha vencido nunca a Su Alteza." `CHUCHELANDIA_POMPON_27`

**Estela** — [annoyed]
"Eso no responde a mi pregunta." `CHUCHELANDIA_POMPON_28`

**Liam** — [quietly] [dry]
"Sí que la responde." `CHUCHELANDIA_POMPON_29`

## 8 · El Duelo de Ternura (minijuego)

Anuncia el suplente. Will y Liam cuchichean en primera fila. Los cartelitos de los jueces (DUELO_CARTEL_*) son texto: no se graban.

**Heraldo suplente** — [announcing] [squeaky] [facing away]
"¡Primera prueba! ¡La reverencia!" `CHUCHELANDIA_DUELO_RONDA1`

**Heraldo suplente** — [announcing] [squeaky]
"¡Segunda prueba! ¡La canción de cuna!" `CHUCHELANDIA_DUELO_RONDA2`

**Heraldo suplente** — [announcing] [dramatic] [squeaky]
"¡Tercera prueba! ¡La prueba final! ¡Los ojitos!" `CHUCHELANDIA_DUELO_RONDA3`

**Heraldo suplente** — [announcing] [very excited] [squeaky]
"¡Y en la prueba final, cada corazón vale por cien!" `CHUCHELANDIA_DUELO_RONDA3_CIEN`

**Heraldo suplente** — [announcing] [squeaky]
"¡Su Alteza Pompón!" `CHUCHELANDIA_DUELO_TURNO_POMPON`

**Heraldo suplente** — [announcing] [nervous] [squeaky]
"¡La candidata!" `CHUCHELANDIA_DUELO_TURNO_CANDIDATA`

**Liam** — [flat] [unimpressed]
"Eso no tiene ningún sentido." `CHUCHELANDIA_DUELO_LIAM_SIN_SENTIDO`

**Gominola de fresa** — [cheerful] [matter-of-fact] [high-pitched]
"Es la tradición." `CHUCHELANDIA_DUELO_FRESA_TRADICION`

**Will** — [whispers] [hopeful]
"¿Cómo vamos?" `CHUCHELANDIA_DUELO_WILL_COMO_VAMOS`

**Liam** — [whispers] [checking notes] [dry]
"Sesenta a nueve." `CHUCHELANDIA_DUELO_LIAM_SESENTA_A_NUEVE`

**Will** — [whispers] [hopeful]
"¿A favor de quién?" `CHUCHELANDIA_DUELO_WILL_A_FAVOR`

**Liam** — [flat] [resigned]
"Perdemos." `CHUCHELANDIA_DUELO_LIAM_PERDEMOS`

## 9 · «¡TE QUIERO!» jugado (bocadillos, como Will)

Tras el duelo, Will tiene una idea: coge el mantel, moja el dedo en la fuente de sirope y se sube a la silla. Las pistas de cada paso (`TEQUIERO_PISTA_*`) y el aviso «Esto es cosa de Will» (`TEQUIERO_SOLO_WILL`) son texto: no se graban.

**Will** — [determined] [whispers]
"Aguanta, Estela. Tengo una idea." `CHUCHELANDIA_TEQUIERO_IDEA_01`

**Liam** — [worried] [deadpan]
"Eso me preocupa." `CHUCHELANDIA_TEQUIERO_IDEA_02`

**Will** — [satisfied] [quietly]
"Esto servirá." `CHUCHELANDIA_TEQUIERO_MANTEL`

**Will** — [concentrated] [proud]
"Con letras bien grandes." `CHUCHELANDIA_TEQUIERO_SIROPE`

**Liam** — [resigned] [dry]
"Sujeto la otra punta." `CHUCHELANDIA_TEQUIERO_SILLA`

## 9 · «¡TE QUIERO!» (cinemática de cine, con subtítulos)

Will y Liam caen a cámara lenta en la fuente de sirope y salen pegados bajo el mantel. La pancarta (TEQUIERO_PANCARTA*) es texto escrito en el mantel: no se graba. El ruidito de cerdito y la risa de Estela son efectos (Pig_-003 y Final_Estela_Risa).

**Will** — [shouts] [enthusiastic] [waving]
"¡Vamos, Estela!" `CHUCHELANDIA_TEQUIERO_01`

**Will** — [wobbling] [nervous] [bouncing]
"¡Uy, uy, uy…!" `CHUCHELANDIA_TEQUIERO_07`

**Liam** — [alarmed] [grabbing his arm]
"¡Will!" `CHUCHELANDIA_TEQUIERO_08`

**Gominola** — [shouts] [overjoyed] [high-pitched]
"¡Vivan los novios!" `CHUCHELANDIA_TEQUIERO_02`

**Liam** — [muffled, under a tablecloth] [deadpan]
"Esto no es lo que habíamos planeado." `CHUCHELANDIA_TEQUIERO_03`

**Will** — [sheepish] [dripping]
"No habíamos planeado nada." `CHUCHELANDIA_TEQUIERO_04`

**Liam** — [deadpan]
"Pues ya ves." `CHUCHELANDIA_TEQUIERO_05`

**Liam** — [quietly] [counting] [surprised]
"Ha ganado." `CHUCHELANDIA_TEQUIERO_06`

## 10 · Victoria y salida (cinemática con bocadillos)

Pompón se le pega a la pierna. Después, ya en la Puerta del Corazón, el vestido se deshace en purpurina y llegan las collejas.

**Will** — [amused] [warm]
"Creo que le caes bien." `CHUCHELANDIA_VICTORIA_01`

**Estela** — [quick] [embarrassed]
"Uno." `CHUCHELANDIA_VICTORIA_02`

**Estela** — [stern] [embarrassed]
"Y no se lo contéis a nadie." `CHUCHELANDIA_VICTORIA_03`

**Estela** — [softly] [gruff tenderness]
"Quédatela. A ti te queda mejor." `CHUCHELANDIA_VICTORIA_04`

**Liam** — [solemn] [teasing] [peeling caramel from hair]
"La hechicera más poderosa y orgullosa del grupo…" `CHUCHELANDIA_VICTORIA_05`

**Liam** — [solemn] [teasing]
"…coronada oficialmente como la princesa más adorable de Chuchelandia." `CHUCHELANDIA_VICTORIA_06`

**Will** — [teasing] [proud]
"Con récord de corazones." `CHUCHELANDIA_VICTORIA_07`

---

# Excluido del guion (no es diálogo hablado)

Para que no se cuele nada al copiar/pegar en ElevenLabs: se han dejado fuera del guion las 32 etiquetas `CHAR_*` (son solo el nombre del personaje para la UI, no una línea), los textos de `quests_es.json` (119 líneas — son descripciones de diario de misión en tercera persona, no las dice ningún personaje en pantalla) y los strings de `other_es.json` que son puramente de interfaz (contadores, prompts de botón, instrucciones de minijuego como `FORAGE_INSTRUCTION` o `MINIGAME_TAG_INSTRUCTION`). Si en algún momento quieres también voces para esas descripciones de misión (por ejemplo, para un modo de accesibilidad con narrador leyendo el diario), dímelo y te preparo esa parte aparte — tiene una voz distinta (narrador de UI, no personaje).
