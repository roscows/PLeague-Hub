namespace PLeagueHub.Api.Services.Football;

public sealed class LocalTeamLogoCache : ITeamLogoCache
{
    private static readonly string[] SupportedExtensions = [".png", ".webp", ".jpg"];
    private readonly string _cacheDirectory;

    public LocalTeamLogoCache(IWebHostEnvironment environment)
    {
        var webRootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        _cacheDirectory = Path.Combine(webRootPath, "team-logos");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public bool Exists(int providerId)
    {
        var path = FindExistingFile(providerId);
        return path is not null && new FileInfo(path).Length > 0;
    }

    public async Task SaveAsync(
        int providerId,
        FootballTeamLogo logo,
        CancellationToken cancellationToken = default)
    {
        var extension = GetExtension(logo.ContentType);
        var destinationPath = GetFilePath(providerId, extension);
        var temporaryPath = Path.Combine(
            _cacheDirectory,
            $"{providerId}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, logo.Content, cancellationToken);
            File.Move(temporaryPath, destinationPath, overwrite: true);

            foreach (var obsoleteExtension in SupportedExtensions.Where(item => item != extension))
            {
                File.Delete(GetFilePath(providerId, obsoleteExtension));
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public string GetPublicUrl(int providerId)
    {
        var existingPath = FindExistingFile(providerId);
        var extension = existingPath is null ? ".png" : Path.GetExtension(existingPath);
        return $"/team-logos/{providerId}{extension}";
    }

    private string GetFilePath(int providerId, string extension)
    {
        if (providerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(providerId));
        }

        return Path.Combine(_cacheDirectory, $"{providerId}{extension}");
    }

    private string? FindExistingFile(int providerId)
    {
        return SupportedExtensions
            .Select(extension => GetFilePath(providerId, extension))
            .FirstOrDefault(File.Exists);
    }

    private static string GetExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/jpeg" => ".jpg",
            _ => throw new InvalidDataException(
                $"Unsupported team logo content type '{contentType}'.")
        };
    }
}
