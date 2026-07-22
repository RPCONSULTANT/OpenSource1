# Entregable 3: Integración total del sistema

**Asignatura:** Desarrollo de Software con Tecnologías Propietarias y Open Source I (ISO-615)  
**Proyecto:** AxionERP, sistema de gestión empresarial con control de acceso basado en roles  
**Repositorio:** OpenSource1  
**Etapa:** III de III, integración total del sistema

> Por indicación académica, este documento no incluye portada ni video. La evidencia visual fue obtenida de una ejecución real del sistema y se conserva en `docs/screenshots/entregable-3/`.

---

## 1. Introducción

AxionERP es una aplicación cliente-servidor orientada a la administración de usuarios, clientes y productos. La solución integra autenticación, autorización basada en roles, operaciones de mantenimiento, consultas dinámicas, reportería, paneles de indicadores y bitácora. Su propósito es centralizar información operativa y restringir cada función de acuerdo con la responsabilidad del usuario autenticado.

La tercera etapa consolida los componentes desarrollados previamente en un flujo único: interfaz Blazor, API REST, servicios de aplicación, persistencia y base de datos PostgreSQL. La evaluación presentada en este informe no se limita a una descripción estática del código. Se complementa con una verificación automatizada sobre el sistema desplegado en contenedores, utilizando tres perfiles representativos: Administrador, Supervisor y Ejecutor.

## 2. Objetivos

### 2.1 Objetivo general

Integrar y verificar una aplicación empresarial funcional que permita autenticar usuarios, administrar información persistente y controlar módulos y operaciones según roles definidos.

### 2.2 Objetivos específicos

- Integrar frontend, API y base de datos en un entorno reproducible.
- comprobar autenticación y establecimiento de sesión segura.
- Verificar visibilidad de menús y autorización de rutas según rol.
- Validar los flujos de consulta, búsqueda, alta, detalle, modificación y confirmación de eliminación.
- Comprobar reportería, exportación, paneles y bitácora.
- Documentar resultados mediante evidencia visual obtenida de la aplicación en ejecución.

## 3. Alcance y metodología de verificación

### 3.1 Alcance

La revisión abarcó la interfaz web y su comunicación con la API y PostgreSQL. Se recorrieron los módulos de usuarios, clientes, productos, reportería, paneles y bitácora. También se observaron el modo oscuro, las variaciones del menú por rol y las respuestas de acceso denegado.

No se ejecutaron eliminaciones ni cambios de estado sobre registros semilla. Las operaciones destructivas se verificaron hasta su diálogo de confirmación. Tampoco se crearon datos permanentes durante la captura; los formularios de alta y edición se inspeccionaron sin enviarlos. Este criterio preservó la base de prueba y evitó presentar como resultado una modificación que no era necesaria para la evidencia solicitada.

### 3.2 Entorno

| Componente | Configuración verificada |
| --- | --- |
| Framework | .NET 10 (`net10.0`) |
| Frontend | Blazor Web App con renderizado Static SSR |
| Backend | ASP.NET Core Web API |
| Persistencia | PostgreSQL 17 |
| Acceso a datos | Entity Framework Core y Dapper |
| Orquestación | Docker Compose |
| Automatización | Playwright CLI sobre Chromium |
| Resolución de captura | 1280 o 1440 px de ancho; altura extendida en páginas largas |

### 3.3 Procedimiento

1. Se construyeron y levantaron los servicios `postgres`, `api` y `blazor` mediante Docker Compose.
2. Se confirmó la disponibilidad de la pantalla de autenticación y se realizaron inicios de sesión reales con las cuentas semilla de cada rol.
3. Se navegó mediante enlaces y formularios SSR, sin modificar el modo de renderizado de la aplicación.
4. Para el Administrador se recorrieron listados, fichas, altas, ediciones, búsquedas, reportería, paneles, bitácora y confirmaciones.
5. Para Supervisor y Ejecutor se compararon menús, acciones visibles y rutas explícitamente denegadas.
6. Se generaron capturas PNG de página completa y se inspeccionó una muestra para confirmar contenido, rol y escenario.
7. Se ejecutaron compilación y pruebas automatizadas como controles técnicos complementarios.

