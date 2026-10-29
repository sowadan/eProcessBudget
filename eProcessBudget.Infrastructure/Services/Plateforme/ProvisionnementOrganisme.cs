using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Plateforme;

/// <summary>
/// Données initiales d'un organisme (cahier technique § 3.7, annexe B du cahier fonctionnel) :
/// paramètres, devise, seuils, axes, natures de dépense, sources de financement, unités,
/// listes de valeurs, structures, catégories d'emplois, risques, types de travaux et natures
/// de recettes. Tout reste modifiable ensuite dans le Paramétrage.
///
/// IDEMPOTENT : seuls les éléments absents (par code) sont ajoutés. On peut donc le rejouer
/// sur un organisme existant pour compléter ses référentiels, sans rien dupliquer ni écraser.
///
/// N'enregistre rien : les lignes sont ajoutées au contexte de l'appelant (un seul
/// SaveChanges, donc une seule transaction). Le contexte doit pouvoir écrire pour
/// l'organisme visé (organisme courant, ou contournement réservé au module Plateforme).
/// </summary>
internal static class ProvisionnementOrganisme
{
    /// <summary>Ajoute ce qui manque ; renvoie le nombre d'éléments ajoutés.</summary>
    public static async Task<int> CompleterAsync(AppDbContext ctx, Ulid organismeId, Ulid auteurId)
    {
        var p = new Ajout(ctx, organismeId, auteurId);

        // ── Devise et paramètres généraux ───────────────────────────────────
        var devise = await p.TrouverAsync<Devise>(d => d.CodeIso == "XOF")
                     ?? p.Nouveau(new Devise { CodeIso = "XOF", Libelle = "Franc CFA (UEMOA)", Symbole = "FCFA", EstReference = true });

        if (!await p.ExisteAsync<ParametreOrganisme>(_ => true))
            p.Nouveau(new ParametreOrganisme
            {
                DeviseReferenceId = devise.Id, NbDecimales = 2, DureeSessionMinutes = 30, NbTentativesMax = 5,
                TaillePagination = 20, RetentionJournauxMois = 12
            });

        if (!await p.ExisteAsync<SeuilAlerte>(s => s.Portee == PorteeSeuil.Globale))
        {
            p.Nouveau(new SeuilAlerte { Portee = PorteeSeuil.Globale, Pourcentage = 80, Couleur = "#F59E0B" });
            p.Nouveau(new SeuilAlerte { Portee = PorteeSeuil.Globale, Pourcentage = 90, Couleur = "#EA580C" });
            p.Nouveau(new SeuilAlerte { Portee = PorteeSeuil.Globale, Pourcentage = 100, Couleur = "#DC2626" });
        }

        // ── Axes, natures, sources, unités ──────────────────────────────────
        // Sections (C.2.4) : les travaux d'entretien et les appuis sont imputés en fonctionnement.
        await p.ParCodeAsync<Axe>(x => x.Code,
            new() { Code = "ENTRETIEN", Libelle = "Entretien routier", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement, Ordre = 1 },
            new() { Code = "INVESTISSEMENT", Libelle = "Investissements", TypeAxe = TypeAxe.Depense, Section = Section.Investissement, Ordre = 2 },
            new() { Code = "FONCTIONNEMENT", Libelle = "Fonctionnement", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement, Ordre = 3 },
            new() { Code = "APPUIS", Libelle = "Appuis", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement, Ordre = 4 },
            new() { Code = "RECETTES", Libelle = "Recettes", TypeAxe = TypeAxe.Recette, Section = Section.Fonctionnement, Ordre = 5 });

        await p.ParCodeAsync<NatureDepense>(x => x.Code,
            new() { Code = "1", Libelle = "Personnel", GroupeNature = GroupeNature.DepensesOrdinaires, Ordre = 1 },
            new() { Code = "2", Libelle = "Biens et services", GroupeNature = GroupeNature.DepensesOrdinaires, Ordre = 2 },
            new() { Code = "3", Libelle = "Transferts courants", GroupeNature = GroupeNature.DepensesOrdinaires, Ordre = 3 },
            new() { Code = "4", Libelle = "Investissements (RI)", GroupeNature = GroupeNature.DepensesEnCapital, Ordre = 4 },
            new() { Code = "5", Libelle = "Investissements (RE)", GroupeNature = GroupeNature.DepensesEnCapital, Ordre = 5 });

        await p.ParCodeAsync<SourceFinancement>(x => x.Code,
            new() { Code = "RI", Libelle = "Ressources internes" },
            new() { Code = "RE", Libelle = "Ressources externes" });

        await p.ParCodeAsync<UniteMesure>(x => x.Code,
            new() { Code = "POURCENT", Libelle = "Pourcentage", TypeUnite = TypeUnite.Pourcentage, Symbole = "%" },
            new() { Code = "NOMBRE", Libelle = "Nombre", TypeUnite = TypeUnite.Nombre },
            new() { Code = "JOURS", Libelle = "Nombre de jours", TypeUnite = TypeUnite.Duree, Symbole = "j" },
            new() { Code = "MONTANT", Libelle = "Montant", TypeUnite = TypeUnite.Montant },
            new() { Code = "KM", Libelle = "Kilomètre", TypeUnite = TypeUnite.Distance, Symbole = "km" });

        // ── Listes de valeurs ───────────────────────────────────────────────
        var listes = new (string Categorie, (string Code, string Libelle)[] Valeurs)[]
        {
            (CategoriesListeValeur.TypeStructure, [("DG", "Direction générale"), ("SERVICE", "Service"), ("CELLULE", "Cellule")]),
            (CategoriesListeValeur.TypeVoie, [("REVETU", "Réseau revêtu"), ("NON_REVETU", "Réseau non revêtu"),
                ("ASSAINISSEMENT", "Ouvrages d'assainissement et de drainage"), ("PISTES", "Pistes rurales")]),
            (CategoriesListeValeur.TypePrestation, [("ETUDES", "Études"), ("CONTROLE", "Contrôle technique"),
                ("SURVEILLANCE", "Surveillance"), ("QUALITE", "Qualité")]),
            (CategoriesListeValeur.CategorieEntite, [("TUTELLE", "Ministères de tutelle"), ("AGENCE", "Agence d'exécution des contrats routiers"),
                ("COLLECTIVITE", "Collectivités locales"), ("FORMATION", "Centres de formation"),
                ("SOCIETAL", "Actions sociétales et fonds spéciaux"), ("AUTRE", "Autres partenaires institutionnels")]),
            (CategoriesListeValeur.SourceVerification, [("RFT", "Rapport financier trimestriel"), ("RSEP", "Rapport semestriel d'exécution des programmes"),
                ("RAP", "Rapport annuel de performance"), ("AUDIT", "Rapport d'audit"), ("SUIVI_RECO", "Rapport de suivi des recommandations"),
                ("CMS", "Rapport de la cellule mixte de suivi"), ("PESEES", "Statistiques des pesées"),
                ("TDB_STAT", "Tableau de bord des statistiques"), ("OUTILS_PLANIF", "Outils de planification")]),
            (CategoriesListeValeur.CategorieRisque, [("PLANIFICATION", "Planification et programmation"), ("PROCEDURES", "Procédures et passation des marchés"),
                ("EXECUTION", "Exécution des activités"), ("RESSOURCES_HUMAINES", "Ressources humaines"), ("CONTROLE", "Contrôle et conformité")]),
            (CategoriesListeValeur.TypePenalite, [("RETARD", "Pénalité de retard"), ("MALFACON", "Malfaçon / non-conformité"), ("AUTRE", "Autre écart d'exécution")]),
            (CategoriesListeValeur.Periodicite, [("MENSUELLE", "Mensuelle"), ("TRIMESTRIELLE", "Trimestrielle"),
                ("SEMESTRIELLE", "Semestrielle"), ("ANNUELLE", "Annuelle")]),
        };
        foreach (var (categorie, valeurs) in listes)
            await p.ParCodeDansAsync<ListeValeur>(x => x.Categorie == categorie, x => x.Code, valeurs.Select((v, i) =>
                new ListeValeur { Categorie = categorie, Code = v.Code, Libelle = v.Libelle, Ordre = i + 1 }).ToArray());

        // ── Structures (annexe B.3) et catégories d'emplois ─────────────────
        var typeDg = await p.IdAsync<ListeValeur>(x => x.Categorie == CategoriesListeValeur.TypeStructure && x.Code == "DG");
        var typeService = await p.IdAsync<ListeValeur>(x => x.Categorie == CategoriesListeValeur.TypeStructure && x.Code == "SERVICE");
        var dg = await p.TrouverAsync<Structure>(s => s.Code == "DG")
                 ?? p.Nouveau(new Structure { Code = "DG", Sigle = "DG", Libelle = "Direction générale", TypeStructureId = typeDg });
        await p.ParCodeAsync<Structure>(x => x.Code,
            new() { Code = "SFC", Sigle = "SFC", Libelle = "Service financier et comptable", TypeStructureId = typeService, StructureParentId = dg.Id },
            new() { Code = "SEP", Sigle = "SEP", Libelle = "Service des études et de la planification", TypeStructureId = typeService, StructureParentId = dg.Id },
            new() { Code = "SAI", Sigle = "SAI", Libelle = "Service d'audit interne", TypeStructureId = typeService, StructureParentId = dg.Id },
            new() { Code = "SARH", Sigle = "SARH", Libelle = "Service administratif et des ressources humaines", TypeStructureId = typeService, StructureParentId = dg.Id });

        await p.ParCodeAsync<CategorieEmploi>(x => x.Code,
            new() { Code = "HC", Libelle = "Hors catégorie", Ordre = 1 },
            new() { Code = "CS", Libelle = "Cadres supérieurs", Ordre = 2 },
            new() { Code = "CM", Libelle = "Cadres moyens", Ordre = 3 },
            new() { Code = "AM", Libelle = "Agents de maîtrise", Ordre = 4 },
            new() { Code = "AE", Libelle = "Agents d'exécution", Ordre = 5 },
            new() { Code = "CONTRACTUEL", Libelle = "Contractuels (pesage)", Ordre = 6 },
            new() { Code = "TEMPORAIRE", Libelle = "Temporaires / stagiaires", Ordre = 7 });

        // ── Risques (annexe B.2) ────────────────────────────────────────────
        Task<Ulid?> CategorieRisque(string code)
            => p.IdAsync<ListeValeur>(x => x.Categorie == CategoriesListeValeur.CategorieRisque && x.Code == code);
        await p.ParCodeAsync<Risque>(x => x.Code,
            new() { Code = "R01", Libelle = "Mauvaise planification budgétaire", CategorieRisqueId = await CategorieRisque("PLANIFICATION") },
            new() { Code = "R02", Libelle = "Lenteur des procédures de passation des marchés publics", CategorieRisqueId = await CategorieRisque("PROCEDURES") },
            new() { Code = "R03", Libelle = "Retard dans la réalisation des activités et des travaux d'investissement", CategorieRisqueId = await CategorieRisque("EXECUTION") },
            new() { Code = "R04", Libelle = "Manque de disponibilité des responsables de programme ou d'action", CategorieRisqueId = await CategorieRisque("RESSOURCES_HUMAINES") },
            new() { Code = "R05", Libelle = "Non-respect des délais de traitement par les agents", CategorieRisqueId = await CategorieRisque("RESSOURCES_HUMAINES") },
            new() { Code = "R06", Libelle = "Absence d'informations sur les bordereaux de convoiement", CategorieRisqueId = await CategorieRisque("CONTROLE") },
            new() { Code = "R07", Libelle = "Non-respect des procédures de contrôle technique au pesage", CategorieRisqueId = await CategorieRisque("CONTROLE") });

        // ── Types de travaux et leurs prestations de contrôle (§ 3.2.7, réponse C.2.11) ──
        // Quatre types ; le type de voie se renseigne à part, sur l'opération (liste TYPE_VOIE).
        var travaux = new (string Code, string Libelle)[]
        {
            (CodesTypeTravaux.Courants, "Travaux courants d'entretien"),
            (CodesTypeTravaux.Periodiques, "Travaux périodiques d'entretien"),
            (CodesTypeTravaux.Cantonnage, "Travaux manuels d'entretien (cantonnage)"),
            (CodesTypeTravaux.Rehabilitation, "Projets de réhabilitation (investissement)"),
        };
        var ordre = 0;
        foreach (var (code, libelle) in travaux)
        {
            var ouvrage = await p.TrouverAsync<TypeTravaux>(t => t.Code == code)
                          ?? p.Nouveau(new TypeTravaux { Code = code, Libelle = libelle, Ordre = ++ordre * 10 });
            await p.ParCodeAsync<TypeTravaux>(x => x.Code, new TypeTravaux
            {
                Code = $"CTRL_{code}"[..Math.Min(20, code.Length + 5)], Libelle = $"Contrôle : {libelle}",
                EstPrestationControle = true, TypeTravauxControleId = ouvrage.Id, Ordre = ordre * 10 + 1
            });
        }

        // Types livrés auparavant (voie et type mêlés) : désactivés tant qu'aucune donnée ne les utilise.
        foreach (var code in CodesTypeTravaux.Anciens.SelectMany(c => new[] { c, $"CTRL_{c}"[..Math.Min(20, c.Length + 5)] }))
        {
            var ancien = await p.TrouverAsync<TypeTravaux>(t => t.Code == code && t.EstActif);
            if (ancien is null) continue;
            var utilise = await ctx.ProjetsTravaux.IgnoreQueryFilters().AnyAsync(x => x.TypeTravauxId == ancien.Id && !x.EstSupprime)
                       || await ctx.AvancementsPhysiques.IgnoreQueryFilters().AnyAsync(x => x.TypeTravauxId == ancien.Id && !x.EstSupprime);
            if (!utilise) ancien.EstActif = false;
        }

        // ── Natures de recettes (§ 3.2.8) ───────────────────────────────────
        async Task Famille(string code, string libelle, CategorieRecette categorie, params (string Code, string Libelle, bool ParSite)[] enfants)
        {
            var parent = await p.TrouverAsync<NatureRecette>(n => n.Code == code)
                         ?? p.Nouveau(new NatureRecette { Code = code, Libelle = libelle, CategorieRecette = categorie });
            await p.ParCodeAsync<NatureRecette>(x => x.Code, enfants.Select(e => new NatureRecette
            {
                Code = e.Code, Libelle = e.Libelle, CategorieRecette = categorie, NatureParentId = parent.Id, SuiviParSite = e.ParSite
            }).ToArray());
        }
        await Famille("RP", "Recettes propres", CategorieRecette.RecettePropre,
            ("RP_PEAGE", "Droits d'usage de la route (péage)", true),
            ("RP_PESAGE", "Pénalités et amendes de pesage", true),
            ("RP_DIVERS", "Loyers et produits divers", false));
        await Famille("TA", "Taxes affectées par l'État", CategorieRecette.TaxeAffectee,
            ("TA_ACCISE", "Droits d'accise sur les produits pétroliers", false),
            ("TA_TVM", "Taxe sur les véhicules à moteur", false));
        await Famille("RA", "Ressources additionnelles", CategorieRecette.RessourceAdditionnelle);
        await Famille("RE", "Ressources externes", CategorieRecette.RessourceExterne);
        await Famille("RR", "Reports de ressources antérieures", CategorieRecette.ReportRessourcesAnterieures,
            ("RR_TRESORERIE", "Trésorerie sans déduction d'encours", false),
            ("RR_FOURNISSEURS", "Fournisseurs débiteurs", false),
            ("RR_TAXES", "Taxes affectées antérieures", false),
            ("RR_PROPRES", "Recettes propres antérieures", false),
            ("RR_AVOIRS", "Avoirs sur l'État à recouvrer", false));

        return p.NbAjouts;
    }

