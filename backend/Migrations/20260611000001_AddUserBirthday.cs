using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SameDaySocialApp.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBirthday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "birthday",
                table: "users",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "birthday",
                table: "users");
        }
    }
}
