using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;
using ShopFlow.Api.ErrorHandling;
using ShopFlow.Api.Middleware;
using ShopFlow.Api.Security;
using ShopFlow.Application;
using ShopFlow.Application.Abstractions;
using ShopFlow.Infrastructure;
using ShopFlow.Infrastructure.Persistence;
using ShopFlow.Infrastructure.Security;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging: Serilog. Levels come from the "Serilog" config section. ----
// JSON console output (one event per line) is meant for log shippers in containers;
// the human-readable template is for local development.
builder.Services.AddSerilog((services, logger) =>
{
    var config = services.GetRequiredService<IConfiguration>();
    logger
        .ReadFrom.Configuration(config)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "ShopFlow");

    if (config.GetValue("Serilog:JsonConsole", true))
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    else
        logger.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {CorrelationId}{NewLine}  {Message:lj}{NewLine}{Exception}");
});

// ---- Application layers ----
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();

// ---- Authentication: JWT bearer ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwt) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ---- Web ----
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
{
    if (ctx.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
        ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")!, name: "postgres", tags: ["ready"])
    .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), name: "redis", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnostics, http) =>
{
    diagnostics.Set("UserId", http.User.FindFirst("sub")?.Value ?? "anonymous");
});

app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // interactive API docs at /scalar/v1
}

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

await DbInitializer.InitializeAsync(app.Services);
await app.RunAsync();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
