# Catálogo de animaciones y caras

Rellenado el 17 de septiembre de 2026 a partir de la grabación de la escena
`Assets/Scenes/Test/CatalogoAnimaciones.unity` (Eldran, 72 animaciones + piezas de cara).

**La columna que importa es "Cuándo usarla".** Es la que se consulta al montar una secuencia,
para no tener que acordarse de los nombres del pack ni ir probando.

Regenerar la lista: *El Sendero ▸ Personajes ▸ Catálogo de animaciones*. Ojo — al regenerarla se
sobreescribe este fichero y se pierden las descripciones, así que antes hay que guardar una copia.

---

## Cómo leer la columna "Capa"

- **UpperBody** — solo mueve tronco y brazos. El personaje puede seguir andando por debajo.
  Es la capa de los gestos de diálogo, y la que usa el sistema de emociones.
- **Base Layer** — mueve el cuerpo entero, piernas incluidas. Congela la locomoción mientras dura.
  Para poses y acciones completas (sentarse, caer, celebrar), no para gesticular hablando.

---

## Las que más se van a usar

Si solo te quedas con esta tabla, ya cubres el 90% de las secuencias:

| Quiero que el personaje… | Usa |
|---|---|
| salude de lejos | `HandWave01` |
| salude de cerca, con reverencia | `Greeting01_NoWeapon` |
| diga que sí | `HeadNod01` |
| diga que no | `HeadShake01` |
| se extrañe, no entienda | `Question01` / `Question02` |
| se enfade | `Angry01` (largo) / `Angry02` (seco) |
| suplique, avise con urgencia | `Beg01` |
| se ría | `Laugh01` |
| se alegre, celebre | `Cheer01` / `Cheer02` |
| llore | `Cry01` |
| se plante, desafíe | `Challenging_NoWeapon` |
| note algo raro, se ponga en alerta | `SenseSomethingStart_NoWeapon` → `SenseSomethingSearching_NoWeapon` |
| señale o descubra algo | `FoundSomething_NoWeapon` |
| hable sin más | `Talk01` / `Talk02` / `Talk03` |
| dé las gracias, haga una reverencia | `Reverence01` |
| aplauda | `HandClap01` ⚠️ *(ver aviso al final)* |

---

## Animaciones

