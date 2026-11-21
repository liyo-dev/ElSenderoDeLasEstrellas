
## Zonas por forma y chequeo de piernas bajo falda (13 nov 2026, INC-1138)
- `rl.completar_zonas(GRUPOS["traje"])`: a cada pieza sin pesos dichos le pone su zona por el nombre y la forma
  (`zona_por_forma`): brazo (`Manga_l`, `Hombro_r`, `Brazal_l`, `CorreaBrazal_r340`…), pierna (`Bota`, `Media`,
  `Zapato`, `Suela`… con poco suavizado para que la rodilla doble como la del cuerpo), pernera (tronco y piernas),
  faldón si baja más de 6 cm de la cintura, y tronco lo demás. Las prendas de tronco se ciñen bajo el brazo
  (`ceñir_axila`, ancho máximo 0,205 entre z 0,54 y 0,70) y las piezas de pierna se parten en anillos de ~3 cm
  (`anillos_en_la_rodilla`): un tubo de tres anillos no puede doblar la rodilla.
- Lo usan ya todos los personajes: Will, Liam, Eldran, Victoria, Erika, Oliver, el Rey, el posadero, la boticaria
  (y el Archimago y el Mago Oscuro, con sus zonas dichas a mano). Todos en PASA.
- `chequeo_choques.py`: un punto del cuerpo cuenta como tapado si una prenda que en reposo ya hacía de piel
  (a menos de 5 cm, `CENIDA_MAX`) sigue a la misma distancia (+1 cm). Antes una media bajo la falda contaba como
  «cuerpo que asoma» en cuanto la falda se apartaba, aunque la media siguiera haciendo de pierna.

## Puntos a revisar en cada modelo nuevo
- **Mangas anchas y puños con las manos juntas delante** (Archimago, 13 nov 2026, en una cinemática conjurando): un
  puño rígido de aro ancho (todo a `lowerarm`) alrededor de una muñeca estrecha, con el codo doblado hacia la tripa,
  mete la mitad de dentro del aro en la túnica y se ve como una pulsera cortada que no sigue a la manga.
  - Al diseñar: la manga ancha se recoge en la muñeca (puño ceñido, poco más ancho que el brazo); el vuelo, en el
    antebrazo, no en el borde. Si el puño es ancho, que no sea un aro rígido: tela que siga a la manga.
  - Al comprobar: `chequeo_choques.py` lleva la pose `manos_juntas` y ya no perdona un puño o una boca de manga
    metidos en el tronco (antes contaban como «manos sobre la tripa»). Mirar la foto de esa pose.
  - Ya resuelto en el taller: `Taller.manga_con_puno` (ver abajo).

- **Huecos vistos desde abajo** (13 nov 2026, INC-1169: volando, tumbado boca arriba, contrapicados). Unity no pinta
  el revés de las caras: por el bajo de la ropa se veía el interior vacío (cuerpo quitado sin tapar), por las bocas
  de mangas, perneras y botas el revés de la tela, y por debajo de los mechones del pelo, el cielo.
  - Al diseñar: con el taller, `quitar_lo_tapado` (tapa los cortes del cuerpo) y las piezas abiertas llevan grosor
    por dentro solas; los mechones o flequillos que se separan de la cabeza, con `con_grosor` (`Caras/caras_lib.txt`).
  - Al comprobar: `chequeo_huecos.py` (abajo). Barrido del 13 nov: no pasaban Boticaria, Eldran, Erika, Mago Oscuro,
    Oliver, Posadero y Victoria (cuerpo): ya pasados al taller y en PASA. Sigue pendiente Will (pelo, `Hair16_Heroe`).
- **Correas cruzadas** (Oliver, 13 nov 2026): eran dos medias correas que acababan sobre el arranque del brazo; al
  moverlo se hundían en él y por detrás se metían bajo el cuello. → `t.bandolera(...)`: una sola correa, pasa junto al
  cuello (|x| ≤ 0,12), apoyada en la prenda de debajo (rayo desde fuera) y con sus pesos.
