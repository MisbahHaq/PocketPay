using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    FeeMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                    table.CheckConstraint("CK_Transfers_Amount_Positive", "\"AmountMinorUnits\" > 0");
                    table.CheckConstraint("CK_Transfers_Completed_Requires_JournalEntry", "(\"Status\" IN ('Completed', 'Reversed') AND \"JournalEntryId\" IS NOT NULL) OR (\"Status\" IN ('Pending', 'Failed') AND \"JournalEntryId\" IS NULL)");
                    table.CheckConstraint("CK_Transfers_Distinct_Wallets", "\"SourceWalletId\" <> \"DestinationWalletId\"");
                    table.CheckConstraint("CK_Transfers_Fee_NonNegative", "\"FeeMinorUnits\" >= 0");
                    table.CheckConstraint("CK_Transfers_Status", "\"Status\" IN ('Pending', 'Completed', 'Failed', 'Reversed')");
                    table.ForeignKey(
                        name: "FK_Transfers_Wallets_DestinationWalletId",
                        column: x => x.DestinationWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transfers_Wallets_SourceWalletId",
                        column: x => x.SourceWalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_DestinationWalletId_CreatedAt",
                table: "Transfers",
                columns: new[] { "DestinationWalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_JournalEntryId",
                table: "Transfers",
                column: "JournalEntryId",
                unique: true,
                filter: "\"JournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_SourceWalletId_CreatedAt",
                table: "Transfers",
                columns: new[] { "SourceWalletId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transfers");
        }
    }
}
