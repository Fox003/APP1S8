namespace SONDAGEAPI.DTOs;

public record AnswerDTO(
    Guid QuestionId,
    int Value
);