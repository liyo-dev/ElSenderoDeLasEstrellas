import os
import re
import json

music_dir = r'Assets/Audio/Music'
files = sorted([f for f in os.listdir(music_dir) if f.endswith('.mp3')])

catalog = []
for fname in files:
    fpath = os.path.join(music_dir, fname)
    suno_id = None
    created = None
    
    with open(fpath, 'rb') as f:
        data = f.read(65536)
        
        m_id = re.search(rb'id=([0-9a-fA-F\-]{36})', data)
        if m_id:
            suno_id = m_id.group(1).decode('ascii')
        else:
            m_id_u16 = re.search(rb'i\x00d\x00=\x00([0-9a-fA-F\-\x00]{71})', data)
            if m_id_u16:
                suno_id = m_id_u16.group(1).replace(b'\x00', b'').decode('ascii')
        
        m_cr = re.search(rb'created=([0-9\-\:TZ\.]+)', data)
        if m_cr:
            created = m_cr.group(1).decode('ascii')
        else:
            m_cr_u16 = re.search(rb'c\x00r\x00e\x00a\x00t\x00e\x00d\x00=\x00([0-9\-\:TZ\.\x00]{40,60})', data)
            if m_cr_u16:
                created = m_cr_u16.group(1).replace(b'\x00', b'').decode('ascii')
                
    catalog.append({
        'file': fname,
        'suno_id': suno_id,
        'suno_url': f'https://suno.com/song/{suno_id}' if suno_id else None,
        'created': created
    })

with open('Tools/Audio/music_scan_results.json', 'r', encoding='utf-8') as f:
    id3_list = {r['file']: r for r in json.load(f)}

with open('Tools/Audio/music_usage.json', 'r', encoding='utf-8') as f:
    usage_list = json.load(f)

for item in catalog:
    f = item['file']
    meta = id3_list.get(f, {})
    item['title'] = meta.get('title')
    item['artist'] = meta.get('artist')
    item['album'] = meta.get('album')
    item['prompt'] = meta.get('genre_prompt')
    item['used_in_project'] = len(usage_list.get(f, [])) > 0
    item['usage_refs'] = usage_list.get(f, [])

with open('Tools/Audio/full_music_catalog.json', 'w', encoding='utf-8') as out:
    json.dump(catalog, out, indent=2, ensure_ascii=False)

print(f"Processed {len(catalog)} tracks.")
for item in catalog:
    status = '[USED]' if item['used_in_project'] else '[UNUSED]'
    suno_name = item['title'] or '(Sin nombre)'
    sid = item['suno_id'] or '(Sin ID)'
    print(f"{status:<8} {item['file']:<40} | Suno: {suno_name:<35} | ID: {sid}")
