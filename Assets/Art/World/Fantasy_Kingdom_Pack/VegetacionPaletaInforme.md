# Paleta de vegetación — Fantasy_Kingdom_Pack (catálogo v4, generado fuera de Unity el 2026-09-11)

Generado por Claude leyendo directamente los FBX (`Meshes/`) y las texturas TGA (`Textures/`) del pack, con el mismo método que implementa `VegetacionPaletaCatalogador.cs` v4: muestreo dentro de los triángulos de cada malla y agrupación en 3 clústeres de color ponderados por superficie UV. Volver a ejecutar el menú `El Sendero → Mundo → Catalogar Paleta de Vegetación` en el Editor regenera este archivo (incluyendo flores y macetas, que aquí no se han incluido).

Hallazgos clave:

- Todos los árboles, setas, hierba y vides comparten el atlas `Tree_D`; el color lo decide qué isla del atlas lee cada malla.
- La letra del nombre NO es un código de color consistente entre familias (Tree04_a y Tree04_c son ambos turquesa; Tree01_a es verde pero Tree02_a también). Hay que mirar el color real de cada prefab, no la letra.
- Las familias `Plant01`–`Plant06` NO son plantas sueltas: `_a` son parches planos de césped/pradera con flores (texturas `Terrain01_D`/`Terrain02_D`), y `_b`–`_e`, `Plant04`–`Plant06` son jardineras y parterres (madera/piedra/tierra, texturas `FK01_D`/`Ground04_D`). Los `_b07–b09`/`_d07–d09` son composiciones jardinera + árbol.

