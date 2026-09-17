using Microsoft.AspNetCore.Authentication;

namespace SONDAGEAPI.Schemes;

// Source - https://stackoverflow.com/a/75059938
// Posted by SergVro
// Retrieved 2026-09-17, License - CC BY-SA 4.0

public class ApiKeyAuthenticationSchemeOptions: AuthenticationSchemeOptions {
    public string ApiKey {get; set;}
}
