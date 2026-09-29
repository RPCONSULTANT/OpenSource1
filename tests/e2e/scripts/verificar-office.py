#!/usr/bin/env python3
"""Verifica el .docx (justificado, sin portada, con imágenes) y el .pptx (8–10 diapositivas) del entregable (D4)."""
import re
import sys
import zipfile
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[3] / "docs" / "entregable-parte-1"
fallos = []

docx = RAIZ / "documento-tecnico.docx"
with zipfile.ZipFile(docx) as z:
    xml = z.read("word/document.xml").decode("utf-8")
    medios = [n for n in z.namelist() if n.startswith("word/media/")]
parrafos = re.findall(r"<w:p[ >].*?</w:p>", xml, flags=re.S)
con_texto = [p for p in parrafos if "<w:t" in p]
justificados = [p for p in con_texto if 'w:jc w:val="both"' in p]
if len(justificados) < len(con_texto) * 0.5:
    fallos.append(f"Solo {len(justificados)} de {len(con_texto)} párrafos con texto están justificados.")
primer_texto = re.sub(r"<[^>]+>", "", con_texto[0]) if con_texto else ""
if "portada" in primer_texto.lower() or not primer_texto.strip().startswith("AxionERP"):
    fallos.append(f"El documento no empieza por el título (posible portada): {primer_texto[:60]!r}")
if 'w:type="page"' in "".join(parrafos[:3]):
    fallos.append("Hay un salto de página al inicio (portada).")
if len(medios) < 10:
    fallos.append(f"Solo {len(medios)} imágenes embebidas (diagramas + capturas).")

pptx = RAIZ / "presentacion.pptx"
with zipfile.ZipFile(pptx) as z:
    diapositivas = [n for n in z.namelist() if re.fullmatch(r"ppt/slides/slide\d+\.xml", n)]
if not 8 <= len(diapositivas) <= 10:
    fallos.append(f"La presentación tiene {len(diapositivas)} diapositivas (se esperan 8–10).")

if fallos:
    print("\n".join(f"✗ {f}" for f in fallos))
    sys.exit(1)
print(f"✓ docx ({len(con_texto)} párrafos, {len(medios)} imágenes) y pptx ({len(diapositivas)} diapositivas)")
