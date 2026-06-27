using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Data.Seeding;
using PLeagueHub.Api.Middleware;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services;
using PLeagueHub.Api.Services.Football;
using PLeagueHub.Api.Services.News;

const string FrontendCorsPolicy = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<FootApiSettings>(
    builder.Configuration.GetSection("FootApi"));
builder.Services.Configure<NewsIngestionSettings>(
    builder.Configuration.GetSection("NewsIngestion"));

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<MongoIndexInitializer>();
builder.Services.AddSingleton<NewsMetadataMigration>();
builder.Services.AddSingleton<FavoriteTeamMigration>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
builder.Services.AddScoped<IForumRepository, MongoForumRepository>();
builder.Services.AddScoped<INewsRepository, MongoNewsRepository>();
builder.Services.AddScoped<IModerationRepository, MongoModerationRepository>();
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<IForumService, ForumService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddScoped<INewsIngestionService, NewsIngestionService>();
builder.Services.AddSingleton<NewsRelevanceFilter>();
builder.Services.AddHostedService<NewsIngestionWorker>();
builder.Services.AddSingleton<INewsFeedProvider, SyndicationNewsFeedProvider>();
builder.Services.AddSingleton<IPublicAddressResolver, PublicAddressResolver>();
builder.Services.AddSingleton<SafeNewsSocketConnector>();
builder.Services.AddHttpClient<INewsFeedClient, SafeNewsFeedClient>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
}).ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var connector = serviceProvider.GetRequiredService<SafeNewsSocketConnector>();
    return new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        ConnectCallback = connector.ConnectAsync
    };
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<TeamLogoSyncService>();
builder.Services.AddScoped<IMatchSyncService, MatchSyncService>();
builder.Services.AddScoped<IPlayerStatsSyncService, PlayerStatsSyncService>();
builder.Services.AddScoped<IPlayerProfileService, PlayerProfileService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<ITeamLogoCache, LocalTeamLogoCache>();
builder.Services.AddSingleton<IPlayerPhotoCache, LocalPlayerPhotoCache>();
builder.Services.AddSingleton<IProviderRequestPacer, ProviderRequestPacer>();
builder.Services.AddHttpClient<IFootballProvider, FootApiClient>((serviceProvider, client) =>
{
    var settings = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<FootApiSettings>>()
        .Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IStandingsService, StandingsService>();
builder.Services.AddScoped<IMatchDetailService, MatchDetailService>();
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

await MigrateNewsMetadataAsync(app.Services);
await MigrateFavoriteTeamsAsync(app.Services);
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
app.UseMiddleware<ActiveSuspensionMiddleware>();
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

static async Task MigrateNewsMetadataAsync(IServiceProvider serviceProvider)
{
    var migration = serviceProvider.GetRequiredService<NewsMetadataMigration>();
    await migration.MigrateAsync();
}

static async Task MigrateFavoriteTeamsAsync(IServiceProvider serviceProvider)
{
    var migration = serviceProvider.GetRequiredService<FavoriteTeamMigration>();
    await migration.MigrateAsync();
}

public partial class Program;
