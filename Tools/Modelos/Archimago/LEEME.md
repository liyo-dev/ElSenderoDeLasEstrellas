# Archimago: diseño propio (12 nov 2026)

Elegido por Raúl: **adulto maduro sin barba larga**, **crema y oro cálido**, **túnica larga abierta con capa corta, sin sombrero ni bastón**.
Es un personaje distinto del Mago Oscuro (cara y diseño aparte) y de Will (lleva su alma, pero no se le parece). **Estado: ya está en Unity (INC-1131), sin probar con luz ni en movimiento.**

## Piezas (`para_unity/`, no están en Assets)
- `archimago.py` (Blender 5.x, bpy; argumentos: base.blend y salida) → `Body34_Archimago`: manos y torso de Body05 con la ropa encima, sobre el esqueleto de RPG Tiny Hero.
  Túnica interior cálida larga hasta media pantorrilla; sobretúnica crema abierta por delante con ribete de oro en los bordes y en el bajo; faja celeste con nudo y cabo; capa corta hasta la cintura (crema por fuera, celeste por dentro, ribete de oro), que cubre solo la espalda (de 32° a 148°) y va pesada al tronco (`spine_03`/`spine_02`), no a los brazos, para que no se meta en las mangas (la primera versión, más ancha y con los pesos del cuerpo, se hundía en ellas; está en `Versiones antiguas/`); cuello suave con ribete de oro y una **estrella de oro** que lo cierra (su objeto: el Sendero de las Estrellas); mangas anchas con puño celeste y de oro; pantalón y botas de cuero. Las funciones de modelado son las de `../MagoOscuro/mago_oscuro.py` y `../Eldran/eldran.py`.
- `Hair20_Archimago.fbx` (`../Caras/pelo_archimago.py`): pelo largo plateado con raya al medio y dos mechones que barren hacia las sienes, coleta baja atada con un aro de oro y **perilla corta**; el casquete va **a 4,5 cm de la cabeza** (a 1 cm parecía un gorro de baño pegado al cráneo en el juego) (la boca queda libre). Pesado a `head`. Pelo 20 (el 19 es del Mago Oscuro).
- `Caras_archimago.fbx` (`../Caras/kit_npcs.py`, estilo anime A): **Eye320-329 + Mouth320-329**. Ojos almendrados gris azulado con brillo, cejas grises gruesas, ojera fina y surco junto a la nariz (mejillas `mayor`), boca de reposo con **sonrisa suave** (`BOCA_ESTILO=amable`); las demás bocas son las de siempre. Trae los símbolos de anime (Fx3200…).
- Cabeza: la del pack, `Head01_Male` (sin cambios). Piel: la que se le ponga en Unity.

## Cómo se distingue
- Will: azul marino y dorado, pelo dorado de punta. Mago Oscuro: violeta/negro/hueso, capucha, figura imponente. Eldran: verde musgo, pelo blanco de pinchos y barba larga. Archimago: **crema y oro con toque celeste, pelo plateado largo recogido, perilla corta, estrella de oro**.

## Previas
- `archimago_previa.png` (frente, tres cuartos, lado, espalda), `archimago_brazos_bajados.png` (lo mismo con los brazos bajados, para comprobar que la capa no roza las mangas), `archimago_caras.png` (reposo, sonrisa, triste, enfado) y `archimago_y_mago_oscuro.png` (los dos a la misma escala). Hechas con Workbench (`../Caras/cuatro.py`, `foto.py`); sin emisión ni sombreado de dibujo.

## Pendiente / sin comprobar
- Unity (INC-1131): hecho el vestuario (ficha en `VestuarioDeLosNPCs`, FBX en `Assets/Art/Characters/NPCs/Vestuario/Archimago/`) y las caras (juego `archimago` en `CarasDeLosNPCs`, FBX `Caras_NPC_archimago.fbx`) sobre `Assets/Prefabs/_WILL_ORIGINAL.prefab`. Falta verlo con luz (la foto del buzón sale a contraluz) y pasar los materiales al shader de dibujo.
- No se ha visto moverse: túnica y capa copian los pesos del cuerpo; la coleta va rígida a `head` (un muelle sería posterior).
- La perilla y el pelo son de Blender «a mano»: se pueden afinar (más volumen, mechones, pelo suelto) si se quiere más presencia.
- Se puede añadir un estado del prólogo con la capa/brillo de «Protección Absoluta» y la pose de brazos arriba (ver INC-909).

## 13 nov 2026 — fallos al andar (vídeo del prólogo) y chequeo de choques
- En el vídeo `prologo_vfx_nuevos.mp4`: la tela se rasgaba bajo la axila y en el faldón al andar, los aros de los puños bailaban alrededor de la mano y la pierna atravesaba el bajo.
- Causa: la túnica copiaba los pesos del cuerpo vértice a vértice y, junto al arranque del brazo, tomaba pesos del brazo; el faldón tomaba a la vez los dos muslos. Los puños medían 0,118 de radio para un brazo de 0,08.
- Arreglo (`archimago.py`, con `../Prendas/ropa_lib.py`):
  - el tronco copia solo del tronco y las piernas;
  - lo que cuelga por debajo de la cintura es un `Faldon` (pelvis y muslos repartidos sin saltos);
  - la túnica es algo más estrecha bajo el brazo (0,19-0,21);
  - los puños van ceñidos (0,10);
  - la capa va más pegada a la espalda y sigue al tronco copiando.
- Coleta: la parte baja pasa de `head` a `spine_03` (`../Caras/coleta_a_la_espalda.py`), para que no se meta en la capa.
- Chequeo (`../Prendas/chequeo_choques.py`): antes NO PASA (6 graves: rasgados en axila, cuello y faldón); ahora PASA, con avisos menores (manos que se apoyan en la tripa y atraviesan el nudo de la faja, pies al correr). Informe y fotos en `chequeo/`; comparación en `archimago_antes_despues.png`.
- Lo anterior está en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.

## 13 nov 2026 — el pelo parpadeaba (INC-1134)
- El casquete se hinchaba 0,045 sobre la malla de la cabeza, que va en centímetros (escala 0,01): quedaba a 0,45 mm de la cabeza y en el juego la piel y la coronilla gris asomaban a ratos.
- `../Caras/pelo_archimago.py` ahora hincha en metros: el pelo va de verdad a 4,5 cm. FBX rehecho y copiado a Assets; el anterior, en `Versiones antiguas/Hair20_Archimago_v3_casquete_pegado_a_la_cabeza.fbx`.
- Segunda pasada (vídeo `prologo_vfx_nuevos_2`): por el hueco entre el borde del pelo y la cabeza se veían la piel y el revés del pelo, y la nuca quedaba al aire. El casquete lleva ahora un faldón en todo el borde hasta la cabeza, y por detrás la línea del pelo baja en cuanto deja la oreja. La versión anterior está en `Versiones antiguas/Hair20_Archimago_v4_sin_faldon_ni_nuca.fbx`, y la comparación en `archimago_pelo_antes_despues.png`.
