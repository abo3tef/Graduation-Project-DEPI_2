using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medilog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "doctors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    full_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    specialization = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    account_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctors", x => x.id);
                    table.CheckConstraint("ck_doctors_account_status", "`account_status` IN ('Active', 'Inactive')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "secretaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    full_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    account_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secretaries", x => x.id);
                    table.CheckConstraint("ck_secretaries_account_status", "`account_status` IN ('Active', 'Inactive')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    doctor_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    full_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    blood_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    emergency_contact = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patients", x => x.id);
                    table.CheckConstraint("ck_patients_blood_type", "`blood_type` IS NULL OR `blood_type` IN ('A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-')");
                    table.CheckConstraint("ck_patients_gender", "`gender` IN ('Male', 'Female')");
                    table.ForeignKey(
                        name: "fk_patients_doctor",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "doctor_secretaries",
                columns: table => new
                {
                    doctor_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    secretary_id = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doctor_secretaries", x => new { x.doctor_id, x.secretary_id });
                    table.ForeignKey(
                        name: "fk_doctor_secretaries_doctor",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_doctor_secretaries_secretary",
                        column: x => x.secretary_id,
                        principalTable: "secretaries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "allergies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    allergy_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allergies", x => x.id);
                    table.CheckConstraint("ck_allergies_severity", "`severity` IS NULL OR `severity` IN ('Mild', 'Moderate', 'Severe')");
                    table.ForeignKey(
                        name: "fk_allergies_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    doctor_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    created_by = table.Column<Guid>(type: "char(36)", nullable: false),
                    appointment_date = table.Column<DateTime>(type: "date", nullable: false),
                    start_time = table.Column<TimeSpan>(type: "time(0)", nullable: false),
                    end_time = table.Column<TimeSpan>(type: "time(0)", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.id);
                    table.UniqueConstraint("ak_appointments_id_patient_id_doctor_id", x => new { x.id, x.patient_id, x.doctor_id });
                    table.CheckConstraint("ck_appointments_end_time_after_start_time", "`end_time` IS NULL OR `end_time` > `start_time`");
                    table.CheckConstraint("ck_appointments_status", "`status` IN ('Scheduled', 'Confirmed', 'Completed', 'Cancelled', 'No-Show')");
                    table.ForeignKey(
                        name: "fk_appointments_doctor",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "chronic_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    condition_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    diagnosis_date = table.Column<DateTime>(type: "date", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chronic_conditions", x => x.id);
                    table.CheckConstraint("ck_chronic_conditions_status", "`status` IS NULL OR `status` IN ('Active', 'Controlled', 'Resolved')");
                    table.ForeignKey(
                        name: "fk_chronic_conditions_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "medical_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "char(36)", nullable: true),
                    file_name = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false),
                    type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    location = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false),
                    upload_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_documents", x => x.id);
                    table.CheckConstraint("ck_medical_documents_type", "`type` IN ('PDF', 'JPG', 'JPEG', 'PNG')");
                    table.ForeignKey(
                        name: "fk_medical_documents_doctor",
                        column: x => x.uploaded_by,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_documents_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "medical_visits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    doctor_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    appointment_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    visit_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    chief_complaint = table.Column<string>(type: "text", nullable: true),
                    symptoms = table.Column<string>(type: "text", nullable: true),
                    clinical_notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_visits", x => x.id);
                    table.UniqueConstraint("ak_medical_visits_id_patient_id", x => new { x.id, x.patient_id });
                    table.ForeignKey(
                        name: "fk_medical_visits_appointment",
                        columns: x => new { x.appointment_id, x.patient_id, x.doctor_id },
                        principalTable: "appointments",
                        principalColumns: new[] { "id", "patient_id", "doctor_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_visits_doctor",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_visits_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "diagnoses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    visit_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    diagnosis_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    diagnosis_date = table.Column<DateTime>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnoses", x => x.id);
                    table.ForeignKey(
                        name: "fk_diagnoses_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_diagnoses_visit",
                        columns: x => new { x.visit_id, x.patient_id },
                        principalTable: "medical_visits",
                        principalColumns: new[] { "id", "patient_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "laboratory_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    visit_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    test_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    test_date = table.Column<DateTime>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    reference_range = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_laboratory_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_laboratory_results_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_laboratory_results_visit",
                        columns: x => new { x.visit_id, x.patient_id },
                        principalTable: "medical_visits",
                        principalColumns: new[] { "id", "patient_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "medical_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    doctor_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    visit_id = table.Column<Guid>(type: "char(36)", nullable: true),
                    note_content = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_medical_notes_doctor",
                        column: x => x.doctor_id,
                        principalTable: "doctors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_notes_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medical_notes_visit",
                        columns: x => new { x.visit_id, x.patient_id },
                        principalTable: "medical_visits",
                        principalColumns: new[] { "id", "patient_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "medications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    visit_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    patient_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    medication_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    dosage = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    frequency = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    duration = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    instructions = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medications", x => x.id);
                    table.ForeignKey(
                        name: "fk_medications_patient",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medications_visit",
                        columns: x => new { x.visit_id, x.patient_id },
                        principalTable: "medical_visits",
                        principalColumns: new[] { "id", "patient_id" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "vital_signs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    visit_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    blood_pressure = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    heart_rate = table.Column<int>(type: "int", nullable: true),
                    temperature = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    respiratory_rate = table.Column<int>(type: "int", nullable: true),
                    weight = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    height = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    spo2 = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vital_signs", x => x.id);
                    table.ForeignKey(
                        name: "fk_vital_signs_visit",
                        column: x => x.visit_id,
                        principalTable: "medical_visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_allergies_patient_id",
                table: "allergies",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_doctor_id_appointment_date_start_time",
                table: "appointments",
                columns: new[] { "doctor_id", "appointment_date", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_patient_id_appointment_date",
                table: "appointments",
                columns: new[] { "patient_id", "appointment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_chronic_conditions_patient_id",
                table: "chronic_conditions",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagnoses_patient_id",
                table: "diagnoses",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_diagnoses_visit_id_patient_id",
                table: "diagnoses",
                columns: new[] { "visit_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_doctor_secretaries_secretary_id",
                table: "doctor_secretaries",
                column: "secretary_id");

            migrationBuilder.CreateIndex(
                name: "uq_doctors_email",
                table: "doctors",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_laboratory_results_patient_id_test_date",
                table: "laboratory_results",
                columns: new[] { "patient_id", "test_date" });

            migrationBuilder.CreateIndex(
                name: "IX_laboratory_results_visit_id_patient_id",
                table: "laboratory_results",
                columns: new[] { "visit_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_medical_documents_patient_id_upload_date",
                table: "medical_documents",
                columns: new[] { "patient_id", "upload_date" });

            migrationBuilder.CreateIndex(
                name: "IX_medical_documents_uploaded_by",
                table: "medical_documents",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "IX_medical_notes_doctor_id",
                table: "medical_notes",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_notes_patient_id",
                table: "medical_notes",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_notes_visit_id_patient_id",
                table: "medical_notes",
                columns: new[] { "visit_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "IX_medical_visits_appointment_id_patient_id_doctor_id",
                table: "medical_visits",
                columns: new[] { "appointment_id", "patient_id", "doctor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medical_visits_doctor_id",
                table: "medical_visits",
                column: "doctor_id");

            migrationBuilder.CreateIndex(
                name: "ix_medical_visits_patient_id_visit_date",
                table: "medical_visits",
                columns: new[] { "patient_id", "visit_date" });

            migrationBuilder.CreateIndex(
                name: "uq_medical_visits_appointment_id",
                table: "medical_visits",
                column: "appointment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medications_patient_id",
                table: "medications",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_medications_visit_id_patient_id",
                table: "medications",
                columns: new[] { "visit_id", "patient_id" });

            migrationBuilder.CreateIndex(
                name: "ix_patients_doctor_id_full_name",
                table: "patients",
                columns: new[] { "doctor_id", "full_name" });

            migrationBuilder.CreateIndex(
                name: "ix_patients_phone",
                table: "patients",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "uq_secretaries_email",
                table: "secretaries",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vital_signs_visit_id",
                table: "vital_signs",
                column: "visit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allergies");

            migrationBuilder.DropTable(
                name: "chronic_conditions");

            migrationBuilder.DropTable(
                name: "diagnoses");

            migrationBuilder.DropTable(
                name: "doctor_secretaries");

            migrationBuilder.DropTable(
                name: "laboratory_results");

            migrationBuilder.DropTable(
                name: "medical_documents");

            migrationBuilder.DropTable(
                name: "medical_notes");

            migrationBuilder.DropTable(
                name: "medications");

            migrationBuilder.DropTable(
                name: "vital_signs");

            migrationBuilder.DropTable(
                name: "secretaries");

            migrationBuilder.DropTable(
                name: "medical_visits");

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "patients");

            migrationBuilder.DropTable(
                name: "doctors");
        }
    }
}
