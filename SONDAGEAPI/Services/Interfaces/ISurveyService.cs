using SONDAGEAPI.DTOs;
using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public interface ISurveyService
{
    Task<SurveyResponseDto?> GetByIdAsync(Guid id);
    Task<List<SurveySummaryDTO>> GetAllAsync();
    Task<SurveySubmissionDTO?> GetSubmissionByIdAsync(Guid id);
    Task<SurveySubmissionDTO?> SubmitSurveyAsync(int submitterId, CreateSurveySubmissionDTO dto);

    Task<SurveySubmissionDTO?> GetSubmissionForUserAsync(int submitterId, Guid surveyId);
}