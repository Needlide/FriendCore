using System.Text;
using System.Text.Json.Serialization;
using FriendCore.API.Authorization;
using FriendCore.API.Extensions;
using FriendCore.API.Realtime;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Users;
using FriendCore.Application;
using FriendCore.Infrastructure;
using FriendCore.Infrastructure.Authentication;
using FriendCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

    // Sentry configuration
    var sentryDsn = builder.Configuration["Sentry:Dsn"];
    if (!string.IsNullOrWhiteSpace(sentryDsn))
        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = sentryDsn;
            options.Environment = builder.Environment.EnvironmentName;
            options.TracesSampleRate = builder.Configuration.GetValue("Sentry:TracesSampleRate", 0.0);
            options.SendDefaultPii = false;
        });

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                     ?? throw new InvalidOperationException("JWT configuration section is missing.");

    if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
        throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters.");

    builder.Services.AddOpenApi();
    builder.Services.AddApiRateLimiting();
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddSignalR();
    // Maps connections → users so Clients.User(id) can target every device a user has open.
    builder.Services.AddSingleton<IUserIdProvider, UlidUserIdProvider>();
    // The push abstraction the application depends on; SignalR is the API-layer implementation.
    builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            // WebSocket handshakes can't carry an Authorization header from a browser, so the
            // SignalR client sends the JWT as ?access_token=... . Lift it into the auth pipeline
            // for hub paths only (everything else keeps using the header).
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.TagModeration, policy =>
            policy.RequireRole(
                nameof(UserRole.Moderator),
                nameof(UserRole.SeniorModerator),
                nameof(UserRole.Admin)));

        options.AddPolicy(AuthorizationPolicies.TagMerge, policy =>
            policy.RequireRole(nameof(UserRole.SeniorModerator), nameof(UserRole.Admin)));

        options.AddPolicy(AuthorizationPolicies.Admin, policy =>
            policy.RequireRole(nameof(UserRole.Admin)));

        options.AddPolicy(AuthorizationPolicies.Moderation, policy =>
            policy.RequireRole(
                nameof(UserRole.Moderator),
                nameof(UserRole.SeniorModerator),
                nameof(UserRole.Admin)));
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            Log.Information("Database migrations applied");
        }
        catch (Exception ex)
        {
            Log.Warning(ex,
                "Database migration skipped — ensure PostgreSQL is running (docker-compose -f docker-compose.dev.yml up -d)");
        }

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health");
    app.MapControllers().RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

    // Hubs require auth (the [Authorize] on each), reached via the access_token query string.
    app.MapHub<NotificationHub>("/hubs/notifications");
    app.MapHub<MessagingHub>("/hubs/messaging");

    app.MapGet("/", () => Results.Ok(new { name = "FriendCore API", status = "running" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the implicit Program class to WebApplicationFactory<Program> for integration tests.
public partial class Program;