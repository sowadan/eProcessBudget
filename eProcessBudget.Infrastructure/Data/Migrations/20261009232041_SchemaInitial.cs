using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcessBudget.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SchemaInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "prog");

            migrationBuilder.EnsureSchema(
                name: "ref");

            migrationBuilder.EnsureSchema(
                name: "bud");

            migrationBuilder.EnsureSchema(
                name: "cfg");

            migrationBuilder.EnsureSchema(
                name: "rec");

            migrationBuilder.EnsureSchema(
                name: "hist");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.EnsureSchema(
                name: "aud");

            migrationBuilder.EnsureSchema(
                name: "ntf");

            migrationBuilder.EnsureSchema(
                name: "plat");

            migrationBuilder.CreateTable(
                name: "Organisme",
                schema: "plat",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RaisonSociale = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sigle = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TutelleTechnique = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TutelleFinanciere = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateActivation = table.Column<DateOnly>(type: "date", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Organisme_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisme", x => x.Id);
                    table.CheckConstraint("CK_Organisme_Statut", "[Statut] IN (1, 2, 3)");
                });

            migrationBuilder.CreateTable(
                name: "ParametreNotification",
                schema: "ntf",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CanalInterneActif = table.Column<bool>(type: "bit", nullable: false),
                    CanalEmailActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ParametreNotification_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametreNotification", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profil",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstSysteme = table.Column<bool>(type: "bit", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Profil_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profil", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Axe",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TypeAxe = table.Column<byte>(type: "tinyint", nullable: false),
                    Section = table.Column<byte>(type: "tinyint", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Axe_DateCreation"),
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
                    table.PrimaryKey("PK_Axe", x => x.Id);
                    table.CheckConstraint("CK_Axe_Section", "[Section] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Axe_TypeAxe", "[TypeAxe] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Axe_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CategorieEmploi",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CategorieEmploi_DateCreation"),
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
                    table.PrimaryKey("PK_CategorieEmploi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategorieEmploi_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompteComptable",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Classe = table.Column<int>(type: "int", nullable: false),
                    TypeCompte = table.Column<byte>(type: "tinyint", nullable: false),
                    CompteParentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstImmobilisation = table.Column<bool>(type: "bit", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CompteComptable_DateCreation"),
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
                    table.PrimaryKey("PK_CompteComptable", x => x.Id);
                    table.CheckConstraint("CK_CompteComptable_Classe", "[Classe] BETWEEN 1 AND 9");
                    table.CheckConstraint("CK_CompteComptable_TypeCompte", "[TypeCompte] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_CompteComptable_CompteComptable_CompteParentId",
                        column: x => x.CompteParentId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompteComptable_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CycleProgrammation",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AnneeReference = table.Column<int>(type: "int", nullable: false),
                    AnneeBase = table.Column<int>(type: "int", nullable: false),
                    AnneeFin = table.Column<int>(type: "int", nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    CyclePrecedentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CycleProgrammation_DateCreation"),
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
                    table.PrimaryKey("PK_CycleProgrammation", x => x.Id);
                    table.CheckConstraint("CK_CycleProgrammation_Annees", "[AnneeReference] = [AnneeBase] - 1 AND [AnneeFin] = [AnneeBase] + 2");
                    table.CheckConstraint("CK_CycleProgrammation_Statut", "[Statut] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CycleProgrammation_CycleProgrammation_CyclePrecedentId",
                        column: x => x.CyclePrecedentId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CycleProgrammation_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Devise",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CodeIso = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Symbole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    EstReference = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Devise_DateCreation"),
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
                    table.PrimaryKey("PK_Devise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devise_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Fichier",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    NomFichier = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Taille = table.Column<int>(type: "int", nullable: false),
                    Empreinte = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Contenu = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Fichier_DateCreation"),
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
                    table.PrimaryKey("PK_Fichier", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fichier_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JournalAudit",
                schema: "aud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    UtilisateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    UtilisateurMatricule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Entite = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EntiteId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AncienneValeur = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NouvelleValeur = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateAction = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_JournalAudit_DateAction"),
                    AdresseIP = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EstSucces = table.Column<bool>(type: "bit", nullable: false),
                    MessageErreur = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalAudit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalAudit_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ListeValeur",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Categorie = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ListeValeur_DateCreation"),
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
                    table.PrimaryKey("PK_ListeValeur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListeValeur_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MotifAjustement",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sens = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_MotifAjustement_DateCreation"),
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
                    table.PrimaryKey("PK_MotifAjustement", x => x.Id);
                    table.CheckConstraint("CK_MotifAjustement_Sens", "[Sens] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_MotifAjustement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NatureDepense",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GroupeNature = table.Column<byte>(type: "tinyint", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_NatureDepense_DateCreation"),
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
                    table.PrimaryKey("PK_NatureDepense", x => x.Id);
                    table.CheckConstraint("CK_NatureDepense_GroupeNature", "[GroupeNature] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_NatureDepense_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Site",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TypeSite = table.Column<byte>(type: "tinyint", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Prefecture = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Commune = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AxeRoutier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DateMiseEnService = table.Column<DateOnly>(type: "date", nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Site_DateCreation"),
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
                    table.PrimaryKey("PK_Site", x => x.Id);
                    table.CheckConstraint("CK_Site_TypeSite", "[TypeSite] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Site_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UniteMesure",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TypeUnite = table.Column<byte>(type: "tinyint", nullable: false),
                    Symbole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_UniteMesure_DateCreation"),
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
                    table.PrimaryKey("PK_UniteMesure", x => x.Id);
                    table.CheckConstraint("CK_UniteMesure_TypeUnite", "[TypeUnite] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_UniteMesure_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProfilDroit",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProfilId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeDroit = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProfilDroit_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfilDroit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfilDroit_Profil_ProfilId",
                        column: x => x.ProfilId,
                        principalSchema: "auth",
                        principalTable: "Profil",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Rubrique",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    AxeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Rubrique_DateCreation"),
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
                    table.PrimaryKey("PK_Rubrique", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rubrique_Axe_AxeId",
                        column: x => x.AxeId,
                        principalSchema: "ref",
                        principalTable: "Axe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Rubrique_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NatureRecette",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategorieRecette = table.Column<byte>(type: "tinyint", nullable: false),
                    NatureParentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    CompteComptableId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SuiviParSite = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_NatureRecette_DateCreation"),
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
                    table.PrimaryKey("PK_NatureRecette", x => x.Id);
                    table.CheckConstraint("CK_NatureRecette_CategorieRecette", "[CategorieRecette] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_NatureRecette_CompteComptable_CompteComptableId",
                        column: x => x.CompteComptableId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NatureRecette_NatureRecette_NatureParentId",
                        column: x => x.NatureParentId,
                        principalSchema: "ref",
                        principalTable: "NatureRecette",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NatureRecette_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompteBancaire",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Banque = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Intitule = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NumeroCompte = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DeviseId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CompteComptableId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CompteBancaire_DateCreation"),
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
                    table.PrimaryKey("PK_CompteBancaire", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompteBancaire_CompteComptable_CompteComptableId",
                        column: x => x.CompteComptableId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompteBancaire_Devise_DeviseId",
                        column: x => x.DeviseId,
                        principalSchema: "cfg",
                        principalTable: "Devise",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompteBancaire_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TauxChange",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DeviseId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateEffet = table.Column<DateOnly>(type: "date", nullable: false),
                    Taux = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TauxChange_DateCreation"),
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
                    table.PrimaryKey("PK_TauxChange", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TauxChange_Devise_DeviseId",
                        column: x => x.DeviseId,
                        principalSchema: "cfg",
                        principalTable: "Devise",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TauxChange_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ParametreOrganisme",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DeviseReferenceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NbDecimales = table.Column<int>(type: "int", nullable: false),
                    DureeSessionMinutes = table.Column<int>(type: "int", nullable: false),
                    NbTentativesMax = table.Column<int>(type: "int", nullable: false),
                    TaillePagination = table.Column<int>(type: "int", nullable: false),
                    RetentionJournauxMois = table.Column<int>(type: "int", nullable: false),
                    LogoFichierId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EnteteRapport = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PiedPageRapport = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ParametreOrganisme_DateCreation"),
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
                    table.PrimaryKey("PK_ParametreOrganisme", x => x.Id);
                    table.CheckConstraint("CK_ParametreOrganisme_Decimales", "[NbDecimales] BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_ParametreOrganisme_Retention", "[RetentionJournauxMois] >= 12");
                    table.ForeignKey(
                        name: "FK_ParametreOrganisme_Devise_DeviseReferenceId",
                        column: x => x.DeviseReferenceId,
                        principalSchema: "cfg",
                        principalTable: "Devise",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParametreOrganisme_Fichier_LogoFichierId",
                        column: x => x.LogoFichierId,
                        principalSchema: "cfg",
                        principalTable: "Fichier",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ParametreOrganisme_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Risque",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CategorieRisqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Risque_DateCreation"),
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
                    table.PrimaryKey("PK_Risque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Risque_ListeValeur_CategorieRisqueId",
                        column: x => x.CategorieRisqueId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Risque_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Structure",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Sigle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TypeStructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    StructureParentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ResponsableNom = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Structure_DateCreation"),
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
                    table.PrimaryKey("PK_Structure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Structure_ListeValeur_TypeStructureId",
                        column: x => x.TypeStructureId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Structure_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Structure_Structure_StructureParentId",
                        column: x => x.StructureParentId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tiers",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TypeTiers = table.Column<byte>(type: "tinyint", nullable: false),
                    CategorieEntiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RaisonSociale = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Sigle = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TiersParentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    IdentifiantFiscal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CompteGeneralId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    CompteAuxiliaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Tiers_DateCreation"),
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
                    table.PrimaryKey("PK_Tiers", x => x.Id);
                    table.CheckConstraint("CK_Tiers_TypeTiers", "[TypeTiers] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Tiers_CompteComptable_CompteAuxiliaireId",
                        column: x => x.CompteAuxiliaireId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tiers_CompteComptable_CompteGeneralId",
                        column: x => x.CompteGeneralId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tiers_ListeValeur_CategorieEntiteId",
                        column: x => x.CategorieEntiteId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tiers_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tiers_Tiers_TiersParentId",
                        column: x => x.TiersParentId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TypeTravaux",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TypeVoieId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstPrestationControle = table.Column<bool>(type: "bit", nullable: false),
                    TypeTravauxControleId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TypeTravaux_DateCreation"),
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
                    table.PrimaryKey("PK_TypeTravaux", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TypeTravaux_ListeValeur_TypeVoieId",
                        column: x => x.TypeVoieId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TypeTravaux_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TypeTravaux_TypeTravaux_TypeTravauxControleId",
                        column: x => x.TypeTravauxControleId,
                        principalSchema: "ref",
                        principalTable: "TypeTravaux",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SousRubrique",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    RubriqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_SousRubrique_DateCreation"),
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
                    table.PrimaryKey("PK_SousRubrique", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SousRubrique_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SousRubrique_Rubrique_RubriqueId",
                        column: x => x.RubriqueId,
                        principalSchema: "ref",
                        principalTable: "Rubrique",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExecutionHistorique",
                schema: "hist",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    AxeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RubriqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NatureDepenseId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NatureRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Prevision = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantAutorise = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MontantRealise = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RestantAPayer = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    KmEntretenus = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Source = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ExecutionHistorique_DateCreation"),
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
                    table.PrimaryKey("PK_ExecutionHistorique", x => x.Id);
                    table.CheckConstraint("CK_ExecutionHistorique_Source", "[Source] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_ExecutionHistorique_Axe_AxeId",
                        column: x => x.AxeId,
                        principalSchema: "ref",
                        principalTable: "Axe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExecutionHistorique_NatureDepense_NatureDepenseId",
                        column: x => x.NatureDepenseId,
                        principalSchema: "ref",
                        principalTable: "NatureDepense",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExecutionHistorique_NatureRecette_NatureRecetteId",
                        column: x => x.NatureRecetteId,
                        principalSchema: "ref",
                        principalTable: "NatureRecette",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExecutionHistorique_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExecutionHistorique_Rubrique_RubriqueId",
                        column: x => x.RubriqueId,
                        principalSchema: "ref",
                        principalTable: "Rubrique",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExecutionHistorique_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Agent",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Matricule = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NomComplet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategorieEmploiId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Agent_DateCreation"),
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
                    table.PrimaryKey("PK_Agent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Agent_CategorieEmploi_CategorieEmploiId",
                        column: x => x.CategorieEmploiId,
                        principalSchema: "ref",
                        principalTable: "CategorieEmploi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Agent_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Agent_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Agent_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Utilisateur",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Matricule = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NomPrenoms = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Fonction = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NbEchecsConnexion = table.Column<int>(type: "int", nullable: false),
                    BloqueJusquAu = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DerniereConnexion = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstAdministrateurPlateforme = table.Column<bool>(type: "bit", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Utilisateur_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateur", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Utilisateur_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Utilisateur_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SiteTiers",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TiersId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_SiteTiers_DateCreation"),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiteTiers_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SiteTiers_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SiteTiers_Tiers_TiersId",
                        column: x => x.TiersId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SourceFinancement",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PartenaireFinancierId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_SourceFinancement_DateCreation"),
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
                    table.PrimaryKey("PK_SourceFinancement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceFinancement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SourceFinancement_Tiers_PartenaireFinancierId",
                        column: x => x.PartenaireFinancierId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Exercice",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateOnly>(type: "date", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    CycleProgrammationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateOuverture = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DateCloture = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ClotureParId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Exercice_DateCreation"),
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
                    table.PrimaryKey("PK_Exercice", x => x.Id);
                    table.CheckConstraint("CK_Exercice_Dates", "[DateFin] > [DateDebut]");
                    table.CheckConstraint("CK_Exercice_Statut", "[Statut] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Exercice_CycleProgrammation_CycleProgrammationId",
                        column: x => x.CycleProgrammationId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Exercice_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Exercice_Utilisateur_ClotureParId",
                        column: x => x.ClotureParId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Habilitation",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    UtilisateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypePerimetre = table.Column<byte>(type: "tinyint", nullable: false),
                    PerimetreId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NiveauAcces = table.Column<byte>(type: "tinyint", nullable: false),
                    DateDebut = table.Column<DateOnly>(type: "date", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DemandeParId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ValideurFinancierId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateValidationFinancier = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ValideurAdminSIId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateValidationAdminSI = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    MotifRejet = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Habilitation_DateCreation"),
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
                    table.PrimaryKey("PK_Habilitation", x => x.Id);
                    table.CheckConstraint("CK_Habilitation_NiveauAcces", "[NiveauAcces] IN (1, 2)");
                    table.CheckConstraint("CK_Habilitation_Statut", "[Statut] IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_Habilitation_TypePerimetre", "[TypePerimetre] IN (1, 2, 3, 4, 5, 6)");
                    table.CheckConstraint("CK_Habilitation_ValideursDistincts", "[ValideurFinancierId] IS NULL OR [ValideurAdminSIId] IS NULL OR [ValideurFinancierId] <> [ValideurAdminSIId]");
                    table.ForeignKey(
                        name: "FK_Habilitation_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Habilitation_Utilisateur_DemandeParId",
                        column: x => x.DemandeParId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Habilitation_Utilisateur_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Habilitation_Utilisateur_ValideurAdminSIId",
                        column: x => x.ValideurAdminSIId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Habilitation_Utilisateur_ValideurFinancierId",
                        column: x => x.ValideurFinancierId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "JournalConnexion",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    UtilisateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Login = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AdresseIp = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DateHeure = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_JournalConnexion_DateHeure"),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    Motif = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalConnexion", x => x.Id);
                    table.CheckConstraint("CK_JournalConnexion_Statut", "[Statut] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_JournalConnexion_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalConnexion_Utilisateur_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                schema: "ntf",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    UtilisateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EstLue = table.Column<bool>(type: "bit", nullable: false),
                    DateLecture = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    Lien = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CleEntite = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActeurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Notification_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notification_Utilisateur_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Programme",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ObjectifGeneral = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Presentation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsableId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Programme_DateCreation"),
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
                    table.PrimaryKey("PK_Programme", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Programme_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Programme_Utilisateur_ResponsableId",
                        column: x => x.ResponsableId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UtilisateurDroit",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    UtilisateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeDroit = table.Column<int>(type: "int", nullable: false),
                    EstAccorde = table.Column<bool>(type: "bit", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateDebut = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    AttribuePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_UtilisateurDroit_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilisateurDroit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilisateurDroit_Utilisateur_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UtilisateurProfil",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    UtilisateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProfilId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    AttribuePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_UtilisateurProfil_DateCreation"),
                    DateModification = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ModifiePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false),
                    DateSuppression = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SupprimePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilisateurProfil", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UtilisateurProfil_Profil_ProfilId",
                        column: x => x.ProfilId,
                        principalSchema: "auth",
                        principalTable: "Profil",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UtilisateurProfil_Utilisateur_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LigneBudgetaire",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SousRubriqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Sens = table.Column<byte>(type: "tinyint", nullable: false),
                    NatureDepenseId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NatureRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SourceFinancementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_LigneBudgetaire_DateCreation"),
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
                    table.PrimaryKey("PK_LigneBudgetaire", x => x.Id);
                    table.CheckConstraint("CK_LigneBudgetaire_Nature", "([Sens] = 1 AND [NatureDepenseId] IS NOT NULL) OR ([Sens] = 2 AND [NatureRecetteId] IS NOT NULL)");
                    table.CheckConstraint("CK_LigneBudgetaire_Sens", "[Sens] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_LigneBudgetaire_NatureDepense_NatureDepenseId",
                        column: x => x.NatureDepenseId,
                        principalSchema: "ref",
                        principalTable: "NatureDepense",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LigneBudgetaire_NatureRecette_NatureRecetteId",
                        column: x => x.NatureRecetteId,
                        principalSchema: "ref",
                        principalTable: "NatureRecette",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LigneBudgetaire_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LigneBudgetaire_SourceFinancement_SourceFinancementId",
                        column: x => x.SourceFinancementId,
                        principalSchema: "ref",
                        principalTable: "SourceFinancement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LigneBudgetaire_SousRubrique_SousRubriqueId",
                        column: x => x.SousRubriqueId,
                        principalSchema: "ref",
                        principalTable: "SousRubrique",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProgrammationRessource",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CycleProgrammationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    NatureRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SourceFinancementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProgrammationRessource_DateCreation"),
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
                    table.PrimaryKey("PK_ProgrammationRessource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgrammationRessource_CycleProgrammation_CycleProgrammationId",
                        column: x => x.CycleProgrammationId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationRessource_NatureRecette_NatureRecetteId",
                        column: x => x.NatureRecetteId,
                        principalSchema: "ref",
                        principalTable: "NatureRecette",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationRessource_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationRessource_SourceFinancement_SourceFinancementId",
                        column: x => x.SourceFinancementId,
                        principalSchema: "ref",
                        principalTable: "SourceFinancement",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Projet",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    AxeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Intitule = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TiersBeneficiaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SourceFinancementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateDebut = table.Column<DateOnly>(type: "date", nullable: true),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: true),
                    EstPluriannuel = table.Column<bool>(type: "bit", nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Projet_DateCreation"),
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
                    table.PrimaryKey("PK_Projet", x => x.Id);
                    table.CheckConstraint("CK_Projet_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Projet_Axe_AxeId",
                        column: x => x.AxeId,
                        principalSchema: "ref",
                        principalTable: "Axe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projet_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projet_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projet_SourceFinancement_SourceFinancementId",
                        column: x => x.SourceFinancementId,
                        principalSchema: "ref",
                        principalTable: "SourceFinancement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projet_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projet_Tiers_TiersBeneficiaireId",
                        column: x => x.TiersBeneficiaireId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EnveloppeMaitreOuvrage",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    MaitreOuvrageId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    RubriqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EnveloppeMaitreOuvrage_DateCreation"),
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
                    table.PrimaryKey("PK_EnveloppeMaitreOuvrage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnveloppeMaitreOuvrage_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnveloppeMaitreOuvrage_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnveloppeMaitreOuvrage_Rubrique_RubriqueId",
                        column: x => x.RubriqueId,
                        principalSchema: "ref",
                        principalTable: "Rubrique",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EnveloppeMaitreOuvrage_Tiers_MaitreOuvrageId",
                        column: x => x.MaitreOuvrageId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Periode",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypePeriode = table.Column<byte>(type: "tinyint", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateOnly>(type: "date", nullable: false),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: false),
                    EstCloturee = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Periode_DateCreation"),
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
                    table.PrimaryKey("PK_Periode", x => x.Id);
                    table.CheckConstraint("CK_Periode_TypePeriode", "[TypePeriode] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Periode_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Periode_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReportRessource",
                schema: "rec",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    NatureRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstPositionne = table.Column<bool>(type: "bit", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ReportRessource_DateCreation"),
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
                    table.PrimaryKey("PK_ReportRessource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportRessource_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportRessource_NatureRecette_NatureRecetteId",
                        column: x => x.NatureRecetteId,
                        principalSchema: "ref",
                        principalTable: "NatureRecette",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReportRessource_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Action",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProgrammeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ObjectifSpecifique = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Action_DateCreation"),
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
                    table.PrimaryKey("PK_Action", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Action_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Action_Programme_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "prog",
                        principalTable: "Programme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Action_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AnalyseQualitative",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeAnalyse = table.Column<byte>(type: "tinyint", nullable: false),
                    ProgrammeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    RubriqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Contenu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_AnalyseQualitative_DateCreation"),
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
                    table.PrimaryKey("PK_AnalyseQualitative", x => x.Id);
                    table.CheckConstraint("CK_AnalyseQualitative_TypeAnalyse", "[TypeAnalyse] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_AnalyseQualitative_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnalyseQualitative_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnalyseQualitative_Programme_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "prog",
                        principalTable: "Programme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AnalyseQualitative_Rubrique_RubriqueId",
                        column: x => x.RubriqueId,
                        principalSchema: "ref",
                        principalTable: "Rubrique",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProgrammationEmploi",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CycleProgrammationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    ProgrammeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CategorieEmploiId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Effectif = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProgrammationEmploi_DateCreation"),
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
                    table.PrimaryKey("PK_ProgrammationEmploi", x => x.Id);
                    table.CheckConstraint("CK_ProgrammationEmploi_Effectif", "[Effectif] >= 0");
                    table.ForeignKey(
                        name: "FK_ProgrammationEmploi_CategorieEmploi_CategorieEmploiId",
                        column: x => x.CategorieEmploiId,
                        principalSchema: "ref",
                        principalTable: "CategorieEmploi",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationEmploi_CycleProgrammation_CycleProgrammationId",
                        column: x => x.CycleProgrammationId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationEmploi_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationEmploi_Programme_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "prog",
                        principalTable: "Programme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProgrammeStructure",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProgrammeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EstPrincipale = table.Column<bool>(type: "bit", nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProgrammeStructure_DateCreation"),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgrammeStructure", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgrammeStructure_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammeStructure_Programme_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "prog",
                        principalTable: "Programme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammeStructure_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LigneCompte",
                schema: "ref",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    LigneBudgetaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CompteComptableId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EstPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_LigneCompte_DateCreation"),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigneCompte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigneCompte_CompteComptable_CompteComptableId",
                        column: x => x.CompteComptableId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LigneCompte_LigneBudgetaire_LigneBudgetaireId",
                        column: x => x.LigneBudgetaireId,
                        principalSchema: "ref",
                        principalTable: "LigneBudgetaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LigneCompte_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrevisionRecette",
                schema: "rec",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    LigneBudgetaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    NatureRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TiersCollecteurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    MontantInitial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantRevise = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    MotifRevision = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_PrevisionRecette_DateCreation"),
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
                    table.PrimaryKey("PK_PrevisionRecette", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrevisionRecette_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrevisionRecette_LigneBudgetaire_LigneBudgetaireId",
                        column: x => x.LigneBudgetaireId,
                        principalSchema: "ref",
                        principalTable: "LigneBudgetaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrevisionRecette_NatureRecette_NatureRecetteId",
                        column: x => x.NatureRecetteId,
                        principalSchema: "ref",
                        principalTable: "NatureRecette",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrevisionRecette_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrevisionRecette_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PrevisionRecette_Tiers_TiersCollecteurId",
                        column: x => x.TiersCollecteurId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SeuilAlerte",
                schema: "cfg",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Portee = table.Column<byte>(type: "tinyint", nullable: false),
                    AxeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    LigneBudgetaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Pourcentage = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Couleur = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_SeuilAlerte_DateCreation"),
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
                    table.PrimaryKey("PK_SeuilAlerte", x => x.Id);
                    table.CheckConstraint("CK_SeuilAlerte_Portee", "[Portee] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_SeuilAlerte_Axe_AxeId",
                        column: x => x.AxeId,
                        principalSchema: "ref",
                        principalTable: "Axe",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SeuilAlerte_LigneBudgetaire_LigneBudgetaireId",
                        column: x => x.LigneBudgetaireId,
                        principalSchema: "ref",
                        principalTable: "LigneBudgetaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SeuilAlerte_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Contrat",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProjetId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Objet = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TypeContrat = table.Column<byte>(type: "tinyint", nullable: false),
                    TypePrestationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ContratTravauxId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TitulaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    MontantInitial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantAvenants = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DateSignature = table.Column<DateOnly>(type: "date", nullable: true),
                    DateDebut = table.Column<DateOnly>(type: "date", nullable: true),
                    DateFin = table.Column<DateOnly>(type: "date", nullable: true),
                    EstPluriannuel = table.Column<bool>(type: "bit", nullable: false),
                    KmContractuels = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Contrat_DateCreation"),
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
                    table.PrimaryKey("PK_Contrat", x => x.Id);
                    table.CheckConstraint("CK_Contrat_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_Contrat_TypeContrat", "[TypeContrat] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Contrat_Contrat_ContratTravauxId",
                        column: x => x.ContratTravauxId,
                        principalSchema: "bud",
                        principalTable: "Contrat",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contrat_ListeValeur_TypePrestationId",
                        column: x => x.TypePrestationId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contrat_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contrat_Projet_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "bud",
                        principalTable: "Projet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Contrat_Tiers_TitulaireId",
                        column: x => x.TitulaireId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Dotation",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    LigneBudgetaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProjetId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TiersId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    MontantInitial = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Dotation_DateCreation"),
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
                    table.PrimaryKey("PK_Dotation", x => x.Id);
                    table.CheckConstraint("CK_Dotation_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Dotation_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Dotation_LigneBudgetaire_LigneBudgetaireId",
                        column: x => x.LigneBudgetaireId,
                        principalSchema: "ref",
                        principalTable: "LigneBudgetaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Dotation_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Dotation_Projet_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "bud",
                        principalTable: "Projet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Dotation_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Dotation_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Dotation_Tiers_TiersId",
                        column: x => x.TiersId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Immobilisation",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ProjetId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SiteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    CompteComptableId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateAcquisition = table.Column<DateOnly>(type: "date", nullable: false),
                    ValeurAcquisition = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Etat = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Immobilisation_DateCreation"),
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
                    table.PrimaryKey("PK_Immobilisation", x => x.Id);
                    table.CheckConstraint("CK_Immobilisation_Etat", "[Etat] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Immobilisation_CompteComptable_CompteComptableId",
                        column: x => x.CompteComptableId,
                        principalSchema: "ref",
                        principalTable: "CompteComptable",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Immobilisation_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Immobilisation_Projet_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "bud",
                        principalTable: "Projet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Immobilisation_Site_SiteId",
                        column: x => x.SiteId,
                        principalSchema: "ref",
                        principalTable: "Site",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Immobilisation_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjetTravaux",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeVoieId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TypeTravauxId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    MaitreOuvrageId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Localisation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    KmPrevus = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProjetTravaux_DateCreation"),
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
                    table.PrimaryKey("PK_ProjetTravaux", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjetTravaux_ListeValeur_TypeVoieId",
                        column: x => x.TypeVoieId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjetTravaux_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjetTravaux_Projet_Id",
                        column: x => x.Id,
                        principalSchema: "bud",
                        principalTable: "Projet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjetTravaux_Tiers_MaitreOuvrageId",
                        column: x => x.MaitreOuvrageId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjetTravaux_TypeTravaux_TypeTravauxId",
                        column: x => x.TypeTravauxId,
                        principalSchema: "ref",
                        principalTable: "TypeTravaux",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Activite",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ActionId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NatureDepensePrincipaleId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Activite_DateCreation"),
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
                    table.PrimaryKey("PK_Activite", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activite_Action_ActionId",
                        column: x => x.ActionId,
                        principalSchema: "prog",
                        principalTable: "Action",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Activite_NatureDepense_NatureDepensePrincipaleId",
                        column: x => x.NatureDepensePrincipaleId,
                        principalSchema: "ref",
                        principalTable: "NatureDepense",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Activite_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Activite_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Indicateur",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Niveau = table.Column<byte>(type: "tinyint", nullable: false),
                    ProgrammeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ActionId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UniteMesureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ModeCalcul = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CodeFormule = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SensEvolution = table.Column<byte>(type: "tinyint", nullable: false),
                    SourceVerificationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    PeriodiciteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    StructureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    AnneeReference = table.Column<int>(type: "int", nullable: true),
                    ValeurReference = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Indicateur_DateCreation"),
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
                    table.PrimaryKey("PK_Indicateur", x => x.Id);
                    table.CheckConstraint("CK_Indicateur_Niveau", "[Niveau] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Indicateur_Rattachement", "([Niveau] = 1 AND [ProgrammeId] IS NOT NULL AND [ActionId] IS NULL) OR ([Niveau] IN (2, 3) AND [ActionId] IS NOT NULL)");
                    table.CheckConstraint("CK_Indicateur_SensEvolution", "[SensEvolution] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Indicateur_Action_ActionId",
                        column: x => x.ActionId,
                        principalSchema: "prog",
                        principalTable: "Action",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Indicateur_ListeValeur_PeriodiciteId",
                        column: x => x.PeriodiciteId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Indicateur_ListeValeur_SourceVerificationId",
                        column: x => x.SourceVerificationId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Indicateur_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Indicateur_Programme_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "prog",
                        principalTable: "Programme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Indicateur_Structure_StructureId",
                        column: x => x.StructureId,
                        principalSchema: "ref",
                        principalTable: "Structure",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Indicateur_UniteMesure_UniteMesureId",
                        column: x => x.UniteMesureId,
                        principalSchema: "ref",
                        principalTable: "UniteMesure",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EcheanceRecouvrement",
                schema: "rec",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    PrevisionRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateEcheance = table.Column<DateOnly>(type: "date", nullable: false),
                    MontantAttendu = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EcheanceRecouvrement_DateCreation"),
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
                    table.PrimaryKey("PK_EcheanceRecouvrement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcheanceRecouvrement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EcheanceRecouvrement_PrevisionRecette_PrevisionRecetteId",
                        column: x => x.PrevisionRecetteId,
                        principalSchema: "rec",
                        principalTable: "PrevisionRecette",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Encaissement",
                schema: "rec",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    PeriodeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    PrevisionRecetteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateEncaissement = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompteBancaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    EstPositionne = table.Column<bool>(type: "bit", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Encaissement_DateCreation"),
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
                    table.PrimaryKey("PK_Encaissement", x => x.Id);
                    table.CheckConstraint("CK_Encaissement_Montant", "[Montant] >= 0");
                    table.CheckConstraint("CK_Encaissement_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Encaissement_CompteBancaire_CompteBancaireId",
                        column: x => x.CompteBancaireId,
                        principalSchema: "ref",
                        principalTable: "CompteBancaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encaissement_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encaissement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encaissement_Periode_PeriodeId",
                        column: x => x.PeriodeId,
                        principalSchema: "cfg",
                        principalTable: "Periode",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Encaissement_PrevisionRecette_PrevisionRecetteId",
                        column: x => x.PrevisionRecetteId,
                        principalSchema: "rec",
                        principalTable: "PrevisionRecette",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AvancementPhysique",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProjetId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ContratId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    PeriodeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeTravauxId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    KmRealises = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    TauxAvancement = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_AvancementPhysique_DateCreation"),
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
                    table.PrimaryKey("PK_AvancementPhysique", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvancementPhysique_Contrat_ContratId",
                        column: x => x.ContratId,
                        principalSchema: "bud",
                        principalTable: "Contrat",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AvancementPhysique_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AvancementPhysique_Periode_PeriodeId",
                        column: x => x.PeriodeId,
                        principalSchema: "cfg",
                        principalTable: "Periode",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AvancementPhysique_Projet_ProjetId",
                        column: x => x.ProjetId,
                        principalSchema: "bud",
                        principalTable: "Projet",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AvancementPhysique_TypeTravaux_TypeTravauxId",
                        column: x => x.TypeTravauxId,
                        principalSchema: "ref",
                        principalTable: "TypeTravaux",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ajustement",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DotationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DateAjustement = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MotifAjustementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    ValideParId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateValidation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Ajustement_DateCreation"),
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
                    table.PrimaryKey("PK_Ajustement", x => x.Id);
                    table.CheckConstraint("CK_Ajustement_Montant", "[Montant] <> 0");
                    table.CheckConstraint("CK_Ajustement_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Ajustement_Dotation_DotationId",
                        column: x => x.DotationId,
                        principalSchema: "bud",
                        principalTable: "Dotation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ajustement_MotifAjustement_MotifAjustementId",
                        column: x => x.MotifAjustementId,
                        principalSchema: "ref",
                        principalTable: "MotifAjustement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ajustement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ajustement_Utilisateur_ValideParId",
                        column: x => x.ValideParId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Engagement",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DotationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ContratId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TiersId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TypeEngagement = table.Column<byte>(type: "tinyint", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DateEngagement = table.Column<DateOnly>(type: "date", nullable: false),
                    Objet = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EstReport = table.Column<bool>(type: "bit", nullable: false),
                    EngagementOrigineId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    ValideParId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateValidation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Engagement_DateCreation"),
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
                    table.PrimaryKey("PK_Engagement", x => x.Id);
                    table.CheckConstraint("CK_Engagement_Montant", "[Montant] > 0");
                    table.CheckConstraint("CK_Engagement_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_Engagement_TypeEngagement", "[TypeEngagement] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_Engagement_Contrat_ContratId",
                        column: x => x.ContratId,
                        principalSchema: "bud",
                        principalTable: "Contrat",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagement_Dotation_DotationId",
                        column: x => x.DotationId,
                        principalSchema: "bud",
                        principalTable: "Dotation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagement_Engagement_EngagementOrigineId",
                        column: x => x.EngagementOrigineId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagement_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagement_Tiers_TiersId",
                        column: x => x.TiersId,
                        principalSchema: "ref",
                        principalTable: "Tiers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Engagement_Utilisateur_ValideParId",
                        column: x => x.ValideParId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CleRepartition",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    LigneBudgetaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ActiviteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Pourcentage = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CleRepartition_DateCreation"),
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
                    table.PrimaryKey("PK_CleRepartition", x => x.Id);
                    table.CheckConstraint("CK_CleRepartition_Pourcentage", "[Pourcentage] > 0 AND [Pourcentage] <= 100");
                    table.ForeignKey(
                        name: "FK_CleRepartition_Activite_ActiviteId",
                        column: x => x.ActiviteId,
                        principalSchema: "prog",
                        principalTable: "Activite",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CleRepartition_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CleRepartition_LigneBudgetaire_LigneBudgetaireId",
                        column: x => x.LigneBudgetaireId,
                        principalSchema: "ref",
                        principalTable: "LigneBudgetaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CleRepartition_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProgrammationFinanciere",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CycleProgrammationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    ActionId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ActiviteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NatureDepenseId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    SourceFinancementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    MontantAE = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantCP = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProgrammationFinanciere_DateCreation"),
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
                    table.PrimaryKey("PK_ProgrammationFinanciere", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgrammationFinanciere_Action_ActionId",
                        column: x => x.ActionId,
                        principalSchema: "prog",
                        principalTable: "Action",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationFinanciere_Activite_ActiviteId",
                        column: x => x.ActiviteId,
                        principalSchema: "prog",
                        principalTable: "Activite",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationFinanciere_CycleProgrammation_CycleProgrammationId",
                        column: x => x.CycleProgrammationId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationFinanciere_NatureDepense_NatureDepenseId",
                        column: x => x.NatureDepenseId,
                        principalSchema: "ref",
                        principalTable: "NatureDepense",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationFinanciere_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProgrammationFinanciere_SourceFinancement_SourceFinancementId",
                        column: x => x.SourceFinancementId,
                        principalSchema: "ref",
                        principalTable: "SourceFinancement",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RepartitionTemps",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    AgentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ActionId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ActiviteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    PourcentageTemps = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RepartitionTemps_DateCreation"),
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
                    table.PrimaryKey("PK_RepartitionTemps", x => x.Id);
                    table.CheckConstraint("CK_RepartitionTemps_Pourcentage", "[PourcentageTemps] > 0 AND [PourcentageTemps] <= 100");
                    table.ForeignKey(
                        name: "FK_RepartitionTemps_Action_ActionId",
                        column: x => x.ActionId,
                        principalSchema: "prog",
                        principalTable: "Action",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RepartitionTemps_Activite_ActiviteId",
                        column: x => x.ActiviteId,
                        principalSchema: "prog",
                        principalTable: "Activite",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RepartitionTemps_Agent_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "ref",
                        principalTable: "Agent",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RepartitionTemps_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RepartitionTemps_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CadreLogiqueElement",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ProgrammeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CycleProgrammationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypeElement = table.Column<byte>(type: "tinyint", nullable: false),
                    ActionId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ElementParentId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Libelle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IndicateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    SourceVerificationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    ValeurCible = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CadreLogiqueElement_DateCreation"),
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
                    table.PrimaryKey("PK_CadreLogiqueElement", x => x.Id);
                    table.CheckConstraint("CK_CadreLogiqueElement_TypeElement", "[TypeElement] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_Action_ActionId",
                        column: x => x.ActionId,
                        principalSchema: "prog",
                        principalTable: "Action",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_CadreLogiqueElement_ElementParentId",
                        column: x => x.ElementParentId,
                        principalSchema: "prog",
                        principalTable: "CadreLogiqueElement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_CycleProgrammation_CycleProgrammationId",
                        column: x => x.CycleProgrammationId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_Indicateur_IndicateurId",
                        column: x => x.IndicateurId,
                        principalSchema: "prog",
                        principalTable: "Indicateur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_ListeValeur_SourceVerificationId",
                        column: x => x.SourceVerificationId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueElement_Programme_ProgrammeId",
                        column: x => x.ProgrammeId,
                        principalSchema: "prog",
                        principalTable: "Programme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IndicateurCible",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    IndicateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CycleProgrammationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    ValeurCible = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_IndicateurCible_DateCreation"),
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
                    table.PrimaryKey("PK_IndicateurCible", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndicateurCible_CycleProgrammation_CycleProgrammationId",
                        column: x => x.CycleProgrammationId,
                        principalSchema: "cfg",
                        principalTable: "CycleProgrammation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicateurCible_Indicateur_IndicateurId",
                        column: x => x.IndicateurId,
                        principalSchema: "prog",
                        principalTable: "Indicateur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicateurCible_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "IndicateurRealisation",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    IndicateurId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    PeriodeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ValeurRealisee = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    JustificationEcart = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TransmisParId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_IndicateurRealisation_DateCreation"),
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
                    table.PrimaryKey("PK_IndicateurRealisation", x => x.Id);
                    table.CheckConstraint("CK_IndicateurRealisation_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_IndicateurRealisation_Indicateur_IndicateurId",
                        column: x => x.IndicateurId,
                        principalSchema: "prog",
                        principalTable: "Indicateur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicateurRealisation_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicateurRealisation_Periode_PeriodeId",
                        column: x => x.PeriodeId,
                        principalSchema: "cfg",
                        principalTable: "Periode",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicateurRealisation_Utilisateur_TransmisParId",
                        column: x => x.TransmisParId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EcheancierCP",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EngagementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    MontantCP = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EcheancierCP_DateCreation"),
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
                    table.PrimaryKey("PK_EcheancierCP", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcheancierCP_Engagement_EngagementId",
                        column: x => x.EngagementId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EcheancierCP_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Facture",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EngagementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateFacture = table.Column<DateOnly>(type: "date", nullable: false),
                    DateReception = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Facture_DateCreation"),
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
                    table.PrimaryKey("PK_Facture", x => x.Id);
                    table.CheckConstraint("CK_Facture_Montant", "[Montant] > 0");
                    table.CheckConstraint("CK_Facture_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Facture_Engagement_EngagementId",
                        column: x => x.EngagementId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Facture_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RejetPenalite",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EngagementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    TypePenaliteId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateConstat = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Motif = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RejetPenalite_DateCreation"),
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
                    table.PrimaryKey("PK_RejetPenalite", x => x.Id);
                    table.CheckConstraint("CK_RejetPenalite_Montant", "[Montant] > 0");
                    table.ForeignKey(
                        name: "FK_RejetPenalite_Engagement_EngagementId",
                        column: x => x.EngagementId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RejetPenalite_ListeValeur_TypePenaliteId",
                        column: x => x.TypePenaliteId,
                        principalSchema: "ref",
                        principalTable: "ListeValeur",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RejetPenalite_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Report",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceSourceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceCibleId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DotationSourceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DotationCibleId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EngagementSourceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    TypeReport = table.Column<byte>(type: "tinyint", nullable: false),
                    Origine = table.Column<byte>(type: "tinyint", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Report_DateCreation"),
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
                    table.PrimaryKey("PK_Report", x => x.Id);
                    table.CheckConstraint("CK_Report_Origine", "[Origine] IN (1, 2)");
                    table.CheckConstraint("CK_Report_TypeReport", "[TypeReport] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_Report_Dotation_DotationCibleId",
                        column: x => x.DotationCibleId,
                        principalSchema: "bud",
                        principalTable: "Dotation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Dotation_DotationSourceId",
                        column: x => x.DotationSourceId,
                        principalSchema: "bud",
                        principalTable: "Dotation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Engagement_EngagementSourceId",
                        column: x => x.EngagementSourceId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Exercice_ExerciceCibleId",
                        column: x => x.ExerciceCibleId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Exercice_ExerciceSourceId",
                        column: x => x.ExerciceSourceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Report_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ValidationDepassement",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DotationId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EngagementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    MontantDepassement = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TauxApres = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    Motif = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DecideParId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    DateDecision = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CommentaireDecision = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ValidationDepassement_DateCreation"),
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
                    table.PrimaryKey("PK_ValidationDepassement", x => x.Id);
                    table.CheckConstraint("CK_ValidationDepassement_Statut", "[Statut] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_ValidationDepassement_Dotation_DotationId",
                        column: x => x.DotationId,
                        principalSchema: "bud",
                        principalTable: "Dotation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ValidationDepassement_Engagement_EngagementId",
                        column: x => x.EngagementId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ValidationDepassement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ValidationDepassement_Utilisateur_DecideParId",
                        column: x => x.DecideParId,
                        principalSchema: "auth",
                        principalTable: "Utilisateur",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CadreLogiqueRisque",
                schema: "prog",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    CadreLogiqueElementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    RisqueId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    OrganismeId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CadreLogiqueRisque_DateCreation"),
                    CreePar = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CadreLogiqueRisque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CadreLogiqueRisque_CadreLogiqueElement_CadreLogiqueElementId",
                        column: x => x.CadreLogiqueElementId,
                        principalSchema: "prog",
                        principalTable: "CadreLogiqueElement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueRisque_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CadreLogiqueRisque_Risque_RisqueId",
                        column: x => x.RisqueId,
                        principalSchema: "ref",
                        principalTable: "Risque",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Paiement",
                schema: "bud",
                columns: table => new
                {
                    Id = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    ExerciceId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    EngagementId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: false),
                    FactureId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    NumeroPiece = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DatePaiement = table.Column<DateOnly>(type: "date", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ModePaiement = table.Column<byte>(type: "tinyint", nullable: false),
                    CompteBancaireId = table.Column<byte[]>(type: "binary(16)", fixedLength: true, maxLength: 16, nullable: true),
                    Statut = table.Column<byte>(type: "tinyint", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Paiement_DateCreation"),
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
                    table.PrimaryKey("PK_Paiement", x => x.Id);
                    table.CheckConstraint("CK_Paiement_ModePaiement", "[ModePaiement] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_Paiement_Montant", "[Montant] > 0");
                    table.CheckConstraint("CK_Paiement_Statut", "[Statut] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_Paiement_CompteBancaire_CompteBancaireId",
                        column: x => x.CompteBancaireId,
                        principalSchema: "ref",
                        principalTable: "CompteBancaire",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Paiement_Engagement_EngagementId",
                        column: x => x.EngagementId,
                        principalSchema: "bud",
                        principalTable: "Engagement",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Paiement_Exercice_ExerciceId",
                        column: x => x.ExerciceId,
                        principalSchema: "cfg",
                        principalTable: "Exercice",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Paiement_Facture_FactureId",
                        column: x => x.FactureId,
                        principalSchema: "bud",
                        principalTable: "Facture",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Paiement_Organisme_OrganismeId",
                        column: x => x.OrganismeId,
                        principalSchema: "plat",
                        principalTable: "Organisme",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Action_ProgrammeId",
                schema: "prog",
                table: "Action",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_Action_StructureId",
                schema: "prog",
                table: "Action",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_Action_OrganismeId_Code",
                schema: "prog",
                table: "Action",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Activite_ActionId",
                schema: "prog",
                table: "Activite",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Activite_NatureDepensePrincipaleId",
                schema: "prog",
                table: "Activite",
                column: "NatureDepensePrincipaleId");

            migrationBuilder.CreateIndex(
                name: "IX_Activite_StructureId",
                schema: "prog",
                table: "Activite",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_Activite_OrganismeId_Code",
                schema: "prog",
                table: "Activite",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Agent_CategorieEmploiId",
                schema: "ref",
                table: "Agent",
                column: "CategorieEmploiId");

            migrationBuilder.CreateIndex(
                name: "IX_Agent_SiteId",
                schema: "ref",
                table: "Agent",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Agent_StructureId",
                schema: "ref",
                table: "Agent",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_Agent_OrganismeId_Matricule",
                schema: "ref",
                table: "Agent",
                columns: new[] { "OrganismeId", "Matricule" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Ajustement_DotationId",
                schema: "bud",
                table: "Ajustement",
                column: "DotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Ajustement_MotifAjustementId",
                schema: "bud",
                table: "Ajustement",
                column: "MotifAjustementId");

            migrationBuilder.CreateIndex(
                name: "IX_Ajustement_ValideParId",
                schema: "bud",
                table: "Ajustement",
                column: "ValideParId");

            migrationBuilder.CreateIndex(
                name: "UX_Ajustement_OrganismeId_Numero",
                schema: "bud",
                table: "Ajustement",
                columns: new[] { "OrganismeId", "Numero" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseQualitative_ExerciceId_TypeAnalyse",
                schema: "prog",
                table: "AnalyseQualitative",
                columns: new[] { "ExerciceId", "TypeAnalyse" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseQualitative_OrganismeId",
                schema: "prog",
                table: "AnalyseQualitative",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseQualitative_ProgrammeId",
                schema: "prog",
                table: "AnalyseQualitative",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyseQualitative_RubriqueId",
                schema: "prog",
                table: "AnalyseQualitative",
                column: "RubriqueId");

            migrationBuilder.CreateIndex(
                name: "IX_AvancementPhysique_ContratId",
                schema: "bud",
                table: "AvancementPhysique",
                column: "ContratId");

            migrationBuilder.CreateIndex(
                name: "IX_AvancementPhysique_OrganismeId",
                schema: "bud",
                table: "AvancementPhysique",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_AvancementPhysique_PeriodeId",
                schema: "bud",
                table: "AvancementPhysique",
                column: "PeriodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AvancementPhysique_ProjetId_PeriodeId",
                schema: "bud",
                table: "AvancementPhysique",
                columns: new[] { "ProjetId", "PeriodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AvancementPhysique_TypeTravauxId",
                schema: "bud",
                table: "AvancementPhysique",
                column: "TypeTravauxId");

            migrationBuilder.CreateIndex(
                name: "UX_Axe_OrganismeId_Code",
                schema: "ref",
                table: "Axe",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_ActionId",
                schema: "prog",
                table: "CadreLogiqueElement",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_CycleProgrammationId",
                schema: "prog",
                table: "CadreLogiqueElement",
                column: "CycleProgrammationId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_ElementParentId",
                schema: "prog",
                table: "CadreLogiqueElement",
                column: "ElementParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_IndicateurId",
                schema: "prog",
                table: "CadreLogiqueElement",
                column: "IndicateurId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_OrganismeId",
                schema: "prog",
                table: "CadreLogiqueElement",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_ProgrammeId_CycleProgrammationId",
                schema: "prog",
                table: "CadreLogiqueElement",
                columns: new[] { "ProgrammeId", "CycleProgrammationId" });

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueElement_SourceVerificationId",
                schema: "prog",
                table: "CadreLogiqueElement",
                column: "SourceVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueRisque_OrganismeId",
                schema: "prog",
                table: "CadreLogiqueRisque",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_CadreLogiqueRisque_RisqueId",
                schema: "prog",
                table: "CadreLogiqueRisque",
                column: "RisqueId");

            migrationBuilder.CreateIndex(
                name: "UX_CadreLogiqueRisque_CadreLogiqueElementId_RisqueId",
                schema: "prog",
                table: "CadreLogiqueRisque",
                columns: new[] { "CadreLogiqueElementId", "RisqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CategorieEmploi_OrganismeId_Code",
                schema: "ref",
                table: "CategorieEmploi",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CleRepartition_ActiviteId",
                schema: "prog",
                table: "CleRepartition",
                column: "ActiviteId");

            migrationBuilder.CreateIndex(
                name: "IX_CleRepartition_LigneBudgetaireId",
                schema: "prog",
                table: "CleRepartition",
                column: "LigneBudgetaireId");

            migrationBuilder.CreateIndex(
                name: "IX_CleRepartition_OrganismeId",
                schema: "prog",
                table: "CleRepartition",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_CleRepartition_ExerciceId_LigneBudgetaireId_ActiviteId",
                schema: "prog",
                table: "CleRepartition",
                columns: new[] { "ExerciceId", "LigneBudgetaireId", "ActiviteId" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompteBancaire_CompteComptableId",
                schema: "ref",
                table: "CompteBancaire",
                column: "CompteComptableId");

            migrationBuilder.CreateIndex(
                name: "IX_CompteBancaire_DeviseId",
                schema: "ref",
                table: "CompteBancaire",
                column: "DeviseId");

            migrationBuilder.CreateIndex(
                name: "UX_CompteBancaire_OrganismeId_NumeroCompte",
                schema: "ref",
                table: "CompteBancaire",
                columns: new[] { "OrganismeId", "NumeroCompte" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CompteComptable_CompteParentId",
                schema: "ref",
                table: "CompteComptable",
                column: "CompteParentId");

            migrationBuilder.CreateIndex(
                name: "UX_CompteComptable_OrganismeId_Numero",
                schema: "ref",
                table: "CompteComptable",
                columns: new[] { "OrganismeId", "Numero" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Contrat_ContratTravauxId",
                schema: "bud",
                table: "Contrat",
                column: "ContratTravauxId");

            migrationBuilder.CreateIndex(
                name: "IX_Contrat_ProjetId",
                schema: "bud",
                table: "Contrat",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Contrat_TitulaireId",
                schema: "bud",
                table: "Contrat",
                column: "TitulaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Contrat_TypePrestationId",
                schema: "bud",
                table: "Contrat",
                column: "TypePrestationId");

            migrationBuilder.CreateIndex(
                name: "UX_Contrat_OrganismeId_Numero",
                schema: "bud",
                table: "Contrat",
                columns: new[] { "OrganismeId", "Numero" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CycleProgrammation_CyclePrecedentId",
                schema: "cfg",
                table: "CycleProgrammation",
                column: "CyclePrecedentId");

            migrationBuilder.CreateIndex(
                name: "UX_CycleProgrammation_OrganismeId_AnneeBase",
                schema: "cfg",
                table: "CycleProgrammation",
                columns: new[] { "OrganismeId", "AnneeBase" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Devise_OrganismeId_CodeIso",
                schema: "cfg",
                table: "Devise",
                columns: new[] { "OrganismeId", "CodeIso" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_ExerciceId_LigneBudgetaireId",
                schema: "bud",
                table: "Dotation",
                columns: new[] { "ExerciceId", "LigneBudgetaireId" });

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_LigneBudgetaireId",
                schema: "bud",
                table: "Dotation",
                column: "LigneBudgetaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_OrganismeId",
                schema: "bud",
                table: "Dotation",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_ProjetId",
                schema: "bud",
                table: "Dotation",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_SiteId",
                schema: "bud",
                table: "Dotation",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_StructureId",
                schema: "bud",
                table: "Dotation",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "IX_Dotation_TiersId",
                schema: "bud",
                table: "Dotation",
                column: "TiersId");

            migrationBuilder.CreateIndex(
                name: "IX_EcheanceRecouvrement_OrganismeId_DateEcheance",
                schema: "rec",
                table: "EcheanceRecouvrement",
                columns: new[] { "OrganismeId", "DateEcheance" });

            migrationBuilder.CreateIndex(
                name: "IX_EcheanceRecouvrement_PrevisionRecetteId",
                schema: "rec",
                table: "EcheanceRecouvrement",
                column: "PrevisionRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_EcheancierCP_OrganismeId",
                schema: "bud",
                table: "EcheancierCP",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_EcheancierCP_EngagementId_Annee",
                schema: "bud",
                table: "EcheancierCP",
                columns: new[] { "EngagementId", "Annee" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Encaissement_CompteBancaireId",
                schema: "rec",
                table: "Encaissement",
                column: "CompteBancaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Encaissement_ExerciceId_PeriodeId",
                schema: "rec",
                table: "Encaissement",
                columns: new[] { "ExerciceId", "PeriodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Encaissement_OrganismeId",
                schema: "rec",
                table: "Encaissement",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_Encaissement_PeriodeId",
                schema: "rec",
                table: "Encaissement",
                column: "PeriodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Encaissement_PrevisionRecetteId",
                schema: "rec",
                table: "Encaissement",
                column: "PrevisionRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_ContratId",
                schema: "bud",
                table: "Engagement",
                column: "ContratId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_DotationId",
                schema: "bud",
                table: "Engagement",
                column: "DotationId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_EngagementOrigineId",
                schema: "bud",
                table: "Engagement",
                column: "EngagementOrigineId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_ExerciceId_Statut",
                schema: "bud",
                table: "Engagement",
                columns: new[] { "ExerciceId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_TiersId",
                schema: "bud",
                table: "Engagement",
                column: "TiersId");

            migrationBuilder.CreateIndex(
                name: "IX_Engagement_ValideParId",
                schema: "bud",
                table: "Engagement",
                column: "ValideParId");

            migrationBuilder.CreateIndex(
                name: "UX_Engagement_OrganismeId_Numero",
                schema: "bud",
                table: "Engagement",
                columns: new[] { "OrganismeId", "Numero" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EnveloppeMaitreOuvrage_MaitreOuvrageId",
                schema: "bud",
                table: "EnveloppeMaitreOuvrage",
                column: "MaitreOuvrageId");

            migrationBuilder.CreateIndex(
                name: "IX_EnveloppeMaitreOuvrage_OrganismeId",
                schema: "bud",
                table: "EnveloppeMaitreOuvrage",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_EnveloppeMaitreOuvrage_RubriqueId",
                schema: "bud",
                table: "EnveloppeMaitreOuvrage",
                column: "RubriqueId");

            migrationBuilder.CreateIndex(
                name: "UX_EnveloppeMaitreOuvrage_ExerciceId_MaitreOuvrageId_RubriqueId",
                schema: "bud",
                table: "EnveloppeMaitreOuvrage",
                columns: new[] { "ExerciceId", "MaitreOuvrageId", "RubriqueId" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionHistorique_AxeId",
                schema: "hist",
                table: "ExecutionHistorique",
                column: "AxeId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionHistorique_NatureDepenseId",
                schema: "hist",
                table: "ExecutionHistorique",
                column: "NatureDepenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionHistorique_NatureRecetteId",
                schema: "hist",
                table: "ExecutionHistorique",
                column: "NatureRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionHistorique_OrganismeId_Annee",
                schema: "hist",
                table: "ExecutionHistorique",
                columns: new[] { "OrganismeId", "Annee" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionHistorique_RubriqueId",
                schema: "hist",
                table: "ExecutionHistorique",
                column: "RubriqueId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionHistorique_SiteId",
                schema: "hist",
                table: "ExecutionHistorique",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercice_ClotureParId",
                schema: "cfg",
                table: "Exercice",
                column: "ClotureParId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercice_CycleProgrammationId",
                schema: "cfg",
                table: "Exercice",
                column: "CycleProgrammationId");

            migrationBuilder.CreateIndex(
                name: "UX_Exercice_OrganismeId_Annee",
                schema: "cfg",
                table: "Exercice",
                columns: new[] { "OrganismeId", "Annee" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Facture_OrganismeId",
                schema: "bud",
                table: "Facture",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_Facture_EngagementId_Numero",
                schema: "bud",
                table: "Facture",
                columns: new[] { "EngagementId", "Numero" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Fichier_OrganismeId",
                schema: "cfg",
                table: "Fichier",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_Habilitation_DemandeParId",
                schema: "auth",
                table: "Habilitation",
                column: "DemandeParId");

            migrationBuilder.CreateIndex(
                name: "IX_Habilitation_OrganismeId_Statut",
                schema: "auth",
                table: "Habilitation",
                columns: new[] { "OrganismeId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_Habilitation_UtilisateurId",
                schema: "auth",
                table: "Habilitation",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Habilitation_ValideurAdminSIId",
                schema: "auth",
                table: "Habilitation",
                column: "ValideurAdminSIId");

            migrationBuilder.CreateIndex(
                name: "IX_Habilitation_ValideurFinancierId",
                schema: "auth",
                table: "Habilitation",
                column: "ValideurFinancierId");

            migrationBuilder.CreateIndex(
                name: "IX_Immobilisation_CompteComptableId",
                schema: "bud",
                table: "Immobilisation",
                column: "CompteComptableId");

            migrationBuilder.CreateIndex(
                name: "IX_Immobilisation_ProjetId",
                schema: "bud",
                table: "Immobilisation",
                column: "ProjetId");

            migrationBuilder.CreateIndex(
                name: "IX_Immobilisation_SiteId",
                schema: "bud",
                table: "Immobilisation",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Immobilisation_StructureId",
                schema: "bud",
                table: "Immobilisation",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_Immobilisation_OrganismeId_Code",
                schema: "bud",
                table: "Immobilisation",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Indicateur_ActionId",
                schema: "prog",
                table: "Indicateur",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicateur_PeriodiciteId",
                schema: "prog",
                table: "Indicateur",
                column: "PeriodiciteId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicateur_ProgrammeId",
                schema: "prog",
                table: "Indicateur",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicateur_SourceVerificationId",
                schema: "prog",
                table: "Indicateur",
                column: "SourceVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicateur_StructureId",
                schema: "prog",
                table: "Indicateur",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicateur_UniteMesureId",
                schema: "prog",
                table: "Indicateur",
                column: "UniteMesureId");

            migrationBuilder.CreateIndex(
                name: "UX_Indicateur_OrganismeId_Code",
                schema: "prog",
                table: "Indicateur",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndicateurCible_CycleProgrammationId",
                schema: "prog",
                table: "IndicateurCible",
                column: "CycleProgrammationId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicateurCible_OrganismeId",
                schema: "prog",
                table: "IndicateurCible",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_IndicateurCible_IndicateurId_CycleProgrammationId_Annee",
                schema: "prog",
                table: "IndicateurCible",
                columns: new[] { "IndicateurId", "CycleProgrammationId", "Annee" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_IndicateurRealisation_OrganismeId",
                schema: "prog",
                table: "IndicateurRealisation",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicateurRealisation_PeriodeId",
                schema: "prog",
                table: "IndicateurRealisation",
                column: "PeriodeId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicateurRealisation_TransmisParId",
                schema: "prog",
                table: "IndicateurRealisation",
                column: "TransmisParId");

            migrationBuilder.CreateIndex(
                name: "UX_IndicateurRealisation_IndicateurId_PeriodeId",
                schema: "prog",
                table: "IndicateurRealisation",
                columns: new[] { "IndicateurId", "PeriodeId" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalAudit_DateAction",
                schema: "aud",
                table: "JournalAudit",
                column: "DateAction");

            migrationBuilder.CreateIndex(
                name: "IX_JournalAudit_Module_DateAction",
                schema: "aud",
                table: "JournalAudit",
                columns: new[] { "Module", "DateAction" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalAudit_OrganismeId_DateAction",
                schema: "aud",
                table: "JournalAudit",
                columns: new[] { "OrganismeId", "DateAction" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalConnexion_DateHeure",
                schema: "auth",
                table: "JournalConnexion",
                column: "DateHeure");

            migrationBuilder.CreateIndex(
                name: "IX_JournalConnexion_OrganismeId",
                schema: "auth",
                table: "JournalConnexion",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalConnexion_UtilisateurId_DateHeure",
                schema: "auth",
                table: "JournalConnexion",
                columns: new[] { "UtilisateurId", "DateHeure" });

            migrationBuilder.CreateIndex(
                name: "IX_LigneBudgetaire_NatureDepenseId",
                schema: "ref",
                table: "LigneBudgetaire",
                column: "NatureDepenseId");

            migrationBuilder.CreateIndex(
                name: "IX_LigneBudgetaire_NatureRecetteId",
                schema: "ref",
                table: "LigneBudgetaire",
                column: "NatureRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_LigneBudgetaire_SourceFinancementId",
                schema: "ref",
                table: "LigneBudgetaire",
                column: "SourceFinancementId");

            migrationBuilder.CreateIndex(
                name: "IX_LigneBudgetaire_SousRubriqueId",
                schema: "ref",
                table: "LigneBudgetaire",
                column: "SousRubriqueId");

            migrationBuilder.CreateIndex(
                name: "UX_LigneBudgetaire_OrganismeId_Code",
                schema: "ref",
                table: "LigneBudgetaire",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LigneCompte_CompteComptableId",
                schema: "ref",
                table: "LigneCompte",
                column: "CompteComptableId");

            migrationBuilder.CreateIndex(
                name: "IX_LigneCompte_OrganismeId",
                schema: "ref",
                table: "LigneCompte",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_LigneCompte_LigneBudgetaireId_CompteComptableId",
                schema: "ref",
                table: "LigneCompte",
                columns: new[] { "LigneBudgetaireId", "CompteComptableId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ListeValeur_OrganismeId_Categorie_Code",
                schema: "ref",
                table: "ListeValeur",
                columns: new[] { "OrganismeId", "Categorie", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_MotifAjustement_OrganismeId_Code",
                schema: "ref",
                table: "MotifAjustement",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_NatureDepense_OrganismeId_Code",
                schema: "ref",
                table: "NatureDepense",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_NatureRecette_CompteComptableId",
                schema: "ref",
                table: "NatureRecette",
                column: "CompteComptableId");

            migrationBuilder.CreateIndex(
                name: "IX_NatureRecette_NatureParentId",
                schema: "ref",
                table: "NatureRecette",
                column: "NatureParentId");

            migrationBuilder.CreateIndex(
                name: "UX_NatureRecette_OrganismeId_Code",
                schema: "ref",
                table: "NatureRecette",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_OrganismeId",
                schema: "ntf",
                table: "Notification",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UtilisateurId_EstLue_DateCreation",
                schema: "ntf",
                table: "Notification",
                columns: new[] { "UtilisateurId", "EstLue", "DateCreation" });

            migrationBuilder.CreateIndex(
                name: "UX_Organisme_Code",
                schema: "plat",
                table: "Organisme",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Paiement_CompteBancaireId",
                schema: "bud",
                table: "Paiement",
                column: "CompteBancaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Paiement_EngagementId",
                schema: "bud",
                table: "Paiement",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_Paiement_ExerciceId_DatePaiement",
                schema: "bud",
                table: "Paiement",
                columns: new[] { "ExerciceId", "DatePaiement" });

            migrationBuilder.CreateIndex(
                name: "IX_Paiement_FactureId",
                schema: "bud",
                table: "Paiement",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "UX_Paiement_OrganismeId_NumeroPiece",
                schema: "bud",
                table: "Paiement",
                columns: new[] { "OrganismeId", "NumeroPiece" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_ParametreNotification_Type",
                schema: "ntf",
                table: "ParametreNotification",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParametreOrganisme_DeviseReferenceId",
                schema: "cfg",
                table: "ParametreOrganisme",
                column: "DeviseReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ParametreOrganisme_LogoFichierId",
                schema: "cfg",
                table: "ParametreOrganisme",
                column: "LogoFichierId");

            migrationBuilder.CreateIndex(
                name: "UX_ParametreOrganisme_OrganismeId",
                schema: "cfg",
                table: "ParametreOrganisme",
                column: "OrganismeId",
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Periode_OrganismeId",
                schema: "cfg",
                table: "Periode",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_Periode_ExerciceId_TypePeriode_Numero",
                schema: "cfg",
                table: "Periode",
                columns: new[] { "ExerciceId", "TypePeriode", "Numero" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PrevisionRecette_ExerciceId_NatureRecetteId_SiteId",
                schema: "rec",
                table: "PrevisionRecette",
                columns: new[] { "ExerciceId", "NatureRecetteId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_PrevisionRecette_LigneBudgetaireId",
                schema: "rec",
                table: "PrevisionRecette",
                column: "LigneBudgetaireId");

            migrationBuilder.CreateIndex(
                name: "IX_PrevisionRecette_NatureRecetteId",
                schema: "rec",
                table: "PrevisionRecette",
                column: "NatureRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_PrevisionRecette_OrganismeId",
                schema: "rec",
                table: "PrevisionRecette",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_PrevisionRecette_SiteId",
                schema: "rec",
                table: "PrevisionRecette",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PrevisionRecette_TiersCollecteurId",
                schema: "rec",
                table: "PrevisionRecette",
                column: "TiersCollecteurId");

            migrationBuilder.CreateIndex(
                name: "UX_Profil_Code",
                schema: "auth",
                table: "Profil",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProfilDroit_ProfilId_TypeDroit",
                schema: "auth",
                table: "ProfilDroit",
                columns: new[] { "ProfilId", "TypeDroit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationEmploi_CategorieEmploiId",
                schema: "prog",
                table: "ProgrammationEmploi",
                column: "CategorieEmploiId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationEmploi_OrganismeId",
                schema: "prog",
                table: "ProgrammationEmploi",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationEmploi_ProgrammeId",
                schema: "prog",
                table: "ProgrammationEmploi",
                column: "ProgrammeId");

            migrationBuilder.CreateIndex(
                name: "UX_ProgrammationEmploi_CycleProgrammationId_Annee_ProgrammeId_CategorieEmploiId",
                schema: "prog",
                table: "ProgrammationEmploi",
                columns: new[] { "CycleProgrammationId", "Annee", "ProgrammeId", "CategorieEmploiId" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationFinanciere_ActionId",
                schema: "prog",
                table: "ProgrammationFinanciere",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationFinanciere_ActiviteId",
                schema: "prog",
                table: "ProgrammationFinanciere",
                column: "ActiviteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationFinanciere_CycleProgrammationId_Annee_ActionId",
                schema: "prog",
                table: "ProgrammationFinanciere",
                columns: new[] { "CycleProgrammationId", "Annee", "ActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationFinanciere_NatureDepenseId",
                schema: "prog",
                table: "ProgrammationFinanciere",
                column: "NatureDepenseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationFinanciere_OrganismeId",
                schema: "prog",
                table: "ProgrammationFinanciere",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationFinanciere_SourceFinancementId",
                schema: "prog",
                table: "ProgrammationFinanciere",
                column: "SourceFinancementId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationRessource_NatureRecetteId",
                schema: "prog",
                table: "ProgrammationRessource",
                column: "NatureRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationRessource_OrganismeId",
                schema: "prog",
                table: "ProgrammationRessource",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammationRessource_SourceFinancementId",
                schema: "prog",
                table: "ProgrammationRessource",
                column: "SourceFinancementId");

            migrationBuilder.CreateIndex(
                name: "UX_ProgrammationRessource_CycleProgrammationId_Annee_NatureRecetteId_SourceFinancementId",
                schema: "prog",
                table: "ProgrammationRessource",
                columns: new[] { "CycleProgrammationId", "Annee", "NatureRecetteId", "SourceFinancementId" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Programme_ResponsableId",
                schema: "prog",
                table: "Programme",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "UX_Programme_OrganismeId_Code",
                schema: "prog",
                table: "Programme",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeStructure_OrganismeId",
                schema: "prog",
                table: "ProgrammeStructure",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgrammeStructure_StructureId",
                schema: "prog",
                table: "ProgrammeStructure",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_ProgrammeStructure_ProgrammeId_StructureId",
                schema: "prog",
                table: "ProgrammeStructure",
                columns: new[] { "ProgrammeId", "StructureId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projet_AxeId",
                schema: "bud",
                table: "Projet",
                column: "AxeId");

            migrationBuilder.CreateIndex(
                name: "IX_Projet_OrganismeId_AxeId_Statut",
                schema: "bud",
                table: "Projet",
                columns: new[] { "OrganismeId", "AxeId", "Statut" });

            migrationBuilder.CreateIndex(
                name: "IX_Projet_SiteId",
                schema: "bud",
                table: "Projet",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Projet_SourceFinancementId",
                schema: "bud",
                table: "Projet",
                column: "SourceFinancementId");

            migrationBuilder.CreateIndex(
                name: "IX_Projet_StructureId",
                schema: "bud",
                table: "Projet",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "IX_Projet_TiersBeneficiaireId",
                schema: "bud",
                table: "Projet",
                column: "TiersBeneficiaireId");

            migrationBuilder.CreateIndex(
                name: "UX_Projet_OrganismeId_Code",
                schema: "bud",
                table: "Projet",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetTravaux_MaitreOuvrageId",
                schema: "bud",
                table: "ProjetTravaux",
                column: "MaitreOuvrageId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetTravaux_OrganismeId",
                schema: "bud",
                table: "ProjetTravaux",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetTravaux_TypeTravauxId",
                schema: "bud",
                table: "ProjetTravaux",
                column: "TypeTravauxId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetTravaux_TypeVoieId",
                schema: "bud",
                table: "ProjetTravaux",
                column: "TypeVoieId");

            migrationBuilder.CreateIndex(
                name: "IX_RejetPenalite_EngagementId",
                schema: "bud",
                table: "RejetPenalite",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_RejetPenalite_OrganismeId",
                schema: "bud",
                table: "RejetPenalite",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_RejetPenalite_TypePenaliteId",
                schema: "bud",
                table: "RejetPenalite",
                column: "TypePenaliteId");

            migrationBuilder.CreateIndex(
                name: "IX_RepartitionTemps_ActionId",
                schema: "prog",
                table: "RepartitionTemps",
                column: "ActionId");

            migrationBuilder.CreateIndex(
                name: "IX_RepartitionTemps_ActiviteId",
                schema: "prog",
                table: "RepartitionTemps",
                column: "ActiviteId");

            migrationBuilder.CreateIndex(
                name: "IX_RepartitionTemps_AgentId",
                schema: "prog",
                table: "RepartitionTemps",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_RepartitionTemps_OrganismeId",
                schema: "prog",
                table: "RepartitionTemps",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_RepartitionTemps_ExerciceId_AgentId_ActionId_ActiviteId",
                schema: "prog",
                table: "RepartitionTemps",
                columns: new[] { "ExerciceId", "AgentId", "ActionId", "ActiviteId" },
                unique: true,
                filter: "[ActiviteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Report_DotationCibleId",
                schema: "bud",
                table: "Report",
                column: "DotationCibleId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_DotationSourceId",
                schema: "bud",
                table: "Report",
                column: "DotationSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_EngagementSourceId",
                schema: "bud",
                table: "Report",
                column: "EngagementSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_ExerciceCibleId_TypeReport",
                schema: "bud",
                table: "Report",
                columns: new[] { "ExerciceCibleId", "TypeReport" });

            migrationBuilder.CreateIndex(
                name: "IX_Report_ExerciceSourceId",
                schema: "bud",
                table: "Report",
                column: "ExerciceSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Report_OrganismeId",
                schema: "bud",
                table: "Report",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRessource_ExerciceId",
                schema: "rec",
                table: "ReportRessource",
                column: "ExerciceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRessource_NatureRecetteId",
                schema: "rec",
                table: "ReportRessource",
                column: "NatureRecetteId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRessource_OrganismeId",
                schema: "rec",
                table: "ReportRessource",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_Risque_CategorieRisqueId",
                schema: "ref",
                table: "Risque",
                column: "CategorieRisqueId");

            migrationBuilder.CreateIndex(
                name: "UX_Risque_OrganismeId_Code",
                schema: "ref",
                table: "Risque",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Rubrique_AxeId",
                schema: "ref",
                table: "Rubrique",
                column: "AxeId");

            migrationBuilder.CreateIndex(
                name: "UX_Rubrique_OrganismeId_Code",
                schema: "ref",
                table: "Rubrique",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SeuilAlerte_AxeId",
                schema: "cfg",
                table: "SeuilAlerte",
                column: "AxeId");

            migrationBuilder.CreateIndex(
                name: "IX_SeuilAlerte_LigneBudgetaireId",
                schema: "cfg",
                table: "SeuilAlerte",
                column: "LigneBudgetaireId");

            migrationBuilder.CreateIndex(
                name: "IX_SeuilAlerte_OrganismeId",
                schema: "cfg",
                table: "SeuilAlerte",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_Site_OrganismeId_Code",
                schema: "ref",
                table: "Site",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SiteTiers_OrganismeId",
                schema: "ref",
                table: "SiteTiers",
                column: "OrganismeId");

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

            migrationBuilder.CreateIndex(
                name: "IX_SourceFinancement_PartenaireFinancierId",
                schema: "ref",
                table: "SourceFinancement",
                column: "PartenaireFinancierId");

            migrationBuilder.CreateIndex(
                name: "UX_SourceFinancement_OrganismeId_Code",
                schema: "ref",
                table: "SourceFinancement",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SousRubrique_RubriqueId",
                schema: "ref",
                table: "SousRubrique",
                column: "RubriqueId");

            migrationBuilder.CreateIndex(
                name: "UX_SousRubrique_OrganismeId_Code",
                schema: "ref",
                table: "SousRubrique",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Structure_StructureParentId",
                schema: "ref",
                table: "Structure",
                column: "StructureParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Structure_TypeStructureId",
                schema: "ref",
                table: "Structure",
                column: "TypeStructureId");

            migrationBuilder.CreateIndex(
                name: "UX_Structure_OrganismeId_Code",
                schema: "ref",
                table: "Structure",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TauxChange_OrganismeId",
                schema: "cfg",
                table: "TauxChange",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "UX_TauxChange_DeviseId_DateEffet",
                schema: "cfg",
                table: "TauxChange",
                columns: new[] { "DeviseId", "DateEffet" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_CategorieEntiteId",
                schema: "ref",
                table: "Tiers",
                column: "CategorieEntiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_CompteAuxiliaireId",
                schema: "ref",
                table: "Tiers",
                column: "CompteAuxiliaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_CompteGeneralId",
                schema: "ref",
                table: "Tiers",
                column: "CompteGeneralId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_OrganismeId_TypeTiers",
                schema: "ref",
                table: "Tiers",
                columns: new[] { "OrganismeId", "TypeTiers" });

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_TiersParentId",
                schema: "ref",
                table: "Tiers",
                column: "TiersParentId");

            migrationBuilder.CreateIndex(
                name: "UX_Tiers_OrganismeId_Code",
                schema: "ref",
                table: "Tiers",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TypeTravaux_TypeTravauxControleId",
                schema: "ref",
                table: "TypeTravaux",
                column: "TypeTravauxControleId");

            migrationBuilder.CreateIndex(
                name: "IX_TypeTravaux_TypeVoieId",
                schema: "ref",
                table: "TypeTravaux",
                column: "TypeVoieId");

            migrationBuilder.CreateIndex(
                name: "UX_TypeTravaux_OrganismeId_Code",
                schema: "ref",
                table: "TypeTravaux",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_UniteMesure_OrganismeId_Code",
                schema: "ref",
                table: "UniteMesure",
                columns: new[] { "OrganismeId", "Code" },
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateur_OrganismeId",
                schema: "auth",
                table: "Utilisateur",
                column: "OrganismeId");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateur_StructureId",
                schema: "auth",
                table: "Utilisateur",
                column: "StructureId");

            migrationBuilder.CreateIndex(
                name: "UX_Utilisateur_Email",
                schema: "auth",
                table: "Utilisateur",
                column: "Email",
                unique: true,
                filter: "[Email] <> N''");

            migrationBuilder.CreateIndex(
                name: "UX_Utilisateur_Matricule",
                schema: "auth",
                table: "Utilisateur",
                column: "Matricule",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateurDroit_UtilisateurId_TypeDroit",
                schema: "auth",
                table: "UtilisateurDroit",
                columns: new[] { "UtilisateurId", "TypeDroit" });

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateurProfil_ProfilId",
                schema: "auth",
                table: "UtilisateurProfil",
                column: "ProfilId");

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateurProfil_UtilisateurId_ProfilId",
                schema: "auth",
                table: "UtilisateurProfil",
                columns: new[] { "UtilisateurId", "ProfilId" });

            migrationBuilder.CreateIndex(
                name: "IX_ValidationDepassement_DecideParId",
                schema: "bud",
                table: "ValidationDepassement",
                column: "DecideParId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationDepassement_DotationId",
                schema: "bud",
                table: "ValidationDepassement",
                column: "DotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationDepassement_EngagementId",
                schema: "bud",
                table: "ValidationDepassement",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_ValidationDepassement_OrganismeId_Statut",
                schema: "bud",
                table: "ValidationDepassement",
                columns: new[] { "OrganismeId", "Statut" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ajustement",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "AnalyseQualitative",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "AvancementPhysique",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "CadreLogiqueRisque",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "CleRepartition",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "EcheanceRecouvrement",
                schema: "rec");

            migrationBuilder.DropTable(
                name: "EcheancierCP",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "Encaissement",
                schema: "rec");

            migrationBuilder.DropTable(
                name: "EnveloppeMaitreOuvrage",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "ExecutionHistorique",
                schema: "hist");

            migrationBuilder.DropTable(
                name: "Habilitation",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Immobilisation",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "IndicateurCible",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "IndicateurRealisation",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "JournalAudit",
                schema: "aud");

            migrationBuilder.DropTable(
                name: "JournalConnexion",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "LigneCompte",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Notification",
                schema: "ntf");

            migrationBuilder.DropTable(
                name: "Paiement",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "ParametreNotification",
                schema: "ntf");

            migrationBuilder.DropTable(
                name: "ParametreOrganisme",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "ProfilDroit",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "ProgrammationEmploi",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "ProgrammationFinanciere",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "ProgrammationRessource",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "ProgrammeStructure",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "ProjetTravaux",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "RejetPenalite",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "RepartitionTemps",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "Report",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "ReportRessource",
                schema: "rec");

            migrationBuilder.DropTable(
                name: "SeuilAlerte",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "SiteTiers",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "TauxChange",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "UtilisateurDroit",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "UtilisateurProfil",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "ValidationDepassement",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "MotifAjustement",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "CadreLogiqueElement",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "Risque",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "PrevisionRecette",
                schema: "rec");

            migrationBuilder.DropTable(
                name: "Periode",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "CompteBancaire",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Facture",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "Fichier",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "TypeTravaux",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Activite",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "Agent",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Profil",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Indicateur",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "Devise",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "Engagement",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "CategorieEmploi",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Action",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "UniteMesure",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Contrat",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "Dotation",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "Programme",
                schema: "prog");

            migrationBuilder.DropTable(
                name: "Exercice",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "LigneBudgetaire",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Projet",
                schema: "bud");

            migrationBuilder.DropTable(
                name: "CycleProgrammation",
                schema: "cfg");

            migrationBuilder.DropTable(
                name: "Utilisateur",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "NatureDepense",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "NatureRecette",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "SousRubrique",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Site",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "SourceFinancement",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Structure",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Rubrique",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Tiers",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Axe",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "CompteComptable",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "ListeValeur",
                schema: "ref");

            migrationBuilder.DropTable(
                name: "Organisme",
                schema: "plat");
        }
    }
}
