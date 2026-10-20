import json
import os

with open('Tools/Audio/final_catalog.json', 'r', encoding='utf-8') as f:
    items = json.load(f)

hardcoded = {
    'La Melodia de Liam.mp3': 'Torre de Liam (ConstruirTorreDeLiam.cs)',
    'Tobias.mp3': 'Charlas sobre Tobías (Cinemáticas)',
    'la-melodia-de-eldran.mp3': 'Capítulo 2 (Cap2MusicaDeEldran.cs)',
    'menu-principal-el-sendero-v2-alegre.mp3': 'Menú Principal (PortadasDelMenuBuilder.cs)',
    'Brisa del Puerto.mp3': 'Puerto / Menú (PortadasDelMenuBuilder.cs)',
    'El Sendero de las Estrellas (Tema Principal).mp3': 'Tema Principal (PortadasDelMenuBuilder.cs)',
    'tinnitus.mp3': 'SFX Ensordecimiento (ModuloBatallaFinal.cs)'
}

for it in items:
    if it['file'] in hardcoded:
        it['hardcoded_script'] = hardcoded[it['file']]
        it['used_in_project'] = True

def get_category(it):
    f = it['file']
    usages = it.get('agp_usages', [])
    usages_str = ' '.join(usages)
    
    if f == 'tinnitus.mp3':
        return 'Miscelánea / SFX (A reubicar fuera de Música)'
    if f == 'Estela Furiosa.mp3':
        return 'Minijuegos y Eventos'
    if f in ['Tobias.mp3', 'La Melodia de Liam.mp3']:
        return 'Cinemáticas y Momentos Narrativos'
    if 'zoneId' in usages_str or 'sceneName' in usages_str:
        return 'Música Ambiental de Zonas y Escenas'
    if 'battleId' in usages_str:
        return 'Combate y Jefes'
    if 'sequenceId' in usages_str:
        return 'Cinemáticas y Momentos Narrativos'
    if ' 1.mp3' in f or f in ['Patio del Reino.mp3', 'Amanecer en el Valle.mp3', 'Battle_song.mp3', 'combate-con-npcs-v3-melodia.mp3', 'combate-contra-npc.mp3', 'Pesadilla en Suspenso.mp3', 'Susurros del Bosque.mp3', 'Batalla magos oscuro principal.mp3']:
        return 'Versiones Anteriores y Duplicados (A archivar)'
    return 'Temas en Reserva (Generados en Suno)'

cat_order = [
    'Música Ambiental de Zonas y Escenas',
    'Combate y Jefes',
    'Cinemáticas y Momentos Narrativos',
    'Minijuegos y Eventos',
    'Temas en Reserva (Generados en Suno)',
    'Versiones Anteriores y Duplicados (A archivar)',
    'Miscelánea / SFX (A reubicar fuera de Música)'
]

grouped = {c: [] for c in cat_order}
for it in items:
    cat = get_category(it)
    grouped[cat].append(it)

md = []
md.append('# Catálogo Maestro de Música y Correspondencia con Suno')
md.append('')
md.append('> **Proyecto:** *El Sendero de las Estrellas*  ')
md.append('> **Cuenta Suno:** `liyodev`  ')
md.append('> **Ubicación en Unity:** `Assets/Audio/Music/`  ')
md.append('')
md.append('Este documento cruza cada archivo de audio del proyecto con su **nombre original en Suno**, su **identificador único (ID)** y enlace directo a Suno (cuando está embebido en metadatos), y su **uso real en el juego** (perfil `AudioGraphProfile.asset`, escenas y scripts).')
md.append('')

total_files = len(items)
used_count = sum(1 for it in items if it.get('used_in_project'))
unused_count = total_files - used_count

md.append('## Resumen de Estado')
md.append(f'- **Total archivos en carpeta:** {total_files}')
md.append(f'- **En uso activo en el juego:** {used_count}')
md.append(f'- **Disponibles en reserva / archivo:** {unused_count}')
md.append('')

subfolder_map = {
    'Música Ambiental de Zonas y Escenas': 'Zonas/',
    'Combate y Jefes': 'Combate/',
    'Cinemáticas y Momentos Narrativos': 'Cinematicas/',
    'Minijuegos y Eventos': 'Cinematicas/',
    'Temas en Reserva (Generados en Suno)': 'Reserva/',
    'Versiones Anteriores y Duplicados (A archivar)': '_Archivo/',
    'Miscelánea / SFX (A reubicar fuera de Música)': '../SFX_Prologo/'
}

