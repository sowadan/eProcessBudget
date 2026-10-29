using eProcessBudget.Core.Entities;
using Microsoft.AspNetCore.Components;

namespace eProcessBudget.Web.Features.Parametrage.Commun;

/// <summary>
/// Base des formulaires de référentiel (Features/Parametrage/Formulaires) : l'élément saisi
/// est reçu en paramètre et modifié directement ; DialogReferentiel l'enregistre.
/// </summary>
public abstract class FormulaireBase<T> : ComponentBase where T : EntiteOrganisme
{
    [Parameter, EditorRequired] public T Element { get; set; } = default!;

    /// <summary>true à la création : la clé métier peut alors être choisie librement.</summary>
    protected bool EstCreation => Element.Id == Ulid.Empty;
}
