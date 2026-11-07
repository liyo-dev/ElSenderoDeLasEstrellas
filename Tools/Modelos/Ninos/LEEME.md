# Niños hechos en Blender (1 nov 2026)

Niños con **cuerpo de niño propio** en vez de encogerlos: el esqueleto de RPG Tiny Hero con proporciones de niño
(mismos huesos, mismos nombres y giros; piernas, tronco, cuello y brazos más cortos, hombros y caderas más
estrechos). La cabeza no cambia de tamaño. Blender 5.2 (módulo `bpy`).

| Archivo | Qué hace |
|---|---|
| `base_adulto.py` | Junta AllBodiesCloaks (esqueleto y cuerpos) y las cabezas de ModularCharacterPolyart → `base_adulto.blend`. Args: los dos FBX y la salida. |
| `cuerpo_nino.py` | Las tallas (`mayor` 11-13 años, `pequeno` 5-8) y `reproporcionar(arm, talla)`: mueve los huesos y deforma las mallas con ellos. Como script: base, talla, salida. |
| `kit.py` | Formas, pelos del pack deformables (`pelo_del_pack`, sin sus cejas), lazos, pesos y unión. Se diseña con medidas del adulto y la talla se pone al final. |
| `tobias.py` | Tobías (talla mayor): pelo `Hair17_Tobias` (Hair02 recogido, flequillo corto, remolino) y `Body24_Tobias` (túnica ocre de casa). Args: base_adulto.blend, carpeta HeadParts del pack, salida. |
| `nina.py` | Nina (talla pequeña): `Hair18_Coletas` (Hair08 con coletas grandes y lazos) y `Body25_Vestido` (vestido coral con delantal). Mismos args. |
| `exportar.py` | FBX: el esqueleto solo (`-`), unas mallas, o todas (`*`). |
| `foto.py`, `juntar.py` | Fotos de comprobación (frente, tres cuartos, lado, espalda) y juntar piezas de otro .blend para la foto. |

Caras (estilo A desde INC-918, 7 nov 2026; todo junto en `caras_ninos.sh`): `../Caras/caras_set.py` con las variables de niño (ojos más grandes y bajos, boca más pequeña):
`OJO_ESCALA=1.22 OJO_Z=0.93 OJO_X=0.13 BOCA_Z=0.825 BOCA_ESCALA=0.85 MEJILLAS=suave`, y después
`cuerpo_nino.py` con su talla. Tobías: `Head01_Male chicoL marron 60 60` (`OJO_FORMA=chico`, como Liam); Nina: `Head02_Female chicaL azul 70 70` (`OJO_FORMA=redondo`, `EXTRAS=0`).

FBX en `Assets/Art/Characters/Ninos/` (`Esqueleto_NinoMayor/Pequeno.fbx`, y por niño su pelo, cuerpo y caras).
En Unity: *El Sendero ▸ Personajes ▸ Niños hechos en Blender (Tobías, Nina)* (`NinosHechosEnBlender`, `CuerpoDeNino`).

Para otro niño: un script como `tobias.py` con su talla, sus caras con otra numeración libre (60-79 ocupados) y
una entrada más en `NinosHechosEnBlender`.
