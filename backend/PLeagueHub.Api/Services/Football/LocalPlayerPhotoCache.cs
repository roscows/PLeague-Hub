namespace PLeagueHub.Api.Services.Football;

public sealed class LocalPlayerPhotoCache : IPlayerPhotoCache
{
    private static readonly string[] SupportedExtensions = [".png", ".webp", ".jpg"];
    private readonly string _cacheDirectory;

    public LocalPlayerPhotoCache(IWebHostEnvironment environment)
    {
        var webRootPath = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;
        _cacheDirectory = Path.Combine(webRootPath, "player-photos");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public bool Exists(int playerId)
    {
        var path = FindExistingFile(playerId);
        return path is not null && new FileInfo(path).Length > 0;
    }

    public async Task SaveAsync(
        int playerId,
        FootballTeamLogo photo,
        CancellationToken cancellationToken = default)
    {
        var extension = GetExtension(photo.ContentType);
        var destinationPath = GetFilePath(playerId, extension);
        var temporaryPath = Path.Combine(
            _cacheDirectory,
            $"{playerId}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, photo.Content, cancellationToken);
            File.Move(temporaryPath, destinationPath, overwrite: true);

            foreach (var obsoleteExtension in SupportedExtensions.Where(item => item != extension))
            {
                File.Delete(GetFilePath(playerId, obsoleteExtension));
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public string GetPublicUrl(int playerId)
    {
        var existingPath = FindExistingFile(playerId);
        var extension = existingPath is null ? ".png" : Path.GetExtension(existingPath);
        return $"/player-photos/{playerId}{extension}";
    }

    private string GetFilePath(int playerId, string extension)
    {
        if (playerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        return Path.Combine(_cacheDirectory, $"{playerId}{extension}");
    }

    private string? FindExistingFile(int playerId)
    {
        return SupportedExtensions
            .Select(extension => GetFilePath(playerId, extension))
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
                $"Unsupported player photo content type '{contentType}'.")
        };
    }
}
