using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StellarSigner.Infrastructure.DrivenAdapter.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remittance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_key = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: true),
                    transaction_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    operation_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(20,7)", precision: 20, scale: 7, nullable: true),
                    asset_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    destination = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: true),
                    result = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    caller_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "partner_wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_key = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: false),
                    derivation_index = table.Column<long>(type: "bigint", nullable: false),
                    derivation_path = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_wallets", x => x.id);
                    table.CheckConstraint("ck_partner_wallets_derivation_index", "derivation_index >= 0 AND derivation_index < 2147483648");
                });

            migrationBuilder.CreateTable(
                name: "wallet_derivation_counters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    next_index = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_derivation_counters", x => x.id);
                    table.CheckConstraint("ck_wallet_derivation_counters_next_index", "next_index >= 0 AND next_index <= 2147483648");
                    table.CheckConstraint("ck_wallet_derivation_counters_singleton", "name = 'wallets'");
                });

            migrationBuilder.CreateTable(
                name: "signing_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    remittance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    partner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_key = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: true),
                    transaction_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    unsigned_xdr_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signed_xdr_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    signed_xdr = table.Column<string>(type: "text", nullable: true),
                    operation_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    asset_code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    asset_issuer = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(20,7)", precision: 20, scale: 7, nullable: false),
                    destination = table.Column<string>(type: "character varying(56)", maxLength: 56, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    failure_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    failure_status_code = table.Column<int>(type: "integer", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signing_requests", x => x.id);
                    table.CheckConstraint("ck_signing_requests_amount", "amount > 0");
                    table.ForeignKey(
                        name: "FK_signing_requests_partner_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "partner_wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_created_at",
                table: "audit_records",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_audit_records_request_id",
                table: "audit_records",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_partner_wallets_derivation_index",
                table: "partner_wallets",
                column: "derivation_index",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partner_wallets_partner_id",
                table: "partner_wallets",
                column: "partner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_partner_wallets_public_key",
                table: "partner_wallets",
                column: "public_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signing_requests_idempotency_key",
                table: "signing_requests",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signing_requests_remittance_id",
                table: "signing_requests",
                column: "remittance_id");

            migrationBuilder.CreateIndex(
                name: "IX_signing_requests_request_id",
                table: "signing_requests",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_signing_requests_wallet_id",
                table: "signing_requests",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_derivation_counters_name",
                table: "wallet_derivation_counters",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records");

            migrationBuilder.DropTable(
                name: "signing_requests");

            migrationBuilder.DropTable(
                name: "wallet_derivation_counters");

            migrationBuilder.DropTable(
                name: "partner_wallets");
        }
    }
}
