using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SONDAGEAPI.Security;

// J'ai choisi un Middleware au lieu d'un Endpoint filter, donc les nouveaux endpoints seront déjà autentifiés
internal sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly byte[] _expectedKeyHash;
    
    public ApiKeyMiddleware(RequestDelegate next, 
        IOptions<ApiKeyOptions> options, 
        ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedKeyHash = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.ApiKey));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<AllowAnonymousApiKeyAttribute>() is not null)
        {
            await _next(context);
            return;
        }

        if (!TryGetPresentedKey(context, out var presentedKey) || !IsValidKey(presentedKey))
        {
            _logger.LogWarning("Clé D'API invalide. {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteUnauthorizedAsync(context);
            return;
        }
        await _next(context);
    }

    private static bool TryGetPresentedKey(HttpContext context, out string presentedKey)
    {
        presentedKey = string.Empty;
        
        // Count != 1 -> en-tête absent ou dupliqué
        if (!context.Request.Headers.TryGetValue(ApiKeyOptions.HeaderName, out var values) || values.Count != 1)
        {
            return false;
        }

        var candidate = values[0];
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }
        
        presentedKey = candidate;
        return true;
    }

    private bool IsValidKey(string presentedKey)
    {
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
        return CryptographicOperations.FixedTimeEquals(presentedHash, _expectedKeyHash);
    }
    
    private static Task WriteUnauthorizedAsync(HttpContext context) =>
        Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Non autorisé",
            detail: "Clé d'API invalide.")
        .ExecuteAsync(context);
}