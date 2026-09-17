using Microsoft.AspNetCore.Mvc;
using Moq;
using SONDAGEAPI.Controllers;
using SONDAGEAPI.DTOs;
using SONDAGEAPI.Services;

namespace Tests;

/// <summary>
/// Endpoints de lecture de SurveyController : GetSurvey et GetSurveys.
/// Le service est bouchonne avec Moq, donc seul le controleur est teste.
/// </summary>
public class SurveyControllerTest
{
    private Mock<ISurveyService> mockSurveyService;

    public SurveyControllerTest()
    {
        mockSurveyService = new Mock<ISurveyService>();
    }

    [Fact]
    public async Task GetSurvey_ShouldReturnNotFound_WhenSurveyDoesNotExist()
    {
        // 1. Arrange
        var surveyId = Guid.NewGuid();

        mockSurveyService
            .Setup(service => service.GetByIdAsync(surveyId))
            .ReturnsAsync((SurveyResponseDto?)null);

        var surveyController = new SurveyController(mockSurveyService.Object);

        // 2. Act
        var result = await surveyController.GetSurvey(surveyId);

        // 3. Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        Assert.Equal(404, notFoundResult.StatusCode);
        Assert.Equal($"Aucun sondage avec l'id {surveyId}", notFoundResult.Value);
    }

    [Fact]
    public async Task GetSurvey_ShouldReturnSurvey_WhenSurveyExists()
    {
        // 1. Arrange
        var surveyId = Guid.NewGuid();

        var expectedSurvey = new SurveyResponseDto(
            surveyId,
            "Satisfaction 2026",
            "Sondage de satisfaction annuel",
            DateTime.UtcNow,
            new List<QuestionDto>
            {
                new(Guid.NewGuid(), "Recommanderiez-vous ce service ?", 1, new List<string> { "Oui", "Non" })
            });

        mockSurveyService
            .Setup(service => service.GetByIdAsync(surveyId))
            .ReturnsAsync(expectedSurvey);

        var surveyController = new SurveyController(mockSurveyService.Object);

        // 2. Act
        var result = await surveyController.GetSurvey(surveyId);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var survey = Assert.IsType<SurveyResponseDto>(okResult.Value);

        Assert.Equal(surveyId, survey.Id);
        Assert.Equal("Satisfaction 2026", survey.Name);
        Assert.Single(survey.Questions);
    }

    [Fact]
    public async Task GetSurveys_ShouldReturnEmptyList_WhenNoSurveyExists()
    {
        // 1. Arrange
        mockSurveyService
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(new List<SurveySummaryDTO>());

        var surveyController = new SurveyController(mockSurveyService.Object);

        // 2. Act
        var result = await surveyController.GetSurveys();

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var surveys = Assert.IsType<List<SurveySummaryDTO>>(okResult.Value);

        Assert.Empty(surveys);
    }

    [Fact]
    public async Task GetSurveys_ShouldReturnAllSurveys_WhenSurveysExist()
    {
        // 1. Arrange
        var expectedSurveys = new List<SurveySummaryDTO>
        {
            new(Guid.NewGuid(), "Sondage A", "Description A", DateTime.UtcNow),
            new(Guid.NewGuid(), "Sondage B", "Description B", DateTime.UtcNow)
        };

        mockSurveyService
            .Setup(service => service.GetAllAsync())
            .ReturnsAsync(expectedSurveys);

        var surveyController = new SurveyController(mockSurveyService.Object);

        // 2. Act
        var result = await surveyController.GetSurveys();

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var surveys = Assert.IsType<List<SurveySummaryDTO>>(okResult.Value);

        Assert.Equal(2, surveys.Count);
        Assert.Equal(expectedSurveys[0].Name, surveys[0].Name);
    }

    [Fact]
    public async Task GetSurveySubmission_ShouldReturnNotFound_WhenSurveyDoesNotExist()
    {
        // 1. Arrange
        var surveyId = Guid.NewGuid();

        mockSurveyService
            .Setup(service => service.GetByIdAsync(surveyId))
            .ReturnsAsync((SurveyResponseDto?)null);

        var surveyController = new SurveyController(mockSurveyService.Object);

        // 2. Act
        var result = await surveyController.GetSurveySubmission(surveyId);

        // 3. Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);

        Assert.Equal(404, notFoundResult.StatusCode);
    }
}