## 4. Tecnologías utilizadas

| Área | Tecnología |
| --- | --- |
| Lenguaje | C# |
| Plataforma | .NET 10 |
| Backend | ASP.NET Core Web API |
| Frontend | Blazor Web App Static SSR |
| Base de datos | PostgreSQL |
| Persistencia | Entity Framework Core y Dapper |
| Identidad y seguridad | ASP.NET Core Identity, JWT, cookie HttpOnly y políticas de autorización |
| Arquitectura | Arquitectura por capas con separación de dominio, aplicación, infraestructura, API e interfaz |
| Presentación | Tailwind CSS |
| Despliegue local | Docker y Docker Compose |
| Verificación E2E | Playwright CLI |

## 5. Arquitectura general de la solución

La solución distribuye responsabilidades entre proyectos especializados:

| Proyecto | Responsabilidad principal |
| --- | --- |
| `OpenSource1.Core` | Entidades, objetos de valor y abstracciones del dominio. |
| `OpenSource1.Application` | Casos de uso, contratos, DTO, consultas y políticas. |
| `OpenSource1.Infrastructure` | Persistencia, Identity, repositorios y servicios de infraestructura. |
| `OpenSource1.Api` | Exposición de recursos HTTP, autenticación y autorización efectiva. |
| `OpenSource1.Blazor` | Presentación SSR, formularios, navegación y consumo tipado de la API. |

La autenticación se inicia en la API, que emite un JWT con roles y permisos. La aplicación Blazor mantiene dicho token en sesión del servidor y representa la sesión del navegador mediante una cookie segura HttpOnly. Así, el token no se expone a JavaScript del navegador. La visibilidad condicional de la interfaz mejora la experiencia, mientras que la API conserva la responsabilidad de aplicar la autorización real.

## 6. Descripción funcional del sistema

El sistema integra los siguientes componentes:

1. Inicio de sesión y administración de sesión.
2. Gestión administrativa de usuarios, roles y estado.
3. Gestión de clientes.
4. Gestión de productos.
5. Búsquedas simples y filtros por campos.
6. Reportes PDF y exportaciones Excel.
7. Panel general y paneles especializados.
8. Bitácora operativa.
9. Control de acceso por rol tanto en interfaz como en API.

## 7. Seguridad por roles

### 7.1 Matriz funcional

| Módulo o acción | Administrador | Supervisor | Ejecutor |
| --- | --- | --- | --- |
| Gestión de usuarios | Sí | No | No |
| Consultar clientes y productos | Sí | Sí | Sí |
| Agregar clientes y productos | Sí | No | Sí |
| Modificar clientes y productos | Sí | Sí | No |
| Eliminar clientes y productos | Sí | No | No |
| Reportería | Sí | Sí | Sí |
| Bitácora | Sí | Sí | No |

Las políticas `CanConsult`, `CanAdd`, `CanModify` y `CanDelete` formalizan esta matriz. Las páginas emplean autorización declarativa y la API vuelve a validar cada operación; por tanto, ocultar un botón no constituye el único control de seguridad.

### 7.2 Evidencia comparativa

![Panel y menú del Administrador](screenshots/entregable-3/02-admin-dashboard.png)

*Figura 1. Sesión Administrador: menú completo, indicadores generales y acceso a Gestión de Usuarios.*

![Panel y menú del Supervisor](screenshots/entregable-3/25-supervisor-dashboard.png)

*Figura 2. Sesión Supervisor: acceso a clientes, productos, reportería y bitácora; Gestión de Usuarios no está visible.*

![Panel y menú del Ejecutor](screenshots/entregable-3/30-ejecutor-dashboard.png)

*Figura 3. Sesión Ejecutor: acceso a clientes, productos y reportería; no se presenta Gestión de Usuarios ni Bitácora.*

![Acceso denegado al Supervisor](screenshots/entregable-3/28-supervisor-acceso-denegado.png)

