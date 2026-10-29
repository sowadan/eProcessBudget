using System.Linq.Expressions;
using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>Critères d'une liste de référentiel.</summary>
/// <param name="Condition">Restriction posée par l'écran (ex. une catégorie de liste de valeurs).</param>
public sealed record FiltreReferentiel<T>(
    string? Recherche = null, bool? EstActif = null, Expression<Func<T, bool>>? Condition = null,
    int Page = 1, int Taille = 20);

/// <summary>
/// Gestion d'un référentiel du Paramétrage : liste paginée, sélection, création,
/// modification, activation, suppression logique, export et import Excel.
///
/// Injecté sous <c>IReferentielService&lt;Structure&gt;</c>, <c>IReferentielService&lt;Axe&gt;</c>... :
/// le comportement propre à chaque table vient de son descripteur (<see cref="CatalogueReferentiels"/>).
/// </summary>
public interface IReferentielService<T> where T : EntiteOrganisme, new()
{
    DescripteurReferentiel<T> Descripteur { get; }

    /// <summary>L'utilisateur connecté peut-il modifier ce référentiel ?</summary>
    Task<bool> PeutGererAsync();

    Task<(List<T> Items, int Total)> ListerAsync(FiltreReferentiel<T> filtre);

    /// <summary>Choix proposés dans les formulaires : « code — libellé », éléments actifs par défaut.</summary>
    Task<List<ElementListe>> ListerPourSelectionAsync(Expression<Func<T, bool>>? condition = null, bool actifsSeulement = true);

    Task<T?> ObtenirAsync(Ulid id);
    Task<(bool Ok, string? Erreur, T? Element)> CreerAsync(T saisie);
    Task<(bool Ok, string? Erreur)> ModifierAsync(T saisie);

    /// <summary>Active ou désactive. Désactivation refusée si des éléments actifs en dépendent.</summary>
    Task<(bool Ok, string? Erreur)> ChangerEtatAsync(Ulid id, bool actif);

    /// <summary>Suppression logique, refusée si l'élément est utilisé (règle § 3.4).</summary>
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid id);

    /// <summary>Classeur Excel du référentiel, qui sert aussi de modèle d'import. null sans le droit.</summary>
    Task<byte[]?> ExporterAsync();

    /// <summary>Import tout ou rien, avec rapport d'erreurs ligne par ligne.</summary>
    Task<RapportImport> ImporterAsync(Stream fichier);
}

