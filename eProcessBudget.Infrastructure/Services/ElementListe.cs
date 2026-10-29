namespace eProcessBudget.Infrastructure.Services;

/// <summary>
/// Élément d'une liste de choix (sélecteur, autocomplétion) : un identifiant et ce qu'on
/// affiche. Les écrans ne chargent ainsi que deux colonnes, jamais l'entité complète.
/// </summary>
public sealed record ElementListe(Ulid Id, string Libelle);
