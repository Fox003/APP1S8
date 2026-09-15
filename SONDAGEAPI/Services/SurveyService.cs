using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Data;
using SONDAGEAPI.DTOs;
using SONDAGEAPI.Models;

namespace SONDAGEAPI.Services;

public class SurveyService(ApplicationDbContext db) : ISurveyService
{
    public async Task<SurveyResponseDto?> GetByIdAsync(Guid id)
    {
        return await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions)
            .Where(s => s.Id == id)
            .Select(s => new SurveyResponseDto(
                s.Id,
                s.Name,
                s.Description,
                DateTime.Now, 
                s.Questions
                    .OrderBy(q => q.Order)
                    .Select(q => new QuestionDto(q.Id, q.Text, q.Order, q.Options))
                    .ToList()
            ))
            .FirstOrDefaultAsync();
    }

    public async Task<List<SurveySummaryDTO>> GetAllAsync()
    {
        return await db.Surveys
            .AsNoTracking()
            .Include(s => s.Questions)
            .Select(s => new SurveySummaryDTO(
                s.Id,
                s.Name,
                s.Description,
                s.CreatedAt
            )).ToListAsync();
    }
}