*Figura 4. Intento directo del Supervisor sobre la ruta administrativa de usuarios, rechazado por autorización.*

![Acceso denegado al Ejecutor](screenshots/entregable-3/35-ejecutor-acceso-denegado.png)

*Figura 5. Intento directo del Ejecutor sobre Bitácora, rechazado por autorización.*

### 7.3 Evidencia de pruebas de autorización por rol

Se ejecutó una segunda ronda de pruebas sobre el despliegue real para comprobar cada permiso de Supervisor y Ejecutor de forma explícita. La verificación combinó tres niveles: controles visibles o ausentes en la interfaz, navegación directa mediante rutas GET seguras y solicitudes autenticadas a la API. Los JWT se mantuvieron únicamente en memoria durante la automatización y no se almacenaron en capturas ni documentos.

Las pruebas de API permitidas que podrían alterar datos se enviaron deliberadamente con un cuerpo vacío y un identificador inexistente. Por ello, un resultado `400 Bad Request` significa que la solicitud superó la autorización y fue detenida por validación antes de ejecutar el caso de uso; no se creó ni modificó ningún registro.

#### Supervisor

| Capacidad esperada | Evidencia UI o HTTP | Resultado observado | Estado |
| --- | --- | --- | --- |
| Consultar Clientes | Listado real con dos registros y rol visible | Página cargada; `GET /api/clientes` devolvió `200` | Conforme |
| Consultar Productos | Listado real con dos registros y rol visible | Página cargada; `GET /api/productos` devolvió `200` | Conforme |
| Modificar | Iconos de edición y formulario de cliente visibles | Ruta de edición cargada; `PUT` de prueba llegó a validación y devolvió `400` | Conforme |
| No agregar | Botón **Nuevo** ausente y acceso directo a `/clientes/new` | Redirección a **Acceso denegado**; `POST` de Clientes y Productos devolvió `403` | Conforme |
| No eliminar | Acciones de papelera ausentes | `DELETE` de Clientes y Productos devolvió `403` | Conforme |
| No acceder a Usuarios | Opción ausente del menú y acceso directo a `/admin/users` | Redirección a **Acceso denegado**; `GET /api/users` devolvió `403` | Conforme |

![Supervisor consulta clientes sin alta ni eliminación](screenshots/entregable-3/36-supervisor-clientes-sin-agregar-eliminar.png)

*Figura 5a. Supervisor en Clientes: consulta y edición visibles; no aparecen Nuevo ni Eliminar.*

![Supervisor modifica cliente](screenshots/entregable-3/37-supervisor-modificar-cliente-permitido.png)

*Figura 5b. Formulario de modificación disponible para Supervisor, abierto sin guardar cambios.*

![Supervisor consulta productos sin alta ni eliminación](screenshots/entregable-3/38-supervisor-productos-sin-agregar-eliminar.png)

*Figura 5c. Supervisor en Productos: consulta y modificación disponibles, sin alta ni eliminación.*

![Alta de cliente denegada al Supervisor](screenshots/entregable-3/39-supervisor-alta-cliente-denegada.png)

*Figura 5d. Acceso directo del Supervisor a la ruta segura GET `/clientes/new`, rechazado por autorización.*

![Usuarios denegado al Supervisor](screenshots/entregable-3/40-supervisor-usuarios-denegado.png)

*Figura 5e. Acceso directo del Supervisor a `/admin/users`, rechazado por autorización.*

#### Ejecutor

