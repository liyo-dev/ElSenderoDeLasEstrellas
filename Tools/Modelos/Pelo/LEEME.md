# La nuca de los pelos hechos aquí (11 nov 2026, INC-1061)

Raúl, con una captura de Will de perfil: «parte del pelo parece que está mal; en Nina me da la sensación de que
también». Los cinco pelos propios (Will `Hair16_Heroe`, Liam `Hair15_Flequillo`, Estela `Hair14_Coleta`, Nina
`Hair18_Coletas`, Tobías `Hair17_Tobias`) acaban en la base del cráneo, y entre ese borde y la ropa quedaba piel
a la vista: de lado y de espaldas parecía una calva detrás de la oreja.

- `nuca.py`: añade al pelo una capa pegada a la cabeza (la propia superficie de la cabeza, separada 8 mm) por
  detrás de las orejas y hasta abajo, del color del pelo y pesada a «head». Gira con la cabeza y no sobresale
  (lo que se quitó en INC-943 era pelo que colgaba por debajo del cráneo; esto no cuelga). La línea del pelo no
  baja recta: por debajo de la oreja se va hacia atrás.
  Argumentos: blend, nombre del pelo, cabeza (objeto del blend, o FBX de cabeza suelta del pack) y salida.
- `nuca_foto.py`: foto de comprobación (lado, tres cuartos por detrás, espalda y desde abajo).
- `nucas_antes_y_despues.jpg`: los cinco, antes a la izquierda y después a la derecha.
- `para_unity/<Personaje>/`: los cinco FBX nuevos, con el mismo nombre que los del juego. **No están en Assets**:
  se copian encima de los de `Assets/Art/Characters/Protagonistas/<Nombre>/` y `Ninos/<Nombre>/` cuando Unity
  esté libre (los de ahora, antes, a `Versiones antiguas/`). No hace falta pasar ningún menú: misma pieza, mismo
  nombre, mismos materiales.

**Al regenerar un pelo con su script (`will.py`, `liam.py`, `estela.py`, `nina.py`, `tobias.py`) hay que pasarle
después `nuca.py`**: los scripts de cada personaje no lo llevan dentro.

Los `.blend` de cada personaje ya llevan la nuca; los de antes, en
`Versiones antiguas/Pelos sin nuca (2026-11-11)/`.

Sin comprobar en el juego. Desde abajo queda un poco de piel en la parte inferior de la cabeza (mira al suelo y
la tapa el cuello de la ropa). Los pelos del pack que llevan los NPCs no se han revisado.
