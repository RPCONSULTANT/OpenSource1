# Entregable, Parte 1 — índice de artefactos

Carpeta del entregable académico de AxionERP (rama `Fix-Features`). El formato de entrega del documento es el `.docx`;
el `.md` es su fuente.

| Archivo / carpeta | Contenido |
| - | - |
| `documento-tecnico.docx` | Documento técnico final (sin portada, texto justificado, figuras numeradas). |
| `documento-tecnico.md` | Fuente del documento técnico (secciones 1–10). |
| `presentacion.pptx` | Presentación breve del proyecto (9 diapositivas). |
| `guion-demo.md` | Guion de la demostración en vivo (unos 9 minutos) con usuarios, URL y requisitos. |
| `diagramas/arquitectura.*` | Diagrama de arquitectura (Mermaid `.mmd`, `.svg`, `.png`). |
| `diagramas/modelo-er*.*` | Modelo entidad-relación generado del esquema real: completo y vistas parciales (maestros e inventario; ventas y CxC). |
| `diagramas/diccionario-datos.md` | Diccionario de datos: tablas, columnas, tipos, claves primarias y foráneas. |
| `capturas/` | Capturas 1440×900 generadas por la suite E2E (numeradas; ver `evidencias/resumen-e2e.md`). |
| `evidencias/resumen-xunit.txt` | Salida de `dotnet test test.slnx` (suite xUnit completa). |
| `evidencias/resumen-e2e.md` | Resultados de la suite E2E y descripción de cada captura. |
| `evidencias/reporte-e2e/`, `evidencias/reporte-e2e-api-caida/` | Reportes HTML de Playwright (ejecución principal y con la API detenida). |

## Cómo regenerar

Desde la raíz del repositorio, con el sistema levantado (`docker compose up -d --build`):

```bash
# Modelo ER desde el esquema real (information_schema -> Mermaid); ver las opciones del script
python3 tests/e2e/scripts/esquema_a_mermaid.py --help

# Diagramas Mermaid -> SVG/PNG
cd tests/e2e && npm run diagramas

# Suite E2E y capturas (credenciales solo por variables de entorno, leídas de .env)
cd tests/e2e && bash scripts/con-credenciales.sh npx playwright test

# Evidencia xUnit
env DOCKER_CONTEXT=default dotnet test test.slnx 2>&1 | tee docs/entregable-parte-1/evidencias/resumen-xunit.txt

# Comprobaciones del entregable
node tests/e2e/scripts/verificar-documento.mjs --completo
python3 tests/e2e/scripts/verificar-office.py
```

El `.docx` y el `.pptx` se generan a partir de `documento-tecnico.md` y del contenido del documento con las skills de
Office (`docx` y `pptx`) de Claude Code; tras cambiar el `.md` hay que volver a generarlos.
