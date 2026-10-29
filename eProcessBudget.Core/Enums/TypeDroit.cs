namespace eProcessBudget.Core.Enums;

/// <summary>
/// Catalogue des droits de l'application. C'est LE point d'extension principal
/// quand vous ajoutez une fonctionnalité.
///
/// CONVENTIONS
/// -----------
/// - Un droit = une action précise (Consultation, Creation, Modification, Suppression,
///   Validation, Exportation). Ne créez pas un droit par écran, mais par action.
/// - Les valeurs numériques sont PERSISTÉES en base : ne les renumérotez jamais.
///   Pour retirer un droit, marquez-le [Obsolete] et laissez son numéro libre.
/// - Réservez des plages de 10 par domaine fonctionnel, ça laisse de la place.
///
/// PLAGES
/// ------
///   0 -  99 : socle (administration, sécurité, audit) - à conserver tel quel
/// 100 +     : VOS droits métier
///
/// Après avoir ajouté un droit : mettez à jour DroitHelper.Libelle() puis, si le droit
/// doit être accordé d'office à un profil livré, SeedService.
/// </summary>
public enum TypeDroit
{
    // ── Socle : administration & sécurité (0-99) ─────────────────────────────
    /// <summary>Accès au bloc Administration du menu (porte d'entrée).</summary>
    AdministrationAcces = 0,

    /// <summary>Voir la liste des utilisateurs de l'application.</summary>
    UtilisateurConsultation = 10,
    /// <summary>Créer, modifier, activer/désactiver un utilisateur.</summary>
    UtilisateurGestion = 11,

    /// <summary>Voir les profils et les droits qu'ils portent.</summary>
    ProfilConsultation = 20,
    /// <summary>Créer, modifier, supprimer un profil et son contenu.</summary>
    ProfilGestion = 21,
    /// <summary>Attribuer ou retirer un profil à un utilisateur.</summary>
    ProfilAttribution = 22,

    /// <summary>Consulter le journal d'audit.</summary>
    AuditConsultation = 30,
    /// <summary>Exporter le journal d'audit.</summary>
    AuditExportation = 31,

    /// <summary>Régler les canaux (interne / email) de chaque type de notification.</summary>
    NotificationParametrage = 40,

    // ── Droits métier (100+) - cahier technique, chapitre 4 ─────────────────
    // UtilisateurGerer et AuditLire du cahier correspondent à UtilisateurGestion et
    // AuditConsultation du socle.

    // Plateforme et habilitations (100-109)
    /// <summary>Demander une habilitation (droit sur un périmètre) pour un utilisateur.</summary>
    HabilitationDemander = 100,
    /// <summary>Valider une habilitation au titre du service financier et comptable.</summary>
    HabilitationValiderFinancier = 101,
    /// <summary>Valider une habilitation au titre de l'administration du SI.</summary>
    HabilitationValiderAdminSI = 102,
    /// <summary>Créer, activer, suspendre les organismes (Administrateur plateforme).</summary>
    OrganismeGerer = 103,

    // Paramétrage (110-119)
    ParametrageLire = 110,
    ParametrageGerer = 111,
    /// <summary>Lancer la clôture d'un exercice (reliquats, encours, reports).</summary>
    ExerciceCloturer = 112,

    // Budget-programme et performance (120-129)
    BudgetProgrammeLire = 120,
    /// <summary>Programmes, actions, activités, objectifs, indicateurs, cadre logique.</summary>
    BudgetProgrammeDefinir = 121,
    IndicateurSaisirRealisation = 122,
    /// <summary>AE/CP, ressources, emplois, clés de répartition, temps des agents.</summary>
    ProgrammationSaisir = 123,

    // Cycle budgétaire commun (130-139)
    ExecutionLire = 130,
    DotationSaisir = 131,
    AjustementSaisir = 132,
    AjustementValider = 133,
    EngagementSaisir = 134,
    EngagementValider = 135,
    DepassementValider = 136,
    PaiementSaisir = 137,

    // Axes de dépenses (140-149)
    EntretienLire = 140,
    EntretienSaisir = 141,
    InvestissementLire = 142,
    InvestissementSaisir = 143,
    FonctionnementLire = 144,
    FonctionnementSaisir = 145,
    AppuisLire = 146,
    AppuisSaisir = 147,

    // Recettes (150-159)
    RecettesLire = 150,
    PrevisionRecetteSaisir = 151,
    EncaissementSaisir = 152,

    // Pilotage (160-169)
    TableauDeBordLire = 160,
    ConsommationLire = 161,

    // Rapports (170-179)
    RapportExporter = 170,
}