| # | Nombre real | Capa | Dura | Qué hace | Cuándo usarla |
|---|---|---|---|---|---|
| 1 | `Dance_NoWeapon` | Base | 2,7s | Paso de baile: se inclina y balancea los brazos de lado a lado | Fiesta, taberna, NPC celebrando |
| 2 | `DefendHit_NoWeapon` | Base | 0,3s | Retroceso corto con los puños arriba, como quien encaja un golpe con la guardia puesta | Combate, encajar un impacto sin caerse |
| 3 | `Defend_NoWeapon` | Base | 0,7s | Se cubre: puños delante del cuerpo, encogido | Protegerse, miedo físico, esperar un golpe |
| 4 | `Die02_NoWeapon` | Base | 1,0s | Se desploma al suelo boca abajo y se queda tirado | Muerte, desmayo |
| 5 | `Dizzy_NoWeapon` | Base | 1,3s | Se tambalea en el sitio, muy sutil | Aturdido, mareado tras un golpe |
| 6 | `Idle01` | Base | 2,7s | Idle de pie, quieto y relajado | Pose neutra por defecto |
| 7 | `Idle02` | Base | 2,7s | Idle de pie, variante con algo más de movimiento | Alternativa a `Idle01` para que no todos hagan lo mismo |
| 8 | `Idle03` | Base | 2,7s | Idle de pie, tercera variante | Ídem |
| 9 | `Idle_Battle_NoWeapon` | Base | 0,7s | Pose de combate: puños arriba, peso adelante | **La pose del despertar de Will.** Combate, determinación |
| 10 | `Idle_Normal_NoWeapon` | Base | 4,7s | Idle largo y tranquilo, brazos caídos | NPC esperando, pose de reposo larga |
| 11 | `InteractWithPeople_NoWeapon` | Base | 7,0s | Gesticula con las manos delante del pecho, conversación larga | Un NPC charlando de fondo, ambiente de pueblo |
| 12 | `LevelUp_NoWeapon` | Base | 2,3s | **Salta** con los brazos arriba y vuelve a caer | Subir de nivel, logro, alegría grande |
| 13 | `Pain01` | Base | 1,0s | Se encoge y se lleva las manos al cuerpo, dolor | Herido, duele algo |
| 14 | `RollBWD_Battle_RM_NoWeapon` | Base | 0,8s | Voltereta hacia atrás (lleva root motion) | Esquiva en combate |
| 15 | `RollFWD_Battle_RM_NoWeapon` | Base | 0,8s | Voltereta hacia delante (root motion) | Esquiva en combate |
| 16 | `RollLFT_Battle_RM_NoWeapon` | Base | 0,8s | Voltereta lateral (root motion) | Esquiva en combate |
| 17 | `SitGround_Begin` | Base | 1,1s | Se sienta en el suelo | Entrada a estar sentado en el suelo |
| 18 | `SitGround_Exit` | Base | 1,5s | Se levanta del suelo | Salida |
| 19 | `SitGround_Loop` | Base | 2,0s | Sentado en el suelo, quieto | Bucle de estar sentado en el suelo |
| 20 | `SitHigh_Begin` | Base | 0,7s | Se sienta en algo alto (taburete) | Entrada |
| 21 | `SitHigh_Exit` | Base | 0,9s | Se levanta de algo alto | Salida |
| 22 | `SitHigh_Loop` | Base | 2,0s | Sentado alto, piernas colgando | Taberna, banqueta |
| 23 | `SitLow_Begin` | Base | 1,0s | Se sienta en algo bajo | Entrada |
| 24 | `SitLow_Exit` | Base | 1,1s | Se levanta de algo bajo | Salida |
| 25 | `SitLow_Loop` | Base | 2,0s | Sentado bajo | Piedra, escalón |
| 26 | `SitMedium_Begin` | Base | 0,7s | Se sienta en algo de altura media | Entrada |
| 27 | `SitMedium_Exit` | Base | 0,9s | Se levanta | Salida |
| 28 | `SitMedium_Loop` | Base | 2,0s | Sentado normal (silla) | Silla, mesa |
| 29 | `Sleeping_NoWeapon` | Base | 1,3s | **Tumbado de lado en el suelo, durmiendo** | Dormir, inconsciente |
| 30 | `TakeDamage` | Base | 0,5s | Sacudida corta al recibir daño | Impacto |
| 31 | `TakeDamage_2` | Base | 0,5s | Variante del anterior, con más giro de tronco | Impacto, alternativa |
| 32 | `Victory_NoWeapon` | Base | 1,7s | Celebra con los brazos, postura abierta | Ganar un combate, fin de misión |
| 33 | `Angry01` | UpperBody | 2,9s | Enfado largo: aprieta los puños, se cierra, gesticula | Bronca, discusión que dura |
| 34 | `Angry02` | UpperBody | 1,4s | Enfado seco y corto, gesto brusco | Un "¡basta!", una réplica cortante |
| 35 | `Beg01` | UpperBody | 4,4s | **Manos juntas al pecho, suplicando**, insistente | Rogar, avisar con urgencia, miedo. *Sustituye a `Fear01`* |
| 36 | `CarryMoveIdle_NoWeapon` | UpperBody | 0,9s | Brazos doblados delante, como sujetando algo | Llevar una caja o un bulto |
| 37 | `CarryStart_NoWeapon` | UpperBody | 1,0s | Coge algo del suelo y lo sube | Recoger un objeto |
| 38 | `CarryThrow_NoWeapon` | UpperBody | 1,0s | Lanza lo que llevaba hacia delante | Soltar o tirar un objeto |
| 39 | `Challenging_NoWeapon` | UpperBody | 3,0s | Se planta, saca pecho y hace un gesto retador con la mano | **Determinación.** Desafiar, "ven aquí", plantarse ante alguien |
| 40 | `Cheer01` | UpperBody | 1,3s | Puño al aire, alegría contenida | Alegrarse, animar |
| 41 | `Cheer02` | UpperBody | 0,9s | Los dos brazos arriba, alegría más explosiva | Emoción, entusiasmo |
| 42 | `Cry01` | UpperBody | 2,7s | Se lleva las manos a la cara y se encoge, llorando | Tristeza, llanto |
| 43 | `DrinkPotion_NoWeapon` | UpperBody | 2,3s | Se lleva algo a la boca y bebe | Beber, tomar una poción |
| 44 | `Eat_Begin` | UpperBody | 2,4s | Se lleva comida a la boca | Empezar a comer |
| 45 | `Eat_Loop` | UpperBody | 2,0s | Masticando, movimiento corto repetido | Bucle de comer |
| 46 | `Fear01` | UpperBody | 1,3s | Se echa atrás con la cabeza | ⛔ **NO USAR.** Retirada del juego (INC-223): el retroceso de cabeza rompe el cuello en estos cuerpos. Usa `Beg01` o `Defend_NoWeapon` |
| 47 | `FoundSomething_NoWeapon` | UpperBody | 2,3s | **Señala hacia delante** y se inclina, como quien descubre algo | Señalar, "¡mira!", encontrar un objeto |
| 48 | `Greeting01_NoWeapon` | UpperBody | 1,7s | Saludo cortés con una inclinación y la mano | Saludar de cerca, recibir a alguien |
| 49 | `HandClap01` | UpperBody | 2,4s | Aplaude | Aplaudir, felicitar. ⚠️ *Ver aviso al final* |
| 50 | `HandWave01` | UpperBody | 2,5s | **Saluda con el brazo en alto**, movimiento amplio | Saludar de lejos, despedirse |
| 51 | `HandWave02` | UpperBody | 3,2s | Saludo más largo con los dos brazos, más aspaviento | Llamar la atención de alguien lejos, "¡eh, aquí!" |
| 52 | `HeadNod01` | UpperBody | 1,1s | Asiente con la cabeza | **Decir que sí**, aprobar |
| 53 | `HeadShake01` | UpperBody | 1,5s | Niega con la cabeza | **Decir que no**, negar |
| 54 | `HeadShake02` | UpperBody | 1,8s | Niega con más énfasis, acompañando con la mano | Negar rotundo, "de eso nada" |
| 55 | `HumanM@MagicAttackCall1H01_L` | UpperBody | 1,4s | Extiende un brazo hacia delante lanzando algo | Lanzar un hechizo con una mano |
| 56 | `HumanM@MagicAttackCall1H01_L - Load` | UpperBody | 1,7s | Carga: recoge el brazo antes de lanzar | Preparación del anterior |
| 57 | `HumanM@MagicAttackDirect2H01 - Cast` | UpperBody | 1,0s | Empuja con las dos manos hacia delante | Hechizo a dos manos, disparo |
| 58 | `HumanM@MagicAttackDirect2H01 - Load` | UpperBody | 1,7s | Junta las manos y las recoge, cargando | Preparación del anterior |
| 59 | `HumanM@MagicAttackOmni01 - Cast` | UpperBody | 1,3s | Abre los dos brazos en cruz | Hechizo en área, conjuro grande |
| 60 | `IdleWounded01` | UpperBody | 2,7s | Encogido, brazo al costado, respirando mal | **Herido, cansado, agotado** |
| 61 | `Laugh01` | UpperBody | 2,6s | Echa la cabeza atrás y se ríe sujetándose | Reír, burlarse |
| 62 | `MagicLeft` | UpperBody | 1,2s | Lanza con la mano izquierda | Hechizo por la izquierda (el del juego) |
| 63 | `MagicRight` | UpperBody | 1,2s | Lanza con la mano derecha | **La que usa Will para su magia** |
| 64 | `MagicSpecial` | UpperBody | 1,3s | Gesto más aparatoso con las dos manos | Hechizo especial |
| 65 | `Question01` | UpperBody | 2,1s | Abre una mano hacia delante, extrañado | **Duda, "¿y eso?"**, pensar |
| 66 | `Question02` | UpperBody | 2,6s | Encoge los hombros con las dos manos abiertas | **"No sé", confusión**, desconcierto |
| 67 | `Reverence01` | UpperBody | 2,3s | **Se inclina en reverencia** | Agradecer, respeto, despedida formal |
| 68 | `SenseSomethingSearching_NoWeapon` | UpperBody | 4,0s | Mira alrededor buscando, tenso | Buscar, estar en guardia, "¿quién anda ahí?" |
| 69 | `SenseSomethingStart_NoWeapon` | UpperBody | 0,4s | Sobresalto corto: se pone alerta de golpe | **Sorpresa**, notar algo. Encadena bien con el anterior |
| 70 | `Talk01` | UpperBody | 2,3s | Gesticula al hablar, una mano | Hablar, variante neutra |
| 71 | `Talk02` | UpperBody | 2,4s | Gesticula al hablar, movimiento más contenido | Hablar, variante neutra |
| 72 | `Talk03` | UpperBody | 3,0s | Gesticula al hablar, más amplio | Hablar, variante neutra |

