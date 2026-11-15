# El deseo del Mago Oscuro: Cover en Suno

Maqueta guía: `deseo_mago_oscuro_guia_tema.mp3` (31,5 s, Re menor, 60 BPM, un tiempo por segundo),
hecha con `componer_tema.py` (mido) y pasada a audio con FluidSynth. Es la música del principio del
prólogo, `DeseoEnElSenderoBeat`: el Mago Oscuro pide su deseo en el Sendero y nota un poder lejano.

## La cadena de la apertura

El tema no va solo: acaba en **Re vacío** (Re-La-Re), que es la dominante de **Sol**, y engancha con
la crecida del vuelo (`../Amanecer en el Valle - apertura/`, 12 compases a 112 en Sol) y con
«Amanecer en el Valle» (Sol). La guía con las tres piezas seguidas está en
`../Amanecer en el Valle - apertura/apertura_completa_guia.mp3` (3:16).

Tiempos de la cinemática ampliada (`AperturaDelValleBeats.DeseoEnElSenderoBeat`, 31,5 s):

```
0-2       sale del negro: grave que aparece, campana lejana
2-6,5     de espaldas contra el halo: el motivo abajo (chelos y contrabajos)
6,5-10    los ojos se abren; el motivo sube a las trompas con coro y pulso; crece
10-14,5   de frente, pide el deseo (10,6): se adelgaza para dejar pasar la voz
14,5      estalla el altar (golpe; el pulso sigue); «¡Por fin!» (16,2), acento
17,8-22   la carcajada: el motivo corto y seco, temblor arriba, redoble al final
22-23,2   los ojos de golpe: todo se corta; campana y corazón
23,6      gira la cabeza: acento seco
24-27,5   aparece la estrella: celesta, arpa y cuerdas sobre Re («…¿Y ese poder?», 24,4)
27,5-30,3 crece hasta llenar la pantalla (Si♭-Do-Re), con redoble y coro
30,3      «CHAN» en Re vacío; se apaga con el fundido a papel (30,3-31,5)
```

## En Suno

1. Crear → Avanzado → **+ Audio → Subir** → `deseo_mago_oscuro_guia_tema.mp3`.
2. Comprobar que el desplegable del audio dice **Cover**.
3. Instrumental. **Estilos**:

```
dark anime villain theme, instrumental, ominous and theatrical, low string ostinato, bass
trombones and tuba, deep male choir, timpani hits and rolls, tubular bells, french horn motif,
slow 60 BPM, D minor, builds from near silence to a full orchestral hit, ends on an open D
power chord (no third), no vocals, no lyrics, no electronic drums, no fade out
```

4. **Letra** (estructura):

```
[Instrumental]
[Intro: grave y campana lejana, sale del negro]
[Theme: el motivo del mago abajo, chelos y contrabajos]
[Build: el motivo sube a las trompas con coro y pulso]
[Verse: se adelgaza, temblor de cuerdas (deja sitio a la voz)]
[Burst: golpe del altar, metales y timpano]
[Laugh: motivo corto y seco, cuerdas pizzicato]
[Stop: silencio con campana]
[Star: celesta y arpa sobre Re]
[Final: crece y «chan» en Re vacio que se apaga]
```

5. Título: `El deseo del Mago Oscuro (cover de la guia)`.
6. Al volver: sustituir el mp3 de `Assets/Audio/Music/Cinematicas/` y dar de alta la clave
   `PROLOGO_DESEO` en `AudioGraphProfile` con `sinBucle`; el guion la arranca al empezar el
   `DeseoEnElSenderoBeat` y la corta a los 31,5 s con `PROLOGO_AMANECER_VALLE`.
