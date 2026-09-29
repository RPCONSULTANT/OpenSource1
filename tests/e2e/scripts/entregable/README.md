# Generadores del entregable (Parte 1)

Generan desde el repositorio los archivos de `docs/entregable-parte-1/` que no se editan a mano:

| Archivo | Qué hace |
| - | - |
| `tpl.md` | Fuente del documento técnico con las figuras por clave (`{11-cliente-alta}`) y el marcador `<!-- DICCIONARIO -->`. **Editar aquí**, no en `documento-tecnico.md`. |
| `resolver.py` | `tpl.md` → `documento-tecnico.md`: numera las figuras por orden, resuelve las capturas por prefijo e inserta el diccionario de `diagramas/diccionario-datos.md`. |
| `md2docx.cjs` | `documento-tecnico.md` → `documento-tecnico.docx` con docx-js: sin portada, cuerpo justificado, A4, Calibri 11, márgenes de 2,5 cm, figuras con pie y "Página X de Y". |
| `deck.cjs` | `presentacion.pptx` (9 diapositivas) con pptxgenjs. |

## Regenerar

Desde `tests/e2e` (las dependencias `docx` y `pptxgenjs` están en `package.json`):

```bash
npm install
npm run entregable          # resolver.py + md2docx.cjs + deck.cjs
```

Si cambia el esquema, antes (desde la raíz, con el stack de Docker levantado):
`python3 tests/e2e/scripts/esquema_a_mermaid.py --salida docs/entregable-parte-1/diagramas` y, en `tests/e2e`,
`npm run diagramas`.

Comprobar (desde la raíz del repositorio):

```bash
node tests/e2e/scripts/verificar-documento.mjs --completo
python3 tests/e2e/scripts/verificar-office.py
```

Para revisar el resultado a ojo: `soffice --headless --convert-to pdf documento-tecnico.docx` en una carpeta temporal
(el PDF no se versiona).
