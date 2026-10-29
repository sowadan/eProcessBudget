namespace eProcessBudget.Infrastructure.Services.Audit;

/// <summary>
/// Adresse IP du client et identifiant de corrélation, pour le journal d'audit (cahier
/// fonctionnel § 3.13.2, technique § 3.3.10).
///
/// En Blazor Server, une fois le circuit ouvert, il n'y a plus de requête HTTP : l'adresse IP
/// est donc relevée à l'ouverture (App.razor la transmet à Routes, qui la dépose ici) et gardée
/// pour la durée du circuit. Hors circuit (points de terminaison HTTP), un intergiciel la dépose
/// à chaque requête. Scoped : une instance par circuit ou par requête.
/// </summary>
public interface IContexteConnexion
{
    string? AdresseIp { get; }

    /// <summary>Identifiant de la requête HTTP, ou du circuit.</summary>
    string CorrelationId { get; }

    /// <summary>Renseigne l'adresse et, le cas échéant, l'identifiant de requête (le premier relevé fait foi pour l'adresse).</summary>
    void Definir(string? adresseIp, string? correlationId = null);
}

public sealed class ContexteConnexion : IContexteConnexion
{
    public string? AdresseIp { get; private set; }
    public string CorrelationId { get; private set; } = Guid.NewGuid().ToString("N");

    public void Definir(string? adresseIp, string? correlationId = null)
    {
        if (AdresseIp is null && !string.IsNullOrWhiteSpace(adresseIp)) AdresseIp = adresseIp.Length > 45 ? adresseIp[..45] : adresseIp;
        if (!string.IsNullOrWhiteSpace(correlationId)) CorrelationId = correlationId.Length > 64 ? correlationId[..64] : correlationId;
    }
}
