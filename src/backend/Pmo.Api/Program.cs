using Pmo.Api.Middleware;
using Pmo.Application.Common.Abstractions;
using Pmo.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging: Serilog reads sinks and levels from the "Serilog" section of appsettings.
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

// Errors: every error response is ProblemDetails and carries the request's correlation id.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] =
        context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] as string);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.MapControllers();
app.Run();

// Lets WebApplicationFactory<Program> in the integration tests find this entry point.
public partial class Program;
