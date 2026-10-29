namespace eProcessBudget.Core.Enums;

/// <summary>
/// Libellés lisibles des droits, utilisés par les écrans d'administration.
/// Ajoutez ici une entrée pour chaque droit ajouté dans <see cref="TypeDroit"/>.
/// </summary>
public static class DroitHelper
{
    public static string Libelle(TypeDroit droit) => droit switch
    {
        TypeDroit.AdministrationAcces     => "Accès à l'administration",

        TypeDroit.UtilisateurConsultation => "Utilisateurs : consulter",
        TypeDroit.UtilisateurGestion      => "Utilisateurs : gérer",

        TypeDroit.ProfilConsultation      => "Profils : consulter",
        TypeDroit.ProfilGestion           => "Profils : gérer",
        TypeDroit.ProfilAttribution       => "Profils : attribuer",

        TypeDroit.AuditConsultation       => "Audit : consulter",
        TypeDroit.AuditExportation        => "Audit : exporter",

        TypeDroit.NotificationParametrage => "Notifications : paramétrer",

        TypeDroit.HabilitationDemander         => "Habilitations : demander",
        TypeDroit.HabilitationValiderFinancier => "Habilitations : valider (service financier)",
        TypeDroit.HabilitationValiderAdminSI   => "Habilitations : valider (administrateur SI)",
        TypeDroit.OrganismeGerer               => "Organismes : gérer (plateforme)",

        TypeDroit.ParametrageLire  => "Paramétrage : consulter",
        TypeDroit.ParametrageGerer => "Paramétrage : gérer",
        TypeDroit.ExerciceCloturer => "Exercices : clôturer",

        TypeDroit.BudgetProgrammeLire         => "Budget-programme : consulter",
        TypeDroit.BudgetProgrammeDefinir      => "Budget-programme : définir",
        TypeDroit.IndicateurSaisirRealisation => "Indicateurs : saisir les réalisations",
        TypeDroit.ProgrammationSaisir         => "Programmation AE/CP, emplois : saisir",

        TypeDroit.ExecutionLire      => "Exécution : consulter",
        TypeDroit.DotationSaisir     => "Dotations : saisir",
        TypeDroit.AjustementSaisir   => "Ajustements : saisir",
        TypeDroit.AjustementValider  => "Ajustements : valider",
        TypeDroit.EngagementSaisir   => "Engagements : saisir",
        TypeDroit.EngagementValider  => "Engagements : valider",
        TypeDroit.DepassementValider => "Dépassements : valider",
        TypeDroit.PaiementSaisir     => "Paiements : saisir",

        TypeDroit.EntretienLire        => "Entretien routier : consulter",
        TypeDroit.EntretienSaisir      => "Entretien routier : saisir",
        TypeDroit.InvestissementLire   => "Investissements : consulter",
        TypeDroit.InvestissementSaisir => "Investissements : saisir",
        TypeDroit.FonctionnementLire   => "Fonctionnement : consulter",
        TypeDroit.FonctionnementSaisir => "Fonctionnement : saisir",
        TypeDroit.AppuisLire           => "Appuis : consulter",
        TypeDroit.AppuisSaisir         => "Appuis : saisir",

        TypeDroit.RecettesLire           => "Recettes : consulter",
        TypeDroit.PrevisionRecetteSaisir => "Prévisions de recettes : saisir",
        TypeDroit.EncaissementSaisir     => "Encaissements : saisir",

        TypeDroit.TableauDeBordLire => "Tableau de bord : consulter",
        TypeDroit.ConsommationLire  => "Consommation budgétaire : consulter",

        TypeDroit.RapportExporter => "Rapports : exporter",

        _ => droit.ToString()
    };

    /// <summary>
    /// Regroupement d'affichage. Utilisé pour trier les cases à cocher dans l'écran
    /// de configuration d'un profil.
    /// </summary>
    public static string Groupe(TypeDroit droit) => (int)droit switch
    {
        < 100 => "Administration",
        < 110 => "Habilitations et plateforme",
        < 120 => "Paramétrage",
        < 130 => "Budget-programme",
        < 140 => "Exécution",
        < 150 => "Axes de dépenses",
        < 160 => "Recettes",
        < 170 => "Pilotage",
        _     => "Rapports"
    };

    /// <summary>Tous les droits, triés par groupe puis par libellé.</summary>
    public static IEnumerable<TypeDroit> Tous()
        => Enum.GetValues<TypeDroit>()
               .Distinct()
               .OrderBy(d => Groupe(d))
               .ThenBy(Libelle);

    // ── Cloisonnement plateforme / organisme (cahier fonctionnel § 2.2 et § 3.1.3) ──

    /// <summary>
    /// Droits réservés à un Administrateur plateforme : ils agissent sur des données
    /// partagées par tous les organismes (les organismes eux-mêmes, les profils, qui sont
    /// globaux). Accordés à un compte d'organisme, ils sont ignorés.
    /// </summary>
    public static readonly IReadOnlySet<TypeDroit> ReservesPlateforme = new HashSet<TypeDroit>
    {
        TypeDroit.OrganismeGerer,
        TypeDroit.ProfilGestion,
        // Les canaux de notification (ntf.ParametreNotification) valent pour toute l'instance.
        TypeDroit.NotificationParametrage,
    };

    /// <summary>
    /// Seuls droits effectifs d'un Administrateur plateforme : administration technique de
    /// l'instance, sans aucun accès aux données budgétaires des organismes.
    /// </summary>
    public static readonly IReadOnlySet<TypeDroit> AdmisPlateforme = new HashSet<TypeDroit>
    {
        TypeDroit.AdministrationAcces,
        TypeDroit.UtilisateurConsultation,
        TypeDroit.UtilisateurGestion,
        TypeDroit.ProfilConsultation,
        TypeDroit.ProfilGestion,
        TypeDroit.ProfilAttribution,
        TypeDroit.AuditConsultation,
        TypeDroit.AuditExportation,
        TypeDroit.NotificationParametrage,
        TypeDroit.OrganismeGerer,
    };

    /// <summary>
    /// Restreint des droits calculés au niveau du compte : un compte plateforme ne garde que
    /// <see cref="AdmisPlateforme"/>, un compte d'organisme perd <see cref="ReservesPlateforme"/>.
    /// </summary>
    public static IEnumerable<TypeDroit> FiltrerSelonNiveau(IEnumerable<TypeDroit> droits, bool estAdministrateurPlateforme)
        => estAdministrateurPlateforme
            ? droits.Where(AdmisPlateforme.Contains)
            : droits.Where(d => !ReservesPlateforme.Contains(d));
}


