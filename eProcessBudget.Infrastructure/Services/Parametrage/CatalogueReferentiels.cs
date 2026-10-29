using System.Linq.Expressions;
using System.Net.Mail;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using L = eProcessBudget.Core.Enums.LibellesParametrage;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>Entrée du catalogue, pour les écrans qui listent tous les référentiels.</summary>
/// <param name="Chemin">Route de la page du référentiel.</param>
public sealed record ReferentielCatalogue(string Cle, string Titre, Type Type, string Groupe, string Chemin);

/// <summary>
/// Les référentiels du Paramétrage (cahier fonctionnel § 3.2.3 à 3.2.8, technique § 3.3.3
/// et 3.3.4) et ce qui les distingue. Pour ajouter un référentiel : un descripteur ici,
/// une page dans Web/Features/Parametrage.
/// </summary>
public static partial class CatalogueReferentiels
{
    private static readonly Dictionary<Type, object> Descripteurs = new();
    private static readonly List<ReferentielCatalogue> Entrees = [];

    public static IReadOnlyList<ReferentielCatalogue> Tous => Entrees;

    public static DescripteurReferentiel<T> Obtenir<T>() where T : EntiteOrganisme
        => Descripteurs.TryGetValue(typeof(T), out var d)
            ? (DescripteurReferentiel<T>)d
            : throw new InvalidOperationException($"Aucun descripteur de référentiel pour {typeof(T).Name}.");

    private static void Ajouter<T>(string groupe, DescripteurReferentiel<T> d) where T : EntiteOrganisme
    {
        Descripteurs[typeof(T)] = d;
        var chemin = groupe == GroupeProgrammes ? $"/budget-programme/{d.Cle}" : $"/parametrage/{d.Cle}";
        Entrees.Add(new ReferentielCatalogue(d.Cle, d.Titre, typeof(T), groupe, chemin));
    }

    private const string GroupeBudgetProgramme = "Budget-programme";
    private const string GroupeBudgetaire = "Budgétaire et comptable";
    private const string GroupeOrganisation = "Organisation";
    private const string GroupeTiers = "Tiers et partenaires";
    private const string GroupeTechnique = "Entretien routier";
    private const string GroupeRecettes = "Recettes";
    private const string GroupeGeneral = "Général";
    public const string GroupeProgrammes = "Programmes et performance";

    static CatalogueReferentiels()
    {
        AjouterParametrage();
        AjouterBudgetProgramme();
    }

    private static void AjouterParametrage()
    {
        // ═════ Général ════════════════════════════════════════════════════════
        Ajouter(GroupeGeneral, new DescripteurReferentiel<ListeValeur>
        {
            Cle = "listes-valeurs", Titre = "Listes de valeurs", Element = "la valeur",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Doublon = s => x => x.Categorie == s.Categorie && x.Code == s.Code && x.Id != s.Id,
            CleImport = x => x.Categorie + "|" + x.Code,
            Normaliser = x => { x.Categorie = Cle(x.Categorie); x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => !CategoriesListeValeur.EstConnue(x.Categorie) ? $"Catégorie inconnue : « {x.Categorie} »."
                         : Longueurs((x.Code, 30, "code"), (x.Libelle, 200, "libellé")),
            Copier = (s, c) => { c.Categorie = s.Categorie; c.Code = s.Code; c.Libelle = s.Libelle; c.Ordre = s.Ordre; },
            Trier = q => q.OrderBy(x => x.Categorie).ThenBy(x => x.Ordre).ThenBy(x => x.Libelle),
            Colonnes =
            [
                Colonnes<ListeValeur>.Texte("Catégorie", x => x.Categorie, (x, v) => x.Categorie = v ?? "", 40, true,
                    string.Join(", ", CategoriesListeValeur.Toutes.Select(c => c.Code))),
                Colonnes<ListeValeur>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 30, true),
                Colonnes<ListeValeur>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 200, true),
                Colonnes<ListeValeur>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<ListeValeur>(),
            ]
        });

