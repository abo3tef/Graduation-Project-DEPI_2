using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medilog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at_utc",
                table: "secretaries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at_utc",
                table: "doctors",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "admins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    full_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    account_status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    email_verified_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admins", x => x.id);
                    table.CheckConstraint("ck_admins_account_status", "`account_status` IN ('Active', 'Inactive')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "auth_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    user_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    purpose = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    consumed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auth_tokens", x => x.id);
                    table.CheckConstraint("ck_auth_tokens_purpose", "`purpose` IN ('EmailVerification', 'PasswordReset')");
                    table.CheckConstraint("ck_auth_tokens_user_type", "`user_type` IN ('Doctor', 'Secretary', 'Admin')");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "uq_admins_email",
                table: "admins",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auth_tokens_user_purpose",
                table: "auth_tokens",
                columns: new[] { "user_id", "user_type", "purpose" });

            migrationBuilder.CreateIndex(
                name: "uq_auth_tokens_token_hash",
                table: "auth_tokens",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admins");

            migrationBuilder.DropTable(
                name: "auth_tokens");

            migrationBuilder.DropColumn(
                name: "email_verified_at_utc",
                table: "secretaries");

            migrationBuilder.DropColumn(
                name: "email_verified_at_utc",
                table: "doctors");
        }
    }
}
