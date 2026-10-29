namespace eProcessBudget.Core.Enums;

/// <summary>
/// Calculs purs du budget-programme (cahier fonctionnel § 4.1). Sans accès aux données :
/// testables isolément, utilisables par les services comme par les écrans.
/// </summary>
public static class CalculsBudgetProgramme
{
    /// <summary>
    /// Taux d'atteinte de la cible, en % : réalisé / cible pour un indicateur à la hausse,
    /// cible / réalisé pour un indicateur à la baisse (ex. un délai). Null si non
    /// significatif (dénominateur nul) — affiché « n.s. ».
    /// Même formule que la vue prog.vw_AtteinteIndicateur.
    /// </summary>
    public static decimal? TauxAtteinte(decimal realise, decimal? cible, SensEvolution sens)
    {
        if (cible is null) return null;
        var (numerateur, denominateur) = sens == SensEvolution.BaisseAttendue ? (cible.Value, realise) : (realise, cible.Value);
        return denominateur == 0 ? null : Math.Round(100m * numerateur / denominateur, 2);
    }

    /// <summary>Cible atteinte : au moins la cible à la hausse, au plus la cible à la baisse.</summary>
    public static bool? EstAtteinte(decimal realise, decimal? cible, SensEvolution sens)
        => cible is null ? null
         : sens == SensEvolution.BaisseAttendue ? realise <= cible.Value : realise >= cible.Value;

    /// <summary>
    /// Ventilation d'un montant (charges de personnel, charges communes) au prorata du temps
    /// imparti à chaque destination (§ 3.3.6). Les parts sont arrondies au centime ; l'écart
    /// d'arrondi va à la plus grosse part, pour que la somme soit exactement le montant.
    /// </summary>
    public static IReadOnlyList<(TCle Cle, decimal Montant)> Ventiler<TCle>(decimal montant, IEnumerable<(TCle Cle, decimal Poids)> poids)
        where TCle : notnull
    {
        var groupes = poids.Where(p => p.Poids > 0)
                           .GroupBy(p => p.Cle)
                           .Select(g => (Cle: g.Key, Poids: g.Sum(p => p.Poids)))
                           .ToList();
        var total = groupes.Sum(g => g.Poids);
        if (total == 0 || montant == 0) return groupes.Select(g => (g.Cle, 0m)).ToList();

        var parts = groupes.Select(g => (g.Cle, Montant: Math.Round(montant * g.Poids / total, 2, MidpointRounding.AwayFromZero))).ToList();
        var ecart = montant - parts.Sum(p => p.Montant);
        if (ecart != 0)
        {
            var i = parts.FindIndex(p => p.Montant == parts.Max(x => x.Montant));
            parts[i] = (parts[i].Cle, parts[i].Montant + ecart);
        }
        return parts;
    }

    /// <summary>Total triennal : années N, N+1 et N+2 (§ 4.1).</summary>
    public static decimal TotalTriennal(int anneeBase, IEnumerable<(int Annee, decimal Montant)> montants)
        => montants.Where(m => m.Annee >= anneeBase && m.Annee <= anneeBase + 2).Sum(m => m.Montant);
}

/// <summary>Libellés des énumérations du budget-programme (§ 3.6).</summary>
public static class LibellesBudgetProgramme
{
    public static readonly IReadOnlyDictionary<NiveauIndicateur, string> Niveau = new Dictionary<NiveauIndicateur, string>
    {
        [NiveauIndicateur.Programme] = "Programme",
        [NiveauIndicateur.Action] = "Action",
        [NiveauIndicateur.Extrant] = "Résultat immédiat (extrant)",
    };

    public static readonly IReadOnlyDictionary<SensEvolution, string> Sens = new Dictionary<SensEvolution, string>
    {
        [SensEvolution.HausseAttendue] = "Hausse attendue",
        [SensEvolution.BaisseAttendue] = "Baisse attendue",
    };

    public static readonly IReadOnlyDictionary<TypeElementCL, string> ElementCadre = new Dictionary<TypeElementCL, string>
    {
        [TypeElementCL.ObjectifGeneral] = "Objectif général",
        [TypeElementCL.ObjectifSpecifique] = "Objectif spécifique",
        [TypeElementCL.ResultatImmediat] = "Résultat immédiat",
    };

    public static readonly IReadOnlyDictionary<TypeAnalyse, string> Analyse = new Dictionary<TypeAnalyse, string>
    {
        [TypeAnalyse.Contrainte] = "Contrainte relevée",
        [TypeAnalyse.PrioriteActionStrategique] = "Priorité / action stratégique",
        [TypeAnalyse.DefiMajeur] = "Défi majeur",
        [TypeAnalyse.Recommandation] = "Recommandation",
        [TypeAnalyse.CommentaireRubrique] = "Commentaire de rubrique",
    };

    public static readonly IReadOnlyDictionary<StatutSaisie, string> StatutSaisie = new Dictionary<StatutSaisie, string>
    {
        [Enums.StatutSaisie.Brouillon] = "Brouillon",
        [Enums.StatutSaisie.Soumis] = "Soumis",
        [Enums.StatutSaisie.Valide] = "Validé",
        [Enums.StatutSaisie.Rejete] = "Rejeté",
        [Enums.StatutSaisie.Annule] = "Annulé",
    };
}
