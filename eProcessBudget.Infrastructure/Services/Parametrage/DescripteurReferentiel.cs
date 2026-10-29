using System.Globalization;
using System.Linq.Expressions;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>
/// Description d'un référentiel du Paramétrage : ce qui le distingue des autres (clé
/// métier, champs modifiables, règles, colonnes Excel). Tout le reste — droits, unicité
/// par organisme, désactivation, suppression logique, audit, import et export — est
/// commun et vit dans <see cref="ReferentielService{T}"/>.
///
/// Les descripteurs sont déclarés dans <see cref="CatalogueReferentiels"/>.
/// </summary>
public sealed class DescripteurReferentiel<T> where T : EntiteOrganisme
{
    /// <summary>Identifiant technique (URL, nom du fichier Excel) : « structures ».</summary>
    public required string Cle { get; init; }

    /// <summary>Titre de l'écran : « Structures et services ».</summary>
    public required string Titre { get; init; }

    /// <summary>Nom d'un élément dans les messages : « la structure ».</summary>
    public required string Element { get; init; }

    /// <summary>Droits dont l'un suffit pour consulter. Par défaut : ceux du Paramétrage.</summary>
    public TypeDroit[] DroitsLecture { get; init; } = [TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer];

    /// <summary>Droits dont l'un suffit pour modifier. Par défaut : ParametrageGerer.</summary>
    public TypeDroit[] DroitsGestion { get; init; } = [TypeDroit.ParametrageGerer];

    /// <summary>Module inscrit dans le journal d'audit.</summary>
    public string ModuleAudit { get; init; } = AccesParametrage.ModuleAudit;

    /// <summary>Clé métier, unique dans l'organisme (code, numéro de compte, matricule...).</summary>
    public required Expression<Func<T, string>> Code { get; init; }

    public required Expression<Func<T, string>> Libelle { get; init; }

    /// <summary>Recopie les champs modifiables d'une saisie vers l'élément enregistré.</summary>
    public required Action<T, T> Copier { get; init; }

    /// <summary>Nettoyage de la saisie (espaces, majuscules) avant validation.</summary>
    public Action<T>? Normaliser { get; init; }

    /// <summary>Règles qui ne lisent que l'élément (longueurs, cohérences). null = valide.</summary>
    public Func<T, string?>? Valider { get; init; }

    /// <summary>
    /// Règles qui lisent la base, dans l'organisme courant (référence existante et active,
    /// pas de cycle dans une hiérarchie...). null = valide.
    /// </summary>
    public Func<AppDbContext, T, Task<string?>>? ValiderAsync { get; init; }

    /// <summary>
    /// Doublon de clé métier pour une saisie. Par défaut : même <see cref="Code"/>, autre Id.
    /// Les listes de valeurs, par exemple, ne sont uniques que dans leur catégorie.
    /// </summary>
    public Func<T, Expression<Func<T, bool>>>? Doublon { get; init; }

    /// <summary>
    /// Clé de rapprochement d'une ligne importée avec un élément existant. Par défaut :
    /// <see cref="Code"/>. Les listes de valeurs, par exemple, se rapprochent par
    /// catégorie et code.
    /// </summary>
    public Expression<Func<T, string>>? CleImport { get; init; }

    /// <summary>Relations à charger pour la liste et l'export (libellés des références).</summary>
    public Func<IQueryable<T>, IQueryable<T>>? Inclure { get; init; }

    /// <summary>Ordre d'affichage. Par défaut : par code.</summary>
    public Func<IQueryable<T>, IOrderedQueryable<T>>? Trier { get; init; }

    /// <summary>Colonnes du fichier Excel, la première étant la clé métier.</summary>
    public IReadOnlyList<ColonneExcel<T>> Colonnes { get; init; } = [];

    /// <summary>
    /// Tables de liaison supprimées avec l'élément (ex. comptes d'une ligne budgétaire) :
    /// elles ne bloquent pas sa suppression.
    /// </summary>
    public IReadOnlyList<Type> LiaisonsPossedees { get; init; } = [];