        Ajouter(GroupeGeneral, new DescripteurReferentiel<Devise>
        {
            Cle = "devises", Titre = "Devises", Element = "la devise",
            Code = x => x.CodeIso, Libelle = x => x.Libelle,
            Normaliser = x => { x.CodeIso = Cle(x.CodeIso); x.Libelle = Texte(x.Libelle); x.Symbole = Facultatif(x.Symbole); },
            Valider = x => x.CodeIso.Length != 3 || !x.CodeIso.All(char.IsAsciiLetterUpper)
                ? "Le code ISO 4217 compte trois lettres (XOF, EUR, USD...)."
                : Longueurs((x.Libelle, 50, "libellé"), (x.Symbole, 10, "symbole")),
            // EstReference se règle dans les paramètres de l'organisme, pas ici.
            Copier = (s, c) => { c.CodeIso = s.CodeIso; c.Libelle = s.Libelle; c.Symbole = s.Symbole; },
            Colonnes =
            [
                Colonnes<Devise>.Texte("Code ISO", x => x.CodeIso, (x, v) => x.CodeIso = v ?? "", 3, true),
                Colonnes<Devise>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 50, true),
                Colonnes<Devise>.Texte("Symbole", x => x.Symbole, (x, v) => x.Symbole = v, 10),
            ]
        });

        // ═════ Budget-programme ═══════════════════════════════════════════════
        Ajouter(GroupeBudgetProgramme, new DescripteurReferentiel<Axe>
        {
            Cle = "axes", Titre = "Axes budgétaires", Element = "l'axe",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Enumere(x.TypeAxe, "type") ?? Enumere(x.Section, "section")
                        ?? Longueurs((x.Code, 20, "code"), (x.Libelle, 100, "libellé")),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.TypeAxe = s.TypeAxe; c.Section = s.Section; c.Ordre = s.Ordre; },
            Trier = q => q.OrderBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<Axe>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Axe>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 100, true),
                Colonnes<Axe>.Enumeration("Type", x => x.TypeAxe, (x, v) => x.TypeAxe = v, L.TypeAxe),
                Colonnes<Axe>.Enumeration("Section", x => x.Section, (x, v) => x.Section = v, L.Section),
                Colonnes<Axe>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<Axe>(),
            ]
        });

        Ajouter(GroupeBudgetProgramme, new DescripteurReferentiel<UniteMesure>
        {
            Cle = "unites-mesure", Titre = "Unités de mesure", Element = "l'unité",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.Symbole = Facultatif(x.Symbole); },
            Valider = x => Enumere(x.TypeUnite, "type") ?? Longueurs((x.Code, 20, "code"), (x.Libelle, 100, "libellé"), (x.Symbole, 10, "symbole")),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.TypeUnite = s.TypeUnite; c.Symbole = s.Symbole; },
            Colonnes =
            [
                Colonnes<UniteMesure>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<UniteMesure>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 100, true),
                Colonnes<UniteMesure>.Enumeration("Type", x => x.TypeUnite, (x, v) => x.TypeUnite = v, L.TypeUnite),
                Colonnes<UniteMesure>.Texte("Symbole", x => x.Symbole, (x, v) => x.Symbole = v, 10),
                Actif<UniteMesure>(),
            ]
        });

        Ajouter(GroupeBudgetProgramme, new DescripteurReferentiel<Risque>
        {
            Cle = "risques", Titre = "Catalogue des risques", Element = "le risque",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Longueurs((x.Code, 20, "code"), (x.Libelle, 300, "libellé")),
            ValiderAsync = (ctx, x) => ValeurDeListeAsync(ctx, x.Id, x.CategorieRisqueId, CategoriesListeValeur.CategorieRisque, "La catégorie"),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.CategorieRisqueId = s.CategorieRisqueId; },
            Inclure = q => q.Include(x => x.CategorieRisque),
            Colonnes =
            [
                Colonnes<Risque>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Risque>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 300, true),
                ColonneListe<Risque>("Catégorie", x => x.CategorieRisque?.Code, (x, v) => x.CategorieRisqueId = v, CategoriesListeValeur.CategorieRisque),
                Actif<Risque>(),
            ]
        });

        // ═════ Budgétaire et comptable ════════════════════════════════════════
        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<NatureDepense>
        {
            Cle = "natures-depense", Titre = "Natures de dépense", Element = "la nature de dépense",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Enumere(x.GroupeNature, "groupe") ?? Longueurs((x.Code, 10, "code"), (x.Libelle, 150, "libellé")),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.GroupeNature = s.GroupeNature; c.Ordre = s.Ordre; },
            Trier = q => q.OrderBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<NatureDepense>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 10, true),
                Colonnes<NatureDepense>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 150, true),
                Colonnes<NatureDepense>.Enumeration("Groupe", x => x.GroupeNature, (x, v) => x.GroupeNature = v, L.GroupeNature),
                Colonnes<NatureDepense>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<NatureDepense>(),
            ]
        });

        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<SourceFinancement>
        {
            Cle = "sources-financement", Titre = "Sources de financement", Element = "la source de financement",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Longueurs((x.Code, 10, "code"), (x.Libelle, 150, "libellé")),
            ValiderAsync = (ctx, x) => ReferenceAsync<Tiers>(ctx, x.Id, x.PartenaireFinancierId, "Le partenaire financier",
                                                            t => t.TypeTiers == TypeTiers.PartenaireFinancier),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.PartenaireFinancierId = s.PartenaireFinancierId; },
            Inclure = q => q.Include(x => x.PartenaireFinancier),
            Colonnes =
            [
                Colonnes<SourceFinancement>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 10, true),
                Colonnes<SourceFinancement>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 150, true),
                Colonnes<SourceFinancement>.Reference<Tiers>("Partenaire financier", x => x.PartenaireFinancier?.Code,
                    (x, v) => x.PartenaireFinancierId = v, t => t.Code, filtre: t => t.TypeTiers == TypeTiers.PartenaireFinancier, cleFiltre: "partenaire"),
                Actif<SourceFinancement>(),
            ]
        });

        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<CompteComptable>
        {
            Cle = "plan-comptable", Titre = "Plan comptable", Element = "le compte",
            Code = x => x.Numero, Libelle = x => x.Libelle,
            Normaliser = x =>
            {
                x.Numero = (x.Numero ?? "").Replace(" ", "").Trim().ToUpperInvariant();
                x.Libelle = Texte(x.Libelle);
                // Classe déduite du premier chiffre quand elle n'est pas saisie.
                if (x.Classe == 0 && x.Numero.Length > 0 && x.Numero[0] is >= '1' and <= '9') x.Classe = x.Numero[0] - '0';
            },
            Valider = x => x.Classe is < 1 or > 9 ? "La classe du compte est comprise entre 1 et 9."
                         : Enumere(x.TypeCompte, "type") ?? Longueurs((x.Numero, 20, "numéro"), (x.Libelle, 250, "libellé")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<CompteComptable>(ctx, x.Id, x.CompteParentId, "Le compte de regroupement")
                ?? await SansCycleAsync(ctx, x.Id, x.CompteParentId, (CompteComptable c) => c.CompteParentId, "Un compte ne peut pas être son propre regroupement."),
            Copier = (s, c) =>
            {
                c.Numero = s.Numero; c.Libelle = s.Libelle; c.Classe = s.Classe; c.TypeCompte = s.TypeCompte;
                c.CompteParentId = s.CompteParentId; c.EstImmobilisation = s.EstImmobilisation;
            },
            Inclure = q => q.Include(x => x.CompteParent),
            Colonnes =
            [
                Colonnes<CompteComptable>.Texte("Numéro", x => x.Numero, (x, v) => x.Numero = v ?? "", 20, true),
                Colonnes<CompteComptable>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<CompteComptable>.Entier("Classe", x => x.Classe, (x, v) => x.Classe = v ?? 0, min: 1, max: 9),
                Colonnes<CompteComptable>.Enumeration("Type", x => x.TypeCompte, (x, v) => x.TypeCompte = v, L.TypeCompte),
                Colonnes<CompteComptable>.Reference<CompteComptable>("Compte de regroupement", x => x.CompteParent?.Numero,
                    (x, v) => x.CompteParentId = v, c => c.Numero),
                Colonnes<CompteComptable>.OuiNon("Immobilisation", x => x.EstImmobilisation, (x, v) => x.EstImmobilisation = v, false),
                Actif<CompteComptable>(),
            ]
        });

        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<Rubrique>
        {
            Cle = "rubriques", Titre = "Rubriques", Element = "la rubrique",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.Note = Facultatif(x.Note); },
            Valider = x => Longueurs((x.Code, 20, "code"), (x.Libelle, 250, "libellé"), (x.Note, 5, "repère")),
            ValiderAsync = (ctx, x) => ReferenceAsync<Axe>(ctx, x.Id, x.AxeId, "L'axe", obligatoire: true),
            Copier = (s, c) => { c.AxeId = s.AxeId; c.Code = s.Code; c.Note = s.Note; c.Libelle = s.Libelle; c.Ordre = s.Ordre; },
            Inclure = q => q.Include(x => x.Axe),
            Trier = q => q.OrderBy(x => x.Axe.Ordre).ThenBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<Rubrique>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Rubrique>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<Rubrique>.Reference<Axe>("Axe", x => x.Axe?.Code, (x, v) => x.AxeId = v ?? Ulid.Empty, a => a.Code, obligatoire: true),
                Colonnes<Rubrique>.Texte("Repère", x => x.Note, (x, v) => x.Note = v, 5),
                Colonnes<Rubrique>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<Rubrique>(),
            ]
        });

        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<SousRubrique>
        {
            Cle = "sous-rubriques", Titre = "Sous-rubriques", Element = "la sous-rubrique",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Longueurs((x.Code, 20, "code"), (x.Libelle, 250, "libellé")),
            ValiderAsync = (ctx, x) => ReferenceAsync<Rubrique>(ctx, x.Id, x.RubriqueId, "La rubrique", obligatoire: true),
            Copier = (s, c) => { c.RubriqueId = s.RubriqueId; c.Code = s.Code; c.Libelle = s.Libelle; c.Ordre = s.Ordre; },
            Inclure = q => q.Include(x => x.Rubrique),
            Trier = q => q.OrderBy(x => x.Rubrique.Code).ThenBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<SousRubrique>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<SousRubrique>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<SousRubrique>.Reference<Rubrique>("Rubrique", x => x.Rubrique?.Code, (x, v) => x.RubriqueId = v ?? Ulid.Empty, r => r.Code, obligatoire: true),
                Colonnes<SousRubrique>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<SousRubrique>(),
            ]
        });

        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<LigneBudgetaire>
        {
            Cle = "lignes-budgetaires", Titre = "Lignes budgétaires", Element = "la ligne budgétaire",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x =>
            {
                x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle);
                // Une ligne porte une nature de dépense OU de recette, selon son sens (CHECK en base).
                if (x.Sens == SensLigne.Depense) x.NatureRecetteId = null; else x.NatureDepenseId = null;
            },
            Valider = x => Enumere(x.Sens, "sens")
                ?? (x.Sens == SensLigne.Depense && x.NatureDepenseId is null ? "Une ligne de dépense porte une nature de dépense."
                  : x.Sens == SensLigne.Recette && x.NatureRecetteId is null ? "Une ligne de recette porte une nature de recette."
                  : Longueurs((x.Code, 30, "code"), (x.Libelle, 250, "libellé"))),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<SousRubrique>(ctx, x.Id, x.SousRubriqueId, "La sous-rubrique", obligatoire: true)
                ?? await ReferenceAsync<SourceFinancement>(ctx, x.Id, x.SourceFinancementId, "La source de financement", obligatoire: true)
                ?? await ReferenceAsync<NatureDepense>(ctx, x.Id, x.NatureDepenseId, "La nature de dépense")
                ?? await ReferenceAsync<NatureRecette>(ctx, x.Id, x.NatureRecetteId, "La nature de recette")
                ?? await SensCoherentAsync(ctx, x),
            Copier = (s, c) =>
            {
                c.SousRubriqueId = s.SousRubriqueId; c.Code = s.Code; c.Libelle = s.Libelle; c.Sens = s.Sens;
                c.NatureDepenseId = s.NatureDepenseId; c.NatureRecetteId = s.NatureRecetteId; c.SourceFinancementId = s.SourceFinancementId;
            },
            Inclure = q => q.Include(x => x.SousRubrique).Include(x => x.NatureDepense).Include(x => x.NatureRecette).Include(x => x.SourceFinancement),
            LiaisonsPossedees = [typeof(LigneCompte)],
            Colonnes =
            [
                Colonnes<LigneBudgetaire>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 30, true),
                Colonnes<LigneBudgetaire>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 250, true),
                Colonnes<LigneBudgetaire>.Reference<SousRubrique>("Sous-rubrique", x => x.SousRubrique?.Code, (x, v) => x.SousRubriqueId = v ?? Ulid.Empty, r => r.Code, obligatoire: true),
                Colonnes<LigneBudgetaire>.Enumeration("Sens", x => x.Sens, (x, v) => x.Sens = v, L.SensLigne),
                Colonnes<LigneBudgetaire>.Reference<NatureDepense>("Nature de dépense", x => x.NatureDepense?.Code, (x, v) => x.NatureDepenseId = v, n => n.Code),
                Colonnes<LigneBudgetaire>.Reference<NatureRecette>("Nature de recette", x => x.NatureRecette?.Code, (x, v) => x.NatureRecetteId = v, n => n.Code),
                Colonnes<LigneBudgetaire>.Reference<SourceFinancement>("Source de financement", x => x.SourceFinancement?.Code, (x, v) => x.SourceFinancementId = v ?? Ulid.Empty, s => s.Code, obligatoire: true),
                Actif<LigneBudgetaire>(),
            ]
        });

        Ajouter(GroupeBudgetaire, new DescripteurReferentiel<MotifAjustement>
        {
            Cle = "motifs-ajustement", Titre = "Motifs d'ajustement", Element = "le motif",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Enumere(x.Sens, "sens") ?? Longueurs((x.Code, 20, "code"), (x.Libelle, 200, "libellé")),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.Sens = s.Sens; },
            Colonnes =
            [
                Colonnes<MotifAjustement>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<MotifAjustement>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 200, true),
                Colonnes<MotifAjustement>.Enumeration("Sens", x => x.Sens, (x, v) => x.Sens = v, L.SensAjustement),
                Actif<MotifAjustement>(),
            ]
        });

        // ═════ Organisation ═══════════════════════════════════════════════════
        Ajouter(GroupeOrganisation, new DescripteurReferentiel<Structure>
        {
            Cle = "structures", Titre = "Structures et services", Element = "la structure",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); x.Sigle = Facultatif(x.Sigle); x.ResponsableNom = Facultatif(x.ResponsableNom); },
            Valider = x => Longueurs((x.Code, 20, "code"), (x.Libelle, 200, "libellé"), (x.Sigle, 20, "sigle"), (x.ResponsableNom, 150, "responsable")),
            ValiderAsync = async (ctx, x) =>
                await ValeurDeListeAsync(ctx, x.Id, x.TypeStructureId, CategoriesListeValeur.TypeStructure, "Le type")
                ?? await ReferenceAsync<Structure>(ctx, x.Id, x.StructureParentId, "La structure de rattachement")
                ?? await SansCycleAsync(ctx, x.Id, x.StructureParentId, (Structure s) => s.StructureParentId, "Une structure ne peut pas se rattacher à elle-même ni à l'une de ses sous-structures."),
            Copier = (s, c) =>
            {
                c.Code = s.Code; c.Sigle = s.Sigle; c.Libelle = s.Libelle; c.TypeStructureId = s.TypeStructureId;
                c.StructureParentId = s.StructureParentId; c.ResponsableNom = s.ResponsableNom;
            },
            Inclure = q => q.Include(x => x.TypeStructure).Include(x => x.StructureParent),
            Colonnes =
            [
                Colonnes<Structure>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Structure>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 200, true),
                Colonnes<Structure>.Texte("Sigle", x => x.Sigle, (x, v) => x.Sigle = v, 20),
                ColonneListe<Structure>("Type", x => x.TypeStructure?.Code, (x, v) => x.TypeStructureId = v, CategoriesListeValeur.TypeStructure),
                Colonnes<Structure>.Reference<Structure>("Structure de rattachement", x => x.StructureParent?.Code, (x, v) => x.StructureParentId = v, s => s.Code),
                Colonnes<Structure>.Texte("Responsable", x => x.ResponsableNom, (x, v) => x.ResponsableNom = v, 150),
                Actif<Structure>(),
            ]
        });

        Ajouter(GroupeOrganisation, new DescripteurReferentiel<Site>
        {
            Cle = "sites", Titre = "Sites géographiques", Element = "le site",
            Code = x => x.Code, Libelle = x => x.Nom,
            Normaliser = x =>
            {
                x.Code = Cle(x.Code); x.Nom = Texte(x.Nom); x.Region = Facultatif(x.Region); x.Prefecture = Facultatif(x.Prefecture);
                x.Commune = Facultatif(x.Commune); x.AxeRoutier = Facultatif(x.AxeRoutier);
            },
            Valider = x => Enumere(x.TypeSite, "type") ?? Longueurs((x.Code, 20, "code"), (x.Nom, 150, "nom"), (x.Region, 100, "région"),
                (x.Prefecture, 100, "préfecture"), (x.Commune, 100, "commune"), (x.AxeRoutier, 100, "axe routier")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<CompteComptable>(ctx, x.Id, x.CompteTiersId, "Le compte tiers", c => c.TypeCompte == TypeCompte.Auxiliaire),
            Copier = (s, c) =>
            {
                c.Code = s.Code; c.Nom = s.Nom; c.TypeSite = s.TypeSite; c.Region = s.Region; c.Prefecture = s.Prefecture;
                c.Commune = s.Commune; c.AxeRoutier = s.AxeRoutier; c.DateMiseEnService = s.DateMiseEnService; c.CompteTiersId = s.CompteTiersId;
            },
            Inclure = q => q.Include(x => x.CompteTiers),
            LiaisonsPossedees = [typeof(SiteTiers)],
            Colonnes =
            [
                Colonnes<Site>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Site>.Texte("Nom", x => x.Nom, (x, v) => x.Nom = v ?? "", 150, true),
                Colonnes<Site>.Enumeration("Type", x => x.TypeSite, (x, v) => x.TypeSite = v, L.TypeSite),
                Colonnes<Site>.Texte("Région", x => x.Region, (x, v) => x.Region = v, 100),
                Colonnes<Site>.Texte("Préfecture", x => x.Prefecture, (x, v) => x.Prefecture = v, 100),
                Colonnes<Site>.Texte("Commune", x => x.Commune, (x, v) => x.Commune = v, 100),
                Colonnes<Site>.Texte("Axe routier", x => x.AxeRoutier, (x, v) => x.AxeRoutier = v, 100),
                Colonnes<Site>.Date("Mise en service", x => x.DateMiseEnService, (x, v) => x.DateMiseEnService = v),
                Colonnes<Site>.Reference<CompteComptable>("Compte tiers", x => x.CompteTiers?.Numero, (x, v) => x.CompteTiersId = v, c => c.Numero),
                Actif<Site>(),
            ]
        });

        Ajouter(GroupeOrganisation, new DescripteurReferentiel<CategorieEmploi>
        {
            Cle = "categories-emploi", Titre = "Catégories d'emplois", Element = "la catégorie d'emplois",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Longueurs((x.Code, 20, "code"), (x.Libelle, 100, "libellé")),
            Copier = (s, c) => { c.Code = s.Code; c.Libelle = s.Libelle; c.Ordre = s.Ordre; },
            Trier = q => q.OrderBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<CategorieEmploi>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<CategorieEmploi>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 100, true),
                Colonnes<CategorieEmploi>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<CategorieEmploi>(),
            ]
        });

        Ajouter(GroupeOrganisation, new DescripteurReferentiel<Agent>
        {
            Cle = "agents", Titre = "Agents", Element = "l'agent",
            Code = x => x.Matricule, Libelle = x => x.NomComplet,
            Normaliser = x => { x.Matricule = Cle(x.Matricule); x.NomComplet = Texte(x.NomComplet); },
            Valider = x => Longueurs((x.Matricule, 30, "matricule"), (x.NomComplet, 200, "nom")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<CategorieEmploi>(ctx, x.Id, x.CategorieEmploiId, "La catégorie d'emplois", obligatoire: true)
                ?? await ReferenceAsync<Structure>(ctx, x.Id, x.StructureId, "La structure", obligatoire: true)
                ?? await ReferenceAsync<Site>(ctx, x.Id, x.SiteId, "Le site"),
            Copier = (s, c) => { c.Matricule = s.Matricule; c.NomComplet = s.NomComplet; c.CategorieEmploiId = s.CategorieEmploiId; c.StructureId = s.StructureId; c.SiteId = s.SiteId; },
            Inclure = q => q.Include(x => x.CategorieEmploi).Include(x => x.Structure).Include(x => x.Site),
            Trier = q => q.OrderBy(x => x.NomComplet),
            Colonnes =
            [
                Colonnes<Agent>.Texte("Matricule", x => x.Matricule, (x, v) => x.Matricule = v ?? "", 30, true),
                Colonnes<Agent>.Texte("Nom complet", x => x.NomComplet, (x, v) => x.NomComplet = v ?? "", 200, true),
                Colonnes<Agent>.Reference<CategorieEmploi>("Catégorie d'emplois", x => x.CategorieEmploi?.Code, (x, v) => x.CategorieEmploiId = v ?? Ulid.Empty, c => c.Code, obligatoire: true),
                Colonnes<Agent>.Reference<Structure>("Structure", x => x.Structure?.Code, (x, v) => x.StructureId = v ?? Ulid.Empty, s => s.Code, obligatoire: true),
                Colonnes<Agent>.Reference<Site>("Site", x => x.Site?.Code, (x, v) => x.SiteId = v, s => s.Code),
                Actif<Agent>(),
            ]
        });

        // ═════ Tiers et partenaires ═══════════════════════════════════════════
        Ajouter(GroupeTiers, new DescripteurReferentiel<Tiers>
        {
            Cle = "tiers", Titre = "Tiers et partenaires", Element = "le tiers",
            Code = x => x.Code, Libelle = x => x.RaisonSociale,
            Normaliser = x =>
            {
                x.Code = Cle(x.Code); x.RaisonSociale = Texte(x.RaisonSociale); x.Sigle = Facultatif(x.Sigle);
                x.IdentifiantFiscal = Facultatif(x.IdentifiantFiscal); x.Adresse = Facultatif(x.Adresse);
                x.Telephone = Facultatif(x.Telephone); x.Email = Facultatif(x.Email);
            },
            Valider = x => Enumere(x.TypeTiers, "type")
                ?? (x.Email is not null && !MailAddress.TryCreate(x.Email, out _) ? "L'adresse e-mail n'est pas valide." : null)
                ?? Longueurs((x.Code, 20, "code"), (x.RaisonSociale, 250, "raison sociale"), (x.Sigle, 30, "sigle"),
                             (x.IdentifiantFiscal, 30, "identifiant fiscal"), (x.Adresse, 300, "adresse"), (x.Telephone, 30, "téléphone"), (x.Email, 150, "e-mail")),
            ValiderAsync = async (ctx, x) =>
                await ValeurDeListeAsync(ctx, x.Id, x.CategorieEntiteId, CategoriesListeValeur.CategorieEntite, "La catégorie")
                ?? await ReferenceAsync<Tiers>(ctx, x.Id, x.TiersParentId, "L'entité de rattachement")
                ?? await SansCycleAsync(ctx, x.Id, x.TiersParentId, (Tiers t) => t.TiersParentId, "Un tiers ne peut pas se rattacher à lui-même ni à l'une de ses sous-entités.")
                ?? await ReferenceAsync<CompteComptable>(ctx, x.Id, x.CompteGeneralId, "Le compte général", c => c.TypeCompte == TypeCompte.General)
                ?? await ReferenceAsync<CompteComptable>(ctx, x.Id, x.CompteAuxiliaireId, "Le compte auxiliaire", c => c.TypeCompte == TypeCompte.Auxiliaire),
            Copier = (s, c) =>
            {
                c.Code = s.Code; c.TypeTiers = s.TypeTiers; c.CategorieEntiteId = s.CategorieEntiteId; c.RaisonSociale = s.RaisonSociale;
                c.Sigle = s.Sigle; c.TiersParentId = s.TiersParentId; c.IdentifiantFiscal = s.IdentifiantFiscal; c.Adresse = s.Adresse;
                c.Telephone = s.Telephone; c.Email = s.Email; c.CompteGeneralId = s.CompteGeneralId; c.CompteAuxiliaireId = s.CompteAuxiliaireId;
            },
            Inclure = q => q.Include(x => x.CategorieEntite).Include(x => x.TiersParent).Include(x => x.CompteGeneral).Include(x => x.CompteAuxiliaire),
            Trier = q => q.OrderBy(x => x.RaisonSociale),
            Colonnes =
            [
                Colonnes<Tiers>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<Tiers>.Texte("Raison sociale", x => x.RaisonSociale, (x, v) => x.RaisonSociale = v ?? "", 250, true),
                Colonnes<Tiers>.Enumeration("Type", x => x.TypeTiers, (x, v) => x.TypeTiers = v, L.TypeTiers),
                ColonneListe<Tiers>("Catégorie", x => x.CategorieEntite?.Code, (x, v) => x.CategorieEntiteId = v, CategoriesListeValeur.CategorieEntite),
                Colonnes<Tiers>.Texte("Sigle", x => x.Sigle, (x, v) => x.Sigle = v, 30),
                Colonnes<Tiers>.Reference<Tiers>("Entité de rattachement", x => x.TiersParent?.Code, (x, v) => x.TiersParentId = v, t => t.Code),
                Colonnes<Tiers>.Texte("Identifiant fiscal", x => x.IdentifiantFiscal, (x, v) => x.IdentifiantFiscal = v, 30),
                Colonnes<Tiers>.Texte("Adresse", x => x.Adresse, (x, v) => x.Adresse = v, 300),
                Colonnes<Tiers>.Texte("Téléphone", x => x.Telephone, (x, v) => x.Telephone = v, 30),
                Colonnes<Tiers>.Texte("E-mail", x => x.Email, (x, v) => x.Email = v, 150),
                Colonnes<Tiers>.Reference<CompteComptable>("Compte général", x => x.CompteGeneral?.Numero, (x, v) => x.CompteGeneralId = v, c => c.Numero),
                Colonnes<Tiers>.Reference<CompteComptable>("Compte auxiliaire", x => x.CompteAuxiliaire?.Numero, (x, v) => x.CompteAuxiliaireId = v, c => c.Numero),
                Actif<Tiers>(),
            ]
        });

        // ═════ Entretien routier ══════════════════════════════════════════════
        Ajouter(GroupeTechnique, new DescripteurReferentiel<TypeTravaux>
        {
            Cle = "types-travaux", Titre = "Types de travaux et de contrôle", Element = "le type de travaux",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); if (!x.EstPrestationControle) x.TypeTravauxControleId = null; },
            Valider = x => x.EstPrestationControle && x.TypeTravauxControleId is null
                ? "Une prestation de contrôle indique les travaux qu'elle contrôle."
                : Longueurs((x.Code, 20, "code"), (x.Libelle, 200, "libellé")),
            ValiderAsync = async (ctx, x) =>
                await ValeurDeListeAsync(ctx, x.Id, x.TypeVoieId, CategoriesListeValeur.TypeVoie, "Le type de voie")
                ?? await ReferenceAsync<TypeTravaux>(ctx, x.Id, x.TypeTravauxControleId, "Les travaux contrôlés", t => !t.EstPrestationControle && t.Id != x.Id),
            Copier = (s, c) =>
            {
                c.Code = s.Code; c.Libelle = s.Libelle; c.TypeVoieId = s.TypeVoieId; c.EstPrestationControle = s.EstPrestationControle;
                c.TypeTravauxControleId = s.TypeTravauxControleId; c.Ordre = s.Ordre;
            },
            Inclure = q => q.Include(x => x.TypeVoie).Include(x => x.TypeTravauxControle),
            Trier = q => q.OrderBy(x => x.Ordre).ThenBy(x => x.Code),
            Colonnes =
            [
                Colonnes<TypeTravaux>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<TypeTravaux>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 200, true),
                ColonneListe<TypeTravaux>("Type de voie", x => x.TypeVoie?.Code, (x, v) => x.TypeVoieId = v, CategoriesListeValeur.TypeVoie),
                Colonnes<TypeTravaux>.OuiNon("Prestation de contrôle", x => x.EstPrestationControle, (x, v) => x.EstPrestationControle = v, false),
                Colonnes<TypeTravaux>.Reference<TypeTravaux>("Travaux contrôlés", x => x.TypeTravauxControle?.Code, (x, v) => x.TypeTravauxControleId = v, t => t.Code),
                Colonnes<TypeTravaux>.Entier("Ordre", x => x.Ordre, (x, v) => x.Ordre = v ?? 0, min: 0),
                Actif<TypeTravaux>(),
            ]
        });

        // ═════ Recettes ═══════════════════════════════════════════════════════
        Ajouter(GroupeRecettes, new DescripteurReferentiel<NatureRecette>
        {
            Cle = "natures-recette", Titre = "Natures de recettes", Element = "la nature de recette",
            Code = x => x.Code, Libelle = x => x.Libelle,
            Normaliser = x => { x.Code = Cle(x.Code); x.Libelle = Texte(x.Libelle); },
            Valider = x => Enumere(x.CategorieRecette, "catégorie") ?? Longueurs((x.Code, 20, "code"), (x.Libelle, 200, "libellé")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<NatureRecette>(ctx, x.Id, x.NatureParentId, "La nature de regroupement")
                ?? await SansCycleAsync(ctx, x.Id, x.NatureParentId, (NatureRecette n) => n.NatureParentId, "Une nature ne peut pas se regrouper sous elle-même.")
                ?? await ReferenceAsync<CompteComptable>(ctx, x.Id, x.CompteComptableId, "Le compte de produit"),
            Copier = (s, c) =>
            {
                c.Code = s.Code; c.Libelle = s.Libelle; c.CategorieRecette = s.CategorieRecette; c.NatureParentId = s.NatureParentId;
                c.CompteComptableId = s.CompteComptableId; c.SuiviParSite = s.SuiviParSite;
            },
            Inclure = q => q.Include(x => x.NatureParent).Include(x => x.CompteComptable),
            Colonnes =
            [
                Colonnes<NatureRecette>.Texte("Code", x => x.Code, (x, v) => x.Code = v ?? "", 20, true),
                Colonnes<NatureRecette>.Texte("Libellé", x => x.Libelle, (x, v) => x.Libelle = v ?? "", 200, true),
                Colonnes<NatureRecette>.Enumeration("Catégorie", x => x.CategorieRecette, (x, v) => x.CategorieRecette = v, L.CategorieRecette),
                Colonnes<NatureRecette>.Reference<NatureRecette>("Nature de regroupement", x => x.NatureParent?.Code, (x, v) => x.NatureParentId = v, n => n.Code),
                Colonnes<NatureRecette>.Reference<CompteComptable>("Compte de produit", x => x.CompteComptable?.Numero, (x, v) => x.CompteComptableId = v, c => c.Numero),
                Colonnes<NatureRecette>.OuiNon("Suivi par site", x => x.SuiviParSite, (x, v) => x.SuiviParSite = v, false),
                Actif<NatureRecette>(),
            ]
        });

        Ajouter(GroupeRecettes, new DescripteurReferentiel<CompteBancaire>
        {
            Cle = "comptes-bancaires", Titre = "Comptes bancaires", Element = "le compte bancaire",
            Code = x => x.NumeroCompte, Libelle = x => x.Intitule,
            Normaliser = x => { x.NumeroCompte = Texte(x.NumeroCompte).ToUpperInvariant(); x.Intitule = Texte(x.Intitule); x.Banque = Texte(x.Banque); },
            Valider = x => x.Banque.Length == 0 ? "La banque est obligatoire."
                         : Longueurs((x.NumeroCompte, 50, "numéro"), (x.Intitule, 200, "intitulé"), (x.Banque, 150, "banque")),
            ValiderAsync = async (ctx, x) =>
                await ReferenceAsync<Devise>(ctx, x.Id, x.DeviseId, "La devise", obligatoire: true)
                ?? await ReferenceAsync<CompteComptable>(ctx, x.Id, x.CompteComptableId, "Le compte de trésorerie"),
            Copier = (s, c) => { c.Banque = s.Banque; c.Intitule = s.Intitule; c.NumeroCompte = s.NumeroCompte; c.DeviseId = s.DeviseId; c.CompteComptableId = s.CompteComptableId; },
            Inclure = q => q.Include(x => x.Devise).Include(x => x.CompteComptable),
            Trier = q => q.OrderBy(x => x.Banque).ThenBy(x => x.NumeroCompte),
            Colonnes =
            [
                Colonnes<CompteBancaire>.Texte("Numéro de compte", x => x.NumeroCompte, (x, v) => x.NumeroCompte = v ?? "", 50, true),
                Colonnes<CompteBancaire>.Texte("Intitulé", x => x.Intitule, (x, v) => x.Intitule = v ?? "", 200, true),
                Colonnes<CompteBancaire>.Texte("Banque", x => x.Banque, (x, v) => x.Banque = v ?? "", 150, true),
                Colonnes<CompteBancaire>.Reference<Devise>("Devise", x => x.Devise?.CodeIso, (x, v) => x.DeviseId = v ?? Ulid.Empty, d => d.CodeIso, obligatoire: true),
                Colonnes<CompteBancaire>.Reference<CompteComptable>("Compte de trésorerie", x => x.CompteComptable?.Numero, (x, v) => x.CompteComptableId = v, c => c.Numero),
                Actif<CompteBancaire>(),
            ]
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Règles et colonnes partagées
    // ═════════════════════════════════════════════════════════════════════════

    private static string Cle(string? v) => (v ?? "").Trim().ToUpperInvariant();
    private static string Texte(string? v) => (v ?? "").Trim();
    private static string? Facultatif(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string? Longueurs(params (string? Valeur, int Max, string Nom)[] champs)
        => champs.Where(c => c.Valeur?.Length > c.Max)
                 .Select(c => $"Le champ « {c.Nom} » ne doit pas dépasser {c.Max} caractères.")
                 .FirstOrDefault();

    private static string? Enumere<TEnum>(TEnum valeur, string nom) where TEnum : struct, Enum
        => Enum.IsDefined(valeur) ? null : $"Choisissez le {nom}.";

    /// <summary>Colonne « Actif » (Oui / Non), présente sur tous les référentiels activables.</summary>
    private static ColonneExcel<T> Actif<T>() where T : EntiteOrganisme, IActivable
        => Colonnes<T>.OuiNon("Actif", x => x.EstActif, (x, v) => x.EstActif = v, true);

    /// <summary>Colonne qui référence une valeur d'une catégorie de liste de valeurs, par son code.</summary>
    private static ColonneExcel<T> ColonneListe<T>(string entete, Func<T, string?> lireCode, Action<T, Ulid?> ecrire, string categorie)
        where T : EntiteOrganisme
        => Colonnes<T>.Reference<ListeValeur>(entete, lireCode, ecrire, l => l.Code,
            filtre: l => l.Categorie == categorie, cleFiltre: categorie) with { Aide = $"code d'une valeur de la liste {categorie}" };

    /// <summary>
    /// La référence existe dans l'organisme courant (filtre global) et respecte la condition.
    /// Elle doit être active à la création ; à la modification, une référence déjà en place
    /// devenue inactive est tolérée.
    /// </summary>
    private static async Task<string?> ReferenceAsync<TRef>(AppDbContext ctx, Ulid elementId, Ulid? id, string nom,
                                                            Expression<Func<TRef, bool>>? condition = null, bool obligatoire = false)
        where TRef : EntiteOrganisme
    {
        if (id is null || id == Ulid.Empty) return obligatoire ? $"{nom} est obligatoire." : null;

        var q = ctx.Set<TRef>().AsNoTracking().Where(r => r.Id == id);
        if (condition is not null) q = q.Where(condition);
        var reference = await q.FirstOrDefaultAsync();

        if (reference is null) return $"{nom} est introuvable, ou ne convient pas.";
        if (elementId == Ulid.Empty && reference is IActivable { EstActif: false }) return $"{nom} est inactif.";
        return null;
    }

    private static Task<string?> ReferenceAsync<TRef>(AppDbContext ctx, Ulid elementId, Ulid id, string nom, bool obligatoire)
        where TRef : EntiteOrganisme
        => ReferenceAsync<TRef>(ctx, elementId, (Ulid?)id, nom, null, obligatoire);

    private static Task<string?> ValeurDeListeAsync(AppDbContext ctx, Ulid elementId, Ulid? id, string categorie, string nom)
        => ReferenceAsync<ListeValeur>(ctx, elementId, id, nom, l => l.Categorie == categorie);

    /// <summary>
    /// Pas de cycle dans une hiérarchie : on remonte les parents depuis le parent choisi,
    /// sans jamais retomber sur l'élément lui-même (profondeur bornée).
    /// </summary>
    private static async Task<string?> SansCycleAsync<TRef>(AppDbContext ctx, Ulid id, Ulid? parentId,
                                                            Expression<Func<TRef, Ulid?>> parent, string message)
        where TRef : EntiteOrganisme
    {
        if (parentId is null || id == Ulid.Empty) return null;
        if (parentId == id) return message;

        var courant = parentId;
        for (var profondeur = 0; courant is not null && profondeur < 50; profondeur++)
        {
            var valeur = courant.Value;
            courant = await ctx.Set<TRef>().AsNoTracking().Where(r => r.Id == valeur).Select(parent).FirstOrDefaultAsync();
            if (courant == id) return message;
        }
        return null;
    }

    /// <summary>Le sens d'une ligne suit le type de son axe : dépense sous un axe de dépense, recette sous un axe de recette.</summary>
    private static async Task<string?> SensCoherentAsync(AppDbContext ctx, LigneBudgetaire x)
    {
        var typeAxe = await ctx.SousRubriques.AsNoTracking()
            .Where(s => s.Id == x.SousRubriqueId)
            .Select(s => (TypeAxe?)s.Rubrique.Axe.TypeAxe)
            .FirstOrDefaultAsync();

        var attendu = typeAxe == TypeAxe.Recette ? SensLigne.Recette : SensLigne.Depense;
        return typeAxe is not null && x.Sens != attendu
            ? $"Cette ligne relève d'un axe de {(typeAxe == TypeAxe.Recette ? "recettes" : "dépenses")} : son sens doit être « {L.De(L.SensLigne, attendu)} »."
            : null;
    }
}
