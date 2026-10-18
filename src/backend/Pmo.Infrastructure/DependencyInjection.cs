using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pmo.Application.Common.Abstractions;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers everything Infrastructure provides. Called once from Program.cs.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. Set it with dotnet user-secrets or ConnectionStrings__Default.");

        services.AddDbContext<PmoDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IDatabaseHealthCheck, DatabaseHealthCheck>();
        return services;
    }
}
