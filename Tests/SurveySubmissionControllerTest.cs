using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SONDAGEAPI.Controllers;
using SONDAGEAPI.DTOs;
using SONDAGEAPI.Services;

namespace Tests;

/// <summary>
/// Endpoints de soumission de SurveyController : SubmitSurvey et GetMySubmission.
/// Ces deux actions lisent l'identifiant du repondant dans la revendication
/// NameIdentifier posee par le schema ApiKey, donc chaque test fabrique son
/// ClaimsPrincipal.
/// </summary>
public class SurveySubmissionControllerTest
{
    private Mock<ISurveyService> mockSurveyService;

    public SurveySubmissionControllerTest()
    {
        mockSurveyService = new Mock<ISurveyService>();
    }

    /// <summary>
    /// Construit le controleur avec un utilisateur authentifie. Passer null pour
    /// simuler une absence de revendication NameIdentifier.
    /// </summary>
    private SurveyController CreateController(string? nameIdentifier)
    {
        var claims = nameIdentifier is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, nameIdentifier) };

        var identity = new ClaimsIdentity(claims, "ApiKey");

        return new SurveyController(mockSurveyService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnUnauthorized_WhenNameIdentifierIsMissing()
    {
        // 1. Arrange
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());
        var surveyController = CreateController(null);

        // 2. Act
        var result = await surveyController.SubmitSurvey(request);

        // 3. Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        mockSurveyService.Verify(
            service => service.SubmitSurveyAsync(It.IsAny<int>(), It.IsAny<CreateSurveySubmissionDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnUnauthorized_WhenNameIdentifierIsNotNumeric()
    {
        // 1. Arrange
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());
        var surveyController = CreateController("pas-un-entier");

        // 2. Act
        var result = await surveyController.SubmitSurvey(request);

        // 3. Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
        mockSurveyService.Verify(
            service => service.SubmitSurveyAsync(It.IsAny<int>(), It.IsAny<CreateSurveySubmissionDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnBadRequest_WhenSurveyIsUnknownOrAlreadySubmitted()
    {
        // 1. Arrange
        int submitterId = 12;
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());

        mockSurveyService
            .Setup(service => service.SubmitSurveyAsync(submitterId, request))
            .ReturnsAsync((SurveySubmissionDTO?)null);

        var surveyController = CreateController(submitterId.ToString());

        // 2. Act
        var result = await surveyController.SubmitSurvey(request);

        // 3. Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);

        Assert.Equal(400, badRequestResult.StatusCode);
        Assert.Equal("Survey does not exist or has already been submitted.", badRequestResult.Value);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldReturnSubmission_WhenSubmissionSucceeds()
    {
        // 1. Arrange
        int submitterId = 12;
        var surveyId = Guid.NewGuid();
        var questionId = Guid.NewGuid();

        var request = new CreateSurveySubmissionDTO(
            surveyId,
            new List<CreateAnswerDTO> { new(questionId, 4) });

        var expectedSubmission = new SurveySubmissionDTO(
            Guid.NewGuid(),
            surveyId,
            submitterId,
            new List<AnswerDTO> { new(questionId, 4) });

        mockSurveyService
            .Setup(service => service.SubmitSurveyAsync(submitterId, request))
            .ReturnsAsync(expectedSubmission);

        var surveyController = CreateController(submitterId.ToString());

        // 2. Act
        var result = await surveyController.SubmitSurvey(request);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var submission = Assert.IsType<SurveySubmissionDTO>(okResult.Value);

        Assert.Equal(expectedSubmission.Id, submission.Id);
        Assert.Equal(submitterId, submission.SubmitterId);
        Assert.Single(submission.Answers);
    }

    [Fact]
    public async Task SubmitSurvey_ShouldUseTheIdentityFromTheClaim_NotTheOneFromTheBody()
    {
        // 1. Arrange
        int submitterId = 99;
        var request = new CreateSurveySubmissionDTO(Guid.NewGuid(), new List<CreateAnswerDTO>());

        mockSurveyService
            .Setup(service => service.SubmitSurveyAsync(It.IsAny<int>(), request))
            .ReturnsAsync((SurveySubmissionDTO?)null);

        var surveyController = CreateController(submitterId.ToString());

        // 2. Act
        await surveyController.SubmitSurvey(request);

        // 3. Assert
        mockSurveyService.Verify(service => service.SubmitSurveyAsync(submitterId, request), Times.Once);
    }

    [Fact]
    public async Task GetMySubmission_ShouldReturnUnauthorized_WhenNameIdentifierIsMissing()
    {
        // 1. Arrange
        var surveyController = CreateController(null);

        // 2. Act
        var result = await surveyController.GetMySubmission(Guid.NewGuid());

        // 3. Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetMySubmission_ShouldReturnNotFound_WhenTheUserHasNotSubmitted()
    {
        // 1. Arrange
        int submitterId = 12;
        var surveyId = Guid.NewGuid();

        mockSurveyService
            .Setup(service => service.GetSubmissionForUserAsync(submitterId, surveyId))
            .ReturnsAsync((SurveySubmissionDTO?)null);

        var surveyController = CreateController(submitterId.ToString());

        // 2. Act
        var result = await surveyController.GetMySubmission(surveyId);

        // 3. Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetMySubmission_ShouldReturnSubmission_WhenTheUserHasSubmitted()
    {
        // 1. Arrange
        int submitterId = 12;
        var surveyId = Guid.NewGuid();

        var expectedSubmission = new SurveySubmissionDTO(
            Guid.NewGuid(),
            surveyId,
            submitterId,
            new List<AnswerDTO> { new(Guid.NewGuid(), 3) });

        mockSurveyService
            .Setup(service => service.GetSubmissionForUserAsync(submitterId, surveyId))
            .ReturnsAsync(expectedSubmission);

        var surveyController = CreateController(submitterId.ToString());

        // 2. Act
        var result = await surveyController.GetMySubmission(surveyId);

        // 3. Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var submission = Assert.IsType<SurveySubmissionDTO>(okResult.Value);

        Assert.Equal(expectedSubmission.Id, submission.Id);
        Assert.Equal(surveyId, submission.SurveyId);
    }
}