- **Botas que se pierden** (Oliver): la pernera acababa a la par de la boca de la bota; al andar asomaba por fuera y la
  bota se fundía con el pantalón. → `t.pernera(...)` acaba 2 cm dentro de la bota y más estrecha; `t.bota(...)` lleva
  una vuelta más clara que corta la silueta. La revisión antes de exportar avisa de piezas que se tocan con casi la
  misma luz («Contraste»; si es a propósito, como el Mago Oscuro todo en sombra, se deja).
- **Nada de bolsas ni mochilas en el costado** (Raúl, 14 nov 2026, INC-1183): la mano se metía en el libro del zurrón
  de Oliver y de Eldran. Lo que va en la cintura, por delante o por detrás. La revisión antes de exportar avisa
  («Paso de las manos») de piezas que cuelgan en el costado entre la cadera y el muslo.
- **Sentado, el faldón se rasgaba por detrás** (Eldran, 14 nov 2026, INC-1186): la espalda del abrigo seguía a los muslos
  hacia delante. → `Faldon(..., atras=0.35)` (por defecto): por detrás sigue menos al muslo. `chequeo_choques.py` tiene
  la pose `sentado`.
- **Tumbado, sin cuerpo bajo la camiseta** (Will, INC-1186): si se quita el cuerpo entero, la prenda de arriba necesita un
  fondo (del color del pantalón) bajo su bajo; con `quitar_lo_tapado` ya se tapa solo.
- **Manos que no se ven** (Nina, Oliver): faldón con mucho vuelo o mangas largas. → `chequeo_huecos.py` mide qué parte
  de cada mano se ve con los brazos caídos (idle); mínimo 35 %.
- **Sentado sin piernas** (Will en el columpio, 13 nov 2026): `chequeo_huecos.py` mide los muslos de frente sentado
  (mínimo 15 %). Will da 70 %: sus muslos no están tapados; de frente se ven en escorzo y la bota alta ocupa toda la
  pierna. Eso es pose y cámara, no el modelo (ver TRACKER INC-1170).
- **Tiras y adornos sobre el faldón, sentado** (Eldran en el filo de la cama, 20 nov 2026, INC-1376): la solapa del abrigo
  (una tira oscura del cuello al bajo) con un botón en la punta, sentado y con la mano en el regazo, se leía como un palo en
  la mano. Dos causas: cada pieza que cuelga se pesa como faldón **hasta su propio bajo**, así que una pieza pegada sobre
  el faldón que copia los pesos del cuerpo sigue al muslo distinto que él y se despega; y una tira estrecha por delante,
  por debajo de la cintura, sentado queda de canto sobre el regazo.
  - Al diseñar: lo que va pegado sobre otra prenda (solapas, ribetes, bajos, botones, bordados) lleva
    `rl.Zona(sobre=<prenda>)` (y `rigida=True` si es un adorno pequeño). Las tiras de delante (solapas, cabos de faja o
    de cordón) y los adornos (botones, frascos) acaban en el cinturón; por debajo, el faldón va limpio.
  - Al comprobar: `tl.revisar_antes_de_exportar` avisa de las dos cosas («Pegada sin "sobre"»: sienta al personaje y mide
    si la pieza se separa de su prenda más de 8 mm; «Tira por delante del faldón»: más estrecha de 8 cm y más de 6 cm por
    debajo de la cintura). `chequeo_choques.py` lleva la pose `sentado_hablando` (tronco adelante, manos al centro del
    regazo) y se puede fotografiar con `--fotos-de sentado,sentado_hablando`.
  - Barrido del 20 nov (solo la revisión antes de exportar, sin volver a exportar nada): con «Tira por delante del faldón»
    salen el cabo de la faja del Archimago, el frasco de la boticaria, los dos cabos de la faja de Liora y un cabo del
    cordón de Oliver; con «Pegada sin "sobre"», ninguno (Eldran ya arreglado). Pendiente de decidir con Raúl cuáles
    se sientan en el juego y se recortan.

