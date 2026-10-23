using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WalletPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBillPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bill_payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillerCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConsumerNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BillerReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundJournalEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "billers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ConsumerNumberRegex = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_payments_RefundJournalEntryId",
                table: "bill_payments",
                column: "RefundJournalEntryId",
                unique: true,
                filter: "\"RefundJournalEntryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bill_payments_WalletId",
                table: "bill_payments",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_billers_Code",
                table: "billers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_ProcessedAt_NextAttemptAt",
                table: "outbox_messages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_payments");

            migrationBuilder.DropTable(
                name: "billers");

            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
