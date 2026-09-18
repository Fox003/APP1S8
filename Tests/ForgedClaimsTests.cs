using System.Net;
using System.Net.Http.Json;
using SONDAGEAPI.Contracts;
using Tests.Infrastructure;

namespace Tests;

/// <summary>
/// Défense en profondeur. La politique d'autorisation n'exige que la *présence* de
/// la revendication <c>invitation_id</c> ; elle ne dit rien de son contenu. Les deux
/// endpoints de participation reconvertissent donc les revendications en GUID et
/// refusent si la conversion échoue.
/// <para>
/// Ces tests substituent le gestionnaire d'authentification par un gestionnaire qui
/// émet des revendications malformées — exactement ce que produirait un second
/// schéma ajouté plus tard (un JWT dont la charge utile n'a pas la même forme) ou un
/// gestionnaire compromis. Le scénario n'est pas atteignable par la porte d'entrée,
/// et c'est précisément pour cela qu'il mérite un test : sans lui, cette garde ne
/// serait jamais exercée.
/// </para>
/// </summary>
public class ForgedClaimsTests(ForgedClaimsApiFactory factory) : IClassFixture<ForgedClaimsApiFactory>
{
    private static readonly Guid AnySurveyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Une_revendication_dinvitation_non_convertible_est_refusee_a_la_soumission()
    {
        var response = await factory.CreateParticipantClient(ForgedClaimsApiFactory.InvitationIdNotAGuid)
            .PostAsJsonAsync($"/api/sondages/{AnySurveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Jeton de participation absent ou invalide.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Une_revendication_de_sondage_absente_est_refusee_a_la_soumission()
    {
        var response = await factory.CreateParticipantClient(ForgedClaimsApiFactory.SurveyIdMissing)
            .PostAsJsonAsync($"/api/sondages/{AnySurveyId}/reponses", new SubmitResponseRequest("Réponse"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Une_revendication_dinvitation_non_convertible_est_refusee_a_la_consultation()
    {
        var response = await factory.CreateParticipantClient(ForgedClaimsApiFactory.InvitationIdNotAGuid)
            .GetAsync($"/api/sondages/{AnySurveyId}/participation");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Une_revendication_de_sondage_absente_est_refusee_a_la_consultation()
    {
        var response = await factory.CreateParticipantClient(ForgedClaimsApiFactory.SurveyIdMissing)
            .GetAsync($"/api/sondages/{AnySurveyId}/participation");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// La validation de la charge utile passe avant l'examen des revendications :
    /// une réponse vide est refusée en 400, pas en 401.
    /// </summary>
    [Fact]
    public async Task La_validation_de_la_charge_utile_precede_lexamen_des_revendications()
    {
        var response = await factory.CreateParticipantClient(ForgedClaimsApiFactory.InvitationIdNotAGuid)
            .PostAsJsonAsync($"/api/sondages/{AnySurveyId}/reponses", new SubmitResponseRequest("  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
