using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using B = eProcessBudget.Core.Enums.LibellesBudgetProgramme;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>
/// Référentiels du module Budget-programme et performance (cahier fonctionnel § 3.3.2 et
/// 3.3.3) : programmes, actions, activités, indicateurs. Même comportement que les
/// référentiels du Paramétrage, mais sous les droits du budget-programme : la Direction et
/// l'Administrateur les définissent (BudgetProgrammeDefinir), tous les acteurs du module
/// les consultent.
/// </summary>
public static partial class CatalogueReferentiels
{
    /// <summary>Droits de consultation du budget-programme : quiconque y lit ou y saisit.</summary>
    public static readonly TypeDroit[] LectureBudgetProgramme =
    [
        TypeDroit.BudgetProgrammeLire, TypeDroit.BudgetProgrammeDefinir,
        TypeDroit.ProgrammationSaisir, TypeDroit.IndicateurSaisirRealisation
    ];

    private static readonly TypeDroit[] DefinitionBudgetProgramme = [TypeDroit.BudgetProgrammeDefinir];
    private const string ModuleBudgetProgramme = "BudgetProgramme";

    private static void AjouterBudgetProgramme()
    {
        Ajouter(GroupeProgrammes, new DescripteurReferentiel<Programme>
        {
            Cle = "programmes", Titre = "Programmes", Element = "le programme",
            DroitsLecture = LectureBudgetProgramme, DroitsGestion = DefinitionBudgetProgramme, ModuleAudit = ModuleBudgetProgramme,
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x =>
            {
                x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.ObjectifGeneral = Texte(x.ObjectifGeneral);
                x.Presentation = Facultatif(x.Presentation);
            },
            Valider = x => x.ObjectifGeneral.Length == 0 ? "L'objectif général (stratégique) est obligatoire."
                         : x.Presentation?.Length > 20_000 ? "La présentation ne doit pas dépasser 20 000 caractères."
                         : Longueurs((x.Code, 10, "code"), (x.Libelle, 250, "libellé"), (x.ObjectifGeneral, 500, "objectif général")),
            ValiderAsync = (ctx, x) => ResponsableAsync(ctx, x.Id, x.ResponsableId),
            Copier = (s, c) =>
            {
                c.Code = s.Code; c.Libelle = s.Libelle; c.ObjectifGeneral = s.ObjectifGeneral; c.Presentation = s.Presentation;
                c.ResponsableId = s.ResponsableId; c.Ordre = s.Ordre;
            },
            Trier = q => q.OrderBy(x => x.Ordre).ThenBy(x => x.Code),
            LiaisonsPossedees = [typeof(ProgrammeStructure)],
            Colonnes =
            [
                Colonnes<Programme>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 10, true),
                Colonnes<Programme>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<Programme>.Texte("Objectif général", x => x.ObjectifGeneral, (x, v) => x.ObjectifGeneral = v ?? "", 500, true),
                Colonnes<Programme>.Utilisateur("Responsable", x => x.ResponsableId, (x, v) => x.ResponsableId = v ?? Ulid.Empty, obligatoire: true),
                Colonnes<Programme>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Colonnes<Programme>.Texte("Présentation", x => x.Presentation, (x, v) => x.Presentation = v, 20_000),
                Actif<Programme>(),
            ]
        });

