# Genera los juegos de caras de anime (ojos con cejas, y bocas en los niños) del kit de NPCs y los de Will, Liam y Estela,
# cada uno en su FBX. Usa caras_set.py y exportar_caras.py de esta carpeta, con Blender 5.2 (módulo bpy).
#   args: modular.blend (ModularCharacterPolyart.fbx importado tal cual), carpeta de salida de los FBX
#   (p. ej. Assets/Art/Characters/NPCs/Caras/; los de los protagonistas, Caras_chic*, van luego a Protagonistas/Caras/).
#   python kit_npcs.py -- modular.blend salida [clave …]   (sin claves: todos)
# La tabla de qué NPC lleva cada juego está en Unity: Assets/Editor/Personajes/CarasDeLosNPCs.cs (mismas claves).
import os, sys, subprocess
D = os.path.dirname(os.path.abspath(__file__))
a = sys.argv[sys.argv.index("--") + 1:]
MOD, SALIDA, PEDIDOS = a[0], a[1], set(a[2:])
NINO = {"OJO_ESCALA": "1.22", "OJO_Z": "0.93", "OJO_X": "0.13", "BOCA_Z": "0.825", "BOCA_ESCALA": "0.85"}
# Mejillas: "" (nada), suave, tostada, pecas, mayor (combinables con «+»). Cejas: fina, gruesa, poblada (ver caras_lib.txt).
# Extras (guiño, conjuro, vergüenza, risa con colmillo): solo Estela. Todos los juegos acaban con los ojos de llorar y
# traen los símbolos de anime (Fx<ojos×10…>), así que cada juego ocupa 10 números de ojos (12+1 = 13 Estela).
#  FBX                            cabeza          estilo   forma      iris     ojos  bocas  niño   mejillas        cejas      extras
KIT = [
    ("Caras_chico_marron",            "Head01_Male",   "chicoL", "chico",    "marron", 20,  20,  False, "suave",        "fina",    False),   # Liam
    ("Caras_chico_verde",             "Head01_Male",   "chicoL", "chico",    "verde",  30,  0,   False, "suave",        "fina",    False),   # Will
    ("Caras_chica_marron",            "Head02_Female", "chicaL", "noventa",  "marron", 40,  40,  False, "suave",        "fina",    True),    # Estela
    ("Caras_NPC_h_chico_azul",        "Head01_Male",   "chicoL", "chico",    "azul",   80,  0,   False, "",             "fina",    False),
    ("Caras_NPC_h_chico_gris",        "Head01_Male",   "chicoL", "chico",    "gris",   90,  0,   False, "",             "fina",    False),
    ("Caras_NPC_h_chico_oscuro",      "Head01_Male",   "chicoL", "chico",    "oscuro", 100, 0,   False, "",             "gruesa",  False),
    ("Caras_NPC_h_almendra_marron",   "Head01_Male",   "chicoL", "almendra", "marron", 110, 0,   False, "",             "gruesa",  False),
    ("Caras_NPC_h_almendra_verde",    "Head01_Male",   "chicoL", "almendra", "verde",  120, 0,   False, "",             "fina",    False),
    ("Caras_NPC_h_almendra_ambar",    "Head01_Male",   "chicoL", "almendra", "ambar",  130, 0,   False, "",             "fina",    False),
    ("Caras_NPC_m_almendra_marron",   "Head02_Female", "chicaL", "almendra", "marron", 140, 0,   False, "",             "fina",    False),
    ("Caras_NPC_m_almendra_azul",     "Head02_Female", "chicaL", "almendra", "azul",   150, 0,   False, "",             "fina",    False),
    ("Caras_NPC_m_afilado_verde",     "Head02_Female", "chicaL", "afilado",  "verde",  160, 0,   False, "",             "fina",    False),
    ("Caras_NPC_m_almendra_oscuro",   "Head02_Female", "chicaL", "almendra", "oscuro", 170, 0,   False, "",             "fina",    False),
    ("Caras_NPC_hf_chico_marron",     "Head02_Female", "chicoL", "chico",    "marron", 180, 0,   False, "",             "fina",    False),   # hombre con cabeza de mujer
    ("Caras_NPC_hf_almendra_gris",    "Head02_Female", "chicoL", "almendra", "gris",   190, 0,   False, "",             "gruesa",  False),
    ("Caras_NPC_mh_almendra_marron",  "Head01_Male",   "chicaL", "almendra", "marron", 200, 0,   False, "",             "fina",    False),   # mujer con cabeza de hombre
    ("Caras_NPC_nino_chico_ambar",    "Head01_Male",   "chicoL", "chico",    "ambar",  210, 210, True,  "suave",        "fina",    False),
    ("Caras_NPC_nina_almendra_azul",  "Head02_Female", "chicaL", "almendra", "azul",   220, 220, True,  "suave",        "fina",    False),
    # 4 nov 2026: mayores, gente del campo y jóvenes con pecas
    ("Caras_NPC_h_mayor_ambar",       "Head01_Male",   "chicoL", "almendra", "ambar",  230, 0,   False, "mayor",        "poblada", False),   # Eldran
    ("Caras_NPC_hf_mayor_marron",     "Head02_Female", "chicoL", "chico",    "marron", 240, 0,   False, "mayor",        "poblada", False),   # el Rey
    ("Caras_NPC_m_mayor_azul",        "Head02_Female", "chicaL", "almendra", "azul",   250, 0,   False, "mayor",        "gruesa",  False),   # Victoria
    ("Caras_NPC_h_tostado_marron",    "Head01_Male",   "chicoL", "almendra", "marron", 260, 0,   False, "tostada",      "gruesa",  False),   # guardias
    ("Caras_NPC_h_pecas_azul",        "Head01_Male",   "chicoL", "chico",    "azul",   270, 0,   False, "pecas",        "fina",    False),   # Oliver
    ("Caras_NPC_m_pecas_marron",      "Head02_Female", "chicaL", "almendra", "marron", 280, 0,   False, "pecas+suave",  "fina",    False),   # Vicky
]
os.makedirs(SALIDA, exist_ok=True)
for fbx, cabeza, estilo, forma, iris, ojos, bocas, nino, mejillas, cejas, extras in KIT:
    if PEDIDOS and fbx not in PEDIDOS and fbx.replace("Caras_NPC_", "") not in PEDIDOS: continue
    env = dict(os.environ, MEJILLAS=mejillas, CEJAS=cejas, EXTRAS="1" if extras else "0", OJO_FORMA=forma, **(NINO if nino else {}))
    blend = os.path.join(SALIDA, fbx + ".blend")
    subprocess.run([sys.executable, os.path.join(D, "caras_set.py"), "--", MOD, cabeza, estilo, iris, str(ojos), str(bocas), blend], env=env, check=True)
    subprocess.run([sys.executable, os.path.join(D, "exportar_caras.py"), "--", blend, os.path.join(SALIDA, fbx + ".fbx")], check=True)
    os.remove(blend)
    print("HECHO", fbx)
