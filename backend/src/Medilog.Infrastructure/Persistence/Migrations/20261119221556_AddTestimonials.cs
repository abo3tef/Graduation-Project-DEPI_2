using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medilog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTestimonials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "testimonials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    quote_ar = table.Column<string>(type: "varchar(800)", maxLength: 800, nullable: false),
                    quote_en = table.Column<string>(type: "varchar(800)", maxLength: 800, nullable: false),
                    name_ar = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    role_ar = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    role_en = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    is_approved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    display_order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_testimonials", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_testimonials_is_approved_display_order",
                table: "testimonials",
                columns: new[] { "is_approved", "display_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "testimonials");
        }
    }
}
