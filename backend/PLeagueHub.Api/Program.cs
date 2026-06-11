using Microsoft.OpenApi;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Data.Seeding;
using PLeagueHub.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<MongoIndexInitializer>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(MongoRepository<>));
builder.Services.AddScoped<DatabaseSeeder>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PLeague Hub API",
        Version = "v1",
        Description = "REST API za PLeague Hub Premier League portal."
    });
});

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

app.UseHttpsRedirection();
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
