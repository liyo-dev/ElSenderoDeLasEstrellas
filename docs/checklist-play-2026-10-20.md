# Checklist de validación en Play — tanda de auditoría y combate

**Fecha:** 20 de octubre de 2026.

**Resultado (20 oct 2026):** validado. Los 24 INC quedan resueltos en `TRACKER.md`: 22 en Play, INC-756 en el build del Player e INC-786 por compilación (no hay ningún `PortalTrigger` con cambio de escena montado en las escenas actuales).

**Alcance:** incidencias de la tanda del 13–20 de octubre: auditoría de código (INC-786…790), combate, hechizos, jefes e interacción (INC-733, 735, 739, 741–744, 746, 749–751, 754–760), build del Player (INC-756) y, como opcional, la cinemática «La mañana después» (INC-752). No cubre el backlog histórico de prólogo y cinemáticas ni las incidencias de contenido: esas siguen en `TRACKER.md`.

**Cómo usarlo:** marca `[x]` solo cuando hayas visto lo que dice **OK**. Si algo falla, deja la casilla vacía, anota el INC y pega la consola. El LAB tiene «Copiar datos para el reporte» en la pestaña **Ayuda**.

---

## 0. Preparación

- [ ] Abrir `Assets/Scenes/Systems/Start.unity` y dar Play. Si pruebas otra escena, `AutoBootstrapOnPlay` carga `Start` de forma aditiva.
- [ ] Confirmar en `Logs/Editor.log` que no hay `error CS` antes de empezar.
- [ ] Limpiar la consola del Editor y dejarla visible.
- [ ] Para combate, hechizos y jefes: abrir `Assets/Scenes/Test/Lab.unity` → Play → `Tab`/`Select` para el panel. La pestaña **Ayuda** tiene los controles y el botón de reporte.
- [ ] Para mundo, diálogo y quests: cargar una partida de Cap1/Cap2 (o empezar nueva). No usar el LAB para esas pruebas.
- [ ] Si pruebas un build, anotar la versión y la plataforma.

---

## 1. Mundo, diálogo y quests (partida normal)

### INC-786 — Portal o puerta con cambio de escena (Media)

> **Nota de montaje:** hoy no hay ningún `PortalTrigger` con cambio de escena montado en las escenas; las puertas de interior usan `InteriorPortalTrigger`. Si no tienes un portal a mano, prueba la puerta de interior para la sensación de llegada y marca este caso como «no aplica / validado por compilación».

- [ ] Entra en una puerta o portal que cargue escena. Repite una vez sin tocar nada y otra pulsando salto justo antes de entrar.
- **OK:** apareces quieto, sin salto pendiente ni deriva; la cámara no da un tirón y puedes moverte al instante.
- **Falla:** el personaje sigue andando solo, salta al aparecer, se queda bloqueado o la cámara se queda girada.

### INC-787 — La boca se mueve al hablar (Media)

- [ ] Habla con Eldrán, Tomasa o cualquier NPC con diálogo; deja correr una línea y cierra el diálogo. Repite abriendo y cerrando rápido.
- **OK:** la boca se mueve solo mientras habla y se detiene al cerrar; no hay excepciones ni avisos de parámetro ausente.
- **Falla:** boca congelada, sigue moviéndose al cerrar o aparece una excepción en consola.

### INC-788 — El grafo narrativo sigue igual y conserva sus trazas en el Editor (Media)

- [ ] Juega una secuencia con nodos del grafo: ofrecer o avanzar una quest, hablar, entrar en combate con señal y completar un paso.
- **OK:** el grafo se comporta igual que antes; en el Editor siguen apareciendo las trazas de diagnóstico del grafo; no hay errores nuevos.
- **Falla:** un nodo deja de dispararse, una señal no llega o desaparecen las trazas que necesitas para depurar en el Editor.
- **Nota:** en un build final las trazas desaparecen a propósito; eso se comprueba en el bloque 6.

### INC-790 — Quest con `requiredPartyMembers` al unirse un compañero (Baja)

- [ ] Carga una partida con `Q_ELDRAN_MISSION6` activa (requiere a Estela, paso `QUEST_ELDRAN_MISSION6_STEP_01`) o un Quick Test al nodo equivalente. Haz que Estela se una y después habla con Eldrán.
- **OK:** la consola no muestra «No se encontró QuestChainEntry para quest …»; al hablar con Eldrán, el paso se completa.
- **Falla:** el paso no avanza o aparece el aviso de entrada no encontrada.
- **Si no tienes esa partida a mano:** añade a Estela en el LAB (pestaña **Grupo**) y comprueba que no aparecen errores; marca el caso exacto como «no probado». El requisito tiene `completeOnlyOnNpcTalk`, así que el paso se cierra al hablar con Eldrán, no al unirse.

