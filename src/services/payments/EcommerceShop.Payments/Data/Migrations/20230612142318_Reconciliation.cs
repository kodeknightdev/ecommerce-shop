using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EcommerceShop.Payments.Data.Migrations;

/// <inheritdoc />
public partial class Reconciliation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RefundReason",
            table: "payments",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RefundReference",
            table: "payments",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RefundedAt",
            table: "payments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "cancelled_orders",
            columns: table => new
            {
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cancelled_orders", x => x.OrderId);
            });

        migrationBuilder.CreateTable(
            name: "reconciliation_runs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Checked = table.Column<int>(type: "integer", nullable: false),
                Unverified = table.Column<int>(type: "integer", nullable: false),
                Refunded = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reconciliation_runs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "reconciliation_issues",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RunId = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Resolution = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reconciliation_issues", x => x.Id);
                table.ForeignKey(
                    name: "FK_reconciliation_issues_reconciliation_runs_RunId",
                    column: x => x.RunId,
                    principalTable: "reconciliation_runs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_payments_CreatedAt",
            table: "payments",
            column: "CreatedAt");

        migrationBuilder.CreateIndex(
            name: "IX_reconciliation_issues_OrderId",
            table: "reconciliation_issues",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "IX_reconciliation_issues_RunId",
            table: "reconciliation_issues",
            column: "RunId");

        migrationBuilder.CreateIndex(
            name: "IX_reconciliation_runs_StartedAt",
            table: "reconciliation_runs",
            column: "StartedAt");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cancelled_orders");

        migrationBuilder.DropTable(
            name: "reconciliation_issues");

        migrationBuilder.DropTable(
            name: "reconciliation_runs");

        migrationBuilder.DropIndex(
            name: "IX_payments_CreatedAt",
            table: "payments");

        migrationBuilder.DropColumn(
            name: "RefundReason",
            table: "payments");

        migrationBuilder.DropColumn(
            name: "RefundReference",
            table: "payments");

        migrationBuilder.DropColumn(
            name: "RefundedAt",
            table: "payments");
    }
}
