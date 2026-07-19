using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class AddValueObjectsClienteProducto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Clientes: Direccion (string) -> DireccionLinea1/DireccionLinea2, + Pais, Sector ---
            migrationBuilder.AddColumn<string>(
                name: "DireccionLinea1",
                table: "Clientes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DireccionLinea2",
                table: "Clientes",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Clientes" SET "DireccionLinea1" = "Direccion" WHERE "Direccion" IS NOT NULL""");

            migrationBuilder.DropColumn(
                name: "Direccion",
                table: "Clientes");

            migrationBuilder.AddColumn<string>(
                name: "PaisCodigo",
                table: "Clientes",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaisNombre",
                table: "Clientes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sector",
                table: "Clientes",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // --- Productos: Categoria (string) -> CategoriaCodigo/CategoriaNombre, + UnidadMedida ---
            migrationBuilder.RenameColumn(
                name: "Categoria",
                table: "Productos",
                newName: "CategoriaNombre");

            migrationBuilder.AddColumn<string>(
                name: "CategoriaCodigo",
                table: "Productos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Productos" SET "CategoriaCodigo" = UPPER(LEFT("CategoriaNombre", 30))""");

            migrationBuilder.AlterColumn<string>(
                name: "CategoriaCodigo",
                table: "Productos",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadMedidaCodigo",
                table: "Productos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadMedidaNombre",
                table: "Productos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "Productos" SET "UnidadMedidaCodigo" = 'UND', "UnidadMedidaNombre" = 'Unidad'""");

            migrationBuilder.AlterColumn<string>(
                name: "UnidadMedidaCodigo",
                table: "Productos",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UnidadMedidaNombre",
                table: "Productos",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoriaCodigo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "UnidadMedidaCodigo",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "UnidadMedidaNombre",
                table: "Productos");

            migrationBuilder.RenameColumn(
                name: "CategoriaNombre",
                table: "Productos",
                newName: "Categoria");

            migrationBuilder.DropColumn(
                name: "DireccionLinea1",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "DireccionLinea2",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "PaisCodigo",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "PaisNombre",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Sector",
                table: "Clientes");

            migrationBuilder.AddColumn<string>(
                name: "Direccion",
                table: "Clientes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
