using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using FriendCore.API.Authorization;
using FriendCore.API.Extensions;
using FriendCore.API.Realtime;
using FriendCore.API.Security;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Users;
using FriendCore.Application;
using FriendCore.Infrastructure;
using FriendCore.Infrastructure.Authentication;
using FriendCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Microsoft.Extensions.FileProviders;

const string FrontendCorsPolicy = "frontend";

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

    // Trust X-Forwarded-* from the reverse proxy so the real client IP/scheme reach the app
    // (rate limiting, the moderation audit IP, and the admin IP-allowlist all depend on this).
    // KnownProxies are configured per-environment (the Nginx container's address); empty in
    // dev/test means forwarded headers are ignored and the direct connection IP is used.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        var knownProxies = builder.Configuration["ForwardedHeaders:KnownProxies"];
        if (!string.IsNullOrWhiteSpace(knownProxies))
            foreach (var entry in knownProxies.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                                         StringSplitOptions.TrimEntries))
                if (IPAddress.TryParse(entry, out var proxy))
                    options.KnownProxies.Add(proxy);
    });

    // CORS locked to the frontend origin(s) (§15.1). Falls back to the local dev frontend when
    // unconfigured in Development; production sets Cors:AllowedOrigins to the real domain.
    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
        corsOrigins = ["http://localhost:3000"];
    builder.Services.AddCors(options =>
        options.AddPolicy(FrontendCorsPolicy, policy =>
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials())); // future-proofs the planned HttpOnly refresh-token cookie

    builder.Services.Configure<AdminAccessOptions>(
        builder.Configuration.GetSection(AdminAccessOptions.SectionName));

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

    // Database bootstrapping.
    //
    // Development keeps the historical convenience of migrating on boot. Deployed environments
    // opt in explicitly (Database:MigrateOnStartup), so a production start never silently changes
    // the schema. `--migrate` and `--seed-demo` are one-off commands for `heroku run` / CI: they
    // do the work and exit without starting the web server.
    var migrateCommand = args.Contains("--migrate");
    var seedCommand = args.Contains("--seed-demo");
    var runMigrations = migrateCommand || seedCommand
                        || app.Configuration.GetValue<bool>(
                            "Database:MigrateOnStartup", app.Environment.IsDevelopment());

    if (runMigrations)
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            Log.Information("Database migrations applied");
        }
        catch (Exception ex) when (!migrateCommand && !seedCommand)
        {
            // Dev convenience only. An explicit one-off command must fail loudly instead.
            Log.Warning(ex,
                "Database migration skipped — ensure PostgreSQL is running (docker-compose -f docker-compose.dev.yml up -d)");
        }

    if (seedCommand || app.Configuration.GetValue<bool>("Seeding:DemoData"))
        await DemoDataSeeder.SeedAsync(app.Services);

    if (migrateCommand || seedCommand)
    {
        Log.Information("One-off database command finished; not starting the web server.");
        return;
    }

    // Must run first: rewrites RemoteIpAddress/scheme from X-Forwarded-* before anything reads them.
    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    // Baseline security response headers (HSTS is added by UseHsts above).
    app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        await next();
    });

    // Logs the client IP on every request. Not decoration: the rate limiter partitions on exactly
    // this value (RateLimitingExtensions.GetPartitionKey), so when beta limits look wrong — "why
    // am I getting 429s", "are these testers sharing a bucket?" — the log is the only place the
    // answer is visible. Serilog's default template omits it entirely.
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            diagnosticContext.Set("RequestIp", httpContext.Connection.RemoteIpAddress?.ToString());
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} from {RequestIp} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

    app.UseHttpsRedirection();

    // Same-origin hosting for the built SPA (opt-in via Spa:Path). When the API serves the
    // client, apiBaseUrl resolves to the page's own origin, so one build works from a LAN IP,
    // a WireGuard address or a hostname — and there is no cross-origin request at all, which
    // removes the Cors:AllowedOrigins and ForwardedHeaders:KnownProxies traps from a
    // laptop-hosted beta.
    //
    // Deliberately NOT mapped in Development: there ng serve owns the SPA, and falling back to
    // index.html would swallow 404s that should stay 404s.
    string? spaFiles = null;
    var spaPath = app.Configuration["Spa:Path"];
    if (!string.IsNullOrWhiteSpace(spaPath))
    {
        var candidate = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, spaPath));
        if (Directory.Exists(candidate))
        {
            spaFiles = candidate;
            // DefaultFiles answers "/" with index.html; StaticFiles serves the hashed bundles.
            app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = new PhysicalFileProvider(spaFiles) });
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(spaFiles) });
            Log.Information("Serving SPA from {SpaPath}", spaFiles);
        }
        else
        {
            Log.Warning("Spa:Path is set to {SpaPath} but no such directory exists under {ContentRoot}",
                spaPath, app.Environment.ContentRootPath);
        }
    }

    app.UseCors(FrontendCorsPolicy);

    // §10.3: 404 the admin surface for non-allowlisted IPs (after forwarded headers; no-op if
    // no allowlist is configured).
    app.UseMiddleware<AdminIpAllowlistMiddleware>();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Production readiness nudges — these are spec non-negotiables that depend on deployment config.
    if (app.Environment.IsProduction())
    {
        if (string.IsNullOrWhiteSpace(app.Configuration["ForwardedHeaders:KnownProxies"]))
            Log.Warning("ForwardedHeaders:KnownProxies is empty in Production — the reverse proxy IP " +
                        "will be seen as the client IP (affects rate limiting, audit IP, admin allowlist).");

        var adminAccess = app.Configuration.GetSection(AdminAccessOptions.SectionName).Get<AdminAccessOptions>();
        if (adminAccess is null || (adminAccess.AllowedIps.Length == 0 && adminAccess.AllowedNetworks.Length == 0))
            Log.Warning("AdminAccess allowlist is empty in Production — /api/v1/admin is gated by role only " +
                        "(no IP 404). Set AdminAccess:AllowedIps or :AllowedNetworks.");

        if (corsOrigins.Length == 0)
            Log.Warning("Cors:AllowedOrigins is empty in Production — no browser origin is permitted.");
    }

    app.MapHealthChecks("/health");
    // No global RequireRateLimiting here by design: every controller declares its own policy
    // ([EnableRateLimiting]), and a global require OVERRIDES those (auth's 10/min and messaging's
    // token bucket silently became the 120/min default — found because 11 rapid auth requests
    // never tripped the 10/min window). Per-controller attributes are the only mechanism.
    app.MapControllers();

    // Hubs require auth (the [Authorize] on each), reached via the access_token query string.
    app.MapHub<NotificationHub>("/hubs/notifications");
    app.MapHub<MessagingHub>("/hubs/messaging");

    if (spaFiles is null)
    {
        app.MapGet("/", () => Results.Ok(new { name = "FriendCore API", status = "running" }));
    }
    else
    {
        // SPA history fallback, written out rather than MapFallbackToFile for two reasons:
        // that overload resolves against the default webroot provider (not ours), and it would
        // swallow genuine 404s under /api and /hubs — a wrong conversation id would start
        // returning HTML, and the client would try to parse it as JSON.
        var indexHtml = Path.Combine(spaFiles, "index.html");
        app.MapFallback(async context =>
        {
            var path = context.Request.Path.Value ?? "/";
            if (!HttpMethods.IsGet(context.Request.Method)
                || path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexHtml);
        });
    }

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