using System.Threading.Channels;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>
/// Email prêt à partir. Le transport reste toujours le compte applicatif ; pour une
/// notification déclenchée par un utilisateur, seuls le nom affiché et le Reply-To
/// pointent vers lui - on n'usurpe jamais son adresse au niveau du transport, ce que
/// les relais modernes rejetteraient (SPF / DMARC).
/// </summary>
public sealed class EmailMessage
{
    public required IReadOnlyList<string> Destinataires { get; init; }
    public required string Sujet { get; init; }
    public required string CorpsHtml { get; init; }

    /// <summary>Nom de l'acteur à l'origine de l'action (null = envoi applicatif).</summary>
    public string? NomExpediteurAffiche { get; init; }
    /// <summary>Adresse de réponse pointant vers l'acteur (null = aucune).</summary>
    public string? ReplyTo { get; init; }
}

/// <summary>
/// File d'attente en mémoire des emails à envoyer.
///
/// Découple la création d'une notification (chemin chaud d'une requête utilisateur) de
/// l'envoi SMTP (latence réseau, réessais) : un relais lent ou indisponible ne bloque
/// jamais et ne fait jamais échouer l'action métier.
///
/// LIMITE ASSUMÉE : file locale au processus et non persistante. Un email en attente lors
/// d'un arrêt brutal est perdu (la notification interne, elle, reste stockée). Pour une
/// garantie stricte, remplacez cette implémentation par une file durable (table SQL, broker).
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
    IAsyncEnumerable<EmailMessage> LireAsync(CancellationToken ct);
}

public class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _canal =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(2000)
        {
            // Sous saturation, on préfère perdre les plus anciens plutôt que bloquer l'appelant.
            FullMode     = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(EmailMessage message) => _canal.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> LireAsync(CancellationToken ct)
        => _canal.Reader.ReadAllAsync(ct);
}


