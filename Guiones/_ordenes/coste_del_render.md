# Coste del render por piezas

- Escena activa: MainWorld; centro (67.40, 33.50, -14.10); pantalla 1920×1080; 9999 renderers activos
- MainWorld: recorte por tamaño vigila 5926, sin dibujar ahora 707

Mediana de GPU (ms) / CPU (ms) por rumbo (0 = norte):

| Prueba | 0° | 90° | 180° | 270° | media GPU | ahorro GPU |
|---|---|---|---|---|---|---|
| Todo como en el juego | 36,2 / 36,9 | 30,9 / 31,7 | 4,8 / 46,8 | 4,2 / 41,4 | 19,0 | — |
| Sin recorte por tamaño en pantalla (ScreenSizeCuller) | 5,4 / 44,1 | 5,6 / 45,6 | 5,4 / 42,4 | 27,3 / 26,5 | 10,9 | 8,1 |
| Sin sombras | 6,4 / 43,1 | 5,2 / 42,4 | 5,3 / 41,9 | 26,2 / 24,0 | 10,8 | 8,2 |
| Sin post-procesado ni contorno (ni su prepase de normales) | 5,1 / 45,6 | 5,3 / 43,1 | 5,7 / 42,7 | 27,3 / 24,6 | 10,8 | 8,2 |
| Sin flores ni árboles del terreno | 6,1 / 44,4 | 5,9 / 44,6 | 5,6 / 40,6 | 26,3 / 25,7 | 11,0 | 8,0 |
| Sin la cámara del minimapa | 34,2 / 31,2 | 1,2 / 40,1 | 2,3 / 38,6 | 25,2 / 24,3 | 15,7 | 3,3 |
| Sin el vestido del mundo (3876 renderers) | 27,3 / 29,2 | 25,0 / 24,7 | 25,9 / 28,9 | 25,9 / 27,5 | 26,0 | -7,0 |
