# -*- coding: utf-8 -*-
"""Mide el tamaño real de una malla leyendo el FBX binario, sin Unity.

Existe porque colocar decorado sin saber cuánto mide cada cosa es adivinar, y el Fantasy Kingdom
Pack está modelado a una escala que no es la de los personajes del juego. La alternativa era
pedirle a Raúl que ejecutara la herramienta de Editor cada vez; esto lo hace aquí y es repetible.

Devuelve las dimensiones en unidades de Unity: vértices del FBX × factor de escala del importador
(0.01 por defecto en FBX de centímetros, o lo que diga el .meta del modelo)."""
import struct, zlib, os, re, json, sys

def _leer_nodo(d, pos, version):
    if version >= 7500:
        fin, nprops, proplen = struct.unpack_from("<QQQ", d, pos); pos += 24
    else:
        fin, nprops, proplen = struct.unpack_from("<III", d, pos); pos += 12
    nlen = d[pos]; pos += 1
    nombre = d[pos:pos+nlen].decode("ascii", "ignore"); pos += nlen
    if fin == 0:
        return None, pos, None
    props = []
    for _ in range(nprops):
        t = chr(d[pos]); pos += 1
        if t in "YCIFDL":
            fmt = {"Y":"<h","C":"<?","I":"<i","F":"<f","D":"<d","L":"<q"}[t]
            n = struct.calcsize(fmt)
            props.append(struct.unpack_from(fmt, d, pos)[0]); pos += n
        elif t in "fdlib":
            largo, enc, comp = struct.unpack_from("<III", d, pos); pos += 12
            bruto = d[pos:pos+comp]; pos += comp
            if enc == 1:
                bruto = zlib.decompress(bruto)
            fmt = {"f":"f","d":"d","l":"q","i":"i","b":"?"}[t]
            props.append(struct.unpack("<%d%s" % (largo, fmt), bruto[:largo*struct.calcsize(fmt)]))
        elif t in "SR":
            largo = struct.unpack_from("<I", d, pos)[0]; pos += 4
            props.append(d[pos:pos+largo]); pos += largo
        else:
            raise ValueError("tipo de propiedad FBX desconocido: %r" % t)
    hijos = []
    while pos < fin:
        hijo, pos, _ = _leer_nodo(d, pos, version)
        if hijo is None:
            break
    # los hijos se recorren por efecto lateral en RECOGIDA
        hijos.append(hijo)
    return (nombre, props, hijos), fin, None

RECOGIDA = []

def _recorrer(d, pos, fin, version, quiero):
    while pos < fin - 13:
        inicio = pos
        if version >= 7500:
            f, np_, pl = struct.unpack_from("<QQQ", d, pos); cab = 24
        else:
            f, np_, pl = struct.unpack_from("<III", d, pos); cab = 12
        if f == 0:
            return pos + cab + 1
        nlen = d[pos+cab]
        nombre = d[pos+cab+1:pos+cab+1+nlen].decode("ascii", "ignore")
        if nombre in quiero:
            nodo, _, _ = _leer_nodo(d, inicio, version)
            RECOGIDA.append(nodo)
        else:
            # entrar en los hijos
            sub = pos + cab + 1 + nlen + pl
            if sub < f:
                _recorrer(d, sub, f, version, quiero)
        pos = f
    return pos

def medir_fbx(ruta):
    d = open(ruta, "rb").read()
    if not d.startswith(b"Kaydara FBX Binary"):
        return None
    version = struct.unpack_from("<I", d, 23)[0]
    RECOGIDA.clear()
    _recorrer(d, 27, len(d), version, {"Vertices"})
    if not RECOGIDA:
        return None
    minimo = [1e30]*3; maximo = [-1e30]*3
    for nodo in RECOGIDA:
        for p in nodo[1]:
            if isinstance(p, tuple) and len(p) >= 3:
                for i in range(0, len(p) - 2, 3):
                    for eje in range(3):
                        val = p[i+eje]
                        if val < minimo[eje]: minimo[eje] = val
                        if val > maximo[eje]: maximo[eje] = val
    if minimo[0] > 1e29:
        return None
    return minimo, maximo

def factor_escala(ruta_fbx):
    """Factor del importador (el .meta manda: useFileScale + globalScale)."""
    meta = ruta_fbx + ".meta"
    if not os.path.exists(meta):
        return 0.01
    t = open(meta, encoding="utf-8", errors="ignore").read()
    g = re.search(r"globalScale: ([\d.]+)", t)
    u = re.search(r"useFileScale: (\d)", t)
    escala = float(g.group(1)) if g else 1.0
    # useFileScale 1 => se aplica la unidad del fichero (cm -> 0.01)
    return escala * (0.01 if (u and u.group(1) == "1") else 1.0)

if __name__ == "__main__":
    for ruta in sys.argv[1:]:
        r = medir_fbx(ruta)
        if not r:
            print("??", ruta); continue
        mn, mx = r
        k = factor_escala(ruta)
        print("%-42s  %6.2f x %6.2f x %6.2f m  (base y=%.2f)" % (
            os.path.basename(ruta), (mx[0]-mn[0])*k, (mx[1]-mn[1])*k, (mx[2]-mn[2])*k, mn[1]*k))
