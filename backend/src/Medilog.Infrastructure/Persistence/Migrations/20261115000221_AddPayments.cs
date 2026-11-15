using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medilog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    appointment_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    doctor_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    method = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    paid_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "char(36)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount_positive", "`amount` > 0");
                    table.CheckConstraint("ck_payments_method", "`method` IN ('Cash', 'Card')");
                    table.CheckConstraint("ck_payments_status", "`status` IN ('Pending', 'Partial', 'Paid')");
                    table.ForeignKey(
                        name: "fk_payments_appointment",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_doctor",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_payments_doctor_id_paid_at_utc",
                table: "payments",
                columns: new[] { "doctor_id", "paid_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_patient_id",
                table: "payments",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ux_payments_appointment_id",
                table: "payments",
                column: "appointment_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payments");
        }
    }
}