for cat in cat_order:
    cat_items = grouped[cat]
    if not cat_items:
        continue
    md.append(f'## {cat} ({len(cat_items)})')
    md.append('')
    md.append('| Archivo en Unity | Título en Suno | ID / Enlace en Suno | Uso en el Juego |')
    md.append('| :--- | :--- | :--- | :--- |')
    
    sub = subfolder_map.get(cat, '')
    for it in sorted(cat_items, key=lambda x: x['file'].lower()):
        f_name = f"`{sub}{it['file']}`"
        suno_title = it.get('title') or '*(Sin título ID3)*'
        sid = it.get('suno_id')
        if sid:
            suno_link = f"[{sid[:8]}...](https://suno.com/song/{sid})"
        else:
            suno_link = "*(Búsqueda por título)*"
            
        usages = []
        if it.get('agp_usages'):
            usages.extend(it['agp_usages'])
        if it.get('hardcoded_script'):
            usages.append(it['hardcoded_script'])
        if not usages and it.get('usage_refs'):
            usages.append(os.path.basename(it['usage_refs'][0]))
            
        usage_str = '<br>'.join(usages) if usages else '*(Reserva / Sin asignar)*'
        
        md.append(f"| {f_name} | **{suno_title}** | {suno_link} | {usage_str} |")
    md.append('')

md.append('## Prompts y Estilos Destacados Registrados en Metadatos')
md.append('')
md.append('| Título en Suno | Estilo / Prompt Original |')
md.append('| :--- | :--- |')
for it in sorted(items, key=lambda x: (it.get('title') or '').lower()):
    p = it.get('prompt')
    t = it.get('title')
    if p and t and t != '(Piano suave y melancólico, tempo lento)':
        p_clean = p.replace('\n', ' ').strip()
        if len(p_clean) > 180:
            p_clean = p_clean[:177] + '...'
        md.append(f"| **{t}** | `{p_clean}` |")
md.append('')

md.append('## Organización de la Carpeta (Completada)')
md.append('')
md.append('La carpeta `Assets/Audio/Music/` ha sido organizada con éxito en subcarpetas temáticas limpias conservando todos los `.meta` y GUIDs de Unity, y actualizando los scripts dependientes:')
md.append('')
md.append('1. **Fase 1: Archivar Duplicados y Tomas Obsoletas**')
md.append('   - Mover a `Assets/Audio/Music/_Archivo/`:')
md.append('     - `combate-con-npcs-v3-melodia 1.mp3`')
md.append('     - `combate-contra-npc 1.mp3`')
md.append('     - `Patio del Reino.mp3` (reemplazado por `Patio del Reino v2.mp3`)')
md.append('     - `Amanecer en el Valle.mp3` (reemplazado por `prologo-1-manana-del-valle-v4-leitmotiv.mp3`)')
md.append('     - `Battle_song.mp3` (sustituido por las nuevas versiones de combate)')
md.append('')
md.append('2. **Fase 2: Reubicar Efectos de Sonido que no son Música**')
md.append('   - `tinnitus.mp3`: Mover a `Assets/Audio/SFX_Prologo/tinnitus.mp3` y actualizar la línea en `ModuloBatallaFinal.cs`.')
md.append('')
md.append('3. **Fase 3: Separar por Subcarpetas Temáticas (Manteniendo .meta)**')
md.append('   - `Music/Zonas/` (Música ambiental de escenarios)')
md.append('   - `Music/Combate/` (Jefes y combates)')
md.append('   - `Music/Cinematicas/` (Momentos narrativos)')
md.append('   - `Music/Reserva/` (Canciones listas en Suno para futuros tramos)')
md.append('   > *Nota técnica:* Al mover archivos junto a su `.meta`, Unity conserva intactos los GUIDs de `AudioGraphProfile.asset` y las escenas.')

artifact_path = r'C:\Users\luarb\.gemini\antigravity\brain\8024f98d-5ccf-40c9-b10e-7def0c26ec03\catalogo_musica_suno.md'
with open(artifact_path, 'w', encoding='utf-8') as f:
    f.write('\n'.join(md))

print(f'Successfully generated artifact at: {artifact_path}')
