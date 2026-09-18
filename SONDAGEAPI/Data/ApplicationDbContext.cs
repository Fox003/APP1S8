using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SONDAGEAPI.Models.Survey;

namespace SONDAGEAPI.Data;

using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Models;

[ExcludeFromCodeCoverage]
public class ApplicationDbContext : DbContext
{
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Survey> Surveys { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    public DbSet<SurveySubmission> SurveySubmissions { get; set; }
    
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
        
    }
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Forces all Guid properties to be stored/compared as standard lowercase 36-char strings in SQLite
        configurationBuilder
            .Properties<Guid>()
            .HaveConversion<GuidToStringConverter>();
    }
}
