using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Data;
using SONDAGEAPI.DTOs;
using SONDAGEAPI.Models;
using SONDAGEAPI.Models.Survey;

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

    public async Task<SurveySubmissionDTO?> GetSubmissionByIdAsync(Guid id)
    {
        return await db.SurveySubmissions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new SurveySubmissionDTO(
                s.Id,
                s.SurveyId,
                s.SubmitterId,
                s.Answers.Select(a => new AnswerDTO(
                    a.QuestionId, 
                    a.Value
                )).ToList()
            ))
            .FirstOrDefaultAsync();
    }

    public async Task<SurveySubmissionDTO?> SubmitSurveyAsync(int submitterId, CreateSurveySubmissionDTO dto)
    {
        var survey = await db.Surveys.FirstOrDefaultAsync(s => s.Id == dto.SurveyId);
        if (survey == null) return null;
        
        var alreadySubmitted = await db.SurveySubmissions
            .AnyAsync(s => s.SurveyId == dto.SurveyId && s.SubmitterId == submitterId);
    
        if (alreadySubmitted) return null;
        
        var submission = new SurveySubmission
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            SubmitterId = submitterId,
            Answers = dto.Answers.Select(a => new Answer
            {
                QuestionId = a.QuestionId,
                Value = a.Value
            }).ToList()
        };

        db.SurveySubmissions.Add(submission);
        await db.SaveChangesAsync();
    
        return new SurveySubmissionDTO(
            submission.Id,
            survey.Id,
            submission.SubmitterId,
            submission.Answers.Select(a => new AnswerDTO(a.QuestionId, a.Value)).ToList()
        );
    }
    
    public async Task<SurveySubmissionDTO?> GetSubmissionForUserAsync(int submitterId, Guid surveyId)
    {
        return await db.SurveySubmissions
            .AsNoTracking()
            .Where(s => s.SurveyId == surveyId && s.SubmitterId == submitterId)
            .Select(s => new SurveySubmissionDTO(
                s.Id,
                s.SurveyId,
                s.SubmitterId,
                s.Answers.Select(a => new AnswerDTO(a.QuestionId, a.Value)).ToList()
            ))
            .FirstOrDefaultAsync();
    }
}