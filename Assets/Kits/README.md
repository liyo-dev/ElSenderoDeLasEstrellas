# Kits

Sistemas genéricos y reutilizables. Cada subcarpeta es un kit con su asmdef (`Kits.<Kit>`, y
`Kits.<Kit>.Editor` para sus herramientas) y su namespace `Kits.<Kit>`.

- Un kit no conoce el juego que lo usa: sin sus nombres, sus assets ni sus menús.
- Un kit solo usa otro kit por interfaces o eventos de `Kits.Core`.
- Tipos y API en inglés; comentarios en español. Menús en `Tools/<Kit>/…`.
- Lo que cambia de un juego a otro va en datos o detrás de una interfaz que el juego implementa.

El guardián (`Kits.EditorTools.KitGuard`) comprueba estas reglas; cada juego lo lanza desde un
test con su propia lista de nombres prohibidos.
