using System.Net;
using System.Net.Http.Json;
using SONDAGEAPI.Contracts;

namespace Tests.Infrastructure;

/// <summary>
/// Raccourcis pour amener l'API dans un état donné. Les scénarios passent par les
/// endpoints publics plutôt que par la base : ce qui est testé ensuite s'appuie donc
/// sur un état que l'API sait réellement produire.
/// </summary>
public static class ApiScenario
{
    public static async Task<SurveyDto> CreateSurveyAsync(
        HttpClient client, string title = "Sondage de test", DateTimeOffset? closesAt = null)
    {
        var response = await client.PostAsJsonAsync("/api/sondages", new CreateSurveyRequest(title, closesAt));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<SurveyDto>())!;
    }

    public static async Task<CreateInvitationResponse> CreateInvitationAsync(
        HttpClient client, Guid surveyId, int validityDays = 30)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/sondages/{surveyId}/invitations", new CreateInvitationRequest(validityDays));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;
    }

    /// <summary>Crée un sondage et une invitation, et rend le jeton en clair.</summary>
    public static async Task<(Guid SurveyId, CreateInvitationResponse Invitation)> CreateInvitedParticipantAsync(
        HttpClient client, DateTimeOffset? closesAt = null, int validityDays = 30)
    {
        var survey = await CreateSurveyAsync(client, closesAt: closesAt);
        var invitation = await CreateInvitationAsync(client, survey.Id, validityDays);

        return (survey.Id, invitation);
    }
}
