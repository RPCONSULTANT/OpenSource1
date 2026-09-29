# Resumen de la suite E2E (Playwright)

Fecha de ejecución: 2026-09-29. Stack de Docker Compose del proyecto (Blazor en `http://localhost:8080`, API en `:8081`,
PostgreSQL publicado en el puerto `5433` de `.env`). Navegador Chromium, viewport 1440×900, un solo worker.

Ejecución (desde `tests/e2e`):

```bash
bash scripts/con-credenciales.sh npx playwright test
# Caso de API caída, aparte y con la API detenida:
docker compose stop api
env E2E_API_CAIDA=1 E2E_REPORTE=reporte-e2e-api-caida bash scripts/con-credenciales.sh npx playwright test specs/07-errores.spec.ts --grep "@api-caida"
docker compose start api
```

## Resultados

| Spec | Prueba | Resultado |
|---|---|---|
| 01-acceso-menu | Login, inicio con grupos y menú por grupos del administrador | PASS |
| 01-acceso-menu | El ejecutor no ve Administración y el modo oscuro se conserva | PASS |
| 02-clientes-crud | Clientes: agregar, consultar, modificar, eliminar y validación | PASS |
| 03-productos-busquedas | Productos: alta y búsquedas por código, nombre, categoría, precio y estado | PASS |
| 04-categorias-usuarios | Categorías: agregar, modificar y eliminar | PASS |
| 04-categorias-usuarios | Usuarios: alta en página propia y ficha para rol/estado | PASS |
| 05-busqueda-global | Paleta Ctrl+K: módulos al escribir, flechas, Enter y Esc | PASS |
| 05-busqueda-global | Paleta tras navegación mejorada: ArrowDown ×2 selecciona la opción 1 (no salta) | PASS |
| 05-busqueda-global | Búsqueda en servidor `/buscar` con módulos y registros | PASS |
| 06-acciones-documentos | Desde un cliente: Crear ▾ Factura abre la nueva factura con el cliente precargado | PASS |
| 06-acciones-documentos | Facturas filtradas por cliente y menú Ver ▾ de producto | PASS |
| 07-errores | 403: el ejecutor no puede abrir Usuarios | PASS |
| 07-errores | 404: grupo inexistente | PASS |
| 07-errores | @api-caida: aviso en el login con la API detenida | PASS (ejecución aparte) |
| 08-sin-flash | Navegación entre páginas sin destello azul | PASS |

Ejecución principal: 14 pasadas, 1 omitida (`@api-caida`, que solo corre con `E2E_API_CAIDA=1`).
Ejecución con la API detenida: 1 pasada. Reportes HTML: `reporte-e2e/index.html` y `reporte-e2e-api-caida/index.html`.

## Capturas (docs/entregable-parte-1/capturas)

| Archivo | Qué muestra |
|---|---|
| 01-login.png | Pantalla de inicio de sesión |
| 02-inicio.png | Inicio del administrador: indicadores y tarjetas de los grupos de módulos |
| 03-menu-grupos.png | Menú lateral por grupos con Configuración abierto en Unidades de medida |
| 04-grupo-facturacion.png | Página del grupo Facturación (`/modulos/facturacion`) con indicadores y módulos |
| 05-inicio-ejecutor-oscuro.png | Inicio del ejecutor en modo oscuro, sin el grupo Administración |
| 10-cliente-validacion.png | Alta de cliente con el mensaje de validación del nombre comercial |
| 11-cliente-alta.png | Formulario de nuevo cliente con los datos del registro E2E |
| 12-cliente-seleccion-acciones.png | Lista de clientes con una fila seleccionada (`?sel=`) y las acciones habilitadas |
| 13-cliente-menu-crear.png | Menú Crear ▾ del cliente: Factura, Nota de crédito y Cobro |
| 14-cliente-modificado.png | Aviso "Cliente modificado" y la fila con el teléfono actualizado |
| 15-cliente-eliminar-confirmacion.png | Diálogo de confirmación de eliminación |
| 16-producto-busqueda-codigo.png | Productos filtrados por código |
| 17-producto-busqueda-nombre.png | Productos filtrados por nombre |
| 18-producto-busqueda-categoria.png | Productos filtrados por categoría (GENERAL) |
| 19-producto-busqueda-precio.png | Productos filtrados por precio de venta (125.50) |
| 20-producto-busqueda-estado.png | Productos filtrados por estado de existencia (sin existencia) |
| 21-categoria-alta.png | Formulario de nueva categoría de producto |
| 22-usuario-alta.png | Alta de usuario en su página propia |
| 23-usuarios-listado.png | Gestión de usuarios con el aviso de usuario creado |
| 24-paleta-modulos.png | Paleta Ctrl+K con el módulo Categorías de producto |
| 25-buscar-resultados.png | `/buscar?q=cliente`: módulos, clientes y facturas |
| 26-nueva-factura-precargada.png | Nueva factura abierta desde un cliente, con el cliente precargado |
| 27-producto-menu-ver.png | Menú Ver ▾ de un producto seleccionado (Ficha, Existencias, Movimientos) |
| 28-facturas.png | Facturas filtradas por cliente (chip "Cliente: …") |
| 29-error-403.png | Acceso denegado del ejecutor a Usuarios |
| 30-error-404.png | Página no encontrada (grupo inexistente, respuesta 404) |
| 31-error-api-caida.png | Login con la API detenida: "El servicio de autenticación no está disponible" |
| 32-navegacion-sin-flash.png | Términos de pago tras una navegación mejorada, sin barra ni destello |

## Datos de prueba

Los registros que crea la suite llevan el prefijo `E2E` (clientes "E2E Cliente-…", productos y categorías "E2E-…",
usuarios `e2e-…@e2e.local`) y la propia suite los elimina desde la interfaz. Clientes, productos y categorías usan
borrado lógico (`IsDeleted`), así que las filas quedan en la base de datos marcadas como eliminadas; los usuarios se
eliminan de verdad. La suite no crea ni postea documentos (la nueva factura precargada solo se abre, no se guarda).
