# Series de numeración configurables, flujo del borrador y documento en una sola página — Diseño

Rama: `feat/no-series` (desde `d54e934`, final de `Fix-Features`). Fecha: 2026-09-29.

## Decisiones del usuario

| Tema | Decisión |
|---|---|
| Número de la factura | **Número nuevo al postear** (como hoy y como Business Central): el borrador tiene su número de una serie de borradores (con huecos) y la factura recibe el siguiente número de su serie de registro (sin huecos). |
| Cambios en el borrador | Elegir series en el borrador (serie de borrador y serie de registro, con próximo número visible); **copiar factura a borrador**; **conservar el borrador tras postear** (estado Posteado, enlazado a la factura). |
| Formato | **Con prefijo**: cada línea de serie define su número inicial con prefijo (`FV-000001`); el contador incrementa la parte numérica final. |
| Configuración | **Enfoque 1**: tabla de configuración por tipo de documento; cada serie declara su tipo. |
| Pantalla del documento | Cabecera y líneas **en una sola página** (estilo documento de Business Central), no en dos. |
| Ejecución | Multiagente en segundo plano (como Fix-Features). |

## Estado actual (resumen)

- `Serie` (Codigo, Descripcion, PermiteHuecos, PorDefecto sin uso) y `LineaSerie` (NumeroInicial, NumeroFinal, UltimoNumeroUsado como texto, FechaInicial, Incremento, Bloqueada). Solo dígitos: `(ultimo+Incremento).ToString().PadLeft(NumeroInicial.Length,'0')`.
- `IGeneradorNumeroDocumento.SiguienteAsync(codigoSerie, fecha)`: exige transacción, `FOR UPDATE` sobre la línea vigente (última `FechaInicial <= fecha`, no bloqueada), actualiza el contador dentro de la transacción del llamador (un fallo no consume número). `PermiteHuecos` se lee pero no cambia el comportamiento.
- Códigos fijos en el código: FV-BORR/FV, NC-BORR/NC, COBRO, CONTAB, SOCIOS. DIARIO-INV vía `PlantillaDiario.SerieId` (validación de prefijo `DIARIO-`).
- Sin API ni UI de series. Al postear, el borrador y sus líneas se eliminan (borrado lógico) y la factura guarda `NumeroBorrador`.
- La cabecera del borrador se edita en `/facturas-venta/borradores/{id}/editar` y las líneas en `/facturas-venta/borradores/{id}` (dos páginas).

## Restricciones globales

- Static SSR (sin `@rendermode`, `@onclick`, `@bind`), antiforgery, patrón de página de Fix-Features (`PageToolbar`, `EntityFormPage`, `RetornoLocal`, `PermisosPagina`, formularios de acción siempre en el árbol, `NavigateTo` después del try/catch).
- JWT solo en el servidor. Permisos de la UI iguales a la API.
- Onion + MediatR + `Result`/`Error` (`.no_encontrado`→404, `.conflicto`→409, resto 400). Escrituras EF, lecturas Dapper, misma `IDbSession`.
- Numeración sin huecos intacta: contador dentro de la transacción del documento, orden global de bloqueos sin cambios, "foto" de nada escrito en los fallos (incluido el contador).
- Migraciones EF con prueba de cadena desde vacío y desde el final de `Fix-Features` con datos, Down y vuelta. No tocar `appsettings*.json`.
- Suite completa verde y build con 0 avisos al cerrar cada fase.

## Parte 1 — Modelo y motor de numeración

### Serie (ampliada)
- `TipoDocumento` (enum `TipoDocumentoSerie`): `BorradorFacturaVenta`, `FacturaVenta`, `BorradorNotaCreditoVenta`, `NotaCreditoVenta`, `Cobro`, `AsientoContable`, `Cliente`, `DiarioInventario`. Obligatorio.
- `PermiteHuecos` (visible y editable; semántica documental — el motor sigue bloqueando la línea en ambos casos, D9).
- `Activa` (bool). Una serie inactiva no genera números ni se puede asignar.
- `PorDefecto` se retira (sin uso) en favor de la configuración.
- Una serie **usada** (alguna línea con `UltimoNumeroUsado`) o **asignada en la configuración** no se elimina: se desactiva. `TipoDocumento` no cambia si la serie está usada.

