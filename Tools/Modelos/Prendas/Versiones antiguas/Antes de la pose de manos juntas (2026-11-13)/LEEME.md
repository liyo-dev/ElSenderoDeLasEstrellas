
## Zonas por forma y chequeo de piernas bajo falda (13 nov 2026, INC-1138)
- `rl.completar_zonas(GRUPOS["traje"])`: a cada pieza sin pesos dichos le pone su zona por el nombre y la forma
  (`zona_por_forma`): brazo (`Manga_l`, `Hombro_r`, `Brazal_l`, `CorreaBrazal_r340`…), pierna (`Bota`, `Media`,
  `Zapato`, `Suela`… con poco suavizado para que la rodilla doble como la del cuerpo), pernera (tronco y piernas),
  faldón si baja más de 6 cm de la cintura, y tronco lo demás. Las prendas de tronco se ciñen bajo el brazo
  (`ceñir_axila`, ancho máximo 0,205 entre z 0,54 y 0,70) y las piezas de pierna se parten en anillos de ~3 cm
  (`anillos_en_la_rodilla`): un tubo de tres anillos no puede doblar la rodilla.
- Lo usan ya todos los personajes: Will, Liam, Eldran, Victoria, Erika, Oliver, el Rey, el posadero, la boticaria
  (y el Archimago y el Mago Oscuro, con sus zonas dichas a mano). Todos en PASA.
- `chequeo_choques.py`: un punto del cuerpo cuenta como tapado si una prenda que en reposo ya hacía de piel
  (a menos de 5 cm, `CENIDA_MAX`) sigue a la misma distancia (+1 cm). Antes una media bajo la falda contaba como
  «cuerpo que asoma» en cuanto la falda se apartaba, aunque la media siguiera haciendo de pierna.
