using SONDAGEAPI.Models.Survey;

namespace SONDAGEAPI.DTOs;

public record SurveySubmissionDTO(
    Guid Id,
    Guid SurveyId,
    int SubmitterId,
    List<AnswerDTO> Answers
);