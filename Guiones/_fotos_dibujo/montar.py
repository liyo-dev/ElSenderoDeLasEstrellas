# Monta antes/después lado a lado para revisar el aspecto de dibujo.
import sys
from PIL import Image
grupos = {
 'mundo': ['dib_pueblo.png', 'dib_pueblo_alto.png', 'dib_aldea.png'],
 'will':  ['Will_00_Neutral_cerca.png', 'Will_00_Neutral_medio.png', 'Will_00_Neutral_juego.png'],
}
w, h = 880, 495
for nombre, fotos in grupos.items():
    hoja = Image.new('RGB', (w * 2 + 6, (h + 6) * len(fotos)), (20, 20, 24))
    for i, f in enumerate(fotos):
        for j, carpeta in enumerate(('antes', 'despues')):
            try: im = Image.open(f'{carpeta}/{f}').convert('RGB').resize((w, h), Image.LANCZOS)
            except Exception as e: print('falta', carpeta, f); continue
            hoja.paste(im, (j * (w + 6), i * (h + 6)))
    hoja.save(f'comparacion_{nombre}.jpg', quality=88)
