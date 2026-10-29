using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using BC = BCrypt.Net.BCrypt;
using HashType = BCrypt.Net.HashType;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Hachage BCrypt des mots de passe (cahier technique § 2.1 et § 2.6), exposé sous
/// l'interface standard IPasswordHasher d'ASP.NET Core.
///
/// Variante « enhanced » : le mot de passe est d'abord condensé en SHA-384, ce qui lève la
/// limite de 72 octets de BCrypt (une phrase de passe longue est entièrement prise en compte).
///
/// BCrypt est volontairement lent (~250 ms au coût 12) : les appelants l'exécutent hors du
/// contexte de synchronisation du circuit (Task.Run), jamais pendant un rendu.
/// </summary>
public sealed class HacheurMotDePasseBCrypt : IPasswordHasher<Utilisateur>
{
    /// <summary>Coût minimal imposé par le cahier technique, quelle que soit la configuration.</summary>
    internal const int CoutMinimal = 12;

    private readonly int _cout;

    public HacheurMotDePasseBCrypt(IOptions<SecuriteOptions> options)
        : this(Math.Max(CoutMinimal, options.Value.CoutHachage)) { }

    /// <summary>Coût libre : réservé aux tests, pour ne pas les ralentir.</summary>
    internal HacheurMotDePasseBCrypt(int cout) => _cout = cout;

    public string HashPassword(Utilisateur user, string password)
        => BC.EnhancedHashPassword(password, _cout, HashType.SHA384);

    public PasswordVerificationResult VerifyHashedPassword(Utilisateur user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            return PasswordVerificationResult.Failed;

        bool correct;
        try
        {
            correct = BC.EnhancedVerify(providedPassword, hashedPassword, HashType.SHA384);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Empreinte en base illisible : le compte n'est pas connectable, sans faire
            // tomber la page de connexion.
            return PasswordVerificationResult.Failed;
        }

        if (!correct) return PasswordVerificationResult.Failed;

        // Coût relevé dans la configuration : l'empreinte est recalculée à cette connexion.
        return BC.PasswordNeedsRehash(hashedPassword, _cout)
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }
}

/// <summary>Politique de complexité des mots de passe (paramétrable, § 2.6).</summary>
public static class PolitiqueMotDePasse
{
    /// <summary>Message d'erreur à afficher, ou null si le mot de passe respecte la politique.</summary>
    public static string? Verifier(string? motDePasse, PolitiqueMotDePasseOptions p)
    {
        if (string.IsNullOrEmpty(motDePasse)) return "Le mot de passe est obligatoire.";
        if (motDePasse.Length > p.LongueurMax) return $"Le mot de passe ne doit pas dépasser {p.LongueurMax} caractères.";

        var manques = new List<string>();
        if (motDePasse.Length < p.LongueurMin) manques.Add($"{p.LongueurMin} caractères minimum");
        if (p.ExigerMajuscule && !motDePasse.Any(char.IsUpper)) manques.Add("une majuscule");
        if (p.ExigerMinuscule && !motDePasse.Any(char.IsLower)) manques.Add("une minuscule");
        if (p.ExigerChiffre && !motDePasse.Any(char.IsDigit)) manques.Add("un chiffre");
        if (p.ExigerCaractereSpecial && motDePasse.All(char.IsLetterOrDigit)) manques.Add("un caractère spécial");

        return manques.Count == 0 ? null : $"Le mot de passe doit contenir : {string.Join(", ", manques)}.";
    }

    /// <summary>Rappel de la politique, affiché sous les champs de saisie.</summary>
    public static string Description(PolitiqueMotDePasseOptions p)
    {
        var regles = new List<string> { $"{p.LongueurMin} caractères minimum" };
        if (p.ExigerMajuscule) regles.Add("une majuscule");
        if (p.ExigerMinuscule) regles.Add("une minuscule");
        if (p.ExigerChiffre) regles.Add("un chiffre");
        if (p.ExigerCaractereSpecial) regles.Add("un caractère spécial");
        return string.Join(", ", regles) + ".";
    }
}
