namespace SONDAGEAPI.Security.Participants;

public static class ParticipantTokenDefaults
{
    public const string AuthenticationScheme = "ParticipantToken";

    // En-tête, jamais la query string : une URL se retrouve dans les journaux
    // du serveur, l'historique du navigateur et l'en-tête Referer.
    public const string HeaderName = "X-Participant-Token";

    public const string PolicyName = "ParticipantOnly";
}
