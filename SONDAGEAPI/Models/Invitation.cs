namespace SONDAGEAPI.Models;

// Double rôle : justificatif d'authentification du participant ET registre de participation.
// RedeemedAt == null => le jeton n'a pas encore servi. C'est cette colonne qui porte
// la garantie d'unicité, via un UPDATE conditionnel (voir l'endpoint de soumission).
public class Invitation
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }

    // SHA-256 du jeton (32 octets). Le jeton en clair n'est jamais stocké :
    // une fuite de app.db ne permet d'usurper personne.
    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    // DateOnly volontairement, pas DateTimeOffset : empêche de corréler une rédemption
    // avec un SurveyResponse.SubmittedAt et de désanonymiser le participant.
    public DateOnly? RedeemedAt { get; set; }

    public Survey Survey { get; set; } = null!;
}
