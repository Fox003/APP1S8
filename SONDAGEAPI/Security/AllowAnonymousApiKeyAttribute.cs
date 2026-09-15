namespace SONDAGEAPI.Security;
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class AllowAnonymousApiKeyAttribute : Attribute;