namespace SONDAGEAPI.Models;

// Aucune clé étrangère vers Invitation ni vers un participant : c'est volontaire.
// Le lien participant <-> réponse ne doit pas exister en base, sinon les réponses
// sont désanonymisables par quiconque obtient la base.
public class SurveyResponse
{
    public const int ContentMaxLength = 4000;

    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string Content { get; set; } = string.Empty;

    public Survey Survey { get; set; } = null!;
}
