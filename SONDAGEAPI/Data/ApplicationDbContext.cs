namespace SONDAGEAPI.Data;

using Microsoft.EntityFrameworkCore;
using SONDAGEAPI.Models;
using SONDAGEAPI.Security.Participants;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<SurveyResponse> Responses => Set<SurveyResponse>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Survey>(entity =>
        {
            entity.HasKey(s => s.Id);

            // Aucune limite de taille, possibilité de faille
            entity.Property(s => s.Title)
                .IsRequired()
                .HasMaxLength(Survey.TitleMaxLength);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.HasKey(i => i.Id);

            // Index Unique
            entity.HasIndex(i => i.TokenHash).IsUnique();

            entity.Property(i => i.TokenHash)
                .IsRequired()
                .HasMaxLength(InvitationToken.HashBytes);

            // Support des décomptes « combien ont répondu » sans parcourir la table.
            entity.HasIndex(i => new { i.SurveyId, i.RedeemedAt });

            entity.HasOne(i => i.Survey)
                .WithMany(s => s.Invitations)
                .HasForeignKey(i => i.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyResponse>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Content)
                .IsRequired()
                .HasMaxLength(SurveyResponse.ContentMaxLength);

            entity.HasOne(r => r.Survey)
                .WithMany(s => s.Responses)
                .HasForeignKey(r => r.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
