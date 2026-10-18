using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Features.Projects;
using Pmo.Application.Features.Snapshots;
using Pmo.Infrastructure.Analytics;
using Pmo.Infrastructure.Identity;
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
        services.AddScoped<IPmoDbContext>(sp => sp.GetRequiredService<PmoDbContext>());

        // Dapper (KPI SQL): snake_case columns fill PascalCase properties; DateOnly needs a handler.
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        services.AddSingleton(NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IProjectReadModel, ProjectReadModel>();
        services.AddScoped<ISnapshotRunner, SnapshotRunner>();

        services.AddScoped<IDatabaseHealthCheck, DatabaseHealthCheck>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        return services;
    }

    /// <summary>ASP.NET Core Identity (users, roles, lockout) plus JWT bearer validation. Fails fast when the signing key is missing.</summary>
    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key is missing or shorter than 32 characters. Set it with dotnet user-secrets or the Jwt__Key environment variable.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Lockout.MaxFailedAccessAttempts = 5;                         // story A1
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.AllowedForNewUsers = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<PmoDbContext>();

        services.AddScoped<JwtTokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<AuthService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;   // keep the claim names we issue: sub, email, name, role
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Issuer,
                    ValidateAudience = true, ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = JwtTokenService.SigningKey(jwt),
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "name", RoleClaimType = "role"
                };
            });
        return services;
    }
}