---

## Ojos

Vistos en Eldran. Son piezas intercambiables, no animaciones: se activan y se desactivan.

| Nombre real | Qué expresa | Emoción a la que pega |
|---|---|---|
| `Eye01` | Óvalos grandes y limpios, mirada neutra | Neutral |
| `Eye02` | Óvalos con la parte de arriba recta, algo más serio | Neutral serio |
| `Eye03` | Curvados hacia arriba, sonrientes | Feliz, agradecido, aliviado |
| `Eye04` | Ceño marcado, párpado recto y bajo | **Enfadado**, molesto, decidido |
| `Eye05` | Pupila pequeña con ceja marcada arriba | **Sorprendido**, confuso, emocionado |
| `Eye06` | Ojos muy abiertos, pupila redonda y blanca | Susto, asombro grande |
| `Eye07` | Curva hacia abajo, párpados caídos | **Pensativo, cansado, preocupado** |
| `Eye08` | Dos curvas finas, ojos cerrados sonriendo | Contento, ojos cerrados |
| `Eye09` | Gotas grandes, mirada baja | **Triste, asustado** |
| `Eye10` | Dos rayas rectas horizontales | Ojos cerrados, dormido, resignado |
| `Eye11` | **Corazones rosas** | Enamorado, encantado (gag) |
| `Eye12` | Fondo negro con pupila **roja** | Poseído, maligno, villano |
| `Eyebrow01` | Cejas visibles | Complemento de expresión |
| `Eyebrow02` | Sin cejas | Complemento de expresión |

