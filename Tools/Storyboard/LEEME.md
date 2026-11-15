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
4. Cada anotación queda con su elemento, su plano y su color. La lista de debajo del plano las
   enseña; la × las borra.
5. Al terminar, **Copiar resumen** y pégalo en el chat. Si el navegador no deja copiar, abre una
   ventana con el texto para copiarlo a mano. **Descargar JSON** guarda la geometría exacta de los
   trazos.

Las anotaciones se guardan en el navegador (`localStorage`) mientras no borres los datos del sitio.
Para conservarlas entre máquinas o mandármelas, usa el resumen o el JSON.

## Cómo se crea otro storyboard

1. Copia `datos.js` a otro archivo, o edita este.
2. Cambia `id`, `titulo`, `subtitulo` y la lista `paneles`.
3. Cada panel necesita:
   - `id`, `titulo`, `tiempo`, `descripcion`, `guion` (texto de contexto).
   - `imagen`: ruta relativa a la página.
   - `elementos`: lista de `{ id, etiqueta }` con **todo** lo que sale en el plano.
4. Cambia el `<script src="datos.js">` de `index.html` si usas otro archivo.

## El storyboard de prueba

`datos.js` trae los seis planos del previz real de **Cap. 3 · El espejo** (`TorreDeLiam`), sacados
del horneado con `PrevizDeGuion`. Los elementos de cada plano son los que salen en cuadro; la
cámara y el plano también se pueden anotar.

Cuando el encargo sea una cinemática nueva, el storyboard se dibuja antes de escribir el guion: la
página es la misma, cambian los paneles y las imágenes.
