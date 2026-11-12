using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Data;

public sealed class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<CinemaRoom> CinemaRooms => Set<CinemaRoom>();
    public DbSet<Screening> Screenings => Set<Screening>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.Property(movie => movie.Title).HasMaxLength(200).IsRequired();
            entity.Property(movie => movie.Description).HasMaxLength(2000);
            entity.Property(movie => movie.PosterUrl).HasMaxLength(500);
            entity.Property(movie => movie.Status).HasConversion<int>().HasDefaultValue(MovieStatus.ComingSoon);
            entity.HasIndex(movie => movie.Title);
        });

        modelBuilder.Entity<CinemaRoom>(entity =>
        {
            entity.Property(room => room.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(room => room.Name).IsUnique();
        });

        modelBuilder.Entity<Screening>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint("CK_Screenings_TimeRange", "[EndsAt] > [StartsAt]"));
            entity.HasIndex(screening => new { screening.CinemaRoomId, screening.StartsAt });
            entity.HasIndex(screening => new { screening.MovieId, screening.StartsAt });
            entity.HasOne(screening => screening.Movie)
                .WithMany(movie => movie.Screenings)
                .HasForeignKey(screening => screening.MovieId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(screening => screening.CinemaRoom)
                .WithMany(room => room.Screenings)
                .HasForeignKey(screening => screening.CinemaRoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
