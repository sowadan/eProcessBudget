using System.ComponentModel.DataAnnotations;

namespace eProcessBudget.Web.Features.Compte;

/// <summary>Formulaire de connexion.</summary>
public sealed class SaisieConnexion
{
    [Required(ErrorMessage = "Saisissez votre identifiant.")]
    [StringLength(150)]
    public string Identifiant { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saisissez votre mot de passe.")]
    [StringLength(128)]
    public string MotDePasse { get; set; } = string.Empty;
}

/// <summary>Formulaire « mot de passe oublié ».</summary>
public sealed class SaisieMotDePasseOublie
{
    [Required(ErrorMessage = "Saisissez votre identifiant ou votre adresse e-mail.")]
    [StringLength(150)]
    public string Identifiant { get; set; } = string.Empty;
}

/// <summary>Choix d'un nouveau mot de passe (réinitialisation ou changement).</summary>
public sealed class SaisieNouveauMotDePasse
{
    /// <summary>Utilisé seulement pour le changement de son propre mot de passe.</summary>
    [StringLength(128)]
    public string MotDePasseActuel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Saisissez le nouveau mot de passe.")]
    [StringLength(128)]
    public string NouveauMotDePasse { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirmez le nouveau mot de passe.")]
    [Compare(nameof(NouveauMotDePasse), ErrorMessage = "Les deux saisies ne correspondent pas.")]
    public string Confirmation { get; set; } = string.Empty;
}
