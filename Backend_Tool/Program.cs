using ApplicationLayer.Interfaces;
using ApplicationLayer;
using Backend_Tool;
using BusinessLayer.Parsers;
using BusinessLayer.Services;
using CoreModels;
using CoreModels.Configuration;
using DataAccessLayer.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;

var backendPort = 5225;
var portAlreadyBound = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
    .Any(endpoint => endpoint.Port == backendPort &&
        (IPAddress.IsLoopback(endpoint.Address) || endpoint.Address.Equals(IPAddress.Any)));
if (portAlreadyBound)
{
    Console.WriteLine($"Backend is already running on http://127.0.0.1:{backendPort}. Exiting duplicate instance.");
    return;
}

var builder = WebApplication.CreateBuilder(args);

var applicationSettings = builder.Configuration.GetSection("Application").Get<ApplicationSettings>() ?? new ApplicationSettings();
builder.Services.Configure<ApplicationSettings>(builder.Configuration.GetSection("Application"));
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = applicationSettings.Upload.MaxFileSizeBytes);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = applicationSettings.Upload.MaxFileSizeBytes);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddSignalR();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=GenericIngestion;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
builder.Services.AddDbContext<IngestionDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<IUploadRepository, UploadRepository>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddSingleton<UploadJobQueue>();
builder.Services.AddSingleton<IUploadJobQueue>(services => services.GetRequiredService<UploadJobQueue>());
builder.Services.AddHostedService(services => services.GetRequiredService<UploadJobQueue>());
builder.Services.AddSingleton<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpClient("Gemini", client => client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/"));
builder.Services.AddScoped<IAiAnalysisService, GeminiSchemaAnalysisService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddSingleton<IPendingUploadStore, PendingUploadStore>();
builder.Services.AddScoped<IFileParser, ExcelParser>();
builder.Services.AddScoped<IFileParser, CsvParser>();
builder.Services.AddScoped<IFileParser, DelimitedTextParser>();
builder.Services.AddScoped<IFileParser, JsonParser>();
builder.Services.AddScoped<IFileParser, XmlParser>();
builder.Services.AddScoped<IFileParser, PdfParser>();
builder.Services.AddScoped<IFileParser, DocParser>();
builder.Services.AddScoped<IFileParser, ZipParser>();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins("http://localhost:4200", "https://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey)) jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("JWT_KEY or Jwt:Key must be configured outside source control before starting the backend.");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrWhiteSpace(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications")) context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
    database.Database.SetCommandTimeout(15);
    try
    {
        database.Database.Migrate();
    }
    catch (Exception exception)
    {
        // Do not prevent Kestrel from starting when LocalDB is temporarily
        // unavailable. API calls will return a diagnosable database error and
        // the next restart will retry migrations.
        app.Logger.LogError(exception, "Database startup/migration failed. Verify the configured SQL Server or LocalDB instance.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Backend Tool API v1");
    });
}

// Angular uses the local HTTP profile on port 5224 during development.
// Redirecting that profile to HTTPS causes the dev proxy to leave the configured API origin.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseSerilogRequestLogging();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.Run();
