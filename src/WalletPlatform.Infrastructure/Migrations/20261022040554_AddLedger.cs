using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReversesJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerAccounts", x => x.Id);
                    table.CheckConstraint("CK_LedgerAccounts_Owner", "(\"WalletId\" IS NOT NULL AND \"SystemCode\" IS NULL) OR (\"WalletId\" IS NULL AND \"SystemCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_LedgerAccounts_Type", "\"Type\" IN ('Asset', 'Liability', 'Revenue')");
                    table.ForeignKey(
                        name: "FK_LedgerAccounts_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Amount_Positive", "\"AmountMinorUnits\" > 0");
                    table.CheckConstraint("CK_LedgerEntries_Direction", "\"Direction\" IN ('Debit', 'Credit')");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerAccounts_LedgerAccountId",
                        column: x => x.LedgerAccountId,
                        principalTable: "LedgerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_CreatedAtUtc",
                table: "JournalEntries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ReversesJournalEntryId",
                table: "JournalEntries",
                column: "ReversesJournalEntryId",
                unique: true,
                filter: "\"ReversesJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_SystemCode_Currency",
                table: "LedgerAccounts",
                columns: new[] { "SystemCode", "Currency" },
                unique: true,
                filter: "\"SystemCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_WalletId",
                table: "LedgerAccounts",
                column: "WalletId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_JournalEntryId",
                table: "LedgerEntries",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerAccountId_CreatedAtUtc",
                table: "LedgerEntries",
                columns: new[] { "LedgerAccountId", "CreatedAtUtc" });

            // Invariant 5: the ledger is append-only. The domain never issues UPDATE/DELETE,
            // but a bug, a stray script or a future developer could bypass the domain and hit
            // the tables directly. This trigger makes tampering impossible at the database,
            // which is the last line of defense for money. Corrections are made with a
            // compensating (reversal) journal entry, never by editing history.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prevent_ledger_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'Ledger tables are append-only: % is not allowed on %.',
                        TG_OP, TG_TABLE_NAME;
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_LedgerEntries_Immutable
                BEFORE UPDATE OR DELETE ON "LedgerEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_ledger_mutation();
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_JournalEntries_Immutable
                BEFORE UPDATE OR DELETE ON "JournalEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_ledger_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_JournalEntries_Immutable ON \"JournalEntries\";");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_LedgerEntries_Immutable ON \"LedgerEntries\";");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_ledger_mutation();");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "LedgerAccounts");
        }
    }
}
