# AxionERP — Documento técnico (Entregable, Parte 1)

**Asignatura:** Desarrollo de Software con Tecnologías Propietarias y Open Source I (ISO-615)
**Proyecto:** AxionERP · **Repositorio:** OpenSource1 · **Rama:** Fix-Features

> Por indicación académica, este documento no incluye portada. Las capturas provienen de una ejecución real del sistema
> sobre Docker Compose y las pruebas se ejecutaron sobre la misma versión del código.

## 1. Planteamiento del problema

### 1.1 Nombre del sistema

El sistema se denomina **AxionERP**. Se trata de una aplicación web de gestión empresarial (ERP) orientada a una pequeña o
mediana empresa comercial dominicana, que reúne en una sola plataforma la administración de clientes, productos,
existencias, facturación, cuentas por cobrar y la contabilidad asociada a esas operaciones, con acceso controlado según
el rol de cada usuario.

### 1.2 Situación actual

En la empresa de referencia, la información de clientes, productos, existencias, facturación y cuentas por cobrar se
registra en hojas de cálculo dispersas, mantenidas por distintas personas y sin una fuente única de verdad. No existe
control de acceso: cualquier empleado con el archivo puede consultar o alterar precios, saldos o existencias, y no queda
constancia de quién realizó cada cambio ni cuándo.

Esta situación produce errores frecuentes de existencia, porque las ventas no descuentan el inventario de forma
automática, y diferencias en los saldos de clientes, porque los cobros se anotan en un archivo distinto al de las
facturas. La conciliación se hace a mano al cierre de cada período, lo que consume tiempo y no ofrece trazabilidad sobre
los documentos emitidos.

### 1.3 Objetivo general

Centralizar la operación comercial de la empresa en un sistema web único, con persistencia en una base de datos
relacional y control de acceso por rol, de modo que cada usuario consulte y modifique únicamente la información que le
corresponde y que todo documento registrado quede trazable.

### 1.4 Objetivos específicos

- Implementar los mantenimientos de clientes, productos, categorías de producto y usuarios, con las acciones de agregar,
  modificar, eliminar, consultar y limpiar campos.
- Controlar el inventario mediante un libro de movimientos por almacén, del que se derivan las existencias y el costo.
- Registrar la facturación de ventas y las notas de crédito como borradores editables que, al postearse, generan sus
  asientos contables y de inventario.
- Llevar las cuentas por cobrar con cobros y aplicaciones sobre las facturas pendientes.
- Ofrecer búsquedas por varios criterios en cada listado y una búsqueda global de módulos y registros.
- Emitir reportes en PDF y exportaciones a Excel de los listados principales.
- Mantener una bitácora que permita auditar las operaciones realizadas en el sistema.

### 1.5 Usuarios del sistema

El sistema distingue tres roles, definidos con ASP.NET Core Identity. Cada rol se traduce en un conjunto de permisos que
la API evalúa en cada petición y que la interfaz utiliza para mostrar u ocultar las acciones disponibles. La
correspondencia es la misma que documenta la sección "Roles y permisos" del `README.md` del repositorio.

| Rol | Consultar | Agregar | Modificar | Eliminar | Configurar |
| - | - | - | - | - | - |
| `Administrador` | Sí | Sí | Sí | Sí | Sí |
| `Supervisor` | Sí | No | Sí | No | No |
| `Ejecutor` | Sí | Sí | No | No | No |

El Administrador gestiona además los usuarios y la configuración reservada del sistema, como las fechas de registro
permitidas. El Supervisor revisa y corrige la información existente sin crear ni eliminar registros, y el Ejecutor
registra nueva información y la consulta, pero no puede alterar lo ya registrado.

### 1.6 Alcance

El alcance de esta entrega comprende los módulos agrupados en el menú principal del sistema: Clientes, Productos,
Inventario, Ventas, Facturación, Contabilidad, Reportes, Configuración y Administración. Dentro de ellos se incluyen los
mantenimientos maestros, el libro de inventario, los borradores y documentos posteados de facturas y notas de crédito, el
libro de clientes con sus cobros, las consultas contables, los reportes PDF y Excel, la gestión de usuarios y la
bitácora.

### 1.7 Limitaciones

