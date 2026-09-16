using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Contracts;
using SONDAGEAPI.Data;
using SONDAGEAPI.Models;
using SONDAGEAPI.Security.Participants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddSondageOpenApi();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddApiKeyAuthentication(builder.Configuration);
builder.Services.AddParticipantAuthentication();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSondageOpenApi();

// Couche 1 (livrable 1) : la clé d'API autorise un client à parler à l'API.
app.UseApiKeyAuthentication();

// Couche 2 (livrable 4) : le jeton identifie UN participant.
// Deux mécanismes distincts : les fusionner donnerait au participant
// la clé qui ouvre toute l'API.
app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// Administration des sondages — clé d'API seulement
// ---------------------------------------------------------------------------

app.MapPost("/api/sondages", async (
    CreateSurveyRequest request,
    ApplicationDbContext db,
    TimeProvider clock) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > Survey.TitleMaxLength)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = [$"Le titre est requis et ne doit pas dépasser {Survey.TitleMaxLength} caractères."]
        });
    }

    var now = clock.GetUtcNow();

    // Un sondage dont la fermeture est déjà passée naîtrait mort : toute soumission
    // serait refusée en 409 « Sondage clos ». Swagger préremplit closesAt avec un
    // horodatage généré au rendu de la page, déjà passé au moment du clic — piège
    // observé en test, d'où cette validation.
    if (request.ClosesAt is { } closesAt && closesAt <= now)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["closesAt"] = ["La date de fermeture doit être dans le futur, ou nulle pour un sondage sans échéance."]
        });
    }

    var survey = new Survey
    {
        Id = Guid.NewGuid(),
        Title = request.Title.Trim(),
        CreatedAt = now,
        ClosesAt = request.ClosesAt
    };

    db.Surveys.Add(survey);
    await db.SaveChangesAsync();

    return Results.Created($"/api/sondages/{survey.Id}",
        new SurveyDto(survey.Id, survey.Title, survey.CreatedAt, survey.ClosesAt));
})
.WithName("CreateSondage");

app.MapGet("/api/sondages/{id:guid}", async (Guid id, ApplicationDbContext db) =>
{
    var survey = await db.Surveys.AsNoTracking()
        .Where(s => s.Id == id)
        .Select(s => new SurveyDto(s.Id, s.Title, s.CreatedAt, s.ClosesAt))
        .SingleOrDefaultAsync();

    return survey is not null
        ? Results.Ok(survey)
        : Results.NotFound($"Aucun sondage avec l'id {id}");
})
.WithName("GetSondage");

// Émet une invitation : c'est ici, et seulement ici, que le jeton en clair existe.
app.MapPost("/api/sondages/{id:guid}/invitations", async (
    Guid id,
    CreateInvitationRequest request,
    ApplicationDbContext db,
    TimeProvider clock) =>
{
    var validityDays = request.ValidityDays;
    if (validityDays is < 1 or > 365)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["validityDays"] = ["La validité doit être comprise entre 1 et 365 jours."]
        });
    }

    if (!await db.Surveys.AnyAsync(s => s.Id == id))
    {
        return Results.NotFound($"Aucun sondage avec l'id {id}");
    }

    var now = clock.GetUtcNow();
    var (token, hash) = InvitationToken.Create();

    var invitation = new Invitation
    {
        Id = Guid.NewGuid(),
        SurveyId = id,
        TokenHash = hash,
        CreatedAt = now,
        ExpiresAt = now.AddDays(validityDays)
    };

    db.Invitations.Add(invitation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/sondages/{id}/invitations/{invitation.Id}",
        new CreateInvitationResponse(invitation.Id, token, invitation.ExpiresAt));
})
.WithName("CreateInvitation");

// ---------------------------------------------------------------------------
// Participation — clé d'API ET jeton de participation
// ---------------------------------------------------------------------------

