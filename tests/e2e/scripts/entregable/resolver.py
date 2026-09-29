#!/usr/bin/env python3
"""Genera docs/entregable-parte-1/documento-tecnico.md desde tpl.md (Fix-Features D4).

- Numera las figuras por orden de aparición: `{clave}` (nombre del PNG sin extensión) se sustituye por su número en los
  pies ("*Figura {clave}.") y en las referencias del texto y de las tablas.
- Si una captura referenciada no existe con ese nombre, usa la que tenga el mismo prefijo numérico (capturas/NN-*.png).
- Sustituye `<!-- DICCIONARIO -->` por las tablas de diagramas/diccionario-datos.md de las entidades del enunciado.

Uso (desde la raíz del repositorio): python3 tests/e2e/scripts/entregable/resolver.py
"""
import glob
import os
import re
import sys
from pathlib import Path

AQUI = Path(__file__).resolve().parent
D = AQUI.parents[3] / "docs" / "entregable-parte-1"
CLAVE = r"(\d\d-[\w-]+|arquitectura|modelo-er-[\w-]+)"
TABLAS_DICCIONARIO = [
    "AspNetUsers", "AspNetRoles", "AspNetUserRoles", "SociosNegocio", "CategoriasProducto", "Productos", "Almacenes",
    "MovimientosProducto", "MovimientosValor", "FacturasVentaBorrador", "LineasFacturaVentaBorrador", "FacturasVenta",
    "LineasFacturaVenta", "MovimientosCliente", "MovimientosClienteDetalle",
]


def diccionario():
    texto = (D / "diagramas" / "diccionario-datos.md").read_text(encoding="utf-8")
    bloques = {}
    for m in re.finditer(r"^### (\w+)\n\n(\|.*?)(?=\n\n|\Z)", texto, flags=re.S | re.M):
        bloques[m.group(1)] = m.group(2).strip()
    faltan = [t for t in TABLAS_DICCIONARIO if t not in bloques]
    if faltan:
        sys.exit(f"diccionario-datos.md no tiene: {faltan}")
    return "\n\n".join(f"#### Tabla `{t}`\n\n{bloques[t]}" for t in TABLAS_DICCIONARIO)


def main():
    t = (AQUI / "tpl.md").read_text(encoding="utf-8")
    t = t.replace("<!-- DICCIONARIO -->", diccionario())
    faltan = []
    for num, resto in set(re.findall(r"\]\(capturas/(\d\d)-([\w-]+)\.png\)", t)):
        clave = f"{num}-{resto}"
        if not (D / "capturas" / f"{clave}.png").exists():
            candidatas = sorted(glob.glob(str(D / "capturas" / f"{num}-*.png")))
            if candidatas:
                t = t.replace(clave, Path(candidatas[0]).stem)
            else:
                faltan.append(clave)
    orden = re.findall(r"\*Figura \{" + CLAVE + r"\}\.", t)
    if len(orden) != len(set(orden)):
        sys.exit("figura duplicada")
    numero = {k: i + 1 for i, k in enumerate(orden)}

    def sustituir(m):
        if m.group(1) not in numero:
            sys.exit(f"referencia sin figura: {m.group(1)}")
        return str(numero[m.group(1)])

    t = re.sub(r"\{" + CLAVE + r"\}", sustituir, t)
    (D / "documento-tecnico.md").write_text(t, encoding="utf-8")
    print(f"{len(orden)} figuras; capturas que faltan: {faltan}")


if __name__ == "__main__":
    main()
