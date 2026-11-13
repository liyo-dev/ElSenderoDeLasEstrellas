import subprocess, os, sys
from PIL import Image
blend, vis, out = sys.argv[1:4]
tiles=[]
for v in ("frente","tres","lado","espalda"):
    subprocess.run(["python3","foto.py","--",blend,f"_t_{v}.png",vis,v],check=True,capture_output=True); tiles.append(Image.open(f"_t_{v}.png")); os.remove(f"_t_{v}.png")
w,h=tiles[0].size; s=Image.new("RGB",(w*2,h*2))
for i,t in enumerate(tiles): s.paste(t,((i%2)*w,(i//2)*h))
s.save(out)
