using System.Text;
using System.Text.Json.Serialization;
using CyberManager.Server;
using CyberManager.Server.Data;
using CyberManager.Server.Endpoints;
using CyberManager.Server.Hubs;
using CyberManager.Server.Security;
using CyberManager.Server.Services;
using CyberManager.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// El primer inicio solicita elevación (UAC) para abrir el puerto en el Firewall.
// Las tareas programadas ya se ejecutan como SYSTEM y no muestran avisos.
var elevationArgs = args
    .Where(a => !string.Equals(a, "--elevated", StringComparison.OrdinalIgnoreCase))
    .ToArray();

// Modo lanzador único: el servidor queda en segundo plano y se abre la consola.
if (Launcher.HasConsoleFlag(args))
{
    Launcher.RunConsoleLauncher(
        Launcher.RemoveFlag(elevationArgs, Launcher.ConsoleFlag),
        Elevation.ResolvePort(elevationArgs));
    return;
}

if (Elevation.TryElevate(elevationArgs, Elevation.ResolvePort(elevationArgs)))
    return;

// Las opciones internas no forman parte de la configuración de ASP.NET Core.
var launchArgs = elevationArgs
    .Where(a => !Launcher.IsInternalFlag(a))
    .ToArray();

var builder = WebApplication.CreateBuilder(launchArgs);

// ---------- Base de datos (ruta absoluta junto al ejecutable) ----------
var configured = builder.Configuration.GetConnectionString("Default") ?? "Data Source=cybermanager.db";
var dbFile = configured.Replace("Data Source=", string.Empty).Trim();
if (!Path.IsPathRooted(dbFile))
    dbFile = Path.Combine(AppContext.BaseDirectory, dbFile);
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbFile}"));

// ---------- Servicios ----------
builder.Services.AddScoped<ExchangeRateService>();
builder.Services.AddScoped<BcvRateService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<CashService>();
builder.Services.AddScoped<StockService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<WallpaperService>();
builder.Services.AddScoped<PromoVideoService>();
builder.Services.AddScoped<RealtimeService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddSingleton<UpdateService>();
builder.Services.AddScoped<UpdateReminderService>();
builder.Services.AddSingleton(new DatabaseInfo(dbFile));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddHostedService<StationMonitorService>();
builder.Services.AddHostedService<BackupWorker>();
builder.Services.AddHostedService<BcvRateWorker>();
builder.Services.AddHostedService<UpdateReminderWorker>();

builder.Services.AddSignalR(o =>
    {
        // Las capturas de pantalla del visualizador remoto viajan en base64 y superan el límite de 32 KB.
        o.MaximumReceiveMessageSize = 4 * 1024 * 1024;
    })
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpClient("rates", c =>
{
    c.Timeout = TimeSpan.FromSeconds(8);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("CyberManager/1.0");
});

builder.Services.AddHttpClient("github", c =>
{
    c.Timeout = TimeSpan.FromSeconds(12);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("CyberManager/" + AppInfo.Version);
    c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
});

// ---------- Autenticación ----------
var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Falta Jwt:Key en appsettings.json");
if (jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 caracteres.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole(nameof(UserRole.Admin)));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var port = builder.Configuration.GetValue<int?>("Server:Port") ?? 5080;
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 512L * 1024 * 1024);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
    o.MultipartBodyLengthLimit = 512L * 1024 * 1024);

var app = builder.Build();

// ---------- Inicialización ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.EnsureCompatibilityAsync(db);
    await DbSeeder.SeedAsync(db);
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// ---------- Rutas ----------
app.MapGet("/", () => Results.Ok(new
{
    name = "CyberManager Server",
    version = AppInfo.Version,
    status = "online",
    timeUtc = DateTime.UtcNow
})).AllowAnonymous();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapCatalogEndpoints();
app.MapStationEndpoints();
app.MapStationClientEndpoints();
app.MapOperationEndpoints();
app.MapReportEndpoints();
app.MapNotificationEndpoints();
app.MapWallpaperEndpoints();
app.MapPromoVideoEndpoints();
app.MapBackupEndpoints();
app.MapUpdateEndpoints();

app.MapHub<AdminHub>("/hubs/admin");
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<StationHub>("/hubs/station");

app.Logger.LogInformation("CyberManager Server escuchando en http://0.0.0.0:{Port}", port);
app.Logger.LogInformation("Base de datos: {Db}", dbFile);

// Ctrl+C pide confirmación para no detener el servidor por accidente.
if (Environment.UserInteractive)
{
    var stopping = 0;
    Console.CancelKeyPress += (_, e) =>
    {
        // Una segunda pulsación fuerza la salida si el apagado ordenado se demora.
        if (Interlocked.Exchange(ref stopping, 1) == 1)
        {
            e.Cancel = false;
            return;
        }

        e.Cancel = true;
        var confirmed = true;

        if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            Console.WriteLine();
            Console.Write("¿Detener el servidor de CyberManager? (s/N): ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            confirmed = answer is "s" or "si" or "sí" or "y" or "yes";
        }

        if (!confirmed)
        {
            Interlocked.Exchange(ref stopping, 0);
            Console.WriteLine("El servidor sigue en ejecución.");
            return;
        }

        Console.WriteLine("Deteniendo el servidor...");
        app.Lifetime.StopApplication();
    };
}

app.Run();
