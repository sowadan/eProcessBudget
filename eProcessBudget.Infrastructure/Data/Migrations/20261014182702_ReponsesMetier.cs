using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcessBudget.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReponsesMetier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SiteTiers_TiersId",
                schema: "ref",
                table: "SiteTiers");

            migrationBuilder.DropIndex(
                name: "UX_SiteTiers_SiteId_TiersId",
                schema: "ref",
                table: "SiteTiers");

            migrationBuilder.AddColumn<byte[]>(
                name: "CompteTiersId",
                schema: "ref",
                table: "Site",
                type: "binary(16)",
                fixedLength: true,
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontantPrevu",
                schema: "rec",
                table: "ReportRessource",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BesoinEntretien",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeTravauxId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Km = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_BesoinEntretien_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BesoinEntretien", x => x.Id);
                    table.CheckConstraint("CK_BesoinEntretien_Montant", "[Montant] >= 0");
                    table.ForeignKey(
                        name: "FK_BesoinEntretien_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BesoinEntretien_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BesoinEntretien_TypeTravaux_TypeTravauxId",
                        column: x => x.TypeTravauxId,
                        principalSchema: "ref",
                        principalTable: "TypeTravaux",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmissionRecette",
                schema: "rec",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    PeriodeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    PrevisionRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateEmission = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EmissionRecette_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmissionRecette", x => x.Id);
                    table.CheckConstraint("CK_EmissionRecette_Montant", "[Montant] >= 0");
                    table.CheckConstraint("CK_EmissionRecette_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_EmissionRecette_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmissionRecette_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmissionRecette_Periode_PeriodeId",
                        column: x => x.PeriodeId,
                        principalSchema: "cfg",
                        principalTable: "Periode",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmissionRecette_PrevisionRecette_PrevisionRecetteId",
                        column: x => x.PrevisionRecetteId,
                        principalSchema: "rec",
                        principalTable: "PrevisionRecette",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SiteTiers_SiteId",
                schema: "ref",
                table: "SiteTiers",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "UX_SiteTiers_TiersId",
                schema: "ref",
                table: "SiteTiers",
                column: "TiersId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Site_CompteTiersId",
                schema: "ref",
                table: "Site",
                column: "CompteTiersId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportRessource_MontantPrevu",
                schema: "rec",
                table: "ReportRessource",
                sql: "[MontantPrevu] IS NULL OR [MontantPrevu] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_BesoinEntretien_ExerciceId_TypeTravauxId",
                schema: "bud",
                table: "BesoinEntretien",
                columns: new[] { "ExerciceId", "TypeTravauxId" });

            migrationBuilder.CreateIndex(
                name: "IX_BesoinEntretien_OrganismeId",
                schema: "bud",
                table: "BesoinEntretien",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_BesoinEntretien_TypeTravauxId",
                schema: "bud",
                table: "BesoinEntretien",
                column: "TypeTravauxId");

            migrationBuilder.CreateIndex(
                name: "IX_EmissionRecette_ExerciceId_PeriodeId",
                schema: "rec",
                table: "EmissionRecette",
                columns: new[] { "ExerciceId", "PeriodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_EmissionRecette_OrganismeId",
                schema: "rec",
                table: "EmissionRecette",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmissionRecette_PeriodeId",
                schema: "rec",
                table: "EmissionRecette",
                column: "PeriodeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmissionRecette_PrevisionRecetteId",
                schema: "rec",
                table: "EmissionRecette",
                column: "PrevisionRecetteId");

            migrationBuilder.AddForeignKey(
                name: "FK_Site_CompteComptable_CompteTiersId",
                schema: "ref",
                table: "Site",
                column: "CompteTiersId",
                principalSchema: "ref",
                principalTable: "CompteComptable",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Site_CompteComptable_CompteTiersId",
                schema: "ref",
                table: "Site");

            migrationBuilder.DropTable(
                name: "BesoinEntretien",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "EmissionRecette",
                schema: "rec");

            migrationBuilder.DropIndex(
                name: "IX_SiteTiers_SiteId",
                schema: "ref",
                table: "SiteTiers");

            migrationBuilder.DropIndex(
                name: "UX_SiteTiers_TiersId",
                schema: "ref",
                table: "SiteTiers");

            migrationBuilder.DropIndex(
                name: "IX_Site_CompteTiersId",
                schema: "ref",
                table: "Site");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportRessource_MontantPrevu",
                schema: "rec",
                table: "ReportRessource");

            migrationBuilder.DropColumn(
                name: "CompteTiersId",
                schema: "ref",
                table: "Site");

            migrationBuilder.DropColumn(
                name: "MontantPrevu",
                schema: "rec",
                table: "ReportRessource");

            migrationBuilder.CreateIndex(
                name: "IX_SiteTiers_TiersId",
                schema: "ref",
                table: "SiteTiers",
                column: "TiersId");

            migrationBuilder.CreateIndex(
                name: "UX_SiteTiers_SiteId_TiersId",
                schema: "ref",
                table: "SiteTiers",
                columns: new[] { "SiteId", "TiersId" },
                unique: true);
        }
    }
}
