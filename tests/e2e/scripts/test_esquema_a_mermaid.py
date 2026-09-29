import unittest

from esquema_a_mermaid import GRUPOS_ER, GRUPOS_ER_DOMINIO, TABLAS_NUCLEO, construir_diccionario, construir_mermaid

COLUMNAS = [
    {"table_name": "SociosNegocio", "column_name": "Id", "data_type": "uuid", "is_nullable": "NO", "ordinal_position": 1},
    {"table_name": "SociosNegocio", "column_name": "NombreComercial", "data_type": "character varying", "is_nullable": "NO", "ordinal_position": 2},
    {"table_name": "FacturasVenta", "column_name": "Numero", "data_type": "character varying", "is_nullable": "NO", "ordinal_position": 1},
    {"table_name": "FacturasVenta", "column_name": "SocioNegocioId", "data_type": "uuid", "is_nullable": "NO", "ordinal_position": 2},
    {"table_name": "FacturasVenta", "column_name": "FechaRegistro", "data_type": "date", "is_nullable": "NO", "ordinal_position": 3},
    {"table_name": "FacturasVenta", "column_name": "Importe", "data_type": "numeric", "is_nullable": "NO", "ordinal_position": 4,
     "character_maximum_length": None, "numeric_precision": 18, "numeric_scale": 4},
    {"table_name": "FacturasVenta", "column_name": "Nota", "data_type": "character varying", "is_nullable": "YES", "ordinal_position": 5,
     "character_maximum_length": 200, "numeric_precision": None, "numeric_scale": None},
    {"table_name": "Otra", "column_name": "Id", "data_type": "uuid", "is_nullable": "NO", "ordinal_position": 1},
]
RESTRICCIONES = [
    {"table_name": "SociosNegocio", "column_name": "Id", "constraint_type": "PRIMARY KEY", "ref_table": None, "ref_column": None},
    {"table_name": "FacturasVenta", "column_name": "Numero", "constraint_type": "PRIMARY KEY", "ref_table": None, "ref_column": None},
    {"table_name": "FacturasVenta", "column_name": "SocioNegocioId", "constraint_type": "FOREIGN KEY", "ref_table": "SociosNegocio", "ref_column": "Id"},
    {"table_name": "Otra", "column_name": "Id", "constraint_type": "FOREIGN KEY", "ref_table": "SociosNegocio", "ref_column": "Id"},
]


class EsquemaAMermaidTests(unittest.TestCase):
    def test_entidades_atributos_y_relaciones_solo_de_las_tablas_pedidas(self):
        texto = construir_mermaid(COLUMNAS, RESTRICCIONES, ["SociosNegocio", "FacturasVenta"])

        self.assertTrue(texto.startswith("erDiagram"))
        self.assertIn("  SociosNegocio {", texto)
        self.assertIn("    uuid Id PK", texto)
        self.assertIn("    character_varying NombreComercial", texto)
        self.assertIn("    uuid SocioNegocioId FK", texto)
        self.assertIn('  SociosNegocio ||--o{ FacturasVenta : "SocioNegocioId"', texto)
        self.assertNotIn("Otra", texto)

    def test_diccionario_con_tipos_nulos_y_claves(self):
        texto = construir_diccionario(COLUMNAS, RESTRICCIONES, ["FacturasVenta"])

        self.assertIn("### FacturasVenta", texto)
        self.assertIn("| Columna | Tipo | Nulo | Clave |", texto)
        self.assertIn("| SocioNegocioId | uuid | No | FK → SociosNegocio.Id |", texto)
        self.assertIn("| Numero | character varying | No | PK |", texto)

    def test_grupos_del_er_cubren_el_nucleo_de_la_app(self):
        agrupadas = {t for tablas in GRUPOS_ER.values() for t in tablas}

        self.assertEqual(["modelo-er-maestros-inventario", "modelo-er-ventas-cxc"], sorted(GRUPOS_ER))
        self.assertTrue(set(TABLAS_NUCLEO["AxionERP_App"]) <= agrupadas)
        self.assertTrue(agrupadas <= {t for tablas in TABLAS_NUCLEO.values() for t in tablas})


    def test_diccionario_con_longitud_y_precision(self):
        texto = construir_diccionario(COLUMNAS, RESTRICCIONES, ["FacturasVenta"])

        self.assertIn("| Importe | numeric(18,4) | No |  |", texto)
        self.assertIn("| Nota | character varying(200) | Sí |  |", texto)

    def test_mermaid_solo_claves_omite_columnas_sin_clave(self):
        texto = construir_mermaid(COLUMNAS, RESTRICCIONES, ["SociosNegocio", "FacturasVenta"], solo_claves=True)

        self.assertIn("    uuid Id PK", texto)
        self.assertIn("    uuid SocioNegocioId FK", texto)
        self.assertNotIn("NombreComercial", texto)
        self.assertNotIn("FechaRegistro", texto)
        self.assertNotIn("Importe", texto)
        self.assertIn('  SociosNegocio ||--o{ FacturasVenta : "SocioNegocioId"', texto)

    def test_vistas_por_dominio_cubren_las_entidades_del_enunciado(self):
        agrupadas = {t for tablas in GRUPOS_ER_DOMINIO.values() for t in tablas}

        self.assertEqual(["modelo-er-clientes-cxc", "modelo-er-productos-inventario", "modelo-er-usuarios-roles",
                          "modelo-er-ventas-borradores", "modelo-er-ventas-facturas", "modelo-er-ventas-notas-credito"],
                         sorted(GRUPOS_ER_DOMINIO))
        for tabla in ["AspNetUsers", "AspNetRoles", "AspNetUserRoles", "SociosNegocio", "Productos", "CategoriasProducto",
                      "Almacenes", "MovimientosProducto", "MovimientosValor", "FacturasVenta", "LineasFacturaVenta",
                      "FacturasVentaBorrador", "LineasFacturaVentaBorrador", "MovimientosCliente",
                      "MovimientosClienteDetalle"]:
            self.assertIn(tabla, agrupadas)
        self.assertTrue(all(len(tablas) <= 6 for tablas in GRUPOS_ER_DOMINIO.values()))


if __name__ == "__main__":
    unittest.main()
