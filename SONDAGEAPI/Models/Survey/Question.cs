namespace SONDAGEAPI.Models.Survey;

public class Question
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; }
    public List<string> Options { get; set; } = new();
}