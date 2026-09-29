# Diccionario de datos (núcleo)

Generado desde `information_schema` por `tests/e2e/scripts/esquema_a_mermaid.py`.

### SociosNegocio

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Email | character varying | Sí |  |
| Telefono | character varying | Sí |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| ImagePath | character varying | Sí |  |
| DireccionLinea1 | character varying | Sí |  |
| DireccionLinea2 | character varying | Sí |  |
| PaisCodigo | character varying | Sí |  |
| PaisNombre | character varying | Sí |  |
| Sector | character varying | Sí |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| Codigo | character varying | No |  |
| Tipo | smallint | No |  |
| NombreComercial | character varying | No |  |
| RazonSocial | character varying | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying | Sí |  |
| Ciudad | character varying | Sí |  |
| TerminoPagoId | uuid | Sí | FK → TerminosPago.Id |
| LimiteCredito | numeric | No |  |
| Bloqueado | smallint | No |  |
| GrupoClienteContableId | uuid | Sí | FK → GruposClienteContable.Id |
| GrupoIvaNegocioId | uuid | Sí | FK → GruposIvaNegocio.Id |
| GrupoNegocioId | uuid | Sí | FK → GruposNegocio.Id |

### TerminosPago

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying | No |  |
| Descripcion | character varying | No |  |
| DiasVencimiento | integer | No |  |
| DiasDescuento | integer | No |  |
| PorcentajeDescuento | numeric | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |

### Productos

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying | No |  |
| Nombre | character varying | No |  |
| PrecioVenta | numeric | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| ImagePath | character varying | Sí |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| CategoriaId | uuid | No | FK → CategoriasProducto.Id |
| UnidadMedidaBaseId | uuid | No | FK → UnidadesMedida.Id |
| MetodoCosteo | smallint | No |  |
| CostoUnitario | numeric | No |  |
| CostoEstandar | numeric | No |  |
| CostoAjustado | boolean | No |  |
| Bloqueado | smallint | No |  |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |

### CategoriasProducto

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying | No |  |
| Nombre | character varying | No |  |
| CategoriaPadreId | uuid | Sí | FK → CategoriasProducto.Id |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |

### UnidadesMedida

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying | No |  |
| Nombre | character varying | No |  |
| Decimales | smallint | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |

### Almacenes

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Codigo | character varying | No |  |
| Nombre | character varying | No |  |
| DireccionLinea1 | character varying | Sí |  |
| DireccionLinea2 | character varying | Sí |  |
| Ciudad | character varying | Sí |  |
| PaisCodigo | character varying | Sí |  |
| Bloqueado | boolean | No |  |
| EsPredeterminado | boolean | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |

### MovimientosProducto

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| ProductoId | uuid | No | FK → Productos.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| TipoMovimiento | smallint | No |  |
| TipoDocumento | smallint | No |  |
| NumeroDocumento | character varying | Sí |  |
| NumeroLineaDocumento | integer | No |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| Cantidad | numeric | No |  |
| CantidadRestante | numeric | Sí |  |
| CantidadFacturada | numeric | No |  |
| UnidadMedidaId | uuid | No | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric | No |  |
| SocioNegocioId | uuid | Sí | FK → SociosNegocio.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
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
| CantidadValorada | numeric | No |  |
| CantidadFacturada | numeric | No |  |
| ImporteCosto | numeric | No |  |
| CostoPorUnidad | numeric | No |  |
| ImporteVenta | numeric | No |  |
| ImporteCostoPosteadoContabilidad | numeric | No |  |
| Ajuste | boolean | No |  |
| TipoDocumento | smallint | No |  |
| NumeroDocumento | character varying | Sí |  |
| NumeroLineaDocumento | integer | No |  |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| GrupoNegocioId | uuid | Sí | FK → GruposNegocio.Id |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UsuarioId | uuid | Sí |  |

### FacturasVentaBorrador

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| Numero | character varying | No |  |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| SocioNegocioFacturarAId | uuid | No | FK → SociosNegocio.Id |
| NombreFacturacion | character varying | No |  |
| RazonSocialFacturacion | character varying | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying | Sí |  |
| DireccionFacturacionLinea1 | character varying | Sí |  |
| DireccionFacturacionLinea2 | character varying | Sí |  |
| CiudadFacturacion | character varying | Sí |  |
| PaisCodigoFacturacion | character varying | Sí |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| FechaVencimiento | date | No |  |
| TerminoPagoId | uuid | Sí | FK → TerminosPago.Id |
| GrupoNegocioId | uuid | No | FK → GruposNegocio.Id |
| GrupoIvaNegocioId | uuid | No | FK → GruposIvaNegocio.Id |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| Estado | smallint | No |  |
| Moneda | character varying | No |  |
| Descripcion | character varying | Sí |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |

