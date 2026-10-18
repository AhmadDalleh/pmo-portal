using System.Threading.RateLimiting;
using Microsoft.OpenApi;
using Pmo.Api.Authorization;
using Pmo.Api.Middleware;
using Pmo.Application.Common.Abstractions;
using Pmo.Application;
using Pmo.Application.Common;
using Pmo.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging: Serilog reads sinks and levels from the "Serilog" section of appsettings.
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.Configure<ReportingOptions>(builder.Configuration.GetSection(ReportingOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);        // Identity + JWT bearer
builder.Services.AddPmoAuthorization();                 // role policies + "must be signed in" fallback
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

// Errors: every error response is ProblemDetails and carries the request's correlation id.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] =
        context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] as string);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// CORS: only the Angular origin(s) in config may call the API from a browser (cookies need an explicit origin).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("X-Correlation-Id")));

// Brute-force protection on sign-in, on top of the 5-attempt account lockout.
var loginPerMinute = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 20);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Paste the accessToken returned by POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Order matters: correlation id first so everything after it (including errors) can use it.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();          // empty 4xx responses (e.g. unknown route) become ProblemDetails
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

// Lets WebApplicationFactory<Program> in the integration tests find this entry point.
public partial class Program;