    /// <summary>
    /// Ajouts pour un organisme donné. Les lectures filtrent explicitement sur cet
    /// organisme : elles valent aussi bien avec l'organisme courant qu'en contournement
    /// (création par l'Administrateur plateforme). Les ajouts non encore enregistrés sont
    /// vus via le suivi des modifications.
    /// </summary>
    private sealed class Ajout(AppDbContext ctx, Ulid organismeId, Ulid auteurId)
    {
        public int NbAjouts { get; private set; }

        public T Nouveau<T>(T entite) where T : EntiteOrganisme
        {
            entite.OrganismeId = organismeId;
            entite.CreePar = auteurId;
            ctx.Set<T>().Add(entite);     // l'ULID est attribué ici
            NbAjouts++;
            return entite;
        }

        public async Task<T?> TrouverAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> condition) where T : EntiteOrganisme
            => ctx.ChangeTracker.Entries<T>().Select(e => e.Entity)
                   .Where(e => e.OrganismeId == organismeId && !e.EstSupprime).FirstOrDefault(condition.Compile())
               ?? await ctx.Set<T>().IgnoreQueryFilters().Where(e => e.OrganismeId == organismeId && !e.EstSupprime)
                                   .FirstOrDefaultAsync(condition);

        public async Task<bool> ExisteAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> condition) where T : EntiteOrganisme
            => await TrouverAsync(condition) is not null;

        public async Task<Ulid?> IdAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> condition) where T : EntiteOrganisme
            => (await TrouverAsync(condition))?.Id;

        /// <summary>Ajoute les éléments dont le code n'existe pas encore (dans le filtre donné).</summary>
        public async Task ParCodeDansAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>>? filtre, Func<T, string> code,
                                              T[] elements) where T : EntiteOrganisme
        {
            var q = ctx.Set<T>().IgnoreQueryFilters().Where(e => e.OrganismeId == organismeId && !e.EstSupprime);
            if (filtre is not null) q = q.Where(filtre);
            var existants = (await q.ToListAsync()).Select(code)
                .Concat(ctx.ChangeTracker.Entries<T>().Select(e => e.Entity)
                           .Where(e => e.OrganismeId == organismeId && (filtre is null || filtre.Compile()(e))).Select(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var element in elements.Where(e => !existants.Contains(code(e))))
                Nouveau(element);
        }

        public Task ParCodeAsync<T>(Func<T, string> code, params T[] elements) where T : EntiteOrganisme
            => ParCodeDansAsync(null, code, elements);
    }
}
