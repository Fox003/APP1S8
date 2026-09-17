namespace SONDAGEAPI.DTOs;

public record CreateSurveySubmissionDTO(
    Guid SurveyId,
    List<CreateAnswerDTO> Answers
);

public record CreateAnswerDTO(
    Guid QuestionId,
    int Value
);