---

## Bocas

**Pendiente.** En la grabación no se ven: Eldran tiene barba y le tapa la boca entera, y además la
cámara se quedó demasiado cerca. Hay que repetir esta parte con un personaje **sin barba**.

Cómo hacerlo, ya arreglado: selecciona en el Project el prefab de un personaje sin barba (Will o
cualquier NPC joven) y ejecuta *El Sendero ▸ Personajes ▸ Catálogo de animaciones*. La herramienta
te preguntará con cuál lo monta. La distancia de los primeros planos también se ha corregido: antes
la cámara se metía dentro de la cabeza.

Piezas a catalogar: `Mouth01` … `Mouth12`.

---

## Avisos

**`HandClap01` no aplaude bien ahora mismo.** Las manos no llegan a juntarse. No es la animación:
es el recorte de brazos del avatar (INC-224). Está en el 50%, y aplaudir necesita que los brazos
crucen hacia el centro del cuerpo. Subiéndolo al 80–85% en *El Sendero ▸ Personajes ▸ Retargeting
del avatar* debería recuperarse; el recorte de cuello y cabeza (20%), que es el que arregló el
problema serio, no hay que tocarlo.

Es un compromiso real, no un bug: cuanto más se recorta el brazo, menos se mete la mano en el
cuerpo, pero menos llegan las manos a juntarse. El punto bueno se busca con el probador de esa
misma ventana.

**Las de locomoción no están catalogadas** porque con el personaje quieto no enseñan nada:
`Free Locomotion`, `Strafing Movement`, `RollRGT_Battle_RM_NoWeapon`. Y `New State` y `UpperIdle`
no tienen animación asignada — son estados vacíos del controller.
