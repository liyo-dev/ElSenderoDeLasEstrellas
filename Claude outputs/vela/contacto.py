import sys,os
from PIL import Image,ImageDraw
src,out,cols=sys.argv[1],sys.argv[2],int(sys.argv[3]) if len(sys.argv)>3 else 6
fs=sorted(f for f in os.listdir(src) if f.endswith('.png'))
if len(sys.argv)>4: fs=fs[int(sys.argv[4]):int(sys.argv[5])]
W,H=320,180
rows=(len(fs)+cols-1)//cols
im=Image.new('RGB',(cols*W,rows*(H+14)),'white')
d=ImageDraw.Draw(im)
for i,f in enumerate(fs):
    t=Image.open(os.path.join(src,f)).convert('RGB'); t.thumbnail((W,H))
    x,y=(i%cols)*W,(i//cols)*(H+14)
    im.paste(t,(x,y+14)); d.text((x+3,y+1),f[:-4],fill='black')
im.save(out,quality=85)