## Cómo se comprueba un personaje (los dos chequeos, más la revisión antes de exportar)
Los chequeos se pasan sobre el `.blend` recién hecho por su script, nunca sobre uno guardado de otro día: el de Will
era del 11 nov y no tenía los arreglos posteriores (14 nov 2026).
1. `python chequeo_choques.py -- personaje.blend salida --piezas pelo.fbx` → `chequeo.md` / `chequeo.png` (en movimiento).
2. `python chequeo_huecos.py -- personaje.blend salida --piezas pelo.fbx` → `huecos.md` / `huecos.png` (desde abajo:
   rayos desde 25 direcciones por debajo, en reposo y andando; en rojo, lo que en Unity se ve a través; además,
   manos con los brazos caídos y muslos sentado de frente).
Los dos tienen que dar PASA antes de meter el personaje en Unity, y la revisión antes de exportar (`REVISAR:` al
pasar el script del personaje) no debe dejar avisos de «Pegada sin "sobre"» ni de «Tira por delante del faldón» sin mirar.
Para ver las poses sentadas: `--fotos-de sentado,sentado_hablando` (las fotos de todas las poses tardan más de 3 minutos).

## Taller de piezas (`taller.py`, 13 nov 2026)
Las funciones de modelado (`tubo`, `torno`, `toro`, `esfera`, `caja`, `estrella`…) estaban copiadas en 18 scripts:
un arreglo en uno no llegaba a los demás. Ahora están una vez en `taller.py`, con piezas que ya traen lo aprendido.
- `Taller(COLORES, grupos=(...))`: materiales y grupos de piezas; `t.tubo(...)`, `t.torno(...)`… igual que antes.
- `t.manga_con_puno(grupo, lado, cuerpo, arm, mat, mat_forro, mat_ribete=None, vuelo=0.10)`: manga con el vuelo en el
  antebrazo y recogida en la muñeca; el puño se mide en el brazo de verdad (`radio_del_miembro`) más 1 cm, copia
  los pesos del brazo (no es un aro rígido) y la boca se tapa con el forro (Unity no pinta el revés).
- `tl.revisar_antes_de_exportar(lista, cuerpo, arm)`, justo después de `rl.pesar_piezas` (y antes de borrar el
  cuerpo tapado): avisa de escala que no está en metros, pesos que no suman 1, más de 4 huesos por vértice y piezas
  rígidas que se separan del miembro más de 2 cm. Con las piezas viejas del Archimago saltaba en los cuatro puños.
- `ropa_lib.pesar` deja como mucho 4 huesos por vértice (Unity descarta el resto).
- `t.quitar_lo_tapado(cuerpo, tapado, mat_tapa)`: quita el cuerpo de base bajo la ropa y tapa los cortes con
  `mat_tapa` (el color que se verá desde abajo, p. ej. el pantalón). Sustituye al borrado a mano de cada script.
- Piezas abiertas (`tubo(cerrar=False)`, `torno` sin grosor) llevan solas `FORRO_MINIMO` (4 mm) de grosor hacia
  dentro, así su interior se pinta; con `mat=(fuera, dentro)` el interior va de otro color (la manga, del forro).
- Lo usan ya el Archimago, el Mago Oscuro, Eldran, Victoria, Erika, Oliver, el posadero y la boticaria (13 nov 2026,
  pasados con `migrar_al_taller`: quita sus copias de las funciones y cambia el borrado del cuerpo por
  `quitar_lo_tapado`). Will, Liam, Estela, el Rey y los niños, cuando se vuelvan a tocar.
- `chequeo_choques.py`: un puño o brazal contra la cadera en `brazos_bajados` no cuenta (el brazo descansa ahí a
  propósito); al correr se le perdona el doble, como a lo demás.
- Pendiente para el taller: más piezas con sus reglas (capa pegada al tronco, pelo con casquete en metros), y meter
  en el chequeo las poses de actuación y las animaciones que de verdad usan las cinemáticas.


## Cabeza que se mueve, piel propia y ribetes en otra prenda (19 nov 2026, INC-1308 / INC-1309)
- `chequeo_choques.py` lleva tres poses de cabeza (`cabeza_arriba`, `cabeza_abajo`, `cabeza_girada`: cuello y cabeza
  hasta lo que permite el avatar del pack más la mirada de las cinemáticas) y una medida nueva, **cabeza que se hunde en
  la ropa** (barbilla o nuca dentro de una prenda del tronco: un cuello alto pesado al pecho). Lo que cuelga de la
  cabeza (pelo, sombreros) no cuenta. Liora (cuello crema hasta la barbilla): no se hunde en ese rango.
