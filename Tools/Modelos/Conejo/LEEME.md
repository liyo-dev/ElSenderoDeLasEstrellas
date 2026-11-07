# Conejo heraldo (Chuchelandia)

Hecho por script con Blender 5.2 (se puede abrir el .blend y retocarlo a mano).

1. `conejo.py`: modelo por piezas + esqueleto (nombres de Mecanim, `RightHandProp` para lo que lleve en la mano) → `Conejo_Heraldo.blend`.
2. `animar.py`: acciones Idle, Idle2, Dance, Walk, Run, Trompeta → `Conejo_Heraldo_anim.blend`.
3. `exportar.py`: FBX (un clip por acción) → `Assets/Art/Characters/Chuchelandia/ConejoHeraldo/Conejo_Heraldo.fbx`.

Con el módulo `bpy` (pip install bpy==5.2.2):
`python -c "import sys; sys.argv=['x','--','.']; exec(open('conejo.py').read())"`, y lo mismo con los otros dos (sus argumentos están al principio de cada script).
En Unity lo prepara `PersonajeHechoEnBlender` (importación, materiales Quibli lisos, controlador).

## Estado (1 nov 2026)
Raúl prefiere el heraldo del pack (Nutty_Knight): el conejo NO se usa en Chuchelandia, pero se conserva
(modelo, controlador `Assets/_NPCs/Chuchelandia/Animacion/Conejo_Heraldo.controller` y materiales
`Mat_Conejo_Heraldo_*`). Para volver a usarlo: `PersonajeHechoEnBlender.Preparar(...)` con su ficha
(colores: Pelaje, Rosa, Ojos, Brillo, Tabardo, Dorado; en bucle Idle, Walk, Run, Dance) y poner el FBX
y el controlador en la tabla de reparto.
