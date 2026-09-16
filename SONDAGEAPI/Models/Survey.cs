namespace SONDAGEAPI.Models;

public class Survey
{
    public const int TitleMaxLength = 200;

    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // null => le sondage reste ouvert indéfiniment.
    public DateTimeOffset? ClosesAt { get; set; }

    public ICollection<Invitation> Invitations { get; set; } = [];
    public ICollection<SurveyResponse> Responses { get; set; } = [];
}
