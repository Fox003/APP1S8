using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace SONDAGEAPI.Security.Participants;

internal static class InvitationToken
{
    // 256 bits tirés du CSPRNG : hors de portée d'une attaque par force brute.
    private const int TokenBytes = 32;

    public const int HashBytes = 32;   // taille d'un SHA-256

    // Pourquoi SHA-256 et non bcrypt/Argon2 : un KDF lent sert à décourager la
    // *devinette* de secrets à faible entropie (mots de passe). Un jeton de 256 bits
    // aléatoires n'est pas devinable, donc le coût d'un KDF serait payé à chaque
    // requête sans rien apporter.
    public static byte[] ComputeHash(string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    // Retourne le jeton en clair (à remettre au participant, une seule fois)
    // et son empreinte (seule chose persistée).
    public static (string Token, byte[] Hash) Create()
    {
        var raw = RandomNumberGenerator.GetBytes(TokenBytes);
        var token = WebEncoders.Base64UrlEncode(raw);   // URL-safe : passe dans un lien courriel
        return (token, ComputeHash(token));
    }
}
