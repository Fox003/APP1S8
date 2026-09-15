using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SONDAGEAPI.Data;

using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Models;

public class ApplicationDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Survey> Surveys { get; set; }
    public DbSet<Question> Questions { get; set; }
    
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
