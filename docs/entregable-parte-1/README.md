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
| `diagramas/modelo-er*.*` | Modelo entidad-relación generado del esquema real: completo, vistas parciales y seis vistas de claves por dominio (usuarios y roles, productos e inventario, clientes y CxC, borradores, facturas, notas de crédito). |
| `diagramas/diccionario-datos.md` | Diccionario de datos del núcleo: columnas, tipo con longitud/precisión, nulo, PK y FK (el documento incluye el de las entidades del enunciado). |
| `capturas/` | Capturas 1440×900 generadas por la suite E2E (numeradas; ver `evidencias/resumen-e2e.md`). |
| `evidencias/resumen-xunit.txt` | Salida de `dotnet test test.slnx` (suite xUnit completa). |
| `evidencias/resumen-build.txt` | Salida de `dotnet build test.slnx --no-incremental -warnaserror` (0 avisos, 0 errores). |
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

El `.md`, el `.docx` y el `.pptx` se generan con `cd tests/e2e && npm run entregable` a partir de
`tests/e2e/scripts/entregable/tpl.md` (ver `tests/e2e/scripts/entregable/README.md`); no se editan a mano.
