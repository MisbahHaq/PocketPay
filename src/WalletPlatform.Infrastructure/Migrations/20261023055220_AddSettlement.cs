using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settlement_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillerCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FileHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RawContent = table.Column<string>(type: "text", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<long>(type: "bigint", nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RejectionReason = table.Column<string>(type: "text", nullable: true),
                    SupersedesRunId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_files", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "settlement_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillerReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConsumerNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_rows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_settlement_files_BillerCode_BusinessDate_FileHash",
                table: "settlement_files",
                columns: new[] { "BillerCode", "BusinessDate", "FileHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlement_rows_SettlementFileId",
                table: "settlement_rows",
                column: "SettlementFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settlement_files");

            migrationBuilder.DropTable(
                name: "settlement_rows");
        }
    }
}
