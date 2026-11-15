# La música de la apertura del prólogo

Tres piezas seguidas, con el mismo hilo tonal (Re → Sol):

1. **El deseo del Mago Oscuro** (`../El deseo del Mago Oscuro/`): 31,5 s en Re menor, para el
   `DeseoEnElSenderoBeat`. Acaba en Re vacío.
2. **La crecida** (esta carpeta): 12 compases a 112 en Sol, 25,71 s. Crece desde casi nada y rompe
   en el tema justo cuando la cámara sube por la cascada y sale el logo. La toma de Suno alarga el
   respiro final: en el montaje el tema entra en 26,85 s.
3. **Amanecer en el Valle**: el tema en Sol. Es el que suena en el valle durante la mañana.

La secuencia arranca la música a los 31,5 s (al acabar el deseo), así que la crecida ocupa el
principio del vuelo y el tema entra a los 25,71 s de música, con el logo.

## Qué hay aquí

| Fichero | Qué es |
|---|---|
| `componer.py` | Maqueta MIDI de la crecida (mido). `python componer.py` deja el `.mid`. |
| `crecida_sola.mp3` | Render de la maqueta (FluidSynth), el que se subió a Suno. |
| `guia_A_rompe_al_principio_del_tema.mp3` | Maqueta de la cadena: crecida + tema desde el principio (2:44). |
| `guia_B_rompe_en_el_compas_9_del_tema.mp3` | La otra prueba: el tema entra en su compás 9 (2:27). |
| `crecida_de_la_apertura_suno.mp3` | **La toma buena de Suno** de la crecida (28 s). |
| `montar.py` | Pega la crecida (hasta 25,71 s) delante del tema. |
| `apertura_crecida_y_valle.mp3` | El montaje con la toma de Suno (2:46). Es lo que está en el juego. |
| `Amanecer en el Valle (tema, sin crecida).mp3` | El tema solo, guardado antes de sustituirlo. |

## En el juego

`Assets/Audio/Music/Cinematicas/Amanecer en el Valle.mp3` es el montaje (mismo GUID de siempre, así
que la regla `PROLOGO_AMANECER_VALLE` de `AudioGraphProfile` no se toca: `fadeIn` 1,5, `fadeOut` 3,
bucle). Suena desde el segundo 31,5 de `Prologo_UltimaNoche`.

## Rehacer el montaje con otra toma

`montar.py` necesita `ffmpeg` en el PATH. En este equipo está en el binario de `imageio-ffmpeg`:

```
python montar.py crecida.wav "amanecer.mp3" 0 salida.mp3 [rompe]
```

El tercer argumento es el segundo del tema por el que rompe (0 = desde el principio) y el quinto,
opcional, el segundo de la crecida en el que entra el tema (por defecto, 25,71 = 12 compases). El
script deja la crecida 4 dB por debajo del arranque del tema para que la entrada se sienta como una
subida y remata el corte con 60 ms de fundido para que no chasquee.