### LineaSerie (ampliada)
- `NumeroInicial`, `NumeroFinal` con prefijo: prefijo = texto antes de los dígitos finales; ambos con **mismo prefijo y mismo ancho numérico**; final > inicial.
- `NumeroAviso` (opcional): al alcanzarlo, la API devuelve una advertencia en el resultado del documento y la ficha de la serie la muestra.
- `FechaInicial`, `Incremento` (≥1), `Bloqueada`, `UltimoNumeroUsado` (solo lectura para el usuario salvo al crear la línea).
- Una línea con número usado no se elimina (se bloquea) y no cambia prefijo, ancho ni `NumeroInicial`; `NumeroFinal` solo puede crecer.
- Formato: `prefijo + (numero+incremento).ToString().PadLeft(ancho,'0')`. Series actuales solo dígitos = prefijo vacío: comportamiento idéntico.

### ConfiguracionNumeracion (nueva)
- Una fila por `TipoDocumentoSerie` con `SerieId` predeterminada (serie activa del mismo tipo). Sembrada con FV-BORR, FV, NC-BORR, NC, COBRO, CONTAB, SOCIOS y DIARIO-INV (esta última solo como predeterminada para plantillas nuevas; los diarios siguen tomando la serie de su plantilla/lote).
- Cambiar la configuración no afecta a documentos ya creados (el borrador guarda sus series).

### Motor
- `IGeneradorNumeroDocumento`:
  - `SiguienteAsync(Guid serieId, TipoDocumentoSerie tipoEsperado, DateOnly fecha, ct)` — valida tipo y `Activa`.
  - `SiguientePorTipoAsync(TipoDocumentoSerie tipo, DateOnly fecha, ct)` — usa la serie de la configuración.
  - `ProximoNumeroAsync(Guid serieId, DateOnly fecha, ct)` — vista previa **sin bloqueo** (solo lectura, orientativa).
- Errores nuevos: `numeracion.serie_inactiva`, `numeracion.tipo_incorrecto`, `numeracion.sin_configuracion`; se mantienen `sin_transaccion`, `serie_inexistente`, `sin_linea_vigente`, `serie_agotada`.
- Se eliminan los códigos fijos: cobros, asientos (CONTAB) y clientes (SOCIOS) piden por tipo; facturas y notas usan las series guardadas en el borrador; diarios, la serie de su plantilla/lote (validación por `TipoDocumento = DiarioInventario` en lugar del prefijo `DIARIO-`).
- Se conservan: bloqueo `FOR UPDATE` de la línea lo más tarde posible, orden global de bloqueos, contador en la transacción del documento.

## Parte 2 — Flujo del borrador

### Series del borrador (factura y nota de crédito)
- `SerieBorradorId` (fija desde la creación; elegible al crear entre series activas del tipo borrador, por defecto la configurada) y `SerieRegistroId` (por defecto la configurada; editable mientras el borrador esté Abierta).
- La página del borrador muestra la serie de registro y el **próximo número** (vista previa, sin reserva).
- Al postear se usa `SerieRegistroId` y se revalida tipo y actividad; el resto del posteo no cambia.

### Borrador conservado al postear
- `EstadoFacturaBorrador` gana `Posteada` (3); el posteo marca el borrador como Posteada, guarda `FacturaVentaNumero` y **no** lo elimina (ni sus líneas). Igual para la nota (`NotaCreditoVentaNumero`).
- Un borrador Posteada es de solo lectura: toda escritura (cabecera, líneas, estado, eliminar, postear) devuelve `…conflicto` con mensaje claro.
- Enlaces en ambos sentidos: borrador → factura/nota; factura/nota → borrador (`NumeroBorrador` ya existe).
- Listados de borradores: por defecto Abierta+Liberada; filtro de estado con Posteada.
- Los borradores ya eliminados por posteos anteriores no se recuperan.

### Copiar factura a borrador
- Acción en la factura posteada: Crear ▾ "Copiar a borrador" (CanAdd). Crea un borrador nuevo (número de la serie de borrador configurada, fecha de hoy, serie de registro configurada).
- Cabecera: cliente, facturar a, almacén y descripción de la factura; término de pago y grupos **actuales del cliente**.
- Líneas: producto, unidad, cantidad, precio y descuento. Productos borrados o bloqueados se omiten con aviso; cliente bloqueado o borrado impide la copia con mensaje.
- Atómico: si falla, no se crea nada ni se consume número.
- Solo facturas (las notas ya nacen de una factura).

## Parte 3 — Pantallas, API y permisos

