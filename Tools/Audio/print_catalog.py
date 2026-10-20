import json

with open('Tools/Audio/final_catalog.json', 'r', encoding='utf-8') as f:
    items = json.load(f)

used_items = [it for it in items if it['used_in_project']]
unused_items = [it for it in items if not it['used_in_project']]

print(f"=== TEMAS ACTIVOS EN EL JUEGO ({len(used_items)}) ===")
for it in used_items:
    usages = ', '.join(it['agp_usages']) or 'MainMenu.unity'
    title = it['title'] or '(Sin TIT2)'
    sid = it['suno_id'] or '-'
    print(f"* Unity: {it['file']:<38} | Uso: {usages:<32} | Suno: {title} | ID: {sid}")

print(f"\n=== TEMAS EN LA CARPETA PERO SIN USO DIRECTO ({len(unused_items)}) ===")
for it in unused_items:
    title = it['title'] or '(Sin TIT2)'
    sid = it['suno_id'] or '-'
    print(f"* Unity: {it['file']:<38} | Suno: {title:<35} | ID: {sid}")
