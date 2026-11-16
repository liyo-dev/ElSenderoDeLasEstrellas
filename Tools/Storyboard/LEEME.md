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
6. Al terminar, **Copiar resumen** y pégalo en el chat. Si el navegador no deja copiar, abre una
   ventana con el texto para copiarlo a mano. **Descargar JSON** guarda la geometría exacta de los
   trazos.

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