- **Series de numeración** (`/series`, grupo Configuración): lista con código, descripción, tipo, activa, último usado y próximo; `+ Nuevo` → `/series/nueva`; ficha `/series/{id}` con cabecera y tabla de líneas (agregar/modificar/bloquear/eliminar con formularios SSR en la misma página).
- **Configuración de numeración** (`/configuracion/numeracion`): una fila por tipo con selector de series activas de ese tipo.
- Registro de módulos: ambas en el grupo Configuración (visibles con CanAdministrar).
- API: `api/series` (+ `/{id}/lineas`), `api/configuracion/numeracion`, `GET api/series?tipo=` para selectores y `GET api/series/{id}/proximo?fecha=`. Consultar: CanConsult. Crear/modificar/eliminar series, líneas y configuración: **CanAdministrar**. Concurrencia `xmin` en series, líneas y configuración.
- Borradores: selector de serie al crear; serie de registro y próximo número en la página del documento; filtro de estado. Factura posteada: Crear ▾ Copiar a borrador y enlace al borrador.

## Parte 4 — Documento en una sola página

- **Borrador de factura** (`/facturas-venta/borradores/{id}`): una única página con
  - cabecera editable en el sitio (cliente/facturar a, fechas, término, almacén, serie de registro + próximo número, descripción) con "Guardar cabecera";
  - tabla de líneas debajo con una fila vacía al final para agregar y edición en la fila (buscador de producto en la fila);
  - totales al pie (subtotal, descuentos, ITBIS, total);
  - barra del documento: Liberar/Reabrir, Postear (ConfirmDialog), Ver ▾ (cliente, movimientos), Eliminar.
- Cada acción es un POST que vuelve a la misma página con aviso (Static SSR).
- `/…/borradores/{id}/editar` se retira: redirige a la página única (conserva `returnUrl`).
- Mismo diseño para el **borrador de nota de crédito**; **factura y nota posteadas** y **borradores Posteada** con el mismo diseño en solo lectura.
- `/facturas-venta/nueva` sigue creando la cabecera y lleva a la página única.

## Pruebas

- Generador: prefijo y ancho, series solo dígitos sin cambios, concurrencia sin duplicados ni huecos, inactiva, tipo incorrecto, sin configuración, agotada, número de aviso.
- Series/líneas/configuración: validaciones, protección de series usadas/asignadas, xmin, permisos por rol (Admin sí; Supervisor/Ejecutor 403).
- Posteo con serie de registro elegida; foto de nada escrito (contador incluido) en fallos; borrador Posteada de solo lectura; enlaces.
- Copia: cabecera con datos actuales del cliente, productos omitidos, cliente bloqueado, atomicidad.
- Páginas Blazor (render, POST, POST forzado sin permiso, mocks asíncronos) y E2E del documento en una sola página.
- Migraciones: cadena desde vacío y desde `Fix-Features` con datos (series existentes → tipo inferido por código; borradores abiertos → series FV-BORR/FV y NC-BORR/NC), Down y vuelta.

## Fuera de alcance

Comprobantes fiscales (NCF), series de compras, renumeración de documentos emitidos, reserva de número en el borrador.

## Resultado

Implementado en la rama `feat/no-series` (`d54e934..HEAD`, commits locales) según el plan
[`2026-09-29-no-series.md`](../plans/2026-09-29-no-series.md), cuya sección "Resultado de feat/no-series" recoge los
commits por task, la cadena de migraciones, las decisiones de ejecución (NS1–NS22, F1–F20, NS-S1, NS-S11/b/c,
NSC1–NSC6) y los residuales aceptados.

- **Parte 1 — Series:** tipo de documento, `Activa` y número de aviso en series y líneas; formato con prefijo y
  ancho; motor por tipo y por serie sin códigos fijos, con la numeración sin huecos y el orden de bloqueos intactos;
  configuración por tipo con ocho filas semilla. La serie asignada a un tipo no se elimina ni se desactiva (NS5,
  aceptado en F13); la usada no se elimina pero sí se desactiva.
- **Parte 2 — API y páginas:** `/series` (lista, ficha con líneas, alta) y `/configuracion/numeracion`, escrituras con
  `CanAdministrar`, `xmin`, sin solapes entre series del mismo tipo.
- **Parte 3 — Flujo del borrador:** series de borrador y de registro elegibles con próximo número; el posteo usa la
  serie de registro, devuelve el aviso de numeración y deja el borrador Posteada en solo lectura enlazado a su
  documento (doble posteo → 409); copia de factura posteada a borrador nuevo.
- **Parte 4 — Documento en una sola página:** borradores de factura y de nota con cabecera editable, líneas en la fila
  y totales; documentos posteados con el mismo diseño en solo lectura.
- **Verificación de cierre:** suite xUnit 1969/1969, build con 0 avisos, sin cambios de modelo pendientes, E2E
  15 passed + `@api-caida` skipped; cadena de migraciones desde vacío y desde el volcado de Fix-Features con datos, Down y vuelta, sin
  errores; sin interactividad añadida ni cambios en `appsettings*.json`.