### INC-735 — La A de Tomasa detrás del puesto (Alta, opcional)

- [ ] Acércate al puesto de Tomasa de frente; pulsa A. Aléjate y comprueba que el aviso se apaga. Prueba también desde detrás de una pared.
- **OK:** aparece la A, abre la tienda, se apaga al alejarse y no atraviesa paredes.
- **Falla:** no aparece, aparece a través del mostrador o de la pared, o se queda encendida.

### INC-754 — La A no se consume al interactuar con compañeros (Alta)

- [ ] Con Estela o Liam cerca de Will, teclea un combo que use A y salta con A.
- **OK:** no aparece el icono de interacción sobre el compañero; A hace saltar o continúa el combo, no abre diálogo.
- **Falla:** la A se consume, aparece el icono sobre el compañero o se abre una interacción.

---

## 2. LAB — hechizos

**Preparación:** `Lab.unity` → Play → `Tab` → pestaña **Magia** para Will. Para Estela y Liam, cambia de personaje en la zona **Pruebas** (este) o usa una partida guardada; el panel de Magia equipa los básicos de Will.

### INC-757 — Hechizos de área apoyados en el suelo (Alta)

- [ ] Lanza Meteoro y Cúpula Estelar (y otra habilidad de zona) en suelo llano, en una rampa y cerca de un borde.
- **OK:** el área nace apoyada en el suelo, no a la altura de las manos ni flotando; el centro cae donde apuntas.
- **Falla:** flota, se queda en la mano, se mete en una rampa o aparece a la altura de la cabeza.

### INC-758 — Hechizo con carga dirigido al objetivo fijado (Alta)

- [ ] Deja que la cámara de combate fije a un enemigo (lock-on) y lanza Amanecer con carga. Repite con el enemigo en movimiento y con el cuerpo girado.
- **OK:** el proyectil sale hacia el enemigo fijado, no hacia donde miraba el cuerpo al empezar la carga; el cuerpo se gira al iniciar el gesto.
- **Falla:** sale recto hacia delante, se queda corto o ignora el lock-on.

### INC-760 — Amanecer: carga, proyectil e impacto (Alta)

- [ ] En **Magia**, aprende Amanecer (casilla) y ejecuta el combo `Y → X A B Y`; mantén la carga y lánzalo contra un enemigo.
- **OK:** runa en el suelo, rayos convergentes, sol creciente en las manos, proyectil grande con estela y luz, impacto con pilar, onda, sacudida, destello y micro-hitstop; sin tirones ni excepciones.
- **Falla:** falta una fase, el VFX es invisible, tapa la cámara o hay caída de FPS o error.

### INC-742 — Ráfaga y Chispa Ígnea de Estela (Alta)

- [ ] Con Estela, lanza Ráfaga y Chispa Ígnea; compara con Bola de Fuego y Ciclón.
- **OK:** ambos se ven durante todo el vuelo, con núcleo y estela propios, impactan y desaparecen; Bola de Fuego y Ciclón siguen igual.
- **Falla:** desaparecen a mitad de vuelo, se ven como polvo o el impacto no se lee.

### INC-743 — Los cuatro básicos de Liam (Alta)

- [ ] Con Liam, lanza Aura Estelar, Dardo Mental, Garra del Pacto y Eco.
- **OK:** Aura Estelar es una esfera cósmica con anillo orbital; los otros tres se distinguen entre sí; ninguno desaparece ni atraviesa el suelo.
- **Falla:** Aura Estelar sigue plana, los proyectiles se confunden o el destello de manos tapa la pantalla.

### INC-744 — Estrella Fugaz, Lluvia de Chispas y Bola Prisma de Will (Alta)

- [ ] Con Will, lanza Estrella Fugaz, Lluvia de Chispas y Bola Prisma; compara con Llama Astral.
- **OK:** Estrella Fugaz no se hunde y sale elevada; las tres estrellas de Lluvia se distinguen y cubren su radio; Bola Prisma recupera su tamaño y efecto; Llama Astral no cambia.
- **Falla:** se hunde, las estrellas parecen chispas, Bola Prisma sigue pequeña o Llama Astral cambió.

---

## 3. LAB — jefes

**Preparación:** `Lab.unity` → **Zona de jefes** (oeste, por el pasillo del plataformeo) → portal Demonio 1, Demonio 2 o Mago Oscuro. Usa la pestaña **Grupo** para añadir compañeros y **Trucos** para invencible o maná infinito si hace falta.

