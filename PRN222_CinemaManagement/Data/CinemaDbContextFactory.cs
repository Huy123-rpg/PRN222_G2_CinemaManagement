using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PRN222_CinemaManagement.Data;

public sealed class CinemaDbContextFactory : IDesignTimeDbContextFactory<CinemaDbContext>
{
    public CinemaDbContext CreateDbContext(string[] args)
    {
        var projectPath = Directory.GetCurrentDirectory();
        if (!File.Exists(Path.Combine(projectPath, "appsettings.json")))
            projectPath = Path.Combine(projectPath, "PRN222_CinemaManagement");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(projectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseSqlServer(configuration.GetConnectionString("CinemaDb"))
            .Options;
        return new CinemaDbContext(options);
    }
}
