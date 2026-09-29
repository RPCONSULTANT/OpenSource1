# Diccionario de datos (núcleo)

Generado desde `information_schema` por `tests/e2e/scripts/esquema_a_mermaid.py`.

### SociosNegocio

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Email | character varying(256) | Sí |  |
| Telefono | character varying(50) | Sí |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| ImagePath | character varying(500) | Sí |  |
| DireccionLinea1 | character varying(300) | Sí |  |
| DireccionLinea2 | character varying(300) | Sí |  |
| PaisCodigo | character varying(2) | Sí |  |
| PaisNombre | character varying(100) | Sí |  |
| Sector | character varying(100) | Sí |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| Codigo | character varying(20) | No |  |
| Tipo | smallint | No |  |
| NombreComercial | character varying(200) | No |  |
| RazonSocial | character varying(200) | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying(20) | Sí |  |
| Ciudad | character varying(100) | Sí |  |
| TerminoPagoId | uuid | Sí | FK → TerminosPago.Id |
| LimiteCredito | numeric(18,4) | No |  |
| Bloqueado | smallint | No |  |
| GrupoClienteContableId | uuid | Sí | FK → GruposClienteContable.Id |
| GrupoIvaNegocioId | uuid | Sí | FK → GruposIvaNegocio.Id |
| GrupoNegocioId | uuid | Sí | FK → GruposNegocio.Id |

### TerminosPago

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying(20) | No |  |
| Descripcion | character varying(200) | No |  |
| DiasVencimiento | integer | No |  |
| DiasDescuento | integer | No |  |
| PorcentajeDescuento | numeric(9,5) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |

### Productos

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying(50) | No |  |
| Nombre | character varying(200) | No |  |
| PrecioVenta | numeric(18,4) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| ImagePath | character varying(500) | Sí |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| CategoriaId | uuid | No | FK → CategoriasProducto.Id |
| UnidadMedidaBaseId | uuid | No | FK → UnidadesMedida.Id |
| MetodoCosteo | smallint | No |  |
| CostoUnitario | numeric(18,4) | No |  |
| CostoEstandar | numeric(18,4) | No |  |
| CostoAjustado | boolean | No |  |
| Bloqueado | smallint | No |  |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |

### CategoriasProducto

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying(30) | No |  |
| Nombre | character varying(100) | No |  |
| CategoriaPadreId | uuid | Sí | FK → CategoriasProducto.Id |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |

### UnidadesMedida

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying(10) | No |  |
| Nombre | character varying(50) | No |  |
| Decimales | smallint | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |

### Almacenes

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying(10) | No |  |
| Nombre | character varying(100) | No |  |
| DireccionLinea1 | character varying(300) | Sí |  |
| DireccionLinea2 | character varying(300) | Sí |  |
| Ciudad | character varying(100) | Sí |  |
| PaisCodigo | character varying(2) | Sí |  |
| Bloqueado | boolean | No |  |
| EsPredeterminado | boolean | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |

### MovimientosProducto

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| ProductoId | uuid | No | FK → Productos.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| TipoMovimiento | smallint | No |  |
| TipoDocumento | smallint | No |  |
| NumeroDocumento | character varying(20) | Sí |  |
| NumeroLineaDocumento | integer | No |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| Cantidad | numeric(18,6) | No |  |
| CantidadRestante | numeric(18,6) | Sí |  |
| CantidadFacturada | numeric(18,6) | No |  |
| UnidadMedidaId | uuid | No | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric(18,6) | No |  |
| SocioNegocioId | uuid | Sí | FK → SociosNegocio.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying(50) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UsuarioId | uuid | Sí |  |

### MovimientosValor

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| MovimientoProductoId | bigint | Sí | FK → MovimientosProducto.Id |
| ProductoId | uuid | No | FK → Productos.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| TipoValor | smallint | No |  |
| TipoMovimiento | smallint | No |  |
| FechaRegistro | date | No |  |
| CantidadValorada | numeric(18,6) | No |  |
| CantidadFacturada | numeric(18,6) | No |  |
| ImporteCosto | numeric(18,4) | No |  |
| CostoPorUnidad | numeric(18,4) | No |  |
| ImporteVenta | numeric(18,4) | No |  |
| ImporteCostoPosteadoContabilidad | numeric(18,4) | No |  |
| Ajuste | boolean | No |  |
| TipoDocumento | smallint | No |  |
| NumeroDocumento | character varying(20) | Sí |  |
| NumeroLineaDocumento | integer | No |  |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| GrupoNegocioId | uuid | Sí | FK → GruposNegocio.Id |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying(50) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UsuarioId | uuid | Sí |  |

