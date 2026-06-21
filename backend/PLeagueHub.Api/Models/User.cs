using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class User : BaseDocument
{
    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("uloga")]
    public string Uloga { get; set; } = "registrovani";

    [BsonElement("aktivan")]
    public bool Aktivan { get; set; } = true;

    [BsonElement("datumReg")]
    public DateTime DatumReg { get; set; } = DateTime.UtcNow;

    [BsonElement("favoritniTimovi")]
    public List<string> FavoritniTimovi { get; set; } = [];

    [BsonElement("aktivnaModeracija")]
    public ActiveModeration? AktivnaModeracija { get; set; }
}
