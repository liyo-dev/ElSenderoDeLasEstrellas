import os
import json

music_dir = r'Assets/Audio/Music'

def parse_id3(filepath):
    meta = {
        'file': os.path.basename(filepath),
        'title': None,
        'artist': None,
        'album': None,
        'genre_prompt': None,
        'comments': [],
        'other': {}
    }
    try:
        with open(filepath, 'rb') as f:
            header = f.read(10)
            if header[:3] != b'ID3':
                return meta
            ver = header[3]
            tag_size = 0
            for b in header[6:10]:
                tag_size = (tag_size << 7) | (b & 0x7f)
            
            data = f.read(tag_size)
            pos = 0
            while pos < len(data) - 10:
                frame_id = data[pos:pos+4].decode('latin1', errors='ignore')
                if not (frame_id.isalnum() and len(frame_id) == 4):
                    break
                if ver >= 4:
                    fsize = 0
                    for b in data[pos+4:pos+8]:
                        fsize = (fsize << 7) | (b & 0x7f)
                else:
                    fsize = int.from_bytes(data[pos+4:pos+8], byteorder='big')
                
                pos += 10
                if pos + fsize > len(data):
                    break
                fdata = data[pos:pos+fsize]
                pos += fsize
                
                text = None
                if fdata.startswith(b'\x01\xff\xfe'):
                    text = fdata[3:].decode('utf-16-le', errors='ignore').rstrip('\x00')
                elif fdata.startswith(b'\x00'):
                    text = fdata[1:].decode('latin1', errors='ignore').rstrip('\x00')
                elif fdata.startswith(b'\x03'):
                    text = fdata[1:].decode('utf-8', errors='ignore').rstrip('\x00')
                
                if frame_id == 'TIT2':
                    meta['title'] = text
                elif frame_id == 'TPE1':
                    meta['artist'] = text
                elif frame_id == 'TALB':
                    meta['album'] = text
                elif frame_id == 'TCON':
                    meta['genre_prompt'] = text
                elif frame_id.startswith('T'):
                    if text:
                        meta['other'][frame_id] = text[:150]
                elif frame_id == 'COMM':
                    try:
                        comm_text = fdata[4:].decode('latin1', errors='ignore').replace('\x00', ' ').strip()
                        meta['comments'].append(comm_text)
                    except:
                        pass
    except Exception as e:
        meta['error'] = str(e)
    return meta

files = sorted([f for f in os.listdir(music_dir) if f.endswith('.mp3')])
results = [parse_id3(os.path.join(music_dir, f)) for f in files]

with open('Tools/Audio/music_scan_results.json', 'w', encoding='utf-8') as out:
    json.dump(results, out, ensure_ascii=False, indent=2)

print(f"Scanned {len(results)} files. Summary:")
for r in results:
    t = r['title'] or '(Sin TIT2)'
    a = r['artist'] or '-'
    alb = r['album'] or '-'
    print(f"{r['file']} | Suno Title: {t} | Artist: {a} | Album: {alb}")