### FacturasVentaBorrador

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Numero | character varying(20) | No |  |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| SocioNegocioFacturarAId | uuid | No | FK → SociosNegocio.Id |
| NombreFacturacion | character varying(200) | No |  |
| RazonSocialFacturacion | character varying(200) | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying(20) | Sí |  |
| DireccionFacturacionLinea1 | character varying(300) | Sí |  |
| DireccionFacturacionLinea2 | character varying(300) | Sí |  |
| CiudadFacturacion | character varying(100) | Sí |  |
| PaisCodigoFacturacion | character varying(2) | Sí |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| FechaVencimiento | date | No |  |
| TerminoPagoId | uuid | Sí | FK → TerminosPago.Id |
| GrupoNegocioId | uuid | No | FK → GruposNegocio.Id |
| GrupoIvaNegocioId | uuid | No | FK → GruposIvaNegocio.Id |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| Estado | smallint | No |  |
| Moneda | character varying(3) | No |  |
| Descripcion | character varying(200) | Sí |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |

### LineasFacturaVentaBorrador

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| FacturaVentaBorradorId | uuid | No | FK → FacturasVentaBorrador.Id |
| NumeroLinea | integer | No |  |
| Tipo | smallint | No |  |
| ProductoId | uuid | Sí | FK → Productos.Id |
| CuentaContableId | uuid | Sí | FK → CuentasContables.Id |
| Descripcion | character varying(200) | Sí |  |
| AlmacenId | uuid | Sí | FK → Almacenes.Id |
| UnidadMedidaId | uuid | Sí | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric(18,6) | No |  |
| Cantidad | numeric(18,6) | No |  |
| PrecioUnitario | numeric(18,4) | No |  |
| PorcentajeDescuentoLinea | numeric(9,5) | No |  |
| ImporteDescuentoLinea | numeric(18,4) | No |  |
| ImporteLinea | numeric(18,4) | No |  |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| IdentificadorIva | character varying(20) | Sí |  |
| PorcentajeIva | numeric(9,5) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying(100) | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying(100) | Sí |  |

### FacturasVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Numero | character varying(20) | No | PK |
| NumeroBorrador | character varying(20) | No |  |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| SocioNegocioFacturarAId | uuid | No | FK → SociosNegocio.Id |
| NombreFacturacion | character varying(200) | No |  |
| RazonSocialFacturacion | character varying(200) | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying(20) | Sí |  |
| DireccionFacturacionLinea1 | character varying(300) | Sí |  |
| DireccionFacturacionLinea2 | character varying(300) | Sí |  |
| CiudadFacturacion | character varying(100) | Sí |  |
| PaisCodigoFacturacion | character varying(2) | Sí |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| FechaVencimiento | date | No |  |
| TerminoPagoId | uuid | Sí | FK → TerminosPago.Id |
| GrupoNegocioId | uuid | No | FK → GruposNegocio.Id |
| GrupoIvaNegocioId | uuid | No | FK → GruposIvaNegocio.Id |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| Moneda | character varying(3) | No |  |
| Descripcion | character varying(200) | Sí |  |
| ImporteSinIva | numeric(18,4) | No |  |
| ImporteIva | numeric(18,4) | No |  |
| ImporteTotal | numeric(18,4) | No |  |
| RegistroContableId | bigint | Sí | FK → RegistrosContables.Id |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UsuarioId | uuid | Sí |  |

### LineasFacturaVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| FacturaVentaNumero | character varying(20) | No | FK → FacturasVenta.Numero |
| NumeroLinea | integer | No |  |
| Tipo | smallint | No |  |
| ProductoId | uuid | Sí | FK → Productos.Id |
| CuentaContableId | uuid | Sí | FK → CuentasContables.Id |
| Descripcion | character varying(200) | Sí |  |
| AlmacenId | uuid | Sí | FK → Almacenes.Id |
| UnidadMedidaId | uuid | Sí | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric(18,6) | No |  |
| Cantidad | numeric(18,6) | No |  |
| PrecioUnitario | numeric(18,4) | No |  |
| PorcentajeDescuentoLinea | numeric(9,5) | No |  |
| ImporteDescuentoLinea | numeric(18,4) | No |  |
| ImporteLinea | numeric(18,4) | No |  |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| IdentificadorIva | character varying(20) | Sí |  |
| PorcentajeIva | numeric(9,5) | No |  |
| MovimientoProductoId | bigint | Sí | FK → MovimientosProducto.Id |

### LineasIvaFacturaVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| FacturaVentaNumero | character varying(20) | No | FK → FacturasVenta.Numero |
| IdentificadorIva | character varying(20) | No |  |
| PorcentajeIva | numeric(9,5) | No |  |
| BaseImponible | numeric(18,4) | No |  |
| ImporteIva | numeric(18,4) | No |  |
| CuentaIvaId | uuid | No | FK → CuentasContables.Id |

### NotasCreditoVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Numero | character varying(20) | No | PK |
| NumeroBorrador | character varying(20) | No |  |
| FacturaVentaNumero | character varying(20) | No | FK → FacturasVenta.Numero |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| SocioNegocioFacturarAId | uuid | No | FK → SociosNegocio.Id |
| NombreFacturacion | character varying(200) | No |  |
| RazonSocialFacturacion | character varying(200) | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying(20) | Sí |  |
| DireccionFacturacionLinea1 | character varying(300) | Sí |  |
| DireccionFacturacionLinea2 | character varying(300) | Sí |  |
| CiudadFacturacion | character varying(100) | Sí |  |
| PaisCodigoFacturacion | character varying(2) | Sí |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| GrupoNegocioId | uuid | No | FK → GruposNegocio.Id |
| GrupoIvaNegocioId | uuid | No | FK → GruposIvaNegocio.Id |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| CuentaCxCId | uuid | Sí | FK → CuentasContables.Id |
| Moneda | character varying(3) | No |  |
| Descripcion | character varying(200) | Sí |  |
| ImporteSinIva | numeric(18,4) | No |  |
| ImporteIva | numeric(18,4) | No |  |
| ImporteTotal | numeric(18,4) | No |  |
| RegistroContableId | bigint | Sí | FK → RegistrosContables.Id |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UsuarioId | uuid | Sí |  |

### LineasNotaCreditoVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| NotaCreditoVentaNumero | character varying(20) | No | FK → NotasCreditoVenta.Numero |
| NumeroLinea | integer | No |  |
| LineaFacturaVentaId | bigint | No | FK → LineasFacturaVenta.Id |
| Tipo | smallint | No |  |
| ProductoId | uuid | Sí | FK → Productos.Id |
| CuentaContableId | uuid | Sí | FK → CuentasContables.Id |
| Descripcion | character varying(200) | Sí |  |
| AlmacenId | uuid | Sí | FK → Almacenes.Id |
| UnidadMedidaId | uuid | Sí | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric(18,6) | No |  |
| Cantidad | numeric(18,6) | No |  |
| PrecioUnitario | numeric(18,4) | No |  |
| PorcentajeDescuentoLinea | numeric(9,5) | No |  |
| ImporteDescuentoLinea | numeric(18,4) | No |  |
| ImporteLinea | numeric(18,4) | No |  |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| IdentificadorIva | character varying(20) | Sí |  |
| PorcentajeIva | numeric(9,5) | No |  |
| DevolverInventario | boolean | No |  |
| MovimientoProductoId | bigint | Sí | FK → MovimientosProducto.Id |

### MovimientosCliente

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| FechaVencimiento | date | No |  |
| TipoDocumento | smallint | No |  |
| NumeroDocumento | character varying(20) | No |  |
| Descripcion | character varying(200) | Sí |  |
| ImporteOriginal | numeric(18,4) | No |  |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| CuentaCxCId | uuid | No | FK → CuentasContables.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying(50) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UsuarioId | uuid | Sí |  |

### MovimientosClienteDetalle

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| MovimientoClienteId | bigint | No | FK → MovimientosCliente.Id |
| TipoMovimiento | smallint | No |  |
| Importe | numeric(18,4) | No |  |
| FechaRegistro | date | No |  |
| MovimientoClienteAplicadoId | bigint | Sí | FK → MovimientosCliente.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying(50) | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying(100) | No |  |
| UsuarioId | uuid | Sí |  |

### AspNetUsers

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | text | No | PK |
| FullName | character varying(200) | Sí |  |
| IsActive | boolean | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UserName | character varying(256) | Sí |  |
| NormalizedUserName | character varying(256) | Sí |  |
| Email | character varying(256) | Sí |  |
| NormalizedEmail | character varying(256) | Sí |  |
| EmailConfirmed | boolean | No |  |
| PasswordHash | text | Sí |  |
| SecurityStamp | text | Sí |  |
| ConcurrencyStamp | text | Sí |  |
| PhoneNumber | text | Sí |  |
| PhoneNumberConfirmed | boolean | No |  |
| TwoFactorEnabled | boolean | No |  |
| LockoutEnd | timestamp with time zone | Sí |  |
| LockoutEnabled | boolean | No |  |
| AccessFailedCount | integer | No |  |
| ProfileImagePath | character varying(500) | Sí |  |

### AspNetRoles

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | text | No | PK |
| Name | character varying(256) | Sí |  |
| NormalizedName | character varying(256) | Sí |  |
| ConcurrencyStamp | text | Sí |  |

### AspNetUserRoles

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| UserId | text | No | PK, FK → AspNetUsers.Id |
| RoleId | text | No | PK, FK → AspNetRoles.Id |