El sistema opera con una sola moneda, el peso dominicano (DOP), y no emite comprobantes fiscales electrónicos ante la
Dirección General de Impuestos Internos (DGII). No incluye el ciclo de compras ni la gestión de proveedores, por lo que
las entradas de inventario se registran mediante diarios de ajuste. El despliegue previsto es local, sobre contenedores
Docker orquestados con Docker Compose, y no contempla alta disponibilidad ni un entorno de nube.

### 1.8 Beneficios esperados

La empresa dispondrá de una sola fuente de datos para clientes, productos, existencias y saldos, lo que elimina la
conciliación manual entre archivos. Los permisos son coherentes entre la interfaz y la API: una acción que el rol no
permite no se muestra, y aunque se forzara la petición, la API la rechaza. Por último, los documentos posteados y los
libros de movimientos son de solo inserción (append-only), de modo que cada factura, nota de crédito o cobro queda
trazable y cualquier corrección se realiza con un documento nuevo en lugar de sobrescribir el anterior.

## 2. Tecnología y lenguaje

### 2.1 Plataforma elegida

AxionERP se desarrolló con **ASP.NET Core 10** y el lenguaje **C#**, sobre el marco `net10.0`, versión de soporte a
largo plazo (LTS) publicada en noviembre de 2025. La solución expone una API REST construida con controladores MVC y una
aplicación **Blazor Web App** en modo **Static SSR** (renderizado estático en el servidor) como capa de vistas. La base
de datos es **PostgreSQL 17**, a la que se accede con Entity Framework Core 10 (proveedor Npgsql) para las escrituras y
las migraciones, y con **Dapper** para las lecturas. La lógica de aplicación se organiza con **MediatR** y se valida con
FluentValidation; los reportes se generan con QuestPDF y las exportaciones con ClosedXML.

### 2.2 Justificación

La elección de ASP.NET Core 10 responde, en primer lugar, al soporte LTS, que garantiza actualizaciones de seguridad
durante la vida académica y operativa del proyecto. En segundo lugar, el servidor Kestrel ofrece un rendimiento elevado
con un consumo moderado de recursos, adecuado para un despliegue local en contenedores. En tercer lugar, el tipado
estático de C# se comparte entre capas: los DTOs definidos en `OpenSource1.Application` son los mismos que devuelve la
API y que consume la interfaz Blazor, lo que evita duplicar contratos y detecta en compilación cualquier incoherencia.

El ecosistema de .NET cubre, además, las dos formas de acceso a datos que el sistema necesita: EF Core para versionar el
esquema y persistir agregados con control de concurrencia, y Dapper para consultas SQL explícitas y eficientes. Por
último, la plataforma favorece la mantenibilidad y las pruebas: la solución cuenta con pruebas automatizadas en xUnit que
levantan la API y la interfaz en memoria con `WebApplicationFactory` y las ejecutan contra un PostgreSQL real en un
contenedor efímero creado por la propia fixture de pruebas.

### 2.3 Por qué Blazor Static SSR en lugar de MVC con Razor Views

Blazor en modo Static SSR produce el HTML en el servidor, igual que MVC con Razor Views, de modo que cada página llega al
navegador completamente renderizada y funciona sin JavaScript. La diferencia está en el modelo de composición: Blazor
permite construir la interfaz con componentes reutilizables, como la barra de acciones `PageToolbar`, la página de
formulario `EntityFormPage` y los componentes de campos `*Fields.razor` (por ejemplo, `ClienteFields.razor` o
`ProductoFields.razor`), que se comparten entre la creación y la edición de cada entidad.

Los formularios se envían por POST con token antiforgery y se enlazan al modelo mediante `[SupplyParameterFromForm]`,
mientras que los filtros y la paginación viajan en la cadena de consulta con `[SupplyParameterFromQuery]`. La navegación
mejorada de Blazor actualiza el contenido sin recargar la página completa. Como no se utiliza ningún modo interactivo, no
hay conexión WebSocket ni código WebAssembly en el navegador; esto mantiene el JWT fuera del alcance de JavaScript, ya que
el servidor Blazor lo guarda en una sesión de servidor asociada a una cookie HttpOnly.

## 3. Arquitectura del sistema

![Arquitectura de AxionERP](diagramas/arquitectura.png)

### 3.1 Flujo de una petición