| Capacidad esperada | Evidencia UI o HTTP | Resultado observado | Estado |
| --- | --- | --- | --- |
| Consultar Clientes | Listado real con dos registros y rol visible | Página cargada; `GET /api/clientes` devolvió `200` | Conforme |
| Consultar Productos | Listado real con dos registros y rol visible | Página cargada; `GET /api/productos` devolvió `200` | Conforme |
| Agregar | Botón **Nuevo** y formularios de alta visibles | Rutas de alta cargaron; `POST` llegó a validación y devolvió `400` | Conforme |
| No modificar | Iconos de edición ausentes; GET con `?edit=true` conserva ficha de solo lectura | `PUT` de Clientes y Productos devolvió `403` | Conforme |
| No eliminar | Acciones de papelera ausentes | `DELETE` de Clientes y Productos devolvió `403` | Conforme |
| No acceder a Usuarios | Opción ausente del menú y acceso directo a `/admin/users` | Redirección a **Acceso denegado**; `GET /api/users` devolvió `403` | Conforme |
| No acceder a Bitácora | Opción ausente del menú y acceso directo a `/bitacora` | Redirección a **Acceso denegado** | Conforme |

![Ejecutor consulta clientes y puede agregar](screenshots/entregable-3/41-ejecutor-clientes-solo-consultar-agregar.png)

*Figura 5f. Ejecutor en Clientes: consulta y alta disponibles; edición y eliminación ausentes.*

![Alta de cliente permitida al Ejecutor](screenshots/entregable-3/42-ejecutor-alta-cliente-permitida.png)

*Figura 5g. Formulario de alta de Cliente disponible para Ejecutor, abierto sin enviarlo.*

![Ejecutor consulta productos y puede agregar](screenshots/entregable-3/43-ejecutor-productos-solo-consultar-agregar.png)

*Figura 5h. Ejecutor en Productos: consulta y alta disponibles; edición y eliminación ausentes.*

![Alta de producto permitida al Ejecutor](screenshots/entregable-3/44-ejecutor-alta-producto-permitida.png)

*Figura 5i. Formulario de alta de Producto disponible para Ejecutor, abierto sin enviarlo.*

![Edición no habilitada al Ejecutor](screenshots/entregable-3/45-ejecutor-edicion-cliente-no-habilitada.png)

*Figura 5j. El parámetro directo `?edit=true` no habilita el formulario ni controles de modificación al Ejecutor.*

![Usuarios denegado al Ejecutor](screenshots/entregable-3/46-ejecutor-usuarios-denegado.png)

*Figura 5k. Acceso directo del Ejecutor a `/admin/users`, rechazado por autorización.*

![Bitácora denegada al Ejecutor](screenshots/entregable-3/47-ejecutor-bitacora-denegada.png)

*Figura 5l. Acceso directo del Ejecutor a `/bitacora`, rechazado por autorización.*

#### Resumen de estados de API

| Solicitud no destructiva | Supervisor | Ejecutor |
| --- | ---: | ---: |
| `GET /api/clientes` | `200` | `200` |
| `GET /api/productos` | `200` | `200` |
| `POST /api/clientes` con cuerpo vacío | `403` | `400` autorizado, rechazado por validación |
| `POST /api/productos` con cuerpo vacío | `403` | `400` autorizado, rechazado por validación |
| `PUT /api/clientes/{id-inexistente}` con cuerpo vacío | `400` autorizado, rechazado por validación | `403` |
| `PUT /api/productos/{id-inexistente}` con cuerpo vacío | `400` autorizado, rechazado por validación | `403` |
| `DELETE /api/clientes/{id-inexistente}` | `403` | `403` |
| `DELETE /api/productos/{id-inexistente}` | `403` | `403` |
| `GET /api/users` | `403` | `403` |

## 8. Integración del inicio de sesión

El formulario de acceso envía las credenciales a `POST /api/auth/login`. Tras validar la identidad, la API devuelve un token con roles y permisos; Blazor establece la cookie HttpOnly y conserva el token en sesión del servidor para las solicitudes posteriores. La prueba automatizada inició sesiones independientes con los tres usuarios semilla y observó la identidad y el rol representados en la barra lateral.

![Pantalla de inicio de sesión](screenshots/entregable-3/01-login.png)

*Figura 6. Formulario real de autenticación antes de iniciar sesión; la contraseña permanece enmascarada.*

## 9. Módulo de usuarios

El módulo, exclusivo del Administrador, permite buscar cuentas, filtrar por rol y estado, abrir perfiles, preparar altas, administrar roles, restablecer contraseñas, cambiar el estado de acceso y solicitar eliminación. La verificación utilizó cuentas semilla y no confirmó ninguna acción irreversible.

