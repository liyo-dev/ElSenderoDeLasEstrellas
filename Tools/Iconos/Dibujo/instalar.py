# Dibuja los iconos de objetos, habilidades y estados de NPC con el aspecto de dibujo y los escribe
# encima de los del proyecto (mismo nombre y mismo tamaño en píxeles: se conservan GUID e import).
# Antes copia los originales a «Versiones antiguas». INC-1137.
# Uso (desde la raíz del proyecto): python Tools/Iconos/Dibujo/instalar.py
import os, sys, shutil
from PIL import Image
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from dibujo import Lienzo
import iconos_objetos as obj
import iconos_estado as est

UI = 'Assets/Art/UI'
COPIA = 'Versiones antiguas/Iconos de objetos, habilidades y estados antes del dibujo (2026-11-13)'

def tamano(ruta, por_defecto):
    try: return Image.open(ruta).size[0]
    except Exception: return por_defecto

def escribir(ruta, dibujar, por_defecto):
    t = tamano(ruta, por_defecto)
    if os.path.exists(ruta):
        destino = os.path.join(COPIA, os.path.relpath(ruta, UI))
        if not os.path.exists(destino):
            os.makedirs(os.path.dirname(destino), exist_ok=True)
            shutil.copy2(ruta, destino)
    L = Lienzo(t); dibujar(L); L.guardar(ruta)
    print(t, ruta)

for n in obj.OBJETOS: escribir(f'{UI}/Items/{n}.png', getattr(obj, n), 512)
for n in obj.HABILIDADES: escribir(f'{UI}/Abilities/{n}.png', getattr(obj, n), 512)
for n in est.ESTADOS: escribir(f'{UI}/NpcStatus/{n}.png', getattr(est, n), 1024)
for f in range(12): escribir(f'{UI}/NpcStatus/Charla/charla_%02d.png' % f, lambda L, f=f: est.charla(L, f), 512)
# Icono del escudo en el botón B del HUD: el mismo escudo que la habilidad.
escribir(f'{UI}/HUD/escudo_hud.png', obj.proteccion, 1024)