Una operación del usuario sigue siempre el mismo recorrido. El usuario interactúa con la interfaz Blazor, que envía un
formulario o una petición GET al servidor Blazor. Este, mediante un cliente HTTP tipado, llama al controlador
correspondiente de la API adjuntando el JWT guardado en la sesión de servidor. El controlador valida la autorización y
envía un comando o una consulta a la capa de aplicación a través de MediatR.

En la capa de aplicación, cada petición atraviesa una cadena de comportamientos (behaviors): `LoggingBehavior` registra
la operación, `ValidationBehavior` aplica las reglas de FluentValidation y `TransactionBehavior` envuelve los comandos en
una transacción. El manejador (handler) ejecuta la lógica de negocio y utiliza la capa de acceso a datos: EF Core para las
escrituras y Dapper para las lecturas, ambos sobre PostgreSQL. La respuesta regresa por el mismo camino y el servidor
Blazor la convierte en HTML.

### 3.2 Capas y proyectos

La solución sigue una arquitectura en capas de tipo Onion, en la que las dependencias apuntan hacia el dominio. Cada capa
es un proyecto independiente dentro de `test.slnx`:

| Proyecto | Responsabilidad |
| - | - |
| `OpenSource1.Core` | Entidades de dominio y abstracciones base. |
| `OpenSource1.Application` | Casos de uso, DTOs, CQRS, contratos y reglas de aplicación. |
| `OpenSource1.Infrastructure` | EF Core, Identity, Dapper, repositorios y Unit of Work. |
| `OpenSource1.Api` | Endpoints HTTP, autenticación JWT y autorización por políticas. |
| `OpenSource1.Blazor` | Interfaz web Blazor, formularios, menú y consumo de API. |

El dominio no depende de ninguna otra capa; la aplicación depende solo del dominio y define las interfaces que la
infraestructura implementa. La API y la interfaz Blazor son los puntos de entrada: la primera compone todas las capas del
servidor y la segunda únicamente se comunica con la API por HTTP, sin acceso directo a la base de datos.

### 3.3 Seguridad en la arquitectura

La autenticación se basa en un JWT emitido por la API tras validar las credenciales con ASP.NET Core Identity. El
servidor Blazor recibe ese token y lo guarda en una sesión de servidor; al navegador solo llega una cookie de sesión
marcada como HttpOnly y `SameSite=Strict`, que JavaScript no puede leer. De esta forma el token no se almacena en
`localStorage`, `sessionStorage` ni en cookies accesibles desde el cliente.

La autorización utiliza las políticas `CanConsult`, `CanAdd`, `CanModify`, `CanDelete` y `CanAdministrar`, derivadas del
rol del usuario y evaluadas en ambos lados. La interfaz las usa para ocultar menús y acciones que el usuario no puede
ejecutar, pero la decisión final corresponde a la API: si una petición llega sin el permiso necesario, la API responde
con un código 403 real, que la interfaz muestra al usuario como mensaje de error.

## 4. Diseño de la base de datos

![Modelo entidad-relación (núcleo)](diagramas/modelo-er.png)

### 4.1 Bases de datos

El sistema utiliza dos bases de datos PostgreSQL independientes dentro del mismo servidor. La base `AxionERP_App`
contiene el modelo de negocio: maestros, libros de movimientos, documentos de venta y contabilidad. La base
`AxionERP_Identity` contiene las tablas de ASP.NET Core Identity con los usuarios, los roles y sus asignaciones. La
separación aísla las credenciales del resto de los datos y permite versionar cada esquema con su propio contexto de EF
Core.

### 4.2 Correspondencia con las entidades solicitadas

El enunciado solicita un conjunto de entidades de referencia. La siguiente tabla indica en qué tablas reales del sistema
se materializa cada una; los nombres corresponden al esquema generado por las migraciones.

| Entidad solicitada | Tabla(s) reales | Observaciones |
| - | - | - |
| Usuarios / Roles | `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` (base `AxionERP_Identity`) | ASP.NET Core Identity; permisos coarse derivados del rol. |
| Clientes | `SociosNegocio` | Código correlativo, tipo, documento fiscal (RNC/cédula), término de pago, límite de crédito, grupos contables. |
| Productos | `Productos` | Precio, unidad base, método de costeo, costo unitario vigente, grupos contables. |
| Categorías | `CategoriasProducto` | Jerarquía con categoría padre. |
| Inventario | `MovimientosProducto`, `MovimientosValor`, `Almacenes` | Libro append-only de cantidades y de costo por almacén. |
| Ventas / DetalleVenta / Facturas | `FacturasVenta`, `LineasFacturaVenta`, `LineasIvaFacturaVenta` (+ `FacturasVentaBorrador`, `LineasFacturaVentaBorrador`) | Borrador editable y documento posteado inmutable. |
| CuentasPorCobrar | `MovimientosCliente`, `MovimientosClienteDetalle` | Importe restante derivado; cobros y aplicaciones. |

