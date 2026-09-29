# AxionERP — Guion de la demostración (Entregable, Parte 1)

Duración total estimada: **9 minutos**. Interfaz en `http://localhost:8080`; API en `http://localhost:8081`.

## Requisitos previos (antes de la sesión)

1. Archivo `.env` en la raíz del repositorio (copiado de `.env.example`) con `POSTGRES_PASSWORD`, `JWT_SIGNING_KEY`,
   `AUTH_SEED_DEFAULT_PASSWORD` y, si el 5432 está ocupado, `POSTGRES_PORT`.
2. Levantar el sistema: `docker compose up -d --build` y comprobar con `docker compose ps` que `postgres`, `api` y
   `blazor` están en ejecución.
3. Usuarios semilla: `admin` (Administrador) y `ejecutor` (Ejecutor), ambos con la contraseña configurada en
   `AUTH_SEED_DEFAULT_PASSWORD`. No mostrar el archivo `.env` en pantalla.
4. Tener abierta una terminal en la raíz del repositorio para los pasos 13 y 14.

## Pasos

| # | Tiempo | Usuario | URL / acción | Qué mostrar y qué decir |
| - | - | - | - | - |
| 1 | 0:00–0:30 | — | `/account/login` | Iniciar sesión como `admin` con la contraseña configurada en `AUTH_SEED_DEFAULT_PASSWORD`. Mencionar que el JWT se queda en el servidor (cookie HttpOnly). |
| 2 | 0:30–1:15 | admin | `/` | Inicio con indicadores y tarjetas de los grupos de módulos; todos salen del registro `CatalogoModulos`. |
| 3 | 1:15–2:00 | admin | Menú lateral → Configuración → Unidades de medida; luego `/modulos/facturacion` | Menú por grupos con el grupo actual abierto; página de grupo con indicadores. |
| 4 | 2:00–2:40 | admin | Pulsar Ctrl+K y escribir `categoria` | La paleta encuentra «Categorías de producto» sin acentos; navegar con flechas y Enter. Cerrar con Esc. |
| 5 | 2:40–3:40 | admin | `/clientes` → `+ Nuevo` (`/clientes/nuevo`) | Guardar vacío para mostrar la validación; `Limpiar campos`; completar y guardar el cliente. |
| 6 | 3:40–4:20 | admin | `/clientes` → seleccionar la fila del cliente nuevo | Barra `PageToolbar`: Editar y Eliminar habilitados con `?sel=`. |
| 7 | 4:20–5:00 | admin | `Crear ▾` → Factura | La nueva factura (`/facturas-venta/nueva`) abre con el cliente precargado. Volver con Cancelar (no guardar). |
| 8 | 5:00–6:00 | admin | `/productos` → Filtros | Buscar por código (`*` como comodín), por categoría, por precio de venta y por estado «Sin existencia». |
| 9 | 6:00–6:30 | admin | Seleccionar un producto → `Ver ▾` → Existencias | Información relacionada desde la barra de acciones. |
| 10 | 6:30–7:10 | admin | Administración → Usuarios → `/admin/users/nuevo` | Alta de un usuario de prueba con rol Ejecutor; mostrar su ficha (rol y estado de la cuenta). |
| 11 | 7:10–7:30 | admin | Salir | Cerrar sesión. |
| 12 | 7:30–8:15 | ejecutor | Login → `/`; luego abrir `/admin/users` | El menú no muestra Administración; forzar la URL lleva a «Acceso denegado» (403). Abrir `/modulos/noexiste` para mostrar el 404. |
| 13 | 8:15–8:45 | — | Terminal: `docker compose stop api`; abrir `/account/login` en una ventana privada e intentar iniciar sesión | Aviso «El servicio de autenticación no está disponible en este momento» en lugar de un error técnico. |
| 14 | 8:45–9:00 | — | Terminal: `docker compose start api` | La API vuelve y el login funciona de nuevo. Cierre. |

## Limpieza tras la demostración

- Eliminar el cliente y el usuario de prueba creados en los pasos 5 y 10 (o dejarlos, si la base es solo de demostración).
- Si se quiere empezar desde cero: `docker compose down -v` borra también los datos persistentes.

## Plan B

Si algún paso falla en vivo, mostrar la captura equivalente de `capturas/` (las figuras del documento técnico) y el
reporte E2E de `evidencias/reporte-e2e/index.html`, que recorre los mismos flujos de forma automatizada.
