using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Data.Seeding;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services;
using PLeagueHub.Api.Services.Football;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FootApiSettings>(
    builder.Configuration.GetSection("FootApi"));

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<MongoIndexInitializer>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
builder.Services.AddScoped<IForumRepository, MongoForumRepository>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<TeamLogoSyncService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ITeamLogoCache, LocalTeamLogoCache>();
builder.Services.AddSingleton<IProviderRequestPacer, ProviderRequestPacer>();
builder.Services.AddHttpClient<IFootballProvider, FootApiClient>((serviceProvider, client) =>
{
    var settings = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<FootApiSettings>>()
        .Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PLeague Hub API",
        Version = "v1",
        Description = "REST API za PLeague Hub Premier League portal."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Unesi JWT token dobijen preko /api/auth/login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [
            new OpenApiSecuritySchemeReference("Bearer", document, null)
        ] = []
    });
});

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? new JwtSettings();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

await EnsureMongoIndexesAsync(app.Services);

if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase)
    || app.Configuration.GetValue<bool>("SeedData:RunOnStartup"))
{
    await SeedDatabaseAsync(app.Services);

    if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
    {
        return;
    }
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PLeague Hub API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors(FrontendCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task SeedDatabaseAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

static async Task EnsureMongoIndexesAsync(IServiceProvider serviceProvider)
{
    var indexInitializer = serviceProvider.GetRequiredService<MongoIndexInitializer>();
    await indexInitializer.EnsureIndexesAsync();
}

public partial class Program;