Además de estas tablas, el esquema incluye las de soporte que requieren los procesos descritos: unidades de medida,
términos de pago, series de numeración, plan de cuentas, grupos y configuraciones contables, diarios de inventario,
notas de crédito y el libro contable (`MovimientosContables`).

### 4.3 Claves y tipos principales

El detalle completo de claves primarias, claves foráneas y tipos de cada columna se recoge en el diccionario de datos
`diagramas/diccionario-datos.md`. En términos generales, las tablas maestras (socios de negocio, productos, categorías,
almacenes) usan claves primarias de tipo `uuid`; los documentos posteados, como `FacturasVenta`, usan como clave primaria
su `Numero` de tipo texto, asignado por la serie de numeración; y los libros de movimientos usan claves `bigint` de
identidad generadas por la base de datos.

Los importes monetarios, precios y costos se almacenan como `numeric(18,4)`, las cantidades como `numeric(18,6)` y los
porcentajes de descuento e impuesto como `numeric(9,5)`, lo que evita los errores de redondeo propios de los tipos de
coma flotante. Las tablas editables incorporan la columna de sistema `xmin` de PostgreSQL como token de concurrencia.

## 5. Persistencia de datos

### 5.1 Code First con migraciones de EF Core

El esquema se define con el enfoque Code First: las entidades y sus configuraciones en C# son la fuente del modelo, y EF
Core genera a partir de ellas las migraciones que crean y modifican las tablas. El contexto de aplicación acumula **38**
migraciones en `src/OpenSource1.Infrastructure/Data/Migrations/Application`, y el contexto de identidad dispone de las
suyas en `src/OpenSource1.Infrastructure/Identity/Migrations`. La API aplica las migraciones pendientes al arrancar
cuando la opción `Database__ApplyMigrationsOnStartup` está activa.

Este enfoque mantiene el esquema versionado junto al código en el mismo repositorio, de modo que cada cambio de modelo
queda revisado y registrado en el historial de Git. También hace que la base de datos sea reproducible: el mismo conjunto
de migraciones construye el esquema en el entorno de Docker Compose y en los contenedores efímeros de las pruebas
automatizadas, sin scripts manuales.

### 5.2 Lecturas con Dapper

Los listados paginados y las consultas con filtros se resuelven con Dapper, que ejecuta SQL explícito y proyecta el
resultado directamente sobre DTOs de lectura. Los filtros de texto se aplican con `ILIKE … ESCAPE`, escapando los
metacaracteres del patrón para que la búsqueda sea literal, y los valores siempre viajan como parámetros, nunca
concatenados en la sentencia. La columna de ordenamiento que elige el usuario se valida contra una lista blanca de
columnas permitidas antes de incorporarse a la cláusula `ORDER BY`, lo que impide la inyección de SQL por esa vía.

### 5.3 Transacciones y concurrencia

Cada comando se ejecuta dentro de una transacción gestionada por `TransactionBehavior`: si el manejador termina
correctamente, los cambios se confirman; si ocurre un error, se revierten en bloque. Esto es esencial en operaciones como
el posteo de una factura, que inserta el documento, sus líneas, los movimientos de inventario, el movimiento del cliente
y los asientos contables en una sola unidad.

La concurrencia se controla de forma optimista con la columna `xmin`. Al editar un registro, la interfaz conserva la
versión leída y la envía con la modificación; si otro usuario cambió el registro en el intervalo, la versión ya no
coincide, EF Core detecta el conflicto y la API responde con un código 409 que informa al usuario de que debe recargar los datos en lugar de
sobrescribir cambios ajenos.

## 6. Mantenimientos (CRUD)
<!-- D4 -->

## 7. Búsquedas y consultas
<!-- D4 -->

## 8. Menú y navegación
<!-- D4 -->

## 9. Pruebas
<!-- D4 -->

## 10. Entrega
<!-- D4 -->