| Familia | Prefab | Textura | Color dominante | % | 2º color | % | 3º color | % |
|---|---|---|---|---|---|---|---|---|
| Grass01 | Grass01_a01 | Tree_D | #109A10 verde | 53 | #11A011 verde | 29 | #0D8E0D verde | 18 |
| Grass01 | Grass01_a02 | Tree_D | #119C11 verde | 60 | #13B613 verde | 24 | #0F930F verde | 16 |
| Grass02 | Grass02_a01 | Tree_D | #109910 verde | 35 | #0F920F verde | 35 | #12A112 verde | 29 |
| Grass02 | Grass02_a02 | Tree_D | #119F11 verde | 37 | #0F940F verde | 36 | #13B613 verde | 27 |
| Grass03 | Grass03_a01 | Tree_D | #0F8A0F verde | 45 | #119A11 verde | 39 | #0B6E0B verde | 16 |
| Mushroom01 | Mushroom01_a01 | Tree_D | #1579C9 azul | 52 | #1689CA azul | 31 | #18ADDA cian | 17 |
| Mushroom01 | Mushroom01_a02 | Tree_D | #1579C9 azul | 52 | #1689CA azul | 32 | #18AFDB cian | 16 |
| Mushroom01 | Mushroom01_a03 | Tree_D | #157AC9 azul | 53 | #1689CA azul | 30 | #18AEDB cian | 17 |
| Mushroom01 | Mushroom01_a04 | Tree_D | #157AC9 azul | 53 | #1689CA azul | 30 | #18AEDB cian | 17 |
| Mushroom01 | Mushroom01_a05 | Tree_D | #157AC9 azul | 53 | #1689CA azul | 31 | #18AFDB cian | 16 |
| Mushroom01 | Mushroom01_b01 | Tree_D | #86AA12 verde amarillento (lima) | 51 | #97A812 verde amarillento (lima) | 31 | #B7B014 amarillo | 18 |
| Mushroom01 | Mushroom01_b02 | Tree_D | #87AA12 verde amarillento (lima) | 50 | #97A812 verde amarillento (lima) | 33 | #B8B014 amarillo | 17 |
| Mushroom01 | Mushroom01_b03 | Tree_D | #87AA12 verde amarillento (lima) | 49 | #97A812 verde amarillento (lima) | 33 | #B8B014 amarillo | 18 |
| Mushroom01 | Mushroom01_b04 | Tree_D | #86AA12 verde amarillento (lima) | 50 | #96A812 verde amarillento (lima) | 32 | #B8B014 amarillo | 18 |
| Mushroom01 | Mushroom01_b05 | Tree_D | #86AA12 verde amarillento (lima) | 49 | #96A712 verde amarillento (lima) | 33 | #B8B014 amarillo | 17 |
| Mushroom01 | Mushroom01_c01 | Tree_D | #DE7418 naranja | 40 | #CC4516 naranja | 39 | #DF1F18 rojo | 21 |
| Mushroom01 | Mushroom01_c02 | Tree_D | #D33817 rojo | 58 | #E47D19 naranja | 22 | #D76A17 naranja | 20 |
| Mushroom01 | Mushroom01_c03 | Tree_D | #DE7418 naranja | 41 | #CD4516 naranja | 39 | #E11C18 rojo | 20 |
| Mushroom01 | Mushroom01_c04 | Tree_D | #DE7418 naranja | 41 | #CD4516 naranja | 40 | #E11B18 rojo | 19 |
| Mushroom01 | Mushroom01_c05 | Tree_D | #DE7418 naranja | 41 | #CD4516 naranja | 40 | #E01C18 rojo | 19 |
| Mushroom02 | Mushroom02_a01 | Tree_D | #C87F56 naranja | 52 | #D29761 naranja | 33 | #E2C073 amarillo/mostaza | 14 |
| Mushroom02 | Mushroom02_a02 | Tree_D | #CB865A naranja | 78 | #DFB870 naranja | 20 | #C96C2B naranja | 2 |
| Mushroom02 | Mushroom02_a03 | Tree_D | #C77D55 naranja | 46 | #D1945F naranja | 40 | #E2BF73 amarillo/mostaza | 14 |
| Mushroom02 | Mushroom02_b01 | Tree_D | #13AB6D verde azulado (turquesa) | 66 | #1BCBB2 verde azulado (turquesa) | 19 | #4CC499 verde azulado (turquesa) | 15 |
| Mushroom02 | Mushroom02_b02 | Tree_D | #11A665 verde azulado (turquesa) | 49 | #1CB987 verde azulado (turquesa) | 30 | #39CEB3 verde azulado (turquesa) | 21 |
| Mushroom02 | Mushroom02_b03 | Tree_D | #11A765 verde azulado (turquesa) | 48 | #1BB986 verde azulado (turquesa) | 31 | #3ACDB2 verde azulado (turquesa) | 21 |
| Mushroom02 | Mushroom02_c01 | Tree_D | #7B71D3 azul | 57 | #977EE0 violeta/morado | 29 | #BF93F2 violeta/morado | 14 |
| Mushroom02 | Mushroom02_c02 | Tree_D | #7870D2 azul | 49 | #907BDD azul | 35 | #BB91F0 violeta/morado | 17 |
| Mushroom02 | Mushroom02_c03 | Tree_D | #7870D2 azul | 48 | #907BDD azul | 36 | #BA90F0 violeta/morado | 16 |
| Mushroom03 | Mushroom03_a01 | Tree_D | #A35911 marrón/naranja | 41 | #8A0F70 rosa/magenta | 34 | #851111 rojo | 25 |
| Mushroom03 | Mushroom03_b01 | Tree_D | #964D10 marrón/naranja | 49 | #6D7C0D verde amarillento (lima) | 31 | #169410 verde | 21 |
| Mushroom03 | Mushroom03_c01 | Tree_D | #2A3590 azul | 58 | #68686A gris oscuro | 26 | #61536E violeta/morado | 16 |
| Mushroom04 | Mushroom04_a01 | Tree_D | #C41615 rojo | 55 | #BA6614 marrón/naranja | 24 | #83360E marrón/naranja | 22 |
| Mushroom04 | Mushroom04_b01 | Tree_D | #ABA213 amarillo/mostaza | 60 | #A35A11 marrón/naranja | 33 | #591009 rojo | 7 |
| Mushroom04 | Mushroom04_c01 | Tree_D | #8113B7 violeta/morado | 61 | #5D5666 violeta/morado | 39 |  |  |
| Plant01 | Plant01_a01 | Terrain01_D | #009432 verde | 48 | #008A2D verde | 32 | #01A234 verde | 20 |
| Plant01 | Plant01_a02 | Terrain01_D | #019431 verde | 48 | #008A2D verde | 39 | #01A134 verde | 14 |
| Plant01 | Plant01_a03 | Terrain01_D | #009132 verde | 35 | #008B2B verde | 34 | #019E32 verde | 31 |
| Plant01 | Plant01_a04 | Terrain01_D | #019631 verde | 46 | #008B2C verde | 33 | #01A135 verde | 21 |
| Plant01 | Plant01_b01 | FK01_D | #AE6508 marrón/naranja | 66 | #82929E azul | 20 | #A7AAA5 gris/blanco | 14 |
| Plant01 | Plant01_b02 | FK01_D | #454988 azul | 50 | #8A99A3 azul | 27 | #AA4701 marrón/naranja | 24 |
| Plant01 | Plant01_b03 | FK01_D | #B14710 marrón/naranja | 43 | #077488 cian | 36 | #7C809F azul | 21 |
| Plant01 | Plant01_b04 | FK01_D | #B67113 marrón/naranja | 43 | #95A0A2 gris/blanco | 30 | #1D6AA9 azul | 27 |
| Plant01 | Plant01_b05 | FK01_D | #B64819 marrón/naranja | 58 | #428291 cian | 35 | #DDFFF5 gris/blanco | 7 |
| Plant01 | Plant01_b06 | FK01_D | #B1580E marrón/naranja | 60 | #117A7F verde azulado (turquesa) | 20 | #ADADA5 gris/blanco | 20 |
| Plant01 | Plant01_c01 | FK01_D | #B04A13 marrón/naranja | 45 | #30858C verde azulado (turquesa) | 33 | #F1B216 amarillo | 22 |
| Plant01 | Plant01_c02 | FK01_D | #6E90A3 azul | 48 | #F1CA65 amarillo/mostaza | 33 | #8A4607 marrón/naranja | 19 |
| Plant01 | Plant01_c03 | FK01_D | #B3590D marrón/naranja | 57 | #1F7C90 cian | 27 | #A1A9A8 gris/blanco | 16 |
| Plant01 | Plant01_c04 | FK01_D | #C25F15 naranja | 60 | #077B76 verde azulado (turquesa) | 23 | #8092A3 azul | 17 |
| Plant01 | Plant01_c05 | FK01_D | #AC4E10 marrón/naranja | 63 | #397C84 cian | 23 | #C6BF94 amarillo/mostaza | 14 |
| Plant01 | Plant01_d01 | Ground04_D | #694D2F marrón/naranja | 53 | #9B7445 marrón/naranja | 39 | #B58E51 marrón/naranja | 8 |
| Plant01 | Plant01_d02 | Ground04_D | #8E5E39 marrón/naranja | 47 | #A9864E marrón/naranja | 31 | #656B7B azul | 22 |
| Plant01 | Plant01_d03 | Ground04_D | #684C2F marrón/naranja | 48 | #946439 marrón/naranja | 31 | #A8844E marrón/naranja | 21 |
| Plant01 | Plant01_d04 | Ground04_D | #694D30 marrón/naranja | 54 | #9D7545 marrón/naranja | 33 | #BA904E marrón/naranja | 12 |
| Plant01 | Plant01_d05 | Ground04_D | #6B4D34 marrón/naranja | 39 | #A48654 marrón/naranja | 33 | #95663A marrón/naranja | 28 |
| Plant01 | Plant01_d06 | Ground04_D | #694D32 marrón/naranja | 56 | #8C6646 marrón/naranja | 23 | #AA834D marrón/naranja | 22 |
| Plant01 | Plant01_e01 | Ground04_D | #694D2F marrón/naranja | 70 | #94633B marrón/naranja | 21 | #9A8059 marrón/naranja | 10 |
| Plant01 | Plant01_e02 | Ground04_D | #694D2F marrón/naranja | 37 | #AA8752 marrón/naranja | 34 | #95683A marrón/naranja | 28 |
| Plant01 | Plant01_e03 | Ground04_D | #6C4E30 marrón/naranja | 61 | #A8814D marrón/naranja | 32 | #606575 azul | 8 |
| Plant01 | Plant01_e04 | Ground04_D | #694D30 marrón/naranja | 64 | #997143 marrón/naranja | 28 | #B28C50 marrón/naranja | 7 |
| Plant01 | Plant01_e05 | Ground04_D | #694D30 marrón/naranja | 57 | #856952 marrón/naranja | 22 | #AA854D marrón/naranja | 21 |
| Plant02 | Plant02_a01 | Terrain02_D | #039031 verde | 85 | #7EC43B verde amarillento (lima) | 9 | #2AA694 verde azulado (turquesa) | 7 |
| Plant02 | Plant02_a02 | Terrain02_D | #019530 verde | 85 | #6DCB29 verde amarillento (lima) | 12 | #6D8A7F verde azulado (turquesa) | 3 |
| Plant02 | Plant02_a03 | Terrain02_D | #019130 verde | 84 | #3EAB5E verde | 9 | #91D421 verde amarillento (lima) | 6 |
| Plant02 | Plant02_a04 | Terrain02_D | #018F30 verde | 72 | #0CA537 verde | 18 | #7CC532 verde amarillento (lima) | 10 |
| Plant02 | Plant02_b01 | FK01_D | #E78810 naranja | 36 | #086A8F cian | 36 | #8897A3 azul | 28 |
| Plant02 | Plant02_b02 | FK01_D | #8897A3 azul | 53 | #A8561A marrón/naranja | 36 | #D7C6B9 gris/blanco | 12 |
| Plant02 | Plant02_b03 | FK01_D | #446732 verde | 58 | #2867A3 azul | 29 | #8A99A3 azul | 13 |
| Plant02 | Plant02_b04 | FK01_D | #80919E azul | 44 | #8B4703 marrón/naranja | 36 | #EDDCAE amarillo/mostaza | 20 |
| Plant02 | Plant02_b05 | FK01_D | #98520A marrón/naranja | 40 | #596392 azul | 34 | #D4D6B8 gris/blanco | 27 |
| Plant02 | Plant02_b06 | FK01_D | #B1580E marrón/naranja | 60 | #117A7F verde azulado (turquesa) | 20 | #ADADA5 gris/blanco | 20 |
| Plant02 | Plant02_c01 | FK01_D | #B04A13 marrón/naranja | 45 | #30858C verde azulado (turquesa) | 33 | #F1B216 amarillo | 22 |
| Plant02 | Plant02_c02 | FK01_D | #6E90A3 azul | 48 | #F1CA65 amarillo/mostaza | 33 | #8A4607 marrón/naranja | 19 |
| Plant02 | Plant02_c03 | FK01_D | #B3590D marrón/naranja | 57 | #1F7C90 cian | 27 | #A1A9A8 gris/blanco | 16 |
| Plant02 | Plant02_c04 | FK01_D | #C25F15 naranja | 60 | #077B76 verde azulado (turquesa) | 23 | #8092A3 azul | 17 |
| Plant02 | Plant02_c05 | FK01_D | #AC4E10 marrón/naranja | 63 | #397C84 cian | 23 | #C6BF94 amarillo/mostaza | 14 |
| Plant02 | Plant02_d01 | Ground04_D | #694D2F marrón/naranja | 53 | #9B7445 marrón/naranja | 39 | #B58E51 marrón/naranja | 8 |
| Plant02 | Plant02_d02 | Ground04_D | #987144 marrón/naranja | 55 | #B28E52 marrón/naranja | 23 | #717682 gris oscuro | 22 |
| Plant02 | Plant02_d03 | Ground04_D | #6A4C2F marrón/naranja | 58 | #986F42 marrón/naranja | 33 | #B88E50 marrón/naranja | 8 |
| Plant02 | Plant02_d04 | Ground04_D | #694D30 marrón/naranja | 56 | #9D7545 marrón/naranja | 32 | #BA904E marrón/naranja | 12 |
| Plant02 | Plant02_d05 | Ground04_D | #6D4D2F marrón/naranja | 58 | #9E7645 marrón/naranja | 33 | #5A6479 azul | 8 |
| Plant02 | Plant02_d06 | Ground04_D | #694D32 marrón/naranja | 56 | #8E6745 marrón/naranja | 25 | #AB854F marrón/naranja | 19 |
| Plant02 | Plant02_e01 | Ground04_D | #694D2F marrón/naranja | 70 | #94633B marrón/naranja | 21 | #9A8059 marrón/naranja | 10 |
| Plant02 | Plant02_e02 | Ground04_D | #694D2F marrón/naranja | 37 | #AA8752 marrón/naranja | 34 | #95683A marrón/naranja | 28 |
| Plant02 | Plant02_e03 | Ground04_D | #6C4E30 marrón/naranja | 61 | #A8814D marrón/naranja | 32 | #606575 azul | 8 |
| Plant02 | Plant02_e04 | Ground04_D | #694D30 marrón/naranja | 64 | #997143 marrón/naranja | 28 | #B28C50 marrón/naranja | 7 |
| Plant02 | Plant02_e05 | Ground04_D | #694D30 marrón/naranja | 57 | #856952 marrón/naranja | 22 | #AA854D marrón/naranja | 21 |
| Plant03 | Plant03_a01 | Terrain02_D | #019531 verde | 90 | #96D340 verde amarillento (lima) | 6 | #2AB268 verde | 4 |
| Plant03 | Plant03_a02 | Terrain02_D | #019431 verde | 88 | #8ED441 verde amarillento (lima) | 6 | #2DB35C verde | 5 |
| Plant03 | Plant03_a03 | Terrain02_D | #019431 verde | 87 | #9ED63E verde amarillento (lima) | 7 | #35B75D verde | 6 |
| Plant03 | Plant03_b01 | Terrain02_D | #019531 verde | 88 | #94D13E verde amarillento (lima) | 6 | #30B576 verde azulado (turquesa) | 5 |
| Plant03 | Plant03_b02 | Terrain02_D | #019531 verde | 89 | #90D23B verde amarillento (lima) | 6 | #2CB36B verde | 5 |
| Plant03 | Plant03_b03 | Terrain02_D | #019431 verde | 90 | #98D536 verde amarillento (lima) | 6 | #29B575 verde azulado (turquesa) | 4 |
| Plant04 | Plant04_a01 | FK01_D | #9E510F marrón/naranja | 48 | #F2BE3D amarillo | 33 | #2A799F cian | 19 |
| Plant04 | Plant04_a02 | FK01_D | #9D520E marrón/naranja | 47 | #F0BE43 amarillo | 34 | #317AA0 azul | 19 |
| Plant04 | Plant04_a03 | FK01_D | #9E510E marrón/naranja | 42 | #F2BD3C amarillo | 31 | #2E7AA2 azul | 27 |
| Plant04 | Plant04_a04 | FK01_D | #9C500F marrón/naranja | 49 | #F0BC3E amarillo | 32 | #2C789C cian | 19 |
| Plant04 | Plant04_a05 | FK01_D | #9E510F marrón/naranja | 49 | #EFBD44 amarillo | 30 | #317A9E cian | 21 |
| Plant04 | Plant04_b01 | Ground04_D | #6A4D30 marrón/naranja | 55 | #684C2F marrón/naranja | 43 | #986F47 marrón/naranja | 2 |
| Plant04 | Plant04_b02 | Ground04_D | #6A4D30 marrón/naranja | 55 | #684C2F marrón/naranja | 42 | #A27C4B marrón/naranja | 3 |
| Plant04 | Plant04_b03 | Ground04_D | #6A4E31 marrón/naranja | 53 | #684C2F marrón/naranja | 43 | #A38052 marrón/naranja | 4 |
| Plant04 | Plant04_b04 | Ground04_D | #6A4D30 marrón/naranja | 57 | #684C2F marrón/naranja | 41 | #9F7748 marrón/naranja | 2 |
| Plant04 | Plant04_b05 | Ground04_D | #6A4E30 marrón/naranja | 53 | #684C2F marrón/naranja | 42 | #997850 marrón/naranja | 5 |
| Plant05 | Plant05_a01 | Ground04_D | #684D33 marrón/naranja | 56 | #916944 marrón/naranja | 25 | #AC8A55 marrón/naranja | 20 |
| Plant06 | Plant06_a01 | FK01_D | #AB5111 marrón/naranja | 44 | #247387 cian | 34 | #BCC0BD gris/blanco | 22 |
| Plant06 | Plant06_b01 | Ground04_D | #684D32 marrón/naranja | 55 | #AD8C58 marrón/naranja | 25 | #926038 marrón/naranja | 20 |
| Tree01 | Tree01_a01 | Tree_D | #0E840E verde | 87 | #A25511 marrón/naranja | 9 | #79400D marrón/naranja | 4 |
| Tree01 | Tree01_a02 | Tree_D | #11A211 verde | 48 | #0A5F0A verde | 39 | #944E10 marrón/naranja | 13 |
| Tree01 | Tree01_b01 | Tree_D | #94B301 verde amarillento (lima) | 49 | #566701 verde amarillento (lima) | 38 | #964E10 marrón/naranja | 13 |
| Tree01 | Tree01_b02 | Tree_D | #95B401 verde amarillento (lima) | 47 | #586A00 verde amarillento (lima) | 40 | #964E10 marrón/naranja | 13 |
| Tree02 | Tree02_a01 | Tree_D | #0F9210 verde | 53 | #0A610A verde | 33 | #985610 marrón/naranja | 15 |
| Tree02 | Tree02_a02 | Tree_D | #0F9210 verde | 53 | #0A610A verde | 33 | #985610 marrón/naranja | 15 |
| Tree02 | Tree02_b01 | Tree_D | #109810 verde | 55 | #944E10 marrón/naranja | 35 | #095509 verde | 10 |
| Tree02 | Tree02_c01 | Tree_D | #129415 verde | 43 | #0A650B verde | 39 | #925D2D marrón/naranja | 18 |
| Tree02 | Tree02_c02 | Tree_D | #129415 verde | 43 | #0A650B verde | 39 | #925D2D marrón/naranja | 18 |
| Tree02 | Tree02_d01 | Tree_D | #109910 verde | 54 | #944E10 marrón/naranja | 35 | #095709 verde | 11 |
| Tree02 | Tree02_e01 | Tree_D | #936104 marrón/naranja | 46 | #694801 amarillo/mostaza | 31 | #B27C01 amarillo/mostaza | 23 |
| Tree02 | Tree02_e02 | Tree_D | #936104 marrón/naranja | 46 | #694801 amarillo/mostaza | 31 | #B27C01 amarillo/mostaza | 23 |
| Tree02 | Tree02_f01 | Tree_D | #A7A700 amarillo/mostaza | 55 | #9F5311 marrón/naranja | 26 | #6A4F07 amarillo/mostaza | 19 |
| Tree03 | Tree03_a01 | Tree_D | #0C750C verde | 46 | #955410 marrón/naranja | 27 | #109A10 verde | 27 |
| Tree03 | Tree03_a02 | Tree_D | #0F8C0F verde | 75 | #A25511 marrón/naranja | 16 | #79400D marrón/naranja | 8 |
| Tree03 | Tree03_b01 | Tree_D | #0C750C verde | 46 | #965410 marrón/naranja | 27 | #109A10 verde | 27 |
| Tree03 | Tree03_c01 | Tree_D | #0C750C verde | 48 | #955510 marrón/naranja | 28 | #149B10 verde | 25 |
| Tree03 | Tree03_c02 | Tree_D | #0F8C0E verde | 75 | #A05411 marrón/naranja | 18 | #79400D marrón/naranja | 7 |
| Tree03 | Tree03_d01 | Tree_D | #0E810E verde | 70 | #965510 marrón/naranja | 27 | #5FA30F verde amarillento (lima) | 2 |
| Tree03 | Tree03_e01 | Tree_D | #7B3702 marrón/naranja | 43 | #964302 marrón/naranja | 30 | #AA580C marrón/naranja | 27 |
| Tree03 | Tree03_e02 | Tree_D | #AAAA02 amarillo/mostaza | 56 | #696102 amarillo/mostaza | 23 | #9C5111 marrón/naranja | 21 |
| Tree03 | Tree03_f01 | Tree_D | #7B3702 marrón/naranja | 44 | #974302 marrón/naranja | 30 | #AB590D marrón/naranja | 26 |
| Tree04 | Tree04_a01 | Tree_D | #0C775B verde azulado (turquesa) | 70 | #A15A11 marrón/naranja | 23 | #74420C marrón/naranja | 7 |
| Tree04 | Tree04_a02 | Tree_D | #0C775B verde azulado (turquesa) | 70 | #A15A11 marrón/naranja | 22 | #74430C marrón/naranja | 7 |
| Tree04 | Tree04_a03 | Tree_D | #0C775B verde azulado (turquesa) | 70 | #A25B11 marrón/naranja | 22 | #75430C marrón/naranja | 8 |
| Tree04 | Tree04_b01 | Tree_D | #943310 marrón/naranja | 63 | #C51516 rojo | 22 | #E6194E rosa/magenta | 15 |
| Tree04 | Tree04_c01 | Tree_D | #0C775C verde azulado (turquesa) | 70 | #A25A11 marrón/naranja | 23 | #74420C marrón/naranja | 7 |
| Tree04 | Tree04_c02 | Tree_D | #0C775C verde azulado (turquesa) | 70 | #A25B11 marrón/naranja | 22 | #74420C marrón/naranja | 7 |
| Tree04 | Tree04_c03 | Tree_D | #0C775C verde azulado (turquesa) | 70 | #A25B11 marrón/naranja | 22 | #74420C marrón/naranja | 7 |
| Tree04 | Tree04_d01 | Tree_D | #496D01 verde amarillento (lima) | 48 | #985510 marrón/naranja | 29 | #AAA200 amarillo/mostaza | 23 |
| Tree04 | Tree04_d02 | Tree_D | #496D01 verde amarillento (lima) | 48 | #985610 marrón/naranja | 29 | #AAA200 amarillo/mostaza | 24 |
| Tree04 | Tree04_d03 | Tree_D | #4A6E01 verde amarillento (lima) | 48 | #985610 marrón/naranja | 29 | #AAA200 amarillo/mostaza | 23 |
| Tree04 | Tree04_e01 | Tree_D | #294E86 azul | 39 | #954E10 marrón/naranja | 37 | #3B86BF azul | 24 |
| Tree05 | Tree05_a01 | Tree_D | #0D7E0D verde | 70 | #14B614 verde | 21 | #954E10 marrón/naranja | 9 |
| Tree05 | Tree05_b01 | Tree_D | #B91414 rojo | 71 | #E3192F rojo | 19 | #934910 marrón/naranja | 10 |
| Tree06 | Tree06_a01 | Tree_D | #0C720C verde | 84 | #A25B11 marrón/naranja | 12 | #7A460D marrón/naranja | 3 |
| Tree06 | Tree06_a02 | Tree_D | #0B680B verde | 52 | #0E810E verde | 33 | #985510 marrón/naranja | 16 |
| Tree06 | Tree06_a03 | Tree_D | #0B680B verde | 52 | #0E810E verde | 33 | #985510 marrón/naranja | 16 |
| Tree07 | Tree07_a01 | Tree_D | #0F895F verde azulado (turquesa) | 38 | #12AE58 verde | 35 | #0B6759 verde azulado (turquesa) | 27 |
| Tree07 | Tree07_b01 | Tree_D | #13B213 verde | 54 | #11A111 verde | 31 | #0E8A0E verde | 15 |
| Tree07 | Tree07_c01 | Tree_D | #E11826 rojo | 44 | #E6195B rosa/magenta | 31 | #A31111 rojo | 25 |
| Vine01 | Vine01_a01 | Tree_D | #167E0B verde | 50 | #069006 verde | 33 | #049304 verde | 17 |
| Vine01 | Vine01_a02 | Tree_D | #0C740A verde | 55 | #078C07 verde | 39 | #419700 verde amarillento (lima) | 6 |
| Vine01 | Vine01_b01 | Tree_D | #0B6C04 verde | 33 | #1E6C05 verde | 33 | #186406 verde | 33 |
| Vine01 | Vine01_b02 | Tree_D | #157308 verde | 55 | #0D800C verde | 27 | #0A9909 verde | 18 |
