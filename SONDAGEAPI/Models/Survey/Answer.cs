namespace SONDAGEAPI.Models.Survey;

public class Answer
{
    public int Id { get; set; }
    public Guid QuestionId { get; set; }
    public int Value {get; set;}
}