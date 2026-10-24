using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Despacha.Api.Modules.Customers.Internal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customers_establishments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers_establishments", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customers_customers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Identification = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Phone = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    Address = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Zone = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true),
                    EstablishmentId = table.Column<long>(type: "bigint", nullable: true),
                    RegisteredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_customers_customers_establishments_EstablishmentId",
                        column: x => x.EstablishmentId,
                        principalTable: "customers_establishments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customers_EstablishmentId",
                table: "customers_customers",
                column: "EstablishmentId");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customers_Identification",
                table: "customers_customers",
                column: "Identification");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customers_Name",
                table: "customers_customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customers_Phone",
                table: "customers_customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_customers_customers_Zone",
                table: "customers_customers",
                column: "Zone");

            migrationBuilder.CreateIndex(
                name: "IX_customers_establishments_Name",
                table: "customers_establishments",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customers_customers");

            migrationBuilder.DropTable(
                name: "customers_establishments");
        }
    }
}
