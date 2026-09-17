namespace SONDAGEAPI.Models.Survey;

public class SurveySubmission
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public int SubmitterId { get; set; }
    public List<Answer> Answers { get; set; } = new();
}