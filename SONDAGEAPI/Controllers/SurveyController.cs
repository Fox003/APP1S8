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
}