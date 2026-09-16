using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SONDAGEAPI.Security.Participants;

namespace Microsoft.Extensions.DependencyInjection;

public static class ParticipantAuthExtensions
{
    public static IServiceCollection AddParticipantAuthentication(this IServiceCollection services)
    {
        // Enregistré explicitement pour que les tests du livrable 5 puissent
        // simuler l'expiration sans attendre.
        services.TryAddSingleton(TimeProvider.System);

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ParticipantTokenAuthenticationHandler>(
                ParticipantTokenDefaults.AuthenticationScheme,
                displayName: null,
                configureOptions: _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ParticipantTokenDefaults.PolicyName, policy =>
            {
                policy.AddAuthenticationSchemes(ParticipantTokenDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.RequireClaim(ParticipantClaims.InvitationId);
            });
        });

        return services;
    }
}
