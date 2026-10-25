"""Comprueba que el código compile con el compilador de Unity (Roslyn).

Toma el .csproj del proyecto real (que tiene las referencias y los defines
generados por Unity) y compila las fuentes del worktree actual contra ellas.
Si el proyecto real no está en disco, solo avisa.

Uso:
    python Tools/UI/compilar_check.py
"""

import os
import re
import subprocess
import sys

RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROYECTO_REAL = r"C:\Users\luarb\dev\unity\ElSenderoDeLasEstrellas"
# Compilador de Unity: el Roslyn del .NET SDK que usa el editor (no el de Mono, que
# en esta maquina no encuentra System.Text.Encoding.CodePages).
DOTNET = (r"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Data"
          r"\DotNetSdk\dotnet.exe")
CSC_DLL = (r"C:\Program Files\Unity\Hub\Editor\6000.6.2f1\Editor\Data\DotNetSdk"
           r"\sdk\8.0.318\Roslyn\bincore\csc.dll")


def main():
    csproj = os.path.join(PROYECTO_REAL, "Assembly-CSharp.csproj")
    if not os.path.exists(csproj):
        print("No hay .csproj del proyecto real; no se puede compilar.")
        return 1

    with open(csproj, encoding="utf-8-sig") as f:
        xml = f.read()

    defines = re.search(r"<DefineConstants>(.*?)</DefineConstants>", xml, re.S)
    defines = defines.group(1).strip() if defines else ""

    refs = re.findall(r"<HintPath>(.*?)</HintPath>", xml)
    refs = [r.replace("%(UnityEngineDirPath)", r"C:\Program Files\Unity\Hub\Editor\6000.6.2f1") for r in refs]
    refs = [r for r in refs if os.path.exists(r)]

    # Los ensamblados que Unity ya compila (firstpass, paquetes con asmdef...) se
    # referencian desde Library/ScriptAssemblies del proyecto real.
    ens = os.path.join(PROYECTO_REAL, "Library", "ScriptAssemblies")
    if os.path.isdir(ens):
        for f in sorted(os.listdir(ens)):
            if f.endswith(".dll") and not f.startswith("Assembly-CSharp."):
                refs.append(os.path.join(ens, f))

    fuentes = re.findall(r'<Compile Include="([^"]+)"', xml)
    presentes = []
    faltan = []
    for s in fuentes:
        p = os.path.join(RAIZ, s.replace("\\", os.sep))
        (presentes if os.path.exists(p) else faltan).append(s)

    print(f"Fuentes en el csproj: {len(fuentes)} · presentes: {len(presentes)} · "
          f"ausentes en el worktree: {len(faltan)}")
    for f in faltan[:10]:
        print("   falta:", f)

    rsp = os.path.join(RAIZ, "Temp", "compilar.rsp")
    os.makedirs(os.path.dirname(rsp), exist_ok=True)
    with open(rsp, "w", encoding="utf-8") as f:
        f.write("-target:library\n")
        f.write("-out:Temp\\Assembly-CSharp-check.dll\n")
        f.write("-nostdlib+\n-noconfig\n")
        f.write("-langversion:9.0\n")
        f.write("-nowarn:0169,0649,0414\n")
        f.write(f"-define:{defines}\n")
        for r in refs:
            f.write(f'-reference:"{r}"\n')
        for s in presentes:
            f.write(f'"{os.path.join(RAIZ, s.replace(chr(92), os.sep))}"\n')

    proc = subprocess.run([DOTNET, "exec", CSC_DLL, f"@{rsp}"], capture_output=True, text=True)
    salida = (proc.stdout or "") + (proc.stderr or "")
    errores = [l for l in salida.splitlines() if ": error " in l]
    avisos = [l for l in salida.splitlines() if ": warning " in l]
    print(f"Errores: {len(errores)} · Avisos: {len(avisos)}")
    for l in errores[:40]:
        print("  ", l)
    if not errores and not salida.strip():
        print("Compila limpio.")
    return 0 if not errores else 2


if __name__ == "__main__":
    sys.exit(main())
