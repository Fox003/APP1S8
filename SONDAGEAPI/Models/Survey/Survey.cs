namespace SONDAGEAPI.Models;

public class Survey
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public List<Question> Questions { get; set; } = new();
}