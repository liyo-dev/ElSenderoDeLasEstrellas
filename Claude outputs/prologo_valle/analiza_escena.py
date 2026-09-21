import sys, re, os, collections
sys.path.insert(0,'.')
import unitylib as u
p = os.path.join(u.RAIZ, sys.argv[1])
t = open(p, encoding="utf-8", errors="ignore").read()
docs = re.split(r"^--- !u!(\d+) &(\d+)(?: stripped)?\s*$", t, flags=re.M)
out=[]
for i in range(1,len(docs),3):
    cid, body = docs[i], docs[i+2]
    if cid!="1001": continue
    g=re.search(r"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]+)", body)
    if not g: continue
    ruta=u.GUIDS.get(g.group(1),"?")
    def prop(k):
        m=re.search(r"propertyPath: %s\n      value: ([-\d.eE]+)\n"%re.escape(k), body)
        return float(m.group(1)) if m else None
    out.append((os.path.basename(ruta), ruta, prop("m_LocalScale.x")))
print(sys.argv[1], "→", len(out), "instancias")
fk=[o for o in out if "Fantasy_Kingdom" in o[1]]
print("  del pack:", len(fk))
c=collections.Counter((o[0], o[2]) for o in fk)
for (n,e),k in c.most_common(30): print("   %-28s escala=%s  x%d"%(n,e,k))
