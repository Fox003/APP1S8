namespace Tests.Infrastructure;

/// <summary>
/// Horloge pilotée par le test. Sans elle, vérifier l'expiration d'un jeton ou la
/// fermeture d'un sondage demanderait d'attendre réellement ; le test serait lent,
/// non déterministe, et la branche « expiré » ne serait jamais couverte.
/// </summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long _utcTicks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() =>
        new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan delta) =>
        Interlocked.Exchange(ref _utcTicks, GetUtcNow().Add(delta).UtcTicks);
}
