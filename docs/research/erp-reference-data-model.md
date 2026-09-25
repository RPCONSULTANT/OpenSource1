# Modelo de datos ERP de referencia — Dynamics 365 Business Central (con comparación SAP)

> Informe técnico de investigación. Fuente primaria: referencia de objetos de la Base Application
> de Dynamics 365 Business Central en Microsoft Learn (`learn.microsoft.com/dynamics365/business-central/application/base-application/...`),
> que documenta cada tabla a nivel de campo y tipo AL. Secundaria: SAP Help Portal.
> Todas las URLs consultadas están en la sección [Fuentes](#fuentes).
>
> Convenciones de este documento:
> - Los nombres de campo se escriben **exactamente** como en AL (`"Gen. Prod. Posting Group"`), incluidos los puntos y abreviaturas.
> - `Code[n]` = string en mayúsculas de longitud fija usado como clave; `Decimal` = numérico con signo.
> - "FlowField" = campo calculado (`SUM`/`LOOKUP`), **no** almacenado: no se persiste, se recalcula. Es el detalle de diseño
>   más fácil de malinterpretar al portar el modelo a SQL/Dapper.

## Contenido

| § | Área | Tablas principales |
| --- | --- | --- |
| [Mapa](#mapa-general-de-relaciones) | Visión general | — |
| [1](#1-item--producto--tabla-item-27) | Item / Producto | `Item` (27), `Item Unit of Measure` (5404) |
| [2](#2-inventario--movimientos--item-ledger-entry-32-y-value-entry-5802) | Inventario y costeo | `Item Ledger Entry` (32), `Value Entry` (5802), `Item Application Entry` (339) |
| [3](#3-almacenes--location-14-y-bin-7354) | Almacenes | `Location` (14), `Bin` (7354) |
| [4](#4-diarios-de-inventario--item-journal-template-82--item-journal-batch-233--item-journal-line-83) | Diarios de inventario | `Item Journal Template` (82) / `Batch` (233) / `Line` (83), `Item Register` (46) |
| [5](#5-facturación-de-ventas--borrador-3637-vs-posteado-112113) | Facturación de ventas | `Sales Header/Line` (36/37), `Sales Invoice Header/Line` (112/113) |
| [6](#6-movimientos-de-cliente--cxc--cust-ledger-entry-21-y-detailed-cust-ledg-entry-379) | Cuentas por cobrar | `Cust. Ledger Entry` (21), `Detailed Cust. Ledg. Entry` (379), `Customer` (18), `Customer Posting Group` (92) |
| [7](#7-contabilidad--gl-account-15-gl-entry-17-gl-register-45-y-las-posting-setups) | **Contabilidad y posting setups** ⭐ | `G/L Account` (15), `G/L Entry` (17), `G/L Register` (45), `General Posting Setup` (252), `VAT Posting Setup` (325), `Inventory Posting Setup` (5813) |
| [8](#8-numeración-de-documentos--no-series-308--no-series-line-309) | Numeración de documentos | `No. Series` (308), `No. Series Line` (309) |
| [9](#9--asientos-generados-al-postear-una-factura-de-venta) | **Asientos de una factura de venta** ⭐ | — |
| [10](#10-comparación-con-sap) | Comparación con SAP | VBRK/VBRP, MARA/MARC/MARD/MBEW, MKPF/MSEG/MATDOC, BKPF/BSEG/ACDOCA, KNA1/BUT000 |
| [11](#11-síntesis-los-diez-patrones-portables-a-un-erp-propio) | Síntesis: patrones portables | — |
| [Fuentes](#fuentes) · [Apéndice](#apéndice-correcciones-a-suposiciones-frecuentes) | URLs oficiales y correcciones | — |

---

## Mapa general de relaciones

```
                    ┌──────────────────────── MAESTROS ────────────────────────┐
  Customer (18) ─────┐                                     Item (27) ──────┬── Item Unit of Measure (5404)
    │ Customer Posting Group ──► Customer Posting Group (92)│              │     (Item No., Code, Qty. per Unit of Measure)
    │ Gen. Bus. Posting Group ──┐                           │              ├── Item Category (5722)
    │ VAT Bus. Posting Group ──┐│                           │              │
    └───────────────┐          ││    Location (14) ◄────────┘              │
                    │          ││        │                                 │
                    │          ││        └── Bin (7354)  (opcional)        │
                    │          ││                                          │
                    │          │└──► General Posting Setup (252) ◄─────── "Gen. Prod. Posting Group"
                    │          └───► VAT Posting Setup     (325) ◄─────── "VAT Prod. Posting Group"
                    │                Inventory Posting Setup (5813) ◄──── "Inventory Posting Group" × Location Code
                    │
  ┌─────────────────┴──── DOCUMENTOS BORRADOR (mutables) ────────────────────┐
  │  Sales Header (36) 1──N Sales Line (37)                                  │
  │     PK (Document Type, No.)     PK (Document Type, Document No., Line No.)│
  └──────────────────────────────┬───────────────────────────────────────────┘
                                 │  POST (codeunit 80 Sales-Post)
                                 ▼
  ┌───────────────── DOCUMENTOS POSTEADOS (inmutables, insert-only) ─────────┐
  │  Sales Invoice Header (112) 1──N Sales Invoice Line (113)                │
  │     PK (No.)                        PK (Document No., Line No.)          │
  └───┬──────────────────┬──────────────────────────┬───────────────────────-┘
      │                  │                          │
      ▼                  ▼                          ▼
  G/L Entry (17)   Cust. Ledger Entry (21)   Item Ledger Entry (32)  ── cantidad
      │             1──N                          1──N
      │            Detailed Cust. Ledg.       Value Entry (5802)      ── valor
      │             Entry (379)                    │
      │                                            └── Item Application Entry (339)
      ▼                                                 (grafo inbound ⇄ outbound)
  G/L Register (45)                              Item Register (46)
```

Los tres ejes del diseño de BC:

1. **Separación borrador / posteado.** Un documento en curso vive en tablas mutables con `Document Type` como
   discriminador; al postear se **copia** a tablas posteadas sin `Document Type`, de solo inserción, y el borrador se borra.
2. **Separación cantidad / valor.** El movimiento físico (`Item Ledger Entry`) y su valoración (`Value Entry`)
   son tablas distintas con cardinalidad 1:N, porque el valor se corrige en el tiempo y la cantidad no.
3. **Resolución de cuentas por matriz de posting groups.** Ninguna cuenta contable se escribe en el documento:
   se *deriva* del cruce (grupo del socio de negocio × grupo del producto) en tiempo de posteo.

---

## 1. Item / Producto — tabla `Item` (27)

Namespace `Microsoft.Inventory.Item`. `DataCaptionFields 1,3` (`No.`, `Description`).

### 1.1 Identidad y clasificación

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"No."` | Code[20] | PK. Se asigna desde una No. Series (`Inventory Setup."Item Nos."`). |
| `"No. 2"` | Code[20] | Número alterno interno. |
| `Description` | Text[100] | Descripción principal. |
| `"Search Description"` | Code[100] | Descripción normalizada en mayúsculas para búsqueda. |
| `"Description 2"` | Text[50] | Línea adicional. |
| `Type` | Enum `"Item Type"` | `Inventory` / `Service` / `Non-Inventory`. **Solo `Inventory` genera Item Ledger Entries valorizados de stock.** |
| `"Item Category Code"` | Code[20] | Categoría jerárquica; arrastra atributos de ítem. Clasificación **comercial**, sin efecto contable. |
| `Blocked` | Boolean | Impide postear transacciones con el ítem. |
| `"Last Date Modified"` | Date | Auditoría ligera. |

### 1.2 Unidades de medida

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Base Unit of Measure"` | Code[10] | **Unidad base.** Todo el inventario se almacena internamente en esta unidad. Es la base de conversión. |
| `"Sales Unit of Measure"` | Code[10] | UoM por defecto al vender. |
| `"Purch. Unit of Measure"` | Code[10] | UoM por defecto al comprar. |
| `"Put-away Unit of Measure Code"` | Code[10] | UoM de almacenaje. |
| `"Rounding Precision"` | Decimal | Precisión de redondeo de cantidades. |

La conversión vive en una tabla hija:

**`Item Unit of Measure` (5404)** — namespace `Microsoft.Inventory.Item`

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Item No."` | Code[20] | PK parte 1 → `Item."No."`. |
| `Code` | Code[10] | PK parte 2 → `Unit of Measure.Code`. |
| `"Qty. per Unit of Measure"` | Decimal | **Factor de conversión: cuántas unidades base contiene una unidad de este código.** |
| `"Qty. Rounding Precision"` | Decimal | Redondeo específico de esta UoM. |
| `Length` / `Width` / `Height` / `Cubage` / `Weight` | Decimal | Dimensiones de una unidad en esta UoM. |
| `"Coupled to Dataverse"` | Boolean | Integración D365 Sales. |

**Regla de oro del diseño.** La `"Base Unit of Measure"` del ítem **tiene que existir** como fila en `Item Unit of Measure`
con `"Qty. per Unit of Measure" = 1`. Cualquier documento que use otra UoM guarda *ambos*: el código
(`"Unit of Measure Code"`) **y** el factor congelado en el momento (`"Qty. per Unit of Measure"`), y además la
cantidad convertida a base (`"Quantity (Base)"` en las líneas, `Quantity` ya en base en `Item Ledger Entry`).

```
Sales Line."Quantity (Base)" = Sales Line.Quantity × Sales Line."Qty. per Unit of Measure"
Item Ledger Entry.Quantity   = cantidad SIEMPRE en unidad base (con signo)
```

Congelar el factor en la línea/entry —en vez de re-leerlo de `Item Unit of Measure` al consultar— es lo que
permite cambiar el factor de una UoM en el futuro sin reescribir la historia. **Esto hay que replicarlo:** un
`JOIN` en tiempo de lectura al factor actual corrompería los movimientos históricos.

### 1.3 Costeo

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Costing Method"` | Enum `"Costing Method"` | `FIFO` / `LIFO` / `Specific` / `Average` / `Standard`. Decide **cómo se elige el costo de una salida**. |
| `"Unit Cost"` | Decimal | Costo unitario *vigente* del ítem. Con `Average` es el promedio móvil recalculado; con `Standard` es igual a `"Standard Cost"`; con FIFO/LIFO/Specific es el costo unitario medio del inventario restante. Es el valor que se copia a `Sales Line."Unit Cost"` al capturar la línea. |
| `"Standard Cost"` | Decimal | Costo estándar presupuestado. Solo se usa como costo real si `"Costing Method" = Standard`; las diferencias generan `Value Entry` de tipo `Variance`. |
| `"Last Direct Cost"` | Decimal | Último costo directo de compra registrado. Informativo / base para recalcular precios. |
| `"Indirect Cost %"` | Decimal | % de costo indirecto que se capitaliza sobre el costo directo. |
| `"Overhead Rate"` (en journal) | Decimal | Tarifa fija de gasto indirecto por unidad. |
| `"Cost is Adjusted"` | Boolean | **Bandera de estado del ajuste de costo.** `false` = hay entries de este ítem cuyo costo aún no fue propagado. |
| `"Cost is Posted to G/L"` | Boolean | `false` = hay `Value Entry` no reflejados todavía en la contabilidad. |
| `"Allow Online Adjustment"` | Boolean | Permite ajuste automático al postear. |
| `"Inventory Value Zero"` | Boolean | Excluye el ítem de la valoración (mercancía en consignación de terceros). |
| `"Unit Price"` | Decimal | Precio de venta unitario. |
| `"Price/Profit Calculation"` | Enum | Relación entre `"Unit Cost"`, `"Unit Price"` y `"Profit %"`: cuál de los tres es derivado. |
| `"Profit %"` | Decimal | Margen. |

### 1.4 Inventario (todos FlowFields — calculados, no almacenados)

| Campo AL | Tipo | Cálculo |
| --- | --- | --- |
| `Inventory` | Decimal | `SUM(Item Ledger Entry.Quantity)` filtrado por ítem (+ filtros de ubicación/variante/fecha aplicados por el consumidor). |
| `"Net Invoiced Qty."` | Decimal | `SUM(Item Ledger Entry."Invoiced Quantity")`. |
| `"Purchases (Qty.)"`, `"Sales (Qty.)"`, `"Positive Adjmt. (Qty.)"`, `"Negative Adjmt. (Qty.)"`, `"Transferred (Qty.)"` | Decimal | Misma suma, filtrada por `"Entry Type"`. |
| `"Qty. on Sales Order"`, `"Qty. on Purch. Order"`, `"Qty. in Transit"`, `"Reserved Qty. on Inventory"` … | Decimal | Sumas sobre líneas de documentos *abiertos* y sobre `Reservation Entry`. |

**Consecuencia de diseño crítica.** En BC no existe una columna `Stock` persistida. El stock es *siempre*
`SUM` del libro mayor de ítems. Eso hace imposible que el saldo se desincronice de los movimientos, y es la razón
por la que BC puede recalcular inventario a cualquier fecha pasada (`"Date Filter"`). El precio es que toda
consulta de stock es una agregación; BC lo mitiga con índices `SIFT`/SumIndexFields (ver el campo
`"SIFT Bucket No."` en `Item Ledger Entry`, que existe únicamente para reducir contención de concurrencia en
esos índices agregados).

### 1.5 Los tres posting groups del ítem — qué hace cada uno

Este es el punto más confundido del modelo. El `Item` lleva **tres** códigos de grupo que **no** son alternativas:
cada uno alimenta una matriz de configuración distinta y resuelve una cuenta contable distinta.

| Campo en `Item` | Tipo | Se cruza con | Tabla de setup | Qué cuenta resuelve |
| --- | --- | --- | --- | --- |
| `"Gen. Prod. Posting Group"` | Code[20] | `"Gen. Bus. Posting Group"` del **cliente/proveedor** | `General Posting Setup` (252) | Cuentas de **resultado**: Ventas, Costo de ventas (COGS), Compras, descuentos de línea y factura. |
| `"VAT Prod. Posting Group"` | Code[20] | `"VAT Bus. Posting Group"` del **cliente/proveedor** | `VAT Posting Setup` (325) | **% de IVA**, tipo de cálculo, cuenta de IVA por pagar / IVA soportado. |
| `"Inventory Posting Group"` | Code[20] | `"Location Code"` | `Inventory Posting Setup` (5813) | Cuenta de **balance**: Inventario (y sus cuentas interinas/WIP/variaciones). |

Leído como una frase:

- **`Gen. Prod. Posting Group`** responde *"¿a qué cuenta de ingreso/gasto va el importe de esta línea, dado con
  qué tipo de socio de negocio estoy operando?"*. Es bidimensional (producto × socio) porque una venta del mismo
  ítem a un cliente nacional y a uno de exportación debe ir a cuentas de ingreso distintas.
- **`VAT Prod. Posting Group`** responde *"¿qué tasa de impuesto aplica y a qué cuenta de impuesto va?"*. También
  bidimensional: el mismo ítem lleva 18 % a cliente local y 0 % a cliente exento/exportador.
- **`Inventory Posting Group`** responde *"¿en qué cuenta de activo está valorizada la existencia física de este
  ítem?"*. Se cruza con la **ubicación**, no con el cliente, porque el inventario es un activo que vive en un
  almacén; el cliente es irrelevante para el balance de existencias.

Corolario: al postear **una sola línea de factura de venta de un ítem**, los tres se usan a la vez —
`Gen. Prod.` da la cuenta de Ventas y la de COGS, `VAT Prod.` da el IVA, e `Inventory Posting Group` da la cuenta
de Inventario que se acredita contra el COGS. Ver §9.

`Item` también guarda `"Gen. Prod. Posting Group Id"`, `"Inventory Posting Group Id"`, `"Item Category Id"`,
`"Unit of Measure Id"` (Guid): claves surrogate añadidas para las APIs OData, en paralelo a los `Code[20]`.

---

## 2. Inventario / movimientos — `Item Ledger Entry` (32) y `Value Entry` (5802)

Namespace de ambas: `Microsoft.Inventory.Ledger`.

### 2.1 Por qué son dos tablas

La cantidad de una transacción se conoce y es definitiva en el instante del posteo. **El valor no.**
Una salida de mercancía puede ocurrir hoy y su costo real puede conocerse semanas después (cuando llega la
factura del proveedor de la entrada con la que se aplicó, cuando corre el ajuste de costo, cuando se revalúa el
inventario, cuando llega un item charge de flete). Si cantidad y valor vivieran en la misma fila, cada corrección
de costo obligaría a **mutar** un movimiento histórico.

BC resuelve esto con cardinalidad **1 `Item Ledger Entry` : N `Value Entry`**:

```
Item Ledger Entry 1042  (Entry Type = Sale, Quantity = -10)   ← INMUTABLE en cantidad
   ├─ Value Entry 9001  Entry Type=Direct Cost, Expected Cost=true,  Cost Amount (Expected) = -1000
   ├─ Value Entry 9002  Entry Type=Direct Cost, Expected Cost=false, Cost Amount (Actual)   = -1000
   ├─ Value Entry 9105  Entry Type=Direct Cost, Adjustment=true,     Cost Amount (Actual)   =   -35   (ajuste de costo)
   └─ Value Entry 9250  Entry Type=Revaluation, Adjustment=false,    Cost Amount (Actual)   =    12
```

`Item Ledger Entry.Quantity` nunca cambia; el costo total real de ese movimiento es la **suma** de sus
`Value Entry`. De hecho, `Item Ledger Entry."Cost Amount (Actual)"` y `"Cost Amount (Expected)"` son FlowFields
que suman los `Value Entry` hijos — no columnas propias.

Motivos por los que aparece más de un `Value Entry` sobre el mismo movimiento: costo esperado (recepción/envío
sin facturar) y luego costo real (facturación), ajuste de costo, revaluación, item charge (fletes/aranceles
asignados a posteriori), variación contra costo estándar, redondeo.

### 2.2 `Item Ledger Entry` (32) — la cantidad

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Entry No."` | Integer | PK secuencial global (no por ítem). |
| `"Item No."` | Code[20] | → `Item."No."`. |
| `"Posting Date"` | Date | Fecha contable del movimiento. |
| `"Entry Type"` | Enum `"Item Ledger Entry Type"` | Clasificación del movimiento (ver 2.3). |
| `Quantity` | Decimal | **Cantidad en unidad base, con signo.** Positiva = entrada, negativa = salida. |
| `"Remaining Quantity"` | Decimal | Cantidad de este movimiento que **aún no ha sido consumida/aplicada** por un movimiento contrario. Solo relevante en entries positivas: es el stock "vivo" de ese lote de costo. |
| `"Invoiced Quantity"` | Decimal | Parte de `Quantity` que ya está facturada (y por tanto con costo real, no esperado). |
| `Open` | Boolean | `true` mientras `"Remaining Quantity" <> 0`. **Es el equivalente de "capa de costo abierta".** |
| `Positive` | Boolean | `Quantity > 0`. Redundante pero indexable — evita filtrar por signo. |
| `"Applies-to Entry"` | Integer | Aplicación **fija** solicitada por el usuario: fuerza que esta salida consuma esa entrada concreta (costeo `Specific`, devoluciones exactas). |
| `"Applied Entry to Adjust"` | Boolean | Hay entries aplicadas cuyo costo hay que reajustar. |
| `"Completely Invoiced"` | Boolean | Todo el movimiento está facturado. Solo estas entries se pueden revaluar. |
| `"Location Code"` | Code[10] | → `Location.Code`. Vacío = ubicación no especificada. |
| `"Variant Code"` | Code[10] | Variante del ítem. |
| `"Unit of Measure Code"` | Code[10] | UoM del documento origen (informativa). |
| `"Qty. per Unit of Measure"` | Decimal | Factor congelado usado para llegar a `Quantity` en base. |
| `"Document Type"` | Enum `"Item Ledger Document Type"` | Tipo de documento posteado que lo originó. |
| `"Document No."` | Code[20] | Número del documento posteado. |
| `"Document Line No."` | Integer | Línea del documento posteado. |
| `"Source Type"` | Enum `"Analysis Source Type"` | `Customer` / `Vendor` / `Item` — de qué clase de socio proviene. |
| `"Source No."` | Code[20] | Cliente/proveedor concreto. |
| `"Order Type"` / `"Order No."` / `"Order Line No."` | Enum / Code[20] / Integer | Orden (producción, ensamble, transferencia, servicio) que lo generó. |
| `"Item Register No."` | Integer | → `Item Register`, el "asiento" de inventario que lo agrupa. |
| `"Serial No."` / `"Lot No."` / `"Package No."` | Code[50] | Trazabilidad. |
| `"Expiration Date"` / `"Warranty Date"` | Date | Trazabilidad. |
| `"Cost Amount (Actual)"` | Decimal | **FlowField** = `SUM(Value Entry."Cost Amount (Actual)")`. |
| `"Cost Amount (Expected)"` | Decimal | **FlowField** = `SUM(Value Entry."Cost Amount (Expected)")`. |
| `"Cost Amount (Non-Invtbl.)"` | Decimal | FlowField: costos no inventariables. |
| `"Sales Amount (Actual)"` / `"Sales Amount (Expected)"` | Decimal | FlowField desde `Value Entry`. Ingreso asociado al movimiento. |
| `"Purchase Amount (Actual)"` / `"(Expected)"` | Decimal | FlowField. |
| `"Shipped Qty. Not Returned"` | Decimal | Base para devoluciones con costo exacto. |
| `"Reserved Quantity"` | Decimal | FlowField sobre `Reservation Entry`. |
| `Correction` | Boolean | Marca de corrección/anulación. |
| `"Dimension Set ID"` | Integer | → combinación de dimensiones analíticas. |
| `"SIFT Bucket No."` | Integer | Partición interna de los índices agregados, para concurrencia. |

### 2.3 `"Entry Type"` — enum `Item Ledger Entry Type`

| Valor | Signo típico de `Quantity` | Origen |
| --- | --- | --- |
| `Purchase` | + | Posteo de recepción/factura de compra. |
| `Sale` | − | Posteo de envío/factura de venta. |
| `"Positive Adjmt."` | + | Item Journal: entrada por ajuste (inventario inicial, sobrante de conteo físico). |
| `"Negative Adjmt."` | − | Item Journal: salida por ajuste (merma, faltante de conteo físico). |
| `Transfer` | + y − | Traslado entre ubicaciones y reclasificación. Genera **un par** de entries (− origen, + destino). |
| `Consumption` | − | Consumo de componentes en una orden de producción. |
| `Output` | + | Producción terminada que ingresa a inventario. |
| `"Assembly Consumption"` / `"Assembly Output"` | − / + | Equivalentes en el módulo de ensamble. |

`Entry Type` es un **enum fijo del producto**, no una tabla de configuración. Esto es exactamente lo contrario
del enfoque de SAP (movement types configurables, §10) y es una de las limitaciones de diseño más citadas de BC.

### 2.4 `Value Entry` (5802) — el valor

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Entry No."` | Integer | PK secuencial. |
| `"Item Ledger Entry No."` | Integer | **FK al movimiento físico.** Puede ser 0 en entries ligadas solo a capacidad (`"Capacity Ledger Entry No."`). |
| `"Item No."` | Code[20] | Desnormalizado desde el ILE, para poder filtrar/sumar sin join. |
| `"Posting Date"` | Date | Fecha contable. |
| `"Valuation Date"` | Date | **Fecha a partir de la cual esta entry entra en el cálculo de costo promedio.** No siempre igual a `"Posting Date"`. |
| `"Item Ledger Entry Type"` | Enum `"Item Ledger Entry Type"` | Copia del tipo del ILE padre (permite segmentar valor por tipo de movimiento sin join). |
| `"Entry Type"` | Enum `"Cost Entry Type"` | **Qué clase de valor es esta fila:** `"Direct Cost"` / `Revaluation` / `Rounding` / `"Indirect Cost"` / `Variance`. |
| `"Variance Type"` | Enum `"Cost Variance Type"` | Si es `Variance`: material / capacidad / subcontratación / gasto indirecto. |
| `"Valued Quantity"` | Decimal | Cantidad a la que corresponde este importe. |
| `"Item Ledger Entry Quantity"` | Decimal | Cantidad total del ILE padre (para prorratear). |
| `"Invoiced Quantity"` | Decimal | Cantidad facturada por este posteo. |
| `"Cost per Unit"` | Decimal | Costo por unidad base. |
| `"Cost Amount (Actual)"` | Decimal | **Importe de costo real.** Columna almacenada (aquí sí). |
| `"Cost Amount (Expected)"` | Decimal | Importe de costo esperado (aún no facturado). |
| `"Cost Amount (Non-Invtbl.)"` | Decimal | Costo no inventariable. |
| `"Sales Amount (Actual)"` / `"(Expected)"` | Decimal | Ingreso reconocido en el movimiento. |
| `"Purchase Amount (Actual)"` / `"(Expected)"` | Decimal | Importe de compra. |
| `"Discount Amount"` | Decimal | Descuento del documento origen. |
| `"Expected Cost"` | Boolean | `true` = esta fila es costo esperado, no real. |
| `Adjustment` | Boolean | **`true` = esta fila la creó el ajuste de costo, no el posteo original.** |
| `"Valued By Average Cost"` | Boolean | `true` = el costo de esta salida se calculó con el promedio del período, no con una aplicación directa. |
| `"Average Cost Exception"` | Boolean | El promedio de este ítem/período requiere recálculo especial. |
| `"Partial Revaluation"` | Boolean | Revaluación parcial de la cantidad. |
| `Inventoriable` | Boolean | El valor afecta el activo de inventario. |
| `"Cost Posted to G/L"` | Decimal | Importe ya reflejado en contabilidad. Diferencia contra `"Cost Amount (Actual)"` = pendiente de `Post Inventory Cost to G/L`. |
| `"Expected Cost Posted to G/L"` | Decimal | Ídem para costo esperado (cuentas interinas). |
| `"Inventory Posting Group"` | Code[20] | **Congelado del ítem al momento del posteo** → resuelve la cuenta de inventario. |
| `"Gen. Bus. Posting Group"` | Code[20] | Congelado del cliente/proveedor → resuelve con el siguiente la cuenta de resultado. |
| `"Gen. Prod. Posting Group"` | Code[20] | Congelado del ítem. |
| `"Source Posting Group"` | Code[20] | Customer/Vendor Posting Group del socio. |
| `"Source Type"` / `"Source No."` | Enum / Code[20] | Socio de negocio. |
| `"Document Type"` / `"Document No."` / `"Document Line No."` | Enum / Code[20] / Integer | Documento posteado origen. |
| `"Item Charge No."` | Code[20] | Si el valor viene de un cargo de ítem (flete, arancel). |
| `"Location Code"` / `"Variant Code"` | Code[10] | Dimensión física del valor. |
| `"Item Register No."` | Integer | Agrupador. |
| `"Applies-to Entry"` | Integer | Aplicación a otra value entry. |
| `"Capacity Ledger Entry No."` | Integer | Enlace al libro de capacidad (producción). |
| `"Job No."` / `"Job Task No."` / `"Job Ledger Entry No."` | Code[20] / Integer | Enlace a proyectos. |
| `"Cost per Unit (ACY)"`, `"Cost Amount (Actual) (ACY)"`, … | Decimal | Espejo en moneda adicional de reporte. |

Nota de permisos reveladora: la tabla declara `Permissions | TableData "Value Entry" = ri` — **solo read e
insert**. `Item Ledger Entry` declara `rimd`. Es la afirmación explícita en el código de que los `Value Entry`
son **append-only**: el costo nunca se corrige editando, solo agregando otra fila.

---

### 2.5 `Item Application Entry` (339) — el grafo de flujo de costo

Namespace `Microsoft.Inventory.Ledger`. Es la tercera tabla del trío y la menos conocida.

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Entry No."` | Integer | PK. |
| `"Item Ledger Entry No."` | Integer | El movimiento **para el que** se creó esta aplicación. |
| `"Inbound Item Entry No."` | Integer | `Item Ledger Entry` del **aumento** de inventario (la capa de costo origen). |
| `"Outbound Item Entry No."` | Integer | `Item Ledger Entry` de la **disminución**. `0` si aún no hay salida. |
| `Quantity` | Decimal | **Cantidad aplicada por esta arista.** |
| `"Posting Date"` | Date | Igual a la del `Item Ledger Entry`. |
| `"Item No."` / `"Location Code"` / `"Variant Code"` | Code | Desnormalizado. |
| `"Cost Application"` | Boolean | Ver abajo. |
| `"Latest Valuation Date"` | Date | Fecha de valoración más reciente de la cadena. |
| `"Transferred-from Entry No."` | Integer | Si la aplicación viene de una transferencia, el `Item Ledger Entry` del aumento original. |
| `"Outbound Entry is Updated"` | Boolean | El costo de la salida relacionada ya se actualizó; el ajuste no volverá a tocarla. |
| `"Output Completely Invd. Date"` | Date | Producción completamente facturada. |
| `"Item Register No."` | Integer | Agrupador. |
| `"Creation Date"` / `"Created By User"` / `"Last Modified Date"` / `"Last Modified By User"` | DateTime / Code[50] | Auditoría propia de la tabla. |

**Cómo el trío forma el grafo.** Es un multigrafo bipartito entrada→salida con pesos:

```
  Compra 10 uds. (ILE 1, Quantity +10)
      → Item Application Entry (Item Ledger Entry No.=1, Inbound=1, Outbound=0, Quantity=10)
        ("media arista": entrada sin salida aún; ILE 1 queda Open, Remaining Quantity=10)

  Venta 5 uds.   (ILE 2, Quantity −5)
      → Item Application Entry (Item Ledger Entry No.=2, Inbound=1, Outbound=2, Quantity=−5)
        (arista cerrada; ILE 1 pasa a Remaining Quantity=5, sigue Open)

  Nota de crédito de venta 5 uds. (ILE 3)
      → cadena multi-salto: el costo de ILE 1 se reenvía a ILE 2 y de ahí a ILE 3
```

`Item Ledger Entry."Remaining Quantity"` es **la capacidad no consumida del nodo de entrada**: *"el campo
Remaining Quantity muestra la cantidad que aún no se ha aplicado. Si la cantidad restante es mayor que 0, la
casilla **Open** está marcada."* El ajuste de costo **recorre estas aristas**: *"El costo se reenvía según las
cadenas de costo registradas en la tabla **Item Application Entry**."*

**Regla de enlace por método de costeo:** *"Para ítems que usan los métodos FIFO, Standard y Average, el enlace
se basa en el principio primero-en-entrar-primero-en-salir… Para ítems que usan LIFO, el enlace se basa en el
principio último-en-entrar-primero-en-salir."* Es decir, **`Average` también recibe una aplicación de
cantidad con forma FIFO**, pero su *valor* no viene de la arista, sino del promedio del período (§2.7).

**`"Cost Application"` — el matiz.** BC hace **dos tipos** de aplicación:

| Tipo | Cuándo |
| --- | --- |
| *Quantity application* | *"Se crea para todas las transacciones de inventario."* |
| *Cost application* | *"Se crea para entries de entrada junto con una aplicación de cantidad, como resultado de la interacción del usuario en procesos especiales."* |

Y la tabla de dirección de la documentación:

| Sentido del entry | `Appl.-to Item Entry` | `Appl.-from Item Entry` |
| --- | --- | --- |
| **Salida** | *"La salida **jala** el costo de la entrada abierta."* → quantity application | *"No soportado."* |
| **Entrada** | *"La entrada **empuja** el costo sobre la salida abierta. La entrada es la fuente de costo."* → quantity application | *"La entrada jala el costo de la salida… la transacción de entrada se trata como una devolución de venta. Por tanto la salida aplicada **permanece abierta**. La entrada **NO** es la fuente de costo."* → **cost application** |

Aviso literal de la documentación: *"Una devolución de venta **NO** se considera fuente de costo cuando está
aplicada fija. La entry de venta permanece abierta hasta que se postea la fuente real."*
Traducido: `"Cost Application" = true` marca una arista que **transporta valor pero no convierte su nodo de
entrada en fuente de costo**. Es el mecanismo con el que una devolución devuelve el costo original al
inventario sin inventar una capa de costo nueva.

### 2.6 Los cinco métodos de costeo

`Item."Costing Method"` — enum `Costing Method` (ID 28), valores en orden: `FIFO`, `LIFO`, `Specific`,
`Average`, `Standard`.

| Método | Cómo se elige el costo de la salida | Mecanismo de flujo |
| --- | --- | --- |
| **FIFO** | *"El costo unitario del ítem es el valor real de cualquier recepción del ítem, seleccionada por la regla FIFO."* Las disminuciones *"se valoran tomando el valor del primer aumento de inventario."* | `Item Application Entry`. *"La aplicación lleva el control de la cantidad restante. El ajuste reenvía costos según la aplicación de cantidad."* |
| **LIFO** | Valor real de la recepción elegida por regla LIFO; las salidas toman *"el valor del último aumento de inventario."* | `Item Application Entry`. |
| **Average** | *"Las disminuciones de inventario se valoran calculando un promedio ponderado del inventario restante en el último día del período de costo promedio en el que se posteó la disminución."* | **Promedio del período**, no cadena de aplicación. *"El costo se calcula y reenvía según la **fecha de valoración**."* La detección va por la tabla `Avg. Cost Adjmt. Entry Point` y *"el costo se reenvía aplicando los costos a value entries con una fecha de valoración posterior."* |
| **Standard** | *"El costo unitario está preestablecido con base en un costo estimado."* Las entradas se valoran *"al costo estándar vigente del ítem"*; las salidas *"de forma similar a FIFO, excepto que la valoración se basa en un costo estándar."* La diferencia contra el real se postea como `Variance`. | `Item Application Entry` — *"la aplicación se basa en FIFO."* |
| **Specific** | *"El costo unitario del ítem es el costo exacto al que se recibió esa unidad en particular."* Las salidas *"se valoran según el aumento de inventario al que la aplicación fija las vincula."* | `Item Application Entry` con **aplicación fija**. *"Todas las aplicaciones son fijas."* Requiere trazabilidad (serie/lote) de entrada y de salida. |

Ejemplo canónico de la documentación (entran 3 uds. a 10 / 20 / 30, salen 3):

| Método | Costo de las 3 salidas |
| --- | --- |
| FIFO | −10, −20, −30 |
| LIFO | −30, −20, −10 |
| Average | −20, −20, −20 |
| Standard (15.00) | −15, −15, −15 |
| Specific (aplicación fija a 2 / 1 / 3) | −20, −10, −30 |

Dos restricciones documentadas: *"No se puede cambiar el método de costeo de un ítem si existen item ledger
entries para el ítem."* Y: *"Se puede usar trazabilidad específica sin usar el método de costeo Specific. El
costo no seguirá al número de lote, sino la suposición de costo del método seleccionado."*

### 2.7 Cómo se calcula el costo promedio (`Average`)

**La fórmula, en los cuatro pasos de la documentación oficial.** Para cada entry con costo no ajustado, el
ajuste de costo:

1. Determina el costo del ítem **al inicio del período de costo promedio**.
2. **Suma** los costos de entrada posteados durante el período (compras, devoluciones de venta, ajustes
   positivos, salidas de producción y ensamble).
3. **Resta** los costos de las transacciones de salida que se **aplicaron fijo** a recepciones del período
   (típicamente devoluciones de compra y salidas negativas).
4. **Divide** por la cantidad total de inventario al **final** del período. *"Excluye las disminuciones de
   inventario que se están valorando."*

```
                   Costo al inicio del período
                 + Σ costos de entradas del período
                 − Σ costos de salidas aplicadas fijo a recepciones del período
Costo promedio = ─────────────────────────────────────────────────────────────────
                   Cantidad total de inventario al FIN del período
                   (excluyendo las disminuciones que se están valorando)
```

*"El costo promedio calculado se aplica entonces a las disminuciones de inventario del ítem (o ítem, ubicación
y variante) con fechas de posteo dentro del período de costo promedio."*

Ejemplo numérico documentado (período mensual): *"Para obtener el costo promedio de febrero, Business Central
suma el costo promedio del ítem recibido en inventario (100.00) al costo promedio al inicio del período
(30.00). La suma (130.00) se divide entre la cantidad total en inventario (2). Este cálculo da el costo
promedio resultante del ítem en el período de febrero (**65.00**)."*

**Configuración (`Inventory Setup`, tabla 313):**

| Campo | Valores | Efecto |
| --- | --- | --- |
| `"Average Cost Calc. Type"` | **`Item`** / **`"Item, Variant, and Location"`** | Granularidad del promedio. Con la segunda opción, *"el costo promedio se calcula para cada ítem, para cada ubicación y para cada variante del ítem."* |
| `"Average Cost Period"` | `Day` / `Week` / `Month` / `Accounting Period` (una página de la doc añade `Quarter`) | Ventana del promedio. |

Restricción: *"Solo se puede usar un período de costo promedio y un tipo de cálculo de costo promedio por año
fiscal."* La página **Accounting Periods** muestra cuáles están vigentes en cada período.

> **Precisiones contra errores comunes.** La opción se escribe `"Item, Variant, and Location"` (no
> "Item & Location & Variant"). Y **`"Average Cost Period"` no incluye `Year`** en ninguna página oficial; las
> dos páginas de la documentación discrepan sobre si ofrece `Quarter`. `Year` sí pertenece a
> `"Automatic Cost Adjustment"`, que es otro campo.

**El papel de `Value Entry."Valuation Date"`.** *"El campo **Valuation Date** de la tabla **Value Entry**
determina el período de costo promedio al que pertenece una entry de disminución de inventario."* Se asigna
automáticamente por cuatro reglas:

| # | Fecha de posteo | `Valued Quantity` | ¿Revaluación? | `Valuation Date` resultante |
| --- | --- | --- | --- | --- |
| 1 | — | Positiva | No | Fecha de posteo del `Item Ledger Entry`. |
| 2 | Posterior a la última fecha de valoración de las value entries aplicadas | Negativa | No | Fecha de posteo del `Item Ledger Entry`. |
| 3 | **Anterior** a la última fecha de valoración de las value entries aplicadas | Positiva | No | **La última fecha de valoración de las value entries aplicadas.** |
| 4 | — | Negativa | Sí | Fecha de posteo de la value entry de revaluación. |

La regla 3 existe para evitar un descuadre cantidad-valor: *"Para evitar tal desajuste cantidad-valor, la fecha
de valoración se establece igual a la última fecha de valoración de las value entries aplicadas."* Advertencia
documentada: *"Como el informe **Inventory Valuation** se basa en la fecha de posteo, el informe reflejará
cualquier desajuste cantidad-valor."*

⚠️ **Colisión de nombres que la propia documentación señala:** existe otro campo `Valuation Date` en la tabla
`Avg. Cost Adjmt. Entry Point`, que *"especifica el último día del período de costo promedio en el que se
posteó la transacción"*. No son lo mismo. Con período mensual, *"la fecha de valoración se fija al último día
del período de costo promedio"*.

**`Value Entry."Valued By Average Cost"`** (Boolean) — *"Especifica si el costo ajustado de la disminución de
inventario se calcula por el costo promedio del ítem en la fecha de valoración."* Es la marca de "esta salida
la valoró el promedio" vs. "la valoró una aplicación fija". Documentación: *"Si crea una aplicación fija para
una disminución de inventario de un ítem que usa el método Average, la disminución **no** recibirá el costo
promedio del ítem como de costumbre, sino el costo del aumento de inventario que especificó. Esa disminución
**deja de formar parte del cálculo del costo promedio**."*

El recálculo del promedio *"ocurre automáticamente al ejecutar el batch job **Adjust Cost - Item Entries**,
manual o automáticamente"*, y se dispara al retro-fechar una entrada o salida antes de una salida existente,
o al cambiar el período/tipo de cálculo.

### 2.8 Cómo funciona el ajuste de costo (`Adjust Cost - Item Entries`)

**Propósito literal:** *"El propósito principal del ajuste de costo es **reenviar los cambios de costo desde
las fuentes de costo hacia los receptores de costo**, de acuerdo con el método de costeo del ítem, para
proporcionar una valoración de inventario correcta."* Secundariamente, facturar órdenes de producción
terminadas: convierte value entries de esperado a real, limpia WIP, postea la variación y actualiza el costo
unitario en la ficha del ítem.

**La arquitectura: detectar ≠ calcular.** *"La tarea de detectar si debe ocurrir un ajuste de costo la realiza
principalmente la rutina **Item Jnl.-Post Line**, mientras que la tarea de calcular y generar las entries de
ajuste la realiza el batch job **Adjust Cost – Item Entries**."* Es decir: el posteo es rápido y solo **marca**;
el cálculo pesado es diferido y por lotes. Este desacoplamiento es la razón por la que BC puede postear
facturas sin resolver el costo real en línea.

**Los tres mecanismos de detección/reenvío:**

| Mecanismo | Métodos que lo usan | Cómo detecta | Cómo reenvía el costo |
| --- | --- | --- | --- |
| **Item Application Entry** | FIFO, LIFO, Standard, Specific y escenarios de aplicación fija | *"Marcando los item ledger entries fuente como `Applied Entry to Adjust` cada vez que se postea un item ledger entry o un value entry."* | *"Según las cadenas de costo registradas en la tabla Item Application Entry."* |
| **Avg. Cost Adjmt. Entry Point** | `Average` | *"Marcando un registro en la tabla `Avg. Cost Adjmt. Entry Point` cada vez que se postea un value entry."* | *"Aplicando los costos a value entries con una fecha de valoración posterior."* |
| **Order Level** | Producción / ensamble | *"Marcando la orden cada vez que se postea un material/recurso como consumido/usado."* | *"Del material/recurso a las entries de salida asociadas a la misma orden."* |

**Qué escribe.** *"Business Central calcula el valor de la transacción de entrada y reenvía ese costo a
cualquier transacción de salida, como ventas o consumos, que se haya aplicado a la transacción de entrada. El
ajuste de costo **crea value entries** que contienen los importes de ajuste y los importes que compensan el
redondeo."* Y: *"El ajuste procesa solo value entries no ajustadas… crea **nuevas** value entries de ajuste.
Las value entries de ajuste se basan en la información de las value entries originales pero contienen el
importe del ajuste."*

Ejemplo documentado (un item charge de 2.00 posteado el 02-10 sobre una compra ya vendida el 01-15):

| Nueva VE | `Item Ledger Entry Type` | Importe | `Posting Date` | `Adjustment` |
| --- | --- | --- | --- | --- |
| 3 | `Purchase` | +2.00 | 02-10 | No |
| 4 | `Sale` | −2.00 | **01-15** | **Sí** |

**Banderas que lo gobiernan** (¡con una corrección importante!):

| Bandera | Dónde vive | Función |
| --- | --- | --- |
| `"Applied Entry to Adjust"` | **`Item Ledger Entry`** (32), Boolean | *"Especifica si hay una o más entries aplicadas que necesitan ser ajustadas."* Setter: `SetAppliedEntryToAdjust()`. **Es la bandera a nivel de movimiento.** |
| `"Cost is Adjusted"` | **`Item`** (27), Boolean — **NO existe en `Item Ledger Entry`** | *"Especifica si el costo unitario del ítem se ha ajustado, automática o manualmente."* También aparece como columna en `Avg. Cost Adjmt. Entry Point`. |
| `Adjustment` | `Value Entry` (5802), Boolean | *"Especifica si esta entry ha sido ajustada de costo."* Marca las filas **producidas** por el ajuste. |
| `"Completely Invoiced"` | `Item Ledger Entry` (32), Boolean | *"Solo las entries completamente facturadas pueden ser revaluadas."* |
| `"Cost is Posted to G/L"` | `Item` (27), Boolean | Todo el costo del ítem ya está reflejado en contabilidad. |

**Fecha de posteo de las entries de ajuste.** *"Las nuevas value entries de ajuste y de redondeo tienen la
fecha de posteo de la factura relacionada."* Excepciones: si ese período contable/de inventario está cerrado, o
si la fecha es anterior a `Allow Posting From`, *"el batch job asigna como fecha de posteo el primer día del
siguiente período abierto."*

**`Inventory Setup."Automatic Cost Adjustment"`** — enum `Automatic Cost Adjustment Type`:
`Never` / `Day` / `Week` / `Month` / `Quarter` / `Year` / `Always`. *"Este campo permite seleccionar cuánto
tiempo atrás desde la fecha de trabajo actual desea que se realice el ajuste automático de costo."*
Con `Always`, *"los costos siempre se ajustan al postear, independientemente de la fecha de posteo."*
El compromiso, literal: *"Períodos más cortos, como **Day** o **Week**, afectan menos el rendimiento del
sistema… pero también significa que los costos unitarios pueden ser menos exactos."*

**Orden obligatorio de los procesos de cierre:** *"Los costos de inventario deben ajustarse antes de que las
value entries relacionadas puedan conciliarse con el mayor general."* Y del propio batch job:
*"Antes de usar este batch job [**Post Inventory Cost to G/L**], debe ejecutar el batch job
**Adjust Cost - Item Entries**."*

```
   postear documento  →  Item Ledger Entry + Value Entry (costo posiblemente provisional)
          │
          ▼
   Adjust Cost - Item Entries   →  nuevas Value Entry con Adjustment = true
          │                        (Entry Type: Direct Cost / Rounding / Variance)
          ▼
   Post Inventory Cost to G/L   →  G/L Entry (Inventario ⇄ COGS)
                                   + G/L - Item Ledger Relation (5823)
```

> **Precisión:** el ajuste de costo **no** escribe value entries de tipo `Revaluation`. Lo que la
> documentación respalda como salida del ajuste es `"Direct Cost"`, `Rounding` (*"los residuales se calculan
> para todos los métodos de costeo al ejecutar Adjust Cost - Item Entries"*) y `Variance` (solo costo
> estándar). La revaluación es una **acción separada del usuario** (diario de revaluación) cuyo resultado el
> ajuste después reenvía. Tampoco existe un `Entry Type` llamado "Cost Adjustment": los ajustes se identifican
> por el booleano `Adjustment`.

**Componentes de costo documentados** (los tipos de valor que puede llevar un ítem): *Direct cost* (incluye
fletes y seguros vía item charges), *Indirect cost*, *Direct cost - Non Inventory*, *Variance*
(`Cost Variance Type`, enum 106: `" "`, `Purchase`, `Material`, `Capacity`, `"Capacity Overhead"`,
`"Manufacturing Overhead"`, `Subcontracted`, `"Material - Non Inventory"`), *Revaluation*
(*"una depreciación o apreciación del valor de inventario actual"*) y *Rounding*
(*"residuales causados por la forma en que se calcula la valoración de las disminuciones de inventario"*).

---

## 3. Almacenes — `Location` (14) y `Bin` (7354)

Namespace `Microsoft.Inventory.Location` / `Microsoft.Warehouse.Structure`.

### 3.1 `Location` (14) — nivel mínimo viable

`DataCaptionFields 1,2` (`Code`, `Name`). PK: `Code`.

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `Code` | Code[10] | **PK.** Código del almacén. |
| `Name` | Text[100] | Nombre. |
| `"Name 2"` | Text[50] | Continuación. |
| `Address` / `"Address 2"` / `City` / `"Post Code"` / `County` / `"Country/Region Code"` | Text/Code | Dirección. |
| `Contact` / `"Phone No."` / `"Fax No."` / `"E-Mail"` / `"Home Page"` | Text | Contacto. |
| `"Use As In-Transit"` | Boolean | Ubicación virtual de tránsito, obligatoria para transferencias en dos pasos (envío → recepción). |
| `"Base Calendar Code"` | Code[10] | Calendario laboral para cálculo de fechas. |
| `"Outbound Whse. Handling Time"` / `"Inbound Whse. Handling Time"` | DateFormula | Tiempos de manipulación que desplazan fechas prometidas. |
| `"Require Receive"` / `"Require Shipment"` / `"Require Put-away"` / `"Require Pick"` | Boolean | **Los cuatro interruptores de complejidad de almacén.** Con los cuatro en `false`, la ubicación es un simple contenedor lógico de stock: postear una venta descarga el stock directamente. |
| `"Bin Mandatory"` | Boolean | Exige `Bin Code` en toda transacción de la ubicación. |
| `"Directed Put-away and Pick"` | Boolean | Activa WMS avanzado (zonas, plantillas, clases). |
| `"Default Bin Code"` / `"Receipt Bin Code"` / `"Shipment Bin Code"` / `"Adjustment Bin Code"` / `"Open Shop Floor Bin Code"` / `"Cross-Dock Bin Code"` / `"To-Assembly Bin Code"` / `"From-Assembly Bin Code"` / `"To-Job Bin Code"` | Code[20] | Bins por defecto por proceso. |
| `"Pick According to FEFO"` | Boolean | Selección por fecha de caducidad. |
| `"Put-away Template Code"` / `"Bin Capacity Policy"` / `"Pick Bin Policy"` / `"Put-away Bin Policy"` / `"Check Whse. Class"` / `"Allow Breakbulk"` / `"Special Equipment"` | varios | Reglas WMS avanzadas. |
| `"Use ADCS"` | Boolean | Captura automática de datos (terminales). |

### 3.2 Relación con `Item Ledger Entry`

```
Location.Code  ──(1:N)──►  Item Ledger Entry."Location Code"
                        ►  Value Entry."Location Code"
                        ►  Sales Line."Location Code"  /  Item Journal Line."Location Code"

Location.Code × Item."Inventory Posting Group"  ──►  Inventory Posting Setup (5813)
```

`Item Ledger Entry."Location Code"` es `Code[10]` y **admite vacío**: el código vacío `''` es una ubicación
legítima ("sin especificar"), que es lo que ocurre en empresas de un solo almacén que nunca configuraron
`Location`. Esto significa que el stock por almacén es literalmente
`SUM(Item Ledger Entry.Quantity) GROUP BY "Item No.", "Location Code"`, sin ninguna tabla de saldos.

**Nivel mínimo viable (sin bins).** Para un ERP propio sirve perfectamente:

1. Tabla `Location` con `Code`, `Name`, dirección y los cuatro `Require *` en `false`.
2. `Location Code` en toda línea de documento y en todo movimiento de inventario.
3. `Inventory Posting Setup` con PK `(Location Code, Invt. Posting Group Code)` — esta es la razón real por la
   que `Location` no es opcional en el diseño: es media clave de la resolución de la cuenta de inventario.
   Se puede arrancar con una única fila de `Location Code = ''` si se quiere postergar el multi-almacén.

`Bin` solo aporta valor cuando hace falta ubicar físicamente dentro del almacén; duplica la lógica de
existencias en una tabla paralela (`Warehouse Entry`) que debe cuadrar con `Item Ledger Entry`. Ese doble libro
(inventario contable vs. inventario físico por bin) es la fuente más frecuente de descuadres en BC y **no se
debe implementar sin necesidad real**.

### 3.3 `Bin` (7354) — campos principales (referencia, no MVP)

| Campo AL | Tipo |
| --- | --- |
| `"Location Code"` | Code[10] (PK 1) |
| `Code` | Code[20] (PK 2) |
| `Description` | Text[100] |
| `"Zone Code"` | Code[10] |
| `"Bin Type Code"` | Code[10] |
| `"Warehouse Class Code"` | Code[10] |
| `"Block Movement"` | Enum |
| `"Bin Ranking"` | Integer |
| `"Maximum Cubage"` / `"Maximum Weight"` | Decimal |
| `Empty` / `"Adjustment Bin"` / `Dedicated` / `"Cross-Dock Bin"` | Boolean |

---


---

## 4. Diarios de inventario — `Item Journal Template` (82) / `Item Journal Batch` (233) / `Item Journal Line` (83)

Namespace `Microsoft.Inventory.Journal`. **Ojo:** la plantilla es la tabla **82**, no 232.

### 4.1 La jerarquía de tres niveles

```
Item Journal Template (82)         ← QUÉ CLASE de diario es (definición de producto)
  PK: Name
  Type: Item | Transfer | "Phys. Inventory" | Revaluation
  Source Code, Reason Code, Page ID, Test Report ID, Posting Report ID
  No. Series, Posting No. Series
     │ 1:N
     ▼
Item Journal Batch (233)           ← ESPACIO DE TRABAJO nombrado (por usuario/proceso)
  PK: (Journal Template Name, Name)
  Template Type (copia), Reason Code, No. Series, Posting No. Series,
  Recurring, No. of Lines, Item Tracking on Lines
     │ 1:N
     ▼
Item Journal Line (83)             ← BORRADOR de movimiento (mutable, se borra al postear)
  PK: (Journal Template Name, Journal Batch Name, Line No.)
     │ POST (codeunit 23 "Item Jnl.-Post Batch" → codeunit 22 "Item Jnl.-Post Line")
     ▼
Item Ledger Entry (32) + Value Entry (5802) + Item Application Entry (339)
     └─► agrupados por Item Register (46)
```

Por qué tres niveles y no uno:

- La **plantilla** es configuración de producto: define el *tipo* de diario, su página, su reporte de prueba y
  su `Source Code` (la etiqueta de procedencia que acabará en cada `Item Ledger Entry`). Las cuatro plantillas
  base (`Item Journal Template Type`, enum 83) son `Item`, `Transfer`, `"Phys. Inventory"`, `Revaluation`;
  las de manufactura (`Consumption`, `Output`, `"Prod. Order"`, `Capacity`) son **valores de extensión**, no
  del enum base — de ahí que la tabla exponga `GetConsumptionTemplateType()` en vez de ordinales fijos.
- El **batch** es un *espacio de trabajo con nombre*: permite que varios usuarios (o varios procesos) tengan
  borradores simultáneos sin pisarse, y que cada uno tenga su propia serie de numeración.
- La **línea** es el borrador del movimiento.

### 4.2 `Item Journal Line` (83) — campos clave

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Journal Template Name"` / `"Journal Batch Name"` / `"Line No."` | Code[10] / Code[10] / Integer | PK. |
| `"Entry Type"` | Enum `"Item Ledger Entry Type"` | **Determina el signo y la semántica del movimiento.** |
| `"Item No."` | Code[20] | |
| `"Posting Date"` / `"Document Date"` | Date | |
| `"Document No."` | Code[20] | Nº de documento (de `No. Series` del batch). |
| `"Document Type"` / `"Document Line No."` | Enum `"Item Ledger Document Type"` / Integer | |
| `Description` | Text[100] | |
| `"Location Code"` / `"Bin Code"` / `"Variant Code"` | Code | Origen físico. |
| `"New Location Code"` / `"New Bin Code"` | Code[10] / Code[20] | **Destino** (solo en reclasificación/transferencia). |
| `"New Serial No."` / `"New Lot No."` / `"New Package No."` / `"New Item Expiration Date"` / `"New Shortcut Dimension 1 Code"` / `"New Shortcut Dimension 2 Code"` / `"New Dimension Set ID"` | varios | Resto de la familia `New *`: **el estado destino de la reclasificación.** |
| `Quantity` | Decimal | Cantidad **sin signo** — el signo lo aplica el posteo según `"Entry Type"`. |
| `"Quantity (Base)"` / `"Invoiced Quantity"` / `"Invoiced Qty. (Base)"` | Decimal | |
| `"Unit of Measure Code"` / `"Qty. per Unit of Measure"` | Code[10] / Decimal | |
| `"Unit Amount"` | Decimal | Importe unitario (precio o costo unitario capturado). |
| `"Unit Cost"` / `"Unit Cost (ACY)"` | Decimal | Costo unitario. |
| `Amount` / `"Amount (ACY)"` / `"Discount Amount"` | Decimal | |
| `"Indirect Cost %"` / `"Overhead Rate"` | Decimal | Generan un `Value Entry` extra de `"Indirect Cost"`. |
| `"Value Entry Type"` | Enum `"Cost Entry Type"` | Qué clase de `Value Entry` producirá. |
| `"Gen. Bus. Posting Group"` / `"Gen. Prod. Posting Group"` / `"Inventory Posting Group"` / `"Source Posting Group"` | Code[20] | Grupos que resolverán las cuentas. |
| `"Applies-to Entry"` | Integer | Fuerza la aplicación a un `Item Ledger Entry` positivo concreto. |
| `"Applies-from Entry"` | Integer | Fuerza el origen del costo (entradas que devuelven a una salida previa). |
| `"Applies-to Value Entry"` / `"Applies-to Rem. Quantity"` | Integer / Decimal | Revaluación parcial. |
| `"Phys. Inventory"` | Boolean | Marca de línea de conteo físico. |
| `"Qty. (Calculated)"` | Decimal | Cantidad **según el sistema** al calcular el conteo. |
| `"Qty. (Phys. Inventory)"` | Decimal | Cantidad **contada físicamente**. La diferencia es el ajuste. |
| `"Warehouse Adjustment"` | Boolean | Línea generada por *Calculate Warehouse Adjustment* (reconciliación WMS ↔ contable). |
| `"Inventory Value (Calculated)"` / `"(Revalued)"` / `"Unit Cost (Calculated)"` / `"(Revalued)"` / `"Inventory Value Per"` / `"Partial Revaluation"` | Decimal / Option / Boolean | Diario de revaluación. |
| `"Variance Type"` | Enum `"Cost Variance Type"` | |
| `"Item Shpt. Entry No."` / `"Last Item Ledger Entry No."` | Integer | Trazas del posteo. |
| `"Order Type"` / `"Order No."` / `"Order Line No."` | Enum / Code[20] / Integer | Orden origen. |
| `"Serial No."` / `"Lot No."` / `"Package No."` / `"Item Expiration Date"` | Code[50] / Date | Trazabilidad **origen**. |
| `"Source Type"` / `"Source No."` / `"Source Code"` / `"Reason Code"` | Enum / Code | Procedencia. |
| `"Posting No. Series"` | Code[20] | Serie del documento de posteo. |
| `"Recurring Method"` / `"Recurring Frequency"` / `"Expiration Date"` | Option / DateFormula / Date | Diarios recurrentes (**`"Expiration Date"` aquí es la caducidad de la línea recurrente, NO la del lote** — esa es `"Item Expiration Date"`). |
| `Correction` / `Adjustment` / `"Direct Transfer"` / `"Update Standard Cost"` | Boolean | Banderas de comportamiento. |

**No existen** en la tabla 83 (contra lo que uno esperaría): `"No. Series"` (vive en el batch/plantilla, la
línea solo tiene `"Posting No. Series"`), `"Journal Template Type"` (se lee de `Item Journal Template.Type`),
`"New Unit of Measure Code"`, y los campos de manufactura `Output Quantity` / `Scrap Quantity` / `Run Time` /
`Setup Time` / `Work Center No.` — se extrajeron a una *table extension* de la app de Manufactura.

### 4.3 El flujo journal → post → entries

`Item Jnl.-Post Batch` (**codeunit 23**, `Microsoft.Inventory.Posting`) recorre el batch, construye el número
de documento del posteo y llama por cada línea a `Item Jnl.-Post Line` (**codeunit 22**), que es quien
efectivamente escribe las entries. Al terminar, la 23 inserta la fila de `Item Register` (46) que abarca los
rangos `From/To Entry No.`, `From/To Value Entry No.`, `From/To Phys. Inventory Entry No.` y
`From/To Capacity Entry No.` — ese rango **es** la unidad de "asiento de inventario" y el ámbito de un undo.

Regla que la documentación enuncia explícitamente: *cada transacción de inventario postea **dos** entries de
tipos distintos* — la **cantidad** al `Item Ledger Entry` (más su `Item Application Entry`) y el **valor** al
`Value Entry`; y *puede existir más de un `Value Entry` por `Item Ledger Entry`*. Ejemplo documentado: una
compra de 10 uds. a 80.00 produce 1 ILE `Purchase` + VE `Direct Cost` 70.00 + VE `"Indirect Cost"` 10.00.

**Signo por `Entry Type`:**

| Dirección | Valores de `"Entry Type"` |
| --- | --- |
| Entrada (`Quantity > 0`) | `Purchase`, `"Positive Adjmt."`, `Output`, `"Assembly Output"` |
| Salida (`Quantity < 0`) | `Sale`, `"Negative Adjmt."`, `Consumption`, `"Assembly Consumption"` |
| Par con signo | `Transfer` (siempre en pareja) |

La línea del diario se captura con `Quantity` **sin signo** y el codeunit 22 le aplica el signo según
`"Entry Type"`. La única excepción documentada es el *Warehouse Item Journal*, donde el usuario firma la
cantidad a mano (positiva si sobra, negativa si falta).

### 4.4 Ajuste positivo, ajuste negativo y reclasificación

| Caso | Diario / plantilla | `"Entry Type"` | Entries que crea |
| --- | --- | --- | --- |
| **Ajuste positivo** | Item Journal (plantilla `Item`) | `"Positive Adjmt."` | **1 `Item Ledger Entry`** con `Quantity > 0`, `Open = true`, `Remaining Quantity = Quantity`. **1+ `Value Entry`** `Direct Cost` (más uno `"Indirect Cost"` si hay `"Indirect Cost %"`/`"Overhead Rate"`). **1 `Item Application Entry`** "simple": `Inbound Item Entry No.` = sí mismo, `Outbound Item Entry No. = 0`. Es una **capa de costo nueva**. |
| **Ajuste negativo** | Item Journal | `"Negative Adjmt."` | **1 `Item Ledger Entry`** con `Quantity < 0`. **1 `Value Entry`** `Direct Cost` negativo. **1+ `Item Application Entry`** que enlaza esta salida con una o varias entradas abiertas según el `"Costing Method"` del ítem (FIFO/Standard/Average → la entrada más antigua; LIFO → la más reciente). **El costo se *toma* de la entrada aplicada**, no se captura. |
| **Reclasificación** | **Item Reclass. Journal** (página 393, `PageType = Worksheet`), plantilla tipo `Transfer` | `Transfer` | **DOS `Item Ledger Entry`**: uno **negativo** en `"Location Code"`/`"Bin Code"` (origen) y uno **positivo** en `"New Location Code"`/`"New Bin Code"` (destino), más una **aplicación entre ambos**. El cambio neto de cantidad es **cero**: solo se mueven atributos. |
| **Conteo físico** | Item Journal, plantilla `"Phys. Inventory"` | `"Positive Adjmt."` / `"Negative Adjmt."` según la diferencia | Crea `Item Ledger Entry` **y** *physical inventory ledger entries* (de ahí el rango separado `From/To Phys. Inventory Entry No.` en `Item Register`). La línea lleva `"Phys. Inventory" = true`, `"Qty. (Calculated)"` (lo que dice el sistema) y `"Qty. (Phys. Inventory)"` (lo contado). |

Precisiones de la documentación oficial que conviene no perder:

- La reclasificación es la herramienta para **cambiar atributos**, no cantidades: *"Para cambiar atributos en
  item ledger entries, use un diario de reclasificación. Atributos típicos: dimensiones, códigos de campaña.
  También sirve para transferencias, reclasificando códigos de bin y de ubicación."*
- Y explícitamente **no** se debe usar el conteo físico para stock mal ubicado: *"Si el conteo revela
  diferencias causadas por ítems posteados con ubicaciones incorrectas, no registre las diferencias en el
  diario de inventario físico. Use un diario de reclasificación o una orden de transferencia."*
- La **valoración del par de transferencia no es libre**: con `Average` usa el promedio del período de costo
  promedio que contiene la fecha de valoración; con los demás métodos *"la valoración se hace rastreando el
  costo del aumento de inventario original"*. El ejemplo documentado con costo estándar muestra una
  transferencia valorada a **10.00** (el costo original) aunque el estándar vigente sea 12.00.
- En almacenes avanzados (*directed put-away and pick*), registrar el inventario físico **no** postea a los
  libros de ítem/valor: *"When you register physical inventory, you don't post to the item, physical inventory,
  or the value ledgers"*. La reconciliación se hace después con la acción *Calculate Warehouse Adjustment*, que
  es lo que marca el booleano `"Warehouse Adjustment"` en la línea.

---


---

## 5. Facturación de ventas — borrador (36/37) vs. posteado (112/113)

### 5.1 El patrón documento-borrador / documento-posteado

BC no tiene un campo "estado = posteado" en el documento. Al postear **copia** el documento a un par de tablas
distintas y **borra el borrador**:

```
    ANTES DEL POSTEO                      DESPUÉS DEL POSTEO
  ┌─────────────────────┐               ┌──────────────────────────┐
  │ Sales Header (36)   │               │ Sales Invoice Header(112) │
  │ PK (Document Type,  │  ──copiar──►  │ PK (No.)                  │
  │     No.)            │   + borrar    │ SIN "Document Type"       │
  │ Status, Ship,       │    origen     │ SIN Status/Ship/Invoice   │
  │ Invoice, Receive    │               │ + Order No.               │
  │ Amount = FlowField  │               │ + Pre-Assigned No.        │
  └─────────────────────┘               │ + Cust. Ledger Entry No.  │
  ┌─────────────────────┐               └──────────────────────────┘
  │ Sales Line (37)     │               ┌──────────────────────────┐
  │ PK (Document Type,  │  ──copiar──►  │ Sales Invoice Line (113) │
  │  Document No.,      │               │ PK (Document No., Line No.)│
  │  Line No.)          │               │ SIN "Document Type"       │
  │ Qty. to Ship/Invoice│               │ SIN Qty. to Ship/Invoice  │
  │ Outstanding Quantity│               │ SIN Currency Code         │
  └─────────────────────┘               └──────────────────────────┘
```

Las diferencias **no son cosméticas**, y cada una tiene una razón:

| Diferencia | Por qué |
| --- | --- |
| El posteado **no tiene `Document Type`** (PK = `No.` solo) | Ya no es un tipo de documento en curso, es *una factura*. Cada tipo posteado tiene su propia tabla: `Sales Invoice Header` (112), `Sales Cr.Memo Header` (114), `Sales Shipment Header` (110). El discriminador se convirtió en la identidad de la tabla. Consecuencia práctica: **una abstracción compartida "documento de venta" no puede asumir un discriminador en el lado posteado.** |
| El posteado no tiene `Status`, `Ship`, `Invoice`, `Receive`, `Shipping No.`, `Posting No.`, `Last Posting No.` | Son campos de *máquina de estados del proceso*. Un documento posteado no tiene proceso pendiente. |
| El posteado no tiene `Outstanding Quantity`, `Qty. to Ship`, `Qty. to Invoice`, `Quantity Shipped`, `Quantity Invoiced` | Cantidades de control de un pedido en curso. En una factura posteada, `Quantity` *es* lo facturado. |
| `Sales Invoice Line` **no tiene `Currency Code`** | Se obtiene de la cabecera (la tabla expone `GetCurrencyCode()`). Evita la posibilidad de una línea con moneda distinta de su cabecera. |
| El posteado añade `Order No.`, `Order No. Series`, `Pre-Assigned No.`, `Pre-Assigned No. Series` | **Provenance.** `Pre-Assigned No.` conserva el número que tenía el borrador antes de postear, para poder rastrear "esta factura salió del pedido/factura borrador X". |
| El posteado añade `Cust. Ledger Entry No.` (Integer) | Enlace directo al movimiento de cartera que generó. |
| El posteado añade `Closed`, `Remaining Amount`, `Cancelled`, `Corrective`, `Reversed` | Estado *posterior* al posteo: cobro y anulación. |
| El posteado añade `Draft Invoice SystemId` (Guid) | Enlace al borrador original vía clave surrogate. |

**El valor real del patrón** es que hace la inmutabilidad estructural en lugar de disciplinaria. No hay ninguna
ruta de código que pueda "editar una factura posteada" porque la tabla posteada no tiene los campos que el
editor de documentos necesita, y el flujo de posteo solo inserta. Corregir una factura exige emitir una nota de
crédito (otra tabla posteada), lo que deja rastro. En SQL esto se traduce en tablas posteadas con permiso de
`INSERT`/`SELECT` únicamente, más un número asignado de una serie distinta (`Posting No. Series`).

### 5.2 `Sales Header` (36) — campos clave

Namespace `Microsoft.Sales.Document`. `DataCaptionFields 3,79`. PK real: `(Document Type, No.)`.

| # | Campo AL | Tipo | Función |
| --- | --- | --- | --- |
| 1 | `"Document Type"` | Enum `"Sales Document Type"` | PK 1. `Quote`(0) / `Order`(1) / `Invoice`(2) / `"Credit Memo"`(3) / `"Blanket Order"`(4) / `"Return Order"`(5). |
| 3 | `"No."` | Code[20] | PK 2. Desde `No. Series`. |
| 2 | `"Sell-to Customer No."` | Code[20] | Cliente que **compra** (destino comercial). |
| 95 | `"Sell-to Customer Name"` | Text[100] | Nombre congelado. |
| 4 | `"Bill-to Customer No."` | Code[20] | Cliente que **paga** — puede diferir. Es el que genera el `Cust. Ledger Entry` y del que salen `Customer Posting Group`, `Gen. Bus.`/`VAT Bus. Posting Group`, `Payment Terms Code`. |
| 5 | `"Bill-to Name"` | Text[100] | |
| 10 | `"Bill-to Contact"` | Text[100] | |
| 12 | `"Ship-to Code"` | Code[10] | Dirección de entrega alterna. |
| 13 | `"Ship-to Name"` | Text[100] | |
| 11 | `"Your Reference"` | Text[35] | Referencia del cliente. |
| 115 | `"External Document No."` | Code[35] | Nº de documento externo (orden de compra del cliente). |
| 19 | `"Order Date"` | Date | Fecha del pedido. |
| 20 | `"Posting Date"` | Date | **Fecha contable.** Determina el período y la fecha de los G/L Entries. |
| 114 | `"Document Date"` | Date | **Fecha del documento** (la impresa). Base para calcular `Due Date` con los `Payment Terms`. |
| 21 | `"Shipment Date"` | Date | Fecha de envío. |
| 24 | `"Due Date"` | Date | **Vencimiento**, derivado de `Document Date` + formula de `Payment Terms Code`. |
| 166 | `"VAT Reporting Date"` | Date | Fecha fiscal del IVA, independiente de la contable. |
| 23 | `"Payment Terms Code"` | Code[10] | Condiciones de pago → calcula `Due Date`, `Pmt. Discount Date`, `Payment Discount %`. |
| 118 | `"Payment Method Code"` | Code[10] | Medio de pago. |
| 33 | `"Currency Code"` | Code[10] | Vacío = moneda local (LCY). |
| 34 | `"Currency Factor"` | Decimal | **Tipo de cambio congelado** en el documento. |
| 36 | `"Prices Including VAT"` | Boolean | Si los `Unit Price` capturados ya traen IVA. Cambia toda la aritmética de la línea. |
| 32 | `"Customer Posting Group"` | Code[20] | Congelado del cliente → cuenta de CxC. |
| 89 | `"Gen. Bus. Posting Group"` | Code[20] | Congelado → mitad de la clave de `General Posting Setup`. |
| 127 | `"VAT Bus. Posting Group"` | Code[20] | Congelado → mitad de la clave de `VAT Posting Setup`. |
| 28 | `"Location Code"` | Code[10] | Almacén por defecto para las líneas. |
| 27 | `"Shipment Method Code"` | Code[10] | Incoterm. (No existe un campo `"Shipping Method Code"`.) |
| 120 | `"Shipping Agent Code"` | Code[10] | Transportista. |
| 42 | `"Salesperson Code"` | Code[20] | Vendedor. |
| 29/30 | `"Shortcut Dimension 1 Code"` / `2` | Code[20] | Dimensiones globales. |
| 122 | `"No. Series"` | Code[20] | Serie del **borrador**. |
| 123 | `"Posting No. Series"` | Code[20] | **Serie del documento posteado.** |
| 76 | `"Posting No."` | Code[20] | Nº reservado para el posteado. |
| 124 | `"Shipping No. Series"` / 75 `"Shipping No."` | Code[20] | Ídem para el albarán. |
| 77/78 | `"Last Shipping No."` / `"Last Posting No."` | Code[20] | Último nº emitido (posteos parciales). |
| 60/61/224 | `Ship` / `Invoice` / `Receive` | Boolean | **Interruptores de la acción de posteo:** qué se postea de este pedido en esta pasada (envío, factura, o ambos). |
| 131 | `Status` | Enum `"Sales Document Status"` | `Open` / `Released` / `Pending Approval` / … |
| 73 | `Amount` | Decimal | **FlowField** = `SUM(Sales Line."Line Amount")`. |
| 74 | `"Amount Including VAT"` | Decimal | **FlowField** = `SUM(Sales Line."Amount Including VAT")`. |
| 200 | `"Invoice Discount Amount"` | Decimal | Descuento de factura. |
| 201 | `"No. of Archived Versions"` | Integer | Versionado del borrador (solo cabecera). |

### 5.3 `Sales Line` (37) — campos clave

PK real: `(Document Type, Document No., Line No.)`.

| # | Campo AL | Tipo | Función |
| --- | --- | --- | --- |
| 1/3/4 | `"Document Type"` / `"Document No."` / `"Line No."` | Enum / Code[20] / Integer | PK. `Line No.` va en saltos de 10000 para permitir inserciones. |
| 5 | `Type` | Enum `"Sales Line Type"` | `" "`(0, comentario) / `"G/L Account"`(1) / `Item`(2) / `Resource`(3) / `"Fixed Asset"`(4) / `"Charge (Item)"`(5) / `"Allocation Account"`(6). **Polimorfismo por campo**: `No.` apunta a una tabla distinta según `Type`. |
| 6 | `"No."` | Code[20] | FK polimórfica según `Type`. |
| 11 | `Description` | Text[100] | Descripción congelada (con `Type = " "` la línea es puro texto). |
| 7 | `"Location Code"` | Code[10] | Almacén de donde sale el ítem. |
| 133 | `"Bin Code"` | Code[20] | Bin (si aplica). |
| 132 | `"Variant Code"` | Code[10] | Variante. |
| 125 | `"Posting Date"` | Date | Fecha contable (copiada de la cabecera). |
| 10 | `"Shipment Date"` | Date | Fecha de envío de esta línea. |
| 16 | `Quantity` | Decimal | Cantidad en la UoM de la línea. |
| 139 | `"Quantity (Base)"` | Decimal | `Quantity × "Qty. per Unit of Measure"`. |
| 138 | `"Unit of Measure Code"` | Code[10] | → `Item Unit of Measure.Code`. |
| 15 | `"Unit of Measure"` | Text[50] | Descripción textual congelada de la UoM, **no** el código. |
| 134 | `"Qty. per Unit of Measure"` | Decimal | Factor congelado. |
| 17 | `"Outstanding Quantity"` | Decimal | Pendiente de entregar. |
| 18 | `"Qty. to Invoice"` | Decimal | **Cuánto de esta línea se factura en el próximo posteo.** Permite facturación parcial. |
| 19 | `"Qty. to Ship"` | Decimal | Ídem para envío. |
| 46/47 | `"Quantity Shipped"` / `"Quantity Invoiced"` | Decimal | Acumulados. |
| 44 | `"Qty. Shipped Not Invoiced"` | Decimal | Entregado sin facturar → base del costo esperado. |
| 22 | `"Unit Price"` | Decimal | Precio unitario. |
| 84 | `"Unit Cost"` | Decimal | Costo unitario en moneda del documento. |
| 23 | `"Unit Cost (LCY)"` | Decimal | **Costo unitario en moneda local congelado**: es el que se usa para el COGS. |
| 25/26 | `"Line Discount %"` / `"Line Discount Amount"` | Decimal | Descuento de línea. |
| 86 | `"Line Amount"` | Decimal | `Quantity × Unit Price − Line Discount Amount` (base imponible de la línea antes del descuento de factura). |
| 52 | `"Inv. Discount Amount"` | Decimal | Parte del descuento de factura prorrateada a esta línea. |
| 29 | `"Allow Invoice Disc."` | Boolean | Si la línea participa del descuento de factura. |
| 27 | `Amount` | Decimal | Base imponible final (sin IVA). |
| 28 | `"Amount Including VAT"` | Decimal | Con IVA. |
| 24 | `"VAT %"` | Decimal | **Congelado desde `VAT Posting Setup."VAT %"`.** |
| 83 | `"VAT Base Amount"` | Decimal | Base sobre la que se calculó el IVA. |
| 58 | `"VAT Calculation Type"` | Enum `"Tax Calculation Type"` | Congelado: `"Normal VAT"` / `"Reverse Charge VAT"` / `"Full VAT"` / `"Sales Tax"`. |
| 89 | `"VAT Identifier"` | Code[20] | **Agrupador de redondeo de IVA**: BC calcula el IVA sobre la suma de todas las líneas con el mismo identificador, no línea por línea. |
| 70/71 | `"VAT Bus. Posting Group"` / `"VAT Prod. Posting Group"` | Code[20] | Clave congelada de `VAT Posting Setup`. |
| 56/57 | `"Gen. Bus. Posting Group"` / `"Gen. Prod. Posting Group"` | Code[20] | Clave congelada de `General Posting Setup`. |
| 72 | `"Currency Code"` | Code[10] | Moneda (sí está en la línea borrador). |
| 48/49 | `"Shipment No."` / `"Shipment Line No."` | Code[20] / Integer | Albarán del que se factura (facturar desde envío). |
| 34 | `"Appl.-to Item Entry"` | Integer | **Aplicación fija a un `Item Ledger Entry` concreto** (costeo `Specific`, devoluciones con costo exacto). |
| 40 | `"Job No."` | Code[20] | Proyecto. |

Nota importante: la línea guarda **una copia congelada de los cuatro posting groups y del `VAT %`**. No los
re-lee en el posteo. Eso significa que cambiar el `VAT Posting Setup` hoy no altera documentos ya capturados
(lo cual es correcto) — pero también que un documento capturado antes de corregir el setup postea con el
valor viejo. La consecuencia de diseño para un ERP propio: **congelar la tasa y los grupos en la línea, y
recalcularlos explícitamente al revalidar cliente/ítem/almacén.**

### 5.4 `Sales Invoice Header` (112) / `Sales Invoice Line` (113)

Namespace `Microsoft.Sales.History`. PK: `No.` / `(Document No., Line No.)`.
Campos: los mismos que 36/37 **menos** los de proceso, **más** los de provenance y cobro
(tabla comparativa en §5.1). Campos propios de 112: `Order No.`, `Order No. Series`, `Pre-Assigned No.`,
`Pre-Assigned No. Series`, `No. Series`, `Cust. Ledger Entry No.`, `Draft Invoice SystemId`, `Closed`,
`Remaining Amount`, `Cancelled`, `Corrective`, `Reversed`. Campos propios de 113: `Order No.`, `Order Line No.`.

---


---

## 6. Movimientos de cliente / CxC — `Cust. Ledger Entry` (21) y `Detailed Cust. Ledg. Entry` (379)

Namespace `Microsoft.Sales.Receivables`.

### 6.1 El diseño de dos niveles

BC separa **la partida** (una factura, un pago) de **los hechos que le ocurren** (aplicaciones, descuentos,
diferencias de cambio, tolerancias):

```
Cust. Ledger Entry 5001   Document Type=Invoice   Document No.=FV-001   Amount=1180  Open=true
   ├─ Detailed CLE 8001  Entry Type=Initial Entry   Amount = +1180
   ├─ Detailed CLE 8055  Entry Type=Application     Amount =  -1180   Applied Cust. Ledger Entry No.=5040
   └─ Detailed CLE 8056  Entry Type=Payment Discount Amount =    -20   (si aplica)

Cust. Ledger Entry 5040   Document Type=Payment   Document No.=REC-77   Amount=-1180 Open=false
   ├─ Detailed CLE 8054  Entry Type=Initial Entry   Amount =  -1180
   └─ Detailed CLE 8057  Entry Type=Application     Amount =  +1180   Applied Cust. Ledger Entry No.=5001
```

El detalle es la razón por la que `Cust. Ledger Entry.Amount`, `"Amount (LCY)"`, `"Remaining Amount"`,
`"Remaining Amt. (LCY)"`, `"Debit Amount"`, `"Credit Amount"` son **FlowFields** (sumas sobre
`Detailed Cust. Ledg. Entry`), no columnas. El saldo pendiente de una factura no se *actualiza*: se *deriva*.
Igual que el stock en §1.4, el saldo nunca puede desincronizarse de sus hechos.

### 6.2 `Cust. Ledger Entry` (21) — campos clave

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Entry No."` | Integer | PK. |
| `"Customer No."` | Code[20] | → `Customer."No."`. Es el **Bill-to**. |
| `"Customer Name"` | Text[100] | Nombre congelado. |
| `"Sell-to Customer No."` | Code[20] | Cliente comercial (si difiere). |
| `"Document Type"` | Enum `"Gen. Journal Document Type"` | `" "`(0) / `Payment`(1) / `Invoice`(2) / `"Credit Memo"`(3) / `"Finance Charge Memo"`(4) / `Reminder`(5) / `Refund`(6). |
| `"Document No."` | Code[20] | Nº del documento posteado. |
| `"Posting Date"` / `"Document Date"` / `"Due Date"` | Date | Fecha contable / del documento / de vencimiento. |
| `"Currency Code"` | Code[10] | |
| `Amount` | Decimal | **FlowField** desde el detalle. |
| `"Amount (LCY)"` | Decimal | **FlowField.** |
| `"Remaining Amount"` | Decimal | **FlowField. Saldo pendiente en moneda del documento.** |
| `"Remaining Amt. (LCY)"` | Decimal | **FlowField.** |
| `"Original Amount"` / `"Original Amt. (LCY)"` | Decimal | Importe original almacenado. |
| `"Debit Amount"` / `"Credit Amount"` / `"(LCY)"` | Decimal | FlowFields. |
| `Open` | Boolean | **`true` mientras quede saldo.** Se limpia cuando `Remaining Amount` llega a cero. |
| `Positive` | Boolean | `true` = partida deudora (factura). |
| `"Closed by Entry No."` | Integer | **Entry que la cerró.** |
| `"Closed at Date"` | Date | Fecha de cierre. |
| `"Closed by Amount"` / `"Closed by Amount (LCY)"` / `"Closed by Currency Code"` / `"Closed by Currency Amount"` | Decimal / Code | Importe que la cerró. |
| `"Applies-to ID"` | Code[50] | **Marca de lote de aplicación.** Sella varias partidas abiertas para que un mismo pago las cierre todas. |
| `"Applies-to Doc. Type"` / `"Applies-to Doc. No."` | Enum / Code[20] | **Aplicación uno-a-uno:** el documento concreto al que se aplica. |
| `"Applies-to Ext. Doc. No."` | Code[35] | Aplicación por nº externo. |
| `"Amount to Apply"` | Decimal | Importe parcial que se quiere aplicar; vacío = el máximo. |
| `"Applying Entry"` | Boolean | Marca la partida que actúa como aplicante durante la operación. |
| `"Pmt. Discount Date"` / `"Pmt. Disc. Tolerance Date"` | Date | Fechas límite de descuento por pronto pago. |
| `"Original Pmt. Disc. Possible"` / `"Remaining Pmt. Disc. Possible"` / `"Orig. Pmt. Disc. Possible(LCY)"` / `"Pmt. Disc. Given (LCY)"` | Decimal | Descuentos por pronto pago. |
| `"Max. Payment Tolerance"` / `"Accepted Payment Tolerance"` / `"Accepted Pmt. Disc. Tolerance"` / `"Pmt. Tolerance (LCY)"` | Decimal / Boolean | Tolerancias de pago (cierra una factura con céntimos de diferencia). |
| `"Customer Posting Group"` | Code[20] | **Congelado** → cuenta de CxC usada. |
| `"Salesperson Code"` | Code[20] | |
| `"Sales (LCY)"` / `"Profit (LCY)"` / `"Inv. Discount (LCY)"` | Decimal | Estadística. |
| `"On Hold"` | Code[3] | Excluye de recordatorios y sugerencias de pago. |
| `"Transaction No."` | Integer | **Agrupa todas las partidas (de todos los libros) creadas por un mismo posteo.** |
| `"Source Code"` / `"Reason Code"` / `"User ID"` / `"Journal Batch Name"` / `"Journal Templ. Name"` / `"No. Series"` | Code | Auditoría/procedencia. |
| `"External Document No."` | Code[35] | |
| `"Bal. Account Type"` / `"Bal. Account No."` | Enum / Code[20] | Contrapartida (para pagos del diario de caja). |
| `"Calculate Interest"` / `"Closing Interest Calculated"` / `"Last Issued Reminder Level"` | Boolean / Integer | Cobranza. |
| `Reversed` / `"Reversed by Entry No."` / `"Reversed Entry No."` | Boolean / Integer | Reversión. |
| `"Direct Debit Mandate ID"` / `"Recipient Bank Account"` / `"Exported to Payment File"` / `"Payment Reference"` / `"Payment Method Code"` / `"Message to Recipient"` | varios | Pagos electrónicos. |
| `"Dispute Status"` / `"Promised Pay Date"` | Code[10] / Date | Gestión de cobro. |
| `"Adjusted Currency Factor"` / `"Original Currency Factor"` | Decimal | Tipo de cambio. |

**No existe** un campo `"Payment Discount %"` en la tabla 21: el porcentaje vive en `Payment Terms`; la partida
guarda solo los **importes** de descuento.

### 6.3 `Detailed Cust. Ledg. Entry` (379)

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Entry No."` | Integer | PK. |
| `"Entry Type"` | Enum `"Detailed CV Ledger Entry Type"` | Qué hecho representa (ver abajo). |
| `"Cust. Ledger Entry No."` | Integer | **FK a la partida padre (tabla 21).** |
| `"Customer No."` | Code[20] | Desnormalizado. |
| `"Posting Date"` | Date | |
| `"Document Type"` / `"Document No."` | Enum / Code[20] | Documento que produjo el hecho. |
| `Amount` / `"Amount (LCY)"` | Decimal | Importe con signo. |
| `"Debit Amount"` / `"Credit Amount"` / `"Debit Amount (LCY)"` / `"Credit Amount (LCY)"` | Decimal | Desglose D/H. |
| `"Currency Code"` | Code[10] | |
| `"Applied Cust. Ledger Entry No."` | Integer | **La partida contraparte de la aplicación.** |
| `Unapplied` | Boolean | El hecho fue revertido por una desaplicación. |
| `"Unapplied by Entry No."` | Integer | Quién lo revirtió. |
| `"Application No."` | Integer | **Agrupa todas las filas de detalle de una misma operación de aplicación.** |
| `"Transaction No."` | Integer | Agrupa por posteo (se pone a 0 en las filas de aplicación; usar `"Application No."`). |
| `"Ledger Entry Amount"` | **Boolean** | Trampa de nombre: es una *bandera* ("esta fila afecta el saldo de la partida"), no un importe. |
| `"Initial Entry Due Date"` | Date | Vencimiento copiado de la partida padre. |
| `"Initial Document Type"` | Enum | Tipo del documento de la partida padre. |
| `"Initial Entry Global Dim. 1"` / `"... 2"` | Code[20] | Dimensiones copiadas. |
| `"Max. Payment Tolerance"` | Decimal | |
| `"Gen. Bus. Posting Group"` / `"Gen. Prod. Posting Group"` / `"VAT Bus. Posting Group"` / `"VAT Prod. Posting Group"` / `"Posting Group"` | Code[20] | Grupos congelados (para el IVA de los descuentos por pronto pago). |
| `"Use Tax"` / `"Tax Jurisdiction Code"` | Boolean / Code[10] | Sales Tax (EEUU). |
| `"Remaining Pmt. Disc. Possible"` | Decimal | |
| `"Exch. Rate Adjmt. Reg. No."` | Integer | Ajuste de tipo de cambio. |
| `"User ID"` / `"Source Code"` / `"Reason Code"` / `"Journal Batch Name"` | Code | Auditoría. |

Miembros del enum `"Detailed CV Ledger Entry Type"` (namespace `Microsoft.Finance.ReceivablesPayables`):
`" "`, `"Initial Entry"`, `Application`, `"Unrealized Loss"`, `"Unrealized Gain"`, `"Realized Loss"`,
`"Realized Gain"`, `"Payment Discount"`, `"Payment Discount (VAT Excl.)"`,
`"Payment Discount (VAT Adjustment)"`, `"Appln. Rounding"`, `"Correction of Remaining Amount"`,
`"Payment Tolerance"`, `"Payment Discount Tolerance"`, `"Payment Tolerance (VAT Excl.)"`,
`"Payment Tolerance (VAT Adjustment)"`, `"Payment Discount Tolerance (VAT Excl.)"`,
`"Payment Discount Tolerance (VAT Adjustment)"`.

### 6.4 Cómo se aplica un pago a una factura

Dos mecanismos, según la cardinalidad:

**A. Un pago → una factura (`Applies-to Doc. No.`).** En la línea del diario de caja se elige el documento
destino; BC rellena `"Applies-to Doc. Type"` y `"Applies-to Doc. No."`. Es una relación 1:1 declarada en la
partida *aplicante*.

**B. Un pago → varias facturas (`Applies-to ID`).** La acción **Set Applies-to ID** sella el mismo
`"Applies-to ID"` (Code[50]) en cada partida abierta seleccionada, y `"Amount to Apply"` fija el importe
parcial de cada una (vacío = el máximo). Al postear, el pago cierra todo el lote marcado.

**Efecto al postear (o al ejecutar *Post Application*):**

1. Se insertan `Detailed Cust. Ledg. Entry` de `"Entry Type" = Application` **en ambos lados**, con signos
   opuestos, mismo `"Application No."`, y `"Applied Cust. Ledger Entry No."` apuntando cada uno a su
   contraparte.
2. Se insertan filas hermanas para los hechos accesorios: `"Payment Discount"`, `"Payment Tolerance"`,
   `"Realized Gain"` / `"Realized Loss"` (diferencia de cambio), `"Appln. Rounding"`.
3. `"Remaining Amount"` de la factura baja **por derivación** (es FlowField sobre el detalle recién insertado).
4. Cuando el saldo llega a cero, se limpia `Open` y se estampan `"Closed by Entry No."`, `"Closed at Date"`,
   `"Closed by Amount"` en **ambas** partidas.
5. Desaplicar no borra nada: inserta filas espejo y marca las originales `Unapplied` + `"Unapplied by Entry No."`.

Nada se borra ni se sobrescribe: **el libro de cartera es append-only y el estado (`Open`, `Closed by ...`) es
una caché derivable**. Se puede reconstruir por completo desde el detalle.

### 6.5 `Customer` (18) y `Customer Posting Group` (92)

**`Customer` (18)** — namespace `Microsoft.Sales.Customer`. Campos relevantes:

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"No."` | Code[20] | PK. |
| `Name` | Text[100] | |
| `"Search Name"` | **Code[100]** | En mayúsculas (es `Code`, no `Text`). |
| `Address` / `"City"` / `"Post Code"` / `"Country/Region Code"` / `"Phone No."` / `"E-Mail"` / `Contact` | Text/Code | Ficha. |
| `"Customer Posting Group"` | Code[20] | **→ cuenta de CxC.** |
| `"Gen. Bus. Posting Group"` | Code[20] | **→ mitad de clave de `General Posting Setup`.** |
| `"VAT Bus. Posting Group"` | Code[20] | **→ mitad de clave de `VAT Posting Setup`.** |
| `"Currency Code"` | Code[10] | Moneda por defecto. |
| `"Payment Terms Code"` / `"Payment Method Code"` | Code[10] | |
| `"Salesperson Code"` | Code[20] | |
| `"Credit Limit (LCY)"` | Decimal | Límite de crédito. |
| `"Balance (LCY)"` / `"Balance Due (LCY)"` | Decimal | **FlowFields** sobre `Cust. Ledger Entry` (total y vencido). |
| `Blocked` | Enum `"Customer Blocked"` | `" "` / `Ship` / `Invoice` / `All`. **No es booleano:** gradúa qué se bloquea. |
| `"Location Code"` / `"Shipment Method Code"` | Code[10] | Defaults logísticos. |
| `"Prices Including VAT"` | Boolean | |
| `"VAT Registration No."` | Text[20] | RNC/NIF. |
| `"Tax Area Code"` / `"Tax Liable"` | Code[20] / Boolean | Sales Tax. |
| `"Invoice Disc. Code"` / `"Customer Price Group"` / `"Customer Disc. Group"` | Code | Precios y descuentos. |
| `"Reminder Terms Code"` | Code[10] | Cobranza. |
| `"Bill-to Customer No."` | Code[20] | Redirige la facturación a otro cliente. |
| `"Last Date Modified"` | Date | |

**`Customer Posting Group` (92)** — tabla completa. Es el mapa "cliente → cuentas de balance y de ajuste":

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `Code` | Code[20] | PK. |
| `Description` | Text[100] | |
| `"Receivables Account"` | Code[20] | **Cuenta de Cuentas por Cobrar.** El único campo imprescindible. |
| `"Service Charge Acc."` | Code[20] | Cargos por servicio. |
| `"Payment Disc. Debit Acc."` / `"Payment Disc. Credit Acc."` | Code[20] | Descuento por pronto pago concedido/recuperado. |
| `"Payment Tolerance Debit Acc."` / `"Payment Tolerance Credit Acc."` | Code[20] | Tolerancia de pago. |
| `"Invoice Rounding Account"` | Code[20] | Redondeo de factura. |
| `"Debit Rounding Account"` / `"Credit Rounding Account"` | Code[20] | Redondeo del saldo residual. |
| `"Debit Curr. Appln. Rndg. Acc."` / `"Credit Curr. Appln. Rndg. Acc."` | Code[20] | Redondeo de aplicación entre monedas. |
| `"Additional Fee Account"` / `"Add. Fee per Line Account"` | Code[20] | Gastos de recordatorio. |
| `"Interest Account"` | Code[20] | Intereses de mora. |
| `"View All Accounts on Lookup"` | Boolean | UI: mostrar todas las cuentas y no solo las de la categoría. |

Nótese: la cuenta de CxC **no** sale de la matriz `General Posting Setup`. Sale de un grupo **unidimensional**
del cliente. Razón de diseño: el ítem es irrelevante para saber en qué cuenta de activo está el crédito.
---


---

## 7. Contabilidad — `G/L Account` (15), `G/L Entry` (17), `G/L Register` (45) y las posting setups

### 7.1 `G/L Account` (15) — namespace `Microsoft.Finance.GeneralLedger.Account`

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"No."` | Code[20] | PK. |
| `Name` | Text[100] | |
| `"Search Name"` | Code[100] | Mayúsculas. |
| `"Account Type"` | Enum `"G/L Account Type"` (16) | **No extensible.** `Posting`(0) / `Heading`(1) / `Total`(2) / `"Begin-Total"`(3) / `"End-Total"`(4). |
| `"Income/Balance"` | Enum `"G/L Account Report Type"` | Resultado vs. balance. (El enum **no** se llama `Income/Balance`.) |
| `"Account Category"` | Enum `"G/L Account Category"` (15) | **No extensible.** `" "` / `Assets` / `Liabilities` / `Equity` / `Income` / `"Cost of Goods Sold"` / `Expense`. Alimenta los estados financieros automáticos. |
| `"Account Subcategory Entry No."` / `"Account Subcategory Descript."` | Integer / Text[80] | Subcategoría (tabla aparte). |
| `"Debit/Credit"` | Option | Naturaleza esperada. |
| `"Direct Posting"` | Boolean | **`true` = se permite postear a esta cuenta desde un diario general a mano.** `false` = solo la puede tocar el motor de posteo vía posting setup. Es el control que evita que un usuario apunte un asiento manual a la cuenta de Inventario o de CxC y descuadre el subsidiario contra el mayor. |
| `Blocked` | Boolean | |
| `"Reconciliation Account"` | Boolean | Se muestra en la ventana de conciliación. |
| `Totaling` | Text[250] | Rango/filtro de cuentas que suma una cuenta `Total` o `End-Total`. |
| `Indentation` / `"No. of Blank Lines"` / `"New Page"` | Integer / Boolean | Presentación del plan de cuentas. |
| `"Gen. Posting Type"` | Enum `"General Posting Type"` (57) | `" "` / `Purchase` / `Sale` / `Settlement`. **Dirección** que la cuenta espera. |
| `"Gen. Bus. Posting Group"` / `"Gen. Prod. Posting Group"` | Code[20] | Grupos por defecto al postear directo a la cuenta. |
| `"VAT Bus. Posting Group"` / `"VAT Prod. Posting Group"` | Code[20] | Ídem para IVA. |
| `Balance` / `"Balance at Date"` / `"Net Change"` / `"Debit Amount"` / `"Credit Amount"` / `"Budgeted Amount"` | Decimal | **Todos FlowFields** sobre `G/L Entry`, gobernados por `"Date Filter"` / `"Budget Filter"`. |
| `"Default Deferral Template Code"` | Code[10] | Diferimiento de ingresos/gastos. |
| `"Consol. Translation Method"` / `"Consol. Debit Acc."` / `"Consol. Credit Acc."` / `"Exclude From Consolidation"` | varios | Consolidación. |
| `"Exchange Rate Adjustment"` | Enum `"Exch. Rate Adjustment Type"` | Revaluación por tipo de cambio. |
| `"Tax Area Code"` / `"Tax Liable"` / `"Tax Group Code"` | Code / Boolean | Sales Tax. |
| `"Omit Default Descr. in Jnl."` / `"Automatic Ext. Texts"` / `"Last Date Modified"` | Boolean / Date | |

Métodos reveladores: `CheckGLAcc()` **falla si `"Account Type" <> Posting` o si `Blocked`** — es la validación
de que solo las cuentas hoja reciben asientos; `IsTotaling()` es `true` para `Total` y `End-Total`.
El plan de cuentas de BC es por tanto un **árbol embebido en una lista plana**: `Heading`/`Begin-Total`/
`End-Total` son filas de presentación y `Totaling` es la expresión de agregación. No hay `ParentId`.

### 7.2 `G/L Entry` (17) — namespace `Microsoft.Finance.GeneralLedger.Ledger`

PK `"Entry No."`; claves secundarias `("G/L Account No.", "Posting Date")` y `"Transaction No."`.

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Entry No."` | Integer | PK. |
| `"G/L Account No."` | Code[20] | → `G/L Account."No."`. |
| `"G/L Account Name"` | Text[100] | FlowField. |
| `"Posting Date"` / `"Document Date"` / `"VAT Reporting Date"` | Date | |
| `"Document Type"` | Enum `"Gen. Journal Document Type"` | |
| `"Document No."` | Code[20] | |
| `Description` | Text[100] | |
| `Amount` | Decimal | **Importe con signo en moneda local.** Positivo = débito. |
| `"Debit Amount"` / `"Credit Amount"` | Decimal | Desglose (uno de los dos es 0). |
| `"Additional-Currency Amount"` / `"Add.-Currency Debit Amount"` / `"Add.-Currency Credit Amount"` | Decimal | Moneda adicional de reporte. |
| `"Source Currency Amount"` / `"Source Currency Code"` / `"Source Currency VAT Amount"` | Decimal / Code | Moneda origen. |
| `"Bal. Account Type"` / `"Bal. Account No."` | Enum `"Gen. Journal Account Type"` / Code[20] | Contrapartida, si el asiento se capturó como dos columnas. |
| `"VAT Amount"` | Decimal | IVA asociado. |
| `"Gen. Posting Type"` | Enum `"General Posting Type"` | `Sale` / `Purchase` / `Settlement`. **Deja grabado qué rama del posting setup se usó.** |
| `"Gen. Bus. Posting Group"` / `"Gen. Prod. Posting Group"` | Code[20] | **Los grupos con los que se resolvió la cuenta, grabados en el asiento.** |
| `"VAT Bus. Posting Group"` / `"VAT Prod. Posting Group"` | Code[20] | Ídem para IVA. |
| `"Source Type"` | Enum `"Gen. Journal Source Type"` | `Customer` / `Vendor` / `"Bank Account"` / `"Fixed Asset"` / … |
| `"Source No."` | Code[20] | El socio de negocio concreto. |
| `"Source Code"` / `"Reason Code"` | Code[10] / Code[10] | Procedencia y motivo. |
| `"Transaction No."` | Integer | **Agrupa todas las líneas de un mismo asiento (y de todos los libros auxiliares del mismo posteo).** |
| `"System-Created Entry"` | Boolean | El asiento lo generó el motor (no un usuario). |
| `"Prior-Year Entry"` | Boolean | |
| `"Journal Batch Name"` / `"Journal Templ. Name"` | Code[10] | |
| `"User ID"` / `"No. Series"` | Code | |
| `Quantity` | Decimal | Cantidad estadística. |
| `"Job No."` / `"Prod. Order No."` / `"FA Entry No."` / `"FA Entry Type"` | Code[20] / Integer / Option | Enlaces a subsistemas. |
| `"Business Unit Code"` | Code[20] | Consolidación. |
| `"Dimension Set ID"` / `"Global Dimension 1 Code"` / `"2"` / `"Shortcut Dimension 3..8 Code"` | Integer / Code[20] | Analítica. |
| `Reversed` / `"Reversed by Entry No."` / `"Reversed Entry No."` | Boolean / Integer | **Reversión, no borrado.** |
| `"External Document No."` | Code[35] | |
| `"Close Income Statement Dim. ID"` | Integer | Cierre de resultados. |
| `"Non-Deductible VAT Amount"` / `"... ACY"` | Decimal | IVA no deducible. |

### 7.3 `G/L Register` (45) — el "asiento" como rango

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"No."` | Integer | PK. |
| `"From Entry No."` / `"To Entry No."` | Integer | **Rango de `G/L Entry` que abarca este registro.** |
| `"From VAT Entry No."` / `"To VAT Entry No."` | Integer | Rango de `VAT Entry`. |
| `"Source Code"` | Code[10] | Qué proceso lo creó. |
| `"User ID"` | Code[50] | |
| `"Journal Batch Name"` / `"Journal Templ. Name"` | Code[10] | |
| `Reversed` | Boolean | |
| `"Creation Date"` / `"Creation Time"` | Date / Time | Legado de auditoría (hoy sustituidos por `SystemCreatedAt`). |

Idea de diseño: BC **no** tiene una tabla "Asiento" con líneas hijas. Tiene un libro plano de líneas
(`G/L Entry`) más un **índice de rangos** (`G/L Register`). Cada corrida de posteo consume un bloque contiguo
de `Entry No.` y deja una fila de registro que dice `[desde, hasta]`. El mismo patrón se repite en
`Item Register` (46) con **cuatro** rangos paralelos (ítem, valor, inventario físico, capacidad).
Ventajas: inserción puramente append, sin cabecera que actualizar, y "deshacer un posteo" es un rango.
Desventaja: el registro no sobrevive a una renumeración y depende de que los `Entry No.` sean estrictamente
crecientes dentro de la transacción.

### 7.4 ⭐ El mecanismo de posting groups — el corazón del modelo

**Principio.** En el documento y en el asiento **nunca se captura una cuenta contable**. Se capturan
*clasificadores* (posting groups) y la cuenta se **deriva** en el momento del posteo por un `GET` de clave
compuesta sobre una tabla de setup. Hay tres derivaciones independientes y una cuarta unidimensional.

#### (a) Resultado: `Gen. Bus. Posting Group` × `Gen. Prod. Posting Group` → `General Posting Setup` (252)

```
  Customer (18)."Gen. Bus. Posting Group"          Item (27)."Gen. Prod. Posting Group"
              │  (se copia a Sales Header.89)                │  (se copia a Sales Line.57)
              └──────────────┐                ┌──────────────┘
                             ▼                ▼
                    ┌────────────────────────────────────┐
                    │  General Posting Setup (252)       │
                    │  PK = ("Gen. Bus. Posting Group",  │
                    │        "Gen. Prod. Posting Group")  │
                    └────────────────────────────────────┘
                             │ un solo GET, sin búsqueda ni fallback
                             ▼
        "Sales Account"  "COGS Account"  "Purch. Account"
        "Sales Line Disc. Account"  "Sales Inv. Disc. Account"  ...
```

La resolución es un **`GET` de dos partes sobre la clave primaria**: no hay búsqueda, no hay secuencia de
acceso, no hay comodines, no hay herencia. La fila existe o el posteo **falla** con un error que nombra el
campo vacío (p. ej. *"Sales Prepayment account is missing a General Posting Setup."*).

Origen de cada mitad, según la documentación: *"Estos posting groups se eligen en la ficha del cliente:
General business posting group, Customer posting group. Estos se eligen en la ficha del ítem: General product
posting group, Inventory posting group. Cuando se crea un documento de venta, la cabecera usa la información
de la ficha del cliente y las líneas usan la información de la ficha del ítem."*

Y el motivo de la matriz: *"Para cada combinación de business y product posting groups se puede asignar un
conjunto de cuentas. Por ejemplo, se puede postear la venta del mismo ítem a cuentas distintas del mayor
porque los clientes están asignados a business posting groups distintos."*

`General Posting Setup` (252) — **tabla completa**:

| Campo AL | Tipo | Qué resuelve |
| --- | --- | --- |
| `"Gen. Bus. Posting Group"` | Code[20] | PK 1. |
| `"Gen. Prod. Posting Group"` | Code[20] | PK 2. |
| `"Sales Account"` | Code[20] | **Ingreso por ventas.** ⭐ |
| `"Sales Credit Memo Account"` | Code[20] | Reversión de ingreso por nota de crédito. |
| `"Sales Line Disc. Account"` | Code[20] | Descuento de línea. |
| `"Sales Inv. Disc. Account"` | Code[20] | Descuento de factura. |
| `"Sales Pmt. Disc. Debit Acc."` / `"Sales Pmt. Disc. Credit Acc."` | Code[20] | Descuento por pronto pago. |
| `"Sales Pmt. Tol. Debit Acc."` / `"Sales Pmt. Tol. Credit Acc."` | Code[20] | Tolerancia de pago. |
| `"Sales Prepayments Account"` | Code[20] | Anticipos de cliente. |
| `"COGS Account"` | Code[20] | **Costo de ventas.** ⭐ |
| `"COGS Account (Interim)"` | Code[20] | COGS de costo **esperado** (entregado sin facturar). |
| `"Purch. Account"` | Code[20] | Compras. |
| `"Purch. Credit Memo Account"` | Code[20] | |
| `"Purch. Line Disc. Account"` / `"Purch. Inv. Disc. Account"` | Code[20] | |
| `"Purch. Pmt. Disc. Credit Acc."` / `"Purch. Pmt. Disc. Debit Acc."` | Code[20] | |
| `"Purch. Pmt. Tol. Debit Acc."` / `"Purch. Pmt. Tol. Credit Acc."` | Code[20] | |
| `"Purch. Prepayments Account"` | Code[20] | |
| `"Purch. FA Disc. Account"` | Code[20] | Descuento en compra de activo fijo. |
| `"Inventory Adjmt. Account"` | Code[20] | Contrapartida de resultado del ajuste de valor de inventario. |
| `"Invt. Accrual Acc. (Interim)"` | Code[20] | Devengo de inventario (costo esperado). |
| `"Direct Cost Applied Account"` / `"Direct Cost Non-Inv. App. Acc."` | Code[20] | Costo directo aplicado (producción). |
| `"Overhead Applied Account"` | Code[20] | Gasto indirecto aplicado. |
| `"Purchase Variance Account"` | Code[20] | Variación de compra contra costo estándar. |
| `Description` | Text[100] | |
| `Blocked` | Boolean | Impide usar la combinación en posteos nuevos (**las filas no se pueden borrar tras usarse, porque existen `G/L Entry` que las referencian**). |
| `"View All Accounts on Lookup"` | Boolean | UI. |

**Qué rama se usa** la decide `"Gen. Posting Type"` (enum 57: `" "`/`Purchase`/`Sale`/`Settlement`), que viaja
en la línea del diario, en la cuenta contable y acaba grabado en el `G/L Entry`: `Sale` selecciona los campos
`Sales*`, `Purchase` los `Purch*`.

Cada campo tiene un accesor que **valida y falla con nombre**: `GetSalesAccount()`, `GetCOGSAccount()`,
`GetCOGSInterimAccount()`, `GetSalesLineDiscAccount()`, `GetSalesInvDiscAccount()`, `GetPurchAccount()`,
`GetInventoryAdjmtAccount()`, `GetInventoryAccrualAccount()`, `GetDirectCostAppliedAccount()`,
`GetOverheadAppliedAccount()`, `GetPurchaseVarianceAccount()`, etc. **Este patrón es directamente portable:**
un `IPostingSetupResolver` cuyo método `GetSalesAccount(busGroup, prodGroup)` lance una excepción de dominio
con el nombre del campo faltante, en lugar de devolver `null` o cadena vacía.

**La cuenta de CxC NO sale de aquí.** *"The accounts receivable posting (balance sheet) is determined by the
customer posting group"* — sale de `Customer Posting Group` (92), tabla unidimensional (§6.5).

#### (b) IVA: `VAT Bus. Posting Group` × `VAT Prod. Posting Group` → `VAT Posting Setup` (325)

```
  Customer."VAT Bus. Posting Group"              Item."VAT Prod. Posting Group"
    (mercados: DOMESTIC / EU / NON-EU)             (tipos de gravamen: NO-VAT / VAT10 / VAT18)
              └──────────────┐            ┌──────────────┘
                             ▼            ▼
                 ┌──────────────────────────────────────┐
                 │  VAT Posting Setup (325)             │
                 │  PK = ("VAT Bus. Posting Group",     │
                 │        "VAT Prod. Posting Group")     │
                 └──────────────────────────────────────┘
                             │ un solo GET
                             ▼
    "VAT %"  "VAT Calculation Type"  "VAT Identifier"
    "Sales VAT Account"  "Purchase VAT Account"  "Reverse Chrg. VAT Acc."
```

Misma forma: el par **es** la clave primaria → un `GET`. La documentación: *"Business Central calcula los
importes de IVA en ventas y compras a partir de los VAT posting setups, que son combinaciones de VAT business
y VAT product posting groups. Para cada combinación se puede especificar el porcentaje de IVA, el tipo de
cálculo de IVA y las cuentas del mayor para postear IVA de ventas, compras y reverse charge."*

Lectura semántica de cada eje: el **business** group representa *"los mercados en los que haces negocio"*
(`DOMESTIC`, `EU`, `NON-EU`); el **product** group representa *"los ítems y recursos que compras o vendes"*
(`NO-VAT`, `VAT10`, `VAT25`). El cruce es el que decide la tasa: el mismo ítem lleva 18 % a un cliente local
y 0 % a un exportador **sin cambiar nada en el ítem**.

`VAT Posting Setup` (325) — **tabla completa**:

| Campo AL | Tipo | Qué resuelve |
| --- | --- | --- |
| `"VAT Bus. Posting Group"` / `"VAT Prod. Posting Group"` | Code[20] | PK. |
| `"VAT %"` | Decimal | **La tasa.** ⭐ |
| `"VAT Calculation Type"` | Enum `"Tax Calculation Type"` (254) | `"Normal VAT"` / `"Reverse Charge VAT"` / `"Full VAT"` / `"Sales Tax"`. |
| `"VAT Identifier"` | Code[20] | **Agrupador de cálculo y reporte.** Ver nota abajo. |
| `"Sales VAT Account"` | Code[20] | **IVA por pagar (ventas).** ⭐ |
| `"Purchase VAT Account"` | Code[20] | **IVA soportado (compras).** ⭐ |
| `"Sales VAT Unreal. Account"` / `"Purch. VAT Unreal. Account"` | Code[20] | IVA no realizado (criterio de caja). |
| `"Unrealized VAT Type"` | Option | Activa el IVA no realizado. |
| `"Reverse Chrg. VAT Acc."` / `"Reverse Chrg. VAT Unreal. Acc."` | Code[20] | Inversión del sujeto pasivo. |
| `"Adjust for Payment Discount"` | Boolean | Recalcula el IVA al aplicar descuento por pronto pago. |
| `"EU Service"` | Boolean | Marca de servicio intracomunitario para reportes. |
| `"VAT Clause Code"` | Code[20] | Texto legal en la factura. |
| `"Certificate of Supply Required"` | Boolean | |
| `"Tax Category"` | Code[10] | Categoría fiscal (facturación electrónica). |
| `"Sale VAT Reporting Code"` / `"Purch. VAT Reporting Code"` | Code[20] | Códigos de declaración. |
| `"Non-Deductible VAT %"` / `"Non-Ded. Purchase VAT Account"` / `"Allow Non-Deductible VAT"` | Decimal / Code[20] / Enum | IVA no deducible. |
| `Description` / `Blocked` | Text[100] / Boolean | |

Accesores: `GetSalesAccount(Unrealized: Boolean)`, `GetPurchAccount(Unrealized: Boolean)`,
`GetRevChargeAccount(Unrealized: Boolean)`; `TestNotSalesTax()` bloquea configurar campos de IVA cuando el tipo
es `"Sales Tax"`.

**`"VAT Identifier"` no es una cuenta, es la unidad de cálculo y redondeo.** Doc: *"Para agrupar combinaciones
de VAT posting setup con atributos similares, defina un VAT Identifier para cada grupo… Recomendamos usar
identificadores distintos para porcentajes distintos."* Y lo decisivo: ***"Business Central calcula el IVA para
un documento completo. El cálculo se basa en la suma de todas las líneas con el mismo VAT Identifier del
documento."*** Es decir: el IVA **no** se calcula línea por línea y se suma — se calcula sobre la **base
agregada por identificador**. Replicar el redondeo línea a línea produce diferencias de céntimos contra la
factura legal. Es probablemente el detalle de implementación más fácil de equivocar de todo el modelo.

Los cuatro `Tax Calculation Type`:

| Valor | Comportamiento |
| --- | --- |
| `"Normal VAT"` | IVA = base × `"VAT %"`; va a `"Sales VAT Account"` / `"Purchase VAT Account"`. |
| `"Reverse Charge VAT"` | Comercio B2B intracomunitario. En ventas: *"Business Central calcula el importe de IVA y crea un `VAT Entry`… **no se postean asientos a las cuentas de IVA del mayor**."* En compras, la autoliquidación va a `"Reverse Chrg. VAT Acc."`. |
| `"Full VAT"` | **El importe completo de la línea *es* IVA** (típico: IVA de importación). Receta documentada: tipo `Full VAT` + solo `"Purchase VAT Account"`; el resto de cuentas es opcional. |
| `"Sales Tax"` | Jurisdicciones sin IVA (EEUU): setup paralelo Tax Business × Tax Product → `Tax Posting Setup`. |

Además, los grupos de propósito general pueden **sembrar** los de IVA:
`Gen. Business Posting Group` (250) tiene `"Def. VAT Bus. Posting Group"` + `"Auto Insert Default"`, y
`Gen. Product Posting Group` (251) tiene `"Def. VAT Prod. Posting Group"` + `"Auto Insert Default"`. Es decir:
elegir el grupo contable del cliente propone su grupo de IVA, pero sigue siendo un campo aparte y editable.

#### (c) Balance de inventario: `Inventory Posting Group` × `Location Code` → `Inventory Posting Setup` (5813)

```
  Item."Inventory Posting Group"          Item Ledger Entry / línea."Location Code"
              └──────────────┐            ┌──────────────┘
                             ▼            ▼
              ┌────────────────────────────────────────────┐
              │  Inventory Posting Setup (5813)            │
              │  PK = ("Location Code",                    │  ← ¡Location va PRIMERO en la clave!
              │        "Invt. Posting Group Code")          │
              └────────────────────────────────────────────┘
                             │
                             ▼
              "Inventory Account"  "Inventory Account (Interim)"  "WIP Account"
              "Material Variance Account"  ... (cuentas de variación)
```

**Ésta es la única de las tres que no es "socio × producto": es "grupo × lugar".** Documentación:
*"Defina inventory posting groups que luego asigna a las cuentas de ítem relevantes en la página Inventory
Posting Setup. Así, al postear entries de un ítem, el sistema postea a la cuenta del mayor configurada para la
**combinación de inventory posting group y la ubicación vinculada al ítem**."* Y la división de
responsabilidades: *"The inventory posting (balance sheet) is determined by the inventory posting group"*,
mientras que ingreso y COGS vienen de `General Posting Setup`.

Nótese además que el namespace de esta tabla es `Microsoft.Inventory.Item`, no `...Setup`, y que
**`"Location Code"` es el primer campo de la clave** (el slug es `microsoft.inventory.item.inventory-posting-setup`).

`Inventory Posting Setup` (5813) — **tabla completa**:

| Campo AL | Tipo | Qué resuelve |
| --- | --- | --- |
| `"Location Code"` | Code[10] | PK 1. |
| `"Invt. Posting Group Code"` | Code[20] | PK 2. |
| `"Inventory Account"` | Code[20] | **Cuenta de activo de inventario (costo real/facturado).** ⭐ |
| `"Inventory Account (Interim)"` | Code[20] | **Cuenta interina de costo esperado** (recibido-no-facturado / enviado-no-facturado). |
| `"WIP Account"` | Code[20] | Producción en curso. |
| `"Material Variance Account"` | Code[20] | Variación de material (costo estándar). |
| `"Capacity Variance Account"` | Code[20] | Variación de capacidad. |
| `"Mfg. Overhead Variance Account"` / `"Cap. Overhead Variance Account"` | Code[20] | Variaciones de gasto indirecto. |
| `"Subcontracted Variance Account"` | Code[20] | Variación de subcontratación. |
| `"Mat. Non-Inv. Variance Acc."` | Code[20] | Variación de material no inventariable. |
| `Description` / `"View All Accounts on Lookup"` | Text[100] / Boolean | |

Accesores: `GetInventoryAccount()`, `GetInventoryAccountInterim()`, `GetWIPAccount()`,
`GetMaterialVarianceAccount()`, `GetCapacityVarianceAccount()`, `GetMfgOverheadVarianceAccount()`,
`GetCapOverheadVarianceAccount()`, `GetSubcontractedVarianceAccount()`,
`GetMaterialNonInventoryVarianceAccount()`.

#### (d) Las cuatro tablas de grupos (son casi vacías, y eso es el punto)

| Tabla | ID | Campos |
| --- | --- | --- |
| `Gen. Business Posting Group` | 250 | `Code`, `Description`, `"Def. VAT Bus. Posting Group"`, `"Auto Insert Default"` |
| `Gen. Product Posting Group` | 251 | `Code`, `Description`, `"Def. VAT Prod. Posting Group"`, `"Auto Insert Default"` |
| `Inventory Posting Group` | 94 | `Code`, `Description` |
| `VAT Business Posting Group` | 323 | `Code`, `Description`, `"Last Modified Date Time"` |
| `VAT Product Posting Group` | 324 | `Code`, `Description`, `"Last Modified DateTime"` |

Las tablas de grupo **no contienen cuentas**. Son puros vocabularios: etiquetas con descripción. Toda la
información contable vive en las tablas de *setup* (252 / 325 / 5813), que son las intersecciones. Esta
separación vocabulario/intersección es lo que hace el modelo extensible sin tocar los maestros: añadir un
mercado nuevo es una fila en 323 más N filas en 325.

(Detalle de inconsistencia real del producto, útil si se van a mapear DTOs: la 323 escribe
`"Last Modified Date Time"` en tres palabras y la 324 `"Last Modified DateTime"` en dos.)

#### (e) Resumen de la resolución para una línea de factura de venta

| Pata del asiento | Fuente de la cuenta | Clave de resolución |
| --- | --- | --- |
| **Débito** Cuentas por cobrar | `Customer Posting Group."Receivables Account"` | `Customer."Customer Posting Group"` (1 dimensión) |
| **Crédito** Ventas | `General Posting Setup."Sales Account"` | (`Gen. Bus. Posting Group` × `Gen. Prod. Posting Group`) |
| **Crédito** IVA por pagar | `VAT Posting Setup."Sales VAT Account"` | (`VAT Bus. Posting Group` × `VAT Prod. Posting Group`) |
| **Débito** Costo de ventas | `General Posting Setup."COGS Account"` | (`Gen. Bus.` × `Gen. Prod.`) — la misma fila que la de Ventas |
| **Crédito** Inventario | `Inventory Posting Setup."Inventory Account"` | (`Location Code` × `Inventory Posting Group`) |

Cinco patas, **cuatro** tablas de configuración, **cero** cuentas escritas en el documento.

---


---

## 8. Numeración de documentos — `No. Series` (308) / `No. Series Line` (309)

Namespace `Microsoft.Foundation.NoSeries`. **Estas dos tablas viven en el módulo *business-foundation*, no en
base-application** (el slug de la documentación lleva el punto: `...noseries.no.-series`).

### 8.1 `No. Series` (308) — la cabecera es mínima

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `Code` | Code[20] | PK. Identificador de la serie (p. ej. `S-INV`). |
| `Description` | Text[100] | |
| `"Default Nos."` | Boolean | La serie se usa automáticamente al crear el documento. |
| `"Manual Nos."` | Boolean | Se permite que el usuario escriba el número a mano. |
| `"Date Order"` | Boolean | **Obliga a que los números se obtengan en orden cronológico.** |

Eso es **todo**. Nada numérico vive aquí.

### 8.2 `No. Series Line` (309) — el estado real

| Campo AL | Tipo | Función |
| --- | --- | --- |
| `"Series Code"` | Code[20] | PK 1 → `No. Series.Code`. |
| `"Line No."` | Integer | PK 2. |
| `"Starting Date"` | Date | **Desde cuándo rige esta línea.** Permite cambiar el formato por año sin tocar el código. |
| `"Starting No."` | Code[20] | Primer número (p. ej. `FV-2026-000001`). Máx. 20 caracteres. |
| `"Ending No."` | Code[20] | Último permitido. |
| `"Increment-by No."` | Integer | Paso. |
| `"Last No. Used"` | Code[20] | **El estado en modo `Normal`.** |
| `"Last Date Used"` | Date | Para validar `"Date Order"`. |
| `"Warning No."` | Code[20] | Avisa al aproximarse a `"Ending No."`. |
| `Open` | Boolean | La línea aún puede emitir. |
| `"Sequence Name"` | Code[40] | **Nombre de la secuencia de SQL Server** en modo `Sequence`. |
| `"Starting Sequence No."` | BigInteger | Valor inicial de esa secuencia. |
| `Implementation` | Enum `"No. Series Implementation"` (397) | `Normal` / `Sequence`. |

La línea activa se elige por `"Starting Date"`: *"las series son consecutivas, comenzando cada serie en su
fecha de inicio respectiva"*; `GetNextNo(NoSeriesCode, UsageDate)` *"encuentra la primera línea válida de
No. Series según UsageDate"*.

### 8.3 Cómo se garantiza la secuencia sin huecos — y qué cuesta

**El campo `"Allow Gaps in Nos."` ya no existe como campo publicado.** Fue sustituido por el enum
`Implementation`; la UI y los docs de usuario siguen llamándolo "Allow Gaps in Nos.", pero internamente es
una elección de implementación despachada por la interfaz `"No. Series - Single"`.

| `Implementation` | Mecanismo (textual de la doc) | Consecuencia |
| --- | --- | --- |
| `Normal` | *"la implementación estándar, que **actualiza la base de datos en cada llamada** y usa la tabla para mantener el estado"* | El estado **es** `"Last No. Used"` en la fila de la 309. Cada asignación hace read-modify-write de esa fila **dentro de la transacción del llamante**. |
| `Sequence` | *"la implementación de secuencia que **no actualiza la base de datos en cada llamada** y usa secuencias de base de datos para mantener el estado"* | Usa una SQL Server SEQUENCE (`"Sequence Name"`, `"Starting Sequence No."`; en AL: `NumberSequence.Next/Current/Insert/Exists/Delete`). No bloquea. |

**La afirmación clave, y la razón real de la garantía:** en modo `Normal`, *"cuando una transacción solicita un
número, la tabla **No. Series Line** queda **bloqueada** hasta que la transacción termina. Esto puede bloquear
a otros usuarios."*

Es decir: **la ausencia de huecos y el bloqueo son el mismo hecho.** Para garantizar que no haya huecos el
número no puede entregarse hasta que la transacción que lo consume haga *commit*, y mientras tanto no puede
entregarse a nadie más. No existe forma de tener ambas cosas: o el número es transaccional (sin huecos,
serializado) o es extra-transaccional (concurrente, con huecos). Y con secuencias los huecos son inevitables
por la asimetría del rollback: *"`NumberSequence.Current` se obtiene fuera de la transacción. El valor no se
devolverá al hacer rollback de la transacción."* → *"Los números se usan secuencialmente, pero se pueden
saltar… los huecos pueden ocurrir cuando las transacciones se revierten o cuando se asignan números que no se
usan."*

**Por defecto no se permiten huecos**, y la justificación es legal, no técnica: *"por defecto no se permiten
huecos en las series numéricas porque el historial exacto de las transacciones financieras debe estar
disponible para auditoría, por ley, y por tanto debe seguir una secuencia ininterrumpida sin números
borrados."* Los huecos se reservan para series no financieras: *"se asignan series a clientes, cotizaciones de
venta y actividades de almacén, pero no están sujetas a auditoría financiera y se pueden borrar."*

**API relevante** (codeunit **310** `"No. Series"`, que *"usa activamente la base de datos para realizar las
operaciones (no agrupa solicitudes)"*):

| Procedimiento | Uso |
| --- | --- |
| `GetNextNo(...)` | Consume el siguiente número (5 sobrecargas). |
| `PeekNextNo(...)` | **Mira el siguiente sin consumirlo** (3 sobrecargas) — para mostrarlo en la UI. |
| `GetLastNoUsed(...)` | Último emitido. |
| `MayProduceGaps(NoSeriesLine): Boolean` | **La forma soportada de preguntar en código si esta serie puede dejar huecos.** |
| `IsManual` / `TestManual` / `IsAutomatic` / `TestAutomatic` | Modo de asignación. |
| `IsNoSeriesInDateOrder` | Valida `"Date Order"`. |
| `AreRelated` / `HasRelatedSeries` / `LookupRelatedNoSeries` | Series relacionadas (p. ej. la serie del posteado respecto a la del borrador). |
| codeunit `"No. Series - Batch"` | *"agrupa solicitudes hasta que se llama a `SaveState()`"* — la variante de alto rendimiento. |

### 8.4 Doble serie: borrador y posteado

El detalle que cierra el círculo con §5.1: los documentos llevan **dos** series.
`Sales Header."No. Series"` numera el **borrador**; `Sales Header."Posting No. Series"` numera el **documento
posteado**, y el número se reserva en `"Posting No."` / `"Last Posting No."`. Lo mismo en el diario de
inventario: `Item Journal Template/Batch."No. Series"` numera las líneas del diario, y `"Posting No. Series"`
numera el documento resultante.

**Consecuencia operativa y trampa de rendimiento:** el número del posteado se extrae *durante* la transacción
de posteo (en el diario de inventario, dentro de `Item Jnl.-Post Batch`, codeunit 23). Con
`Implementation = Normal`, ese es exactamente el escenario en el que el bloqueo de la fila de
`No. Series Line` se mantiene durante **toda** la rutina de posteo — larga — y serializa a todos los demás
usuarios de la misma serie. La mitigación de diseño es tener series **separadas** para el borrador (donde los
huecos son tolerables → `Sequence`) y para el posteado (donde no lo son → `Normal`, y donde se debe minimizar
la duración de la transacción de posteo).
---

## 9. ⭐ Asientos generados al postear una factura de venta

Escenario: una `Sales Invoice` con **una** línea de `Type = Item`, cantidad **Q**, precio unitario **P**,
tasa de IVA **V**, costo unitario del ítem **C**. Base = `Q × P`, IVA = base × V, total = base + IVA.

### 9.1 Inventario completo de lo que se crea

| # | Tabla | Qué fila | Detalle |
| --- | --- | --- | --- |
| 1 | `Sales Invoice Header` (112) | 1 | Copia de la cabecera. `No.` de la serie del posteado; `"Pre-Assigned No."` = el número que tenía el borrador. Congela `"Customer Posting Group"`, `"Gen. Bus. Posting Group"`, `"VAT Bus. Posting Group"`, `"Location Code"`, `"Dimension Set ID"`. Lleva `"Cust. Ledger Entry No."` apuntando a (3). |
| 2 | `Sales Invoice Line` (113) | 1 por línea | Copia de la línea sin los campos de proceso. |
| — | `Sales Header` (36) / `Sales Line` (37) | **se eliminan** | *"La factura de venta se elimina de la lista de facturas de venta y se sustituye por un documento nuevo en la lista de facturas de venta posteadas."* |
| 3 | `Cust. Ledger Entry` (21) | **1** | `"Document Type" = Invoice`, `"Document No."` = nº del posteado, `"Customer No."` = Bill-to, `"Posting Date"`, `"Due Date"`, `"Customer Posting Group"` congelado, `Open = true`, `Positive = true`. `Amount` / `"Remaining Amount"` son FlowFields = **total con IVA**. |
| 4 | `Detailed Cust. Ledg. Entry` (379) | **1** | `"Entry Type" = "Initial Entry"`, `Amount` = +total, `"Cust. Ledger Entry No."` → (3). *(La documentación no lo enuncia en prosa; se deduce del enum más las descripciones de los FlowFields de la tabla 21.)* |
| 5 | `Item Ledger Entry` (32) | **1** por línea de ítem | `"Entry Type" = Sale`, `Quantity = −Q`, `"Invoiced Quantity" = −Q`, `Positive = false`, `"Location Code"`, `"Document Type"`, `"Document No."`, `"Source Type" = Customer`, `"Source No."` = cliente, `"Completely Invoiced" = true`. |
| 6 | `Item Application Entry` (339) | 1 o más | `(Inbound = <ILE de la entrada fuente>, Outbound = <ILE (5)>, Quantity = −Q)`. Una arista por cada capa de costo consumida. Reduce el `"Remaining Quantity"` de las entradas. |
| 7 | `Value Entry` (5802) | **1 o 2** por línea de ítem | `"Item Ledger Entry Type" = Sale`, `"Entry Type" = "Direct Cost"`. `"Cost Amount (Actual)" = −(Q × C)`, `"Sales Amount (Actual)"`, `"Invoiced Quantity" = −Q`. Si el envío se posteó antes (albarán), hay **dos**: una de costo esperado al enviar y otra al facturar que **revierte** la esperada (`"Cost Amount (Expected)"` con signo contrario) y registra la real. |
| 8 | `VAT Entry` | 1 por `VAT Identifier` | Registro fiscal del IVA (base + importe). |
| 9 | `G/L Entry` (17) | **3 o 5** — ver 9.2 | Las patas del asiento. Todas con el mismo `"Transaction No."`. |
| 10 | `G/L Register` (45) | 1 | Rango `[From Entry No., To Entry No.]` de (9) + rango de `VAT Entry`. |
| 11 | `Item Register` (46) | 1 | Rangos de `Item Ledger Entry` y de `Value Entry`. *(Deducido de los FK `"Item Register No."`; ninguna página lo enuncia en prosa para el caso de la factura de venta.)* |
| 12 | `G/L - Item Ledger Relation` (5823) | 1 por (G/L Entry × Value Entry) | Solo cuando el costo se lleva al mayor. Tres campos: `"G/L Entry No."`, `"Value Entry No."`, `"G/L Register No."`. *"La relación entre value entries y general ledger entries se almacena en la tabla **G/L - Item Ledger Relation**."* |

Confirmación textual de (3) y (5): *"Se crea también una entry en la cuenta del cliente en la tabla
**Cust. Ledger Entry** y una entry del mayor en la cuenta de cobros correspondiente."* / *"Para cada línea de
pedido de venta se crea un item ledger entry en la tabla **Item Ledger Entry** (si las líneas contienen
números de ítem) o un general ledger entry en la tabla **G/L Entry** (si las líneas contienen una cuenta del
mayor)."*

### 9.2 Las patas contables y de dónde sale cada cuenta

```
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │  BLOQUE COMERCIAL — se postea SIEMPRE al facturar                                     │
 ├──────────────────────────────────────────────────────────────────────────────────────┤
 │  D  Cuentas por cobrar        Q×P×(1+V)                                              │
 │        ← Customer Posting Group[ Customer."Customer Posting Group" ]                 │
 │            ."Receivables Account"                      (accesor GetReceivablesAccount)│
 │                                                                                       │
 │  H  Ventas                    Q×P                                                    │
 │        ← General Posting Setup[ "Gen. Bus. Posting Group" × "Gen. Prod. Posting Group" ]│
 │            ."Sales Account"                            (accesor GetSalesAccount)      │
 │                                                                                       │
 │  H  IVA por pagar             Q×P×V                                                  │
 │        ← VAT Posting Setup[ "VAT Bus. Posting Group" × "VAT Prod. Posting Group" ]    │
 │            ."Sales VAT Account"                        (accesor GetSalesAccount(false))│
 └──────────────────────────────────────────────────────────────────────────────────────┘
 ┌──────────────────────────────────────────────────────────────────────────────────────┐
 │  BLOQUE DE COSTO — se postea al facturar SOLO SI Inventory Setup."Automatic Cost       │
 │  Posting" = true; en caso contrario espera al batch job Post Inventory Cost to G/L    │
 ├──────────────────────────────────────────────────────────────────────────────────────┤
 │  D  Costo de ventas (COGS)    Q×C                                                    │
 │        ← General Posting Setup[ mismos dos grupos, LA MISMA FILA que Ventas ]         │
 │            ."COGS Account"                             (accesor GetCOGSAccount)       │
 │                                                                                       │
 │  H  Inventario                Q×C                                                    │
 │        ← Inventory Posting Setup[ "Location Code" × "Invt. Posting Group Code" ]      │
 │            ."Inventory Account"                        (accesor GetInventoryAccount)  │
 └──────────────────────────────────────────────────────────────────────────────────────┘
 (+ opcional) H/D  Descuento de línea / de factura
        ← General Posting Setup."Sales Line Disc. Account" / ."Sales Inv. Disc. Account"
          Solo si Sales & Receivables Setup."Discount Posting" lo pide.
```

Dirección confirmada por el ejemplo documentado — una venta de 10 uds. a costo 80 produce
`[Inventory Account] 2130 = −80.00` y `[COGS Account] 7290 = +80.00`. Regla general literal:
*"Durante la conciliación, los valores de inventario se postean a la cuenta de inventario del balance. El mismo
importe, pero con signo inverso, se postea a la cuenta de contrapartida correspondiente… **El tipo del item
ledger entry y del value entry determina a qué cuenta del mayor postear.**"*

### 9.3 ⚠️ El timing del bloque de costo — el punto que más se malinterpreta

**Cita literal y sin ambigüedad:** *"A menos que haya seleccionado la casilla **Automatic Cost Posting** en la
ventana **Inventory Setup**, los costos de inventario **no se registran dinámicamente** en el mayor general, y
**el COGS no se calcula con una venta**. Por tanto, debe postear al mayor manualmente ejecutando el batch job
**Post Inventory Cost to G/L**."*

Por tanto:

| | Al facturar | Al correr `Post Inventory Cost to G/L` |
| --- | --- | --- |
| `Sales Invoice Header/Line` | ✅ | — |
| `Cust. Ledger Entry` + detalle | ✅ | — |
| `Item Ledger Entry` + `Item Application Entry` | ✅ | — |
| `Value Entry` | ✅ (con `"Cost Posted to G/L" = 0`) | se actualiza `"Cost Posted to G/L"` |
| G/L: CxC / Ventas / IVA | ✅ | — |
| G/L: COGS / Inventario | ✅ **solo si `Automatic Cost Posting = true`** | ✅ en caso contrario |

Detalles del batch job:

- **Importe que postea:** `"Cost Amount (Actual)" − "Cost Posted to G/L"` (costo real) y
  `"Cost Amount (Expected)" − "Expected Cost Posted to G/L"` (costo esperado). Es idempotente por construcción:
  postea solo el delta.
- **Granularidad configurable:** un `G/L Entry` por value entry, o *"un general ledger entry por fecha de
  posteo por combinación de posting groups… para cada combinación de fecha de posteo, gen. business posting
  group, gen. product posting group, inventory posting group y location code. Además, el batch job crea general
  ledger entries separados para costos con signos distintos."*
- Las entries de períodos cerrados se omiten y se listan bajo **"Skipped Entries"**. Hay simulación
  (desmarcar **Post**, o el reporte `Post Invt. Cost to G/L - Test`) y auditoría
  (página **Inventory - G/L Reconciliation**).
- **Orden obligatorio:** `Adjust Cost - Item Entries` **antes** de `Post Inventory Cost to G/L`.

### 9.4 Costo esperado y cuentas interinas

Si se postea el **envío** sin facturar, *"se crea un value entry con el costo esperado. Este costo esperado
afecta el valor del inventario pero **no** se postea al mayor general a menos que se configure el sistema para
hacerlo."*

Con `Inventory Setup."Expected Cost Posting to G/L" = true`, *"el costo esperado se postea a **cuentas
interinas** en el momento de la recepción. Después de que la recepción se ha facturado completamente, las
cuentas interinas se saldan y el costo real se postea a la cuenta de inventario."*
Prerrequisitos: `"Automatic Cost Posting"` **y** `"Expected Cost Posting to G/L"` en `Inventory Setup`, más
`"Inventory Account (Interim)"` en `Inventory Posting Setup` y `"Invt. Accrual Acc. (Interim)"` en
`General Posting Setup`.

Mapa de cuentas para una **venta** (tabla oficial):

| `Item Ledger Entry Type` | `Value Entry."Entry Type"` | `"Expected Cost"` | Cuenta | Contrapartida |
| --- | --- | --- | --- | --- |
| `Sale` | `"Direct Cost"` | **Sí** | `Inventory Account (Interim)` | **`COGS Account (Interim)`** |
| `Sale` | `"Direct Cost"` | No | `Inventory Account` | `COGS Account` |
| `Sale` | `Revaluation` / `Rounding` | No | `Inventory Account` | `Inventory Adjmt. Account` |

Campos implicados en `Value Entry`: `"Expected Cost"` (Boolean), `"Cost Amount (Expected)"`,
`"Expected Cost Posted to G/L"`, `"Exp. Cost Posted to G/L (ACY)"`.

Nota: *"Los costos esperados solo se gestionan para transacciones de ítem. No se gestionan para tipos de
transacción inmateriales, como capacidad e item charges."* Y *"los costos de ensamble solo se postean como
costo real, nunca como costo esperado."*

> **Tres cuentas interinas en tres tablas distintas** — es un error clásico confundirlas:
> `"Inventory Account (Interim)"` está en `Inventory Posting Setup` (5813);
> `"COGS Account (Interim)"` está en `General Posting Setup` (252);
> `"Invt. Accrual Acc. (Interim)"` (devengo del lado compra) también está en `General Posting Setup` (252).

### 9.5 Inmutabilidad: qué está confirmado y qué no

Confirmado por la documentación oficial:

- El borrador **desaparece de la lista** y se sustituye por el documento posteado; *"cuando el posteo termina,
  las líneas de venta posteadas se eliminan del pedido."*
- Los documentos posteados **no son editables** salvo una lista blanca estrecha, vía una página aparte marcada
  con `"- Update"` en el título: *"Realiza el cambio en una versión editable del documento original… La página
  contiene un subconjunto de los campos del documento original."* Y: *"Para campos más críticos que afectan la
  pista de auditoría, debe revertir o deshacer el posteo."* (Nota: la factura de venta posteada es editable
  **solo en la localización de España**; en W1 hay que corregir/cancelar o emitir nota de crédito.)
- `Value Entry` declara `Permissions | TableData "Value Entry" = ri` → **read + insert, sin modify ni delete**.
- La configuración usada **no se puede borrar**: *"después de postear documentos, no se pueden eliminar
  posting groups o setups usados incorrectamente porque se crearon general ledger entries para ellos"* — hay
  que usar `Blocked`. Igual para el `VAT Posting Setup`.
- Los números del posteado salen de **series distintas** a las del borrador
  (`Sales & Receivables Setup` → `Posted Invoice Nos.`, `Posted Credit Memo Nos.`, …), y la tabla 112 conserva
  **ambas identidades** (`"No."` + `"No. Series"` del posteado, `"Pre-Assigned No."` + `"Pre-Assigned No. Series"`
  del borrador, `"Order No."`, `"Draft Invoice SystemId"`).

**Matiz importante, en honor a la exactitud:** *`Item Ledger Entry` declara `rimd`, es decir **no** es
insert-only.* Y debe serlo: `"Remaining Quantity"`, `Open`, `"Applied Entry to Adjust"` y
`"Completely Invoiced"` se mutan a medida que avanzan las aplicaciones y la facturación. Por tanto la frase
correcta no es "los libros de BC son append-only", sino: **la cantidad y el valor son inmutables; el estado de
aplicación es mutable, y es una caché derivable de las tablas de aplicación.**

También, para no sobrevender el patrón: ninguna página oficial afirma explícitamente que las tablas de
documento posteado sean "append-only por diseño" ni que las filas de `Sales Header`/`Sales Line` se *borren*
(la redacción de la UI dice "se eliminan de la lista"). La inmutabilidad real está respaldada por: la ausencia
de una ruta de edición, la lista blanca `"- Update"`, el permiso `ri` de `Value Entry` y la imposibilidad de
borrar los setups usados.
---

## 10. Comparación con SAP

> Nota metodológica: las páginas modernas de `help.sap.com/docs/...` son una SPA JavaScript y no se pueden leer
> automáticamente. La verificación a nivel de campo proviene de las páginas legacy
> `help.sap.com/doc/saphelp_*/.../content.htm`, de `learning.sap.com`, de los catálogos de campos de BW/extractores
> y de SAP Notes/KBAs públicos. Los pocos nombres de campo que no se pudieron anclar a una página oficial están
> marcados como tales.

### 10.1 Mapa de equivalencias

| Área | Business Central | SAP S/4HANA / ECC | SAP Business One |
| --- | --- | --- | --- |
| Factura de venta | `Sales Invoice Header` (112) / `Sales Invoice Line` (113) | **VBRK** (cabecera) / **VBRP** (posición) | `OINV` / `INV1` (ObjType 13) |
| Pedido / albarán | `Sales Header`/`Line` (36/37) + `Sales Shipment Header` (110) | **VBAK/VBAP** (pedido), **LIKP/LIPS** (entrega) | `ORDR`/`RDR1`, `ODLN`/`DLN1` (ObjType 15) |
| Flujo de documentos | campos embebidos (`Order No.`, `Shipment No.`, …) | **VBFA** — tabla de aristas genérica | `BaseType`/`BaseEntry`/`BaseLine` + `TargetType`/`TrgetEntry` en la **línea** |
| Maestro de artículo | `Item` (27) | **MARA** (cliente) + **MARC** (centro) + **MARD** (almacén) + **MBEW** (valoración) | `OITM` + `OITW` |
| Unidades de medida | `Item Unit of Measure` (5404) `Qty. per Unit of Measure` | **MARM** (`MEINH`, `UMREZ`/`UMREN`) + **T006/T006A** (dimensiones) | `OUOM`/`UGP1` |
| Almacén | `Location` (14) | **T001W** (centro) + `LGORT` (almacén) + área de valoración | `OWHS` |
| Movimientos de inventario | `Item Ledger Entry` (32) + `Value Entry` (5802) | **MKPF** (cabecera) / **MSEG** (posición) — en S/4HANA fusionadas en **MATDOC** | `OILM` (cantidad) + `OIVL` (valor); `OINM` es hoy una **vista** |
| Contabilidad | `G/L Entry` (17) + `G/L Register` (45) | **BKPF** (cabecera) / **BSEG** (posición) — en S/4HANA más **ACDOCA** (Universal Journal) | `OJDT` / `JDT1` |
| Plan de cuentas | `G/L Account` (15) | **SKA1**/**SKB1** | `OACT` (con `FatherNum` — jerarquía **real**) |
| Cliente | `Customer` (18) | **KNA1** (general) + **KNB1** (sociedad) + **KNVV** (área de ventas); en S/4HANA **BUT000** + roles | `OCRD` (¡clientes, proveedores **y** leads en una tabla, `CardType` C/S/L) |
| Cuenta de CxC del cliente | `Customer Posting Group."Receivables Account"` | **KNB1-AKONT** (cuenta de reconciliación) | `OCRD.DebPayAcct` |
| Determinación de cuenta de ingreso | `General Posting Setup` (Gen.Bus × Gen.Prod) | **VKOA / KOFI00**, técnica de condiciones: (KTOPL × VKORG × **KTGRD** × **KTGRM** × **KTOSL**) | Advanced G/L Account Determination (matriz de 15 criterios) |
| Determinación de cuenta de inventario | `Inventory Posting Setup` (Location × Invt. Posting Group) | **OBYC / T030**: (KTOPL × **BWMOD** × **KTOSL** × KOMOK × **BKLAS**) | `GLMethod` = W/C/L → `OWHS` / `OITB` / `OITM` |
| Clasificación del movimiento | `Item Ledger Entry."Entry Type"` — **enum fijo** | **BWART** (movement type) — **tabla configurable T156** | `TransType` (tipo de objeto origen) |
| Numeración | `No. Series` / `No. Series Line` | Objetos de rango de números (**SNRO**), intervalos en **NRIV**, con buffering opcional | `ONNM` / `NNM1` (`InitialNum`, `NextNumber`, `LastNum`) |
| Partida abierta / aplicación | `Open` + `Closed by Entry No.` + `Detailed Cust. Ledg. Entry` | **BSEG-AUGBL** (documento de compensación) + **AUGDT** | `OINV.DocStatus` + `ORCT`/`RCT2` |

### 10.2 Campos clave de las tablas SAP citadas

**VBRK** (Billing Document: Header) — PK `MANDT + VBELN`: `VBELN` nº factura · `FKART` clase de factura ·
`FKDAT` fecha de facturación · `KUNRG` pagador · `KUNAG` solicitante · `WAERK` moneda · `NETWR` valor neto ·
`MWSBK` importe de impuesto · `BUKRS` sociedad · `VKORG` organización de ventas ·
`RFBSK` **estado de traspaso a contabilidad**.

**VBRP** (Billing Document: Item) — PK `+ POSNR`: `MATNR` material · `FKIMG` cantidad facturada ·
`VRKME` unidad de venta · `NETWR` / `MWSBP` valor neto / impuesto de la posición · `WERKS` centro ·
`AUBEL`/`AUPOS` **pedido de venta origen** · `VGBEL`/`VGPOS` **documento precedente inmediato** (la entrega, si
la facturación es por entrega) · `PRSDT` fecha de precio/tipo de cambio.

> Ese desdoblamiento `AUBEL` vs `VGBEL` es una idea fina: un puntero **estable** al origen comercial y otro
> **al predecesor inmediato**, porque difieren según la ruta de facturación. BC solo tiene el segundo
> (`Sales Invoice Line."Shipment No."` / `"Order No."`).

**VBFA** (SD Document Flow) — el grafo: `VBELV`/`POSNV` predecesor · `VBELN`/`POSNN` sucesor ·
`VBTYP_V`/`VBTYP_N` **categoría** de documento de cada extremo · `STUFE` nivel/profundidad del salto ·
`RFMNG`/`RFWRT` **cantidad / valor efectivamente referenciados**.

**MARA / MARC / MARD / MBEW:**
`MARA` (nivel cliente): `MATNR`, `MTART` tipo de material, `MATKL` grupo, `MEINS` **unidad base**, `BRGEW` peso bruto.
`MARC` (`+WERKS` centro): `DISPO` planificador MRP, `BESKZ` tipo de aprovisionamiento.
`MARD` (`+LGORT` almacén): `LABST` **libre utilización**, `INSME` en control de calidad, `SPEME` bloqueado.
`MBEW` (`+BWKEY` área de valoración `+BWTAR`): `VPRSV` **control de precio** (`S` estándar / `V` medio móvil),
`STPRS` precio estándar, `VERPR` precio medio móvil, `PEINH` **unidad de precio**, `LBKUM` cantidad valorada
total, `SALK3` valor total.

> **Separación cantidad/valor, también en SAP.** `MARD` lleva cantidades, `MBEW` lleva valor. Es el mismo
> instinto que `Item Ledger Entry` / `Value Entry`, aplicado al maestro en vez de al movimiento. Y SAP añade
> `PEINH` (*price unit*): el precio se refiere a una cantidad, no a la unidad — evita la pérdida de precisión
> de "el costo de 1 litro es 0.0034". **BC no tiene equivalente y es una carencia real** para artículos baratos.

**MKPF / MSEG:**
`MKPF` — PK `MBLNR + MJAHR`: `BUDAT` fecha de contabilización, `BLDAT` fecha del documento, `XBLNR` referencia.
`MSEG` — PK `+ ZEILE`: `BWART` **movement type** · `MATNR`/`WERKS`/`LGORT` · `SHKZG` **indicador D/H**
(`S` = *Soll*/debe, `H` = *Haben*/haber) · `MENGE`/`MEINS` · `DMBTR`/`WAERS` · `SAKTO` cuenta de mayor ·
`CHARG` lote · `SOBKZ` **indicador de stock especial** · `KOSTL`/`KSTRG` imputación.

> `SHKZG` es una decisión de diseño distinta de la de BC: SAP guarda `MENGE` **sin signo** y lleva la dirección
> en un campo aparte; BC firma la cantidad (`Item Ledger Entry.Quantity` negativa = salida). El enfoque de BC
> es más simple para sumar (`SUM(Quantity)` es el stock); el de SAP es más explícito y evita errores de signo
> en reportes. Para un ERP propio, **firmar la cantidad** es la opción correcta, precisamente porque el saldo
> es una suma.

**BKPF / BSEG:**
`BKPF` — PK `BUKRS + BELNR + GJAHR`: `BLART` clase de documento · `BUDAT`/`BLDAT` · `WAERS` · `XBLNR` ·
**`AWTYP`** tipo de objeto originador (5 car.) · **`AWKEY`** clave de ese objeto (20 car.) · `AWSYS` sistema origen.
`BSEG` — PK `+ BUZEI`: `KOART` **tipo de cuenta** (`S` mayor/*Sachkonto*, `D` cliente/*Debitor*,
`K` proveedor/*Kreditor*, `A` activo fijo, `M` material) · `SHKZG` · `DMBTR` importe en moneda local ·
`WRBTR` importe en moneda del documento · `HKONT` cuenta contabilizada (en líneas `D`/`K` es la **cuenta de
reconciliación**) · `KUNNR`/`LIFNR` · `MATNR` · `MWSKZ` indicador de impuesto · `ZFBDT` fecha base de
vencimiento · `ZTERM` condiciones de pago · **`AUGBL` documento de compensación** · `AUGDT` fecha de
compensación · `ZUONR` clave de asignación.

**KNA1 / KNB1 / KNVV:** `KNA1` (`KUNNR`): `NAME1`, `STRAS`, `ORT01`, `LAND1`, `KTOKD` grupo de cuentas,
`STCEG` NIF. `KNB1` (`+BUKRS`): **`AKONT` cuenta de reconciliación**, `ZTERM`, `ZUAWA` **clave de
clasificación** (determina cómo se llena `BSEG-ZUONR` y por tanto cómo se ordenan/casan las partidas abiertas),
`MAHNA` procedimiento de reclamación. `KNVV` (`+VKORG+VTWEG+SPART`): **`KTGRD` grupo de imputación del
cliente** (entrada directa a la determinación de cuenta de ingreso), `KDGRP`, `KALKS`, `INCO1`, `VSBED`.

### 10.3 Compensación de partidas (SAP) vs. aplicación (BC)

Definición oficial de SAP: en una cuenta con gestión de partidas abiertas, *"las partidas deben ser compensadas
por otras partidas, la transacción de compensación debe netear a cero, y **el saldo de la cuenta es siempre la
suma de las partidas abiertas**"*. El mecanismo cabe en una frase: *"Durante la compensación, el sistema
introduce un **número de documento de compensación** y la **fecha de compensación** en esas partidas."*

```
SAP:    BSEG.AUGBL IS NULL   →  partida ABIERTA
        BSEG.AUGBL = 'X'     →  partida COMPENSADA por el documento X
        (N facturas contra M pagos = UN documento de compensación referenciado por todas las partidas)

BC:     Cust. Ledger Entry.Open (Boolean)  +  "Closed by Entry No."  (apunta a UNA entry)
        Las aplicaciones parciales / N:M van a Detailed Cust. Ledg. Entry (Entry Type = Application)
```

**El modelo de SAP es estructuralmente más limpio en este punto**: un FK anulable a un *documento* de
compensación expresa cualquier neteo N:M, y "abierta" es un **predicado derivado** (`AUGBL IS NULL`), no una
bandera denormalizada que puede desincronizarse del detalle. BC guarda `Open` **y** `Closed by Entry No.` **y**
las filas de detalle: tres representaciones del mismo hecho.

**Recomendación para un ERP propio:** modelar la compensación como una **entidad** (`Aplicacion` /
`ClearingDocument`) con líneas, y derivar `Abierta` de la ausencia de una aplicación que la sature. Si se guarda
una bandera `Open`, que sea explícitamente una caché reconstruible, con un job de verificación.

### 10.4 Las tablas de índice secundario: la lección que SAP ya aprendió y deshizo

En ECC las partidas se escribían **redundantemente** en tablas de índice secundario con un esquema de nombres
sistemático: `BS` + `I` (abierta/*offen*) o `A` (compensada/*ausgeglichen*) + `D`/`K`/`S`
(Debitor/Kreditor/Sachkonto):

| Tabla | Contenido |
| --- | --- |
| `BSID` / `BSAD` | Partidas de cliente abiertas / compensadas |
| `BSIK` / `BSAK` | Partidas de proveedor abiertas / compensadas |
| `BSIS` / `BSAS` | Partidas de cuenta de mayor abiertas / compensadas |

Compensar **era literalmente una migración de fila** de `BSI*` a `BSA*`.

**En S/4HANA ya no se persisten.** La documentación oficial de archivado lo dice sin rodeos: *"los índices
secundarios están implementados como una **vista** sobre BKPF y BSEG"*. Se convirtieron en *compatibility
views*: *"las peticiones de acceso a esa tabla son redirigidas por el sistema a la vista de compatibilidad… los
datos no se escriben en la tabla ni en la vista de compatibilidad, sino en las tablas nuevas que forman parte
de la vista de compatibilidad."* El ABAP antiguo `SELECT ... FROM BSIK` sigue compilando, pero es una vista.

> **Lección directa para este proyecto:** el almacenamiento columnar y los índices modernos hicieron
> innecesaria la redundancia que SAP mantuvo durante 20 años. **No construir tablas de "partidas abiertas"
> separadas de las "partidas cerradas".** Una tabla con un predicado indexado es mejor, más simple y no puede
> desincronizarse.

### 10.5 MATDOC y ACDOCA: la consolidación "sin agregados"

**MATDOC** (S/4HANA) sustituye a MKPF + MSEG por **una sola tabla** que contiene cabecera e ítem, discriminada
por `RECORD_TYPE` (`MDOC`, `MDOC_CP`, `MIG_DELTA`, …). La premisa oficial es exactamente el patrón #1 de §11:
***"Todos los datos de stock se calcularán a partir de la información de documentos de material almacenada en
una única tabla que se gestiona usando **solo operaciones INSERT**."*** Las tablas híbridas (MARC, MARD, MCHB,
MBEW) siguen existiendo pero **ya solo guardan datos maestros**: *"la cantidad mostrada en MARD-LABST para
stock de libre utilización es ahora el resultado de la suma de todos los documentos de material relacionados."*

El motivo declarado: el modelo antiguo tenía tablas de documento, **híbridas, agregadas e históricas** cuyas
cifras de stock eran redundantes, lo que producía reportes lentos y **contención de bloqueos por los UPDATE**.

⚠️ **Y la advertencia oficial, que es la parte que hay que interiorizar antes de copiar el patrón:** *"el
cálculo al vuelo es más lento que leer datos ya agregados… el rendimiento del cálculo de las cifras de stock es
**proporcional al número de registros** en la tabla de documentos de material."* Por eso SAP añade
`MATDOC_EXTRACT` (un subconjunto condensado) y un **precompactado** que corre automáticamente al cerrar período
(MMPV) o manualmente (`NSDM_MTDCSA_PRECOMP`).

**ACDOCA** (Universal Journal) hace lo mismo en contabilidad: una tabla de líneas que absorbe FAGLFLEX*, los
datos **reales** de COEP/COSS/COSP, el material ledger (MLIT) y activos fijos (ANEP), con ledger, sociedad,
cuenta, centro de costo, centro de beneficio, segmento, material, centro y **hasta 10 monedas** como columnas
de la misma fila. *"No se necesita reconciliación entre contabilidad financiera y controlling."*
Lo que sobrevive: *"la tabla BKPF que almacena la cabecera permanece sin cambios"* y *"la antigua tabla
BSEG sigue existiendo porque se necesita para almacenar los documentos fuente… además almacena las entradas
relativas a la gestión de partidas abiertas."* ACDOCA **no tiene clave primaria en base de datos** (por huella
de memoria y flexibilidad de particionado).

### 10.6 Determinación de cuentas: técnica de condiciones vs. matriz fija

**Lado ventas (SD).** SAP resuelve la cuenta de ingreso con la **técnica de condiciones**: un motor genérico
de búsqueda por clave múltiple con *fallback*, reutilizado para precios, cuentas, mensajes, textos y sustitución
de material. *"Una secuencia de acceso es una estrategia de búsqueda… La secuencia de los accesos establece qué
registros de condición tienen prioridad sobre otros. Los accesos le dicen al sistema dónde mirar primero,
segundo, etc., hasta que encuentra un registro de condición válido."*

Los cinco campos de determinación (literal de la documentación):

1. Plan de cuentas de la sociedad (`KTOPL`)
2. Organización de ventas (`VKORG`)
3. **Grupo de imputación del cliente** (`KTGRD`) — *"del maestro de clientes, pantalla Facturación, campo
   Grupo de cuentas"*
4. **Grupo de imputación del material** (`KTGRM`) — *"del maestro de materiales, pantalla Ventas 2"*
5. **Clave de cuenta del esquema de cálculo** (`KTOSL`)

Objetos: procedimiento **KOFI00** (asignado a la **clase de factura**), tipos de condición **KOFI** / **KOFK**
(KOFK = con objeto CO), secuencia de acceso **KOFI**, tablas de condición **C001–C005** (de la más específica
`Cust.Grp/MaterialGrp/AcctKey` a la más genérica `General`), customizing en **VKOA**.

**De dónde sale `KTOSL` — el matiz.** No es dato maestro: **el esquema de precios asigna una clave de cuenta a
cada tipo de condición** (`ERL` ingresos por ventas, `ERS` descuentos, `ERF` ingresos por flete, `MWS` impuesto
repercutido). El esquema de precios clasifica *qué clase de valor* transporta cada condición, y la determinación
de cuentas convierte esa clase en una cuenta.

```
BC:     General Posting Setup[ Gen.Bus × Gen.Prod ] . <columna>
         ↑ KTGRD             ↑ KTGRM                   ↑ KTOSL
SAP:    acceso 1 → C001 (KTOPL × VKORG × KTGRD × KTGRM × KTOSL)   ¿hay registro? → usar
        acceso 2 → C002 (KTOPL × VKORG × KTGRD × KTOSL)           ¿hay registro? → usar
        acceso 3 → C003 (KTOPL × VKORG × KTGRM × KTOSL)           ...
        acceso 4 → C005 / C004 (genéricas)
```

**La equivalencia exacta:** `KTGRD` ≈ `Gen. Bus. Posting Group`; `KTGRM` ≈ `Gen. Prod. Posting Group`; y
**`KTOSL` ≈ la *columna* de la fila de `General Posting Setup`** (`ERL` ≈ `"Sales Account"`,
`ERS` ≈ `"Sales Line Disc. Account"`, `MWS` ≈ la cuenta del `VAT Posting Setup`). SAP añade plan de cuentas y
organización de ventas a la clave, y resuelve por secuencia de acceso sobre cinco tablas de lo específico a lo
genérico, en vez de una única tabla plana. Además BC **no tiene validez por fechas** en la fila de setup, ni
traza de determinación.

**Lado materiales (MM).** Otro camino, dirigido por el *movement type*:

```
movement type (BWART)              → value string           (T156 / T156W)
value string                       → claves de transacción/evento (BSX, WRX, PRD, GBB…)
material (MBEW-BKLAS)              → clase de valoración
área de valoración / centro        → código de agrupación de valoración (BWMOD)
contexto de la operación           → modificación de cuenta / account grouping (p. ej. GBB/VBR)
──────────────────────────────────────────────────────────────────────────────────
T030 (vía OBYC):  KTOPL + BWMOD + KTOSL + KOMOK + BKLAS  →  cuenta debe + cuenta haber
```

SAP publica incluso la lectura literal:

```abap
SELECT SINGLE * FROM T030 WHERE KTOPL = <plan de cuentas>
  AND KTOSL = <clave de transacción>  AND BWMOD = <cód. agrupación de valoración>
  AND KOMOK = <modificación de cuenta>  AND BKLAS = <clase de valoración>
```

**Equivalencia:** `BKLAS` ≈ `Inventory Posting Group`; `BWMOD` (área de valoración) ≈ `Location Code`;
`KTOSL` ≈ qué columna (`BSX` ≈ `"Inventory Account"`, `WRX` ≈ la cuenta de devengo interina). Claves estándar:
`BSX` stock, `GBB` contrapartida del movimiento de stock (subdividida por *account grouping*: `VBR` consumo
interno, `VAX`/`VAY` salida por pedido de venta, `INV` diferencias de inventario, `VNG` desguace, `BSA` carga
inicial de stock), `WRX` cuenta puente GR/IR, `PRD` diferencias de precio, `KDM` diferencias de cambio.

El *value string* es la indirección que mantiene la configuración **independiente del plan de cuentas**:
*"contienen claves de transacción en lugar de números de cuenta concretos, permitiendo que distintas empresas
usen distintos planes de cuentas."*

**Diferencia estructural que importa:** en SAP, el **movement type decide cuántas líneas de contabilización y
qué claves de transacción se generan ANTES de buscar ninguna cuenta**. BC no tiene esa capa: el lado
contrapartida (COGS, ajuste de inventario) se elige *implícitamente* a partir del tipo de documento origen,
mientras SAP lo elige *explícitamente* desde el modificador de cuenta del movement type.

### 10.7 Movement types (BWART): el contraste más instructivo

Definición oficial: una clave de **tres dígitos** que identifica un movimiento de mercancías, obligatoria en
todo movimiento, y *"un parámetro de control central"*. Controla: la actualización de los campos de cantidad,
la actualización de las cuentas de stock y de consumo, y qué campos se muestran al capturar. Su customizing
vive en **T156** (+ T156T textos, T156C control de tipo de stock, **T156W** valores del *posting string*).

| BWART | Significado |
| --- | --- |
| 101 / 102 | Entrada de mercancía por pedido de compra / anulación |
| 201 / 202 | Salida a centro de costo / anulación |
| 261 / 262 | Salida para una orden (producción/mantenimiento) / anulación |
| 301 / 302 | Traslado centro → centro, un paso / anulación |
| 311 / 312 | Traslado almacén → almacén dentro del centro / anulación |
| 321 | Traslado de stock en control de calidad → libre utilización |
| 501 / 502 | Entrada **sin** pedido de compra / anulación |
| 561 / 562 | **Carga inicial de saldos de stock** (sin movimiento físico) / anulación |
| 601 / 602 | Salida por entrega de venta (601 es el movimiento por defecto de la entrega) / anulación |

**La convención de anulación** (*reversal = original + 1 en el último dígito*) SAP la enuncia como guía —
*"como regla, esto es válido para el movement type de anulación, donde anulación = movement type original + 1"*
— y advierte que al crear un movement type hay que crear y **enlazar** su anulación. Tiene excepciones
documentadas (542/544 en subcontratación). Los tipos nuevos deben empezar por 9/X/Y/Z y se crean **copiando**
uno estándar, de modo que el comportamiento se **hereda** en vez de programarse.

**Contra BC:** `Item Ledger Entry."Entry Type"` es un enum fijo (`Purchase`, `Sale`, `Positive Adjmt.`,
`Negative Adjmt.`, `Transfer`, `Consumption`, `Output`, …). No se puede añadir "Merma – siniestro asegurado"
como configuración: hay que extender el enum y tocar el código de posteo. Y la anulación es una ruta de código
(`Reversed`, `Reversed by Entry No.`), no un emparejamiento configurado.

### 10.8 SAP Business One: la versión "a escala" de las buenas ideas

B1 es el punto de comparación más útil para un ERP propio, porque resolvió los mismos problemas con un décimo
de la maquinaria:

- **Un solo maestro de socios de negocio.** `OCRD` con `CardType`: `C` cliente, `S` proveedor, `L` lead.
  Exactamente el modelo de Business Partner de S/4HANA, sin la infraestructura de roles.
- **Plan de cuentas con jerarquía real.** `OACT.FatherNum` (padre) + `Postable` (Y/N). Sin la lista plana con
  `Heading`/`Begin-Total`/`End-Total`/`Totaling` de BC.
- **Provenance en la línea, bidireccional.** Cada fila copiada lleva `BaseType`/`BaseEntry`/`BaseLine`
  (de dónde vino) y la fila origen recibe `TargetType`/`TrgetEntry` (a dónde fue). Más barato que VBFA, pero
  **solo admite un predecesor por línea**.
- **Cantidad y valor separados, igual que BC.** `OILM` (cantidad: `Quantity`, `EffectQty`, `ActionType` 1/2
  entrada/salida) y `OIVL` (valor: `InQty`/`OutQty`, `Price`, capas de costo). `OINM`, la tabla "clásica" de
  movimientos, **desde la 8.8 es una vista** sobre esas dos (+`OIVK` como mapa de claves) — otro caso de SAP
  deshaciendo su propia redundancia.
- **Determinación de cuentas en tres niveles simples.** `GLMethod` (en `OITM` por artículo, en `OADM` como
  defecto de empresa): `W` = por almacén (cuentas en `OWHS`), `C` = por grupo de artículos (cuentas en `OITB`),
  `L` = por artículo (cuentas en `OITM`). Las tres tablas usan **los mismos nombres de columna de cuenta**
  (`BalInvntAc`, `SaleCostAc`, `RevenuesAc`, `VarianceAc`, …), así que el resolutor es un `switch` de tres ramas.
- **Y la joya: Advanced G/L Account Determination** (`OADM.NewAcctDe = 'Y'`) — una técnica de condiciones
  *ligera* que un equipo pequeño sí puede construir: *"una matriz centralizada para determinar reglas de
  asignación de cuentas de mayor"*. 15 criterios activables (país de destino, grupo de artículos, código de
  artículo, almacén, grupo de socio, NIF, estado, código de impuesto, código de socio, tipo de socio y
  **UDF1–UDF5**); cada regla tiene **Prioridad**, **From Date / To Date**, `Active`, y cada columna de criterio
  admite el comodín **`All`**. Y lo más interesante: **la prioridad es derivada, no asignada a mano** — *"la
  regla que contiene el criterio de determinación de mayor prioridad se convierte en la regla de mayor
  prioridad… Entre dos reglas con los mismos criterios, la regla donde los criterios tienen valores específicos
  tiene precedencia sobre la regla donde están puestos en `All`."* Con *fallback* explícito a la pestaña de
  configuración general. Límite: 4.000 reglas.

### 10.9 Qué ideas de SAP vale la pena robar (y cuáles no)

| Idea de SAP | Qué le falta a BC | ¿Vale la pena en un ERP .NET pequeño? |
| --- | --- | --- |
| **Técnica de condiciones / secuencia de acceso** para determinar cuentas y precios: búsqueda por clave múltiple, de lo específico a lo genérico, con validez por fechas y traza de "por qué eligió esto". | Matriz fija bidimensional sin *fallback*, sin fechas de validez y sin traza. Añadir una tercera dimensión exige extender la tabla. | **SÍ — la de mayor valor de la lista, pero en la versión de B1, no la de S/4HANA.** Una tabla `ReglaDeterminacionCuenta` con una columna anulable por criterio (`NULL` = "todos"), `ValidoDesde`/`ValidoHasta`, `Activa`, una columna por rol de cuenta, y un resolutor que ordene por especificidad y devuelva la primera coincidencia, con *fallback* a una tabla de defectos. **~2–4 días**, más una pantalla de "explicar esta resolución" que **no es opcional**: sin ella el sistema es insoportable de soportar (por eso SAP envía el *account determination analysis*). Trampa a evitar: definir el orden de especificidad **declarativamente** (una prioridad por criterio, como B1), no dejar que emerja del orden de las filas. |
| **Movement types configurables**, con la anulación emparejada como dato. | `Entry Type` es un enum cerrado del producto; añadir una clase de movimiento es un despliegue. | **SÍ, pero solo la tabla, no la maquinaria de *strings*.** Una tabla `TipoMovimiento` (código, descripción, signo/dirección, exige-ubicación, exige-motivo, `TipoMovimientoAnulacionCode`, FK a un conjunto de reglas de contabilización) convierte "añadir un nuevo tipo de ajuste" en un cambio de datos, y la anulación en un `LOOKUP` en vez de un `if`. **~1–2 días** en greenfield y se paga de inmediato. **No** copiar los *value/quantity strings*: el propio SAP los hace fijos y no modificables, lo que delata que son codificación heredada, no diseño. |
| **ACDOCA / diario universal**: una tabla ancha de líneas con todas las dimensiones como columnas reales. | El posteo se abre en `G/L Entry` + `Value Entry` + `Item Ledger Entry` + `Cust. Ledger Entry` + detalle, y las dimensiones analíticas se normalizan a `Dimension Set ID` (con 8 columnas *shortcut* como parche de rendimiento). Conciliar mayor contra inventario es una tarea recurrente. | **SÍ, con disciplina.** En SQL Server/PostgreSQL con un equipo pequeño, una tabla `AsientoLinea` ancha y *append-only*, con las dimensiones como columnas anulables reales, gana a cinco tablas más una indirección de conjunto de dimensiones. Tres condiciones innegociables: (1) solo `INSERT`, las correcciones son filas nuevas; (2) una política explícita de adición de columnas para que no crezca a 300; (3) hacer caso a la advertencia de SAP — planificar una tabla de **saldos periódicos** *antes* de necesitarla, porque el costo de la agregación al vuelo es proporcional al nº de filas. |
| **`AWTYP`/`AWKEY`/`AWSYS`: puntero de procedencia genérico** en **toda** línea contable. | `Source Type`/`Source No.` existe pero es por tabla, se rellena de forma inconsistente y es semánticamente estrecho (`G/L Entry` tiene `Source Code` + `Document No.` + `Job No.` + `Prod. Order No.` …). No hay un "qué causó esta fila" uniforme. | **SÍ, y el día uno.** `(TipoObjetoOrigen, ClaveObjetoOrigen)` + un `CorrelationId` de la corrida de posteo en cada fila de libro cuesta dos columnas y se paga la primera vez que alguien pregunta "¿por qué esta cuenta tiene este saldo?". **Coste ~0 si se añade al inicio; doloroso de retrofitear**, porque rellenar procedencia que nunca se registró es adivinar. Mantener `TipoObjetoOrigen` como enum/lookup acotado (no un JSON). |
| **VBFA: flujo de documentos como grafo genérico** con cantidad/valor referenciados y nivel de salto. | Procedencia embebida por tabla; trazar una cadena exige saber qué tablas unir en qué orden, y cada tipo de documento nuevo añade rutas de join. | **SÍ — la mejor relación valor/líneas-de-código de la lista.** Una tabla `DocumentLink(TipoPredecesor, IdPredecesor, LineaPredecesor, TipoSucesor, IdSucesor, LineaSucesor, CantidadReferenciada, ValorReferenciado)`: una tabla y dos índices, y el flujo de documentos, la cantidad pendiente y el bloqueo en cascada pasan a ser **consultas** en vez de código a medida por cada par de documentos. **~1 día**; el único riesgo real es la disciplina de escribir la arista en la misma transacción que el documento sucesor. La alternativa barata de B1 (columnas `BaseType`/`BaseEntry`/`BaseLine` en la línea) es legítima pero limita a **un** predecesor por línea; la tabla de aristas no, y los CTE recursivos hacen trivial el recorrido. |
| **Business Partner único** (BUT000 + roles) — y **`OCRD` con `CardType` en B1**. | Tablas `Customer` y `Vendor` separadas con nombre/dirección/contacto/banco duplicados. Una entidad que es ambas cosas son dos registros, dos números y ningún vínculo forzado. | **SÍ, y es la decisión que hay que tomar MÁS TEMPRANO**, porque cambia todas las claves foráneas. Un `SocioNegocio` + `SocioNegocioRol` + tablas satélite por rol para lo genuinamente divergente (cuenta de control de CxC vs. CxP, datos de venta vs. de compra). **~1–2 días en greenfield; caro después.** Advertencia: mantener los datos **financieros de control** (cuenta de reconciliación, condiciones de pago, límite de crédito) estrictamente en el satélite del rol, o se recrea la duplicación de BC dentro de una sola tabla. |
| **Separación de niveles organizativos** (mandante / sociedad / centro / almacén / **área de valoración**). | Una empresa por base de datos + `Location`. No hay nivel "centro", ni área de valoración separada: el costeo es de toda la empresa y `Inventory Posting Setup` hace doble función con `Location`. | **MAYORMENTE NO.** La jerarquía de cinco niveles es sobreingeniería para una empresa única. **Pero una pieza sí vale**: separar *"dónde está físicamente el stock"* de *"a qué nivel se valora el stock"* — `UbicacionId` en las filas de inventario y un `GrupoValoracionId` **aparte** (que puede mapear N ubicaciones a 1 grupo) en las filas de costo. **Coste ~0 ahora, migración de datos después.** La advertencia de SAP es aleccionadora: el nivel de valoración *"no se puede cambiar de centro a sociedad o viceversa sin volver a realizar una conversión de datos"*. |
| **Material Ledger / valoraciones paralelas** (legal, de grupo, por centro de beneficio). | Un método de costeo por artículo, un valor de inventario, punto. | **NO construirlo.** Existe para precios de transferencia entre entidades legales bajo varios principios contables — un problema que un ERP de empresa única no tiene. El propio SAP advierte que *"la implementación posterior de enfoques de valoración múltiple en un sistema productivo aún no está soportada"*, lo que dice mucho de su complejidad. **La generalización barata que sí conviene:** poner la valoración como **columna** (`VistaValoracion`) en las filas de costo desde el principio, aunque solo se rellene con un valor. Así, "añadir una segunda valoración" es luego un problema de datos, no de migración de esquema. |
| **Objetos de rango de números (SNRO/NRIV) con buffering opcional.** | `No. Series` con `Implementation` `Normal`/`Sequence` — la misma disyuntiva, con menos granularidad de configuración. | **SAP NO resuelve esto mejor; es exactamente el mismo compromiso, y su propia documentación es tajante:** *"si hay un rollback, se crea un hueco en los números de documento"*, *"todos los números almacenados en el buffer se pierden si el servidor se apaga"*, y la conclusión: ***"las aplicaciones que no quieren huecos (o que no los tienen permitidos por razones legales) no deberían usar el buffer de rango de números."*** Es literalmente la decisión de `Allow Gaps in Nos.` de BC. **Lo que sí vale es la *forma*:** que el rango de números sea un **objeto configurable con buffering por tipo de documento**, de modo que la decisión se tome por documento y no globalmente — documentos legales (facturas, notas de crédito) con asignación serializada dentro de la transacción (`UPDATE ... OUTPUT`), y todo lo demás (cotizaciones, movimientos internos, actividades de almacén) con secuencia de base de datos y huecos aceptados. **~1 día.** |

**Si solo se implementan tres:** (1) la tabla de reglas de determinación de cuentas al estilo *Advanced G/L
Account Determination* de B1, (2) la tabla de aristas `DocumentLink`, y (3) `(TipoObjetoOrigen,
ClaveObjetoOrigen)` en toda fila de libro. Las tres son baratas ahora y caras de retrofitear. La decisión de
Socio de Negocio único es la cuarta en valor pero **la primera en el tiempo**, porque cambia todas las FKs.
---

## 11. Síntesis: los diez patrones portables a un ERP propio

Ordenados por relación valor/costo de implementación.

| # | Patrón | Qué evita | Costo de implementar |
| --- | --- | --- | --- |
| 1 | **Saldos derivados, no almacenados.** Stock = `SUM(ItemLedgerEntry.Quantity)`; saldo de cartera = `SUM(DetailedCustLedgEntry.Amount)`. Ninguna columna `Stock` ni `Balance`. | El descuadre saldo↔movimientos, la clase entera de bugs "el stock dice 5 pero los movimientos dicen 3". | Bajo. Vistas indexadas / tablas de resumen *derivadas y reconstruibles*, nunca autoritativas. |
| 2 | **Resolución de cuentas por matriz de posting groups.** Cero cuentas contables en documentos; se derivan de `(grupo socio × grupo producto)` y `(grupo inventario × ubicación)`. | Que cambiar el plan de cuentas exija tocar documentos; que el usuario elija una cuenta equivocada. | Bajo-medio. Tres tablas de intersección + un servicio resolutor que **falle con el nombre del campo vacío**. |
| 3 | **Separación cantidad / valor** (`ItemLedgerEntry` 1:N `ValueEntry`). | Mutar movimientos históricos cada vez que se conoce un costo nuevo. | Medio. Pero es lo que permite facturar sin conocer el costo real. |
| 4 | **Borrador mutable → posteado inmutable en tablas distintas**, con doble serie de numeración y `Pre-Assigned No.` como puente. | Que exista alguna ruta de código capaz de editar una factura emitida. | Bajo. Dos pares de tablas y permisos `INSERT`/`SELECT` en las posteadas. |
| 5 | **Congelar en la línea** el factor de UoM, el `VAT %`, los cuatro posting groups y el costo unitario. | Que un `JOIN` al maestro actual reescriba la historia. | Trivial, y no hacerlo es irreversible. |
| 6 | **Libro plano + índice de rangos** (`G/L Entry` + `G/L Register` con `[From, To] Entry No.`). | Una cabecera de asiento que hay que mantener consistente con sus líneas. | Bajo. Requiere `Entry No.` estrictamente creciente por transacción. |
| 7 | **`Transaction No.` transversal**: un mismo identificador en `G/L Entry`, `Cust. Ledger Entry`, etc., para todo lo creado por un posteo. | No poder responder "muéstrame todo lo que generó este posteo". | Trivial. |
| 8 | **IVA agregado por `VAT Identifier`, no por línea.** El IVA se calcula sobre la suma de las líneas con el mismo identificador. | Diferencias de céntimos contra la factura legal. | Bajo, si se hace desde el principio; caro de retrofitear. |
| 9 | **Detectar ≠ calcular** en el costeo: el posteo marca (`Applied Entry to Adjust`), un batch calcula (`Adjust Cost - Item Entries`). | Resolver costos FIFO/promedio dentro de la transacción de posteo. | Medio-alto. Es la pieza más compleja; se puede diferir usando solo costo promedio simple al inicio. |
| 10 | **Aplicación de pagos y de costos como grafo append-only** (`ItemApplicationEntry`, `DetailedCustLedgEntry` tipo `Application`) con `Open`/`Closed by Entry No.` como caché derivable. | `UPDATE` sobre partidas para "aplicar" un pago, sin rastro de quién lo aplicó. | Medio. |

### Lo que NO conviene copiar de BC

- **`Bin` / `Warehouse Entry`.** Un segundo libro de existencias paralelo al contable que debe cuadrar con él.
  Es la fuente de descuadres más común y no aporta nada sin necesidad física real. Quedarse en `Location`.
- **El plan de cuentas como lista plana** con `Heading`/`Begin-Total`/`End-Total` y `Totaling` textual.
  Es un árbol disfrazado; un `ParentId` + jerarquía real es más simple y más consultable.
- **`Entry Type` como enum cerrado del producto.** Ver §10: es la limitación de extensibilidad más citada de BC
  frente a los *movement types* de SAP. Si el dominio va a crecer, conviene una tabla de configuración
  `TipoMovimiento` desde el día uno (aunque arranque con seis filas).
- **`Shortcut Dimension 3..8` como columnas.** BC ya lo abandonó a favor de `Dimension Set ID`; las columnas
  siguen ahí por compatibilidad. Ir directo a un set de dimensiones.

### Orden de construcción sugerido (nivel mínimo viable)

```
 1. G/L Account (Posting/Heading/Total, Direct Posting) + G/L Entry + G/L Register
 2. Gen. Bus./Gen. Prod./VAT Bus./VAT Prod./Inventory Posting Group   ← vocabularios (Code, Description)
 3. General Posting Setup + VAT Posting Setup + Inventory Posting Setup + Customer Posting Group
    → servicio resolutor de cuentas, con excepción de dominio nombrada
 4. No. Series + No. Series Line (modo sin huecos; serie separada para borrador y posteado)
 5. Customer / Item / Item Unit of Measure / Location
 6. Item Journal (plantilla+batch+línea) → Item Ledger Entry + Value Entry + Item Register
    (arrancar con Costing Method = Average, período mensual, sin ajuste diferido)
 7. Sales Header/Line → Sales Invoice Header/Line + Cust. Ledger Entry + Detailed Cust. Ledg. Entry
    + las 5 patas de G/L Entry
 8. Aplicación de pagos (Applies-to Doc. No. y Applies-to ID)
 9. Item Application Entry + Adjust Cost (solo cuando haga falta FIFO/Standard)
```

---

## Fuentes

Toda la información de Business Central proviene de `learn.microsoft.com`. Las páginas de
`.../application/base-application/...` y `.../application/business-foundation/...` son la **referencia de
objetos generada desde el código AL**, y publican nombre y tipo de cada campo (no publican los números de
campo ni los ordinales de enum).

### Referencia de tablas y enums (Business Central)

| Objeto | URL |
| --- | --- |
| `Item` (27) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.item.item |
| `Item Unit of Measure` (5404) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.item.item-unit-of-measure |
| `Item Ledger Entry` (32) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.ledger.item-ledger-entry |
| `Value Entry` (5802) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.ledger.value-entry |
| `Item Application Entry` (339) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.ledger.item-application-entry |
| `Item Register` (46) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.ledger.item-register |
| `G/L - Item Ledger Relation` (5823) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.ledger.g-l---item-ledger-relation |
| `Location` (14) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.location.location |
| `Bin` (7354) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.warehouse.structure.bin |
| `Item Journal Line` (83) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.journal.item-journal-line |
| `Item Journal Batch` (233) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.journal.item-journal-batch |
| `Item Journal Template` (82) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.journal.item-journal-template |
| `Inventory Setup` (313) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.setup.inventory-setup |
| `Avg. Cost Adjmt. Entry Point` | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.costing.avg.-cost-adjmt.-entry-point |
| `Sales Header` (36) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.document.sales-header |
| `Sales Line` (37) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.document.sales-line |
| `Sales Invoice Header` (112) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.history.sales-invoice-header |
| `Sales Invoice Line` (113) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.history.sales-invoice-line |
| `Cust. Ledger Entry` (21) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.receivables.cust.-ledger-entry |
| `Detailed Cust. Ledg. Entry` (379) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.receivables.detailed-cust.-ledg.-entry |
| `Customer` (18) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.customer.customer |
| `Customer Posting Group` (92) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.sales.customer.customer-posting-group |
| `G/L Account` (15) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.generalledger.account.g-l-account |
| `G/L Entry` (17) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.generalledger.ledger.g-l-entry |
| `G/L Register` (45) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.generalledger.ledger.g-l-register |
| `General Posting Setup` (252) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.generalledger.setup.general-posting-setup |
| `VAT Posting Setup` (325) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.vat.setup.vat-posting-setup |
| `Inventory Posting Setup` (5813) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.item.inventory-posting-setup |
| `Gen. Business Posting Group` (250) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.generalledger.setup.gen.-business-posting-group |
| `Gen. Product Posting Group` (251) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.generalledger.setup.gen.-product-posting-group |
| `Inventory Posting Group` (94) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.inventory.item.inventory-posting-group |
| `VAT Business Posting Group` (323) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.vat.setup.vat-business-posting-group |
| `VAT Product Posting Group` (324) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/table/microsoft.finance.vat.setup.vat-product-posting-group |
| `No. Series` (308) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/business-foundation/table/microsoft.foundation.noseries.no.-series |
| `No. Series Line` (309) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/business-foundation/table/microsoft.foundation.noseries.no.-series-line |
| Enum `Costing Method` (28) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.inventory.item.costing-method |
| Enum `Item Ledger Entry Type` (78) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.inventory.ledger.item-ledger-entry-type |
| Enum `Cost Entry Type` (104) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.inventory.costing.cost-entry-type |
| Enum `Cost Variance Type` (106) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.inventory.costing.cost-variance-type |
| Enum `Item Journal Template Type` (83) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.inventory.journal.item-journal-template-type |
| Enum `Sales Document Type` (36) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.sales.document.sales-document-type |
| Enum `Sales Line Type` (37) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.sales.document.sales-line-type |
| Enum `Detailed CV Ledger Entry Type` (379) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.finance.receivablespayables.detailed-cv-ledger-entry-type |
| Enum `Gen. Journal Document Type` (6) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.finance.generalledger.journal.gen.-journal-document-type |
| Enum `G/L Account Type` (16) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.finance.generalledger.account.g-l-account-type |
| Enum `G/L Account Category` (15) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.finance.generalledger.account.g-l-account-category |
| Enum `General Posting Type` (57) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.foundation.enums.general-posting-type |
| Enum `Tax Calculation Type` (254) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/enum/microsoft.foundation.enums.tax-calculation-type |
| Enum `No. Series Implementation` (397) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/business-foundation/enum/microsoft.foundation.noseries.no.-series-implementation |
| Codeunit `Item Jnl.-Post Line` (22) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/codeunit/microsoft.inventory.posting.item-jnl.-post-line |
| Codeunit `Item Jnl.-Post Batch` (23) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/codeunit/microsoft.inventory.posting.item-jnl.-post-batch |
| Codeunit `No. Series` (310) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/business-foundation/codeunit/microsoft.foundation.noseries.no.-series |
| Page `Item Reclass. Journal` (393) | https://learn.microsoft.com/en-us/dynamics365/business-central/application/base-application/page/microsoft.inventory.journal.item-reclass.-journal |

### Serie "Design Details" (arquitectura de costeo y posteo)

- Inventory Costing (índice) — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-inventory-costing
- Costing Methods — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-costing-methods
- Average Cost — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-average-cost
- Cost Adjustment — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-cost-adjustment
- Item Application — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-item-application
- Inventory Posting — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-inventory-posting
- Cost Components — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-cost-components
- Expected Cost Posting — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-expected-cost-posting
- Accounts in the General Ledger — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-accounts-in-the-general-ledger
- Reconciliation with the General Ledger — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-reconciliation-with-the-general-ledger
- Variance — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-variance
- Rounding — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-rounding
- Inventory Adjustment / Value Entry Posting Date — https://learn.microsoft.com/en-us/dynamics365/business-central/design-details-inventory-adjustment-value-entry-posting-date

### Documentación funcional (conceptos y procedimientos)

- Posting group setup — https://learn.microsoft.com/en-us/dynamics365/business-central/finance-posting-groups
- Set up value-added tax — https://learn.microsoft.com/en-us/dynamics365/business-central/finance-setup-vat
- Understand the general ledger and Chart of Accounts — https://learn.microsoft.com/en-us/dynamics365/business-central/finance-general-ledger
- Post inventory costs to the general ledger — https://learn.microsoft.com/en-us/dynamics365/business-central/finance-how-to-post-inventory-costs-to-the-general-ledger
- Adjust item costs — https://learn.microsoft.com/en-us/dynamics365/business-central/inventory-how-adjust-item-costs
- Manage inventory costs — https://learn.microsoft.com/en-us/dynamics365/business-central/finance-manage-inventory-costs
- Count, adjust, and reclassify inventory — https://learn.microsoft.com/en-us/dynamics365/business-central/inventory-how-count-adjust-reclassify
- Post sales documents — https://learn.microsoft.com/en-us/dynamics365/business-central/ui-post-sales
- Invoice sales — https://learn.microsoft.com/en-us/dynamics365/business-central/sales-how-invoice-sales
- Edit posted documents — https://learn.microsoft.com/en-us/dynamics365/business-central/across-edit-posted-document
- Apply sales transactions manually (aplicar pagos) — https://learn.microsoft.com/en-us/dynamics365/business-central/receivables-how-apply-sales-transactions-manually
- Create number series — https://learn.microsoft.com/en-us/dynamics365/business-central/ui-create-number-series
- Number sequences in Business Central (dev) — https://learn.microsoft.com/en-us/dynamics365/business-central/dev-itpro/developer/devenv-number-sequences
- Lock-free number series (release plan 2019 W2) — https://learn.microsoft.com/en-us/previous-versions/dynamics365-release-plan/2019wave2/dynamics365-business-central/lock-free-number-series
- Allow non-sequential numbering (release plan 2019 W2) — https://learn.microsoft.com/en-us/previous-versions/dynamics365-release-plan/2019wave2/dynamics365-business-central/allow-non-sequential-numbering

### SAP — SAP Help Portal y SAP Learning (todas oficiales salvo lo indicado)

**Documentos de venta y flujo de documentos**
- VBRK — Billing Document: Header Data — https://help.sap.com/docs/SAP_PROFITABILITY_PERFORMANCE_MANAGEMENT/7fa13890d47b4c69bbb62175e84e4aa8/86fa003717be4560b37a1a80a8dd6217.html
- VBRP — Billing Document Items — https://help.sap.com/docs/SAP_PROFITABILITY_PERFORMANCE_MANAGEMENT/7fa13890d47b4c69bbb62175e84e4aa8/7f423ce47a6c45dcadd4d02748f9dbf8.html
- Extracción SD 2LIS_13_VDHDR (campos VBRK) — https://help.sap.com/doc/saphelp_nw74/7.4.16/en-US/18/8595d918904438993b95484269d4a2/content.htm
- Extracción SD 2LIS_13_VDITM (campos VBRP) — https://help.sap.com/doc/saphelp_nw74/7.4.16/en-US/42/9a54a7a7e541b1b5f8036979540339/content.htm
- VBFA — SD Document Flow — https://help.sap.com/docs/SAP_PROFITABILITY_PERFORMANCE_MANAGEMENT/7fa13890d47b4c69bbb62175e84e4aa8/cf4208e5d31947b89bf4fb4787955d27.html
- VBFA lista de campos (VBELV/POSNV/VBTYP_*/RFMNG/RFWRT) — https://help.sap.com/docs/BI_CONTENT_757/39f153060fa34c0185d35b071c49c2b4/28367e53f33d6359e10000000a174cb4.html
- Document Flow — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/29b076e23a9f4b0b9d41c5af9c6f7da0/5b2b9cedb9ac455dbe219701e556d98d.html

**Maestro de materiales y unidades de medida**
- Materials Management Tables — https://help.sap.com/docs/SUPPORT_CONTENT/spmm/3362168098.html
- Stock Tables and Stock Types (MARD LABST/INSME/SPEME; MBEW; BWKEY) — https://help.sap.com/docs/SUPPORT_CONTENT/erpscm/3362167795.html
- MBEW — Material Valuation — https://help.sap.com/docs/SAP_PROFITABILITY_PERFORMANCE_MANAGEMENT/7fa13890d47b4c69bbb62175e84e4aa8/9d4b5cb5d7dd4cec8295ed615001e64e.html
- Price Control (S vs V, price unit `PEINH`) — https://help.sap.com/doc/saphelp_scm700_ehp02/7.0.2/en-US/40/ca8853630b3d58e10000000a174cb4/content.htm
- Alternative Units of Measure (MARM: MEINH/UMREZ/UMREN) — https://help.sap.com/docs/SAP_ERP/bfece09273bd474d82fdd97bae070c25/477cb6535fe6b74ce10000000a174cb4.html
- Units of Measurement (T006/T006A, CUNI) — https://help.sap.com/doc/saphelp_nw74/7.4.16/en-US/48/e0a5d99f6a41a3e10000000a42189c/content.htm

**Documentos de material y movement types**
- **The Movement Type Concept** — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/91b21005dded4984bcccf4a69ae1300c/1663bd534f22b44ce10000000a174cb4.html
- Material Movements from IM (campos MKPF + MSEG) — https://help.sap.com/docs/BI_CONTENT_757/d1e4c9f0ffc047ec9f945e64026ffab1/e4497053bbe77c1ee10000000a174cb4.html
- Cancellation of Goods Movements (102, 542, 544) — https://help.sap.com/docs/SAP_ERP/cfff13ea14f3430488af41bbc02dbb69/7bdbc353b677b44ce10000000a174cb4.html
- Setting Up Movement Types (SAP Learning) — https://learning.sap.com/courses/inventory-management-and-physical-inventory-in-sap-s-4hana/setting-up-movement-types-1
- Adjusting Settings for Goods Movements (regla "anulación = original + 1") — https://learning.sap.com/courses/cross-functional-customizing-in-sap-s-4hana-materials-management/adjusting-settings-for-goods-movements

**NSDM / MATDOC**
- New Simplified Data Model (NSDM) for Inventory Management Tables — https://help.sap.com/docs/SUPPORT_CONTENT/erpscm/3362167285.html
- Archiving Material Documents ("una única tabla MATDOC en lugar de MKPF y MSEG") — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/677f0a4e71d7487ebb70683014761789/75bcb6531de6b64ce10000000a174cb4.html
- Purging and Precompacting of Material Document Data — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/677f0a4e71d7487ebb70683014761789/7a29ed568b0c41828a4e2c8da9ae1082.html

**Contabilidad: BKPF / BSEG / compensación / índices secundarios**
- BKPF lista de campos (AWTYP/AWKEY/AWSYS) — https://help.sap.com/docs/SAP_PROFITABILITY_PERFORMANCE_MANAGEMENT/7fa13890d47b4c69bbb62175e84e4aa8/4b340289e9634b7486c3abf02a853a8f.html
- BSEG lista de campos — https://help.sap.com/docs/SAP_PROFITABILITY_PERFORMANCE_MANAGEMENT_CLOUD/299cc31818394e94bf54da6fac6ffcdc/dcb1dfb9dd41438dba27a4bd2bd00d0f.html
- AWTYP/AWKEY → documento origen (BWFIU_GET_DOCUMENT_ORIGIN) — https://help.sap.com/doc/saphelp_nw74/7.4.16/en-US/67/ad8953b97a3d58e10000000a174cb4/content.htm
- **Clearing** ("el sistema introduce el nº de documento de compensación y la fecha") — https://help.sap.com/docs/SAP_ERP/c587d5cadece40a285ce9a6d4a2a4908/2884c353b677b44ce10000000a174cb4.html
- Open Item Management — https://help.sap.com/docs/SAP_ERP/17ec785ed2294431b933daf9a926af80/fdcbae52f0ecc04ae10000000a44176d.html
- Archivado FI ("los índices secundarios están implementados como una **vista** sobre BKPF y BSEG") — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/8fbeed5f2046489696a50ac7fd76f9c6/84e8265313b9383ee10000000a423f68.html
- AR/AP Line Items (BSID/BSAD, BSIK/BSAK: AUGBL/AUGDT/ZUONR) — https://help.sap.com/docs/BI_CONTENT_757/a8bf810ad0e94bb4a6527e93881dab18/497b895360b93d58e10000000a174cb4.html
- KBA 3523400 — vistas de compatibilidad en S/4HANA (NSDM_V_MKPF, NSDM_V_MSEG, BSID/BSAD/…) — https://userapps.support.sap.com/sap/support/knowledge/en/3523400

**ACDOCA / Universal Journal**
- **Universal Journal: FAQ** — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/3cb1182b4a184bdd93f8d62e3f1f0741/8b8e5695c4dc4749a706f9fa2f6bda92.html
- ACDOCA lista de campos — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/b9277744b8d84c74a15f185faa990e56/9e83bd572801bc38e10000000a44147b.html
- Extension Ledger — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/651d8af3ea974ad1a4d74449122c620e/f2a1f5756fa148c8ae8ec338a2653fad.html

**Cliente / Business Partner**
- **Business Partner Approach (Customer/Vendor Integration)** — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/74b0b157c81944ffaac6ebc07245b9dc/25b46c8241fd4852bf7876d87bed8fd0.html
- Customer master data (KNA1/KNB1/KNVV) — https://help.sap.com/docs/SUPPORT_CONTENT/sd/3362915401.html
- AKONT "Reconciliation Account in General Ledger" — https://help.sap.com/docs/BI_CONTENT_757/94bc4f8e196f451dbe80c55a7cbcbf2b/6ee493a1fe5744f883f82ce11b81b484.html
- KTGRD "Account assignment group" (KNVV) — https://help.sap.com/docs/BI_CONTENT_757/45e4eda3753c458ead20c81f77c55921/e86b7053bbe77c1ee10000000a174cb4.html

**Determinación de cuentas**
- Condition Technique — https://help.sap.com/docs/SAP_ERP/967e1c2a6a8c4183b7e07d28e7574445/db7fb65334e6b54ce10000000a174cb4.html
- Access Sequences — https://help.sap.com/docs/SAP_ERP/04ed152d92884a6da49c778a13aecb21/b78dc95360267214e10000000a174cb4.html
- **Working with Account Assignment** (los cinco campos de determinación) — https://help.sap.com/doc/2f92d0533f8e4308e10000000a174cb4/700_SFIN20%20006/en-US/a270b6535fe6b74ce10000000a174cb4.html
- ERP SD Account Determination (KOFI00, KOFI/KOFK, C001–C005, claves ERL/ERS/MWS, VKOA) — https://help.sap.com/docs/SUPPORT_CONTENT/sd/3362915200.html
- Performing an Account Determination Analysis — https://help.sap.com/docs/SAP_S4HANA_ON-PREMISE/7b24a64d9d0941bda1afa753263d9e39/ee9cc353fad0b44ce10000000a174cb4.html
- **Error M8147** (cadena OX14/OMWM/OMWD/OMWN/OMSK/OBYC + el `SELECT` literal sobre T030) — https://help.sap.com/docs/SUPPORT_CONTENT/spmm/3362168039.html
- Describing / Setting Up Automatic Account Determination (BSX, WRX, GBB, PRD, KDM; value strings) — https://learning.sap.com/courses/cross-functional-customizing-in-sap-s-4hana-materials-management/setting-up-account-determination-for-specific-transactions
- Subdividing a Transaction with the Account Grouping Code (GBB/VBR/VAX/INV/VNG/BSA) — https://learning.sap.com/courses/cross-functional-customizing-in-sap-s-4hana-materials-management/subdividing-a-transaction-with-the-account-grouping-code

**Niveles organizativos, valoraciones paralelas, rangos de números**
- Valuation Areas — https://help.sap.com/docs/SAP_S4HANA_CLOUD/f86dc2eb1f8b48c880a7607213104b27/75484ede382a419da5cdee501e279d18.html
- Multiple Valuation Approaches / Transfer Prices — https://help.sap.com/docs/SAP_ERP/17ec785ed2294431b933daf9a926af80/0259bf53d25ab64ce10000000a174cb4.html
- **How the Number Range Buffer Works** (NRIV; "si hay un rollback, se crea un hueco") — https://help.sap.com/doc/saphelp_nw73ehp1/7.31.19/en-US/47/d5659d167e3c84e10000000a42189c/content.htm

**SAP Business One**
- SDK 10.0 Database Tables Reference — https://help.sap.com/doc/089315d8d0f8475a9fc84fb919b501a3/10.0/en-US/SDKHelp/Table_Overview.htm
  (páginas individuales de `OITM`, `OITW`, `OWHS`, `OITB`, `OIVL`, `OILM`, `OIVK`, `OITL`, `OINV`, `INV1`, `ODLN`, `DLN1`, `OCRD`, `CRD1`, `OJDT`, `JDT1`, `OACT`, `ONNM`, `NNM1`, `OVTG`, `OADM`)
- **How to Set Up Advanced G/L Account Determination** (15 criterios, prioridad derivada, comodín `All`, validez por fechas, *fallback*) — https://help.sap.com/doc/d696f18779e04ed1808acaf7147f13b5/9.3/en-US/How_to_Set_Up_Advanced_GL_Account_Determination.pdf
- G/L Account Determination — https://help.sap.com/docs/SAP_BUSINESS_ONE/68a2e87fb29941b5bf959a184d9c6727/4505bc7324a70489e10000000a155369.html
- General Settings: Inventory Tab ("Set G/L Accounts By") — https://help.sap.com/docs/SAP_BUSINESS_ONE/68a2e87fb29941b5bf959a184d9c6727/450706f5ae742461e10000000a1553f7.html

**Fuentes NO oficiales (comunidad), usadas solo en tres puntos y marcadas como tales**
- Que `OINM` es una **vista** sobre `OIVL`/`IVL1`/`OILM` desde B1 8.8 — https://community.sap.com/t5/enterprise-resource-planning-blog-posts-by-members/sap-business-one-tables/ba-p/13280709 (ninguna página de help.sap.com documenta `OINM`; el título oficial de `OIVK`, *"IVL Vs OINM Keys"*, lo corrobora)
- Columnas de `OINM` anteriores a 8.8 — https://erpref.com/BusinessOne8.8/Table/Detail/OINM
- Nombres `NSDM_MIG_*` y el fraseo "hasta tres vistas de valoración" — https://community.sap.com/t5/enterprise-resource-planning-blog-posts-by-sap/sap-s-4hana-inventory-management-tables-new-simplified-data-model-nsdm/ba-p/13497469

**Nombres de campo SAP que NO se pudieron anclar a una página oficial** (declarados aquí para no darlos por
ciertos): `MSEG-KONTO` (parece no existir; el campo documentado de cuenta de mayor es **`SAKTO`**),
`VBRK-MWSBK` (inferido por simetría con `VBRP-MWSBP`, que sí está documentado), `ACDOCA-DOCLN` como nombre
literal, `V_BSEG` (casi con seguridad no existe: BSEG sigue siendo tabla real), `KTGRM` como nombre literal de
campo (la documentación dice "grupo de imputación del material" / "AcctAssmtGrpMat"; **`KTGRD` sí** está
nombrado oficialmente), `VBFA-PLMIN`, y los códigos de letra `S`/`H` de `SHKZG` (la *semántica* debe/haber es
oficial; las letras son convencionales).

---

## Apéndice: correcciones a suposiciones frecuentes

Recogidas durante la investigación; cada una es un error que se cometería al modelar "de memoria".

| Suposición habitual | Realidad documentada |
| --- | --- |
| `Item Journal Template` es la tabla 232 | Es la **82**. (233 es el batch, 83 la línea.) |
| `Item Ledger Entry` tiene `"Cost is Adjusted"` | **No.** Ese campo está en `Item` (27) y en `Avg. Cost Adjmt. Entry Point`. La bandera del movimiento es `"Applied Entry to Adjust"`. |
| `No. Series Line` tiene `"Allow Gaps in Nos."` | Ya **no** es un campo publicado; fue sustituido por el enum `Implementation` (`Normal` / `Sequence`). La UI sigue llamándolo así. |
| `Average Cost Calc. Type` = "Item & Location & Variant" | Se escribe **`"Item, Variant, and Location"`**. |
| `Average Cost Period` incluye `Year` | **No** en ninguna página oficial. `Year` pertenece a `"Automatic Cost Adjustment"`. Las dos páginas oficiales discrepan sobre si ofrece `Quarter`. |
| El ajuste de costo escribe value entries de tipo `Revaluation` | No respaldado. Lo que escribe es `"Direct Cost"`, `Rounding` y `Variance`. La revaluación es una acción aparte del usuario. |
| Hay un `Entry Type` = "Cost Adjustment" | No existe. Los ajustes se identifican por el booleano `Adjustment`. |
| `Inventory Posting Setup` está en el namespace `...Setup` | Está en **`Microsoft.Inventory.Item`**. Y su clave empieza por `"Location Code"`. |
| `Cust. Ledger Entry` tiene `"Payment Discount %"` | **No.** El porcentaje vive en `Payment Terms`; la partida guarda solo importes. |
| `Detailed Cust. Ledg. Entry."Ledger Entry Amount"` es un importe | Es un **Boolean** (bandera "esta fila afecta el saldo"). |
| `Customer.Blocked` es booleano | Es el enum `"Customer Blocked"` (`" "` / `Ship` / `Invoice` / `All`). |
| `Sales Header` tiene `"Shipping Method Code"` | Se llama `"Shipment Method Code"`. |
| `Sales Invoice Line` tiene `"Currency Code"` | **No**; se obtiene de la cabecera vía `GetCurrencyCode()`. |
| Las tablas posteadas tienen `Document Type` | **No.** La PK de `Sales Invoice Header` es `No.` a secas; el discriminador se convirtió en la identidad de la tabla. |
| Los libros de BC son todos append-only | `Value Entry` declara permisos `ri` (solo lectura+inserción); `Item Ledger Entry` declara `rimd` — su estado de aplicación **sí** se muta. |
| El COGS se contabiliza siempre al facturar | Solo si `Inventory Setup."Automatic Cost Posting" = true`. Si no, espera al batch `Post Inventory Cost to G/L`. |
| El IVA se calcula línea por línea | Se calcula **sobre la suma de las líneas con el mismo `"VAT Identifier"`** del documento. |
| `VAT Business Posting Group` y `VAT Product Posting Group` nombran igual su campo de auditoría | 323 usa `"Last Modified Date Time"` (tres palabras), 324 usa `"Last Modified DateTime"` (dos). |
