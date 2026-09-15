namespace SONDAGEAPI.DTOs;

public record QuestionDto(
    Guid Id,
    string Text,
    int Order,
    List<string> Options
);