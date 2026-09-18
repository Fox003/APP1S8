using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SONDAGEAPI.Controllers;
using SONDAGEAPI.DTOs;
using SONDAGEAPI.Services;

namespace Tests;

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
    
    // Helper to build a controller with a fake authenticated user
    private static SurveyController CreateControllerWithUser(
        Mock<ISurveyService> serviceMock, string? nameIdentifierValue)
    {
        var controller = new SurveyController(serviceMock.Object);

        var claims = new List<Claim>();
        if (nameIdentifierValue is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifierValue));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    // ---------- GetMySubmission ----------

    [Fact]
    public async Task GetMySubmission_ShouldReturnUnauthorized_WhenNoNameIdentifierClaim()
    {
        // Arrange
        var surveyController = CreateControllerWithUser(mockSurveyService, nameIdentifierValue: null);
        var surveyId = Guid.NewGuid();

        // Act
        var result = await surveyController.GetMySubmission(surveyId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMySubmission_ShouldReturnUnauthorized_WhenNameIdentifierIsNotAnInt()
    {
        // Arrange
        var surveyController = CreateControllerWithUser(mockSurveyService, nameIdentifierValue: "not-an-int");
        var surveyId = Guid.NewGuid();

        // Act
        var result = await surveyController.GetMySubmission(surveyId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMySubmission_ShouldReturnNotFound_WhenSubmissionDoesNotExist()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var userId = 42;

        mockSurveyService
            .Setup(service => service.GetSubmissionForUserAsync(userId, surveyId))
            .ReturnsAsync((SurveySubmissionDTO?)null);

        var surveyController = CreateControllerWithUser(mockSurveyService, userId.ToString());

        // Act
        var result = await surveyController.GetMySubmission(surveyId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetMySubmission_ShouldReturnSubmission_WhenSubmissionExists()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var userId = 42;
        
        var expectedSubmission = new SurveySubmissionDTO(
            Guid.NewGuid(), surveyId, userId, new List<AnswerDTO>());

        mockSurveyService
            .Setup(service => service.GetSubmissionForUserAsync(userId, surveyId))
            .ReturnsAsync(expectedSubmission);

        var surveyController = CreateControllerWithUser(mockSurveyService, userId.ToString());

        // Act
        var result = await surveyController.GetMySubmission(surveyId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedSubmission, okResult.Value);
    }

    // ---------- SubmitSurvey ----------

    [Fact]
    public async Task SubmitSurvey_ShouldReturnUnauthorized_WhenNoNameIdentifierClaim()
    {
        // Arrange
        var surveyController = CreateControllerWithUser(mockSurveyService, nameIdentifierValue: null);

        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());

        // Act
        var result = await surveyController.SubmitSurvey(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnUnauthorized_WhenNameIdentifierIsNotAnInt()
    {
        // Arrange
        var surveyController = CreateControllerWithUser(mockSurveyService, nameIdentifierValue: "not-an-int");
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());

        // Act
        var result = await surveyController.SubmitSurvey(request);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnBadRequest_WhenServiceReturnsNull()
    {
        // Arrange
        var userId = 42;
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());

        mockSurveyService
            .Setup(service => service.SubmitSurveyAsync(userId, request))
            .ReturnsAsync((SurveySubmissionDTO?)null);

        var surveyController = CreateControllerWithUser(mockSurveyService, userId.ToString());

        // Act
        var result = await surveyController.SubmitSurvey(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Survey does not exist or has already been submitted.", badRequestResult.Value);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnOk_WhenSubmissionSucceeds()
    {
        // Arrange
        var userId = 42;
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());

        // TODO: adjust constructor args to match your real SurveySubmissionDTO
        var expectedResult = new SurveySubmissionDTO(
            Guid.NewGuid(), request.SurveyId, userId, new List<AnswerDTO>());

        mockSurveyService
            .Setup(service => service.SubmitSurveyAsync(userId, request))
            .ReturnsAsync(expectedResult);

        var surveyController = CreateControllerWithUser(mockSurveyService, userId.ToString());

        // Act
        var result = await surveyController.SubmitSurvey(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expectedResult, okResult.Value);
    }
    
    [Fact]
    public async Task GetSurveySubmission_ShouldReturnSurvey_WhenSurveyExists()
    {
        // Arrange
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

        // Act
        var result = await surveyController.GetSurveySubmission(surveyId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var survey = Assert.IsType<SurveyResponseDto>(okResult.Value);

        Assert.Equal(surveyId, survey.Id);
    }
}