public sealed class ReferentielService<T> : IReferentielService<T> where T : EntiteOrganisme, new()
{
    private const int TailleMax = 200;
    private const int ExportMax = 20_000;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public ReferentielService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
        Descripteur = CatalogueReferentiels.Obtenir<T>();
    }

    public DescripteurReferentiel<T> Descripteur { get; }

    private DescripteurReferentiel<T> D => Descripteur;

    private string MessageGestion => $"Vous n'avez pas le droit de modifier : {D.Titre.ToLowerInvariant()}.";

    public async Task<bool> PeutGererAsync() => await _acces.ActeurAsync(D.DroitsGestion) != Ulid.Empty;

    // ═════════════════════════════════════════════════════════════════════════
    //  Lecture
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(List<T> Items, int Total)> ListerAsync(FiltreReferentiel<T> filtre)
    {
        if (await _acces.ActeurAsync(D.DroitsLecture) == Ulid.Empty) return ([], 0);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Set<T>().AsNoTracking();

        if (filtre.Condition is not null) q = q.Where(filtre.Condition);
        if (!string.IsNullOrWhiteSpace(filtre.Recherche)) q = q.Where(D.PredicatRecherche(filtre.Recherche.Trim()));
        if (filtre.EstActif is { } actif && D.EstActivable)
            q = q.Where(D.PredicatActif(actif));

        var total = await q.CountAsync();
        var taille = Math.Clamp(filtre.Taille, 1, TailleMax);

        if (D.Inclure is not null) q = D.Inclure(q);
        var items = await Trier(q)
            .Skip(Math.Max(0, (filtre.Page - 1) * taille))
            .Take(taille)
            .ToListAsync();

        return (items, total);
    }

    public async Task<List<ElementListe>> ListerPourSelectionAsync(Expression<Func<T, bool>>? condition = null, bool actifsSeulement = true)
    {
        if (await _acces.MembreOrganismeAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Set<T>().AsNoTracking();
        if (condition is not null) q = q.Where(condition);
        if (actifsSeulement && D.EstActivable) q = q.Where(D.PredicatActif(true));

        // Projection : trois colonnes, pas l'entité.
        var x = D.Code.Parameters[0];
        var libelle = new RemplacementParametre(D.Libelle.Parameters[0], x).Visit(D.Libelle.Body);
        var projection = Expression.Lambda<Func<T, (Ulid, string, string)>>(
            Expression.New(typeof(ValueTuple<Ulid, string, string>).GetConstructor([typeof(Ulid), typeof(string), typeof(string)])!,
                           Expression.Property(x, nameof(BaseEntity.Id)), D.Code.Body, libelle), x);

        var lignes = await Trier(q).Select(projection).ToListAsync();
        return lignes.Select(l => new ElementListe(l.Item1, $"{l.Item2} — {l.Item3}")).ToList();
    }

    public async Task<T?> ObtenirAsync(Ulid id)
    {
        if (await _acces.ActeurAsync(D.DroitsLecture) == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Écriture
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(bool Ok, string? Erreur, T? Element)> CreerAsync(T saisie)
    {
        var acteurId = await _acces.ActeurAsync(D.DroitsGestion);
        if (acteurId == Ulid.Empty) return (false, MessageGestion, null);

        await using var ctx = await _factory.CreateDbContextAsync();

        saisie.Id = Ulid.Empty;
        var erreur = await ValiderAsync(ctx, saisie);
        if (erreur is not null) return (false, erreur, null);

        // Seuls les champs modifiables sont repris ; l'organisme est posé par le contexte.
        var element = new T { CreePar = acteurId, DateCreation = DateTime.UtcNow };
        D.Copier(saisie, element);
        if (element is IActivable activable) activable.EstActif = true;

        ctx.Set<T>().Add(element);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CREATION", D.ModuleAudit, typeof(T).Name,
            element.Id.ToString(), null, Instantane(element));

        return (true, null, element);
    }

    public async Task<(bool Ok, string? Erreur)> ModifierAsync(T saisie)
    {
        var acteurId = await _acces.ActeurAsync(D.DroitsGestion);
        if (acteurId == Ulid.Empty) return (false, MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var existant = await ctx.Set<T>().FirstOrDefaultAsync(e => e.Id == saisie.Id);
        if (existant is null) return (false, $"{Majuscule(D.Element)} est introuvable.");

        var erreur = await ValiderAsync(ctx, saisie);
        if (erreur is not null) return (false, erreur);

        var avant = Instantane(existant);

        // Concurrence : refus si l'élément a changé depuis son ouverture à l'écran.
        if (saisie.RowVersion is { Length: > 0 })
            ctx.Entry(existant).Property(e => e.RowVersion).OriginalValue = saisie.RowVersion;

        D.Copier(saisie, existant);
        existant.ModifiePar = acteurId;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, $"{Majuscule(D.Element)} a été modifié(e) par quelqu'un d'autre entre-temps. Rechargez la page.");
        }

        await _audit.LoggerAsync(acteurId, null, "MODIFICATION", D.ModuleAudit, typeof(T).Name,
            existant.Id.ToString(), avant, Instantane(existant));

        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ChangerEtatAsync(Ulid id, bool actif)
    {
        var acteurId = await _acces.ActeurAsync(D.DroitsGestion);
        if (acteurId == Ulid.Empty) return (false, MessageGestion);
        if (!D.EstActivable) return (false, "Ce référentiel n'a pas de statut actif / inactif.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var element = await ctx.Set<T>().FirstOrDefaultAsync(e => e.Id == id);
        if (element is null) return (false, $"{Majuscule(D.Element)} est introuvable.");

        var activable = (IActivable)element;
        if (activable.EstActif == actif) return (true, null);

        if (!actif)
        {
            var utilisation = await VerificateurUtilisation.TrouverAsync(ctx, typeof(T), id, actifsSeulement: true);
            if (utilisation is not null)
                return (false, $"Désactivation impossible : {D.Element} est utilisé(e) par {utilisation} encore actifs. Désactivez-les d'abord.");
        }

        activable.EstActif = actif;
        element.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, actif ? "ACTIVATION" : "DESACTIVATION", D.ModuleAudit,
            typeof(T).Name, id.ToString(), null, Instantane(element));

        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid id)
    {
        var acteurId = await _acces.ActeurAsync(D.DroitsGestion);
        if (acteurId == Ulid.Empty) return (false, MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var element = await ctx.Set<T>().FirstOrDefaultAsync(e => e.Id == id);
        if (element is null) return (false, $"{Majuscule(D.Element)} est introuvable.");

        var utilisation = await VerificateurUtilisation.TrouverAsync(ctx, typeof(T), id, actifsSeulement: false, D.LiaisonsPossedees);
        if (utilisation is not null)
            return (false, $"Suppression impossible : {D.Element} est utilisé(e) par {utilisation}. Vous pouvez le (la) désactiver.");

        // Liaisons possédées (ex. comptes d'une ligne) : supprimées avec l'élément, dans la même transaction.
        foreach (var liaison in D.LiaisonsPossedees)
            await SupprimerLiaisonsAsync(ctx, liaison, id);

        element.EstSupprime = true;
        element.DateSuppression = DateTime.UtcNow;
        element.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION", D.ModuleAudit, typeof(T).Name,
            id.ToString(), Instantane(element), null);

        return (true, null);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Excel
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<byte[]?> ExporterAsync()
    {
        var acteurId = await _acces.ActeurAsync(D.DroitsLecture);
        if (acteurId == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Set<T>().AsNoTracking();
        if (D.Inclure is not null) q = D.Inclure(q);
        var elements = await Trier(q).Take(ExportMax).ToListAsync();

        // Comptes cités (responsables...) : leurs identifiants de connexion, en une requête.
        Dictionary<Ulid, string>? matricules = null;
        var colonnesUtilisateur = D.Colonnes.Where(c => c.LireUtilisateur is not null).ToList();
        if (colonnesUtilisateur.Count > 0)
        {
            var ids = elements.SelectMany(e => colonnesUtilisateur.Select(c => c.LireUtilisateur!(e)))
                              .OfType<Ulid>().Distinct().ToList();
            matricules = await ctx.Utilisateurs.AsNoTracking().Where(u => ids.Contains(u.Id))
                                  .ToDictionaryAsync(u => u.Id, u => u.Matricule);
        }

        // Construction du classeur hors du fil du circuit : c'est du calcul.
        var octets = await Task.Run(() => ExcelReferentiel.Exporter(D, elements, matricules));

        await _audit.LoggerAsync(acteurId, null, "EXPORT", D.ModuleAudit, typeof(T).Name,
            commentaire: $"{elements.Count} ligne(s)");
        return octets;
    }

    public async Task<RapportImport> ImporterAsync(Stream fichier)
    {
        var acteurId = await _acces.ActeurAsync(D.DroitsGestion);
        if (acteurId == Ulid.Empty) return RapportImport.Refus(MessageGestion);
        if (D.Colonnes.Count == 0) return RapportImport.Refus("Ce référentiel ne s'importe pas par Excel.");

        var (lignes, erreurs) = await Task.Run(() => ExcelReferentiel.Lire(fichier, D));
        if (erreurs.Count > 0) return new RapportImport(lignes.Count, 0, 0, erreurs);

        await using var ctx = await _factory.CreateDbContextAsync();
        var contexte = new ContexteImport(ctx);
        var cle = D.CleImport ?? D.Code;
        var lireCle = cle.Compile();

        // 1. Lecture de chaque ligne dans un élément « saisie ».
        var saisies = new List<(int Ligne, T Saisie)>();
        foreach (var (numero, valeurs) in lignes)
        {
            var saisie = new T();
            var ok = true;
            foreach (var colonne in D.Colonnes)
            {
                var message = await colonne.Ecrire(contexte, saisie, valeurs.GetValueOrDefault(colonne.Entete));
                if (message is null) continue;
                erreurs.Add(new ErreurImport(numero, colonne.Entete, message));
                ok = false;
            }
            if (!ok) continue;

            // Normalisée avant le rapprochement : « ent » et « ENT » désignent le même élément,
            // quelle que soit la collation de la base.
            D.Normaliser?.Invoke(saisie);
            saisies.Add((numero, saisie));
        }

        // 2. Rapprochement avec l'existant, par clé : seules les clés du fichier sont lues.
        var cles = saisies.Select(s => lireCle(s.Saisie)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existants = (await ctx.Set<T>().Where(Expressions.Dans(cle, cles)).ToListAsync())
            .ToDictionary(e => lireCle(e), StringComparer.OrdinalIgnoreCase);

        var vues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int crees = 0, modifies = 0;

        foreach (var (numero, saisie) in saisies)
        {
            var cleLigne = lireCle(saisie);
            if (vues.TryGetValue(cleLigne, out var premiere))
            {
                erreurs.Add(new ErreurImport(numero, D.Colonnes[0].Entete, $"« {cleLigne} » figure déjà ligne {premiere}"));
                continue;
            }
            vues[cleLigne] = numero;

            existants.TryGetValue(cleLigne, out var existant);
            saisie.Id = existant?.Id ?? Ulid.Empty;

            // Même règle qu'à l'écran : pas de désactivation d'un élément dont dépendent des éléments actifs.
            if (existant is IActivable { EstActif: true } && saisie is IActivable { EstActif: false }
                && await VerificateurUtilisation.TrouverAsync(ctx, typeof(T), existant.Id, actifsSeulement: true) is { } utilise)
            {
                erreurs.Add(new ErreurImport(numero, null, $"désactivation impossible : utilisé(e) par {utilise} encore actifs"));
                continue;
            }

            var message = await ValiderAsync(ctx, saisie);
            if (message is not null)
            {
                erreurs.Add(new ErreurImport(numero, null, message));
                continue;
            }

            if (existant is null)
            {
                var element = new T { CreePar = acteurId, DateCreation = DateTime.UtcNow };
                D.Copier(saisie, element);
                if (element is IActivable a && saisie is IActivable s) a.EstActif = s.EstActif;
                ctx.Set<T>().Add(element);
                crees++;
            }
            else
            {
                D.Copier(saisie, existant);
                if (existant is IActivable a && saisie is IActivable s) a.EstActif = s.EstActif;
                existant.ModifiePar = acteurId;
                modifies++;
            }
        }

        // 3. Tout ou rien : une seule erreur et rien n'est enregistré.
        if (erreurs.Count > 0)
            return new RapportImport(lignes.Count, 0, 0, erreurs.OrderBy(e => e.Ligne).ToList());

        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "IMPORT", D.ModuleAudit, typeof(T).Name,
            commentaire: $"{crees} créé(s), {modifies} modifié(s)");

        return new RapportImport(lignes.Count, crees, modifies, []);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Outils
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Normalisation, règles du descripteur, puis unicité de la clé dans l'organisme.</summary>
    private async Task<string?> ValiderAsync(AppDbContext ctx, T saisie)
    {
        D.Normaliser?.Invoke(saisie);

        if (string.IsNullOrWhiteSpace(D.LireCode(saisie))) return "Le code est obligatoire.";
        if (string.IsNullOrWhiteSpace(D.LireLibelle(saisie))) return "Le libellé est obligatoire.";

        var erreur = D.Valider?.Invoke(saisie);
        if (erreur is not null) return erreur;

        if (D.ValiderAsync is not null && await D.ValiderAsync(ctx, saisie) is { } erreurAsync) return erreurAsync;

        // Unicité dans l'organisme courant, lignes supprimées exclues (comme l'index filtré).
        if (await ctx.Set<T>().AnyAsync(D.PredicatDoublon(saisie)))
            return $"Le code « {D.LireCode(saisie)} » est déjà utilisé par un(e) autre {Sans(D.Element)}.";

        return null;
    }

    private IOrderedQueryable<T> Trier(IQueryable<T> q) => D.Trier?.Invoke(q) ?? q.OrderBy(D.Code);

    private static async Task SupprimerLiaisonsAsync(AppDbContext ctx, Type liaison, Ulid id)
    {
        var methode = typeof(ReferentielService<T>)
            .GetMethod(nameof(SupprimerLiaisonsTypeesAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(liaison);
        await (Task)methode.Invoke(null, [ctx, id])!;
    }

    /// <summary>Liaisons (tables allégées) qui pointent vers l'élément : suppression physique.</summary>
    private static async Task SupprimerLiaisonsTypeesAsync<TLiaison>(AppDbContext ctx, Ulid id) where TLiaison : class
    {
        var cle = ctx.Model.FindEntityType(typeof(TLiaison))!.GetForeignKeys()
            .First(c => c.PrincipalEntityType.ClrType == typeof(T));
        var nom = cle.Properties[0].Name;
        var liens = await ctx.Set<TLiaison>().Where(e => EF.Property<Ulid>(e, nom) == id).ToListAsync();
        ctx.Set<TLiaison>().RemoveRange(liens);
    }

    private object Instantane(T e) => new { Code = D.LireCode(e), Libelle = D.LireLibelle(e), (e as IActivable)?.EstActif };

    private static string Majuscule(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>« la structure » → « structure » (pour « un(e) autre structure »).</summary>
    private static string Sans(string element)
        => element.StartsWith("l'") ? element[2..]
         : element.StartsWith("le ") || element.StartsWith("la ") ? element[3..] : element;

    private sealed class RemplacementParametre(ParameterExpression ancien, ParameterExpression nouveau) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == ancien ? nouveau : node;
    }
}
