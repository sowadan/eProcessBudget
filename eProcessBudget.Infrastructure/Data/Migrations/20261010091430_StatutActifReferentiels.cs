using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcessBudget.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Statut actif / inactif (cahier fonctionnel § 3.2.1) sur les 9 référentiels que le
    /// dictionnaire de données n'en dotait pas. Valeur par défaut true : les éléments déjà
    /// en base restent actifs.
    /// </summary>
    public partial class StatutActifReferentiels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "UniteMesure",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "TypeTravaux",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "SourceFinancement",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "Risque",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "NatureRecette",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "NatureDepense",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "MotifAjustement",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "CategorieEmploi",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstActif",
                schema: "ref",
                table: "Axe",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "UniteMesure");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "TypeTravaux");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "SourceFinancement");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "Risque");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "NatureRecette");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "NatureDepense");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "MotifAjustement");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "CategorieEmploi");

            migrationBuilder.DropColumn(
                name: "EstActif",
                schema: "ref",
                table: "Axe");
        }
    }
}
