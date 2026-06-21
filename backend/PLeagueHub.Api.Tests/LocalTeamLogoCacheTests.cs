using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class LocalTeamLogoCacheTests
{
    [Fact]
    public async Task SaveAsync_AtomicallyStoresLogoUnderWebRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pleaguehub-logo-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var environment = new FakeWebHostEnvironment(root);
            var cache = new LocalTeamLogoCache(environment);
            var expected = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

            await cache.SaveAsync(60, new FootballTeamLogo(expected, "image/png"));

            Assert.True(cache.Exists(60));
            Assert.Equal("/team-logos/60.png", cache.GetPublicUrl(60));
            Assert.Equal(expected, await File.ReadAllBytesAsync(Path.Combine(root, "team-logos", "60.png")));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "team-logos"), "*.tmp"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_UsesWebpExtensionForWebpLogo()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pleaguehub-logo-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var cache = new LocalTeamLogoCache(new FakeWebHostEnvironment(root));
            var expected = new byte[] { 82, 73, 70, 70 };

            await cache.SaveAsync(11, new FootballTeamLogo(expected, "image/webp"));

            Assert.True(cache.Exists(11));
            Assert.Equal("/team-logos/11.webp", cache.GetPublicUrl(11));
            Assert.Equal(expected, await File.ReadAllBytesAsync(Path.Combine(root, "team-logos", "11.webp")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "PLeagueHub.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
    }
}