    /// <summary>Le référentiel a-t-il un statut actif / inactif ?</summary>
    public bool EstActivable => typeof(IActivable).IsAssignableFrom(typeof(T));

    // ═════════════════════════════════════════════════════════════════════════
    //  Expressions dérivées, compilées une fois
    // ═════════════════════════════════════════════════════════════════════════

    private Func<T, string>? _codeCompile;
    private Func<T, string>? _libelleCompile;

    public string LireCode(T element) => (_codeCompile ??= Code.Compile())(element);
    public string LireLibelle(T element) => (_libelleCompile ??= Libelle.Compile())(element);

    /// <summary>Prédicat « même clé métier, autre élément ».</summary>
    public Expression<Func<T, bool>> PredicatDoublon(T saisie)
        => Doublon?.Invoke(saisie) ?? Expressions.Egal(Code, LireCode(saisie), saisie.Id);

    /// <summary>Prédicat « clé métier égale à ».</summary>
    public Expression<Func<T, bool>> PredicatCode(string code) => Expressions.Egal(Code, code, null);

    /// <summary>x => x.EstActif == actif, sur la propriété de l'entité (traduisible en SQL).</summary>
    public Expression<Func<T, bool>> PredicatActif(bool actif)
    {
        var x = Expression.Parameter(typeof(T), "x");
        return Expression.Lambda<Func<T, bool>>(
            Expression.Equal(Expression.Property(x, nameof(IActivable.EstActif)), Expressions.Parametre(actif)), x);
    }

    /// <summary>Recherche dans la clé métier et le libellé (traduit en LIKE paramétré).</summary>
    public Expression<Func<T, bool>> PredicatRecherche(string texte) => Expressions.Contient(Code, Libelle, texte);
}

/// <summary>
/// Colonne du fichier Excel d'un référentiel : lecture pour l'export, écriture pour
/// l'import (renvoie un message d'erreur, ou null).
/// </summary>
public sealed record ColonneExcel<T>(
    string Entete,
    bool Obligatoire,
    Func<T, object?> Lire,
    Func<ContexteImport, T, string?, Task<string?>> Ecrire,
    string? Aide = null)
{
    /// <summary>
    /// Colonne qui désigne un compte utilisateur : l'export écrit son identifiant de
    /// connexion (lu une fois pour tout le fichier), l'import le résout dans l'organisme.
    /// </summary>
    public Func<T, Ulid?>? LireUtilisateur { get; init; }
}

/// <summary>Fabriques des colonnes Excel courantes.</summary>
public static class Colonnes<T> where T : EntiteOrganisme
{
    public static ColonneExcel<T> Texte(string entete, Func<T, string?> lire, Action<T, string?> ecrire,
                                        int longueurMax, bool obligatoire = false, string? aide = null)
        => new(entete, obligatoire, lire, (_, e, v) =>
        {
            v = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            if (v is null && obligatoire) return Task.FromResult<string?>("valeur obligatoire");
            if (v?.Length > longueurMax) return Task.FromResult<string?>($"{longueurMax} caractères au plus");
            ecrire(e, v);
            return Task.FromResult<string?>(null);
        }, aide);

