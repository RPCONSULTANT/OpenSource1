# Evidencia visual del Entregable 3

Capturas generadas con Playwright CLI sobre AxionERP ejecutándose mediante Docker Compose. Todas corresponden a navegación real a 1280 o 1440 px de ancho. Las confirmaciones destructivas fueron abiertas, pero no aceptadas.

| Archivo | Rol | Escenario |
| --- | --- | --- |
| `01-login.png` | Anónimo | Formulario de inicio de sesión. |
| `02-admin-dashboard.png` | Administrador | Panel general y menú completo. |
| `03-usuarios-listado.png` | Administrador | Listado, filtros, estados y roles. |
| `04-usuario-detalle.png` | Administrador | Perfil y administración de cuenta. |
| `05-usuario-estado-confirm.png` | Administrador | Confirmación de cambio de estado, no ejecutada. |
| `06-usuario-eliminar-confirm.png` | Administrador | Confirmación de eliminación, no ejecutada. |
| `07-usuario-alta.png` | Administrador | Formulario de nueva cuenta, no enviado. |
| `08-clientes-busqueda.png` | Administrador | Búsqueda por nombre en vista tabular. |
| `09-cliente-alta.png` | Administrador | Formulario de alta, no enviado. |
| `10-cliente-detalle.png` | Administrador | Ficha de cliente persistido. |
| `11-cliente-edicion.png` | Administrador | Formulario de edición, sin guardar cambios. |
| `12-cliente-eliminar-confirm.png` | Administrador | Confirmación de eliminación, no ejecutada. |
| `13-productos-busqueda.png` | Administrador | Búsqueda por nombre en vista tabular. |
| `14-producto-alta.png` | Administrador | Formulario de alta, no enviado. |
| `15-producto-detalle.png` | Administrador | Ficha de producto persistido. |
| `16-producto-edicion.png` | Administrador | Formulario de edición, sin guardar cambios. |
| `17-producto-eliminar-confirm.png` | Administrador | Confirmación de eliminación, no ejecutada. |
| `18-reporteria.png` | Administrador | Módulo central de reportería. |
| `19-clientes-reporte-confirm.png` | Administrador | Preparación de PDF/Excel de clientes. |
| `20-productos-reporte-confirm.png` | Administrador | Preparación de PDF/Excel de productos. |
| `21-dashboard-clientes.png` | Administrador | Panel especializado de clientes. |
| `22-dashboard-productos.png` | Administrador | Panel especializado de productos. |
| `23-bitacora.png` | Administrador | Consulta de bitácora. |
| `24-modo-oscuro.png` | Administrador | Bitácora con tema oscuro. |
| `25-supervisor-dashboard.png` | Supervisor | Panel y menú restringido por rol. |
| `26-supervisor-clientes.png` | Supervisor | Clientes con consulta/modificación y sin alta/eliminación. |
| `27-supervisor-productos.png` | Supervisor | Productos con consulta/modificación y sin alta/eliminación. |
| `28-supervisor-acceso-denegado.png` | Supervisor | Ruta de usuarios rechazada. |
| `29-supervisor-bitacora.png` | Supervisor | Bitácora permitida. |
| `30-ejecutor-dashboard.png` | Ejecutor | Panel y menú restringido por rol. |
| `31-ejecutor-clientes.png` | Ejecutor | Clientes con consulta/alta y sin modificación/eliminación. |
| `32-ejecutor-cliente-alta.png` | Ejecutor | Alta de cliente permitida, formulario no enviado. |
| `33-ejecutor-productos.png` | Ejecutor | Productos con consulta/alta y sin modificación/eliminación. |
| `34-ejecutor-producto-alta.png` | Ejecutor | Alta de producto permitida, formulario no enviado. |
| `35-ejecutor-acceso-denegado.png` | Ejecutor | Ruta de Bitácora rechazada. |
| `36-supervisor-clientes-sin-agregar-eliminar.png` | Supervisor | Clientes en vista completa: consulta y edición visibles; alta y eliminación ausentes. |
| `37-supervisor-modificar-cliente-permitido.png` | Supervisor | Formulario de modificación permitido, sin guardar cambios. |
| `38-supervisor-productos-sin-agregar-eliminar.png` | Supervisor | Productos en vista completa: consulta y edición visibles; alta y eliminación ausentes. |
| `39-supervisor-alta-cliente-denegada.png` | Supervisor | Acceso GET directo a alta de Cliente rechazado. |
| `40-supervisor-usuarios-denegado.png` | Supervisor | Acceso GET directo a Usuarios rechazado. |
| `41-ejecutor-clientes-solo-consultar-agregar.png` | Ejecutor | Clientes en vista completa: consulta y alta visibles; edición y eliminación ausentes. |
| `42-ejecutor-alta-cliente-permitida.png` | Ejecutor | Formulario de alta de Cliente permitido, no enviado. |
| `43-ejecutor-productos-solo-consultar-agregar.png` | Ejecutor | Productos en vista completa: consulta y alta visibles; edición y eliminación ausentes. |
| `44-ejecutor-alta-producto-permitida.png` | Ejecutor | Formulario de alta de Producto permitido, no enviado. |
| `45-ejecutor-edicion-cliente-no-habilitada.png` | Ejecutor | GET directo con `edit=true` mantiene ficha sin controles de edición. |
| `46-ejecutor-usuarios-denegado.png` | Ejecutor | Acceso GET directo a Usuarios rechazado. |
| `47-ejecutor-bitacora-denegada.png` | Ejecutor | Acceso GET directo a Bitácora rechazado. |

No se almacenan contraseñas, tokens ni otros secretos en las imágenes o en este inventario.
