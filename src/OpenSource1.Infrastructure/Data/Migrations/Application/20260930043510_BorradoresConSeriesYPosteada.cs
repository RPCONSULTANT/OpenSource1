using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenSource1.Infrastructure.Data.Migrations.Application
{
    /// <inheritdoc />
    public partial class BorradoresConSeriesYPosteada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturasVentaBorrador_Estado",
                table: "FacturasVentaBorrador");

            migrationBuilder.AddColumn<short>(
                name: "Estado",
                table: "NotasCreditoVentaBorrador",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<string>(
                name: "NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SerieBorradorId",
                table: "NotasCreditoVentaBorrador",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SerieRegistroId",
                table: "NotasCreditoVentaBorrador",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "FacturaVentaNumero",
                table: "FacturasVentaBorrador",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SerieBorradorId",
                table: "FacturasVentaBorrador",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SerieRegistroId",
                table: "FacturasVentaBorrador",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Borradores existentes (vivos y borrados por posteos anteriores): las series que hoy usan los códigos fijos.
            migrationBuilder.Sql("""
                UPDATE "FacturasVentaBorrador" SET "SerieBorradorId" = 'e1000000-0000-0000-0000-000000000005',
                                                   "SerieRegistroId" = 'e1000000-0000-0000-0000-000000000007';
                UPDATE "NotasCreditoVentaBorrador" SET "SerieBorradorId" = 'e1000000-0000-0000-0000-00000000000b',
                                                       "SerieRegistroId" = 'e1000000-0000-0000-0000-00000000000d';
                ALTER TABLE "FacturasVentaBorrador" ALTER COLUMN "SerieBorradorId" DROP DEFAULT, ALTER COLUMN "SerieRegistroId" DROP DEFAULT;
                ALTER TABLE "NotasCreditoVentaBorrador" ALTER COLUMN "SerieBorradorId" DROP DEFAULT, ALTER COLUMN "SerieRegistroId" DROP DEFAULT,
                                                        ALTER COLUMN "Estado" DROP DEFAULT;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador",
                column: "NotaCreditoVentaNumero");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_SerieBorradorId",
                table: "NotasCreditoVentaBorrador",
                column: "SerieBorradorId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasCreditoVentaBorrador_SerieRegistroId",
                table: "NotasCreditoVentaBorrador",
                column: "SerieRegistroId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotasCreditoVentaBorrador_Estado",
                table: "NotasCreditoVentaBorrador",
                sql: "\"Estado\" IN (1, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotasCreditoVentaBorrador_Posteada",
                table: "NotasCreditoVentaBorrador",
                sql: "(\"Estado\" = 3) = (\"NotaCreditoVentaNumero\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_FacturaVentaNumero",
                table: "FacturasVentaBorrador",
                column: "FacturaVentaNumero");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_SerieBorradorId",
                table: "FacturasVentaBorrador",
                column: "SerieBorradorId");

            migrationBuilder.CreateIndex(
                name: "IX_FacturasVentaBorrador_SerieRegistroId",
                table: "FacturasVentaBorrador",
                column: "SerieRegistroId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturasVentaBorrador_Estado",
                table: "FacturasVentaBorrador",
                sql: "\"Estado\" IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturasVentaBorrador_Posteada",
                table: "FacturasVentaBorrador",
                sql: "(\"Estado\" = 3) = (\"FacturaVentaNumero\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FacturasVentaBorrador_FacturasVenta_FacturaVentaNumero",
                table: "FacturasVentaBorrador",
                column: "FacturaVentaNumero",
                principalTable: "FacturasVenta",
                principalColumn: "Numero",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FacturasVentaBorrador_Series_SerieBorradorId",
                table: "FacturasVentaBorrador",
                column: "SerieBorradorId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FacturasVentaBorrador_Series_SerieRegistroId",
                table: "FacturasVentaBorrador",
                column: "SerieRegistroId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasCreditoVentaBorrador_NotasCreditoVenta_NotaCreditoVent~",
                table: "NotasCreditoVentaBorrador",
                column: "NotaCreditoVentaNumero",
                principalTable: "NotasCreditoVenta",
                principalColumn: "Numero",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasCreditoVentaBorrador_Series_SerieBorradorId",
                table: "NotasCreditoVentaBorrador",
                column: "SerieBorradorId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotasCreditoVentaBorrador_Series_SerieRegistroId",
                table: "NotasCreditoVentaBorrador",
                column: "SerieRegistroId",
                principalTable: "Series",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FacturasVentaBorrador_FacturasVenta_FacturaVentaNumero",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropForeignKey(
                name: "FK_FacturasVentaBorrador_Series_SerieBorradorId",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropForeignKey(
                name: "FK_FacturasVentaBorrador_Series_SerieRegistroId",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasCreditoVentaBorrador_NotasCreditoVenta_NotaCreditoVent~",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasCreditoVentaBorrador_Series_SerieBorradorId",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropForeignKey(
                name: "FK_NotasCreditoVentaBorrador_Series_SerieRegistroId",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_NotasCreditoVentaBorrador_NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_NotasCreditoVentaBorrador_SerieBorradorId",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_NotasCreditoVentaBorrador_SerieRegistroId",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotasCreditoVentaBorrador_Estado",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotasCreditoVentaBorrador_Posteada",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVentaBorrador_FacturaVentaNumero",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVentaBorrador_SerieBorradorId",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropIndex(
                name: "IX_FacturasVentaBorrador_SerieRegistroId",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturasVentaBorrador_Estado",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacturasVentaBorrador_Posteada",
                table: "FacturasVentaBorrador");

            // Vuelta a la semántica anterior: un borrador posteado se borraba lógicamente con sus líneas al postear.
            migrationBuilder.Sql("""
                UPDATE "LineasFacturaVentaBorrador" l SET "IsDeleted" = true, "DeletedAtUtc" = now(), "DeletedBy" = 'migracion'
                FROM "FacturasVentaBorrador" b WHERE l."FacturaVentaBorradorId" = b."Id" AND b."Estado" = 3 AND l."IsDeleted" = false;
                UPDATE "FacturasVentaBorrador" SET "IsDeleted" = true, "DeletedAtUtc" = now(), "DeletedBy" = 'migracion', "Estado" = 1
                WHERE "Estado" = 3;
                UPDATE "LineasNotaCreditoVentaBorrador" l SET "IsDeleted" = true, "DeletedAtUtc" = now(), "DeletedBy" = 'migracion'
                FROM "NotasCreditoVentaBorrador" b WHERE l."NotaCreditoVentaBorradorId" = b."Id" AND b."Estado" = 3 AND l."IsDeleted" = false;
                UPDATE "NotasCreditoVentaBorrador" SET "IsDeleted" = true, "DeletedAtUtc" = now(), "DeletedBy" = 'migracion'
                WHERE "Estado" = 3;
                """);

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropColumn(
                name: "NotaCreditoVentaNumero",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropColumn(
                name: "SerieBorradorId",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropColumn(
                name: "SerieRegistroId",
                table: "NotasCreditoVentaBorrador");

            migrationBuilder.DropColumn(
                name: "FacturaVentaNumero",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropColumn(
                name: "SerieBorradorId",
                table: "FacturasVentaBorrador");

            migrationBuilder.DropColumn(
                name: "SerieRegistroId",
                table: "FacturasVentaBorrador");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacturasVentaBorrador_Estado",
                table: "FacturasVentaBorrador",
                sql: "\"Estado\" IN (1, 2)");
        }
    }
}
