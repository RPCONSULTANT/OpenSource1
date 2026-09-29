#!/usr/bin/env python3
"""Modelo ER (Mermaid) y diccionario de datos desde el esquema REAL de PostgreSQL (information_schema).

Uso (desde la raíz del repositorio, con el stack de Docker levantado):
    python3 tests/e2e/scripts/esquema_a_mermaid.py --salida docs/entregable-parte-1/diagramas

Genera modelo-er.mmd (núcleo), el núcleo partido en dos vistas (GRUPOS_ER), una vista de claves por dominio
(GRUPOS_ER_DOMINIO, legible a 16 cm), modelo-er-completo.mmd
(todas las tablas de AxionERP_App) y diccionario-datos.md.
"""
import argparse
import json
import os
import subprocess
from pathlib import Path

TABLAS_NUCLEO = {
    "AxionERP_App": [
        "SociosNegocio", "TerminosPago", "Productos", "CategoriasProducto", "UnidadesMedida", "Almacenes",
        "MovimientosProducto", "MovimientosValor", "FacturasVentaBorrador", "LineasFacturaVentaBorrador",
        "FacturasVenta", "LineasFacturaVenta", "LineasIvaFacturaVenta", "NotasCreditoVenta", "LineasNotaCreditoVenta",
        "MovimientosCliente", "MovimientosClienteDetalle",
    ],
    "AxionERP_Identity": ["AspNetUsers", "AspNetRoles", "AspNetUserRoles"],
}

# El núcleo completo no se lee a ancho de página: se parte en dos vistas. SociosNegocio se repite en ventas y CxC
# como ancla de las relaciones de cliente.
GRUPOS_ER = {
    "modelo-er-maestros-inventario": [
        "SociosNegocio", "TerminosPago", "Productos", "CategoriasProducto", "UnidadesMedida", "Almacenes",
        "MovimientosProducto", "MovimientosValor", "AspNetUsers", "AspNetRoles", "AspNetUserRoles",
    ],
    "modelo-er-ventas-cxc": [
        "SociosNegocio", "FacturasVentaBorrador", "LineasFacturaVentaBorrador", "FacturasVenta", "LineasFacturaVenta",
        "LineasIvaFacturaVenta", "NotasCreditoVenta", "LineasNotaCreditoVenta", "MovimientosCliente",
        "MovimientosClienteDetalle",
    ],
}

# Vistas legibles a ancho de página (16 cm): una por dominio, solo con las columnas clave (PK/FK); el detalle de todas
# las columnas va en diccionario-datos.md. SociosNegocio y Productos se repiten como anclas de las relaciones.
GRUPOS_ER_DOMINIO = {
    "modelo-er-usuarios-roles": ["AspNetUsers", "AspNetRoles", "AspNetUserRoles"],
    "modelo-er-productos-inventario": [
        "CategoriasProducto", "UnidadesMedida", "Productos", "Almacenes", "MovimientosProducto", "MovimientosValor",
    ],
    "modelo-er-clientes-cxc": ["TerminosPago", "SociosNegocio", "MovimientosCliente", "MovimientosClienteDetalle"],
    "modelo-er-ventas-borradores": ["SociosNegocio", "Productos", "FacturasVentaBorrador", "LineasFacturaVentaBorrador"],
    "modelo-er-ventas-facturas": ["SociosNegocio", "Productos", "FacturasVenta", "LineasFacturaVenta", "LineasIvaFacturaVenta"],
    "modelo-er-ventas-notas-credito": ["SociosNegocio", "Productos", "NotasCreditoVenta", "LineasNotaCreditoVenta"],
}

SQL_COLUMNAS = """
SELECT coalesce(json_agg(t ORDER BY t.table_name, t.ordinal_position), '[]')
FROM (SELECT table_name, column_name, data_type, is_nullable, ordinal_position,
             character_maximum_length, numeric_precision, numeric_scale
      FROM information_schema.columns WHERE table_schema = 'public') t;
"""

SQL_RESTRICCIONES = """
SELECT coalesce(json_agg(t), '[]')
FROM (SELECT tc.table_name, kcu.column_name, tc.constraint_type, ccu.table_name AS ref_table, ccu.column_name AS ref_column
      FROM information_schema.table_constraints tc
      JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema
      LEFT JOIN information_schema.constraint_column_usage ccu
        ON tc.constraint_type = 'FOREIGN KEY' AND ccu.constraint_name = tc.constraint_name AND ccu.table_schema = tc.table_schema
      WHERE tc.table_schema = 'public' AND tc.constraint_type IN ('PRIMARY KEY', 'FOREIGN KEY')) t;
"""


def consultar(base, sql):
    """Ejecuta SQL dentro del contenedor postgres del compose (DOCKER_CONTEXT=default solo en el entorno del proceso)."""
    usuario = os.environ.get("POSTGRES_USER", "Rainiery")
    entorno = dict(os.environ, DOCKER_CONTEXT="default")
    salida = subprocess.run(
        ["docker", "compose", "exec", "-T", "postgres", "psql", "-U", usuario, "-d", base, "-At", "-c", sql],
        check=True, capture_output=True, text=True, env=entorno)
    return json.loads(salida.stdout.strip() or "[]")


