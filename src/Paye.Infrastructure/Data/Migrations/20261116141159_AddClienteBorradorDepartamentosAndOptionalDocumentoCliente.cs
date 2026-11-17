using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paye.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteBorradorDepartamentosAndOptionalDocumentoCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "documentacion_documentos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "DepartamentosJson",
                table: "clientes",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "EsBorrador",
                table: "clientes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartamentosJson",
                table: "clientes");

            migrationBuilder.DropColumn(
                name: "EsBorrador",
                table: "clientes");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClienteId",
                table: "documentacion_documentos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