### LineasFacturaVentaBorrador

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | uuid | No | PK |
| FacturaVentaBorradorId | uuid | No | FK → FacturasVentaBorrador.Id |
| NumeroLinea | integer | No |  |
| Tipo | smallint | No |  |
| ProductoId | uuid | Sí | FK → Productos.Id |
| CuentaContableId | uuid | Sí | FK → CuentasContables.Id |
| Descripcion | character varying | Sí |  |
| AlmacenId | uuid | Sí | FK → Almacenes.Id |
| UnidadMedidaId | uuid | Sí | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric | No |  |
| Cantidad | numeric | No |  |
| PrecioUnitario | numeric | No |  |
| PorcentajeDescuentoLinea | numeric | No |  |
| ImporteDescuentoLinea | numeric | No |  |
| ImporteLinea | numeric | No |  |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| IdentificadorIva | character varying | Sí |  |
| PorcentajeIva | numeric | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UpdatedBy | character varying | Sí |  |
| IsDeleted | boolean | No |  |
| DeletedAtUtc | timestamp with time zone | Sí |  |
| DeletedBy | character varying | Sí |  |

### FacturasVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Numero | character varying | No | PK |
| NumeroBorrador | character varying | No |  |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| SocioNegocioFacturarAId | uuid | No | FK → SociosNegocio.Id |
| NombreFacturacion | character varying | No |  |
| RazonSocialFacturacion | character varying | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying | Sí |  |
| DireccionFacturacionLinea1 | character varying | Sí |  |
| DireccionFacturacionLinea2 | character varying | Sí |  |
| CiudadFacturacion | character varying | Sí |  |
| PaisCodigoFacturacion | character varying | Sí |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| FechaVencimiento | date | No |  |
| TerminoPagoId | uuid | Sí | FK → TerminosPago.Id |
| GrupoNegocioId | uuid | No | FK → GruposNegocio.Id |
| GrupoIvaNegocioId | uuid | No | FK → GruposIvaNegocio.Id |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| AlmacenId | uuid | No | FK → Almacenes.Id |
| Moneda | character varying | No |  |
| Descripcion | character varying | Sí |  |
| ImporteSinIva | numeric | No |  |
| ImporteIva | numeric | No |  |
| ImporteTotal | numeric | No |  |
| RegistroContableId | bigint | Sí | FK → RegistrosContables.Id |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UsuarioId | uuid | Sí |  |

### LineasFacturaVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| FacturaVentaNumero | character varying | No | FK → FacturasVenta.Numero |
| NumeroLinea | integer | No |  |
| Tipo | smallint | No |  |
| ProductoId | uuid | Sí | FK → Productos.Id |
| CuentaContableId | uuid | Sí | FK → CuentasContables.Id |
| Descripcion | character varying | Sí |  |
| AlmacenId | uuid | Sí | FK → Almacenes.Id |
| UnidadMedidaId | uuid | Sí | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric | No |  |
| Cantidad | numeric | No |  |
| PrecioUnitario | numeric | No |  |
| PorcentajeDescuentoLinea | numeric | No |  |
| ImporteDescuentoLinea | numeric | No |  |
| ImporteLinea | numeric | No |  |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| IdentificadorIva | character varying | Sí |  |
| PorcentajeIva | numeric | No |  |
| MovimientoProductoId | bigint | Sí | FK → MovimientosProducto.Id |

### LineasIvaFacturaVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| FacturaVentaNumero | character varying | No | FK → FacturasVenta.Numero |
| IdentificadorIva | character varying | No |  |
| PorcentajeIva | numeric | No |  |
| BaseImponible | numeric | No |  |
| ImporteIva | numeric | No |  |
| CuentaIvaId | uuid | No | FK → CuentasContables.Id |

### NotasCreditoVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Numero | character varying | No | PK |
| NumeroBorrador | character varying | No |  |
| FacturaVentaNumero | character varying | No | FK → FacturasVenta.Numero |
| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |
| SocioNegocioFacturarAId | uuid | No | FK → SociosNegocio.Id |
| NombreFacturacion | character varying | No |  |
| RazonSocialFacturacion | character varying | Sí |  |
| TipoDocumentoFiscal | smallint | No |  |
| NumeroDocumentoFiscal | character varying | Sí |  |
| DireccionFacturacionLinea1 | character varying | Sí |  |
| DireccionFacturacionLinea2 | character varying | Sí |  |
| CiudadFacturacion | character varying | Sí |  |
| PaisCodigoFacturacion | character varying | Sí |  |
| FechaRegistro | date | No |  |
| FechaDocumento | date | No |  |
| GrupoNegocioId | uuid | No | FK → GruposNegocio.Id |
| GrupoIvaNegocioId | uuid | No | FK → GruposIvaNegocio.Id |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| CuentaCxCId | uuid | Sí | FK → CuentasContables.Id |
| Moneda | character varying | No |  |
| Descripcion | character varying | Sí |  |
| ImporteSinIva | numeric | No |  |
| ImporteIva | numeric | No |  |
| ImporteTotal | numeric | No |  |
| RegistroContableId | bigint | Sí | FK → RegistrosContables.Id |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UsuarioId | uuid | Sí |  |

### LineasNotaCreditoVenta

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| NotaCreditoVentaNumero | character varying | No | FK → NotasCreditoVenta.Numero |
| NumeroLinea | integer | No |  |
| LineaFacturaVentaId | bigint | No | FK → LineasFacturaVenta.Id |
| Tipo | smallint | No |  |
| ProductoId | uuid | Sí | FK → Productos.Id |
| CuentaContableId | uuid | Sí | FK → CuentasContables.Id |
| Descripcion | character varying | Sí |  |
| AlmacenId | uuid | Sí | FK → Almacenes.Id |
| UnidadMedidaId | uuid | Sí | FK → UnidadesMedida.Id |
| CantidadPorUnidadMedida | numeric | No |  |
| Cantidad | numeric | No |  |
| PrecioUnitario | numeric | No |  |
| PorcentajeDescuentoLinea | numeric | No |  |
| ImporteDescuentoLinea | numeric | No |  |
| ImporteLinea | numeric | No |  |
| GrupoProductoId | uuid | Sí | FK → GruposProducto.Id |
| GrupoIvaProductoId | uuid | Sí | FK → GruposIvaProducto.Id |
| GrupoInventarioId | uuid | Sí | FK → GruposInventario.Id |
| IdentificadorIva | character varying | Sí |  |
| PorcentajeIva | numeric | No |  |
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
| NumeroDocumento | character varying | No |  |
| Descripcion | character varying | Sí |  |
| ImporteOriginal | numeric | No |  |
| GrupoClienteContableId | uuid | No | FK → GruposClienteContable.Id |
| CuentaCxCId | uuid | No | FK → CuentasContables.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UsuarioId | uuid | Sí |  |

### MovimientosClienteDetalle

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | bigint | No | PK |
| MovimientoClienteId | bigint | No | FK → MovimientosCliente.Id |
| TipoMovimiento | smallint | No |  |
| Importe | numeric | No |  |
| FechaRegistro | date | No |  |
| MovimientoClienteAplicadoId | bigint | Sí | FK → MovimientosCliente.Id |
| TipoOrigen | smallint | No |  |
| ClaveOrigen | character varying | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| CreatedBy | character varying | No |  |
| UsuarioId | uuid | Sí |  |

### AspNetUsers

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | text | No | PK |
| FullName | character varying | Sí |  |
| IsActive | boolean | No |  |
| CreatedAtUtc | timestamp with time zone | No |  |
| UpdatedAtUtc | timestamp with time zone | Sí |  |
| UserName | character varying | Sí |  |
| NormalizedUserName | character varying | Sí |  |
| Email | character varying | Sí |  |
| NormalizedEmail | character varying | Sí |  |
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
| ProfileImagePath | character varying | Sí |  |

### AspNetRoles

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| Id | text | No | PK |
| Name | character varying | Sí |  |
| NormalizedName | character varying | Sí |  |
| ConcurrencyStamp | text | Sí |  |

### AspNetUserRoles

| Columna | Tipo | Nulo | Clave |
| - | - | - | - |
| UserId | text | No | PK, FK → AspNetUsers.Id |
| RoleId | text | No | PK, FK → AspNetRoles.Id |
