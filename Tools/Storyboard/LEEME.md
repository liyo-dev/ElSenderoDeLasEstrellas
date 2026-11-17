# Storyboard para revisar cinemáticas

Una página local para revisar un storyboard y dejar anotaciones **pegadas a cada elemento** de cada
plano. No hace falta Unity ni servidor.

## Abrirla

Doble clic en `Tools/Storyboard/index.html`.

Si el navegador no deja cargar las imágenes desde `file://`, abre una consola en la raíz del
proyecto y sirve la carpeta:

```
python -m http.server 8765
```

y entra en `http://localhost:8765/Tools/Storyboard/index.html`.

## Cómo se usa

1. Elige un plano de la tira.
2. En su leyenda, elige el elemento: **Plano / general**, **Liam**, **Bola de visión**, **Cámara**…
3. Escribe una nota o elige una herramienta y dibuja sobre la imagen:
   - **Flecha**: de un punto a otro.
   - **Trazo libre**: un recorrido, una trayectoria, una corrección a mano.
   - **Caja**: una zona del plano.
4. Elige el **color** en la barra: **Auto** usa el color del elemento (Liam amarillo, bola cian,
   cámara naranja…); un círculo lo fija para la siguiente nota o trazo.
5. Cada anotación queda con su elemento, su plano y su color. La lista de debajo del plano las
   enseña; la × las borra.
6. Al terminar, **Guardar correcciones** deja `correcciones-<guion>.json` (en Descargas o donde elijas).
   En Unity: **Liyodev ▸ Director ▸ Aplicar correcciones del storyboard…**, eliges ese fichero y el
   Editor hace lo que el guion sabe solo: si la nota es de la cámara y dice `alejar/abrir` baja el
   `lente` 6, y si dice `acercar/cerrar` lo sube 8; escribe ese `lente=` en la línea del plano,
   rehornea y regenera esta página. **Lo demás no se pierde**: queda como **encargo para la IA** en
   `Guiones/_revisiones/encargos-<guion>.json` (y su vista `.md`), con el plano, la línea del guion y el
   fotograma. La mesa lo enseña con el botón **Encargos de la IA (N)**; una sesión de IA los lee por el
   buzón (`encargos <guion>`), hace el cambio (guion, previz o lo que pida), rehornea y los cierra con
   `revisiones-hechas <guion>` (o `revisiones-hechas <guion> P06`). También vale por el buzón:
   `revisiones Guiones/_revisiones/<fichero>.json`.
   **Copiar resumen** sigue sirviendo para pegarlo en el chat y hablarlo.

   Las notas guardadas **desaparecen de la página** (ya van en el fichero) y Unity lleva su propio
   libro de aplicadas (`Guiones/_revisiones/aplicadas-<guion>.json`): mandar dos veces el mismo fichero
   no aplica dos veces el cambio. Los encargos pendientes se quedan hasta que se cierran. En la mesa,
   las notas de la pizarra **se borran al aplicarse o al cerrarse su encargo**.

Las anotaciones se guardan en el navegador (`localStorage`) mientras no borres los datos del sitio.
Para conservarlas entre máquinas o mandármelas, usa el resumen o el JSON.

## Cómo se crea otro storyboard

**Con un guion horneado, se genera solo:** selecciona su `GuionHorneado` en el Project (si no hay
ninguno seleccionado, coge el del prólogo) y pasa *El Sendero ▸ Cinemáticas ▸ Guion ▸ Storyboard
anotable: generar la página del guion*. Deja en esta carpeta:

- `datos_<Guion>.js` con un panel por plano: la foto, su tiempo, lo que se dice, la línea del guion
  que lo pide (con su número) y un elemento por personaje que sale o habla, más los efectos con
  nombre (orbe, círculos, agujero negro, forcejeo) y la cámara.
- `<Guion>.html`, la misma página apuntando a esos datos. Se abre con doble clic.

Se vuelve a pasar el menú después de cada horneado y la página queda al día. No se editan a mano.

Sin tocar el ratón, por el buzón de órdenes (`Guiones/_ordenes/`): `storyboard Assets/_SEQUENCES/Guiones/<Guion>_Horneado.asset`
(también vale el nombre del guion). El menú usa lo que haya seleccionado en el Project; la orden no.

Si hace falta otro storyboard sin horneado, se copia `datos.js` a otro archivo, se cambian `id`,
`titulo`, `subtitulo` y `paneles`, y se cambia el `<script src="datos.js">` de una copia de
`index.html`. Cada panel necesita `id`, `titulo`, `tiempo`, `descripcion`, `guion`, `imagen` y
`elementos` (`{ id, etiqueta }`).

## El storyboard de prueba

`datos.js` trae los seis planos del previz real de **Cap. 3 · El espejo** (`TorreDeLiam`), sacados
del horneado con `PrevizDeGuion`. Los elementos de cada plano son los que salen en cuadro; la
cámara y el plano también se pueden anotar.

`Prologo_UltimaNoche.html` es el del prólogo, generado del horneado real (58 planos).

Cuando el encargo sea una cinemática nueva, el storyboard se dibuja antes de escribir el guion: la
página es la misma, cambian los paneles y las imágenes.
