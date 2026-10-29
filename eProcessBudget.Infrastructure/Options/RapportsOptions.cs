namespace eProcessBudget.Infrastructure.Options;

/// <summary>Options des exports, section "Rapports" de appsettings.json (cahier technique § 2.10).</summary>
public class RapportsOptions
{
    public const string SectionName = "Rapports";

    /// <summary>
    /// Licence QuestPDF déclarée : Community (gratuite sous un seuil de chiffre d'affaires),
    /// Professional ou Enterprise. L'organisme exploitant vérifie son éligibilité.
    /// </summary>
    public string LicenceQuestPdf { get; set; } = "Community";
}
