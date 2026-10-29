using System.Net;
using System.Net.Mail;
using eProcessBudget.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eProcessBudget.Infrastructure.Services.Notifications;

public interface IEmailSender
{
    /// <summary>Envoie un email via le relais SMTP. Lève une exception en cas d'échec.</summary>
    Task EnvoyerAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// Envoi SMTP basé sur System.Net.Mail. Suffisant pour un relais interne d'entreprise.
/// Pour du SMTP moderne authentifié (OAuth2, STARTTLS strict), remplacez cette classe
/// par une implémentation MailKit - l'interface ne change pas.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _opt;
    private readonly ApplicationOptions _app;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(
        IOptions<EmailOptions> opt,
        IOptions<ApplicationOptions> app,
        ILogger<SmtpEmailSender> log)
    {
        _opt = opt.Value;
        _app = app.Value;
        _log = log;
    }

    public async Task EnvoyerAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!_opt.Active || string.IsNullOrWhiteSpace(_opt.Host))
        {
            _log.LogDebug("Envoi email ignoré : canal inactif ou hôte SMTP non configuré.");
            return;
        }

        var destinataires = message.Destinataires
            .Where(d => !string.IsNullOrWhiteSpace(d) && EstAdresseValide(d))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (destinataires.Count == 0)
        {
            _log.LogDebug("Envoi email ignoré : aucun destinataire valide.");
            return;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(
                _opt.ExpediteurApplicatif,
                string.IsNullOrWhiteSpace(message.NomExpediteurAffiche)
                    ? _opt.NomExpediteurApplicatif
                    : $"{message.NomExpediteurAffiche} (via {_app.NomCourt})"),
            Subject         = message.Sujet,
            Body            = message.CorpsHtml,
            IsBodyHtml      = true,
            BodyEncoding    = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };

        // Envoi en copie cachée : les destinataires ne voient pas les adresses des autres.
        foreach (var d in destinataires) mail.Bcc.Add(d);
        // Un To technique, exigé par certains relais.
        mail.To.Add(new MailAddress(_opt.ExpediteurApplicatif, _opt.NomExpediteurApplicatif));

        if (!string.IsNullOrWhiteSpace(message.ReplyTo) && EstAdresseValide(message.ReplyTo))
            mail.ReplyToList.Add(new MailAddress(message.ReplyTo, message.NomExpediteurAffiche ?? message.ReplyTo));

        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl      = _opt.UseSsl,
            Timeout        = _opt.TimeoutSecondes * 1000,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_opt.User))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_opt.User, _opt.Password);
        }

        // SendMailAsync n'accepte pas de CancellationToken : on branche l'annulation à la main.
        using var enregistrement = ct.Register(() => { try { client.SendAsyncCancel(); } catch { /* ignore */ } });
        await client.SendMailAsync(mail);
    }

    private static bool EstAdresseValide(string adresse)
    {
        try { _ = new MailAddress(adresse); return true; }
        catch { return false; }
    }
}

/// <summary>
/// Consommateur de la file d'emails : envoie chaque message avec un nombre borné de
/// réessais. Un échec définitif est journalisé, jamais propagé.
/// </summary>
public class EmailQueueProcessor : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly EmailOptions _opt;
    private readonly ILogger<EmailQueueProcessor> _log;

    public EmailQueueProcessor(
        IEmailQueue queue, IEmailSender sender,
        IOptions<EmailOptions> opt, ILogger<EmailQueueProcessor> log)
    {
        _queue = queue;
        _sender = sender;
        _opt = opt.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.LireAsync(stoppingToken))
            await EnvoyerAvecReessaiAsync(message, stoppingToken);
    }

    private async Task EnvoyerAvecReessaiAsync(EmailMessage message, CancellationToken ct)
    {
        var maxTentatives = Math.Max(1, _opt.NombreMaxTentatives);

        for (var tentative = 1; tentative <= maxTentatives && !ct.IsCancellationRequested; tentative++)
        {
            try
            {
                await _sender.EnvoyerAsync(message, ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Échec d'envoi email (tentative {Tentative}/{Max}) - sujet : {Sujet}",
                    tentative, maxTentatives, message.Sujet);

                if (tentative >= maxTentatives)
                {
                    _log.LogError(ex, "Abandon de l'envoi email après {Max} tentatives - sujet : {Sujet}",
                        maxTentatives, message.Sujet);
                    return;
                }

                try { await Task.Delay(TimeSpan.FromSeconds(_opt.DelaiReessaiSecondes), ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}