### INC-750 — El Demonio persigue a cualquier distancia (Alta)

- [ ] En Demonio 1 y Demonio 2, aléjate a más de 20 m, espera a que termine una animación y comprueba que vuelve a perseguirte. Provoca que se acerque a un borde o obstáculo y espera a que rodee.
- **OK:** no se queda parado al salir de detección; actualiza la ruta, no se atasca y rodea mientras espera.
- **Falla:** se queda quieto a distancia, vibra contra un borde o deja de atacar.

### INC-749 — Lluvia de esferas y plasma del Demonio 1 (Alta)

- [ ] Demonio 1: espera la lluvia de esferas; comprueba el impacto en el suelo y el daño cerca de la marca (no solo encima). Comprueba también el plasma directo.
- **OK:** la esfera toca el suelo y explota; el daño de área alcanza el radio; el plasma explota al impactar.
- **Falla:** la esfera se hunde, no hay explosión, el daño solo se aplica en el centro o el plasma no explota.

### INC-751 — Carga de plasma del Demonio 2 (Media)

- [ ] Demonio 2, fase 2 o 3: espera la carga ocasional de plasma (esfera fija en el suelo).
- **OK:** cae en la posición de Will, cambia azul → amarillo → rojo durante unos 2,1 s, da tiempo a salir, explota y daña dentro del radio, no fuera; el abanico y la lluvia de sombras siguen funcionando.
- **Falla:** explota al caer, no cambia de color, no da tiempo, daña fuera del radio o sustituye también el abanico en fase 1.

### INC-733 — Lluvia de sombras de la fase 3 (Alta)

- [ ] Demonio 2, fase 3: espera la lluvia de sombras y observa las dos oleadas.
- **OK:** las sombras caen desde arriba durante unos 0,65 s, se ven las marcas, el daño solo se aplica al aterrizar y se limpian entre oleadas y al morir o en la revancha.
- **Falla:** las esferas aparecen ya en el suelo, no caen, el daño se aplica antes o quedan VFX tras morir.

### INC-789 — VFX del hechizo del Imp (Media)

- [ ] Demonio (ImpDemonAI): provoca el ataque de hechizo a media distancia y mira el efecto sobre el jugador.
- **OK:** el efecto se ve en la posición del jugador, dura unos 3 s y desaparece solo; no se acumula en la jerarquía.
- **Falla:** no se ve, se queda pegado, se acumula o lanza una excepción.

---

## 4. LAB — ataques de equipo

**Preparación:** pestaña **Grupo** → `+ Estela`, `+ Liam` o `Los tres`. Si no tienes carga, en **Magia** pulsa «Recargar maná y carga de equipo». Lanza con `LT+RT` (mando) o `Ctrl` (teclado) cerca de un compañero.

### INC-739 — LT+RT dispara el dúo o el trío (Alta)

- [ ] Con Estela cerca, lanza el dúo; repite con Liam y con los tres. Cambia de personaje y repite un dúo.
- **OK:** LT+RT dispara el ataque (no solo llena la barra), se gasta la carga, se ve la puesta en escena y el lanzador es el personaje actual.
- **Falla:** no pasa nada, no se gasta la carga o el ataque sale con el personaje anterior.

### INC-741 — Puesta en escena, audio y VFX de los dúos y el trío (Alta)

- [ ] Durante los ataques anteriores, mira la entrada cinematográfica (~1,5 s), las bandas, el cut-in, la coreografía, la cámara, la música y los VFX.
- **OK:** cada dúo y el trío tienen su puesta en escena, audio y efectos; el trío ya no lleva coro celestial.
- **Falla:** falta una fase, la cámara se atasca, el audio no cuadra o un VFX tapa la acción.

### INC-746 — Coreografías v5 (Alta)

- [ ] Repite el dúo de Estela, el de Liam y el trío; mira los momentos aéreos y las poses finales.
- **OK:** no hay flexiones ni rebotes en el aire; Liam canaliza con las dos manos; el trío usa posturas de combate firmes; las caras acompañan.
- **Falla:** rebote, pose festiva, brazos congelados o cara fuera de contexto.

### INC-755 — Will no se queda en «Falling» tras el trío (Alta)

- [ ] Termina el trío y el dúo de Estela en suelo llano y en una rampa; espera a que vuelva el control.
- **OK:** Will vuelve a locomoción, se mueve y salta con normalidad; no se queda en «Falling» ni flotando.
- **Falla:** se queda en la pose de caída, flota, no responde o vuelve con la capa 1 apagada.

---

## 5. Batalla Final

### INC-759 — Mago Oscuro: cámara, arena, cristales y tutorial (Alta)