![Listado administrativo de usuarios](screenshots/entregable-3/03-usuarios-listado.png)

*Figura 7. Listado de usuarios con filtros, estado y roles visibles.*

![Detalle de usuario](screenshots/entregable-3/04-usuario-detalle.png)

*Figura 8. Perfil administrativo con controles de datos, roles, contraseña y estado.*

![Confirmación de cambio de estado](screenshots/entregable-3/05-usuario-estado-confirm.png)

*Figura 9. Confirmación previa al cambio de estado de una cuenta semilla; la acción no fue ejecutada.*

![Confirmación de eliminación de usuario](screenshots/entregable-3/06-usuario-eliminar-confirm.png)

*Figura 10. Salvaguarda previa a eliminación; no se confirmó la operación.*

![Formulario de alta de usuario](screenshots/entregable-3/07-usuario-alta.png)

*Figura 11. Formulario administrativo para registrar una cuenta.*

## 10. Módulo de clientes

El mantenimiento de clientes ofrece listado en cuadrícula o tabla, búsqueda por nombre, filtros adicionales, alta, ficha, edición, eliminación condicionada por rol, panel y generación de reportes. La búsqueda automatizada consultó un nombre existente y abrió una ficha desde el resultado real.

![Búsqueda de clientes](screenshots/entregable-3/08-clientes-busqueda.png)

*Figura 12. Resultado de búsqueda en vista tabular durante sesión Administrador.*

![Alta de cliente](screenshots/entregable-3/09-cliente-alta.png)

*Figura 13. Formulario de registro con campos y validaciones del módulo.*

![Detalle de cliente](screenshots/entregable-3/10-cliente-detalle.png)

*Figura 14. Ficha de consulta obtenida al recorrer una tarjeta del listado.*

![Edición de cliente](screenshots/entregable-3/11-cliente-edicion.png)

*Figura 15. Estado de edición de una ficha existente; no se enviaron cambios.*

![Confirmación de eliminación de cliente](screenshots/entregable-3/12-cliente-eliminar-confirm.png)

*Figura 16. Confirmación previa a eliminación desde listado; la operación no fue ejecutada.*

La comparación por rol confirmó que Supervisor dispone de modificación pero no de alta ni eliminación, mientras Ejecutor dispone de alta pero no de modificación ni eliminación.

![Clientes como Supervisor](screenshots/entregable-3/26-supervisor-clientes.png)

*Figura 17. Módulo de clientes para Supervisor, con acciones ajustadas a su política.*

![Clientes como Ejecutor](screenshots/entregable-3/31-ejecutor-clientes.png)

*Figura 18. Módulo de clientes para Ejecutor, con alta disponible y acciones de modificación/eliminación ausentes.*

## 11. Módulo de productos

El módulo gestiona código, nombre, precio, stock, categoría y unidad de medida. Incluye búsqueda, filtros combinables, alta, consulta, modificación, confirmación de eliminación, exportación y panel especializado.

![Búsqueda de productos](screenshots/entregable-3/13-productos-busqueda.png)

*Figura 19. Consulta por nombre en vista tabular.*

![Alta de producto](screenshots/entregable-3/14-producto-alta.png)

*Figura 20. Formulario de registro de producto.*

![Detalle de producto](screenshots/entregable-3/15-producto-detalle.png)

*Figura 21. Ficha de un producto persistido.*

![Edición de producto](screenshots/entregable-3/16-producto-edicion.png)

*Figura 22. Formulario de modificación; los cambios no fueron enviados.*

![Confirmación de eliminación de producto](screenshots/entregable-3/17-producto-eliminar-confirm.png)

*Figura 23. Confirmación destructiva disponible solo para Administrador; no se confirmó.*

![Productos como Supervisor](screenshots/entregable-3/27-supervisor-productos.png)

*Figura 24. Supervisor con consulta y modificación, sin alta ni eliminación.*

