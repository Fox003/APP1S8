using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SONDAGEAPI.DTOs;
using SONDAGEAPI.Services;

namespace SONDAGEAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public class SurveyController(ISurveyService surveyService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SurveyResponseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrorResponseDto))]
    public async Task<IActionResult> GetSurvey(Guid id)
    {
        var survey = await surveyService.GetByIdAsync(id);

        if (survey is null)
            return NotFound($"Aucun sondage avec l'id {id}");

        return Ok(survey);
    }
    
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<SurveyResponseDto>))]
    public async Task<IActionResult> GetSurveys()
    {
        var surveys = await surveyService.GetAllAsync();
        return Ok(surveys);
    }
    
    [HttpGet("/submission/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SurveyResponseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErrorResponseDto))]
    public async Task<IActionResult> GetSurveySubmission(Guid id)
    {
        var survey = await surveyService.GetByIdAsync(id);

        if (survey is null)
            return NotFound($"Aucun sondage avec l'id {id}");

        return Ok(survey);
    }
    
    [HttpGet("{id:guid}/submission")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(SurveySubmissionDTO))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubmission(Guid id)
    {
        var submitterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(submitterId) || !int.TryParse(submitterId, out var submitterUserId))
            return Unauthorized();

        var submission = await surveyService.GetSubmissionForUserAsync(submitterUserId, id);
        if (submission is null)
            return NotFound();

        return Ok(submission);
    }
    
    [HttpPost("submissions")]
    public async Task<ActionResult<SurveySubmissionDTO>> SubmitSurvey([FromBody] CreateSurveySubmissionDTO request)
    {
        var submitterId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(submitterId))
        {
            return Unauthorized();
        }

        if (!int.TryParse(submitterId, out var submitterUserId))
            return Unauthorized();
        var result = await surveyService.SubmitSurveyAsync(submitterUserId, request);
        if (result == null)
        {
            return BadRequest("Survey does not exist or has already been submitted.");
        }

        return Ok(result);
    }
}