        Ajouter(GroupeProgrammes, new DescripteurReferentiel<ActionProgramme>
        {
            Cle = "actions", Titre = "Actions", Element = "l'action",
            DroitsLecture = LectureBudgetProgramme, DroitsGestion = DefinitionBudgetProgramme, ModuleAudit = ModuleBudgetProgramme,
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.ObjectifSpecifique = Texte(x.ObjectifSpecifique); },
            Valider = x => x.ObjectifSpecifique.Length == 0 ? "L'objectif spécifique (opérationnel) est obligatoire."
                         : Longueurs((x.Code, 10, "code"), (x.Libelle, 250, "libellé"), (x.ObjectifSpecifique, 500, "objectif spécifique")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<Programme>(ctx, x.Id, x.ProgrammeId, "Le programme", obligatoire: true)
                ?? await ReferenceAsync<Structure>(ctx, x.Id, x.StructureId, "La structure responsable"),
            Copier = (s, c) =>
            {
                c.ProgrammeId = s.ProgrammeId; c.Code = s.Code; c.Libelle = s.Libelle; c.ObjectifSpecifique = s.ObjectifSpecifique;
                c.StructureId = s.StructureId; c.Ordre = s.Ordre;
            },
            Inclure = q => q.Include(x => x.Programme).Include(x => x.Structure),
            Trier = q => q.OrderBy(x => x.Programme.Ordre).ThenBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<ActionProgramme>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 10, true),
                Colonnes<ActionProgramme>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<ActionProgramme>.Reference<Programme>("Programme", x => x.Programme?.Code, (x, v) => x.ProgrammeId = v ?? Ulid.Empty, p => p.Code, obligatoire: true),
                Colonnes<ActionProgramme>.Texte("Objectif spécifique", x => x.ObjectifSpecifique, (x, v) => x.ObjectifSpecifique = v ?? "", 500, true),
                Colonnes<ActionProgramme>.Reference<Structure>("Structure responsable", x => x.Structure?.Code, (x, v) => x.StructureId = v, s => s.Code),
                Colonnes<ActionProgramme>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<ActionProgramme>(),
            ]
        });

        Ajouter(GroupeProgrammes, new DescripteurReferentiel<Activite>
        {
            Cle = "activites", Titre = "Activités", Element = "l'activité",
            DroitsLecture = LectureBudgetProgramme, DroitsGestion = DefinitionBudgetProgramme, ModuleAudit = ModuleBudgetProgramme,
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.Description = Facultatif(x.Description); },
            Valider = x => Longueurs((x.Code, 15, "code"), (x.Libelle, 250, "libellé"), (x.Description, 1000, "description")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<ActionProgramme>(ctx, x.Id, x.ActionId, "L'action", obligatoire: true)
                ?? await ReferenceAsync<Structure>(ctx, x.Id, x.StructureId, "La structure responsable")
                ?? await ReferenceAsync<NatureDepense>(ctx, x.Id, x.NatureDepensePrincipaleId, "La nature de dépense principale"),
            Copier = (s, c) =>
            {
                c.ActionId = s.ActionId; c.Code = s.Code; c.Libelle = s.Libelle; c.Description = s.Description;
                c.StructureId = s.StructureId; c.NatureDepensePrincipaleId = s.NatureDepensePrincipaleId;
            },
            Inclure = q => q.Include(x => x.Action).Include(x => x.Structure).Include(x => x.NatureDepensePrincipale),
            Trier = q => q.OrderBy(x => x.Action.Code).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<Activite>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 15, true),
                Colonnes<Activite>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<Activite>.Reference<ActionProgramme>("Action", x => x.Action?.Code, (x, v) => x.ActionId = v ?? Ulid.Empty, a => a.Code, obligatoire: true),
                Colonnes<Activite>.Reference<Structure>("Structure responsable", x => x.Structure?.Code, (x, v) => x.StructureId = v, s => s.Code),
                Colonnes<Activite>.Reference<NatureDepense>("Nature de dépense principale", x => x.NatureDepensePrincipale?.Code, (x, v) => x.NatureDepensePrincipaleId = v, n => n.Code),
                Colonnes<Activite>.Texte("Description", x => x.Description, (x, v) => x.Description = v, 1000),
                Actif<Activite>(),
            ]
        });

        Ajouter(GroupeProgrammes, new DescripteurReferentiel<Indicateur>
        {
            Cle = "indicateurs", Titre = "Indicateurs de performance", Element = "l'indicateur",
            DroitsLecture = LectureBudgetProgramme, DroitsGestion = DefinitionBudgetProgramme, ModuleAudit = ModuleBudgetProgramme,
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x =>
            {
                x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.ModeCalcul = Facultatif(x.ModeCalcul);
                // Rattachement exclusif (CHECK en base) : le programme pour le niveau Programme, l'action sinon.
                if (x.Niveau == NiveauIndicateur.Programme) x.ActionId = null; else x.ProgrammeId = null;
            },
            Valider = x => Enumere(x.Niveau, "niveau") ?? Enumere(x.SensEvolution, "sens d'évolution")
                ?? (x.Niveau == NiveauIndicateur.Programme && x.ProgrammeId is null ? "Choisissez le programme de l'indicateur."
                  : x.Niveau != NiveauIndicateur.Programme && x.ActionId is null ? "Choisissez l'action de l'indicateur."
                  : (x.AnneeReference is null) != (x.ValeurReference is null) ? "L'année et la valeur de référence vont ensemble."
                  : x.AnneeReference is < 1990 or > 2100 ? "L'année de référence est comprise entre 1990 et 2100."
                  : Longueurs((x.Code, 20, "code"), (x.Libelle, 300, "libellé"), (x.ModeCalcul, 1000, "mode de calcul"))),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<Programme>(ctx, x.Id, x.ProgrammeId, "Le programme")
                ?? await ReferenceAsync<ActionProgramme>(ctx, x.Id, x.ActionId, "L'action")
                ?? await ReferenceAsync<UniteMesure>(ctx, x.Id, x.UniteMesureId, "L'unité de mesure", obligatoire: true)
                ?? await ValeurDeListeAsync(ctx, x.Id, x.SourceVerificationId, CategoriesListeValeur.SourceVerification, "La source de vérification")
                ?? await ValeurDeListeAsync(ctx, x.Id, x.PeriodiciteId, CategoriesListeValeur.Periodicite, "La périodicité")
                ?? await ReferenceAsync<Structure>(ctx, x.Id, x.StructureId, "La structure responsable"),
            // CodeFormule (calcul automatique futur, C.2.1) n'est pas saisi : il n'est jamais écrasé.
            Copier = (s, c) =>
            {
                c.Niveau = s.Niveau; c.ProgrammeId = s.ProgrammeId; c.ActionId = s.ActionId; c.Code = s.Code; c.Libelle = s.Libelle;
                c.UniteMesureId = s.UniteMesureId; c.ModeCalcul = s.ModeCalcul; c.SensEvolution = s.SensEvolution;
                c.SourceVerificationId = s.SourceVerificationId; c.PeriodiciteId = s.PeriodiciteId; c.StructureId = s.StructureId;
                c.AnneeReference = s.AnneeReference; c.ValeurReference = s.ValeurReference;
            },
            Inclure = q => q.Include(x => x.Programme).Include(x => x.Action).Include(x => x.UniteMesure)
                            .Include(x => x.SourceVerification).Include(x => x.Periodicite).Include(x => x.Structure),
            Colonnes =
            [
                Colonnes<Indicateur>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Indicateur>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 300, true),
                Colonnes<Indicateur>.Enumeration("Niveau", x => x.Niveau, (x, v) => x.Niveau = v, B.Niveau),
                Colonnes<Indicateur>.Reference<Programme>("Programme", x => x.Programme?.Code, (x, v) => x.ProgrammeId = v, p => p.Code),
                Colonnes<Indicateur>.Reference<ActionProgramme>("Action", x => x.Action?.Code, (x, v) => x.ActionId = v, a => a.Code),
                Colonnes<Indicateur>.Reference<UniteMesure>("Unité", x => x.UniteMesure?.Code, (x, v) => x.UniteMesureId = v ?? Ulid.Empty, u => u.Code, obligatoire: true),
                Colonnes<Indicateur>.Enumeration("Sens d'évolution", x => x.SensEvolution, (x, v) => x.SensEvolution = v, B.Sens),
                Colonnes<Indicateur>.Entier("Année de référence", x => x.AnneeReference, (x, v) => x.AnneeReference = v, min: 1990, max: 2100),
                Colonnes<Indicateur>.Decimal("Valeur de référence", x => x.ValeurReference, (x, v) => x.ValeurReference = v, 4),
                ColonneListe<Indicateur>("Source de vérification", x => x.SourceVerification?.Code, (x, v) => x.SourceVerificationId = v, CategoriesListeValeur.SourceVerification),
                ColonneListe<Indicateur>("Périodicité", x => x.Periodicite?.Code, (x, v) => x.PeriodiciteId = v, CategoriesListeValeur.Periodicite),
                Colonnes<Indicateur>.Reference<Structure>("Structure responsable", x => x.Structure?.Code, (x, v) => x.StructureId = v, s => s.Code),
                Colonnes<Indicateur>.Texte("Mode de calcul", x => x.ModeCalcul, (x, v) => x.ModeCalcul = v, 1000),
                Actif<Indicateur>(),
            ]
        });
    }

    /// <summary>Le responsable du programme : un compte actif de l'organisme courant (une seule personne, C.1).</summary>
    private static async Task<string?> ResponsableAsync(AppDbContext ctx, Ulid elementId, Ulid responsableId)
    {
        if (responsableId == Ulid.Empty) return "Le responsable du programme est obligatoire.";
        var organismeId = ctx.OrganismeCourantId;
        var compte = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.Id == responsableId && u.OrganismeId == organismeId)
            .Select(u => new { u.EstActif })
            .FirstOrDefaultAsync();
        if (compte is null) return "Le responsable doit être un compte de l'organisme.";
        return elementId == Ulid.Empty && !compte.EstActif ? "Le responsable choisi est un compte désactivé." : null;
    }
}
