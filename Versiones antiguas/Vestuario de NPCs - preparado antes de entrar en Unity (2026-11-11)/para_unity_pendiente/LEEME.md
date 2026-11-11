# Vestuario de los NPCs: lo que falta para que entre al juego (11 nov 2026)

Preparado sin tocar Unity (Raúl estaba probando). Cuando dé el visto bueno a las previas y Unity esté libre:

1. Copiar cada `Tools/Modelos/<Personaje>/para_unity/*.fbx` a `Assets/Art/Characters/NPCs/Vestuario/<Personaje>/`
   (Eldran, Victoria, Erika, Oliver, Rey, Posadero, Boticaria).
2. Copiar `VestuarioDeLosNPCs.cs.txt` a `Assets/Editor/Personajes/VestuarioDeLosNPCs.cs` (va con la extensión
   `.txt` para que nada lo compile antes de tiempo).
3. `refrescar` y el menú *El Sendero ▸ Personajes ▸ Vestuario propio de los NPCs*.
4. Pasar los materiales nuevos al shader de dibujo (*El Sendero ▸ Aspecto de dibujo ▸ Pasar los materiales…*)
   y hacer la hoja de fotos de los NPCs para comprobarlos.

Sin probar: el script no se ha compilado nunca. Dudas abiertas, apuntadas en cada LEEME: el Rey (qué cabeza y
pelo lleva bajo la corona, y poner `AC06_Crown01` en lugar de `Hat10`), el color del moño de Victoria (debe ser
el material de su pelo) y cómo se mueven las prendas largas al andar.
