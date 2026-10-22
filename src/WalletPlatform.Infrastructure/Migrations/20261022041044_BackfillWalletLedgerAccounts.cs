using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillWalletLedgerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Wallets created before the ledger existed have no LedgerAccount. Give each
            // one a Liability account so historical wallets can hold postings. The
            // NOT EXISTS guard makes this safe to run against databases where some (or
            // all) wallets were already backfilled.
            migrationBuilder.Sql(
                """
                INSERT INTO "LedgerAccounts" ("Id", "WalletId", "SystemCode", "Name", "Type", "Currency", "CreatedAtUtc")
                SELECT gen_random_uuid(), w."Id", NULL, 'Customer wallet ' || w."Id", 'Liability', w."Currency", now()
                FROM "Wallets" w
                WHERE NOT EXISTS (
                    SELECT 1 FROM "LedgerAccounts" a WHERE a."WalletId" = w."Id"
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only remove the backfilled wallet accounts. System accounts and any account
            // that acquired postings are left alone (postings would block deletion anyway).
            migrationBuilder.Sql(
                """
                DELETE FROM "LedgerAccounts" a
                WHERE a."WalletId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "LedgerEntries" e WHERE e."LedgerAccountId" = a."Id"
                  );
                """);
        }
    }
}
