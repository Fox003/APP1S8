namespace SONDAGEAPI.DTOs;

public record SurveyResponseDto(
    Guid Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    List<QuestionDto> Questions
);