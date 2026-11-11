# Monta la tira de un gesto (frente arriba, lado abajo) a partir de las fotos gesto_<Estado>_<n>_*.png.
import sys
from PIL import Image
for e in sys.argv[1:]:
    s = Image.new('RGB', (6 * 340, 2 * 330))
    for i in range(6):
        for j, v in enumerate(('frente', 'lado')):
            im = Image.open(f'gesto_{e}_{i}_{v}.png').convert('RGB'); w, h = im.size
            s.paste(im.crop((w // 2 - 300, h // 2 - 300, w // 2 + 300, h // 2 + 282)).resize((340, 330)), (i * 340, j * 330))
    s.save(f'../_fotos_dibujo/tira_{e}.jpg', quality=86)
