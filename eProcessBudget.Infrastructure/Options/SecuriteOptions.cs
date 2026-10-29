namespace eProcessBudget.Infrastructure.Options;

/// <summary>
/// Authentification et sessions, section "Securite" de appsettings.json (cahier technique § 2.6).
///
/// Les valeurs propres à un organisme (durée de session, nombre de tentatives) sont lues
/// en priorité dans cfg.ParametreOrganisme ; celles d'ici servent de valeurs par défaut,
/// et de valeurs tout court pour les comptes de niveau plateforme.
/// </summary>
public class SecuriteOptions
{
    public const string SectionName = "Securite";

    /// <summary>Expiration glissante de la session, en minutes d'inactivité.</summary>
    public int DureeSessionMinutes { get; set; } = 30;

    /// <summary>Échecs consécutifs avant blocage du compte.</summary>
    public int NbTentativesMax { get; set; } = 5;

    /// <summary>Durée du blocage automatique. Un administrateur peut débloquer avant.</summary>
    public int DureeBlocageMinutes { get; set; } = 15;

    /// <summary>Facteur de coût BCrypt (2^n itérations). 12 minimum (§ 2.6).</summary>
    public int CoutHachage { get; set; } = 12;

    /// <summary>Validité d'un lien de réinitialisation du mot de passe.</summary>
    public int DureeLienReinitialisationMinutes { get; set; } = 60;

    /// <summary>
    /// Fréquence à laquelle une session ouverte (circuit Blazor) revérifie le compte :
    /// désactivé, bloqué, mot de passe changé ou organisme suspendu ferment la session.
    /// </summary>
    public int IntervalleRevalidationSecondes { get; set; } = 60;

    /// <summary>Requêtes POST de connexion / réinitialisation admises par minute et par adresse IP.</summary>
    public int LimiteTentativesParMinute { get; set; } = 10;

    public PolitiqueMotDePasseOptions MotDePasse { get; set; } = new();
}

/// <summary>Politique de complexité des mots de passe (paramétrable, § 2.6).</summary>
public class PolitiqueMotDePasseOptions
{
    public int LongueurMin { get; set; } = 10;

    /// <summary>Au-delà, BCrypt ignorerait la fin ; la longueur est bornée pour éviter tout abus.</summary>
    public int LongueurMax { get; set; } = 128;

    public bool ExigerMajuscule { get; set; } = true;
    public bool ExigerMinuscule { get; set; } = true;
    public bool ExigerChiffre { get; set; } = true;
    public bool ExigerCaractereSpecial { get; set; } = true;
}
