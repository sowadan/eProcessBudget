using eProcessBudget.Core.Enums;
using MudBlazor;

namespace eProcessBudget.Web.Components.UI;

/// <summary>
/// Traduction visuelle d'un <see cref="TypeNotification"/> : libellé, icône, couleurs.
///
/// Quand vous ajoutez un type de notification, ajoutez-le dans les trois méthodes
/// ci-dessous. Le cas par défaut évite un écran cassé si vous l'oubliez, mais la
/// notification aura alors une apparence générique.
/// </summary>
public static class NotificationHelpers
{
    public static string Libelle(TypeNotification type) => type switch
    {
        TypeNotification.Information   => "Information",
        TypeNotification.Succes        => "Opération réussie",
        TypeNotification.Avertissement => "Avertissement",
        TypeNotification.Erreur        => "Erreur",
        TypeNotification.ActionRequise => "Action requise",
        TypeNotification.Rappel        => "Rappel",

        TypeNotification.SeuilConsommation    => "Seuil de consommation atteint",
        TypeNotification.DepassementDotation  => "Dépassement de dotation à valider",
        TypeNotification.AppuiEnAttente       => "Appui en attente de versement",
        TypeNotification.EcheanceRecouvrement => "Échéance de recouvrement dépassée",
        TypeNotification.HabilitationAValider => "Habilitation à valider",
        _ => type.ToString()
    };

    public static string Icone(TypeNotification type) => type switch
    {
        TypeNotification.Information   => Icons.Material.Outlined.Info,
        TypeNotification.Succes        => Icons.Material.Filled.CheckCircle,
        TypeNotification.Avertissement => Icons.Material.Outlined.WarningAmber,
        TypeNotification.Erreur        => Icons.Material.Filled.Cancel,
        TypeNotification.ActionRequise => Icons.Material.Outlined.HowToReg,
        TypeNotification.Rappel        => Icons.Material.Outlined.Alarm,

        TypeNotification.SeuilConsommation    => Icons.Material.Outlined.Speed,
        TypeNotification.DepassementDotation  => Icons.Material.Outlined.ReportProblem,
        TypeNotification.AppuiEnAttente       => Icons.Material.Outlined.HourglassTop,
        TypeNotification.EcheanceRecouvrement => Icons.Material.Outlined.EventBusy,
        TypeNotification.HabilitationAValider => Icons.Material.Outlined.AdminPanelSettings,
        _ => Icons.Material.Outlined.Notifications
    };

    /// <summary>Couple (fond, texte) en hexadécimal, pour la pastille d'icône.</summary>
    public static (string Fond, string Texte) Couleurs(TypeNotification type) => type switch
    {
        TypeNotification.Succes        => ("#DCFCE7", "#166534"),
        TypeNotification.Erreur        => ("#FEE2E2", "#991B1B"),
        TypeNotification.Avertissement => ("#FEF3C7", "#92400E"),
        TypeNotification.ActionRequise => ("#E0F2FE", "#075985"),
        TypeNotification.Rappel        => ("#EDE9FE", "#5B21B6"),

        TypeNotification.SeuilConsommation    => ("#FEF3C7", "#92400E"),
        TypeNotification.DepassementDotation  => ("#FEE2E2", "#991B1B"),
        TypeNotification.AppuiEnAttente       => ("#E0F2FE", "#075985"),
        TypeNotification.EcheanceRecouvrement => ("#FEF3C7", "#92400E"),
        TypeNotification.HabilitationAValider => ("#E0F2FE", "#075985"),
        _                              => ("#E5E7EB", "#374151")
    };

    /// <summary>Gravité MudBlazor correspondante, pour les messages toast.</summary>
    public static Severity Gravite(TypeNotification type) => type switch
    {
        TypeNotification.Succes        => Severity.Success,
        TypeNotification.Erreur        => Severity.Error,
        TypeNotification.Avertissement => Severity.Warning,

        TypeNotification.SeuilConsommation    => Severity.Warning,
        TypeNotification.DepassementDotation  => Severity.Error,
        TypeNotification.EcheanceRecouvrement => Severity.Warning,
        _                              => Severity.Info
    };

    /// <summary>
    /// Ancienneté en langage courant. Les dates sont stockées en UTC : la conversion
    /// vers l'heure locale se fait ici, au moment de l'affichage.
    /// </summary>
    public static string TempsRelatif(DateTime utc)
    {
        var ecart = DateTime.UtcNow - utc;

        if (ecart.TotalSeconds < 60) return "à l'instant";
        if (ecart.TotalMinutes < 60) return $"il y a {(int)ecart.TotalMinutes} min";
        if (ecart.TotalHours < 24)   return $"il y a {(int)ecart.TotalHours} h";
        if (ecart.TotalDays < 2)     return "hier";
        if (ecart.TotalDays < 7)     return $"il y a {(int)ecart.TotalDays} j";

        return utc.ToLocalTime().ToString("dd/MM/yyyy");
    }

    /// <summary>Regroupement par ancienneté, pour les séparateurs de la page /notifications.</summary>
    public static string GroupeDate(DateTime utc)
    {
        var jour = utc.ToLocalTime().Date;
        var ecart = (DateTime.Now.Date - jour).TotalDays;

        if (ecart <= 0) return "Aujourd'hui";
        if (ecart == 1) return "Hier";
        if (ecart < 7)  return "Cette semaine";
        if (ecart < 30) return "Ce mois-ci";

        return "Plus ancien";
    }
}


