using System.Text;
using System.Text.Json;

namespace PLeagueHub.Api.Services.News;

public static class NewsCursorCodec
{
    private sealed record CursorPayload(DateTime PublishedAt, string Id);

    public static string Encode(DateTime publishedAt, string id)
    {
        var json = JsonSerializer.Serialize(new CursorPayload(publishedAt.ToUniversalTime(), id));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static (DateTime PublishedAt, string Id) Decode(string cursor)
    {
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var payload = JsonSerializer.Deserialize<CursorPayload>(
                Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            if (payload is null || string.IsNullOrWhiteSpace(payload.Id))
                throw new FormatException("Kursor nije validan.");
            return (payload.PublishedAt.ToUniversalTime(), payload.Id);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new FormatException("Kursor nije validan.", exception);
        }
    }
}
