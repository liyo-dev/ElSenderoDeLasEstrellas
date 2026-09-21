import sys, re, os, collections
sys.path.insert(0,'.')
import unitylib as u
p = os.path.join(u.RAIZ, "Assets/Art/World/Fantasy_Kingdom_Pack/Demo/01.unity")
t = open(p, encoding="utf-8", errors="ignore").read()
docs = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\s*$", t, flags=re.M)
items=[]
for i in range(1,len(docs),3):
    cid, fid, body = docs[i], docs[i+1], docs[i+2]
    if cid!="1001": continue
    g=re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)", body)
    if not g: continue
    nombre=os.path.basename(u.GUIDS.get(g.group(1),"?"))
    def prop(k):
        m=re.search(r"propertyPath: %s\n      value: ([-\d.eE]+)\n"%re.escape(k), body)
        return float(m.group(1)) if m else None
    items.append((nombre, prop("m_LocalPosition.x"), prop("m_LocalPosition.y"), prop("m_LocalPosition.z"), prop("m_LocalScale.x")))
print("instancias:", len(items))
esc=collections.Counter(round(i[4],2) if i[4] is not None else None for i in items)
print("escalas usadas:", esc.most_common(6))
fam=collections.Counter(re.sub(r"[0-9_].*","",n) for n,_,_,_,_ in items)
print("familias:", fam.most_common(18))
casas=[i for i in items if i[0].startswith(("Building","Church","Well","Bridge"))]
print("\nedificios (nombre, x, z):")
for c in casas[:40]: print("  ", c[0], c[1], c[3])
xs=[i[1] for i in items if i[1] is not None]; zs=[i[3] for i in items if i[3] is not None]
if xs: print("\nextensión de la escena demo: X %.0f..%.0f  Z %.0f..%.0f"%(min(xs),max(xs),min(zs),max(zs)))
