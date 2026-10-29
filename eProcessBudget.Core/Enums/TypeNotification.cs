namespace eProcessBudget.Core.Enums;

/// <summary>
/// Catégories de notification. Chaque valeur détermine l'icône, la couleur et le libellé
/// affichés dans la cloche, ainsi que la ligne de paramétrage des canaux
/// (interne / email) en base.
///
/// Les valeurs sont PERSISTÉES : ne les renumérotez pas. Ajoutez les vôtres à partir de 100.
/// Après ajout : mettez à jour NotificationHelpers (libellé, icône, couleurs) côté Web.
/// </summary>
public enum TypeNotification
{
    /// <summary>Information neutre, sans action attendue.</summary>
    Information = 0,

    /// <summary>Opération réussie (création validée, traitement terminé).</summary>
    Succes = 1,

    /// <summary>Point d'attention qui n'empêche pas de continuer.</summary>
    Avertissement = 2,

    /// <summary>Échec ou refus : l'utilisateur doit agir.</summary>
    Erreur = 3,

    /// <summary>Une action est attendue de la part du destinataire (validation, saisie).</summary>
    ActionRequise = 4,

    /// <summary>Rappel automatique émis par un traitement planifié (échéance proche).</summary>
    Rappel = 5,

    // ── Alertes budgétaires (100+) - cahier technique § 3.6 (TypeAlerte) ─────
    /// <summary>La consommation d'une dotation atteint un seuil paramétré (80/90/100 %).</summary>
    SeuilConsommation = 100,
    /// <summary>Un engagement dépasse la dotation : validation du Responsable Budget attendue.</summary>
    DepassementDotation = 101,
    /// <summary>Engagement d'appui validé et non entièrement versé.</summary>
    AppuiEnAttente = 102,
    /// <summary>Échéance de recouvrement dépassée.</summary>
    EcheanceRecouvrement = 103,
    /// <summary>Habilitation en attente de validation.</summary>
    HabilitationAValider = 104,
}


