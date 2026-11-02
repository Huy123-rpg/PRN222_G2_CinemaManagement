using Microsoft.EntityFrameworkCore;
using PRN222_CinemaManagement.Models.Entities;

namespace PRN222_CinemaManagement.Data;

public sealed class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Movie>(entity =>
        {
            entity.Property(movie => movie.Title).HasMaxLength(200).IsRequired();
            entity.Property(movie => movie.Description).HasMaxLength(2000);
            entity.Property(movie => movie.PosterUrl).HasMaxLength(500);
        });
    }
}