![Productos como Ejecutor](screenshots/entregable-3/33-ejecutor-productos.png)

*Figura 25. Ejecutor con consulta y alta, sin modificación ni eliminación.*

## 12. Reportería y exportación

Reportería centraliza accesos a informes de clientes y productos. Cada mantenimiento permite aplicar filtros antes de generar PDF o Excel y expone una descarga de datos crudos. Durante la revisión se abrieron los diálogos de preparación; no se afirma la inspección del contenido binario descargado, pues la evidencia se concentra en el flujo de selección y confirmación visible.

![Módulo de reportería](screenshots/entregable-3/18-reporteria.png)

*Figura 26. Punto de acceso central a reportes y exportaciones.*

![Preparación de reporte de clientes](screenshots/entregable-3/19-clientes-reporte-confirm.png)

*Figura 27. Confirmación y filtros previos a generar PDF o Excel de clientes.*

![Preparación de reporte de productos](screenshots/entregable-3/20-productos-reporte-confirm.png)

*Figura 28. Confirmación previa, filtros y selección de estado de stock para productos.*

## 13. Paneles de indicadores

El panel general presenta totales de clientes y productos, productos sin stock, stock bajo, distribución por categoría, altas mensuales y actividad reciente. Los paneles especializados amplían la lectura por dominio.

![Panel de clientes](screenshots/entregable-3/21-dashboard-clientes.png)

*Figura 29. Indicadores especializados de clientes.*

![Panel de productos](screenshots/entregable-3/22-dashboard-productos.png)

*Figura 30. Indicadores especializados de productos y existencias.*

## 14. Bitácora y trazabilidad

Bitácora permite consultar actividad operativa y está disponible para Administrador y Supervisor. Su ausencia en el menú del Ejecutor y el rechazo de acceso directo corroboran la política definida.

![Bitácora como Administrador](screenshots/entregable-3/23-bitacora.png)

*Figura 31. Consulta de eventos durante sesión Administrador.*

![Bitácora como Supervisor](screenshots/entregable-3/29-supervisor-bitacora.png)

*Figura 32. Consulta permitida durante sesión Supervisor.*

## 15. Buscadores, filtros y consultas dinámicas

Usuarios admite búsqueda por nombre o correo y filtros por rol y estado. Clientes y productos incorporan búsqueda principal, campos opcionales y operadores documentados en la interfaz (`*`, `||` y `&&`). Los parámetros se representan en la URL y son procesados mediante formularios GET, mecanismo compatible con Static SSR y favorable para reproducir consultas.

Las figuras 7, 12 y 19 evidencian los tres contextos de consulta. Los enlaces para limpiar restablecen el estado de los filtros sin requerir componentes interactivos.

## 16. Interfaz y experiencia de usuario

La interfaz conserva navegación lateral, jerarquía visual consistente, mensajes de estado, formularios alineados, diseño adaptable y acciones identificadas por color e iconografía. El cambio de tema se persiste mediante cookie y recarga la página, en coherencia con el modelo Static SSR.

![Modo oscuro](screenshots/entregable-3/24-modo-oscuro.png)

*Figura 33. Bitácora renderizada con el tema oscuro persistido.*

## 17. Base de datos e integración completa

La ejecución en Docker Compose confirmó el arranque coordinado de PostgreSQL, API y Blazor. Los listados mostraron registros persistidos, los paneles calcularon indicadores a partir de ellos y las sesiones autenticadas consumieron la API mediante el token almacenado en servidor. Las migraciones se aplican al iniciar la API en el entorno Compose y las cuentas semilla se habilitan mediante configuración local.

No se incluyen credenciales ni valores secretos en este informe. El archivo `.env.example` documenta únicamente las variables requeridas; los valores reales permanecen fuera de la evidencia.

## 18. Resultados y trazabilidad de requisitos

