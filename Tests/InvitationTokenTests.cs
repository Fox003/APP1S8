using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using SONDAGEAPI.Security.Participants;

namespace Tests;

/// <summary>
/// Le jeton est le seul secret que détient un participant. Sa qualité conditionne
/// toute la garantie d'unicité : un jeton devinable rendrait inutile tout le reste.
/// </summary>
public class InvitationTokenTests
{
    [Fact]
    public void Un_jeton_genere_porte_256_bits_dalea_encodes_en_base64url()
    {
        var (token, _) = InvitationToken.Create();

        var raw = WebEncoders.Base64UrlDecode(token);

        Assert.Equal(32, raw.Length);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void Lempreinte_rendue_par_Create_correspond_au_jeton_rendu()
    {
        var (token, hash) = InvitationToken.Create();

        Assert.Equal(InvitationToken.ComputeHash(token), hash);
        Assert.Equal(InvitationToken.HashBytes, hash.Length);
    }

    [Fact]
    public void Lempreinte_est_bien_un_SHA256_du_jeton_en_UTF8()
    {
        var (token, hash) = InvitationToken.Create();

        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)), hash);
    }

    [Fact]
    public void Lempreinte_est_deterministe()
    {
        Assert.Equal(InvitationToken.ComputeHash("un-jeton"), InvitationToken.ComputeHash("un-jeton"));
    }

    [Fact]
    public void Deux_jetons_differents_donnent_deux_empreintes_differentes()
    {
        Assert.NotEqual(InvitationToken.ComputeHash("jeton-a"), InvitationToken.ComputeHash("jeton-b"));
    }

    /// <summary>
    /// 1000 tirages sans collision ne prouvent pas l'entropie, mais un générateur
    /// cassé (compteur, horloge, graine fixe) tomberait ici immédiatement.
    /// </summary>
    [Fact]
    public void Mille_jetons_consecutifs_sont_tous_distincts()
    {
        var tokens = new HashSet<string>();

        for (var i = 0; i < 1000; i++)
        {
            tokens.Add(InvitationToken.Create().Token);
        }

        Assert.Equal(1000, tokens.Count);
    }
}