app.MapPost("/api/sondages/{id:guid}/reponses", async (
    Guid id,
    SubmitResponseRequest request,
    ClaimsPrincipal user,
    ApplicationDbContext db,
    TimeProvider clock) =>
{
    if (string.IsNullOrWhiteSpace(request.Content) ||
        request.Content.Length > SurveyResponse.ContentMaxLength)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["content"] = [$"La réponse est requise et ne doit pas dépasser {SurveyResponse.ContentMaxLength} caractères."]
        });
    }

    if (!TryGetParticipant(user, out var invitationId, out var tokenSurveyId))
    {
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
            title: "Non autorisé", detail: "Jeton de participation absent ou invalide.");
    }

    // Un jeton n'ouvre que SON sondage : sans ce contrôle, une invitation
    // au sondage A permettrait de répondre au sondage B.
    if (tokenSurveyId != id)
    {
        return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "Interdit", detail: "Ce jeton ne concerne pas ce sondage.");
    }

    var now = clock.GetUtcNow();

    var survey = await db.Surveys.AsNoTracking()
        .Where(s => s.Id == id)
        .Select(s => new { s.ClosesAt })
        .SingleOrDefaultAsync();

    if (survey is null)
    {
        return Results.NotFound($"Aucun sondage avec l'id {id}");
    }

    if (survey.ClosesAt is { } closesAt && closesAt <= now)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Sondage clos", detail: "Ce sondage n'accepte plus de réponses.");
    }

    await using var transaction = await db.Database.BeginTransactionAsync();

    // LE point du livrable. Un « if (déjà répondu) » suivi d'un insert serait une
    // course TOCTOU : deux requêtes simultanées passeraient toutes deux le test.
    // Ici la condition est DANS le UPDATE, c'est donc la base qui arbitre, et le
    // nombre de lignes affectées dit qui a gagné.
    DateOnly? redeemedOn = DateOnly.FromDateTime(now.UtcDateTime);

    var rowsAffected = await db.Invitations
        .Where(i => i.Id == invitationId && i.RedeemedAt == null)
        .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.RedeemedAt, redeemedOn));

    if (rowsAffected == 0)
    {
        await transaction.RollbackAsync();
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "Participation déjà enregistrée",
            detail: "Ce jeton a déjà servi. Une seule réponse par participant.");
    }

    // Même transaction que la rédemption : sinon un plantage ici consommerait
    // la participation sans enregistrer la réponse.
    db.Responses.Add(new SurveyResponse
    {
        Id = Guid.NewGuid(),
        SurveyId = id,
        SubmittedAt = now,
        Content = request.Content
    });

    await db.SaveChangesAsync();
    await transaction.CommitAsync();

    return Results.Created($"/api/sondages/{id}/reponses", value: null);
})
.RequireAuthorization(ParticipantTokenDefaults.PolicyName)
.WithName("SubmitReponse");

// Démontre que s'authentifier ne consomme pas le jeton : on peut interroger
// son statut autant de fois qu'on veut sans brûler sa participation.
app.MapGet("/api/sondages/{id:guid}/participation", async (
    Guid id,
    ClaimsPrincipal user,
    ApplicationDbContext db) =>
{
    if (!TryGetParticipant(user, out var invitationId, out var tokenSurveyId) || tokenSurveyId != id)
    {
        return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
            title: "Interdit", detail: "Ce jeton ne concerne pas ce sondage.");
    }

    var hasParticipated = await db.Invitations.AsNoTracking()
        .Where(i => i.Id == invitationId)
        .Select(i => i.RedeemedAt != null)
        .SingleOrDefaultAsync();

    return Results.Ok(new ParticipationStatusDto(id, hasParticipated));
})
.RequireAuthorization(ParticipantTokenDefaults.PolicyName)
.WithName("GetParticipationStatus");

app.MapGet("/ping", () =>
{
    var ping = new PingResponse("pong", DateTime.UtcNow);
    return ping;
}).WithoutApiKey();

app.Run();

static bool TryGetParticipant(ClaimsPrincipal user, out Guid invitationId, out Guid surveyId)
{
    invitationId = Guid.Empty;
    surveyId = Guid.Empty;

    return Guid.TryParse(user.FindFirstValue(ParticipantClaims.InvitationId), out invitationId)
           && Guid.TryParse(user.FindFirstValue(ParticipantClaims.SurveyId), out surveyId);
}

record PingResponse(string Status, DateTime Timestamp);
