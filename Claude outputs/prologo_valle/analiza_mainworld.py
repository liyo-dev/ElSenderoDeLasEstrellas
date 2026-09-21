import sys, re, os, collections
sys.path.insert(0,'.')
import unitylib as u
p = os.path.join(u.RAIZ, "Assets/Scenes/Worlds/MainWorld.unity")
t = open(p, encoding="utf-8", errors="ignore").read()
docs = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\s*$", t, flags=re.M)
items=[]
for i in range(1,len(docs),3):
    cid, fid, body = docs[i], docs[i+1], docs[i+2]
    if cid!="1001": continue
    g=re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)", body)
    if not g: continue
    ruta=u.GUIDS.get(g.group(1),"?")
    nombre=os.path.basename(ruta)
    def prop(k):
        m=re.search(r"propertyPath: %s\n      value: ([-\d.eE]+)\n"%re.escape(k), body)
        return float(m.group(1)) if m else None
    nm=re.search(r"propertyPath: m_Name\n      value: (.*)\n", body)
    items.append((nombre, ruta, nm.group(1).strip() if nm else "", prop("m_LocalPosition.x"), prop("m_LocalPosition.y"), prop("m_LocalPosition.z"), prop("m_LocalScale.x")))
print("instancias de prefab en MainWorld:", len(items))
fk=[i for i in items if "Fantasy_Kingdom" in i[1]]
print("del Fantasy Kingdom Pack:", len(fk))
esc=collections.Counter(round(i[6],3) if i[6] is not None else "sin cambio" for i in fk)
print("escalas:", esc.most_common(10))
fam=collections.Counter(re.sub(r"\d.*","",i[0]) for i in fk)
print("familias:", fam.most_common(20))
print("\nEdificios/estructuras y su escala y posición:")
for i in fk:
    if re.match(r"(Building|Church|Well|Bridge|Tower|Windmill|Shop)", i[0]):
        print("   %-24s %-22s esc=%s  pos=(%s, %s, %s)"%(i[0], i[2][:20], i[6], i[3], i[4], i[5]))
