# Casas y piezas propias de las zonas (Blender)

`zonas.py` las genera con `bpy` (Blender 5.3 de pip): `python zonas.py <carpeta> [fotos] [piezas]`.
Con `fotos` saca además dos imágenes de maqueta (`aldea.png`, `capital.png`); con `piezas`, una lámina de las piezas sueltas.

Los FBX están en `Assets/Art/World/Zonas/`. *El Sendero ▸ Escenario ▸ Zonas: preparar las casas nuevas*
(`ModelosDeLasZonas`) les pone los materiales lisos con el estilo del pack, les añade colisión y deja un prefab de cada
uno en `Assets/Art/World/Zonas/Prefabs/`. El vestido de MainWorld (`VestidoDelMundo.Zonas.cs`, TDD § 23.7c) los coloca.

Convenios: 1 unidad = 1 m; la fachada mira a −Z en Unity, como las casas del pack; y = 0 es el suelo (en las piezas
sobre pilotes, la lámina del mar: las tablas quedan a 1,6 m y los pilotes bajan hasta −2,4 m).

## Aldea del Mar

| FBX | Qué es |
|---|---|
| `CasaDeLaAldea`, `_Azul`, `_Coral`, `_Amarilla`, `_Alta`, `_Pequena` | Casa encalada sobre pilotes, con porche, barandilla de cabo, farol y escalones de tabla hasta el agua (sin escalada: se sube andando al salir de nadar). Cambian el color de teja y zócalo, el tamaño y las plantas. |
| `CasaDeLaAldeaEnTierra_Turquesa/_Azul/_Coral/_Amarilla` | La misma casa sin pilotes, sobre un zócalo de piedra de 0,45 m con dos peldaños. |
| `PasarelaDeLaAldea` | Tramo de pasarela de 3 m con pilotes y cabo a los lados. |
| `RellanoDeLaAldea` | Rellano de descanso entre pasarelas, con banco y farol. |
| `TiendaDeLaAldea_Pescado/_Fruta/_Aparejos` | Tienda abierta sobre pilotes, con toldo de rayas y género en el mostrador. |
| `MiradorDeLaAldea` | Templete al final del barrio. |
| `PaseoDeTablas` | Tramo de 3 × 1,8 m para ir por la arena. |
| `Faro` | Faro del cabo, de franjas, con linterna y galería. |
| `Lonja` | Lonja abierta de la plaza del puerto. |
| `SecaderoDePescado` | Secadero (modelado; hoy no se coloca). |
| `RosaDeLosVientos` | Rosa de 9,4 m sobre una tarima de piedra de dos peldaños bajos; el cuerpo baja 1,2 m para asentarse en una plaza que no es llana. |
| `Banderines` | Ristra de banderines entre dos palos, 20 m de luz. Sin colisión. |

## Capital

| FBX | Qué es |
|---|---|
| `CasaDeCiudad_A`…`_H` | Casa de ciudad con soportal, impostas de piedra lila, contraventanas azules y tejado de pizarra. Anchos de 4,8 a 6,2 m, de 2 a 4 plantas, fondo 6 m. Unas con hastial a la calle y otras con alero y buhardillas; `_G` y `_H` llevan toldo de tienda. Encajan pared con pared; los costados tienen zócalo, impostas y ventanas para que el extremo de una hilera no sea un muro ciego. |
| `EdificioDeCiudad_Taberna/_Posada/_Palacio/_Archivo/_Guardia` | Edificios públicos de la misma familia: 6,8 a 12 m de ancho y 8 m de fondo (Guardia, 6 m). |
| `TorreDelReloj` | Torre del reloj de la Plaza Real. |
| `FuenteDeLaPlaza` | Fuente de dos tazas. |
| `ArbolDePlaza` | Árbol en alcorque de piedra. |
| `PuestoDeMercado_Fruta/_Verdura/_Pan/_Pescado` | Puesto con toldo de rayas y género. |

## Pueblo de Will

| FBX | Qué es |
|---|---|
| `ArbolConColumpio` | Árbol grande con columpio. |

## Materiales

Van por nombre (tabla `COLORES` de `zonas.py`). `ModelosDeLasZonas` los cambia por lisos con el estilo del pack
(`PiezaDeBlender`). **Un material nuevo en `zonas.py` hay que añadirlo también a la tabla de colores de `ModelosDeLasZonas`.**

Al reflejar una casa (`espejo`) basta con aplicar la escala negativa: Blender ya da la vuelta a las caras.

## Pendiente de modelar

Entrada de templo, cueva de la cascada, variantes propias para el pueblo de Will.

## Exploración y pueblo de Will (montado el 11 nov 2026, TDD § 23.7d)

| FBX | Qué es |
|---|---|
| `CasaDelPueblo_A`…`_F` | Casa de campo del pueblo de Will: zócalo de piedra, estuco con entramado, tejado de paja, porche y chimenea. De 4,6 a 8,4 m de fachada, una o dos plantas, con la cumbrera a lo ancho o con el hastial a la calle. |
| `GraneroDelPueblo`, `PozoDelPueblo`, `ArcoDelPueblo` | Granero de tablas rojas, pozo con tejadillo y arco de entrada al pueblo (paso de 4 m). |
| `ArcoDeLaAldea` | Entrada a la Aldea del Mar: dos mástiles, red con boyas y gallardete. |
| `RepisaDeRoca_Grande/_Mediana/_Pequena` | Repisa con la cara de arriba llana en y = 0 y 7 m de cuerpo para clavarla en una ladera. Se salta de una a otra. |
| `EscalonesDePiedra` | Diez peldaños de 0,2 m (sube 2 m en 4,5 m), macizos. |
| `PuenteColgante`, `PuenteColgante_Largo` | Puente de tablas de 10 y 16 m. |
| `MiradorDeCumbre` | Mirador de piedra de 8 m con asta y gallardete: destino de vuelo. |
| `BocaDeCueva`, `TunelDeRoca`, `TunelEscalonado`, `SalaDeLaCueva` | Piezas de cueva con paso de 3,2 × 3,4 m y el suelo en y = 0: boca de 5 m, túnel de 8 m, túnel con escalera que sube 5 m en 8 m y sala redonda de 11 m (12,6 m entre pasos). Por fuera son peñas: van medio enterradas. |
| `CortinaDeHiedra`, `GrietaSecreta` | Hiedra que tapa un paso (sin colisión) y pasillo entre dos lajas de roca. |
| `EntradaDeTemplo` | Entrada de un templo de las pruebas; el velo de luz del fondo es donde irá el disparador que carga el nivel. |