def _claves(restricciones):
    claves = {}
    for r in restricciones:
        clave = (r["table_name"], r["column_name"])
        claves.setdefault(clave, set()).add("PK" if r["constraint_type"] == "PRIMARY KEY" else "FK")
    return claves


def _tipo(data_type):
    return data_type.replace(" ", "_").replace("(", "").replace(")", "")


def _tipo_completo(c):
    """Tipo con longitud o precisión cuando la tiene: character varying(200), numeric(18,4)."""
    if c.get("character_maximum_length"):
        return f'{c["data_type"]}({c["character_maximum_length"]})'
    if c["data_type"] == "numeric" and c.get("numeric_precision"):
        return f'numeric({c["numeric_precision"]},{c.get("numeric_scale") or 0})'
    return c["data_type"]


def construir_mermaid(columnas, restricciones, tablas, solo_claves=False):
    incluidas = list(dict.fromkeys(tablas))
    conjunto = set(incluidas)
    claves = _claves(restricciones)
    lineas = ["erDiagram"]
    for tabla in incluidas:
        filas = sorted((c for c in columnas if c["table_name"] == tabla), key=lambda c: c["ordinal_position"])
        if not filas:
            continue
        lineas.append(f"  {tabla} {{")
        for c in filas:
            if solo_claves and (tabla, c["column_name"]) not in claves:
                continue
            marcas = sorted(claves.get((tabla, c["column_name"]), set()), key=lambda m: 0 if m == "PK" else 1)
            sufijo = f" {', '.join(marcas)}" if marcas else ""
            lineas.append(f"    {_tipo(c['data_type'])} {c['column_name']}{sufijo}")
        lineas.append("  }")
    vistas = set()
    for r in restricciones:
        if r["constraint_type"] != "FOREIGN KEY":
            continue
        if r["table_name"] not in conjunto or r["ref_table"] not in conjunto:
            continue
        relacion = (r["ref_table"], r["table_name"], r["column_name"])
        if relacion in vistas:
            continue
        vistas.add(relacion)
        lineas.append(f'  {r["ref_table"]} ||--o{{ {r["table_name"]} : "{r["column_name"]}"')
    return "\n".join(lineas) + "\n"


def construir_diccionario(columnas, restricciones, tablas):
    claves = {}
    for r in restricciones:
        clave = (r["table_name"], r["column_name"])
        texto = "PK" if r["constraint_type"] == "PRIMARY KEY" else f'FK → {r["ref_table"]}.{r["ref_column"]}'
        claves.setdefault(clave, []).append(texto)
    partes = ["# Diccionario de datos (núcleo)", "", "Generado desde `information_schema` por `tests/e2e/scripts/esquema_a_mermaid.py`.", ""]
    for tabla in dict.fromkeys(tablas):
        filas = sorted((c for c in columnas if c["table_name"] == tabla), key=lambda c: c["ordinal_position"])
        if not filas:
            continue
        partes += [f"### {tabla}", "", "| Columna | Tipo | Nulo | Clave |", "| - | - | - | - |"]
        for c in filas:
            nulo = "Sí" if c["is_nullable"] == "YES" else "No"
            clave = ", ".join(sorted(set(claves.get((tabla, c["column_name"]), [])), key=lambda k: 0 if k == "PK" else 1))
            partes.append(f'| {c["column_name"]} | {_tipo_completo(c)} | {nulo} | {clave} |')
        partes.append("")
    return "\n".join(partes)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--salida", required=True, type=Path)
    args = parser.parse_args()
    args.salida.mkdir(parents=True, exist_ok=True)

    columnas, restricciones, nucleo = [], [], []
    for base, tablas in TABLAS_NUCLEO.items():
        columnas += consultar(base, SQL_COLUMNAS)
        restricciones += consultar(base, SQL_RESTRICCIONES)
        nucleo += tablas

    (args.salida / "modelo-er.mmd").write_text(construir_mermaid(columnas, restricciones, nucleo), encoding="utf-8")
    for nombre, tablas in GRUPOS_ER.items():
        (args.salida / f"{nombre}.mmd").write_text(construir_mermaid(columnas, restricciones, tablas), encoding="utf-8")
    for nombre, tablas in GRUPOS_ER_DOMINIO.items():
        (args.salida / f"{nombre}.mmd").write_text(
            construir_mermaid(columnas, restricciones, tablas, solo_claves=True), encoding="utf-8")
    todas_app = sorted({c["table_name"] for c in consultar("AxionERP_App", SQL_COLUMNAS)} - {"__EFMigrationsHistory"})
    (args.salida / "modelo-er-completo.mmd").write_text(construir_mermaid(columnas, restricciones, todas_app), encoding="utf-8")
    (args.salida / "diccionario-datos.md").write_text(construir_diccionario(columnas, restricciones, nucleo), encoding="utf-8")
    print(f"OK: {args.salida}/modelo-er.mmd, {', '.join([*GRUPOS_ER, *GRUPOS_ER_DOMINIO])}, modelo-er-completo.mmd y "
          "diccionario-datos.md")


if __name__ == "__main__":
    main()
