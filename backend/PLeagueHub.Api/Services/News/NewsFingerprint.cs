using System.Security.Cryptography;
using System.Text;

namespace PLeagueHub.Api.Services.News;

public static class NewsFingerprint
{
    private static readonly HashSet<string> TrackingParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "fbclid", "gclid", "dclid", "mc_cid", "mc_eid", "igshid", "ref_src"
    };

    public static string Create(string url, string title)
    {
        var input = $"{NormalizeUrl(url)}\n{NewsRelevanceFilter.Normalize(title)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    public static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("URL vesti nije validan.", nameof(url));

        var host = uri.IdnHost.ToLowerInvariant();
        foreach (var prefix in new[] { "www.", "m.", "amp." })
        {
            if (host.StartsWith(prefix, StringComparison.Ordinal))
            {
                host = host[prefix.Length..];
                break;
            }
        }

        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        path = path.Length > 1 ? path.TrimEnd('/') : path;
        var query = ParseQuery(uri.Query)
            .Where(pair => !pair.Key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
                && !TrackingParameters.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}");

        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var queryString = string.Join('&', query);
        return $"{uri.Scheme.ToLowerInvariant()}://{host}{port}{path}{(queryString.Length > 0 ? $"?{queryString}" : string.Empty)}";
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            var value = separator < 0 ? string.Empty : part[(separator + 1)..];
            yield return new(
                Uri.UnescapeDataString(key.Replace('+', ' ')),
                Uri.UnescapeDataString(value.Replace('+', ' ')));
        }
    }
}
