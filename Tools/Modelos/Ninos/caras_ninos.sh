#!/bin/bash
# Caras de Tobías y Nina (estilo A, INC-918): caras_set.py con medidas de niño, cuerpo_nino.py con su talla y FBX.
#   bash caras_ninos.sh RUTA/modular.blend CARPETA_SALIDA   (con Blender 5.2 / módulo bpy en python3)
# Los FBX van luego a Assets/Art/Characters/Ninos/Tobias/ y Ninos/Nina/ (Caras_Tobias.fbx, Caras_Nina.fbx).
set -e
N=$(cd "$(dirname "$0")" && pwd); C="$N/../Caras"; MOD="$1"; OUT="$2"; mkdir -p "$OUT"
NINO="OJO_ESCALA=1.22 OJO_Z=0.93 OJO_X=0.13 BOCA_Z=0.825 BOCA_ESCALA=0.85 MEJILLAS=suave EXTRAS=0"
mk(){ # nombre cabeza estilo forma iris ojos bocas talla
  env $NINO OJO_FORMA=$4 python3 "$C/caras_set.py" -- "$MOD" $2 $3 $5 $6 $7 "$OUT/$1.blend"
  python3 "$N/cuerpo_nino.py" -- "$OUT/$1.blend" $8 "$OUT/${1}_t.blend"
  python3 "$C/exportar_caras.py" -- "$OUT/${1}_t.blend" "$OUT/Caras_$1.fbx"
  echo HECHO $1
}
mk Tobias Head01_Male chicoL chico marron 60 60 mayor      # ojos como los de Liam, su hermano
mk Nina Head02_Female chicaL redondo azul 70 70 pequeno    # ojos redondos de niña pequeña, con pestañas
