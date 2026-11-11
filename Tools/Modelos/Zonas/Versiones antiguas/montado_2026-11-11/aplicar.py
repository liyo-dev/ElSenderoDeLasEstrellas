# Mete en el proyecto lo que hay en esta carpeta. Se ejecuta desde la raíz del proyecto, con Unity fuera de Play:
#   python3 Tools/Modelos/Zonas/pendiente_de_montar/aplicar.py
# Copia los FBX a Assets/Art/World/Zonas, el código de exploración al vestido y engancha lo nuevo en
# ModelosDeLasZonas y en VestidoDelMundo. Se puede repetir: no duplica lo que ya está.
import glob, os, shutil

AQUI = os.path.dirname(os.path.abspath(__file__))
FBX = "Assets/Art/World/Zonas"
VESTIDO = "Assets/Editor/VestidoDelMundo"

def leer(ruta):
    crudo = open(ruta, "rb").read()
    bom = crudo.startswith(b"\xef\xbb\xbf")
    texto = crudo.decode("utf-8-sig")
    return texto, bom, ("\r\n" if "\r\n" in texto else "\n")

def escribir(ruta, texto, bom):
    open(ruta, "wb").write((b"\xef\xbb\xbf" if bom else b"") + texto.encode("utf-8"))

def cambiar(ruta, viejo, nuevo, marca):
    texto, bom, nl = leer(ruta)
    if marca in texto:
        print("ya estaba:", ruta, "·", marca); return
    viejo, nuevo = viejo.replace("\n", nl), nuevo.replace("\n", nl)
    assert texto.count(viejo) == 1, f"no encuentro el punto de enganche en {ruta}: {viejo[:60]!r}"
    escribir(ruta, texto.replace(viejo, nuevo), bom)
    print("enganchado:", ruta, "·", marca)

for f in sorted(glob.glob(os.path.join(AQUI, "*.fbx"))):
    shutil.copyfile(f, os.path.join(FBX, os.path.basename(f)))
print(len(glob.glob(os.path.join(AQUI, "*.fbx"))), "FBX copiados a", FBX)
shutil.copyfile(os.path.join(AQUI, "VestidoDelMundo.Exploracion.cs"), os.path.join(VESTIDO, "VestidoDelMundo.Exploracion.cs"))

M = "Assets/Editor/Escenario/ModelosDeLasZonas.cs"
cambiar(M,
    '        "FuenteDeLaPlaza", "ArbolDePlaza", "PuestoDeMercado_Fruta", "PuestoDeMercado_Verdura", "PuestoDeMercado_Pan", "PuestoDeMercado_Pescado",\n    };',
    '        "FuenteDeLaPlaza", "ArbolDePlaza", "PuestoDeMercado_Fruta", "PuestoDeMercado_Verdura", "PuestoDeMercado_Pan", "PuestoDeMercado_Pescado",\n'
    '        "CasaDelPueblo_A", "CasaDelPueblo_B", "CasaDelPueblo_C", "CasaDelPueblo_D", "CasaDelPueblo_E", "CasaDelPueblo_F",\n'
    '        "GraneroDelPueblo", "PozoDelPueblo", "ArcoDelPueblo", "ArcoDeLaAldea",\n'
    '        "RepisaDeRoca_Grande", "RepisaDeRoca_Mediana", "RepisaDeRoca_Pequena", "EscalonesDePiedra", "PuenteColgante", "PuenteColgante_Largo",\n'
    '        "MiradorDeCumbre", "TunelDeRoca", "TunelEscalonado", "BocaDeCueva", "SalaDeLaCueva", "CortinaDeHiedra", "GrietaSecreta", "EntradaDeTemplo",\n    };',
    '"CasaDelPueblo_A"')
cambiar(M,
    '("ToldoRojo", Hex("e0483e")), ("ToldoVerde", Hex("3fa85c")), ("ToldoAmarillo", Hex("f0b83a")), ("Agua", Hex("2f86c0")),\n',
    '("ToldoRojo", Hex("e0483e")), ("ToldoVerde", Hex("3fa85c")), ("ToldoAmarillo", Hex("f0b83a")), ("Agua", Hex("2f86c0")),\n'
    '        ("Estuco", Hex("f3e3c0")), ("PiedraCampo", Hex("b8a88c")), ("Paja", Hex("e3b44a")), ("PajaSombra", Hex("c9982f")),\n'
    '        ("ContraRoja", Hex("c9553f")), ("ContraVerde", Hex("3f8f5c")), ("RojoGranero", Hex("b8483a")), ("Hierba", Hex("5fb85f")),\n'
    '        ("RocaOscura", Hex("4a4658")), ("SueloDeCueva", Hex("6b6470")), ("Hiedra", Hex("2f8f4e")), ("PiedraTemplo", Hex("d9d2c0")),\n'
    '        ("Musgo", Hex("6f9f5a")), ("Bronce", Hex("b8763a")), ("Portal", Hex("7fe0d8")),\n',
    '("Estuco", Hex("f3e3c0"))')
cambiar(M,
    '            if (nombre != "Banderines")\n',
    '            // Banderines e hiedra se atraviesan.\n            if (nombre != "Banderines" && nombre != "CortinaDeHiedra")\n',
    'nombre != "CortinaDeHiedra"')

cambiar(VESTIDO + "/VestidoDelMundo.Detalles.cs",
    '        ArbolDelPuebloInicial(o);\n        PoblarParajes(o);',
    '        ArbolDelPuebloInicial(o);\n        ExplorarElMundo(o);\n        PoblarParajes(o);',
    'ExplorarElMundo(o);')
print("Falta a mano: que «quitar el vestido» (VestidoDelMundo.cs, MenuQuitar) llame a CerrarElTerreno; "
      "después: refrescar, «Zonas: preparar las casas nuevas» y «vestir solo los detalles».")