    public static ColonneExcel<T> Entier(string entete, Func<T, int?> lire, Action<T, int?> ecrire,
                                         bool obligatoire = false, int min = int.MinValue, int max = int.MaxValue)
        => new(entete, obligatoire, e => lire(e), (_, e, v) =>
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                if (obligatoire) return Task.FromResult<string?>("valeur obligatoire");
                ecrire(e, null);
                return Task.FromResult<string?>(null);
            }
            var t = v.Trim();
            int n;
            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var entier)) n = entier;
            // Excel renvoie parfois « 3.0 » pour une cellule numérique.
            else if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                     && d == Math.Floor(d) && d is >= int.MinValue and <= int.MaxValue) n = (int)d;
            else return Task.FromResult<string?>("nombre entier attendu");
            if (n < min || n > max) return Task.FromResult<string?>($"entre {min} et {max}");
            ecrire(e, n);
            return Task.FromResult<string?>(null);
        });

    /// <summary>Nombre décimal (point ou virgule), avec un nombre maximal de décimales.</summary>
    public static ColonneExcel<T> Decimal(string entete, Func<T, decimal?> lire, Action<T, decimal?> ecrire,
                                          int decimales, bool obligatoire = false)
        => new(entete, obligatoire, e => lire(e)?.ToString(CultureInfo.InvariantCulture), (_, e, v) =>
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                if (obligatoire) return Task.FromResult<string?>("valeur obligatoire");
                ecrire(e, null);
                return Task.FromResult<string?>(null);
            }
            if (!decimal.TryParse(v.Trim().Replace(" ", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                return Task.FromResult<string?>("nombre attendu");
            if (decimal.Round(d, decimales) != d) return Task.FromResult<string?>($"{decimales} décimales au plus");
            ecrire(e, d);
            return Task.FromResult<string?>(null);
        });

    /// <summary>Compte utilisateur de l'organisme, saisi par son identifiant de connexion.</summary>
    public static ColonneExcel<T> Utilisateur(string entete, Func<T, Ulid?> lireId, Action<T, Ulid?> ecrire, bool obligatoire = false)
        => new(entete, obligatoire, _ => null, async (ctx, e, v) =>
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                if (obligatoire) return "valeur obligatoire";
                ecrire(e, null);
                return null;
            }
            var id = await ctx.ResoudreUtilisateurAsync(v.Trim());
            if (id is null) return $"compte « {v.Trim()} » introuvable ou inactif dans l'organisme";
            ecrire(e, id);
            return null;
        }, "identifiant de connexion d'un compte de l'organisme") { LireUtilisateur = lireId };

    /// <summary>Date au format jj/mm/aaaa (ou aaaa-mm-jj, ou date Excel).</summary>
    public static ColonneExcel<T> Date(string entete, Func<T, DateOnly?> lire, Action<T, DateOnly?> ecrire)
        => new(entete, false, e => lire(e)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), (_, e, v) =>
        {
            if (string.IsNullOrWhiteSpace(v)) { ecrire(e, null); return Task.FromResult<string?>(null); }
            var t = v.Trim();
            if (DateOnly.TryParseExact(t, ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var date)
                || DateTime.TryParse(t, CultureInfo.GetCultureInfo("fr-FR"), DateTimeStyles.None, out var dt) && (date = DateOnly.FromDateTime(dt)) != default)
            {
                ecrire(e, date);
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>("date attendue au format jj/mm/aaaa");
        }, "jj/mm/aaaa");

    /// <summary>Booléen saisi « Oui » / « Non » (ou 1 / 0, vrai / faux). Vide = valeur par défaut.</summary>
    public static ColonneExcel<T> OuiNon(string entete, Func<T, bool> lire, Action<T, bool> ecrire, bool parDefaut)
        => new(entete, false, e => lire(e) ? "Oui" : "Non", (_, e, v) =>
        {
            var t = v?.Trim().ToLowerInvariant();
            bool? valeur = t switch
            {
                null or "" => parDefaut,
                "oui" or "o" or "1" or "vrai" or "true" or "x" => true,
                "non" or "n" or "0" or "faux" or "false" => false,
                _ => null
            };
            if (valeur is null) return Task.FromResult<string?>("« Oui » ou « Non » attendu");
            ecrire(e, valeur.Value);
            return Task.FromResult<string?>(null);
        }, "Oui / Non");

    /// <summary>Valeur d'énumération, saisie par son libellé ou son numéro.</summary>
    public static ColonneExcel<T> Enumeration<TEnum>(string entete, Func<T, TEnum> lire, Action<T, TEnum> ecrire,
                                                     IReadOnlyDictionary<TEnum, string> libelles, bool obligatoire = true)
        where TEnum : struct, Enum
        => new(entete, obligatoire, e => libelles.GetValueOrDefault(lire(e)), (_, e, v) =>
        {
            if (string.IsNullOrWhiteSpace(v))
                return Task.FromResult<string?>(obligatoire ? "valeur obligatoire" : null);

            var t = v.Trim();
            var trouve = libelles.FirstOrDefault(l => string.Equals(l.Value, t, StringComparison.OrdinalIgnoreCase));
            if (trouve.Value is not null) { ecrire(e, trouve.Key); return Task.FromResult<string?>(null); }
            if (byte.TryParse(t, out var n) && Enum.IsDefined(typeof(TEnum), Enum.ToObject(typeof(TEnum), n)))
            {
                ecrire(e, (TEnum)Enum.ToObject(typeof(TEnum), n));
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>($"valeur attendue : {string.Join(", ", libelles.Values)}");
        }, string.Join(" / ", libelles.Values));

    /// <summary>
    /// Référence à un autre référentiel, saisie par sa clé métier (code). Le filtre
    /// restreint les candidats (ex. une catégorie de liste de valeurs).
    /// </summary>
    public static ColonneExcel<T> Reference<TRef>(string entete, Func<T, string?> lireCode, Action<T, Ulid?> ecrire,
                                                  Expression<Func<TRef, string>> code, bool obligatoire = false,
                                                  Expression<Func<TRef, bool>>? filtre = null, string? cleFiltre = null)
        where TRef : EntiteOrganisme
        => new(entete, obligatoire, lireCode, async (ctx, e, v) =>
        {
            if (string.IsNullOrWhiteSpace(v))
            {
                if (obligatoire) return "valeur obligatoire";
                ecrire(e, null);
                return null;
            }
            var id = await ctx.ResoudreAsync(code, v.Trim(), filtre, cleFiltre);
            if (id is null) return $"« {v.Trim()} » introuvable";
            ecrire(e, id);
            return null;
        }, "code de l'élément référencé");
}

