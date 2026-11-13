# Will: aspecto nuevo (1 nov 2026)

- `will.py` (Blender 5.2, bpy): pelo rubio de picos hacia arriba con flequillo de lado (`Hair16_Heroe`, estilo
  del pack: casquete y pocos picos de caras planas; `pelo_conos.txt` son esas funciones) y ropa de aventurero
  sin bufanda en azul marino y dorado (`Body23_Aventurero`). Argumentos: base.blend y salida.
- 4 nov 2026 (Raúl: «que no se vean como pirámides, algo más homogéneo»; las puntas caídas se probaron y se
  descartaron): el pelo conserva su forma, con sombreado suave y duras solo las crestas de los picos
  (`PELO_ANGULO`, 45°), los vértices intermedios algo alisados (`PELO_ALISAR`) y los picos muy cortos aplanados
  (se veían como bultos). El anterior, en `Versiones antiguas/Pelo de Will con caras planas (2026-11-04)/`.
- FBX en `Assets/Art/Characters/Protagonistas/Will/`. En Unity: *El Sendero ▸ Personajes ▸ Will: aspecto nuevo*.
- 7 nov 2026 (INC-920): ribete dorado pegado a los bordes y al bajo de la chaqueta (antes flotaba delante y cruzaba la camisa), cinturón por fuera de la chaqueta, hombros más grandes y pañuelo azul claro al cuello, con el pico por delante (`Panuelo`). Lo anterior, en `Versiones antiguas/Trajes de Liam y Will antes del repaso (2026-11-07)/`.
- 8 nov 2026 (Raúl: «demasiado gordo», «la camiseta se mete en la piel» junto a las manos; «camiseta chula corta con su pañuelito» y la chaqueta al armario): `Body23_Aventurero` es ahora camiseta de manga corta azul marino con ribete dorado (torso con el radio del Body05, ~0,17), brazos de piel que entran en la mano (material del propio cuerpo con la UV en el color de piel de la textura, el mismo tono que manos y cabeza), cinturón y pañuelo. La chaqueta es una pieza aparte, `Cloak05_Chaqueta` (hueco de capa en el armario, solo para Will, no va puesta de serie). Exporta las dos con `Prendas/exportar_prenda.py`. Lo anterior, en `Versiones antiguas/Will con chaqueta puesta y camisa (2026-11-08)/`.
- 8 nov 2026, después: con la chaqueta en el hueco de capa no se podían llevar las dos. La chaqueta pasa a otro cuerpo, `Body24_AventureroChaqueta` (la misma ropa con la chaqueta encima; «Ropa de aventurero con chaqueta» en el armario), y deja libre la capa. Comprobado en Blender con las capas del pack: no se atraviesan. `Cloak05_Chaqueta` retirada a `Versiones antiguas/Chaqueta de Will como pieza de capa Cloak05 (2026-11-08)/`.
- 8 nov 2026 (INC-943): la nuca del pelo ya no baja de la base del cráneo (`PELO_NUCA_Z`, 0,78): antes se metía en el pañuelo y, al girar la cabeza, la punta parecía un trozo suelto. Lo anterior, en `Versiones antiguas/Pelo de Will con la nuca larga (2026-11-08)/`.
- 8 nov 2026 (INC-1003): el brazo de piel se engrosó de 0,056–0,066 a 0,068–0,070 de radio, igual que el borde de la mano (0,067–0,078). Antes el antebrazo era ~20 % más fino que la mano y parecía que llevaba guantes. Se aplicó sobre `will.blend` (mismos vértices y pesos) y se reexportaron `Body23_Aventurero` y `Body24_AventureroChaqueta`; `will.py` lleva los radios nuevos. Lo anterior, en `Versiones antiguas/Brazos de Will con escalon en la muneca (2026-11-08)/`.

## 13 nov 2026 — chequeo de choques (INC-1138)
Pesos con `../Prendas/ropa_lib.py` (`completar_zonas`): antes NO PASA, ahora PASA. Informe y fotos en `chequeo/`. FBX nuevo copiado a Assets; el anterior, en `Versiones antiguas/Antes del chequeo de choques (2026-11-13)/`.
