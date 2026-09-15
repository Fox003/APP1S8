using System.ComponentModel.DataAnnotations;

namespace SONDAGEAPI.Security;

public class ApiKeyOptions
{
    public const string SectionName = "Sondage";
    public const string HeaderName = "X-API-Key";
    
    [Required(AllowEmptyStrings = false, ErrorMessage = "La clé Sondage:ApiKey n'est pas configurée.")]                                                                                                                           
    [MinLength(4, ErrorMessage = "La clé Sondage:ApiKey doit contenir au moins 4 caractères.")]                                                                                                                                 
    public string ApiKey { get; set; } = string.Empty;
}