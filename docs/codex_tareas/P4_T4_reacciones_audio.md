# P4-T4 (INC-584) — Reacciones vocales sin repetición

Lee P4_comun.md y comun.md. En paralelo trabajan P4-T1 (cámara) y P4-T2 (movimiento). Tus archivos: Assets/Scripts/Sequences/Acting/ReaccionesDeEscena.cs, AccionDeEscenaBeat.cs, AudioService.cs (solo la parte de reacciones), VocalReactions.cs. NO toques ActorBeats.cs (lo edita P4-T2): GestureBeat ya delega en ReaccionesDeEscena.VozParaGesto, cambia el comportamiento ahí.
Raúl: «lo de "muy bien" que se dice en el baile es muy repetitivo; lo pondría una vez y ya».
- Las reacciones con palabras (cheer: «¡Bravo! ¡Eso es! ¡Muy bien!») suenan como mucho UNA vez por personaje-voz y secuencia (Vecino, Vecina, cada protagonista), y entre dos cheer de cualquier voz al menos 20 s. Las no verbales (laugh, gasp, sigh, hurt) pueden repetirse, alternando variantes y con al menos 4 s entre dos de la misma voz.
- GestureBeat.voz Automática ya no pone voz a Cheer01/02 ni a HandClap (solo laugh, gasp, hurt, sigh). La voz de ánimo Alegre de AccionDeEscenaBeat usa laugh si el cheer ya se gastó.
- AccionDeEscenaBeat.maxVoces por defecto 1.
- Estado por secuencia (se limpia al terminar/saltar), con ResetStatics si es estático.
## Terminado cuando
- Compila (Roslyn). TDD.md y fila INC-584 en TRACKER.md.
