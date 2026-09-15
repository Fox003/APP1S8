using SONDAGEAPI.DTOs;
using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public interface ISurveyService
{
    Task<SurveyResponseDto?> GetByIdAsync(Guid id);
    Task<List<SurveySummaryDTO>> GetAllAsync();
}