| Requisito | Resultado observado | Evidencia |
| --- | --- | --- |
| Menú principal integrado | Conforme; cambia según rol | Figuras 1 a 3 |
| Inicio de sesión | Conforme para tres cuentas semilla | Figura 6 y figuras 1 a 3 |
| Seguridad por roles | Conforme en visibilidad y rechazo de ruta | Figuras 1 a 5, 17, 18, 24 y 25 |
| Gestión de usuarios | Listado, detalle, alta y confirmaciones observados | Figuras 7 a 11 |
| CRUD de clientes | Consulta, búsqueda, alta, detalle, edición y confirmación observados | Figuras 12 a 18 |
| CRUD de productos | Consulta, búsqueda, alta, detalle, edición y confirmación observados | Figuras 19 a 25 |
| Buscadores y filtros | Conforme en módulos principales | Figuras 7, 12 y 19 |
| Reportería | Acceso y preparación de reportes observados | Figuras 26 a 28 |
| Exportación Excel | Controles de exportación visibles; contenido descargado no inspeccionado en esta sesión | Figuras 26 a 28 |
| Paneles | Indicadores generales y especializados renderizados | Figuras 1, 29 y 30 |
| Bitácora | Permitida a Administrador/Supervisor y denegada a Ejecutor | Figuras 5, 31 y 32 |
| Modo oscuro | Conforme | Figura 33 |
| Persistencia | Registros e indicadores reales recuperados desde el stack | Figuras 1, 7, 12, 19, 29 y 30 |

### 18.1 Resultado cuantitativo de la sesión

- Tres roles autenticados y recorridos.
- Cuarenta y siete capturas PNG generadas sobre la aplicación real.
- Tres módulos de mantenimiento inspeccionados.
- Cuatro rutas UI no autorizadas comprobadas explícitamente, además de nueve combinaciones de autorización por rol verificadas en la API.
- Tres diálogos destructivos y un cambio de estado observados sin ejecutar la acción final.
- Cero registros creados, modificados o eliminados durante la captura.

## 19. Limitaciones de la evidencia

La sesión documenta comportamiento funcional visible y autorización de rutas, pero no sustituye pruebas especializadas de carga, penetración o accesibilidad. La exportación se verificó hasta sus controles y confirmaciones, no mediante análisis del archivo descargado. Las operaciones destructivas se detuvieron intencionalmente antes de su ejecución para conservar los datos semilla.

## 20. Guion para defensa final

1. Explicar la arquitectura y el flujo API-first de autenticación.
2. Iniciar sesión como Administrador y mostrar el panel general.
3. Recorrer usuarios y abrir confirmaciones de rol, estado o eliminación sin afectar cuentas esenciales.
4. Demostrar búsqueda, detalle y edición en clientes y productos.
5. Mostrar preparación de PDF y Excel, junto con los paneles especializados.
6. Cambiar a Supervisor y comparar las acciones disponibles.
7. Intentar la ruta administrativa para evidenciar la denegación efectiva.
8. Cambiar a Ejecutor y mostrar alta habilitada, modificación ausente y Bitácora denegada.
9. Cerrar con trazabilidad de requisitos, resultados y limitaciones.

## 21. Conclusión

La integración verificada demuestra que AxionERP articula frontend, API y persistencia en una solución funcional y coherente con la matriz de responsabilidades propuesta. Los tres perfiles reciben menús y acciones diferenciados, mientras las rutas protegidas rechazan accesos incompatibles con el rol. Los módulos de usuarios, clientes y productos comparten patrones consistentes de consulta, formulario y confirmación; reportería, paneles, bitácora y modo oscuro amplían el alcance operativo.

La evidencia fue producida mediante navegación automatizada sobre el despliegue real, no mediante maquetas. Al distinguir las acciones observadas de aquellas que deliberadamente no se ejecutaron, el informe mantiene trazabilidad académica sin atribuir resultados no comprobados. En consecuencia, la etapa III presenta una integración reproducible, documentada y apta para evaluación, con limitaciones claramente declaradas.

## Anexo A. Inventario de evidencia

El inventario completo, con rol y escenario de cada una de las 47 imágenes, se encuentra en [`docs/screenshots/entregable-3/README.md`](screenshots/entregable-3/README.md).