- La ropa se mide también contra la **piel propia** del modelo (brazos de piel hechos a medida, más gruesos que los del
  pack); antes solo contra el cuerpo de referencia, y una camiseta que se metía en el brazo de Will no salía.
- Un **ribete o puño de manga que se hunde en otra prenda** (la manga en el costado de la camiseta, por la axila) es
  grave a partir del 10 % de la pieza: por la espalda se ve la tela del tronco atravesando la manga y el contorno se
  corta ahí. Will (`Body23_Aventurero`) da NO PASA por esto en `brazos_bajados` (RibeteManga dentro de Camiseta).
- Poses de tronco (`tronco_adelante`, `tronco_atras`, `tronco_girado`: pecho doblado y girado como en los gestos de
  charla) y medida **tela tendida sobre otra** (a menos de 3 mm y en paralelo, no un borde que solo toca): a unos
  metros de cámara parpadea. Aviso desde el 12 % de la pieza, grave desde el 25 %. Victoria: el peto no llega a meterse
  en la blusa en ninguna pose (su cara de dentro queda a 2,5–3,3 mm en el borde de arriba); sí avisa de la falda sobre la
  blusa y de los hombros sobre las mangas (Will y Victoria).

## Piel en la ropa, pelo por el gorro, tumbado y un cuerpo cada vez (19 nov 2026, INC-1340)
- `chequeo_choques.py --solo Body23_Aventurero[,Hair16_Heroe]`: mide solo esas mallas del `.blend` (más `--piezas`). El
  `.blend` de Will lleva dos cuerpos (con y sin chaqueta) y el pelo del script: medidos juntos salía «tela sobre tela»
  falsa (cada camiseta sobre su gemela). `chequeo_huecos.py` acepta lo mismo. Pasar siempre un cuerpo cada vez.
- **Piel propia en la ropa**: la piel hecha a medida (brazos de piel) que se mete en una prenda del cuerpo al moverse
  (Raúl: «de espaldas se ve que la camiseta se mete en la carne de los brazos»). Grave desde el 6 % de la pieza; las
  manos del pack (con nombre de pieza o sin él) solo avisan: una mano sobre el pecho o el cinturón es un gesto.
- **Pelo por fuera del gorro**: desde el centro del cráneo hacia cada punto del pelo; si el gorro o la capucha está antes,
  el pelo lo atraviesa (Liam: un pincho por encima de la capucha). Grave desde 2 puntos. Arreglo:
  `../Pelo/pelo_bajo_el_gorro.py -- pelo.fbx gorro.fbx salida.fbx` (o lo mismo dentro del script del personaje).
- Pose **tumbado** (boca arriba, piernas dobladas, brazos junto a la cabeza, cabeza atrás: Will en la cama de Eldran,
  «SÚPER ROTO») y la animación `Sleeping_NoWeapon`. Las fotos de tumbado salen siempre y una de ellas desde los pies.
  Con ella salió el `Fondo` de Will (disco bajo la camiseta) atravesando la camiseta con las piernas dobladas.
- Barrido de todos los modelos propios con las medidas nuevas: `barrido_choques_2026-11-19.md`. Pasan Will, Liam (con su
  script actual), Oliver y la boticaria; el resto tiene graves que no son de hoy (ver el informe).

## Adornos rígidos (19 nov 2026, INC-1338)
- `ropa_lib`: una pieza de tronco sin zona dicha que cabe en una caja de 8 cm (`ADORNO_MAX`: hebilla, botón, broche) va
  **rígida** (`Zona(..., rigida=True)`: toda la pieza con los mismos pesos, la media de los que copia). Antes copiaba los pesos
  vértice a vértice y, entre la pelvis y el pecho, se estiraba: la hebilla de Eldran y la de Erika daban grave en `tronco_atras`.
- Con esto Eldran vuelve a PASA. Erika tiene el mismo grave y se arregla al volver a pasar su script (no hecho).
