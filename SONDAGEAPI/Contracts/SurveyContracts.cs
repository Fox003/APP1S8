namespace SONDAGEAPI.Contracts;

public record CreateSurveyRequest(string Title, DateTimeOffset? ClosesAt);

public record SurveyDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset? ClosesAt);

public record CreateInvitationRequest(int? ValidityDays);

// Token n'apparaît qu'ici, à la création. Impossible de le réafficher plus tard.
public record CreateInvitationResponse(Guid InvitationId, string Token, DateTimeOffset ExpiresAt);

public record SubmitResponseRequest(string Content);

public record ParticipationStatusDto(Guid SurveyId, bool HasParticipated);
