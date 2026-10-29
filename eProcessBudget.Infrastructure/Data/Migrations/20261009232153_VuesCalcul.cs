using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eProcessBudget.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Vues de calcul du cahier technique (§ 2.8 et § 3.5), mappées dans EF Core comme
    /// entités sans clé (Core/Entities/Vues.cs). EF Core ne génère pas les vues : leur SQL est
    /// écrit ici, figé avec la migration.
    ///
    /// Pour faire évoluer une vue après application de cette migration : ne pas modifier ce
    /// fichier, créer une nouvelle migration qui rejoue CREATE OR ALTER VIEW avec le nouveau SQL.
    ///
    /// Conventions de calcul :
    ///   - seules les pièces au statut Validé (StatutSaisie = 3) sont comptées ;
    ///   - les lignes supprimées logiquement (EstSupprime = 1) sont exclues ;
    ///   - la réalisation des dépenses est mesurée par les paiements (C.2.2) ;
    ///   - les taux sont en pourcentage ; un dénominateur nul donne NULL (« n.s. »).
    /// </summary>
    public partial class VuesCalcul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SituationDotation);
            // Dépend de bud.vw_SituationDotation : créée après elle.
            migrationBuilder.Sql(ConsommationActivite);
            migrationBuilder.Sql(AtteinteIndicateur);
            migrationBuilder.Sql(SituationRecette);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [rec].[vw_SituationRecette];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [prog].[vw_AtteinteIndicateur];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [prog].[vw_ConsommationActivite];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [bud].[vw_SituationDotation];");
        }

        /// <summary>
        /// Situation de chaque dotation : prévision ajustée, reports, AE, paiements, restes à
        /// payer, encours à reporter et taux sur deux bases (période seule / y compris reports).
        /// Les engagements EstReport = 1 sont les encours reportés d'exercices antérieurs.
        /// </summary>
        private const string SituationDotation = """
            CREATE OR ALTER VIEW [bud].[vw_SituationDotation] AS
            WITH Base AS (
                SELECT
                    d.Id AS DotationId, d.OrganismeId, d.ExerciceId,
                    r.AxeId, sr.RubriqueId, l.SousRubriqueId, d.LigneBudgetaireId,
                    l.NatureDepenseId, l.SourceFinancementId,
                    d.ProjetId, d.SiteId, d.StructureId, d.TiersId,
                    d.MontantInitial,
                    ISNULL(aj.Total, 0)            AS Ajustements,
                    ISNULL(rp.Total, 0)            AS Reports,
                    ISNULL(en.AEPeriode, 0)        AS AEPeriode,
                    ISNULL(en.AEReports, 0)        AS AEReports,
                    ISNULL(pa.PaiementsPeriode, 0) AS PaiementsPeriode,
                    ISNULL(pa.PaiementsTotal, 0)   AS PaiementsTotal,
                    ISNULL(rj.Total, 0)            AS RejetsPenalites
                FROM [bud].[Dotation] d
                JOIN [ref].[LigneBudgetaire] l ON l.Id = d.LigneBudgetaireId
                JOIN [ref].[SousRubrique] sr   ON sr.Id = l.SousRubriqueId
                JOIN [ref].[Rubrique] r        ON r.Id = sr.RubriqueId
                OUTER APPLY (
                    SELECT SUM(a.Montant) AS Total
                    FROM [bud].[Ajustement] a
                    WHERE a.DotationId = d.Id AND a.EstSupprime = 0 AND a.Statut = 3
                ) aj
                OUTER APPLY (
                    SELECT SUM(x.Montant) AS Total
                    FROM [bud].[Report] x
                    WHERE x.DotationCibleId = d.Id AND x.EstSupprime = 0
                ) rp
                OUTER APPLY (
                    SELECT SUM(CASE WHEN e.EstReport = 0 THEN e.Montant END) AS AEPeriode,
                           SUM(CASE WHEN e.EstReport = 1 THEN e.Montant END) AS AEReports
                    FROM [bud].[Engagement] e
                    WHERE e.DotationId = d.Id AND e.EstSupprime = 0 AND e.Statut = 3
                ) en
                OUTER APPLY (
                    SELECT SUM(CASE WHEN e.EstReport = 0 THEN p.Montant END) AS PaiementsPeriode,
                           SUM(p.Montant) AS PaiementsTotal
                    FROM [bud].[Paiement] p
                    JOIN [bud].[Engagement] e ON e.Id = p.EngagementId
                    WHERE e.DotationId = d.Id AND e.EstSupprime = 0 AND e.Statut = 3
                      AND p.EstSupprime = 0 AND p.Statut = 3
                ) pa
                OUTER APPLY (
                    SELECT SUM(j.Montant) AS Total
                    FROM [bud].[RejetPenalite] j
                    JOIN [bud].[Engagement] e ON e.Id = j.EngagementId
                    WHERE e.DotationId = d.Id AND e.EstSupprime = 0 AND e.Statut = 3
                      AND j.EstSupprime = 0
                ) rj
                WHERE d.EstSupprime = 0
            ),
            Calcul AS (
                SELECT Base.*,
                       MontantInitial + Ajustements           AS PrevisionAjustee,
                       MontantInitial + Ajustements + Reports AS DotationTotale,
                       AEPeriode + AEReports                  AS AETotal
                FROM Base
            )
            SELECT
                DotationId, OrganismeId, ExerciceId,
                AxeId, RubriqueId, SousRubriqueId, LigneBudgetaireId,
                NatureDepenseId, SourceFinancementId,
                ProjetId, SiteId, StructureId, TiersId,
                CAST(MontantInitial   AS decimal(18,2)) AS MontantInitial,
                CAST(Ajustements      AS decimal(18,2)) AS Ajustements,
                CAST(PrevisionAjustee AS decimal(18,2)) AS PrevisionAjustee,
                CAST(Reports          AS decimal(18,2)) AS Reports,
                CAST(DotationTotale   AS decimal(18,2)) AS DotationTotale,
                CAST(AEPeriode        AS decimal(18,2)) AS AEPeriode,
                CAST(AEReports        AS decimal(18,2)) AS AEReports,
                CAST(AETotal          AS decimal(18,2)) AS AETotal,
                CAST(PaiementsPeriode AS decimal(18,2)) AS PaiementsPeriode,
                CAST(PaiementsTotal   AS decimal(18,2)) AS PaiementsTotal,
                CAST(RejetsPenalites  AS decimal(18,2)) AS RejetsPenalites,
                CAST(AEPeriode - PaiementsPeriode                AS decimal(18,2)) AS ResteAPayerPeriode,
                CAST(AETotal - PaiementsTotal                    AS decimal(18,2)) AS ResteAPayerYCReports,
                CAST(AETotal - PaiementsTotal - RejetsPenalites  AS decimal(18,2)) AS EncoursAReporter,
                CAST(DotationTotale - PaiementsTotal             AS decimal(18,2)) AS Ecart,
                CAST(100.0 * AEPeriode        / NULLIF(PrevisionAjustee, 0) AS decimal(19,4)) AS TauxEngagementPeriode,
                CAST(100.0 * AETotal          / NULLIF(DotationTotale, 0)   AS decimal(19,4)) AS TauxEngagementYCReports,
                CAST(100.0 * PaiementsPeriode / NULLIF(PrevisionAjustee, 0) AS decimal(19,4)) AS TauxConsommationPeriode,
                CAST(100.0 * PaiementsTotal   / NULLIF(DotationTotale, 0)   AS decimal(19,4)) AS TauxConsommationYCReports,
                CAST(100.0 * PaiementsTotal   / NULLIF(AETotal, 0)          AS decimal(19,4)) AS TauxConsommationAD
            FROM Calcul;
            """;

        /// <summary>
        /// Situation des dotations agrégée par ligne et exercice, puis ventilée entre activités
        /// selon les clés de répartition (une ligne de résultat par clé).
        /// </summary>
        private const string ConsommationActivite = """
            CREATE OR ALTER VIEW [prog].[vw_ConsommationActivite] AS
            WITH Ligne AS (
                SELECT s.ExerciceId, s.LigneBudgetaireId,
                       SUM(s.PrevisionAjustee)     AS PrevisionAjustee,
                       SUM(s.DotationTotale)       AS DotationTotale,
                       SUM(s.AETotal)              AS AETotal,
                       SUM(s.PaiementsTotal)       AS PaiementsTotal,
                       SUM(s.ResteAPayerYCReports) AS ResteAPayerYCReports
                FROM [bud].[vw_SituationDotation] s
                GROUP BY s.ExerciceId, s.LigneBudgetaireId
            )
            SELECT
                c.OrganismeId, c.ExerciceId,
                ac.ProgrammeId, act.ActionId, c.ActiviteId, c.LigneBudgetaireId,
                l.NatureDepenseId, l.SourceFinancementId,
                c.Pourcentage,
                CAST(ISNULL(g.PrevisionAjustee, 0)     * c.Pourcentage / 100 AS decimal(18,2)) AS PrevisionAjustee,
                CAST(ISNULL(g.DotationTotale, 0)       * c.Pourcentage / 100 AS decimal(18,2)) AS DotationTotale,
                CAST(ISNULL(g.AETotal, 0)              * c.Pourcentage / 100 AS decimal(18,2)) AS AETotal,
                CAST(ISNULL(g.PaiementsTotal, 0)       * c.Pourcentage / 100 AS decimal(18,2)) AS PaiementsTotal,
                CAST(ISNULL(g.ResteAPayerYCReports, 0) * c.Pourcentage / 100 AS decimal(18,2)) AS ResteAPayerYCReports
            FROM [prog].[CleRepartition] c
            JOIN [prog].[Activite] act      ON act.Id = c.ActiviteId
            JOIN [prog].[Action] ac         ON ac.Id = act.ActionId
            JOIN [ref].[LigneBudgetaire] l  ON l.Id = c.LigneBudgetaireId
            LEFT JOIN Ligne g               ON g.ExerciceId = c.ExerciceId AND g.LigneBudgetaireId = c.LigneBudgetaireId
            WHERE c.EstSupprime = 0;
            """;

        /// <summary>
        /// Réalisation d'un indicateur rapprochée de sa cible. La cible retenue est celle du
        /// cycle le plus récent qui couvre l'année de la période. Pour un indicateur à la
        /// baisse (ex. délai), le taux d'atteinte est cible / réalisé.
        /// ProgrammeId : programme de l'indicateur, ou celui de son action.
        /// </summary>
        private const string AtteinteIndicateur = """
            CREATE OR ALTER VIEW [prog].[vw_AtteinteIndicateur] AS
            SELECT
                r.Id AS IndicateurRealisationId, r.OrganismeId, r.IndicateurId,
                i.Niveau,
                COALESCE(i.ProgrammeId, a.ProgrammeId) AS ProgrammeId,
                i.ActionId,
                r.PeriodeId, p.ExerciceId, e.Annee,
                i.SensEvolution, r.Statut,
                r.ValeurRealisee,
                c.ValeurCible,
                CAST(r.ValeurRealisee - c.ValeurCible AS decimal(19,4)) AS EcartCible,
                CAST(CASE WHEN i.SensEvolution = 1
                          THEN 100.0 * r.ValeurRealisee / NULLIF(c.ValeurCible, 0)
                          ELSE 100.0 * c.ValeurCible / NULLIF(r.ValeurRealisee, 0)
                     END AS decimal(19,4)) AS TauxAtteinte,
                CAST(CASE WHEN c.ValeurCible IS NULL THEN NULL
                          WHEN i.SensEvolution = 1 AND r.ValeurRealisee >= c.ValeurCible THEN 1
                          WHEN i.SensEvolution = 2 AND r.ValeurRealisee <= c.ValeurCible THEN 1
                          ELSE 0
                     END AS bit) AS EstAtteinte
            FROM [prog].[IndicateurRealisation] r
            JOIN [prog].[Indicateur] i ON i.Id = r.IndicateurId
            LEFT JOIN [prog].[Action] a ON a.Id = i.ActionId
            JOIN [cfg].[Periode] p     ON p.Id = r.PeriodeId
            JOIN [cfg].[Exercice] e    ON e.Id = p.ExerciceId
            OUTER APPLY (
                SELECT TOP (1) ic.ValeurCible
                FROM [prog].[IndicateurCible] ic
                JOIN [cfg].[CycleProgrammation] cy ON cy.Id = ic.CycleProgrammationId
                WHERE ic.IndicateurId = r.IndicateurId AND ic.Annee = e.Annee
                  AND ic.EstSupprime = 0 AND cy.EstSupprime = 0
                ORDER BY cy.AnneeBase DESC
            ) c
            WHERE r.EstSupprime = 0 AND i.EstSupprime = 0;
            """;

        /// <summary>
        /// Situation de chaque prévision de recette : prévision retenue (révisée sinon
        /// initiale), encaissé, part positionnée sur un compte bancaire, écart et taux de
        /// recouvrement.
        /// </summary>
        private const string SituationRecette = """
            CREATE OR ALTER VIEW [rec].[vw_SituationRecette] AS
            WITH Base AS (
                SELECT
                    pr.Id AS PrevisionRecetteId, pr.OrganismeId, pr.ExerciceId,
                    pr.LigneBudgetaireId, pr.NatureRecetteId, pr.SiteId,
                    ISNULL(pr.MontantRevise, pr.MontantInitial) AS PrevisionRetenue,
                    ISNULL(en.Encaisse, 0)           AS Encaisse,
                    ISNULL(en.EncaissePositionne, 0) AS EncaissePositionne
                FROM [rec].[PrevisionRecette] pr
                OUTER APPLY (
                    SELECT SUM(x.Montant) AS Encaisse,
                           SUM(CASE WHEN x.EstPositionne = 1 THEN x.Montant END) AS EncaissePositionne
                    FROM [rec].[Encaissement] x
                    WHERE x.PrevisionRecetteId = pr.Id AND x.EstSupprime = 0 AND x.Statut = 3
                ) en
                WHERE pr.EstSupprime = 0
            )
            SELECT
                PrevisionRecetteId, OrganismeId, ExerciceId,
                LigneBudgetaireId, NatureRecetteId, SiteId,
                CAST(PrevisionRetenue   AS decimal(18,2)) AS PrevisionRetenue,
                CAST(Encaisse           AS decimal(18,2)) AS Encaisse,
                CAST(EncaissePositionne AS decimal(18,2)) AS EncaissePositionne,
                CAST(PrevisionRetenue - Encaisse AS decimal(18,2)) AS Ecart,
                CAST(100.0 * Encaisse / NULLIF(PrevisionRetenue, 0) AS decimal(19,4)) AS TauxRecouvrement
            FROM Base;
            """;
    }
}