/// <summary>Composition d'expressions traduisibles en SQL (paramètres, jamais de concaténation).</summary>
internal static class Expressions
{
    /// <summary>x => cle(x) == valeur [&amp;&amp; x.Id != exclu]</summary>
    public static Expression<Func<T, bool>> Egal<T>(Expression<Func<T, string>> cle, string valeur, Ulid? exclu)
        where T : EntiteOrganisme
    {
        var x = cle.Parameters[0];
        Expression corps = Expression.Equal(cle.Body, Parametre(valeur));
        if (exclu is { } id && id != Ulid.Empty)
            corps = Expression.AndAlso(corps,
                Expression.NotEqual(Expression.Property(x, nameof(BaseEntity.Id)), Parametre(id)));
        return Expression.Lambda<Func<T, bool>>(corps, x);
    }

    /// <summary>x => code(x).Contains(texte) || libelle(x).Contains(texte)</summary>
    public static Expression<Func<T, bool>> Contient<T>(Expression<Func<T, string>> code, Expression<Func<T, string>> libelle, string texte)
    {
        var x = code.Parameters[0];
        var libelleCorps = new Remplacement(libelle.Parameters[0], x).Visit(libelle.Body);
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var valeur = Parametre(texte);
        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(Expression.Call(code.Body, contains, valeur), Expression.Call(libelleCorps, contains, valeur)), x);
    }

    /// <summary>x => valeurs.Contains(cle(x))  (liste passée en paramètres SQL)</summary>
    public static Expression<Func<T, bool>> Dans<T>(Expression<Func<T, string>> cle, IReadOnlyCollection<string> valeurs)
    {
        var contains = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(string));
        return Expression.Lambda<Func<T, bool>>(
            Expression.Call(contains, Parametre<IEnumerable<string>>(valeurs.ToList()), cle.Body), cle.Parameters[0]);
    }

    /// <summary>
    /// Valeur capturée dans une fermeture : EF Core en fait un paramètre SQL, là où une
    /// constante serait incrustée dans le texte de la requête (et dans le cache des plans).
    /// </summary>
    public static Expression Parametre<TValeur>(TValeur valeur)
        => Expression.Property(Expression.Constant(new Boite<TValeur>(valeur)), nameof(Boite<TValeur>.Valeur));

    private sealed record Boite<TValeur>(TValeur Valeur);

    private sealed class Remplacement(ParameterExpression ancien, ParameterExpression nouveau) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == ancien ? nouveau : node;
    }
}
