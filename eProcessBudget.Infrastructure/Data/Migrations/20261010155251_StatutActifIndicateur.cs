using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcessBudget.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class StatutActifIndicateur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "prog",
                table: "Indicateur",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "prog",
                table: "Indicateur");
        }
    }
}