- [ ] Abre `Assets/Scenes/Test/BatallaFinal.unity` o el LAB → Mago Oscuro. Comprueba:
  - [ ] el Mago vuela bajo (~2 m) y la cámara no choca contra el suelo;
  - [ ] no quedan plataformas flotantes ni trampolines;
  - [ ] los cristales se conectan con el escudo mediante haces púrpura que pulsan al golpear;
  - [ ] al destruir todos los cristales, el escudo cae, el Mago cae derribado y queda vulnerable;
  - [ ] Estela no lanza un bocadillo gigante de tutorial.
- **OK:** la mecánica se entiende sin texto, la cámara no se atasca y el combate se puede completar.
- **Falla:** cámara al suelo, el Mago vuela alto, quedan plataformas, los cristales no se conectan, el escudo no cae o el bocadillo tapa.
- **Si algo no cuadra:** regenerar con `El Sendero ▸ Combate ▸ Crear o regenerar la Batalla Final (prueba)` y repetir.

---

## 6. Build del Player

### INC-756 — El build deja de fallar por `SolForzado` (Alta)

- [ ] `File → Build Profiles → Windows → Build` (sin instrumentación). Espera a que termine.
- **OK:** no aparece `CS0103` por `SolForzado._ultimaCamara`; el Player arranca y una secuencia con cámara forzada se ve bien.
- [ ] **Opcional (INC-788):** en el Player, el log no muestra las trazas de diagnóstico del grafo.
- **Falla:** error de compilación, el build no arranca o faltan cosas en la escena.

---

## 7. Cinemática «La mañana después» (opcional)

### INC-752 — La mañana después (Alta, opcional)

- [ ] Quick Test del Cap2 → «La mañana después» (o carga la escena correspondiente). Mira:
  - [ ] Will camina moviendo las piernas;
  - [ ] Eldrán lleva el pan con las dos manos y no atraviesa la mesa;
  - [ ] Will no se hunde ni parpadea en la cama;
  - [ ] el plano de la ventana es limpio;
  - [ ] la música cuadra con la escena.
- **OK:** todo lo anterior.
- **Falla:** patina, atraviesa, parpadea o hay desajuste de música.

---

## 8. Regresión mínima (15 minutos)

- [ ] Cargar partida de Cap1/Cap2: reanuda en el nodo correcto y no repite el prólogo.
- [ ] Nueva partida: arranca el prólogo, primeras interacciones y HUD.
- [ ] Guardar y cargar: posición, quests, grupo y equipo.
- [ ] Entrar y salir de un interior: sin deriva, la música vuelve y la puerta funciona.
- [ ] Combate normal: ataque, hechizo, escudo, combo y ataque de equipo.
- [ ] Consola: solo avisos conocidos; ningún `NullReferenceException` o `MissingReferenceException` nuevo.
- [ ] Cerrar y volver a abrir el Editor: sin errores de importación.

---

## 9. Resultados

| INC | Bloque | Resultado | Notas |
|---|---|---|---|
| INC-786 | 1 |  |  |
| INC-787 | 1 |  |  |
| INC-788 | 1 |  |  |
| INC-790 | 1 |  |  |
| INC-735 | 1 |  |  |
| INC-754 | 1 |  |  |
| INC-757 | 2 |  |  |
| INC-758 | 2 |  |  |
| INC-760 | 2 |  |  |
| INC-742 | 2 |  |  |
| INC-743 | 2 |  |  |
| INC-744 | 2 |  |  |
| INC-750 | 3 |  |  |
| INC-749 | 3 |  |  |
| INC-751 | 3 |  |  |
| INC-733 | 3 |  |  |
| INC-789 | 3 |  |  |
| INC-739 | 4 |  |  |
| INC-741 | 4 |  |  |
| INC-746 | 4 |  |  |
| INC-755 | 4 |  |  |
| INC-759 | 5 |  |  |
| INC-756 | 6 |  |  |
| INC-752 | 7 |  |  |

**Resultado:** `OK`, `Falla`, `No probado` o `No aplica`.

---

## 10. Cierre

- **Todo OK:** actualizar en `TRACKER.md` el estado de cada fila a `Resuelto (validado en Play)` y anotar la fecha en la columna Validación. No cambiar la prioridad.
- **Algo falla:** dejar la fila como está, añadir el síntoma y el error de consola; si hace falta, abrir una incidencia nueva y enlazarla.
- **Pendiente por falta de montaje:** anotar `No probado` y el motivo (por ejemplo, INC-786 sin portal montado).
- Los cambios de código de esta sesión siguen sin commit; este checklist no modifica el estado por sí solo.
