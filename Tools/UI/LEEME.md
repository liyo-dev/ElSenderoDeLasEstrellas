# Tools/UI — bocadillo de diálogo

Utilidades para revisar el arte del bocadillo (`SpeechBubbleUI`) **sin abrir Unity**.

| Archivo | Qué hace |
|---|---|
| `bocadillo_papel.py` | Traducción literal a Python de `PintarBocadillo()` (C#). Si cambias el C#, cambia aquí: es la maqueta fiel. |
| `bocadillo_preview.py` | Compara el bocadillo de ahora (el sprite `Speechbubble_0006` del atlas, emulando su 9-slice) con el nuevo. |
| `compilar_check.py` | Compila `Assembly-CSharp` con el compilador de Unity (Roslyn) usando el `.csproj` y las referencias del proyecto real, para saber si hay errores `CS` sin abrir el Editor. |

Uso:

```
python Tools/UI/bocadillo_preview.py      # -> Temp/UI/bocadillo_comparativa.png
python Tools/UI/compilar_check.py         # -> errores de compilación, si los hay
```

`compilar_check.py` necesita el proyecto real en
`C:\Users\luarb\dev\unity\ElSenderoDeLasEstrellas` (de ahí saca las referencias y
los defines que genera Unity). Si no está, avisa y no compila.
